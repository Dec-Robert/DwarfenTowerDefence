using UnityEngine;
using System;
using System.Collections.Generic;

public class EnemyWalker : MonoBehaviour
{
    public float speed = 3f;
    private List<Vector3> pathPoints;
    private int targetIndex = 0;
    private bool isInitialized = false;

    // Callback ustawiany przez EnemySpawner – powiadamia Echo o przełomie
    // Argument: EnemyData wroga który dotarł
    public Action<EnemyData> OnReachedBase;

    // Cache na dane wroga – potrzebne do Echo callback
    private EnemyData enemyData;

    public void Initialize(List<Vector3> path, EnemyData data = null)
    {
        pathPoints = path;
        targetIndex = 0;
        enemyData = data;

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
        GameManager.Instance.ModifyBaseHealth(-1);

        // Powiadom Echo
        OnReachedBase?.Invoke(enemyData);

        Destroy(gameObject);
    }

    public void CopyProgressFrom(EnemyWalker other)
    {
        if (other == null) return;
        pathPoints  = other.pathPoints;
        targetIndex = other.targetIndex;
        transform.position = other.transform.position;
        isInitialized = true;
    }
}