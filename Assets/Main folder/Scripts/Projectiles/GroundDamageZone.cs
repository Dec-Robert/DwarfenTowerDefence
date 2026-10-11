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

    private List<EnemyStats> enemiesInZone = new List<EnemyStats>();
    private float tickTimer = 0f;
    private TowerEntity sourceTower;

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

        Destroy(gameObject, duration);
    }

    void Update()
    {
        tickTimer += Time.deltaTime;

        if (tickTimer >= tickInterval)
        {
            ApplyDamageToAll();
            tickTimer = 0f;
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