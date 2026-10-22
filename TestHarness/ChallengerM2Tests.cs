using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ChallengerM2Tests
{
    private static int _passed = 0;
    private static int _failed = 0;

    private static void Assert(bool condition, string testName, string details = "")
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"[CHALLENGER-PASS] {testName}");
        }
        else
        {
            _failed++;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[CHALLENGER-FAIL] {testName} - {details}");
            Console.ResetColor();
        }
    }

    public static int RunAll()
    {
        Console.WriteLine("\n=================================================");
        Console.WriteLine("CHALLENGER 2: EMPIRICAL STRESS TESTS FOR M2");
        Console.WriteLine("=================================================");

        Test1_FlowWeightsSumToOneHundredSeeds();
        Test2_Night1SpawnerConsolidation();
        Test3_LoopInFogBoundaryAndZeroEnemiesInFog();
        Test4_FullyRevealedLoopTrafficSplit70_30();
        Test5_MapExpansionManagerRozdroze2Unlock();

        Console.WriteLine("\n=================================================");
        Console.WriteLine($"CHALLENGER 2 RESULTS: {_passed} PASSED, {_failed} FAILED");
        Console.WriteLine("=================================================");

        return _failed > 0 ? 1 : 0;
    }

    // -------------------------------------------------------------------------
    // Helper to generate a fully valid map for a given seed
    // -------------------------------------------------------------------------
    private static (MapGenerationContext ctx, PathGenerator pathGen, HexMapGenerator.RoadTopologySettings top)
        GenerateMap(int seed)
    {
        var ctx = new MapGenerationContext(seed);
        var pathfinder = new HexPathfinder(3);
        var registry = new ChunkRegistry(ctx, null, 8, -6, 6);
        registry.RegisterAllChunks();

        var styleSettings = new HexMapGenerator.ChunkStyleSettings();
        var topologySettings = new HexMapGenerator.RoadTopologySettings();

        var pathGen = new PathGenerator(
            ctx, registry, pathfinder,
            3, 1f, 0.02f,
            11, 40, 30,
            new int[] { 100, 85 }, new Vector2Int(2, 6),
            styleSettings, topologySettings);

        for (int attempt = 0; attempt < 50; attempt++)
        {
            if (pathGen.CalculateMainPath(new Vector2Int(3, -2)))
            {
                pathGen.GenerateLoops();
                pathGen.GenerateSideBranches();
                pathGen.RebuildAllCrossroadsInternalPaths();
                return (ctx, pathGen, topologySettings);
            }
        }

        throw new InvalidOperationException($"Could not generate valid map for seed {seed}");
    }

    // -------------------------------------------------------------------------
    // 1. Flow weights: sum across GetAllSpawnRoutes() equals 1.000 +/- 0.001
    // -------------------------------------------------------------------------
    private static void Test1_FlowWeightsSumToOneHundredSeeds()
    {
        bool allPassed = true;
        string detail = "";
        int testCount = 100;

        for (int seed = 1; seed <= testCount; seed++)
        {
            var (ctx, pathGen, _) = GenerateMap(seed);
            var routes = pathGen.GetAllSpawnRoutes(new Vector2Int(3, -2));

            if (routes == null || routes.Count == 0)
            {
                allPassed = false;
                detail = $"Seed {seed}: No routes returned!";
                break;
            }

            float sum = routes.Sum(r => r.flowWeight);
            if (Math.Abs(sum - 1.000f) > 0.001f)
            {
                allPassed = false;
                detail = $"Seed {seed}: sum of flowWeight = {sum:F6}, expected 1.000 +/- 0.001 (routes: {routes.Count})";
                break;
            }

            foreach (var r in routes)
            {
                if (r.flowWeight <= 0f || r.flowWeight > 1.0f)
                {
                    allPassed = false;
                    detail = $"Seed {seed}: Route '{r.name}' has invalid flowWeight {r.flowWeight}";
                    break;
                }
            }
            if (!allPassed) break;
        }

        Assert(allPassed, "Challenge 1: Sum of flowWeight across GetAllSpawnRoutes() equals 1.000 +/- 0.001 across 100 seeds", detail);
    }

    // -------------------------------------------------------------------------
    // 2. Night 1 Spawner consolidation:
    //    simulate initial game state (only base revealed, crossroads in fog),
    //    assert exactly 1 active spawner exists with currentSpawnChance == 100%.
    // -------------------------------------------------------------------------
    private static void Test2_Night1SpawnerConsolidation()
    {
        int[] testSeeds = { 7, 23, 42, 99, 1337 };
        bool allConsolidated = true;
        string detail = "";

        foreach (int seed in testSeeds)
        {
            var (ctx, pathGen, _) = GenerateMap(seed);
            var routes = pathGen.GetAllSpawnRoutes(new Vector2Int(3, -2));

            // Initial game state according to MapExpansionManager.Initialize():
            // Base (0,0) and the initial road chunk (Gate Chunk = chunkSequence[N-1]) are Settled.
            // Crossroads and spawners are far away in Wilderness/fog.
            int N = ctx.generatedChunkSequence.Count;
            Vector2Int gateChunk = ctx.generatedChunkSequence[N - 1];

            var revealedChunks = new HashSet<Vector2Int>
            {
                Vector2Int.zero,
                gateChunk
            };

            // Execute the route scanning and merging logic of EnemySpawner.RecalculateActiveRoutes()
            var activeRoutes = SimulateEnemySpawnerRecalculate(routes, revealedChunks, 3, 1f, 0.02f, ctx.allValidChunks);

            if (activeRoutes.Count != 1)
            {
                allConsolidated = false;
                detail = $"Seed {seed}: Expected exactly 1 active route on Night 1, but got {activeRoutes.Count}";
                break;
            }

            if (Math.Abs(activeRoutes[0].currentSpawnChance - 100.0f) > 0.001f)
            {
                allConsolidated = false;
                detail = $"Seed {seed}: Active route chance is {activeRoutes[0].currentSpawnChance}%, expected 100%";
                break;
            }

            // Assert spawn point is on Gate Chunk (the frontier boundary facing the fog)
            Vector2Int spawnChunk = GetChunkCoordFromWorldPos(activeRoutes[0].currentSpawnPoint, ctx.allValidChunks, 3, 1f, 0.02f);
            if (spawnChunk != gateChunk)
            {
                allConsolidated = false;
                detail = $"Seed {seed}: Spawn point chunk is {spawnChunk}, expected Gate Chunk {gateChunk}";
                break;
            }
        }

        Assert(allConsolidated, "Challenge 2: Night 1 Spawner consolidation produces exactly 1 active spawner with 100% chance on Gate Chunk fog boundary", detail);
    }

    // -------------------------------------------------------------------------
    // 3. Loop in fog:
    //    simulate Rozdroże 1 revealed with loop hidden in fog,
    //    assert active spawner rests on Rozdroże 1 fog boundary with 30% spawn chance,
    //    and 0 enemies enter fog.
    // -------------------------------------------------------------------------
    private static void Test3_LoopInFogBoundaryAndZeroEnemiesInFog()
    {
        // Test with maps that have guaranteed 1 loop and no side branches to isolate loop flow = exactly 30%
        int[] testSeeds = { 5, 12, 42, 77, 88 };
        bool allLoopFogOk = true;
        string detail = "";

        foreach (int seed in testSeeds)
        {
            var ctx = new MapGenerationContext(seed);
            var pathfinder = new HexPathfinder(3);
            var registry = new ChunkRegistry(ctx, null, 8, -6, 6);
            registry.RegisterAllChunks();

            var styleSettings = new HexMapGenerator.ChunkStyleSettings();
            var topologySettings = new HexMapGenerator.RoadTopologySettings
            {
                guaranteedLoops = 1,
                extraLoopChance = 0, // exactly 1 loop
                loopFlowRatio = 0.30f
            };

            var pathGen = new PathGenerator(
                ctx, registry, pathfinder,
                3, 1f, 0.02f,
                11, 40, 30,
                new int[0], new Vector2Int(2, 6), // 0 side branches
                styleSettings, topologySettings);

            bool generated = false;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                if (pathGen.CalculateMainPath(new Vector2Int(3, -2)))
                {
                    pathGen.GenerateLoops();
                    pathGen.RebuildAllCrossroadsInternalPaths();
                    generated = true;
                    break;
                }
            }

            if (!generated || ctx.loops.Count == 0) continue;

            var loop = ctx.loops[0];
            var routes = pathGen.GetAllSpawnRoutes(new Vector2Int(3, -2));

            // Chunks revealed: Base, all Path A chunks (including Rozdroże 1, Rozdroże 2, and Spawner).
            // Loop chunks (loop.loopChunks) are strictly hidden in FOG!
            var revealed = new HashSet<Vector2Int>(ctx.allValidChunks);
            foreach (var lc in loop.loopChunks)
            {
                revealed.Remove(lc);
            }

            var activeRoutes = SimulateEnemySpawnerRecalculate(routes, revealed, 3, 1f, 0.02f, ctx.allValidChunks);

            // Find the active route corresponding to the loop
            var loopActive = activeRoutes.FirstOrDefault(r => r.name.Contains($"via {loop.splitCrossroads}"));
            if (loopActive == null)
            {
                allLoopFogOk = false;
                detail = $"Seed {seed}: Could not find active route for loop in fog!";
                break;
            }

            // Assert active spawner rests on Rozdroże 1 fog boundary
            Vector2Int spawnChunk = GetChunkCoordFromWorldPos(loopActive.currentSpawnPoint, ctx.allValidChunks, 3, 1f, 0.02f);
            if (spawnChunk != loop.mergeCrossroads)
            {
                allLoopFogOk = false;
                detail = $"Seed {seed}: Loop spawner chunk is {spawnChunk}, expected Rozdroże 1 {loop.mergeCrossroads}";
                break;
            }

            // Assert spawn chance is exactly 30% (+/- 0.001f)
            if (Math.Abs(loopActive.currentSpawnChance - 30.0f) > 0.001f)
            {
                allLoopFogOk = false;
                detail = $"Seed {seed}: Loop spawn chance is {loopActive.currentSpawnChance:F2}%, expected 30.00%";
                break;
            }

            // Assert 0 enemies enter fog: all active path waypoints are in revealed chunks, none in loopChunks
            foreach (var pt in loopActive.currentActivePath)
            {
                Vector2Int c = GetChunkCoordFromWorldPos(pt, ctx.allValidChunks, 3, 1f, 0.02f);
                if (!revealed.Contains(c))
                {
                    allLoopFogOk = false;
                    detail = $"Seed {seed}: Enemy active path traverses foggy chunk {c}!";
                    break;
                }
                if (loop.loopChunks.Contains(c))
                {
                    allLoopFogOk = false;
                    detail = $"Seed {seed}: Enemy active path enters unrevealed loop chunk {c}!";
                    break;
                }
            }
            if (!allLoopFogOk) break;
        }

        Assert(allLoopFogOk, "Challenge 3: Loop in fog spawns at Rozdroże 1 boundary with exactly 30% chance and ZERO enemies enter fog", detail);
    }

    // -------------------------------------------------------------------------
    // 4. Fully revealed loop: assert traffic divides 70/30 at Rozdroże 2
    // -------------------------------------------------------------------------
    private static void Test4_FullyRevealedLoopTrafficSplit70_30()
    {
        int[] testSeeds = { 3, 15, 42, 61, 84 };
        bool allSplitOk = true;
        string detail = "";

        foreach (int seed in testSeeds)
        {
            var (ctx, pathGen, top) = GenerateMap(seed);
            if (ctx.loops.Count == 0) continue;

            // Sort loops by splitIdx (lowest splitIdx = closest to spawner, where 100% spawner traffic enters)
            var loop = ctx.loops.OrderBy(l => ctx.generatedChunkSequence.IndexOf(l.splitCrossroads)).First();
            var routes = pathGen.GetAllSpawnRoutes(new Vector2Int(3, -2));

            // Fully reveal all chunks: Base, Path A, loops, side branches
            var allRevealed = new HashSet<Vector2Int>(ctx.allValidChunks);

            var activeRoutes = SimulateEnemySpawnerRecalculate(routes, allRevealed, 3, 1f, 0.02f, ctx.allValidChunks);

            var loopRoute = activeRoutes.FirstOrDefault(r => r.name.Contains($"via {loop.splitCrossroads}"));
            if (loopRoute == null)
            {
                allSplitOk = false;
                detail = $"Seed {seed}: Missing loop route in fully revealed map";
                break;
            }

            // At Rozdroże 2 (loop.splitCrossroads), routes passing through are:
            // 1. loopRoute (takes the loop detour via extraExits)
            // 2. all other routes from main spawner that stay on Path A (mainRoute + any other downstream loops)
            var continuingRoutes = activeRoutes.Where(r => r.fullPath != null &&
                r != loopRoute &&
                !r.name.Contains("Branch") &&
                r.currentActivePath.Any(pt => GetChunkCoordFromWorldPos(pt, ctx.allValidChunks, 3, 1f, 0.02f) == loop.splitCrossroads)).ToList();

            float continuingFlow = continuingRoutes.Sum(r => r.flowWeight);
            float loopFlow = loopRoute.flowWeight;
            float totalAtRozdroze2 = continuingFlow + loopFlow;

            float loopRatio = loopFlow / totalAtRozdroze2;
            float mainRatio = continuingFlow / totalAtRozdroze2;

            if (Math.Abs(loopRatio - 0.30f) > 0.001f || Math.Abs(mainRatio - 0.70f) > 0.001f)
            {
                allSplitOk = false;
                detail = $"Seed {seed}: Split at Rozdroże 2 is {mainRatio * 100:F1}% / {loopRatio * 100:F1}%, expected 70.0% / 30.0%";
                break;
            }
        }

        Assert(allSplitOk, "Challenge 4: Fully revealed loop traffic divides 70/30 at Rozdroże 2", detail);
    }

    // -------------------------------------------------------------------------
    // 5. MapExpansionManager: assert Rozdroże 2 unlocks to Borderlands when
    //    either predecessor path is settled/outskirts.
    // -------------------------------------------------------------------------
    private static void Test5_MapExpansionManagerRozdroze2Unlock()
    {
        int[] testSeeds = { 7, 21, 42, 73, 95 };
        bool allExpansionOk = true;
        string detail = "";

        foreach (int seed in testSeeds)
        {
            var (ctx, pathGen, _) = GenerateMap(seed);
            if (ctx.loops.Count == 0) continue;

            var loop = ctx.loops[0];
            Vector2Int cr2 = loop.splitCrossroads;

            var multiDeps = pathGen.GetRoadRevealDependenciesMulti();
            if (!multiDeps.TryGetValue(cr2, out var predecessors) || predecessors.Count < 2)
            {
                allExpansionOk = false;
                detail = $"Seed {seed}: Rozdroże 2 {cr2} does not have >= 2 road predecessors!";
                break;
            }

            Vector2Int predA = predecessors[0]; // Predecessor on Path A
            Vector2Int predB = predecessors[1]; // Predecessor on Loop

            // Setup active chunks dictionary simulating MapExpansionManager
            var activeChunks = new Dictionary<Vector2Int, ChunkStateData>();
            foreach (var chunk in ctx.allValidChunks)
            {
                activeChunks[chunk] = new ChunkStateData
                {
                    chunkCoord = chunk,
                    baseState = ChunkState.Wilderness,
                    isRoadChunk = multiDeps.ContainsKey(chunk),
                    roadPredecessors = multiDeps.TryGetValue(chunk, out var p) ? new List<Vector2Int>(p) : new List<Vector2Int>()
                };
            }

            // Case A: When both predecessors are Wilderness:
            SimulatePropagateBorderlands(activeChunks);
            if (activeChunks[cr2].baseState == ChunkState.Borderlands)
            {
                allExpansionOk = false;
                detail = $"Seed {seed}: Rozdroże 2 unlocked when all predecessors are Wilderness!";
                break;
            }

            // Case B: A valid non-road neighbor of cr2 is Settled, but both predecessors are Wilderness:
            var nonRoadNeighbors = HexGridMath.GetNeighbors(cr2)
                .Where(n => activeChunks.ContainsKey(n) && !predecessors.Contains(n))
                .ToList();
            if (nonRoadNeighbors.Count > 0)
            {
                activeChunks[nonRoadNeighbors[0]].baseState = ChunkState.Settled;
                SimulatePropagateBorderlands(activeChunks);
                if (activeChunks[cr2].baseState == ChunkState.Borderlands)
                {
                    allExpansionOk = false;
                    detail = $"Seed {seed}: Rozdroże 2 unlocked from non-road neighbor while road predecessors were Wilderness!";
                    break;
                }
                activeChunks[nonRoadNeighbors[0]].baseState = ChunkState.Wilderness;
            }

            // Case C: Predecessor B (Loop) becomes Settled, Predecessor A is Wilderness:
            activeChunks[predB].baseState = ChunkState.Settled;
            SimulatePropagateBorderlands(activeChunks);
            if (activeChunks[cr2].baseState != ChunkState.Borderlands)
            {
                allExpansionOk = false;
                detail = $"Seed {seed}: Rozdroże 2 failed to unlock to Borderlands when Loop predecessor was Settled!";
                break;
            }

            // Reset cr2
            activeChunks[cr2].baseState = ChunkState.Wilderness;
            activeChunks[predB].baseState = ChunkState.Wilderness;

            // Case D: Predecessor A (Path A) becomes Outskirts, Predecessor B is Wilderness:
            activeChunks[predA].baseState = ChunkState.Outskirts;
            SimulatePropagateBorderlands(activeChunks);
            if (activeChunks[cr2].baseState != ChunkState.Borderlands)
            {
                allExpansionOk = false;
                detail = $"Seed {seed}: Rozdroże 2 failed to unlock to Borderlands when Path A predecessor was Outskirts!";
                break;
            }
        }

        Assert(allExpansionOk, "Challenge 5: MapExpansionManager unlocks Rozdroże 2 to Borderlands when EITHER predecessor is Settled/Outskirts", detail);
    }

    // =========================================================================
    // Simulation helpers mirroring exact EnemySpawner and MapExpansionManager logic
    // =========================================================================

    private static List<EnemySpawner.SpawnRoute> SimulateEnemySpawnerRecalculate(
        List<SpawnPathVariant> potentialRoutes,
        HashSet<Vector2Int> revealedChunks,
        int chunkRadius, float hexSize, float padding,
        HashSet<Vector2Int> allValidChunks)
    {
        var candidateRoutes = new List<EnemySpawner.SpawnRoute>();

        foreach (var variant in potentialRoutes)
        {
            if (variant.fullPath == null || variant.fullPath.Count < 2) continue;

            int activeSpawnIndex = variant.fullPath.Count - 1;
            for (int i = variant.fullPath.Count - 1; i >= 0; i--)
            {
                var chunk = GetChunkCoordFromWorldPos(variant.fullPath[i], allValidChunks, chunkRadius, hexSize, padding);
                if (revealedChunks.Contains(chunk))
                {
                    activeSpawnIndex = i;
                }
                else
                {
                    break;
                }
            }

            if (activeSpawnIndex >= 0 && activeSpawnIndex < variant.fullPath.Count - 1)
            {
                var activePath = variant.fullPath.GetRange(activeSpawnIndex, variant.fullPath.Count - activeSpawnIndex);
                var spawnPoint = variant.fullPath[activeSpawnIndex];

                candidateRoutes.Add(new EnemySpawner.SpawnRoute
                {
                    fullPath = variant.fullPath,
                    flowWeight = variant.flowWeight,
                    currentSpawnPoint = spawnPoint,
                    currentActivePath = activePath,
                    name = variant.name
                });
            }
        }

        if (candidateRoutes.Count == 0) return new List<EnemySpawner.SpawnRoute>();

        // Merging pre-crossroads routes
        var mergedRoutes = new List<EnemySpawner.SpawnRoute>();
        foreach (var candidate in candidateRoutes)
        {
            EnemySpawner.SpawnRoute? existingGroup = null;
            foreach (var merged in mergedRoutes)
            {
                if (AreActivePathsIdentical(merged.currentActivePath, candidate.currentActivePath))
                {
                    existingGroup = merged;
                    break;
                }
            }

            if (existingGroup != null)
            {
                existingGroup.flowWeight += candidate.flowWeight;
            }
            else
            {
                mergedRoutes.Add(new EnemySpawner.SpawnRoute
                {
                    name = candidate.name,
                    fullPath = candidate.fullPath,
                    flowWeight = candidate.flowWeight,
                    currentSpawnPoint = candidate.currentSpawnPoint,
                    currentActivePath = new List<Vector3>(candidate.currentActivePath)
                });
            }
        }

        // Normalization
        float totalFlow = mergedRoutes.Sum(r => r.flowWeight);
        if (totalFlow > 0.0001f)
        {
            foreach (var r in mergedRoutes)
                r.currentSpawnChance = (r.flowWeight / totalFlow) * 100f;
        }
        else
        {
            float equalShare = 100f / mergedRoutes.Count;
            foreach (var r in mergedRoutes)
                r.currentSpawnChance = equalShare;
        }

        return mergedRoutes.OrderByDescending(r => r.currentSpawnChance).ToList();
    }

    private static bool AreActivePathsIdentical(List<Vector3> pathA, List<Vector3> pathB)
    {
        if (pathA == null || pathB == null) return false;
        if (pathA.Count != pathB.Count) return false;

        const float toleranceSq = 0.001f;
        for (int i = 0; i < pathA.Count; i++)
        {
            if ((pathA[i] - pathB[i]).sqrMagnitude > toleranceSq)
                return false;
        }

        return true;
    }

    private static Vector2Int GetChunkCoordFromWorldPos(
        Vector3 worldPos, HashSet<Vector2Int> allValidChunks,
        int chunkRadius, float hexSize, float padding)
    {
        Vector2Int bestChunk = Vector2Int.zero;
        float minDst = float.MaxValue;

        foreach (var chunk in allValidChunks)
        {
            Vector3 center = HexGridMath.GetChunkCenterWorld(chunk, chunkRadius, hexSize, padding);
            float d = Vector2.Distance(new Vector2(center.x, center.z), new Vector2(worldPos.x, worldPos.z));
            if (d < minDst) { minDst = d; bestChunk = chunk; }
        }

        return bestChunk;
    }

    private static void SimulatePropagateBorderlands(Dictionary<Vector2Int, ChunkStateData> activeChunks)
    {
        List<Vector2Int> toBorderlands = new List<Vector2Int>();

        foreach (var kvp in activeChunks)
        {
            Vector2Int coord = kvp.Key;
            ChunkStateData data = kvp.Value;

            if (data.baseState != ChunkState.Wilderness) continue;

            bool hasValidNeighbor = false;
            foreach (var neighborCoord in HexGridMath.GetNeighbors(coord))
            {
                if (activeChunks.TryGetValue(neighborCoord, out var neighborData))
                {
                    if (neighborData.baseState == ChunkState.Outskirts || neighborData.baseState == ChunkState.Settled)
                    {
                        hasValidNeighbor = true;
                        break;
                    }
                }
            }

            if (!hasValidNeighbor) continue;

            if (data.isRoadChunk && data.hasRoadPredecessor)
            {
                bool anyPredecessorRevealed = false;
                if (data.roadPredecessors != null && data.roadPredecessors.Count > 0)
                {
                    foreach (var predCoord in data.roadPredecessors)
                    {
                        if (activeChunks.TryGetValue(predCoord, out var predData))
                        {
                            if (predData.baseState == ChunkState.Outskirts || predData.baseState == ChunkState.Settled)
                            {
                                anyPredecessorRevealed = true;
                                break;
                            }
                        }
                    }
                }

                if (!anyPredecessorRevealed)
                {
                    continue;
                }
            }

            toBorderlands.Add(coord);
        }

        foreach (var coord in toBorderlands)
        {
            activeChunks[coord].baseState = ChunkState.Borderlands;
        }
    }
}
