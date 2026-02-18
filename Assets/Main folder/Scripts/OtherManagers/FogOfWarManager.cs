using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.EventSystems;

public class FogOfWarManager : MonoBehaviour
{
    [Header("Referencje")]
    public MapExpansionManager expansionManager;
    public HexMapVisualizer mapVisualizer;

    [Header("Materia³y")]
    public Material defaultFogMaterial;
    public Material ableToBuyFogMaterial;

    [Header("Ustawienia Wizualne")]
    public GameObject fogPrefab;
    public float fogHeightOffset = 0.0f;
    public float fogHeightScale = 1.0f;

    [Header("Debug")]
    public bool showDebugLogs = true; // W³¹czone logi
    public bool debugClickToReveal = true;

    private Dictionary<Vector2Int, GameObject> activeFogChunks = new Dictionary<Vector2Int, GameObject>();
    public event Action<Vector2Int> OnChunkRevealed;

    private int chunkRadius;
    private float hexSize;
    private float padding;

    private void Update()
    {
        // 1. Sprawdzenie blokady UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // 2. Wykonanie Raycasta
            if (Physics.Raycast(ray, out hit))
            {
                if (showDebugLogs)
                    Debug.Log($"[Fog Debug] Trafiono obiekt: <b>{hit.transform.name}</b> (Rodzic: {hit.transform.parent?.name})");
                // Sprawdzamy czy trafiliœmy w mg³ê
                foreach (var kvp in activeFogChunks)
                {
                    GameObject fogRoot = kvp.Value;

                    // Sprawdzamy czy trafiony obiekt to czêœæ tej konkretnej mg³y
                    if (hit.transform.gameObject == fogRoot || hit.transform.IsChildOf(fogRoot.transform))
                    {
                        if (showDebugLogs) Debug.Log($"[Fog Debug] To jest mg³a na chunku: {kvp.Key}");

                        if (expansionManager != null)
                        {
                            expansionManager.OnFogClicked(kvp.Key);
                        }
                        else
                        {
                            Debug.LogError("[Fog Debug] B£¥D! Pole 'Expansion Manager' w FogOfWarManager jest puste! Przypisz je w Inspektorze.");
                        }
                        return; // Znaleziono i obs³u¿ono
                    }
                }

                // Jeœli pêtla siê skoñczy³a i nie weszliœmy w 'if', znaczy ¿e trafiliœmy w coœ co nie jest mg³¹ w s³owniku
                if (showDebugLogs) Debug.Log("[Fog Debug] Trafiony obiekt nie znajduje siê w s³owniku aktywnej mg³y.");
            }
            else
            {
                if (showDebugLogs) Debug.Log("[Fog Debug] Raycast nie trafi³ w nic (w powietrze lub brak collidera).");
            }
        }
    }

    // --- RESZTA KODU BEZ ZMIAN ---

    public void UpdateAllFogVisuals()
    {
        if (expansionManager == null) return;
        foreach (var kvp in activeFogChunks)
        {
            Vector2Int coord = kvp.Key;
            GameObject fogRoot = kvp.Value;
            bool buyable = expansionManager.IsChunkBuyable(coord);
            Material matToUse = buyable ? ableToBuyFogMaterial : defaultFogMaterial;
            ApplyMaterialToHierarchy(fogRoot, matToUse);
        }
    }

    void ApplyMaterialToHierarchy(GameObject root, Material mat)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers) r.sharedMaterial = mat;
    }

    public void InitializeFog(HashSet<Vector2Int> allChunks, int radius, float size, float pad)
    {
        ClearFog();
        chunkRadius = radius; hexSize = size; padding = pad;
        if (fogPrefab == null) return;
        foreach (var chunkCoord in allChunks)
        {
            CreateFogForChunk(chunkCoord);
            if (mapVisualizer != null) { GameObject chunkObj = mapVisualizer.GetChunkGameObject(chunkCoord); if (chunkObj != null) chunkObj.SetActive(false); }
        }
        UpdateAllFogVisuals();
    }

    void CreateFogForChunk(Vector2Int coord)
    {
        Vector3 center = HexGridMath.GetChunkCenterWorld(coord, chunkRadius, hexSize, padding);
        Vector3 spawnPos = center + Vector3.up * fogHeightOffset;
        GameObject fogObj = Instantiate(fogPrefab, spawnPos, Quaternion.identity, transform);
        fogObj.name = $"Fog_{coord.x}_{coord.y}";
        fogObj.transform.localScale = new Vector3(1f, fogHeightScale, 1f);
        activeFogChunks.Add(coord, fogObj);
    }

    public void RevealChunk(Vector2Int coord)
    {
        if (activeFogChunks.ContainsKey(coord))
        {
            GameObject fogObj = activeFogChunks[coord];
            if (fogObj != null) Destroy(fogObj);
            activeFogChunks.Remove(coord);
            if (mapVisualizer != null) { GameObject chunkObj = mapVisualizer.GetChunkGameObject(coord); if (chunkObj != null) chunkObj.SetActive(true); }
            OnChunkRevealed?.Invoke(coord);
        }
    }

    public bool IsChunkRevealed(Vector2Int coord) { return !activeFogChunks.ContainsKey(coord); }
    private void ClearFog() { foreach (var kvp in activeFogChunks) if (kvp.Value != null) Destroy(kvp.Value); activeFogChunks.Clear(); for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject); }
}