using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.EventSystems;

/// <summary>
/// Zarządza mgłą wojenną z czterema stanami wizualnymi:
///
///   Locked       → pełna, ciemna mgła (defaultFogMaterial, pełna skala)
///   Unlocked /
///   Scouting     → pełna mgła z jaśniejszym odcieniem (unlockedFogMaterial, pełna skala)
///   MilitaryOnly → niska mgła/opary tuż nad ziemią (militaryFogMaterial, zredukowana skala Y)
///   FullyUnlocked → brak mgły, terrain w pełni widoczny
/// </summary>
public class FogOfWarManager : MonoBehaviour
{
    [Header("Referencje")]
    public MapExpansionManager expansionManager;
    public HexMapVisualizer    mapVisualizer;

    [Header("Materiały mgły")]
    [Tooltip("Pełna ciemna mgła – stan Locked")]
    public Material defaultFogMaterial;

    [Tooltip("Jaśniejsza mgła – stan Unlocked / Scouting (można odkryć)")]
    public Material unlockedFogMaterial;

    [Tooltip("Niskie opary – stan MilitaryOnly (teren widoczny, mgła przy ziemi)")]
    public Material militaryFogMaterial;

    [Header("Ustawienia Wizualne")]
    public GameObject fogPrefab;

    [Tooltip("Offset w osi Y przy spawnie mgły")]
    public float fogHeightOffset = 0.0f;

    [Tooltip("Skala Y mgły dla stanów Locked / Unlocked (pełna mgła)")]
    public float fogHeightScale = 1.0f;

    [Tooltip("Skala Y mgły dla stanu MilitaryOnly (niska mgiełka przy ziemi)")]
    [Range(0.01f, 0.5f)]
    public float militaryFogHeightScale = 0.08f;

    [Tooltip("Offset Y dla stanu MilitaryOnly – unosi mgłę nieznacznie nad teren")]
    public float militaryFogYOffset = 0.0f;

    [Header("Debug")]
    public bool showDebugLogs      = true;
    public bool debugClickToReveal = true;

    // ── Stan wewnętrzny ────────────────────────────────────────────────────────
    private Dictionary<Vector2Int, GameObject> activeFogChunks
        = new Dictionary<Vector2Int, GameObject>();

    public event Action<Vector2Int> OnChunkRevealed;

    private int   chunkRadius;
    private float hexSize;
    private float padding;

    // =========================================================================
    // UNITY
    // =========================================================================

    private void Update()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (!Input.GetMouseButtonDown(0)) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        if (showDebugLogs)
            Debug.Log($"[Fog Debug] Trafiono obiekt: <b>{hit.transform.name}</b> (Rodzic: {hit.transform.parent?.name})");

        foreach (var kvp in activeFogChunks)
        {
            if (hit.transform.gameObject == kvp.Value ||
                hit.transform.IsChildOf(kvp.Value.transform))
            {
                if (showDebugLogs) Debug.Log($"[Fog Debug] To jest mgła na chunku: {kvp.Key}");

                if (expansionManager != null)
                {
                    Vector3 worldPos = HexGridMath.GetChunkCenterWorld(
                        kvp.Key, chunkRadius, hexSize, padding);
                    //TODO: Napisać poprawny expansionManager expansionManager.OnFogClicked(kvp.Key, worldPos);
                }
                else
                {
                    Debug.LogError("[Fog Debug] Pole 'Expansion Manager' w FogOfWarManager jest puste!");
                }
                return;
            }
        }

        if (showDebugLogs)
            Debug.Log("[Fog Debug] Trafiony obiekt nie jest mgłą.");
    }

    // =========================================================================
    // INICJALIZACJA
    // =========================================================================

    public void InitializeFog(HashSet<Vector2Int> allChunks, int radius, float size, float pad)
    {
        ClearFog();
        chunkRadius = radius;
        hexSize     = size;
        padding     = pad;

        if (fogPrefab == null) return;

        foreach (var coord in allChunks)
        {
            SpawnFogObject(coord);

            // Teren domyślnie ukryty
            if (mapVisualizer != null)
            {
                var chunkObj = mapVisualizer.GetChunkGameObject(coord);
                if (chunkObj != null) chunkObj.SetActive(false);
            }
        }

        // Odśwież wizualizację na podstawie stanów z MapExpansionManager
        UpdateAllFogVisuals();
    }

    // =========================================================================
    // AKTUALIZACJA STANU POJEDYNCZEGO CHUNKU
    // =========================================================================

    /// <summary>
    /// Wywołaj za każdym razem gdy stan chunku się zmienia.
    /// Automatycznie dobiera materiał, skalę i widoczność terenu.
    /// </summary>
    public void UpdateChunkFogState(Vector2Int coord, ChunkState state)
    {
        switch (state)
        {
            case ChunkState.Locked:
                SetFogFull(coord, defaultFogMaterial);
                break;

            case ChunkState.Unlocked:
            case ChunkState.Scouting:
                SetFogFull(coord, unlockedFogMaterial);
                break;

            case ChunkState.MilitaryOnly:
                SetFogMilitary(coord);
                break;

            case ChunkState.FullyUnlocked:
                RevealChunk(coord);
                break;
        }
    }

    /// <summary>Pełna aktualizacja wszystkich chunków (np. po inicjalizacji).</summary>
    public void UpdateAllFogVisuals()
    {
        if (expansionManager == null) return;

        foreach (var kvp in new Dictionary<Vector2Int, GameObject>(activeFogChunks))
        {
            if (!expansionManager.activeChunks.TryGetValue(kvp.Key, out var data)) continue;
            UpdateChunkFogState(kvp.Key, data.state);
        }
    }

    // =========================================================================
    // UJAWNIANIE CHUNKU (FullyUnlocked)
    // =========================================================================

    /// <summary>Całkowite usunięcie mgły i pokazanie terenu.</summary>
    public void RevealChunk(Vector2Int coord)
    {
        if (activeFogChunks.TryGetValue(coord, out var fogObj))
        {
            if (fogObj != null) Destroy(fogObj);
            activeFogChunks.Remove(coord);
        }

        if (mapVisualizer != null)
        {
            var chunkObj = mapVisualizer.GetChunkGameObject(coord);
            if (chunkObj != null) chunkObj.SetActive(true);
        }

        OnChunkRevealed?.Invoke(coord);
    }

    public bool IsChunkRevealed(Vector2Int coord) => !activeFogChunks.ContainsKey(coord);

    // =========================================================================
    // Prywatne – ustawianie stanów wizualnych
    // =========================================================================

    /// <summary>Pełna mgła (Locked / Unlocked / Scouting) – teren ukryty.</summary>
    private void SetFogFull(Vector2Int coord, Material mat)
    {
        // Upewnij się że obiekt mgły istnieje (mógł nie istnieć przy ponownej inicjalizacji)
        if (!activeFogChunks.ContainsKey(coord))
            SpawnFogObject(coord);

        var fog = activeFogChunks[coord];
        if (fog == null) return;

        // Pełna skala
        fog.transform.localScale = new Vector3(1f, fogHeightScale, 1f);
        fog.transform.localPosition = new Vector3(
            fog.transform.localPosition.x,
            fogHeightOffset,
            fog.transform.localPosition.z);

        ApplyMaterial(fog, mat);

        // Teren ukryty
        SetTerrainVisible(coord, false);
    }

    /// <summary>Niska mgiełka (MilitaryOnly) – teren widoczny, opary przy ziemi.</summary>
    private void SetFogMilitary(Vector2Int coord)
    {
        if (!activeFogChunks.ContainsKey(coord))
            SpawnFogObject(coord);

        var fog = activeFogChunks[coord];
        if (fog == null) return;

        // Zredukowana skala Y → niska mgiełka
        fog.transform.localScale = new Vector3(1f, militaryFogHeightScale, 1f);
        fog.transform.localPosition = new Vector3(
            fog.transform.localPosition.x,
            militaryFogYOffset,
            fog.transform.localPosition.z);

        ApplyMaterial(fog, militaryFogMaterial != null ? militaryFogMaterial : defaultFogMaterial);

        // Teren widoczny spod mgły
        SetTerrainVisible(coord, true);
    }

    // =========================================================================
    // Prywatne – pomocnicze
    // =========================================================================

    private void SpawnFogObject(Vector2Int coord)
    {
        Vector3 center   = HexGridMath.GetChunkCenterWorld(coord, chunkRadius, hexSize, padding);
        Vector3 spawnPos = center + Vector3.up * fogHeightOffset;

        var fogObj = Instantiate(fogPrefab, spawnPos, Quaternion.identity, transform);
        fogObj.name = $"Fog_{coord.x}_{coord.y}";
        fogObj.transform.localScale = new Vector3(1f, fogHeightScale, 1f);

        activeFogChunks[coord] = fogObj;
    }

    private void ApplyMaterial(GameObject root, Material mat)
    {
        if (mat == null) return;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = mat;
    }

    private void SetTerrainVisible(Vector2Int coord, bool visible)
    {
        if (mapVisualizer == null) return;
        var chunkObj = mapVisualizer.GetChunkGameObject(coord);
        if (chunkObj != null) chunkObj.SetActive(visible);
    }

    private void ClearFog()
    {
        foreach (var kvp in activeFogChunks)
            if (kvp.Value != null) Destroy(kvp.Value);
        activeFogChunks.Clear();

        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }
}