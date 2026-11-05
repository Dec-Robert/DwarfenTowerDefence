using UnityEngine;

[CreateAssetMenu(fileName = "NewSplashEffect", menuName = "Game/Towers/Effects/Splash Damage Effect")]
public class SplashEffect : TowerEffectSO
{
    public float explosionRadius = 3f;
    public float splashDamage = 5f;
    public DamageType damageType = DamageType.Physical;

    public override void ApplyEffect(EnemyStats target, float damageMultiplier)
    {
//
    }
}