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

        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted += HandleMorningReset;
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

        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= HandleMorningReset;
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

    public void UnassignWorker(Citizen worker, bool keepLocked = false)
    {
        if (!ReleaseWorker(worker, keepLocked)) return;
        RefreshStats();
    }

    public void UnassignWorkers(IEnumerable<Citizen> workers, bool keepLocked = false)
    {
        if (workers == null) return;

        var anyReleased = false;
        foreach (var worker in workers)
            anyReleased |= ReleaseWorker(worker, keepLocked);

        if (anyReleased) RefreshStats();
    }

    public WorkforceStats GetStats(Race race) => stats.TryGetValue(race, out var s) ? s : new WorkforceStats();
    public int GetIdleCount(Race race) => GetStats(race).Idle;
    public int GetWorkingCount(Race race) => GetStats(race).Working;
    public bool HasIdleWorker(Race race) => GetIdleCount(race) > 0;

    public bool TryAssignExpeditionWorkers(int count, out List<Citizen> assignedElves)
    {
        assignedElves = new List<Citizen>();
        if (count <= 0) return true;
        if (PopLifeCycleManager.Instance == null) return false;
        if (GetIdleCount(Race.Elves) < count) return false;

        foreach (var citizen in PopLifeCycleManager.Instance.Citizens)
        {
            if (citizen.race == Race.Elves && citizen.workState == WorkState.Idle)
            {
                citizen.workState = WorkState.OnExpedition;
                assignedElves.Add(citizen);
                if (assignedElves.Count == count) break;
            }
        }

        if (assignedElves.Count < count)
        {
            foreach (var c in assignedElves) c.workState = WorkState.Idle;
            assignedElves.Clear();
            return false;
        }

        RefreshStats();
        return true;
    }

    public void ReleaseExpeditionWorkers(IEnumerable<Citizen> workers)
    {
        if (workers == null) return;

        bool anyReleased = false;
        foreach (var citizen in workers)
        {
            if (citizen != null && citizen.workState == WorkState.OnExpedition)
            {
                citizen.workState = WorkState.Idle;
                anyReleased = true;
            }
        }

        if (anyReleased) RefreshStats();
    }

    private bool ReleaseWorker(Citizen worker, bool keepLocked = false)
    {
        if (worker == null || worker.workState == WorkState.Idle) return false;

        var wasLocked = worker.workState == WorkState.Locked;
        var workplace = worker.currentWorkplace;
        worker.RemoveFromWorkplace();
        if (keepLocked && wasLocked)
        {
            worker.workState = WorkState.Locked;
        }
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

    private void HandleMorningReset()
    {
        if (PopLifeCycleManager.Instance != null)
        {
            foreach (var citizen in PopLifeCycleManager.Instance.Citizens)
            {
                if (citizen.currentWorkplace == null && citizen.workState == WorkState.Locked)
                {
                    citizen.workState = WorkState.Idle;
                }
            }
        }
        RefreshStats();
    }

    private void HandleCitizenBorn(Citizen citizen) => RefreshStats();

    private void HandleCitizenDied(Citizen citizen, DeathReason reason)
    {
        var workplace = citizen.currentWorkplace;
        if (workplace != null) workplace.HandleWorkerLost(citizen);

        ReleaseWorker(citizen);
        RefreshStats();
    }

    public void RefreshStats()
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
