using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Odpowiada za kalkulację przewidywanego bilansu surowców na koniec bieżącej fazy gry.
/// - Poranek (Morning): bilans przejścia Morning -> Dusk (Kroki 1-4: budynki ekonomiczne, Krok 5: domostwa).
/// - Zmierzch (Dusk): bilans przejścia Dusk -> Night (wyłącznie nocne utrzymanie obsadzonych wież obronnych).
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

        try
        {
            switch (currentPhase)
            {
                case TimePhases.Morning:
                    CalculateMorningToDuskForecast(result);
                    break;

                case TimePhases.Dusk:
                    CalculateDuskToNightForecast(result);
                    break;

                case TimePhases.Night:
                    // W nocy 0 kapsułek / brak podglądu
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PhaseResourceForecastManager] Błąd kalkulacji prognozy: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Prognoza dla Poranka: co wydarzy się przy przejściu Morning -> Dusk (Kroki 1-5).
    /// </summary>
    private static void CalculateMorningToDuskForecast(Dictionary<ResourceType, int> deltas)
    {
        AddEconomicForecast(deltas);
        AddHousingForecast(deltas);
    }

    /// <summary>
    /// Prognoza dla Zmierzchu: co wydarzy się przy przejściu Dusk -> Night (wyłącznie utrzymanie wież obronnych).
    /// </summary>
    private static void CalculateDuskToNightForecast(Dictionary<ResourceType, int> deltas)
    {
        var defenseBuildings = BuildingRegistry.Instance.AllBuildings
            .Where(b => b != null && b.isBuildingActive && b.data != null && b.data.type == BuildingType.Defense)
            .ToList();

        foreach (var defense in defenseBuildings)
        {
            if (defense is TowerEntity tower && tower.GetTotalWorkerCount() <= 0)
            {
                continue;
            }

            var towerUpkeep = TowerEntity.GetEffectiveTowerUpkeep(defense.data);
            if (towerUpkeep == null) continue;

            foreach (var cost in towerUpkeep)
            {
                deltas[cost.type] -= cost.amount;
            }
        }
    }

    private static void AddEconomicForecast(Dictionary<ResourceType, int> deltas)
    {
        var activeBuildings = BuildingRegistry.Instance.AllBuildings
            .Where(b => b != null && b.isBuildingActive && b.data != null &&
                        b.data.type == BuildingType.Economic &&
                        b.Production != null &&
                        !(b is HousingEntity))
            .ToList();

        var floatProduction = new Dictionary<ResourceType, float>();

        foreach (var building in activeBuildings)
        {
            float crewEfficiency = building.Production.GetCrewEfficiency();
            if (crewEfficiency <= 0f) continue;

            var effectiveUpkeep = building.GetEffectiveUpkeep();
            if (effectiveUpkeep != null)
            {
                foreach (var cost in effectiveUpkeep)
                {
                    deltas[cost.type] -= cost.amount;
                }
            }

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

        foreach (var kvp in floatProduction)
        {
            deltas[kvp.Key] += Mathf.RoundToInt(kvp.Value);
        }
    }

    private static void AddHousingForecast(Dictionary<ResourceType, int> deltas)
    {
        var housingBuildings = BuildingRegistry.Instance.AllBuildings
            .OfType<HousingEntity>()
            .Where(h => h != null && h.isBuildingActive && h.housingData != null)
            .ToList();

        foreach (var housing in housingBuildings)
        {
            int residentCount = housing.residents != null ? housing.residents.Count : 0;

            if (residentCount > 0 && housing.housingData.productionPerResident != null)
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

            var survivalCosts = housing.CalculateSurvivalCost();
            if (survivalCosts != null)
            {
                foreach (var cost in survivalCosts)
                {
                    deltas[cost.type] -= cost.amount;
                }
            }

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

    /// <summary>
    /// Przewiduje, dla których ras przy najbliższym przejściu Morning -> Dusk urodzi się co najmniej jeden mieszkaniec.
    /// Symuluje Kroki 1-4 (produkcja i upkeep ekonomii) oraz Krok 5 (globalne utrzymanie domów, następnie wzrost wg priorytetu).
    /// Poza fazą Morning zawsze zwraca false dla wszystkich ras.
    /// </summary>
    public static Dictionary<Race, bool> PredictIncomingPopulationGrowth(TimePhases currentPhase)
    {
        var result = new Dictionary<Race, bool>();
        foreach (Race r in Enum.GetValues(typeof(Race)))
        {
            result[r] = false;
        }

        if (currentPhase != TimePhases.Morning || BuildingRegistry.Instance == null)
        {
            return result;
        }

        var simResources = new Dictionary<ResourceType, int>();
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            simResources[type] = ResourceManager.Instance != null
                ? ResourceManager.Instance.GetResourceAmount(type)
                : 0;
        }

        var activeBuildings = BuildingRegistry.Instance.AllBuildings
            .Where(b => b != null && b.isBuildingActive)
            .ToList();

        SimulateEconomicSteps1To4(activeBuildings, simResources);
        SimulateHousingStep5Growth(activeBuildings, simResources, result);

        return result;
    }

    private static void SimulateEconomicSteps1To4(List<BuildingEntity> activeBuildings, Dictionary<ResourceType, int> simResources)
    {
        var economicBuildings = activeBuildings
            .Where(b => b.data != null && b.data.type == BuildingType.Economic && b.Production != null && !(b is HousingEntity))
            .OrderBy(b => (int)b.data.GetEffectiveDuskStep())
            .ThenBy(b => b.upkeepPriority)
            .ToList();

        for (int step = (int)EconomicPhaseStep.Step1_PrimaryWood; step <= (int)EconomicPhaseStep.Step4_OtherEconomic; step++)
        {
            var stepBuildings = economicBuildings
                .Where(b => (int)b.data.GetEffectiveDuskStep() == step)
                .ToList();
            if (stepBuildings.Count == 0) continue;

            var stepEfficiencies = new List<float>(stepBuildings.Count);

            foreach (var building in stepBuildings)
            {
                float crewEff = building.Production.GetCrewEfficiency();
                if (crewEff <= 0f)
                {
                    stepEfficiencies.Add(0f);
                    continue;
                }

                var effectiveUpkeep = building.GetEffectiveUpkeep();
                bool hasUpkeep = TrySpendSimulated(simResources, effectiveUpkeep);
                float finalEff = Mathf.Max(0.1f, crewEff - (hasUpkeep ? 0f : 0.5f));
                stepEfficiencies.Add(finalEff);
            }

            for (int i = 0; i < stepBuildings.Count; i++)
            {
                if (stepEfficiencies[i] <= 0f) continue;

                var produced = stepBuildings[i].Production.SimulateWholeProduction(stepEfficiencies[i]);
                foreach (var kvp in produced)
                {
                    simResources[kvp.Key] += kvp.Value;
                }
            }
        }
    }

    private static void SimulateHousingStep5Growth(
        List<BuildingEntity> activeBuildings,
        Dictionary<ResourceType, int> simResources,
        Dictionary<Race, bool> growthByRace)
    {
        var housingBuildings = activeBuildings
            .OfType<HousingEntity>()
            .Where(h => h.housingData != null)
            .OrderBy(h => h.upkeepPriority)
            .ThenBy(h => h.GetDaysRemaining())
            .ToList();

        if (housingBuildings.Count == 0) return;

        foreach (var housing in housingBuildings)
        {
            if (housing.housingData.productionPerResident == null) continue;
            int residentCount = housing.residents != null ? housing.residents.Count : 0;
            if (residentCount <= 0) continue;

            foreach (var prod in housing.housingData.productionPerResident)
            {
                int totalAmount = Mathf.FloorToInt(prod.amount * residentCount);
                if (totalAmount > 0)
                {
                    simResources[prod.type] += totalAmount;
                }
            }
        }

        bool allHousesPaidSurvival = true;
        foreach (var housing in housingBuildings)
        {
            if (!TrySpendSimulated(simResources, housing.CalculateSurvivalCost()))
            {
                allHousesPaidSurvival = false;
            }
        }

        if (!allHousesPaidSurvival) return;

        foreach (var housing in housingBuildings)
        {
            int residentCount = housing.residents != null ? housing.residents.Count : 0;
            if (housing.stopGrowth || residentCount >= housing.GetEffectiveMaxResidents()) continue;

            if (!TrySpendSimulated(simResources, housing.CalculateGrowthCost())) continue;

            int requiredTicks = housing.CalculateRequiredGrowthTicks();
            if (housing.currentGrowthProgress + 1 >= requiredTicks)
            {
                growthByRace[housing.housingData.housingRace] = true;
            }
        }
    }

    private static bool TrySpendSimulated(Dictionary<ResourceType, int> simResources, List<ResourceCost> costs)
    {
        if (costs == null || costs.Count == 0) return true;

        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            if (!simResources.TryGetValue(cost.type, out int available) || available < cost.amount)
            {
                return false;
            }
        }

        for (int i = 0; i < costs.Count; i++)
        {
            var cost = costs[i];
            simResources[cost.type] -= cost.amount;
        }

        return true;
    }
}
