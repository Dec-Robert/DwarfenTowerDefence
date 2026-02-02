using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BuildingEntity : MonoBehaviour
{
    [Header("Dane Bazowe")]
    public BuildingData data;

    [Header("Stan Instancji")]
    public int currentTier = 0;
    public List<BuildingUpgradeSO> appliedUpgrades = new List<BuildingUpgradeSO>();

    [Header("Ustawienia Czasu Pracy")]
    public int shiftStartHour = 6;

    // Wartoœæ bazowa z inspektora
    public int shiftLength = 8;

    // Wartoœæ rzeczywista (obliczana)
    public float currentShiftLength;

    [Header("Pracownicy")]
    [SerializeField] private List<Citizen> assignedCitizens = new List<Citizen>();

    // Bonusy z ulepszeñ lokalnych
    private int localBonusShifts = 0;
    private int localBonusWorkers = 0;
    private int currentShift = 0;

    // --- BUFORY ---
    private Dictionary<ResourceType, float> productionBuffer = new Dictionary<ResourceType, float>();
    private Dictionary<ResourceType, float> operationalBudget = new Dictionary<ResourceType, float>();

    public static List<BuildingEntity> AllBuildings = new List<BuildingEntity>();

    // --- S¥SIEDZTWO (Naprawione) ---
    private List<HexCell> adjacentResourceHexes = new List<HexCell>();

    // Cache bonusów (do wyœwietlania w UI)
    private float cachedTerrainBonus = 0f;
    private float cachedOnTopBonus = 0f;

    // --- CYKL ¯YCIA ---

    private void Start()
    {
        // 1. Podpiêcie pod czas
        if (TimeCycleManager.Instance != null)
        {
            TimeCycleManager.Instance.OnHourTick += HandleHourlyProduction;
            TimeCycleManager.Instance.OnDayChanged += HandleDayReset;
        }

        // 2. Szukanie s¹siadów (Wymaga gotowej mapy)
        if (data != null)
        {
            // Przeliczamy od razu na start
            FindAdjacentResources();
            CalculateFinalShiftLength();
        }
    }

    private void OnDestroy()
    {
        if (TimeCycleManager.Instance != null)
        {
            TimeCycleManager.Instance.OnHourTick -= HandleHourlyProduction;
            TimeCycleManager.Instance.OnDayChanged -= HandleDayReset;
        }

        // Zwolnienie zasobów w rejestrze
        foreach (var hexCell in adjacentResourceHexes)
        {
            if (hexCell != null) BuildingProductionRegistry.UnregisterUsage(hexCell);
        }
    }

    protected virtual void OnEnable()
    {
        AllBuildings.Add(this);
    }

    protected virtual void OnDisable()
    {
        AllBuildings.Remove(this);
    }

    public virtual void Initialize(BuildingData _data)
    {
        data = _data;
        currentTier = 0;

        appliedUpgrades.Clear();
        productionBuffer.Clear();
        operationalBudget.Clear();

        localBonusShifts = 0;
        localBonusWorkers = 0;

        // 1. Obliczamy d³ugoœæ zmiany
        CalculateFinalShiftLength();

        // 2. Szukamy s¹siadów
        FindAdjacentResources();

        // 3. Tankowanie startowe (20% bud¿etu)
        Dictionary<ResourceType, float> maxDaily = CalculateMaxDailyConsumption();
        foreach (var kvp in maxDaily)
        {
            operationalBudget[kvp.Key] = kvp.Value * 0.20f;
        }
    }

    // --- LOGIKA S¥SIEDZTWA I TERENU ---

    void FindAdjacentResources()
    {
        // Wyrejestrowanie starych
        foreach (var cell in adjacentResourceHexes) BuildingProductionRegistry.UnregisterUsage(cell);
        adjacentResourceHexes.Clear();
        cachedOnTopBonus = 0f;

        if (data.bonusRule.requiredFeature == HexFeatureType.None) return;

        HexCell myCell = GetComponentInParent<HexCell>();
        if (myCell == null) return;

        var mapGen = FindObjectOfType<HexMapGenerator>();
        if (mapGen == null) return;

        // 1. SPRAWDZENIE ON TOP
        if (mapGen.worldData.ContainsKey(myCell.chunkCoord) && mapGen.worldData[myCell.chunkCoord].ContainsKey(myCell.localCoord))
        {
            var myData = mapGen.worldData[myCell.chunkCoord][myCell.localCoord];
            if (myData.feature == data.bonusRule.requiredFeature)
            {
                cachedOnTopBonus = data.bonusRule.onTopProductionBonus;
            }
        }

        // 2. SPRAWDZENIE S¥SIADÓW
        List<Vector2Int> neighbors = HexGridMath.GetNeighbors(myCell.localCoord);

        foreach (var nCoord in neighbors)
        {
            if (mapGen.worldData.ContainsKey(myCell.chunkCoord) && mapGen.worldData[myCell.chunkCoord].ContainsKey(nCoord))
            {
                HexCellData cellData = mapGen.worldData[myCell.chunkCoord][nCoord];

                if (cellData.feature == data.bonusRule.requiredFeature)
                {
                    if (HexMapVisualizer.Instance != null)
                    {
                        HexCell neighborCell = HexMapVisualizer.Instance.GetHexCell(myCell.chunkCoord, nCoord);

                        if (neighborCell != null)
                        {
                            // Ignorujemy zajête pola
                            if (neighborCell.HasBuilding()) continue;

                            RegisterHex(neighborCell);
                        }
                    }
                }
            }
        }
    }

    public void ForceRescan()
    {
        FindAdjacentResources();
        if (this is TowerEntity tower) tower.RecalculateStats();
        // UI odœwie¿y siê przy nastêpnym ticku lub otwarciu
    }

    void RegisterHex(HexCell cell)
    {
        BuildingProductionRegistry.RegisterUsage(cell);
        adjacentResourceHexes.Add(cell);
    }

    float CalculateTerrainBonus()
    {
        if (data.bonusRule.requiredFeature == HexFeatureType.None) return 0f;

        float totalBonus = cachedOnTopBonus;
        var rule = data.bonusRule;

        foreach (var cell in adjacentResourceHexes)
        {
            if (cell == null) continue;
            int usersCount = BuildingProductionRegistry.GetUsageCount(cell);

            // Wzór: Base - (Penalty * (n - 1))
            float bonus = rule.baseBonusPerHex - (rule.penaltyPerUser * (usersCount - 1));
            if (bonus < rule.minBonus) bonus = rule.minBonus;

            totalBonus += bonus;
        }

        return totalBonus;
    }

    // --- LOGIKA CZASU I PRODUKCJI ---

    void CalculateFinalShiftLength()
    {
        int globalMod = (CityStatsManager.Instance != null) ? CityStatsManager.Instance.globalShiftLengthModifier : 0;
        currentShiftLength = shiftLength + globalMod;
        if (currentShiftLength < 1) currentShiftLength = 1;
    }

    protected virtual void HandleHourlyProduction(int currentHour)
    {
        int shiftEndHour = shiftStartHour + (int)currentShiftLength;
        bool isWorkingHours = (currentHour >= shiftStartHour && currentHour < shiftEndHour);
        if (!isWorkingHours) return;

        Dictionary<ResourceType, float> loggedProduction = new Dictionary<ResourceType, float>();

        List<Citizen> activeWorkers = assignedCitizens.FindAll(c => c.workState == WorkState.Assigned || c.workState == WorkState.Working);
        int workerCount = activeWorkers.Count;
        if (workerCount == 0) return;

        foreach (var w in activeWorkers)
        {
            if (w.workState == WorkState.Assigned)
            {
                w.workState = WorkState.Working;
                if (UIBuildingInspector.Instance != null) UIBuildingInspector.Instance.RefreshContent();
            }
        }

        // Wydajnoœæ
        float efficiency = 1.0f;
        float scale = data.workerScalingFactor > 0 ? data.workerScalingFactor : 0.1f;
        if (workerCount > 1) efficiency += (workerCount - 1) * scale;

        // Bonusy
        cachedTerrainBonus = CalculateTerrainBonus(); // Aktualizujemy bonus co godzinê
        float beaconBonus = (BeaconEntity.Instance != null) ? BeaconEntity.Instance.GetGlobalProductionMultiplier() : 1f;

        // Paliwo
        Dictionary<ResourceType, float> baseUpkeepPerShift = GetCurrentUpkeep();
        bool hasFuel = true;

        foreach (var cost in baseUpkeepPerShift)
        {
            float neededNow = (cost.Value / currentShiftLength) * efficiency;
            if (!operationalBudget.ContainsKey(cost.Key) || operationalBudget[cost.Key] < neededNow)
            {
                hasFuel = false;
                break;
            }
        }

        if (hasFuel)
        {

            Dictionary<ResourceType, float> baseProdPerShift = GetCurrentProduction();

            foreach (var cost in baseUpkeepPerShift)
            {
                float neededNow = (cost.Value / currentShiftLength) * efficiency;
                operationalBudget[cost.Key] -= neededNow;
            }

            Dictionary<ResourceType, float> totalProd = GetCurrentProduction(); // Zawiera bonus terenowy!

            foreach (var kvp in totalProd)
            {
                float hourlyAmount = (kvp.Value / currentShiftLength) * efficiency * beaconBonus;

                if (!productionBuffer.ContainsKey(kvp.Key)) productionBuffer[kvp.Key] = 0f;
                productionBuffer[kvp.Key] += hourlyAmount;

                if (productionBuffer[kvp.Key] >= 1.0f)
                {
                    int amountToGive = Mathf.FloorToInt(productionBuffer[kvp.Key]);
                    productionBuffer[kvp.Key] -= amountToGive;
                    ResourceManager.Instance.AddResource(kvp.Key, amountToGive);

                    if (amountToGive > 0)
                    {
                        if (loggedProduction.ContainsKey(kvp.Key)) loggedProduction[kvp.Key] += amountToGive;
                        else loggedProduction.Add(kvp.Key, amountToGive);
                    }

                    if (FloatingTextManager.Instance != null)
                        FloatingTextManager.Instance.ShowGain(transform.position, kvp.Key.ToString(), amountToGive);
                }
            }
            if (loggedProduction.Count > 0 && ResourceLogger.Instance != null)
            {
                ResourceLogger.Instance.LogTransaction($"Produkcja: {data.buildingName}", loggedProduction);
            }
        }
        else
        {
            // Debug.Log($"[Building] {name} brak paliwa.");
        }

        if (currentHour == shiftEndHour - 1)
        {
            foreach (var w in activeWorkers) w.workState = WorkState.Exhausted;
            if (UIBuildingInspector.Instance != null) UIBuildingInspector.Instance.RefreshContent();
        }
    }

    private void HandleDayReset(int day)
    {
        foreach (var worker in assignedCitizens)
        {
            if (worker.workState == WorkState.Exhausted || worker.workState == WorkState.Working)
                worker.workState = WorkState.Assigned;
        }

        Dictionary<ResourceType, float> maxDaily = CalculateMaxDailyConsumption();
        Dictionary<ResourceType, float> resourcesToRefill = new Dictionary<ResourceType, float>();

        foreach (var kvp in maxDaily)
        {
            float current = operationalBudget.ContainsKey(kvp.Key) ? operationalBudget[kvp.Key] : 0;
            float needed = kvp.Value - current;
            if (needed > 0) resourcesToRefill.Add(kvp.Key, needed);
        }

        foreach (var req in resourcesToRefill)
        {
            if (ResourceManager.Instance.SpendResource(req.Key, req.Value))
            {
                if (!operationalBudget.ContainsKey(req.Key)) operationalBudget[req.Key] = 0;
                operationalBudget[req.Key] += req.Value;
            }
            else
            {
                float available = ResourceManager.Instance.GetResourceAmount(req.Key);
                if (available > 0)
                {
                    ResourceManager.Instance.SpendResource(req.Key, available);
                    if (!operationalBudget.ContainsKey(req.Key)) operationalBudget[req.Key] = 0;
                    operationalBudget[req.Key] += available;
                }
            }
        }

        if (UIBuildingInspector.Instance != null) UIBuildingInspector.Instance.RefreshContent();
    }

    // --- GETTERY I API ---

    public Dictionary<ResourceType, float> GetCurrentProduction()
    {
        Dictionary<ResourceType, float> total = new Dictionary<ResourceType, float>();

        // 1. Baza
        if (data.productionPerCycle != null)
            foreach (var res in data.productionPerCycle) AddToDict(total, res.type, res.amount);

        // 2. Ulepszenia
        foreach (var up in appliedUpgrades)
            if (up.productionBonus != null)
                foreach (var res in up.productionBonus) AddToDict(total, res.type, res.amount);

        // 3. Bonus Terenowy (Obliczany na ¿ywo, aby UI widzia³o zmiany)
        float currentLiveBonus = CalculateTerrainBonus();

        if (currentLiveBonus > 0 && data.productionPerCycle != null && data.productionPerCycle.Count > 0)
        {
            ResourceType mainRes = data.productionPerCycle[0].type;
            if (total.ContainsKey(mainRes)) total[mainRes] += currentLiveBonus;
            else total.Add(mainRes, currentLiveBonus);
        }

        return total;
    }

    public Dictionary<ResourceType, float> CalculateMaxDailyConsumption()
    {
        Dictionary<ResourceType, float> maxDaily = new Dictionary<ResourceType, float>();
        Dictionary<ResourceType, float> baseUpkeep = GetCurrentUpkeep();

        float scale = data.workerScalingFactor > 0 ? data.workerScalingFactor : 0.1f;
        float maxShiftScale = 1.0f + ((getMaxWorkersPerShift() - 1) * scale);

        foreach (var kvp in baseUpkeep)
        {
            float total = (kvp.Value * maxShiftScale) * getMaxShifts();
            maxDaily.Add(kvp.Key, total);
        }
        return maxDaily;
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

    // --- ZARZ¥DZANIE PRACOWNIKAMI ---

    public virtual bool TryAddWorker(Race race)
    {
        if (assignedCitizens.Count >= (getMaxShifts() * getMaxWorkersPerShift())) return false;
        Citizen newWorker = CitizenManager.Instance.FindAndAssignCitizen(race, this);
        if (newWorker != null) { assignedCitizens.Add(newWorker); if (UIBuildingInspector.Instance != null) UIBuildingInspector.Instance.RefreshContent(); return true; }
        return false;
    }

    public virtual void RemoveWorker(Race race)
    {
        Citizen workerToRemove = assignedCitizens.Find(c => c.race == race && c.workState != WorkState.Working);
        if (workerToRemove != null) { workerToRemove.RemoveFromWorkplace(); assignedCitizens.Remove(workerToRemove); if (UIBuildingInspector.Instance != null) UIBuildingInspector.Instance.RefreshContent(); }
    }

    public List<Citizen> GetAssignedCitizens() => assignedCitizens;
    public int GetWorkerCount(Race r) => assignedCitizens.FindAll(c => c.race == r).Count;
    public int getMaxShifts() { int global = (CityStatsManager.Instance != null) ? CityStatsManager.Instance.globalBonusShifts : 0; return (data != null ? data.baseShifts : 1) + localBonusShifts + global; }
    public int getMaxWorkersPerShift() { int global = (CityStatsManager.Instance != null) ? CityStatsManager.Instance.globalBonusWorkersPerShift : 0; return (data != null ? data.baseWorkersPerShift : 1) + localBonusWorkers + global; }
    public Dictionary<ResourceType, float> GetCurrentBudget() => operationalBudget;

    public List<BuildingUpgradeSO> GetAvailableUpgrades() { if (currentTier == 0 && data.tier1Upgrades != null) return data.tier1Upgrades; if (appliedUpgrades.Count > 0) { var last = appliedUpgrades[appliedUpgrades.Count - 1]; if (last.nextTierOptions != null) return last.nextTierOptions; } return new List<BuildingUpgradeSO>(); }
    public void ApplyUpgrade(BuildingUpgradeSO upgrade) { appliedUpgrades.Add(upgrade); currentTier++; localBonusShifts += upgrade.extraShifts; localBonusWorkers += upgrade.extraWorkersPerShift; CalculateFinalShiftLength(); if (UIBuildingInspector.Instance != null) UIBuildingInspector.Instance.RefreshContent(); }
    public void Demolish() { foreach (var c in new List<Citizen>(assignedCitizens)) c.RemoveFromWorkplace(); Destroy(gameObject); }
}