using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GroundDamageZone : MonoBehaviour
{
    [SerializeField] private float damage;
    [SerializeField] private DamageType damageType;
    [SerializeField] private List<TowerEffectSO> effects;

    [SerializeField] private float armourPiercing;
    [SerializeField] private float magicPiercing;
    [SerializeField] private float criticalMultiplier;
    [SerializeField] private float criticalChance;

    [SerializeField] private float tickInterval;
    [SerializeField] private float duration;

    [Header("VFX Strefy Podpalenia")]
    [SerializeField] private Renderer groundDiscRenderer;
    [SerializeField] private ParticleSystem impactBurstVfx;
    [SerializeField] private ParticleSystem zoneFlamesVfx;
    [SerializeField] private float igniteDuration = 0.2f;
    [SerializeField] private float fadeOutDuration = 0.7f;

    public Renderer GroundDiscRenderer => groundDiscRenderer;
    public ParticleSystem ImpactBurstVfx => impactBurstVfx;
    public ParticleSystem ZoneFlamesVfx => zoneFlamesVfx;
    public float CurrentPhaseFade => currentPhaseFade;

    private static readonly int PhaseFadeId = Shader.PropertyToID("_PhaseFade");

    private List<EnemyStats> enemiesInZone = new List<EnemyStats>();
    private float tickTimer = 0f;
    private TowerEntity sourceTower;
    private MaterialPropertyBlock propertyBlock;
    private Vector3 baseDiscLocalScale = Vector3.one;
    private bool hasCachedDiscScale;
    private float elapsedTime = 0f;
    private bool isSetup = false;
    private bool flamesStopped = false;
    private float currentPhaseFade = 1f;

    private void Awake()
    {
        if (groundDiscRenderer != null && !hasCachedDiscScale)
        {
            baseDiscLocalScale = groundDiscRenderer.transform.localScale;
            hasCachedDiscScale = true;
        }
    }

    public void Setup(float _dmg, DamageType _type, List<TowerEffectSO> _fx, float _interval, float _dur, float _arPier, float _mgPier, float _crMult, float _crChan, TowerEntity _sourceTower = null)
    {
        damage = _dmg;
        damageType = _type;
        effects = _fx;
        tickInterval = _interval;
        duration = _dur;
        armourPiercing = _arPier;
        magicPiercing = _mgPier;
        criticalMultiplier = _crMult;
        criticalChance = _crChan;
        sourceTower = _sourceTower;

        elapsedTime = 0f;
        isSetup = true;
        flamesStopped = false;

        if (impactBurstVfx != null)
        {
            impactBurstVfx.Clear(true);
            impactBurstVfx.Play(true);
        }

        if (zoneFlamesVfx != null)
        {
            zoneFlamesVfx.Clear(true);
            zoneFlamesVfx.Play(true);
        }

        ApplyVisualLifecycle(0f);

        if (Application.isPlaying)
        {
            Destroy(gameObject, duration);
        }
    }

    void Update()
    {
        tickTimer += Time.deltaTime;

        if (tickTimer >= tickInterval)
        {
            ApplyDamageToAll();
            tickTimer = 0f;
        }

        AdvanceVisualLifecycle(Time.deltaTime);
    }

    public void AdvanceVisualLifecycle(float deltaTime)
    {
        if (!isSetup || duration <= 0f) return;

        elapsedTime += Mathf.Max(0f, deltaTime);
        ApplyVisualLifecycle(elapsedTime);

        float effectiveFadeOut = Mathf.Clamp(fadeOutDuration, 0f, duration);
        float fadeStart = duration - effectiveFadeOut;
        if (!flamesStopped && elapsedTime >= fadeStart)
        {
            flamesStopped = true;
            if (zoneFlamesVfx != null)
            {
                zoneFlamesVfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    private void ApplyVisualLifecycle(float time)
    {
        float igniteFactor = igniteDuration > 0.001f ? Mathf.Clamp01(time / igniteDuration) : 1f;
        float effectiveFadeOut = Mathf.Clamp(fadeOutDuration, 0f, duration);
        float fadeStart = duration - effectiveFadeOut;
        float fadeOutFactor = 1f;
        if (effectiveFadeOut > 0.001f && time >= fadeStart)
        {
            fadeOutFactor = 1f - Mathf.Clamp01((time - fadeStart) / effectiveFadeOut);
        }

        currentPhaseFade = Mathf.Clamp01(Mathf.Lerp(0.2f, 1f, igniteFactor) * fadeOutFactor);

        if (groundDiscRenderer != null)
        {
            if (!hasCachedDiscScale)
            {
                baseDiscLocalScale = groundDiscRenderer.transform.localScale;
                hasCachedDiscScale = true;
            }

            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            groundDiscRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(PhaseFadeId, currentPhaseFade);
            groundDiscRenderer.SetPropertyBlock(propertyBlock);

            float scaleMult = Mathf.Lerp(0.65f, 1.0f, igniteFactor);
            groundDiscRenderer.transform.localScale = baseDiscLocalScale * scaleMult;
        }
    }

    void ApplyDamageToAll()
    {
        enemiesInZone.RemoveAll(x => x == null);

        float effectiveCritChance = criticalChance > 1f ? criticalChance / 100f : criticalChance;

        foreach (var enemy in enemiesInZone)
        {
            bool isCritical = Random.value < effectiveCritChance;
            enemy.TakeDamage(damage, damageType, armourPiercing, magicPiercing, isCritical, criticalMultiplier, sourceTower, true);

            if (effects != null)
            {
                foreach (var effect in effects) effect.ApplyEffect(enemy);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            EnemyStats enemy = other.GetComponent<EnemyStats>();
            if (enemy != null && !enemiesInZone.Contains(enemy))
            {
                enemiesInZone.Add(enemy);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            EnemyStats enemy = other.GetComponent<EnemyStats>();
            if (enemy != null && enemiesInZone.Contains(enemy))
            {
                enemiesInZone.Remove(enemy);
            }
        }
    }
}