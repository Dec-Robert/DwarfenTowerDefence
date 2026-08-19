using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thin building coordinator.
/// Responsible exclusively for:
/// - creating and linking logical components,
/// - subscribing to time events (TimePhaseManager),
/// - delegating calls to appropriate components,
/// - global building registry (AllBuildings).
/// All logic for production, fuel, workers, terrain, and upgrades lives in dedicated components.
/// </summary>
public class BuildingEntity : MonoBehaviour
{
    [Header("Base Data")] 
    public BuildingData data;

    [Header("Management")] 
    public bool isBuildingActive = true;
    
    [Header("Refund State")]
    [Tooltip("Has the building been used? If not = 100% refund. If yes = 50% refund.")]
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
        if (isInitialized)
        {
            Debug.LogWarning($"[BuildingEntity] {name} has already been initialized! Skipping subsequent call.");
            return;
        }

        data = buildingData;

        CreateComponents();
        Terrain.FindAdjacentResources();
        SubscribeToPhaseChange();

        isInitialized = true;
    }

    private void CreateComponents()
    {
        Upgrades = new BuildingUpgradeComponent(data);

        Workers = new BuildingWorkerComponent(
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
        TimePhaseManager.Instance.OnDuskStarted += HandleProduction;
        TimePhaseManager.Instance.OnMorningStarted += HandleDayReset;
    }

    private void UnsubscribeFromPhaseChange()
    {
        if (TimePhaseManager.Instance == null) return;
        TimePhaseManager.Instance.OnDuskStarted -= HandleProduction;
        TimePhaseManager.Instance.OnMorningStarted -= HandleDayReset;
    }

    protected virtual void HandleProduction()
    {
        if (isBuildingActive) Production.ProcessProduction();
        RefreshInspectorUI();
    }

    protected virtual void HandleDayReset()
    {
        Workers.ResetWorkersForNewDay();
        if (Terrain != null) Terrain.ProcessDailyTerrainModifiers();
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

    public List<Citizen> GetAssignedCitizens()
    {
        return Workers.GetAssignedCitizens();
    }

    public int GetWorkerCount(Race race)
    {
        return Workers.GetWorkerCount(race);
    }

    public List<BuildingUpgradeSO> GetAvailableUpgrades()
    {
        return Upgrades.GetAvailableUpgrades();
    }

    public void ApplyUpgrade(BuildingUpgradeSO upgrade)
    {
        Upgrades.ApplyUpgrade(upgrade);
        RefreshInspectorUI();
    }

    public Dictionary<ResourceType, float> GetCurrentProduction()
    {
        return Production.GetCurrentProduction();
    }

    public void ForceRescan()
    {
        Terrain.FindAdjacentResources();
        if (this is TowerEntity tower) tower.RecalculateStats();
    }

    public void Demolish()
    {
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

    /*
    public void ToggleBuildingActive()
    {
        isBuildingActive = !isBuildingActive;

        if (!isBuildingActive)
        {
            var workers = Workers.GetAssignedCitizens();
            foreach (var w in new List<Citizen>(workers))
                if (w.workState == WorkState.Assigned)
                    Workers.RemoveSpecificWorker(w);
        }

        RefreshInspectorUI();
    }
    */
    
    public int GetTotalWorkerCount()
    {
        return Workers.GetAssignedCitizens().Count;
    }
}