using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GroundDamageZone : MonoBehaviour
{
    [SerializeField] private float damage;
    [SerializeField] private DamageType damageType;
    [SerializeField] private List<TowerEffectSO> effects;

    [SerializeField] private float armourPiercing; // Wartoœæ od 0 do 100, reprezentuj¹ca procent obra¿eñ ignoruj¹cych pancerz
    [SerializeField] private float magicPiercing;  // Wartoœæ od 0 do 100, reprezentuj¹ca procent obra¿eñ ignoruj¹cych odpornoœæ magiczn¹
    [SerializeField] private float criticalMultiplier;
    [SerializeField] private float criticalChance;

    [SerializeField] private float tickInterval;
    [SerializeField] private float duration;

    // Lista wrogów aktualnie stoj¹cych w strefie
    private List<EnemyStats> enemiesInZone = new List<EnemyStats>();
    private float tickTimer = 0f;

    public void Setup(float _dmg, DamageType _type, List<TowerEffectSO> _fx, float _interval, float _dur, float _arPier, float _mgPier, float _crMult, float _crChan)
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

        // Autodestrukcja po czasie trwania
        Destroy(gameObject, duration);
    }

    void Update()
    {
        // Odliczanie do ticka (np. co 1 sekundê)
        tickTimer += Time.deltaTime;

        if (tickTimer >= tickInterval)
        {
            ApplyDamageToAll();
            tickTimer = 0f;
        }
    }

    void ApplyDamageToAll()
    {
        // Czyœcimy listê z "nulli" (wrogów, którzy zginêli w miêdzyczasie)
        enemiesInZone.RemoveAll(x => x == null);

        foreach (var enemy in enemiesInZone)
        {
            bool isCritical = Random.value < criticalChance / 100f;
            // Zadajemy obra¿enia
            enemy.TakeDamage(damage, damageType,armourPiercing,magicPiercing,isCritical,criticalMultiplier);

            // Nak³adamy efekty (np. spowolnienie, podpalenie)
            if (effects != null)
            {
                foreach (var effect in effects) effect.ApplyEffect(enemy);
            }
        }
    }

    // --- DETEKCJA WROGÓW ---

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