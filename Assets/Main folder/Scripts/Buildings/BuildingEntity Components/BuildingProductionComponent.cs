using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Responsible for:
///     calculating efficency, taking resource need for building, accumulatin buffer
///     transfering ready products to resoureceManager
/// </summary>
public class BuildingProductionComponent
{
    private readonly Transform buildingTransform;

    // --- Referencje ---
    private readonly BuildingData data;


    // --- Stan ---
    private readonly Dictionary<ResourceType, float> productionBuffer = new Dictionary<ResourceType, float>();

    private readonly BuildingTerrainComponent terrainComponent;
    private readonly BuildingUpgradeComponent upgradeComponent;
    private readonly BuildingWorkerComponent workerComponent;

    // --- Konstruktor ---
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
    

    // Public API
    public bool ProcessProduction()
    {
        workerComponent.LockWorkers();
        int workerCount = workerComponent.GetActiveWorkers().Count;
        if (workerCount == 0) return false;
        
        var efficiency = CalculateDynamicEfficiency(workerCount);
        var production = GetCurrentProduction();
        var loggedProduction = new Dictionary<ResourceType, float>();
        
        
        // Maintenance Costs
        if (!ResourceManager.Instance.SpendResources(data.upkeepPerCycle))
        {
            efficiency *= 0.5f;
        }
        
        // Final production
        foreach (var rsc in production)
        {
            float resourceAmount = rsc.Value * efficiency;
            int finalAmountToProduce = 0;
            
            float fraction = resourceAmount - Mathf.Floor(resourceAmount);

            if (fraction <= 0.01f || fraction >= 0.99f) finalAmountToProduce = Mathf.RoundToInt(resourceAmount);
            else finalAmountToProduce = AddToBufferAndGetWhole(rsc.Key, resourceAmount);
            

            if (finalAmountToProduce > 0) ResourceManager.Instance.AddResource(rsc.Key, finalAmountToProduce);
            
        }
        
        /*
        if (loggedProduction.Count > 0 && ResourceLogger.Instance != null)
            ResourceLogger.Instance.LogTransaction($"Produkcja: {data.buildingName}", loggedProduction);
        */


        return true;
    }

    // Calculating Efficency from all source
    //TODO: Calculate additional modifiers
    private float CalculateDynamicEfficiency(int workerCount)
    {
        var scale = data.workerScalingFactor;
        var eff = 1f;
        var firstWorkerEff = data.firstWorkerProduction;
        
        return workerCount > 1 ? 1f + (workerCount - 1) * scale : firstWorkerEff;
    }
    
    /// <summary>
    ///     Oblicza bieżącą produkcję per cykl (baza + ulepszenia + teren + meta progresja specyficzna).
    /// </summary>
    public Dictionary<ResourceType, float> GetCurrentProduction()
    {
        var total = new Dictionary<ResourceType, float>();

        // 1. Baza z BuildingData
        if (data.productionPerCycle != null)
            foreach (var res in data.productionPerCycle)
                AddToDict(total, res.type, res.amount);

        // 2. Bonusy z ulepszeń w grze (Tier 1 -> Tier 2)
        var upgradeBonus = upgradeComponent.GetAllProductionBonuses();
        foreach (var kvp in upgradeBonus)
            AddToDict(total, kvp.Key, kvp.Value);

        // 3. Bonus terenowy doliczany do głównego surowca
        var terrainBonus = terrainComponent.CalculateTerrainBonus();
        if (terrainBonus > 0f
            && data.productionPerCycle != null
            && data.productionPerCycle.Count > 0)
        {
            var mainRes = data.productionPerCycle[0].type;
            AddToDict(total, mainRes, terrainBonus);
        }

        // 4. NOWE: Bonus ze specyficznej Meta-Progresji (tylko dla tego konkretnego budynku)
        if (MetaUpgradeManager.Instance != null && data.productionPerCycle != null)
        {
            // Pobieramy wartość. Zwraca np. 0.15 (jeśli daliśmy 15% boosta do Tartaku)
            var metaSpecificBonus =
                MetaUpgradeManager.Instance.GetBuildingValue(MetaEffectType.SpecificBuildingProductionBonus, data);

            if (metaSpecificBonus > 0f)
                // Aplikujemy bonus (mnożnik) do KAŻDEGO bazowego surowca produkowanego przez ten budynek
                foreach (var res in data.productionPerCycle)
                {
                    var extraAmount = res.amount * metaSpecificBonus;
                    AddToDict(total, res.type, extraAmount);
                }
        }

        var globalUpgradeMulti = upgradeComponent.GetGlobalProductionMultiplier();
        if (globalUpgradeMulti != 1f)
        {
            var keys = new List<ResourceType>(total.Keys);
            foreach (var k in keys) total[k] *= globalUpgradeMulti;
        }

        return total;
    }
    
    // --- Helpers ---
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