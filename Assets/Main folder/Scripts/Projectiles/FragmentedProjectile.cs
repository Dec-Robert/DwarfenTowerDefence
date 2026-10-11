using System.Collections.Generic;
using UnityEngine;

public class FragmentedProjectile : ProjectileBase
{
    [Header("Ustawienia Garłacza")]
    public GameObject shardPrefab;      
    public int extraShards = 4;         
    public float spreadRadius = 3.0f;   
    public string targetTag = "Enemy"; 

    // Metoda wywo�ywana przez TowerController
    public override void Launch(Transform mainTarget, Vector3 fallbackPos, Transform firingPoint = null)
    {
        if (shardPrefab == null)
        {
            Destroy(gameObject);
            return;
        }

        // 1. GŁÓWNY CEL
        if (mainTarget != null)
        {
            SpawnShard(mainTarget);
        }
        else
        {
            SpawnShardToGround(fallbackPos);
        }

        // 2. SZUKANIE DODATKOWYCH CELÓW
        List<Transform> potentialTargets = new List<Transform>();
        Vector3 searchCenter = mainTarget != null ? mainTarget.position : fallbackPos;
        float radiusSq = spreadRadius * spreadRadius;

        var activeEnemies = EnemyRegistry.ActiveEnemies;
        for (int i = 0; i < activeEnemies.Count; i++)
        {
            EnemyStats enemy = activeEnemies[i];
            if (enemy == null) continue;
            Transform enemyTr = enemy.transform;
            if (mainTarget != null && enemyTr == mainTarget) continue;

            if ((enemyTr.position - searchCenter).sqrMagnitude <= radiusSq)
            {
                potentialTargets.Add(enemyTr);
            }
        }

        // 3. STRZELANIE ODŁAMKAMI
        for (int i = 0; i < extraShards; i++)
        {
            if (potentialTargets.Count > 0)
            {
                int randomIndex = Random.Range(0, potentialTargets.Count);
                Transform target = potentialTargets[randomIndex];
                SpawnShard(target);
                potentialTargets.RemoveAt(randomIndex);
            }
            else
            {
                SpawnShardToGround(searchCenter);
            }
        }

        Destroy(gameObject);
    }

    void SpawnShard(Transform target)
    {
        GameObject shardObj = Instantiate(shardPrefab, transform.position, Quaternion.identity);
        ProjectileBase bullet = shardObj.GetComponent<ProjectileBase>(); 
        
        if (bullet != null)
        {
            bullet.Initialize(damage, damageType, effects, isCritical, criticalMultiplier, armourPiercing, magicPiercing, 0f, sourceTower);
            bullet.Launch(target, target.position, null);
        }
    }

    void SpawnShardToGround(Vector3 centerPoint)
    {
        Vector2 randomCircle = Random.insideUnitCircle * spreadRadius;
        Vector3 groundPos = centerPoint + new Vector3(randomCircle.x, 0, randomCircle.y);
        
        GameObject dummyTarget = new GameObject("Dummy_Miss_Target");
        dummyTarget.transform.position = groundPos;
        
        SpawnShard(dummyTarget.transform);
        Destroy(dummyTarget, 2.0f);
    }
}