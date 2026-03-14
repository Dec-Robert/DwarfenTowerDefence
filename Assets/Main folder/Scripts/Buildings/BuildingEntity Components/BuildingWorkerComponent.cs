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
    private readonly BuildingData data;
    private readonly Func<int> getGlobalBonusShifts;
    private readonly Func<int> getGlobalBonusWorkers;
    private readonly BuildingUpgradeComponent upgradeComponent;

    // --- Konstruktor ---
    public BuildingWorkerComponent(
        BuildingData data,
        BuildingUpgradeComponent upgradeComponent,
        Func<int> getGlobalBonusShifts,
        Func<int> getGlobalBonusWorkers)
    {
        this.data = data;
        this.upgradeComponent = upgradeComponent;
        this.getGlobalBonusShifts = getGlobalBonusShifts;
        this.getGlobalBonusWorkers = getGlobalBonusWorkers;
    }

    // --- API Publiczne ---

    /// <summary>Próbuje przypisać nowego pracownika danej rasy do budynku.</summary>
    public bool TryAddWorker(Race race)
    {
        var maxWorkers = GetMaxShifts() * GetMaxWorkersPerShift();
        if (assignedCitizens.Count >= maxWorkers) return false;

        var newWorker = CitizenManager.Instance.FindAndAssignCitizen(race, null);
        if (newWorker == null) return false;

        assignedCitizens.Add(newWorker);
        return true;
    }

    /// <summary>Usuwa pierwszego nieaktywnego pracownika danej rasy.</summary>
    public void RemoveWorker(Race race)
    {
        var workerToRemove = assignedCitizens.Find(c => c.race == race && c.workState != WorkState.Working);

        if (workerToRemove == null) return;

        workerToRemove.RemoveFromWorkplace();
        assignedCitizens.Remove(workerToRemove);
    }

    /// <summary>Zwraca listę aktywnych pracowników (Assigned lub Working).</summary>
    public List<Citizen> GetActiveWorkers()
    {
        return assignedCitizens.FindAll(c => c.workState == WorkState.Assigned || c.workState == WorkState.Working);
    }

    /// <summary>Ustawia stan Working dla pracowników Assigned na początku zmiany.</summary>
    public void ActivateWorkersForShift()
    {
        foreach (var worker in assignedCitizens)
            if (worker.workState == WorkState.Assigned)
                worker.workState = WorkState.Working;
    }

    /// <summary>Ustawia stan Exhausted dla aktywnych pracowników na koniec zmiany.</summary>
    public void ExhaustWorkers()
    {
        var active = GetActiveWorkers();
        foreach (var worker in active)
            worker.workState = WorkState.Exhausted;
    }

    /// <summary>Resetuje stan pracowników na początku nowego dnia.</summary>
    public void ResetWorkersForNewDay()
    {
        foreach (var worker in assignedCitizens)
            if (worker.workState == WorkState.Exhausted || worker.workState == WorkState.Working)
                worker.workState = WorkState.Assigned;
    }

    /// <summary>Zwalnia wszystkich pracowników (np. przy rozbiórce budynku).</summary>
    public void ReleaseAllWorkers()
    {
        foreach (var citizen in new List<Citizen>(assignedCitizens))
            citizen.RemoveFromWorkplace();

        assignedCitizens.Clear();
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

    public int GetMaxShifts()
    {
        var globalBonus = getGlobalBonusShifts?.Invoke() ?? 0;
        var baseShifts = data != null ? data.baseShifts : 1;
        return baseShifts + upgradeComponent.LocalBonusShifts + globalBonus;
    }

    public int GetMaxWorkersPerShift()
    {
        var globalBonus = getGlobalBonusWorkers?.Invoke() ?? 0;
        var baseWorkers = data != null ? data.baseWorkersPerShift : 1;
        return baseWorkers + upgradeComponent.LocalBonusWorkers + globalBonus;
    }

    public void RemoveSpecificWorker(Citizen worker)
    {
        if (assignedCitizens.Contains(worker))
        {
            worker.RemoveFromWorkplace();
            assignedCitizens.Remove(worker);
        }
    }

    public int GetTotalWorkerCount()
    {
        return assignedCitizens.Count;
    }
}