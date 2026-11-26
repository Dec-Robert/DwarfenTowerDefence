using UnityEngine;
using System;
using System.Collections.Generic;

public class EnemyWalker : MonoBehaviour
{
    [Header("Ruch i Rotacja")]
    public float speed = 3f;
    [Tooltip("Prędkość płynnego obrotu w kierunku marszu (0 = natychmiastowy obrót)")]
    public float rotationSpeed = 10f;
    [Tooltip("Korekta kąta Y (w stopniach), jeśli model patrzy w inną oś niż +Z")]
    public float facingYawOffset = 0f;

    [Header("Debug / Gizmos")]
    [SerializeField] private bool showFacingGizmo = true;
    [SerializeField] private float facingGizmoLength = 1.5f;

    private List<Vector3> pathPoints;
    private int targetIndex = 0;
    private bool isInitialized = false;

    private EnemyStats enemyStats;
    private float currentFogSpeedMultiplier = 1f;
    private bool isInSurveyingFog = false;

    private float spellSpeedMultiplier = 1f;
    private float spellSlowTimer = 0f;

    private float towerSpeedMultiplier = 1f;
    private float towerSlowTimer = 0f;
    private const float MinCombinedNonFreezeSpeedMultiplier = 0.30f;

    public float EffectiveSpeed
    {
        get
        {
            float slowMult = spellSpeedMultiplier * towerSpeedMultiplier;
            if (spellSpeedMultiplier > 0.001f)
            {
                slowMult = Mathf.Max(MinCombinedNonFreezeSpeedMultiplier, slowMult);
            }
            return speed * currentFogSpeedMultiplier * slowMult;
        }
    }
    public bool IsInSurveyingFog => isInSurveyingFog;

    public void Initialize(List<Vector3> path, EnemyStats stats)
    {
        pathPoints = path;
        targetIndex = 0;
        enemyStats = stats;

        if (pathPoints != null && pathPoints.Count > 0)
        {
            transform.position = pathPoints[0];
            FaceInitialPathDirection();
            isInitialized = true;
            RefreshSurveyingFogState();
        }
    }

    void Update()
    {
        if (!isInitialized || pathPoints == null || targetIndex >= pathPoints.Count) return;

        if (spellSlowTimer > 0f)
        {
            spellSlowTimer -= Time.deltaTime;
            if (spellSlowTimer <= 0f)
            {
                spellSlowTimer = 0f;
                spellSpeedMultiplier = 1f;
            }
        }

        if (towerSlowTimer > 0f)
        {
            towerSlowTimer -= Time.deltaTime;
            if (towerSlowTimer <= 0f)
            {
                towerSlowTimer = 0f;
                towerSpeedMultiplier = 1f;
            }
        }

        RefreshSurveyingFogState();

        Vector3 targetPos = pathPoints[targetIndex];
        targetPos.y = transform.position.y;

        float effectiveSpeed = EffectiveSpeed;
        if (effectiveSpeed > 0.001f)
        {
            RotateTowardsPoint(targetPos, Time.deltaTime);
            transform.position = Vector3.MoveTowards(transform.position, targetPos, effectiveSpeed * Time.deltaTime);
        }

        if (Vector3.Distance(transform.position, targetPos) < 0.1f)
        {
            targetIndex++;
            if (targetIndex >= pathPoints.Count)
                ReachDestination();
        }
    }

    /// <summary>Obraca przeciwnika natychmiast w stronę pierwszego odległego punktu na ścieżce.</summary>
    private void FaceInitialPathDirection()
    {
        if (pathPoints == null) return;

        for (int i = 0; i < pathPoints.Count; i++)
        {
            if (FacePointImmediately(pathPoints[i]))
                return;
        }
    }

    private bool TryGetTargetRotation(Vector3 targetPos, out Quaternion targetRotation)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude <= 0.0001f)
        {
            targetRotation = transform.rotation;
            return false;
        }

        targetRotation = Quaternion.LookRotation(dir, Vector3.up);
        if (Mathf.Abs(facingYawOffset) > 0.0001f)
        {
            targetRotation *= Quaternion.Euler(0f, facingYawOffset, 0f);
        }
        return true;
    }

    private bool FacePointImmediately(Vector3 targetPos)
    {
        if (!TryGetTargetRotation(targetPos, out Quaternion targetRotation))
            return false;

        transform.rotation = targetRotation;
        return true;
    }

    private void RotateTowardsPoint(Vector3 targetPos, float deltaTime)
    {
        if (!TryGetTargetRotation(targetPos, out Quaternion targetRotation))
            return;

        if (rotationSpeed <= 0f)
        {
            transform.rotation = targetRotation;
            return;
        }

        float t = 1f - Mathf.Exp(-rotationSpeed * Mathf.Max(0f, deltaTime));
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
    }

    public void ApplySpellSlow(float slowPercent, float duration)
    {
        if (duration <= 0f) return;

        float targetMultiplier = Mathf.Clamp01(1f - slowPercent);
        if (targetMultiplier <= spellSpeedMultiplier || spellSlowTimer <= 0f)
        {
            spellSpeedMultiplier = targetMultiplier;
            spellSlowTimer = Mathf.Max(spellSlowTimer, duration);
        }
    }

    public void ApplyTowerSlow(float slowPercent, float duration)
    {
        if (duration <= 0f) return;

        float targetMultiplier = Mathf.Clamp01(1f - slowPercent);
        if (targetMultiplier < towerSpeedMultiplier - 0.0001f || towerSlowTimer <= 0f)
        {
            towerSpeedMultiplier = targetMultiplier;
            towerSlowTimer = duration;
        }
        else if (Mathf.Abs(targetMultiplier - towerSpeedMultiplier) <= 0.0001f)
        {
            towerSlowTimer = Mathf.Max(towerSlowTimer, duration);
        }
    }

    public void RefreshSurveyingFogState()
    {
        FogOfWarManager fog = FogOfWarManager.Instance;
        bool inFog = fog != null && fog.IsWorldPositionInSurveyingFog(transform.position);
        isInSurveyingFog = inFog;

        if (inFog)
        {
            currentFogSpeedMultiplier = Mathf.Max(0.1f, fog.surveyingEnemySpeedMultiplier);
        }
        else
        {
            currentFogSpeedMultiplier = 1f;
        }
    }

    void ReachDestination()
    {
        Debug.Log("Przeciwnik dotarł do bazy!");

        if (enemyStats != null && GameManager.Instance != null)
        {
            switch (enemyStats.rank)
            {
                case EnemyRank.Normal:
                    GameManager.Instance.ModifyBaseHealth(-1);
                    break;
                case EnemyRank.Elite:
                    GameManager.Instance.ModifyBaseHealth(-3);
                    break;
                case EnemyRank.Boss:
                    GameManager.Instance.InstantDestruction();
                    break;
            }
        }

        if (enemyStats != null)
        {
            enemyStats.GetToBase();
        }
    }

    public void CopyProgressFrom(EnemyWalker other)
    {
        if (other == null) return;
        speed = other.speed;
        rotationSpeed = other.rotationSpeed;
        facingYawOffset = other.facingYawOffset;
        pathPoints = other.pathPoints;
        targetIndex = other.targetIndex;
        transform.position = other.transform.position;
        transform.rotation = other.transform.rotation;
        isInitialized = true;
        RefreshSurveyingFogState();
    }

    public int GetPathProgress()
    {
        return targetIndex;
    }

    private void OnDrawGizmos()
    {
        if (!showFacingGizmo || facingGizmoLength <= 0f) return;

        Vector3 origin = transform.position + Vector3.up * 0.25f;
        Vector3 facingDir = transform.rotation * (Quaternion.Euler(0f, -facingYawOffset, 0f) * Vector3.forward);
        facingDir.y = 0f;
        if (facingDir.sqrMagnitude <= 0.0001f)
            facingDir = Vector3.forward;
        else
            facingDir.Normalize();

        Vector3 tip = origin + facingDir * facingGizmoLength;
        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, tip);

        float headLen = Mathf.Min(0.35f, facingGizmoLength * 0.35f);
        Vector3 leftHead = Quaternion.Euler(0f, 150f, 0f) * facingDir * headLen;
        Vector3 rightHead = Quaternion.Euler(0f, -150f, 0f) * facingDir * headLen;
        Gizmos.DrawLine(tip, tip + leftHead);
        Gizmos.DrawLine(tip, tip + rightHead);
    }
}