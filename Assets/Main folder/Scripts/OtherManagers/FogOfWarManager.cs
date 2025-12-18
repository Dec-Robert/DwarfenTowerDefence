using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.EventSystems;

public class FogOfWarManager : MonoBehaviour
{
    [Header("Referencje")]
    public MapExpansionManager expansionManager;
    public HexMapVisualizer mapVisualizer;

    [Header("Ustawienia Wizualne")]
    public GameObject fogPrefab;

    public float fogHeightOffset = 0.0f; // Przesuniêcie w pionie
    public float fogHeightScale = 1.0f;  // Skala Y (ustaw na 5 jeœli chcesz wysoki kloc, na 1 jeœli standard)

    [Header("Debug")]
    public bool showDebugLogs = false;
    public bool debugClickToReveal = true;

    private Dictionary<Vector2Int, GameObject> activeFogChunks = new Dictionary<Vector2Int, GameObject>();
    public event Action<Vector2Int> OnChunkRevealed;

    // Zmienne pomocnicze
    private int chunkRadius;
    private float hexSize;
    private float padding;

    private void Update()
    {
        // 1. Sprawdzenie UI
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            // Debug.Log("Klikniêcie zablokowane przez UI");
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // 2. Strza³ promieniem
            if (Physics.Raycast(ray, out hit))
            {
                // DEBUG: W co trafiliœmy?
                Debug.Log($"Raycast trafi³ w obiekt: <b>{hit.transform.name}</b> (Rodzic: {hit.transform.parent?.name})");

                foreach (var kvp in activeFogChunks)
                {
                    GameObject fogRoot = kvp.Value;

                    // Sprawdzamy czy trafiony obiekt to czêœæ mg³y
                    if (hit.transform.gameObject == fogRoot || hit.transform.IsChildOf(fogRoot.transform))
                    {
                        Debug.Log($"<color=green>Trafiono w MG£Ê na chunku: {kvp.Key}</color>");

                        if (expansionManager != null)
                        {
                            expansionManager.OnFogClicked(kvp.Key);
                        }
                        else
                        {
                            Debug.LogError("Brak przypisanego Expansion Managera w FogOfWarManager!");
                        }
                        return;
                    }
                }

                Debug.Log("<color=orange>Trafiono w coœ, ale to NIE jest aktywna mg³a (mo¿e teren pod spodem?).</color>");
            }
            else
            {
                Debug.Log("<color=red>Raycast nie trafi³ w nic (Brak collidera?).</color>");
            }
        }
    }

    public void InitializeFog(HashSet<Vector2Int> allChunks, int radius, float size, float pad)
    {
        ClearFog();
        chunkRadius = radius; hexSize = size; padding = pad;

        if (fogPrefab == null) return;

        foreach (var chunkCoord in allChunks)
        {
            CreateFogForChunk(chunkCoord);

            // Wy³¹czamy chunk terenu na start (ukrywamy go)
            if (mapVisualizer != null)
            {
                GameObject chunkObj = mapVisualizer.GetChunkGameObject(chunkCoord);
                if (chunkObj != null)
                {
                    chunkObj.SetActive(false);
                }
            }
        }
    }

    void CreateFogForChunk(Vector2Int coord)
    {
        Vector3 center = HexGridMath.GetChunkCenterWorld(coord, chunkRadius, hexSize, padding);
        Vector3 spawnPos = center + Vector3.up * fogHeightOffset;

        GameObject fogObj = Instantiate(fogPrefab, spawnPos, Quaternion.identity, transform);
        fogObj.name = $"Fog_{coord.x}_{coord.y}";

        // POPRAWKA: Ustawiamy skalê X i Z na 1, bo Twój prefab ma ju¿ dobry rozmiar.
        // Skalujemy tylko Y (wysokoœæ) zgodnie z Twoim ¿yczeniem.
        fogObj.transform.localScale = new Vector3(1f, fogHeightScale, 1f);

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

            // W³¹cz teren pod spodem
            if (mapVisualizer != null)
            {
                GameObject chunkObj = mapVisualizer.GetChunkGameObject(coord);
                if (chunkObj != null)
                {
                    chunkObj.SetActive(true);
                }
            }

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

        for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);
    }
}