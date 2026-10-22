using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pełny wariant trasy od spawnera do bazy wraz z wagą przepływu (Flow Model).
/// </summary>
[System.Serializable]
public class SpawnPathVariant
{
    public string name;
    public List<Vector3> fullPath = new List<Vector3>();
    public float flowWeight = 1.0f; // [0.0 .. 1.0], suma wszystkich wariantów = 1.0
    public Vector2Int spawnerChunk;
    public bool isLoopRoute;
    public Vector2Int loopSplitCrossroads;
    public Vector2Int loopMergeCrossroads;

    public SpawnPathVariant() { }

    public SpawnPathVariant(List<Vector3> path, float weight, string routeName = null)
    {
        fullPath = path ?? new List<Vector3>();
        flowWeight = weight;
        name = routeName;
    }
}

/// <summary>
/// Generuje główną ścieżkę wrogów (Ścieżka A >= 11 chunków), strefę ochronną przy bazie,
/// pętle (Rozdroże 2 -> Rozdroże 1), boczne odnogi (ze spawnerami niecofającymi się ku bazie),
/// czysty kształt Y/T na rozdrożach (hubHex) oraz Model Przepływu (Flow Model).
/// 
/// 100% deterministyczny dzięki ctx.Rng (MapRng).
/// </summary>
public class PathGenerator
{
    private readonly MapGenerationContext ctx;
    private readonly ChunkRegistry registry;
    private readonly HexPathfinder pathfinder;

    private readonly int chunkRadius;
    private readonly float hexSize;
    private readonly float padding;
    private readonly int minChunkDistance;
    private readonly int globalObstacleChance;
    private readonly int chanceForMoreBranching;
    private readonly int[] extraSpawnerChances;
    private readonly Vector2Int extraSpawnDistanceRange;
    private readonly HexMapGenerator.ChunkStyleSettings styleSettings;
    private readonly HexMapGenerator.RoadTopologySettings topologySettings;

    public PathGenerator(
        MapGenerationContext ctx,
        ChunkRegistry registry,
        HexPathfinder pathfinder,
        int chunkRadius, float hexSize, float padding,
        int minChunkDistance, int globalObstacleChance, int chanceForMoreBranching,
        int[] extraSpawnerChances, Vector2Int extraSpawnDistanceRange,
        HexMapGenerator.ChunkStyleSettings styleSettings,
        HexMapGenerator.RoadTopologySettings topologySettings = null)
    {
        this.ctx                     = ctx;
        this.registry                = registry;
        this.pathfinder              = pathfinder;
        this.chunkRadius             = chunkRadius;
        this.hexSize                 = hexSize;
        this.padding                 = padding;
        this.minChunkDistance        = minChunkDistance;
        this.globalObstacleChance    = globalObstacleChance;
        this.chanceForMoreBranching  = chanceForMoreBranching;
        this.extraSpawnerChances     = extraSpawnerChances;
        this.extraSpawnDistanceRange = extraSpawnDistanceRange;
        this.styleSettings           = styleSettings;
        this.topologySettings        = topologySettings ?? new HexMapGenerator.RoadTopologySettings();
    }

    // =========================================================================
    // API Publiczne
    // =========================================================================

    /// <summary>
    /// Wyznacza główną Ścieżkę A o długości >= minChunkDistance (domyślnie 11).
    /// Pierwsze safeStartChunks (2) chunków przy bazie stanowią strefę ochronną
    /// i wymuszają długość drogi >= minStartRoadLength (6 heksów).
    /// </summary>
    public bool CalculateMainPath(Vector2Int baseRoadEndLocal)
    {
        ctx.chunkPaths.Clear();
        ctx.chunkInternalCosts.Clear();
        ctx.loops.Clear();
        ctx.crossroadsChunks.Clear();
        ctx.extraSpawnerChunks.Clear();

        ctx.mainSpawnerChunk = GetFurthestOrRandomChunk();

        Vector3 baseTargetWorld = HexGridMath.GetChunkCenterWorld(Vector2Int.zero, chunkRadius, hexSize, padding)
                                + HexGridMath.AxialToWorld(baseRoadEndLocal.x, baseRoadEndLocal.y, hexSize, padding);
        Vector2Int gateChunk = GetBestGateChunk(baseTargetWorld);

        // Walkable = nie-meta (lub sama baza)
        var walkableChunks = new HashSet<Vector2Int>(
            ctx.allValidChunks.Where(c => !registry.IsMetaChunk(c) || c == Vector2Int.zero));

        // Koszty globalne (użycie ctx.Rng zamiast UnityEngine.Random)
        var globalChunkCosts = new Dictionary<Vector2Int, int>();
        foreach (var chunk in walkableChunks)
        {
            bool isNearBase  = HexGridMath.GetDistance(chunk, Vector2Int.zero) <= 1;
            bool isMainSpawn = chunk == ctx.mainSpawnerChunk;
            globalChunkCosts[chunk] = (isNearBase || isMainSpawn) ? 1
                : (ctx.Rng.Range(0, 100) < globalObstacleChance ? 10 : 1);
        }

        var chunkSequence = pathfinder.FindChunkPath(ctx.mainSpawnerChunk, gateChunk, walkableChunks, globalChunkCosts);
        ctx.generatedChunkSequence = chunkSequence ?? new List<Vector2Int>();

        if (chunkSequence == null || chunkSequence.Count < minChunkDistance) return false;

        Vector3 previousExitWorldPos = Vector3.zero;
        int safeStartThreshold = chunkSequence.Count - topologySettings.safeStartChunks;

        for (int i = 0; i < chunkSequence.Count; i++)
        {
            Vector2Int currentChunk = chunkSequence[i];
            var data = new ChunkPathData();

            data.nextChunkTowardsBase = (i < chunkSequence.Count - 1)
                ? chunkSequence[i + 1]
                : Vector2Int.zero;

            data.entryHex = (i == 0)
                ? Vector2Int.zero
                : FindHexClosestToWorldPos(currentChunk, previousExitWorldPos);

            data.exitHex = (i == chunkSequence.Count - 1)
                ? FindHexClosestToWorldPos(currentChunk, baseTargetWorld)
                : FindRandomHexFacingChunk(currentChunk, chunkSequence[i + 1]);

            previousExitWorldPos = HexGridMath.GetChunkCenterWorld(currentChunk, chunkRadius, hexSize, padding)
                                 + HexGridMath.AxialToWorld(data.exitHex.x, data.exitHex.y, hexSize, padding);

            bool isSafeStart = (i >= safeStartThreshold);
            if (!GenerateAndValidateInternalPath(currentChunk, data, isSafeStart)) return false;

            // Twarde wymuszenie warunku min. 6 heksów na pierwszych 2 chunkach od bazy
            if (isSafeStart && (data.internalPath == null || data.internalPath.Count < topologySettings.minStartRoadLength))
            {
                return false;
            }

            ctx.chunkPaths.Add(currentChunk, data);
        }

        // Oznacz chunk startowy jako Spawner
        ctx.chunkTypes[ctx.mainSpawnerChunk] = ChunkType.Spawner;
        return true;
    }

    /// <summary>
    /// Generuje pętle drogowe (domyślnie 1 gwarantowana + 30% szansy na drugą).
    /// Pętla łączy Rozdroże 2 (dalej od bazy) z Rozdrożem 1 (bliżej bazy) poza strefą ochronną.
    /// Długość pętli >= długość omijanego odcinka Ścieżki A.
    /// </summary>
    public bool GenerateLoops()
    {
        int targetLoops = topologySettings.guaranteedLoops
                        + (ctx.Rng.Range(0, 100) < topologySettings.extraLoopChance ? 1 : 0);

        if (targetLoops <= 0) return true;

        int N = ctx.generatedChunkSequence.Count;
        if (N <= topologySettings.safeStartChunks + 3) return false;

        // Zbierz wszystkie potencjalne pary (splitIdx, mergeIdx)
        // splitIdx = Rozdroże 2 (dalej od bazy -> niższy indeks)
        // mergeIdx = Rozdroże 1 (bliżej bazy -> wyższy indeks)
        var candidatePairs = new List<Vector2Int>();

        int maxMergeIdx = N - topologySettings.safeStartChunks - 1; // wyklucz safeStartChunks przy bazie

        for (int splitIdx = 1; splitIdx <= maxMergeIdx - 2; splitIdx++)
        {
            Vector2Int splitChunk = ctx.generatedChunkSequence[splitIdx];
            if (ctx.crossroadsChunks.Contains(splitChunk)) continue;
            if (ctx.crossroadsChunks.Contains(ctx.generatedChunkSequence[splitIdx - 1])) continue;
            if (ctx.crossroadsChunks.Contains(ctx.generatedChunkSequence[splitIdx + 1])) continue;

            for (int span = topologySettings.loopSpanRange.x; span <= topologySettings.loopSpanRange.y; span++)
            {
                int mergeIdx = splitIdx + span + 1;
                if (mergeIdx > maxMergeIdx) continue;

                Vector2Int mergeChunk = ctx.generatedChunkSequence[mergeIdx];
                if (ctx.crossroadsChunks.Contains(mergeChunk)) continue;
                if (ctx.crossroadsChunks.Contains(ctx.generatedChunkSequence[mergeIdx - 1])) continue;
                if (mergeIdx + 1 < N && ctx.crossroadsChunks.Contains(ctx.generatedChunkSequence[mergeIdx + 1])) continue;

                var splitData = ctx.chunkPaths[splitChunk];
                var mergeData = ctx.chunkPaths[mergeChunk];
                if (splitData.extraEntries.Count > 0 || splitData.extraExits.Count > 0) continue;
                if (mergeData.extraEntries.Count > 0 || mergeData.extraExits.Count > 0) continue;

                candidatePairs.Add(new Vector2Int(splitIdx, mergeIdx));
            }
        }

        ctx.Rng.Shuffle(candidatePairs);

        int loopsCreated = 0;
        foreach (var pair in candidatePairs)
        {
            int splitIdx = pair.x;
            int mergeIdx = pair.y;
            int span = mergeIdx - splitIdx - 1;

            Vector2Int splitChunk = ctx.generatedChunkSequence[splitIdx];
            Vector2Int mergeChunk = ctx.generatedChunkSequence[mergeIdx];

            // Ponownie sprawdź czy żaden z nich nie stał się rozdrożem w poprzedniej iteracji
            if (ctx.crossroadsChunks.Contains(splitChunk) || ctx.crossroadsChunks.Contains(mergeChunk))
                continue;

            // Wolne chunki dla pętli (nie-meta, niebędące na ścieżce, poza punktem docelowym mergeChunk)
            var validForLoop = new HashSet<Vector2Int>();
            foreach (var c in ctx.allValidChunks)
            {
                if (c == Vector2Int.zero || registry.IsMetaChunk(c)) continue;
                if (!ctx.chunkPaths.ContainsKey(c) || c == mergeChunk)
                {
                    validForLoop.Add(c);
                }
            }

            // Koszty zniechęcające do przylegania do omijanego odcinka
            var bypassedChunks = new HashSet<Vector2Int>();
            for (int b = splitIdx + 1; b < mergeIdx; b++)
            {
                bypassedChunks.Add(ctx.generatedChunkSequence[b]);
            }

            var loopCosts = new Dictionary<Vector2Int, int>();
            foreach (var c in validForLoop)
            {
                bool hugsBypassed = HexGridMath.GetNeighbors(c).Any(n => bypassedChunks.Contains(n));
                loopCosts[c] = hugsBypassed ? 5 : 1;
            }

            var loopSequence = pathfinder.FindChunkPath(splitChunk, mergeChunk, validForLoop, loopCosts);
            if (loopSequence == null || loopSequence.Count < 4) continue;

            // Warunek długości: długość pętli w chunkach >= długość omijanego odcinka
            int bypassedChunkCount = span + 2; // split + intermediate + merge
            if (loopSequence.Count < bypassedChunkCount) continue;

            // Upewnij się, że pośrednie chunki pętli nie kolidują z istniejącymi ścieżkami
            bool overlap = false;
            for (int k = 1; k < loopSequence.Count - 1; k++)
            {
                if (ctx.chunkPaths.ContainsKey(loopSequence[k]))
                {
                    overlap = true;
                    break;
                }
            }
            if (overlap) continue;

            // Połączenie pętli
            var splitData = ctx.chunkPaths[splitChunk];
            var mergeData = ctx.chunkPaths[mergeChunk];

            Vector2Int firstLoopChunk = loopSequence[1];
            Vector2Int lastLoopChunk  = loopSequence[loopSequence.Count - 2];

            Vector2Int splitExitHex = FindRandomHexFacingChunk(splitChunk, firstLoopChunk);
            splitData.AddExtraExit(splitExitHex);
            splitData.AddNextChunkTowardsBase(firstLoopChunk);

            Vector3 prevExitWorldPos = HexGridMath.GetChunkCenterWorld(splitChunk, chunkRadius, hexSize, padding)
                                     + HexGridMath.AxialToWorld(splitExitHex.x, splitExitHex.y, hexSize, padding);

            for (int k = 1; k < loopSequence.Count - 1; k++)
            {
                Vector2Int curr = loopSequence[k];
                Vector2Int next = loopSequence[k + 1];

                var cData = new ChunkPathData();
                cData.nextChunkTowardsBase = next;
                cData.entryHex = FindHexClosestToWorldPos(curr, prevExitWorldPos);
                cData.exitHex = (k == loopSequence.Count - 2)
                    ? FindRandomHexFacingChunk(curr, mergeChunk)
                    : FindRandomHexFacingChunk(curr, next);

                prevExitWorldPos = HexGridMath.GetChunkCenterWorld(curr, chunkRadius, hexSize, padding)
                                 + HexGridMath.AxialToWorld(cData.exitHex.x, cData.exitHex.y, hexSize, padding);

                GenerateAndValidateInternalPath(curr, cData);
                ctx.chunkPaths.Add(curr, cData);
            }

            Vector2Int mergeEntryHex = FindHexClosestToWorldPos(mergeChunk, prevExitWorldPos);
            mergeData.AddExtraEntry(mergeEntryHex);

            ctx.crossroadsChunks.Add(splitChunk);
            ctx.crossroadsChunks.Add(mergeChunk);

            var loopDef = new LoopDefinition(splitChunk, mergeChunk, topologySettings.loopFlowRatio)
            {
                mainSegmentChunks = ctx.generatedChunkSequence.GetRange(splitIdx + 1, span),
                loopChunks = loopSequence.GetRange(1, loopSequence.Count - 2)
            };
            ctx.loops.Add(loopDef);

            loopsCreated++;
            if (loopsCreated >= targetLoops) break;
        }

        return ctx.loops.Count >= topologySettings.guaranteedLoops;
    }

    /// <summary>
    /// Generuje boczne ścieżki z własnymi spawnerami (domyślnie 2 gałęzie: 100% i 85%).
    /// Spawny oddalają się od bazy i nie cofają w jej stronę.
    /// Rozdroża posiadają maksymalnie 2 kierunki wyjściowe.
    /// </summary>
    public void GenerateSideBranches()
    {
        if (topologySettings.sideBranchChances == null) return;

        int N = ctx.generatedChunkSequence.Count;
        int maxJunctionIdx = N - topologySettings.safeStartChunks - 1;

        foreach (int chance in topologySettings.sideBranchChances)
        {
            if (ctx.Rng.Range(0, 100) >= chance) continue;

            // Zbierz kandydatów na rozdroże na Ścieżce A
            var candidateJunctions = new List<int>();
            for (int idx = 1; idx <= maxJunctionIdx; idx++)
            {
                Vector2Int c = ctx.generatedChunkSequence[idx];
                if (ctx.crossroadsChunks.Contains(c)) continue;
                if (ctx.crossroadsChunks.Contains(ctx.generatedChunkSequence[idx - 1])) continue;
                if (idx + 1 < N && ctx.crossroadsChunks.Contains(ctx.generatedChunkSequence[idx + 1])) continue;

                if (!ctx.chunkPaths.TryGetValue(c, out var data)) continue;
                if (data.extraEntries.Count > 0 || data.extraExits.Count > 0) continue;

                candidateJunctions.Add(idx);
            }

            if (candidateJunctions.Count == 0) continue;
            ctx.Rng.Shuffle(candidateJunctions);

            bool branchCreated = false;
            foreach (int jIdx in candidateJunctions)
            {
                Vector2Int junctionChunk = ctx.generatedChunkSequence[jIdx];
                int jDistFromBase = HexGridMath.GetDistance(Vector2Int.zero, junctionChunk);

                // Szukaj spawnera bocznego: dist >= jDistFromBase (brak cofania ku bazie!)
                var spawnerCandidates = ctx.allValidChunks
                    .Where(c => c != Vector2Int.zero && !registry.IsMetaChunk(c) && !ctx.chunkPaths.ContainsKey(c))
                    .Where(c =>
                    {
                        int dJ = HexGridMath.GetDistance(junctionChunk, c);
                        int dBase = HexGridMath.GetDistance(Vector2Int.zero, c);
                        return dJ >= extraSpawnDistanceRange.x && dJ <= extraSpawnDistanceRange.y
                            && dBase >= jDistFromBase;
                    })
                    .OrderByDescending(c => HexGridMath.GetDistance(Vector2Int.zero, c))
                    .ToList();

                if (spawnerCandidates.Count == 0) continue;

                // Wybierz spawner z puli najdalszych
                int pickPool = Mathf.Min(spawnerCandidates.Count, 3);
                Vector2Int newSpawner = spawnerCandidates[ctx.Rng.Range(0, pickPool)];

                var validForBranch = new HashSet<Vector2Int>(ctx.allValidChunks.Where(c =>
                    c != Vector2Int.zero && !registry.IsMetaChunk(c) && (!ctx.chunkPaths.ContainsKey(c) || c == junctionChunk)));

                var branchChunks = pathfinder.FindChunkPath(newSpawner, junctionChunk, validForBranch, null);
                if (branchChunks == null || branchChunks.Count < 2) continue;

                // Weryfikacja braku cofania ku bazie wzdłuż całej gałęzi
                bool backtracks = false;
                foreach (var bc in branchChunks)
                {
                    if (HexGridMath.GetDistance(Vector2Int.zero, bc) < jDistFromBase)
                    {
                        backtracks = true;
                        break;
                    }
                }
                if (backtracks) continue;

                // Zbuduj ścieżkę gałęzi od spawnera do rozdroża
                Vector3 prevExitWorldPos = Vector3.zero;
                for (int i = 0; i < branchChunks.Count - 1; i++)
                {
                    Vector2Int current = branchChunks[i];
                    Vector2Int next    = branchChunks[i + 1];

                    var bData = new ChunkPathData();
                    bData.nextChunkTowardsBase = next;
                    bData.entryHex = (i == 0) ? Vector2Int.zero : FindHexClosestToWorldPos(current, prevExitWorldPos);
                    bData.exitHex  = FindRandomHexFacingChunk(current, next);

                    prevExitWorldPos = HexGridMath.GetChunkCenterWorld(current, chunkRadius, hexSize, padding)
                                     + HexGridMath.AxialToWorld(bData.exitHex.x, bData.exitHex.y, hexSize, padding);

                    GenerateAndValidateInternalPath(current, bData);
                    ctx.chunkPaths.Add(current, bData);
                }

                // Podłącz dodatkowe wejście do chunku rozdroża
                var jData = ctx.chunkPaths[junctionChunk];
                Vector2Int jEntry = FindHexClosestToWorldPos(junctionChunk, prevExitWorldPos);
                jData.AddExtraEntry(jEntry);

                ctx.crossroadsChunks.Add(junctionChunk);
                ctx.extraSpawnerChunks.Add(newSpawner);
                ctx.chunkTypes[newSpawner] = ChunkType.Spawner;

                branchCreated = true;
                break;
            }

            if (!branchCreated)
            {
                // Kontynuuj dla kolejnej szansy
            }
        }
    }

    /// <summary>
    /// Zgodność wsteczna z poprzednim pipeline. Deleguje do GenerateSideBranches.
    /// </summary>
    public void GenerateRecursiveBranches()
    {
        GenerateSideBranches();
    }

    /// <summary>
    /// Buduje czysty kształt węzła Y / T wewnątrz chunku rozdroża z centralnym hubHex (dist <= 1).
    /// </summary>
    public void BuildCrossroadsInternalPath(Vector2Int chunkCoord, ChunkPathData data)
    {
        var endpoints = data.GetAllEndpoints();
        if (endpoints == null || endpoints.Count == 0) return;

        // 1. Wyznacz geometryczny środek ciężkości wszystkich wejść i wyjść
        Vector3 centroidWorld = Vector3.zero;
        foreach (var ep in endpoints)
        {
            centroidWorld += HexGridMath.AxialToWorld(ep.x, ep.y, hexSize, padding);
        }
        centroidWorld /= endpoints.Count;

        // 2. Znajdź hubHex w obrębie promienia <= 1 od środka chunku (0,0)
        var hubCandidates = new List<Vector2Int> { Vector2Int.zero };
        foreach (var n in HexGridMath.GetNeighbors(Vector2Int.zero)) hubCandidates.Add(n);

        Vector2Int hubHex = Vector2Int.zero;
        float minDst = float.MaxValue;
        foreach (var cand in hubCandidates)
        {
            Vector3 candWorld = HexGridMath.AxialToWorld(cand.x, cand.y, hexSize, padding);
            float d = Vector3.Distance(candWorld, centroidWorld);
            if (d < minDst)
            {
                minDst = d;
                hubHex = cand;
            }
        }

        // 3. Zbuduj główny korytarz: entryHex -> hubHex -> exitHex
        var baseCosts = new Dictionary<Vector2Int, int>();
        GenerateHexGridPoints(chunkRadius, (q, r) => baseCosts[new Vector2Int(q, r)] = 1);

        var p1 = pathfinder.FindLocalPath(data.entryHex, hubHex, baseCosts);
        var p2 = pathfinder.FindLocalPath(hubHex, data.exitHex, baseCosts);
        var mainCorridor = new List<Vector2Int>(p1);
        for (int k = 1; k < p2.Count; k++)
        {
            if (!mainCorridor.Contains(p2[k])) mainCorridor.Add(p2[k]);
        }

        // 4. Koszty dla gałęzi bocznej (wysoki koszt na heksach głównego korytarza poza hubHex i sąsiadami)
        var branchCosts = new Dictionary<Vector2Int, int>(baseCosts);
        foreach (var h in mainCorridor)
        {
            if (h != hubHex && HexGridMath.GetDistance(h, hubHex) > 1)
            {
                branchCosts[h] = styleSettings.wallCost * 5;
            }
        }

        var mergedPath = new HashSet<Vector2Int>(mainCorridor);

        // Dodatkowe wejścia (odnoga boczna lub powrót pętli)
        if (data.extraEntries != null)
        {
            foreach (var extraEntry in data.extraEntries)
            {
                var pBranch = pathfinder.FindLocalPath(extraEntry, hubHex, branchCosts);
                if (pBranch == null || pBranch.Count == 0)
                    pBranch = pathfinder.FindLocalPath(extraEntry, hubHex, baseCosts);
                if (pBranch != null)
                {
                    foreach (var h in pBranch) mergedPath.Add(h);
                }
            }
        }

        // Dodatkowe wyjścia (odgałęzienie pętli Rozdroże 2)
        if (data.extraExits != null)
        {
            foreach (var extraExit in data.extraExits)
            {
                var pBranch = pathfinder.FindLocalPath(hubHex, extraExit, branchCosts);
                if (pBranch == null || pBranch.Count == 0)
                    pBranch = pathfinder.FindLocalPath(hubHex, extraExit, baseCosts);
                if (pBranch != null)
                {
                    foreach (var h in pBranch) mergedPath.Add(h);
                }
            }
        }

        data.internalPath = mergedPath.ToList();
        ctx.chunkInternalCosts[chunkCoord] = branchCosts;
    }

    /// <summary>
    /// Przebudowuje wewnętrzne trasy wszystkich zarejestrowanych rozdroży do czystego kształtu Y/T.
    /// </summary>
    public void RebuildAllCrossroadsInternalPaths()
    {
        foreach (var coord in ctx.crossroadsChunks)
        {
            if (ctx.chunkPaths.TryGetValue(coord, out var data))
            {
                BuildCrossroadsInternalPath(coord, data);
            }
        }
    }

    /// <summary>
    /// Zwraca zależności ujawniania dróg dla pojedynczego poprzednika (wsteczna kompatybilność).
    /// </summary>
    public Dictionary<Vector2Int, Vector2Int> GetRoadRevealDependencies()
    {
        var deps = new Dictionary<Vector2Int, Vector2Int>();
        foreach (var kvp in ctx.chunkPaths)
            if (kvp.Value.nextChunkTowardsBase.x != -999 && !deps.ContainsKey(kvp.Key))
                deps.Add(kvp.Key, kvp.Value.nextChunkTowardsBase);
        return deps;
    }

    /// <summary>
    /// Zwraca pełne zależności ujawniania dróg (z wieloma poprzednikami dla pętli i rozdroży).
    /// </summary>
    public Dictionary<Vector2Int, List<Vector2Int>> GetRoadRevealDependenciesMulti()
    {
        var deps = new Dictionary<Vector2Int, List<Vector2Int>>();
        foreach (var kvp in ctx.chunkPaths)
        {
            if (kvp.Value.nextChunksTowardsBase != null && kvp.Value.nextChunksTowardsBase.Count > 0)
                deps[kvp.Key] = new List<Vector2Int>(kvp.Value.nextChunksTowardsBase);
        }
        return deps;
    }

    /// <summary>
    /// Zwraca wszystkie warianty tras od docelowych spawnerów do Bazy z wyliczonymi wagami Flow Model.
    /// Suma wag flowWeight wszystkich wariantów wynosi dokładnie 1.0f.
    /// </summary>
    public List<SpawnPathVariant> GetAllSpawnRoutes(Vector2Int baseRoadEndLocal)
    {
        var variants = new List<SpawnPathVariant>();
        Vector3 baseTarget = HexGridMath.GetChunkCenterWorld(Vector2Int.zero, chunkRadius, hexSize, padding)
                           + HexGridMath.AxialToWorld(baseRoadEndLocal.x, baseRoadEndLocal.y, hexSize, padding);

        if (ctx.generatedChunkSequence == null || ctx.generatedChunkSequence.Count == 0)
            return variants;

        // 1. Zbuduj główną trasę Ścieżki A
        var mainRoute = BuildMainPathARoute(baseTarget);
        if (mainRoute != null)
        {
            variants.Add(mainRoute);
        }

        // 2. Zbuduj warianty tras dla każdej pętli
        foreach (var loop in ctx.loops)
        {
            var loopRoute = BuildLoopRoute(loop, baseTarget);
            if (loopRoute != null)
            {
                variants.Add(loopRoute);
            }
        }

        // 3. Zbuduj warianty tras dla każdego bocznego spawnera
        foreach (var extraSpawn in ctx.extraSpawnerChunks)
        {
            var branchRoute = BuildBranchRoute(extraSpawn, baseTarget);
            if (branchRoute != null)
            {
                variants.Add(branchRoute);
            }
        }

        // 4. Model Przepływu (Flow Model) – wyznacz wagi przepływu drzewa od Bazy
        CalculateFlowWeights(variants);

        return variants;
    }

    /// <summary>
    /// Zwraca listę punktów Vector3 tras spawnerów (wsteczna kompatybilność).
    /// </summary>
    public List<List<Vector3>> GetAllSpawnPaths(Vector2Int baseRoadEndLocal)
    {
        var routes = GetAllSpawnRoutes(baseRoadEndLocal);
        var paths = new List<List<Vector3>>();
        foreach (var r in routes)
        {
            if (r.fullPath != null && r.fullPath.Count > 0)
                paths.Add(r.fullPath);
        }
        return paths;
    }

    // =========================================================================
    // Prywatne metody pomocnicze tras i Flow Model
    // =========================================================================

    private SpawnPathVariant BuildMainPathARoute(Vector3 baseTarget)
    {
        var waypoints = new List<Vector3>();

        for (int i = 0; i < ctx.generatedChunkSequence.Count; i++)
        {
            Vector2Int chunk = ctx.generatedChunkSequence[i];
            if (!ctx.chunkPaths.TryGetValue(chunk, out var data)) continue;

            Vector2Int entry = data.entryHex;
            Vector2Int exit  = data.exitHex;

            var chunkPts = GetChunkWorldWaypoints(chunk, entry, exit);
            waypoints.AddRange(chunkPts);
        }

        waypoints.Add(baseTarget);

        return new SpawnPathVariant
        {
            name = "Main Route (Path A)",
            fullPath = waypoints,
            spawnerChunk = ctx.mainSpawnerChunk,
            isLoopRoute = false,
            flowWeight = 1.0f
        };
    }

    private SpawnPathVariant BuildLoopRoute(LoopDefinition loop, Vector3 baseTarget)
    {
        var waypoints = new List<Vector3>();

        int splitIdx = ctx.generatedChunkSequence.IndexOf(loop.splitCrossroads);
        int mergeIdx = ctx.generatedChunkSequence.IndexOf(loop.mergeCrossroads);
        if (splitIdx < 0 || mergeIdx < 0) return null;

        // Od spawnera do Rozdroża 2 włącznie
        for (int i = 0; i < splitIdx; i++)
        {
            Vector2Int chunk = ctx.generatedChunkSequence[i];
            if (!ctx.chunkPaths.TryGetValue(chunk, out var data)) continue;
            waypoints.AddRange(GetChunkWorldWaypoints(chunk, data.entryHex, data.exitHex));
        }

        // Rozdroże 2 (Split): wejście od upstream Path A, wyjście w stronę pętli (extraExits[0])
        if (ctx.chunkPaths.TryGetValue(loop.splitCrossroads, out var splitData))
        {
            Vector2Int splitExit = splitData.extraExits.Count > 0 ? splitData.extraExits[0] : splitData.exitHex;
            waypoints.AddRange(GetChunkWorldWaypoints(loop.splitCrossroads, splitData.entryHex, splitExit));
        }

        // Chunki pośrednie pętli
        foreach (var lChunk in loop.loopChunks)
        {
            if (!ctx.chunkPaths.TryGetValue(lChunk, out var lData)) continue;
            waypoints.AddRange(GetChunkWorldWaypoints(lChunk, lData.entryHex, lData.exitHex));
        }

        // Rozdroże 1 (Merge): wejście z pętli (extraEntries[0]), wyjście w stronę bazy (exitHex)
        if (ctx.chunkPaths.TryGetValue(loop.mergeCrossroads, out var mergeData))
        {
            Vector2Int mergeEntry = mergeData.extraEntries.Count > 0 ? mergeData.extraEntries[0] : mergeData.entryHex;
            waypoints.AddRange(GetChunkWorldWaypoints(loop.mergeCrossroads, mergeEntry, mergeData.exitHex));
        }

        // Kontynuacja Ścieżki A od mergeIdx + 1 do Gate Chunk
        for (int i = mergeIdx + 1; i < ctx.generatedChunkSequence.Count; i++)
        {
            Vector2Int chunk = ctx.generatedChunkSequence[i];
            if (!ctx.chunkPaths.TryGetValue(chunk, out var data)) continue;
            waypoints.AddRange(GetChunkWorldWaypoints(chunk, data.entryHex, data.exitHex));
        }

        waypoints.Add(baseTarget);

        return new SpawnPathVariant
        {
            name = $"Loop Route (via {loop.splitCrossroads} -> {loop.mergeCrossroads})",
            fullPath = waypoints,
            spawnerChunk = ctx.mainSpawnerChunk,
            isLoopRoute = true,
            loopSplitCrossroads = loop.splitCrossroads,
            loopMergeCrossroads = loop.mergeCrossroads,
            flowWeight = loop.loopFlowRatio
        };
    }

    private SpawnPathVariant BuildBranchRoute(Vector2Int spawnerChunk, Vector3 baseTarget)
    {
        var chunkSeq = new List<Vector2Int>();
        Vector2Int curr = spawnerChunk;

        // Śledź trasę po nextChunkTowardsBase do bazy
        while (curr.x != -999 && curr != Vector2Int.zero && !chunkSeq.Contains(curr))
        {
            chunkSeq.Add(curr);
            if (ctx.chunkPaths.TryGetValue(curr, out var d))
            {
                curr = d.nextChunkTowardsBase;
            }
            else break;
        }

        if (chunkSeq.Count == 0) return null;

        var waypoints = new List<Vector3>();

        for (int i = 0; i < chunkSeq.Count; i++)
        {
            Vector2Int chunk = chunkSeq[i];
            if (!ctx.chunkPaths.TryGetValue(chunk, out var data)) continue;

            Vector2Int entry = data.entryHex;
            Vector2Int exit  = data.exitHex;

            // Jeśli to rozdroże, na którym gałąź łączy się ze Ścieżką A:
            if (i > 0 && data.isCrossroads)
            {
                Vector2Int prevChunk = chunkSeq[i - 1];
                Vector3 prevCenter = HexGridMath.GetChunkCenterWorld(prevChunk, chunkRadius, hexSize, padding);
                entry = FindHexClosestToWorldPos(chunk, prevCenter);
            }

            waypoints.AddRange(GetChunkWorldWaypoints(chunk, entry, exit));
        }

        waypoints.Add(baseTarget);

        return new SpawnPathVariant
        {
            name = $"Branch Route (from {spawnerChunk})",
            fullPath = waypoints,
            spawnerChunk = spawnerChunk,
            isLoopRoute = false,
            flowWeight = topologySettings.branchFlowRatio
        };
    }

    private List<Vector3> GetChunkWorldWaypoints(Vector2Int chunkCoord, Vector2Int entryHex, Vector2Int exitHex)
    {
        var waypoints = new List<Vector3>();
        if (!ctx.chunkPaths.TryGetValue(chunkCoord, out var data)) return waypoints;
        Vector3 center = HexGridMath.GetChunkCenterWorld(chunkCoord, chunkRadius, hexSize, padding);

        if (data.isCrossroads)
        {
            var subpath = GetCrossroadsSubpath(data, entryHex, exitHex);
            if (subpath != null && subpath.Count > 0)
            {
                foreach (var h in subpath)
                    waypoints.Add(center + HexGridMath.AxialToWorld(h.x, h.y, hexSize, padding));
                return waypoints;
            }
        }

        if (data.internalPath != null)
        {
            foreach (var h in data.internalPath)
                waypoints.Add(center + HexGridMath.AxialToWorld(h.x, h.y, hexSize, padding));
        }

        return waypoints;
    }

    private List<Vector2Int> GetCrossroadsSubpath(ChunkPathData data, Vector2Int entry, Vector2Int exit)
    {
        var costs = new Dictionary<Vector2Int, int>();
        GenerateHexGridPoints(chunkRadius, (q, r) =>
        {
            var h = new Vector2Int(q, r);
            costs[h] = (data.internalPath != null && data.internalPath.Contains(h)) ? 1 : 9999;
        });
        costs[entry] = 1;
        costs[exit]  = 1;

        var path = pathfinder.FindLocalPath(entry, exit, costs);
        if (path != null && path.Count > 0) return path;

        return data.internalPath ?? new List<Vector2Int>();
    }

    private void CalculateFlowWeights(List<SpawnPathVariant> variants)
    {
        if (variants == null || variants.Count == 0) return;

        // Model Przepływu (Flow Model):
        // Zaczynamy od Bazy z przepływem 1.0.
        // Idziemy w górę Ścieżki A od Gate Chunk do Main Spawner.
        // Na rozdrożu bocznej gałęzi: boczna gałąź dostaje branchFlowRatio (30%), a główna nitka (1 - 30% = 70%).
        // Na Rozdrożu 1 pętli: pętla dostaje loopFlowRatio (30%), a omijany odcinek Ścieżki A dostaje (1 - 30% = 70%).
        // Przepływ, który dociera do Main Spawner, rozdziela się pomiędzy Main Route a Loop Route(s).
        float currentFlow = 1.0f;
        int N = ctx.generatedChunkSequence.Count;

        var branchMap = new Dictionary<Vector2Int, SpawnPathVariant>();
        foreach (var v in variants)
        {
            if (!v.isLoopRoute && v.spawnerChunk != ctx.mainSpawnerChunk)
            {
                branchMap[v.spawnerChunk] = v;
            }
        }

        float loopDetourShare = topologySettings.loopFlowRatio;
        bool hasLoop = ctx.loops.Count > 0;

        for (int i = N - 1; i >= 0; i--)
        {
            Vector2Int chunk = ctx.generatedChunkSequence[i];

            // Czy to rozdroże bocznej gałęzi?
            foreach (var kvp in branchMap)
            {
                // Sprawdź czy ta gałąź łączy się z tym chunkiem
                if (ctx.chunkPaths.TryGetValue(kvp.Key, out var bData))
                {
                    // Śledź czy prowadzi do chunk
                    Vector2Int walker = kvp.Key;
                    while (walker.x != -999 && walker != Vector2Int.zero && walker != chunk)
                    {
                        if (ctx.chunkPaths.TryGetValue(walker, out var wData))
                            walker = wData.nextChunkTowardsBase;
                        else break;
                    }

                    if (walker == chunk && !kvp.Value.name.EndsWith("[ASSIGNED]"))
                    {
                        float bFlow = currentFlow * topologySettings.branchFlowRatio;
                        kvp.Value.flowWeight = bFlow;
                        kvp.Value.name += "[ASSIGNED]";
                        currentFlow *= (1f - topologySettings.branchFlowRatio);
                    }
                }
            }
        }

        // Oczyść znaczniki tymczasowe z nazw
        foreach (var v in variants)
        {
            if (v.name.EndsWith("[ASSIGNED]"))
                v.name = v.name.Substring(0, v.name.Length - "[ASSIGNED]".Length);
        }

        // Pozostały przepływ na Main Spawnerze rozdziela się na Main Route i Loop Route(s)
        var mainRoute = variants.FirstOrDefault(v => !v.isLoopRoute && v.spawnerChunk == ctx.mainSpawnerChunk);
        var loopRoutes = variants.Where(v => v.isLoopRoute).ToList();

        if (mainRoute != null)
        {
            if (hasLoop && loopRoutes.Count > 0)
            {
                float totalLoopRatio = loopDetourShare * loopRoutes.Count;
                if (totalLoopRatio >= 0.9f) totalLoopRatio = 0.5f;

                float mainRatio = 1f - totalLoopRatio;
                mainRoute.flowWeight = currentFlow * mainRatio;

                float perLoop = (currentFlow * totalLoopRatio) / loopRoutes.Count;
                foreach (var lr in loopRoutes)
                {
                    lr.flowWeight = perLoop;
                }
            }
            else
            {
                mainRoute.flowWeight = currentFlow;
            }
        }

        // Twarda normalizacja sumy wag do 1.000f
        float total = variants.Sum(v => v.flowWeight);
        if (total > 0.0001f)
        {
            foreach (var v in variants) v.flowWeight /= total;
        }
    }

    // =========================================================================
    // Generowanie wnętrza chunków i meandrowanie (>= 6 heksów)
    // =========================================================================

    private bool GenerateAndValidateInternalPath(Vector2Int chunkCoord, ChunkPathData data, bool isSafeStartChunk = false)
    {
        int style        = isSafeStartChunk ? (ctx.Rng.Range(0, 2) == 0 ? 1 : 2) : RollStyle();
        int baseDist     = HexGridMath.GetDistance(data.entryHex, data.exitHex);
        int shortestLen  = baseDist + 1;
        int minLenTarget = style == 1 ? shortestLen + 2 : style == 2 ? shortestLen + 4 : shortestLen;
        if (isSafeStartChunk)
        {
            minLenTarget = Mathf.Max(topologySettings.minStartRoadLength, minLenTarget);
        }

        List<Vector2Int> bestPath  = new List<Vector2Int>();
        Dictionary<Vector2Int, int> bestCosts = new Dictionary<Vector2Int, int>();
        int bestLen = -1;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            int wallCount = (style == 1) ? styleSettings.windingWallCount : styleSettings.mazeWallCount;
            if (style == 2) wallCount += attempt < 8 ? 2 : attempt > 14 ? -2 : 0;

            var costs = GenerateStructuredCosts(style, data.entryHex, data.exitHex, wallCount);
            var path  = pathfinder.FindLocalPath(data.entryHex, data.exitHex, costs);

            if (path.Count > 0)
            {
                if (path.Count >= minLenTarget)
                {
                    data.internalPath = path;
                    ctx.chunkInternalCosts[chunkCoord] = costs;
                    return true;
                }
                if (path.Count > bestLen)
                {
                    bestLen = path.Count;
                    bestPath = path;
                    bestCosts = costs;
                }
            }
        }

        if (bestLen != -1 && (!isSafeStartChunk || bestLen >= topologySettings.minStartRoadLength))
        {
            data.internalPath = bestPath;
            ctx.chunkInternalCosts[chunkCoord] = bestCosts;
            return true;
        }

        // Gdy normalne próby nie spełniły wymogu na startowym chunku – uruchom algorytm meandrowania
        if (isSafeStartChunk)
        {
            return EnsureMeanderingPath(chunkCoord, data, topologySettings.minStartRoadLength);
        }

        data.internalPath = pathfinder.FindLocalPath(data.entryHex, data.exitHex, new Dictionary<Vector2Int, int>());
        ctx.chunkInternalCosts[chunkCoord] = new Dictionary<Vector2Int, int>();
        return true;
    }

    private bool EnsureMeanderingPath(Vector2Int chunkCoord, ChunkPathData data, int minLength)
    {
        // 1. Sprawdź czy aktualna prosta ścieżka już spełnia warunek
        var directPath = (data.internalPath != null && data.internalPath.Count > 0)
            ? data.internalPath
            : pathfinder.FindLocalPath(data.entryHex, data.exitHex, new Dictionary<Vector2Int, int>());

        if (directPath != null && directPath.Count >= minLength)
        {
            data.internalPath = directPath;
            if (!ctx.chunkInternalCosts.ContainsKey(chunkCoord))
                ctx.chunkInternalCosts[chunkCoord] = new Dictionary<Vector2Int, int>();
            return true;
        }

        // 2. Metoda A: Wstrzykiwanie kosztów ściany na heksy bezpośredniego skrótu
        var blockedCosts = new Dictionary<Vector2Int, int>();
        GenerateHexGridPoints(chunkRadius, (q, r) => blockedCosts[new Vector2Int(q, r)] = 1);

        for (int iter = 0; iter < 10; iter++)
        {
            if (directPath != null && directPath.Count >= 3)
            {
                for (int p = 1; p < directPath.Count - 1; p++)
                {
                    blockedCosts[directPath[p]] = styleSettings.wallCost;
                }
            }
            blockedCosts[data.entryHex] = 1;
            blockedCosts[data.exitHex] = 1;

            var detourPath = pathfinder.FindLocalPath(data.entryHex, data.exitHex, blockedCosts);
            if (detourPath != null && detourPath.Count >= minLength)
            {
                data.internalPath = detourPath;
                ctx.chunkInternalCosts[chunkCoord] = blockedCosts;
                return true;
            }

            if (detourPath != null && detourPath.Count > 0)
            {
                directPath = detourPath;
            }
            else
            {
                break;
            }
        }

        // 3. Metoda B: Detour Waypoints (tranzyt przez środek chunku)
        var candidateWaypoints = new List<Vector2Int>();
        GenerateHexGridPoints(chunkRadius - 1, (q, r) =>
        {
            var h = new Vector2Int(q, r);
            if (h != data.entryHex && h != data.exitHex)
            {
                int distEntry = HexGridMath.GetDistance(data.entryHex, h);
                int distExit  = HexGridMath.GetDistance(h, data.exitHex);
                if (distEntry + distExit + 1 >= minLength)
                {
                    candidateWaypoints.Add(h);
                }
            }
        });

        // Preferuj centrum chunku (0,0) i promień 1
        candidateWaypoints.Sort((a, b) =>
        {
            int da = HexGridMath.GetDistance(Vector2Int.zero, a);
            int db = HexGridMath.GetDistance(Vector2Int.zero, b);
            return da.CompareTo(db);
        });

        foreach (var wp in candidateWaypoints)
        {
            var p1 = pathfinder.FindLocalPath(data.entryHex, wp, new Dictionary<Vector2Int, int>());
            if (p1 == null || p1.Count == 0) continue;

            var p2Costs = new Dictionary<Vector2Int, int>();
            for (int p = 0; p < p1.Count - 1; p++)
            {
                p2Costs[p1[p]] = styleSettings.wallCost;
            }
            p2Costs[wp] = 1;
            p2Costs[data.exitHex] = 1;

            var p2 = pathfinder.FindLocalPath(wp, data.exitHex, p2Costs);
            if (p2 == null || p2.Count == 0)
            {
                p2 = pathfinder.FindLocalPath(wp, data.exitHex, new Dictionary<Vector2Int, int>());
            }

            if (p2 != null && p2.Count > 0)
            {
                var combined = new List<Vector2Int>(p1);
                for (int k = 1; k < p2.Count; k++)
                {
                    if (!combined.Contains(p2[k]))
                    {
                        combined.Add(p2[k]);
                    }
                }

                if (combined.Count >= minLength && combined[combined.Count - 1] == data.exitHex)
                {
                    data.internalPath = combined;
                    ctx.chunkInternalCosts[chunkCoord] = p2Costs;
                    return true;
                }
            }
        }

        // 4. Metoda C: Deterministyczny bounded DFS poszukujący ścieżki prostej >= minLength
        var dfsPath = FindSimplePathWithMinLength(data.entryHex, data.exitHex, minLength);
        if (dfsPath != null && dfsPath.Count >= minLength)
        {
            data.internalPath = dfsPath;
            ctx.chunkInternalCosts[chunkCoord] = new Dictionary<Vector2Int, int>();
            return true;
        }

        return false;
    }

    private List<Vector2Int> FindSimplePathWithMinLength(Vector2Int start, Vector2Int goal, int minLength, int maxDepth = 12)
    {
        var visited = new HashSet<Vector2Int> { start };
        var currentPath = new List<Vector2Int> { start };
        List<Vector2Int> result = null;

        bool Dfs(Vector2Int current)
        {
            if (current == goal)
            {
                if (currentPath.Count >= minLength)
                {
                    result = new List<Vector2Int>(currentPath);
                    return true;
                }
                return false;
            }

            if (currentPath.Count >= maxDepth) return false;

            var neighbors = HexGridMath.GetNeighbors(current)
                .Where(n =>
                {
                    int d = HexGridMath.GetDistance(Vector2Int.zero, n);
                    if (d > chunkRadius) return false;
                    if (d == chunkRadius && n != start && n != goal) return false;
                    return !visited.Contains(n);
                })
                .OrderByDescending(n => HexGridMath.GetDistance(n, goal))
                .ToList();

            foreach (var n in neighbors)
            {
                visited.Add(n);
                currentPath.Add(n);
                if (Dfs(n)) return true;
                currentPath.RemoveAt(currentPath.Count - 1);
                visited.Remove(n);
            }

            return false;
        }

        Dfs(start);
        return result;
    }

    // =========================================================================
    // Deterministyczne generowanie kosztów i wybory (ctx.Rng)
    // =========================================================================

    private Dictionary<Vector2Int, int> GenerateStructuredCosts(int style, Vector2Int entry, Vector2Int exit, int wallCount)
    {
        var costs = new Dictionary<Vector2Int, int>();
        GenerateHexGridPoints(chunkRadius, (q, r) => costs[new Vector2Int(q, r)] = 1);
        if (style == 0) { costs[entry] = 1; costs[exit] = 1; return costs; }

        int wallLength = (style == 1) ? styleSettings.windingWallLength : styleSettings.mazeWallLength;
        if (style == 2) wallLength += ctx.Rng.Range(0, 3);

        var allHexes = new List<Vector2Int>(costs.Keys);
        for (int w = 0; w < wallCount; w++)
        {
            Vector2Int current = allHexes[ctx.Rng.Range(0, allHexes.Count)];
            for (int step = 0; step < wallLength; step++)
            {
                if (current != entry && current != exit) costs[current] = styleSettings.wallCost;
                var validN = HexGridMath.GetNeighbors(current).Where(n => costs.ContainsKey(n)).ToList();
                if (validN.Count > 0) current = validN[ctx.Rng.Range(0, validN.Count)]; else break;
            }
        }

        if (style == 1)
        {
            Vector2Int center = Vector2Int.RoundToInt((Vector2)(entry + exit) / 2f);
            costs[center] = styleSettings.wallCost;
            foreach (var n in HexGridMath.GetNeighbors(center))
                if (costs.ContainsKey(n) && n != entry && n != exit) costs[n] = styleSettings.wallCost;
        }

        costs[entry] = 1;
        costs[exit]  = 1;
        return costs;
    }

    private int RollStyle()
    {
        int totalWeight = styleSettings.highwayWeight + styleSettings.windingWeight + styleSettings.mazeWeight;
        int r = ctx.Rng.Range(0, totalWeight);
        if (r < styleSettings.highwayWeight) return 0;
        if (r < styleSettings.highwayWeight + styleSettings.windingWeight) return 1;
        return 2;
    }

    private Vector2Int GetFurthestOrRandomChunk()
    {
        int maxDist = 0;
        foreach (var c in ctx.allValidChunks)
        {
            if (c == Vector2Int.zero || registry.IsMetaChunk(c)) continue;
            int d = HexGridMath.GetDistance(Vector2Int.zero, c);
            if (d > maxDist) maxDist = d;
        }

        int target    = Mathf.Min(minChunkDistance, maxDist);
        int minAccept = Mathf.Max(1, target - 1);
        var valid     = ctx.allValidChunks
            .Where(c => c != Vector2Int.zero && !registry.IsMetaChunk(c)
                     && HexGridMath.GetDistance(Vector2Int.zero, c) >= minAccept)
            .ToList();

        return valid.Count > 0 ? valid[ctx.Rng.Range(0, valid.Count)] : new Vector2Int(1, 0);
    }

    private Vector2Int FindValidExtraSpawnChunk(Vector2Int center)
    {
        var candidates = ctx.allValidChunks
            .Where(c => !ctx.chunkPaths.ContainsKey(c) && !registry.IsMetaChunk(c) && c != Vector2Int.zero)
            .Where(c => { int d = HexGridMath.GetDistance(center, c); return d >= extraSpawnDistanceRange.x && d <= extraSpawnDistanceRange.y; })
            .ToList();

        return candidates.Count > 0 ? candidates[ctx.Rng.Range(0, candidates.Count)] : Vector2Int.zero;
    }

    private Vector2Int GetBestGateChunk(Vector3 targetWorldPos)
    {
        Vector2Int best = new Vector2Int(1, 0);
        float minDst    = float.MaxValue;
        foreach (var n in HexGridMath.GetNeighbors(Vector2Int.zero))
        {
            float d = Vector3.Distance(HexGridMath.GetChunkCenterWorld(n, chunkRadius, hexSize, padding), targetWorldPos);
            if (d < minDst) { minDst = d; best = n; }
        }
        return best;
    }

    private Vector2Int FindHexClosestToWorldPos(Vector2Int chunkCoord, Vector3 targetWorldPos)
    {
        Vector2Int best = Vector2Int.zero;
        float minDst    = float.MaxValue;
        Vector3 center  = HexGridMath.GetChunkCenterWorld(chunkCoord, chunkRadius, hexSize, padding);

        GenerateHexGridPoints(chunkRadius, (q, r) =>
        {
            float d = Vector3.Distance(center + HexGridMath.AxialToWorld(q, r, hexSize, padding), targetWorldPos);
            if (d < minDst) { minDst = d; best = new Vector2Int(q, r); }
        });

        return best;
    }

    private Vector2Int FindRandomHexFacingChunk(Vector2Int currentChunk, Vector2Int nextChunk)
    {
        Vector3 currentCenter = HexGridMath.GetChunkCenterWorld(currentChunk, chunkRadius, hexSize, padding);
        Vector3 nextCenter    = HexGridMath.GetChunkCenterWorld(nextChunk, chunkRadius, hexSize, padding);

        var candidates = new List<Vector2Int>();
        GenerateHexGridPoints(chunkRadius, (q, r) =>
        {
            if ((Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(q + r)) / 2 == chunkRadius)
                candidates.Add(new Vector2Int(q, r));
        });

        candidates.Sort((a, b) =>
        {
            float da = Vector3.Distance(currentCenter + HexGridMath.AxialToWorld(a.x, a.y, hexSize, padding), nextCenter);
            float db = Vector3.Distance(currentCenter + HexGridMath.AxialToWorld(b.x, b.y, hexSize, padding), nextCenter);
            return da.CompareTo(db);
        });

        int count = Mathf.Min(candidates.Count, 3);
        return count > 0 ? candidates[ctx.Rng.Range(0, count)] : Vector2Int.zero;
    }

    private void GenerateHexGridPoints(int radius, System.Action<int, int> action)
    {
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Mathf.Max(-radius, -q - radius);
            int r2 = Mathf.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++) action(q, r);
        }
    }
}