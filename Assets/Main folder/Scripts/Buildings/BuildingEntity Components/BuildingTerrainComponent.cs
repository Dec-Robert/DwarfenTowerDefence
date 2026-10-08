using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BuildingTerrainComponent
{
    private readonly List<HexCell> adjacentResourceHexes = new List<HexCell>();
    private readonly Transform buildingTransform;
    private readonly BuildingData data;
    private readonly BuildingUpgradeComponent upgrades;
    private readonly HashSet<Vector2Int> artificiallyCreatedFeatures = new HashSet<Vector2Int>();

    private float cachedOnTopBonus;
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

        if (data.bonusRule.requiredFeature == HexFeatureType.None) return;

        var myCell = buildingTransform.GetComponentInParent<HexCell>();
        if (myCell == null) return;

        var mapGen = Object.FindAnyObjectByType<HexMapGenerator>();
        if (mapGen == null || !mapGen.worldData.ContainsKey(myCell.chunkCoord)) return;

        var chunkData = mapGen.worldData[myCell.chunkCoord];

        cachedOnTopBonus = 0f;
        if (chunkData.TryGetValue(myCell.localCoord, out var myHexData))
        {
            if (myHexData.feature == data.bonusRule.requiredFeature)
            {
                cachedOnTopBonus = data.bonusRule.onTopProductionBonus;
            }
        }

        if (upgrades != null && upgrades.HasSpecialEffect("MINE_MOUNTAIN_CHAIN"))
        {
            CalculateMountainChain(myCell, mapGen);
            return;
        }

        int radius = data.bonusRule.searchRadius <= 0 ? 1 : data.bonusRule.searchRadius;
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
            if (neighborData.feature != data.bonusRule.requiredFeature) continue;

            var neighborCell = HexMapVisualizer.Instance.GetHexCell(myCell.chunkCoord, coord);
            if (neighborCell == null) continue;

            bool ignoreOccupied = upgrades != null && upgrades.HasSpecialEffect("IGNORE_OCCUPIED_PENALTY");
            if (neighborCell.HasBuilding() && !ignoreOccupied) continue;

            RegisterHex(neighborCell);
        }
    }

    public float GetOnTopBonus() => cachedOnTopBonus;

    public float GetAdjacentBonus()
    {
        if (data.bonusRule.requiredFeature == HexFeatureType.None) return 0f;

        if (upgrades != null && upgrades.HasSpecialEffect("MINE_MOUNTAIN_CHAIN"))
        {
            return mountainChainSize * (data.bonusRule.baseBonusPerHex * 0.8f);
        }

        float myFlatBonus = upgrades != null ? upgrades.GetTotalTerrainFlatBonus() : 0f;
        float totalAdjacentYield = 0f;

        foreach (var cell in adjacentResourceHexes)
        {
            if (cell == null) continue;

            int usersCount = BuildingProductionRegistry.GetUsageCount(cell);
            if (upgrades != null && upgrades.HasSpecialEffect("IGNORE_OCCUPIED_PENALTY"))
            {
                usersCount = 1;
            }

            float baseHexYield = data.bonusRule.baseBonusPerHex - data.bonusRule.penaltyPerUser * (usersCount - 1);
            baseHexYield = Mathf.Max(data.bonusRule.minBonus, baseHexYield) + myFlatBonus;

            if (baseHexYield > 0f)
            {
                totalAdjacentYield += baseHexYield;
            }
        }

        return totalAdjacentYield;
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

    public float CalculateTerrainBonus()
    {
        if (data.bonusRule.requiredFeature == HexFeatureType.None) return 0f;

        if (upgrades != null && upgrades.HasSpecialEffect("MINE_MOUNTAIN_CHAIN"))
        {
            return mountainChainSize * (data.bonusRule.baseBonusPerHex * 0.8f);
        }

        return (GetOnTopBonus() + GetAdjacentBonus()) * GetTerrainMultiplier();
    }

    public void ReleaseAll()
    {
        UnregisterAll();
    }

    private void RegisterHex(HexCell cell)
    {
        BuildingProductionRegistry.RegisterUsage(cell);
        adjacentResourceHexes.Add(cell);
    }

    private void UnregisterAll()
    {
        foreach (var cell in adjacentResourceHexes)
        {
            if (cell != null)
            {
                BuildingProductionRegistry.UnregisterUsage(cell);
            }
        }
        adjacentResourceHexes.Clear();
        cachedOnTopBonus = 0f;
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

        int radius = data.bonusRule.searchRadius <= 0 ? 1 : data.bonusRule.searchRadius;
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

        var targetToDestroy = adjacentResourceHexes[Random.Range(0, adjacentResourceHexes.Count)];
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
            var currentBiome = BiomeType.Plains;

            var biomeDict = new Dictionary<BiomeType, Material>();
            foreach (var bs in mapGen.biomeSettings)
            {
                if (bs.biomeGroundMaterial != null && !biomeDict.ContainsKey(bs.type))
                {
                    biomeDict.Add(bs.type, bs.biomeGroundMaterial);
                }
            }

            if (biomeDict.Count > 0) currentBiome = biomeDict.Keys.First();

            HexMapVisualizer.Instance.RedrawSingleHex(chunkCoord, localCoord, cellData, biomeDict, currentBiome);
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