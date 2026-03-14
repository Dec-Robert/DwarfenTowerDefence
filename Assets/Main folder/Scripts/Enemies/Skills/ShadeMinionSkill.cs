using UnityEngine;
using System.Collections.Generic;

public class ShadeMinionSkill : EnemySkill
{
    [Header("Konfiguracja Shade'a")]
    [Tooltip("Zasięg, w jakim Shade prowokuje wieże")]
    public float tauntRadius = 5f;
    
    [Tooltip("Zasięg od głównej bramy (w Hexach), w którym Cień znika nie zadając obrażeń")]
    public int dissolveDistanceToGate = 1;

    // Śledzimy, które wieże sprowokowaliśmy, by po śmierci Cienia zdjąć z nich Override
    private List<TowerController> tauntedTowers = new List<TowerController>();

    void Update()
    {
        if (stats == null || walker == null) return;

        // 1. Prowokacja okolicznych wież
        Collider[] hits = Physics.OverlapSphere(transform.position, tauntRadius);
        foreach (var hit in hits)
        {
            TowerController tower = hit.GetComponent<TowerController>();
            if (tower != null && !tauntedTowers.Contains(tower))
            {
                tower.OverrideTargeting(transform);
                tauntedTowers.Add(tower);
            }
        }

        // 2. Sprawdzenie odległości od bramy (Jeśli jest blisko - ginie)
        // Pobieramy globalną pozycję Hexa bazy (0,0)
        Vector3 basePos = HexGridMath.GetChunkCenterWorld(Vector2Int.zero, 4, 35f, 0f);
        float distanceToGate = Vector3.Distance(transform.position, basePos);

        // Upraszczając: 1 hex to ok. 35 jednostek. Więc jeśli dystans < 50, niszczymy go
        if (distanceToGate <= (35f * dissolveDistanceToGate) + 15f)
        {
            Debug.Log($"<color=magenta>Shade rozprasza się przed uderzeniem w bramę!</color>");
            OnDeath(); // Ważne, by wyczyścić wieże!
            Destroy(stats.gameObject);
        }
    }

    public override void OnDeath()
    {
        // Zwalniamy wszystkie wieże, by znów mogły normalnie strzelać
        foreach (var tower in tauntedTowers)
        {
            if (tower != null) tower.ClearOverride();
        }
        tauntedTowers.Clear();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, tauntRadius);
    }
}