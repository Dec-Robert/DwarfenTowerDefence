using UnityEngine;

[RequireComponent(typeof(TowerEntity))]
public class TowerController : MonoBehaviour
{
    [Header("Wizualizacja Zasiêgu")]
    public GameObject rangeIndicatorPrefab; // Ten sam prefab co w InteractionManager
    private GameObject rangeIndicatorInstance;

    [Header("Konfiguracja")]
    public TowerData towerData;
    public Transform firePoint;

    // NOWE: Referencja do czêœci wie¿y, która ma siê obracaæ (np. lufa/g³owica)
    [Tooltip("Obiekt, który bêdzie obracaæ siê w stronê wroga (np. góra wie¿y)")]
    public Transform partToRotate;
    public float turnSpeed = 10f; // Prêdkoœæ obracania

    [Header("Status (Read Only)")]
    [SerializeField] private bool canShoot = false;
    [SerializeField] private float currentRange;
    [SerializeField] private float currentDamage;
    [SerializeField] private float currentFireRate;

    private Transform target;
    private float fireCountdown = 0f;

    private TowerEntity towerEntity;

    void Start()
    {
        towerEntity = GetComponent<TowerEntity>();

        if (towerData != null)
        {
            currentRange = towerData.baseRange;
            currentDamage = towerData.baseDamage;
            currentFireRate = towerData.fireRate;
        }

        InvokeRepeating("UpdateTarget", 0f, 0.5f);
    }

    public void UpdateCombatStats(float efficiency, float rangeMod, float damageMod, float fireRateMod, bool isActive)
    {
        canShoot = isActive;

        if (towerData != null)
        {
            currentRange = towerData.baseRange * rangeMod;
            currentDamage = towerData.baseDamage * damageMod * efficiency;
            currentFireRate = towerData.fireRate * fireRateMod * efficiency;

            // AKTUALIZACJA WIZUALNA (jeœli wskaŸnik jest w³¹czony)
            if (rangeIndicatorInstance != null && rangeIndicatorInstance.activeSelf)
            {
                float scale = currentRange * 2f;
                rangeIndicatorInstance.transform.localScale = new Vector3(scale, 1f, scale);
            }
        }
    }

    void UpdateTarget()
    {
        if (!canShoot)
        {
            target = null;
            return;
        }

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
        // Jeœli nieaktywna -> nic nie rób
        if (!canShoot) return;

        // Jeœli mamy cel -> obracaj g³owicê wie¿y
        if (target != null)
        {
            LockOnTarget();
        }

        // Logika strza³u
        if (target != null && fireCountdown <= 0f)
        {
            Shoot();
            if (currentFireRate > 0)
                fireCountdown = 1f / currentFireRate;
            else
                fireCountdown = 999f;
        }

        fireCountdown -= Time.deltaTime;
    }

    // NOWA METODA: Obracanie wie¿y
    void LockOnTarget()
    {
        if (partToRotate == null) return;

        // Wyliczamy kierunek do celu
        Vector3 dir = target.position - transform.position;

        // Tworzymy rotacjê (interesuje nas g³ównie obrót wokó³ osi Y)
        Quaternion lookRotation = Quaternion.LookRotation(dir);

        // P³ynne przejœcie (Lerp/Slerp) do celu. 
        // Nawet przy du¿ym smoothing wie¿a "zd¹¿y" przed strza³em, bo UpdateTarget 
        // reaguje natychmiast na zmianê celu.
        Vector3 rotation = Quaternion.Lerp(partToRotate.rotation, lookRotation, Time.deltaTime * turnSpeed).eulerAngles;

        // Zastosowanie rotacji (blokujemy X i Z, ¿eby wie¿a nie "buja³a siê" góra-dó³, 
        // chyba ¿e modele tego wymagaj¹ - wtedy usuñ .y i przeka¿ ca³e rotation)
        partToRotate.rotation = Quaternion.Euler(0f, rotation.y, 0f);
    }

    public void ShowRangeIndicator(bool show)
    {
        if (show)
        {
            if (rangeIndicatorInstance == null && rangeIndicatorPrefab != null)
            {
                rangeIndicatorInstance = Instantiate(rangeIndicatorPrefab, transform);
                rangeIndicatorInstance.transform.localPosition = new Vector3(0, 0.15f, 0); // Lekko nad ziemi¹
            }

            if (rangeIndicatorInstance != null)
            {
                rangeIndicatorInstance.SetActive(true);
                // Skalujemy na podstawie AKTUALNEGO zasiêgu (currentRange)
                float scale = currentRange * 2f;
                rangeIndicatorInstance.transform.localScale = new Vector3(scale, 1f, scale);
            }
        }
        else
        {
            if (rangeIndicatorInstance != null)
            {
                rangeIndicatorInstance.SetActive(false);
            }
        }
    }

    void Shoot()
    {
        TowerData tData = towerData as TowerData;
        if (tData == null || tData.bulletPrefab == null) return;

        Vector3 spawnPos = (firePoint != null) ? firePoint.position : transform.position;

        // Pocisk spawnuje siê z rotacj¹ lufy (kluczowe dla LinearProjectile!)
        GameObject bulletGO = Instantiate(tData.bulletPrefab, spawnPos, firePoint != null ? firePoint.rotation : transform.rotation);

        // Pobieramy komponent bazowy
        ProjectileBase projectile = bulletGO.GetComponent<ProjectileBase>();

        if (projectile != null)
        {
            // 1. Inicjalizujemy wspólne dane (Dmg, Typ, Efekty)
            projectile.Initialize(currentDamage, tData.damageType, tData.effects);

            // 2. Jeœli to pocisk naprowadzany (SimpleBullet), podajemy mu cel
            if (projectile is SimpleBullet simple)
            {
                simple.Seek(target);
            }
            // Jeœli to LinearProjectile, nie robimy nic wiêcej – on leci sam w kierunku rotacji lufy
        }

        if (towerEntity != null) towerEntity.RegisterShot();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = canShoot ? Color.cyan : Color.red;
        Gizmos.DrawWireSphere(transform.position, currentRange > 0 ? currentRange : (towerData ? towerData.baseRange : 0));
    }

    public float GetCurrentDamage() => currentDamage;
    public float GetBaseDamage() => towerData != null ? towerData.baseDamage : 0;
    public float GetCurrentRange() => currentRange;
    public float GetBaseRange() => towerData != null ? towerData.baseRange : 0;
    public float GetCurrentFireRate() => currentFireRate;
    public float GetBaseFireRate() => towerData != null ? towerData.fireRate : 0;
}