using UnityEngine;
using System.Collections.Generic;

public class HexMapGenerator : MonoBehaviour
{
    [Header("Rozmiar Mapy")]
    public int mapWidth = 5;
    public int mapMinY = -3;
    public int mapMaxY = 3;

    [Header("Ustawienia Chunku")]
    [Tooltip("Musi być min. 6, aby punkt [4,2] istniał!")]
    public int chunkRadius = 6;

    [Header("Materiały Specjalne")]
    public Material exitMaterial;  // Np. Niebieski (Koniec drogi)
    public Material enterMaterial; // Np. Czerwony (Start drogi / Spawner)
    public Material roadMaterial;  // Opcjonalny (Środek drogi)

    [Header("Wymiary")]
    public float hexSize = 1f;
    public float padding = 0.02f;
    public GameObject hexPrefab;

    private Transform mapHolder;
    private MaterialPropertyBlock propBlock;
    private Vector2Int spawnerChunkCoord;

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

        mapHolder = new GameObject("World Map").transform;
        mapHolder.parent = transform;

        CalculateSpawnerChunkLocation();

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

        bool isSpawnerChunk = (chunkID_Q == spawnerChunkCoord.x && chunkID_R == spawnerChunkCoord.y);
        bool isStartChunk = (chunkID_Q == 0 && chunkID_R == 0);

        // Obliczamy lokalizację wejścia w chunku Spawnera
        Vector2Int spawnerEntryHex = Vector2Int.zero;
        if (isSpawnerChunk)
        {
            spawnerEntryHex = CalculateClosestHexToCenter(centerGlobalQ, centerGlobalR, chunkRadius);
        }

        Color chunkColor = ((chunkID_Q + chunkID_R) % 2 == 0) ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.6f, 0.6f, 0.6f);
        if (isStartChunk) chunkColor = new Color(0.3f, 0.7f, 0.3f);
        if (isSpawnerChunk) chunkColor = new Color(0.8f, 0.3f, 0.3f);

        GenerateHexGridLogic(chunkRadius, (localQ, localR) =>
        {
            int finalQ = centerGlobalQ + localQ;
            int finalR = centerGlobalR + localR;

            Vector3 worldPos = AxialToWorld(finalQ, finalR);
            GameObject hex = Instantiate(hexPrefab, worldPos, Quaternion.identity);
            hex.name = $"Hex_{finalQ}_{finalR}";
            hex.transform.parent = chunkObj.transform;

            // 1. Najpierw nadajemy tagi krawędzi (Domyślne)
            HandleTags(hex, localQ, localR, chunkRadius);

            // 2. Nadpisujemy tagi dla punktów specjalnych
            bool isSpecial = false;

            // --- WYJŚCIE Z BAZY (Chunk 0,0 -> Hex 4,2) ---
            // To jest "Road end", bo tu wrogowie kończą trasę
            if (isStartChunk && localQ == 4 && localR == 2)
            {
                ApplySpecialMaterial(hex, exitMaterial);
                hex.tag = "Road end"; // <--- POPRAWIONY TAG
                isSpecial = true;
            }
            // Zabezpieczenie: Jeśli radius < 6, heks 4,2 nie istnieje. 
            // Można tu dodać logikę fallback, ale zakładam, że ustawisz Radius >= 6.

            // --- WEJŚCIE SPAWNERA (Chunk Spawner -> Od strony bazy) ---
            // To jest "Road start", bo tu wrogowie wchodzą na mapę
            else if (isSpawnerChunk && localQ == spawnerEntryHex.x && localR == spawnerEntryHex.y)
            {
                ApplySpecialMaterial(hex, enterMaterial);
                hex.tag = "Road start"; // <--- POPRAWIONY TAG
                isSpecial = true;
            }

            // --- ŚRODEK SPAWNERA ---
            else if (isSpawnerChunk && localQ == 0 && localR == 0)
            {
                hex.tag = "Spawner";
            }

            // Renderowanie kolorów dla reszty
            if (!isSpecial)
            {
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

    void HandleTags(GameObject hex, int q, int r, int radius)
    {
        int dist = (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(q + r)) / 2;

        // Jeśli to nie krawędź, tagujemy jako domyślny (lub zostawiamy Untagged)
        if (dist != radius) return;

        int s = -q - r;
        bool isCorner = false;

        if (q == 0 && r == radius) isCorner = true;
        else if (q == radius && r == 0) isCorner = true;
        else if (q == radius && r == -radius) isCorner = true;
        else if (q == 0 && r == -radius) isCorner = true;
        else if (q == -radius && r == 0) isCorner = true;
        else if (q == -radius && r == radius) isCorner = true;

        if (isCorner)
        {
            hex.tag = "Chunk Corner";
            return;
        }

        // Tagi krawędzi
        if (r == radius) hex.tag = "Chunk edge 0";
        else if (s == -radius) hex.tag = "Chunk edge 1";
        else if (q == radius) hex.tag = "Chunk edge 2";
        else if (r == -radius) hex.tag = "Chunk edge 3";
        else if (s == radius) hex.tag = "Chunk edge 4";
        else if (q == -radius) hex.tag = "Chunk edge 5";
    }

    void ApplySpecialMaterial(GameObject hex, Material mat)
    {
        Renderer r = hex.GetComponentInChildren<Renderer>();
        if (r != null && mat != null) r.material = mat;
    }

    // --- Reszta funkcji matematycznych bez zmian ---

    Vector2Int CalculateClosestHexToCenter(int chunkCenterQ, int chunkCenterR, int radius)
    {
        Vector2Int bestHex = Vector2Int.zero;
        int minDistance = int.MaxValue;

        GenerateHexGridLogic(radius, (localQ, localR) =>
        {
            int distLocal = (Mathf.Abs(localQ) + Mathf.Abs(localR) + Mathf.Abs(localQ + localR)) / 2;
            if (distLocal == radius)
            {
                int globalQ = chunkCenterQ + localQ;
                int globalR = chunkCenterR + localR;
                int distToZero = (Mathf.Abs(globalQ) + Mathf.Abs(globalR) + Mathf.Abs(globalQ + globalR)) / 2;

                if (distToZero < minDistance)
                {
                    minDistance = distToZero;
                    bestHex = new Vector2Int(localQ, localR);
                }
            }
        });
        return bestHex;
    }

    void CalculateSpawnerChunkLocation()
    {
        List<Vector2Int> candidates = new List<Vector2Int>();
        int maxDistFound = 0;
        for (int x = 0; x <= mapWidth; x++)
        {
            for (int y = mapMinY; y <= mapMaxY; y++)
            {
                int q = x; int r = y - (x / 2);
                if (q == 0 && r == 0) continue;
                int dist = (Mathf.Abs(q) + Mathf.Abs(r) + Mathf.Abs(q + r)) / 2;
                if (dist >= 3) candidates.Add(new Vector2Int(q, r));
                if (dist > maxDistFound) maxDistFound = dist;
            }
        }
        if (candidates.Count > 0) spawnerChunkCoord = candidates[Random.Range(0, candidates.Count)];
        else spawnerChunkCoord = Vector2Int.zero;
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
}