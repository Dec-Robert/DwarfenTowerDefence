using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class HexMapGenerator : MonoBehaviour
{
    [Header("Rozmiar Mapy")]
    public int mapWidth = 5;
    public int mapMinY = -3;
    public int mapMaxY = 3;

    [Header("Ustawienia Chunku")]
    public int chunkRadius = 3;

    [Header("Punkty Strategiczne")]
    [Tooltip("Punkt na krawędzi Chunku 0, gdzie kończy się droga z sąsiedniego chunku.")]
    public Vector2Int baseRoadEndLocal = new Vector2Int(3, -2);

    [Header("Materiały")]
    public Material exitMaterial; // Materiał końca (Baza)
    public Material enterMaterial; // Materiał startu (Spawner)
    public Material roadMaterial;

    [Header("Wymiary")]
    public float hexSize = 1f;
    public float padding = 0.02f;
    public GameObject hexPrefab;

    private Transform mapHolder;
    private MaterialPropertyBlock propBlock;

    private Dictionary<Vector2Int, ChunkPathData> chunkPaths = new Dictionary<Vector2Int, ChunkPathData>();
    private Dictionary<Vector2Int, int> movementCostMap = new Dictionary<Vector2Int, int>();
    private Vector2Int spawnerChunkCoord;

    // ZMIANA: HashSet na List, aby zachować kolejność kroków
    public struct ChunkPathData
    {
        public Vector2Int entryHex;
        public Vector2Int exitHex;
        public List<Vector2Int> internalPath;
    }

    private void Start()
    {
        propBlock = new MaterialPropertyBlock();
        GenerateMap();
    }

    [ContextMenu("Generuj Mapę")]
    public void GenerateMap()
    {
        if (mapHolder != null) DestroyImmediate(mapHolder.gameObject);
        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        chunkPaths.Clear();

        mapHolder = new GameObject("World Map").transform;
        mapHolder.parent = transform;

        movementCostMap.Clear();

        CalculateSpawnerChunkLocation();
        CalculateChunkPathAndConnections();

        for (int x = 0; x <= mapWidth; x++)
        {
            for (int y = mapMinY; y <= mapMaxY; y++)
            {
                int chunkQ = x;
                int chunkR = y - (x / 2);
                int centerQ = chunkQ * (2 * chunkRadius + 1) + chunkR * chunkRadius;
                int centerR = chunkQ * -chunkRadius + chunkR * (chunkRadius + 1);
                CreateChunk(chunkQ, chunkR, centerQ, centerR);
            }
        }
    }

    void CreateChunk(int chunkID_Q, int chunkID_R, int centerGlobalQ, int centerGlobalR)
    {
        GameObject chunkObj = new GameObject($"Chunk_{chunkID_Q}_{chunkID_R}");
        chunkObj.transform.parent = mapHolder;
        chunkObj.transform.position = AxialToWorld(centerGlobalQ, centerGlobalR);

        Vector2Int chunkCoord = new Vector2Int(chunkID_Q, chunkID_R);
        bool isBaseChunk = (chunkID_Q == 0 && chunkID_R == 0);
        bool isSpawnerChunk = (chunkID_Q == spawnerChunkCoord.x && chunkID_R == spawnerChunkCoord.y);

        ChunkPathData pathData = new ChunkPathData();
        bool hasPath = chunkPaths.ContainsKey(chunkCoord);
        if (hasPath) pathData = chunkPaths[chunkCoord];

        Color chunkColor = ((chunkID_Q + chunkID_R) % 2 == 0) ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.6f, 0.6f, 0.6f);
        if (isBaseChunk) chunkColor = new Color(0.3f, 0.7f, 0.3f);
        if (isSpawnerChunk) chunkColor = new Color(0.8f, 0.3f, 0.3f);

        GenerateHexGridLogic(chunkRadius, (localQ, localR) =>
        {
            int finalQ = centerGlobalQ + localQ;
            int finalR = centerGlobalR + localR;
            Vector2Int localCoord = new Vector2Int(localQ, localR);
            Vector3 worldPos = AxialToWorld(finalQ, finalR);
            GameObject hex = Instantiate(hexPrefab, worldPos, Quaternion.identity);
            hex.name = $"Hex_{finalQ}_{finalR}";
            hex.transform.parent = chunkObj.transform;

            bool usesCustomMaterial = false;

            // --- SPECJALNA LOGIKA DLA BAZY (CHUNK 0) ---
            if (isBaseChunk)
            {
                // W bazie NIE rysujemy żadnych dróg wewnętrznych.
                // Jedynie oznaczamy punkt styku, aby wizualnie pasował do drogi z sąsiedniego chunku.
                if (localCoord == baseRoadEndLocal)
                {
                    ApplySpecialHex(hex, exitMaterial, "Road end"); // "Road end" jako punkt docelowy bazy
                    usesCustomMaterial = true;
                }
            }
            // --- LOGIKA DLA POZOSTAŁYCH CHUNKÓW ZE ŚCIEŻKĄ ---
            else if (hasPath)
            {
                bool isEntry = (localCoord == pathData.entryHex);
                bool isExit = (localCoord == pathData.exitHex);
                bool isPath = (pathData.internalPath != null && pathData.internalPath.Contains(localCoord));

                // 1. Spawner Start (Punkt startowy)
                if (isSpawnerChunk && localCoord == Vector2Int.zero)
                {
                    ApplySpecialHex(hex, enterMaterial, "Road start");
                    hex.transform.localScale *= 1.2f;
                    usesCustomMaterial = true;
                }
                // 2. Drogi, wejścia i wyjścia w chunkach
                else if (isEntry || isExit || isPath)
                {
                    Material matToUse = roadMaterial;
                    string tagToUse = "Road";

                    if (isEntry && !isSpawnerChunk) matToUse = enterMaterial;
                    if (isExit) matToUse = roadMaterial; // Wyjścia są zwykłą drogą, chyba że to koniec gry (obsłużony wyżej w BaseChunk)

                    ApplySpecialHex(hex, matToUse, tagToUse);
                    usesCustomMaterial = true;
                }
            }

            // Standardowe tagi krawędzi i kolory tła
            if (!usesCustomMaterial)
            {
                HandleTags(hex, localQ, localR, chunkRadius);
                Renderer r = hex.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    r.GetPropertyBlock(propBlock);
                    propBlock.SetColor("_Color", chunkColor);
                    r.SetPropertyBlock(propBlock);
                }
            }
        });
    }

    void CalculateChunkPathAndConnections()
    {
        // 1. Znajdź sąsiedni chunk, który jest najbliżej punktu 'baseRoadEndLocal' w Bazie.
        // To będzie nasz faktyczny cel podróży dla algorytmu A* (zamiast (0,0)).
        Vector2Int gateChunk = FindNeighborChunkTouchingLocalHex(Vector2Int.zero, baseRoadEndLocal);

        // 2. Znajdź drogę od Spawnera do tego "Bramnego Chunku"
        List<Vector2Int> path = FindPathAStar(spawnerChunkCoord, gateChunk);

        if (path == null || path.Count == 0)
        {
            Debug.LogError("Nie znaleziono ścieżki między chunkami!");
            return;
        }

        for (int i = 0; i < path.Count; i++)
        {
            Vector2Int currentChunk = path[i];
            ChunkPathData data = new ChunkPathData();
            data.internalPath = new List<Vector2Int>();

            // --- A. ENTRY HEX ---
            if (i == 0) // Spawner Chunk
            {
                data.entryHex = Vector2Int.zero; // Startujemy ze środka
            }
            else
            {
                Vector2Int prevChunk = path[i - 1];
                data.entryHex = FindHexFacingChunk(currentChunk, prevChunk);
            }

            // --- B. EXIT HEX ---
            if (i == path.Count - 1) // Ostatni Chunk (Ten stykający się z bazą)
            {
                // Tutaj kluczowa zmiana: Exit nie celuje w środek chunku (0,0).
                // Exit celuje w KONKRETNY hex (baseRoadEndLocal) wewnątrz chunku (0,0).
                Vector3 baseEndWorldPos = GetChunkCenterWorld(Vector2Int.zero) + AxialToWorld(baseRoadEndLocal.x, baseRoadEndLocal.y);
                data.exitHex = FindHexClosestToWorldPos(currentChunk, baseEndWorldPos);
            }
            else // Chunki pośrednie
            {
                Vector2Int nextChunk = path[i + 1];
                data.exitHex = FindHexFacingChunk(currentChunk, nextChunk);
            }

            // --- C. INTERNAL PATH ---
            List<Vector2Int> localPath = FindLocalPath(data.entryHex, data.exitHex);

            // Fallback: Wydłużanie drogi
            if (localPath.Count < 4 && path.Count > 1)
            {
                if (data.entryHex != Vector2Int.zero && data.exitHex != Vector2Int.zero)
                {
                    List<Vector2Int> p1 = FindLocalPath(data.entryHex, Vector2Int.zero);
                    List<Vector2Int> p2 = FindLocalPath(Vector2Int.zero, data.exitHex);
                    if (p1.Count > 0 && p2.Count > 0)
                    {
                        localPath = p1;
                        localPath.AddRange(p2.GetRange(1, p2.Count - 1));
                    }
                }
            }

            foreach (var hex in localPath) data.internalPath.Add(hex);
            chunkPaths.Add(currentChunk, data);
        }
    }

    // --- ALGORYTM A* LOKALNY ---
    List<Vector2Int> FindLocalPath(Vector2Int start, Vector2Int goal)
    {
        var frontier = new PriorityQueue<Vector2Int>();
        frontier.Enqueue(start, 0);
        var cameFrom = new Dictionary<Vector2Int, Vector2Int?>();
        var costSoFar = new Dictionary<Vector2Int, int>();
        cameFrom[start] = null; costSoFar[start] = 0;

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (current == goal) break;

            foreach (var next in GetHexNeighbors(current))
            {
                // Bounds Check
                int dist = (Mathf.Abs(next.x) + Mathf.Abs(next.y) + Mathf.Abs(next.x + next.y)) / 2;
                if (dist > chunkRadius) continue;

                // Krawędzie tylko dla start/goal
                if (dist == chunkRadius)
                {
                    if (next != start && next != goal) continue;
                }

                // Unikanie klastrów (wężyk)
                bool createsCluster = false;
                Vector2Int? trace = current;
                while (trace != null)
                {
                    if (trace.Value != current)
                    {
                        if (HexDistance(next, trace.Value) == 1) { createsCluster = true; break; }
                    }
                    trace = cameFrom[trace.Value];
                }
                if (createsCluster) continue;

                // Koszt
                if (!movementCostMap.ContainsKey(next)) movementCostMap[next] = Random.Range(1, 5);
                int newCost = costSoFar[current] + movementCostMap[next];

                if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
                {
                    costSoFar[next] = newCost;
                    int priority = newCost + HexDistance(next, goal);
                    frontier.Enqueue(next, priority);
                    cameFrom[next] = current;
                }
            }
        }

        List<Vector2Int> path = new List<Vector2Int>();
        if (!cameFrom.ContainsKey(goal)) return path;
        Vector2Int? curr = goal;
        while (curr != null) { path.Add(curr.Value); curr = cameFrom[curr.Value]; }
        path.Reverse();
        return path;
    }

    // --- HELPERY ---

    // Nowa funkcja do znajdowania sąsiada bazy, który jest najbliżej punktu wejścia
    Vector2Int FindNeighborChunkTouchingLocalHex(Vector2Int baseChunkCoord, Vector2Int localHexInBase)
    {
        Vector3 targetWorldPos = GetChunkCenterWorld(baseChunkCoord) + AxialToWorld(localHexInBase.x, localHexInBase.y);

        Vector2Int bestChunk = baseChunkCoord;
        float minDst = float.MaxValue;

        List<Vector2Int> neighbors = GetChunkNeighbors(baseChunkCoord);
        foreach (var neighbor in neighbors)
        {
            float dst = Vector3.Distance(GetChunkCenterWorld(neighbor), targetWorldPos);
            if (dst < minDst)
            {
                minDst = dst;
                bestChunk = neighbor;
            }
        }
        return bestChunk;
    }

    Vector2Int FindHexFacingChunk(Vector2Int originChunk, Vector2Int targetChunk)
    {
        Vector3 targetPos = GetChunkCenterWorld(targetChunk);
        return FindHexClosestToWorldPos(originChunk, targetPos);
    }

    Vector2Int FindHexClosestToWorldPos(Vector2Int chunkCoord, Vector3 targetWorldPos)
    {
        Vector2Int best = Vector2Int.zero; float minD = float.MaxValue;
        Vector3 center = GetChunkCenterWorld(chunkCoord);
        GenerateHexGridLogic(chunkRadius, (q, r) =>
        {
            if ((Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(q + r)) / 2 == chunkRadius)
            {
                float d = Vector3.Distance(center + AxialToWorld(q, r), targetWorldPos);
                if (d < minD) { minD = d; best = new Vector2Int(q, r); }
            }
        });
        return best;
    }

    Vector3 GetChunkCenterWorld(Vector2Int chunkCoord)
    {
        int cq = chunkCoord.x; int cr = chunkCoord.y;
        int centerQ = cq * (2 * chunkRadius + 1) + cr * chunkRadius;
        int centerR = cq * -chunkRadius + cr * (chunkRadius + 1);
        return AxialToWorld(centerQ, centerR);
    }

    List<Vector2Int> FindPathAStar(Vector2Int start, Vector2Int goal)
    {
        var frontier = new PriorityQueue<Vector2Int>(); frontier.Enqueue(start, 0);
        var cameFrom = new Dictionary<Vector2Int, Vector2Int?>(); cameFrom[start] = null;
        var costSoFar = new Dictionary<Vector2Int, int>(); costSoFar[start] = 0;

        while (frontier.Count > 0)
        {
            var curr = frontier.Dequeue();
            if (curr == goal) break;

            foreach (var next in GetChunkNeighbors(curr))
            {
                if (next.x < 0 || next.x > mapWidth) continue;
                // Ważne: Nie pozwól, aby ścieżka przeszła przez samą Bazę (0,0), chyba że to Baza jest celem (ale tu celem jest sąsiad)
                if (next == Vector2Int.zero && goal != Vector2Int.zero) continue;

                int newCost = costSoFar[curr] + 1;
                if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
                {
                    costSoFar[next] = newCost; frontier.Enqueue(next, newCost + HexDistance(next, goal)); cameFrom[next] = curr;
                }
            }
        }
        if (!cameFrom.ContainsKey(goal)) return new List<Vector2Int>();
        var path = new List<Vector2Int>(); Vector2Int? c = goal;
        while (c != null) { path.Add(c.Value); c = cameFrom[c.Value]; }
        path.Reverse(); return path;
    }

    List<Vector2Int> GetChunkNeighbors(Vector2Int hex) { return GetHexNeighbors(hex); }

    List<Vector2Int> GetHexNeighbors(Vector2Int hex)
    {
        return new List<Vector2Int> { new Vector2Int(hex.x + 1, hex.y), new Vector2Int(hex.x + 1, hex.y - 1), new Vector2Int(hex.x, hex.y - 1), new Vector2Int(hex.x - 1, hex.y), new Vector2Int(hex.x - 1, hex.y + 1), new Vector2Int(hex.x, hex.y + 1) };
    }

    int HexDistance(Vector2Int a, Vector2Int b) { return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.x + a.y - b.x - b.y) + Mathf.Abs(a.y - b.y)) / 2; }

    void ApplySpecialHex(GameObject hex, Material mat, string tagName)
    {
        hex.tag = tagName;
        Renderer r = hex.GetComponentInChildren<Renderer>();
        if (r != null && mat != null) r.sharedMaterial = mat;
    }

    void CalculateSpawnerChunkLocation()
    {
        List<Vector2Int> c = new List<Vector2Int>(); int maxD = 0;
        for (int x = 0; x <= mapWidth; x++) for (int y = mapMinY; y <= mapMaxY; y++)
            {
                if (x == 0 && y - (x / 2) == 0) continue;
                int d = (Mathf.Abs(x) + Mathf.Abs(y - (x / 2)) + Mathf.Abs(x + y - (x / 2))) / 2;
                if (d >= 3) c.Add(new Vector2Int(x, y - (x / 2)));
                if (d > maxD) maxD = d;
            }
        spawnerChunkCoord = (c.Count > 0) ? c[Random.Range(0, c.Count)] : Vector2Int.zero;
        if (spawnerChunkCoord == Vector2Int.zero && c.Count > 0) spawnerChunkCoord = c[0];
    }

    void HandleTags(GameObject hex, int q, int r, int radius)
    {
        int dist = (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(q + r)) / 2;
        if (dist != radius) return;
        int s = -q - r;
        bool isCorner = false;
        if (q == 0 && r == radius) isCorner = true;
        else if (q == radius && r == 0) isCorner = true;
        else if (q == radius && r == -radius) isCorner = true;
        else if (q == 0 && r == -radius) isCorner = true;
        else if (q == -radius && r == 0) isCorner = true;
        else if (q == -radius && r == radius) isCorner = true;
        if (isCorner) { hex.tag = "Chunk Corner"; return; }
        if (r == radius) hex.tag = "Chunk edge 0";
        else if (s == -radius) hex.tag = "Chunk edge 1";
        else if (q == radius) hex.tag = "Chunk edge 2";
        else if (r == -radius) hex.tag = "Chunk edge 3";
        else if (s == radius) hex.tag = "Chunk edge 4";
        else if (q == -radius) hex.tag = "Chunk edge 5";
    }

    void GenerateHexGridLogic(int radius, System.Action<int, int> onPointGenerated)
    {
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Mathf.Max(-radius, -q - radius);
            int r2 = Mathf.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++) onPointGenerated(q, r);
        }
    }

    Vector3 AxialToWorld(int q, int r)
    {
        float size = hexSize + padding;
        float x = size * Mathf.Sqrt(3) * (q + r / 2f);
        float z = size * 3f / 2f * r;
        return new Vector3(x, 0, z);
    }

    // Nowa metoda do wyciągania pełnej trasy w koordynatach świata (Vector3)
    public List<Vector3> GetGlobalWorldPath()
    {
        List<Vector3> fullWorldPath = new List<Vector3>();

        // 1. Odtwarzamy kolejność chunków (musimy to zrobić ponownie, lub zapisać wcześniej w zmiennej)
        // Dla uproszczenia obliczamy to tu szybko:
        Vector2Int gateChunk = FindNeighborChunkTouchingLocalHex(Vector2Int.zero, baseRoadEndLocal);
        List<Vector2Int> chunksOrder = FindPathAStar(spawnerChunkCoord, gateChunk);

        if (chunksOrder == null) return fullWorldPath;

        // 2. Iterujemy przez chunki i sklejamy ich wewnętrzne ścieżki
        foreach (var chunkCoord in chunksOrder)
        {
            if (chunkPaths.ContainsKey(chunkCoord))
            {
                var data = chunkPaths[chunkCoord];
                // Konwertujemy każdy hex lokalny na pozycję świata
                foreach (var localHex in data.internalPath)
                {
                    Vector3 worldPos = GetChunkCenterWorld(chunkCoord) + AxialToWorld(localHex.x, localHex.y);
                    fullWorldPath.Add(worldPos);
                }
            }
        }

        // 3. Dodajemy finalny punkt w bazie (baseRoadEndLocal)
        Vector3 finalBasePos = GetChunkCenterWorld(Vector2Int.zero) + AxialToWorld(baseRoadEndLocal.x, baseRoadEndLocal.y);
        fullWorldPath.Add(finalBasePos);

        return fullWorldPath;
    }
}