using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class HexMapGenerator : MonoBehaviour
{
    [Header("Referencje")]
    public HexMapVisualizer visualizer; // <--- TUTAJ PRZYPISZ NOWY KOMPONENT
    public FogOfWarManager fogManager;

    [Header("Rozmiar Mapy")]
    public int mapWidth = 8;
    public int mapMinY = -6;
    public int mapMaxY = 6;

    [Header("Ustawienia Chunku")]
    public int chunkRadius = 4;
    public float hexSize = 1f;
    public float padding = 0.02f;

    [Header("Logika Drogi")]
    public int minChunkDistance = 6;
    public int maxGenerationAttempts = 50;
    public bool allowExtraSpawners = true;
    public int[] extraSpawnerChances = { 80, 60, 40, 20 };
    public Vector2Int extraSpawnDistanceRange = new Vector2Int(2, 6);
    [Range(0, 100)] public int chanceForMoreBranching = 30;
    [Range(0, 100)] public int globalObstacleChance = 40;

    [Header("Ustawienia Terenu")]
    public int biomeSeedCount = 3;
    [Range(0, 100)] public int forestClusterChance = 30;
    [Range(0, 100)] public int mountainChance = 15;
    [Range(0, 100)] public int hillChance = 20;
    [Range(0, 100)] public int sinkholeChance = 10;
    [Range(0, 100)] public int fertileSoilChance = 15;

    [Header("Punkty Strategiczne")]
    public Vector2Int baseRoadEndLocal = new Vector2Int(3, -2);

    // --- DANE ---
    public Dictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>> worldData = new Dictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>>();
    private Dictionary<Vector2Int, BiomeType> chunkBiomes = new Dictionary<Vector2Int, BiomeType>();

    // --- WEWNĘTRZNE ---
    [System.Serializable]
    public class ChunkStyleSettings
    {
        [Range(0, 100)] public int highwayWeight = 20;
        [Range(0, 100)] public int windingWeight = 40;
        [Range(0, 100)] public int mazeWeight = 40;
        public int wallCost = 50;
        public int windingWallCount = 3;
        public int windingWallLength = 3;
        public int mazeWallCount = 10;
        public int mazeWallLength = 4;
    }
    public ChunkStyleSettings internalSettings;

    public class ChunkPathData
    {
        public Vector2Int entryHex;
        public List<Vector2Int> extraEntries = new List<Vector2Int>();
        public Vector2Int exitHex;
        public List<Vector2Int> internalPath;
        public List<Vector2Int> GetAllEntries() { var l = new List<Vector2Int> { entryHex }; l.AddRange(extraEntries); return l; }
    }

    private Dictionary<Vector2Int, ChunkPathData> chunkPaths = new Dictionary<Vector2Int, ChunkPathData>();
    private Dictionary<Vector2Int, Dictionary<Vector2Int, int>> chunkInternalCosts = new Dictionary<Vector2Int, Dictionary<Vector2Int, int>>();

    private Vector2Int mainSpawnerChunk;
    private List<Vector2Int> extraSpawnerChunks = new List<Vector2Int>();
    private HashSet<Vector2Int> allValidChunks = new HashSet<Vector2Int>();
    private List<Vector2Int> generatedChunkSequence = new List<Vector2Int>();
    private HexPathfinder pathfinder;

    private void Start()
    {
        pathfinder = new HexPathfinder(chunkRadius);
        GenerateMap();
    }

    [ContextMenu("Generuj Mapę")]
    public void GenerateMap()
    {
        if (visualizer == null) { Debug.LogError("Brak przypisanego Visualizera!"); return; }

        pathfinder = new HexPathfinder(chunkRadius);
        chunkPaths.Clear(); chunkInternalCosts.Clear();
        worldData.Clear(); chunkBiomes.Clear();
        allValidChunks.Clear(); generatedChunkSequence.Clear();
        extraSpawnerChunks.Clear();

        RegisterAllChunks();

        bool success = false;
        for (int i = 0; i < maxGenerationAttempts; i++)
        {
            if (CalculateMainPath())
            {
                if (allowExtraSpawners) GenerateRecursiveBranches();
                success = true;
                Debug.Log($"<color=green>Mapa logiczna gotowa.</color>");
                break;
            }
        }
        if (!success) Debug.LogWarning("Nie udało się wygenerować tras.");

        GenerateBiomeMap();
        GenerateTerrain();

        // DELEGOWANIE RYSOWANIA DO WIZUALIZERA
        visualizer.VisualizeWorld(worldData, allValidChunks, chunkRadius, hexSize, padding);

        if (fogManager != null)
        {
            fogManager.InitializeFog(allValidChunks, chunkRadius, hexSize, padding);
            HandleInitialFogReveal();
        }
    }

    // --- LOGIKA TERENU ---
    void GenerateBiomeMap()
    {
        List<Vector2Int> biomeCenters = new List<Vector2Int> { Vector2Int.zero };
        Dictionary<Vector2Int, BiomeType> centerTypes = new Dictionary<Vector2Int, BiomeType> { { Vector2Int.zero, BiomeType.Plains } };

        List<Vector2Int> validCandidates = allValidChunks.ToList();
        validCandidates.Remove(Vector2Int.zero);
        int centersToSpawn = Mathf.Min(biomeSeedCount, validCandidates.Count);

        for (int i = 0; i < centersToSpawn; i++)
        {
            Vector2Int rndChunk = validCandidates[Random.Range(0, validCandidates.Count)];
            validCandidates.Remove(rndChunk);
            biomeCenters.Add(rndChunk);
            centerTypes.Add(rndChunk, (BiomeType)Random.Range(0, 3));
        }

        foreach (var chunk in allValidChunks)
        {
            Vector2Int closestCenter = Vector2Int.zero;
            float minDst = float.MaxValue;
            foreach (var center in biomeCenters)
            {
                float d = HexGridMath.GetDistance(chunk, center);
                if (d < minDst) { minDst = d; closestCenter = center; }
            }
            chunkBiomes[chunk] = centerTypes[closestCenter];
        }
    }

    void GenerateTerrain()
    {
        foreach (var chunkCoord in allValidChunks)
        {
            worldData[chunkCoord] = new Dictionary<Vector2Int, HexCellData>();
            BiomeType currentBiome = chunkBiomes[chunkCoord];

            GenerateHexGridPoints(chunkRadius, (q, r) => {
                HexCellData cell = new HexCellData();
                cell.chunkCoord = chunkCoord;
                cell.localCoord = new Vector2Int(q, r);
                worldData[chunkCoord].Add(new Vector2Int(q, r), cell);
            });

            if (chunkPaths.ContainsKey(chunkCoord))
            {
                var pathData = chunkPaths[chunkCoord];
                if (pathData.internalPath != null)
                {
                    foreach (var pathHex in pathData.internalPath)
                    {
                        if (worldData[chunkCoord].ContainsKey(pathHex))
                            worldData[chunkCoord][pathHex].isPath = true;
                    }
                }
            }
            FillChunkFeatures(chunkCoord, currentBiome);
        }

        SetFeature(Vector2Int.zero, Vector2Int.zero, HexFeatureType.Base);
        Vector2Int beaconPos = new Vector2Int(0, 1);
        if (!IsPath(Vector2Int.zero, beaconPos)) SetFeature(Vector2Int.zero, beaconPos, HexFeatureType.Beacon);
        else SetFeature(Vector2Int.zero, new Vector2Int(1, 0), HexFeatureType.Beacon);
    }

    void FillChunkFeatures(Vector2Int chunkCoord, BiomeType biome)
    {
        var chunkCells = worldData[chunkCoord];
        List<Vector2Int> availableHexes = chunkCells.Keys.Where(k => !chunkCells[k].isPath).ToList();

        int forestChance = forestClusterChance;
        int mntChance = mountainChance;
        int hlChance = hillChance;
        int sinkChance = sinkholeChance;

        if (biome == BiomeType.Forest) { forestChance += 40; mntChance -= 10; }
        if (biome == BiomeType.Mountains) { forestChance -= 20; mntChance += 30; hlChance += 20; sinkChance -= 10; }

        int numClusters = Random.Range(1, 4);
        for (int i = 0; i < numClusters; i++)
        {
            if (Random.Range(0, 100) < forestChance && availableHexes.Count > 0)
            {
                Vector2Int center = availableHexes[Random.Range(0, availableHexes.Count)];
                List<Vector2Int> cluster = new List<Vector2Int> { center };
                foreach (var n in HexGridMath.GetNeighbors(center)) cluster.Add(n);
                foreach (var hex in cluster)
                {
                    if (chunkCells.ContainsKey(hex) && !chunkCells[hex].isPath && chunkCells[hex].feature == HexFeatureType.None)
                    {
                        chunkCells[hex].feature = HexFeatureType.Forest;
                        availableHexes.Remove(hex);
                    }
                }
            }
        }

        availableHexes = chunkCells.Keys.Where(k => !chunkCells[k].isPath && chunkCells[k].feature == HexFeatureType.None).ToList();
        foreach (var hex in availableHexes)
        {
            int roll = Random.Range(0, 100);
            if (roll < mntChance) chunkCells[hex].feature = HexFeatureType.Mountain;
            else if (roll < mntChance + hlChance) { chunkCells[hex].feature = HexFeatureType.Hill; chunkCells[hex].featureLevel = Random.Range(1, 6); }
            else if (roll < mntChance + hlChance + sinkChance) { chunkCells[hex].feature = HexFeatureType.Sinkhole; chunkCells[hex].featureLevel = Random.Range(-5, 0); }
            else if (roll < mntChance + hlChance + sinkChance + fertileSoilChance) { if (biome != BiomeType.Mountains) chunkCells[hex].feature = HexFeatureType.FertileSoil; }
        }
    }

    // =================================================================================
    //                    PATHFINDING I POMOCNIKI (Bez zmian logiki)
    // =================================================================================

    bool CalculateMainPath()
    {
        chunkPaths.Clear(); chunkInternalCosts.Clear();
        mainSpawnerChunk = GetFurthestOrRandomChunk();
        Vector3 baseTargetWorld = HexGridMath.GetChunkCenterWorld(Vector2Int.zero, chunkRadius, hexSize, padding) + HexGridMath.AxialToWorld(baseRoadEndLocal.x, baseRoadEndLocal.y, hexSize, padding);
        Vector2Int gateChunk = GetBestGateChunk(baseTargetWorld);
        Dictionary<Vector2Int, int> globalChunkCosts = new Dictionary<Vector2Int, int>();
        foreach (var chunk in allValidChunks) { if (HexGridMath.GetDistance(chunk, Vector2Int.zero) <= 1 || chunk == mainSpawnerChunk) globalChunkCosts[chunk] = 1; else globalChunkCosts[chunk] = (Random.Range(0, 100) < globalObstacleChance) ? 10 : 1; }
        List<Vector2Int> chunkSequence = pathfinder.FindChunkPath(mainSpawnerChunk, gateChunk, allValidChunks, globalChunkCosts);
        generatedChunkSequence = chunkSequence;
        if (chunkSequence == null || chunkSequence.Count < minChunkDistance) return false;
        Vector3 previousExitWorldPos = Vector3.zero;
        for (int i = 0; i < chunkSequence.Count; i++)
        {
            Vector2Int currentChunk = chunkSequence[i]; ChunkPathData data = new ChunkPathData();
            if (i == 0) data.entryHex = Vector2Int.zero; else data.entryHex = FindHexClosestToWorldPos(currentChunk, previousExitWorldPos);
            if (i == chunkSequence.Count - 1) data.exitHex = FindHexClosestToWorldPos(currentChunk, baseTargetWorld); else { Vector2Int nextChunk = chunkSequence[i + 1]; data.exitHex = FindRandomHexFacingChunk(currentChunk, nextChunk); }
            previousExitWorldPos = HexGridMath.GetChunkCenterWorld(currentChunk, chunkRadius, hexSize, padding) + HexGridMath.AxialToWorld(data.exitHex.x, data.exitHex.y, hexSize, padding);
            if (!GenerateAndValidateInternalPath(currentChunk, data)) return false;
            chunkPaths.Add(currentChunk, data);
        }
        return true;
    }

    void GenerateRecursiveBranches()
    {
        int totalExtraSpawners = 0; foreach (int chance in extraSpawnerChances) if (Random.Range(0, 100) < chance) totalExtraSpawners++; if (totalExtraSpawners == 0) return;
        List<Vector2Int> potentialJunctions = new List<Vector2Int>(); if (generatedChunkSequence.Count > 2) for (int i = 1; i < generatedChunkSequence.Count - 1; i++) potentialJunctions.Add(generatedChunkSequence[i]);
        HashSet<Vector2Int> usedJunctions = new HashSet<Vector2Int>(); int spawnersLeft = totalExtraSpawners;
        while (spawnersLeft > 0)
        {
            var candidates = potentialJunctions.Where(c => !usedJunctions.Contains(c)).ToList(); if (candidates.Count == 0) break;
            Vector2Int junctionChunk = candidates[Random.Range(0, candidates.Count)]; usedJunctions.Add(junctionChunk);
            int batchSize = (spawnersLeft >= 2) ? 2 : 1; while (batchSize < spawnersLeft) { if (Random.Range(0, 100) < chanceForMoreBranching) batchSize++; else break; }
            int currentEntries = chunkPaths[junctionChunk].GetAllEntries().Count; int maxPossibleAdds = 6 - currentEntries; batchSize = Mathf.Min(batchSize, maxPossibleAdds); if (batchSize <= 0) continue;
            for (int i = 0; i < batchSize; i++) { List<Vector2Int> newBranchPath = TryGenerateBranch(junctionChunk); if (newBranchPath != null && newBranchPath.Count > 0) { spawnersLeft--; for (int k = 1; k < newBranchPath.Count - 1; k++) if (!potentialJunctions.Contains(newBranchPath[k])) potentialJunctions.Add(newBranchPath[k]); } }
            ChunkPathData jData = chunkPaths[junctionChunk]; if (jData.extraEntries.Count > 0) RegenerateJunctionInternal(junctionChunk, jData);
        }
    }

    List<Vector2Int> TryGenerateBranch(Vector2Int junctionChunk)
    {
        Vector2Int newSpawn = FindValidExtraSpawnChunk(junctionChunk); if (newSpawn == Vector2Int.zero) return null;
        HashSet<Vector2Int> validForBranch = new HashSet<Vector2Int>(allValidChunks); foreach (var c in chunkPaths.Keys) if (c != junctionChunk) validForBranch.Remove(c);
        var branchPath = pathfinder.FindChunkPath(newSpawn, junctionChunk, validForBranch, null);
        if (branchPath != null && branchPath.Count >= extraSpawnDistanceRange.x && branchPath.Count <= extraSpawnDistanceRange.y) { extraSpawnerChunks.Add(newSpawn); ProcessBranchPathInternal(branchPath, junctionChunk); return branchPath; }
        return null;
    }

    void ProcessBranchPathInternal(List<Vector2Int> branchChunks, Vector2Int junctionChunk)
    {
        Vector3 previousExitWorldPos = Vector3.zero; for (int i = 0; i < branchChunks.Count - 1; i++)
        {
            Vector2Int currentChunk = branchChunks[i]; ChunkPathData data = new ChunkPathData();
            if (i == 0) data.entryHex = Vector2Int.zero; else data.entryHex = FindHexClosestToWorldPos(currentChunk, previousExitWorldPos);
            Vector2Int nextChunk = branchChunks[i + 1];
            if (nextChunk == junctionChunk) { data.exitHex = FindRandomHexFacingChunk(currentChunk, nextChunk); ChunkPathData jData = chunkPaths[junctionChunk]; Vector3 exitWorld = HexGridMath.GetChunkCenterWorld(currentChunk, chunkRadius, hexSize, padding) + HexGridMath.AxialToWorld(data.exitHex.x, data.exitHex.y, hexSize, padding); Vector2Int junctionEntry = FindHexClosestToWorldPos(junctionChunk, exitWorld); if (!jData.extraEntries.Contains(junctionEntry)) jData.extraEntries.Add(junctionEntry); } else { data.exitHex = FindRandomHexFacingChunk(currentChunk, nextChunk); }
            previousExitWorldPos = HexGridMath.GetChunkCenterWorld(currentChunk, chunkRadius, hexSize, padding) + HexGridMath.AxialToWorld(data.exitHex.x, data.exitHex.y, hexSize, padding);
            GenerateAndValidateInternalPath(currentChunk, data); if (!chunkPaths.ContainsKey(currentChunk)) chunkPaths.Add(currentChunk, data);
        }
    }

    void RegenerateJunctionInternal(Vector2Int chunkCoord, ChunkPathData data)
    {
        int style = (Random.Range(0, 2) == 0) ? 0 : 1; int seed = chunkCoord.GetHashCode() + 9999 + data.extraEntries.Count;
        var costs = GenerateStructuredCosts(seed, style, data.entryHex, data.exitHex, internalSettings.windingWallCount);
        foreach (var e in data.GetAllEntries()) costs[e] = 1; costs[data.exitHex] = 1;
        HashSet<Vector2Int> mergedPath = new HashSet<Vector2Int>(); bool criticalFail = false;
        foreach (var entry in data.GetAllEntries()) { var path = pathfinder.FindLocalPath(entry, data.exitHex, costs); if (path.Count == 0) { criticalFail = true; break; } foreach (var hex in path) mergedPath.Add(hex); }
        if (criticalFail) { costs = new Dictionary<Vector2Int, int>(); mergedPath.Clear(); foreach (var entry in data.GetAllEntries()) { var path = pathfinder.FindLocalPath(entry, data.exitHex, costs); foreach (var hex in path) mergedPath.Add(hex); } }
        data.internalPath = mergedPath.ToList(); chunkInternalCosts[chunkCoord] = costs;
    }

    bool GenerateAndValidateInternalPath(Vector2Int chunkCoord, ChunkPathData data)
    {
        bool perfectMatchFound = false; int style = RollStyle(); int baseDist = HexGridMath.GetDistance(data.entryHex, data.exitHex); int shortestPathCount = baseDist + 1; int minLenTarget = shortestPathCount; if (style == 1) minLenTarget = shortestPathCount + 3; else if (style == 2) minLenTarget = shortestPathCount + 6;
        List<Vector2Int> bestPathSoFar = new List<Vector2Int>(); Dictionary<Vector2Int, int> bestCostsSoFar = new Dictionary<Vector2Int, int>(); int bestLengthSoFar = -1;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int seed = chunkCoord.GetHashCode() + attempt + (int)System.DateTime.Now.Ticks; int currentWallCount = (style == 1) ? internalSettings.windingWallCount : internalSettings.mazeWallCount; if (style == 2) { if (attempt < 8) currentWallCount += 2; else if (attempt > 14) currentWallCount -= 2; }
            var costs = GenerateStructuredCosts(seed, style, data.entryHex, data.exitHex, currentWallCount); var path = pathfinder.FindLocalPath(data.entryHex, data.exitHex, costs);
            if (path.Count > 0) { if (style > 0 && path.Count >= minLenTarget) { data.internalPath = path; chunkInternalCosts[chunkCoord] = costs; perfectMatchFound = true; break; } if (path.Count > bestLengthSoFar) { bestLengthSoFar = path.Count; bestPathSoFar = path; bestCostsSoFar = costs; } }
        }
        if (perfectMatchFound) return true; if (bestLengthSoFar != -1) { data.internalPath = bestPathSoFar; chunkInternalCosts[chunkCoord] = bestCostsSoFar; return true; }
        var emptyCosts = new Dictionary<Vector2Int, int>(); data.internalPath = pathfinder.FindLocalPath(data.entryHex, data.exitHex, emptyCosts); chunkInternalCosts[chunkCoord] = emptyCosts; return true;
    }

    public Vector2Int GetChunkCoordFromWorldPosition(Vector3 worldPos)
    {
        Vector2Int bestChunk = Vector2Int.zero; float minDst = float.MaxValue;
        foreach (var chunk in allValidChunks) { Vector3 center = HexGridMath.GetChunkCenterWorld(chunk, chunkRadius, hexSize, padding); float d = Vector2.Distance(new Vector2(center.x, center.z), new Vector2(worldPos.x, worldPos.z)); if (d < minDst) { minDst = d; bestChunk = chunk; } }
        return bestChunk;
    }
    public List<Vector3> GetGlobalWorldPath() { List<List<Vector3>> allPaths = GetAllSpawnPaths(); if (allPaths != null && allPaths.Count > 0) return allPaths[0]; return new List<Vector3>(); }

    // API: GetAllSpawnPaths (z precyzyjnym grafem drogowym)
    public List<List<Vector3>> GetAllSpawnPaths()
    {
        List<List<Vector3>> allPaths = new List<List<Vector3>>();
        HashSet<Vector3> allRoadNodes = new HashSet<Vector3>();
        Vector3 baseTarget = HexGridMath.GetChunkCenterWorld(Vector2Int.zero, chunkRadius, hexSize, padding) + HexGridMath.AxialToWorld(baseRoadEndLocal.x, baseRoadEndLocal.y, hexSize, padding);
        allRoadNodes.Add(baseTarget);
        foreach (var kvp in chunkPaths) { Vector2Int chunkCoord = kvp.Key; var data = kvp.Value; Vector3 chunkCenter = HexGridMath.GetChunkCenterWorld(chunkCoord, chunkRadius, hexSize, padding); if (data.internalPath != null) { foreach (var localHex in data.internalPath) { Vector3 worldPos = chunkCenter + HexGridMath.AxialToWorld(localHex.x, localHex.y, hexSize, padding); allRoadNodes.Add(worldPos); } } }
        List<Vector2Int> starts = new List<Vector2Int> { mainSpawnerChunk }; if (extraSpawnerChunks != null) starts.AddRange(extraSpawnerChunks);
        foreach (var startChunk in starts)
        {
            Vector3 startPos = Vector3.zero; bool foundStart = false; if (chunkPaths.ContainsKey(startChunk)) { Vector3 center = HexGridMath.GetChunkCenterWorld(startChunk, chunkRadius, hexSize, padding); if (allRoadNodes.Contains(center)) { startPos = center; foundStart = true; } else { var data = chunkPaths[startChunk]; if (data.internalPath != null && data.internalPath.Count > 0) { var firstHex = data.internalPath[0]; startPos = center + HexGridMath.AxialToWorld(firstHex.x, firstHex.y, hexSize, padding); foundStart = true; } } }
            if (foundStart) { List<Vector3> precisePath = FindPathOnRoadGraph(startPos, baseTarget, allRoadNodes); if (precisePath.Count > 0) allPaths.Add(precisePath); }
        }
        return allPaths;
    }
    List<Vector3> FindPathOnRoadGraph(Vector3 startNode, Vector3 targetNode, HashSet<Vector3> allNodes)
    {
        if (!allNodes.Contains(startNode)) startNode = GetClosestNode(startNode, allNodes); if (!allNodes.Contains(targetNode)) targetNode = GetClosestNode(targetNode, allNodes);
        List<Vector3> openSet = new List<Vector3> { startNode }; HashSet<Vector3> closedSet = new HashSet<Vector3>(); Dictionary<Vector3, Vector3> cameFrom = new Dictionary<Vector3, Vector3>(); Dictionary<Vector3, float> gScore = new Dictionary<Vector3, float> { [startNode] = 0 }; Dictionary<Vector3, float> fScore = new Dictionary<Vector3, float> { [startNode] = Vector3.Distance(startNode, targetNode) };
        while (openSet.Count > 0)
        {
            Vector3 current = openSet[0]; float lowestF = fScore.ContainsKey(current) ? fScore[current] : float.MaxValue; for (int i = 1; i < openSet.Count; i++) { float f = fScore.ContainsKey(openSet[i]) ? fScore[openSet[i]] : float.MaxValue; if (f < lowestF) { current = openSet[i]; lowestF = f; } }
            if (Vector3.Distance(current, targetNode) < 0.1f) return ReconstructVectorPath(cameFrom, current); openSet.Remove(current); closedSet.Add(current);
            foreach (Vector3 potentialNeighbor in GetNeighborsFromSet(current, allNodes)) { if (closedSet.Contains(potentialNeighbor)) continue; float tentativeG = gScore[current] + Vector3.Distance(current, potentialNeighbor); if (!gScore.ContainsKey(potentialNeighbor) || tentativeG < gScore[potentialNeighbor]) { cameFrom[potentialNeighbor] = current; gScore[potentialNeighbor] = tentativeG; fScore[potentialNeighbor] = gScore[potentialNeighbor] + Vector3.Distance(potentialNeighbor, targetNode); if (!openSet.Contains(potentialNeighbor)) openSet.Add(potentialNeighbor); } }
        }
        return new List<Vector3>();
    }
    List<Vector3> GetNeighborsFromSet(Vector3 center, HashSet<Vector3> allNodes) { List<Vector3> neighbors = new List<Vector3>(); float threshold = hexSize * 1.9f; float thresholdSq = threshold * threshold; foreach (var node in allNodes) { if (node == center) continue; float distSq = (node - center).sqrMagnitude; if (distSq < thresholdSq) neighbors.Add(node); } return neighbors; }
    Vector3 GetClosestNode(Vector3 target, HashSet<Vector3> nodes) { Vector3 best = target; float minDst = float.MaxValue; foreach (var n in nodes) { float d = Vector3.Distance(n, target); if (d < minDst) { minDst = d; best = n; } } return best; }
    List<Vector3> ReconstructVectorPath(Dictionary<Vector3, Vector3> cameFrom, Vector3 current) { var path = new List<Vector3> { current }; while (cameFrom.ContainsKey(current)) { current = cameFrom[current]; path.Add(current); } path.Reverse(); return path; }

    // --- HELPERS ---
    void RegisterAllChunks() { allValidChunks.Clear(); for (int x = 0; x <= mapWidth; x++) for (int y = mapMinY; y <= mapMaxY; y++) allValidChunks.Add(new Vector2Int(x, y - (x / 2))); }
    void GenerateHexGridPoints(int radius, System.Action<int, int> action) { for (int q = -radius; q <= radius; q++) { int r1 = Mathf.Max(-radius, -q - radius); int r2 = Mathf.Min(radius, -q + radius); for (int r = r1; r <= r2; r++) action(q, r); } }
    Vector2Int FindHexClosestToWorldPos(Vector2Int chunkCoord, Vector3 targetWorldPos) { Vector2Int best = Vector2Int.zero; float minDst = float.MaxValue; Vector3 center = HexGridMath.GetChunkCenterWorld(chunkCoord, chunkRadius, hexSize, padding); GenerateHexGridPoints(chunkRadius, (q, r) => { float d = Vector3.Distance(center + HexGridMath.AxialToWorld(q, r, hexSize, padding), targetWorldPos); if (d < minDst) { minDst = d; best = new Vector2Int(q, r); } }); return best; }
    Vector2Int FindRandomHexFacingChunk(Vector2Int currentChunk, Vector2Int nextChunk) { Vector3 currentCenter = HexGridMath.GetChunkCenterWorld(currentChunk, chunkRadius, hexSize, padding); Vector3 nextCenter = HexGridMath.GetChunkCenterWorld(nextChunk, chunkRadius, hexSize, padding); List<Vector2Int> candidates = new List<Vector2Int>(); GenerateHexGridPoints(chunkRadius, (q, r) => { if ((Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(q + r)) / 2 == chunkRadius) candidates.Add(new Vector2Int(q, r)); }); candidates.Sort((a, b) => { Vector3 posA = currentCenter + HexGridMath.AxialToWorld(a.x, a.y, hexSize, padding); Vector3 posB = currentCenter + HexGridMath.AxialToWorld(b.x, b.y, hexSize, padding); return Vector3.Distance(posA, nextCenter).CompareTo(Vector3.Distance(posB, nextCenter)); }); int count = Mathf.Min(candidates.Count, 3); return (count > 0) ? candidates[Random.Range(0, count)] : Vector2Int.zero; }
    Vector2Int FindValidExtraSpawnChunk(Vector2Int center) { List<Vector2Int> candidates = new List<Vector2Int>(); foreach (var chunk in allValidChunks) { if (chunkPaths.ContainsKey(chunk)) continue; int dist = HexGridMath.GetDistance(center, chunk); if (dist >= extraSpawnDistanceRange.x && dist <= extraSpawnDistanceRange.y) candidates.Add(chunk); } if (candidates.Count > 0) return candidates[Random.Range(0, candidates.Count)]; return Vector2Int.zero; }
    Dictionary<Vector2Int, int> GenerateStructuredCosts(int seed, int style, Vector2Int entry, Vector2Int exit, int wallCountOverride) { Dictionary<Vector2Int, int> costs = new Dictionary<Vector2Int, int>(); System.Random rng = new System.Random(seed); GenerateHexGridPoints(chunkRadius, (q, r) => costs[new Vector2Int(q, r)] = 1); if (style == 0) return costs; int wallLength = (style == 1) ? internalSettings.windingWallLength : internalSettings.mazeWallLength; if (style == 2) wallLength += rng.Next(0, 3); List<Vector2Int> allHexes = new List<Vector2Int>(costs.Keys); for (int w = 0; w < wallCountOverride; w++) { Vector2Int current = allHexes[rng.Next(allHexes.Count)]; for (int step = 0; step < wallLength; step++) { if (current != entry && current != exit) costs[current] = internalSettings.wallCost; var neighbors = HexGridMath.GetNeighbors(current); var validNeighbors = neighbors.Where(n => costs.ContainsKey(n)).ToList(); if (validNeighbors.Count > 0) current = validNeighbors[rng.Next(validNeighbors.Count)]; else break; } } if (style == 1) { Vector2Int centerPoint = Vector2Int.RoundToInt((Vector2)(entry + exit) / 2f); costs[centerPoint] = internalSettings.wallCost; foreach (var n in HexGridMath.GetNeighbors(centerPoint)) if (costs.ContainsKey(n) && n != entry && n != exit) costs[n] = internalSettings.wallCost; } costs[entry] = 1; costs[exit] = 1; return costs; }
    int RollStyle() { int r = Random.Range(0, internalSettings.highwayWeight + internalSettings.windingWeight + internalSettings.mazeWeight); if (r < internalSettings.highwayWeight) return 0; if (r < internalSettings.highwayWeight + internalSettings.windingWeight) return 1; return 2; }
    Vector2Int GetFurthestOrRandomChunk() { List<Vector2Int> valid = new List<Vector2Int>(); int maxDistFound = 0; foreach (var c in allValidChunks) { if (c == Vector2Int.zero) continue; int d = HexGridMath.GetDistance(Vector2Int.zero, c); if (d > maxDistFound) maxDistFound = d; } int targetDist = Mathf.Min(minChunkDistance, maxDistFound); int minAcceptable = Mathf.Max(1, targetDist - 1); foreach (var c in allValidChunks) { if (c == Vector2Int.zero) continue; int d = HexGridMath.GetDistance(Vector2Int.zero, c); if (d >= minAcceptable) valid.Add(c); } if (valid.Count > 0) return valid[Random.Range(0, valid.Count)]; return new Vector2Int(mapWidth, 0); }
    Vector2Int GetBestGateChunk(Vector3 targetWorldPos) { Vector2Int best = new Vector2Int(1, 0); float minDst = float.MaxValue; foreach (var n in HexGridMath.GetNeighbors(Vector2Int.zero)) { float d = Vector3.Distance(HexGridMath.GetChunkCenterWorld(n, chunkRadius, hexSize, padding), targetWorldPos); if (d < minDst) { minDst = d; best = n; } } return best; }
    void SetFeature(Vector2Int chunk, Vector2Int local, HexFeatureType type) { if (worldData.ContainsKey(chunk) && worldData[chunk].ContainsKey(local)) worldData[chunk][local].feature = type; }
    bool IsPath(Vector2Int chunk, Vector2Int local) { if (worldData.ContainsKey(chunk) && worldData[chunk].ContainsKey(local)) return worldData[chunk][local].isPath; return false; }
    void HandleInitialFogReveal() { fogManager.RevealChunk(Vector2Int.zero); if (generatedChunkSequence != null) { for (int i = generatedChunkSequence.Count - 1; i >= 0; i--) { Vector2Int chunk = generatedChunkSequence[i]; if (HexGridMath.GetDistance(chunk, Vector2Int.zero) == 1) { fogManager.RevealChunk(chunk); break; } } } }
}

#if UNITY_EDITOR
[CustomEditor(typeof(HexMapGenerator))]
public class HexMapGeneratorEditor : Editor
{
    public override void OnInspectorGUI() { DrawDefaultInspector(); HexMapGenerator gen = (HexMapGenerator)target; GUILayout.Space(10); if (GUILayout.Button("Generuj Mapę (Debug)", GUILayout.Height(30))) gen.GenerateMap(); if (GUILayout.Button("Wyczyść Mapę", GUILayout.Height(20))) { if (gen.visualizer != null) gen.visualizer.ClearMap(); } }
}
#endif