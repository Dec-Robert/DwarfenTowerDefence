using System.Collections.Generic;
using UnityEngine;

public class ShadeMinionSkill : EnemySkill
{
    [Header("Konfiguracja Shade'a")]
    [Tooltip("Zasięg, w jakim Shade prowokuje wieże")]
    public float tauntRadius = 5f;

    [Tooltip("Zasięg od głównej bramy (w Hexach), w którym Cień znika nie zadając obrażeń")]
    public int dissolveDistanceToGate = 1;

    [SerializeField] private LayerMask layerMask = ~0;

    private readonly List<TowerController> tauntedTowers = new List<TowerController>();
    private static readonly Collider[] hitBuffer = new Collider[64];

    private Vector3 cachedBasePos;
    private bool basePosCached = false;
    private float scanTimer = 0f;
    private const float ScanInterval = 0.1f;

    private void Awake()
    {
        cachedBasePos = HexGridMath.GetChunkCenterWorld(Vector2Int.zero, 4, 35f, 0f);
        basePosCached = true;
    }

    void Update()
    {
        if (stats == null || walker == null) return;

        scanTimer += Time.deltaTime;
        if (scanTimer >= ScanInterval)
        {
            scanTimer = 0f;
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, tauntRadius, hitBuffer, layerMask);
            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = hitBuffer[i];
                if (hit == null) continue;

                TowerController tower = hit.GetComponent<TowerController>();
                if (tower == null) tower = hit.GetComponentInChildren<TowerController>();
                if (tower == null) tower = hit.GetComponentInParent<TowerController>();

                if (tower != null && !tauntedTowers.Contains(tower))
                {
                    tower.OverrideTargeting(transform);
                    tauntedTowers.Add(tower);
                }
            }
            System.Array.Clear(hitBuffer, 0, hitCount);
        }

        if (!basePosCached)
        {
            cachedBasePos = HexGridMath.GetChunkCenterWorld(Vector2Int.zero, 4, 35f, 0f);
            basePosCached = true;
        }

        // 2. Sprawdzenie odległości od bramy (Jeśli jest blisko - ginie)
        float distanceToGate = Vector3.Distance(transform.position, cachedBasePos);

        // Upraszczając: 1 hex to ok. 35 jednostek. Więc jeśli dystans < 50, niszczymy go
        if (distanceToGate <= (35f * dissolveDistanceToGate) + 15f)
        {
            Debug.Log($"<color=magenta>Shade rozprasza się przed uderzeniem w bramę!</color>");
            OnDeath(); // Ważne, by wyczyścić wieże!
            Destroy(stats.gameObject);
        }
    }

    private void ClearAllTaunts()
    {
        for (int i = 0; i < tauntedTowers.Count; i++)
        {
            TowerController tower = tauntedTowers[i];
            if (tower != null) tower.ClearOverride();
        }
        tauntedTowers.Clear();
    }

    public override void OnDeath()
    {
        ClearAllTaunts();
    }

    private void OnDisable()
    {
        ClearAllTaunts();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, tauntRadius);
    }
}