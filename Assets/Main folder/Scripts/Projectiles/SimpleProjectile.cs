using UnityEngine;

public class SimpleProjectile : ProjectileBase
{
    private Transform target;
    [SerializeField] private float speed = 20f;

    [Header("VFX Trail")]
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private bool detachTrailOnDestroy = true;
    [SerializeField] private bool orientToFlightDirection = true;
    [SerializeField] private bool useCriticalTrailColor = true;
    [SerializeField] private Gradient normalTrailGradient = CreateDefaultNormalGradient();
    [SerializeField] private Gradient criticalTrailGradient = CreateDefaultCriticalGradient();
    [SerializeField] private float criticalWidthMultiplier = 1.25f;

    private float baseTrailWidth = 0.07f;
    private bool hasCachedBaseWidth;
    private bool trailDetached;

    private void Reset()
    {
        trailRenderer = GetComponentInChildren<TrailRenderer>(true);
        normalTrailGradient = CreateDefaultNormalGradient();
        criticalTrailGradient = CreateDefaultCriticalGradient();
    }

    private void Awake()
    {
        if (trailRenderer == null)
        {
            trailRenderer = GetComponentInChildren<TrailRenderer>(true);
        }

        if (trailRenderer != null && !hasCachedBaseWidth)
        {
            baseTrailWidth = trailRenderer.widthMultiplier;
            hasCachedBaseWidth = true;
        }
    }

    public override void Launch(Transform _target, Vector3 _targetPos, Transform _firingPoint = null)
    {
        target = _target;
        trailDetached = false;

        if (target == null)
        {
            SafeDestroy(gameObject);
            return;
        }

        OrientTowards(target.position - transform.position);
        ConfigureTrail();
    }

    private void Update()
    {
        if (target == null)
        {
            DetachTrail();
            SafeDestroy(gameObject);
            return;
        }

        Vector3 dir = target.position - transform.position;
        float distanceThisFrame = speed * Time.deltaTime;

        OrientTowards(dir);

        if (dir.magnitude <= distanceThisFrame)
        {
            transform.position = target.position;
            HitTarget();
            return;
        }

        transform.Translate(dir.normalized * distanceThisFrame, Space.World);
    }

    private void HitTarget()
    {
        EnemyStats enemy = target.GetComponent<EnemyStats>();
        if (enemy != null) ApplyDamageAndEffects(enemy);

        DetachTrail();
        SafeDestroy(gameObject);
    }

    private void OrientTowards(Vector3 dir)
    {
        if (!orientToFlightDirection || dir.sqrMagnitude <= 0.000001f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private void ConfigureTrail()
    {
        if (trailRenderer == null) return;

        if (!hasCachedBaseWidth)
        {
            baseTrailWidth = trailRenderer.widthMultiplier;
            hasCachedBaseWidth = true;
        }

        if (normalTrailGradient == null || normalTrailGradient.colorKeys.Length == 0)
        {
            normalTrailGradient = CreateDefaultNormalGradient();
        }

        if (criticalTrailGradient == null || criticalTrailGradient.colorKeys.Length == 0)
        {
            criticalTrailGradient = CreateDefaultCriticalGradient();
        }

        bool applyCritical = useCriticalTrailColor && isCritical;
        trailRenderer.colorGradient = applyCritical ? criticalTrailGradient : normalTrailGradient;
        trailRenderer.widthMultiplier = applyCritical ? baseTrailWidth * criticalWidthMultiplier : baseTrailWidth;
        trailRenderer.Clear();
        trailRenderer.emitting = true;
    }

    private void DetachTrail()
    {
        if (trailDetached || trailRenderer == null || !detachTrailOnDestroy) return;

        trailDetached = true;
        trailRenderer.transform.SetParent(null, true);
        trailRenderer.emitting = false;
        trailRenderer.autodestruct = true;

        float cleanupDelay = Mathf.Max(trailRenderer.time, 0.05f) + 0.1f;
        SafeDestroy(trailRenderer.gameObject, cleanupDelay);
    }

    private static void SafeDestroy(Object obj, float delay = 0f)
    {
        if (obj == null) return;

        if (Application.isPlaying)
        {
            if (delay > 0f) Destroy(obj, delay);
            else Destroy(obj);
        }
        else if (delay <= 0f)
        {
            DestroyImmediate(obj);
        }
    }

    private static Gradient CreateDefaultNormalGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.92f, 0.90f, 0.84f), 0.0f),
                new GradientColorKey(new Color(0.68f, 0.66f, 0.60f), 0.45f),
                new GradientColorKey(new Color(0.35f, 0.34f, 0.31f), 1.0f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0.0f),
                new GradientAlphaKey(0.55f, 0.35f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        return gradient;
    }

    private static Gradient CreateDefaultCriticalGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1.0f, 0.94f, 0.71f), 0.0f),
                new GradientColorKey(new Color(0.94f, 0.71f, 0.24f), 0.45f),
                new GradientColorKey(new Color(0.71f, 0.43f, 0.08f), 1.0f)
            },
            new[]
            {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.58f, 0.4f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        return gradient;
    }
}
