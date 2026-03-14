using UnityEngine;
using System.Collections.Generic;

public abstract class ProjectileBase : MonoBehaviour
{
    protected float damage;
    protected float armourPiercing;     // Wartości procentowa przebicia pancerze 0-100%
    protected float magicPiercing;      // Wartości procentowa przebicia pancerze 0-100%
    protected float criticalMultiplier;
    protected bool isCritical;


    protected DamageType damageType;
    protected List<TowerEffectSO> effects;
    protected TowerEntity sourceTower;

    // Wspólna metoda inicjalizacji statystyk
// Zaktualizowana sygnatura z Armor/Magic Pen
    public virtual void Initialize(
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
        damage = _damage;
        damageType = _type;
        effects = _effects;
        isCritical = _isCritical;
        criticalMultiplier = _criticalMultiplier;
        armourPiercing = _armorPen;
        magicPiercing = _magicPen;
        sourceTower = _sourceTower;

    }

    public abstract void Launch(Transform target, Vector3 targetPos, Transform firingPoint = null);
    
    
    protected void ApplyDamageAndEffects(EnemyStats enemy)
    {
        if (enemy == null) return;
        // Przekazujemy nadawcę (sourceTower) oraz flagę czy pocisk jest AoE do EnemyStats
        bool isAoE = this is GroundProjectile; // Z Twoich klas wynika, że Ground to pocisk obszarowy
        enemy.TakeDamage(damage, damageType, armourPiercing, magicPiercing, isCritical, criticalMultiplier, sourceTower, isAoE);

        if (effects != null)
        {
            foreach (var effect in effects) effect.ApplyEffect(enemy);
        }
    }
}