using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class FogOfWarManager : MonoBehaviour
{
    public static FogOfWarManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
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

    [Header("Maska Globalna Mgły")]
    [Tooltip("Rozdzielczość generowanej tekstury maski mgły (np. 128, 256)")]
    public int maskResolution = 128;
    [Tooltip("Mnożnik promienia chunka w świecie określający zasięg mgły")]
    public float chunkRadiusMultiplier = 2.2f;
    [Tooltip("Promień wewnętrznego jednolitego rdzenia fioletowej mgły (0..1)")]
    [Range(0.2f, 0.8f)] public float coreRadiusRatio = 0.55f;
    [Tooltip("Margines wokół granic mapy (w jednostkach promienia chunka)")]
    public float worldMarginMultiplier = 3.0f;

    public event Action<Vector2Int> OnChunkRevealed;
    public event Action<Vector2Int> OnBorderlandsClicked;

    private readonly Dictionary<Vector2Int, GameObject> activeFogChunks = new Dictionary<Vector2Int, GameObject>();
    private int chunkRadius;
    private float hexSize;
    private float padding;

    // Pola dla globalnej maski mgły w shaderze
    private Texture2D fogMaskTexture;
    private Color32[] maskPixels;
    private Vector2 worldBoundsMin;
    private Vector2 worldBoundsSize;

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
            expansionManager.OnChunkStateChanged += OnChunkStateChangedHandler;

            if (expansionManager.activeChunks.Count > 0 && activeFogChunks.Count == 0)
            {
                HandleMapInitialized(null);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (expansionManager != null)
        {
            expansionManager.OnMapInitialized -= HandleMapInitialized;
            expansionManager.OnChunkStateChanged -= OnChunkStateChangedHandler;
        }

        DestroyMaskTexture();
    }

    private void OnChunkStateChangedHandler(Vector2Int coord, ChunkState state)
    {
        UpdateChunkFogState(coord, state);
        UpdateGlobalFogMask();
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

        CalculateWorldBounds(allChunks);
        InitMaskTexture();

        foreach (var coord in allChunks)
        {
            SpawnFogObject(coord);
        }

        UpdateAllFogVisuals();
    }

    // =========================================================================
    // LOGIKA GLOBALNEJ MASKI MGŁY
    // =========================================================================

    private void CalculateWorldBounds(HashSet<Vector2Int> allChunks)
    {
        if (allChunks == null || allChunks.Count == 0) return;

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);

        foreach (var coord in allChunks)
        {
            Vector3 center = HexGridMath.GetChunkCenterWorld(coord, chunkRadius, hexSize, padding);
            min = Vector2.Min(min, new Vector2(center.x, center.z));
            max = Vector2.Max(max, new Vector2(center.x, center.z));
        }

        // Dodajemy margines wielkości chunka pomnożonej przez worldMarginMultiplier z każdej strony
        float margin = chunkRadius * hexSize * worldMarginMultiplier;
        worldBoundsMin = min - new Vector2(margin, margin);
        Vector2 worldBoundsMax = max + new Vector2(margin, margin);
        worldBoundsSize = worldBoundsMax - worldBoundsMin;

        if (worldBoundsSize.x <= 0f) worldBoundsSize.x = 1f;
        if (worldBoundsSize.y <= 0f) worldBoundsSize.y = 1f;

        // Wysyłamy współrzędne świata do shaderów
        Shader.SetGlobalVector("_FogWorldBounds", new Vector4(worldBoundsMin.x, worldBoundsMin.y, worldBoundsSize.x, worldBoundsSize.y));
    }

    private void InitMaskTexture()
    {
        int res = Mathf.Clamp(maskResolution, 32, 512);

        if (fogMaskTexture != null && (fogMaskTexture.width != res || fogMaskTexture.height != res))
        {
            DestroyMaskTexture();
        }

        if (fogMaskTexture == null)
        {
            fogMaskTexture = new Texture2D(res, res, TextureFormat.R8, false);
            fogMaskTexture.filterMode = FilterMode.Bilinear;
            fogMaskTexture.wrapMode = TextureWrapMode.Clamp;
            maskPixels = new Color32[res * res];
        }

        Shader.SetGlobalTexture("_FogMask", fogMaskTexture);
    }

    private void DestroyMaskTexture()
    {
        if (fogMaskTexture != null)
        {
            if (Application.isPlaying)
            {
                Destroy(fogMaskTexture);
            }
            else
            {
                DestroyImmediate(fogMaskTexture);
            }
            fogMaskTexture = null;
        }
        maskPixels = null;
    }

    public void UpdateGlobalFogMask()
    {
        if (fogMaskTexture == null || expansionManager == null || maskPixels == null || worldBoundsSize.x <= 0f) return;

        int res = fogMaskTexture.width;

        // Domyślnie cała mapa to Wilderness (255 = 1.0 / ciemna mgła)
        byte defaultVal = 255;
        for (int i = 0; i < maskPixels.Length; i++)
        {
            maskPixels[i] = new Color32(defaultVal, defaultVal, defaultVal, 255);
        }

        float chunkWorldRadius = chunkRadius * hexSize * chunkRadiusMultiplier;
        float pixelRadius = (chunkWorldRadius / worldBoundsSize.x) * res;
        int r = Mathf.CeilToInt(pixelRadius);

        // Wypalamy wyłącznie tereny odkryte (Settled) i do odkrycia (Borderlands)
        foreach (var kvp in expansionManager.activeChunks)
        {
            if (kvp.Value.EffectiveState == ChunkState.Wilderness) continue;

            Vector3 center = HexGridMath.GetChunkCenterWorld(kvp.Key, chunkRadius, hexSize, padding);

            float u = (center.x - worldBoundsMin.x) / worldBoundsSize.x;
            float v = (center.z - worldBoundsMin.y) / worldBoundsSize.y;
            int px = Mathf.RoundToInt(u * (res - 1));
            int py = Mathf.RoundToInt(v * (res - 1));

            bool isBorderlands = kvp.Value.EffectiveState == ChunkState.Borderlands || kvp.Value.EffectiveState == ChunkState.Surveying;
            bool isSettled = kvp.Value.EffectiveState == ChunkState.Settled || kvp.Value.EffectiveState == ChunkState.Outskirts;

            for (int y = -r; y <= r; y++)
            {
                int targetY = py + y;
                if (targetY < 0 || targetY >= res) continue;

                for (int x = -r; x <= r; x++)
                {
                    int targetX = px + x;
                    if (targetX < 0 || targetX >= res) continue;

                    float distNorm = Mathf.Sqrt(x * x + y * y) / pixelRadius;
                    if (distNorm <= 1.0f)
                    {
                        int idx = targetY * res + targetX;
                        byte candidate = 255;

                        if (isSettled)
                        {
                            // Osada: czyste 0 (brak mgły, brak ciemnienia przy krawędzi z Borderlands)
                            candidate = 0;
                        }
                        else if (isBorderlands)
                        {
                            // Borderlands:
                            // Wewnątrz coreRadiusRatio (np. 0.55): czysty, jednolity fiolet (brak sztucznego punktowego światła)
                            // Na zewnątrz coreRadiusRatio ku 1.0: ciemna mgła napływa z Wilderness (wartość rośnie do 255)
                            if (distNorm <= coreRadiusRatio)
                            {
                                candidate = 0;
                            }
                            else
                            {
                                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(coreRadiusRatio, 1.0f, distNorm));
                                candidate = (byte)Mathf.RoundToInt(Mathf.Lerp(0f, 255f, t));
                            }
                        }

                        if (candidate < maskPixels[idx].r)
                        {
                            maskPixels[idx] = new Color32(candidate, candidate, candidate, 255);
                        }
                    }
                }
            }
        }

        fogMaskTexture.SetPixels32(maskPixels);
        fogMaskTexture.Apply();
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

        UpdateGlobalFogMask();
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

        DestroyMaskTexture();
    }
}
