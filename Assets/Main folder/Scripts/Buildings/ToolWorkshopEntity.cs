using UnityEngine;

/// <summary>
/// Budynek specjalistyczny Ludzi: Warsztat narzędziowy.
/// Mieści maksymalnie 3 Ludzi (Race.Humans). Aktywny warsztat z obsadą 3/3 przyspiesza globalne badania.
/// </summary>
public class ToolWorkshopEntity : BuildingEntity
{
    public const int RequiredWorkersForResearch = 3;

    public bool IsContributingToResearch =>
        isBuildingActive && gameObject.activeInHierarchy && GetWorkerCount(Race.Humans) >= RequiredWorkersForResearch;

    public override void Initialize(BuildingData buildingData)
    {
        base.Initialize(buildingData);
        ToolWorkshopResearchManager.Instance?.RegisterWorkshop(this);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        ToolWorkshopResearchManager.Instance?.RegisterWorkshop(this);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ToolWorkshopResearchManager.Instance?.UnregisterWorkshop(this);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        ToolWorkshopResearchManager.Instance?.UnregisterWorkshop(this);
    }

    public override int GetGlobalWorkerBonus() => 0;

    public override bool TryAddWorker(Race race)
    {
        if (race != Race.Humans) return false;
        if (!isBuildingActive || Workers == null) return false;
        if (TimePhaseManager.Instance != null && TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Night)
            return false;

        if (GetWorkerCount(Race.Humans) >= RequiredWorkersForResearch) return false;

        bool success = base.TryAddWorker(race);
        if (success)
        {
            ToolWorkshopResearchManager.Instance?.NotifyWorkshopsChanged();
        }
        return success;
    }

    public override void RemoveWorker(Race race)
    {
        if (race != Race.Humans) return;
        base.RemoveWorker(race);
        ToolWorkshopResearchManager.Instance?.NotifyWorkshopsChanged();
    }

    public override void HandleWorkerLost(Citizen citizen)
    {
        base.HandleWorkerLost(citizen);
        ToolWorkshopResearchManager.Instance?.NotifyWorkshopsChanged();
    }

    public void SetActiveState(bool active)
    {
        if (isBuildingActive == active) return;
        isBuildingActive = active;
        if (!isBuildingActive)
        {
            Workers?.ReleaseAllWorkers(true);
        }
        ToolWorkshopResearchManager.Instance?.NotifyWorkshopsChanged();
        NotifyEconomyChanged();
    }

    public override void ProcessUpkeep()
    {
        if (!isBuildingActive) return;
        Workers?.LockWorkers();
    }

    public override void ProcessProduction()
    {
        // Warsztat nie produkuje surowców — postęp badań nalicza ToolWorkshopResearchManager o poranku.
    }
}
