using System;
using System.Collections.Generic;
using UnityEngine;

public class BuildingFuelComponent
{
    private readonly BuildingData data;
    private readonly Func<float> getCurrentShiftLength;
    private readonly Func<int> getMaxShifts;
    private readonly Func<int> getMaxWorkersPerShift;

    private readonly Dictionary<ResourceType, float> operationalBudget = new();
    private readonly BuildingUpgradeComponent upgradeComponent;

    public BuildingFuelComponent(
        BuildingData data, BuildingUpgradeComponent upgradeComponent,
        Func<int> getMaxShifts, Func<int> getMaxWorkersPerShift,
        Func<float> getCurrentShiftLength)
    {
        this.data = data;
        this.upgradeComponent = upgradeComponent;
        this.getMaxShifts = getMaxShifts;
        this.getMaxWorkersPerShift = getMaxWorkersPerShift;
        this.getCurrentShiftLength = getCurrentShiftLength;
    }

    /// <summary>Puste na starcie. Paliwo ładujemy dopiero jak zaczyna się pierwsza zmiana.</summary>
    public void InitialFill()
    {
        operationalBudget.Clear();
    }

    /// <summary>
    ///     Uzupełnia budżet przed startem ZMIANY, a nie dnia.
    ///     Pobiera dokładnie tyle, ile wymaga 1 pełna zmiana z ulepszeniami.
    ///     Zwraca TRUE jeśli zatankowano w pełni.
    /// </summary>
    public bool RefillForShift()
    {
        // 1. Liczymy maksymalny koszt na JEDNĄ ZMIANĘ
        var shiftUpkeep = CalculateMaxShiftConsumption();
        var isFullyFueled = true;

        foreach (var kvp in shiftUpkeep)
        {
            var current = operationalBudget.TryGetValue(kvp.Key, out var val) ? val : 0f;
            var needed = kvp.Value - current;

            if (needed <= 0f) continue; // Mamy wystarczająco dużo (np. resztki z poprzedniej zmiany)

            if (ResourceManager.Instance.SpendResource(kvp.Key, needed))
            {
                AddToBudget(kvp.Key, needed);
            }
            else
            {
                // Jeśli nie stać nas na pełne zatankowanie - dolewamy to, co gracz ma, ale oznaczamy że brakuje
                var available = ResourceManager.Instance.GetResourceAmount(kvp.Key);
                if (available > 0f)
                {
                    ResourceManager.Instance.SpendResource(kvp.Key, available);
                    AddToBudget(kvp.Key, available);
                }

                isFullyFueled = false;
            }
        }

        if (!isFullyFueled) Debug.LogWarning($"[Fuel] {data.buildingName} nie ma pełnego paliwa na start zmiany!");
        return isFullyFueled;
    }

    public bool HasFuelForHour(Dictionary<ResourceType, float> baseUpkeepPerShift, float efficiency)
    {
        var shiftLen = getCurrentShiftLength();
        foreach (var cost in baseUpkeepPerShift)
        {
            var needed = cost.Value / shiftLen * efficiency;
            if (!operationalBudget.TryGetValue(cost.Key, out var available) || available < needed)
                return false;
        }

        return true;
    }

    public void ConsumeFuelForHour(Dictionary<ResourceType, float> baseUpkeepPerShift, float efficiency)
    {
        var shiftLen = getCurrentShiftLength();
        foreach (var cost in baseUpkeepPerShift)
        {
            var needed = cost.Value / shiftLen * efficiency;
            if (operationalBudget.ContainsKey(cost.Key))
                operationalBudget[cost.Key] -= needed;
        }
    }

    /// <summary>
    ///     Oblicza zużycie na JEDNĄ zmianę przy maksymalnym obłożeniu pracownikami (baza + mnożniki).
    /// </summary>
    public Dictionary<ResourceType, float> CalculateMaxShiftConsumption()
    {
        var result = new Dictionary<ResourceType, float>();
        var baseUpkeep = GetCurrentUpkeep();

        // Maksymalne skalowanie dla tej ZMIANY (zależne od max pracowników)
        var scale = data.workerScalingFactor > 0 ? data.workerScalingFactor : 0.1f;
        var maxScale = 1f + (getMaxWorkersPerShift() - 1) * scale;

        foreach (var kvp in baseUpkeep)
            // Nie mnożymy już przez getMaxShifts() ! Liczymy tylko dla 1 zmiany.
            result.Add(kvp.Key, kvp.Value * maxScale);
        return result;
    }

    public Dictionary<ResourceType, float> GetCurrentUpkeep()
    {
        var total = new Dictionary<ResourceType, float>();

        if (data.upkeepPerCycle != null)
            foreach (var res in data.upkeepPerCycle)
                AddToDict(total, res.type, res.amount);

        var upgradeUpkeep = upgradeComponent.GetAllUpkeepIncreases();
        foreach (var kvp in upgradeUpkeep)
            AddToDict(total, kvp.Key, kvp.Value);

        // Mnożnik globalny z drzewka
        var globalUpgradeMulti = upgradeComponent.GetGlobalUpkeepMultiplier();
        if (globalUpgradeMulti != 1f)
        {
            var keys = new List<ResourceType>(total.Keys);
            foreach (var k in keys) total[k] *= globalUpgradeMulti;
        }

        return total;
    }

    public Dictionary<ResourceType, float> GetCurrentBudget()
    {
        return new Dictionary<ResourceType, float>(operationalBudget);
    }

    private void AddToBudget(ResourceType type, float amount)
    {
        if (operationalBudget.ContainsKey(type)) operationalBudget[type] += amount;
        else operationalBudget[type] = amount;
    }

    private void AddToDict(Dictionary<ResourceType, float> dict, ResourceType type, float amount)
    {
        if (dict.ContainsKey(type)) dict[type] += amount;
        else dict.Add(type, amount);
    }
}