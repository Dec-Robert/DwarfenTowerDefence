using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Housing building entity responsible for population management.
/// Handles resident allocation, passive resource generation per resident, 
/// daily survival upkeep, and demographic growth. 
/// Operates strictly on a daily cycle triggered at the dawn phase.
/// Integrates with the integer-based resource economy and meta-progression systems.
/// </summary>
public class HousingEntity : BuildingEntity
{
    [Header("Status Mieszkańców")] 
    public List<Citizen> residents = new();

    public int currentGrowthProgress;

    [Header("Sterowanie Graczem")] 
    public bool stopGrowth;

    private bool lastSurvivalPaid;

    public event System.Action OnHousingStateChanged;

    public HousingBuildingData housingData => data as HousingBuildingData;

    private CityBaseConfigSO GetConfig()
    {
        return ResourceManager.Instance?.cityConfig;
    }

    public override void Initialize(BuildingData buildingData)
    {
        base.Initialize(buildingData);

        if (housingData == null) return;

        var baseStartPop = housingData.initialResidents;
        
        if (GetConfig() != null && GetConfig().overrideHousingData)
        {
            if (housingData.housingRace == Race.Humans) baseStartPop = GetConfig().humanStartPop;
            else if (housingData.housingRace == Race.Elves) baseStartPop = GetConfig().elfStartPop;
            else if (housingData.housingRace == Race.Dwarves) baseStartPop = GetConfig().dwarfStartPop;
        }

        var metaBonus = MetaUpgradeManager.Instance != null
            ? Mathf.RoundToInt(MetaUpgradeManager.Instance.GetValue(MetaEffectType.HousingStartPopulation))
            : 0;
            
        var finalStartPop = baseStartPop + metaBonus;

        for (var i = 0; i < finalStartPop; i++)
        {
            if (residents.Count >= GetEffectiveMaxResidents()) break;
            if (PopLifeCycleManager.Instance == null) break;
            PopLifeCycleManager.Instance.BirthCitizen(housingData.housingRace, this);
        }
    }

    public override void ProcessUpkeep()
    {
        // Domy mieszkalne rozliczają się wyłącznie o Świcie przez BuildingRegistry
    }

    public override void ProcessProduction()
    {
        // Domy mieszkalne rozliczają się wyłącznie o Świcie przez BuildingRegistry
    }

    public void ProcessMorningProduction()
    {
        if (!isBuildingActive || housingData == null) return;

        if (!hasBeenUsed)
        {
            foreach (var citizen in residents)
            {
                if (citizen.workState != WorkState.Idle)
                {
                    hasBeenUsed = true;
                    break;
                }
            }
        }

        ProducePerResident();
    }

    public void ProcessMorningSurvival()
    {
        if (!isBuildingActive || housingData == null)
        {
            lastSurvivalPaid = false;
            return;
        }

        var survivalCost = CalculateSurvivalCost();
        lastSurvivalPaid = ResourceManager.Instance != null && ResourceManager.Instance.SpendResources(survivalCost);

        if (!lastSurvivalPaid)
        {
            if (currentGrowthProgress > 0) currentGrowthProgress--;
        }
    }

    public void ProcessMorningGrowth()
    {
        if (!isBuildingActive || housingData == null) return;

        if (lastSurvivalPaid && !stopGrowth)
        {
            TryGrow();
        }

        OnHousingStateChanged?.Invoke();
    }

    private void ProducePerResident()
    {
        if (housingData.productionPerResident == null) return;

        foreach (var prod in housingData.productionPerResident)
        {
            var totalAmount = Mathf.FloorToInt(prod.amount * residents.Count);
            if (totalAmount <= 0) continue;

            ResourceManager.Instance.AddResource(prod.type, totalAmount);

            if (FloatingTextManager.Instance != null)
            {
                FloatingTextManager.Instance.ShowGain(transform.position, prod.type.ToString(), totalAmount);
            }
        }
    }

    private void TryGrow()
    {
        if (residents.Count >= GetEffectiveMaxResidents()) return;

        var growthCost = CalculateGrowthCost();
        if (!ResourceManager.Instance.SpendResources(growthCost)) return;

        currentGrowthProgress++;
        CheckForNewResident();
    }

    private void CheckForNewResident()
    {
        var required = CalculateRequiredGrowthTicks();
        if (currentGrowthProgress < required) return;
        if (PopLifeCycleManager.Instance == null) return;

        PopLifeCycleManager.Instance.BirthCitizen(housingData.housingRace, this);
        currentGrowthProgress = 0;
    }

    public void NotifyStateChanged()
    {
        OnHousingStateChanged?.Invoke();
        NotifyEconomyChanged();
    }

    public void AddResident(Citizen citizen)
    {
        if (citizen == null || residents.Contains(citizen)) return;
        residents.Add(citizen);
        NotifyStateChanged();
    }

    public void RemoveResident(Citizen citizen)
    {
        residents.Remove(citizen);
        NotifyStateChanged();
    }

    public override void Demolish()
    {
        if (PopLifeCycleManager.Instance != null)
            PopLifeCycleManager.Instance.KillAllResidents(this, DeathReason.Demolition);

        base.Demolish();
    }

    public int GetEffectiveMaxResidents()
    {
        var baseMaxPop = housingData.maxResidents;

        if (GetConfig() != null && GetConfig().overrideHousingData)
        {
            if (housingData.housingRace == Race.Humans) baseMaxPop = GetConfig().humanMaxPop;
            else if (housingData.housingRace == Race.Elves) baseMaxPop = GetConfig().elfMaxPop;
            else if (housingData.housingRace == Race.Dwarves) baseMaxPop = GetConfig().dwarfMaxPop;
        }

        var bonus = 0;

        if (MetaUpgradeManager.Instance != null)
        {
            bonus += Mathf.RoundToInt(MetaUpgradeManager.Instance.GetValue(MetaEffectType.HousingMaxResidents));

            var raceEffect = housingData.housingRace switch
            {
                Race.Humans => MetaEffectType.HousingMaxResidents_Humans,
                Race.Elves => MetaEffectType.HousingMaxResidents_Elves,
                Race.Dwarves => MetaEffectType.HousingMaxResidents_Dwarves,
                _ => MetaEffectType.None
            };

            if (raceEffect != MetaEffectType.None)
            {
                bonus += Mathf.RoundToInt(MetaUpgradeManager.Instance.GetValue(raceEffect));
            }
        }

        return baseMaxPop + bonus;
    }

    public int CalculateRequiredGrowthTicks()
    {
        return Mathf.FloorToInt(housingData.baseGrowthTicks + residents.Count * housingData.growthDifficultyMultiplier);
    }

    public List<ResourceCost> CalculateSurvivalCost()
    {
        var totalDict = new Dictionary<ResourceType, int>();
        if (housingData == null) return new List<ResourceCost>();

        if (housingData.baseDailyUpkeep != null)
        {
            foreach (var c in housingData.baseDailyUpkeep)
            {
                AddToDict(totalDict, c.type, Mathf.FloorToInt(c.amount));
            }
        }

        if (housingData.upkeepPerResident != null)
        {
            foreach (var c in housingData.upkeepPerResident)
            {
                AddToDict(totalDict, c.type, Mathf.FloorToInt(c.amount * residents.Count));
            }
        }

        var resultList = new List<ResourceCost>();
        foreach (var kvp in totalDict)
        {
            resultList.Add(new ResourceCost { type = kvp.Key, amount = kvp.Value });
        }
        
        return resultList;
    }

    public List<ResourceCost> CalculateGrowthCost()
    {
        var totalDict = new Dictionary<ResourceType, int>();
        if (housingData == null || housingData.growthSurplusCost == null) return new List<ResourceCost>();

        foreach (var c in housingData.growthSurplusCost)
        {
            AddToDict(totalDict, c.type, Mathf.FloorToInt(c.amount));
        }

        var resultList = new List<ResourceCost>();
        foreach (var kvp in totalDict)
        {
            resultList.Add(new ResourceCost { type = kvp.Key, amount = kvp.Value });
        }
        
        return resultList;
    }

    private void AddToDict(Dictionary<ResourceType, int> d, ResourceType t, int a)
    {
        if (d.ContainsKey(t)) d[t] += a;
        else d.Add(t, a);
    }

    public float GetGrowthProgress()
    {
        if (residents.Count >= GetEffectiveMaxResidents()) return 1f;
        var required = CalculateRequiredGrowthTicks();
        return required == 0 ? 1f : (float)currentGrowthProgress / required;
    }

    public int GetDaysRemaining()
    {
        if (residents.Count >= GetEffectiveMaxResidents()) return 0;
        return Mathf.Max(0, CalculateRequiredGrowthTicks() - currentGrowthProgress);
    }

    public int GetProjectedUpkeep()
    {
        int total = 0;
        foreach (var cost in CalculateSurvivalCost()) total += cost.amount;
        return total;
    }

    public string GetGrowthStatus()
    {
        if (residents.Count >= GetEffectiveMaxResidents()) return "PEŁNY";
        if (stopGrowth) return "WSTRZYMANY";

        var survivalCosts = CalculateSurvivalCost();
        if (ResourceManager.Instance != null)
        {
            foreach (var cost in survivalCosts)
            {
                if (!ResourceManager.Instance.CanAfford(cost.type, cost.amount))
                    return "GŁÓD (-1 CYKL)";
            }

            var combinedDict = new Dictionary<ResourceType, int>();
            foreach (var cost in survivalCosts)
                AddToDict(combinedDict, cost.type, cost.amount);
            foreach (var cost in CalculateGrowthCost())
                AddToDict(combinedDict, cost.type, cost.amount);

            foreach (var kvp in combinedDict)
            {
                if (!ResourceManager.Instance.CanAfford(kvp.Key, kvp.Value))
                    return "BRAK NADWYŻKI";
            }
        }

        return "ROSNĄCY";
    }
}