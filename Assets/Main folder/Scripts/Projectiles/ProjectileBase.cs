using UnityEngine;
using System.Collections.Generic;

public abstract class ProjectileBase : MonoBehaviour
{
    protected float damage;
    protected float armourPiercing; // Wartoœæ od 0 do 100, reprezentuj¹ca procent obra¿eñ ignoruj¹cych pancerz
    protected float magicPiercing;  // Wartoœæ od 0 do 100, reprezentuj¹ca procent obra¿eñ ignoruj¹cych odpornoœæ magiczn¹
    protected float criticalMultiplier;
    protected bool isCritical;


    protected DamageType damageType;
    protected List<TowerEffectSO> effects;

    // Wspólna metoda inicjalizacji dla wszystkich pocisków
    public virtual void Initialize(float _damage, DamageType _type, List<TowerEffectSO> _effects, bool _isCritical, float _criticalMultiplier, float _critChance = 0f)
    {
        damage = _damage;
        damageType = _type;
        effects = _effects;
        isCritical = _isCritical;
        criticalMultiplier = _criticalMultiplier;
    }


    protected void ApplyDamageAndEffects(EnemyStats enemy)
    {
        if (enemy == null) return;

        enemy.TakeDamage(damage, damageType, armourPiercing,magicPiercing, isCritical, criticalMultiplier);

        if (effects != null)
        {
            foreach (var effect in effects)
            {
                effect.ApplyEffect(enemy);
            }
        }
    }
}