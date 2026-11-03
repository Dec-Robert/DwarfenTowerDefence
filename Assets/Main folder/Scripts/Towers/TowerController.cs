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

    public float CurrentRange => currentRange;
    public float CurrentWorldRange => (currentRange > 0 ? currentRange : (towerData ? towerData.baseRange : 0)) * TowerData.HexToWorldRangeMultiplier;
    public bool IsNightTime => TimePhaseManager.Instance == null || TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Night;

    [HideInInspector] public bool isFeared = false;

    private Transform target;
    private float fireCountdown = 0f;
    private TowerEntity towerEntity;

    [Header("System Celowania")]
    public List<TargetingMode> activeTargetingRules = new List<TargetingMode>();
    public Vector2Int targetChunkCoord;

    [Header("Celowanie w Heks (Ground Target)")]
    [SerializeField] private bool hasGroundTarget = false;
    [SerializeField] private Vector3 groundTargetPosition;
    [SerializeField] private HexCell targetHexCell;
    private List<TargetingMode> savedPrioritiesBeforeHexTarget = new List<TargetingMode>();
    private GameObject targetHexIndicatorInstance;

    public bool HasGroundTarget => hasGroundTarget;
    public Vector3 GroundTargetPosition => groundTargetPosition;
    public HexCell TargetHexCell => targetHexCell;

    private Transform forcedTarget = null;

    private readonly List<EnemyStats> candidateBufferA = new List<EnemyStats>(64);
    private readonly List<EnemyStats> candidateBufferB = new List<EnemyStats>(64);
    private const float FilterEpsilon = 0.05f;

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

        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnNightStarted -= HandleNightStarted;
            TimePhaseManager.Instance.OnNightStarted += HandleNightStarted;
        }

        wasNight = IsNightTime;
        if (wasNight && hasGroundTarget && activeTargetingRules.Count > 0 && activeTargetingRules[0] == TargetingMode.Chunk)
        {
            fireCountdown = UnityEngine.Random.Range(0.5f, 1.0f);
        }

        InvokeRepeating(nameof(UpdateTarget), 0f, 0.5f);
    }

    public void ApplyCalculatedStats(TowerStats stats, float efficiency)
    {
        float fearMultiplier = isFeared ? 0.5f : 1f;

        currentRange = stats.range;
        currentDamage = stats.damage;
        currentFireRate = stats.fireRate * fearMultiplier;
        currentArmorPen = stats.armorPenetration;
        currentMagicPen = stats.magicPenetration;
        criticalChance = stats.criticalChancel;
        criticalMultiplier = stats.criticalDamageMultiplier;

        if (!ReferenceEquals(rangeIndicatorInstance, null) && rangeIndicatorInstance.activeSelf)
        {
            float scale = CurrentWorldRange * 2f;
            rangeIndicatorInstance.transform.localScale = new Vector3(scale, 1f, scale);
        }

        OnStatsChanged?.Invoke();
    }

    public void SetGroundTarget(Vector3 worldPos, HexCell cell)
    {
        hasGroundTarget = true;
        groundTargetPosition = worldPos;
        targetHexCell = cell;

        if (activeTargetingRules.Count == 0 || activeTargetingRules[0] != TargetingMode.Chunk)
        {
            savedPrioritiesBeforeHexTarget = new List<TargetingMode>(activeTargetingRules);
        }

        activeTargetingRules.Clear();
        activeTargetingRules.Add(TargetingMode.Chunk);

        // Opóźnienie pierwszego strzału o losowo 0.5 - 1.0s, aby wieże nie strzelały jednocześnie
        fireCountdown = UnityEngine.Random.Range(0.5f, 1.0f);

        UpdateTargetHexIndicator(rangeIndicatorInstance != null && rangeIndicatorInstance.activeSelf);
        OnStatsChanged?.Invoke();
    }

    public void ClearGroundTarget()
    {
        hasGroundTarget = false;
        targetHexCell = null;

        if (activeTargetingRules.Count > 0 && activeTargetingRules[0] == TargetingMode.Chunk)
        {
            activeTargetingRules.Clear();
            if (savedPrioritiesBeforeHexTarget != null && savedPrioritiesBeforeHexTarget.Count > 0)
            {
                activeTargetingRules.AddRange(savedPrioritiesBeforeHexTarget);
            }
            else if (towerData != null && towerData.defaultTargetingPriorities != null && towerData.defaultTargetingPriorities.Count > 0)
            {
                activeTargetingRules.AddRange(towerData.defaultTargetingPriorities);
            }
            else
            {
                activeTargetingRules.Add(TargetingMode.ClosestOnTrack);
            }
        }

        UpdateTargetHexIndicator(false);
        OnStatsChanged?.Invoke();
    }

    private void UpdateTargetHexIndicator(bool show)
    {
        if (!hasGroundTarget || !show)
        {
            if (targetHexIndicatorInstance != null)
                targetHexIndicatorInstance.SetActive(false);
            return;
        }

        if (targetHexIndicatorInstance == null && rangeIndicatorPrefab != null)
        {
            targetHexIndicatorInstance = Instantiate(rangeIndicatorPrefab);
            targetHexIndicatorInstance.name = "TargetHexIndicator_" + gameObject.name;
        }

        if (targetHexIndicatorInstance != null)
        {
            targetHexIndicatorInstance.transform.position = groundTargetPosition + new Vector3(0, 0.05f, 0);
            targetHexIndicatorInstance.transform.localScale = new Vector3(1.6f, 1f, 1.6f);
            targetHexIndicatorInstance.SetActive(true);
        }
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
                float scale = CurrentWorldRange * 2f;
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

        UpdateTargetHexIndicator(show);
    }

    private void OnEnable()
    {
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnNightStarted -= HandleNightStarted;
            TimePhaseManager.Instance.OnNightStarted += HandleNightStarted;
        }
    }

    private void OnDisable()
    {
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnNightStarted -= HandleNightStarted;
        }

        if (targetHexIndicatorInstance != null)
            targetHexIndicatorInstance.SetActive(false);
    }

    private void HandleNightStarted()
    {
        if (hasGroundTarget && activeTargetingRules.Count > 0 && activeTargetingRules[0] == TargetingMode.Chunk)
        {
            fireCountdown = UnityEngine.Random.Range(0.5f, 1.0f);
        }
    }

    private void OnDestroy()
    {
        if (targetHexIndicatorInstance != null)
            Destroy(targetHexIndicatorInstance);
    }

    private void UpdateTarget()
    {
        if (!canShoot || !IsNightTime)
        {
            target = null;
            return;
        }

        if (hasGroundTarget && activeTargetingRules.Count > 0 && activeTargetingRules[0] == TargetingMode.Chunk)
        {
            target = null;
            return;
        }

        float worldRange = CurrentWorldRange;

        if (forcedTarget != null)
        {
            Vector3 diff = forcedTarget.position - transform.position;
            diff.y = 0;
            if (diff.sqrMagnitude <= worldRange * worldRange)
            {
                target = forcedTarget;
                return;
            }
            else
            {
                forcedTarget = null;
            }
        }

        List<EnemyStats> current = candidateBufferA;
        List<EnemyStats> next = candidateBufferB;
        current.Clear();
        next.Clear();

        List<EnemyStats> allEnemies = EnemyRegistry.ActiveEnemies;
        Vector3 towerPos = transform.position;
        float rangeSqr = worldRange * worldRange;

        for (int i = 0; i < allEnemies.Count; i++)
        {
            EnemyStats enemy = allEnemies[i];
            if (enemy == null) continue;
            Vector3 diff = enemy.transform.position - towerPos;
            diff.y = 0;
            if (diff.sqrMagnitude <= rangeSqr)
            {
                current.Add(enemy);
            }
        }

        if (current.Count == 0)
        {
            target = null;
            return;
        }

        for (int r = 0; r < activeTargetingRules.Count; r++)
        {
            if (current.Count <= 1) break;
            ApplyFilter(current, next, activeTargetingRules[r]);
            if (next.Count > 0)
            {
                var temp = current;
                current = next;
                next = temp;
            }
        }

        if (current.Count > 1)
        {
            ApplyFilter(current, next, TargetingMode.ClosestOnTrack);
            if (next.Count > 0)
            {
                var temp = current;
                current = next;
                next = temp;
            }
        }

        target = current.Count > 0 ? current[0].transform : null;
        candidateBufferA.Clear();
        candidateBufferB.Clear();
    }

    private bool wasNight = false;

    private void Update()
    {
        bool isNight = IsNightTime;
        if (isNight && !wasNight)
        {
            if (hasGroundTarget && activeTargetingRules.Count > 0 && activeTargetingRules[0] == TargetingMode.Chunk)
            {
                fireCountdown = UnityEngine.Random.Range(0.5f, 1.0f);
            }
        }
        wasNight = isNight;

        if (!canShoot || !isNight)
        {
            isAimed = false;
            return;
        }

        bool isGroundMode = hasGroundTarget && activeTargetingRules.Count > 0 && activeTargetingRules[0] == TargetingMode.Chunk;

        if (isGroundMode)
        {
            LockOnGroundTarget();
            CheckIfAimedGround();
        }
        else
        {
            if (target == null)
            {
                isAimed = false;
                return;
            }

            LockOnTarget();
            CheckIfAimed();
        }

        if (fireCountdown <= 0f)
        {
            if (partToRotate != null)
            {
                if (isAimed)
                {
                    Shoot(isGroundMode);
                    if (currentFireRate > 0)
                        fireCountdown = 1f / currentFireRate;
                }
            }
            else
            {
                Shoot(isGroundMode);
                if (currentFireRate > 0)
                    fireCountdown = 1f / currentFireRate;
            }
        }
        fireCountdown -= Time.deltaTime;
    }

    private void LockOnGroundTarget()
    {
        if (partToRotate == null) return;
        Vector3 dir = groundTargetPosition - transform.position;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(dir);
            Vector3 rotation = Quaternion.Lerp(partToRotate.rotation, lookRotation, Time.deltaTime * turnSpeed).eulerAngles;
            partToRotate.rotation = Quaternion.Euler(0f, rotation.y, 0f);
        }
    }

    private void CheckIfAimedGround()
    {
        if (partToRotate == null)
        {
            isAimed = true;
            return;
        }

        Vector3 directionToTarget = (groundTargetPosition - transform.position).normalized;
        directionToTarget.y = 0;
        float angle = Vector3.Angle(partToRotate.forward, directionToTarget);
        isAimed = angle <= shootAngleMargin;
    }

    private void LockOnTarget()
    {
        if (partToRotate == null || target == null) return;
        Vector3 dir = target.position - transform.position;
        dir.y = 0;
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

    private void Shoot(bool isGroundMode = false)
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

            if (isGroundMode)
            {
                Vector3 toTarget = groundTargetPosition - transform.position;
                toTarget.y = 0;
                float dist = toTarget.magnitude;
                float worldRange = CurrentWorldRange;
                Vector3 effectiveGroundPos = groundTargetPosition;
                if (dist > worldRange && dist > 0.001f)
                {
                    effectiveGroundPos = transform.position + (toTarget / dist) * worldRange;
                }
                targetPos = effectiveGroundPos;
                projectile.Launch(null, targetPos, firePoint);
            }
            else if (target != null)
            {
                targetPos = target.position;
                projectile.Launch(target, targetPos, firePoint);
            }
            else
            {
                targetPos = spawnPos + (spawnRot * Vector3.forward * 5f);
                projectile.Launch(null, targetPos, firePoint);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = canShoot ? Color.cyan : Color.red;
        Gizmos.DrawWireSphere(transform.position, CurrentWorldRange);

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

    private void ApplyFilter(List<EnemyStats> input, List<EnemyStats> output, TargetingMode mode)
    {
        output.Clear();
        if (input.Count == 0) return;

        Vector3 towerPos = transform.position;

        switch (mode)
        {
            case TargetingMode.Closest:
            {
                float minDistance = float.MaxValue;
                for (int i = 0; i < input.Count; i++)
                {
                    EnemyStats e = input[i];
                    if (e == null) continue;
                    Vector3 diff = e.transform.position - towerPos;
                    diff.y = 0;
                    float d = diff.magnitude;
                    if (d < minDistance) minDistance = d;
                }
                for (int i = 0; i < input.Count; i++)
                {
                    EnemyStats e = input[i];
                    if (e == null) continue;
                    Vector3 diff = e.transform.position - towerPos;
                    diff.y = 0;
                    if (Mathf.Abs(diff.magnitude - minDistance) <= FilterEpsilon)
                    {
                        output.Add(e);
                    }
                }
                break;
            }

            case TargetingMode.Furthest:
            {
                float maxDistance = float.MinValue;
                for (int i = 0; i < input.Count; i++)
                {
                    EnemyStats e = input[i];
                    if (e == null) continue;
                    Vector3 diff = e.transform.position - towerPos;
                    diff.y = 0;
                    float d = diff.magnitude;
                    if (d > maxDistance) maxDistance = d;
                }
                for (int i = 0; i < input.Count; i++)
                {
                    EnemyStats e = input[i];
                    if (e == null) continue;
                    Vector3 diff = e.transform.position - towerPos;
                    diff.y = 0;
                    if (Mathf.Abs(diff.magnitude - maxDistance) <= FilterEpsilon)
                    {
                        output.Add(e);
                    }
                }
                break;
            }

            case TargetingMode.ClosestOnTrack:
            {
                int maxProgress = -1;
                for (int i = 0; i < input.Count; i++)
                {
                    EnemyStats e = input[i];
                    if (e == null) continue;
                    EnemyWalker walker = e.Walker != null ? e.Walker : e.GetComponent<EnemyWalker>();
                    if (walker != null && walker.GetPathProgress() > maxProgress)
                    {
                        maxProgress = walker.GetPathProgress();
                    }
                }
                for (int i = 0; i < input.Count; i++)
                {
                    EnemyStats e = input[i];
                    if (e == null) continue;
                    EnemyWalker walker = e.Walker != null ? e.Walker : e.GetComponent<EnemyWalker>();
                    if (walker != null && walker.GetPathProgress() == maxProgress)
                    {
                        output.Add(e);
                    }
                }
                break;
            }

            case TargetingMode.FurthestOnTrack:
            {
                int minProgress = int.MaxValue;
                for (int i = 0; i < input.Count; i++)
                {
                    EnemyStats e = input[i];
                    if (e == null) continue;
                    EnemyWalker walker = e.Walker != null ? e.Walker : e.GetComponent<EnemyWalker>();
                    if (walker != null && walker.GetPathProgress() < minProgress)
                    {
                        minProgress = walker.GetPathProgress();
                    }
                }
                for (int i = 0; i < input.Count; i++)
                {
                    EnemyStats e = input[i];
                    if (e == null) continue;
                    EnemyWalker walker = e.Walker != null ? e.Walker : e.GetComponent<EnemyWalker>();
                    if (walker != null && walker.GetPathProgress() == minProgress)
                    {
                        output.Add(e);
                    }
                }
                break;
            }

            case TargetingMode.MostHP:
            {
                float maxHP = float.MinValue;
                for (int i = 0; i < input.Count; i++)
                {
                    EnemyStats e = input[i];
                    if (e == null) continue;
                    float hp = e.GetCurrentHealth();
                    if (hp > maxHP) maxHP = hp;
                }
                for (int i = 0; i < input.Count; i++)
                {
                    EnemyStats e = input[i];
                    if (e == null) continue;
                    if (Mathf.Abs(e.GetCurrentHealth() - maxHP) <= FilterEpsilon)
                    {
                        output.Add(e);
                    }
                }
                break;
            }

            case TargetingMode.LeastHP:
            {
                float minHP = float.MaxValue;
                for (int i = 0; i < input.Count; i++)
                {
                    EnemyStats e = input[i];
                    if (e == null) continue;
                    float hp = e.GetCurrentHealth();
                    if (hp < minHP) minHP = hp;
                }
                for (int i = 0; i < input.Count; i++)
                {
                    EnemyStats e = input[i];
                    if (e == null) continue;
                    if (Mathf.Abs(e.GetCurrentHealth() - minHP) <= FilterEpsilon)
                    {
                        output.Add(e);
                    }
                }
                break;
            }

            case TargetingMode.HighestThreat:
            {
                int highestThreat = -1;
                for (int i = 0; i < input.Count; i++)
                {
                    EnemyStats e = input[i];
                    if (e == null) continue;
                    int threat = GetThreat(e.rank);
                    if (threat > highestThreat) highestThreat = threat;
                }
                for (int i = 0; i < input.Count; i++)
                {
                    EnemyStats e = input[i];
                    if (e == null) continue;
                    if (GetThreat(e.rank) == highestThreat)
                    {
                        output.Add(e);
                    }
                }
                break;
            }

            case TargetingMode.Random:
            {
                if (input.Count > 0)
                {
                    output.Add(input[Random.Range(0, input.Count)]);
                }
                break;
            }

            default:
                break;
        }
    }

    private static int GetThreat(EnemyRank rank)
    {
        return rank == EnemyRank.Boss ? 3 : rank == EnemyRank.Elite ? 2 : 1;
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