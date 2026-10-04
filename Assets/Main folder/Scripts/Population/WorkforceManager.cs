using System;
using System.Collections.Generic;
using UnityEngine;

public class WorkforceManager : MonoBehaviour
{
    [Serializable]
    public class WorkforceStats
    {
        public int Total;
        public int Idle;
        public int Working;
    }

    public static WorkforceManager Instance { get; private set; }

    private readonly Dictionary<Race, WorkforceStats> stats = new();
    private bool isSubscribed;

    public event Action<Race, WorkforceStats> OnWorkforceStatsChanged;
    public event Action<Citizen, BuildingEntity> OnWorkerAssigned;
    public event Action<Citizen, BuildingEntity> OnWorkerUnassigned;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        foreach (var race in (Race[])Enum.GetValues(typeof(Race)))
            stats[race] = new WorkforceStats();
    }

    private void Start()
    {
        if (Instance != this) return;

        if (PopLifeCycleManager.Instance != null)
        {
            PopLifeCycleManager.Instance.OnCitizenBorn += HandleCitizenBorn;
            PopLifeCycleManager.Instance.OnCitizenDied += HandleCitizenDied;
            isSubscribed = true;
        }

        RefreshStats();
    }

    private void OnDestroy()
    {
        if (isSubscribed && PopLifeCycleManager.Instance != null)
        {
            PopLifeCycleManager.Instance.OnCitizenBorn -= HandleCitizenBorn;
            PopLifeCycleManager.Instance.OnCitizenDied -= HandleCitizenDied;
        }

        if (Instance == this) Instance = null;
    }

    public bool TryAssignWorker(Race race, BuildingEntity workplace, out Citizen worker)
    {
        worker = FindIdle(race);
        if (worker == null) return false;

        worker.AssignToWorkplace(workplace);
        OnWorkerAssigned?.Invoke(worker, workplace);
        RefreshStats();
        return true;
    }

    public void UnassignWorker(Citizen worker)
    {
        if (!ReleaseWorker(worker)) return;
        RefreshStats();
    }

    public void UnassignWorkers(IEnumerable<Citizen> workers)
    {
        if (workers == null) return;

        var anyReleased = false;
        foreach (var worker in workers)
            anyReleased |= ReleaseWorker(worker);

        if (anyReleased) RefreshStats();
    }

    public WorkforceStats GetStats(Race race) => stats.TryGetValue(race, out var s) ? s : new WorkforceStats();
    public int GetIdleCount(Race race) => GetStats(race).Idle;
    public int GetWorkingCount(Race race) => GetStats(race).Working;
    public bool HasIdleWorker(Race race) => GetIdleCount(race) > 0;

    private bool ReleaseWorker(Citizen worker)
    {
        if (worker == null || worker.workState == WorkState.Idle) return false;

        var workplace = worker.currentWorkplace;
        worker.RemoveFromWorkplace();
        OnWorkerUnassigned?.Invoke(worker, workplace);
        return true;
    }

    private Citizen FindIdle(Race race)
    {
        if (PopLifeCycleManager.Instance == null) return null;

        foreach (var citizen in PopLifeCycleManager.Instance.Citizens)
            if (citizen.race == race && citizen.workState == WorkState.Idle)
                return citizen;

        return null;
    }

    private void HandleCitizenBorn(Citizen citizen) => RefreshStats();

    private void HandleCitizenDied(Citizen citizen, DeathReason reason)
    {
        var workplace = citizen.currentWorkplace;
        if (workplace != null) workplace.HandleWorkerLost(citizen);

        ReleaseWorker(citizen);
        RefreshStats();
    }

    private void RefreshStats()
    {
        foreach (var s in stats.Values)
            s.Total = s.Idle = s.Working = 0;

        if (PopLifeCycleManager.Instance != null)
        {
            foreach (var citizen in PopLifeCycleManager.Instance.Citizens)
            {
                var s = stats[citizen.race];
                s.Total++;

                if (citizen.workState == WorkState.Idle) s.Idle++;
                else s.Working++;
            }
        }

        foreach (var kvp in stats)
            OnWorkforceStatsChanged?.Invoke(kvp.Key, kvp.Value);
    }
}
