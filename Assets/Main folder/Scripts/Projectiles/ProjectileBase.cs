using UnityEngine;
using System.Collections.Generic;

public abstract class ProjectileBase : MonoBehaviour
{
    protected float damage;
    protected DamageType damageType;
    protected List<TowerEffectSO> effects;

    // Wspólna metoda inicjalizacji dla wszystkich pocisków
    public virtual void Initialize(float _damage, DamageType _type, List<TowerEffectSO> _effects)
    {
        damage = _damage;
        damageType = _type;
        effects = _effects;
    }

    protected void ApplyDamageAndEffects(EnemyStats enemy)
    {
        if (enemy == null) return;

        enemy.TakeDamage(damage, damageType);

        if (effects != null)
        {
            foreach (var effect in effects)
            {
                effect.ApplyEffect(enemy);
            }
        }
    }
}