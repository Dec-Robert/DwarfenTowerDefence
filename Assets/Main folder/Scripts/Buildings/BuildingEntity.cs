using UnityEngine;
using System.Collections.Generic;

public class BuildingEntity : MonoBehaviour
{
    [Header("Dane Bazowe")]
    public BuildingData data;

    [Header("Stan Instancji")]
    public int currentTier = 0;
    public List<BuildingUpgradeSO> appliedUpgrades = new List<BuildingUpgradeSO>();

    [Header("Ustawienia Czasu Pracy")]
    public int shiftStartHour = 6;  // Start zmiany (np. 6:00)
    public int shiftLength = 10;    // D³ugoœæ zmiany w godzinach

    [Header("Pracownicy")]
    [SerializeField] List<Citizen> assignedCitizens = new List<Citizen>();

    // --- NOWE ZMIENNE: Bonusy z ulepszeñ (zamiast sztywnych limitów) ---
    private int localBonusShifts = 0;
    private int localBonusWorkers = 0;
    private int currentShift = 0;

    // --- BUFORY ---
    // 1. Bufor Produkcji (u³amki przed wys³aniem do magazynu)
    private Dictionary<ResourceType, float> productionBuffer = new Dictionary<ResourceType, float>();

    // 2. Bud¿et Operacyjny (paliwo pobrane rano)
    private Dictionary<ResourceType, float> operationalBudget = new Dictionary<ResourceType, float>();

    // --- CYKL ¯YCIA ---

    private void Start()
    {
        if (TimeCycleManager.Instance != null)
        {
            TimeCycleManager.Instance.OnHourTick += HandleHourlyProduction;
            TimeCycleManager.Instance.OnDayChanged += HandleDayReset;
        }
    }

    private void OnDestroy()
    {
        if (TimeCycleManager.Instance != null)
        {
            TimeCycleManager.Instance.OnHourTick -= HandleHourlyProduction;
            TimeCycleManager.Instance.OnDayChanged -= HandleDayReset;
        }
    }

    public virtual void Initialize(BuildingData _data)
    {
        data = _data;
        currentTier = 0;

        // Reset stanów
        appliedUpgrades.Clear();
        productionBuffer.Clear();
        operationalBudget.Clear();
        localBonusShifts = 0;
        localBonusWorkers = 0;

        // --- Automatyczne nape³nienie 20% baku na start ---
        Dictionary<ResourceType, float> maxDaily = CalculateMaxDailyConsumption();
        foreach (var kvp in maxDaily)
        {
            operationalBudget[kvp.Key] = kvp.Value * 0.20f;
        }
    }

    // ========================================================================
    //                        LOGIKA DZIENNA (TANKOWANIE)
    // ========================================================================

    private void HandleDayReset(int day)
    {
        // 1. Reset pracowników (Nowy dzieñ = wypoczêci)
        foreach (var worker in assignedCitizens)
        {
            if (worker.workState == WorkState.Exhausted || worker.workState == WorkState.Working)
            {
                worker.workState = WorkState.Assigned;
            }
        }

        // 2. UZUPE£NIANIE BUD¯ETU OPERACYJNEGO
        Dictionary<ResourceType, float> maxDailyConsumption = CalculateMaxDailyConsumption();
        Dictionary<ResourceType, float> resourcesToRefill = new Dictionary<ResourceType, float>();

        foreach (var kvp in maxDailyConsumption)
        {
            ResourceType type = kvp.Key;
            float maxCap = kvp.Value;

            float currentAmount = operationalBudget.ContainsKey(type) ? operationalBudget[type] : 0;
            float needed = maxCap - currentAmount;

            if (needed > 0)
            {
                resourcesToRefill.Add(type, needed);
            }
        }

        // Pobieramy surowce z magazynu
        foreach (var req in resourcesToRefill)
        {
            if (ResourceManager.Instance.SpendResource(req.Key, req.Value))
            {
                if (!operationalBudget.ContainsKey(req.Key)) operationalBudget[req.Key] = 0;
                operationalBudget[req.Key] += req.Value;
            }
            else
            {
                // Jeœli nie staæ na full, bierzemy resztê
                float available = ResourceManager.Instance.GetResourceAmount(req.Key);
                if (available > 0)
                {
                    ResourceManager.Instance.SpendResource(req.Key, available);
                    if (!operationalBudget.ContainsKey(req.Key)) operationalBudget[req.Key] = 0;
                    operationalBudget[req.Key] += available;
                }
            }
        }

        if (BuildingCitizenUI.Instance != null) BuildingCitizenUI.Instance.Refresh(this);
        if (UIBuildingInspector.Instance != null) UIBuildingInspector.Instance.RefreshContent();
    }

    // ========================================================================
    //                        LOGIKA GODZINOWA (PRODUKCJA)
    // ========================================================================

    protected virtual void HandleHourlyProduction(int currentHour)
    {
        if (this is HousingEntity) return;

        int shiftEndHour = shiftStartHour + shiftLength;
        bool isWorkingHours = (currentHour >= shiftStartHour && currentHour < shiftEndHour);
        if (!isWorkingHours) return;

        // 1. Kto pracuje?
        List<Citizen> activeWorkers = assignedCitizens.FindAll(c =>
            c.workState == WorkState.Assigned || c.workState == WorkState.Working);
        int workerCount = activeWorkers.Count;

        if (workerCount == 0) return;

        // Blokowanie pracowników (Godzina minê³a, s¹ w pracy)
        foreach (var w in activeWorkers)
        {
            if (w.workState == WorkState.Assigned)
            {
                w.workState = WorkState.Working;
                if (BuildingCitizenUI.Instance != null) BuildingCitizenUI.Instance.Refresh(this);
            }
        }

        // 2. Skalowanie kosztów/produkcji
        float scale = data.workerScalingFactor > 0 ? data.workerScalingFactor : 0.1f;
        float hourlyScale = 1.0f;
        if (workerCount > 1)
            hourlyScale += (workerCount - 1) * scale;

        // Bonus z Beacona
        float beaconBonus = (BeaconEntity.Instance != null) ? BeaconEntity.Instance.GetGlobalProductionMultiplier() : 1f;

        // 3. Sprawdzanie paliwa
        Dictionary<ResourceType, float> baseUpkeepPerShift = GetCurrentUpkeep();
        bool hasFuel = true;

        foreach (var cost in baseUpkeepPerShift)
        {
            float neededNow = (cost.Value / shiftLength) * hourlyScale;
            if (!operationalBudget.ContainsKey(cost.Key) || operationalBudget[cost.Key] < neededNow)
            {
                hasFuel = false;
                break;
            }
        }

        if (hasFuel)
        {
            // A. Spalanie paliwa
            foreach (var cost in baseUpkeepPerShift)
            {
                float neededNow = (cost.Value / shiftLength) * hourlyScale;
                operationalBudget[cost.Key] -= neededNow;
            }

            // B. Produkcja
            Dictionary<ResourceType, float> totalDailyProduction = GetCurrentProduction();
            foreach (var kvp in totalDailyProduction)
            {
                ResourceType type = kvp.Key;
                float dailyAmount = kvp.Value;

                float hourlyAmount = ((float)dailyAmount / shiftLength) * hourlyScale * beaconBonus;

                if (!productionBuffer.ContainsKey(type)) productionBuffer[type] = 0f;
                productionBuffer[type] += hourlyAmount;

                if (productionBuffer[type] >= 1.0f)
                {
                    int amountToGive = Mathf.FloorToInt(productionBuffer[type]);
                    productionBuffer[type] -= amountToGive;
                    ResourceManager.Instance.AddResource(type, amountToGive);

                    if (FloatingTextManager.Instance != null)
                        FloatingTextManager.Instance.ShowGain(transform.position, type.ToString(), amountToGive);
                }
            }
        }
        else
        {
            Debug.Log($"[Building] {name} wstrzyma³ pracê (Brak paliwa).");
        }

        // 4. Koniec zmiany
        if (currentHour == shiftEndHour - 1)
        {
            foreach (var w in activeWorkers) w.workState = WorkState.Exhausted;
            if (BuildingCitizenUI.Instance != null) BuildingCitizenUI.Instance.Refresh(this);
        }
    }

    // ========================================================================
    //                              MATEMATYKA & STATYSTYKI
    // ========================================================================

    // Oblicza ile budynek potrzebuje na dzieñ przy MAX ob³o¿eniu (Baza + Lokalne + Globalne)
    public Dictionary<ResourceType, float> CalculateMaxDailyConsumption()
    {
        Dictionary<ResourceType, float> maxDaily = new Dictionary<ResourceType, float>();
        Dictionary<ResourceType, float> baseUpkeep = GetCurrentUpkeep();

        // U¿ywamy getterów uwzglêdniaj¹cych bonusy globalne/lokalne
        int currentMaxWorkers = getMaxWorkersPerShift();
        int currentMaxShifts = getMaxShifts();

        float scale = data.workerScalingFactor > 0 ? data.workerScalingFactor : 0.1f;
        float maxShiftScale = 1.0f + ((currentMaxWorkers - 1) * scale);

        foreach (var kvp in baseUpkeep)
        {
            float total = (kvp.Value * maxShiftScale) * currentMaxShifts;
            maxDaily.Add(kvp.Key, total);
        }
        return maxDaily;
    }

    // --- Gettery dynamiczne (Base + Local + Global) ---

    public int getMaxShifts()
    {
        int global = (CityStatsManager.Instance != null) ? CityStatsManager.Instance.globalBonusShifts : 0;
        // Zabezpieczenie przed nullem danych
        int baseVal = (data != null) ? data.baseShifts : 1;
        return baseVal + localBonusShifts + global;
    }

    public int getMaxWorkersPerShift()
    {
        int global = (CityStatsManager.Instance != null) ? CityStatsManager.Instance.globalBonusWorkersPerShift : 0;
        int baseVal = (data != null) ? data.baseWorkersPerShift : 1;
        return baseVal + localBonusWorkers + global;
    }

    // --- Produkcja i Utrzymanie ---

    public Dictionary<ResourceType, float> GetCurrentProduction()
    {
        Dictionary<ResourceType, float> total = new Dictionary<ResourceType, float>();
        if (data.productionPerCycle != null)
            foreach (var res in data.productionPerCycle) AddToDict(total, res.type, res.amount);

        foreach (var up in appliedUpgrades)
            if (up.productionBonus != null)
                foreach (var res in up.productionBonus) AddToDict(total, res.type, res.amount);

        return total;
    }

    public Dictionary<ResourceType, float> GetCurrentUpkeep()
    {
        Dictionary<ResourceType, float> total = new Dictionary<ResourceType, float>();
        if (data.upkeepPerCycle != null)
            foreach (var res in data.upkeepPerCycle) AddToDict(total, res.type, res.amount);

        foreach (var up in appliedUpgrades)
            if (up.upkeepIncrease != null)
                foreach (var res in up.upkeepIncrease) AddToDict(total, res.type, res.amount);

        return total;
    }

    void AddToDict(Dictionary<ResourceType, float> dict, ResourceType type, float amount)
    {
        if (dict.ContainsKey(type)) dict[type] += amount;
        else dict.Add(type, amount);
    }

    // ========================================================================
    //                        ZARZ¥DZANIE PRACOWNIKAMI
    // ========================================================================

    public virtual bool TryAddWorker(Race race)
    {
        // Sprawdzamy limit u¿ywaj¹c dynamicznych getterów
        if (assignedCitizens.Count >= (getMaxShifts() * getMaxWorkersPerShift())) return false;

        Citizen newWorker = CitizenManager.Instance.FindAndAssignCitizen(race, this);
        if (newWorker != null)
        {
            assignedCitizens.Add(newWorker);

            if (UIBuildingInspector.Instance != null)
                UIBuildingInspector.Instance.RefreshContent();

            return true;
        }
        return false;
    }

    public virtual void RemoveWorker(Race race)
    {
        // Usuwamy tylko niezablokowanych (Working = zablokowany)
        Citizen workerToRemove = assignedCitizens.Find(c => c.race == race && c.workState != WorkState.Working);
        if (workerToRemove != null)
        {
            workerToRemove.RemoveFromWorkplace();
            assignedCitizens.Remove(workerToRemove);

            if (UIBuildingInspector.Instance != null)
                UIBuildingInspector.Instance.RefreshContent();
        }
    }

    // ========================================================================
    //                        SYSTEM ULEPSZEÑ
    // ========================================================================

    public List<BuildingUpgradeSO> GetAvailableUpgrades()
    {
        if (currentTier == 0 && data.tier1Upgrades != null) return data.tier1Upgrades;
        if (appliedUpgrades.Count > 0)
        {
            var last = appliedUpgrades[appliedUpgrades.Count - 1];
            if (last.nextTierOptions != null) return last.nextTierOptions;
        }
        return new List<BuildingUpgradeSO>();
    }

    public void ApplyUpgrade(BuildingUpgradeSO upgrade)
    {
        appliedUpgrades.Add(upgrade);
        currentTier++;

        // Dodajemy bonusy do slotów (lokalne)
        localBonusShifts += upgrade.extraShifts;
        localBonusWorkers += upgrade.extraWorkersPerShift;

        // Odœwie¿enie UI po ulepszeniu
        if (UIBuildingInspector.Instance != null) UIBuildingInspector.Instance.RefreshContent();
    }

    // ========================================================================
    //                            INNE HELPERY
    // ========================================================================

    public List<Citizen> GetAssignedCitizens() => assignedCitizens;

    public int GetWorkerCount(Race r) => assignedCitizens.FindAll(c => c.race == r).Count;

    public void NextShift() { currentShift = (currentShift + 1) % getMaxShifts(); }

    public Dictionary<ResourceType, float> GetCurrentBudget() => operationalBudget;

    public void Demolish()
    {
        foreach (var c in new List<Citizen>(assignedCitizens)) c.RemoveFromWorkplace();
        Destroy(gameObject);
    }
}