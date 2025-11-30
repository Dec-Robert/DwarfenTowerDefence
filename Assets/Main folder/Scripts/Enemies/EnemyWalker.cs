using UnityEngine;
using System.Collections.Generic;

public class EnemyWalker : MonoBehaviour
{
    public float speed = 3f;
    private List<Vector3> pathPoints;
    private int targetIndex = 0;
    private bool isInitialized = false;

    public void Initialize(List<Vector3> path)
    {
        pathPoints = path;
        targetIndex = 0;

        // Ustawiamy pozycjê na start (pierwszy punkt)
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
        // Ignorujemy oœ Y przy ruchu, ¿eby kulka nie zapada³a siê w ziemiê jeœli pivoty s¹ ró¿ne
        targetPos.y = transform.position.y;

        // Ruch w stronê celu
        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

        // Sprawdzenie czy dotarliœmy do punktu (z marginesem b³êdu)
        if (Vector3.Distance(transform.position, targetPos) < 0.1f)
        {
            targetIndex++;

            // Koniec trasy
            if (targetIndex >= pathPoints.Count)
            {
                ReachDestination();
            }
        }
    }

    void ReachDestination()
    {
        Debug.Log("Przeciwnik dotar³ do bazy!");
        GameManager.Instance.ModifyBaseHealth(-1);
        Destroy(gameObject);
        // Tutaj w przysz³oœci odejmiesz ¿ycie graczowi
    }
}