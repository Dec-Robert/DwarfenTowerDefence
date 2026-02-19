using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Generuje główną ścieżkę wrogów, gałęzie oraz spawnerów.
/// Odpowiada za chunkPaths, chunkInternalCosts, generatedChunkSequence.
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

    public PathGenerator(
        MapGenerationContext ctx,
        ChunkRegistry registry,
        HexPathfinder pathfinder,
        int chunkRadius, float hexSize, float padding,
        int minChunkDistance, int globalObstacleChance, int chanceForMoreBranching,
        int[] extraSpawnerChances, Vector2Int extraSpawnDistanceRange,
        HexMapGenerator.ChunkStyleSettings styleSettings)
    {
        this.ctx                    = ctx;
        this.registry               = registry;
        this.pathfinder             = pathfinder;
        this.chunkRadius            = chunkRadius;
        this.hexSize                = hexSize;
        this.padding                = padding;
        this.minChunkDistance       = minChunkDistance;
        this.globalObstacleChance   = globalObstacleChance;
        this.chanceForMoreBranching = chanceForMoreBranching;
        this.extraSpawnerChances    = extraSpawnerChances;
        this.extraSpawnDistanceRange = extraSpawnDistanceRange;
        this.styleSettings          = styleSettings;
    }

    // =========================================================================
    // API Publiczne
    // =========================================================================

    public bool CalculateMainPath(Vector2Int baseRoadEndLocal)
    {
        ctx.chunkPaths.Clear();
        ctx.chunkInternalCosts.Clear();

        ctx.mainSpawnerChunk = GetFurthestOrRandomChunk();

        Vector3 baseTargetWorld = HexGridMath.GetChunkCenterWorld(Vector2Int.zero, chunkRadius, hexSize, padding)
                                + HexGridMath.AxialToWorld(baseRoadEndLocal.x, baseRoadEndLocal.y, hexSize, padding);
        Vector2Int gateChunk = GetBestGateChunk(baseTargetWorld);

        // Walkable = nie-meta (lub sama baza)
        var walkableChunks = new HashSet<Vector2Int>(
            ctx.allValidChunks.Where(c => !registry.IsMetaChunk(c) || c == Vector2Int.zero));

        // Koszty globalne
        var globalChunkCosts = new Dictionary<Vector2Int, int>();
        foreach (var chunk in walkableChunks)
        {
            bool isNearBase  = HexGridMath.GetDistance(chunk, Vector2Int.zero) <= 1;
            bool isMainSpawn = chunk == ctx.mainSpawnerChunk;
            globalChunkCosts[chunk] = (isNearBase || isMainSpawn) ? 1
                : (Random.Range(0, 100) < globalObstacleChance ? 10 : 1);
        }

        var chunkSequence = pathfinder.FindChunkPath(ctx.mainSpawnerChunk, gateChunk, walkableChunks, globalChunkCosts);
        ctx.generatedChunkSequence = chunkSequence ?? new List<Vector2Int>();

        if (chunkSequence == null || chunkSequence.Count < minChunkDistance) return false;

        Vector3 previousExitWorldPos = Vector3.zero;

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

            if (!GenerateAndValidateInternalPath(currentChunk, data)) return false;

            ctx.chunkPaths.Add(currentChunk, data);
        }

        // Oznacz chunk startowy jako Spawner
        ctx.chunkTypes[ctx.mainSpawnerChunk] = ChunkType.Spawner;
        return true;
    }

    public void GenerateRecursiveBranches()
    {
        int totalExtra = extraSpawnerChances.Count(c => Random.Range(0, 100) < c);
        if (totalExtra == 0) return;

        var potentialJunctions = ctx.generatedChunkSequence.Count > 2
            ? ctx.generatedChunkSequence.Skip(1).Take(ctx.generatedChunkSequence.Count - 2).ToList()
            : new List<Vector2Int>();

        var usedJunctions = new HashSet<Vector2Int>();
        int spawnersLeft = totalExtra;

        while (spawnersLeft > 0)
        {
            var candidates = potentialJunctions.Where(c => !usedJunctions.Contains(c)).ToList();
            if (candidates.Count == 0) break;

            var junction = candidates[Random.Range(0, candidates.Count)];
            usedJunctions.Add(junction);

            int batchSize = Mathf.Min(spawnersLeft >= 2 ? 2 : 1, 6 - ctx.chunkPaths[junction].GetAllEntries().Count);
            while (batchSize < spawnersLeft && Random.Range(0, 100) < chanceForMoreBranching) batchSize++;
            batchSize = Mathf.Min(batchSize, 6 - ctx.chunkPaths[junction].GetAllEntries().Count);
            if (batchSize <= 0) continue;

            for (int i = 0; i < batchSize; i++)
            {
                var newBranchPath = TryGenerateBranch(junction);
                if (newBranchPath != null && newBranchPath.Count > 0)
                {
                    spawnersLeft--;
                    foreach (var c in newBranchPath.Skip(1).Take(newBranchPath.Count - 2))
                        if (!potentialJunctions.Contains(c)) potentialJunctions.Add(c);
                }
            }

            var jData = ctx.chunkPaths[junction];
            if (jData.extraEntries.Count > 0) RegenerateJunctionInternal(junction, jData);
        }
    }

    public Dictionary<Vector2Int, Vector2Int> GetRoadRevealDependencies()
    {
        var deps = new Dictionary<Vector2Int, Vector2Int>();
        foreach (var kvp in ctx.chunkPaths)
            if (kvp.Value.nextChunkTowardsBase.x != -999 && !deps.ContainsKey(kvp.Key))
                deps.Add(kvp.Key, kvp.Value.nextChunkTowardsBase);
        return deps;
    }

    public List<List<Vector3>> GetAllSpawnPaths(Vector2Int baseRoadEndLocal)
    {
        var allPaths    = new List<List<Vector3>>();
        var allRoadNodes = new HashSet<Vector3>();

        Vector3 baseTarget = HexGridMath.GetChunkCenterWorld(Vector2Int.zero, chunkRadius, hexSize, padding)
                           + HexGridMath.AxialToWorld(baseRoadEndLocal.x, baseRoadEndLocal.y, hexSize, padding);
        allRoadNodes.Add(baseTarget);

        foreach (var kvp in ctx.chunkPaths)
        {
            Vector3 center = HexGridMath.GetChunkCenterWorld(kvp.Key, chunkRadius, hexSize, padding);
            if (kvp.Value.internalPath != null)
                foreach (var h in kvp.Value.internalPath)
                    allRoadNodes.Add(center + HexGridMath.AxialToWorld(h.x, h.y, hexSize, padding));
        }

        var starts = new List<Vector2Int> { ctx.mainSpawnerChunk };
        starts.AddRange(ctx.extraSpawnerChunks);

        foreach (var startChunk in starts)
        {
            if (!ctx.chunkPaths.TryGetValue(startChunk, out var startData)) continue;
            Vector3 center   = HexGridMath.GetChunkCenterWorld(startChunk, chunkRadius, hexSize, padding);
            Vector3 startPos = (startData.internalPath?.Count > 0)
                ? center + HexGridMath.AxialToWorld(startData.internalPath[0].x, startData.internalPath[0].y, hexSize, padding)
                : center;

            var path = FindPathOnRoadGraph(startPos, baseTarget, allRoadNodes);
            if (path.Count > 0) allPaths.Add(path);
        }

        return allPaths;
    }

    // =========================================================================
    // Prywatne
    // =========================================================================

    private List<Vector2Int> TryGenerateBranch(Vector2Int junctionChunk)
    {
        Vector2Int newSpawn = FindValidExtraSpawnChunk(junctionChunk);
        if (newSpawn == Vector2Int.zero) return null;

        var validForBranch = new HashSet<Vector2Int>(ctx.allValidChunks);
        foreach (var c in ctx.chunkPaths.Keys)
            if (c != junctionChunk) validForBranch.Remove(c);
        foreach (var c in validForBranch.Where(c => registry.IsMetaChunk(c) && c != Vector2Int.zero).ToList())
            validForBranch.Remove(c);

        var branchChunks = pathfinder.FindChunkPath(newSpawn, junctionChunk, validForBranch, null);
        if (branchChunks == null || branchChunks.Count < 2) return null;

        Vector3 prevExitWorldPos = Vector3.zero;

        for (int i = 0; i < branchChunks.Count - 1; i++)
        {
            Vector2Int current = branchChunks[i];
            var data = new ChunkPathData();
            data.nextChunkTowardsBase = branchChunks[i + 1];
            data.entryHex = (i == 0) ? Vector2Int.zero : FindHexClosestToWorldPos(current, prevExitWorldPos);

            Vector2Int next = branchChunks[i + 1];
            data.exitHex = FindRandomHexFacingChunk(current, next);

            if (next == junctionChunk && ctx.chunkPaths.TryGetValue(junctionChunk, out var jData))
            {
                Vector3 exitWorld = HexGridMath.GetChunkCenterWorld(current, chunkRadius, hexSize, padding)
                                  + HexGridMath.AxialToWorld(data.exitHex.x, data.exitHex.y, hexSize, padding);
                Vector2Int jEntry = FindHexClosestToWorldPos(junctionChunk, exitWorld);
                if (!jData.extraEntries.Contains(jEntry)) jData.extraEntries.Add(jEntry);
            }

            prevExitWorldPos = HexGridMath.GetChunkCenterWorld(current, chunkRadius, hexSize, padding)
                             + HexGridMath.AxialToWorld(data.exitHex.x, data.exitHex.y, hexSize, padding);

            GenerateAndValidateInternalPath(current, data);
            if (!ctx.chunkPaths.ContainsKey(current)) ctx.chunkPaths.Add(current, data);
        }

        ctx.extraSpawnerChunks.Add(newSpawn);
        ctx.chunkTypes[newSpawn] = ChunkType.Spawner;
        return branchChunks;
    }

    private void RegenerateJunctionInternal(Vector2Int chunkCoord, ChunkPathData data)
    {
        int seed  = chunkCoord.GetHashCode() + 9999 + data.extraEntries.Count;
        int style = Random.Range(0, 2) == 0 ? 0 : 1;
        var costs = GenerateStructuredCosts(seed, style, data.entryHex, data.exitHex, styleSettings.windingWallCount);

        foreach (var e in data.GetAllEntries()) costs[e] = 1;
        costs[data.exitHex] = 1;

        var mergedPath = new HashSet<Vector2Int>();
        bool criticalFail = false;

        foreach (var entry in data.GetAllEntries())
        {
            var path = pathfinder.FindLocalPath(entry, data.exitHex, costs);
            if (path.Count == 0) { criticalFail = true; break; }
            foreach (var h in path) mergedPath.Add(h);
        }

        if (criticalFail)
        {
            costs = new Dictionary<Vector2Int, int>();
            mergedPath.Clear();
            foreach (var entry in data.GetAllEntries())
                foreach (var h in pathfinder.FindLocalPath(entry, data.exitHex, costs))
                    mergedPath.Add(h);
        }

        data.internalPath = mergedPath.ToList();
        ctx.chunkInternalCosts[chunkCoord] = costs;
    }

    private bool GenerateAndValidateInternalPath(Vector2Int chunkCoord, ChunkPathData data)
    {
        int style        = RollStyle();
        int baseDist     = HexGridMath.GetDistance(data.entryHex, data.exitHex);
        int shortestLen  = baseDist + 1;
        int minLenTarget = style == 1 ? shortestLen + 3 : style == 2 ? shortestLen + 6 : shortestLen;

        List<Vector2Int> bestPath  = new List<Vector2Int>();
        Dictionary<Vector2Int, int> bestCosts = new Dictionary<Vector2Int, int>();
        int bestLen = -1;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            int seed = chunkCoord.GetHashCode() + attempt + (int)System.DateTime.Now.Ticks;
            int wallCount = (style == 1) ? styleSettings.windingWallCount : styleSettings.mazeWallCount;
            if (style == 2) wallCount += attempt < 8 ? 2 : attempt > 14 ? -2 : 0;

            var costs = GenerateStructuredCosts(seed, style, data.entryHex, data.exitHex, wallCount);
            var path  = pathfinder.FindLocalPath(data.entryHex, data.exitHex, costs);

            if (path.Count > 0)
            {
                if (style > 0 && path.Count >= minLenTarget)
                {
                    data.internalPath = path;
                    ctx.chunkInternalCosts[chunkCoord] = costs;
                    return true;
                }
                if (path.Count > bestLen) { bestLen = path.Count; bestPath = path; bestCosts = costs; }
            }
        }

        if (bestLen != -1) { data.internalPath = bestPath; ctx.chunkInternalCosts[chunkCoord] = bestCosts; return true; }

        data.internalPath = pathfinder.FindLocalPath(data.entryHex, data.exitHex, new Dictionary<Vector2Int, int>());
        ctx.chunkInternalCosts[chunkCoord] = new Dictionary<Vector2Int, int>();
        return true;
    }

    private Dictionary<Vector2Int, int> GenerateStructuredCosts(int seed, int style, Vector2Int entry, Vector2Int exit, int wallCount)
    {
        var costs = new Dictionary<Vector2Int, int>();
        var rng   = new System.Random(seed);
        GenerateHexGridPoints(chunkRadius, (q, r) => costs[new Vector2Int(q, r)] = 1);
        if (style == 0) { costs[entry] = 1; costs[exit] = 1; return costs; }

        int wallLength = (style == 1) ? styleSettings.windingWallLength : styleSettings.mazeWallLength;
        if (style == 2) wallLength += rng.Next(0, 3);

        var allHexes = new List<Vector2Int>(costs.Keys);
        for (int w = 0; w < wallCount; w++)
        {
            Vector2Int current = allHexes[rng.Next(allHexes.Count)];
            for (int step = 0; step < wallLength; step++)
            {
                if (current != entry && current != exit) costs[current] = styleSettings.wallCost;
                var validN = HexGridMath.GetNeighbors(current).Where(n => costs.ContainsKey(n)).ToList();
                if (validN.Count > 0) current = validN[rng.Next(validN.Count)]; else break;
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
        int r = Random.Range(0, styleSettings.highwayWeight + styleSettings.windingWeight + styleSettings.mazeWeight);
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

        int target      = Mathf.Min(minChunkDistance, maxDist);
        int minAccept   = Mathf.Max(1, target - 1);
        var valid       = ctx.allValidChunks
            .Where(c => c != Vector2Int.zero && !registry.IsMetaChunk(c)
                     && HexGridMath.GetDistance(Vector2Int.zero, c) >= minAccept)
            .ToList();

        return valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : new Vector2Int(1, 0);
    }

    private Vector2Int FindValidExtraSpawnChunk(Vector2Int center)
    {
        var candidates = ctx.allValidChunks
            .Where(c => !ctx.chunkPaths.ContainsKey(c) && !registry.IsMetaChunk(c))
            .Where(c => { int d = HexGridMath.GetDistance(center, c); return d >= extraSpawnDistanceRange.x && d <= extraSpawnDistanceRange.y; })
            .ToList();

        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : Vector2Int.zero;
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
        return count > 0 ? candidates[Random.Range(0, count)] : Vector2Int.zero;
    }

    private List<Vector3> FindPathOnRoadGraph(Vector3 startNode, Vector3 targetNode, HashSet<Vector3> allNodes)
    {
        if (!allNodes.Contains(startNode)) startNode = GetClosestNode(startNode, allNodes);
        if (!allNodes.Contains(targetNode)) targetNode = GetClosestNode(targetNode, allNodes);

        var openSet  = new List<Vector3> { startNode };
        var closed   = new HashSet<Vector3>();
        var cameFrom = new Dictionary<Vector3, Vector3>();
        var gScore   = new Dictionary<Vector3, float> { [startNode] = 0 };
        var fScore   = new Dictionary<Vector3, float> { [startNode] = Vector3.Distance(startNode, targetNode) };

        while (openSet.Count > 0)
        {
            Vector3 current  = openSet.OrderBy(n => fScore.TryGetValue(n, out float f) ? f : float.MaxValue).First();
            if (Vector3.Distance(current, targetNode) < 0.1f) return ReconstructVectorPath(cameFrom, current);

            openSet.Remove(current);
            closed.Add(current);

            foreach (var neighbor in GetNeighborsFromSet(current, allNodes))
            {
                if (closed.Contains(neighbor)) continue;
                float tG = gScore[current] + Vector3.Distance(current, neighbor);
                if (!gScore.ContainsKey(neighbor) || tG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor]   = tG;
                    fScore[neighbor]   = tG + Vector3.Distance(neighbor, targetNode);
                    if (!openSet.Contains(neighbor)) openSet.Add(neighbor);
                }
            }
        }
        return new List<Vector3>();
    }

    private List<Vector3> GetNeighborsFromSet(Vector3 center, HashSet<Vector3> allNodes)
    {
        float threshold = hexSize * 1.9f;
        float thSq      = threshold * threshold;
        return allNodes.Where(n => n != center && (n - center).sqrMagnitude < thSq).ToList();
    }

    private Vector3 GetClosestNode(Vector3 target, HashSet<Vector3> nodes)
    {
        Vector3 best = target;
        float minDst = float.MaxValue;
        foreach (var n in nodes) { float d = Vector3.Distance(n, target); if (d < minDst) { minDst = d; best = n; } }
        return best;
    }

    private List<Vector3> ReconstructVectorPath(Dictionary<Vector3, Vector3> cameFrom, Vector3 current)
    {
        var path = new List<Vector3> { current };
        while (cameFrom.ContainsKey(current)) { current = cameFrom[current]; path.Add(current); }
        path.Reverse();
        return path;
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