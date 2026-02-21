using UnityEngine;

// Abstrakcyjna baza - nic nie robi, ale definiuje kszta�t efektu
public abstract class TowerEffectSO : ScriptableObject
{
    public string effectName;

    // T� metod� wywo�amy, gdy pocisk trafi wroga
    public abstract void ApplyEffect(EnemyStats target, float damageMultiplier = 1f);
}//