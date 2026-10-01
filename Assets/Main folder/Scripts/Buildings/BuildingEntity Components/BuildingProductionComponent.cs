using System.Collections.Generic;
using UnityEngine;

public class BuildingProductionComponent
{
    private readonly Transform buildingTransform;
    private readonly BuildingData data;
    private readonly Dictionary<ResourceType, float> productionBuffer = new Dictionary<ResourceType, float>();
    private readonly BuildingTerrainComponent terrainComponent;
    private readonly BuildingUpgradeComponent upgradeComponent;
    private readonly BuildingWorkerComponent workerComponent;

    private float currentCalculatedEfficiency;
    private bool isReadyToProduce;

    public BuildingProductionComponent(
        BuildingData data,
        BuildingUpgradeComponent upgradeComponent,
        BuildingTerrainComponent terrainComponent,
        BuildingWorkerComponent workerComponent,
        Transform buildingTransform)
    {
        this.data = data;
        this.upgradeComponent = upgradeComponent;
        this.terrainComponent = terrainComponent;
        this.workerComponent = workerComponent;
        this.buildingTransform = buildingTransform;
    }

    public void ProcessUpkeep()
    {
        isReadyToProduce = false;
        currentCalculatedEfficiency = 0f;

        if (workerComponent != null)
        {
            workerComponent.LockWorkers();
        }

        int workerCount = workerComponent != null ? workerComponent.GetActiveWorkers().Count : 0;

        float crewEfficiency = 0f;
        if (workerCount > 0)
        {
            crewEfficiency = 1f + (workerCount - 1) * data.workerScalingFactor;
        }
        else if (MetaUpgradeManager.Instance != null)
        {
            crewEfficiency = MetaUpgradeManager.Instance.GetValue(MetaEffectType.BuildingPassiveEfficiency);
        }

        if (crewEfficiency <= 0f) return;

        bool hasUpkeep = true;
        if (data.upkeepPerCycle != null && data.upkeepPerCycle.Count > 0 && ResourceManager.Instance != null)
        {
            hasUpkeep = ResourceManager.Instance.SpendResources(data.upkeepPerCycle);
        }

        float upkeepPenalty = hasUpkeep ? 0f : 0.5f;
        currentCalculatedEfficiency = Mathf.Max(0.1f, crewEfficiency - upkeepPenalty);
        isReadyToProduce = true;
    }

    public void ProcessProduction()
    {
        if (!isReadyToProduce || currentCalculatedEfficiency <= 0f) return;

        float metaBonus = 0f;
        if (MetaUpgradeManager.Instance != null)
        {
            metaBonus = MetaUpgradeManager.Instance.GetBuildingValue(MetaEffectType.SpecificBuildingProductionBonus, data);
        }

        float upgradePercent = upgradeComponent != null ? (upgradeComponent.GetGlobalProductionMultiplier() - 1f) : 0f;
        float finalMultiplier = Mathf.Max(0.1f, 1f + metaBonus + upgradePercent);

        var baseAmounts = GetBaseProductionWithFlats();

        foreach (var kvp in baseAmounts)
        {
            float rawProduced = kvp.Value * finalMultiplier * currentCalculatedEfficiency;
            int wholeProduced = AddToBufferAndGetWhole(kvp.Key, rawProduced);

            if (wholeProduced > 0 && ResourceManager.Instance != null)
            {
                ResourceManager.Instance.AddResource(kvp.Key, wholeProduced);

                if (FloatingTextManager.Instance != null)
                {
                    FloatingTextManager.Instance.ShowGain(buildingTransform.position, kvp.Key.ToString(), wholeProduced);
                }
            }
        }

        isReadyToProduce = false;
    }

    public Dictionary<ResourceType, float> GetBaseProductionWithFlats()
    {
        var total = new Dictionary<ResourceType, float>();

        if (data.productionPerCycle != null)
        {
            foreach (var res in data.productionPerCycle)
            {
                AddToDict(total, res.type, res.amount);
            }
        }

        if (upgradeComponent != null)
        {
            var upgradeBonus = upgradeComponent.GetAllProductionBonuses();
            foreach (var kvp in upgradeBonus)
            {
                AddToDict(total, kvp.Key, kvp.Value);
            }
        }

        if (terrainComponent != null)
        {
            float terrainBonus = terrainComponent.CalculateTerrainBonus();
            if (terrainBonus > 0f && data.productionPerCycle != null && data.productionPerCycle.Count > 0)
            {
                var mainRes = data.productionPerCycle[0].type;
                AddToDict(total, mainRes, terrainBonus);
            }
        }

        return total;
    }

    public Dictionary<ResourceType, float> GetCurrentProduction()
    {
        int workerCount = workerComponent != null ? workerComponent.GetActiveWorkers().Count : 0;

        float crewEfficiency = 0f;
        if (workerCount > 0)
        {
            crewEfficiency = 1f + (workerCount - 1) * data.workerScalingFactor;
        }
        else if (MetaUpgradeManager.Instance != null)
        {
            crewEfficiency = MetaUpgradeManager.Instance.GetValue(MetaEffectType.BuildingPassiveEfficiency);
        }

        float metaBonus = 0f;
        if (MetaUpgradeManager.Instance != null)
        {
            metaBonus = MetaUpgradeManager.Instance.GetBuildingValue(MetaEffectType.SpecificBuildingProductionBonus, data);
        }

        float upgradePercent = upgradeComponent != null ? (upgradeComponent.GetGlobalProductionMultiplier() - 1f) : 0f;
        float finalMultiplier = Mathf.Max(0.1f, 1f + metaBonus + upgradePercent);

        var baseAmounts = GetBaseProductionWithFlats();
        var result = new Dictionary<ResourceType, float>();

        foreach (var kvp in baseAmounts)
        {
            result[kvp.Key] = kvp.Value * finalMultiplier * crewEfficiency;
        }

        return result;
    }

    private void AddToDict(Dictionary<ResourceType, float> dict, ResourceType type, float amount)
    {
        if (dict.ContainsKey(type)) dict[type] += amount;
        else dict.Add(type, amount);
    }

    private int AddToBufferAndGetWhole(ResourceType type, float amount)
    {
        int wholePart = Mathf.FloorToInt(amount);
        float fractionPart = amount - wholePart;

        if (!productionBuffer.ContainsKey(type))
        {
            productionBuffer[type] = 0f;
        }

        productionBuffer[type] += fractionPart;

        if (productionBuffer[type] >= 1f)
        {
            wholePart += 1;
            productionBuffer[type] -= 1f;
        }

        return wholePart;
    }
}