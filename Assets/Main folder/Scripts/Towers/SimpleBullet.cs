using UnityEngine;
using System.Collections.Generic;

public class SimpleBullet : MonoBehaviour
{
    private Transform target;
    private float damage;
    private DamageType damageType;          // Typ (Fizyczne/Magiczne)
    private List<TowerEffectSO> effects;    // Efekty (Slow, Poison)
    private float speed = 20f;

    // ZMIENIONA METODA SEEK (4 argumenty)
    public void Seek(Transform _target, float _damage, DamageType _type, List<TowerEffectSO> _effects)
    {
        target = _target;
        damage = _damage;
        damageType = _type;
        effects = _effects;
    }

    void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 dir = target.position - transform.position;
        float distanceThisFrame = speed * Time.deltaTime;

        if (dir.magnitude <= distanceThisFrame)
        {
            HitTarget();
            return;
        }

        transform.Translate(dir.normalized * distanceThisFrame, Space.World);
    }

    void HitTarget()
    {
        EnemyStats enemy = target.GetComponent<EnemyStats>();
        if (enemy != null)
        {
            // 1. Zadaj obra¿enia z uwzglêdnieniem typu (Pancerz/Odpornoœæ)
            enemy.TakeDamage(damage, damageType);

            // 2. Na³ó¿ efekty specjalne
            if (effects != null)
            {
                foreach (var effect in effects)
                {
                    effect.ApplyEffect(enemy);
                }
            }
        }
        Destroy(gameObject);
    }
}