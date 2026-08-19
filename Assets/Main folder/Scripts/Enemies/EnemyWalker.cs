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
    

    public void Initialize(List<Vector3> path, EnemyStats stats)
    {
        pathPoints = path;
        targetIndex = 0;
        enemyStats = stats;

        if (pathPoints != null && pathPoints.Count > 0)
        {
            transform.position = pathPoints[0];
            isInitialized = true;
        }
    }

    void Update()
    {
        if (!isInitialized || pathPoints == null || targetIndex >= pathPoints.Count) return;

        Vector3 targetPos = pathPoints[targetIndex];
        targetPos.y = transform.position.y;

        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPos) < 0.1f)
        {
            targetIndex++;
            if (targetIndex >= pathPoints.Count)
                ReachDestination();
        }
    }

    void ReachDestination()
    {
        Debug.Log("Przeciwnik dotarł do bazy!");

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

        enemyStats.GetToBase();
    }

    public void CopyProgressFrom(EnemyWalker other)
    {
        if (other == null) return;
        pathPoints  = other.pathPoints;
        targetIndex = other.targetIndex;
        transform.position = other.transform.position;
        isInitialized = true;
    }

    public int GetPathProgress()
    {
        return targetIndex;
    }
}