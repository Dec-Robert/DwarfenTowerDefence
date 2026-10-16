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

    public IReadOnlyList<TowerController> FearedTowers => fearedTowers;

    void Update()
    {
        scanTimer += Time.deltaTime;
        if (scanTimer < ScanInterval) return;
        scanTimer = 0f;

        ScanTowers();
    }

    public void ScanTowers()
    {
        currentTowersInRadius.Clear();

        // 1. Sprawdź wieże zarejestrowane w BuildingRegistry (brak fizycznych colliderów na prefabach wież)
        if (BuildingRegistry.Instance != null)
        {
            var towerEntities = BuildingRegistry.Instance.GetAllOfType<TowerEntity>();
            for (int i = 0; i < towerEntities.Count; i++)
            {
                TowerEntity entity = towerEntities[i];
                if (entity == null) continue;
                TowerController tower = entity.controller ?? entity.GetComponent<TowerController>();
                if (tower != null && Vector3.Distance(transform.position, entity.transform.position) <= auraRadius)
                {
                    ApplyFearToTower(tower);
                }
            }
        }
        else
        {
            // Fallback: bezpośrednie wyszukanie kontrolerów wież na scenie
            var allTowers = Object.FindObjectsByType<TowerController>(FindObjectsSortMode.None);
            for (int i = 0; i < allTowers.Length; i++)
            {
                TowerController tower = allTowers[i];
                if (tower != null && Vector3.Distance(transform.position, tower.transform.position) <= auraRadius)
                {
                    ApplyFearToTower(tower);
                }
            }
        }

        // 2. Dodatkowo sprawdź collidery (gdyby obiekt wieży posiadał collider)
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, auraRadius, hitBuffer, layerMask);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitBuffer[i];
            if (hit == null) continue;

            TowerController tower = hit.GetComponent<TowerController>()
                                 ?? hit.GetComponentInChildren<TowerController>()
                                 ?? hit.GetComponentInParent<TowerController>();
            if (tower != null)
            {
                ApplyFearToTower(tower);
            }
        }
        System.Array.Clear(hitBuffer, 0, hitCount);

        // 3. Zdejmij efekt paniki z wież, które opuściły zasięg aury
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

    private void ApplyFearToTower(TowerController tower)
    {
        currentTowersInRadius.Add(tower);
        if (!fearedTowers.Contains(tower))
        {
            tower.isFeared = true;
            fearedTowers.Add(tower);
            tower.GetComponent<TowerEntity>()?.RecalculateStats();
        }
    }

    public void RemoveFearFromAll()
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

    public override void OnDeath(DeathSource source = DeathSource.Combat)
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