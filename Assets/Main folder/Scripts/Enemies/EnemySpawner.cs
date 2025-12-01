using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class EnemySpawner : MonoBehaviour
{
    [Header("Referencje")]
    public HexMapGenerator mapGenerator;
    public FogOfWarManager fogManager;
    public GameObject enemyPrefab;

    [Header("Ustawienia Spawnu")]
    public float spawnInterval = 2.0f;
    public bool spawningActive = true;

    [Header("Balans Fal")]
    public int enemiesPerWave = 10;
    public int additionEnemiesPerWave = 5;

    [Header("Balans Œcie¿ek")]
    public float baseFormulaConst = 10f;
    public float perSpawnerMultiplier = 2.5f;

    [System.Serializable]
    public class SpawnRoute
    {
        public string name;
        [Range(0, 100)] public float currentSpawnChance;
        public List<Vector3> fullPath;
        public Vector3 currentSpawnPoint;
        public List<Vector3> currentActivePath;
        public float fullLength;
    }

    private List<SpawnRoute> allPotentialRoutes = new List<SpawnRoute>();
    [Header("Aktywne Trasy (Podgl¹d)")]
    public List<SpawnRoute> activeRoutes = new List<SpawnRoute>();

    // --- NOWE GIZMOS DO DEBUGOWANIA ---
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || activeRoutes == null) return;

        // Rysowanie WSZYSTKICH potencjalnych tras (¿eby widzieæ jak id¹, nawet te nieaktywne)
        if (allPotentialRoutes != null)
        {
            int colorIndex = 0;
            foreach (var route in allPotentialRoutes)
            {
                if (route.fullPath == null || route.fullPath.Count < 2) continue;

                // Generowanie unikalnego koloru dla trasy (Pastelowe kolory s¹ czytelniejsze)
                Color routeColor = Color.HSVToRGB((float)colorIndex / allPotentialRoutes.Count, 0.7f, 1.0f);
                Gizmos.color = routeColor;

                // Rysowanie linii punkt-po-punkcie
                for (int i = 0; i < route.fullPath.Count - 1; i++)
                {
                    Gizmos.DrawLine(route.fullPath[i], route.fullPath[i + 1]);
                }

                // Ma³a kropka na pocz¹tku trasy
                Gizmos.DrawSphere(route.fullPath[0], 0.2f);

                colorIndex++;
            }
        }

        // Rysowanie AKTYWNYCH SPAWNÓW (Na wierzchu)
        if (activeRoutes != null)
        {
            foreach (var route in activeRoutes)
            {
                // WyraŸny Niebieski Punkt Spawnu (granica mg³y)
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(route.currentSpawnPoint, 0.8f);

                // Opcjonalnie: Grubasza linia od spawnu do bazy, ¿eby widzieæ aktywny odcinek
                Gizmos.color = Color.cyan;
                if (route.currentActivePath != null)
                {
                    for (int i = 0; i < route.currentActivePath.Count - 1; i++)
                    {
                        Vector3 p1 = route.currentActivePath[i];
                        Vector3 p2 = route.currentActivePath[i + 1];
                        // Rysujemy 3 linie obok siebie dla efektu gruboœci
                        Gizmos.DrawLine(p1, p2);
                        Gizmos.DrawLine(p1 + Vector3.up * 0.1f, p2 + Vector3.up * 0.1f);
                        Gizmos.DrawLine(p1 + Vector3.right * 0.1f, p2 + Vector3.right * 0.1f);
                    }
                }
            }
        }
    }

    void Start()
    {
        if (fogManager != null) fogManager.OnChunkRevealed += OnChunkRevealedHandler;
        StartCoroutine(WaitForMapAndSpawn());
    }

    void OnDestroy()
    {
        if (fogManager != null) fogManager.OnChunkRevealed -= OnChunkRevealedHandler;
    }

    IEnumerator WaitForMapAndSpawn()
    {
        yield return null;
        yield return null;
        yield return null;

        if (mapGenerator == null || fogManager == null)
        {
            Debug.LogError("[Spawner] Brak referencji!");
            yield break;
        }

        InitializeRoutes();

        int spawnedInWave = 0;

        while (spawningActive)
        {
            if (GameManager.Instance.currentGameState == GameManager.gameStates.InWave && spawnedInWave < enemiesPerWave)
            {
                if (activeRoutes.Count > 0)
                {
                    SpawnEnemy();
                    spawnedInWave++;
                }

                float currentDelay = spawnInterval - (GameManager.Instance.waveNumber + 1) / 8f;
                if (currentDelay < 0.1f) currentDelay = 0.1f;
                yield return new WaitForSeconds(currentDelay);
            }
            else if (GameManager.Instance.currentGameState == GameManager.gameStates.InWave && spawnedInWave >= enemiesPerWave)
            {
                GameManager.Instance.EndWave();
                spawnedInWave = 0;
                enemiesPerWave += additionEnemiesPerWave;
                yield return null;
            }
            else
            {
                yield return null;
            }
        }
    }

    void InitializeRoutes()
    {
        Debug.Log("[Spawner] Rozpoczynam pobieranie tras...");
        allPotentialRoutes.Clear();

        List<List<Vector3>> rawPaths = mapGenerator.GetAllSpawnPaths();

        // --- RETRY MECHANISM ---
        if (rawPaths == null || rawPaths.Count == 0)
        {
            Debug.LogError("[Spawner] Generator zwróci³ 0 tras! Próbujê ponownie za 1s...");
            StartCoroutine(RetryInitialize());
            return;
        }

        Debug.Log($"[Spawner] Otrzymano {rawPaths.Count} tras.");

        foreach (var path in rawPaths)
        {
            if (path == null || path.Count < 2) continue;

            float length = 0;
            for (int i = 0; i < path.Count - 1; i++) length += Vector3.Distance(path[i], path[i + 1]);

            SpawnRoute newRoute = new SpawnRoute
            {
                fullPath = path,
                fullLength = length,
                name = "Route " + allPotentialRoutes.Count
            };
            allPotentialRoutes.Add(newRoute);
        }

        allPotentialRoutes = allPotentialRoutes.OrderByDescending(r => r.fullLength).ToList();

        // Wymuszamy przeliczenie od razu po za³adowaniu
        RecalculateActiveRoutes();
    }

    IEnumerator RetryInitialize()
    {
        yield return new WaitForSeconds(1.0f);
        InitializeRoutes();
    }

    void OnChunkRevealedHandler(Vector2Int revealedChunk)
    {
        RecalculateActiveRoutes();
    }

    void RecalculateActiveRoutes()
    {
        activeRoutes.Clear();

        foreach (var route in allPotentialRoutes)
        {
            // --- LOGIKA DYNAMICZNEJ GRANICY MG£Y ---
            int foundIndex = -1;

            // Szukamy pierwszego ODKRYTEGO punktu
            for (int i = 0; i < route.fullPath.Count; i++)
            {
                Vector3 point = route.fullPath[i];
                Vector2Int chunkCoord = mapGenerator.GetChunkCoordFromWorldPosition(point);

                if (fogManager.IsChunkRevealed(chunkCoord))
                {
                    foundIndex = i;
                    break;
                }
            }

            // Jeœli znaleziono punkt i nie jest to sama baza
            if (foundIndex != -1 && foundIndex < route.fullPath.Count - 2)
            {
                route.currentSpawnPoint = route.fullPath[foundIndex];
                route.currentActivePath = route.fullPath.GetRange(foundIndex, route.fullPath.Count - foundIndex);

                Vector2Int coords = mapGenerator.GetChunkCoordFromWorldPosition(route.currentSpawnPoint);
                route.name = $"Spawn at Chunk {coords}";

                activeRoutes.Add(route);
            }
        }

        if (activeRoutes.Count == 0) return;

        activeRoutes = activeRoutes.OrderByDescending(r => r.fullLength).ToList();
        int count = activeRoutes.Count;

        if (count == 1)
        {
            activeRoutes[0].currentSpawnChance = 100f;
            activeRoutes[0].name += " [100%]";
        }
        else
        {
            float deduction = baseFormulaConst + (perSpawnerMultiplier * count);
            float mainChance = 100f - deduction;
            if (mainChance < 40f) { mainChance = 40f; deduction = 60f; }

            activeRoutes[0].currentSpawnChance = mainChance;

            float totalMinorLength = 0f;
            for (int i = 1; i < count; i++) totalMinorLength += activeRoutes[i].fullLength;

            for (int i = 1; i < count; i++)
            {
                float share = activeRoutes[i].fullLength / totalMinorLength;
                activeRoutes[i].currentSpawnChance = share * deduction;
            }
        }
    }

    void SpawnEnemy()
    {
        if (enemyPrefab == null || activeRoutes.Count == 0) return;

        SpawnRoute selectedRoute = null;
        float randomVal = UnityEngine.Random.Range(0f, 100f);
        float currentSum = 0f;

        foreach (var route in activeRoutes)
        {
            currentSum += route.currentSpawnChance;
            if (randomVal <= currentSum)
            {
                selectedRoute = route;
                break;
            }
        }
        if (selectedRoute == null) selectedRoute = activeRoutes[0];

        GameObject newEnemy = Instantiate(enemyPrefab, selectedRoute.currentSpawnPoint, Quaternion.identity);
        EnemyWalker walker = newEnemy.GetComponent<EnemyWalker>();

        if (walker != null)
        {
            walker.Initialize(selectedRoute.currentActivePath);
        }
    }
}