using UnityEngine;

// Abstrakcyjna baza - nic nie robi, ale definiuje kszta³t efektu
public abstract class TowerEffectSO : ScriptableObject
{
    public string effectName;

    // Tê metodê wywo³amy, gdy pocisk trafi wroga
    public abstract void ApplyEffect(EnemyStats target, float damageMultiplier = 1f);
}