using System;
using System.Collections.Generic;

/// <summary>
///     Odpowiada za całą logikę pracowników budynku:
///     przypisywanie, usuwanie, liczenie oraz resetowanie stanów pracy.
///     Trzymana jako pole w BuildingEntity (zwykła klasa C#, nie MonoBehaviour).
/// </summary>
public class BuildingWorkerComponent
{
    // --- Stan ---
    private readonly List<Citizen> assignedCitizens = new();

    // --- Referencje ---
    private readonly BuildingEntity owner;
    private readonly BuildingData data;
    private readonly Func<int> getGlobalBonusWorkers;
    private readonly BuildingUpgradeComponent upgradeComponent;

    // --- Konstruktor ---
    public BuildingWorkerComponent(
        BuildingEntity owner,
        BuildingData data,
        BuildingUpgradeComponent upgradeComponent,
        Func<int> getGlobalBonusWorkers)
    {
        this.owner = owner;
        this.data = data;
        this.upgradeComponent = upgradeComponent;
        this.getGlobalBonusWorkers = getGlobalBonusWorkers;
    }

    // --- API Publiczne ---

    /// <summary>Próbuje przypisać nowego pracownika danej rasy do budynku.</summary>
    public bool TryAddWorker(Race race)
    {
        var maxWorkers = data.baseMaxWorkers + upgradeComponent.LocalBonusWorkers + (getGlobalBonusWorkers?.Invoke() ?? 0);
        if (assignedCitizens.Count >= maxWorkers) return false;
        if (WorkforceManager.Instance == null) return false;

        if (!WorkforceManager.Instance.TryAssignWorker(race, owner, out var newWorker)) return false;

        assignedCitizens.Add(newWorker);
        return true;
    }

    /// <summary>Usuwa pierwszego nieaktywnego pracownika danej rasy.</summary>
    public void RemoveWorker(Race race)
    {
        var workerToRemove = assignedCitizens.Find(c => c.race == race && c.workState != WorkState.Locked);

        if (workerToRemove == null) return;

        assignedCitizens.Remove(workerToRemove);
        if (WorkforceManager.Instance != null) WorkforceManager.Instance.UnassignWorker(workerToRemove);
    }

    /// <summary>Zwraca listę aktywnych pracowników (Assigned lub Working).</summary>
    public List<Citizen> GetActiveWorkers()
    {
        return assignedCitizens.FindAll(c => c.workState == WorkState.Assigned || c.workState == WorkState.Locked);
    }
    

    /// <summary>Locking worker at the end of the work day</summary>
    public void LockWorkers()
    {
        var active = GetActiveWorkers();
        foreach (var worker in active)
            worker.workState = WorkState.Locked;
    }

    /// <summary>Resetuje stan pracowników na początku nowego dnia.</summary>
    public void ResetWorkersForNewDay()
    {
        foreach (var worker in assignedCitizens)
            if (worker.workState == WorkState.Assigned || worker.workState == WorkState.Locked)
                worker.workState = WorkState.Assigned;
    }

    /// <summary>Zwalnia wszystkich pracowników (np. przy wstrzymaniu lub rozbiórce budynku).</summary>
    public void ReleaseAllWorkers(bool preserveLockedState = false)
    {
        var released = new List<Citizen>(assignedCitizens);
        assignedCitizens.Clear();

        if (WorkforceManager.Instance != null) WorkforceManager.Instance.UnassignWorkers(released, preserveLockedState);
    }

    // --- Gettery ---

    public List<Citizen> GetAssignedCitizens()
    {
        return assignedCitizens;
    }

    public int GetWorkerCount(Race race)
    {
        return assignedCitizens.FindAll(c => c.race == race).Count;
    }
    
    public void RemoveSpecificWorker(Citizen worker)
    {
        if (!assignedCitizens.Remove(worker)) return;

        if (WorkforceManager.Instance != null) WorkforceManager.Instance.UnassignWorker(worker);
    }

    public void ForgetWorker(Citizen worker)
    {
        assignedCitizens.Remove(worker);
    }


}