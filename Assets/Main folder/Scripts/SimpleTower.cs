using UnityEngine;

public class SimpleTower : MonoBehaviour
{
    [Header("Ustawienia")]
    public float range = 3f;
    public float damage = 2f;
    public float fireRate = 1f; // Strza³y na sekundê

    [Header("Unity Setup")]
    public GameObject bulletPrefab;
    public Transform firePoint; // Punkt wylotu pocisku (opcjonalne, domyœlnie œrodek)

    private Transform target;
    private float fireCountdown = 0f;

    void Start()
    {
        // Uruchamiamy szukanie celu co 0.5 sekundy (optymalizacja)
        InvokeRepeating("UpdateTarget", 0f, 0.5f);
    }

    void UpdateTarget()
    {
        // ZnajdŸ wszystkich wrogów o tagu "Enemy" (pamiêtaj o ustawieniu tagu na prefabie wroga!)
        // LUB po prostu szukamy obiektów ze skryptem EnemyStats
        EnemyStats[] enemies = FindObjectsOfType<EnemyStats>(); // W wersji finalnej zmienimy to na bardziej wydajne rozwi¹zanie

        float shortestDistance = Mathf.Infinity;
        GameObject nearestEnemy = null;

        foreach (EnemyStats enemy in enemies)
        {
            float distanceToEnemy = Vector3.Distance(transform.position, enemy.transform.position);
            if (distanceToEnemy < shortestDistance)
            {
                shortestDistance = distanceToEnemy;
                nearestEnemy = enemy.gameObject;
            }
        }

        if (nearestEnemy != null && shortestDistance <= range)
        {
            target = nearestEnemy.transform;
        }
        else
        {
            target = null;
        }
    }

    void Update()
    {
        if (target == null) return;

        if (fireCountdown <= 0f)
        {
            Shoot();
            fireCountdown = 1f / fireRate;
        }

        fireCountdown -= Time.deltaTime;
    }

    void Shoot()
    {
        if (bulletPrefab == null) return;

        // U¿yj firePoint jeœli jest, w przeciwnym razie strzelaj ze œrodka wie¿y
        Vector3 spawnPos = (firePoint != null) ? firePoint.position : transform.position;

        GameObject bulletGO = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
        SimpleBullet bullet = bulletGO.GetComponent<SimpleBullet>();

        if (bullet != null)
        {
            bullet.Seek(target, damage);
        }
    }

    // Rysowanie zasiêgu w edytorze (dla u³atwienia debugowania)
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}