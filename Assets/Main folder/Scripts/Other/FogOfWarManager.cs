using UnityEngine;
using System.Collections.Generic;
using System;

public class FogOfWarManager : MonoBehaviour
{
    [Header("Ustawienia Wizualne")]
    public GameObject fogPrefab;
    public float fogHeightOffset = 2.0f;

    [Header("Debug")]
    public bool showDebugLogs = false;
    [Tooltip("Jeœli zaznaczone, klikniêcie LPM na mg³ê usunie j¹.")]
    public bool debugClickToReveal = true; // <--- NOWA ZMIENNA

    private Dictionary<Vector2Int, GameObject> activeFogChunks = new Dictionary<Vector2Int, GameObject>();

    // Zdarzenie dla innych skryptów
    public event Action<Vector2Int> OnChunkRevealed;

    private int chunkRadius;
    private float hexSize;
    private float padding;

    // --- NOWA METODA UPDATE DO OBS£UGI KLIKNIÊÆ ---
    private void Update()
    {
        // Jeœli debug jest wy³¹czony, nic nie rób
        if (!debugClickToReveal) return;

        // Reakcja na lewy przycisk myszy
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Strzelamy promieniem (Wymaga Collidera na obiekcie mg³y!)
            if (Physics.Raycast(ray, out hit))
            {
                GameObject hitObj = hit.transform.gameObject;

                // Szukamy, który to chunk (iterujemy po s³owniku)
                Vector2Int? foundCoord = null;
                foreach (var kvp in activeFogChunks)
                {
                    if (kvp.Value == hitObj)
                    {
                        foundCoord = kvp.Key;
                        break;
                    }
                }

                // Jeœli znaleŸliœmy pasuj¹cy chunk, odkrywamy go
                if (foundCoord.HasValue)
                {
                    if (showDebugLogs) Debug.Log($"[FogManager Debug] Klikniêto i odkryto: {foundCoord.Value}");
                    RevealChunk(foundCoord.Value);
                }
            }
        }
    }

    public void InitializeFog(HashSet<Vector2Int> allChunks, int radius, float size, float pad)
    {
        ClearFog();
        chunkRadius = radius; hexSize = size; padding = pad;

        if (fogPrefab == null) return;

        foreach (var chunkCoord in allChunks) CreateFogForChunk(chunkCoord);
    }

    void CreateFogForChunk(Vector2Int coord)
    {
        Vector3 center = HexGridMath.GetChunkCenterWorld(coord, chunkRadius, hexSize, padding);
        Vector3 spawnPos = center + Vector3.up * fogHeightOffset;
        GameObject fogObj = Instantiate(fogPrefab, spawnPos, Quaternion.identity, transform);
        fogObj.name = $"Fog_{coord.x}_{coord.y}";

        float width = (chunkRadius * 2 + 1) * hexSize * 1.75f;
        fogObj.transform.localScale = new Vector3(width, 1, width);

        activeFogChunks.Add(coord, fogObj);
    }

    public void RevealChunk(Vector2Int coord)
    {
        if (activeFogChunks.ContainsKey(coord))
        {
            if (showDebugLogs) Debug.Log($"[FogManager] Odkrywanie chunku: {coord}");
            GameObject fogObj = activeFogChunks[coord];
            if (fogObj != null) Destroy(fogObj);

            activeFogChunks.Remove(coord);

            // Powiadamiamy Spawner
            OnChunkRevealed?.Invoke(coord);
        }
    }

    public bool IsChunkRevealed(Vector2Int coord)
    {
        return !activeFogChunks.ContainsKey(coord);
    }

    public void ClearFog()
    {
        foreach (var kvp in activeFogChunks) if (kvp.Value != null) Destroy(kvp.Value);
        activeFogChunks.Clear();

        List<GameObject> children = new List<GameObject>();
        foreach (Transform child in transform) children.Add(child.gameObject);
        foreach (GameObject child in children) DestroyImmediate(child);
    }
}