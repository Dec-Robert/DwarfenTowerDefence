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

        if (surveyTransitionDuration <= 0f) surveyTransitionDuration = 1.2f;
        if (surveyPulseMultiplier <= 1f) surveyPulseMultiplier = 2.2f;
        if (outskirtsTransitionDuration <= 0f) outskirtsTransitionDuration = 1.5f;
        if (outskirtsSinkDistance <= 0f) outskirtsSinkDistance = 0.8f;
        if (boundaryTransitionWidth <= 0.01f) boundaryTransitionWidth = 0.16f;
        if (maskResolution < 256) maskResolution = 512;
        if (transitionEaseCurve == null || transitionEaseCurve.length == 0)
        {
            transitionEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        if (mistHeightOffset <= 0f && fogHeightOffset > 0f)
        {
            mistHeightOffset = fogHeightOffset;
        }
        else if (mistHeightOffset <= 0f)
        {
            mistHeightOffset = 0.1f;
        }
    }

    private void OnValidate()
    {
        EnsureMaterials();
        if (surveyTransitionDuration <= 0f) surveyTransitionDuration = 1.2f;
        if (surveyPulseMultiplier <= 1f) surveyPulseMultiplier = 2.2f;
        if (outskirtsTransitionDuration <= 0f) outskirtsTransitionDuration = 1.5f;
        if (outskirtsSinkDistance <= 0f) outskirtsSinkDistance = 0.8f;
        boundaryTransitionWidth = Mathf.Clamp(boundaryTransitionWidth, 0.05f, 0.45f);
        boundaryWarpAmplitude = Mathf.Clamp(boundaryWarpAmplitude, 0f, 1.5f);
        boundaryDetailStrength = Mathf.Clamp(boundaryDetailStrength, 0f, 1.5f);
        boundaryAnimationSpeed = Mathf.Clamp(boundaryAnimationSpeed, 0f, 3f);
        maskResolution = Mathf.Clamp(maskResolution, 128, 1024);
        if (transitionEaseCurve == null || transitionEaseCurve.length == 0)
        {
            transitionEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        SyncBoundaryParametersToMaterials();
        if (Application.isPlaying && fogMaskTexture != null && expansionManager != null)
        {
            UpdateGlobalFogMask();
        }
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
        if (surveyingDepthMaterial != null)
        {
            surveyingDepthMaterial.renderQueue = 3105;
        }
        if (surveyingFogMaterial != null)
        {
            surveyingFogMaterial.renderQueue = 3106;
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

    [Header("Animacje Przejść Mgły (Transitions)")]
    [Tooltip("Czas trwania animacji rozpoczęcia badania chunka (Borderlands -> Surveying) w sekundach.")]
    public float surveyTransitionDuration = 1.2f;
    [Tooltip("Mnożnik rozbłysku emisji (rozjaśnienia) mgły w szczytowym momencie animacji badania.")]
    public float surveyPulseMultiplier = 2.2f;
    [Tooltip("Czas trwania animacji opadania mgły po zbadaniu chunka (Surveying -> Outskirts) w sekundach.")]
    public float outskirtsTransitionDuration = 1.5f;
    [Tooltip("Dystans w dół (w jednostkach świata), o jaki opada tafla mgły badawczej podczas przejścia w Outskirts.")]
    public float outskirtsSinkDistance = 0.8f;
    [Tooltip("Krzywa przejścia interpolacji animacji mgły.")]
    public AnimationCurve transitionEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private static readonly int PropGroundMistFade = Shader.PropertyToID("_GroundMistFade");
    private static readonly int PropMistAlpha = Shader.PropertyToID("_MistAlpha");
    private static readonly int PropDiscoverableEmission = Shader.PropertyToID("_DiscoverableEmission");
    private static readonly int PropLogicalWilderness = Shader.PropertyToID("_LogicalWilderness");
    private static readonly int PropBoundaryWarpAmplitude = Shader.PropertyToID("_BoundaryWarpAmplitude");
    private static readonly int PropBoundaryDetailStrength = Shader.PropertyToID("_BoundaryDetailStrength");
    private static readonly int PropBoundaryAnimationSpeed = Shader.PropertyToID("_BoundaryAnimationSpeed");
    private static readonly int PropFogTransitionWidthWorld = Shader.PropertyToID("_FogTransitionWidthWorld");
    private static readonly WaitForFixedUpdate WaitForFixed = new WaitForFixedUpdate();

    private MaterialPropertyBlock groundMistPropertyBlock;
    private MaterialPropertyBlock highMistPropertyBlock;
    private MaterialPropertyBlock logicalStatePropertyBlock;
    private readonly Dictionary<Vector2Int, Coroutine> activeFadeCoroutines = new Dictionary<Vector2Int, Coroutine>();
    private readonly Dictionary<Vector2Int, Coroutine> activeTransitionCoroutines = new Dictionary<Vector2Int, Coroutine>();
    private readonly Dictionary<Vector2Int, GameObject> transitionalFogChunks = new Dictionary<Vector2Int, GameObject>();
    private readonly Dictionary<Vector2Int, ChunkState> chunkFogStates = new Dictionary<Vector2Int, ChunkState>();

    [Header("Wspólna Wysokość Mgły (Mist World Height)")]
    [Tooltip("Czy automatycznie obliczać wysokość mgły na podstawie docelowego poziomu wzniesienia terenu (wzgórze +3).")]
    public bool autoCalculateMistHeight = true;

    [Tooltip("Ręczna wysokość Y mgły w przestrzeni świata (używana gdy autoCalculateMistHeight = false).")]
    public float manualMistWorldHeight = 0.85f;

    [Tooltip("Dodatni offset ponad powierzchnię docelowego poziomu wzniesienia zapobiegający Z-fighting.")]
    public float mistHeightOffset = 0.1f;

    [Tooltip("Poziom wzniesienia terenu (np. 3 dla wzgórza +3), na wysokości którego spawnowana jest mgła.")]
    public int mistElevationLevel = 3;

    private float cachedMistWorldHeight = 0f;
    private bool isMistHeightCached = false;

    /// <summary>
    /// Jednolita wysokość Y w przestrzeni świata dla wszystkich poziomych powierzchni mgły eksploracyjnej.
    /// MistWorldHeight = mistElevationLevel * elevationHeightStep + mistHeightOffset.
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
            return mistElevationLevel * 0.25f;
        }
    }

    private float ElevationHeightStep
    {
        get
        {
            if (mapVisualizer != null && mapVisualizer.elevationHeightStep > 0f)
            {
                return mapVisualizer.elevationHeightStep;
            }
            if (mapGenerator != null && mapGenerator.visualizer != null && mapGenerator.visualizer.elevationHeightStep > 0f)
            {
                return mapGenerator.visualizer.elevationHeightStep;
            }
            return 0.25f;
        }
    }

    public float CalculateMistWorldHeight()
    {
        if (!autoCalculateMistHeight)
        {
            return manualMistWorldHeight;
        }

        float baseY = mistElevationLevel * ElevationHeightStep;
        float offset = mistHeightOffset > 0f ? mistHeightOffset : (fogHeightOffset > 0f ? fogHeightOffset : 0.1f);
        return baseY + offset;
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
    [Tooltip("Minimalna rozdzielczość generowanej tekstury maski mgły (automatycznie skalowana do rozmiaru świata).")]
    public int maskResolution = 512;
    [Tooltip("Docelowa liczba pikseli maski na pojedynczy heks dla precyzyjnego odwzorowania granic chunków.")]
    [Min(2f)] public float maskPixelsPerHex = 4.5f;
    [Tooltip("Margines wokół granic mapy (w jednostkach promienia chunka).")]
    public float worldMarginMultiplier = 3.0f;

    [Header("Granica Mgły (Frontier Boundary)")]
    [Tooltip("Szerokość pasa przejściowego wewnątrz Borderlands jako ułamek szerokości chunka (np. 0.16 = 16%).")]
    [Range(0.05f, 0.45f)] public float boundaryTransitionWidth = 0.16f;
    [Tooltip("Amplituda deformacji (Domain Warping) proporcjonalna do szerokości pasa przejściowego.")]
    [Range(0f, 1.5f)] public float boundaryWarpAmplitude = 0.65f;
    [Tooltip("Siła poszarpania i detalu FBM na granicy Frontier.")]
    [Range(0f, 1.5f)] public float boundaryDetailStrength = 0.55f;
    [Tooltip("Prędkość animacji granicy po stronie GPU.")]
    [Range(0f, 3f)] public float boundaryAnimationSpeed = 1.0f;

    private struct FrontierSegment
    {
        public Vector2 p0;
        public Vector2 p1;
        public float minX;
        public float maxX;
        public float minZ;
        public float maxZ;
    }

    private readonly List<FrontierSegment> frontierSegments = new List<FrontierSegment>(128);
    private static readonly Vector2Int[] AxialNeighborDirs =
    {
        new Vector2Int(1, 0),
        new Vector2Int(1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(-1, 1),
        new Vector2Int(0, 1)
    };

    [Header("Efekty Mgły na Przeciwnikach")]
    [Tooltip("Mnożnik prędkości przeciwników poruszających się po chunku w trakcie eksploracji (Surveying). 1.5 = +50%.")]
    public float surveyingEnemySpeedMultiplier = 1.5f;

    public event Action<Vector2Int> OnChunkRevealed;
    public event Action<Vector2Int> OnChunkSurveyingStarted;
    public event Action<Vector2Int> OnBorderlandsClicked;

    private readonly Dictionary<Vector2Int, GameObject> activeFogChunks = new Dictionary<Vector2Int, GameObject>();
    private readonly HashSet<Vector2Int> activeSurveyingChunks = new HashSet<Vector2Int>();
    private int chunkRadius;
    private float hexSize;
    private float padding;

    // Pola dla globalnej maski mgły w shaderze
    private Texture2D fogMaskTexture;
    public Texture2D FogMaskTexture => fogMaskTexture;
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

        SyncBoundaryParametersToMaterials();

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

        StopAllTransitionCoroutines();
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
        SyncBoundaryParametersToMaterials();

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
    // LOGIKA GLOBALNEJ MASKI MGŁY (FRONTIER DISTANCE + STAN LOGICZNY)
    // =========================================================================

    private float GetChunkWidthWorld()
    {
        EnsureGridMetrics();
        int r = chunkRadius > 0 ? chunkRadius : 3;
        float totalHex = Mathf.Max(0.1f, hexSize + padding);
        return Mathf.Sqrt(3f) * (2 * r + 1) * totalHex;
    }

    private float GetTransitionWidthWorld()
    {
        return Mathf.Max(0.25f, GetChunkWidthWorld() * Mathf.Clamp(boundaryTransitionWidth, 0.05f, 0.45f));
    }

    private void SyncBoundaryParametersToMaterials()
    {
        float transitionWorld = GetTransitionWidthWorld();
        Shader.SetGlobalFloat(PropFogTransitionWidthWorld, transitionWorld);

        ApplyBoundaryMaterialParams(defaultFogMaterial);
        if (borderlandsFogMaterial != null && borderlandsFogMaterial != defaultFogMaterial)
        {
            ApplyBoundaryMaterialParams(borderlandsFogMaterial);
        }
        ApplyBoundaryMaterialParams(surveyingFogMaterial);
    }

    private void ApplyBoundaryMaterialParams(Material mat)
    {
        if (mat == null) return;
        if (mat.HasProperty(PropBoundaryWarpAmplitude)) mat.SetFloat(PropBoundaryWarpAmplitude, boundaryWarpAmplitude);
        if (mat.HasProperty(PropBoundaryDetailStrength)) mat.SetFloat(PropBoundaryDetailStrength, boundaryDetailStrength);
        if (mat.HasProperty(PropBoundaryAnimationSpeed)) mat.SetFloat(PropBoundaryAnimationSpeed, boundaryAnimationSpeed);
    }

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

    private int ComputeTargetMaskResolution()
    {
        EnsureGridMetrics();
        float totalHex = Mathf.Max(0.1f, hexSize + padding);
        float maxSpan = Mathf.Max(worldBoundsSize.x, worldBoundsSize.y);
        int worldDriven = maxSpan > 1f
            ? Mathf.CeilToInt((maxSpan / totalHex) * Mathf.Max(2f, maskPixelsPerHex))
            : 512;
        int minAllowed = Mathf.Max(256, maskResolution);
        int res = Mathf.Clamp(Mathf.Max(minAllowed, worldDriven), 256, 1024);
        // Zaokraglenie do wielokrotnosci 64 dla spojnosci alokacji tekstury
        return ((res + 63) / 64) * 64;
    }

    private void InitMaskTexture()
    {
        int res = ComputeTargetMaskResolution();

        if (fogMaskTexture != null && (fogMaskTexture.width != res || fogMaskTexture.height != res))
        {
            DestroyMaskTexture();
        }

        if (fogMaskTexture == null)
        {
            // R = dystans od Frontier w Borderlands (1 na Frontier/Wilderness -> 0 w glebi Borderlands)
            // G = logiczny stan (255 = Wilderness, 0 = pozostale)
            // B = cienka linia krawedzi Frontier (do debugowania)
            fogMaskTexture = new Texture2D(res, res, TextureFormat.RGBA32, false, true);
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

    private static float DistancePointToSegmentSq(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq <= 1e-6f) return (p - a).sqrMagnitude;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
        Vector2 proj = a + ab * t;
        return (p - proj).sqrMagnitude;
    }

    private static bool IsDiscoverableFogState(ChunkState state)
    {
        return state == ChunkState.Borderlands || state == ChunkState.Surveying;
    }

    private void BuildFrontierSegments(int rChunk, float totalHexSize, float transitionWidthWorld)
    {
        frontierSegments.Clear();
        if (expansionManager == null) return;

        float halfEdge = 0.5f * totalHexSize;

        foreach (var kvp in expansionManager.activeChunks)
        {
            if (kvp.Value == null || !IsDiscoverableFogState(kvp.Value.EffectiveState)) continue;

            Vector2Int chunkCoord = kvp.Key;
            for (int q = -rChunk; q <= rChunk; q++)
            {
                int r1 = Mathf.Max(-rChunk, -q - rChunk);
                int r2 = Mathf.Min(rChunk, -q + rChunk);
                for (int r = r1; r <= r2; r++)
                {
                    // Sprawdzamy wylacznie zewnetrzny obwod heksow chunka
                    int hexDist = Mathf.Max(Mathf.Abs(q), Mathf.Max(Mathf.Abs(r), Mathf.Abs(-q - r)));
                    if (hexDist < rChunk) continue;

                    Vector2Int globalHex = HexGridMath.LocalToGlobal(chunkCoord, new Vector2Int(q, r), rChunk);
                    Vector3 centerA3 = HexGridMath.AxialToWorld(globalHex.x, globalHex.y, hexSize, padding);
                    Vector2 cA = new Vector2(centerA3.x, centerA3.z);

                    for (int k = 0; k < 6; k++)
                    {
                        Vector2Int nGlobal = globalHex + AxialNeighborDirs[k];
                        HexGridMath.GlobalToChunkAndLocal(nGlobal, rChunk, out var nChunk, out _);
                        if (nChunk == chunkCoord) continue;

                        // Frontier istnieje WYLACZNIE miedzy Borderlands/Surveying a istniejacym chunkiem Wilderness.
                        // Nie generujemy granicy z Borderlands, Settled ani Outskirts.
                        if (!expansionManager.activeChunks.TryGetValue(nChunk, out var nData) || nData == null) continue;
                        if (nData.EffectiveState != ChunkState.Wilderness) continue;

                        Vector3 centerB3 = HexGridMath.AxialToWorld(nGlobal.x, nGlobal.y, hexSize, padding);
                        Vector2 cB = new Vector2(centerB3.x, centerB3.z);
                        Vector2 mid = 0.5f * (cA + cB);
                        Vector2 dir = (cB - cA).normalized;
                        Vector2 tan = new Vector2(-dir.y, dir.x);

                        Vector2 p0 = mid - tan * halfEdge;
                        Vector2 p1 = mid + tan * halfEdge;

                        frontierSegments.Add(new FrontierSegment
                        {
                            p0 = p0,
                            p1 = p1,
                            minX = Mathf.Min(p0.x, p1.x) - transitionWidthWorld,
                            maxX = Mathf.Max(p0.x, p1.x) + transitionWidthWorld,
                            minZ = Mathf.Min(p0.y, p1.y) - transitionWidthWorld,
                            maxZ = Mathf.Max(p0.y, p1.y) + transitionWidthWorld
                        });
                    }
                }
            }
        }
    }

    public void UpdateGlobalFogMask()
    {
        if (fogMaskTexture == null || expansionManager == null || maskPixels == null || worldBoundsSize.x <= 0f) return;

        EnsureGridMetrics();
        int res = fogMaskTexture.width;
        int rChunk = chunkRadius > 0 ? chunkRadius : 3;
        float totalHexSize = Mathf.Max(0.1f, hexSize + padding);
        float transitionWidthWorld = GetTransitionWidthWorld();
        Shader.SetGlobalFloat(PropFogTransitionWidthWorld, transitionWidthWorld);

        // 1. Wypelnienie rzeczywistego stanu logicznego dla kazdego piksela maski na bazie heksow
        float invRes = 1f / res;
        for (int py = 0; py < res; py++)
        {
            float v = (py + 0.5f) * invRes;
            float worldZ = worldBoundsMin.y + v * worldBoundsSize.y;
            int rowOffset = py * res;

            for (int px = 0; px < res; px++)
            {
                float u = (px + 0.5f) * invRes;
                float worldX = worldBoundsMin.x + u * worldBoundsSize.x;

                Vector2Int globalHex = HexGridMath.WorldToAxial(worldX, worldZ, hexSize, padding);
                HexGridMath.GlobalToChunkAndLocal(globalHex, rChunk, out var cCoord, out _);

                bool isWild = true;
                if (expansionManager.activeChunks.TryGetValue(cCoord, out var chunkData) && chunkData != null)
                {
                    isWild = chunkData.EffectiveState == ChunkState.Wilderness;
                }

                byte val = isWild ? (byte)255 : (byte)0;
                maskPixels[rowOffset + px] = new Color32(val, val, 0, 255);
            }
        }

        // 2. Wyznaczenie rzeczywistych odcinkow Frontier (Borderlands/Surveying <-> Wilderness)
        BuildFrontierSegments(rChunk, totalHexSize, transitionWidthWorld);

        // 3. Wypalenie maski odleglosci od Frontier wylacznie wewnatrz Borderlands/Surveying
        if (frontierSegments.Count > 0)
        {
            float transitionSq = transitionWidthWorld * transitionWidthWorld;
            float frontierDebugThickness = Mathf.Max(0.22f, totalHexSize * 0.25f);
            float frontierDebugSq = frontierDebugThickness * frontierDebugThickness;

            for (int s = 0; s < frontierSegments.Count; s++)
            {
                FrontierSegment seg = frontierSegments[s];
                int minPx = Mathf.Clamp(Mathf.FloorToInt(((seg.minX - worldBoundsMin.x) / worldBoundsSize.x) * res), 0, res - 1);
                int maxPx = Mathf.Clamp(Mathf.CeilToInt(((seg.maxX - worldBoundsMin.x) / worldBoundsSize.x) * res), 0, res - 1);
                int minPy = Mathf.Clamp(Mathf.FloorToInt(((seg.minZ - worldBoundsMin.y) / worldBoundsSize.y) * res), 0, res - 1);
                int maxPy = Mathf.Clamp(Mathf.CeilToInt(((seg.maxZ - worldBoundsMin.y) / worldBoundsSize.y) * res), 0, res - 1);

                for (int py = minPy; py <= maxPy; py++)
                {
                    float worldZ = worldBoundsMin.y + (py + 0.5f) * invRes * worldBoundsSize.y;
                    int rowOffset = py * res;

                    for (int px = minPx; px <= maxPx; px++)
                    {
                        int idx = rowOffset + px;
                        Color32 cur = maskPixels[idx];

                        float worldX = worldBoundsMin.x + (px + 0.5f) * invRes * worldBoundsSize.x;
                        float distSq = DistancePointToSegmentSq(new Vector2(worldX, worldZ), seg.p0, seg.p1);

                        if (distSq <= frontierDebugSq && cur.b == 0)
                        {
                            cur.b = 255;
                        }

                        // Maska odleglosci w pasie przejsciowym obliczana tylko wewnatrz Borderlands (G == 0)
                        if (cur.g == 0 && distSq <= transitionSq)
                        {
                            float d = Mathf.Sqrt(distSq);
                            byte distMask = (byte)Mathf.RoundToInt((1f - Mathf.Clamp01(d / transitionWidthWorld)) * 255f);
                            if (distMask > cur.r)
                            {
                                cur.r = distMask;
                            }
                        }

                        maskPixels[idx] = cur;
                    }
                }
            }
        }

        fogMaskTexture.SetPixels32(maskPixels);
        fogMaskTexture.Apply();
    }

    public void UpdateChunkFogState(Vector2Int coord, ChunkState state)
    {
        UpdateChunkFogState(coord, state, true);
    }

    public void UpdateChunkFogState(Vector2Int coord, ChunkState state, bool animate)
    {
        bool hadPrev = chunkFogStates.TryGetValue(coord, out ChunkState prevState);
        if (hadPrev && prevState == state)
        {
            // Stan tego chunka w ogóle się nie zmienił - ignorujemy powtórne wywołanie zdarzenia
            // (np. natychmiastowe HandleMissionChanged z OnMissionStarted/OnMissionCompleted),
            // aby nie przerywać trwającej właśnie animacji przejścia!
            return;
        }

        chunkFogStates[coord] = state;
        if (state == ChunkState.Surveying)
        {
            activeSurveyingChunks.Add(coord);
        }
        else
        {
            activeSurveyingChunks.Remove(coord);
        }

        bool canAnimate = animate && Application.isPlaying && gameObject.activeInHierarchy && hadPrev;

        Material discMat = borderlandsFogMaterial != null ? borderlandsFogMaterial : defaultFogMaterial;

        switch (state)
        {
            case ChunkState.Wilderness:
                StopTransitionCoroutine(coord);
                SetFogFull(coord, defaultFogMaterial, null, 1f);
                SetTerrainVisible(coord, false);
                break;

            case ChunkState.Borderlands:
                StopTransitionCoroutine(coord);
                SetFogFull(coord, discMat, null, 0f);
                SetTerrainVisible(coord, false);
                break;

            case ChunkState.Surveying:
                EnsureMaterials();
                Material surveyMat = (surveyingFogMaterial != null && !surveyingFogMaterial.name.Contains("FogOfWar3"))
                    ? surveyingFogMaterial
                    : discMat;

                if (canAnimate && (prevState == ChunkState.Borderlands || prevState == ChunkState.Wilderness) && surveyTransitionDuration > 0f)
                {
                    StartSurveyingTransition(coord, surveyMat, surveyingDepthMaterial);
                }
                else
                {
                    StopTransitionCoroutine(coord);
                    SetFogFull(coord, surveyMat, surveyingDepthMaterial, 0f);
                    SetTerrainVisible(coord, true);
                }
                OnChunkSurveyingStarted?.Invoke(coord);
                break;

            case ChunkState.Outskirts:
                EnsureMaterials();
                Material outskirtMat = outskirtsFogMaterial != null ? outskirtsFogMaterial : defaultFogMaterial;

                if (canAnimate && (prevState == ChunkState.Surveying || prevState == ChunkState.Borderlands || prevState == ChunkState.Wilderness) && outskirtsTransitionDuration > 0f)
                {
                    StartOutskirtsTransition(coord, outskirtMat);
                }
                else
                {
                    StopTransitionCoroutine(coord);
                    SetFogLow(coord, outskirtMat, true);
                    SetTerrainVisible(coord, true);
                    OnChunkRevealed?.Invoke(coord);
                }
                break;

            case ChunkState.Settled:
                StopTransitionCoroutine(coord);
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
            UpdateChunkFogState(kvp.Key, kvp.Value.EffectiveState, false);
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

    /// <summary>
    /// Zwraca true, jeśli chunk jest otwarty dla wyznaczania punktu spawnu
    /// (faktyczna eksploracja Surveying, Pogranicze Outskirts lub Osada Settled).
    /// Zwiad taktyczny (Recon) nie zmienia stanu chunka i pozostaje zamknięty.
    /// </summary>
    public bool IsChunkOpenForSpawn(Vector2Int coord)
    {
        if (expansionManager != null)
        {
            var state = expansionManager.GetEffectiveState(coord);
            return state == ChunkState.Surveying || state == ChunkState.Outskirts || state == ChunkState.Settled;
        }
        if (chunkFogStates.TryGetValue(coord, out var fogState))
        {
            return fogState == ChunkState.Surveying || fogState == ChunkState.Outskirts || fogState == ChunkState.Settled;
        }
        return !activeFogChunks.ContainsKey(coord);
    }

    /// <summary>
    /// Zwraca true, jeśli dany chunk znajduje się obecnie w stanie aktywnej eksploracji (Surveying).
    /// </summary>
    public bool IsChunkSurveying(Vector2Int coord)
    {
        if (activeSurveyingChunks.Contains(coord))
        {
            return true;
        }
        if (expansionManager != null)
        {
            return expansionManager.GetEffectiveState(coord) == ChunkState.Surveying;
        }
        return chunkFogStates.TryGetValue(coord, out var fogState) && fogState == ChunkState.Surveying;
    }

    /// <summary>
    /// Szybkie (O(1)) sprawdzenie, czy podana pozycja świata leży na chunku w stanie Surveying.
    /// </summary>
    public bool IsWorldPositionInSurveyingFog(Vector3 worldPos)
    {
        if (activeSurveyingChunks.Count == 0)
        {
            return false;
        }

        EnsureGridMetrics();
        if (hexSize <= 0f)
        {
            return false;
        }

        Vector2Int globalHex = HexGridMath.WorldToAxial(worldPos.x, worldPos.z, hexSize, padding);
        HexGridMath.GlobalToChunkAndLocal(globalHex, chunkRadius, out Vector2Int chunkCoord, out _);
        return activeSurveyingChunks.Contains(chunkCoord);
    }

    private void EnsureGridMetrics()
    {
        if (hexSize > 0f && chunkRadius > 0) return;

        if (mapGenerator == null)
        {
            mapGenerator = HexMapGenerator.Instance ?? FindFirstObjectByType<HexMapGenerator>();
        }

        if (mapGenerator != null)
        {
            if (chunkRadius <= 0) chunkRadius = mapGenerator.chunkRadius;
            if (hexSize <= 0f) hexSize = mapGenerator.hexSize;
            if (padding <= 0f) padding = mapGenerator.padding;
        }

        if (chunkRadius <= 0) chunkRadius = 3;
        if (hexSize <= 0f) hexSize = 1f;
    }

    private void SetFogFull(Vector2Int coord, Material mat, Material depthPrepassMat = null, float logicalWilderness = 0f)
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
        SetChunkLogicalWilderness(fog, logicalWilderness);
    }

    private void SetChunkLogicalWilderness(GameObject fog, float logicalWilderness)
    {
        if (fog == null) return;
        if (logicalStatePropertyBlock == null) logicalStatePropertyBlock = new MaterialPropertyBlock();
        logicalStatePropertyBlock.SetFloat(PropLogicalWilderness, logicalWilderness);

        rendererBuffer.Clear();
        fog.GetComponentsInChildren(rendererBuffer);
        for (int i = 0; i < rendererBuffer.Count; i++)
        {
            rendererBuffer[i].SetPropertyBlock(logicalStatePropertyBlock);
        }
        rendererBuffer.Clear();
    }

    private void DisableAndDestroyColliders(GameObject fog)
    {
        if (fog == null) return;
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
    }

    private void SetChunkHighMistProperties(GameObject fog, float alpha, float emission)
    {
        if (fog == null) return;
        if (highMistPropertyBlock == null) highMistPropertyBlock = new MaterialPropertyBlock();
        highMistPropertyBlock.SetFloat(PropMistAlpha, Mathf.Clamp01(alpha));
        highMistPropertyBlock.SetFloat(PropDiscoverableEmission, Mathf.Max(0f, emission));
        highMistPropertyBlock.SetFloat(PropLogicalWilderness, 0f);

        rendererBuffer.Clear();
        fog.GetComponentsInChildren(rendererBuffer);
        for (int i = 0; i < rendererBuffer.Count; i++)
        {
            rendererBuffer[i].SetPropertyBlock(highMistPropertyBlock);
        }
        rendererBuffer.Clear();
    }

    private void ClearPropertyBlock(GameObject fog)
    {
        SetChunkLogicalWilderness(fog, 0f);
    }

    private void StopTransitionCoroutine(Vector2Int coord)
    {
        if (activeTransitionCoroutines.TryGetValue(coord, out var coroutine))
        {
            if (coroutine != null) StopCoroutine(coroutine);
            activeTransitionCoroutines.Remove(coord);
        }

        if (transitionalFogChunks.TryGetValue(coord, out var transObj))
        {
            if (transObj != null)
            {
                if (Application.isPlaying) Destroy(transObj);
                else DestroyImmediate(transObj);
            }
            transitionalFogChunks.Remove(coord);
        }
    }

    private void StopAllTransitionCoroutines()
    {
        foreach (var coroutine in activeTransitionCoroutines.Values)
        {
            if (coroutine != null) StopCoroutine(coroutine);
        }
        activeTransitionCoroutines.Clear();

        foreach (var transObj in transitionalFogChunks.Values)
        {
            if (transObj != null)
            {
                if (Application.isPlaying) Destroy(transObj);
                else DestroyImmediate(transObj);
            }
        }
        transitionalFogChunks.Clear();
    }

    private void StartSurveyingTransition(Vector2Int coord, Material surveyMat, Material depthPrepassMat)
    {
        StopTransitionCoroutine(coord);
        StopFadeCoroutine(coord);

        if (!activeFogChunks.ContainsKey(coord) || activeFogChunks[coord] == null)
        {
            SpawnFogObject(coord);
        }

        var fog = activeFogChunks[coord];
        if (fog == null) return;

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

        ApplyMaterial(fog, surveyMat, depthPrepassMat);
        SetTerrainVisible(coord, true);

        activeTransitionCoroutines[coord] = StartCoroutine(BorderlandsToSurveyingRoutine(coord, fog, surveyMat, surveyTransitionDuration));
    }

    private IEnumerator BorderlandsToSurveyingRoutine(Vector2Int coord, GameObject fog, Material surveyMat, float duration)
    {
        float targetAlpha = (surveyMat != null && surveyMat.HasProperty(PropMistAlpha))
            ? surveyMat.GetFloat(PropMistAlpha)
            : 0.5f;

        float baseEmission = (surveyMat != null && surveyMat.HasProperty(PropDiscoverableEmission))
            ? surveyMat.GetFloat(PropDiscoverableEmission)
            : 0.4f;

        float startAlpha = 1.0f;
        float elapsed = 0f;

        SetChunkHighMistProperties(fog, startAlpha, baseEmission);

        while (elapsed < duration)
        {
            if (fog == null) break;

            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curveT = transitionEaseCurve != null ? transitionEaseCurve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);

            float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, curveT);
            float pulse = Mathf.Sin(t * Mathf.PI);
            float currentEmission = baseEmission * (1f + (surveyPulseMultiplier - 1f) * pulse);

            SetChunkHighMistProperties(fog, currentAlpha, currentEmission);

            yield return WaitForFixed;
        }

        if (fog != null)
        {
            ClearPropertyBlock(fog);
        }

        activeTransitionCoroutines.Remove(coord);
    }

    private void StartOutskirtsTransition(Vector2Int coord, Material outskirtMat)
    {
        StopTransitionCoroutine(coord);
        StopFadeCoroutine(coord);

        GameObject oldFog = null;
        if (activeFogChunks.TryGetValue(coord, out var existingFog) && existingFog != null)
        {
            oldFog = existingFog;
            activeFogChunks.Remove(coord);
        }

        GameObject newFog = SetFogLow(coord, outskirtMat, startGroundMistFade: false);
        SetTerrainVisible(coord, true);
        OnChunkRevealed?.Invoke(coord);

        if (oldFog == null)
        {
            if (newFog != null)
            {
                if (Application.isPlaying && gameObject.activeInHierarchy && groundMistFadeDuration > 0f)
                {
                    activeFadeCoroutines[coord] = StartCoroutine(FadeGroundMistRoutine(newFog, 0f, 1f, groundMistFadeDuration, coord));
                }
                else
                {
                    SetChunkGroundMistFade(newFog, 1f);
                }
            }
            return;
        }

        DisableAndDestroyColliders(oldFog);

        var rend = oldFog.GetComponentInChildren<Renderer>();
        Material surveyMat = rend != null ? rend.sharedMaterial : surveyingFogMaterial;
        ApplyMaterial(oldFog, surveyMat, null);

        oldFog.name = $"Fog_{coord.x}_{coord.y}_Sinking";
        transitionalFogChunks[coord] = oldFog;

        activeTransitionCoroutines[coord] = StartCoroutine(SurveyingToOutskirtsRoutine(coord, oldFog, newFog, surveyMat, outskirtsTransitionDuration));
    }

    private IEnumerator SurveyingToOutskirtsRoutine(Vector2Int coord, GameObject oldFog, GameObject newFog, Material surveyMat, float duration)
    {
        float startAlpha = (surveyMat != null && surveyMat.HasProperty(PropMistAlpha))
            ? surveyMat.GetFloat(PropMistAlpha)
            : 0.5f;

        Vector3 startPos = oldFog != null ? oldFog.transform.position : Vector3.zero;
        Vector3 targetPos = startPos - new Vector3(0f, outskirtsSinkDistance, 0f);

        float elapsed = 0f;

        if (oldFog != null) SetChunkHighMistProperties(oldFog, startAlpha, 0.4f);
        if (newFog != null) SetChunkGroundMistFade(newFog, 0f);

        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curveT = transitionEaseCurve != null ? transitionEaseCurve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);

            if (oldFog != null)
            {
                oldFog.transform.position = Vector3.Lerp(startPos, targetPos, curveT);
                float curAlpha = Mathf.Lerp(startAlpha, 0f, curveT);
                SetChunkHighMistProperties(oldFog, curAlpha, 0.4f);
            }

            if (newFog != null)
            {
                float curGroundFade = Mathf.Lerp(0f, 1f, curveT);
                SetChunkGroundMistFade(newFog, curGroundFade);
            }

            yield return WaitForFixed;
        }

        if (oldFog != null)
        {
            Destroy(oldFog);
        }
        transitionalFogChunks.Remove(coord);

        if (newFog != null)
        {
            SetChunkGroundMistFade(newFog, 1f);
        }

        activeTransitionCoroutines.Remove(coord);
    }

    private GameObject SetFogLow(Vector2Int coord, Material mat, bool startGroundMistFade = true)
    {
        StopFadeCoroutine(coord);

        if (!activeFogChunks.ContainsKey(coord) || activeFogChunks[coord] == null)
        {
            SpawnFogObject(coord);
        }

        var fog = activeFogChunks[coord];
        if (fog == null) return null;

        // Usuwamy collidery z mgly na Outskirts, aby gracz mogl swobodnie klikac teren, wybierac heksy i budowac
        DisableAndDestroyColliders(fog);

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

        if (startGroundMistFade)
        {
            if (Application.isPlaying && gameObject.activeInHierarchy && groundMistFadeDuration > 0f)
            {
                activeFadeCoroutines[coord] = StartCoroutine(FadeGroundMistRoutine(fog, 0f, 1f, groundMistFadeDuration, coord));
            }
            else
            {
                SetChunkGroundMistFade(fog, 1f);
            }
        }
        else
        {
            SetChunkGroundMistFade(fog, 0f);
        }

        return fog;
    }

    private void SetChunkGroundMistFade(GameObject fog, float fade)
    {
        if (fog == null) return;
        if (groundMistPropertyBlock == null) groundMistPropertyBlock = new MaterialPropertyBlock();
        float clamped = Mathf.Clamp01(fade);
        groundMistPropertyBlock.SetFloat(PropGroundMistFade, clamped);

        float targetAlpha = (outskirtsFogMaterial != null && outskirtsFogMaterial.HasProperty(PropMistAlpha))
            ? outskirtsFogMaterial.GetFloat(PropMistAlpha)
            : 0.2f;
        groundMistPropertyBlock.SetFloat(PropMistAlpha, clamped * targetAlpha);
        groundMistPropertyBlock.SetFloat(PropLogicalWilderness, 0f);

        rendererBuffer.Clear();
        fog.GetComponentsInChildren(rendererBuffer);
        for (int i = 0; i < rendererBuffer.Count; i++)
        {
            rendererBuffer[i].SetPropertyBlock(groundMistPropertyBlock);
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
            elapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float currentFade = Mathf.Lerp(startFade, targetFade, t);
            SetChunkGroundMistFade(fog, currentFade);
            yield return WaitForFixed;
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
                if (rend != null && rend.sharedMaterial != null &&
                    (rend.sharedMaterial.HasProperty(PropGroundMistFade) || rend.sharedMaterial.HasProperty(PropMistAlpha)))
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
        StopAllTransitionCoroutines();
        chunkFogStates.Clear();
        activeSurveyingChunks.Clear();
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
