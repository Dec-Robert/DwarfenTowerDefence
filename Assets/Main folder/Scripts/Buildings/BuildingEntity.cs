using System;
using System.Collections.Generic;
using UnityEngine;

public class BuildingEntity : MonoBehaviour
{
    [Header("Base Data")]
    public BuildingData data;

    [Header("Management")]
    public bool isBuildingActive = true;
    public BuildingPriority upkeepPriority = BuildingPriority.Normal;

    [Header("Refund State")]
    public bool hasBeenUsed;

    private bool isInitialized;

    public BuildingUpgradeComponent Upgrades { get; private set; }
    public BuildingWorkerComponent Workers { get; private set; }
    public BuildingTerrainComponent Terrain { get; private set; }
    public BuildingProductionComponent Production { get; private set; }

    public int currentTier => Upgrades?.CurrentTier ?? 0;

    public static event Action OnAnyBuildingEconomyChanged;

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
        Terrain.FindAdjacentResources();
        SubscribeToPhaseChange();
        SetupAtmosphericLighting();

        isInitialized = true;
    }

    private void CreateComponents()
    {
        Upgrades = new BuildingUpgradeComponent(data);

        Workers = new BuildingWorkerComponent(
            this,
            data,
            Upgrades,
            () => CityStatsManager.Instance?.globalBonusWorkersPerShift ?? 0);

        Terrain = new BuildingTerrainComponent(data, transform, Upgrades);

        Production = new BuildingProductionComponent(
            data,
            Upgrades,
            Terrain,
            Workers,
            transform);
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
        if (!isBuildingActive) return false;

        var success = Workers.TryAddWorker(race);
        if (success)
        {
            hasBeenUsed = true;
            RefreshInspectorUI();
            NotifyEconomyChanged();
        }

        return success;
    }

    public virtual void RemoveWorker(Race race)
    {
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

    public List<Citizen> GetAssignedCitizens() => Workers.GetAssignedCitizens();

    public int GetWorkerCount(Race race) => Workers.GetWorkerCount(race);

    public List<BuildingUpgradeSO> GetAvailableUpgrades() => Upgrades.GetAvailableUpgrades();

    public void ApplyUpgrade(BuildingUpgradeSO upgrade)
    {
        Upgrades.ApplyUpgrade(upgrade);
        RefreshInspectorUI();
        NotifyEconomyChanged();
    }

    public Dictionary<ResourceType, float> GetCurrentProduction() => Production.GetCurrentProduction();

    public void ForceRescan()
    {
        Terrain.FindAdjacentResources();
        if (this is TowerEntity tower) tower.RecalculateStats();
    }

    public virtual bool CanDemolish() => true;

    public virtual void Demolish()
    {
        if (!CanDemolish()) return;

        Workers.ReleaseAllWorkers();

        if (data != null && data.constructionCost != null && ResourceManager.Instance != null)
        {
            var refundMultiplier = hasBeenUsed ? 0.5f : 1.0f;

            foreach (var cost in data.constructionCost)
            {
                var amountToRefund = cost.amount * refundMultiplier;
                var intRefund = Mathf.FloorToInt(amountToRefund);

                if (intRefund > 0)
                {
                    ResourceManager.Instance.AddResource(cost.type, intRefund);

                    if (FloatingTextManager.Instance != null)
                        FloatingTextManager.Instance.ShowGain(transform.position, cost.type.ToString(), intRefund);
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

    public int GetTotalWorkerCount() => Workers.GetAssignedCitizens().Count;

    protected virtual void SetupAtmosphericLighting()
    {
        if (GetComponentInChildren<SettlementLight>() != null) return;

        GameObject lightObj = new GameObject("Settlement_Light");
        lightObj.transform.SetParent(transform, false);

        Light ptLight = lightObj.AddComponent<Light>();
        ptLight.type = LightType.Point;
        ptLight.renderMode = LightRenderMode.Auto;

        SettlementLight settlementLight = lightObj.AddComponent<SettlementLight>();

        if (this is BeaconEntity || (data != null && data.type == BuildingType.Unique))
        {
            lightObj.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            ptLight.shadows = LightShadows.Soft;
            settlementLight.isBeacon = true;
            settlementLight.baseIntensity = 3.5f;
            settlementLight.baseRange = 25f;
            settlementLight.lightColor = new Color(1.0f, 0.65f, 0.30f, 1f);
            settlementLight.autoControlByPhase = false;
        }
        else if (data != null && data.type == BuildingType.Defense)
        {
            lightObj.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            ptLight.shadows = LightShadows.Soft;
            settlementLight.baseIntensity = 2.2f;
            settlementLight.baseRange = 14f;
            settlementLight.lightColor = new Color(1.0f, 0.58f, 0.22f, 1f);
            settlementLight.autoControlByPhase = true;
        }
        else
        {
            lightObj.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            ptLight.shadows = LightShadows.None;
            settlementLight.baseIntensity = 1.3f;
            settlementLight.baseRange = 8f;
            settlementLight.lightColor = new Color(1.0f, 0.72f, 0.40f, 1f);
            settlementLight.autoControlByPhase = true;
        }
    }
}