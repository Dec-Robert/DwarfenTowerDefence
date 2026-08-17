using System.Collections.Generic;
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
    //
    [Header("Status (Read Only)")]
    [SerializeField] private bool canShoot = false;
    [SerializeField] private float currentRange;
    [SerializeField] private float currentDamage;
    [SerializeField] private float currentFireRate;
    [SerializeField] private float currentArmorPen;  
    [SerializeField] private float currentMagicPen;  
    [SerializeField] private float criticalChance;
    [SerializeField] private float criticalMultiplier;

    [SerializeField] private bool isAimed = false;
    
    
    //Boole specjalne
    [HideInInspector] public bool isFeared = false; // Paraliż strachem od bossa

    private Transform target;
    private float fireCountdown = 0f;
    private TowerEntity towerEntity;
    
    [Header("System Celowania")]
    public List<TargetingMode> activeTargetingRules = new List<TargetingMode>();
    public Vector2Int targetChunkCoord; // Używane, jeśli tryb to Chunk
    
    private Transform forcedTarget = null; // Dla OverrideTargeting (Prowokacja)

    //Event to listen for stat change
    public event System.Action OnStatsChanged;


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
            
            if (towerData.defaultTargetingPriorities != null && towerData.defaultTargetingPriorities.Count > 0)
                activeTargetingRules = new List<TargetingMode>(towerData.defaultTargetingPriorities);
            else
                activeTargetingRules = new List<TargetingMode> { TargetingMode.ClosestOnTrack }; // Fallback
        }
        
        InvokeRepeating("UpdateTarget", 0f, 0.5f);
    }

    public void UpdateCombatStats(
        float efficiency, 
        float rangeMulti, float flatRange, 
        float damageMulti, float flatDamage, 
        float fireRateMulti, float flatFireRate,
        float flatArmorPen, float flatMagicPen, float flatCritChan, float flatCritDmg,
        bool isActive)
    {
        canShoot = isActive;

        if (isFeared && efficiency > 0f)
        {
            efficiency = Mathf.Max(0f, efficiency - (towerData != null ? towerData.workerScalingFactor : 0.25f));
        }
        
        if (towerData != null)
        {
            // (Baza + Flat) * Mnożnik * Opcjonalna Wydajność Załogi
            currentRange = (towerData.baseRange + flatRange) * rangeMulti;
            currentDamage = (towerData.baseDamage + flatDamage) * damageMulti * efficiency;
            currentFireRate = (towerData.fireRate + flatFireRate) * fireRateMulti * efficiency;

            // Nowe statystyki penetracji (tylko Flat dodawany do bazy)
            currentArmorPen = towerData.armorPenetration + flatArmorPen;
            currentMagicPen = towerData.magicPenetration + flatMagicPen;

            // Statystyki krytyczne
            criticalChance = towerData.criticalChancel + flatCritChan;
            
            // Dla krytyka mnożnik bazowy (np. 200%) + flat runy (np. +50%) = 250%
            criticalMultiplier = towerData.criticalDamageMultiplier + flatCritDmg;

            // Aktualizacja wizualizacji zasięgu
            if (rangeIndicatorInstance != null && rangeIndicatorInstance.activeSelf)
            {
                float scale = currentRange * 2f;
                rangeIndicatorInstance.transform.localScale = new Vector3(scale, 1f, scale);
            }
        }
        OnStatsChanged?.Invoke();
    }

    // --- POKAZYWANIE ZASIĘGU ---
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

        // 1. CZY MAMY OVERRIDE? (np. Shade)
        if (forcedTarget != null)
        {
            if (Vector3.Distance(transform.position, forcedTarget.position) <= currentRange)
            {
                target = forcedTarget;
                return;
            }
            else
            {
                forcedTarget = null; // Cel uciekł z zasięgu, zdejmujemy override
            }
        }

        // 2. CZY STRZELAMY W CHUNK?
        if (activeTargetingRules.Count > 0 && activeTargetingRules[0] == TargetingMode.Chunk)
        {
            TowerData tData = towerData as TowerData;
            if (tData != null && tData.canShootChunk)
            {
                target = null; // Nie śledzimy konkretnego transformu!
                return;
            }
        }

        // 3. POBRANIE WROGÓW W ZASIĘGU
        EnemyStats[] allEnemies = FindObjectsOfType<EnemyStats>();
        List<EnemyStats> candidates = new List<EnemyStats>();

        foreach (EnemyStats enemy in allEnemies)
        {
            if (Vector3.Distance(transform.position, enemy.transform.position) <= currentRange)
                candidates.Add(enemy);
        }

        if (candidates.Count == 0)
        {
            target = null;
            return;
        }

        // 4. FILTROWANIE WEDŁUG ZASAD GRACZA
        foreach (var mode in activeTargetingRules)
        {
            if (candidates.Count <= 1) break; // Został tylko jeden (lub zero), koniec filtrowania
            candidates = ApplyTargetingFilter(candidates, mode);
        }

        // 5. FALLBACK / ROZSTRZYGNIĘCIE REMISU
        // Jeśli lista nada ma kilku wrogów po wszystkich filtrach, bierzemy tego, który jest najbliżej bramy!
        if (candidates.Count > 1)
        {
            candidates = ApplyTargetingFilter(candidates, TargetingMode.ClosestOnTrack);
        }

        // Ustalenie ostatecznego celu
        if (candidates.Count > 0)
            target = candidates[0].transform;
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
            // 1. Obliczenia Krytyka używając AKTUALNYCH wartości
            bool isCritical = Random.value * 100f < criticalChance;
            
            // 2. Inicjalizacja statystyk
            // PRZEKAZUJEMY Armor i Magic Pen do pocisku (za chwilę zaktualizujemy pocisk)
            projectile.Initialize(currentDamage, tData.damageType, tData.effects, isCritical, criticalMultiplier, currentArmorPen, currentMagicPen, criticalChance, towerEntity);
            // 3. Przygotowanie danych do strzału
            Vector3 targetPos = Vector3.zero;
            
            if (target != null)
            {
                targetPos = target.position;
            }
            else
            {
                // CZY STRZELAMY W CHUNK?
                if (activeTargetingRules.Count > 0 && activeTargetingRules[0] == TargetingMode.Chunk && tData.canShootChunk)
                {
                    // Uderzamy w sam środek ustawionego Chunku
                    float hexSize = 35f; // Możesz tu pobrać referencję z mapy, jeśli potrzeba
                    float padding = 0f;
                    targetPos = HexGridMath.GetChunkCenterWorld(targetChunkCoord, 4, hexSize, padding);
                }
                else
                {
                    // Fallback: strzał przed lufę
                    targetPos = spawnPos + (spawnRot * Vector3.forward * 5f);
                }
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
    
    /// <summary>
    /// Zmusza wieżę do strzelania w konkretny cel (np. minion "Shade" ściągający aggro).
    /// Ignoruje wszystkie inne filtry.
    /// </summary>
    public void OverrideTargeting(Transform newTarget)
    {
        forcedTarget = newTarget;
    }

    public void ClearOverride()
    {
        forcedTarget = null;
    }
    
    private List<EnemyStats> ApplyTargetingFilter(List<EnemyStats> list, TargetingMode mode)
    {
        if (list.Count == 0) return list;

        List<EnemyStats> result = new List<EnemyStats>();
        float epsilon = 0.05f; // Mały margines błędu dla floatów

        switch (mode)
        {
            case TargetingMode.Closest:
                float minDistance = float.MaxValue;
                foreach (var e in list)
                {
                    float d = Vector3.Distance(transform.position, e.transform.position);
                    if (d < minDistance) minDistance = d;
                }
                result = list.FindAll(e => Mathf.Abs(Vector3.Distance(transform.position, e.transform.position) - minDistance) <= epsilon);
                break;

            case TargetingMode.Furthest:
                float maxDistance = float.MinValue;
                foreach (var e in list)
                {
                    float d = Vector3.Distance(transform.position, e.transform.position);
                    if (d > maxDistance) maxDistance = d;
                }
                result = list.FindAll(e => Mathf.Abs(Vector3.Distance(transform.position, e.transform.position) - maxDistance) <= epsilon);
                break;

            case TargetingMode.ClosestOnTrack: // Najbliżej bramy (największy indeks ścieżki)
                int maxProgress = -1;
                foreach (var e in list)
                {
                    var walker = e.GetComponent<EnemyWalker>();
                    if (walker != null && walker.GetPathProgress() > maxProgress) maxProgress = walker.GetPathProgress();
                }
                result = list.FindAll(e => { var w = e.GetComponent<EnemyWalker>(); return w != null && w.GetPathProgress() == maxProgress; });
                break;

            case TargetingMode.FurthestOnTrack: // Najdalej od bramy (świeżo po spawnie, najmniejszy indeks)
                int minProgress = int.MaxValue;
                foreach (var e in list)
                {
                    var walker = e.GetComponent<EnemyWalker>();
                    if (walker != null && walker.GetPathProgress() < minProgress) minProgress = walker.GetPathProgress();
                }
                result = list.FindAll(e => { var w = e.GetComponent<EnemyWalker>(); return w != null && w.GetPathProgress() == minProgress; });
                break;

            case TargetingMode.MostHP:
                float maxHP = float.MinValue;
                foreach (var e in list) { if (e.GetCurrentHealth() > maxHP) maxHP = e.GetCurrentHealth(); }
                result = list.FindAll(e => Mathf.Abs(e.GetCurrentHealth() - maxHP) <= epsilon);
                break;

            case TargetingMode.LeastHP:
                float minHP = float.MaxValue;
                foreach (var e in list) { if (e.GetCurrentHealth() < minHP) minHP = e.GetCurrentHealth(); }
                result = list.FindAll(e => Mathf.Abs(e.GetCurrentHealth() - minHP) <= epsilon);
                break;

            case TargetingMode.HighestThreat:
                // Boss = 3, Elite = 2, Normal = 1
                int GetThreat(EnemyRank rank) => rank == EnemyRank.Boss ? 3 : rank == EnemyRank.Elite ? 2 : 1;
                int highestThreat = -1;
                foreach (var e in list) { if (GetThreat(e.rank) > highestThreat) highestThreat = GetThreat(e.rank); }
                result = list.FindAll(e => GetThreat(e.rank) == highestThreat);
                break;

            case TargetingMode.Random:
                result.Add(list[Random.Range(0, list.Count)]);
                break;

            default:
                return list;
        }

        // Zabezpieczenie: jeśli filtr wywalił wszystkich (np. wrogowie nie mają komponentu Walker), zwróć oryginał
        return result.Count > 0 ? result : list;
    }

    public TowerStats GetAllStats()
    { 
        TowerStats stats = new TowerStats();
        stats.range = currentRange;
        stats.damage = currentDamage;
        stats.fireRate = currentFireRate;
        stats.criticalChancel = criticalChance;
        stats.criticalDamageMultiplier = criticalMultiplier;
        stats.armorPenetration = currentArmorPen;
        stats.magicPenetration = currentMagicPen;
        stats.damageType = towerData.damageType;

        return stats;
    }
}