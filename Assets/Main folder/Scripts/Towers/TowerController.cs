using UnityEngine;

public class TowerController : MonoBehaviour
{
    [Header("Konfiguracja")]
    public TowerData towerData; // Dane bazowe (ScriptableObject)
    public Transform firePoint;

    [Header("Status (Read Only)")]
    [SerializeField] private bool canShoot = false;
    [SerializeField] private float currentRange;
    [SerializeField] private float currentDamage;
    [SerializeField] private float currentFireRate;

    private Transform target;
    private float fireCountdown = 0f;

    // Referencja zwrotna do logiki ekonomicznej
    private TowerEntity towerEntity;

    void Start()
    {
        towerEntity = GetComponent<TowerEntity>();

        // Inicjalizacja wstêpna (¿eby nie by³o zer, zanim Entity przeliczy)
        if (towerData != null)
        {
            currentRange = towerData.baseRange;
            currentDamage = towerData.baseDamage;
            currentFireRate = towerData.fireRate;
        }

        // Szukanie celu co 0.5s (optymalizacja)
        InvokeRepeating("UpdateTarget", 0f, 0.5f);
    }

    // --- KOMUNIKACJA Z TOWER ENTITY ---

    /// <summary>
    /// Metoda wywo³ywana przez TowerEntity, gdy zmieni siê obsada, pora dnia lub amunicja.
    /// </summary>
    public void UpdateCombatStats(float efficiency, float rangeMod, float damageMod, float fireRateMod, bool isActive)
    {
        canShoot = isActive;

        if (towerData != null)
        {
            // Matematyka: Baza * Bonus Rasowy * Wydajnoœæ (Obsada)

            // Zasiêg zale¿y g³ównie od rasy (Elf)
            currentRange = towerData.baseRange * rangeMod;

            // Obra¿enia i Szybkostrzelnoœæ zale¿¹ od obsady (efficiency 0.7 lub 1.0)
            currentDamage = towerData.baseDamage * damageMod * efficiency;
            currentFireRate = towerData.fireRate * fireRateMod * efficiency;
        }
    }

    // --- LOGIKA BOJOWA ---

    void UpdateTarget()
    {
        // Jeœli wie¿a jest wy³¹czona (brak ludzi/ammo), nie szukamy celu (oszczêdnoœæ CPU)
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

        // U¿ywamy PRZELICZONEGO zasiêgu (currentRange)
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
        // Jeœli nieaktywna lub brak celu -> nic nie rób
        if (!canShoot || target == null) return;

        if (fireCountdown <= 0f)
        {
            Shoot();
            // U¿ywamy PRZELICZONEJ szybkostrzelnoœci
            // Zabezpieczenie przed dzieleniem przez zero
            if (currentFireRate > 0)
                fireCountdown = 1f / currentFireRate;
            else
                fireCountdown = 999f;
        }

        fireCountdown -= Time.deltaTime;
    }

    void Shoot()
    {
        // Rzutowanie na TowerData (bo zmienna to BuildingData)
        TowerData tData = towerData as TowerData;

        if (tData == null || tData.bulletPrefab == null) return;

        Vector3 spawnPos = (firePoint != null) ? firePoint.position : transform.position;
        GameObject bulletGO = Instantiate(tData.bulletPrefab, spawnPos, Quaternion.identity);

        SimpleBullet bullet = bulletGO.GetComponent<SimpleBullet>();
        if (bullet != null)
        {
            bullet.Seek(target, currentDamage, tData.damageType, tData.effects);
        }

        if (towerEntity != null)
        {
            towerEntity.RegisterShot();
        }
    }

    void OnDrawGizmosSelected()
    {
        // Rysujemy aktualny zasiêg (uwzglêdniaj¹c bonusy elfów)
        Gizmos.color = canShoot ? Color.cyan : Color.red; // Czerwony jeœli nieaktywna
        Gizmos.DrawWireSphere(transform.position, currentRange > 0 ? currentRange : (towerData ? towerData.baseRange : 0));
    }

    // --- API DLA UI (INSPECJA) ---
    public float GetCurrentDamage() => currentDamage;
    public float GetBaseDamage() => towerData != null ? towerData.baseDamage : 0;

    public float GetCurrentRange() => currentRange;
    public float GetBaseRange() => towerData != null ? towerData.baseRange : 0;

    public float GetCurrentFireRate() => currentFireRate;
    public float GetBaseFireRate() => towerData != null ? towerData.fireRate : 0;

}