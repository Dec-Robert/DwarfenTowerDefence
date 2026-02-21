using UnityEngine;

[CreateAssetMenu(menuName = "Tower Effects/Poison")]
public class PoisonEffect : TowerEffectSO
{
    public float damagePerTick = 2f;
    public int ticks = 5;
    public float interval = 1f;

    public override void ApplyEffect(EnemyStats target, float damageMultiplier)
    {
        // target.ApplyPoison(damagePerTick, ticks, interval);
        Debug.Log("Wrog zatruty!");
    }
}