using UnityEngine;

public class SimpleBullet : MonoBehaviour
{
    private Transform target;
    private float damage;
    private float speed = 10f;

    public void Seek(Transform _target, float _damage)
    {
        target = _target;
        damage = _damage;
    }

    void Update()
    {
        if (target == null)
        {
            // Cel zgin¹³ zanim pocisk dolecia³
            Destroy(gameObject);
            return;
        }

        Vector3 dir = target.position - transform.position;
        float distanceThisFrame = speed * Time.deltaTime;

        // Jeœli jesteœmy wystarczaj¹co blisko, trafiamy
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
            enemy.TakeDamage(damage);
        }
        Destroy(gameObject); // Niszczymy pocisk
    }
}