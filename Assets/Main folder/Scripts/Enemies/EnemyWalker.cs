using UnityEngine;
using System;
using System.Collections.Generic;

public class EnemyWalker : MonoBehaviour
{
    public float speed = 3f;
    private List<Vector3> pathPoints;
    private int targetIndex = 0;
    private bool isInitialized = false;

    private EnemyStats enemyStats;
    private float currentFogSpeedMultiplier = 1f;
    private bool isInSurveyingFog = false;

    private float spellSpeedMultiplier = 1f;
    private float spellSlowTimer = 0f;

    public float EffectiveSpeed => speed * currentFogSpeedMultiplier * spellSpeedMultiplier;
    public bool IsInSurveyingFog => isInSurveyingFog;

    public void Initialize(List<Vector3> path, EnemyStats stats)
    {
        pathPoints = path;
        targetIndex = 0;
        enemyStats = stats;

        if (pathPoints != null && pathPoints.Count > 0)
        {
            transform.position = pathPoints[0];
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

        RefreshSurveyingFogState();

        Vector3 targetPos = pathPoints[targetIndex];
        targetPos.y = transform.position.y;

        transform.position = Vector3.MoveTowards(transform.position, targetPos, EffectiveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPos) < 0.1f)
        {
            targetIndex++;
            if (targetIndex >= pathPoints.Count)
                ReachDestination();
        }
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
        speed       = other.speed;
        pathPoints  = other.pathPoints;
        targetIndex = other.targetIndex;
        transform.position = other.transform.position;
        isInitialized = true;
        RefreshSurveyingFogState();
    }

    public int GetPathProgress()
    {
        return targetIndex;
    }
}