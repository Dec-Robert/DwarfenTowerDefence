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
    [Tooltip("Punkt docelowy w Bazie (Koniec gry).")]
    public Vector2Int baseRoadEndLocal = new Vector2Int(3, -2);

    [Header("Materiały")]
    public Material exitMaterial;   // Wyjście z chunku (Brama wyjściowa)
    public Material enterMaterial;  // Wejście do chunku (Brama wejściowa / Start)
    public Material roadMaterial;   // Zwykła droga

    [Header("Wymiary")]
    public float hexSize = 1f;
    public float padding = 0.02f;
    public GameObject hexPrefab;

    private Transform mapHolder;
    private MaterialPropertyBlock propBlock;

    // Klucz: ChunkCoord, Wartość: Dane o ścieżce
    private Dictionary<Vector2Int, ChunkPathData> chunkPaths = new Dictionary<Vector2Int, ChunkPathData>();

    // Struktura danych
    struct ChunkPathData
    {
        public Vector2Int entryHex; // Punkt startowy wewnątrz chunku
        public Vector2Int exitHex;  // Punkt końcowy wewnątrz chunku
        public HashSet<Vector2Int> internalPath; // Zbiór heksów drogi
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

        // 1. Oblicz logiczną ścieżkę
        CalculateSpawnerChunkLocation();
        CalculateChunkPathAndConnections();

        // 2. Generuj heksy
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

        // Tło chunku
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

            if (hasPath)
            {
                // --- CASE 1: SPAWNER START (Tylko środek spawnera) ---
                if (isSpawnerChunk && localCoord == Vector2Int.zero)
                {
                    // To jest jedyny Road Start
                    ApplySpecialHex(hex, enterMaterial, "Road start");
                    hex.transform.localScale *= 1.2f;
                    usesCustomMaterial = true;
                }
                // --- CASE 2: BASE END (Tylko punkt docelowy bazy) ---
                else if (isBaseChunk && localCoord == baseRoadEndLocal)
                {
                    // To jest jedyny Road End
                    ApplySpecialHex(hex, exitMaterial, "Road end");
                    usesCustomMaterial = true;
                }
                // --- CASE 3: DROGA WEWNĘTRZNA I BRAMY ---
                else
                {
                    // Sprawdzamy czy heks jest częścią ścieżki
                    bool isEntry = (localCoord == pathData.entryHex);
                    bool isExit = (localCoord == pathData.exitHex);
                    bool isPath = (pathData.internalPath != null && pathData.internalPath.Contains(localCoord));

                    if (isEntry || isExit || isPath)
                    {
                        // Domyślnie materiał drogi
                        Material matToUse = roadMaterial;
                        string tagToUse = "Road";

                        // Jeśli to brama wejściowa/wyjściowa na krawędzi (poza Startem/Koniecem gry)
                        // Baza nie powinna mieć "Enter Material" jeśli nie chcemy, ale zaznaczmy bramy:
                        if (isEntry && !isSpawnerChunk) matToUse = enterMaterial;
                        if (isExit && !isBaseChunk) matToUse = exitMaterial;

                        ApplySpecialHex(hex, matToUse, tagToUse);
                        usesCustomMaterial = true;
                    }
                }
            }

            // --- STANDARDOWE TAGOWANIE KRAWĘDZI ---
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
        List<Vector2Int> path = FindPathAStar(spawnerChunkCoord, Vector2Int.zero);

        if (path == null || path.Count == 0) return;

        for (int i = 0; i < path.Count; i++)
        {
            Vector2Int currentChunk = path[i];
            ChunkPathData data = new ChunkPathData();
            data.internalPath = new HashSet<Vector2Int>();

            // --- KROK 1: Ustalenie punktów Entry i Exit ---

            if (i == 0) // SPAWNER CHUNK
            {
                Vector2Int nextChunk = path[i + 1];

                // ZMIANA: Startem jest ŚRODEK (0,0), a nie krawędź
                data.entryHex = Vector2Int.zero;

                // Wyjście w stronę następnego chunku
                data.exitHex = FindHexFacingChunk(currentChunk, nextChunk);
            }
            else if (i == path.Count - 1) // BASE CHUNK
            {
                Vector2Int prevChunk = path[i - 1];

                // Wejście od poprzedniego chunku
                data.entryHex = FindHexFacingChunk(currentChunk, prevChunk);

                // Wyjście to punkt docelowy w bazie
                data.exitHex = baseRoadEndLocal;
            }
            else // CHUNKI POŚREDNIE
            {
                Vector2Int prevChunk = path[i - 1];
                Vector2Int nextChunk = path[i + 1];

                data.entryHex = FindHexFacingChunk(currentChunk, prevChunk);
                data.exitHex = FindHexFacingChunk(currentChunk, nextChunk);
            }

            // --- KROK 2: Wyznaczenie drogi wewnątrz chunku (A*) ---
            // Łączymy entryHex z exitHex
            List<Vector2Int> localPath = FindLocalPath(data.entryHex, data.exitHex);
            foreach (var hex in localPath)
            {
                data.internalPath.Add(hex);
            }

            chunkPaths.Add(currentChunk, data);
        }
    }

    // --- Reszta funkcji pomocniczych bez zmian ---
    // (Dla kompletności kodu - kopiuj z poprzednich odpowiedzi jeśli potrzeba, 
    //  ale kluczowe zmiany są powyżej)

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
                int dist = (Mathf.Abs(next.x) + Mathf.Abs(next.y) + Mathf.Abs(next.x + next.y)) / 2;
                if (dist > chunkRadius) continue; // Granica chunku

                int newCost = costSoFar[current] + 1;
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
        return path;
    }

    // --- Standardowe Helpery ---

    // (Zakładam, że te funkcje masz w kodzie z poprzedniej odpowiedzi,
    //  są niezbędne do działania: FindPathAStar, FindHexFacingChunk, FindHexClosestToWorldPos, etc.)

    Vector2Int spawnerChunkCoord; // Deklaracja dla kompilatora w tym kontekście

    // Poniżej skrócone wersje brakujących metod, wklej je do klasy:

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
            var curr = frontier.Dequeue(); if (curr == goal) break;
            foreach (var next in GetChunkNeighbors(curr))
            {
                if (next.x < 0 || next.x > mapWidth) continue;
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
        if (c.Count == 0 && maxD > 0) { /* Fallback code for small maps */ }
    }
    void HandleTags(GameObject hex, int q, int r, int radius) { /* Logika krawędzi bez zmian */ }
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

    // Wklej to NA SAMYM KOŃCU pliku, POZA klamrą klasy HexMapGenerator

    public class PriorityQueue<T>
    {
        private List<KeyValuePair<T, int>> elements = new List<KeyValuePair<T, int>>();

        public int Count => elements.Count;

        public void Enqueue(T item, int priority)
        {
            elements.Add(new KeyValuePair<T, int>(item, priority));
            // Sortujemy listę tak, aby element z najmniejszym priorytetem (kosztem) był pierwszy.
            // To prosta implementacja dla celów edukacyjnych.
            elements.Sort((x, y) => x.Value.CompareTo(y.Value));
        }

        public T Dequeue()
        {
            var item = elements[0].Key;
            elements.RemoveAt(0);
            return item;
        }
    }
}