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

    private Vector3 startPos;
    private Vector3 targetPos;
    private float progress = 0f;
    private bool launched = false;


    public override void Initialize(float _damage, DamageType _type, List<TowerEffectSO> _effects, bool _isCritical, float _criticalMultiplier, float _critChance)
    {
        base.Initialize(_damage, _type, _effects, _isCritical, _criticalMultiplier, _critChance);
        zoneCritChance = _critChance;
    }
    
    public override void Launch(Transform _target, Vector3 _targetPos, Transform _firingPoint = null)
    {
        startPos = transform.position;
        targetPos = _targetPos;
        launched = true;
    }

    void Update()
    {
        if (!launched) return;

        float totalDist = Vector3.Distance(startPos, targetPos);
        if (totalDist <= 0.1f) { SpawnZone(); return; }

        float step = speed * Time.deltaTime;
        progress += step / totalDist;

        if (progress >= 1.0f)
        {
            SpawnZone();
            return;
        }

        Vector3 currentPos = Vector3.Lerp(startPos, targetPos, progress);
        float height = Mathf.Sin(Mathf.PI * progress) * arcHeight;
        currentPos.y += height;

        transform.position = currentPos;
    }

    void SpawnZone()
    {
        if (groundEffectPrefab != null)
        {
            GameObject zoneObj = Instantiate(groundEffectPrefab, targetPos, Quaternion.identity);
            GroundDamageZone zone = zoneObj.GetComponent<GroundDamageZone>();
            if (zone != null)
            {
                // Używamy "damage" z klasy bazowej ProjectileBase
                zone.Setup(damage, damageType, effects, tickInterval, duration, armourPiercing, magicPiercing, criticalMultiplier, zoneCritChance);
            }
        }
        Destroy(gameObject); 
    }
}