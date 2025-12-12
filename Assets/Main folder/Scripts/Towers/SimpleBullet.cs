using UnityEngine;
using System.Collections.Generic;

public class SimpleBullet : MonoBehaviour
{
    private Transform target;
    private float damage;
    private float speed = 20f;
    private List<TowerEffectSO> effectsToApply; // Lista efektów do na³o¿enia

    public void Seek(Transform _target, float _damage, List<TowerEffectSO> _effects)
    {
        target = _target;
        damage = _damage;
        effectsToApply = _effects; // Przekazujemy listê dalej
    }

    void Update()
    {
        if (target == null) { Destroy(gameObject); return; }

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
            // 1. Zadaj podstawowe obra¿enia
            enemy.TakeDamage(damage);

            // 2. Aplikuj wszystkie efekty specjalne (Lód, Ogieñ, Wybuch)
            if (effectsToApply != null)
            {
                foreach (var effect in effectsToApply)
                {
                    effect.ApplyEffect(enemy);
                }
            }
        }
        Destroy(gameObject);
    }
}