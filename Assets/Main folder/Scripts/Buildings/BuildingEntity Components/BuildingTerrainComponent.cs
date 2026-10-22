using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BuildingTerrainComponent
{
    private readonly Dictionary<HexCell, HexFeatureType> adjacentResourceHexes = new Dictionary<HexCell, HexFeatureType>();
    private readonly Transform buildingTransform;
    private readonly BuildingData data;
    private readonly BuildingUpgradeComponent upgrades;
    private readonly HashSet<Vector2Int> artificiallyCreatedFeatures = new HashSet<Vector2Int>();

    private readonly Dictionary<ResourceType, float> cachedOnTopBonuses = new Dictionary<ResourceType, float>();
    private int daysSinceLastDestroy;
    private int daysSinceLastPlant;
    private int mountainChainSize;

    public BuildingTerrainComponent(BuildingData data, Transform buildingTransform, BuildingUpgradeComponent upgrades)
    {
        this.data = data;
        this.buildingTransform = buildingTransform;
        this.upgrades = upgrades;
    }

    public void FindAdjacentResources()
    {
        UnregisterAll();
        mountainChainSize = 0;

        var rules = data.GetEffectiveBonusRules();
        if (rules == null || rules.Count == 0) return;

        var myCell = buildingTransform.GetComponentInParent<HexCell>();
        if (myCell == null) return;

        var mapGen = Object.FindAnyObjectByType<HexMapGenerator>();
        if (mapGen == null || !mapGen.worldData.ContainsKey(myCell.chunkCoord)) return;

        var chunkData = mapGen.worldData[myCell.chunkCoord];

        cachedOnTopBonuses.Clear();
        if (chunkData.TryGetValue(myCell.localCoord, out var myHexData))
        {
            foreach (var rule in rules)
            {
                if (rule.requiredFeature == myHexData.feature && rule.resourceYields != null)
                {
                    foreach (var yield in rule.resourceYields)
                    {
                        if (yield.onTopProductionBonus > 0f)
                        {
                            cachedOnTopBonuses[yield.resourceType] =
                                (cachedOnTopBonuses.TryGetValue(yield.resourceType, out float existing) ? existing : 0f) +
                                yield.onTopProductionBonus;
                        }
                    }
                }
            }
        }

        if (upgrades != null && upgrades.HasSpecialEffect("MINE_MOUNTAIN_CHAIN"))
        {
            CalculateMountainChain(myCell, mapGen);
            return;
        }

        foreach (var rule in rules)
        {
            if (rule.requiredFeature == HexFeatureType.None) continue;

            int radius = rule.searchRadius <= 0 ? 1 : rule.searchRadius;
            if (upgrades != null)
            {
                if (upgrades.HasSpecialEffect("INCREASE_RANGE_1")) radius += 1;
                if (upgrades.HasSpecialEffect("INCREASE_RANGE_2")) radius += 1;
            }

            var hexesToScan = GetHexesInRadius(myCell.localCoord, radius);

            foreach (var coord in hexesToScan)
            {
                if (coord == myCell.localCoord) continue;
                if (!chunkData.TryGetValue(coord, out var neighborData)) continue;
                if (neighborData.feature != rule.requiredFeature) continue;

                var neighborCell = HexMapVisualizer.Instance.GetHexCell(myCell.chunkCoord, coord);
                if (neighborCell == null) continue;

                bool ignoreOccupied = upgrades != null && upgrades.HasSpecialEffect("IGNORE_OCCUPIED_PENALTY");
                if (neighborCell.HasBuilding() && !ignoreOccupied) continue;

                RegisterHex(neighborCell, neighborData.feature);
            }
        }
    }

    public float GetOnTopBonus(ResourceType type)
    {
        return cachedOnTopBonuses.TryGetValue(type, out float val) ? val : 0f;
    }

    public float GetOnTopBonus()
    {
        float sum = 0f;
        foreach (var val in cachedOnTopBonuses.Values) sum += val;
        return sum;
    }

    public Dictionary<ResourceType, float> GetAdjacentBonuses()
    {
        var result = new Dictionary<ResourceType, float>();
        var rules = data.GetEffectiveBonusRules();
        if (rules == null || rules.Count == 0) return result;

        if (upgrades != null && upgrades.HasSpecialEffect("MINE_MOUNTAIN_CHAIN"))
        {
            foreach (var rule in rules)
            {
                if (rule.requiredFeature == HexFeatureType.Mountain && rule.resourceYields != null)
                {
                    foreach (var yield in rule.resourceYields)
                    {
                        float chainYield = mountainChainSize * (yield.baseBonusPerHex * 0.8f);
                        if (chainYield > 0f)
                        {
                            result[yield.resourceType] =
                                (result.TryGetValue(yield.resourceType, out float cur) ? cur : 0f) + chainYield;
                        }
                    }
                }
            }
            return result;
        }

        float myFlatBonus = upgrades != null ? upgrades.GetTotalTerrainFlatBonus() : 0f;

        foreach (var kvp in adjacentResourceHexes)
        {
            var cell = kvp.Key;
            var feature = kvp.Value;
            if (cell == null) continue;

            int usersCount = BuildingProductionRegistry.GetUsageCount(cell);
            if (upgrades != null && upgrades.HasSpecialEffect("IGNORE_OCCUPIED_PENALTY"))
            {
                usersCount = 1;
            }

            foreach (var rule in rules)
            {
                if (rule.requiredFeature != feature || rule.resourceYields == null) continue;

                foreach (var yield in rule.resourceYields)
                {
                    float baseHexYield = yield.baseBonusPerHex - yield.penaltyPerUser * (usersCount - 1);
                    baseHexYield = Mathf.Max(yield.minBonus, baseHexYield) + myFlatBonus;

                    if (baseHexYield > 0f)
                    {
                        result[yield.resourceType] =
                            (result.TryGetValue(yield.resourceType, out float cur) ? cur : 0f) + baseHexYield;
                    }
                }
            }
        }

        return result;
    }

    public float GetAdjacentBonus(ResourceType type)
    {
        var dict = GetAdjacentBonuses();
        return dict.TryGetValue(type, out float val) ? val : 0f;
    }

    public float GetAdjacentBonus()
    {
        var dict = GetAdjacentBonuses();
        float sum = 0f;
        foreach (var val in dict.Values) sum += val;
        return sum;
    }

    public float GetTerrainMultiplier()
    {
        if (upgrades != null && upgrades.HasSpecialEffect("MINE_MOUNTAIN_CHAIN"))
        {
            return 1f;
        }

        float myPercentBonus = upgrades != null ? (upgrades.GetTotalTerrainMultiplier() - 1f) : 0f;
        float metaTerrainBonus = 0f;
        if (MetaUpgradeManager.Instance != null)
        {
            metaTerrainBonus = MetaUpgradeManager.Instance.GetBuildingValue(MetaEffectType.BuildingTerrainBonusMultiplier, data);
        }

        return Mathf.Max(0.1f, 1f + myPercentBonus + metaTerrainBonus);
    }

    public Dictionary<ResourceType, float> CalculateTerrainBonuses()
    {
        var result = new Dictionary<ResourceType, float>();
        var rules = data.GetEffectiveBonusRules();
        if (rules == null || rules.Count == 0) return result;

        float multiplier = GetTerrainMultiplier();
        var adjacentBonuses = GetAdjacentBonuses();

        var allResources = new HashSet<ResourceType>(cachedOnTopBonuses.Keys);
        foreach (var res in adjacentBonuses.Keys) allResources.Add(res);

        foreach (var res in allResources)
        {
            float onTop = cachedOnTopBonuses.TryGetValue(res, out float ot) ? ot : 0f;
            float adj = adjacentBonuses.TryGetValue(res, out float ab) ? ab : 0f;
            float total = (onTop + adj) * multiplier;
            if (total > 0f)
            {
                result[res] = total;
            }
        }

        return result;
    }

    public float CalculateTerrainBonus()
    {
        var bonuses = CalculateTerrainBonuses();
        if (bonuses.Count == 0) return 0f;
        float sum = 0f;
        foreach (var val in bonuses.Values) sum += val;
        return sum;
    }

    public void ReleaseAll()
    {
        UnregisterAll();
    }

    private void RegisterHex(HexCell cell, HexFeatureType feature)
    {
        if (cell == null || adjacentResourceHexes.ContainsKey(cell)) return;

        BuildingProductionRegistry.RegisterUsage(cell);
        adjacentResourceHexes.Add(cell, feature);
    }

    private void UnregisterAll()
    {
        foreach (var kvp in adjacentResourceHexes)
        {
            if (kvp.Key != null)
            {
                BuildingProductionRegistry.UnregisterUsage(kvp.Key);
            }
        }
        adjacentResourceHexes.Clear();
        cachedOnTopBonuses.Clear();
    }

    private List<Vector2Int> GetHexesInRadius(Vector2Int center, int radius)
    {
        var results = new List<Vector2Int>();
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Mathf.Max(-radius, -q - radius);
            int r2 = Mathf.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++)
            {
                results.Add(new Vector2Int(center.x + q, center.y + r));
            }
        }
        return results;
    }

    private void CalculateMountainChain(HexCell startCell, HexMapGenerator mapGen)
    {
        var chunkData = mapGen.worldData[startCell.chunkCoord];
        var visited = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();

        queue.Enqueue(startCell.localCoord);
        visited.Add(startCell.localCoord);

        mountainChainSize = 0;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            mountainChainSize++;

            foreach (var neighbor in HexGridMath.GetNeighbors(current))
            {
                if (visited.Contains(neighbor)) continue;

                if (chunkData.TryGetValue(neighbor, out var neighborData))
                {
                    if (neighborData.feature == HexFeatureType.Mountain)
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }
    }

    public void ProcessDailyTerrainModifiers()
    {
        if (upgrades == null) return;

        if (upgrades.HasSpecialEffect("SAWMILL_PLANT_FOREST"))
        {
            daysSinceLastPlant++;
            if (daysSinceLastPlant >= 2)
            {
                TryConvertRandomHex(HexFeatureType.Forest);
                daysSinceLastPlant = 0;
            }
        }

        if (upgrades.HasSpecialEffect("SAWMILL_DESTROY_FOREST"))
        {
            daysSinceLastDestroy++;
            if (daysSinceLastDestroy >= 3)
            {
                TryDestroyResourceHex();
                daysSinceLastDestroy = 0;
            }
        }

        if (upgrades.HasSpecialEffect("FARM_CREATE_SOIL"))
        {
            daysSinceLastPlant++;
            if (daysSinceLastPlant >= 4)
            {
                TryConvertRandomHex(HexFeatureType.FertileSoil);
                daysSinceLastPlant = 0;
            }
        }
    }

    private void TryConvertRandomHex(HexFeatureType newFeature)
    {
        var myCell = buildingTransform.GetComponentInParent<HexCell>();
        if (myCell == null) return;

        var mapGen = Object.FindAnyObjectByType<HexMapGenerator>();
        if (mapGen == null || !mapGen.worldData.ContainsKey(myCell.chunkCoord)) return;

        int radius = 1;
        var rules = data.GetEffectiveBonusRules();
        if (rules != null && rules.Count > 0)
        {
            radius = rules[0].searchRadius <= 0 ? 1 : rules[0].searchRadius;
        }

        if (upgrades != null)
        {
            if (upgrades.HasSpecialEffect("INCREASE_RANGE_1")) radius += 1;
            if (upgrades.HasSpecialEffect("INCREASE_RANGE_2")) radius += 1;
        }

        var hexesToScan = GetHexesInRadius(myCell.localCoord, radius);
        var emptyCandidates = new List<Vector2Int>();
        var chunkData = mapGen.worldData[myCell.chunkCoord];

        foreach (var coord in hexesToScan)
        {
            if (coord == myCell.localCoord) continue;
            if (!chunkData.TryGetValue(coord, out var cellData)) continue;

            var neighborCell = HexMapVisualizer.Instance.GetHexCell(myCell.chunkCoord, coord);
            if (neighborCell == null || neighborCell.HasBuilding()) continue;

            if (cellData.feature == HexFeatureType.None && !cellData.isPath)
            {
                emptyCandidates.Add(coord);
            }
        }

        if (emptyCandidates.Count > 0)
        {
            var chosenHex = emptyCandidates[Random.Range(0, emptyCandidates.Count)];
            chunkData[chosenHex].feature = newFeature;
            artificiallyCreatedFeatures.Add(chosenHex);

            RefreshVisualsAndRescan(myCell.chunkCoord, chosenHex);
        }
    }

    private void TryDestroyResourceHex()
    {
        if (adjacentResourceHexes.Count == 0) return;

        var targetToDestroy = adjacentResourceHexes.Keys.ElementAt(Random.Range(0, adjacentResourceHexes.Count));
        var mapGen = Object.FindAnyObjectByType<HexMapGenerator>();

        if (mapGen != null && mapGen.worldData.ContainsKey(targetToDestroy.chunkCoord))
        {
            var cellData = mapGen.worldData[targetToDestroy.chunkCoord][targetToDestroy.localCoord];
            cellData.feature = HexFeatureType.None;

            RefreshVisualsAndRescan(targetToDestroy.chunkCoord, targetToDestroy.localCoord);
        }
    }

    private void RefreshVisualsAndRescan(Vector2Int chunkCoord, Vector2Int localCoord)
    {
        var changedCell = HexMapVisualizer.Instance.GetHexCell(chunkCoord, localCoord);
        var mapGen = Object.FindAnyObjectByType<HexMapGenerator>();

        if (changedCell != null && mapGen != null)
        {
            var cellData = mapGen.worldData[chunkCoord][localCoord];
            HexMapVisualizer.Instance.RedrawSingleHex(chunkCoord, localCoord, cellData);
        }

        var entity = buildingTransform.GetComponent<BuildingEntity>();
        if (entity != null)
        {
            entity.ForceRescan();

            foreach (var nCoord in HexGridMath.GetNeighbors(localCoord))
            {
                var neighborHex = HexMapVisualizer.Instance.GetHexCell(chunkCoord, nCoord);
                if (neighborHex != null)
                {
                    neighborHex.GetComponentInChildren<BuildingEntity>()?.ForceRescan();
                }
            }
        }
    }

    public void DestroyArtificialFeatures()
    {
        var mapGen = Object.FindAnyObjectByType<HexMapGenerator>();
        var myCell = buildingTransform.GetComponentInParent<HexCell>();

        if (mapGen == null || myCell == null) return;

        var chunkData = mapGen.worldData[myCell.chunkCoord];

        foreach (var hexCoord in artificiallyCreatedFeatures)
        {
            if (chunkData.TryGetValue(hexCoord, out var cellData))
            {
                if (cellData.feature == HexFeatureType.Forest || cellData.feature == HexFeatureType.FertileSoil)
                {
                    cellData.feature = HexFeatureType.None;
                    RefreshVisualsAndRescan(myCell.chunkCoord, hexCoord);
                }
            }
        }

        artificiallyCreatedFeatures.Clear();
    }
}