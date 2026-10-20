using System.Collections;
using UnityEngine;

/// <summary>
/// Kontroler oświetlenia punktowego dla osady krasnoludów (Wielkie Palenisko, latarnie, wieże).
/// Obsługuje automatyczne zapalanie o zmierzchu/nocy (1s fade), proceduralne migotanie płomienia (Perlin noise)
/// oraz dynamiczne skalowanie zasięgu i natężenia w oparciu o poziom Wielkiego Paleniska (BeaconEntity).
/// </summary>
[RequireComponent(typeof(Light))]
public class SettlementLight : MonoBehaviour
{
    [Header("Referencje")]
    [SerializeField] private Light targetLight;

    [Header("Typ Źródła Światła")]
    [Tooltip("Czy to jest Wielkie Palenisko Twierdzy (Beacon of Hope), skalujące się z poziomem węgla?")]
    public bool isBeacon = false;

    [Header("Ustawienia Podstawowe")]
    public float baseIntensity = 2.0f;
    public float baseRange = 12.0f;
    public Color lightColor = new Color(1.0f, 0.68f, 0.38f, 1.0f); // Ciepły bursztyn ognia

    [Header("Cykl Dobowy")]
    [Tooltip("Czy światło ma się zapalać automatycznie o zmierzchu/nocy i gasnąć rano?")]
    public bool autoControlByPhase = true;
    [Tooltip("Minimalny mnożnik jasności za dnia (0 = całkowicie wygaszone, >0 = tlący się ogień)")]
    [Range(0f, 1f)] public float dayIntensityMultiplier = 0.05f;
    [Tooltip("Czas płynnego zapalania/gaszenia światła w sekundach")]
    public float transitionDuration = 1.0f;

    [Header("Proceduralne Migotanie (Flicker)")]
    public bool enableFlicker = true;
    public float flickerSpeed = 7.5f;
    [Range(0f, 1f)] public float flickerIntensityJitter = 0.22f;
    [Range(0f, 0.5f)] public float flickerRangeJitter = 0.08f;

    // Poziomy Wielkiego Paleniska (poziom 0 do 4+)
    private static readonly float[] BeaconLevelRanges = { 12f, 20f, 28f, 36f, 46f, 55f };
    private static readonly float[] BeaconLevelIntensities = { 1.0f, 2.0f, 3.0f, 4.2f, 5.5f, 6.5f };

    private float noiseOffset;
    private float targetPhaseMultiplier = 1.0f;
    private float currentPhaseMultiplier = 1.0f;
    private Coroutine transitionCoroutine;

    private void Awake()
    {
        if (targetLight == null)
            targetLight = GetComponent<Light>();

        noiseOffset = Random.Range(0f, 1000f);
        ApplyInitialBaseValues();
    }

    private void Start()
    {
        UpdateTargetStateFromPhase(false);

        if (isBeacon)
        {
            BeaconEntity.OnHearthStateChanged += HandleHearthStateChanged;
            ApplyBeaconLevelSettings(BeaconEntity.ActiveNightLevel);
        }

        SubscribeToTimePhaseManager();
    }

    private void OnDestroy()
    {
        if (isBeacon)
        {
            BeaconEntity.OnHearthStateChanged -= HandleHearthStateChanged;
        }

        UnsubscribeFromTimePhaseManager();
    }

    private void ApplyInitialBaseValues()
    {
        if (targetLight != null)
        {
            targetLight.color = lightColor;
            targetLight.intensity = baseIntensity;
            targetLight.range = baseRange;
        }
    }

    private void SubscribeToTimePhaseManager()
    {
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted += HandleMorningStarted;
            TimePhaseManager.Instance.OnDuskStarted += HandleDuskOrNightStarted;
            TimePhaseManager.Instance.OnNightStarted += HandleDuskOrNightStarted;
        }
    }

    private void UnsubscribeFromTimePhaseManager()
    {
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= HandleMorningStarted;
            TimePhaseManager.Instance.OnDuskStarted -= HandleDuskOrNightStarted;
            TimePhaseManager.Instance.OnNightStarted -= HandleDuskOrNightStarted;
        }
    }

    private void HandleMorningStarted()
    {
        if (!autoControlByPhase) return;
        SetPhaseTargetMultiplier(dayIntensityMultiplier);
    }

    private void HandleDuskOrNightStarted()
    {
        if (!autoControlByPhase) return;
        SetPhaseTargetMultiplier(1.0f);
    }

    private void UpdateTargetStateFromPhase(bool animate)
    {
        if (!autoControlByPhase)
        {
            currentPhaseMultiplier = 1.0f;
            targetPhaseMultiplier = 1.0f;
            return;
        }

        if (TimePhaseManager.Instance != null)
        {
            bool isDay = TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Morning;
            float target = isDay ? dayIntensityMultiplier : 1.0f;
            if (animate)
            {
                SetPhaseTargetMultiplier(target);
            }
            else
            {
                currentPhaseMultiplier = target;
                targetPhaseMultiplier = target;
            }
        }
    }

    private void SetPhaseTargetMultiplier(float target)
    {
        targetPhaseMultiplier = target;
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        if (gameObject.activeInHierarchy)
            transitionCoroutine = StartCoroutine(TransitionRoutine(targetPhaseMultiplier, transitionDuration));
        else
            currentPhaseMultiplier = targetPhaseMultiplier;
    }

    private IEnumerator TransitionRoutine(float target, float duration)
    {
        float start = currentPhaseMultiplier;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Płynna krzywa smoothstep dla naturalnego zapalania płomienia
            currentPhaseMultiplier = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        currentPhaseMultiplier = target;
        transitionCoroutine = null;
    }

    private void HandleHearthStateChanged()
    {
        if (isBeacon)
        {
            ApplyBeaconLevelSettings(BeaconEntity.ActiveNightLevel);
        }
    }

    public void ApplyBeaconLevelSettings(int level)
    {
        int clampedLevel = Mathf.Clamp(level, 0, BeaconLevelRanges.Length - 1);
        baseRange = BeaconLevelRanges[clampedLevel];
        baseIntensity = BeaconLevelIntensities[clampedLevel];

        if (targetLight != null)
        {
            targetLight.range = baseRange;
        }
    }

    private void Update()
    {
        if (targetLight == null || currentPhaseMultiplier <= 0.001f)
        {
            if (targetLight != null && targetLight.intensity > 0f && currentPhaseMultiplier <= 0.001f)
                targetLight.intensity = 0f;
            return;
        }

        float calculatedIntensity = baseIntensity * currentPhaseMultiplier;
        float calculatedRange = baseRange;

        if (enableFlicker)
        {
            float time = Time.time * flickerSpeed + noiseOffset;
            float noiseVal = Mathf.PerlinNoise(time, 0.5f); // Zakres [0, 1]
            float jitter = (noiseVal - 0.5f) * 2.0f;       // Zakres [-1, 1]

            calculatedIntensity += baseIntensity * (jitter * flickerIntensityJitter);
            calculatedRange += baseRange * (jitter * flickerRangeJitter);
        }

        targetLight.intensity = Mathf.Max(0f, calculatedIntensity);
        targetLight.range = Mathf.Max(0.5f, calculatedRange);
    }
}
