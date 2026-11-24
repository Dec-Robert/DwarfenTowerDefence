using UnityEngine;
using System.Collections.Generic;

public class GroundProjectile : ProjectileBase
{
    [Header("Statystyki Strefy")]
    public float damagePerTick = 10f;
    public float tickInterval = 1f;
    public float duration = 5f;
    public GameObject groundEffectPrefab; 
    public float zoneCritChance = 0f;

    [Header("Parametry Lotu")]
    public float speed = 10f;
    public float arcHeight = 2.0f;

    [Header("VFX Lotu")]
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private ParticleSystem flightParticles;
    [SerializeField] private bool detachVfxOnImpact = true;
    [SerializeField] private bool orientToFlightTangent = true;

    public TrailRenderer TrailRenderer => trailRenderer;
    public ParticleSystem FlightParticles => flightParticles;

    private Vector3 startPos;
    private Vector3 targetPos;
    private float progress = 0f;
    private bool launched = false;
    private bool vfxDetached = false;

    private void Awake()
    {
        if (trailRenderer == null)
        {
            trailRenderer = GetComponentInChildren<TrailRenderer>(true);
        }

        if (flightParticles == null)
        {
            flightParticles = GetComponentInChildren<ParticleSystem>(true);
        }
    }

    public override void Initialize(
        float _damage, 
        DamageType _type, 
        List<TowerEffectSO> _effects, 
        bool _isCritical, 
        float _criticalMultiplier, 
        float _armorPen = 0f,   
        float _magicPen = 0f,   
        float _critChance = 0f,
        TowerEntity _sourceTower = null)
    {
        base.Initialize(_damage, _type, _effects, _isCritical, _criticalMultiplier, _armorPen, _magicPen, _critChance,
            _sourceTower);
        zoneCritChance = _critChance;
    }
    
    public override void Launch(Transform _target, Vector3 _targetPos, Transform _firingPoint = null)
    {
        startPos = transform.position;
        targetPos = _targetPos;
        progress = 0f;
        launched = true;
        vfxDetached = false;

        Vector3 initialTangent = (targetPos - startPos);
        initialTangent.y += arcHeight * Mathf.PI;
        OrientAlongFlight(initialTangent);

        if (trailRenderer != null)
        {
            trailRenderer.Clear();
            trailRenderer.emitting = true;
        }

        if (flightParticles != null)
        {
            flightParticles.Clear(true);
            flightParticles.Play(true);
        }
    }

    void Update()
    {
        if (!launched) return;

        float totalDist = Vector3.Distance(startPos, targetPos);
        if (totalDist <= 0.1f)
        {
            transform.position = targetPos;
            SpawnZone();
            return;
        }

        float step = speed * Time.deltaTime;
        progress += step / totalDist;

        if (progress >= 1.0f)
        {
            transform.position = targetPos;
            SpawnZone();
            return;
        }

        Vector3 previousPos = transform.position;
        Vector3 currentPos = Vector3.Lerp(startPos, targetPos, progress);
        float height = Mathf.Sin(Mathf.PI * progress) * arcHeight;
        currentPos.y += height;

        transform.position = currentPos;
        OrientAlongFlight(currentPos - previousPos);
    }

    void SpawnZone()
    {
        DetachVfxOnImpact();

        if (groundEffectPrefab != null)
        {
            GameObject zoneObj = Instantiate(groundEffectPrefab, targetPos, Quaternion.identity);
            GroundDamageZone zone = zoneObj.GetComponent<GroundDamageZone>();
            if (zone != null)
            {
                // Używamy "damage" z klasy bazowej ProjectileBase
                zone.Setup(damage, damageType, effects, tickInterval, duration, armourPiercing, magicPiercing, criticalMultiplier, zoneCritChance, sourceTower);
            }
        }
        SafeDestroy(gameObject);
    }

    private void OrientAlongFlight(Vector3 tangent)
    {
        if (!orientToFlightTangent || tangent.sqrMagnitude <= 0.000001f) return;
        transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
    }

    private void DetachVfxOnImpact()
    {
        if (vfxDetached || !detachVfxOnImpact) return;
        vfxDetached = true;

        if (trailRenderer != null)
        {
            trailRenderer.transform.SetParent(null, true);
            trailRenderer.emitting = false;
            trailRenderer.autodestruct = true;
            float cleanupDelay = Mathf.Max(trailRenderer.time, 0.05f) + 0.1f;
            SafeDestroy(trailRenderer.gameObject, cleanupDelay);
        }

        if (flightParticles != null)
        {
            flightParticles.transform.SetParent(null, true);
            flightParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            float maxLife = flightParticles.main.startLifetime.constantMax;
            SafeDestroy(flightParticles.gameObject, Mathf.Max(maxLife, 0.1f) + 0.15f);
        }
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
}