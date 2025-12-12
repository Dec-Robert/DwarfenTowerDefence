using UnityEngine;

[CreateAssetMenu(menuName = "Tower Effects/Slow")]
public class SlowEffect : TowerEffectSO
{
    public float slowAmount = 0.5f; // 50% prêdkoœci
    public float duration = 2.0f;

    public override void ApplyEffect(EnemyStats target, float damageMultiplier)
    {
        // Zak³adam, ¿e w EnemyStats lub EnemyWalker dodasz metodê ApplySlow
        // target.GetComponent<EnemyWalker>().ApplySlow(slowAmount, duration);
        Debug.Log($"{target.name} zosta³ spowolniony!");
    }
}