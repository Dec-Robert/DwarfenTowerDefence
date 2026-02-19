using UnityEngine;

[RequireComponent(typeof(TowerEntity))]
public class TowerController : MonoBehaviour
{
    [Header("Konfiguracja")]
    public TowerData towerData;
    public Transform firePoint;
    public Transform partToRotate;
    public float turnSpeed = 10f;
    public float shootAngleMargin = 10f;
    
    [Header("Wizualizacja Zasi�gu")]
    public GameObject rangeIndicatorPrefab;
    private GameObject rangeIndicatorInstance;

    [Header("Status (Read Only)")]
    [SerializeField] private bool canShoot = false;
    [SerializeField] private float currentRange;
    [SerializeField] private float currentDamage;
    [SerializeField] private float currentFireRate;
    [SerializeField] private float criticalChance;
    [SerializeField] private float criticalMultiplier;

    [SerializeField] private bool isAimed = false;

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
            criticalChance = towerData.criticalChancel;
            criticalMultiplier = towerData.criticalDamageMultiplier;
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

            // Aktualizacja wizualizacji zasi�gu (skalowanie w czasie rzeczywistym)
            if (rangeIndicatorInstance != null && rangeIndicatorInstance.activeSelf)
            {
                float scale = currentRange * 2f;
                rangeIndicatorInstance.transform.localScale = new Vector3(scale, 1f, scale);
            }
        }
    }

    // --- METODA PRZYWR�CONA: POKAZYWANIE ZASI�GU ---
    public void ShowRangeIndicator(bool show)
    {
        if (show)
        {
            if (rangeIndicatorInstance == null && rangeIndicatorPrefab != null)
            {
                rangeIndicatorInstance = Instantiate(rangeIndicatorPrefab, transform);
                rangeIndicatorInstance.transform.localPosition = new Vector3(0, 0.15f, 0);
            }

            if (rangeIndicatorInstance != null)
            {
                rangeIndicatorInstance.SetActive(true);
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

    void UpdateTarget()
    {
        if (!canShoot) { target = null; return; }

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
            target = nearestEnemy.transform;
        else
            target = null;
    }

    void Update()
    {
        if (!canShoot || target == null)
        {
            isAimed = false;
            return;
        }

        // 1. Obracanie
        LockOnTarget();

        // 2. Sprawdzanie k�ta
        CheckIfAimed();

        // 3. Strzelanie
        if (fireCountdown <= 0f)
        {
            if(partToRotate != null)
            {
                if (isAimed)
                {

                    Shoot();
                    if (currentFireRate > 0)
                        fireCountdown = 1f / currentFireRate;
                }
            }
            else
            {
                Shoot();
                if (currentFireRate > 0)
                    fireCountdown = 1f / currentFireRate;

            }
        }
        fireCountdown -= Time.deltaTime;
    }

    void LockOnTarget()
    {
        if (partToRotate == null) return;
        Vector3 dir = target.position - transform.position;
        // Zabezpieczenie przed zerowym wektorem
        if (dir != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(dir);
            Vector3 rotation = Quaternion.Lerp(partToRotate.rotation, lookRotation, Time.deltaTime * turnSpeed).eulerAngles;
            partToRotate.rotation = Quaternion.Euler(0f, rotation.y, 0f);
        }
    }

    void CheckIfAimed()
    {
        if (partToRotate == null || target == null) return;
        Vector3 directionToTarget = (target.position - transform.position).normalized;
        directionToTarget.y = 0;
        float angle = Vector3.Angle(partToRotate.forward, directionToTarget);
        isAimed = angle <= shootAngleMargin;
    }

    void Shoot()
    {
        TowerData tData = towerData as TowerData;
        if (tData == null || tData.bulletPrefab == null) return;

        // Ustalenie pozycji startowej
        Vector3 spawnPos = (firePoint != null) ? firePoint.position : transform.position;
        Quaternion spawnRot = (firePoint != null) ? firePoint.rotation : 
            (partToRotate != null ? partToRotate.rotation : transform.rotation);

        // Instancja
        GameObject bulletGO = Instantiate(tData.bulletPrefab, spawnPos, spawnRot);
        ProjectileBase projectile = bulletGO.GetComponent<ProjectileBase>();

        if (projectile != null)
        {
            // 1. Obliczenia
            bool isCritical = Random.value * 100f < tData.criticalChancel;
            
            // 2. Inicjalizacja statystyk (Damage, Effecty)
            projectile.Initialize(currentDamage, tData.damageType, tData.effects, isCritical, criticalMultiplier);

            // 3. Przygotowanie danych do strzału
            Vector3 targetPos = Vector3.zero;
            
            if (target != null)
            {
                targetPos = target.position;
            }
            else
            {
                // Fallback: jeśli cel zniknął (jest null), strzelamy w punkt przed lufą
                // Dzięki temu GroundProjectile nadal poleci w "ostatnie znane miejsce" lub przed siebie
                targetPos = spawnPos + (spawnRot * Vector3.forward * 5f);
            }

            // 4. ODPALENIE - Polimorfizm w akcji
            // Nie obchodzi nas czy to SimpleBullet, GroundProjectile czy Hitscan.
            // Każdy pocisk obsłuży te dane po swojemu.
            projectile.Launch(target, targetPos, firePoint);
        }

        if (towerEntity != null) towerEntity.RegisterShot();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = canShoot ? Color.cyan : Color.red;
        Gizmos.DrawWireSphere(transform.position, currentRange > 0 ? currentRange : (towerData ? towerData.baseRange : 0));

        // Debug promienia strza�u
        if (partToRotate != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 forward = partToRotate.forward * 3f;
            Gizmos.DrawRay(partToRotate.position, Quaternion.AngleAxis(-shootAngleMargin, Vector3.up) * forward);
            Gizmos.DrawRay(partToRotate.position, Quaternion.AngleAxis(shootAngleMargin, Vector3.up) * forward);
        }
    }

    public float GetCurrentDamage() => currentDamage;
    public float GetBaseDamage() => towerData != null ? towerData.baseDamage : 0;
    public float GetCurrentRange() => currentRange;
    public float GetBaseRange() => towerData != null ? towerData.baseRange : 0;
    public float GetCurrentFireRate() => currentFireRate;
    public float GetBaseFireRate() => towerData != null ? towerData.fireRate : 0;
}