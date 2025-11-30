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

    public int enemiesPerWave = 10;
    public int additionEnemiesPerWave = 5;

    private List<Vector3> cachedPath;

    void Start()
    {
        StartCoroutine(WaitForMapAndSpawn());
    }

    IEnumerator WaitForMapAndSpawn()
    {
        yield return null;
        yield return null;

        if (mapGenerator == null)
        {
            Debug.LogError("Przypisz HexMapGenerator do Spawnera!");
            yield break;
        }

        cachedPath = mapGenerator.GetGlobalWorldPath();

        if (cachedPath == null || cachedPath.Count == 0)
        {
            Debug.LogError("Nie uda³o siê pobraæ œcie¿ki z generatora!");
            yield break;
        }

        int spawnedInWave = 0;

        // Pêtla spawnowania
        while (spawningActive)
        {
            // Sytuacja 1: Trwa fala i mamy kogo spawnowaæ
            if (GameManager.Instance.currentGameState == GameManager.gameStates.InWave && spawnedInWave < enemiesPerWave)
            {
                SpawnEnemy();
                spawnedInWave++;

                // Obliczenie czasu (doda³em 'f' przy 8, ¿eby dzielenie by³o zmiennoprzecinkowe)
                float currentDelay = spawnInterval - (GameManager.Instance.waveNumber + 1) / 8f;
                // Zabezpieczenie, ¿eby delay nie by³ ujemny
                if (currentDelay < 0.1f) currentDelay = 0.1f;

                yield return new WaitForSeconds(currentDelay);
            }
            // Sytuacja 2: Trwa fala, ale wyczerpaliœmy limit wrogów -> Koñczymy falê
            else if (GameManager.Instance.currentGameState == GameManager.gameStates.InWave && spawnedInWave >= enemiesPerWave)
            {
                GameManager.Instance.EndWave();
                spawnedInWave = 0;
                enemiesPerWave = enemiesPerWave + additionEnemiesPerWave;

                // WA¯NE: Musimy poczekaæ klatkê po zmianie stanu, ¿eby nie wpaœæ w pêtlê
                yield return null;
            }
            // Sytuacja 3: Faza PreparePhase lub GameOver -> Czekamy
            else
            {
                // TO JEST LINIKA, KTÓRA NAPRAWIA ZAWIESZANIE
                // Jeœli nie spawnowaliœmy i nie koñczyliœmy fali, czekamy do nastêpnej klatki
                yield return null;
            }
        }
    }

    void SpawnEnemy()
    {
        if (enemyPrefab == null) return;
        GameObject newEnemy = Instantiate(enemyPrefab);
        EnemyWalker walker = newEnemy.GetComponent<EnemyWalker>();
        if (walker != null)
        {
            walker.Initialize(cachedPath);
        }
    }
}