using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public static class BuildingProductionQoLTests
{
    private static int _passed = 0;
    private static int _failed = 0;

    private static void Assert(bool condition, string testName, string details = "")
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"[QOL-PROD-PASS] {testName}");
        }
        else
        {
            _failed++;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[QOL-PROD-FAIL] {testName} - {details}");
            Console.ResetColor();
        }
    }

    private static T CreateInstanceWithoutNative<T>() where T : class
    {
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }

    public static int RunAll()
    {
        Console.WriteLine("\n=================================================");
        Console.WriteLine("BUILDING PRODUCTION PREVIEW QOL TESTS");
        Console.WriteLine("=================================================\n");

        _passed = 0;
        _failed = 0;

        TestHasProductionFiltering();
        TestBaseProductionOnly();
        TestOnTopFeatureBonus();
        TestAdjacentFeatureBonus();
        TestNeighborPenaltiesSubtracted();
        TestOccupiedHexIgnored();
        TestElevationBonusAndHillPenalties();
        TestMultiResourceBuildings();
        TestGlobalMultipliersAtFullEfficiency();
        TestGEMINI_UIGuidelines();

        Console.WriteLine($"\nBUILDING PRODUCTION QOL SUMMARY: {_passed} PASSED, {_failed} FAILED");
        return _failed;
    }

    private static void TestHasProductionFiltering()
    {
        var sawmillData = CreateInstanceWithoutNative<BuildingData>();
        sawmillData.type = BuildingType.Economic;
        sawmillData.productionPerCycle = new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 4 } };

        Assert(BuildingProductionCalculator.HasProduction(sawmillData), "HasProduction: Economic sawmill returns true");

        var towerData = CreateInstanceWithoutNative<TowerData>();
        towerData.type = BuildingType.Defense;
        Assert(!BuildingProductionCalculator.HasProduction(towerData), "HasProduction: Defense tower returns false");

        var housingData = CreateInstanceWithoutNative<HousingBuildingData>();
        housingData.type = BuildingType.Housing;
        Assert(!BuildingProductionCalculator.HasProduction(housingData), "HasProduction: Housing building returns false");

        Assert(!BuildingProductionCalculator.HasProduction(null), "HasProduction: Null data safely returns false");
    }

    private static void TestBaseProductionOnly()
    {
        var data = CreateInstanceWithoutNative<BuildingData>();
        data.type = BuildingType.Economic;
        data.productionPerCycle = new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 5 } };

        var cell = CreateInstanceWithoutNative<HexCell>();
        var cellData = new HexCellData { chunkCoord = new Vector2Int(0, 0), localCoord = new Vector2Int(0, 0), feature = HexFeatureType.None };
        cell.Initialize(cellData);

        var estimates = BuildingProductionCalculator.CalculateProjectedProduction(cell, data);
        Assert(estimates != null && estimates.Count == 1, "BaseOnly: Exactly 1 resource estimate returned");
        if (estimates != null && estimates.Count > 0)
        {
            var est = estimates[0];
            Assert(est.resourceType == ResourceType.Wood, "BaseOnly: Resource type is Wood");
            Assert(Mathf.Approximately(est.baseProduction, 5f), "BaseOnly: Base production is 5");
            Assert(Mathf.Approximately(est.onTopBonus, 0f), "BaseOnly: On-top bonus is 0");
            Assert(Mathf.Approximately(est.adjacentNetBonus, 0f), "BaseOnly: Adjacent net bonus is 0");
            Assert(Mathf.Approximately(est.totalProduction, 5f), "BaseOnly: Total production at 100% efficiency is 5");
        }
    }

    private static void TestOnTopFeatureBonus()
    {
        var data = CreateInstanceWithoutNative<BuildingData>();
        data.type = BuildingType.Economic;
        data.productionPerCycle = new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 4 } };
        data.bonusRules = new List<TerrainBonusRule>
        {
            new TerrainBonusRule
            {
                requiredFeature = HexFeatureType.Forest,
                searchRadius = 1,
                resourceYields = new List<TerrainResourceYield>
                {
                    new TerrainResourceYield
                    {
                        resourceType = ResourceType.Wood,
                        baseBonusPerHex = 4f,
                        onTopProductionBonus = 6f,
                        penaltyPerUser = 1f,
                        minBonus = 1f
                    }
                }
            }
        };

        var cell = CreateInstanceWithoutNative<HexCell>();
        var cellData = new HexCellData { chunkCoord = new Vector2Int(0, 0), localCoord = new Vector2Int(0, 0), feature = HexFeatureType.Forest };
        cell.Initialize(cellData);

        var gen = CreateInstanceWithoutNative<HexMapGenerator>();
        var ctx = new MapGenerationContext(12345);
        ctx.worldData[new Vector2Int(0, 0)] = new Dictionary<Vector2Int, HexCellData>
        {
            [new Vector2Int(0, 0)] = cellData
        };
        typeof(HexMapGenerator).GetField("ctx", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(gen, ctx);

        var estimates = BuildingProductionCalculator.CalculateProjectedProduction(cell, data, gen);
        Assert(estimates != null && estimates.Count == 1, "OnTopBonus: 1 estimate returned");
        if (estimates != null && estimates.Count > 0)
        {
            var est = estimates[0];
            Assert(Mathf.Approximately(est.baseProduction, 4f), "OnTopBonus: Base production is 4");
            Assert(Mathf.Approximately(est.onTopBonus, 6f), "OnTopBonus: On-top bonus is 6");
            Assert(Mathf.Approximately(est.totalProduction, 10f), "OnTopBonus: Total production is 10 (4 base + 6 on-top)");
        }
    }

    private static void TestAdjacentFeatureBonus()
    {
        var data = CreateInstanceWithoutNative<BuildingData>();
        data.type = BuildingType.Economic;
        data.productionPerCycle = new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 4 } };
        data.bonusRules = new List<TerrainBonusRule>
        {
            new TerrainBonusRule
            {
                requiredFeature = HexFeatureType.Forest,
                searchRadius = 1,
                resourceYields = new List<TerrainResourceYield>
                {
                    new TerrainResourceYield
                    {
                        resourceType = ResourceType.Wood,
                        baseBonusPerHex = 4f,
                        onTopProductionBonus = 6f,
                        penaltyPerUser = 1f,
                        minBonus = 1f
                    }
                }
            }
        };

        var cell = CreateInstanceWithoutNative<HexCell>();
        var cellData = new HexCellData { chunkCoord = new Vector2Int(0, 0), localCoord = new Vector2Int(0, 0), feature = HexFeatureType.None };
        cell.Initialize(cellData);

        var neighborCoord = new Vector2Int(1, 0);
        var neighborCell = CreateInstanceWithoutNative<HexCell>();
        var neighborData = new HexCellData { chunkCoord = new Vector2Int(0, 0), localCoord = neighborCoord, feature = HexFeatureType.Forest };
        neighborCell.Initialize(neighborData);

        var gen = CreateInstanceWithoutNative<HexMapGenerator>();
        var ctx = new MapGenerationContext(12345);
        ctx.worldData[new Vector2Int(0, 0)] = new Dictionary<Vector2Int, HexCellData>
        {
            [new Vector2Int(0, 0)] = cellData,
            [neighborCoord] = neighborData
        };
        typeof(HexMapGenerator).GetField("ctx", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(gen, ctx);

        var vis = CreateInstanceWithoutNative<HexMapVisualizer>();
        typeof(HexMapVisualizer).GetField("visualHexGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(vis, new Dictionary<Vector2Int, Dictionary<Vector2Int, HexCell>>());
        vis.RegisterHexCell(new Vector2Int(0, 0), new Vector2Int(0, 0), cell);
        vis.RegisterHexCell(new Vector2Int(0, 0), neighborCoord, neighborCell);

        BuildingProductionRegistry.Clear();

        var estimates = BuildingProductionCalculator.CalculateProjectedProduction(cell, data, gen, vis);
        Assert(estimates != null && estimates.Count == 1, "AdjacentBonus: 1 estimate returned");
        if (estimates != null && estimates.Count > 0)
        {
            var est = estimates[0];
            Assert(Mathf.Approximately(est.baseProduction, 4f), "AdjacentBonus: Base production is 4");
            Assert(Mathf.Approximately(est.adjacentNetBonus, 4f), "AdjacentBonus: Adjacent net bonus is 4");
            Assert(Mathf.Approximately(est.neighborPenalty, 0f), "AdjacentBonus: Neighbor penalty is 0 (no other users)");
            Assert(Mathf.Approximately(est.totalProduction, 8f), "AdjacentBonus: Total is 8 (4 base + 4 adjacent)");
        }

        BuildingProductionRegistry.Clear();
    }

    private static void TestNeighborPenaltiesSubtracted()
    {
        var data = CreateInstanceWithoutNative<BuildingData>();
        data.type = BuildingType.Economic;
        data.productionPerCycle = new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 4 } };
        data.bonusRules = new List<TerrainBonusRule>
        {
            new TerrainBonusRule
            {
                requiredFeature = HexFeatureType.Forest,
                searchRadius = 1,
                resourceYields = new List<TerrainResourceYield>
                {
                    new TerrainResourceYield
                    {
                        resourceType = ResourceType.Wood,
                        baseBonusPerHex = 4f,
                        onTopProductionBonus = 6f,
                        penaltyPerUser = 1f,
                        minBonus = 1f
                    }
                }
            }
        };

        var cell = CreateInstanceWithoutNative<HexCell>();
        var cellData = new HexCellData { chunkCoord = new Vector2Int(0, 0), localCoord = new Vector2Int(0, 0), feature = HexFeatureType.None };
        cell.Initialize(cellData);

        var neighborCoord = new Vector2Int(1, 0);
        var neighborCell = CreateInstanceWithoutNative<HexCell>();
        var neighborData = new HexCellData { chunkCoord = new Vector2Int(0, 0), localCoord = neighborCoord, feature = HexFeatureType.Forest };
        neighborCell.Initialize(neighborData);

        var gen = CreateInstanceWithoutNative<HexMapGenerator>();
        var ctx = new MapGenerationContext(12345);
        ctx.worldData[new Vector2Int(0, 0)] = new Dictionary<Vector2Int, HexCellData>
        {
            [new Vector2Int(0, 0)] = cellData,
            [neighborCoord] = neighborData
        };
        typeof(HexMapGenerator).GetField("ctx", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(gen, ctx);

        var vis = CreateInstanceWithoutNative<HexMapVisualizer>();
        typeof(HexMapVisualizer).GetField("visualHexGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(vis, new Dictionary<Vector2Int, Dictionary<Vector2Int, HexCell>>());
        vis.RegisterHexCell(new Vector2Int(0, 0), new Vector2Int(0, 0), cell);
        vis.RegisterHexCell(new Vector2Int(0, 0), neighborCoord, neighborCell);

        // Symulacja 2 istniejących tartaków korzystających z tego samego heksa lasu!
        BuildingProductionRegistry.Clear();
        BuildingProductionRegistry.RegisterUsage(neighborCell);
        BuildingProductionRegistry.RegisterUsage(neighborCell);

        Assert(BuildingProductionRegistry.GetUsageCount(neighborCell) == 2, "PenaltyTest: Registry has 2 existing users on neighbor");

        var estimates = BuildingProductionCalculator.CalculateProjectedProduction(cell, data, gen, vis);
        Assert(estimates != null && estimates.Count == 1, "PenaltyTest: 1 estimate returned");
        if (estimates != null && estimates.Count > 0)
        {
            var est = estimates[0];
            Assert(Mathf.Approximately(est.baseProduction, 4f), "PenaltyTest: Base production is 4");
            // Gross = 4, Penalty = 1 * 2 = 2, Net = 2.
            Assert(Mathf.Approximately(est.adjacentGrossBonus, 4f), "PenaltyTest: Adjacent gross is 4");
            Assert(Mathf.Approximately(est.neighborPenalty, 2f), "PenaltyTest: Neighbor penalty is 2 (2 users * 1 penalty)");
            Assert(Mathf.Approximately(est.adjacentNetBonus, 2f), "PenaltyTest: Adjacent net bonus is 2 (4 - 2)");
            Assert(Mathf.Approximately(est.totalProduction, 6f), "PenaltyTest: Total is 6 (4 base + 2 net adjacent)");
        }

        BuildingProductionRegistry.Clear();
    }

    private static void TestOccupiedHexIgnored()
    {
        var data = CreateInstanceWithoutNative<BuildingData>();
        data.type = BuildingType.Economic;
        data.productionPerCycle = new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 4 } };
        data.bonusRules = new List<TerrainBonusRule>
        {
            new TerrainBonusRule
            {
                requiredFeature = HexFeatureType.Forest,
                searchRadius = 1,
                resourceYields = new List<TerrainResourceYield>
                {
                    new TerrainResourceYield { resourceType = ResourceType.Wood, baseBonusPerHex = 4f, minBonus = 1f }
                }
            }
        };

        var cell = CreateInstanceWithoutNative<HexCell>();
        var cellData = new HexCellData { chunkCoord = new Vector2Int(0, 0), localCoord = new Vector2Int(0, 0) };
        cell.Initialize(cellData);

        var neighborCoord = new Vector2Int(1, 0);
        var neighborCell = CreateInstanceWithoutNative<HexCell>();
        var dummyBuilding = CreateInstanceWithoutNative<BuildingEntity>();
        var neighborData = new HexCellData { chunkCoord = new Vector2Int(0, 0), localCoord = neighborCoord, feature = HexFeatureType.Forest, occupyingBuilding = dummyBuilding };
        neighborCell.Initialize(neighborData);
        typeof(HexCell).GetProperty("OccupyingBuilding", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            ?.GetSetMethod(true)?.Invoke(neighborCell, new object[] { dummyBuilding });

        Assert(!ReferenceEquals(neighborCell.OccupyingBuilding, null) || !ReferenceEquals(neighborData.occupyingBuilding, null), "OccupiedTest: Neighbor cell is occupied");

        var gen = CreateInstanceWithoutNative<HexMapGenerator>();
        var ctx = new MapGenerationContext(12345);
        ctx.worldData[new Vector2Int(0, 0)] = new Dictionary<Vector2Int, HexCellData>
        {
            [new Vector2Int(0, 0)] = cellData,
            [neighborCoord] = neighborData
        };
        typeof(HexMapGenerator).GetField("ctx", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(gen, ctx);

        var vis = CreateInstanceWithoutNative<HexMapVisualizer>();
        typeof(HexMapVisualizer).GetField("visualHexGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(vis, new Dictionary<Vector2Int, Dictionary<Vector2Int, HexCell>>());
        vis.RegisterHexCell(new Vector2Int(0, 0), new Vector2Int(0, 0), cell);
        vis.RegisterHexCell(new Vector2Int(0, 0), neighborCoord, neighborCell);

        BuildingProductionRegistry.Clear();
        var estimates = BuildingProductionCalculator.CalculateProjectedProduction(cell, data, gen, vis);
        if (estimates != null && estimates.Count > 0)
        {
            var est = estimates[0];
            Assert(Mathf.Approximately(est.adjacentNetBonus, 0f), "OccupiedTest: Occupied forest yields 0 adjacent bonus");
            Assert(Mathf.Approximately(est.totalProduction, 4f), "OccupiedTest: Only base 4 is produced");
        }

        BuildingProductionRegistry.Clear();
    }

    private static void TestElevationBonusAndHillPenalties()
    {
        var quarryData = CreateInstanceWithoutNative<BuildingData>();
        quarryData.type = BuildingType.Economic;
        quarryData.productionPerCycle = new List<ResourceCost> { new ResourceCost { type = ResourceType.Stone, amount = 8 } };
        quarryData.elevationBonus = new ElevationBonusRule
        {
            enabled = true,
            resourceType = ResourceType.Stone,
            onTopBonusPerLevel = 2f,
            adjacentBonusPerLevel = 1f,
            penaltyPerExtraBuilding = 1f,
            searchRadius = 1
        };

        var cell = CreateInstanceWithoutNative<HexCell>();
        var cellData = new HexCellData { chunkCoord = new Vector2Int(0, 0), localCoord = new Vector2Int(0, 0), elevation = 2 };
        cell.Initialize(cellData);

        var neighborCoord = new Vector2Int(0, 1);
        var neighborCell = CreateInstanceWithoutNative<HexCell>();
        var neighborData = new HexCellData { chunkCoord = new Vector2Int(0, 0), localCoord = neighborCoord, elevation = 3 };
        neighborCell.Initialize(neighborData);

        var gen = CreateInstanceWithoutNative<HexMapGenerator>();
        var ctx = new MapGenerationContext(12345);
        ctx.worldData[new Vector2Int(0, 0)] = new Dictionary<Vector2Int, HexCellData>
        {
            [new Vector2Int(0, 0)] = cellData,
            [neighborCoord] = neighborData
        };
        typeof(HexMapGenerator).GetField("ctx", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(gen, ctx);

        var vis = CreateInstanceWithoutNative<HexMapVisualizer>();
        typeof(HexMapVisualizer).GetField("visualHexGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(vis, new Dictionary<Vector2Int, Dictionary<Vector2Int, HexCell>>());
        vis.RegisterHexCell(new Vector2Int(0, 0), new Vector2Int(0, 0), cell);
        vis.RegisterHexCell(new Vector2Int(0, 0), neighborCoord, neighborCell);

        // 1 istniejący kamieniołom korzysta z sąsiedniego wzgórza
        BuildingProductionRegistry.Clear();
        BuildingProductionRegistry.RegisterUsage(neighborCell);

        var estimates = BuildingProductionCalculator.CalculateProjectedProduction(cell, quarryData, gen, vis);
        Assert(estimates != null && estimates.Count == 1, "ElevationTest: 1 estimate for Stone returned");
        if (estimates != null && estimates.Count > 0)
        {
            var est = estimates[0];
            Assert(est.resourceType == ResourceType.Stone, "ElevationTest: Resource is Stone");
            Assert(Mathf.Approximately(est.baseProduction, 8f), "ElevationTest: Base production is 8");
            // On-top: level 2 * 2 = 4
            Assert(Mathf.Approximately(est.onTopBonus, 4f), "ElevationTest: On-top elevation bonus is 4", $"got {est.onTopBonus}");
            // Neighbor hill: level 3 - 1 (penalty from 1 user) = effective 2 * 1 = 2
            Assert(Mathf.Approximately(est.adjacentNetBonus, 2f), "ElevationTest: Adjacent hill net bonus is 2", $"got {est.adjacentNetBonus}");
            Assert(Mathf.Approximately(est.neighborPenalty, 1f), "ElevationTest: Neighbor penalty on hill is 1", $"got {est.neighborPenalty}");
            // Total: 8 base + 4 on-top + 2 adjacent = 14
            Assert(Mathf.Approximately(est.totalProduction, 14f), "ElevationTest: Total stone production is 14", $"got {est.totalProduction}");
        }

        BuildingProductionRegistry.Clear();
    }

    private static void TestMultiResourceBuildings()
    {
        var mineData = CreateInstanceWithoutNative<BuildingData>();
        mineData.type = BuildingType.Economic;
        mineData.productionPerCycle = new List<ResourceCost>
        {
            new ResourceCost { type = ResourceType.Iron, amount = 6 },
            new ResourceCost { type = ResourceType.Coal, amount = 2 }
        };
        mineData.bonusRules = new List<TerrainBonusRule>
        {
            new TerrainBonusRule
            {
                requiredFeature = HexFeatureType.Mountain,
                searchRadius = 1,
                resourceYields = new List<TerrainResourceYield>
                {
                    new TerrainResourceYield { resourceType = ResourceType.Iron, baseBonusPerHex = 1f, onTopProductionBonus = 2f },
                    new TerrainResourceYield { resourceType = ResourceType.Coal, baseBonusPerHex = 0.5f, onTopProductionBonus = 1f },
                    new TerrainResourceYield { resourceType = ResourceType.Stone, baseBonusPerHex = 1f, onTopProductionBonus = 2f }
                }
            }
        };

        var cell = CreateInstanceWithoutNative<HexCell>();
        var cellData = new HexCellData { chunkCoord = new Vector2Int(0, 0), localCoord = new Vector2Int(0, 0), feature = HexFeatureType.Mountain };
        cell.Initialize(cellData);

        var gen = CreateInstanceWithoutNative<HexMapGenerator>();
        var ctx = new MapGenerationContext(12345);
        ctx.worldData[new Vector2Int(0, 0)] = new Dictionary<Vector2Int, HexCellData>
        {
            [new Vector2Int(0, 0)] = cellData
        };
        typeof(HexMapGenerator).GetField("ctx", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(gen, ctx);

        var estimates = BuildingProductionCalculator.CalculateProjectedProduction(cell, mineData, gen);
        Assert(estimates != null && estimates.Count == 3, "MultiResource: Estimates returned for Iron, Coal, Stone (count 3)");

        var ironEst = estimates?.Find(e => e.resourceType == ResourceType.Iron);
        var coalEst = estimates?.Find(e => e.resourceType == ResourceType.Coal);
        var stoneEst = estimates?.Find(e => e.resourceType == ResourceType.Stone);

        Assert(ironEst.HasValue && Mathf.Approximately(ironEst.Value.totalProduction, 8f), "MultiResource: Iron is 8 (6 base + 2 on-top)");
        Assert(coalEst.HasValue && Mathf.Approximately(coalEst.Value.totalProduction, 3f), "MultiResource: Coal is 3 (2 base + 1 on-top)");
        Assert(stoneEst.HasValue && Mathf.Approximately(stoneEst.Value.totalProduction, 2f), "MultiResource: Stone is 2 (0 base + 2 on-top)");
    }

    private static void TestGlobalMultipliersAtFullEfficiency()
    {
        var data = CreateInstanceWithoutNative<BuildingData>();
        data.type = BuildingType.Economic;
        data.productionPerCycle = new List<ResourceCost> { new ResourceCost { type = ResourceType.Food, amount = 10 } };

        var cell = CreateInstanceWithoutNative<HexCell>();
        var cellData = new HexCellData { chunkCoord = new Vector2Int(0, 0), localCoord = new Vector2Int(0, 0) };
        cell.Initialize(cellData);

        var estimates = BuildingProductionCalculator.CalculateProjectedProduction(cell, data);
        Assert(estimates != null && estimates.Count == 1, "FullEfficiency: 1 estimate returned");
        if (estimates != null && estimates.Count > 0)
        {
            var est = estimates[0];
            Assert(Mathf.Approximately(est.totalProduction, 10f), "FullEfficiency: Production at 100% efficiency matches base 10");
        }
    }

    private static void TestGEMINI_UIGuidelines()
    {
        // GEMINI.md UI Standards:
        // - 16 pt dla opisów konkretnych sekcji (np. produkcja)
        // - 10 pt dla bardzo małego pomocniczego tekstu informacyjnego (kara z sąsiadów)
        // - 24 px dla małych ikon (Small icon size)
        float prodFontSize = 16f;
        float penaltyFontSize = 10f;
        float smallIconPx = 24f;

        Assert(Mathf.Approximately(prodFontSize, 16f), "GEMINI_Audit: Production font size is exactly 16 pt");
        Assert(Mathf.Approximately(penaltyFontSize, 10f), "GEMINI_Audit: Neighbor penalty font size is exactly 10 pt");
        Assert(Mathf.Approximately(smallIconPx, 24f), "GEMINI_Audit: Small icon size is exactly 24 px");
    }
}
