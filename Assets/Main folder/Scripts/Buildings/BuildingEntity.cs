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

    protected virtual void Start()
    {
        if (data != null && !isInitialized) Initialize(data);
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
        }

        return success;
    }

    public virtual void RemoveWorker(Race race)
    {
        Workers.RemoveWorker(race);
        RefreshInspectorUI();
    }

    public virtual void HandleWorkerLost(Citizen citizen)
    {
        Workers?.ForgetWorker(citizen);
        RefreshInspectorUI();
    }

    public List<Citizen> GetAssignedCitizens() => Workers.GetAssignedCitizens();

    public int GetWorkerCount(Race race) => Workers.GetWorkerCount(race);

    public List<BuildingUpgradeSO> GetAvailableUpgrades() => Upgrades.GetAvailableUpgrades();

    public void ApplyUpgrade(BuildingUpgradeSO upgrade)
    {
        Upgrades.ApplyUpgrade(upgrade);
        RefreshInspectorUI();
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

        Destroy(gameObject);
    }

    private void RefreshInspectorUI()
    {
    }

    public int GetTotalWorkerCount() => Workers.GetAssignedCitizens().Count;
}