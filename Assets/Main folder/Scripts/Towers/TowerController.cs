using UnityEngine;

public class TowerController : MonoBehaviour
{
    [Header("Konfiguracja")]
    public TowerData towerData; // TU PRZYPISUJEMY NASZ PLIK DANYCH

    [Header("Opcjonalne (np. lufa)")]
    public Transform firePoint;

    private Transform target;
    private float fireCountdown = 0f;

    // Lokalne zmienne (opcjonalne, przydatne do buffów w trakcie gry)
    private float currentRange;
    private float currentFireRate;

    void Start()
    {
        // Pobieramy statystyki z Data na start
        if (towerData != null)
        {
            currentRange = towerData.baseRange;
            currentFireRate = towerData.fireRate;
        }
        else
        {
            Debug.LogError("Wie¿a nie ma przypisanego TowerData!");
        }

        InvokeRepeating("UpdateTarget", 0f, 0.5f);
    }

    void UpdateTarget()
    {
        // Szukanie celu (zoptymalizowane pod EnemyStats)
        EnemyStats[] enemies = FindObjectsOfType<EnemyStats>();
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

        if (nearestEnemy != null && shortestDistance <= currentRange)
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
            fireCountdown = 1f / currentFireRate;
        }

        fireCountdown -= Time.deltaTime;
    }

    void Shoot()
    {
        // Pobieramy prefab pocisku z DANYCH
        GameObject bulletToSpawn = towerData.bulletPrefab;

        if (bulletToSpawn == null) return;

        Vector3 spawnPos = (firePoint != null) ? firePoint.position : transform.position;
        GameObject bulletGO = Instantiate(bulletToSpawn, spawnPos, Quaternion.identity);

        SimpleBullet bullet = bulletGO.GetComponent<SimpleBullet>();
        if (bullet != null)
        {
            // Pobieramy obra¿enia z DANYCH
            bullet.Seek(target, towerData.baseDamage);
        }
    }

    void OnDrawGizmosSelected()
    {
        // Rysowanie zasiêgu w edytorze (pobierane z danych, jeœli s¹ przypisane)
        if (towerData != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, towerData.baseRange);
        }
    }
}