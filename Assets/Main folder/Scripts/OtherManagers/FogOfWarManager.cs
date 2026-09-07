using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class FogOfWarManager : MonoBehaviour
{
    [Header("Referencje")]
    public MapExpansionManager expansionManager;
    public HexMapVisualizer mapVisualizer;
    public HexMapGenerator mapGenerator;

    [Header("Materiały Mgły")]
    public Material defaultFogMaterial;
    public Material borderlandsFogMaterial;
    public Material surveyingFogMaterial;
    public Material outskirtsFogMaterial;

    [Header("Ustawienia Obiektu")]
    public GameObject fogPrefab;
    public float fogHeightOffset = 0.0f;
    public float fogHeightScale = 1.0f;
    public float outskirtsFogHeightScale = 0.08f;
    public float outskirtsFogYOffset = 0.0f;

    public event Action<Vector2Int> OnChunkRevealed;
    public event Action<Vector2Int> OnBorderlandsClicked;

    private readonly Dictionary<Vector2Int, GameObject> activeFogChunks = new Dictionary<Vector2Int, GameObject>();
    private int chunkRadius;
    private float hexSize;
    private float padding;

    private void Start()
    {
        if (mapGenerator != null)
        {
            chunkRadius = mapGenerator.chunkRadius;
            hexSize = mapGenerator.hexSize;
            padding = mapGenerator.padding;
        }

        if (expansionManager != null)
        {
            expansionManager.OnMapInitialized += HandleMapInitialized;
            expansionManager.OnChunkStateChanged += UpdateChunkFogState;

            if (expansionManager.activeChunks.Count > 0 && activeFogChunks.Count == 0)
            {
                HandleMapInitialized(null);
            }
        }
    }

    private void OnDestroy()
    {
        if (expansionManager != null)
        {
            expansionManager.OnMapInitialized -= HandleMapInitialized;
            expansionManager.OnChunkStateChanged -= UpdateChunkFogState;
        }
    }

    private void Update()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (!Input.GetMouseButtonDown(0)) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return;

        foreach (var kvp in activeFogChunks)
        {
            if (hit.transform.gameObject == kvp.Value || hit.transform.IsChildOf(kvp.Value.transform))
            {
                if (expansionManager != null && expansionManager.GetEffectiveState(kvp.Key) == ChunkState.Borderlands)
                {
                    OnBorderlandsClicked?.Invoke(kvp.Key);
                }
                return;
            }
        }
    }

    private void HandleMapInitialized(List<Vector2Int> _)
    {
        if (expansionManager == null) return;
        InitializeFog(new HashSet<Vector2Int>(expansionManager.activeChunks.Keys), chunkRadius, hexSize, padding);
    }

    public void InitializeFog(HashSet<Vector2Int> allChunks, int radius, float size, float pad)
    {
        ClearFog();
        chunkRadius = radius;
        hexSize = size;
        padding = pad;

        if (fogPrefab == null) return;

        foreach (var coord in allChunks)
        {
            SpawnFogObject(coord);
        }

        UpdateAllFogVisuals();
    }

    public void UpdateChunkFogState(Vector2Int coord, ChunkState state)
    {
        switch (state)
        {
            case ChunkState.Wilderness:
                SetFogFull(coord, defaultFogMaterial);
                SetTerrainVisible(coord, false);
                break;

            case ChunkState.Borderlands:
                Material borderMat = borderlandsFogMaterial != null ? borderlandsFogMaterial : defaultFogMaterial;
                SetFogFull(coord, borderMat);
                SetTerrainVisible(coord, false);
                break;

            case ChunkState.Surveying:
                Material surveyMat = surveyingFogMaterial != null ? surveyingFogMaterial : defaultFogMaterial;
                SetFogFull(coord, surveyMat);
                SetTerrainVisible(coord, true);
                break;

            case ChunkState.Outskirts:
                Material outskirtMat = outskirtsFogMaterial != null ? outskirtsFogMaterial : defaultFogMaterial;
                SetFogLow(coord, outskirtMat);
                SetTerrainVisible(coord, true);
                OnChunkRevealed?.Invoke(coord);
                break;

            case ChunkState.Settled:
                RemoveFog(coord);
                SetTerrainVisible(coord, true);
                OnChunkRevealed?.Invoke(coord);
                break;
        }
    }

    public void UpdateAllFogVisuals()
    {
        if (expansionManager == null) return;

        foreach (var kvp in expansionManager.activeChunks)
        {
            UpdateChunkFogState(kvp.Key, kvp.Value.EffectiveState);
        }
    }

    public bool IsChunkRevealed(Vector2Int coord)
    {
        if (expansionManager != null)
        {
            var state = expansionManager.GetEffectiveState(coord);
            return state == ChunkState.Outskirts || state == ChunkState.Settled;
        }
        return !activeFogChunks.ContainsKey(coord);
    }

    private void SetFogFull(Vector2Int coord, Material mat)
    {
        if (!activeFogChunks.ContainsKey(coord))
        {
            SpawnFogObject(coord);
        }

        var fog = activeFogChunks[coord];
        if (fog == null) return;

        fog.transform.localScale = new Vector3(1f, fogHeightScale, 1f);
        fog.transform.localPosition = new Vector3(fog.transform.localPosition.x, fogHeightOffset, fog.transform.localPosition.z);
        ApplyMaterial(fog, mat);
    }

    private void SetFogLow(Vector2Int coord, Material mat)
    {
        if (!activeFogChunks.ContainsKey(coord))
        {
            SpawnFogObject(coord);
        }

        var fog = activeFogChunks[coord];
        if (fog == null) return;

        fog.transform.localScale = new Vector3(1f, outskirtsFogHeightScale, 1f);
        fog.transform.localPosition = new Vector3(fog.transform.localPosition.x, outskirtsFogYOffset, fog.transform.localPosition.z);
        ApplyMaterial(fog, mat);
    }

    private void RemoveFog(Vector2Int coord)
    {
        if (activeFogChunks.TryGetValue(coord, out var fogObj))
        {
            if (fogObj != null)
            {
                Destroy(fogObj);
            }
            activeFogChunks.Remove(coord);
        }
    }

    private void SpawnFogObject(Vector2Int coord)
    {
        Vector3 center = HexGridMath.GetChunkCenterWorld(coord, chunkRadius, hexSize, padding);
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
        {
            r.sharedMaterial = mat;
        }
    }

    private void SetTerrainVisible(Vector2Int coord, bool visible)
    {
        if (mapVisualizer == null) return;
        var chunkObj = mapVisualizer.GetChunkGameObject(coord);
        if (chunkObj != null)
        {
            chunkObj.SetActive(visible);
        }
    }

    private void ClearFog()
    {
        foreach (var kvp in activeFogChunks)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }
        activeFogChunks.Clear();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }
}