using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Dark fantasy: ostatnia osada w umierającym świecie.
/// Morning: chłodny, pochmurny dzień; Dusk: przygaszony, złowrogi zmierzch;
/// Night: czytelna granatowa noc, bez czarnych plam na mapie.
///
/// Steruje Directional Light, ambientem Trilight, mgłą i EnemyGlow.
/// Opcjonalnie steruje ciepłym światłem bezpiecznej strefy (sanctuaryLight).
///
/// UWAGA: Unity zachowuje wartości zapisane w Inspectorze. Po podmianie skryptu:
/// menu komponentu (trzy kropki) -> "Zastosuj presety Dark Fantasy".
/// </summary>
public class DayNightCycle : MonoBehaviour
{
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

        [Header("Mgła")]
        public Color fogColor;
        [Min(0f)] public float fogDensity;

        [Header("Dodatkowe światła")]
        [Range(0f, 1f)] public float enemyGlow;
        [Min(0f)] public float sanctuaryIntensity;
    }

    [Header("Referencje")]
    [Tooltip("Główne Directional Light. Jeśli puste, skrypt spróbuje pobrać Light z tego obiektu.")]
    public Light sunLight;

    [Tooltip("Opcjonalny Point/Spot Light umieszczony w centrum osady. Nie przypisuj tu Directional Light.")]
    public Light sanctuaryLight;

    [Header("Barwa ognia / bezpieczeństwa")]
    [Tooltip("Ciepła poświata osady; intensywność zależy od fazy.")]
    public Color sanctuaryLightColor = new Color(1f, 0.60f, 0.30f, 1f);

    [Header("Zmiana fazy")]
    [Min(0f)]
    public float transitionDuration = 2.5f;

    [Header("Poświata wrogów")]
    [Tooltip("Kolor emisji wrogów (HDR). Elity i bossowie zachowują własne mnożniki EnemyGlow.")]
    [ColorUsage(false, true)]
    public Color enemyGlowColor = new Color(0.40f, 0.13f, 0.72f, 1f);

    [Header("Konfiguracje faz")]
    public PhaseLightingConfig morningConfig = RecommendedMorning();
    public PhaseLightingConfig duskConfig = RecommendedDusk();
    public PhaseLightingConfig nightConfig = RecommendedNight();

    // Oświetlenie globalne pozostaje raczej neutralne.
    // Fiolet należy do kataklizmu, a nie do każdej powierzchni na mapie.
    private static PhaseLightingConfig RecommendedMorning()
    {
        return new PhaseLightingConfig
        {
            phaseName = "Morning - martwy świt",
            sunEulerAngles = new Vector3(50f, -35f, 0f),
            sunColor = new Color(0.79f, 0.84f, 0.89f, 1f),
            sunIntensity = 0.95f,
            shadowStrength = 0.68f,

            ambientSkyColor = new Color(0.39f, 0.43f, 0.48f, 1f),
            ambientEquatorColor = new Color(0.30f, 0.34f, 0.39f, 1f),
            ambientGroundColor = new Color(0.19f, 0.22f, 0.26f, 1f),

            fogColor = new Color(0.27f, 0.30f, 0.33f, 1f),
            fogDensity = 0.0028f,
            enemyGlow = 0f,
            sanctuaryIntensity = 0.25f
        };
    }

    private static PhaseLightingConfig RecommendedDusk()
    {
        return new PhaseLightingConfig
        {
            phaseName = "Dusk - ostatnie światło",
            // Światło jest niskie, ale pada również na górne powierzchnie heksów.
            sunEulerAngles = new Vector3(32f, -60f, 0f),
            sunColor = new Color(0.88f, 0.55f, 0.50f, 1f),
            sunIntensity = 0.82f,
            shadowStrength = 0.56f,

            // Chłodne wypełnienie zamiast prawie czarnego, purpurowego cienia.
            ambientSkyColor = new Color(0.34f, 0.34f, 0.43f, 1f),
            ambientEquatorColor = new Color(0.27f, 0.28f, 0.36f, 1f),
            ambientGroundColor = new Color(0.19f, 0.20f, 0.28f, 1f),

            fogColor = new Color(0.24f, 0.20f, 0.29f, 1f),
            fogDensity = 0.0036f,
            enemyGlow = 0f,
            sanctuaryIntensity = 0.85f
        };
    }

    private static PhaseLightingConfig RecommendedNight()
    {
        return new PhaseLightingConfig
        {
            phaseName = "Night - obcy półmrok",
            // Księżyc wysoko: czytelne górne płaszczyzny heksów i roślinność.
            sunEulerAngles = new Vector3(56f, -35f, 0f),
            sunColor = new Color(0.54f, 0.66f, 0.83f, 1f),
            sunIntensity = 0.62f,
            shadowStrength = 0.38f,

            // Najważniejsza zmiana: ciemny granat, ale nie czerń.
            ambientSkyColor = new Color(0.29f, 0.34f, 0.45f, 1f),
            ambientEquatorColor = new Color(0.24f, 0.29f, 0.39f, 1f),
            ambientGroundColor = new Color(0.17f, 0.21f, 0.30f, 1f),

            fogColor = new Color(0.13f, 0.16f, 0.26f, 1f),
            fogDensity = 0.0045f,
            enemyGlow = 1f,
            sanctuaryIntensity = 1.65f
        };
    }

    private Coroutine transitionCoroutine;
    private PhaseLightingConfig currentConfig;
    private TimePhaseManager phaseManager;

    private void Awake()
    {
        if (sunLight == null)
            sunLight = GetComponent<Light>();

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
    }

    private void Start()
    {
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

    private void OnDestroy()
    {
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
    private void ApplyRecommendedPresets()
    {
        morningConfig = RecommendedMorning();
        duskConfig = RecommendedDusk();
        nightConfig = RecommendedNight();

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
    private void ApplyRecommendedPresetsLegacy()
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

        // currentConfig aktualizujemy co klatkę, więc przerwanie animacji nie robi skoku.
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
        result.enemyGlow = Mathf.Lerp(from.enemyGlow, to.enemyGlow, t);
        result.sanctuaryIntensity = Mathf.Lerp(from.sanctuaryIntensity, to.sanctuaryIntensity, t);
        return result;
    }

    private void ApplyConfigValues(PhaseLightingConfig config)
    {
        if (sunLight != null)
        {
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

        if (sanctuaryLight != null && sanctuaryLight != sunLight)
        {
            sanctuaryLight.color = sanctuaryLightColor;
            sanctuaryLight.intensity = config.sanctuaryIntensity;
        }

        EnemyGlow.SetGlow(config.enemyGlow, enemyGlowColor);
    }
}
