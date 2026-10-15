using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Odpowiada za kalkulację przewidywanego bilansu surowców na koniec bieżącej fazy gry.
/// - Poranek (Morning): bilans produkcji i kosztów utrzymania budynków produkcyjnych na Zmierzch.
/// - Zmierzch (Dusk): bilans kosztów wież obronnych (Noc) oraz kosztów/produkcji mieszkańców (kolejny Poranek).
/// - Noc (Night): bilans pusty (kapsułki są ukryte w nocy).
/// </summary>
public static class PhaseResourceForecastManager
{
    public static event Action OnForecastDirty;

    public static void NotifyForecastDirty()
    {
        OnForecastDirty?.Invoke();
    }

    /// <summary>
    /// Oblicza prognozę zmiany surowców dla danej fazy.
    /// </summary>
    public static Dictionary<ResourceType, int> CalculateForecast(TimePhases currentPhase)
    {
        var result = new Dictionary<ResourceType, int>();
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            result[type] = 0;
        }

        if (BuildingRegistry.Instance == null) return result;

        switch (currentPhase)
        {
            case TimePhases.Morning:
                CalculateMorningToDuskForecast(result);
                break;

            case TimePhases.Dusk:
                CalculateDuskToMorningForecast(result);
                break;

            case TimePhases.Night:
                // W nocy 0 kapsułek / brak podglądu
                break;
        }

        return result;
    }

    /// <summary>
    /// Prognoza dla Poranka: co wydarzy się o Zmierzchu (produkcja i utrzymanie budynków ekonomicznych).
    /// </summary>
    private static void CalculateMorningToDuskForecast(Dictionary<ResourceType, int> deltas)
    {
        var activeBuildings = BuildingRegistry.Instance.AllBuildings
            .Where(b => b != null && b.isBuildingActive && b.data != null &&
                        b.data.type != BuildingType.Defense &&
                        b.data.type != BuildingType.Housing &&
                        !(b is HousingEntity))
            .ToList();

        var floatProduction = new Dictionary<ResourceType, float>();

        foreach (var building in activeBuildings)
        {
            float crewEfficiency = building.Production != null ? building.Production.GetCrewEfficiency() : 1f;
            if (crewEfficiency <= 0f) continue;

            // 1. Koszty utrzymania (Upkeep)
            if (building.data.upkeepPerCycle != null)
            {
                foreach (var cost in building.data.upkeepPerCycle)
                {
                    deltas[cost.type] -= cost.amount;
                }
            }

            // 2. Produkcja
            var currentProd = building.GetCurrentProduction();
            if (currentProd != null)
            {
                foreach (var kvp in currentProd)
                {
                    if (!floatProduction.ContainsKey(kvp.Key)) floatProduction[kvp.Key] = 0f;
                    floatProduction[kvp.Key] += kvp.Value;
                }
            }
        }

        // Dodajemy zaokrągloną do liczb całkowitych produkcję
        foreach (var kvp in floatProduction)
        {
            deltas[kvp.Key] += Mathf.RoundToInt(kvp.Value);
        }
    }

    /// <summary>
    /// Prognoza dla Zmierzchu: co wydarzy się w Nocy (wieże) oraz kolejnego Poranka (mieszkańcy/domostwa).
    /// </summary>
    private static void CalculateDuskToMorningForecast(Dictionary<ResourceType, int> deltas)
    {
        // 1. Utrzymanie wież obronnych na noc
        var defenseBuildings = BuildingRegistry.Instance.AllBuildings
            .Where(b => b != null && b.isBuildingActive && b.data != null && b.data.type == BuildingType.Defense)
            .ToList();

        foreach (var defense in defenseBuildings)
        {
            if (defense is TowerEntity tower)
            {
                // Wieża ponosi koszty tylko jeśli posiada załogę
                if (tower.GetTotalWorkerCount() <= 0) continue;
            }

            if (defense.data.upkeepPerCycle != null)
            {
                foreach (var cost in defense.data.upkeepPerCycle)
                {
                    deltas[cost.type] -= cost.amount;
                }
            }
        }

        // 2. Gospodarka domostw na kolejny poranek
        var housingBuildings = BuildingRegistry.Instance.AllBuildings
            .OfType<HousingEntity>()
            .Where(h => h != null && h.isBuildingActive && h.housingData != null)
            .ToList();

        foreach (var housing in housingBuildings)
        {
            int residentCount = housing.residents != null ? housing.residents.Count : 0;
            if (residentCount <= 0) continue;

            // Produkcja z mieszkańców
            if (housing.housingData.productionPerResident != null)
            {
                foreach (var prod in housing.housingData.productionPerResident)
                {
                    int totalAmount = Mathf.FloorToInt(prod.amount * residentCount);
                    if (totalAmount > 0)
                    {
                        deltas[prod.type] += totalAmount;
                    }
                }
            }

            // Koszt przetrwania mieszkańców (survival upkeep)
            var survivalCosts = housing.CalculateSurvivalCost();
            if (survivalCosts != null)
            {
                foreach (var cost in survivalCosts)
                {
                    deltas[cost.type] -= cost.amount;
                }
            }

            // Koszt wzrostu domostwa
            if (!housing.stopGrowth && residentCount < housing.GetEffectiveMaxResidents())
            {
                var growthCosts = housing.CalculateGrowthCost();
                if (growthCosts != null)
                {
                    foreach (var cost in growthCosts)
                    {
                        deltas[cost.type] -= cost.amount;
                    }
                }
            }
        }
    }
}
