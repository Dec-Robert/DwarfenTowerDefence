using UnityEngine;
using System.Collections.Generic;

public class SimpleProjectile : ProjectileBase
{
    private Transform target;
    [SerializeField] private float speed = 20f; 

    public override void Launch(Transform _target, Vector3 _targetPos, Transform _firingPoint = null)
    {
        target = _target;
        if (target == null) Destroy(gameObject); 

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
        if (enemy != null) ApplyDamageAndEffects(enemy);
        Destroy(gameObject);
    }
}