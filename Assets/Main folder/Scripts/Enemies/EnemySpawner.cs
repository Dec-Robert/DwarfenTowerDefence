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

    [Header("Ustawienia Fali (NOC)")]
    public float nightSpawnInterval = 1.0f; // Jak szybko wychodz¹ w nocy
    public int enemiesPerWave = 10;
    public int additionEnemiesPerWave = 5;

    [Header("Ustawienia Ambientu (DZIEÑ)")]
    public float daySpawnInterval = 10.0f; // Co ile sekund próba spawnu w dzieñ
    [Range(0, 100)] public int daySpawnChance = 30; // Szansa w % ¿e wróg siê pojawi

    [Header("Balans Œcie¿ek (Wagi)")]
    public float baseFormulaConst = 10f;
    public float perSpawnerMultiplier = 2.5f;

    [System.Serializable]
    public class SpawnRoute
    {
        public string name;
        [Range(0, 100)] public float currentSpawnChance;

        // Pe³na trasa (od krawêdzi mapy do bazy)
        public List<Vector3> fullPath;

        // Aktualny punkt startu (przesuwa siê wraz z odkrywaniem mapy)
        public Vector3 currentSpawnPoint;

        // Wycinek trasy od spawnu do bazy (to dostaje przeciwnik)
        public List<Vector3> currentActivePath;

        public float fullLength;
    }

    // Wszystkie trasy pobrane z generatora
    private List<SpawnRoute> allPotentialRoutes = new List<SpawnRoute>();

    // Tylko te trasy, których start jest odkryty (widoczny)
    [Header("Aktywne Trasy (Podgl¹d)")]
    public List<SpawnRoute> activeRoutes = new List<SpawnRoute>();

    // Liczniki wewnêtrzne
    private int spawnedInCurrentWave = 0;
    private float spawnTimer = 0f;

    // --- CYKL ¯YCIA ---

    void Start()
    {
        // Subskrypcja odkrywania mg³y
        if (fogManager != null)
            fogManager.OnChunkRevealed += OnChunkRevealedHandler;

        // Subskrypcja zmiany stanu gry (Dzieñ/Noc)
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged += HandleStateChanged;

        StartCoroutine(WaitForMapAndSpawn());
    }

    void OnDestroy()
    {
        if (fogManager != null)
            fogManager.OnChunkRevealed -= OnChunkRevealedHandler;

        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    // Resetowanie liczników gdy nastaje Noc
    void HandleStateChanged(GameManager.gameStates newState)
    {
        if (newState == GameManager.gameStates.InWave)
        {
            spawnedInCurrentWave = 0;
            spawnTimer = 0f;
            Debug.Log($"[Spawner] Noc nadesz³a! Fala wielkoœci: {enemiesPerWave}");
        }
    }

    // --- G£ÓWNA PÊTLA LOGIKI ---

    IEnumerator WaitForMapAndSpawn()
    {
        // Czekamy na inicjalizacjê mapy
        yield return null;
        yield return null;
        yield return null;

        if (mapGenerator == null || fogManager == null)
        {
            Debug.LogError("[Spawner] Brak przypisanego MapGeneratora lub FogManagera!");
            yield break;
        }

        // Pobieramy trasy po raz pierwszy
        InitializeRoutes();

        while (true) // Nieskoñczona pêtla (zastêpuje Update)
        {
            // Dzia³amy tylko jeœli mamy sk¹d spawnowaæ (odkryte œcie¿ki)
            if (activeRoutes.Count > 0)
            {
                // 1. TRYB NOCNY (FALA)
                if (GameManager.Instance.currentGameState == GameManager.gameStates.InWave)
                {
                    if (spawnedInCurrentWave < enemiesPerWave)
                    {
                        spawnTimer += Time.deltaTime;
                        if (spawnTimer >= nightSpawnInterval)
                        {
                            SpawnEnemy();
                            spawnedInCurrentWave++;
                            spawnTimer = 0f;
                        }
                    }
                    else
                    {
                        // Fala zakoñczona - powiadamiamy GameManager
                        // GameManager zmieni stan na PreparePhase, ale dopiero o œwicie?
                        // LUB: Koñczymy falê logicznie (zwiêkszamy trudnoœæ), a czas p³ynie dalej.
                        // Tutaj wywo³ujemy EndWave, co w Twoim GameManagerze zmienia stan na PreparePhase od razu.
                        // Jeœli wolisz czekaæ na œwit, usuñ EndWave st¹d.

                        GameManager.Instance.EndWave();
                        enemiesPerWave += additionEnemiesPerWave;
                        Debug.Log("[Spawner] Limit fali wyczerpany. Koniec ataku.");
                    }
                }
                // 2. TRYB DZIENNY (AMBIENT / ZWIADOWCY)
                else if (GameManager.Instance.currentGameState == GameManager.gameStates.PreparePhase)
                {
                    spawnTimer += Time.deltaTime;
                    if (spawnTimer >= daySpawnInterval)
                    {
                        spawnTimer = 0f;
                        // Losujemy czy zrespiæ zwiadowcê
                        if (Random.Range(0, 100) < daySpawnChance)
                        {
                            SpawnEnemy();
                            Debug.Log("[Spawner] Pojawi³ siê dzienny zwiadowca.");
                        }
                    }
                }
            }
            else
            {
                // Brak aktywnych tras - mo¿e gracz jeszcze nie odkry³ wyjœcia z bazy?
            }

            yield return null; // Czekamy jedn¹ klatkê
        }
    }

    // --- LOGIKA TRAS I MG£Y ---

    void InitializeRoutes()
    {
        allPotentialRoutes.Clear();
        // Pobieramy trasy z generatora (ignorujemy tagi, u¿ywamy geometrii)
        List<List<Vector3>> rawPaths = mapGenerator.GetAllSpawnPaths();

        if (rawPaths == null)
        {
            // Retry mechanism w razie gdyby mapa jeszcze siê nie zrobi³a
            StartCoroutine(RetryInitialize());
            return;
        }

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

        // Sortujemy: Najd³u¿sza trasa to zazwyczaj ta g³ówna
        allPotentialRoutes = allPotentialRoutes.OrderByDescending(r => r.fullLength).ToList();

        RecalculateActiveRoutes();
    }

    IEnumerator RetryInitialize()
    {
        yield return new WaitForSeconds(1f);
        InitializeRoutes();
    }

    void OnChunkRevealedHandler(Vector2Int revealedChunk)
    {
        // Gdy mg³a znika, spawny mog¹ siê przesun¹æ
        RecalculateActiveRoutes();
    }

    void RecalculateActiveRoutes()
    {
        activeRoutes.Clear();

        foreach (var route in allPotentialRoutes)
        {
            // Szukamy pierwszego punktu na trasie (od dalekiego startu do bazy), który jest ODKRYTY.
            int foundIndex = -1;

            for (int i = 0; i < route.fullPath.Count; i++)
            {
                Vector3 point = route.fullPath[i];
                Vector2Int chunkCoord = mapGenerator.GetChunkCoordFromWorldPosition(point);

                if (fogManager.IsChunkRevealed(chunkCoord))
                {
                    foundIndex = i;
                    break; // ZnaleŸliœmy granicê mg³y
                }
            }

            // Jeœli znaleziono punkt i nie jest to sama baza (margines bezpieczeñstwa)
            if (foundIndex != -1 && foundIndex < route.fullPath.Count - 2)
            {
                route.currentSpawnPoint = route.fullPath[foundIndex];

                // Kopiujemy resztê trasy (od granicy mg³y do bazy)
                route.currentActivePath = route.fullPath.GetRange(foundIndex, route.fullPath.Count - foundIndex);

                Vector2Int coords = mapGenerator.GetChunkCoordFromWorldPosition(route.currentSpawnPoint);
                route.name = $"Spawn at Chunk {coords}";

                activeRoutes.Add(route);
            }
        }

        if (activeRoutes.Count == 0) return;

        // --- PRZELICZANIE SZANS (WAGI) ---
        // Sortujemy aktywne trasy po d³ugoœci (Najd³u¿sza = G³ówna)
        activeRoutes = activeRoutes.OrderByDescending(r => r.fullLength).ToList();
        int count = activeRoutes.Count;

        if (count == 1)
        {
            activeRoutes[0].currentSpawnChance = 100f;
            activeRoutes[0].name += " [MAIN]";
        }
        else
        {
            float deduction = baseFormulaConst + (perSpawnerMultiplier * count);
            float mainChance = 100f - deduction;
            if (mainChance < 40f) { mainChance = 40f; deduction = 60f; }

            activeRoutes[0].currentSpawnChance = mainChance;
            activeRoutes[0].name += " [MAIN]";

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

        // Losowanie trasy na podstawie wag
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

        // Spawnowanie
        GameObject newEnemy = Instantiate(enemyPrefab, selectedRoute.currentSpawnPoint, Quaternion.identity);
        EnemyWalker walker = newEnemy.GetComponent<EnemyWalker>();

        if (walker != null)
        {
            walker.Initialize(selectedRoute.currentActivePath);
        }
    }

    // --- DEBUG GIZMOS ---
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || activeRoutes == null) return;

        // 1. Rysuj wszystkie trasy (t³o)
        if (allPotentialRoutes != null)
        {
            int colorIndex = 0;
            foreach (var route in allPotentialRoutes)
            {
                if (route.fullPath == null || route.fullPath.Count < 2) continue;

                Color routeColor = Color.HSVToRGB((float)colorIndex / allPotentialRoutes.Count, 0.5f, 0.5f);
                Gizmos.color = routeColor;

                for (int i = 0; i < route.fullPath.Count - 1; i++)
                {
                    Gizmos.DrawLine(route.fullPath[i], route.fullPath[i + 1]);
                }
                colorIndex++;
            }
        }

        // 2. Rysuj AKTYWNE odcinki i punkty spawnu (na wierzchu)
        if (activeRoutes != null)
        {
            foreach (var route in activeRoutes)
            {
                // Niebieska Kula = Punkt Spawnu (Granica Mg³y)
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(route.currentSpawnPoint, 0.8f);

                // Cyjanowa linia = Droga wroga do bazy
                Gizmos.color = Color.cyan;
                if (route.currentActivePath != null)
                {
                    for (int i = 0; i < route.currentActivePath.Count - 1; i++)
                    {
                        Vector3 p1 = route.currentActivePath[i];
                        Vector3 p2 = route.currentActivePath[i + 1];

                        // Pogrubiona linia (3 kreski)
                        Gizmos.DrawLine(p1, p2);
                        Gizmos.DrawLine(p1 + Vector3.up * 0.2f, p2 + Vector3.up * 0.2f);
                        Gizmos.DrawLine(p1 + Vector3.right * 0.2f, p2 + Vector3.right * 0.2f);
                    }
                }
            }
        }
    }
}