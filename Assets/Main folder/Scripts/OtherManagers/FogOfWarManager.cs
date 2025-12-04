using UnityEngine;
using System.Collections.Generic;
using System;

public class FogOfWarManager : MonoBehaviour
{
    [Header("Interakcja")]
    public MapExpansionManager expansionManager;

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
        // Blokada klikania przez UI
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Uwaga: Heksy wewn¹trz prefabu mg³y musz¹ mieæ Collidery!
            if (Physics.Raycast(ray, out hit))
            {
                // Iterujemy po wszystkich aktywnych chunkach mg³y
                foreach (var kvp in activeFogChunks)
                {
                    GameObject fogRoot = kvp.Value;

                    // SPRAWDZENIE:
                    // Czy trafiliœmy w sam korzeñ mg³y LUB w którekolwiek z jego dzieci (ma³e heksy)?
                    if (hit.transform.gameObject == fogRoot || hit.transform.IsChildOf(fogRoot.transform))
                    {
                        // Mamy trafienie!
                        if (expansionManager != null)
                        {
                            expansionManager.OnFogClicked(kvp.Key);
                        }
                        return; // Przerywamy pêtlê, znaleŸliœmy w³aœciwy chunk
                    }
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

        fogObj.transform.localScale = new Vector3(1, 1, 1);

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