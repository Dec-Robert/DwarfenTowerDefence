using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Centralny kontroler atmosfery faz dnia (Świt / Zmierzch / Noc).
/// Integruje się bezpośrednio z TimePhaseManagerem oraz FogOfWarManagerem:
/// - płynnie interpoluje Directional Light, ambient Trilight, mgłę dystansową (RenderSettings),
///   parametry proceduralnej fioletowej mgły (MistHLSL / TmpMist), tło kamery oraz EnemyGlow;
/// - centralnie napędza wszystkie nocne światła budynków (SettlementLight) bez pętli Update() na każdym budynku.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    public const int CurrentPresetVersion = 2;

    public static DayNightCycle Instance { get; private set; }

    [System.Serializable]
    public struct PhaseLightingConfig
    {
        public string phaseName;

        [Header("Światło kierunkowe")]
        public Vector3 sunEulerAngles;
        public Color sunColor;
        [Min(0f)] public float sunIntensity;
        [Range(0f, 1f)] public float shadowStrength;

        [Header("Światło otoczenia (Trilight)")]
        public Color ambientSkyColor;
        public Color ambientEquatorColor;
        public Color ambientGroundColor;

        [Header("Mgła dystansowa i tło za mapą")]
        public Color fogColor;
        [Min(0f)] public float fogDensity;
        public Color cameraBackgroundColor;

        [Header("Fioletowa mgła proceduralna (Mist)")]
        [Range(0f, 3f)] public float mistEmissionMultiplier;
        [Range(0f, 3f)] public float mistBrightnessMultiplier;
        [Range(0f, 3f)] public float mistSparkMultiplier;
        [Range(0f, 1.5f)] public float outskirtsMistMultiplier;

        [Header("Dodatkowe światła")]
        [Range(0f, 1f)] public float enemyGlow;
        [Min(0f)] public float sanctuaryIntensity;
    }

    private struct MistMaterialBaseline
    {
        public Material material;
        public bool hasDiscoverableEmission;
        public float discoverableEmission;
        public bool hasBlockedEmission;
        public float blockedEmission;
        public bool hasDiscoverableBrightness;
        public float discoverableBrightness;
        public bool hasBlockedBrightness;
        public float blockedBrightness;
        public bool hasSparkIntensity;
        public float sparkIntensity;
        public bool hasBaseColor;
        public Color baseColor;
        public bool hasEmissionColor;
        public Color emissionColor;
        public bool hasEmissionStrength;
        public float emissionStrength;
    }

    private static readonly int PropDiscoverableEmission = Shader.PropertyToID("_DiscoverableEmission");
    private static readonly int PropBlockedEmission = Shader.PropertyToID("_BlockedEmission");
    private static readonly int PropDiscoverableBrightness = Shader.PropertyToID("_DiscoverableBrightness");
    private static readonly int PropBlockedBrightness = Shader.PropertyToID("_BlockedBrightness");
    private static readonly int PropSparkIntensity = Shader.PropertyToID("_SparkIntensity");
    private static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int PropEmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int PropEmissionStrength = Shader.PropertyToID("_EmissionStrength");

    [Header("Referencje")]
    [Tooltip("Główne Directional Light. Jeśli puste, skrypt spróbuje pobrać Light z tego obiektu.")]
    public Light sunLight;

    [Tooltip("Opcjonalny Point/Spot Light umieszczony w centrum osady. Nie przypisuj tu Directional Light.")]
    public Light sanctuaryLight;

    [Tooltip("Kamera główna (jeśli pusta, używa Camera.main).")]
    public Camera targetCamera;

    [Header("Tło kamery i barwa ognia")]
    [Tooltip("Zastępuje jasny domyślny skybox ciemnym tłem zsynchronizowanym z fazą.")]
    public bool syncCameraBackgroundColor = true;
    [Tooltip("Ciepła poświata osady; intensywność zależy od fazy.")]
    public Color sanctuaryLightColor = new Color(1.0f, 0.737f, 0.439f, 1f);

    [Header("Zmiana fazy")]
    [Min(0f)]
    public float transitionDuration = 2.5f;

    [Header("Poświata wrogów")]
    [Tooltip("Kolor emisji wrogów (HDR). Elity i bossowie zachowują własne mnożniki EnemyGlow.")]
    [ColorUsage(false, true)]
    public Color enemyGlowColor = new Color(0.40f, 0.13f, 0.72f, 1f);

    [Header("Optymalizacja Świateł Budynków")]
    [Tooltip("Maksymalna liczba jednocześnie aktywnych Point Lightów budynków w pobliżu kamery (0 = bez limitu).")]
    [Min(0)] public int maxActiveBuildingLights = 64;
    [Tooltip("Promień od punktu obserwacji kamery, w którym światła budynków działają w pełni (0 = bez limitu).")]
    [Min(0f)] public float buildingLightCullDistance = 90f;
    [Tooltip("Strefa płynnego wygaszania świateł budynków na granicy zasięgu.")]
    [Min(1f)] public float buildingLightFadeMargin = 15f;

    [HideInInspector]
    [SerializeField] private int appliedPresetVersion = 0;

    [Header("Konfiguracje faz")]
    public PhaseLightingConfig morningConfig = RecommendedMorning();
    public PhaseLightingConfig duskConfig = RecommendedDusk();
    public PhaseLightingConfig nightConfig = RecommendedNight();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    public static PhaseLightingConfig RecommendedMorning()
    {
        return new PhaseLightingConfig
        {
            phaseName = "Świt (Dawn) - Posępny poranek",
            sunEulerAngles = new Vector3(32f, -35f, 0f),
            sunColor = new Color(0.812f, 0.635f, 0.510f, 1f), // #CFA282
            sunIntensity = 0.68f,
            shadowStrength = 0.78f,

            ambientSkyColor = new Color(0.298f, 0.333f, 0.420f, 1f),     // #4C556B
            ambientEquatorColor = new Color(0.227f, 0.259f, 0.337f, 1f), // #3A4256
            ambientGroundColor = new Color(0.149f, 0.176f, 0.247f, 1f),  // #262D3F

            fogColor = new Color(0.208f, 0.157f, 0.278f, 1f),            // #352847
            fogDensity = 0.007f,
            cameraBackgroundColor = new Color(0.094f, 0.078f, 0.137f, 1f), // #181423

            mistEmissionMultiplier = 0.32f,
            mistBrightnessMultiplier = 0.48f,
            mistSparkMultiplier = 0.25f,
            outskirtsMistMultiplier = 0.45f,

            enemyGlow = 0f,
            sanctuaryIntensity = 0f
        };
    }

    public static PhaseLightingConfig RecommendedDusk()
    {
        return new PhaseLightingConfig
        {
            phaseName = "Zmierzch (Dusk) - Nadciąga mrok",
            sunEulerAngles = new Vector3(26f, -75f, 0f),
            sunColor = new Color(0.620f, 0.408f, 0.518f, 1f), // #9E6884
            sunIntensity = 0.46f,
            shadowStrength = 0.80f,

            ambientSkyColor = new Color(0.204f, 0.196f, 0.306f, 1f),     // #34324E
            ambientEquatorColor = new Color(0.157f, 0.149f, 0.243f, 1f), // #28263E
            ambientGroundColor = new Color(0.102f, 0.106f, 0.180f, 1f),  // #1A1B2E

            fogColor = new Color(0.220f, 0.106f, 0.322f, 1f),            // #381B52
            fogDensity = 0.010f,
            cameraBackgroundColor = new Color(0.071f, 0.051f, 0.118f, 1f), // #120D1E

            mistEmissionMultiplier = 0.58f,
            mistBrightnessMultiplier = 0.68f,
            mistSparkMultiplier = 0.55f,
            outskirtsMistMultiplier = 0.70f,

            enemyGlow = 0f,
            sanctuaryIntensity = 0f
        };
    }

    public static PhaseLightingConfig RecommendedNight()
    {
        return new PhaseLightingConfig
        {
            phaseName = "Noc (Night) - Wyspy światła",
            sunEulerAngles = new Vector3(52f, -35f, 0f),
            sunColor = new Color(0.290f, 0.384f, 0.557f, 1f), // #4A628E
            sunIntensity = 0.24f,
            shadowStrength = 0.45f,

            ambientSkyColor = new Color(0.125f, 0.180f, 0.290f, 1f),     // #202E4A
            ambientEquatorColor = new Color(0.090f, 0.129f, 0.220f, 1f), // #172138
            ambientGroundColor = new Color(0.059f, 0.078f, 0.149f, 1f),  // #0F1426

            fogColor = new Color(0.149f, 0.063f, 0.247f, 1f),            // #26103F
            fogDensity = 0.013f,
            cameraBackgroundColor = new Color(0.039f, 0.024f, 0.078f, 1f), // #0A0614

            mistEmissionMultiplier = 1.20f,
            mistBrightnessMultiplier = 1.05f,
            mistSparkMultiplier = 1.25f,
            outskirtsMistMultiplier = 1.00f,

            enemyGlow = 1f,
            sanctuaryIntensity = 1.65f
        };
    }

    private Coroutine transitionCoroutine;
    private PhaseLightingConfig currentConfig;
    private TimePhaseManager phaseManager;
    private readonly List<MistMaterialBaseline> mistBaselines = new List<MistMaterialBaseline>();
    private bool hasCapturedMistBaselines;

    private void Awake()
    {
        Instance = this;

        if (sunLight == null)
            sunLight = GetComponent<Light>();

        if (targetCamera == null)
            targetCamera = Camera.main;

        // Automatyczna jednorazowa migracja sceny z pamięci (np. przed przeładowaniem z dysku)
        if (appliedPresetVersion < CurrentPresetVersion)
        {
            morningConfig = RecommendedMorning();
            duskConfig = RecommendedDusk();
            nightConfig = RecommendedNight();
            appliedPresetVersion = CurrentPresetVersion;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
    }

    private void Start()
    {
        CaptureMistBaselines();

        phaseManager = TimePhaseManager.Instance;
        if (phaseManager != null)
        {
            phaseManager.OnMorningStarted += HandleMorningStarted;
            phaseManager.OnDuskStarted += HandleDuskStarted;
            phaseManager.OnNightStarted += HandleNightStarted;
            ApplyPhaseImmediate(phaseManager.CurrentTimePhase);
        }
        else
        {
            ApplyConfigImmediate(morningConfig);
        }
    }

    private void Update()
    {
        SettlementLight.TickAllLights(
            Time.deltaTime,
            maxActiveBuildingLights,
            buildingLightCullDistance,
            buildingLightFadeMargin);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        RestoreMistBaselines();

        if (phaseManager == null) return;
        phaseManager.OnMorningStarted -= HandleMorningStarted;
        phaseManager.OnDuskStarted -= HandleDuskStarted;
        phaseManager.OnNightStarted -= HandleNightStarted;
    }

    private void OnValidate()
    {
        transitionDuration = Mathf.Max(0f, transitionDuration);
        if (!Application.isPlaying || !isActiveAndEnabled || transitionCoroutine != null)
            return;

        if (TimePhaseManager.Instance != null)
            ApplyPhaseImmediate(TimePhaseManager.Instance.CurrentTimePhase);
    }

    [ContextMenu("Zastosuj presety Dark Fantasy")]
    public void ApplyRecommendedPresets()
    {
        morningConfig = RecommendedMorning();
        duskConfig = RecommendedDusk();
        nightConfig = RecommendedNight();
        appliedPresetVersion = CurrentPresetVersion;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        if (Application.isPlaying)
        {
            if (TimePhaseManager.Instance != null)
                ApplyPhaseImmediate(TimePhaseManager.Instance.CurrentTimePhase);
            else
                ApplyConfigImmediate(morningConfig);
        }
    }

    // Wsteczna kompatybilność ze starą nazwą menu kontekstowego.
    [ContextMenu("Zastosuj zalecane ustawienia faz")]
    public void ApplyRecommendedPresetsLegacy()
    {
        ApplyRecommendedPresets();
    }

    private void HandleMorningStarted() => TransitionToConfig(morningConfig, transitionDuration);
    private void HandleDuskStarted() => TransitionToConfig(duskConfig, transitionDuration);
    private void HandleNightStarted() => TransitionToConfig(nightConfig, transitionDuration);

    public void ApplyPhaseImmediate(TimePhases phase)
    {
        switch (phase)
        {
            case TimePhases.Morning:
                ApplyConfigImmediate(morningConfig);
                break;
            case TimePhases.Dusk:
                ApplyConfigImmediate(duskConfig);
                break;
            case TimePhases.Night:
                ApplyConfigImmediate(nightConfig);
                break;
        }
    }

    private void ApplyConfigImmediate(PhaseLightingConfig target)
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        currentConfig = target;
        ApplyConfigValues(target);
    }

    private void TransitionToConfig(PhaseLightingConfig target, float duration)
    {
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        if (!isActiveAndEnabled || !gameObject.activeInHierarchy || duration <= 0f)
        {
            ApplyConfigImmediate(target);
            return;
        }

        transitionCoroutine = StartCoroutine(TransitionRoutine(currentConfig, target, duration));
    }

    private IEnumerator TransitionRoutine(PhaseLightingConfig start, PhaseLightingConfig target, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            currentConfig = LerpConfig(start, target, t);
            ApplyConfigValues(currentConfig);
            yield return null;
        }

        currentConfig = target;
        ApplyConfigValues(target);
        transitionCoroutine = null;
    }

    private static PhaseLightingConfig LerpConfig(PhaseLightingConfig from, PhaseLightingConfig to, float t)
    {
        PhaseLightingConfig result = to;
        result.sunEulerAngles = Quaternion.Slerp(
            Quaternion.Euler(from.sunEulerAngles),
            Quaternion.Euler(to.sunEulerAngles), t).eulerAngles;
        result.sunColor = Color.Lerp(from.sunColor, to.sunColor, t);
        result.sunIntensity = Mathf.Lerp(from.sunIntensity, to.sunIntensity, t);
        result.shadowStrength = Mathf.Lerp(from.shadowStrength, to.shadowStrength, t);
        result.ambientSkyColor = Color.Lerp(from.ambientSkyColor, to.ambientSkyColor, t);
        result.ambientEquatorColor = Color.Lerp(from.ambientEquatorColor, to.ambientEquatorColor, t);
        result.ambientGroundColor = Color.Lerp(from.ambientGroundColor, to.ambientGroundColor, t);
        result.fogColor = Color.Lerp(from.fogColor, to.fogColor, t);
        result.fogDensity = Mathf.Lerp(from.fogDensity, to.fogDensity, t);
        result.cameraBackgroundColor = Color.Lerp(from.cameraBackgroundColor, to.cameraBackgroundColor, t);
        result.mistEmissionMultiplier = Mathf.Lerp(from.mistEmissionMultiplier, to.mistEmissionMultiplier, t);
        result.mistBrightnessMultiplier = Mathf.Lerp(from.mistBrightnessMultiplier, to.mistBrightnessMultiplier, t);
        result.mistSparkMultiplier = Mathf.Lerp(from.mistSparkMultiplier, to.mistSparkMultiplier, t);
        result.outskirtsMistMultiplier = Mathf.Lerp(from.outskirtsMistMultiplier, to.outskirtsMistMultiplier, t);
        result.enemyGlow = Mathf.Lerp(from.enemyGlow, to.enemyGlow, t);
        result.sanctuaryIntensity = Mathf.Lerp(from.sanctuaryIntensity, to.sanctuaryIntensity, t);
        return result;
    }

    private void ApplyConfigValues(PhaseLightingConfig config)
    {
        if (sunLight != null)
        {
            sunLight.useColorTemperature = false;
            sunLight.transform.rotation = Quaternion.Euler(config.sunEulerAngles);
            sunLight.color = config.sunColor;
            sunLight.intensity = config.sunIntensity;
            sunLight.shadowStrength = config.shadowStrength;
        }

        RenderSettings.ambientSkyColor = config.ambientSkyColor;
        RenderSettings.ambientEquatorColor = config.ambientEquatorColor;
        RenderSettings.ambientGroundColor = config.ambientGroundColor;
        RenderSettings.fogColor = config.fogColor;
        RenderSettings.fogDensity = config.fogDensity;

        if (syncCameraBackgroundColor)
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null)
            {
                if (targetCamera.clearFlags != CameraClearFlags.SolidColor)
                {
                    targetCamera.clearFlags = CameraClearFlags.SolidColor;
                }
                targetCamera.backgroundColor = config.cameraBackgroundColor;
            }
        }

        if (sanctuaryLight != null && sanctuaryLight != sunLight)
        {
            sanctuaryLight.color = sanctuaryLightColor;
            sanctuaryLight.intensity = config.sanctuaryIntensity;
            sanctuaryLight.enabled = config.sanctuaryIntensity > 0.001f;
        }

        ApplyMistModulation(config);
        EnemyGlow.SetGlow(config.enemyGlow, enemyGlowColor);
    }

    private void CaptureMistBaselines()
    {
        if (hasCapturedMistBaselines) return;
        hasCapturedMistBaselines = true;
        mistBaselines.Clear();

        var fogMgr = FogOfWarManager.Instance;
        if (fogMgr == null) return;

        TryRegisterMistMaterial(fogMgr.defaultFogMaterial);
        TryRegisterMistMaterial(fogMgr.borderlandsFogMaterial);
        TryRegisterMistMaterial(fogMgr.surveyingFogMaterial);
        TryRegisterMistMaterial(fogMgr.outskirtsFogMaterial);
    }

    private void TryRegisterMistMaterial(Material mat)
    {
        if (mat == null) return;
        for (int i = 0; i < mistBaselines.Count; i++)
        {
            if (mistBaselines[i].material == mat) return;
        }

        var baseline = new MistMaterialBaseline
        {
            material = mat,
            hasDiscoverableEmission = mat.HasProperty(PropDiscoverableEmission),
            discoverableEmission = mat.HasProperty(PropDiscoverableEmission) ? mat.GetFloat(PropDiscoverableEmission) : 0f,
            hasBlockedEmission = mat.HasProperty(PropBlockedEmission),
            blockedEmission = mat.HasProperty(PropBlockedEmission) ? mat.GetFloat(PropBlockedEmission) : 0f,
            hasDiscoverableBrightness = mat.HasProperty(PropDiscoverableBrightness),
            discoverableBrightness = mat.HasProperty(PropDiscoverableBrightness) ? mat.GetFloat(PropDiscoverableBrightness) : 0f,
            hasBlockedBrightness = mat.HasProperty(PropBlockedBrightness),
            blockedBrightness = mat.HasProperty(PropBlockedBrightness) ? mat.GetFloat(PropBlockedBrightness) : 0f,
            hasSparkIntensity = mat.HasProperty(PropSparkIntensity),
            sparkIntensity = mat.HasProperty(PropSparkIntensity) ? mat.GetFloat(PropSparkIntensity) : 0f,
            hasBaseColor = mat.HasProperty(PropBaseColor),
            baseColor = mat.HasProperty(PropBaseColor) ? mat.GetColor(PropBaseColor) : Color.white,
            hasEmissionColor = mat.HasProperty(PropEmissionColor),
            emissionColor = mat.HasProperty(PropEmissionColor) ? mat.GetColor(PropEmissionColor) : Color.black,
            hasEmissionStrength = mat.HasProperty(PropEmissionStrength),
            emissionStrength = mat.HasProperty(PropEmissionStrength) ? mat.GetFloat(PropEmissionStrength) : 0f,
        };

        mistBaselines.Add(baseline);
    }

    private void ApplyMistModulation(PhaseLightingConfig config)
    {
        if (!hasCapturedMistBaselines) CaptureMistBaselines();
        if (mistBaselines.Count == 0) return;

        var fogMgr = FogOfWarManager.Instance;
        Material outskirtsMat = fogMgr != null ? fogMgr.outskirtsFogMaterial : null;

        for (int i = 0; i < mistBaselines.Count; i++)
        {
            MistMaterialBaseline b = mistBaselines[i];
            Material mat = b.material;
            if (mat == null) continue;

            if (b.hasDiscoverableEmission)
                mat.SetFloat(PropDiscoverableEmission, b.discoverableEmission * config.mistEmissionMultiplier);
            if (b.hasBlockedEmission)
                mat.SetFloat(PropBlockedEmission, b.blockedEmission * config.mistEmissionMultiplier);
            if (b.hasDiscoverableBrightness)
                mat.SetFloat(PropDiscoverableBrightness, b.discoverableBrightness * config.mistBrightnessMultiplier);
            if (b.hasBlockedBrightness)
                mat.SetFloat(PropBlockedBrightness, b.blockedBrightness * config.mistBrightnessMultiplier);
            if (b.hasSparkIntensity)
                mat.SetFloat(PropSparkIntensity, b.sparkIntensity * config.mistSparkMultiplier);

            if (mat == outskirtsMat)
            {
                if (b.hasEmissionStrength)
                    mat.SetFloat(PropEmissionStrength, b.emissionStrength * config.mistEmissionMultiplier);
                if (b.hasEmissionColor)
                {
                    Color c = b.emissionColor * config.mistEmissionMultiplier;
                    c.a = b.emissionColor.a;
                    mat.SetColor(PropEmissionColor, c);
                }
                if (b.hasBaseColor)
                {
                    Color col = b.baseColor;
                    col.a = Mathf.Clamp01(b.baseColor.a * config.outskirtsMistMultiplier);
                    mat.SetColor(PropBaseColor, col);
                }
            }
        }
    }

    private void RestoreMistBaselines()
    {
        for (int i = 0; i < mistBaselines.Count; i++)
        {
            MistMaterialBaseline b = mistBaselines[i];
            Material mat = b.material;
            if (mat == null) continue;

            if (b.hasDiscoverableEmission) mat.SetFloat(PropDiscoverableEmission, b.discoverableEmission);
            if (b.hasBlockedEmission) mat.SetFloat(PropBlockedEmission, b.blockedEmission);
            if (b.hasDiscoverableBrightness) mat.SetFloat(PropDiscoverableBrightness, b.discoverableBrightness);
            if (b.hasBlockedBrightness) mat.SetFloat(PropBlockedBrightness, b.blockedBrightness);
            if (b.hasSparkIntensity) mat.SetFloat(PropSparkIntensity, b.sparkIntensity);
            if (b.hasBaseColor) mat.SetColor(PropBaseColor, b.baseColor);
            if (b.hasEmissionColor) mat.SetColor(PropEmissionColor, b.emissionColor);
            if (b.hasEmissionStrength) mat.SetFloat(PropEmissionStrength, b.emissionStrength);
        }
        mistBaselines.Clear();
        hasCapturedMistBaselines = false;
    }
}
