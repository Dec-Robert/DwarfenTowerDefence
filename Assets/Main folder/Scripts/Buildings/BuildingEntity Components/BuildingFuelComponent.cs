using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Odpowiada za budżet operacyjny budynku (paliwo/surowce zużywane do pracy):
/// codzienne tankowanie z ResourceManager, sprawdzanie dostępności zasobów
/// i pobieranie kosztów podczas produkcji.
/// Trzymana jako pole w BuildingEntity (zwykła klasa C#, nie MonoBehaviour).
/// </summary>
public class BuildingFuelComponent
{
    // --- Referencje ---
    private readonly BuildingData data;
    private readonly BuildingUpgradeComponent upgradeComponent;
    private readonly System.Func<int> getMaxShifts;
    private readonly System.Func<int> getMaxWorkersPerShift;
    private readonly System.Func<float> getCurrentShiftLength;

    // --- Stan ---
    private readonly Dictionary<ResourceType, float> operationalBudget =
        new Dictionary<ResourceType, float>();

    // --- Konstruktor ---
    public BuildingFuelComponent(
        BuildingData data,
        BuildingUpgradeComponent upgradeComponent,
        System.Func<int> getMaxShifts,
        System.Func<int> getMaxWorkersPerShift,
        System.Func<float> getCurrentShiftLength)
    {
        this.data = data;
        this.upgradeComponent = upgradeComponent;
        this.getMaxShifts = getMaxShifts;
        this.getMaxWorkersPerShift = getMaxWorkersPerShift;
        this.getCurrentShiftLength = getCurrentShiftLength;
    }

    // --- API Publiczne ---

    /// <summary>
    /// Wstępne napełnienie budżetu przy inicjalizacji budynku (40% dziennego zapotrzebowania).
    /// </summary>
    public void InitialFill()
    {
        operationalBudget.Clear();
        var maxDaily = CalculateMaxDailyConsumption();

        foreach (var kvp in maxDaily)
            operationalBudget[kvp.Key] = kvp.Value * 0.40f;
    }

    /// <summary>
    /// Sprawdza, czy budynek ma wystarczające paliwo na bieżącą godzinę pracy
    /// przy danej liczbie aktywnych pracowników (efficiency).
    /// </summary>
    public bool HasFuelForHour(Dictionary<ResourceType, float> baseUpkeepPerShift, float efficiency)
    {
        float shiftLen = getCurrentShiftLength();

        foreach (var cost in baseUpkeepPerShift)
        {
            float needed = (cost.Value / shiftLen) * efficiency;

            if (!operationalBudget.TryGetValue(cost.Key, out float available)
                || available < needed)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Pobiera koszt paliwa za bieżącą godzinę pracy.
    /// Należy wywołać tylko po uprzednim potwierdzeniu przez HasFuelForHour.
    /// </summary>
    public void ConsumeFuelForHour(Dictionary<ResourceType, float> baseUpkeepPerShift, float efficiency)
    {
        float shiftLen = getCurrentShiftLength();

        foreach (var cost in baseUpkeepPerShift)
        {
            float needed = (cost.Value / shiftLen) * efficiency;

            if (operationalBudget.ContainsKey(cost.Key))
                operationalBudget[cost.Key] -= needed;
        }
    }

    /// <summary>
    /// Uzupełnia budżet operacyjny na początku nowego dnia.
    /// Pobiera brakujące zasoby z ResourceManager (tyle ile dostępne).
    /// </summary>
    public void RefillForNewDay()
    {
        var maxDaily = CalculateMaxDailyConsumption();

        foreach (var kvp in maxDaily)
        {
            float current = operationalBudget.TryGetValue(kvp.Key, out float val) ? val : 0f;
            float needed = kvp.Value - current;
            if (needed <= 0f) continue;

            if (ResourceManager.Instance.SpendResource(kvp.Key, needed))
            {
                AddToBudget(kvp.Key, needed);
            }
            else
            {
                // Bierz ile jest – nie blokuj działania budynku całkowicie
                float available = ResourceManager.Instance.GetResourceAmount(kvp.Key);
                if (available > 0f)
                {
                    ResourceManager.Instance.SpendResource(kvp.Key, available);
                    AddToBudget(kvp.Key, available);
                }
            }
        }
    }

    /// <summary>
    /// Oblicza maksymalne dzienne zużycie surowców przy pełnym obłożeniu.
    /// Używane do rezerwowania budżetu i wyświetlania w UI.
    /// </summary>
    public Dictionary<ResourceType, float> CalculateMaxDailyConsumption()
    {
        var result = new Dictionary<ResourceType, float>();
        var baseUpkeep = GetCurrentUpkeep();

        float scale = data.workerScalingFactor > 0 ? data.workerScalingFactor : 0.1f;
        float maxScale = 1f + (getMaxWorkersPerShift() - 1) * scale;

        foreach (var kvp in baseUpkeep)
        {
            float dailyTotal = kvp.Value * maxScale * getMaxShifts();
            result.Add(kvp.Key, dailyTotal);
        }

        return result;
    }

    /// <summary>Zwraca aktualny upkeep (baza + ulepszenia).</summary>
    public Dictionary<ResourceType, float> GetCurrentUpkeep()
    {
        var total = new Dictionary<ResourceType, float>();

        if (data.upkeepPerCycle != null)
            foreach (var res in data.upkeepPerCycle)
                AddToDict(total, res.type, res.amount);

        var upgradeUpkeep = upgradeComponent.GetAllUpkeepIncreases();
        foreach (var kvp in upgradeUpkeep)
            AddToDict(total, kvp.Key, kvp.Value);

        return total;
    }

    /// <summary>Zwraca kopię bieżącego budżetu (do odczytu w UI).</summary>
    public Dictionary<ResourceType, float> GetCurrentBudget() =>
        new Dictionary<ResourceType, float>(operationalBudget);

    // --- Helpers ---

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
