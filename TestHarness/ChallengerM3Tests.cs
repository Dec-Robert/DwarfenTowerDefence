using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ChallengerM3Tests
{
    private static int _passed = 0;
    private static int _failed = 0;

    private static void Assert(bool condition, string testName, string details = "")
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"[CHALLENGER-M3-PASS] {testName}");
        }
        else
        {
            _failed++;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[CHALLENGER-M3-FAIL] {testName} - {details}");
            Console.ResetColor();
        }
    }

    public static int RunAll()
    {
        Console.WriteLine("\n=================================================");
        Console.WriteLine("CHALLENGER M3: EMPIRICAL STRESS TESTS FOR TERRAIN & SPECIAL CHUNKS");
        Console.WriteLine("=================================================");

        Test1_TerrainGenerationConfigDefaults();
        Test2_DirectEnsureGuaranteesUnitSanitization();
        Test3_FullPipelineResourceGuaranteesEmpirical100Seeds();
        Test4_SpecialChunkPlacerFilteringAndExclusions50Seeds();
        Test5_SpecialChunksSeedDeterminism();
        Test6_SpecialChunkResourceApplicationOrdering();
        Test7_BatchSimulation500SeedsFullVerification();

        Console.WriteLine("\n=================================================");
        Console.WriteLine($"CHALLENGER M3 RESULTS: {_passed} PASSED, {_failed} FAILED");
        Console.WriteLine("=================================================");

        return _failed > 0 ? 1 : 0;
    }

    private static T CreateScriptableObject<T>() where T : ScriptableObject
    {
        try
        {
            return ScriptableObject.CreateInstance<T>();
        }
        catch
        {
            return (T)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(T));
        }
    }

    // -------------------------------------------------------------------------
    // Helper to generate full map data matching HexMapGenerator.BuildMapData pipeline
    // -------------------------------------------------------------------------
    private static MapGenerationContext GenerateFullMap(
        int seed,
        TerrainGenerationConfig? config = null,
        List<SpecialChunkDefinition>? specialDefinitions = null)
    {
        var ctx = new MapGenerationContext(seed);
        var pathfinder = new HexPathfinder(3);
        var registry = new ChunkRegistry(ctx, null, 8, -6, 6);
        registry.RegisterAllChunks();

        var effectiveConfig = (object)config != null ? config : TerrainGenerationConfig.CreateDefault();
        var styleSettings = new HexMapGenerator.ChunkStyleSettings();
        var topologySettings = new HexMapGenerator.RoadTopologySettings();

        var pathGen = new PathGenerator(
            ctx, registry, pathfinder,
            3, 1f, 0.02f,
            11, 40, 30,
            new int[] { 100, 85 }, new Vector2Int(2, 6),
            styleSettings, topologySettings);

        var terrainGen = new TerrainGenerator(ctx, registry, null, 3, effectiveConfig);
        var specialPlacer = new SpecialChunkPlacer(ctx, registry, specialDefinitions ?? new List<SpecialChunkDefinition>());

        Vector2Int baseRoadEndLocal = new Vector2Int(3, -2);
        terrainGen.ApplyMetaChunks(baseRoadEndLocal);

        bool pathSuccess = false;
        for (int i = 0; i < 50; i++)
        {
            if (pathGen.CalculateMainPath(baseRoadEndLocal) && pathGen.GenerateLoops())
            {
                pathGen.GenerateSideBranches();
                pathGen.RebuildAllCrossroadsInternalPaths();
                pathSuccess = true;
                break;
            }
        }

        if (!pathSuccess)
        {
            throw new InvalidOperationException($"Failed to generate paths for seed {seed}");
        }

        specialPlacer.PlaceSpecialChunks();
        terrainGen.GenerateTerrain();
        terrainGen.EnsureGuarantees();
        terrainGen.ApplySpecialChunkResources();

        return ctx;
    }

    // =========================================================================
    // 1. CONFIG DEFAULTS TEST
    // =========================================================================
    private static void Test1_TerrainGenerationConfigDefaults()
    {
        var config = TerrainGenerationConfig.CreateDefault();

        Assert((object)config != null, "Config: CreateDefault returns non-null instance");
        if ((object)config == null) return;

        Assert(config.ringRules != null && config.ringRules.Count >= 3,
            "Config: ringRules has at least 3 defined tiers (R1-2, R3-4, R5+)");

        var ruleR1 = config.GetRuleForRing(1);
        var ruleR2 = config.GetRuleForRing(2);
        var ruleR3 = config.GetRuleForRing(3);
        var ruleR4 = config.GetRuleForRing(4);
        var ruleR5 = config.GetRuleForRing(5);

        Assert(ruleR1.mountainWeight == 0 && ruleR2.mountainWeight == 0,
            "Config: Ring 1-2 rules strictly have mountainWeight == 0");

        Assert(ruleR3.mountainWeight > 0 && ruleR4.mountainWeight > 0,
            "Config: Ring 3-4 rules have mountainWeight > 0");

        Assert(ruleR1.forestWeight >= 20 && ruleR1.fertileSoilWeight >= 15 && ruleR1.hillWeight >= 10,
            "Config: Ring 1-2 rules prioritize survival resources (Forest, FertileSoil, Hill)");

        Assert(config.guarantees != null && config.guarantees.Count >= 5,
            "Config: Contains at least 5 resource guarantees");

        bool hasR12Forest = config.guarantees.Any(g => g.ringMin == 1 && g.ringMax == 2 && g.feature == HexFeatureType.Forest && g.minCount >= 10);
        bool hasR12Fertile = config.guarantees.Any(g => g.ringMin == 1 && g.ringMax == 2 && g.feature == HexFeatureType.FertileSoil && g.minCount >= 6);
        bool hasR12Hill = config.guarantees.Any(g => g.ringMin == 1 && g.ringMax == 2 && g.feature == HexFeatureType.Hill && g.minCount >= 6);
        bool hasR12MountainZero = config.guarantees.Any(g => g.ringMin == 1 && g.ringMax == 2 && g.feature == HexFeatureType.Mountain && g.maxAllowed == 0);
        bool hasR34Mountain = config.guarantees.Any(g => g.ringMin == 3 && g.ringMax == 4 && g.feature == HexFeatureType.Mountain && g.minCount >= 10 && g.minDistinctChunks >= 2);

        Assert(hasR12Forest, "Config: Guarantee R1-2 Forest >= 10 present");
        Assert(hasR12Fertile, "Config: Guarantee R1-2 FertileSoil >= 6 present");
        Assert(hasR12Hill, "Config: Guarantee R1-2 Hill >= 6 present");
        Assert(hasR12MountainZero, "Config: Guarantee R1-2 Mountain == 0 (Zero Tolerance) present");
        Assert(hasR34Mountain, "Config: Guarantee R3-4 Mountain >= 10 across >= 2 chunks present");
    }

    // =========================================================================
    // 2. DIRECT ENSURE GUARANTEES UNIT TEST
    // =========================================================================
    private static void Test2_DirectEnsureGuaranteesUnitSanitization()
    {
        var ctx = new MapGenerationContext(999);
        var config = TerrainGenerationConfig.CreateDefault();

        // Create artificial chunks:
        // Chunk (1, 0) in Ring 1
        // Chunk (2, 0) in Ring 2
        // Chunk (3, 0) in Ring 3
        // Chunk (4, 0) in Ring 4
        var testChunks = new List<Vector2Int>
        {
            new Vector2Int(1, 0),
            new Vector2Int(2, 0),
            new Vector2Int(3, 0),
            new Vector2Int(4, 0)
        };

        ctx.allValidChunks.UnionWith(testChunks);

        foreach (var c in testChunks)
        {
            ctx.worldData[c] = new Dictionary<Vector2Int, HexCellData>();
            for (int q = -3; q <= 3; q++)
            {
                int r1 = Math.Max(-3, -q - 3);
                int r2 = Math.Min(3, -q + 3);
                for (int r = r1; r <= r2; r++)
                {
                    var local = new Vector2Int(q, r);
                    ctx.worldData[c][local] = new HexCellData
                    {
                        chunkCoord = c,
                        localCoord = local,
                        isPath = false,
                        feature = HexFeatureType.None
                    };
                }
            }
        }

        // Intentionally pollute Ring 1 and 2 with illegal Mountains
        ctx.worldData[new Vector2Int(1, 0)][Vector2Int.zero].feature = HexFeatureType.Mountain;
        ctx.worldData[new Vector2Int(1, 0)][new Vector2Int(1, 0)].feature = HexFeatureType.Mountain;
        ctx.worldData[new Vector2Int(2, 0)][Vector2Int.zero].feature = HexFeatureType.Mountain;

        // Mark a cell as road in (1, 0)
        var roadCell = new Vector2Int(-1, 0);
        ctx.worldData[new Vector2Int(1, 0)][roadCell].isPath = true;

        TerrainGenerator.EnsureGuarantees(ctx, config, null);

        // Verify Ring 1-2 mountains were wiped
        int r12Mountains = 0;
        int r12Forest = 0;
        int r12Fertile = 0;
        int r12Hill = 0;

        foreach (var c in new[] { new Vector2Int(1, 0), new Vector2Int(2, 0) })
        {
            foreach (var cell in ctx.worldData[c].Values)
            {
                if (cell.feature == HexFeatureType.Mountain) r12Mountains++;
                if (cell.feature == HexFeatureType.Forest) r12Forest++;
                if (cell.feature == HexFeatureType.FertileSoil) r12Fertile++;
                if (cell.feature == HexFeatureType.Hill) r12Hill++;
            }
        }

        Assert(r12Mountains == 0, "EnsureGuarantees Unit: All illegal mountains in Ring 1-2 purged to 0");
        Assert(r12Forest >= 10, $"EnsureGuarantees Unit: Forest topped up to >= 10 (actual: {r12Forest})");
        Assert(r12Fertile >= 6, $"EnsureGuarantees Unit: FertileSoil topped up to >= 6 (actual: {r12Fertile})");
        Assert(r12Hill >= 6, $"EnsureGuarantees Unit: Hill topped up to >= 6 (actual: {r12Hill})");

        // Verify Ring 3-4 mountains
        int r34Mountains = 0;
        var r34ChunksWithMountain = new HashSet<Vector2Int>();
        foreach (var c in new[] { new Vector2Int(3, 0), new Vector2Int(4, 0) })
        {
            foreach (var cell in ctx.worldData[c].Values)
            {
                if (cell.feature == HexFeatureType.Mountain)
                {
                    r34Mountains++;
                    r34ChunksWithMountain.Add(c);
                }
            }
        }

        Assert(r34Mountains >= 10, $"EnsureGuarantees Unit: Mountain in R3-4 >= 10 (actual: {r34Mountains})");
        Assert(r34ChunksWithMountain.Count >= 2, $"EnsureGuarantees Unit: Mountain in R3-4 spread across >= 2 chunks (actual: {r34ChunksWithMountain.Count})");

        // Verify road cell was untouched
        Assert(ctx.worldData[new Vector2Int(1, 0)][roadCell].isPath, "EnsureGuarantees Unit: Road cell isPath preserved");
        Assert(ctx.worldData[new Vector2Int(1, 0)][roadCell].feature == HexFeatureType.None, "EnsureGuarantees Unit: Road cell not overwritten by feature");
    }

    // =========================================================================
    // 3. EMPIRICAL STRESS TEST (100 SEEDS)
    // =========================================================================
    private static void Test3_FullPipelineResourceGuaranteesEmpirical100Seeds()
    {
        const int testCount = 100;
        bool allR12MountainsZero = true;
        bool allR12ForestOk = true;
        bool allR12FertileOk = true;
        bool allR12HillOk = true;
        bool allR34MountainCountOk = true;
        bool allR34MountainChunksOk = true;
        bool allRoadsUntouched = true;

        string failureDetail = "";

        for (int seed = 1001; seed <= 1001 + testCount - 1; seed++)
        {
            var ctx = GenerateFullMap(seed);

            int r12Forest = 0;
            int r12Fertile = 0;
            int r12Hill = 0;
            int r12Mountain = 0;

            int r34Mountain = 0;
            var r34MountainChunks = new HashSet<Vector2Int>();

            foreach (var chunkCoord in ctx.allValidChunks)
            {
                if (chunkCoord == Vector2Int.zero) continue; // Base meta chunk
                if (ctx.chunkTypes.TryGetValue(chunkCoord, out var ct) && ct == ChunkType.Special) continue;

                int ring = HexGridMath.GetDistance(Vector2Int.zero, chunkCoord);
                if (!ctx.worldData.ContainsKey(chunkCoord)) continue;

                var cells = ctx.worldData[chunkCoord];
                foreach (var cell in cells.Values)
                {
                    // Check road immunity
                    if (cell.isPath && cell.feature != HexFeatureType.None)
                    {
                        allRoadsUntouched = false;
                        failureDetail = $"Seed {seed}: Road cell at chunk {chunkCoord} has feature {cell.feature}!";
                    }

                    if (ring >= 1 && ring <= 2)
                    {
                        if (cell.feature == HexFeatureType.Mountain) r12Mountain++;
                        if (cell.feature == HexFeatureType.Forest) r12Forest++;
                        if (cell.feature == HexFeatureType.FertileSoil) r12Fertile++;
                        if (cell.feature == HexFeatureType.Hill) r12Hill++;
                    }
                    else if (ring >= 3 && ring <= 4)
                    {
                        if (cell.feature == HexFeatureType.Mountain)
                        {
                            r34Mountain++;
                            r34MountainChunks.Add(chunkCoord);
                        }
                    }
                }
            }

            if (r12Mountain > 0)
            {
                allR12MountainsZero = false;
                failureDetail = $"Seed {seed}: Ring 1-2 has {r12Mountain} mountains (must be 0)!";
                break;
            }

            if (r12Forest < 10)
            {
                allR12ForestOk = false;
                failureDetail = $"Seed {seed}: Ring 1-2 Forest count = {r12Forest} < 10!";
                break;
            }

            if (r12Fertile < 6)
            {
                allR12FertileOk = false;
                failureDetail = $"Seed {seed}: Ring 1-2 FertileSoil count = {r12Fertile} < 6!";
                break;
            }

            if (r12Hill < 6)
            {
                allR12HillOk = false;
                failureDetail = $"Seed {seed}: Ring 1-2 Hill count = {r12Hill} < 6!";
                break;
            }

            if (r34Mountain < 10)
            {
                allR34MountainCountOk = false;
                failureDetail = $"Seed {seed}: Ring 3-4 Mountain count = {r34Mountain} < 10!";
                break;
            }

            if (r34MountainChunks.Count < 2)
            {
                allR34MountainChunksOk = false;
                failureDetail = $"Seed {seed}: Ring 3-4 Mountain distinct chunks = {r34MountainChunks.Count} < 2!";
                break;
            }
        }

        Assert(allR12MountainsZero, $"M3 Empirical (100 seeds): Ring 1-2 Mountain strictly 0 (Zero Tolerance)", failureDetail);
        Assert(allR12ForestOk, $"M3 Empirical (100 seeds): Ring 1-2 Forest >= 10 on all seeds", failureDetail);
        Assert(allR12FertileOk, $"M3 Empirical (100 seeds): Ring 1-2 FertileSoil >= 6 on all seeds", failureDetail);
        Assert(allR12HillOk, $"M3 Empirical (100 seeds): Ring 1-2 Hill >= 6 on all seeds", failureDetail);
        Assert(allR34MountainCountOk, $"M3 Empirical (100 seeds): Ring 3-4 Mountain >= 10 on all seeds", failureDetail);
        Assert(allR34MountainChunksOk, $"M3 Empirical (100 seeds): Ring 3-4 Mountain across >= 2 distinct chunks on all seeds", failureDetail);
        Assert(allRoadsUntouched, $"M3 Empirical (100 seeds): Road hexes never overwritten by natural features", failureDetail);
    }

    // =========================================================================
    // 4. SPECIAL CHUNK PLACER FILTERING & EXCLUSIONS (50 SEEDS)
    // =========================================================================
    private static void Test4_SpecialChunkPlacerFilteringAndExclusions50Seeds()
    {
        const int testCount = 50;

        // Create 3 mock special chunk definitions
        var defRuins = CreateScriptableObject<SpecialChunkDefinition>();
        defRuins.chunkId = "ancient_ruins";
        defRuins.spawnChance = 100;
        defRuins.distanceRange = new Vector2Int(3, 8);
        defRuins.uniquePerMap = true;

        var defMines = CreateScriptableObject<SpecialChunkDefinition>();
        defMines.chunkId = "abandoned_mines";
        defMines.spawnChance = 100;
        defMines.distanceRange = new Vector2Int(4, 7);
        defMines.uniquePerMap = true;

        var defShrine = CreateScriptableObject<SpecialChunkDefinition>();
        defShrine.chunkId = "mystic_shrine";
        defShrine.spawnChance = 100;
        defShrine.distanceRange = new Vector2Int(3, 6);
        defShrine.uniquePerMap = true;

        var testDefs = new List<SpecialChunkDefinition> { defRuins, defMines, defShrine };

        bool allInRings3To8 = true;
        bool allOutsidePaths = true;
        bool allOutsideLoops = true;
        bool allFarFromSpawners = true;
        bool allUnique = true;
        string failDetail = "";

        for (int seed = 2001; seed <= 2001 + testCount - 1; seed++)
        {
            var ctx = GenerateFullMap(seed, null, testDefs);

            var seenIds = new HashSet<string>();

            foreach (var kvp in ctx.specialChunkData)
            {
                var coord = kvp.Key;
                var def = kvp.Value;
                int dist = HexGridMath.GetDistance(Vector2Int.zero, coord);

                // 1. Ring bounds [3, 8]
                if (dist < 3 || dist > 8)
                {
                    allInRings3To8 = false;
                    failDetail = $"Seed {seed}: Special chunk {def.chunkId} at {coord} has distance {dist} outside [3, 8]";
                    break;
                }

                // 2. Outside paths
                if (ctx.chunkPaths.ContainsKey(coord) || (ctx.generatedChunkSequence != null && ctx.generatedChunkSequence.Contains(coord)))
                {
                    allOutsidePaths = false;
                    failDetail = $"Seed {seed}: Special chunk {def.chunkId} at {coord} collides with path!";
                    break;
                }

                // 3. Outside loops & crossroads
                if (ctx.crossroadsChunks != null && ctx.crossroadsChunks.Contains(coord))
                {
                    allOutsideLoops = false;
                    failDetail = $"Seed {seed}: Special chunk {def.chunkId} at {coord} is a crossroads chunk!";
                    break;
                }

                if (ctx.loops != null)
                {
                    foreach (var loop in ctx.loops)
                    {
                        if (loop.splitCrossroads == coord || loop.mergeCrossroads == coord ||
                            (loop.loopChunks != null && loop.loopChunks.Contains(coord)) ||
                            (loop.mainSegmentChunks != null && loop.mainSegmentChunks.Contains(coord)))
                        {
                            allOutsideLoops = false;
                            failDetail = $"Seed {seed}: Special chunk {def.chunkId} at {coord} collides with loop {loop.splitCrossroads}->{loop.mergeCrossroads}!";
                            break;
                        }
                    }
                }
                if (!allOutsideLoops) break;

                // 4. Away from spawners (dist > 1)
                if (ctx.mainSpawnerChunk != Vector2Int.zero && HexGridMath.GetDistance(coord, ctx.mainSpawnerChunk) <= 1)
                {
                    allFarFromSpawners = false;
                    failDetail = $"Seed {seed}: Special chunk {def.chunkId} at {coord} is adjacent (dist <= 1) to main spawner {ctx.mainSpawnerChunk}!";
                    break;
                }

                if (ctx.extraSpawnerChunks != null)
                {
                    foreach (var extra in ctx.extraSpawnerChunks)
                    {
                        if (HexGridMath.GetDistance(coord, extra) <= 1)
                        {
                            allFarFromSpawners = false;
                            failDetail = $"Seed {seed}: Special chunk {def.chunkId} at {coord} is adjacent (dist <= 1) to extra spawner {extra}!";
                            break;
                        }
                    }
                }
                if (!allFarFromSpawners) break;

                // 5. Unique check
                if (def.uniquePerMap)
                {
                    if (!seenIds.Add(def.chunkId))
                    {
                        allUnique = false;
                        failDetail = $"Seed {seed}: Duplicate placement of unique special chunk {def.chunkId}!";
                        break;
                    }
                }
            }

            if (!allInRings3To8 || !allOutsidePaths || !allOutsideLoops || !allFarFromSpawners || !allUnique)
                break;
        }

        Assert(allInRings3To8, "SpecialChunkPlacer: Chunks placed strictly in rings 3-8 across 50 seeds", failDetail);
        Assert(allOutsidePaths, "SpecialChunkPlacer: Chunks strictly excluded from all enemy paths", failDetail);
        Assert(allOutsideLoops, "SpecialChunkPlacer: Chunks strictly excluded from loops and crossroads", failDetail);
        Assert(allFarFromSpawners, "SpecialChunkPlacer: Chunks strictly excluded from spawners and spawner neighbors (dist > 1)", failDetail);
        Assert(allUnique, "SpecialChunkPlacer: Unique definitions never duplicated per map", failDetail);
    }

    // =========================================================================
    // 5. SEED DETERMINISM
    // =========================================================================
    private static void Test5_SpecialChunksSeedDeterminism()
    {
        var defRuins = CreateScriptableObject<SpecialChunkDefinition>();
        defRuins.chunkId = "ancient_ruins";
        defRuins.spawnChance = 100;
        defRuins.distanceRange = new Vector2Int(3, 8);
        defRuins.uniquePerMap = true;

        var defMines = CreateScriptableObject<SpecialChunkDefinition>();
        defMines.chunkId = "abandoned_mines";
        defMines.spawnChance = 100;
        defMines.distanceRange = new Vector2Int(4, 7);
        defMines.uniquePerMap = true;

        var defs = new List<SpecialChunkDefinition> { defRuins, defMines };

        int testSeed = 54321;
        var map1 = GenerateFullMap(testSeed, null, defs);
        var map2 = GenerateFullMap(testSeed, null, defs);

        bool identicalPlacements = true;
        string diffDetail = "";

        if (map1.specialChunkData.Count != map2.specialChunkData.Count)
        {
            identicalPlacements = false;
            diffDetail = $"Count mismatch: {map1.specialChunkData.Count} vs {map2.specialChunkData.Count}";
        }
        else
        {
            foreach (var kvp in map1.specialChunkData)
            {
                if (!map2.specialChunkData.TryGetValue(kvp.Key, out var def2) || def2.chunkId != kvp.Value.chunkId)
                {
                    identicalPlacements = false;
                    diffDetail = $"Chunk {kvp.Key} mismatch in run 2";
                    break;
                }
            }
        }

        Assert(identicalPlacements, "SpecialChunkPlacer Determinism: Identical seed produces identical special chunk placements", diffDetail);
    }

    // =========================================================================
    // 6. SPECIAL CHUNK RESOURCE ORDERING
    // =========================================================================
    private static void Test6_SpecialChunkResourceApplicationOrdering()
    {
        var def = CreateScriptableObject<SpecialChunkDefinition>();
        def.chunkId = "resource_haven";
        def.spawnChance = 100;
        def.distanceRange = new Vector2Int(3, 5);
        def.uniquePerMap = true;
        def.guaranteedResources = new List<SpecialChunkDefinition.ResourceDeposit>
        {
            new SpecialChunkDefinition.ResourceDeposit { featureType = HexFeatureType.Forest, count = 3 },
            new SpecialChunkDefinition.ResourceDeposit { featureType = HexFeatureType.Hill, count = 2 }
        };

        var map = GenerateFullMap(777, null, new List<SpecialChunkDefinition> { def });

        var specialCoord = map.specialChunkData.Keys.FirstOrDefault();
        Assert(specialCoord != Vector2Int.zero, "Special Chunk Resources: Special chunk successfully placed");

        if (specialCoord != Vector2Int.zero && map.worldData.TryGetValue(specialCoord, out var cells))
        {
            int forestCount = cells.Values.Count(c => c.feature == HexFeatureType.Forest);
            int hillCount = cells.Values.Count(c => c.feature == HexFeatureType.Hill);

            Assert(forestCount >= 3, $"Special Chunk Resources: Guaranteed Forest deposit applied (count = {forestCount} >= 3)");
            Assert(hillCount >= 2, $"Special Chunk Resources: Guaranteed Hill deposit applied (count = {hillCount} >= 2)");
            Assert(cells.Values.All(c => !c.isPath || c.feature == HexFeatureType.None), "Special Chunk Resources: Road cells remain unpolluted");
        }
    }

    // =========================================================================
    // 7. 500-SEED BATCH SIMULATION (PHASE 4 VERIFICATION)
    // =========================================================================
    private static void Test7_BatchSimulation500SeedsFullVerification()
    {
        const int totalSeeds = 500;
        int passedSeeds = 0;
        string firstFail = "";

        for (int seed = 1; seed <= totalSeeds; seed++)
        {
            var ctx = GenerateFullMap(seed);

            // 1. Main path length >= 11
            if (ctx.generatedChunkSequence == null || ctx.generatedChunkSequence.Count < 11)
            {
                firstFail = $"Seed {seed}: Main path length {ctx.generatedChunkSequence?.Count ?? 0} < 11";
                break;
            }

            // 2. First 2 start chunks on main path (closest to base: indices Count - 1 and Count - 2): no crossroads & internalPath.Count >= 6
            int seqCount = ctx.generatedChunkSequence.Count;
            var c1 = ctx.generatedChunkSequence[seqCount - 1];
            var c2 = ctx.generatedChunkSequence[seqCount - 2];
            if (!ctx.chunkPaths.TryGetValue(c1, out var pd1) || pd1.isCrossroads || pd1.internalPath == null || pd1.internalPath.Count < 6)
            {
                firstFail = $"Seed {seed}: Start chunk #1 ({c1}) failed (isCrossroads={pd1?.isCrossroads}, hexes={pd1?.internalPath?.Count ?? 0})";
                break;
            }
            if (!ctx.chunkPaths.TryGetValue(c2, out var pd2) || pd2.isCrossroads || pd2.internalPath == null || pd2.internalPath.Count < 6)
            {
                firstFail = $"Seed {seed}: Start chunk #2 ({c2}) failed (isCrossroads={pd2?.isCrossroads}, hexes={pd2?.internalPath?.Count ?? 0})";
                break;
            }

            // 3. Loops >= 1
            if (ctx.loops == null || ctx.loops.Count < 1)
            {
                firstFail = $"Seed {seed}: Loops count = {ctx.loops?.Count ?? 0} < 1";
                break;
            }

            // 4. Resource guarantees in R1-2 and R3-4
            int r12Forest = 0, r12Fertile = 0, r12Hill = 0, r12Mountain = 0, r34Mountain = 0;
            var r34MtnChunks = new HashSet<Vector2Int>();

            foreach (var coord in ctx.allValidChunks)
            {
                if (coord == Vector2Int.zero) continue;
                int dist = HexGridMath.GetDistance(Vector2Int.zero, coord);
                if (!ctx.worldData.TryGetValue(coord, out var cells)) continue;

                foreach (var cell in cells.Values)
                {
                    if (dist >= 1 && dist <= 2)
                    {
                        if (cell.feature == HexFeatureType.Forest) r12Forest++;
                        else if (cell.feature == HexFeatureType.FertileSoil) r12Fertile++;
                        else if (cell.feature == HexFeatureType.Hill) r12Hill++;
                        else if (cell.feature == HexFeatureType.Mountain) r12Mountain++;
                    }
                    else if (dist >= 3 && dist <= 4)
                    {
                        if (cell.feature == HexFeatureType.Mountain)
                        {
                            r34Mountain++;
                            r34MtnChunks.Add(coord);
                        }
                    }
                }
            }

            if (r12Forest < 10 || r12Fertile < 6 || r12Hill < 6 || r12Mountain != 0 || r34Mountain < 10 || r34MtnChunks.Count < 2)
            {
                firstFail = $"Seed {seed}: Resource guarantee failed (R1-2 F={r12Forest}, S={r12Fertile}, H={r12Hill}, M={r12Mountain}; R3-4 M={r34Mountain} in {r34MtnChunks.Count} chunks)";
                break;
            }

            passedSeeds++;
        }

        Assert(passedSeeds == totalSeeds, $"Phase 4 Batch Simulation: 500/500 seeds passed all path, start-zone (>=6 hexes), loop, and resource invariants (passed: {passedSeeds}/{totalSeeds})", firstFail);
    }
}
