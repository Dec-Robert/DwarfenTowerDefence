using System.Collections;
using UnityEngine;

/// <summary>
/// Zarządza oświetleniem globalnym sceny (Directional Light, Ambient, Mgła) w relacji do faz gry (TimePhaseManager).
/// Zapewnia 1-sekundowe, płynne przejście pomiędzy nastrojami:
/// - Rano: Chłodny, posępny świt (wysokie słońce, blady beż, chłodny szary ambient)
/// - Zmierzch: Złowrogi, alarmujący zachód (bardzo niskie słońce, ostre długie cienie, płomienna pomarańcz i purpura)
/// - Noc: Czytelny granatowo-fioletowy półmrok w świetle księżyca (teren widoczny, ognie osady nadal najjaśniejsze)
///   oraz fioletowa poświata wrogów (EnemyGlow), włączana tylko w fazie Night.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    [System.Serializable]
    public struct PhaseLightingConfig
    {
        public string phaseName;
        [Header("Światło Słoneczne / Księżycowe")]
        public Vector3 sunEulerAngles;
        public Color sunColor;
        public float sunIntensity;

        [Header("Oświetlenie Otoczenia (Ambient)")]
        public Color ambientSkyColor;
        public Color ambientEquatorColor;
        public Color ambientGroundColor;

        [Header("Mgła Sceny")]
        public Color fogColor;
        public float fogDensity;

        [Header("Poświata wrogów")]
        [Range(0f, 1f)] public float enemyGlow;
    }

    [Header("Referencje")]
    [Tooltip("Główne światło kierunkowe (Directional Light)")]
    public Light sunLight;

    [Header("Czas Przejścia")]
    [Tooltip("Czas trwania przejścia oświetlenia między fazami (w sekundach)")]
    public float transitionDuration = 1.0f;

    [Header("Poświata wrogów (emisja, mnożona przez enemyGlow z konfiguracji fazy)")]
    [Tooltip("Kolor emisji wrogów (HDR). Elity x1.6, bossowie x2.2.")]
    [ColorUsage(false, true)]
    public Color enemyGlowColor = new Color(0.34f, 0.08f, 0.68f, 1f);

    [Header("Konfiguracje Faz")]
    public PhaseLightingConfig morningConfig = new PhaseLightingConfig
    {
        phaseName = "Rano",
        sunEulerAngles = new Vector3(45f, -30f, 0f),
        sunColor = new Color(0.90f, 0.88f, 0.84f, 1f),
        sunIntensity = 1.15f,
        ambientSkyColor = new Color(0.18f, 0.20f, 0.24f, 1f),
        ambientEquatorColor = new Color(0.12f, 0.13f, 0.15f, 1f),
        ambientGroundColor = new Color(0.06f, 0.06f, 0.07f, 1f),
        fogColor = new Color(0.22f, 0.25f, 0.28f, 1f),
        fogDensity = 0.003f
    };

    public PhaseLightingConfig duskConfig = new PhaseLightingConfig
    {
        phaseName = "Zmierzch",
        sunEulerAngles = new Vector3(14f, -60f, 0f), // Niskie słońce = długie, złowrogie cienie
        sunColor = new Color(0.92f, 0.46f, 0.18f, 1f), // Płomienna pomarańcz
        sunIntensity = 0.65f,
        ambientSkyColor = new Color(0.20f, 0.12f, 0.18f, 1f), // Fioletowo-zmierzchowy ton
        ambientEquatorColor = new Color(0.13f, 0.08f, 0.11f, 1f),
        ambientGroundColor = new Color(0.05f, 0.03f, 0.04f, 1f),
        fogColor = new Color(0.23f, 0.12f, 0.21f, 1f),
        fogDensity = 0.005f
    };

    public PhaseLightingConfig nightConfig = new PhaseLightingConfig
    {
        phaseName = "Noc",
        sunEulerAngles = new Vector3(70f, -30f, 0f), // Chłodne strome światło księżyca
        sunColor = new Color(0.36f, 0.34f, 0.70f, 1f), // Granatowo-fioletowa poświata księżyca
        sunIntensity = 0.45f, // Teren czytelny, ale ognie osady nadal wyraźnie jaśniejsze
        ambientSkyColor = new Color(0.11f, 0.10f, 0.22f, 1f), // Granat z nutą fioletu
        ambientEquatorColor = new Color(0.075f, 0.065f, 0.15f, 1f),
        ambientGroundColor = new Color(0.035f, 0.03f, 0.07f, 1f),
        fogColor = new Color(0.11f, 0.07f, 0.20f, 1f), // Nocna purpura
        fogDensity = 0.006f,
        enemyGlow = 1f // Fioletowa poświata wrogów tylko w nocy
    };

    private Coroutine transitionCoroutine;
    private PhaseLightingConfig currentConfig;

    private void Awake()
    {
        if (sunLight == null)
            sunLight = GetComponent<Light>();

        // Włączamy zaawansowany ambient Trilight oraz mgłę
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
    }

    private void Start()
    {
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted += HandleMorningStarted;
            TimePhaseManager.Instance.OnDuskStarted += HandleDuskStarted;
            TimePhaseManager.Instance.OnNightStarted += HandleNightStarted;

            ApplyPhaseImmediate(TimePhaseManager.Instance.CurrentTimePhase);
        }
        else
        {
            ApplyConfigImmediate(morningConfig);
        }
    }

    private void OnDestroy()
    {
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= HandleMorningStarted;
            TimePhaseManager.Instance.OnDuskStarted -= HandleDuskStarted;
            TimePhaseManager.Instance.OnNightStarted -= HandleNightStarted;
        }
    }

    private void OnValidate()
    {
        // Strojenie kolorów na żywo: zmiana wartości w Inspectorze w Play Mode od razu przelicza bieżącą fazę.
        if (!Application.isPlaying || !isActiveAndEnabled || transitionCoroutine != null) return;
        if (TimePhaseManager.Instance != null)
            ApplyPhaseImmediate(TimePhaseManager.Instance.CurrentTimePhase);
    }

    private void HandleMorningStarted()
    {
        TransitionToConfig(morningConfig, transitionDuration);
    }

    private void HandleDuskStarted()
    {
        TransitionToConfig(duskConfig, transitionDuration);
    }

    private void HandleNightStarted()
    {
        TransitionToConfig(nightConfig, transitionDuration);
    }

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
            StopCoroutine(transitionCoroutine);

        if (gameObject.activeInHierarchy)
            transitionCoroutine = StartCoroutine(TransitionRoutine(currentConfig, target, duration));
        else
            ApplyConfigImmediate(target);
    }

    private IEnumerator TransitionRoutine(PhaseLightingConfig start, PhaseLightingConfig target, float duration)
    {
        float elapsed = 0f;
        Quaternion startRot = Quaternion.Euler(start.sunEulerAngles);
        Quaternion targetRot = Quaternion.Euler(target.sunEulerAngles);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);
                sunLight.color = Color.Lerp(start.sunColor, target.sunColor, smoothT);
                sunLight.intensity = Mathf.Lerp(start.sunIntensity, target.sunIntensity, smoothT);
            }

            RenderSettings.ambientSkyColor = Color.Lerp(start.ambientSkyColor, target.ambientSkyColor, smoothT);
            RenderSettings.ambientEquatorColor = Color.Lerp(start.ambientEquatorColor, target.ambientEquatorColor, smoothT);
            RenderSettings.ambientGroundColor = Color.Lerp(start.ambientGroundColor, target.ambientGroundColor, smoothT);

            RenderSettings.fogColor = Color.Lerp(start.fogColor, target.fogColor, smoothT);
            RenderSettings.fogDensity = Mathf.Lerp(start.fogDensity, target.fogDensity, smoothT);

            EnemyGlow.SetGlow(Mathf.Lerp(start.enemyGlow, target.enemyGlow, smoothT), enemyGlowColor);

            yield return null;
        }

        currentConfig = target;
        ApplyConfigValues(target);
        transitionCoroutine = null;
    }

    private void ApplyConfigValues(PhaseLightingConfig config)
    {
        if (sunLight != null)
        {
            sunLight.transform.rotation = Quaternion.Euler(config.sunEulerAngles);
            sunLight.color = config.sunColor;
            sunLight.intensity = config.sunIntensity;
        }

        RenderSettings.ambientSkyColor = config.ambientSkyColor;
        RenderSettings.ambientEquatorColor = config.ambientEquatorColor;
        RenderSettings.ambientGroundColor = config.ambientGroundColor;

        RenderSettings.fogColor = config.fogColor;
        RenderSettings.fogDensity = config.fogDensity;

        EnemyGlow.SetGlow(config.enemyGlow, enemyGlowColor);
    }
}
