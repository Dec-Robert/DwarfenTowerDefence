using UnityEngine;

public class TowerBeamWeapon : MonoBehaviour
{
    [Header("Parametry Promienia")]
    [SerializeField] private float baseTicksPerSecond = 10f;
    [SerializeField] private float startDamageMultiplier = 0.10f;
    [SerializeField] private int rampTo100Ticks = 10;
    [SerializeField] private float extraMultiplierPerTick = 0.02f;
    [SerializeField] private float maxDamageMultiplier = 2.50f;

    [Header("Spowolnienie Celu")]
    [SerializeField] private float slowPercent = 0.35f;
    [SerializeField] private float slowRefreshDuration = 0.25f;

    [Header("Wizualia Promienia")]
    [SerializeField] private LineRenderer beamLine;
    [SerializeField] private ParticleSystem originVfx;
    [SerializeField] private ParticleSystem impactVfx;
    [SerializeField] private float targetHeightOffset = 0.45f;
    [SerializeField] private float baseBeamWidth = 0.22f;
    [SerializeField] private float maxOverchargeWidthMultiplier = 1.50f;
    [SerializeField] private float minImpactVfxScale = 0.60f;
    [SerializeField] private float maxImpactVfxScale = 1.25f;
    [SerializeField] private float hitFlashInterval = 0.25f;

    private static readonly int ChargeId = Shader.PropertyToID("_Charge01");

    private Transform currentTarget;
    private EnemyStats cachedEnemy;
    private EnemyWalker cachedWalker;
    private int currentTickCount;
    private float tickAccumulator;
    private float hitFlashTimer;
    private bool isBeaming;
    private MaterialPropertyBlock propertyBlock;

    public Transform CurrentTarget => currentTarget;
    public int CurrentTickCount => currentTickCount;
    public float CurrentDamageMultiplier => currentTickCount > 0 ? GetMultiplierForTick(currentTickCount) : startDamageMultiplier;
    public float CurrentCharge01 => ComputeCharge01();
    public bool IsBeaming => isBeaming;
    public LineRenderer BeamLine => beamLine;
    public ParticleSystem OriginVfx => originVfx;
    public ParticleSystem ImpactVfx => impactVfx;

    private void Awake()
    {
        if (beamLine == null)
        {
            beamLine = GetComponentInChildren<LineRenderer>(true);
        }

        if (beamLine != null)
        {
            beamLine.useWorldSpace = true;
            beamLine.enabled = false;
        }
    }

    private void OnDisable()
    {
        StopBeam();
    }

    public float GetMultiplierForTick(int tickNumber)
    {
        if (tickNumber <= 1) return startDamageMultiplier;

        if (tickNumber <= rampTo100Ticks)
        {
            float t = (float)(tickNumber - 1) / Mathf.Max(1, rampTo100Ticks - 1);
            return Mathf.Lerp(startDamageMultiplier, 1f, t);
        }

        float extra = (tickNumber - rampTo100Ticks) * extraMultiplierPerTick;
        return Mathf.Min(maxDamageMultiplier, 1f + extra);
    }

    public float GetBeamWidthForMultiplier(float damageMultiplier)
    {
        if (damageMultiplier <= 1f)
        {
            float clamped = Mathf.Clamp(damageMultiplier, startDamageMultiplier, 1f);
            return baseBeamWidth * clamped;
        }

        float overchargeDenom = Mathf.Max(0.01f, maxDamageMultiplier - 1f);
        float overchargeT = Mathf.Clamp01((damageMultiplier - 1f) / overchargeDenom);
        return baseBeamWidth * Mathf.Lerp(1f, maxOverchargeWidthMultiplier, overchargeT);
    }

    public void TickBeam(Transform target, TowerStats stats, TowerEntity sourceTower, Transform originTransform, float deltaTime)
    {
        if (target == null)
        {
            StopBeam();
            return;
        }

        if (target != currentTarget)
        {
            ResetRampState();
            currentTarget = target;
            cachedEnemy = target.GetComponent<EnemyStats>();
            cachedWalker = target.GetComponent<EnemyWalker>();
        }

        if (cachedEnemy == null || cachedEnemy.IsDead)
        {
            StopBeam();
            return;
        }

        isBeaming = true;

        if (cachedWalker != null)
        {
            cachedWalker.ApplyTowerSlow(slowPercent, slowRefreshDuration);
        }

        UpdateBeamVisuals(originTransform, target);

        float effectiveRate = Mathf.Max(0.1f, stats.fireRate);
        float dt = Mathf.Max(0f, deltaTime);
        tickAccumulator += dt * effectiveRate;
        hitFlashTimer += dt;

        while (tickAccumulator >= 1f)
        {
            tickAccumulator -= 1f;

            if (cachedEnemy == null || cachedEnemy.IsDead)
            {
                StopBeam();
                return;
            }

            currentTickCount++;
            float mult = GetMultiplierForTick(currentTickCount);
            float baseTickDamage = stats.damage / Mathf.Max(1f, baseTicksPerSecond);
            float tickDamage = baseTickDamage * mult;
            bool isCritical = Random.value < stats.criticalChancel;

            bool shouldFlash = hitFlashTimer >= hitFlashInterval;
            if (shouldFlash)
            {
                hitFlashTimer = 0f;
            }

            cachedEnemy.TakeDamage(
                tickDamage,
                stats.damageType,
                stats.armorPenetration,
                stats.magicPenetration,
                isCritical,
                stats.criticalDamageMultiplier,
                sourceTower,
                false,
                DeathSource.Combat,
                shouldFlash
            );

            sourceTower?.MarkAsUsed();

            if (cachedEnemy == null || cachedEnemy.IsDead)
            {
                StopBeam();
                return;
            }
        }

        UpdateBeamVisuals(originTransform, target);
    }

    public void StopBeam()
    {
        isBeaming = false;
        ResetRampState();

        if (beamLine != null)
        {
            beamLine.enabled = false;
        }

        StopParticleEmitting(originVfx);
        StopParticleEmitting(impactVfx);
    }

    private void ResetRampState()
    {
        currentTarget = null;
        cachedEnemy = null;
        cachedWalker = null;
        currentTickCount = 0;
        tickAccumulator = 0f;
        hitFlashTimer = hitFlashInterval;
    }

    private float ComputeCharge01()
    {
        if (currentTickCount <= 0) return 0f;
        float denom = Mathf.Max(0.01f, maxDamageMultiplier - startDamageMultiplier);
        return Mathf.Clamp01((CurrentDamageMultiplier - startDamageMultiplier) / denom);
    }

    private void UpdateBeamVisuals(Transform originTransform, Transform target)
    {
        Vector3 startPos = originTransform != null ? originTransform.position : transform.position;
        Vector3 endPos = target.position + Vector3.up * targetHeightOffset;
        float charge01 = ComputeCharge01();

        if (beamLine != null)
        {
            beamLine.enabled = true;
            beamLine.positionCount = 2;
            beamLine.SetPosition(0, startPos);
            beamLine.SetPosition(1, endPos);

            float width = GetBeamWidthForMultiplier(CurrentDamageMultiplier);
            beamLine.widthMultiplier = width;

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }
            beamLine.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(ChargeId, charge01);
            beamLine.SetPropertyBlock(propertyBlock);
        }

        if (originVfx != null)
        {
            originVfx.transform.position = startPos;
            if (!originVfx.isPlaying)
            {
                originVfx.Play(true);
            }
        }

        if (impactVfx != null)
        {
            impactVfx.transform.position = endPos;
            impactVfx.transform.localScale = Vector3.one * Mathf.Lerp(minImpactVfxScale, maxImpactVfxScale, charge01);
            if (!impactVfx.isPlaying)
            {
                impactVfx.Play(true);
            }
        }
    }

    private static void StopParticleEmitting(ParticleSystem ps)
    {
        if (ps != null && ps.isEmitting)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
