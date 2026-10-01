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

    [Header("Wizualizacja Zasięgu")]
    public GameObject rangeIndicatorPrefab;
    private GameObject rangeIndicatorInstance;

    [Header("Status (Read Only)")]
    [SerializeField] public bool canShoot = true;
    [SerializeField] private float currentRange;
    [SerializeField] private float currentDamage;
    [SerializeField] private float currentFireRate;
    [SerializeField] private float currentArmorPen;
    [SerializeField] private float currentMagicPen;
    [SerializeField] private float criticalChance;
    [SerializeField] private float criticalMultiplier;
    [SerializeField] private bool isAimed = false;

    [HideInInspector] public bool isFeared = false;

    private Transform target;
    private float fireCountdown = 0f;
    private TowerEntity towerEntity;

    [Header("System Celowania")]
    public List<TargetingMode> activeTargetingRules = new List<TargetingMode>();
    public Vector2Int targetChunkCoord;

    private Transform forcedTarget = null;

    public event System.Action OnStatsChanged;

    private void Awake()
    {
        towerEntity = GetComponent<TowerEntity>();
    }

    private void Start()
    {
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
                activeTargetingRules = new List<TargetingMode> { TargetingMode.ClosestOnTrack };
        }

        InvokeRepeating(nameof(UpdateTarget), 0f, 0.5f);
    }

    public void ApplyCalculatedStats(TowerStats stats, float efficiency)
    {
        float fearMultiplier = isFeared ? 0.5f : 1f;

        currentRange = stats.range;
        currentDamage = stats.damage * fearMultiplier;
        currentFireRate = stats.fireRate * fearMultiplier;
        currentArmorPen = stats.armorPenetration;
        currentMagicPen = stats.magicPenetration;
        criticalChance = stats.criticalChancel;
        criticalMultiplier = stats.criticalDamageMultiplier;

        if (rangeIndicatorInstance != null && rangeIndicatorInstance.activeSelf)
        {
            float scale = currentRange * 2f;
            rangeIndicatorInstance.transform.localScale = new Vector3(scale, 1f, scale);
        }

        OnStatsChanged?.Invoke();
    }

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

    private void UpdateTarget()
    {
        if (!canShoot)
        {
            target = null;
            return;
        }

        if (forcedTarget != null)
        {
            if (Vector3.Distance(transform.position, forcedTarget.position) <= currentRange)
            {
                target = forcedTarget;
                return;
            }
            else
            {
                forcedTarget = null;
            }
        }

        if (activeTargetingRules.Count > 0 && activeTargetingRules[0] == TargetingMode.Chunk)
        {
            if (towerData != null && towerData.canShootChunk)
            {
                target = null;
                return;
            }
        }

        EnemyStats[] allEnemies = FindObjectsByType<EnemyStats>(FindObjectsSortMode.None);
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

        foreach (var mode in activeTargetingRules)
        {
            if (candidates.Count <= 1) break;
            candidates = ApplyTargetingFilter(candidates, mode);
        }

        if (candidates.Count > 1)
        {
            candidates = ApplyTargetingFilter(candidates, TargetingMode.ClosestOnTrack);
        }

        target = candidates.Count > 0 ? candidates[0].transform : null;
    }

    private void Update()
    {
        if (!canShoot || target == null)
        {
            isAimed = false;
            return;
        }

        LockOnTarget();
        CheckIfAimed();

        if (fireCountdown <= 0f)
        {
            if (partToRotate != null)
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

    private void LockOnTarget()
    {
        if (partToRotate == null || target == null) return;
        Vector3 dir = target.position - transform.position;
        if (dir != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(dir);
            Vector3 rotation = Quaternion.Lerp(partToRotate.rotation, lookRotation, Time.deltaTime * turnSpeed).eulerAngles;
            partToRotate.rotation = Quaternion.Euler(0f, rotation.y, 0f);
        }
    }

    private void CheckIfAimed()
    {
        if (partToRotate == null || target == null) return;
        Vector3 directionToTarget = (target.position - transform.position).normalized;
        directionToTarget.y = 0;
        float angle = Vector3.Angle(partToRotate.forward, directionToTarget);
        isAimed = angle <= shootAngleMargin;
    }

    private void Shoot()
    {
        if (towerData == null || towerData.bulletPrefab == null) return;

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        Quaternion spawnRot = firePoint != null ? firePoint.rotation : (partToRotate != null ? partToRotate.rotation : transform.rotation);

        GameObject bulletGO = Instantiate(towerData.bulletPrefab, spawnPos, spawnRot);
        ProjectileBase projectile = bulletGO.GetComponent<ProjectileBase>();

        if (projectile != null)
        {
            bool isCritical = Random.value < criticalChance;

            projectile.Initialize(
                currentDamage,
                towerData.damageType,
                towerData.effects,
                isCritical,
                criticalMultiplier,
                currentArmorPen,
                currentMagicPen,
                criticalChance,
                towerEntity
            );

            Vector3 targetPos;

            if (target != null)
            {
                targetPos = target.position;
            }
            else
            {
                if (activeTargetingRules.Count > 0 && activeTargetingRules[0] == TargetingMode.Chunk && towerData.canShootChunk)
                {
                    float hexSize = 35f;
                    targetPos = HexGridMath.GetChunkCenterWorld(targetChunkCoord, 4, hexSize, 0f);
                }
                else
                {
                    targetPos = spawnPos + (spawnRot * Vector3.forward * 5f);
                }
            }

            projectile.Launch(target, targetPos, firePoint);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = canShoot ? Color.cyan : Color.red;
        Gizmos.DrawWireSphere(transform.position, currentRange > 0 ? currentRange : (towerData ? towerData.baseRange : 0));

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
        float epsilon = 0.05f;

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

            case TargetingMode.ClosestOnTrack:
                int maxProgress = -1;
                foreach (var e in list)
                {
                    var walker = e.GetComponent<EnemyWalker>();
                    if (walker != null && walker.GetPathProgress() > maxProgress) maxProgress = walker.GetPathProgress();
                }
                result = list.FindAll(e => { var w = e.GetComponent<EnemyWalker>(); return w != null && w.GetPathProgress() == maxProgress; });
                break;

            case TargetingMode.FurthestOnTrack:
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

        return result.Count > 0 ? result : list;
    }

    public TowerStats GetAllStats()
    {
        return new TowerStats
        {
            range = currentRange,
            damage = currentDamage,
            fireRate = currentFireRate,
            criticalChancel = criticalChance,
            criticalDamageMultiplier = criticalMultiplier,
            armorPenetration = currentArmorPen,
            magicPenetration = currentMagicPen,
            damageType = towerData != null ? towerData.damageType : DamageType.Physical
        };
    }
}