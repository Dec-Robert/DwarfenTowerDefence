using System.Collections.Generic;
using UnityEngine;

public class MaddeningGazeSkill : EnemySkill
{
    [Header("Konfiguracja Gaze")]
    [Tooltip("Promień, w jakim wieże wpadają w panikę (Zmniejszona wydajność)")]
    public float auraRadius = 25f;

    [SerializeField] private LayerMask layerMask = ~0;

    private readonly List<TowerController> fearedTowers = new List<TowerController>();
    private readonly HashSet<TowerController> currentTowersInRadius = new HashSet<TowerController>();
    private static readonly Collider[] hitBuffer = new Collider[64];

    private float scanTimer = 0f;
    private const float ScanInterval = 0.2f;

    void Update()
    {
        scanTimer += Time.deltaTime;
        if (scanTimer < ScanInterval) return;
        scanTimer = 0f;

        currentTowersInRadius.Clear();

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, auraRadius, hitBuffer, layerMask);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];
            if (hit == null) continue;

            TowerController tower = hit.GetComponent<TowerController>();
            if (tower == null) tower = hit.GetComponentInChildren<TowerController>();
            if (tower == null) tower = hit.GetComponentInParent<TowerController>();

            if (tower != null)
            {
                currentTowersInRadius.Add(tower);
                if (!fearedTowers.Contains(tower))
                {
                    // Nakładamy Aurę
                    tower.isFeared = true;
                    fearedTowers.Add(tower);

                    // Wymuszamy przeliczenie statystyk wieży by uwzględnić karę!
                    tower.GetComponent<TowerEntity>()?.RecalculateStats();
                }
            }
        }
        System.Array.Clear(hitBuffer, 0, hitCount);

        // Szukamy wież, które uciekły z aury (boss sobie poszedł), by zdjąć im karę
        for (int i = fearedTowers.Count - 1; i >= 0; i--)
        {
            TowerController tower = fearedTowers[i];
            if (tower == null || !currentTowersInRadius.Contains(tower))
            {
                if (tower != null)
                {
                    tower.isFeared = false;
                    tower.GetComponent<TowerEntity>()?.RecalculateStats();
                }
                fearedTowers.RemoveAt(i);
            }
        }
    }

    private void RemoveFearFromAll()
    {
        for (int i = 0; i < fearedTowers.Count; i++)
        {
            TowerController tower = fearedTowers[i];
            if (tower != null)
            {
                tower.isFeared = false;
                tower.GetComponent<TowerEntity>()?.RecalculateStats();
            }
        }
        fearedTowers.Clear();
        currentTowersInRadius.Clear();
    }

    public override void OnDeath()
    {
        RemoveFearFromAll();
    }

    private void OnDisable()
    {
        RemoveFearFromAll();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 0f, 0.8f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, auraRadius);
    }
}