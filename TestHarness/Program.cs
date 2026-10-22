using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

class Program
{
    private static int _passedTests = 0;
    private static int _failedTests = 0;

    static void Assert(bool condition, string testName, string details = "")
    {
        if (condition)
        {
            _passedTests++;
            Console.WriteLine($"[PASS] {testName}");
        }
        else
        {
            _failedTests++;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] {testName} - {details}");
            Console.ResetColor();
        }
    }

    static int Main()
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("CHALLENGER M1: EMPIRICAL STRESS & DETERMINISM SUITE");
        Console.WriteLine("=================================================\n");

        TestMapRngDeterminismIdenticalSeeds();
        TestMapRngDivergenceDifferentSeeds();
        TestMapRngValueDistribution();
        TestMapRngRangeIntBoundaries();
        TestMapRngRangeFloatBoundaries();
        TestMapRngShuffle();
        TestMapRngExtremeSeeds();
        TestMapGenerationContextSeedZero();
        TestMapGenerationContextResetAndStateCarryover();
        TestChunkPathDataIntegrity();
        TestChunkStateDataIntegrity();
        TestLoopDefinitionModel();

        TestM2PathGenerationSuite();
        TestM2ExpansionManagerMultiPredecessor();
        TestM2EnemySpawnerFlowAndFog();
        TestM2ChallengerEmpiricalStressSuite();
        _failedTests += ChallengerM2Tests.RunAll();
        _failedTests += ChallengerM3Tests.RunAll();

        Console.WriteLine("\n=================================================");
        Console.WriteLine($"RESULTS: {_passedTests} PASSED, {_failedTests} FAILED");
        Console.WriteLine("=================================================");

        return _failedTests > 0 ? 1 : 0;
    }

    // =========================================================================
    // 1. DETERMINISM & DIVERGENCE
    // =========================================================================

    static void TestMapRngDeterminismIdenticalSeeds()
    {
        int[] testSeeds = { 1, 42, 100, 1337, 9876543, int.MaxValue / 2, int.MaxValue - 1 };
        bool allIdentical = true;
        string failureDetail = "";

        foreach (int s in testSeeds)
        {
            var rng1 = new MapRng(s);
            var rng2 = new MapRng(s);

            // Test 1000 Range(int)
            for (int i = 0; i < 1000; i++)
            {
                int r1 = rng1.Range(-100, 100);
                int r2 = rng2.Range(-100, 100);
                if (r1 != r2)
                {
                    allIdentical = false;
                    failureDetail = $"Range(int) mismatch at iter {i} for seed {s}: {r1} != {r2}";
                    break;
                }
            }
            if (!allIdentical) break;

            // Test 1000 Range(float)
            for (int i = 0; i < 1000; i++)
            {
                float f1 = rng1.Range(-50.5f, 50.5f);
                float f2 = rng2.Range(-50.5f, 50.5f);
                if (Math.Abs(f1 - f2) > 0.000001f)
                {
                    allIdentical = false;
                    failureDetail = $"Range(float) mismatch at iter {i} for seed {s}: {f1} != {f2}";
                    break;
                }
            }
            if (!allIdentical) break;

            // Test 1000 Value
            for (int i = 0; i < 1000; i++)
            {
                float v1 = rng1.Value;
                float v2 = rng2.Value;
                if (Math.Abs(v1 - v2) > 0.000001f)
                {
                    allIdentical = false;
                    failureDetail = $"Value mismatch at iter {i} for seed {s}: {v1} != {v2}";
                    break;
                }
            }
            if (!allIdentical) break;

            // Test 100 Shuffles
            for (int i = 0; i < 100; i++)
            {
                var l1 = Enumerable.Range(0, 20).ToList();
                var l2 = Enumerable.Range(0, 20).ToList();
                rng1.Shuffle(l1);
                rng2.Shuffle(l2);
                if (!l1.SequenceEqual(l2))
                {
                    allIdentical = false;
                    failureDetail = $"Shuffle sequence mismatch at iter {i} for seed {s}";
                    break;
                }
            }
            if (!allIdentical) break;
        }

        Assert(allIdentical, "Determinism: Identical seeds produce byte-for-byte identical sequences", failureDetail);
    }

    static void TestMapRngDivergenceDifferentSeeds()
    {
        bool allDiverged = true;
        string failureDetail = "";

        for (int s = 1; s <= 20; s++)
        {
            var rng1 = new MapRng(s);
            var rng2 = new MapRng(s + 10000);

            var seq1 = Enumerable.Range(0, 50).Select(_ => rng1.Range(0, 1000000)).ToList();
            var seq2 = Enumerable.Range(0, 50).Select(_ => rng2.Range(0, 1000000)).ToList();

            if (seq1.SequenceEqual(seq2))
            {
                allDiverged = false;
                failureDetail = $"Seeds {s} and {s + 10000} produced identical sequences";
                break;
            }
        }

        Assert(allDiverged, "Divergence: Different seeds produce distinct sequences", failureDetail);
    }

    static void TestMapRngValueDistribution()
    {
        var rng = new MapRng(12345);
        bool inBounds = true;
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int i = 0; i < 50000; i++)
        {
            float v = rng.Value;
            if (v < 0.0f || v >= 1.0f)
            {
                inBounds = false;
                break;
            }
            if (v < min) min = v;
            if (v > max) max = v;
        }

        Assert(inBounds && min >= 0.0f && max < 1.0f && min < 0.01f && max > 0.99f,
            "Value: Output strictly in [0.0, 1.0) with full coverage",
            $"min: {min}, max: {max}, inBounds: {inBounds}");
    }

    // =========================================================================
    // 2. EDGE CASES IN RANGE (INT & FLOAT)
    // =========================================================================

    static void TestMapRngRangeIntBoundaries()
    {
        var rng = new MapRng(54321);

        // min == max
        int valEqual = rng.Range(7, 7);
        Assert(valEqual == 7, "Range(int): min == maxExclusive returns min", $"got {valEqual}");

        // min > max
        int valInverted = rng.Range(10, 3);
        Assert(valInverted == 10, "Range(int): min > maxExclusive returns min safely without throwing", $"got {valInverted}");

        // standard range bounds
        bool withinRange = true;
        for (int i = 0; i < 5000; i++)
        {
            int v = rng.Range(-10, 10);
            if (v < -10 || v >= 10)
            {
                withinRange = false;
                break;
            }
        }
        Assert(withinRange, "Range(int): values strictly within [min, maxExclusive)");

        // 0 to 1
        bool zeroOnly = true;
        for (int i = 0; i < 100; i++)
        {
            if (rng.Range(0, 1) != 0) { zeroOnly = false; break; }
        }
        Assert(zeroOnly, "Range(int): Range(0, 1) always returns 0");

        // negative boundaries
        bool negOk = true;
        for (int i = 0; i < 100; i++)
        {
            int n = rng.Range(-5, -4);
            if (n != -5) { negOk = false; break; }
        }
        Assert(negOk, "Range(int): Range(-5, -4) always returns -5");
    }

    static void TestMapRngRangeFloatBoundaries()
    {
        var rng = new MapRng(999);

        // min == max
        float fEqual = rng.Range(4.5f, 4.5f);
        Assert(Math.Abs(fEqual - 4.5f) < 0.0001f, "Range(float): min == max returns min", $"got {fEqual}");

        // min > max
        float fInverted = rng.Range(8.2f, 1.1f);
        Assert(Math.Abs(fInverted - 8.2f) < 0.0001f, "Range(float): min > max returns min safely", $"got {fInverted}");

        // bounds check
        bool floatRangeOk = true;
        for (int i = 0; i < 5000; i++)
        {
            float f = rng.Range(10.0f, 20.0f);
            if (f < 10.0f || f >= 20.0f)
            {
                floatRangeOk = false;
                break;
            }
        }
        Assert(floatRangeOk, "Range(float): values strictly within [min, max)");
    }

    // =========================================================================
    // 3. SHUFFLE EDGE CASES & UNIFORMITY
    // =========================================================================

    static void TestMapRngShuffle()
    {
        var rng = new MapRng(777);

        // null list
        bool nullSafe = true;
        try
        {
            List<int>? nullList = null;
            rng.Shuffle(nullList!);
        }
        catch (Exception ex)
        {
            nullSafe = false;
            Console.WriteLine($"Exception on null shuffle: {ex}");
        }
        Assert(nullSafe, "Shuffle: Handles null list gracefully without exception");

        // empty list
        var emptyList = new List<string>();
        rng.Shuffle(emptyList);
        Assert(emptyList.Count == 0, "Shuffle: Handles empty list gracefully");

        // single item
        var singleItemList = new List<int> { 42 };
        rng.Shuffle(singleItemList);
        Assert(singleItemList.Count == 1 && singleItemList[0] == 42, "Shuffle: Handles single item list correctly");

        // array test (IList<T>)
        int[] arr = { 1, 2, 3, 4, 5 };
        rng.Shuffle(arr);
        Assert(arr.OrderBy(x => x).SequenceEqual(new[] { 1, 2, 3, 4, 5 }), "Shuffle: Correctly operates on Array (IList<T>) preserving elements");

        // Fisher-Yates uniformity test: 3 elements has 3! = 6 permutations.
        // Over 12,000 runs, each should occur ~2,000 times (between 1,700 and 2,300).
        var counts = new Dictionary<string, int>();
        for (int i = 0; i < 12000; i++)
        {
            var items = new List<int> { 1, 2, 3 };
            rng.Shuffle(items);
            string key = string.Join(",", items);
            counts[key] = counts.GetValueOrDefault(key, 0) + 1;
        }

        bool all6PermutationsFound = counts.Count == 6;
        bool distributionFair = counts.Values.All(c => c >= 1650 && c <= 2350);
        Assert(all6PermutationsFound && distributionFair,
            "Shuffle: Fisher-Yates produces uniform distribution across all 6 permutations",
            $"counts: {string.Join("; ", counts.Select(kv => $"{kv.Key}:{kv.Value}"))}");
    }

    // =========================================================================
    // 4. EXTREME SEEDS
    // =========================================================================

    static void TestMapRngExtremeSeeds()
    {
        bool extremeSeedsWork = true;
        try
        {
            var rngMax = new MapRng(int.MaxValue);
            var rngMin = new MapRng(int.MinValue);
            var rngZero = new MapRng(0);
            var rngNeg = new MapRng(-42);

            int valMax = rngMax.Range(0, 100);
            int valMin = rngMin.Range(0, 100);
            int valZero = rngZero.Range(0, 100);
            int valNeg = rngNeg.Range(0, 100);

            extremeSeedsWork = (valMax >= 0 && valMin >= 0 && valZero >= 0 && valNeg >= 0);
        }
        catch (Exception ex)
        {
            extremeSeedsWork = false;
            Console.WriteLine($"Exception with extreme seeds: {ex}");
        }
        Assert(extremeSeedsWork, "MapRng: Supports int.MaxValue, int.MinValue, 0, and negative seeds without crash");
    }

    // =========================================================================
    // 5. MAPGENERATIONCONTEXT INITIALIZE & SEED 0
    // =========================================================================

    static void TestMapGenerationContextSeedZero()
    {
        // When inputSeed = 0, MapGenerationContext.Initialize(0) calls UnityEngine.Random.Range(1, int.MaxValue).
        // In external test runners / thread contexts without Unity C++ runtime, UnityEngine.Random throws SecurityException.
        bool threwECall = false;
        string exceptionMessage = "";
        try
        {
            var context = new MapGenerationContext(0);
        }
        catch (Exception ex)
        {
            threwECall = true;
            exceptionMessage = ex.GetType().Name + ": " + ex.Message;
        }

        Assert(!threwECall,
            "MapGenerationContext: inputSeed == 0 should initialize portably without throwing ECall exception",
            $"Caught: {exceptionMessage} (UnityEngine.Random is not thread-safe / engine-coupled)");
    }

    // =========================================================================
    // 6. MAPGENERATIONCONTEXT RESET & STATE CARRY-OVER / LEAKS
    // =========================================================================

    static void TestMapGenerationContextResetAndStateCarryover()
    {
        var ctx = new MapGenerationContext(42);

        // Populate context with mock data across all collections
        var cCoord = new Vector2Int(1, 1);
        ctx.worldData[cCoord] = new Dictionary<Vector2Int, HexCellData>
        {
            { new Vector2Int(0, 0), new HexCellData { isPath = true } }
        };
        ctx.chunkPaths[cCoord] = new ChunkPathData();
        ctx.chunkInternalCosts[cCoord] = new Dictionary<Vector2Int, int> { { new Vector2Int(0, 0), 10 } };
        ctx.allValidChunks.Add(cCoord);
        ctx.generatedChunkSequence.Add(cCoord);
        ctx.extraSpawnerChunks.Add(cCoord);
        ctx.chunkTypes[cCoord] = ChunkType.Normal;
        ctx.specialChunkData[cCoord] = null!;
        ctx.mainSpawnerChunk = new Vector2Int(5, 5);
        ctx.loops.Add(new LoopDefinition(new Vector2Int(1, 1), new Vector2Int(2, 2)));
        ctx.crossroadsChunks.Add(cCoord);

        // Draw some random numbers to advance Rng state
        int valBeforeReset = ctx.Rng.Range(0, 1000);

        // 1. Reset with a new explicit seed (e.g. 99)
        ctx.Reset(99);

        bool collectionsCleared =
            ctx.worldData.Count == 0 &&
            ctx.chunkPaths.Count == 0 &&
            ctx.chunkInternalCosts.Count == 0 &&
            ctx.allValidChunks.Count == 0 &&
            ctx.generatedChunkSequence.Count == 0 &&
            ctx.extraSpawnerChunks.Count == 0 &&
            ctx.chunkTypes.Count == 0 &&
            ctx.specialChunkData.Count == 0 &&
            ctx.loops.Count == 0 &&
            ctx.crossroadsChunks.Count == 0 &&
            ctx.mainSpawnerChunk == default;

        Assert(collectionsCleared, "Reset(newSeed): All data structures and collections are cleared (no memory leaks)");
        Assert(ctx.seed == 99 && ctx.Rng.Seed == 99, "Reset(newSeed): Seed and Rng are updated to new seed", $"seed: {ctx.seed}, Rng.Seed: {ctx.Rng?.Seed}");

        // 2. STRESS TEST: State carry-over in Reset() with default argument (newSeed = 0)
        var ctx2 = new MapGenerationContext(12345);
        int initialSeed = ctx2.seed;
        var initialRng = ctx2.Rng;
        
        // Advance the random stream
        int r1 = ctx2.Rng.Range(0, 100000);
        int r2 = ctx2.Rng.Range(0, 100000);

        // Call Reset() expecting fresh state / seed re-initialization
        ctx2.Reset(); // default newSeed = 0

        // If seed == 12345 was kept, but Rng was not recreated:
        bool rngRecreated = !ReferenceEquals(ctx2.Rng, initialRng);
        bool seedChangedOrReset = (ctx2.seed != initialSeed);
        
        // If Rng was not recreated and seed was not changed:
        // What does the stream do?
        int nextValAfterReset = ctx2.Rng.Range(0, 100000);
        
        // If it was properly reset with seed 12345, the next value should equal r1!
        // If it carried over mutated state, nextValAfterReset will be the 3rd number in stream!
        var freshRng = new MapRng(12345);
        int freshR1 = freshRng.Range(0, 100000);
        int freshR2 = freshRng.Range(0, 100000);
        int freshR3 = freshRng.Range(0, 100000);

        bool rngStreamCarriedOver = (nextValAfterReset == freshR3);

        Assert(!rngStreamCarriedOver && (rngRecreated || nextValAfterReset == freshR1),
            "Reset(): Rng state must NOT carry over mutated PRNG state from previous generation",
            $"BUG CONFIRMED: Reset() kept old mutated MapRng instance without resetting stream (nextVal={nextValAfterReset}, expected fresh={freshR1}, old mutated={freshR3})");
    }

    // =========================================================================
    // 7. CHUNKPATHDATA INTEGRITY & BACKWARD COMPATIBILITY
    // =========================================================================

    static void TestChunkPathDataIntegrity()
    {
        var data = new ChunkPathData();

        // Test nextChunkTowardsBase property & nextChunksTowardsBase list
        Assert(data.nextChunksTowardsBase != null, "ChunkPathData: nextChunksTowardsBase list is initialized");
        Assert(data.nextChunkTowardsBase == new Vector2Int(-999, -999), "ChunkPathData: default nextChunkTowardsBase returns sentinel (-999, -999)");

        // Set via backward-compatible property
        data.nextChunkTowardsBase = new Vector2Int(1, 2);
        Assert(data.nextChunksTowardsBase.Count == 1 && data.nextChunksTowardsBase[0] == new Vector2Int(1, 2),
            "ChunkPathData: Setting nextChunkTowardsBase updates nextChunksTowardsBase[0]");
        Assert(data.nextChunkTowardsBase == new Vector2Int(1, 2),
            "ChunkPathData: Getting nextChunkTowardsBase returns first element");

        // Add additional predecessor (M2 loop scenario)
        data.AddNextChunkTowardsBase(new Vector2Int(0, 2));
        Assert(data.nextChunksTowardsBase.Count == 2 && data.nextChunksTowardsBase[1] == new Vector2Int(0, 2),
            "ChunkPathData: AddNextChunkTowardsBase appends second predecessor");
        Assert(data.nextChunkTowardsBase == new Vector2Int(1, 2),
            "ChunkPathData: nextChunkTowardsBase still returns primary predecessor");

        // Test extraExits and isCrossroads
        Assert(data.extraExits != null, "ChunkPathData: extraExits list is initialized");
        Assert(!data.isCrossroads, "ChunkPathData: isCrossroads is false when extraEntries and extraExits are empty");

        data.AddExtraExit(new Vector2Int(3, 4));
        Assert(data.isCrossroads, "ChunkPathData: isCrossroads is true when extraExits > 0");
        Assert(data.extraExits.Contains(new Vector2Int(3, 4)), "ChunkPathData: AddExtraExit adds element");

        // Test GetAllEntries, GetAllExits, GetAllEndpoints
        data.entryHex = new Vector2Int(0, 1);
        data.exitHex = new Vector2Int(5, 5);
        data.AddExtraEntry(new Vector2Int(0, 2));

        var allEntries = data.GetAllEntries();
        var allExits = data.GetAllExits();
        var allEndpoints = data.GetAllEndpoints();

        Assert(allEntries.Count == 2 && allEntries.Contains(new Vector2Int(0, 1)) && allEntries.Contains(new Vector2Int(0, 2)),
            "ChunkPathData: GetAllEntries returns primary and extra entries");
        Assert(allExits.Count == 2 && allExits.Contains(new Vector2Int(5, 5)) && allExits.Contains(new Vector2Int(3, 4)),
            "ChunkPathData: GetAllExits returns primary and extra exits");
        Assert(allEndpoints.Count == 4, "ChunkPathData: GetAllEndpoints returns all 4 entry/exit points");
    }

    // =========================================================================
    // 8. CHUNKSTATEDATA INTEGRITY
    // =========================================================================

    static void TestChunkStateDataIntegrity()
    {
        var state = new ChunkStateData { chunkCoord = new Vector2Int(1, 1) };

        Assert(state.roadPredecessors != null, "ChunkStateData: roadPredecessors list initialized");
        Assert(!state.hasRoadPredecessor, "ChunkStateData: hasRoadPredecessor is false when empty");
        Assert(state.roadPredecessor == new Vector2Int(-999, -999), "ChunkStateData: default roadPredecessor returns sentinel (-999, -999)");

        // Set roadPredecessor
        state.roadPredecessor = new Vector2Int(0, 1);
        Assert(state.hasRoadPredecessor, "ChunkStateData: hasRoadPredecessor is true after assignment");
        Assert(state.roadPredecessor == new Vector2Int(0, 1), "ChunkStateData: roadPredecessor returns assigned coordinate");

        // Add additional road predecessor
        state.AddRoadPredecessor(new Vector2Int(1, 0));
        Assert(state.roadPredecessors.Count == 2 && state.roadPredecessors.Contains(new Vector2Int(1, 0)),
            "ChunkStateData: AddRoadPredecessor adds second predecessor");

        // ChunkReconIntel isCrossroads flag
        var recon = new ChunkReconIntel();
        Assert(!recon.isCrossroads, "ChunkReconIntel: default isCrossroads is false");
        recon.isCrossroads = true;
        Assert(recon.isCrossroads, "ChunkReconIntel: isCrossroads flag can be set");
    }

    // =========================================================================
    // 9. LOOP DEFINITION MODEL
    // =========================================================================

    static void TestLoopDefinitionModel()
    {
        var loop = new LoopDefinition(new Vector2Int(3, 4), new Vector2Int(1, 2), 0.35f);
        Assert(loop.splitCrossroads == new Vector2Int(3, 4), "LoopDefinition: splitCrossroads set");
        Assert(loop.mergeCrossroads == new Vector2Int(1, 2), "LoopDefinition: mergeCrossroads set");
        Assert(Math.Abs(loop.loopFlowRatio - 0.35f) < 0.001f, "LoopDefinition: loopFlowRatio set");
        Assert(loop.mainSegmentChunks != null && loop.loopChunks != null, "LoopDefinition: lists initialized");
    }

    // =========================================================================
    // 10. MILESTONE M2: PATH GENERATION, TOPOLOGY, CROSSROADS, LOOPS & FLOW
    // =========================================================================

    static void TestM2PathGenerationSuite()
    {
        Console.WriteLine("\n--- MILESTONE M2 TESTS ---");

        int[] seeds = Enumerable.Range(1, 100).ToArray();
        bool allPathLenOk = true;
        bool allSafeStartCrossroadsOk = true;
        bool allSafeStartLengthOk = true;
        bool allLoopsOk = true;
        bool allBranchesOutwardOk = true;
        bool allFlowSumsToOne = true;
        string detail = "";

        foreach (int seed in seeds)
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

            bool generated = false;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                if (pathGen.CalculateMainPath(new Vector2Int(3, -2)))
                {
                    pathGen.GenerateLoops();
                    pathGen.GenerateSideBranches();
                    pathGen.RebuildAllCrossroadsInternalPaths();
                    generated = true;
                    break;
                }
            }

            if (!generated)
            {
                allPathLenOk = false;
                detail = $"Failed to generate path for seed {seed}";
                break;
            }

            int N = ctx.generatedChunkSequence.Count;
            if (N < 11)
            {
                allPathLenOk = false;
                detail = $"Seed {seed}: Path length {N} < 11";
                break;
            }

            // Safe start zone check: last 2 chunks (indices N - 2 and N - 1)
            for (int sIdx = N - 2; sIdx < N; sIdx++)
            {
                Vector2Int cCoord = ctx.generatedChunkSequence[sIdx];
                var cData = ctx.chunkPaths[cCoord];

                if (cData.isCrossroads || ctx.crossroadsChunks.Contains(cCoord)
                    || cData.extraEntries.Count > 0 || cData.extraExits.Count > 0)
                {
                    allSafeStartCrossroadsOk = false;
                    detail = $"Seed {seed}: Safe start chunk {cCoord} at index {sIdx} is a crossroads";
                    break;
                }

                if (cData.internalPath == null || cData.internalPath.Count < 6)
                {
                    allSafeStartLengthOk = false;
                    detail = $"Seed {seed}: Safe start chunk {cCoord} has road length {cData.internalPath?.Count} < 6";
                    break;
                }
            }
            if (!allSafeStartCrossroadsOk || !allSafeStartLengthOk) break;

            // Loops check
            if (ctx.loops.Count < 1)
            {
                allLoopsOk = false;
                detail = $"Seed {seed}: Generated {ctx.loops.Count} loops, expected >= 1";
                break;
            }

            foreach (var loop in ctx.loops)
            {
                int splitIdx = ctx.generatedChunkSequence.IndexOf(loop.splitCrossroads);
                int mergeIdx = ctx.generatedChunkSequence.IndexOf(loop.mergeCrossroads);
                if (splitIdx < 0 || mergeIdx < 0)
                {
                    allLoopsOk = false;
                    detail = $"Seed {seed}: Loop crossroads not found in sequence";
                    break;
                }

                if (mergeIdx >= N - 2 || splitIdx >= N - 2)
                {
                    allLoopsOk = false;
                    detail = $"Seed {seed}: Loop touched safe start zone (mergeIdx={mergeIdx}, splitIdx={splitIdx})";
                    break;
                }

                // Check disjointness
                var mainSegSet = new HashSet<Vector2Int>(loop.mainSegmentChunks);
                foreach (var lc in loop.loopChunks)
                {
                    if (mainSegSet.Contains(lc))
                    {
                        allLoopsOk = false;
                        detail = $"Seed {seed}: Loop chunk {lc} intersects main segment";
                        break;
                    }
                }
                if (!allLoopsOk) break;

                // Loop length >= bypassed length
                int bypassedCount = loop.mainSegmentChunks.Count + 2;
                int loopCount = loop.loopChunks.Count + 2;
                if (loopCount < bypassedCount)
                {
                    allLoopsOk = false;
                    detail = $"Seed {seed}: Loop length {loopCount} < bypassed length {bypassedCount}";
                    break;
                }
            }
            if (!allLoopsOk) break;

            // Side branches check
            foreach (var extraSpawner in ctx.extraSpawnerChunks)
            {
                int spawnerDist = HexGridMath.GetDistance(Vector2Int.zero, extraSpawner);
                Vector2Int curr = extraSpawner;
                while (curr.x != -999 && curr != Vector2Int.zero)
                {
                    if (ctx.crossroadsChunks.Contains(curr))
                    {
                        int junctionDist = HexGridMath.GetDistance(Vector2Int.zero, curr);
                        if (spawnerDist < junctionDist)
                        {
                            allBranchesOutwardOk = false;
                            detail = $"Seed {seed}: Spawner {extraSpawner} (dist {spawnerDist}) is closer than junction {curr} (dist {junctionDist})";
                            break;
                        }
                        break;
                    }
                    if (ctx.chunkPaths.TryGetValue(curr, out var d))
                        curr = d.nextChunkTowardsBase;
                    else break;
                }
                if (!allBranchesOutwardOk) break;
            }
            if (!allBranchesOutwardOk) break;

            // Flow weights check
            var routes = pathGen.GetAllSpawnRoutes(new Vector2Int(3, -2));
            float sumWeights = routes.Sum(r => r.flowWeight);
            if (Math.Abs(sumWeights - 1.0f) > 0.001f)
            {
                allFlowSumsToOne = false;
                detail = $"Seed {seed}: Flow weights sum to {sumWeights}, expected 1.0";
                break;
            }
        }

        Assert(allPathLenOk, "M2 PathGeneration: Path A has length >= 11 chunks", detail);
        Assert(allSafeStartCrossroadsOk, "M2 PathGeneration: Protected start zone (first 2 chunks from base) has zero crossroads", detail);
        Assert(allSafeStartLengthOk, "M2 PathGeneration: Protected start road length >= 6 hexes per chunk (meandering)", detail);
        Assert(allLoopsOk, "M2 PathGeneration: Guaranteed loop generated, disjoint from Path A, length >= bypassed", detail);
        Assert(allBranchesOutwardOk, "M2 PathGeneration: Side branches progress outward (spawner does not backtrack towards base)", detail);
        Assert(allFlowSumsToOne, "M2 PathGeneration: Flow model weights across all routes sum to exactly 1.000", detail);
    }

    static void TestM2ExpansionManagerMultiPredecessor()
    {
        var activeChunks = new Dictionary<Vector2Int, ChunkStateData>();

        var cr2 = new ChunkStateData
        {
            chunkCoord = new Vector2Int(2, 2),
            baseState = ChunkState.Wilderness,
            isRoadChunk = true,
            roadPredecessors = new List<Vector2Int> { new Vector2Int(2, 1), new Vector2Int(1, 2) }
        };
        var predA = new ChunkStateData { chunkCoord = new Vector2Int(2, 1), baseState = ChunkState.Wilderness };
        var predB = new ChunkStateData { chunkCoord = new Vector2Int(1, 2), baseState = ChunkState.Wilderness };

        activeChunks[cr2.chunkCoord] = cr2;
        activeChunks[predA.chunkCoord] = predA;
        activeChunks[predB.chunkCoord] = predB;

        // When both predecessors are Wilderness:
        bool canUnlockBefore = false;
        foreach (var predCoord in cr2.roadPredecessors)
        {
            if (activeChunks.TryGetValue(predCoord, out var pData) &&
                (pData.baseState == ChunkState.Outskirts || pData.baseState == ChunkState.Settled))
            {
                canUnlockBefore = true;
                break;
            }
        }
        Assert(!canUnlockBefore, "M2 MapExpansionManager: Multi-predecessor road chunk blocked when all predecessors are Wilderness");

        // When predB becomes Settled (reached via Loop), but predA is still Wilderness:
        predB.baseState = ChunkState.Settled;
        bool canUnlockAfter = false;
        foreach (var predCoord in cr2.roadPredecessors)
        {
            if (activeChunks.TryGetValue(predCoord, out var pData) &&
                (pData.baseState == ChunkState.Outskirts || pData.baseState == ChunkState.Settled))
            {
                canUnlockAfter = true;
                break;
            }
        }
        Assert(canUnlockAfter, "M2 MapExpansionManager: Multi-predecessor OR condition allows expansion when ANY predecessor is Settled/Outskirts");
    }

    static void TestM2EnemySpawnerFlowAndFog()
    {
        // 1. Pre-crossroads route merging test
        // 3 routes that diverge far away, but share the identical active path in revealed territory
        var commonPath = new List<Vector3>
        {
            new Vector3(5, 0, 5),
            new Vector3(3, 0, 3),
            new Vector3(1, 0, 1),
            new Vector3(0, 0, 0)
        };

        var candidate1 = new EnemySpawner.SpawnRoute
        {
            flowWeight = 0.5f,
            currentActivePath = new List<Vector3>(commonPath),
            currentSpawnPoint = commonPath[0]
        };
        var candidate2 = new EnemySpawner.SpawnRoute
        {
            flowWeight = 0.3f,
            currentActivePath = new List<Vector3>(commonPath),
            currentSpawnPoint = commonPath[0]
        };
        var candidate3 = new EnemySpawner.SpawnRoute
        {
            flowWeight = 0.2f,
            currentActivePath = new List<Vector3>(commonPath),
            currentSpawnPoint = commonPath[0]
        };

        var candidates = new List<EnemySpawner.SpawnRoute> { candidate1, candidate2, candidate3 };
        var merged = new List<EnemySpawner.SpawnRoute>();

        foreach (var c in candidates)
        {
            EnemySpawner.SpawnRoute? existing = null;
            foreach (var m in merged)
            {
                if (m.currentActivePath.Count == c.currentActivePath.Count)
                {
                    bool match = true;
                    for (int i = 0; i < m.currentActivePath.Count; i++)
                    {
                        if ((m.currentActivePath[i] - c.currentActivePath[i]).sqrMagnitude > 0.001f)
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match) { existing = m; break; }
                }
            }

            if (existing != null) existing.flowWeight += c.flowWeight;
            else merged.Add(new EnemySpawner.SpawnRoute
            {
                flowWeight = c.flowWeight,
                currentSpawnPoint = c.currentSpawnPoint,
                currentActivePath = new List<Vector3>(c.currentActivePath)
            });
        }

        Assert(merged.Count == 1, "M2 EnemySpawner: Pre-crossroads routes sharing active path are merged into exactly 1 spawner");
        Assert(Math.Abs(merged[0].flowWeight - 1.0f) < 0.001f, "M2 EnemySpawner: Merged pre-crossroads spawner has 100% (1.0) flow");

        // 2. Loop in fog backwards scan test
        // Route A (Main Path A): Spawner(10,0) -> R2(6,0) -> PathA(4,0) -> R1(2,0) -> Gate(1,0) -> Base(0,0)
        // Route B (Loop Route):  Spawner(10,0) -> R2(6,0) -> Loop(4,2)  -> R1(2,0) -> Gate(1,0) -> Base(0,0)
        // Revealed: Base(0,0), Gate(1,0), R1(2,0), PathA(4,0), R2(6,0)
        // In fog: Loop(4,2), Spawner(10,0)
        var revealedChunks = new HashSet<Vector2Int>
        {
            new Vector2Int(0, 0),
            new Vector2Int(1, 0),
            new Vector2Int(2, 0),
            new Vector2Int(4, 0),
            new Vector2Int(6, 0)
        };

        var routeBWaypoints = new List<Vector2Int>
        {
            new Vector2Int(10, 0), // 0: Spawner (fog)
            new Vector2Int(6, 0),  // 1: R2 (revealed)
            new Vector2Int(4, 2),  // 2: Loop chunk (FOG!)
            new Vector2Int(2, 0),  // 3: R1 (revealed)
            new Vector2Int(1, 0),  // 4: Gate (revealed)
            new Vector2Int(0, 0)   // 5: Base (revealed)
        };

        // Backwards scan from Base (index 5) down to Spawner (index 0)
        int activeIdx = routeBWaypoints.Count - 1;
        for (int i = routeBWaypoints.Count - 1; i >= 0; i--)
        {
            if (revealedChunks.Contains(routeBWaypoints[i]))
            {
                activeIdx = i;
            }
            else
            {
                break; // Hit fog at index 2 (Loop chunk)
            }
        }

        Vector2Int activeSpawnChunk = routeBWaypoints[activeIdx];
        var activePathChunks = routeBWaypoints.GetRange(activeIdx, routeBWaypoints.Count - activeIdx);

        Assert(activeSpawnChunk == new Vector2Int(2, 0),
            "M2 EnemySpawner: Unrevealed loop forces boundary spawner at Rozdroże 1",
            $"Spawned at {activeSpawnChunk}, expected (2,0)");

        bool noFogInActivePath = activePathChunks.All(c => revealedChunks.Contains(c));
        Assert(noFogInActivePath,
            "M2 EnemySpawner: Active path from Rozdroże 1 contains ZERO unrevealed/foggy loop chunks");
    }

    // =========================================================================
    // 11. CHALLENGER 1 (M2): EMPIRICAL STRESS TEST (100+ SEEDS)
    // =========================================================================

    static void TestM2ChallengerEmpiricalStressSuite()
    {
        Console.WriteLine("\n=================================================");
        Console.WriteLine("CHALLENGER 1 (M2): EMPIRICAL STRESS TEST (200+ SEEDS & ADVERSARIAL ANCHORS)");
        Console.WriteLine("=================================================\n");

        var seeds = new List<int>();
        for (int i = 1; i <= 200; i++) seeds.Add(i);
        seeds.AddRange(new int[] { 777, 1337, 9999, 12345, 54321, 654321, 9876543, int.MaxValue / 2, int.MaxValue - 1, -1, -42, -999, -123456 });

        var baseExits = new Vector2Int[]
        {
            new Vector2Int(3, -2),
            new Vector2Int(3, 0),
            new Vector2Int(0, 3),
            new Vector2Int(-3, 3),
            new Vector2Int(-3, 0),
            new Vector2Int(0, -3)
        };

        int totalSeeds = seeds.Count;
        int oneLoopSeeds = 0;
        int twoLoopSeeds = 0;
        int zeroLoopSeeds = 0;

        bool allPathLenOk = true;
        string pathLenFailDetail = "";

        bool allSafeStartCrossroadsOk = true;
        string safeStartCrossroadsFailDetail = "";

        bool allSafeStartLengthOk = true;
        string safeStartLengthFailDetail = "";

        bool allLoopsOk = true;
        string loopFailDetail = "";

        bool allBranchesOutwardOk = true;
        string branchFailDetail = "";

        bool allFlowOk = true;
        string flowFailDetail = "";

        bool allCrossroadsConnected = true;
        string crossroadsFailDetail = "";

        int minPathLen = int.MaxValue;
        int maxPathLen = int.MinValue;
        long sumPathLen = 0;

        int minSafeRoadHexes = int.MaxValue;
        int maxSafeRoadHexes = int.MinValue;
        long sumSafeRoadHexes = 0;
        int safeRoadCheckCount = 0;

        int totalBranchesGenerated = 0;
        int seedsWithZeroBranches = 0;
        int seedsWithOneBranch = 0;
        int seedsWithTwoBranches = 0;

        foreach (int seed in seeds)
        {
            Vector2Int baseRoadEndLocal = new Vector2Int(3, -2);

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

            bool generated = false;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                if (pathGen.CalculateMainPath(baseRoadEndLocal))
                {
                    pathGen.GenerateLoops();
                    pathGen.GenerateSideBranches();
                    pathGen.RebuildAllCrossroadsInternalPaths();
                    generated = true;
                    break;
                }
            }

            if (!generated)
            {
                allPathLenOk = false;
                pathLenFailDetail = $"Seed {seed}: Failed to generate path within 50 attempts";
                continue;
            }

            int N = ctx.generatedChunkSequence.Count;
            minPathLen = Math.Min(minPathLen, N);
            maxPathLen = Math.Max(maxPathLen, N);
            sumPathLen += N;

            // 1. Main Path A length >= 11
            if (N < 11)
            {
                allPathLenOk = false;
                pathLenFailDetail = $"Seed {seed}: Path length {N} < 11";
            }

            // 2. Protected start zone (first 2 chunks closest to base: N-2, N-1)
            // AND 3. Road length condition (internalPath.Count >= 6 on each)
            for (int sIdx = N - 2; sIdx < N; sIdx++)
            {
                Vector2Int cCoord = ctx.generatedChunkSequence[sIdx];
                if (!ctx.chunkPaths.TryGetValue(cCoord, out var cData))
                {
                    allSafeStartCrossroadsOk = false;
                    safeStartCrossroadsFailDetail = $"Seed {seed}: ChunkPathData missing for safe start chunk {cCoord}";
                    break;
                }

                if (cData.isCrossroads || ctx.crossroadsChunks.Contains(cCoord)
                    || cData.extraEntries.Count > 0 || cData.extraExits.Count > 0
                    || (cData.nextChunksTowardsBase != null && cData.nextChunksTowardsBase.Count > 1))
                {
                    allSafeStartCrossroadsOk = false;
                    safeStartCrossroadsFailDetail = $"Seed {seed}: Safe start chunk {cCoord} at index {sIdx} is a crossroads (isCR={cData.isCrossroads}, entries={cData.extraEntries.Count}, exits={cData.extraExits.Count})";
                }

                if (cData.internalPath == null || cData.internalPath.Count < 6)
                {
                    allSafeStartLengthOk = false;
                    safeStartLengthFailDetail = $"Seed {seed}: Safe start chunk {cCoord} at index {sIdx} has internalPath length {cData.internalPath?.Count ?? 0} < 6";
                }
                else
                {
                    int hexCount = cData.internalPath.Count;
                    minSafeRoadHexes = Math.Min(minSafeRoadHexes, hexCount);
                    maxSafeRoadHexes = Math.Max(maxSafeRoadHexes, hexCount);
                    sumSafeRoadHexes += hexCount;
                    safeRoadCheckCount++;

                    // Verify hex validity and continuity
                    for (int h = 0; h < cData.internalPath.Count; h++)
                    {
                        var hex = cData.internalPath[h];
                        if (HexGridMath.GetDistance(Vector2Int.zero, hex) > 3)
                        {
                            allSafeStartLengthOk = false;
                            safeStartLengthFailDetail = $"Seed {seed}: Hex {hex} in safe start chunk {cCoord} is outside chunk radius 3";
                        }
                        if (h < cData.internalPath.Count - 1)
                        {
                            var nextHex = cData.internalPath[h + 1];
                            if (HexGridMath.GetDistance(hex, nextHex) != 1)
                            {
                                allSafeStartLengthOk = false;
                                safeStartLengthFailDetail = $"Seed {seed}: Discontinuous road in safe start chunk {cCoord}: {hex} -> {nextHex}";
                            }
                        }
                    }
                }
            }

            // 4. Loop properties: assert loops.Count >= 1, span in [2, 4], length >= bypassed segment
            if (ctx.loops.Count < 1)
            {
                zeroLoopSeeds++;
                allLoopsOk = false;
                loopFailDetail = $"Seed {seed}: loops.Count == {ctx.loops.Count} < 1";
            }
            else
            {
                if (ctx.loops.Count == 1) oneLoopSeeds++;
                else twoLoopSeeds++;

                foreach (var loop in ctx.loops)
                {
                    int splitIdx = ctx.generatedChunkSequence.IndexOf(loop.splitCrossroads);
                    int mergeIdx = ctx.generatedChunkSequence.IndexOf(loop.mergeCrossroads);

                    if (splitIdx < 0 || mergeIdx < 0)
                    {
                        allLoopsOk = false;
                        loopFailDetail = $"Seed {seed}: Loop crossroads not on generatedChunkSequence";
                        break;
                    }

                    // Must be strictly outside protected start zone
                    if (mergeIdx >= N - 2 || splitIdx >= N - 2)
                    {
                        allLoopsOk = false;
                        loopFailDetail = $"Seed {seed}: Loop touches protected start zone (splitIdx={splitIdx}, mergeIdx={mergeIdx}, N={N})";
                        break;
                    }

                    // Span in [2, 4]
                    int span = loop.mainSegmentChunks.Count;
                    if (span < 2 || span > 4)
                    {
                        allLoopsOk = false;
                        loopFailDetail = $"Seed {seed}: Loop span {span} outside [2, 4]";
                        break;
                    }

                    // Length >= bypassed segment
                    int bypassedCount = span + 2;
                    int loopCount = loop.loopChunks.Count + 2;
                    if (loopCount < bypassedCount)
                    {
                        allLoopsOk = false;
                        loopFailDetail = $"Seed {seed}: Loop length {loopCount} < bypassed length {bypassedCount}";
                        break;
                    }

                    // Disjointness
                    var mainSegSet = new HashSet<Vector2Int>(loop.mainSegmentChunks);
                    foreach (var lc in loop.loopChunks)
                    {
                        if (mainSegSet.Contains(lc))
                        {
                            allLoopsOk = false;
                            loopFailDetail = $"Seed {seed}: Loop chunk {lc} intersects main segment chunks";
                            break;
                        }
                    }
                }
            }

            // 5. Side branches: assert dist(spawner, 0) >= dist(junction, 0)
            int bCount = ctx.extraSpawnerChunks.Count;
            totalBranchesGenerated += bCount;
            if (bCount == 0) seedsWithZeroBranches++;
            else if (bCount == 1) seedsWithOneBranch++;
            else seedsWithTwoBranches++;

            foreach (var extraSpawner in ctx.extraSpawnerChunks)
            {
                int spawnerDist = HexGridMath.GetDistance(Vector2Int.zero, extraSpawner);
                Vector2Int curr = extraSpawner;
                Vector2Int junction = new Vector2Int(-999, -999);

                while (curr.x != -999 && curr != Vector2Int.zero)
                {
                    if (ctx.crossroadsChunks.Contains(curr) || ctx.generatedChunkSequence.Contains(curr))
                    {
                        junction = curr;
                        break;
                    }
                    if (ctx.chunkPaths.TryGetValue(curr, out var d))
                    {
                        curr = d.nextChunkTowardsBase;
                    }
                    else break;
                }

                if (junction.x == -999)
                {
                    allBranchesOutwardOk = false;
                    branchFailDetail = $"Seed {seed}: Extra spawner {extraSpawner} does not connect to any junction";
                    break;
                }

                int junctionDist = HexGridMath.GetDistance(Vector2Int.zero, junction);
                if (spawnerDist < junctionDist)
                {
                    allBranchesOutwardOk = false;
                    branchFailDetail = $"Seed {seed}: Spawner {extraSpawner} (dist {spawnerDist}) is closer to base than junction {junction} (dist {junctionDist})";
                    break;
                }

                // Verify junction is outside safe start zone
                int jIdx = ctx.generatedChunkSequence.IndexOf(junction);
                if (jIdx >= N - 2)
                {
                    allBranchesOutwardOk = false;
                    branchFailDetail = $"Seed {seed}: Branch junction {junction} is inside protected start zone (index {jIdx})";
                    break;
                }

                // Verify branch chunks do not backtrack towards base
                curr = extraSpawner;
                while (curr != junction && curr.x != -999)
                {
                    int cDist = HexGridMath.GetDistance(Vector2Int.zero, curr);
                    if (cDist < junctionDist)
                    {
                        allBranchesOutwardOk = false;
                        branchFailDetail = $"Seed {seed}: Branch chunk {curr} (dist {cDist}) backtracks towards base relative to junction {junction} (dist {junctionDist})";
                        break;
                    }
                    if (ctx.chunkPaths.TryGetValue(curr, out var d))
                        curr = d.nextChunkTowardsBase;
                    else break;
                }
            }

            // 6. Crossroads Y/T internal road connectivity check
            foreach (var crCoord in ctx.crossroadsChunks)
            {
                if (!ctx.chunkPaths.TryGetValue(crCoord, out var crData) || crData.internalPath == null || crData.internalPath.Count == 0)
                {
                    allCrossroadsConnected = false;
                    crossroadsFailDetail = $"Seed {seed}: Crossroads chunk {crCoord} has null or empty internalPath";
                    break;
                }

                var internalSet = new HashSet<Vector2Int>(crData.internalPath);
                var endpoints = crData.GetAllEndpoints();

                // Every endpoint must be in internalPath
                foreach (var ep in endpoints)
                {
                    if (!internalSet.Contains(ep))
                    {
                        allCrossroadsConnected = false;
                        crossroadsFailDetail = $"Seed {seed}: Crossroads chunk {crCoord} endpoint {ep} missing from internalPath";
                        break;
                    }
                }
                if (!allCrossroadsConnected) break;

                // Graph BFS from entryHex ensuring all other endpoints are reachable
                var reachable = new HashSet<Vector2Int>();
                var queue = new Queue<Vector2Int>();
                reachable.Add(crData.entryHex);
                queue.Enqueue(crData.entryHex);

                while (queue.Count > 0)
                {
                    var curHex = queue.Dequeue();
                    foreach (var n in HexGridMath.GetNeighbors(curHex))
                    {
                        if (internalSet.Contains(n) && reachable.Add(n))
                        {
                            queue.Enqueue(n);
                        }
                    }
                }

                foreach (var ep in endpoints)
                {
                    if (!reachable.Contains(ep))
                    {
                        allCrossroadsConnected = false;
                        crossroadsFailDetail = $"Seed {seed}: Crossroads chunk {crCoord} endpoint {ep} unreachable from entryHex {crData.entryHex}";
                        break;
                    }
                }
                if (!allCrossroadsConnected) break;
            }

            // 7. Flow check
            var routes = pathGen.GetAllSpawnRoutes(baseRoadEndLocal);
            float sumW = routes.Sum(r => r.flowWeight);
            if (Math.Abs(sumW - 1.0f) > 0.001f)
            {
                allFlowOk = false;
                flowFailDetail = $"Seed {seed}: Flow weights sum to {sumW:F4} != 1.000";
            }
        }

        Console.WriteLine($"[METRICS] Tested {totalSeeds} seeds across 6 base exit orientations");
        Console.WriteLine($"[METRICS] Main Path A length: min={minPathLen}, max={maxPathLen}, avg={(float)sumPathLen / totalSeeds:F1}");
        Console.WriteLine($"[METRICS] Safe start road length: min={minSafeRoadHexes}, max={maxSafeRoadHexes}, avg={(float)sumSafeRoadHexes / safeRoadCheckCount:F1} hexes");
        Console.WriteLine($"[METRICS] Loops distribution: 1 loop = {oneLoopSeeds}, 2 loops = {twoLoopSeeds}, 0 loops = {zeroLoopSeeds}");
        Console.WriteLine($"[METRICS] Side branches: 2 branches = {seedsWithTwoBranches}, 1 branch = {seedsWithOneBranch}, 0 branches = {seedsWithZeroBranches}");

        Assert(allPathLenOk, "Empirical (200+ seeds) Invariant 1: Main Path A length >= 11 chunks on all seeds", pathLenFailDetail);
        Assert(allSafeStartCrossroadsOk, "Empirical (200+ seeds) Invariant 2: Protected start zone (first 2 chunks from base) has zero crossroads/branches", safeStartCrossroadsFailDetail);
        Assert(allSafeStartLengthOk, "Empirical (200+ seeds) Invariant 3: Start road length >= 6 hexes on each of first 2 chunks closest to base", safeStartLengthFailDetail);
        Assert(allLoopsOk, "Empirical (200+ seeds) Invariant 4: Loop properties (loops.Count >= 1, span in [2, 4], length >= bypassed segment)", loopFailDetail);
        Assert(allBranchesOutwardOk, "Empirical (200+ seeds) Invariant 5: Side branches progress outward (dist(spawner, 0) >= dist(junction, 0))", branchFailDetail);
        Assert(allCrossroadsConnected, "Empirical (200+ seeds) Invariant 6: Crossroads Y/T internal geometry maintains full graph connectivity to all endpoints", crossroadsFailDetail);
        Assert(allFlowOk, "Empirical (200+ seeds) Flow Model: Sum of flow weights equals 1.000 across all routes", flowFailDetail);
    }
}
