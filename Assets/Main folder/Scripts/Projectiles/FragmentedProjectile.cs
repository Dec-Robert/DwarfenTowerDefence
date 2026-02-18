using System.Collections.Generic;
using UnityEngine;

public class FragmentedProjectile : ProjectileBase
{
    [Header("Ustawienia Gar³acza")]
    public GameObject shardPrefab;      // Prefab ma³ego od³amka
    public int extraShards = 4;         // Iloœæ dodatkowych od³amków
    public float spreadRadius = 3.0f;   // Rozrzut wokó³ celu
    public string targetTag = "Enemy";  // Tag do szukania wrogów

    // Metoda wywo³ywana przez TowerController
    public void LaunchAt(Transform mainTarget)
    {
        // Walidacja
        if (shardPrefab == null)
        {
            Debug.LogError($"[FragmentedProjectile] BRAK PREFABU OD£AMKA w obiekcie {name}!");
            Destroy(gameObject);
            return;
        }

        // --- 1. GLÓWNY CEL ---
        // Strzelamy w cel, w który celowa³a wie¿a
        if (mainTarget != null)
        {
            SpawnShard(mainTarget);
        }
        else
        {
            // Jeœli cel zgin¹³ w u³amku sekundy przed strza³em, strzel w ziemiê tam gdzie celowaliœmy
            // (transform.forward to kierunek lufy)
            Vector3 estimatedHitPos = transform.position + transform.forward * 5f; // strza³ przed siebie
            SpawnShardToGround(estimatedHitPos);
        }

        // --- 2. SZUKANIE DODATKOWYCH CELÓW ---
        List<Transform> potentialTargets = new List<Transform>();

        // Punkt centralny poszukiwañ (cel lub lufa, jeœli cel znikn¹³)
        Vector3 searchCenter = mainTarget != null ? mainTarget.position : (transform.position + transform.forward * 5f);

        Collider[] hits = Physics.OverlapSphere(searchCenter, spreadRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag(targetTag))
            {
                // Nie celujemy drugi raz w g³ównego wroga
                if (mainTarget != null && hit.transform == mainTarget) continue;

                if (hit.GetComponent<EnemyStats>() != null)
                {
                    potentialTargets.Add(hit.transform);
                }
            }
        }

        // --- 3. STRZELANIE OD£AMKAMI ---
        for (int i = 0; i < extraShards; i++)
        {
            if (potentialTargets.Count > 0)
            {
                // A) Mamy wroga - losujemy i strzelamy
                int randomIndex = Random.Range(0, potentialTargets.Count);
                Transform target = potentialTargets[randomIndex];

                SpawnShard(target);

                // Usuwamy z listy, ¿eby nie trafiæ go 2 razy (chyba ¿e chcesz shotgun w jednego)
                potentialTargets.RemoveAt(randomIndex);
            }
            else
            {
                // B) Brak wrogów - strzelamy w losowy punkt na ziemi (efekt rozrzutu)
                SpawnShardToGround(searchCenter);
            }
        }

        // Pocisk-Matka (Manager) wykona³ zadanie - niszczymy go natychmiast
        // Shardy s¹ ju¿ niezale¿nymi obiektami
        Destroy(gameObject);
    }

    void SpawnShard(Transform target)
    {
        // Tworzymy od³amek dok³adnie w miejscu lufy (tam gdzie jest ten skrypt)
        GameObject shardObj = Instantiate(shardPrefab, transform.position, Quaternion.identity);

        // DEBUG: Rysujemy liniê w edytorze, ¿eby widzieæ gdzie leci
        Debug.DrawLine(transform.position, target.position, Color.yellow, 0.5f);

        SimpleBullet bullet = shardObj.GetComponent<SimpleBullet>();
        if (bullet != null)
        {
            // Przekazujemy dane, które ten skrypt (ProjectileBase) otrzyma³ w Initialize
            // Zak³adam, ¿e w ProjectileBase masz protected zmienne: damage, damageType, effects
            bullet.Initialize(damage, damageType, effects,isCritical,criticalMultiplier);
            bullet.Seek(target);
        }
        else
        {
            Debug.LogError("Prefab od³amka nie ma komponentu SimpleBullet!");
        }
    }

    void SpawnShardToGround(Vector3 centerPoint)
    {
        // Losujemy punkt na p³aszczyŸnie XZ wokó³ celu
        Vector2 randomCircle = Random.insideUnitCircle * spreadRadius;
        Vector3 groundPos = centerPoint + new Vector3(randomCircle.x, 0, randomCircle.y);

        // Tworzymy pusty obiekt jako cel, ¿eby SimpleBullet mia³ w co lecieæ
        GameObject dummyTarget = new GameObject("Dummy_Miss_Target");
        dummyTarget.transform.position = groundPos;

        SpawnShard(dummyTarget.transform);

        // Niszczymy cel po 2 sekundach (pocisk powinien dolecieæ szybciej)
        Destroy(dummyTarget, 2.0f);
    }
}