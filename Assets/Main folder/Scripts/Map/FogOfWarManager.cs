using System;
using System.Collections;
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

        if (expansionManager == null) expansionManager = MapExpansionManager.Instance ?? FindFirstObjectByType<MapExpansionManager>();
        if (mapGenerator == null) mapGenerator = HexMapGenerator.Instance ?? FindFirstObjectByType<HexMapGenerator>();
        if (mapVisualizer == null) mapVisualizer = HexMapVisualizer.Instance ?? FindFirstObjectByType<HexMapVisualizer>();

        EnsureMaterials();

        if (mistHeightOffset <= 0f && fogHeightOffset > 0f)
        {
            mistHeightOffset = fogHeightOffset;
        }
        else if (mistHeightOffset <= 0f)
        {
            mistHeightOffset = 0.2f;
        }
    }

    private void OnValidate()
    {
        EnsureMaterials();
    }

    private void EnsureMaterials()
    {
#if UNITY_EDITOR
        if (defaultFogMaterial == null)
        {
            defaultFogMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Main folder/Materials/MistHLSL.mat");
        }
        if (borderlandsFogMaterial == null)
        {
            borderlandsFogMaterial = defaultFogMaterial;
        }
        if (surveyingFogMaterial == null || surveyingFogMaterial.name.Contains("FogOfWar3"))
        {
            surveyingFogMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Main folder/Materials/MistHLSL_Surveying.mat");
        }
        if (surveyingDepthMaterial == null)
        {
            surveyingDepthMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Main folder/Materials/MistHLSL_SurveyingDepth.mat");
        }
        if (outskirtsFogMaterial == null || outskirtsFogMaterial.name.Contains("FogOfWarAbleToBuy") || outskirtsFogMaterial.name.Contains("GroundMist"))
        {
            outskirtsFogMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Main folder/Materials/Subject To Change/TmpFog.mat");
            if (outskirtsFogMaterial == null)
            {
                outskirtsFogMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Main folder/Materials/TmpMist.mat");
            }
        }
#endif
        if (defaultFogMaterial != null)
        {
            defaultFogMaterial.renderQueue = 2990;
            defaultFogMaterial.SetFloat("_Cull", 0f);
        }
        if (borderlandsFogMaterial != null && borderlandsFogMaterial != defaultFogMaterial)
        {
            borderlandsFogMaterial.renderQueue = 2990;
            borderlandsFogMaterial.SetFloat("_Cull", 0f);
        }
        if (surveyingFogMaterial != null)
        {
            surveyingFogMaterial.renderQueue = 3000;
        }
    }
    [Header("Referencje")]
    public MapExpansionManager expansionManager;
    public HexMapVisualizer mapVisualizer;
    public HexMapGenerator mapGenerator;

    [Header("Materiały Mgły")]
    public Material defaultFogMaterial;
    public Material borderlandsFogMaterial;
    public Material surveyingFogMaterial;
    [Tooltip("Depth prepass gornej tafli mgly Surveying (shader Custom/MistDepthPrepass). Ukrywa boczne scianki widziane przez polprzezroczysta gore, scianki zewnetrzne zostaja widoczne.")]
    public Material surveyingDepthMaterial;
    public Material outskirtsFogMaterial;

    [Header("Ustawienia Obiektu")]
    public GameObject fogPrefab;
    public float fogHeightOffset = 0.0f;
    public float fogHeightScale = 1.0f;
    public float outskirtsFogHeightScale = 0.08f;
    public float outskirtsFogYOffset = 0.0f;

    [Header("Ground Mist (Niska Mgła Odkrytego Terenu)")]
    [Tooltip("Offset ponad rzeczywista powierzchnie kazdego heksa zapobiegajacy Z-fighting (np. 0.03).")]
    public float groundMistHeightOffset = 0.03f;
    [Tooltip("Czas plynnego pojawiania sie / zanikania Ground Mist w sekundach (0 = natychmiast).")]
    public float groundMistFadeDuration = 1.2f;

    private static readonly int PropGroundMistFade = Shader.PropertyToID("_GroundMistFade");
    private MaterialPropertyBlock groundMistPropertyBlock;
    private readonly Dictionary<Vector2Int, Coroutine> activeFadeCoroutines = new Dictionary<Vector2Int, Coroutine>();

    [Header("Wspólna Wysokość Mgły (Mist World Height)")]
    [Tooltip("Czy automatycznie obliczać wysokość mgły na podstawie maksymalnej wysokości powierzchni terenu.")]
    public bool autoCalculateMistHeight = true;

    [Tooltip("Ręczna wysokość Y mgły w przestrzeni świata (używana gdy autoCalculateMistHeight = false).")]
    public float manualMistWorldHeight = 1.3f;

    [Tooltip("Dodatni offset ponad maksymalną powierzchnię terenu zapobiegający Z-fighting.")]
    public float mistHeightOffset = 0.2f;

    [Tooltip("Awaryjny maksymalny poziom wzniesienia terenu (gdy brak danych z wizualizatora, np. 5).")]
    public int fallbackMaxElevationLevel = 5;

    private float cachedMistWorldHeight = 0f;
    private bool isMistHeightCached = false;

    /// <summary>
    /// Jednolita wysokość Y w przestrzeni świata dla wszystkich poziomych powierzchni mgły eksploracyjnej.
    /// MistWorldHeight = MaxTerrainSurfaceY + mistHeightOffset.
    /// </summary>
    public float MistWorldHeight
    {
        get
        {
            if (isMistHeightCached)
            {
                return cachedMistWorldHeight;
            }
            return CalculateMistWorldHeight();
        }
    }

    /// <summary>
    /// Maksymalna wysokość powierzchni terenu w przestrzeni świata (Y).
    /// </summary>
    public float MaxTerrainSurfaceY
    {
        get
        {
            if (mapVisualizer != null)
            {
                return mapVisualizer.MaxTerrainSurfaceY;
            }
            if (mapGenerator != null && mapGenerator.visualizer != null)
            {
                return mapGenerator.visualizer.MaxTerrainSurfaceY;
            }
            return fallbackMaxElevationLevel * 0.25f;
        }
    }

    public float CalculateMistWorldHeight()
    {
        if (!autoCalculateMistHeight)
        {
            return manualMistWorldHeight;
        }

        float maxY = MaxTerrainSurfaceY;
        float offset = mistHeightOffset > 0f ? mistHeightOffset : (fogHeightOffset > 0f ? fogHeightOffset : 0.2f);
        return maxY + offset;
    }

    public void RefreshMistHeight()
    {
        cachedMistWorldHeight = CalculateMistWorldHeight();
        isMistHeightCached = true;

        if (expansionManager != null)
        {
            foreach (var kvp in activeFogChunks)
            {
                if (kvp.Value == null) continue;
                var state = expansionManager.GetEffectiveState(kvp.Key);
                if (state == ChunkState.Wilderness || state == ChunkState.Borderlands || state == ChunkState.Surveying)
                {
                    Vector3 pos = kvp.Value.transform.position;
                    pos.y = cachedMistWorldHeight;
                    kvp.Value.transform.position = pos;
                }
            }
        }
    }

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
        if (expansionManager == null) expansionManager = MapExpansionManager.Instance ?? FindFirstObjectByType<MapExpansionManager>();
        if (mapGenerator == null) mapGenerator = HexMapGenerator.Instance ?? FindFirstObjectByType<HexMapGenerator>();
        if (mapVisualizer == null) mapVisualizer = HexMapVisualizer.Instance ?? FindFirstObjectByType<HexMapVisualizer>();

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
            expansionManager.OnMissionStarted += HandleMissionChanged;
            expansionManager.OnMissionCompleted += HandleMissionChanged;

            if (expansionManager.activeChunks.Count > 0 && activeFogChunks.Count == 0)
            {
                HandleMapInitialized(null);
            }
        }
    }

    private void HandleMissionChanged(ScoutingMission mission)
    {
        if (mission != null && expansionManager != null)
        {
            UpdateChunkFogState(mission.targetChunk, expansionManager.GetEffectiveState(mission.targetChunk));
            UpdateGlobalFogMask();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (expansionManager != null)
        {
            expansionManager.OnMapInitialized -= HandleMapInitialized;
            expansionManager.OnChunkStateChanged -= OnChunkStateChangedHandler;
            expansionManager.OnMissionStarted -= HandleMissionChanged;
            expansionManager.OnMissionCompleted -= HandleMissionChanged;
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

        RefreshMistHeight();

        if (fogPrefab == null) return;

        CalculateWorldBounds(allChunks);
        InitMaskTexture();

        if (expansionManager == null) expansionManager = MapExpansionManager.Instance ?? FindFirstObjectByType<MapExpansionManager>();
        if (mapGenerator == null) mapGenerator = HexMapGenerator.Instance ?? FindFirstObjectByType<HexMapGenerator>();
        if (mapVisualizer == null) mapVisualizer = HexMapVisualizer.Instance ?? FindFirstObjectByType<HexMapVisualizer>();

        if (chunkRadius <= 0 && mapGenerator != null) chunkRadius = mapGenerator.chunkRadius;
        if (hexSize <= 0f && mapGenerator != null) hexSize = mapGenerator.hexSize;
        if (padding <= 0f && mapGenerator != null) padding = mapGenerator.padding;

        foreach (var coord in allChunks)
        {
            // Nigdy nie spawnujemy prefabu mgly na chunkach startowo osiedlonych/odkrytych (np. baza)
            if (expansionManager != null && expansionManager.GetEffectiveState(coord) == ChunkState.Settled)
            {
                continue;
            }
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
        Material discMat = borderlandsFogMaterial != null ? borderlandsFogMaterial : defaultFogMaterial;

        switch (state)
        {
            case ChunkState.Wilderness:
                SetFogFull(coord, defaultFogMaterial);
                SetTerrainVisible(coord, false);
                break;

            case ChunkState.Borderlands:
                SetFogFull(coord, discMat);
                SetTerrainVisible(coord, false);
                break;

            case ChunkState.Surveying:
                EnsureMaterials();
                Material surveyMat = (surveyingFogMaterial != null && !surveyingFogMaterial.name.Contains("FogOfWar3"))
                    ? surveyingFogMaterial
                    : discMat;
                SetFogFull(coord, surveyMat, surveyingDepthMaterial);
                SetTerrainVisible(coord, true);
                break;

            case ChunkState.Outskirts:
                EnsureMaterials();
                Material outskirtMat = outskirtsFogMaterial != null ? outskirtsFogMaterial : defaultFogMaterial;
                SetFogLow(coord, outskirtMat);
                SetTerrainVisible(coord, true);
                OnChunkRevealed?.Invoke(coord);
                break;

            case ChunkState.Settled:
                RemoveFogWithFade(coord, groundMistFadeDuration);
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

    private void SetFogFull(Vector2Int coord, Material mat, Material depthPrepassMat = null)
    {
        StopFadeCoroutine(coord);

        if (!activeFogChunks.ContainsKey(coord))
        {
            SpawnFogObject(coord);
        }

        var fog = activeFogChunks[coord];
        if (fog == null) return;

        // Przywracamy pozycje Y=0 dla wszystkich dzieci heksow (jednolity dach mgly eksploracyjnej)
        for (int i = 0; i < fog.transform.childCount; i++)
        {
            Transform child = fog.transform.GetChild(i);
            child.localPosition = new Vector3(child.localPosition.x, 0f, child.localPosition.z);
            child.localScale = Vector3.one;
        }

        float xzScale = hexSize > 0f ? (hexSize + padding) / hexSize : 1f;
        fog.transform.localScale = new Vector3(xzScale, fogHeightScale, xzScale);
        Vector3 pos = fog.transform.position;
        pos.y = MistWorldHeight;
        fog.transform.position = pos;
        ApplyMaterial(fog, mat, depthPrepassMat);
    }

    private void SetFogLow(Vector2Int coord, Material mat)
    {
        StopFadeCoroutine(coord);

        if (!activeFogChunks.ContainsKey(coord))
        {
            SpawnFogObject(coord);
        }

        var fog = activeFogChunks[coord];
        if (fog == null) return;

        // Usuwamy collidery z mgly na Outskirts, aby gracz mogl swobodnie klikac teren, wybierac heksy i budowac
        Collider[] colliders = fog.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
                if (Application.isPlaying)
                {
                    Destroy(colliders[i]);
                }
                else
                {
                    DestroyImmediate(colliders[i]);
                }
            }
        }

        Vector3 center = HexGridMath.GetChunkCenterWorld(coord, chunkRadius, hexSize, padding);
        fog.transform.position = new Vector3(center.x, 0f, center.z);
        float xzScale = hexSize > 0f ? (hexSize + padding) / hexSize : 1f;
        fog.transform.localScale = new Vector3(xzScale, 1f, xzScale);

        float heightStep = 0.25f;
        if (mapVisualizer != null)
        {
            heightStep = mapVisualizer.elevationHeightStep;
        }

        // Dopasowanie kazdego heksa w prefabie do jego rzeczywistej wysokosci terenu:
        // wysokosc heksa + skok na jeden poziom / 2 (np. dla poziomu 4 mgla bedzie na poziomie 4.5)
        float halfStep = heightStep * 0.5f;

        for (int i = 0; i < fog.transform.childCount; i++)
        {
            Transform child = fog.transform.GetChild(i);
            float surfaceY = 0f;
            if (mapVisualizer != null)
            {
                surfaceY = mapVisualizer.GetHexTerrainSurfaceYByLocalPos(coord, child.localPosition);
            }
            float targetY = surfaceY + halfStep;
            child.localPosition = new Vector3(child.localPosition.x, targetY, child.localPosition.z);
            child.localScale = Vector3.one;
        }

        ApplyMaterial(fog, mat);

        if (Application.isPlaying && gameObject.activeInHierarchy && groundMistFadeDuration > 0f)
        {
            activeFadeCoroutines[coord] = StartCoroutine(FadeGroundMistRoutine(fog, 0f, 1f, groundMistFadeDuration, coord));
        }
        else
        {
            SetChunkGroundMistFade(fog, 1f);
        }
    }

    private void SetChunkGroundMistFade(GameObject fog, float fade)
    {
        if (fog == null) return;
        if (groundMistPropertyBlock == null) groundMistPropertyBlock = new MaterialPropertyBlock();
        groundMistPropertyBlock.SetFloat(PropGroundMistFade, Mathf.Clamp01(fade));

        fog.GetComponentsInChildren(rendererBuffer);
        foreach (var r in rendererBuffer)
        {
            r.SetPropertyBlock(groundMistPropertyBlock);
        }
        rendererBuffer.Clear();
    }

    private void StopFadeCoroutine(Vector2Int coord)
    {
        if (activeFadeCoroutines.TryGetValue(coord, out var coroutine))
        {
            if (coroutine != null) StopCoroutine(coroutine);
            activeFadeCoroutines.Remove(coord);
        }
    }

    private IEnumerator FadeGroundMistRoutine(GameObject fog, float startFade, float targetFade, float duration, Vector2Int coord, System.Action onComplete = null)
    {
        float elapsed = 0f;
        SetChunkGroundMistFade(fog, startFade);
        while (elapsed < duration)
        {
            if (fog == null) break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float currentFade = Mathf.Lerp(startFade, targetFade, t);
            SetChunkGroundMistFade(fog, currentFade);
            yield return null;
        }
        if (fog != null)
        {
            SetChunkGroundMistFade(fog, targetFade);
        }
        activeFadeCoroutines.Remove(coord);
        onComplete?.Invoke();
    }

    private void RemoveFogWithFade(Vector2Int coord, float duration)
    {
        StopFadeCoroutine(coord);

        if (activeFogChunks.TryGetValue(coord, out var fogObj) && fogObj != null)
        {
            // Plynne zanikanie (Ground Mist Fade) ma zastosowanie WYLACZNIE do niskiej mgly (GroundMist),
            // ktora posiada wlasciwosc _GroundMistFade w shaderze.
            // Prefab wysokiej mgly eksploracyjnej (MistHLSL) nie posiada tej wlasciwosci - oczekiwanie na coroutine
            // powodowalo, ze mgla wisiala w 100% nieprzezroczysta przez caly czas trwania duration.
            bool canFade = false;
            if (Application.isPlaying && gameObject.activeInHierarchy && duration > 0f)
            {
                var rend = fogObj.GetComponentInChildren<Renderer>();
                if (rend != null && rend.sharedMaterial != null && rend.sharedMaterial.HasProperty(PropGroundMistFade))
                {
                    canFade = true;
                }
            }

            if (canFade)
            {
                activeFadeCoroutines[coord] = StartCoroutine(FadeGroundMistRoutine(fogObj, 1f, 0f, duration, coord, () =>
                {
                    RemoveFog(coord);
                }));
                return;
            }
        }
        RemoveFog(coord);
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
        Vector3 spawnPos = new Vector3(center.x, MistWorldHeight, center.z);

        var fogObj = Instantiate(fogPrefab, spawnPos, Quaternion.identity, transform);
        fogObj.name = $"Fog_{coord.x}_{coord.y}";
        fogObj.transform.position = spawnPos;
        float xzScale = hexSize > 0f ? (hexSize + padding) / hexSize : 1f;
        fogObj.transform.localScale = new Vector3(xzScale, fogHeightScale, xzScale);

        
        activeFogChunks[coord] = fogObj;
    }

    // Wspoldzielone tablice materialow (brak alokacji na kazdy renderer).
    private Material[] singleMatBuffer;
    private Material[] dualMatBuffer;
    private static readonly List<Renderer> rendererBuffer = new List<Renderer>(64);

    /// <summary>
    /// Ustawia material mgly na wszystkich rendererach chunka. Opcjonalny depthPrepassMat jest
    /// dokladany jako drugi material (mesh ma jeden submesh, wiec Unity renderuje go drugi raz
    /// tym materialem). Uzywane tylko dla Surveying - pozostale stany maja jeden material.
    /// </summary>
    private void ApplyMaterial(GameObject root, Material mat, Material depthPrepassMat = null)
    {
        if (root == null || mat == null) return;

        Material[] mats;
        if (depthPrepassMat != null)
        {
            if (dualMatBuffer == null) dualMatBuffer = new Material[2];
            dualMatBuffer[0] = mat;
            dualMatBuffer[1] = depthPrepassMat;
            mats = dualMatBuffer;
        }
        else
        {
            if (singleMatBuffer == null) singleMatBuffer = new Material[1];
            singleMatBuffer[0] = mat;
            mats = singleMatBuffer;
        }

        root.GetComponentsInChildren(rendererBuffer);
        foreach (var r in rendererBuffer)
        {
            r.sharedMaterials = mats; // Unity kopiuje tablice
            r.SetPropertyBlock(null);
        }
        rendererBuffer.Clear();
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
        isMistHeightCached = false;
        foreach (var coroutine in activeFadeCoroutines.Values)
        {
            if (coroutine != null) StopCoroutine(coroutine);
        }
        activeFadeCoroutines.Clear();
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
