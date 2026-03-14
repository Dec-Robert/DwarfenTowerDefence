using UnityEngine;

public abstract class EnemySkill : MonoBehaviour
{
    protected EnemyStats stats;
    protected EnemyWalker walker;

    public virtual void Initialize(EnemyStats _stats)
    {
        stats = _stats;
        walker = _stats.GetComponent<EnemyWalker>();
    }

    // Hooki (Metody wirtualne), kt�re wywo�a EnemyStats
    public virtual void OnDamageTaken(float amount, float currentHp) { }
    public virtual float OnBeforeDamageCalculation(float incomingDamage, DamageType type, TowerEntity sourceTower = null, bool isAoE = false) { return incomingDamage; }
    public virtual void OnDeath() { } //
}