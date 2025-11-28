using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [Header("Referencje")]
    public HexMapGenerator mapGenerator;
    public GameObject enemyPrefab;

    [Header("Ustawienia Spawnu")]
    public float spawnInterval = 2.0f;
    public bool spawningActive = true;

    private List<Vector3> cachedPath;

    void Start()
    {
        // Czekamy chwilê na wygenerowanie mapy (prosta metoda)
        // W wersji finalnej lepiej u¿yæ eventów/Action
        StartCoroutine(WaitForMapAndSpawn());
    }

    IEnumerator WaitForMapAndSpawn()
    {
        // Czekamy 2 klatki, ¿eby upewniæ siê, ¿e Start() w Generatorze siê wykona³
        yield return null;
        yield return null;

        if (mapGenerator == null)
        {
            Debug.LogError("Przypisz HexMapGenerator do Spawnera!");
            yield break;
        }

        // Pobieramy trasê RAZ (optymalizacja)
        cachedPath = mapGenerator.GetGlobalWorldPath();

        if (cachedPath == null || cachedPath.Count == 0)
        {
            Debug.LogError("Nie uda³o siê pobraæ œcie¿ki z generatora!");
            yield break;
        }

        // Pêtla spawnowania
        while (spawningActive)
        {
            SpawnEnemy();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    void SpawnEnemy()
    {
        if (enemyPrefab == null) return;

        // Tworzymy wroga (pozycjê ustawi sobie sam w Initialize)
        GameObject newEnemy = Instantiate(enemyPrefab);

        // Pobieramy komponent ruchu i dajemy mu trasê
        EnemyWalker walker = newEnemy.GetComponent<EnemyWalker>();
        if (walker != null)
        {
            walker.Initialize(cachedPath);
        }
        else
        {
            Debug.LogWarning("Prefab wroga nie ma komponentu EnemyWalker!");
        }
    }
}