using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class HexMapGenerator : MonoBehaviour
{
    [Header("Rozmiar Mapy")]
    public int mapWidth = 6;
    public int mapMinY = -4;
    public int mapMaxY = 4;

    [Header("Ustawienia Chunku")]
    public int chunkRadius = 4; // Zmieniono na 4 zgodnie z Twoim projektem

    [Header("Logika Rozgrywki")]
    [Tooltip("Minimalna liczba chunków, przez które musi przejść droga.")]
    public int minChunkDistance = 6;
    [Tooltip("Ile razy generator ma próbować ułożyć układ chunków.")]
    public int maxGenerationAttempts = 50;

    [Header("Globalne Ułożenie")]
    [Tooltip("Szansa (0-100%), że cały chunk będzie traktowany jako przeszkoda.")]
    [Range(0, 100)] public int globalObstacleChance = 40;

    [System.Serializable]
    public class ChunkStyleSettings
    {
        [Header("Prawdopodobieństwo Stylu")]
        [Range(0, 100)] public int highwayWeight = 20;
        [Range(0, 100)] public int windingWeight = 40;
        [Range(0, 100)] public int mazeWeight = 40;

        [Header("Generowanie Przeszkód")]
        [Tooltip("Koszt przeszkody (Mur). Musi być wysoki (>20), żeby pathfinder go omijał.")]
        public int wallCost = 50;

        [Tooltip("Ile 'węży' (ścian) generować w stylu Winding.")]
        public int windingWallCount = 3;
        [Tooltip("Długość ścian w stylu Winding.")]
        public int windingWallLength = 3;

        [Tooltip("Ile 'węży' (ścian) generować w stylu Maze.")]
        public int mazeWallCount = 10;
        [Tooltip("Długość ścian w stylu Maze.")]
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
    public Material obstacleMaterial; // WAŻNE: Przypisz tu materiał skały/muru
    public GameObject hexPrefab;

    [Header("Wymiary")]
    public float hexSize = 1f;
    public float padding = 0.02f;

    private Transform mapHolder;
    private MaterialPropertyBlock propBlock;

    // Struktura przechowująca dane o ścieżce w chunku
    public struct ChunkPathData
    {
        public Vector2Int entryHex;
        public Vector2Int exitHex;
        public List<Vector2Int> internalPath;
    }

    private Dictionary<Vector2Int, ChunkPathData> chunkPaths = new Dictionary<Vector2Int, ChunkPathData>();
    private Dictionary<Vector2Int, Dictionary<Vector2Int, int>> chunkInternalCosts = new Dictionary<Vector2Int, Dictionary<Vector2Int, int>>();

    private Vector2Int spawnerChunkCoord;
    private HashSet<Vector2Int> allValidChunks = new HashSet<Vector2Int>();
    private List<Vector2Int> generatedChunkSequence = new List<Vector2Int>();

    private HexPathfinder pathfinder;

    private void Start()
    {
        chunkPaths = new Dictionary<Vector2Int, ChunkPathData>();
        pathfinder = new HexPathfinder(chunkRadius);
        propBlock = new MaterialPropertyBlock();
        // GenerateMap(); // Możesz odkomentować, jeśli chcesz generować na starcie gry
    }

    [ContextMenu("Generuj Mapę")]
    public void GenerateMap()
    {
        if (mapHolder != null) DestroyImmediate(mapHolder.gameObject);
        mapHolder = new GameObject("World Map").transform;
        mapHolder.parent = transform;

        // Inicjalizacja pathfindera (na wypadek zmiany radiusa w inspektorze)
        pathfinder = new HexPathfinder(chunkRadius);

        chunkPaths.Clear();
        chunkInternalCosts.Clear();
        allValidChunks.Clear();
        generatedChunkSequence.Clear();

        RegisterAllChunks();

        bool success = false;
        for (int i = 0; i < maxGenerationAttempts; i++)
        {
            if (CalculateLogic())
            {
                success = true;
                Debug.Log($"<color=green>Mapa wygenerowana w próbie: {i + 1}. Długość trasy (chunki): {generatedChunkSequence.Count}</color>");
                break;
            }
        }

        if (!success) Debug.LogWarning($"Nie udało się spełnić wymogu minChunkDistance ({minChunkDistance}).");

        DrawWorld();
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

    bool CalculateLogic()
    {
        chunkPaths.Clear();
        chunkInternalCosts.Clear();

        spawnerChunkCoord = GetFurthestOrRandomChunk();

        Vector3 baseTargetWorld = HexGridMath.GetChunkCenterWorld(Vector2Int.zero, chunkRadius, hexSize, padding)
                                + HexGridMath.AxialToWorld(baseRoadEndLocal.x, baseRoadEndLocal.y, hexSize, padding);

        Vector2Int gateChunk = GetBestGateChunk(baseTargetWorld);

        // --- GLOBALNY PATHFINDING ---
        Dictionary<Vector2Int, int> globalChunkCosts = new Dictionary<Vector2Int, int>();
        foreach (var chunk in allValidChunks)
        {
            if (HexGridMath.GetDistance(chunk, Vector2Int.zero) <= 1 || chunk == spawnerChunkCoord)
                globalChunkCosts[chunk] = 1;
            else
                globalChunkCosts[chunk] = (Random.Range(0, 100) < globalObstacleChance) ? 10 : 1;
        }

        List<Vector2Int> chunkSequence = pathfinder.FindChunkPath(spawnerChunkCoord, gateChunk, allValidChunks, globalChunkCosts);
        generatedChunkSequence = chunkSequence;

        if (chunkSequence == null || chunkSequence.Count < minChunkDistance) return false;

        // --- GENEROWANIE ŚCIEŻEK LOKALNYCH ---
        Vector3 previousExitWorldPos = Vector3.zero;

        for (int i = 0; i < chunkSequence.Count; i++)
        {
            Vector2Int currentChunk = chunkSequence[i];
            ChunkPathData data = new ChunkPathData();

            // Ustalanie wejścia/wyjścia
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

            // --- LOGIKA GENEROWANIA WNĘTRZA (POPRAWIONA) ---
            bool perfectMatchFound = false;
            int style = RollStyle(); // 0=Highway, 1=Winding, 2=Maze

            int baseDist = HexGridMath.GetDistance(data.entryHex, data.exitHex);
            int shortestPathCount = baseDist + 1;

            // Ustalanie wymagań
            int minLenTarget = shortestPathCount;

            if (style == 1) minLenTarget = shortestPathCount + 3; // Winding
            else if (style == 2) minLenTarget = shortestPathCount + 6; // Maze (wymaga dużego wydłużenia)

            // Zmienne do przechowywania "Najlepszej znalezionej do tej pory opcji"
            List<Vector2Int> bestPathSoFar = new List<Vector2Int>();
            Dictionary<Vector2Int, int> bestCostsSoFar = new Dictionary<Vector2Int, int>();
            int bestLengthSoFar = -1;

            // Pętla prób
            for (int attempt = 0; attempt < 25; attempt++)
            {
                int seed = currentChunk.GetHashCode() + i * 100 + attempt + (int)System.DateTime.Now.Ticks;

                int currentWallCount = (style == 1) ? internalSettings.windingWallCount : internalSettings.mazeWallCount;

                // Dynamiczna trudność: w pierwszych próbach próbujemy mocno, potem odpuszczamy
                if (style == 2)
                {
                    if (attempt < 10) currentWallCount += 2; // Agresywny Maze
                    else if (attempt > 15) currentWallCount -= 2; // Lżejszy Maze, żeby cokolwiek znaleźć
                }

                var costs = GenerateStructuredCosts(seed, style, data.entryHex, data.exitHex, currentWallCount);
                var path = pathfinder.FindLocalPath(data.entryHex, data.exitHex, costs);

                // Jeśli ścieżka istnieje
                if (path.Count > 0)
                {
                    // 1. Sprawdź czy to "Ideału"
                    if (style > 0 && path.Count >= minLenTarget)
                    {
                        data.internalPath = path;
                        chunkInternalCosts[currentChunk] = costs;
                        perfectMatchFound = true;
                        break; // Mamy to, wychodzimy z pętli prób
                    }

                    // 2. Jeśli nie ideał, sprawdź czy to "Najlepsze co mamy"
                    // (Szukamy najdłuższej trasy, która wciąż jest przejezdna)
                    if (path.Count > bestLengthSoFar)
                    {
                        bestLengthSoFar = path.Count;
                        bestPathSoFar = path;
                        bestCostsSoFar = costs;
                    }
                }
            }

            if (perfectMatchFound)
            {
                // Idealnie, nic nie robimy
            }
            else if (bestLengthSoFar != -1)
            {
                // Nie znaleźliśmy ideału, ale bierzemy najlepszy znaleziony "kręty" wariant
                // zamiast resetować do pustego pola!
                data.internalPath = bestPathSoFar;
                chunkInternalCosts[currentChunk] = bestCostsSoFar;
                // Debug.Log($"Chunk {currentChunk}: Użyto 'Best Attempt'. Styl: {style}, Target: {minLenTarget}, Wynik: {bestLengthSoFar}");
            }
            else
            {
                // Totalna porażka (zablokowany chunk we wszystkich 25 próbach) -> Fallback do pustego
                var emptyCosts = new Dictionary<Vector2Int, int>();
                data.internalPath = pathfinder.FindLocalPath(data.entryHex, data.exitHex, emptyCosts);
                chunkInternalCosts[currentChunk] = emptyCosts;
            }

            chunkPaths.Add(currentChunk, data);
        }

        return true;
    }

    int RollStyle()
    {
        int total = internalSettings.highwayWeight + internalSettings.windingWeight + internalSettings.mazeWeight;
        int r = Random.Range(0, total);
        if (r < internalSettings.highwayWeight) return 0;
        if (r < internalSettings.highwayWeight + internalSettings.windingWeight) return 1;
        return 2;
    }

    Dictionary<Vector2Int, int> GenerateStructuredCosts(int seed, int style, Vector2Int entry, Vector2Int exit, int wallCountOverride)
    {
        Dictionary<Vector2Int, int> costs = new Dictionary<Vector2Int, int>();
        System.Random rng = new System.Random(seed);

        GenerateHexGridPoints(chunkRadius, (q, r) => {
            costs[new Vector2Int(q, r)] = 1;
        });

        if (style == 0) return costs; // Highway

        int wallLength = (style == 1) ? internalSettings.windingWallLength : internalSettings.mazeWallLength;
        if (style == 2) wallLength += rng.Next(0, 3); // Losowa wariacja dla Maze

        List<Vector2Int> allHexes = new List<Vector2Int>(costs.Keys);

        // Generowanie ścian
        for (int w = 0; w < wallCountOverride; w++)
        {
            Vector2Int current = allHexes[rng.Next(allHexes.Count)];

            for (int step = 0; step < wallLength; step++)
            {
                // Nie blokujemy wejścia/wyjścia
                if (current != entry && current != exit)
                {
                    costs[current] = internalSettings.wallCost;
                }

                var neighbors = HexGridMath.GetNeighbors(current);
                var validNeighbors = neighbors.Where(n => costs.ContainsKey(n)).ToList();

                if (validNeighbors.Count > 0)
                    current = validNeighbors[rng.Next(validNeighbors.Count)];
                else break;
            }
        }

        // Winding Center Block - wymuszacz łuku
        if (style == 1)
        {
            Vector2Int centerPoint = Vector2Int.RoundToInt((Vector2)(entry + exit) / 2f);
            costs[centerPoint] = internalSettings.wallCost;
            foreach (var n in HexGridMath.GetNeighbors(centerPoint))
                if (costs.ContainsKey(n) && n != entry && n != exit) costs[n] = internalSettings.wallCost;
        }

        costs[entry] = 1;
        costs[exit] = 1;

        return costs;
    }

    // --- METODA PUBLICZNA DLA ENEMY SPAWNERA ---
    public List<Vector3> GetGlobalWorldPath()
    {
        List<Vector3> fullWorldPath = new List<Vector3>();

        if (generatedChunkSequence == null || generatedChunkSequence.Count == 0)
        {
            GenerateMap();
            if (generatedChunkSequence == null || generatedChunkSequence.Count == 0) return fullWorldPath;
        }

        Vector3 baseTargetWorld = HexGridMath.GetChunkCenterWorld(Vector2Int.zero, chunkRadius, hexSize, padding)
                                + HexGridMath.AxialToWorld(baseRoadEndLocal.x, baseRoadEndLocal.y, hexSize, padding);

        foreach (var chunkCoord in generatedChunkSequence)
        {
            if (chunkPaths.ContainsKey(chunkCoord))
            {
                var data = chunkPaths[chunkCoord];
                Vector3 chunkCenter = HexGridMath.GetChunkCenterWorld(chunkCoord, chunkRadius, hexSize, padding);

                if (data.internalPath != null)
                {
                    foreach (var localHex in data.internalPath)
                    {
                        Vector3 worldPos = chunkCenter + HexGridMath.AxialToWorld(localHex.x, localHex.y, hexSize, padding);
                        fullWorldPath.Add(worldPos);
                    }
                }
            }
        }

        // Dodanie punktu końcowego
        if (fullWorldPath.Count == 0 || Vector3.Distance(fullWorldPath[fullWorldPath.Count - 1], baseTargetWorld) > 0.1f)
        {
            fullWorldPath.Add(baseTargetWorld);
        }

        return fullWorldPath;
    }

    // --- Wizualizacja ---

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

        Dictionary<Vector2Int, int> localCosts = chunkInternalCosts.ContainsKey(chunkCoord) ? chunkInternalCosts[chunkCoord] : null;

        GenerateHexGridPoints(chunkRadius, (q, r) =>
        {
            Vector2Int localCoord = new Vector2Int(q, r);
            Vector3 pos = centerWorld + HexGridMath.AxialToWorld(q, r, hexSize, padding);
            GameObject hex = Instantiate(hexPrefab, pos, Quaternion.identity, chunkObj.transform);
            hex.name = $"Hex_{q}_{r}";

            ApplyHexVisuals(hex, localCoord, isBase, hasPath, pathData, (chunkCoord == spawnerChunkCoord), chunkQ, chunkR, localCosts);
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
                if (localCoord == pathData.entryHex && !isSpawner) matToUse = enterMaterial;
                ApplySpecialHex(hex, matToUse, "Road");
                usesCustomMaterial = true;
            }
            else if (isObstacle)
            {
                if (obstacleMaterial != null) ApplySpecialHex(hex, obstacleMaterial, "Obstacle");
                else hex.transform.localScale *= 1.5f; // Debug size
                usesCustomMaterial = true;
            }
        }

        if (!usesCustomMaterial)
        {
            Renderer r = hex.GetComponentInChildren<Renderer>();
            if (r != null)
            {
                r.GetPropertyBlock(propBlock);
                propBlock.SetColor("_Color", chunkColor);
                r.SetPropertyBlock(propBlock);
            }
        }
    }

    void ApplySpecialHex(GameObject hex, Material mat, string tagName) { hex.tag = tagName; Renderer r = hex.GetComponentInChildren<Renderer>(); if (r != null) r.sharedMaterial = mat; }

    void GenerateHexGridPoints(int radius, System.Action<int, int> action)
    {
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Mathf.Max(-radius, -q - radius);
            int r2 = Mathf.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++) action(q, r);
        }
    }

    Vector2Int FindHexClosestToWorldPos(Vector2Int chunkCoord, Vector3 targetWorldPos)
    {
        Vector2Int best = Vector2Int.zero; float minDst = float.MaxValue;
        Vector3 center = HexGridMath.GetChunkCenterWorld(chunkCoord, chunkRadius, hexSize, padding);
        GenerateHexGridPoints(chunkRadius, (q, r) => {
            float d = Vector3.Distance(center + HexGridMath.AxialToWorld(q, r, hexSize, padding), targetWorldPos);
            if (d < minDst) { minDst = d; best = new Vector2Int(q, r); }
        });
        return best;
    }

    Vector2Int FindRandomHexFacingChunk(Vector2Int currentChunk, Vector2Int nextChunk)
    {
        Vector3 currentCenter = HexGridMath.GetChunkCenterWorld(currentChunk, chunkRadius, hexSize, padding);
        Vector3 nextCenter = HexGridMath.GetChunkCenterWorld(nextChunk, chunkRadius, hexSize, padding);
        List<Vector2Int> candidates = new List<Vector2Int>();
        GenerateHexGridPoints(chunkRadius, (q, r) => {
            if ((Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(q + r)) / 2 == chunkRadius) candidates.Add(new Vector2Int(q, r));
        });
        candidates.Sort((a, b) => {
            Vector3 posA = currentCenter + HexGridMath.AxialToWorld(a.x, a.y, hexSize, padding);
            Vector3 posB = currentCenter + HexGridMath.AxialToWorld(b.x, b.y, hexSize, padding);
            return Vector3.Distance(posA, nextCenter).CompareTo(Vector3.Distance(posB, nextCenter));
        });
        int count = Mathf.Min(candidates.Count, 3);
        return (count > 0) ? candidates[Random.Range(0, count)] : Vector2Int.zero;
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
}

#if UNITY_EDITOR
[CustomEditor(typeof(HexMapGenerator))]
public class HexMapGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        HexMapGenerator gen = (HexMapGenerator)target;
        GUILayout.Space(10);
        if (GUILayout.Button("Generuj Mapę (Debug)", GUILayout.Height(30))) gen.GenerateMap();
        if (GUILayout.Button("Wyczyść Mapę", GUILayout.Height(20)))
        {
            while (gen.transform.childCount > 0) DestroyImmediate(gen.transform.GetChild(0).gameObject);
        }
    }
}
#endif