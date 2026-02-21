using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GroundDamageZone : MonoBehaviour
{
    [SerializeField] private float damage;
    [SerializeField] private DamageType damageType;
    [SerializeField] private List<TowerEffectSO> effects;

    [SerializeField] private float armourPiercing; // Wartość od 0 do 100, reprezentuj�ca procent obra�e� ignoruj�cych pancerz
    [SerializeField] private float magicPiercing;  // Warto�� od 0 do 100, reprezentuj�ca procent obra�e� ignoruj�cych odporno�� magiczn�
    [SerializeField] private float criticalMultiplier;
    [SerializeField] private float criticalChance;

    [SerializeField] private float tickInterval;
    [SerializeField] private float duration;

    // Lista wrog�w aktualnie stoj�cych w strefie
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
        // Odliczanie do ticka (np. co 1 sekund�)
        tickTimer += Time.deltaTime;

        if (tickTimer >= tickInterval)
        {
            ApplyDamageToAll();
            tickTimer = 0f;
        }
    }

    void ApplyDamageToAll()
    {
        // Czy�cimy list� z "nulli" (wrog�w, kt�rzy zgin�li w mi�dzyczasie)
        enemiesInZone.RemoveAll(x => x == null);

        foreach (var enemy in enemiesInZone)
        {
            bool isCritical = Random.value < criticalChance / 100f;
            // Zadajemy obra�enia
            enemy.TakeDamage(damage, damageType,armourPiercing,magicPiercing,isCritical,criticalMultiplier);

            // Nak�adamy efekty (np. spowolnienie, podpalenie)
            if (effects != null)
            {
                foreach (var effect in effects) effect.ApplyEffect(enemy);
            }
        }
    }

    // --- DETEKCJA WROG�W ---

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