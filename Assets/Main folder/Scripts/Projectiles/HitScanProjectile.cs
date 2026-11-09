using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class HitscanProjectile : ProjectileBase
{
    [Header("Wizualia - Czas i Fade")]
    [Tooltip("Czas trwania wyładowania w sekundach.")]
    public float visualDuration = 0.2f;

    [Tooltip("Prędkość przesuwania tekstury pioruna (jeśli materiał ją posiada).")]
    public float textureScrollSpeed = 20f;

    [Header("Geometria Pioruna (Low-Poly Zygzak)")]
    [Tooltip("Docelowa długość pojedynczego segmentu linii.")]
    public float segmentLength = 0.7f;

    [Tooltip("Minimalna liczba segmentów zygzaka.")]
    public int minSegments = 8;

    [Tooltip("Maksymalna liczba segmentów zygzaka.")]
    public int maxSegments = 24;

    [Tooltip("Maksymalna amplituda odchylenia zygzaka (agresywność załamań).")]
    public float chaosFactor = 0.55f;

    [Tooltip("Początkowa szerokość linii przy wylocie z iglicy.")]
    public float startWidth = 0.35f;

    [Tooltip("Końcowa szerokość linii przy trafieniu wroga.")]
    public float endWidth = 0.2f;

    [Header("Dynamika i Jitter (Drżenie)")]
    [Tooltip("Czy punkty zygzaka mają drżeć/przeskakiwać proceduralnie w trakcie trwania wyładowania?")]
    public bool enableJitter = true;

    [Tooltip("Odstęp czasu (w sekundach) pomiędzy kolejnymi przeskoczeniami drżenia.")]
    public float jitterInterval = 0.035f;

    [Header("Kolorystyka (Stylizowany Błękit i Biel)")]
    [Tooltip("Czy wymusić stylizowany gradient kolorów (błękit/cyjan z jasnym rdzeniem)?")]
    public bool overrideGradient = true;

    public Gradient lightningGradient;

    private LineRenderer lineRenderer;
    private Vector3[] points;
    private Vector3 cachedStart;
    private Vector3 cachedEnd;
    private Material runtimeMaterial;

    private void Reset()
    {
        SetupDefaultGradient();
    }

    private void SetupDefaultGradient()
    {
        lightningGradient = new Gradient();
        lightningGradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.1f, 0.85f, 1f), 0.0f),       // neon cyjan przy iglicy
                new GradientColorKey(new Color(0.92f, 0.98f, 1f), 0.45f),     // jasny biało-błękitny rdzeń
                new GradientColorKey(new Color(0.0f, 0.6f, 1f), 1.0f)        // głębszy błękit przy celu
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(1.0f, 0.75f),
                new GradientAlphaKey(0.8f, 1.0f)
            }
        );
    }

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.enabled = false;

        if (lightningGradient == null || lightningGradient.colorKeys.Length == 0)
        {
            SetupDefaultGradient();
        }

        // Fallback materiału, jeśli LineRenderer nie ma przypisanego materiału
        if (lineRenderer.sharedMaterial == null)
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (unlitShader != null)
            {
                runtimeMaterial = new Material(unlitShader);
                lineRenderer.material = runtimeMaterial;
            }
        }
    }

    public override void Launch(Transform _target, Vector3 _targetPos, Transform _firingPoint = null)
    {
        // Jeśli nie podano firingPoint, używamy własnej pozycji (instancja jest w lufie)
        Vector3 startPos = _firingPoint != null ? _firingPoint.position : transform.position;

        if (_target != null)
        {
            EnemyStats enemy = _target.GetComponent<EnemyStats>();
            Vector3 enemyPos = enemy != null ? enemy.transform.position : _targetPos;

            // Przenosimy obiekt do wroga (logic damage point)
            transform.position = enemyPos;

            // Konfiguracja wyglądu LineRenderera
            lineRenderer.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, startWidth);
            curve.AddKey(1f, endWidth);
            lineRenderer.widthCurve = curve;
            lineRenderer.widthMultiplier = 1f;

            if (overrideGradient && lightningGradient != null)
            {
                lineRenderer.colorGradient = lightningGradient;
            }

            // Generujemy zygzak pioruna
            GenerateLightningPath(startPos, enemyPos);

            if (enemy != null)
            {
                ApplyDamageAndEffects(enemy);
            }

            StartCoroutine(FadeAndDestroy());
        }
        else
        {
            // Hitscan musi mieć cel, żeby trafić. Jak nie ma, znika.
            Destroy(gameObject);
        }
    }

    private void GenerateLightningPath(Vector3 start, Vector3 end)
    {
        cachedStart = start;
        cachedEnd = end;

        Vector3 vector = end - start;
        float distance = vector.magnitude;
        if (distance < 0.05f)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
            return;
        }

        Vector3 direction = vector / distance;

        // Wyznaczenie płaszczyzny prostopadłej do wektora pioruna
        Vector3 perp1 = Vector3.Cross(direction, Vector3.up);
        if (perp1.sqrMagnitude < 0.001f)
        {
            perp1 = Vector3.Cross(direction, Vector3.right);
        }
        perp1.Normalize();
        Vector3 perp2 = Vector3.Cross(direction, perp1).normalized;

        int segCount = Mathf.Clamp(Mathf.RoundToInt(distance / segmentLength), minSegments, maxSegments);
        int pointCount = segCount + 1;

        if (points == null || points.Length != pointCount)
        {
            points = new Vector3[pointCount];
        }

        points[0] = start;
        points[pointCount - 1] = end;

        for (int i = 1; i < pointCount - 1; i++)
        {
            float t = (float)i / segCount;
            Vector3 basePoint = Vector3.Lerp(start, end, t);

            // Obwiednia sinusoidalna gwarantuje zbieżność w punktach start i end
            float envelope = Mathf.Sin(t * Mathf.PI);

            // Losowe odchylenie w płaszczyźnie prostopadłej do kierunku strzału
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float offsetMagnitude = Random.Range(-chaosFactor, chaosFactor) * envelope;
            Vector3 offset = (perp1 * Mathf.Cos(angle) + perp2 * Mathf.Sin(angle)) * offsetMagnitude;

            points[i] = basePoint + offset;
        }

        lineRenderer.positionCount = pointCount;
        lineRenderer.SetPositions(points);
    }

    private IEnumerator FadeAndDestroy()
    {
        float timer = 0f;
        float jitterTimer = 0f;
        float initialWidthMult = lineRenderer.widthMultiplier;

        while (timer < visualDuration)
        {
            float dt = Time.deltaTime;
            timer += dt;
            jitterTimer += dt;

            // Proceduralny jitter (drżenie zygzaka w klatkach)
            if (enableJitter && jitterTimer >= jitterInterval)
            {
                jitterTimer = 0f;
                GenerateLightningPath(cachedStart, cachedEnd);
            }

            // Przesuwanie tekstury (jeśli materiał posiada _MainTex)
            if (lineRenderer.sharedMaterial != null && lineRenderer.sharedMaterial.HasProperty("_MainTex"))
            {
                float offset = Time.time * textureScrollSpeed;
                lineRenderer.material.SetTextureOffset("_MainTex", new Vector2(offset, 0f));
            }

            // Płynny zanik grubości
            float progress = timer / visualDuration;
            lineRenderer.widthMultiplier = Mathf.Lerp(initialWidthMult, 0f, progress);

            yield return null;
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }
}
