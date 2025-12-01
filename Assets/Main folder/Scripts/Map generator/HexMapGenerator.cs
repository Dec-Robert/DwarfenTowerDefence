using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class HexMapGenerator : MonoBehaviour
{
    [Header("Rozmiar Mapy")]
    public int mapWidth = 8;
    public int mapMinY = -6;
    public int mapMaxY = 6;

    [Header("Ustawienia Chunku")]
    public int chunkRadius = 4;

    [Header("Logika Rozgrywki - Główna")]
    public int minChunkDistance = 6;
    public int maxGenerationAttempts = 50;

    [Header("Logika Rozgrywki - Dodatkowe Spawny")]
    public bool allowExtraSpawners = true;
    [Tooltip("Szanse na kolejne spawnery: 1-szy, 2-gi, 3-ci, 4-ty")]
    public int[] extraSpawnerChances = { 80, 60, 40, 20 };
    [Tooltip("Min/Max odległość (w chunkach) od rozdroża do dodatkowego spawnu.")]
    public Vector2Int extraSpawnDistanceRange = new Vector2Int(2, 6);
    [Tooltip("Szansa (0-100), że rozdroże przyjmie kolejny spawner (powyżej minimum 2).")]
    [Range(0, 100)] public int chanceForMoreBranching = 30;

    [Header("Globalne Ułożenie")]
    [Range(0, 100)] public int globalObstacleChance = 40;

    [Header("Mgła Wojny")]
    public FogOfWarManager fogManager;

    [System.Serializable]
    public class ChunkStyleSettings
    {
        [Header("Prawdopodobieństwo Stylu")]
        [Range(0, 100)] public int highwayWeight = 20;
        [Range(0, 100)] public int windingWeight = 40;
        [Range(0, 100)] public int mazeWeight = 40;

        [Header("Generowanie Przeszkód")]
        public int wallCost = 50;
        public int windingWallCount = 3;
        public int windingWallLength = 3;
        public int mazeWallCount = 10;
        public int mazeWallLength = 4;
    }

    [Header("Ustawienia Wnętrza Chunku")]
    public ChunkStyleSettings internalSettings;

    [Header("Punkty Strategiczne")]
    public Vector2Int baseRoadEndLocal = new Vector2Int(3, -2);

    [Header("Materiały i Prefaby")]
    public Material exitMaterial;
    public Material enterMaterial;
    public Material roadMaterial;
    public Material obstacleMaterial;
    public GameObject hexPrefab;

    [Header("Wymiary")]
    public float hexSize = 1f;
    public float padding = 0.02f;

    private Transform mapHolder;
    private MaterialPropertyBlock propBlock;

    // --- STRUKTURA DANYCH ---
    public class ChunkPathData
    {
        public Vector2Int entryHex; // Główne wejście
        public List<Vector2Int> extraEntries = new List<Vector2Int>(); // Dodatkowe wejścia (dla rozdroża)
        public Vector2Int exitHex;
        public List<Vector2Int> internalPath;

        public List<Vector2Int> GetAllEntries()
        {
            var list = new List<Vector2Int> { entryHex };
            list.AddRange(extraEntries);
            return list;
        }
    }

    private Dictionary<Vector2Int, ChunkPathData> chunkPaths = new Dictionary<Vector2Int, ChunkPathData>();
    private Dictionary<Vector2Int, Dictionary<Vector2Int, int>> chunkInternalCosts = new Dictionary<Vector2Int, Dictionary<Vector2Int, int>>();

    private Vector2Int mainSpawnerChunk;
    private List<Vector2Int> extraSpawnerChunks = new List<Vector2Int>();

    private HashSet<Vector2Int> allValidChunks = new HashSet<Vector2Int>();
    private List<Vector2Int> generatedChunkSequence = new List<Vector2Int>(); // Główna trasa

    private HexPathfinder pathfinder;

    private void Start()
    {
        pathfinder = new HexPathfinder(chunkRadius);
        propBlock = new MaterialPropertyBlock();

        // Generujemy mapę przy starcie gry
        GenerateMap();
    }

    [ContextMenu("Generuj Mapę")]
    public void GenerateMap()
    {
        if (mapHolder != null) DestroyImmediate(mapHolder.gameObject);
        mapHolder = new GameObject("World Map").transform;
        mapHolder.parent = transform;

        pathfinder = new HexPathfinder(chunkRadius);
        chunkPaths.Clear();
        chunkInternalCosts.Clear();
        allValidChunks.Clear();
        generatedChunkSequence.Clear();
        extraSpawnerChunks.Clear();

        RegisterAllChunks();

        bool success = false;
        for (int i = 0; i < maxGenerationAttempts; i++)
        {
            // 1. Generuj główny kręgosłup
            if (CalculateMainPath())
            {
                // 2. Jeśli się udało, próbuj dokleić gałęzie
                if (allowExtraSpawners)
                {
                    GenerateRecursiveBranches();
                }

                success = true;
                Debug.Log($"<color=green>Mapa wygenerowana. Długość głównej: {generatedChunkSequence.Count}. Dodatkowe spawny: {extraSpawnerChunks.Count}</color>");
                break;
            }
        }

        if (!success) Debug.LogWarning($"Nie udało się wygenerować mapy spełniającej wymogi.");

        DrawWorld();

        // --- INTEGRACJA FOG OF WAR ---
        if (fogManager != null)
        {
            fogManager.InitializeFog(allValidChunks, chunkRadius, hexSize, padding);
            HandleInitialFogReveal();
        }
    }

    void HandleInitialFogReveal()
    {
        // 1. Zawsze odkrywamy bazę (0,0)
        fogManager.RevealChunk(Vector2Int.zero);

        // 2. Odkrywamy sąsiada bazy (żeby było widać kawałek drogi wyjściowej)
        if (generatedChunkSequence != null)
        {
            for (int i = generatedChunkSequence.Count - 1; i >= 0; i--)
            {
                Vector2Int chunk = generatedChunkSequence[i];
                if (HexGridMath.GetDistance(chunk, Vector2Int.zero) == 1)
                {
                    fogManager.RevealChunk(chunk);
                    break;
                }
            }
        }
    }

    void RegisterAllChunks()
    {
        allValidChunks.Clear();
        for (int x = 0; x <= mapWidth; x++)
        {
            for (int y = mapMinY; y <= mapMaxY; y++)
            {
                int chunkQ = x;
                int chunkR = y - (x / 2);
                allValidChunks.Add(new Vector2Int(chunkQ, chunkR));
            }
        }
    }

    // --- KROK 1: GŁÓWNA TRASA ---
    bool CalculateMainPath()
    {
        chunkPaths.Clear();
        chunkInternalCosts.Clear();

        mainSpawnerChunk = GetFurthestOrRandomChunk();
        Vector3 baseTargetWorld = HexGridMath.GetChunkCenterWorld(Vector2Int.zero, chunkRadius, hexSize, padding)
                                + HexGridMath.AxialToWorld(baseRoadEndLocal.x, baseRoadEndLocal.y, hexSize, padding);
        Vector2Int gateChunk = GetBestGateChunk(baseTargetWorld);

        Dictionary<Vector2Int, int> globalChunkCosts = new Dictionary<Vector2Int, int>();
        foreach (var chunk in allValidChunks)
        {
            if (HexGridMath.GetDistance(chunk, Vector2Int.zero) <= 1 || chunk == mainSpawnerChunk)
                globalChunkCosts[chunk] = 1;
            else
                globalChunkCosts[chunk] = (Random.Range(0, 100) < globalObstacleChance) ? 10 : 1;
        }

        List<Vector2Int> chunkSequence = pathfinder.FindChunkPath(mainSpawnerChunk, gateChunk, allValidChunks, globalChunkCosts);
        generatedChunkSequence = chunkSequence;

        if (chunkSequence == null || chunkSequence.Count < minChunkDistance) return false;

        Vector3 previousExitWorldPos = Vector3.zero;

        for (int i = 0; i < chunkSequence.Count; i++)
        {
            Vector2Int currentChunk = chunkSequence[i];
            ChunkPathData data = new ChunkPathData();

            if (i == 0) data.entryHex = Vector2Int.zero;
            else data.entryHex = FindHexClosestToWorldPos(currentChunk, previousExitWorldPos);

            if (i == chunkSequence.Count - 1)
                data.exitHex = FindHexClosestToWorldPos(currentChunk, baseTargetWorld);
            else
            {
                Vector2Int nextChunk = chunkSequence[i + 1];
                data.exitHex = FindRandomHexFacingChunk(currentChunk, nextChunk);
            }

            previousExitWorldPos = HexGridMath.GetChunkCenterWorld(currentChunk, chunkRadius, hexSize, padding)
                                 + HexGridMath.AxialToWorld(data.exitHex.x, data.exitHex.y, hexSize, padding);

            if (!GenerateAndValidateInternalPath(currentChunk, data)) return false;

            chunkPaths.Add(currentChunk, data);
        }

        return true;
    }

    // --- KROK 2: LOGIKA ROZGAŁĘZIEŃ ---
    void GenerateRecursiveBranches()
    {
        int totalExtraSpawners = 0;
        foreach (int chance in extraSpawnerChances)
        {
            if (Random.Range(0, 100) < chance) totalExtraSpawners++;
        }

        if (totalExtraSpawners == 0) return;

        List<Vector2Int> potentialJunctions = new List<Vector2Int>();
        if (generatedChunkSequence.Count > 2)
        {
            for (int i = 1; i < generatedChunkSequence.Count - 1; i++)
                potentialJunctions.Add(generatedChunkSequence[i]);
        }

        HashSet<Vector2Int> usedJunctions = new HashSet<Vector2Int>();
        int spawnersLeft = totalExtraSpawners;

        while (spawnersLeft > 0)
        {
            var candidates = potentialJunctions.Where(c => !usedJunctions.Contains(c)).ToList();
            if (candidates.Count == 0) break;

            Vector2Int junctionChunk = candidates[Random.Range(0, candidates.Count)];
            usedJunctions.Add(junctionChunk);

            int batchSize = (spawnersLeft >= 2) ? 2 : 1;
            while (batchSize < spawnersLeft)
            {
                if (Random.Range(0, 100) < chanceForMoreBranching) batchSize++;
                else break;
            }

            int currentEntries = chunkPaths[junctionChunk].GetAllEntries().Count;
            int maxPossibleAdds = 6 - currentEntries;
            batchSize = Mathf.Min(batchSize, maxPossibleAdds);

            if (batchSize <= 0) continue;

            for (int i = 0; i < batchSize; i++)
            {
                List<Vector2Int> newBranchPath = TryGenerateBranch(junctionChunk);
                if (newBranchPath != null && newBranchPath.Count > 0)
                {
                    spawnersLeft--;
                    for (int k = 1; k < newBranchPath.Count - 1; k++)
                    {
                        if (!potentialJunctions.Contains(newBranchPath[k]))
                            potentialJunctions.Add(newBranchPath[k]);
                    }
                }
            }

            ChunkPathData jData = chunkPaths[junctionChunk];
            if (jData.extraEntries.Count > 0)
            {
                RegenerateJunctionInternal(junctionChunk, jData);
            }
        }
    }

    List<Vector2Int> TryGenerateBranch(Vector2Int junctionChunk)
    {
        Vector2Int newSpawn = FindValidExtraSpawnChunk(junctionChunk);
        if (newSpawn == Vector2Int.zero) return null;

        HashSet<Vector2Int> validForBranch = new HashSet<Vector2Int>(allValidChunks);
        foreach (var c in chunkPaths.Keys) if (c != junctionChunk) validForBranch.Remove(c);

        var branchPath = pathfinder.FindChunkPath(newSpawn, junctionChunk, validForBranch, null);

        if (branchPath != null && branchPath.Count >= extraSpawnDistanceRange.x && branchPath.Count <= extraSpawnDistanceRange.y)
        {
            extraSpawnerChunks.Add(newSpawn);
            ProcessBranchPathInternal(branchPath, junctionChunk);
            return branchPath;
        }
        return null;
    }

    Vector2Int FindValidExtraSpawnChunk(Vector2Int center)
    {
        List<Vector2Int> candidates = new List<Vector2Int>();
        foreach (var chunk in allValidChunks)
        {
            if (chunkPaths.ContainsKey(chunk)) continue;
            int dist = HexGridMath.GetDistance(center, chunk);
            if (dist >= extraSpawnDistanceRange.x && dist <= extraSpawnDistanceRange.y)
            {
                candidates.Add(chunk);
            }
        }
        if (candidates.Count > 0) return candidates[Random.Range(0, candidates.Count)];
        return Vector2Int.zero;
    }

    void ProcessBranchPathInternal(List<Vector2Int> branchChunks, Vector2Int junctionChunk)
    {
        Vector3 previousExitWorldPos = Vector3.zero;

        for (int i = 0; i < branchChunks.Count - 1; i++)
        {
            Vector2Int currentChunk = branchChunks[i];
            ChunkPathData data = new ChunkPathData();

            if (i == 0) data.entryHex = Vector2Int.zero;
            else data.entryHex = FindHexClosestToWorldPos(currentChunk, previousExitWorldPos);

            Vector2Int nextChunk = branchChunks[i + 1];

            if (nextChunk == junctionChunk)
            {
                data.exitHex = FindRandomHexFacingChunk(currentChunk, nextChunk);

                ChunkPathData jData = chunkPaths[junctionChunk];
                Vector3 exitWorld = HexGridMath.GetChunkCenterWorld(currentChunk, chunkRadius, hexSize, padding)
                                  + HexGridMath.AxialToWorld(data.exitHex.x, data.exitHex.y, hexSize, padding);

                Vector2Int junctionEntry = FindHexClosestToWorldPos(junctionChunk, exitWorld);
                if (!jData.extraEntries.Contains(junctionEntry))
                    jData.extraEntries.Add(junctionEntry);
            }
            else
            {
                data.exitHex = FindRandomHexFacingChunk(currentChunk, nextChunk);
            }

            previousExitWorldPos = HexGridMath.GetChunkCenterWorld(currentChunk, chunkRadius, hexSize, padding)
                                 + HexGridMath.AxialToWorld(data.exitHex.x, data.exitHex.y, hexSize, padding);

            GenerateAndValidateInternalPath(currentChunk, data);

            if (!chunkPaths.ContainsKey(currentChunk))
                chunkPaths.Add(currentChunk, data);
        }
    }

    void RegenerateJunctionInternal(Vector2Int chunkCoord, ChunkPathData data)
    {
        int style = (Random.Range(0, 2) == 0) ? 0 : 1;
        int seed = chunkCoord.GetHashCode() + 9999 + data.extraEntries.Count;

        var costs = GenerateStructuredCosts(seed, style, data.entryHex, data.exitHex, internalSettings.windingWallCount);

        foreach (var e in data.GetAllEntries()) costs[e] = 1;
        costs[data.exitHex] = 1;

        HashSet<Vector2Int> mergedPath = new HashSet<Vector2Int>();
        bool criticalFail = false;

        foreach (var entry in data.GetAllEntries())
        {
            var path = pathfinder.FindLocalPath(entry, data.exitHex, costs);
            if (path.Count == 0)
            {
                criticalFail = true;
                break;
            }
            foreach (var hex in path) mergedPath.Add(hex);
        }

        if (criticalFail)
        {
            costs = new Dictionary<Vector2Int, int>();
            mergedPath.Clear();
            foreach (var entry in data.GetAllEntries())
            {
                var path = pathfinder.FindLocalPath(entry, data.exitHex, costs);
                foreach (var hex in path) mergedPath.Add(hex);
            }
        }

        data.internalPath = mergedPath.ToList();
        chunkInternalCosts[chunkCoord] = costs;
    }

    bool GenerateAndValidateInternalPath(Vector2Int chunkCoord, ChunkPathData data)
    {
        bool perfectMatchFound = false;
        int style = RollStyle();

        int baseDist = HexGridMath.GetDistance(data.entryHex, data.exitHex);
        int shortestPathCount = baseDist + 1;
        int minLenTarget = shortestPathCount;
        if (style == 1) minLenTarget = shortestPathCount + 3;
        else if (style == 2) minLenTarget = shortestPathCount + 6;

        List<Vector2Int> bestPathSoFar = new List<Vector2Int>();
        Dictionary<Vector2Int, int> bestCostsSoFar = new Dictionary<Vector2Int, int>();
        int bestLengthSoFar = -1;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            int seed = chunkCoord.GetHashCode() + attempt + (int)System.DateTime.Now.Ticks;
            int currentWallCount = (style == 1) ? internalSettings.windingWallCount : internalSettings.mazeWallCount;
            if (style == 2)
            {
                if (attempt < 8) currentWallCount += 2;
                else if (attempt > 14) currentWallCount -= 2;
            }

            var costs = GenerateStructuredCosts(seed, style, data.entryHex, data.exitHex, currentWallCount);
            var path = pathfinder.FindLocalPath(data.entryHex, data.exitHex, costs);

            if (path.Count > 0)
            {
                if (style > 0 && path.Count >= minLenTarget)
                {
                    data.internalPath = path;
                    chunkInternalCosts[chunkCoord] = costs;
                    perfectMatchFound = true;
                    break;
                }
                if (path.Count > bestLengthSoFar)
                {
                    bestLengthSoFar = path.Count;
                    bestPathSoFar = path;
                    bestCostsSoFar = costs;
                }
            }
        }

        if (perfectMatchFound) return true;
        if (bestLengthSoFar != -1)
        {
            data.internalPath = bestPathSoFar;
            chunkInternalCosts[chunkCoord] = bestCostsSoFar;
            return true;
        }

        var emptyCosts = new Dictionary<Vector2Int, int>();
        data.internalPath = pathfinder.FindLocalPath(data.entryHex, data.exitHex, emptyCosts);
        chunkInternalCosts[chunkCoord] = emptyCosts;
        return true;
    }

    // --- METODY PUBLICZNE DLA SPAWNERA ---

    // 1. Przeliczanie pozycji świata na chunk
    public Vector2Int GetChunkCoordFromWorldPosition(Vector3 worldPos)
    {
        Vector2Int bestChunk = Vector2Int.zero;
        float minDst = float.MaxValue;

        foreach (var chunk in allValidChunks)
        {
            Vector3 center = HexGridMath.GetChunkCenterWorld(chunk, chunkRadius, hexSize, padding);
            float d = Vector2.Distance(new Vector2(center.x, center.z), new Vector2(worldPos.x, worldPos.z));
            if (d < minDst)
            {
                minDst = d;
                bestChunk = chunk;
            }
        }
        return bestChunk;
    }

    // 2. Metoda kompatybilności (zwraca pierwszą trasę)
    public List<Vector3> GetGlobalWorldPath()
    {
        List<List<Vector3>> allPaths = GetAllSpawnPaths();
        if (allPaths != null && allPaths.Count > 0) return allPaths[0];
        return new List<Vector3>();
    }

    // 3. NOWA, PRECYZYJNA METODA GENEROWANIA TRAS (GLOBALNY GRAF)
    public List<List<Vector3>> GetAllSpawnPaths()
    {
        List<List<Vector3>> allPaths = new List<List<Vector3>>();

        // A. ZBIERZ WSZYSTKIE PUNKTY DROGOWE Z CAŁEJ MAPY
        HashSet<Vector3> allRoadNodes = new HashSet<Vector3>();
        Vector3 baseTarget = HexGridMath.GetChunkCenterWorld(Vector2Int.zero, chunkRadius, hexSize, padding)
                           + HexGridMath.AxialToWorld(baseRoadEndLocal.x, baseRoadEndLocal.y, hexSize, padding);
        allRoadNodes.Add(baseTarget);

        foreach (var kvp in chunkPaths)
        {
            Vector2Int chunkCoord = kvp.Key;
            var data = kvp.Value;
            Vector3 chunkCenter = HexGridMath.GetChunkCenterWorld(chunkCoord, chunkRadius, hexSize, padding);

            if (data.internalPath != null)
            {
                foreach (var localHex in data.internalPath)
                {
                    Vector3 worldPos = chunkCenter + HexGridMath.AxialToWorld(localHex.x, localHex.y, hexSize, padding);
                    allRoadNodes.Add(worldPos);
                }
            }
        }

        // B. PRZYGOTUJ PUNKTY STARTOWE
        List<Vector2Int> starts = new List<Vector2Int> { mainSpawnerChunk };
        if (extraSpawnerChunks != null) starts.AddRange(extraSpawnerChunks);

        // C. DLA KAŻDEGO SPAWNERA ZNAJDŹ DROGĘ DO BAZY PO GRAFIE
        foreach (var startChunk in starts)
        {
            Vector3 startPos = Vector3.zero;
            bool foundStart = false;

            if (chunkPaths.ContainsKey(startChunk))
            {
                Vector3 center = HexGridMath.GetChunkCenterWorld(startChunk, chunkRadius, hexSize, padding);
                if (allRoadNodes.Contains(center))
                {
                    startPos = center;
                    foundStart = true;
                }
                else
                {
                    var data = chunkPaths[startChunk];
                    if (data.internalPath != null && data.internalPath.Count > 0)
                    {
                        var firstHex = data.internalPath[0];
                        startPos = center + HexGridMath.AxialToWorld(firstHex.x, firstHex.y, hexSize, padding);
                        foundStart = true;
                    }
                }
            }

            if (foundStart)
            {
                List<Vector3> precisePath = FindPathOnRoadGraph(startPos, baseTarget, allRoadNodes);
                if (precisePath.Count > 0)
                {
                    allPaths.Add(precisePath);
                }
            }
        }

        return allPaths;
    }

    // --- GLOBALNY A* DLA PUNKTÓW W ŚWIECIE ---
    List<Vector3> FindPathOnRoadGraph(Vector3 startNode, Vector3 targetNode, HashSet<Vector3> allNodes)
    {
        if (!allNodes.Contains(startNode)) startNode = GetClosestNode(startNode, allNodes);
        if (!allNodes.Contains(targetNode)) targetNode = GetClosestNode(targetNode, allNodes);

        List<Vector3> openSet = new List<Vector3> { startNode };
        HashSet<Vector3> closedSet = new HashSet<Vector3>();
        Dictionary<Vector3, Vector3> cameFrom = new Dictionary<Vector3, Vector3>();
        Dictionary<Vector3, float> gScore = new Dictionary<Vector3, float> { [startNode] = 0 };
        Dictionary<Vector3, float> fScore = new Dictionary<Vector3, float> { [startNode] = Vector3.Distance(startNode, targetNode) };

        while (openSet.Count > 0)
        {
            Vector3 current = openSet[0];
            float lowestF = fScore.ContainsKey(current) ? fScore[current] : float.MaxValue;

            for (int i = 1; i < openSet.Count; i++)
            {
                float f = fScore.ContainsKey(openSet[i]) ? fScore[openSet[i]] : float.MaxValue;
                if (f < lowestF)
                {
                    current = openSet[i];
                    lowestF = f;
                }
            }

            if (Vector3.Distance(current, targetNode) < 0.1f) return ReconstructVectorPath(cameFrom, current);

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (Vector3 potentialNeighbor in GetNeighborsFromSet(current, allNodes))
            {
                if (closedSet.Contains(potentialNeighbor)) continue;
                float tentativeG = gScore[current] + Vector3.Distance(current, potentialNeighbor);

                if (!gScore.ContainsKey(potentialNeighbor) || tentativeG < gScore[potentialNeighbor])
                {
                    cameFrom[potentialNeighbor] = current;
                    gScore[potentialNeighbor] = tentativeG;
                    fScore[potentialNeighbor] = gScore[potentialNeighbor] + Vector3.Distance(potentialNeighbor, targetNode);
                    if (!openSet.Contains(potentialNeighbor)) openSet.Add(potentialNeighbor);
                }
            }
        }
        return new List<Vector3>();
    }

    List<Vector3> GetNeighborsFromSet(Vector3 center, HashSet<Vector3> allNodes)
    {
        List<Vector3> neighbors = new List<Vector3>();
        float threshold = hexSize * 1.9f;
        float thresholdSq = threshold * threshold;

        foreach (var node in allNodes)
        {
            if (node == center) continue;
            float distSq = (node - center).sqrMagnitude;
            if (distSq < thresholdSq) neighbors.Add(node);
        }
        return neighbors;
    }

    Vector3 GetClosestNode(Vector3 target, HashSet<Vector3> nodes)
    {
        Vector3 best = target;
        float minDst = float.MaxValue;
        foreach (var n in nodes)
        {
            float d = Vector3.Distance(n, target);
            if (d < minDst) { minDst = d; best = n; }
        }
        return best;
    }

    List<Vector3> ReconstructVectorPath(Dictionary<Vector3, Vector3> cameFrom, Vector3 current)
    {
        var path = new List<Vector3> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    // --- HELPERS (Style, Draw, Math) ---
    Dictionary<Vector2Int, int> GenerateStructuredCosts(int seed, int style, Vector2Int entry, Vector2Int exit, int wallCountOverride)
    {
        Dictionary<Vector2Int, int> costs = new Dictionary<Vector2Int, int>();
        System.Random rng = new System.Random(seed);
        GenerateHexGridPoints(chunkRadius, (q, r) => costs[new Vector2Int(q, r)] = 1);
        if (style == 0) return costs;
        int wallLength = (style == 1) ? internalSettings.windingWallLength : internalSettings.mazeWallLength;
        if (style == 2) wallLength += rng.Next(0, 3);
        List<Vector2Int> allHexes = new List<Vector2Int>(costs.Keys);
        for (int w = 0; w < wallCountOverride; w++)
        {
            Vector2Int current = allHexes[rng.Next(allHexes.Count)];
            for (int step = 0; step < wallLength; step++)
            {
                if (current != entry && current != exit) costs[current] = internalSettings.wallCost;
                var neighbors = HexGridMath.GetNeighbors(current);
                var validNeighbors = neighbors.Where(n => costs.ContainsKey(n)).ToList();
                if (validNeighbors.Count > 0) current = validNeighbors[rng.Next(validNeighbors.Count)]; else break;
            }
        }
        if (style == 1)
        {
            Vector2Int centerPoint = Vector2Int.RoundToInt((Vector2)(entry + exit) / 2f);
            costs[centerPoint] = internalSettings.wallCost;
            foreach (var n in HexGridMath.GetNeighbors(centerPoint)) if (costs.ContainsKey(n) && n != entry && n != exit) costs[n] = internalSettings.wallCost;
        }
        costs[entry] = 1; costs[exit] = 1;
        return costs;
    }

    int RollStyle()
    {
        int r = Random.Range(0, internalSettings.highwayWeight + internalSettings.windingWeight + internalSettings.mazeWeight);
        if (r < internalSettings.highwayWeight) return 0;
        if (r < internalSettings.highwayWeight + internalSettings.windingWeight) return 1;
        return 2;
    }

    Vector2Int GetFurthestOrRandomChunk()
    {
        List<Vector2Int> valid = new List<Vector2Int>();
        int maxDistFound = 0;
        foreach (var c in allValidChunks)
        {
            if (c == Vector2Int.zero) continue;
            int d = HexGridMath.GetDistance(Vector2Int.zero, c);
            if (d > maxDistFound) maxDistFound = d;
        }
        int targetDist = Mathf.Min(minChunkDistance, maxDistFound);
        int minAcceptable = Mathf.Max(1, targetDist - 1);
        foreach (var c in allValidChunks)
        {
            if (c == Vector2Int.zero) continue;
            int d = HexGridMath.GetDistance(Vector2Int.zero, c);
            if (d >= minAcceptable) valid.Add(c);
        }
        if (valid.Count > 0) return valid[Random.Range(0, valid.Count)];
        return new Vector2Int(mapWidth, 0);
    }

    Vector2Int GetBestGateChunk(Vector3 targetWorldPos)
    {
        Vector2Int best = new Vector2Int(1, 0); float minDst = float.MaxValue;
        foreach (var n in HexGridMath.GetNeighbors(Vector2Int.zero))
        {
            float d = Vector3.Distance(HexGridMath.GetChunkCenterWorld(n, chunkRadius, hexSize, padding), targetWorldPos);
            if (d < minDst) { minDst = d; best = n; }
        }
        return best;
    }

    void ApplySpecialHex(GameObject hex, Material mat, string tagName) { hex.tag = tagName; Renderer r = hex.GetComponentInChildren<Renderer>(); if (r != null) r.sharedMaterial = mat; }
    void GenerateHexGridPoints(int radius, System.Action<int, int> action) { for (int q = -radius; q <= radius; q++) { int r1 = Mathf.Max(-radius, -q - radius); int r2 = Mathf.Min(radius, -q + radius); for (int r = r1; r <= r2; r++) action(q, r); } }
    Vector2Int FindHexClosestToWorldPos(Vector2Int chunkCoord, Vector3 targetWorldPos) { Vector2Int best = Vector2Int.zero; float minDst = float.MaxValue; Vector3 center = HexGridMath.GetChunkCenterWorld(chunkCoord, chunkRadius, hexSize, padding); GenerateHexGridPoints(chunkRadius, (q, r) => { float d = Vector3.Distance(center + HexGridMath.AxialToWorld(q, r, hexSize, padding), targetWorldPos); if (d < minDst) { minDst = d; best = new Vector2Int(q, r); } }); return best; }
    Vector2Int FindRandomHexFacingChunk(Vector2Int currentChunk, Vector2Int nextChunk) { Vector3 currentCenter = HexGridMath.GetChunkCenterWorld(currentChunk, chunkRadius, hexSize, padding); Vector3 nextCenter = HexGridMath.GetChunkCenterWorld(nextChunk, chunkRadius, hexSize, padding); List<Vector2Int> candidates = new List<Vector2Int>(); GenerateHexGridPoints(chunkRadius, (q, r) => { if ((Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(q + r)) / 2 == chunkRadius) candidates.Add(new Vector2Int(q, r)); }); candidates.Sort((a, b) => { Vector3 posA = currentCenter + HexGridMath.AxialToWorld(a.x, a.y, hexSize, padding); Vector3 posB = currentCenter + HexGridMath.AxialToWorld(b.x, b.y, hexSize, padding); return Vector3.Distance(posA, nextCenter).CompareTo(Vector3.Distance(posB, nextCenter)); }); int count = Mathf.Min(candidates.Count, 3); return (count > 0) ? candidates[Random.Range(0, count)] : Vector2Int.zero; }

    void DrawWorld()
    {
        foreach (var chunkCoord in allValidChunks) CreateChunkVisuals(chunkCoord.x, chunkCoord.y);
    }

    void CreateChunkVisuals(int chunkQ, int chunkR)
    {
        Vector2Int chunkCoord = new Vector2Int(chunkQ, chunkR);
        Vector3 centerWorld = HexGridMath.GetChunkCenterWorld(chunkCoord, chunkRadius, hexSize, padding);
        GameObject chunkObj = new GameObject($"Chunk_{chunkQ}_{chunkR}");
        chunkObj.transform.parent = mapHolder;
        chunkObj.transform.position = centerWorld;

        bool isBase = (chunkCoord == Vector2Int.zero);
        bool hasPath = chunkPaths.ContainsKey(chunkCoord);
        ChunkPathData pathData = hasPath ? chunkPaths[chunkCoord] : new ChunkPathData();
        bool isSpawner = (chunkCoord == mainSpawnerChunk || extraSpawnerChunks.Contains(chunkCoord));
        Dictionary<Vector2Int, int> localCosts = chunkInternalCosts.ContainsKey(chunkCoord) ? chunkInternalCosts[chunkCoord] : null;

        GenerateHexGridPoints(chunkRadius, (q, r) =>
        {
            Vector2Int localCoord = new Vector2Int(q, r);
            Vector3 pos = centerWorld + HexGridMath.AxialToWorld(q, r, hexSize, padding);
            GameObject hex = Instantiate(hexPrefab, pos, Quaternion.identity, chunkObj.transform);
            ApplyHexVisuals(hex, localCoord, isBase, hasPath, pathData, isSpawner, chunkQ, chunkR, localCosts);
        });
    }

    void ApplyHexVisuals(GameObject hex, Vector2Int localCoord, bool isBase, bool hasPath, ChunkPathData pathData, bool isSpawner, int chunkQ, int chunkR, Dictionary<Vector2Int, int> costs)
    {
        bool usesCustomMaterial = false;
        Color chunkColor = ((chunkQ + chunkR) % 2 == 0) ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.6f, 0.6f, 0.6f);
        if (isBase) chunkColor = new Color(0.3f, 0.7f, 0.3f);
        if (isSpawner) chunkColor = new Color(0.8f, 0.3f, 0.3f);

        bool isObstacle = (costs != null && costs.ContainsKey(localCoord) && costs[localCoord] >= internalSettings.wallCost);

        if (isBase)
        {
            if (localCoord == baseRoadEndLocal) { ApplySpecialHex(hex, exitMaterial, "Road end"); usesCustomMaterial = true; }
        }
        else if (hasPath)
        {
            bool isAnyEntry = (localCoord == pathData.entryHex) || pathData.extraEntries.Contains(localCoord);
            bool isPath = (pathData.internalPath != null && pathData.internalPath.Contains(localCoord));

            if (isSpawner && localCoord == Vector2Int.zero)
            {
                ApplySpecialHex(hex, enterMaterial, "Road start");
                hex.transform.localScale *= 1.2f;
                usesCustomMaterial = true;
            }
            else if (isPath)
            {
                Material matToUse = roadMaterial;
                if (isAnyEntry && !isSpawner) matToUse = enterMaterial;
                ApplySpecialHex(hex, matToUse, "Road");
                usesCustomMaterial = true;
            }
            else if (isObstacle)
            {
                if (obstacleMaterial != null) ApplySpecialHex(hex, obstacleMaterial, "Obstacle");
                // else hex.transform.localScale *= 1.5f; // Wyłączony debug size
                usesCustomMaterial = true;
            }
        }
        if (!usesCustomMaterial)
        {
            Renderer r = hex.GetComponentInChildren<Renderer>();
            if (r != null) { r.GetPropertyBlock(propBlock); propBlock.SetColor("_Color", chunkColor); r.SetPropertyBlock(propBlock); }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(HexMapGenerator))]
public class HexMapGeneratorEditor : Editor
{
    public override void OnInspectorGUI() { DrawDefaultInspector(); HexMapGenerator gen = (HexMapGenerator)target; GUILayout.Space(10); if (GUILayout.Button("Generuj Mapę (Debug)", GUILayout.Height(30))) gen.GenerateMap(); if (GUILayout.Button("Wyczyść Mapę", GUILayout.Height(20))) { while (gen.transform.childCount > 0) DestroyImmediate(gen.transform.GetChild(0).gameObject); } }
}
#endif