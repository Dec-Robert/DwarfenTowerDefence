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
    [SerializeField] private int maxShifts = 1;
    [SerializeField] private int maxWorkersPerShift = 2;
    [SerializeField] List<Citizen> assignedCitizens = new List<Citizen>();

    private int currentShift = 0;

    // BUFOR PRODUKCJI: Przechowuje u³amki surowców (np. 0.33 drewna)
    // Dopiero gdy uzbiera siê >= 1.0, wysy³amy do ResourceManagera (bo on przyjmuje inty)
    private Dictionary<ResourceType, float> productionBuffer = new Dictionary<ResourceType, float>();

    // --- CYKL ¯YCIA ---

    private void Start()
    {
        // Podpinamy siê pod zegar globalny
        if (TimeCycleManager.Instance != null)
        {
            TimeCycleManager.Instance.OnHourTick += HandleHourlyProduction;
            TimeCycleManager.Instance.OnDayChanged += HandleDayReset;
        }
    }

    private void OnDestroy()
    {
        // Sprz¹tanie subskrypcji
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
        appliedUpgrades.Clear();
        productionBuffer.Clear();
    }

    // --- LOGIKA PRODUKCJI I CZASU (NOWOŒÆ) ---

    private void HandleHourlyProduction(int currentHour)
    {
        // 1. SprawdŸ czy trwa zmiana
        int shiftEndHour = shiftStartHour + shiftLength;
        bool isWorkingHours = (currentHour >= shiftStartHour && currentHour < shiftEndHour);

        if (!isWorkingHours) return;

        // 2. ZnajdŸ aktywnych pracowników (Ci co czekaj¹ na pracê LUB ju¿ pracuj¹)
        // Ignorujemy Exhausted
        List<Citizen> activeWorkers = assignedCitizens.FindAll(c =>
            c.workState == WorkState.Assigned || c.workState == WorkState.Working);

        int workerCount = activeWorkers.Count;
        if (workerCount == 0) return; // Brak r¹k do pracy

        // 3. Logika blokowania pracowników (Zgodnie z ¿yczeniem: po godzinie pracy)
        // Wykonujemy to PRZED naliczeniem surowców, czy PO? 
        // Skoro "godzina mija", to znaczy ¿e przepracowali tê godzinê.
        foreach (var worker in activeWorkers)
        {
            if (worker.workState == WorkState.Assigned)
            {
                // By³ przypisany, minê³a godzina -> teraz jest zablokowany w pracy
                worker.workState = WorkState.Working;

                // Odœwie¿amy UI, ¿eby pokazaæ zmianê koloru na czarny
                if (BuildingCitizenUI.Instance != null) BuildingCitizenUI.Instance.Refresh(this);
            }
        }

        // 4. Oblicz wydajnoœæ
        // 1 os = 100%, ka¿da kolejna +10%
        float efficiency = 1.0f;
        if (workerCount > 1)
        {
            efficiency += (workerCount - 1) * 0.10f;
        }

        // 5. Produkcja
        Dictionary<ResourceType, int> totalDailyProduction = GetCurrentProduction();

        foreach (var kvp in totalDailyProduction)
        {
            ResourceType type = kvp.Key;
            int dailyAmount = kvp.Value;

            // Ile wyprodukowano w tê jedn¹ godzinê
            float hourlyAmount = ((float)dailyAmount / shiftLength) * efficiency;

            // Dodajemy do bufora
            if (!productionBuffer.ContainsKey(type)) productionBuffer[type] = 0f;
            productionBuffer[type] += hourlyAmount;

            // Sprawdzamy czy uzbiera³a siê pe³na jednostka (bo ResourceManager przyjmuje int)
            // Jeœli ResourceManager obs³u¿y floaty w przysz³oœci, ten warunek mo¿na usun¹æ
            if (productionBuffer[type] >= 1.0f)
            {
                int amountToGive = Mathf.FloorToInt(productionBuffer[type]);
                productionBuffer[type] -= amountToGive; // Zostawiamy resztê (np. 0.032) w buforze

                ResourceManager.Instance.AddResource(type, amountToGive);
                // Debug.Log($"[Building] {name} wyprodukowa³ {amountToGive} {type}");
            }
        }

        // 6. Koniec zmiany - zmêczenie
        // Jeœli ta godzina by³a ostatni¹ godzin¹ zmiany
        if (currentHour == shiftEndHour - 1)
        {
            foreach (var worker in activeWorkers)
            {
                worker.workState = WorkState.Exhausted;
            }
            if (BuildingCitizenUI.Instance != null) BuildingCitizenUI.Instance.Refresh(this);
        }
    }

    private void HandleDayReset(int day)
    {
        // Nowy dzieñ - resetujemy zmêczonych pracowników, ¿eby byli gotowi na rano
        foreach (var worker in assignedCitizens)
        {
            if (worker.workState == WorkState.Exhausted || worker.workState == WorkState.Working)
            {
                worker.workState = WorkState.Assigned;
            }
        }
        if (BuildingCitizenUI.Instance != null) BuildingCitizenUI.Instance.Refresh(this);
    }

    // --- ZARZ¥DZANIE PRACOWNIKAMI ---

    public bool TryAddWorker(Race race)
    {
        if (assignedCitizens.Count >= (maxShifts * maxWorkersPerShift))
        {
            Debug.Log("Brak wolnych miejsc pracy w tym budynku!");
            return false;
        }

        Citizen newWorker = CitizenManager.Instance.FindAndAssignCitizen(race, this);

        if (newWorker != null)
        {
            assignedCitizens.Add(newWorker);
            // newWorker.AssignToWorkplace(this); <- To robi manager, ale dla pewnoœci nie szkodzi

            BuildingCitizenUI.Instance.Refresh(this);
            return true;
        }

        // Refresh na wypadek gdyby coœ siê nie odœwie¿y³o
        BuildingCitizenUI.Instance.Refresh(this);
        return false;
    }

    public void RemoveWorker(Race race)
    {
        // ZMODYFIKOWANE: Szukamy pracownika, który NIE jest zablokowany (Working).
        // Assigned (zielony) -> mo¿na usun¹æ.
        // Exhausted (czerwony) -> mo¿na usun¹æ (bo ju¿ po pracy).
        // Working (czarny) -> NIE mo¿na usun¹æ (jest w trakcie zmiany).

        Citizen workerToRemove = assignedCitizens.Find(c => c.race == race && c.workState != WorkState.Working);

        if (workerToRemove != null)
        {
            workerToRemove.RemoveFromWorkplace();
            assignedCitizens.Remove(workerToRemove);
            Debug.Log($"Zwolniono pracownika rasy {race}");

            BuildingCitizenUI.Instance.Refresh(this);
        }
        else
        {
            // SprawdŸmy czy powodem jest brak ludzi czy to, ¿e wszyscy pracuj¹
            bool anyoneWorking = assignedCitizens.Exists(c => c.race == race && c.workState == WorkState.Working);
            if (anyoneWorking)
                Debug.Log($"Nie mo¿na zwolniæ pracownika {race} - jest w trakcie pracy (Working)!");
            else
                Debug.Log($"Brak pracowników rasy {race} w tym budynku.");
        }
    }

    // --- GETTERS & HELPERS ---

    public List<Citizen> GetAssignedCitizens()
    {
        return assignedCitizens;
    }

    public int GetWorkerCount(Race r)
    {
        int count = 0;
        foreach (var citizen in assignedCitizens)
        {
            if (citizen.race == r) count++;
        }
        return count;
    }

    public void NextShift()
    {
        currentShift = (currentShift + 1) % maxShifts;
        Debug.Log($"Prze³¹czono na zmianê {currentShift + 1}");
    }

    public int getMaxShifts() { return maxShifts; }
    public int getMaxWorkersPerShift() { return maxWorkersPerShift; }

    // --- LOGIKA ULEPSZEÑ (Bez zmian) ---

    public List<BuildingUpgradeSO> GetAvailableUpgrades()
    {
        if (currentTier == 0)
        {
            if (data.tier1Upgrades != null) return data.tier1Upgrades;
        }
        if (appliedUpgrades.Count > 0)
        {
            BuildingUpgradeSO lastUpgrade = appliedUpgrades[appliedUpgrades.Count - 1];
            if (lastUpgrade.nextTierOptions != null) return lastUpgrade.nextTierOptions;
        }
        return new List<BuildingUpgradeSO>();
    }

    public void ApplyUpgrade(BuildingUpgradeSO upgrade)
    {
        appliedUpgrades.Add(upgrade);
        currentTier++;
        Debug.Log($"Budynek {name} ulepszony do: {upgrade.upgradeName}");
        // Tutaj logika modeli/efektów
    }

    public Dictionary<ResourceType, int> GetCurrentProduction()
    {
        Dictionary<ResourceType, int> total = new Dictionary<ResourceType, int>();
        if (data.productionPerCycle != null)
        {
            foreach (var res in data.productionPerCycle) AddToDict(total, res.type, res.amount);
        }
        foreach (var up in appliedUpgrades)
        {
            if (up.productionBonus != null)
            {
                foreach (var res in up.productionBonus) AddToDict(total, res.type, res.amount);
            }
        }
        return total;
    }

    public Dictionary<ResourceType, int> GetCurrentUpkeep()
    {
        Dictionary<ResourceType, int> total = new Dictionary<ResourceType, int>();
        if (data.upkeepPerCycle != null)
        {
            foreach (var res in data.upkeepPerCycle) AddToDict(total, res.type, res.amount);
        }
        foreach (var up in appliedUpgrades)
        {
            if (up.upkeepIncrease != null)
            {
                foreach (var res in up.upkeepIncrease) AddToDict(total, res.type, res.amount);
            }
        }
        return total;
    }

    void AddToDict(Dictionary<ResourceType, int> dict, ResourceType type, int amount)
    {
        if (dict.ContainsKey(type)) dict[type] += amount;
        else dict.Add(type, amount);
    }

    public void Demolish()
    {
        // Opcjonalnie: Zwolnij wszystkich pracowników przed zniszczeniem
        foreach (var c in new List<Citizen>(assignedCitizens)) // kopia listy
        {
            c.RemoveFromWorkplace();
        }
        Debug.Log($"Budynek {data.buildingName} zosta³ zburzony.");
        Destroy(gameObject);
    }
}