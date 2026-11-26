using System;
using System.Collections.Generic;
using UnityEngine;

public class BuildingEntity : MonoBehaviour
{
    [Header("Base Data")]
    public BuildingData data;
    public BuildingData Data => data;

    [Header("Management")]
    public bool isBuildingActive = true;
    public BuildingPriority upkeepPriority = BuildingPriority.Normal;

    [Header("Refund State")]
    public bool hasBeenUsed;

    private bool isInitialized;
    public bool IsInitialized => isInitialized;

    public BuildingUpgradeComponent Upgrades { get; private set; }
    public BuildingWorkerComponent Workers { get; private set; }
    public BuildingTerrainComponent Terrain { get; private set; }
    public BuildingProductionComponent Production { get; private set; }

    public int currentTier => Upgrades?.CurrentTier ?? 0;

    public static event Action OnAnyBuildingEconomyChanged;
    public static event Action<BuildingEntity> OnAnyBuildingInitialized;

    public static void NotifyEconomyChanged()
    {
        OnAnyBuildingEconomyChanged?.Invoke();
    }

    protected virtual void Start()
    {
        if (data != null && !isInitialized) Initialize(data);
        SetupAtmosphericLighting();
    }

    protected virtual void OnEnable()
    {
        BuildingRegistry.Instance?.Register(this);
    }

    protected virtual void OnDisable()
    {
        BuildingRegistry.Instance?.Unregister(this);
    }

    protected virtual void OnDestroy()
    {
        UnsubscribeFromPhaseChange();
        Terrain?.ReleaseAll();
        var myCell = GetComponentInParent<HexCell>();
        myCell?.ClearBuilding();

        if (myCell != null && HexMapVisualizer.Instance != null)
        {
            int chunkRadius = HexMapGenerator.Instance != null ? HexMapGenerator.Instance.chunkRadius : 3;
            foreach (var nInfo in HexGridMath.GetNeighborsCrossChunk(myCell.chunkCoord, myCell.localCoord, chunkRadius))
            {
                var neighborHex = HexMapVisualizer.Instance.GetHexCell(nInfo.chunkCoord, nInfo.localCoord);
                neighborHex?.GetComponentInChildren<BuildingEntity>()?.ForceRescan();
            }
        }
    }

    public virtual void Initialize(BuildingData buildingData)
    {
        if (isInitialized) return;

        data = buildingData;

        if (data != null)
        {
            upkeepPriority = data.type == BuildingType.Defense ? BuildingPriority.High : BuildingPriority.Normal;
        }

        CreateComponents();
        Terrain?.FindAdjacentResources();
        SubscribeToPhaseChange();
        SetupAtmosphericLighting();

        isInitialized = true;
        OnAnyBuildingInitialized?.Invoke(this);
    }

    private void CreateComponents()
    {
        Upgrades = new BuildingUpgradeComponent(data);

        Workers = new BuildingWorkerComponent(
            this,
            data,
            Upgrades,
            GetGlobalBonusWorkersDelegate());

        Terrain = new BuildingTerrainComponent(data, transform, Upgrades);

        Production = new BuildingProductionComponent(
            this,
            data,
            Upgrades,
            Terrain,
            Workers,
            transform);
    }

    public virtual int GetGlobalWorkerBonus() => CityStatsManager.Instance?.globalBonusProductionWorkers ?? 0;

    protected virtual System.Func<int> GetGlobalBonusWorkersDelegate()
    {
        return () => GetGlobalWorkerBonus();
    }

    private void SubscribeToPhaseChange()
    {
        if (TimePhaseManager.Instance == null) return;
        TimePhaseManager.Instance.OnMorningStarted += HandleDayReset;
    }

    private void UnsubscribeFromPhaseChange()
    {
        if (TimePhaseManager.Instance == null) return;
        TimePhaseManager.Instance.OnMorningStarted -= HandleDayReset;
    }

    public virtual void ProcessUpkeep()
    {
        if (!isBuildingActive) return;
        Production?.ProcessUpkeep();
    }

    public virtual void ProcessProduction()
    {
        if (!isBuildingActive) return;
        Production?.ProcessProduction();
        RefreshInspectorUI();
    }

    protected virtual void HandleDayReset()
    {
        Workers?.ResetWorkersForNewDay();
        Terrain?.ProcessDailyTerrainModifiers();
        RefreshInspectorUI();
    }

    public virtual bool TryAddWorker(Race race)
    {
        if (!isBuildingActive || Workers == null) return false;
        if (TimePhaseManager.Instance != null && TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Night) return false;

        var success = Workers.TryAddWorker(race);
        if (success)
        {
            RefreshInspectorUI();
            NotifyEconomyChanged();
        }

        return success;
    }

    public virtual void RemoveWorker(Race race)
    {
        if (Workers == null) return;
        Workers.RemoveWorker(race);
        RefreshInspectorUI();
        NotifyEconomyChanged();
    }

    public virtual void HandleWorkerLost(Citizen citizen)
    {
        Workers?.ForgetWorker(citizen);
        RefreshInspectorUI();
        NotifyEconomyChanged();
    }

    public List<Citizen> GetAssignedCitizens() => Workers != null ? Workers.GetAssignedCitizens() : new List<Citizen>();

    public int GetWorkerCount(Race race) => Workers != null ? Workers.GetWorkerCount(race) : 0;

    public List<BuildingUpgradeSO> GetAvailableUpgrades() => Upgrades != null ? Upgrades.GetAvailableUpgrades() : new List<BuildingUpgradeSO>();

    public void ApplyUpgrade(BuildingUpgradeSO upgrade)
    {
        if (Upgrades == null) return;
        Upgrades.ApplyUpgrade(upgrade);
        RefreshInspectorUI();
        NotifyEconomyChanged();
    }

    public Dictionary<ResourceType, float> GetCurrentProduction()
    {
        if (Production == null) return new Dictionary<ResourceType, float>();
        return Production.GetCurrentProduction();
    }

    /// <summary>
    /// Zwraca efektywny dzienny koszt utrzymania budynku z uwzględnieniem globalnych ulepszeń Warsztatu.
    /// </summary>
    public List<ResourceCost> GetEffectiveUpkeep()
    {
        var result = new List<ResourceCost>();
        if (data == null) return result;

        var totals = new Dictionary<ResourceType, float>();

        if (data.upkeepPerCycle != null)
        {
            for (int i = 0; i < data.upkeepPerCycle.Count; i++)
            {
                var c = data.upkeepPerCycle[i];
                totals[c.type] = (totals.TryGetValue(c.type, out float cur) ? cur : 0f) + c.amount;
            }
        }

        if (Upgrades != null)
        {
            var increases = Upgrades.GetAllUpkeepIncreases();
            foreach (var kvp in increases)
            {
                totals[kvp.Key] = (totals.TryGetValue(kvp.Key, out float cur) ? cur : 0f) + kvp.Value;
            }

            var reductions = Upgrades.GetAllUpkeepReductions();
            foreach (var kvp in reductions)
            {
                totals[kvp.Key] = (totals.TryGetValue(kvp.Key, out float cur) ? cur : 0f) - kvp.Value;
            }

            float mult = Upgrades.GetGlobalUpkeepMultiplier();
            var keys = new List<ResourceType>(totals.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                totals[keys[i]] *= mult;
            }
        }

        foreach (var kvp in totals)
        {
            int finalAmt = Mathf.RoundToInt(kvp.Value);
            if (finalAmt > 0)
            {
                result.Add(new ResourceCost { type = kvp.Key, amount = finalAmt });
            }
        }

        return result;
    }

    /// <summary>
    /// Zwraca efektywna biezaca produkcje - jesli budynek nie ma jeszcze przypisanych pracownikow,
    /// symuluje produkcje przy 100% wydajnosci (dzieki czemu gracz widzi podglad wplywu zanim jeszcze przypisze zaloge).
    /// </summary>
    public Dictionary<ResourceType, float> GetEffectiveCurrentProduction()
    {
        if (Production == null) return new Dictionary<ResourceType, float>();
        float crew = Production.GetCrewEfficiency();
        if (crew <= 0f)
        {
            return Production.SimulateProductionWithTerrain(Terrain?.CalculateTerrainBonuses());
        }
        return Production.GetCurrentProduction();
    }

    /// <summary>
    /// Symuluje produkcje budynku przy zalozeniu, ze na candidateCell staje nowy budynek
    /// oraz tenze nowy budynek czerpie surowce z candidateClaimedHexes.
    /// </summary>
    public Dictionary<ResourceType, float> SimulateProjectedProduction(HexCell candidateCell, HashSet<HexCell> candidateClaimedHexes)
    {
        if (Terrain == null || Production == null) return GetEffectiveCurrentProduction();
        var simulatedTerrain = Terrain.SimulateTerrainBonuses(candidateCell, candidateClaimedHexes);
        return Production.SimulateProductionWithTerrain(simulatedTerrain);
    }

    public void ForceRescan()
    {
        Terrain?.FindAdjacentResources();
        if (this is TowerEntity tower) tower.RecalculateStats();
    }

    public virtual void MarkAsUsed()
    {
        if (hasBeenUsed) return;
        hasBeenUsed = true;
    }

    /// <summary>
    /// Mnożnik zwrotu kosztów budowy przy wyburzeniu (1.0 = 100% dopóki budynek nie zaczął wydobycia/strzału/następnego dnia, 0.5 = 50% po użyciu).
    /// </summary>
    public virtual float RefundMultiplier => hasBeenUsed ? 0.5f : 1.0f;

    /// <summary>
    /// Procentowa wartość zwracanych surowców przy wyburzeniu (100% lub 50%).
    /// </summary>
    public virtual int RefundPercentage => Mathf.RoundToInt(RefundMultiplier * 100f);

    public virtual bool CanDemolish() => true;

    public virtual void Demolish()
    {
        if (!CanDemolish()) return;

        Workers?.ReleaseAllWorkers();

        if (data != null && data.constructionCost != null && ResourceManager.Instance != null)
        {
            var refundMultiplier = RefundMultiplier;

            foreach (var cost in data.constructionCost)
            {
                var amountToRefund = cost.amount * refundMultiplier;
                var intRefund = Mathf.FloorToInt(amountToRefund);

                if (intRefund > 0)
                {
                    ResourceManager.Instance.AddResource(cost.type, intRefund);

                    if (FloatingTextManager.Instance != null)
                        FloatingTextManager.Instance.ShowGain(transform.position, DailyResourceCalculator.GetResourceDisplayName(cost.type), intRefund);
                }
            }
        }

        if (Terrain != null) Terrain.DestroyArtificialFeatures();

        NotifyEconomyChanged();
        Destroy(gameObject);
    }

    private void RefreshInspectorUI()
    {
    }

    public int GetTotalWorkerCount() => Workers != null ? Workers.GetAssignedCitizens().Count : 0;

    protected virtual void SetupAtmosphericLighting()
    {
        SettlementLight existingLight = GetComponentInChildren<SettlementLight>(true);
        bool isBeaconBuilding = this is BeaconEntity;

        if (existingLight != null)
        {
            existingLight.isBeacon = isBeaconBuilding;
            existingLight.autoControlByPhase = true;
            return;
        }

        GameObject lightObj = new GameObject("NightLight");
        lightObj.transform.SetParent(transform, false);

        Light ptLight = lightObj.AddComponent<Light>();
        ptLight.type = LightType.Point;
        ptLight.renderMode = LightRenderMode.Auto;
        ptLight.shadows = LightShadows.None;
        ptLight.enabled = false;

        SettlementLight settlementLight = lightObj.AddComponent<SettlementLight>();

        if (isBeaconBuilding)
        {
            settlementLight.Configure(
                SettlementLight.DefaultWarmLightColor,
                4.5f,
                16.0f,
                new Vector3(0f, 2.2f, 0f),
                allowShadows: false,
                beaconMode: true,
                lightPreset: SettlementLight.BuildingLightPreset.BeaconOfHope);
        }
        else if (data != null && data.type == BuildingType.Unique)
        {
            settlementLight.Configure(
                SettlementLight.DefaultWarmLightColor,
                2.5f,
                7.0f,
                new Vector3(0f, 1.8f, 0f),
                allowShadows: false,
                beaconMode: false,
                lightPreset: SettlementLight.BuildingLightPreset.Landmark);
        }
        else if (data != null && data.type == BuildingType.Defense)
        {
            settlementLight.Configure(
                SettlementLight.DefaultWarmLightColor,
                2.1f,
                5.8f,
                new Vector3(0f, 1.75f, 0f),
                allowShadows: false,
                beaconMode: false,
                lightPreset: SettlementLight.BuildingLightPreset.DefenseTower);
        }
        else
        {
            settlementLight.Configure(
                SettlementLight.DefaultWarmLightColor,
                2.0f,
                5.2f,
                new Vector3(0f, 1.3f, 0f),
                allowShadows: false,
                beaconMode: false,
                lightPreset: SettlementLight.BuildingLightPreset.ResourceBuilding);
        }
    }
}
