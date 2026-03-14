using UnityEngine;
using System.Collections.Generic;

public class MaddeningGazeSkill : EnemySkill
{
    [Header("Konfiguracja Gaze")]
    [Tooltip("Promień, w jakim wieże wpadają w panikę (Zmniejszona wydajność)")]
    public float auraRadius = 25f;

    private List<TowerController> fearedTowers = new List<TowerController>();

    void Update()
    {
        // 1. Skanujemy teren co klatkę (lub z timerem by oszczędzić CPU) szukając wież
        Collider[] hits = Physics.OverlapSphere(transform.position, auraRadius);
        List<TowerController> currentTowersInRadius = new List<TowerController>();

        foreach (var hit in hits)
        {
            TowerController tower = hit.GetComponent<TowerController>();
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

        // 2. Szukamy wież, które uciekły z aury (boss sobie poszedł), by zdjąć im karę
        for (int i = fearedTowers.Count - 1; i >= 0; i--)
        {
            if (!currentTowersInRadius.Contains(fearedTowers[i]))
            {
                var escapedTower = fearedTowers[i];
                escapedTower.isFeared = false;
                escapedTower.GetComponent<TowerEntity>()?.RecalculateStats(); // Wracają do sił
                fearedTowers.RemoveAt(i);
            }
        }
    }

    public override void OnDeath()
    {
        // Zdejmujemy aurę ze wszystkich przy śmierci!
        foreach (var tower in fearedTowers)
        {
            if (tower != null)
            {
                tower.isFeared = false;
                tower.GetComponent<TowerEntity>()?.RecalculateStats();
            }
        }
        fearedTowers.Clear();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 0f, 0.8f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, auraRadius);
    }
}