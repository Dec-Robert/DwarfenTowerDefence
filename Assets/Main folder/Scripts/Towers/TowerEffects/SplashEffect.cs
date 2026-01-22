using UnityEngine;

[CreateAssetMenu(menuName = "Tower Effects/Splash Damage")]
public class SplashEffect : TowerEffectSO
{
    public float explosionRadius = 3f;
    public float splashDamage = 5f;
    public DamageType damageType = DamageType.Physical;

    public override void ApplyEffect(EnemyStats target, float damageMultiplier)
    {
        // Pobieramy wszystkich wrogów w promieniu
        Collider[] colliders = Physics.OverlapSphere(target.transform.position, explosionRadius);
        foreach (var col in colliders)
        {
            EnemyStats enemy = col.GetComponent<EnemyStats>();
            if (enemy != null && enemy != target) // G³ówny cel ju¿ dosta³ dmg od pocisku
            {
                enemy.TakeDamage(splashDamage * damageMultiplier, damageType);
            }
        }
        Debug.Log("BOOM! Obra¿enia obszarowe.");
    }
}