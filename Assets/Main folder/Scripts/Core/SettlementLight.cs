using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Reużywalny komponent nocnego oświetlenia budynku.
///     - Włącza się WYŁĄCZNIE w fazie Night (wyłączony w Morning i Dusk).
///     - Zarządzany centralnie przez DayNightCycle (brak pętli Update() na każdym budynku).
///     - Domyślnie bez cieni (LightShadows.None) z opcjonalnym wyjątkiem w Inspectorze.
///     - Obsługuje skalowanie koloru, zasięgu i intensywności dla Beacon of Hope (poziomy 1-5)
///       oraz budżetowanie / culling dystansu dla dużych miast.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public class SettlementLight : MonoBehaviour
{
    public enum BuildingLightPreset
    {
        Custom,
        SmallResidential,
        ResourceBuilding,
        ProductionBuilding,
        DefenseTower,
        Outpost,
        Landmark,
        WallGate,
        BeaconOfHope
    }

    [System.Serializable]
    public struct BeaconLevelLightConfig
    {
        public Color color;
        [Min(0f)] public float intensity;
        [Min(0.5f)] public float range;
    }

    public static readonly Color DefaultWarmLightColor = new Color(1.0f, 0.737f, 0.439f, 1.0f); // #FFBC70

    private static readonly BeaconLevelLightConfig[] DefaultBeaconLevels =
    {
        new BeaconLevelLightConfig { color = new Color(1.00f, 0.471f, 0.196f, 1f), intensity = 2.8f, range = 10.0f }, // Lvl 1 (#FF7832)
        new BeaconLevelLightConfig { color = new Color(1.00f, 0.573f, 0.251f, 1f), intensity = 3.6f, range = 13.0f }, // Lvl 2 (#FF9240)
        new BeaconLevelLightConfig { color = new Color(1.00f, 0.678f, 0.329f, 1f), intensity = 4.5f, range = 16.0f }, // Lvl 3 (#FFAD54)
        new BeaconLevelLightConfig { color = new Color(1.00f, 0.776f, 0.439f, 1f), intensity = 5.5f, range = 19.5f }, // Lvl 4 (#FFC670)
        new BeaconLevelLightConfig { color = new Color(1.00f, 0.878f, 0.580f, 1f), intensity = 6.6f, range = 23.0f }, // Lvl 5 (#FFE094)
    };

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // Centralny rejestr aktywnych komponentów - zero Update() per budynek
    private static readonly List<SettlementLight> RegisteredLights = new List<SettlementLight>(128);
    private static readonly List<LightDistanceEntry> DistanceSortBuffer = new List<LightDistanceEntry>(128);
    private static bool anyLightLitOrTransitioning;
    private static float globalNightTarget;
    private static bool hasSubscribedGlobalPhase;
    private static TimePhaseManager subscribedPhaseManager;
    private static GameObject fallbackDriverObject;
    private static Camera cachedMainCamera;

    private struct LightDistanceEntry
    {
        public int index;
        public float sqrDistance;
    }

    [Header("Referencje i Typ")]
    [SerializeField] private Light targetLight;
    [SerializeField] private Transform lightAnchor;
    [SerializeField] private BuildingLightPreset preset = BuildingLightPreset.Custom;

    [Tooltip("Czy to jest Wielkie Palenisko Twierdzy (Beacon of Hope), skalujące kolor, zasięg i moc z poziomem (1-5)?")]
    public bool isBeacon = false;

    [Header("Parametry Światła")]
    public Color lightColor = DefaultWarmLightColor;
    [Min(0f)] public float baseIntensity = 2.0f;
    [Min(0.5f)] public float baseRange = 5.5f;
    public Vector3 lightOffset = new Vector3(0f, 1.4f, 0f);
    public bool applyLightOffset = true;

    [Tooltip("Domyślnie WYŁĄCZONE (LightShadows.None) dla wysokiej wydajności. Włącz tylko dla absolutnych wyjątków.")]
    [SerializeField] private bool castShadows = false;

    [Header("Aktywacja Nocna (Tylko NOC)")]
    [Tooltip("Światło włącza się automatycznie wyłącznie w fazie Night (wyłączone w Morning i Dusk).")]
    public bool autoControlByPhase = true;
    [Min(0.05f)] public float fadeInDuration = 1.4f;
    [Min(0.05f)] public float fadeOutDuration = 1.0f;

    [Header("Subtelne Migotanie (Flicker)")]
    public bool enableFlicker = true;
    public float flickerSpeed = 6.5f;
    [Range(0f, 0.5f)] public float flickerIntensityJitter = 0.14f;
    [Range(0f, 0.25f)] public float flickerRangeJitter = 0.04f;

    [Header("Opcjonalna Synchronizacja Emisji Materiału")]
    [SerializeField] private Renderer[] emissiveRenderers;
    [ColorUsage(false, true)]
    [SerializeField] private Color emissionColor = new Color(1.0f, 0.72f, 0.38f, 1f);
    [Min(0f)] [SerializeField] private float maxEmissionIntensity = 1.6f;

    private int registryIndex = -1;
    private float noiseOffset;
    private float currentPhaseMultiplier;
    private float targetPhaseMultiplier;
    private float currentDistanceMultiplier = 1f;
    private bool operationalEnabled = true;
    private bool beaconColorTransitioning;
    private Color currentColor;
    private Color targetBeaconColor;
    private float currentIntensitySetting;
    private float targetBeaconIntensity;
    private float currentRangeSetting;
    private float targetBeaconRange;
    private MaterialPropertyBlock emissionBlock;
    private bool hasInitialized;

    public static int ActivePointLightCount { get; private set; }
    public static int RegisteredCount => RegisteredLights.Count;
    public Light TargetLight => targetLight;
    public bool CastShadows => castShadows;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        RegisteredLights.Clear();
        DistanceSortBuffer.Clear();
        anyLightLitOrTransitioning = false;
        globalNightTarget = 0f;
        hasSubscribedGlobalPhase = false;
        subscribedPhaseManager = null;
        fallbackDriverObject = null;
        cachedMainCamera = null;
        ActivePointLightCount = 0;
    }

    private void Awake()
    {
        noiseOffset = Random.Range(0f, 1000f);
        InitializeInternal(applyImmediateFromCurrentPhase: false);
    }

    private void OnEnable()
    {
        EnsureGlobalPhaseSubscription();
        RegisterSelf();
        EnsureFallbackDriverIfNeeded();

        InitializeInternal(applyImmediateFromCurrentPhase: true);

        if (isBeacon)
        {
            BeaconEntity.OnHearthStateChanged -= HandleHearthStateChanged;
            BeaconEntity.OnHearthStateChanged += HandleHearthStateChanged;
            ApplyBeaconLevelSettings(GetEffectiveBeaconLevel(), immediate: true);
        }
    }

    private void Start()
    {
        EnsureGlobalPhaseSubscription();
        InitializeInternal(applyImmediateFromCurrentPhase: true);

        if (isBeacon)
        {
            ApplyBeaconLevelSettings(GetEffectiveBeaconLevel(), immediate: true);
        }
    }

    private void OnDisable()
    {
        if (isBeacon)
        {
            BeaconEntity.OnHearthStateChanged -= HandleHearthStateChanged;
        }

        UnregisterSelf();

        if (targetLight != null)
        {
            targetLight.intensity = 0f;
            targetLight.enabled = false;
        }
        ApplyEmission(0f);
    }

    private void OnDestroy()
    {
        if (isBeacon)
        {
            BeaconEntity.OnHearthStateChanged -= HandleHearthStateChanged;
        }

        UnregisterSelf();
    }

    private void OnValidate()
    {
        fadeInDuration = Mathf.Max(0.05f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0.05f, fadeOutDuration);
        baseIntensity = Mathf.Max(0f, baseIntensity);
        baseRange = Mathf.Max(0.5f, baseRange);

        if (targetLight == null)
            targetLight = GetComponent<Light>();

        if (targetLight != null)
        {
            targetLight.type = LightType.Point;
            targetLight.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
            targetLight.color = lightColor;
            targetLight.range = baseRange;
        }

        if (!Application.isPlaying && applyLightOffset && lightAnchor == null && targetLight != null)
        {
            // Gdy SettlementLight jest na dziecku, ustawienie lokalnej pozycji utrzymuje spójność z Inspektorem.
            if (targetLight.transform.parent != null)
            {
                targetLight.transform.localPosition = lightOffset;
            }
        }
    }

    public void Configure(
        Color color,
        float intensity,
        float range,
        Vector3 offset,
        bool allowShadows = false,
        bool beaconMode = false,
        BuildingLightPreset lightPreset = BuildingLightPreset.Custom)
    {
        lightColor = color;
        baseIntensity = Mathf.Max(0f, intensity);
        baseRange = Mathf.Max(0.5f, range);
        lightOffset = offset;
        applyLightOffset = true;
        castShadows = allowShadows;
        isBeacon = beaconMode;
        preset = lightPreset;

        currentColor = lightColor;
        currentIntensitySetting = baseIntensity;
        currentRangeSetting = baseRange;

        if (isBeacon)
        {
            BeaconEntity.OnHearthStateChanged -= HandleHearthStateChanged;
            BeaconEntity.OnHearthStateChanged += HandleHearthStateChanged;
        }

        InitializeInternal(applyImmediateFromCurrentPhase: true);

        if (isBeacon)
        {
            ApplyBeaconLevelSettings(GetEffectiveBeaconLevel(), immediate: true);
        }
    }

    public void SetOperationalEnabled(bool enabledState)
    {
        if (operationalEnabled == enabledState) return;
        operationalEnabled = enabledState;
        RefreshTargetMultiplier();
    }

    public void ApplyPreset(BuildingLightPreset targetPreset)
    {
        preset = targetPreset;
        lightColor = DefaultWarmLightColor;
        castShadows = false;

        switch (targetPreset)
        {
            case BuildingLightPreset.SmallResidential:
                baseIntensity = 1.8f;
                baseRange = 4.5f;
                lightOffset = new Vector3(0f, 1.15f, 0f);
                break;
            case BuildingLightPreset.ResourceBuilding:
                baseIntensity = 2.0f;
                baseRange = 5.2f;
                lightOffset = new Vector3(0f, 1.2f, 0f);
                break;
            case BuildingLightPreset.ProductionBuilding:
                baseIntensity = 2.15f;
                baseRange = 5.6f;
                lightOffset = new Vector3(0f, 1.4f, 0f);
                break;
            case BuildingLightPreset.DefenseTower:
                baseIntensity = 2.1f;
                baseRange = 5.8f;
                lightOffset = new Vector3(0f, 1.75f, 0f);
                break;
            case BuildingLightPreset.Outpost:
                baseIntensity = 2.4f;
                baseRange = 6.5f;
                lightOffset = new Vector3(0f, 1.6f, 0f);
                break;
            case BuildingLightPreset.Landmark:
                baseIntensity = 2.6f;
                baseRange = 7.5f;
                lightOffset = new Vector3(0f, 1.8f, 0f);
                break;
            case BuildingLightPreset.WallGate:
                baseIntensity = 1.7f;
                baseRange = 4.5f;
                lightOffset = new Vector3(0f, 1.3f, 0f);
                break;
            case BuildingLightPreset.BeaconOfHope:
                isBeacon = true;
                baseIntensity = DefaultBeaconLevels[2].intensity;
                baseRange = DefaultBeaconLevels[2].range;
                lightColor = DefaultBeaconLevels[2].color;
                lightOffset = new Vector3(0f, 2.2f, 0f);
                break;
        }

        currentColor = lightColor;
        currentIntensitySetting = baseIntensity;
        currentRangeSetting = baseRange;
        ApplyPlacement();
        ApplyVisualState(Time.time);
    }

    public void ApplyBeaconLevelSettings(int level, bool immediate = false)
    {
        int clamped = Mathf.Clamp(level, 1, DefaultBeaconLevels.Length);
        BeaconLevelLightConfig cfg = DefaultBeaconLevels[clamped - 1];

        targetBeaconColor = cfg.color;
        targetBeaconIntensity = cfg.intensity;
        targetBeaconRange = cfg.range;

        if (immediate || !Application.isPlaying)
        {
            lightColor = cfg.color;
            baseIntensity = cfg.intensity;
            baseRange = cfg.range;
            currentColor = cfg.color;
            currentIntensitySetting = cfg.intensity;
            currentRangeSetting = cfg.range;
            beaconColorTransitioning = false;
            ApplyVisualState(Time.time);
        }
        else
        {
            beaconColorTransitioning = true;
            anyLightLitOrTransitioning = true;
        }
    }

    private void InitializeInternal(bool applyImmediateFromCurrentPhase)
    {
        if (targetLight == null)
            targetLight = GetComponent<Light>();

        if (targetLight != null)
        {
            targetLight.type = LightType.Point;
            targetLight.renderMode = LightRenderMode.Auto;
            targetLight.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
        }

        if (!hasInitialized)
        {
            currentColor = lightColor;
            currentIntensitySetting = baseIntensity;
            currentRangeSetting = baseRange;
            targetBeaconColor = lightColor;
            targetBeaconIntensity = baseIntensity;
            targetBeaconRange = baseRange;
            hasInitialized = true;
        }

        ApplyPlacement();

        if (applyImmediateFromCurrentPhase)
        {
            float desired = ComputeDesiredPhaseMultiplier();
            currentPhaseMultiplier = desired;
            targetPhaseMultiplier = desired;
            if (desired > 0.001f)
            {
                anyLightLitOrTransitioning = true;
            }
            ApplyVisualState(Time.time);
        }
    }

    private void ApplyPlacement()
    {
        if (targetLight == null) return;

        if (lightAnchor != null)
        {
            targetLight.transform.position = lightAnchor.TransformPoint(lightOffset);
        }
        else if (applyLightOffset && targetLight.transform.parent != null)
        {
            targetLight.transform.localPosition = lightOffset;
        }
    }

    private float ComputeDesiredPhaseMultiplier()
    {
        if (!operationalEnabled) return 0f;
        if (!autoControlByPhase)
        {
            // Nawet bez autoControlByPhase respektuje fazę, chyba że świadomie wyłączono ją na obiekcie.
            return 1f;
        }

        if (TimePhaseManager.Instance != null)
        {
            return TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Night ? 1f : 0f;
        }

        return globalNightTarget;
    }

    private void RefreshTargetMultiplier()
    {
        if (isBeacon)
        {
            ApplyBeaconLevelSettings(GetEffectiveBeaconLevel(), immediate: !Application.isPlaying || currentPhaseMultiplier <= 0.001f);
        }

        targetPhaseMultiplier = ComputeDesiredPhaseMultiplier();
        if (targetPhaseMultiplier > 0.001f || currentPhaseMultiplier > 0.001f)
        {
            anyLightLitOrTransitioning = true;
        }
    }

    private static int GetEffectiveBeaconLevel()
    {
        if (BeaconEntity.ActiveNightLevel > 0)
            return Mathf.Clamp(BeaconEntity.ActiveNightLevel, 1, 5);
        return Mathf.Clamp(BeaconEntity.SelectedDraftLevel, 1, 5);
    }

    private void HandleHearthStateChanged()
    {
        if (isBeacon)
        {
            ApplyBeaconLevelSettings(GetEffectiveBeaconLevel(), immediate: false);
        }
    }

    private void RegisterSelf()
    {
        if (registryIndex >= 0 && registryIndex < RegisteredLights.Count && RegisteredLights[registryIndex] == this)
            return;

        registryIndex = RegisteredLights.Count;
        RegisteredLights.Add(this);
    }

    private void UnregisterSelf()
    {
        if (registryIndex < 0 || registryIndex >= RegisteredLights.Count) return;

        int lastIdx = RegisteredLights.Count - 1;
        if (registryIndex != lastIdx)
        {
            SettlementLight swap = RegisteredLights[lastIdx];
            RegisteredLights[registryIndex] = swap;
            swap.registryIndex = registryIndex;
        }

        RegisteredLights.RemoveAt(lastIdx);
        registryIndex = -1;
    }

    private static void EnsureGlobalPhaseSubscription()
    {
        TimePhaseManager manager = TimePhaseManager.Instance;
        if (manager == null) return;

        if (hasSubscribedGlobalPhase && subscribedPhaseManager == manager) return;

        if (subscribedPhaseManager != null)
        {
            subscribedPhaseManager.OnMorningStarted -= HandleGlobalNonNightPhase;
            subscribedPhaseManager.OnDuskStarted -= HandleGlobalNonNightPhase;
            subscribedPhaseManager.OnNightStarted -= HandleGlobalNightPhase;
        }

        subscribedPhaseManager = manager;
        subscribedPhaseManager.OnMorningStarted += HandleGlobalNonNightPhase;
        subscribedPhaseManager.OnDuskStarted += HandleGlobalNonNightPhase;
        subscribedPhaseManager.OnNightStarted += HandleGlobalNightPhase;
        hasSubscribedGlobalPhase = true;

        globalNightTarget = manager.CurrentTimePhase == TimePhases.Night ? 1f : 0f;
        if (globalNightTarget > 0.5f)
        {
            anyLightLitOrTransitioning = true;
        }
    }

    private static void HandleGlobalNonNightPhase()
    {
        globalNightTarget = 0f;
        RefreshAllTargetMultipliers();
    }

    private static void HandleGlobalNightPhase()
    {
        globalNightTarget = 1f;
        anyLightLitOrTransitioning = true;
        RefreshAllTargetMultipliers();
    }

    private static void RefreshAllTargetMultipliers()
    {
        for (int i = 0; i < RegisteredLights.Count; i++)
        {
            SettlementLight lightComp = RegisteredLights[i];
            if (lightComp != null)
            {
                lightComp.RefreshTargetMultiplier();
            }
        }
    }

    private static void EnsureFallbackDriverIfNeeded()
    {
        if (!Application.isPlaying) return;
        if (DayNightCycle.Instance != null) return;
        if (fallbackDriverObject != null) return;

        fallbackDriverObject = new GameObject("SettlementLightFallbackDriver")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        fallbackDriverObject.AddComponent<SettlementLightFallbackRunner>();
    }

    /// <summary>
    ///     Jedna centralna aktualizacja wszystkich świateł budynków (wywoływana przez DayNightCycle).
    ///     W fazach Morning i Dusk (po zakończeniu wygaszania) wychodzi w O(1).
    /// </summary>
    public static void TickAllLights(
        float deltaTime,
        int maxActivePointLights = 64,
        float maxVisibleDistance = 90f,
        float distanceFadeMargin = 15f)
    {
        EnsureGlobalPhaseSubscription();

        if (!anyLightLitOrTransitioning || RegisteredLights.Count == 0)
        {
            ActivePointLightCount = 0;
            return;
        }

        float now = Time.time;
        bool stillActive = false;

        // Krok 1: Aktualizuj płynne przejścia fazy i paleniska (Beacon)
        for (int i = RegisteredLights.Count - 1; i >= 0; i--)
        {
            SettlementLight s = RegisteredLights[i];
            if (s == null)
            {
                RegisteredLights.RemoveAt(i);
                continue;
            }

            if (s.beaconColorTransitioning)
            {
                float step = deltaTime * 2.5f;
                s.currentColor = Color.Lerp(s.currentColor, s.targetBeaconColor, step);
                s.currentIntensitySetting = Mathf.Lerp(s.currentIntensitySetting, s.targetBeaconIntensity, step);
                s.currentRangeSetting = Mathf.Lerp(s.currentRangeSetting, s.targetBeaconRange, step);
                s.lightColor = s.currentColor;
                s.baseIntensity = s.currentIntensitySetting;
                s.baseRange = s.currentRangeSetting;

                if (Mathf.Abs(s.currentIntensitySetting - s.targetBeaconIntensity) < 0.01f &&
                    Mathf.Abs(s.currentRangeSetting - s.targetBeaconRange) < 0.02f)
                {
                    s.currentColor = s.targetBeaconColor;
                    s.currentIntensitySetting = s.targetBeaconIntensity;
                    s.currentRangeSetting = s.targetBeaconRange;
                    s.lightColor = s.targetBeaconColor;
                    s.baseIntensity = s.targetBeaconIntensity;
                    s.baseRange = s.targetBeaconRange;
                    s.beaconColorTransitioning = false;
                }
            }

            float target = s.targetPhaseMultiplier;
            if (!Mathf.Approximately(s.currentPhaseMultiplier, target))
            {
                float duration = target > s.currentPhaseMultiplier ? s.fadeInDuration : s.fadeOutDuration;
                float rate = duration > 0.001f ? deltaTime / duration : 1f;
                s.currentPhaseMultiplier = Mathf.MoveTowards(s.currentPhaseMultiplier, target, rate);
            }

            if (s.currentPhaseMultiplier > 0.001f || s.targetPhaseMultiplier > 0.001f || s.beaconColorTransitioning)
            {
                stillActive = true;
            }
        }

        // Krok 2: Budżetowanie i culling po odległości od punktu obserwacji kamery (dla bardzo dużych miast)
        UpdateDistanceBudgets(maxActivePointLights, maxVisibleDistance, distanceFadeMargin);

        // Krok 3: Zastosuj docelowe wartości na komponentach Light (z wyłączeniem Light.enabled = false przy 0)
        int activeLightsCount = 0;
        for (int i = 0; i < RegisteredLights.Count; i++)
        {
            SettlementLight s = RegisteredLights[i];
            if (s != null && s.ApplyVisualState(now))
            {
                activeLightsCount++;
            }
        }

        ActivePointLightCount = activeLightsCount;
        anyLightLitOrTransitioning = stillActive;
    }

    private static void UpdateDistanceBudgets(int maxActivePointLights, float maxVisibleDistance, float fadeMargin)
    {
        int count = RegisteredLights.Count;
        if (count == 0) return;

        bool useBudget = maxActivePointLights > 0 && count > maxActivePointLights;
        bool useDistanceCull = maxVisibleDistance > 0f;

        if (!useBudget && !useDistanceCull)
        {
            for (int i = 0; i < count; i++)
            {
                if (RegisteredLights[i] != null)
                    RegisteredLights[i].currentDistanceMultiplier = 1f;
            }
            return;
        }

        if (cachedMainCamera == null || !cachedMainCamera.isActiveAndEnabled)
        {
            cachedMainCamera = Camera.main;
        }

        if (cachedMainCamera == null)
        {
            for (int i = 0; i < count; i++)
            {
                if (RegisteredLights[i] != null)
                    RegisteredLights[i].currentDistanceMultiplier = 1f;
            }
            return;
        }

        Vector3 focusPos = GetCameraGroundFocus(cachedMainCamera);
        float maxDist = Mathf.Max(1f, maxVisibleDistance);
        float fadeStart = Mathf.Max(0f, maxDist - Mathf.Max(1f, fadeMargin));

        DistanceSortBuffer.Clear();
        for (int i = 0; i < count; i++)
        {
            SettlementLight s = RegisteredLights[i];
            if (s == null || s.currentPhaseMultiplier <= 0.001f)
            {
                if (s != null) s.currentDistanceMultiplier = 0f;
                continue;
            }

            Vector3 p = s.transform.position;
            float dx = p.x - focusPos.x;
            float dz = p.z - focusPos.z;
            float sqrDist = dx * dx + dz * dz;

            if (s.isBeacon)
            {
                // Wielkie Palenisko ma najwyższy priorytet widoczności
                s.currentDistanceMultiplier = 1f;
                DistanceSortBuffer.Add(new LightDistanceEntry { index = i, sqrDistance = -1f });
            }
            else
            {
                float dist = Mathf.Sqrt(sqrDist);
                float distFactor = dist <= fadeStart
                    ? 1f
                    : 1f - Mathf.Clamp01((dist - fadeStart) / Mathf.Max(0.01f, maxDist - fadeStart));
                s.currentDistanceMultiplier = distFactor;

                if (distFactor > 0.001f)
                {
                    DistanceSortBuffer.Add(new LightDistanceEntry { index = i, sqrDistance = sqrDist });
                }
            }
        }

        if (useBudget && DistanceSortBuffer.Count > maxActivePointLights)
        {
            DistanceSortBuffer.Sort((a, b) => a.sqrDistance.CompareTo(b.sqrDistance));
            for (int k = maxActivePointLights; k < DistanceSortBuffer.Count; k++)
            {
                SettlementLight culled = RegisteredLights[DistanceSortBuffer[k].index];
                if (culled != null && !culled.isBeacon)
                {
                    culled.currentDistanceMultiplier = 0f;
                }
            }
        }
    }

    private static Vector3 GetCameraGroundFocus(Camera cam)
    {
        Transform camT = cam.transform;
        Vector3 pos = camT.position;
        Vector3 fwd = camT.forward;
        if (fwd.y < -0.05f)
        {
            float t = pos.y / -fwd.y;
            return pos + fwd * Mathf.Clamp(t, 0f, 200f);
        }
        return pos;
    }

    private bool ApplyVisualState(float now)
    {
        if (targetLight == null) return false;

        float phaseCurve = Mathf.SmoothStep(0f, 1f, currentPhaseMultiplier);
        ApplyEmission(phaseCurve);

        float combinedMultiplier = phaseCurve * currentDistanceMultiplier;
        if (combinedMultiplier <= 0.001f)
        {
            targetLight.intensity = 0f;
            if (targetLight.enabled) targetLight.enabled = false;
            return false;
        }

        if (!targetLight.enabled) targetLight.enabled = true;

        float calculatedIntensity = currentIntensitySetting * combinedMultiplier;
        float calculatedRange = currentRangeSetting;

        if (enableFlicker && Application.isPlaying)
        {
            float t = now * flickerSpeed + noiseOffset;
            float noiseVal = Mathf.PerlinNoise(t, 0.5f);
            float jitter = (noiseVal - 0.5f) * 2.0f;

            calculatedIntensity += currentIntensitySetting * combinedMultiplier * (jitter * flickerIntensityJitter);
            calculatedRange += currentRangeSetting * (jitter * flickerRangeJitter);
        }

        targetLight.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
        targetLight.color = currentColor;
        targetLight.intensity = Mathf.Max(0f, calculatedIntensity);
        targetLight.range = Mathf.Max(0.5f, calculatedRange);
        return true;
    }

    private void ApplyEmission(float factor)
    {
        if (emissiveRenderers == null || emissiveRenderers.Length == 0) return;

        if (emissionBlock == null) emissionBlock = new MaterialPropertyBlock();
        Color hdrEmission = emissionColor * (factor * maxEmissionIntensity);
        hdrEmission.a = 1f;

        for (int i = 0; i < emissiveRenderers.Length; i++)
        {
            Renderer r = emissiveRenderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(emissionBlock);
            emissionBlock.SetColor(EmissionColorId, hdrEmission);
            r.SetPropertyBlock(emissionBlock);
        }
    }

    private sealed class SettlementLightFallbackRunner : MonoBehaviour
    {
        private void Update()
        {
            if (DayNightCycle.Instance != null)
            {
                Destroy(gameObject);
                fallbackDriverObject = null;
                return;
            }

            TickAllLights(Time.deltaTime);
        }
    }
}
