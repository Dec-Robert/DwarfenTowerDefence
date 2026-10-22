using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Testy EditMode weryfikujące przebudowany system generacji mapy (Fazy 1–4):
/// determinizm MapRng, strefę ochronną (min. 6 heksów drogi na 2 pierwszych chunkach przy bazie),
/// pętle, rozdroża Y/T, Model Przepływu (Flow), spawny na granicy mgły, gwarancje zasobów i symulator 500 map.
/// </summary>
[TestFixture]
public class MapGenerationTests
{
    private List<GameObject> createdObjects;

    [SetUp]
    public void SetUp()
    {
        createdObjects = new List<GameObject>();
    }

    [TearDown]
    public void TearDown()
    {
        if (createdObjects != null)
        {
            for (int i = 0; i < createdObjects.Count; i++)
            {
                if ((object)createdObjects[i] != null)
                {
                    try { UnityEngine.Object.DestroyImmediate(createdObjects[i]); } catch { }
                }
            }
            createdObjects.Clear();
        }
    }

    private HexMapGenerator CreateGenerator()
    {
        HexMapGenerator gen = null;
        try
        {
            var go = new GameObject("__TestHexMapGenerator");
            createdObjects.Add(go);
            gen = go.AddComponent<HexMapGenerator>();
        }
        catch
        {
            gen = (HexMapGenerator)RuntimeHelpers.GetUninitializedObject(typeof(HexMapGenerator));
        }

        gen.mapWidth = 8;
        gen.mapMinY = -6;
        gen.mapMaxY = 6;
        gen.chunkRadius = 3;
        gen.hexSize = 1f;
        gen.padding = 0.02f;
        gen.minChunkDistance = 11;
        gen.maxGenerationAttempts = 50;
        gen.allowExtraSpawners = true;
        gen.extraSpawnerChances = new int[] { 80, 60, 40, 20 };
        gen.extraSpawnDistanceRange = new Vector2Int(2, 6);
        gen.chanceForMoreBranching = 30;
        gen.globalObstacleChance = 40;
        gen.baseRoadEndLocal = new Vector2Int(3, -2);
        gen.topologySettings = new HexMapGenerator.RoadTopologySettings();
        gen.internalSettings = new HexMapGenerator.ChunkStyleSettings();
        gen.terrainConfig = TerrainGenerationConfig.CreateDefault();
        gen.specialChunkTypes = new List<SpecialChunkDefinition>();

        return gen;
    }

    private static List<EnemySpawner.SpawnRoute> EvaluateActiveSpawnerRoutes(
        List<SpawnPathVariant> potentialRoutes,
        Func<Vector3, Vector2Int> worldToChunk,
        Func<Vector2Int, bool> isChunkRevealed)
    {
        var candidateRoutes = new List<EnemySpawner.SpawnRoute>();

        foreach (var variant in potentialRoutes)
        {
            if (variant == null || variant.fullPath == null || variant.fullPath.Count < 2) continue;

            int activeSpawnIndex = variant.fullPath.Count - 1;
            for (int i = variant.fullPath.Count - 1; i >= 0; i--)
            {
                var chunk = worldToChunk(variant.fullPath[i]);
                if (isChunkRevealed(chunk))
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

        var mergedRoutes = new List<EnemySpawner.SpawnRoute>();
        foreach (var candidate in candidateRoutes)
        {
            EnemySpawner.SpawnRoute existingGroup = null;
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

    [Test]
    public void Test01_SameSeed_ProducesIdenticalMapData()
    {
        var gen1 = CreateGenerator();
        var gen2 = CreateGenerator();

        MapGenerationContext ctx1 = gen1.BuildMapData(12345);
        MapGenerationContext ctx2 = gen2.BuildMapData(12345);

        Assert.AreEqual(ctx1.seed, ctx2.seed, "Seeds must match");
        CollectionAssert.AreEqual(ctx1.generatedChunkSequence, ctx2.generatedChunkSequence, "Main path chunk sequences must be identical");
        CollectionAssert.AreEqual(ctx1.extraSpawnerChunks, ctx2.extraSpawnerChunks, "Extra spawners must be identical");
        Assert.AreEqual(ctx1.loops.Count, ctx2.loops.Count, "Loop count must be identical");

        foreach (var chunkCoord in ctx1.allValidChunks)
        {
            Assert.IsTrue(ctx2.worldData.ContainsKey(chunkCoord));
            foreach (var kvp in ctx1.worldData[chunkCoord])
            {
                var cell1 = kvp.Value;
                var cell2 = ctx2.worldData[chunkCoord][kvp.Key];
                Assert.AreEqual(cell1.isPath, cell2.isPath, $"isPath mismatch at {chunkCoord}:{kvp.Key}");
                Assert.AreEqual(cell1.feature, cell2.feature, $"feature mismatch at {chunkCoord}:{kvp.Key}");
                Assert.AreEqual(cell1.featureLevel, cell2.featureLevel, $"featureLevel mismatch at {chunkCoord}:{kvp.Key}");
            }
        }
    }

    [Test]
    public void Test02_ProtectedStartZone_FirstTwoChunksHaveMin6RoadHexesAndNoCrossroads()
    {
        var gen = CreateGenerator();

        for (int seed = 1; seed <= 100; seed++)
        {
            MapGenerationContext ctx = gen.BuildMapData(seed);
            int seqCount = ctx.generatedChunkSequence.Count;
            Assert.GreaterOrEqual(seqCount, 11, $"Seed {seed}: Main Path A must have >= 11 chunks");

            for (int k = 0; k < 2; k++)
            {
                Vector2Int startChunk = ctx.generatedChunkSequence[seqCount - 1 - k];
                Assert.IsFalse(ctx.crossroadsChunks.Contains(startChunk), $"Seed {seed}: Safe start chunk #{k + 1} ({startChunk}) cannot be a crossroads");
                Assert.IsTrue(ctx.chunkPaths.TryGetValue(startChunk, out var pathData), $"Seed {seed}: Missing pathData for start chunk {startChunk}");
                Assert.IsFalse(pathData.isCrossroads, $"Seed {seed}: Start chunk {startChunk} pathData.isCrossroads must be false");
                Assert.IsNotNull(pathData.internalPath, $"Seed {seed}: internalPath is null on start chunk {startChunk}");
                Assert.GreaterOrEqual(pathData.internalPath.Count, 6, $"Seed {seed}: Start chunk #{k + 1} ({startChunk}) must have >= 6 road hexes, got {pathData.internalPath.Count}");
            }
        }
    }

    [Test]
    public void Test03_MainPathAndLoops_ValidTopologyAndFlowWeightsSumToOne()
    {
        var gen = CreateGenerator();

        for (int seed = 1; seed <= 100; seed++)
        {
            MapGenerationContext ctx = gen.BuildMapData(seed);

            // Co najmniej 1 gwarantowana pętla
            Assert.GreaterOrEqual(ctx.loops.Count, 1, $"Seed {seed}: Expected at least 1 loop");

            foreach (var loop in ctx.loops)
            {
                Assert.GreaterOrEqual(loop.loopChunks.Count, 2, $"Seed {seed}: Loop must have >= 2 intermediate chunks");
                foreach (var lc in loop.loopChunks)
                {
                    Assert.IsFalse(loop.mainSegmentChunks.Contains(lc), $"Seed {seed}: Loop chunk {lc} overlaps with mainSegmentChunks");
                }
            }

            // Boczne spawny nie mogą cofać się w stronę bazy względem swojego rozdroża
            foreach (var extraSpawner in ctx.extraSpawnerChunks)
            {
                Vector2Int cur = extraSpawner;
                int guard = 0;
                while (ctx.chunkPaths.TryGetValue(cur, out var pData) && guard++ < 30)
                {
                    Vector2Int next = pData.nextChunkTowardsBase;
                    if (ctx.crossroadsChunks.Contains(next) || next == Vector2Int.zero)
                    {
                        int spawnerDist = HexGridMath.GetDistance(Vector2Int.zero, extraSpawner);
                        int junctionDist = HexGridMath.GetDistance(Vector2Int.zero, next);
                        Assert.GreaterOrEqual(spawnerDist, junctionDist,
                            $"Seed {seed}: Extra spawner {extraSpawner} (dist {spawnerDist}) backtracks closer to base than junction {next} (dist {junctionDist})");
                        break;
                    }
                    cur = next;
                }
            }

            // Suma wag Flow == 1.0
            List<SpawnPathVariant> routes = gen.GetAllSpawnRoutes();
            Assert.IsNotEmpty(routes, $"Seed {seed}: GetAllSpawnRoutes returned empty list");
            float sumFlow = routes.Sum(r => r.flowWeight);
            Assert.AreEqual(1.0f, sumFlow, 0.005f, $"Seed {seed}: Sum of flowWeight across variants must equal 1.0, got {sumFlow}");
        }
    }

    [Test]
    public void Test04_ActiveSpawnerAndLoopInFog_CorrectFrontierAndMergeBehavior()
    {
        var gen = CreateGenerator();
        MapGenerationContext ctx = gen.BuildMapData(42);
        List<SpawnPathVariant> routes = gen.GetAllSpawnRoutes();

        Assert.GreaterOrEqual(ctx.loops.Count, 1, "Seed 42 must have at least 1 loop");
        LoopDefinition loop = ctx.loops[0];

        // 1. Noc 1: Odkryta tylko baza (0,0) i pierwszy chunk przy bazie (gateChunk) -> 1 zintegrowany spawner (100%)
        Vector2Int gateChunk = ctx.generatedChunkSequence[ctx.generatedChunkSequence.Count - 1];
        var revealedNight1 = new HashSet<Vector2Int> { Vector2Int.zero, gateChunk };

        var activeNight1 = EvaluateActiveSpawnerRoutes(
            routes,
            pos => gen.GetChunkCoordFromWorldPosition(pos),
            chunk => revealedNight1.Contains(chunk));

        Assert.AreEqual(1, activeNight1.Count, "Before revealing any crossroads, all routes must consolidate into 1 active spawner");
        Assert.AreEqual(100f, activeNight1[0].currentSpawnChance, 0.1f, "Single pre-crossroads spawner must have 100% spawn chance");

        // 2. Odkryta cała Ścieżka A (w tym Rozdroże 1 i Rozdroże 2), ale chunki Pętli pozostają w mgle
        var revealedMainOnly = new HashSet<Vector2Int>(ctx.generatedChunkSequence) { Vector2Int.zero };
        foreach (var lc in loop.loopChunks)
        {
            revealedMainOnly.Remove(lc);
        }

        var activeLoopInFog = EvaluateActiveSpawnerRoutes(
            routes,
            pos => gen.GetChunkCoordFromWorldPosition(pos),
            chunk => revealedMainOnly.Contains(chunk));

        // Żadna aktywna trasa nie może przechodzić przez nieodkryte chunki pętli
        foreach (var ar in activeLoopInFog)
        {
            foreach (var pos in ar.currentActivePath)
            {
                Vector2Int c = gen.GetChunkCoordFromWorldPosition(pos);
                Assert.IsFalse(loop.loopChunks.Contains(c),
                    $"Active path must never enter unrevealed loop chunk {c}");
            }
        }

        // Musi istnieć spawner na granicy mgły przy Rozdrożu 1 (mergeCrossroads) dla nieodkrytej pętli
        bool hasMergeBoundarySpawner = activeLoopInFog.Any(
            ar => gen.GetChunkCoordFromWorldPosition(ar.currentSpawnPoint) == loop.mergeCrossroads);
        Assert.IsTrue(hasMergeBoundarySpawner,
            $"Unrevealed loop must hold its active spawner at mergeCrossroads ({loop.mergeCrossroads})");
    }

    [Test]
    public void Test05_ResourceGuarantees_100Seeds_Rings1To2AndRings3To4()
    {
        var gen = CreateGenerator();

        for (int seed = 1; seed <= 100; seed++)
        {
            MapGenerationContext ctx = gen.BuildMapData(seed);

            int r12Forest = 0, r12Fertile = 0, r12Hill = 0, r12Mountain = 0;
            int r34Mountain = 0;
            var r34MtnChunks = new HashSet<Vector2Int>();

            foreach (var kvp in ctx.worldData)
            {
                Vector2Int chunkCoord = kvp.Key;
                if (chunkCoord == Vector2Int.zero) continue;

                int ring = HexGridMath.GetDistance(Vector2Int.zero, chunkCoord);
                foreach (var cell in kvp.Value.Values)
                {
                    if (cell.isPath)
                    {
                        Assert.AreEqual(HexFeatureType.None, cell.feature,
                            $"Seed {seed}: Path hex at {chunkCoord}:{cell.localCoord} cannot have natural feature {cell.feature}");
                        continue;
                    }

                    if (ring >= 1 && ring <= 2)
                    {
                        if (cell.feature == HexFeatureType.Forest) r12Forest++;
                        else if (cell.feature == HexFeatureType.FertileSoil) r12Fertile++;
                        else if (cell.feature == HexFeatureType.Hill) r12Hill++;
                        else if (cell.feature == HexFeatureType.Mountain) r12Mountain++;
                    }
                    else if (ring >= 3 && ring <= 4)
                    {
                        if (cell.feature == HexFeatureType.Mountain)
                        {
                            r34Mountain++;
                            r34MtnChunks.Add(chunkCoord);
                        }
                    }
                }
            }

            Assert.AreEqual(0, r12Mountain, $"Seed {seed}: Ring 1-2 Mountain count must be 0");
            Assert.GreaterOrEqual(r12Forest, 10, $"Seed {seed}: Ring 1-2 Forest must be >= 10, got {r12Forest}");
            Assert.GreaterOrEqual(r12Fertile, 6, $"Seed {seed}: Ring 1-2 FertileSoil must be >= 6, got {r12Fertile}");
            Assert.GreaterOrEqual(r12Hill, 6, $"Seed {seed}: Ring 1-2 Hill must be >= 6, got {r12Hill}");
            Assert.GreaterOrEqual(r34Mountain, 10, $"Seed {seed}: Ring 3-4 Mountain must be >= 10, got {r34Mountain}");
            Assert.GreaterOrEqual(r34MtnChunks.Count, 2, $"Seed {seed}: Ring 3-4 Mountain must appear in >= 2 chunks, got {r34MtnChunks.Count}");
        }
    }

    [Test]
    public void Test06_SpecialChunks_NonCollisionWithPathsAndLoops()
    {
        var gen = CreateGenerator();

        SpecialChunkDefinition def = null;
        try
        {
            def = ScriptableObject.CreateInstance<SpecialChunkDefinition>();
        }
        catch
        {
            def = (SpecialChunkDefinition)RuntimeHelpers.GetUninitializedObject(typeof(SpecialChunkDefinition));
        }
        def.chunkId = "TestRuins";
        def.spawnChance = 100;
        def.distanceRange = new Vector2Int(3, 8);
        def.uniquePerMap = true;
        def.guaranteedResources = new List<SpecialChunkDefinition.ResourceDeposit>
        {
            new SpecialChunkDefinition.ResourceDeposit { featureType = HexFeatureType.Forest, count = 3, avoidPaths = true }
        };

        gen.specialChunkTypes = new List<SpecialChunkDefinition> { def };

        for (int seed = 1; seed <= 30; seed++)
        {
            MapGenerationContext ctx = gen.BuildMapData(seed);
            Assert.AreEqual(1, ctx.specialChunkData.Count, $"Seed {seed}: Expected 1 unique special chunk placed");

            Vector2Int placedCoord = ctx.specialChunkData.Keys.First();
            int dist = HexGridMath.GetDistance(Vector2Int.zero, placedCoord);
            Assert.GreaterOrEqual(dist, 3, $"Seed {seed}: Special chunk {placedCoord} dist {dist} < 3");
            Assert.LessOrEqual(dist, 8, $"Seed {seed}: Special chunk {placedCoord} dist {dist} > 8");
            Assert.IsFalse(ctx.chunkPaths.ContainsKey(placedCoord), $"Seed {seed}: Special chunk {placedCoord} collides with enemy path");
            Assert.IsFalse(ctx.crossroadsChunks.Contains(placedCoord), $"Seed {seed}: Special chunk {placedCoord} collides with crossroads");
        }
    }

    [Test]
    public void Test07_SimulatorBatch500Seeds_100PercentSuccessAndGuarantees()
    {
        var report = MapGenerationSimulatorWindow.RunBatchSimulation(500, 1);

        Assert.AreEqual(500, report.totalSeeds);
        Assert.AreEqual(500, report.successfulPathGenerations,
            $"Expected 500/500 successful paths, failed seeds: {string.Join(",", report.failedPathSeeds)}");
        Assert.AreEqual(500, report.guaranteesMetCount,
            $"Expected 500/500 guarantees met, failed seeds: {string.Join(",", report.failedGuaranteeSeeds)}");
        Assert.AreEqual(500, report.startZoneValidCount,
            $"Expected 500/500 valid start zones, failed seeds: {string.Join(",", report.failedStartZoneSeeds)}");
        Assert.GreaterOrEqual(report.minPathLength, 11, "Minimum main path length across 500 seeds must be >= 11");
        Assert.GreaterOrEqual(report.minStartChunkRoadHexes, 6, "Minimum road hexes on first 2 chunks across 500 seeds must be >= 6");
        Assert.GreaterOrEqual(report.minLoops, 1, "Minimum loops across 500 seeds must be >= 1");
    }
}
