using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class EnemySpawner : MonoBehaviour
{
    [Header("Referencje")]
    public HexMapGenerator mapGenerator;
    public FogOfWarManager fogManager;

    [Header("Pula Wrog�w")]
    // Lista wszystkich wrogow dostępnych do losowania (Szkielet, Blob, Arcanist)
    public List<EnemyData> availableEnemies;

    [Header("Fale Fabularne")]
    // Sztywne rozpiski na konkretne dni (np. Boss w dniu 5)
    public List<WaveDefinition> predefinedWaves;

    [Header("Matematyka Fali (Bud�et Zagro�enia)")]
    public float baseBudget = 4f;      // Startowa siła fali
    public float budgetPerDay = 2f;     // Ile punktów dochodzi co dzień
    public float budgetExponent = 1.1f; // Mnożnik trudności (krzywa)

    [Header("Interwa�y Spawnu")]
    public float nightSpawnInterval = 1.0f; // Szybkość spawnu w nocy (co ile sek)

    [Header("Ambient (Dzie�)")]
    public float daySpawnInterval = 10.0f;  // Co ile sek próba spawnu w dzień
    [Range(0, 100)] public int daySpawnChance = 30; // Szansa na spawn w dzień

    [Header("Balans �cie�ek (Wagi)")]
    public float baseFormulaConst = 10f;
    public float perSpawnerMultiplier = 2.5f;

    // Klasa pomocnicza trasy
    [System.Serializable]
    public class SpawnRoute
    {
        public string name;
        [Range(0, 100)] public float currentSpawnChance;

        public List<Vector3> fullPath;          // Cała trasa (Mapa -> Baza)
        public Vector3 currentSpawnPoint;       // Punkt na granicy mg�y
        public List<Vector3> currentActivePath; // Odcinek (Spawn -> Baza)
        public float fullLength;
    }

    // Listy tras
    private List<SpawnRoute> allPotentialRoutes = new List<SpawnRoute>();
    [Header("Podgl�d Aktywnych Tras")]
    public List<SpawnRoute> activeRoutes = new List<SpawnRoute>();

    // Kolejka wrog�w do zrespienia w bie��cej nocy
    private Queue<EnemyData> enemiesToSpawnQueue = new Queue<EnemyData>();
    private float spawnTimer = 0f;

    // --- CYKL �YCIA ---

    void Start()
    {
        if (fogManager != null) fogManager.OnChunkRevealed += OnChunkRevealedHandler;
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += HandleStateChanged;

        StartCoroutine(WaitForMapAndSpawn());
    }

    void OnDestroy()
    {
        if (fogManager != null) fogManager.OnChunkRevealed -= OnChunkRevealedHandler;
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    // Reakcja na zmian� pory dnia
    void HandleStateChanged(GameManager.gameStates newState)
    {
        // Je�li nadesz�a NOC (InWave), przygotuj kolejk� wrog�w
        if (newState == GameManager.gameStates.InWave)
        {
            spawnTimer = 0f;
            PrepareWave(GameManager.Instance.waveNumber);
        }
    }

    // --- LOGIKA GENEROWANIA FALI ---

    void PrepareWave(int day)
    {
        enemiesToSpawnQueue.Clear();

        // 1. Sprawd� czy mamy predefiniowan� fal� na ten dzie�
        WaveDefinition scriptedWave = null;
        if (predefinedWaves != null)
        {
            scriptedWave = predefinedWaves.Find(w => w.dayNumber == day);
        }

        if (scriptedWave != null)
        {
            // --- SCENARIUSZ SZTYWNY (PREDEFINIOWANY) ---
            Debug.Log($"<color=cyan><b>[Spawner] FALA FABULARNA (Dzie� {day})</b></color>");
            Debug.Log($"<color=cyan>Opis: {scriptedWave.waveMessage}</color>");

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Sk�ad armii:");

            int totalCount = 0;

            foreach (var group in scriptedWave.enemies)
            {
                if (group.enemy != null)
                {
                    // Dodawanie do kolejki
                    for (int i = 0; i < group.count; i++)
                    {
                        enemiesToSpawnQueue.Enqueue(group.enemy);
                    }

                    // Logowanie
                    sb.AppendLine($"- {group.enemy.name}: {group.count} szt.");
                    totalCount += group.count;
                }
            }
            sb.AppendLine($"RAZEM: {totalCount} przeciwnik�w.");
            Debug.Log(sb.ToString());
        }
        else
        {
            // --- SCENARIUSZ MATEMATYCZNY (KREDYTY) ---
            GenerateMathematicalWave(day);
        }
    }

    void GenerateMathematicalWave(int day)
    {
        Debug.Log($"<color=orange><b>[Spawner] FALA MATEMATYCZNA (Dzie� {day})</b></color>");

        // 1. Obliczenie i logowanie bud�etu
        // Wz�r: Baza + (Dni * Przyrost) * (Dni ^ Wyk�adnik)
        float growthFactor = Mathf.Pow(day > 1 ? day : 1, budgetExponent); // oddzielnie dla czytelno�ci
        float budget = baseBudget + (day * budgetPerDay) * growthFactor;

        Debug.Log($"<b>Obliczanie Bud�etu Zagro�enia:</b>\n" +
                  $"Baza ({baseBudget}) + [Dzie� ({day}) * Przyrost ({budgetPerDay})] * Mno�nik ({growthFactor:F2}) = <b>{budget:F1} PKT</b>");

        List<EnemyData> waveList = new List<EnemyData>();

        // S�ownik do zliczania co kupili�my (tylko do log�w)
        Dictionary<string, int> purchaseSummary = new Dictionary<string, int>();

        // 2. Kupowanie wrog�w
        int safetyCounter = 1000;
        float currentBudget = budget;

        while (currentBudget > 0 && safetyCounter > 0)
        {
            safetyCounter--;

            // Znajd� wrog�w, na kt�rych nas sta�
            var affordableEnemies = availableEnemies.Where(e => e.threatCost <= currentBudget).ToList();

            if (affordableEnemies.Count == 0)
            {
                // Je�li zosta�o np. 0.5 pkt bud�etu, a najta�szy wr�g kosztuje 1, ko�czymy
                break;
            }

            // Wylosuj wroga
            EnemyData selected = affordableEnemies[Random.Range(0, affordableEnemies.Count)];

            waveList.Add(selected);
            currentBudget -= selected.threatCost;

            // Zliczanie do raportu
            if (purchaseSummary.ContainsKey(selected.name))
                purchaseSummary[selected.name]++;
            else
                purchaseSummary.Add(selected.name, 1);
        }

        // 3. Raport zakup�w
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("Zakupione jednostki:");
        foreach (var kvp in purchaseSummary)
        {
            // Znajd� koszt tego wroga dla info
            int cost = availableEnemies.Find(e => e.name == kvp.Key).threatCost;
            sb.AppendLine($"- {kvp.Key} (Koszt {cost}): {kvp.Value} szt.");
        }
        sb.AppendLine($"Pozosta�y (niewykorzystany) bud�et: {currentBudget:F1}");
        Debug.Log(sb.ToString());

        // 4. Tasowanie i dodawanie do kolejki
        waveList = ShuffleList(waveList);

        foreach (var enemy in waveList)
        {
            enemiesToSpawnQueue.Enqueue(enemy);
        }
    }

    // --- G��WNA P�TLA (COROUTINE) ---

    IEnumerator WaitForMapAndSpawn()
    {
        // Czekamy na inicjalizacj� innych system�w
        yield return null; yield return null; yield return null;

        if (mapGenerator == null || fogManager == null)
        {
            Debug.LogError("[Spawner] Brak referencji do Mapy lub Mg�y!");
            yield break;
        }

        if (availableEnemies == null || availableEnemies.Count == 0)
        {
            Debug.LogError("[Spawner] Brak zdefiniowanych wrog�w w Available Enemies!");
        }

        InitializeRoutes();

        while (true)
        {
            // Spawnowanie mo�liwe tylko je�li mamy aktywne trasy
            if (activeRoutes.Count > 0)
            {
                // A. TRYB NOCNY (FALA)
                if (GameManager.Instance.currentGameState == GameManager.gameStates.InWave)
                {
                    if (enemiesToSpawnQueue.Count > 0)
                    {
                        spawnTimer += Time.deltaTime;
                        if (spawnTimer >= nightSpawnInterval)
                        {
                            // Pobierz kolejnego wroga z kolejki
                            EnemyData nextEnemy = enemiesToSpawnQueue.Dequeue();
                            SpawnEnemy(nextEnemy);
                            spawnTimer = 0f;
                        }
                    }
                    else
                    {
                        // Kolejka pusta -> Koniec fali
                        GameManager.Instance.EndWave();
                        Debug.Log("[Spawner] Fala zako�czona.");
                    }
                }
                // B. TRYB DZIENNY (AMBIENT)
                else if (GameManager.Instance.currentGameState == GameManager.gameStates.PreparePhase)
                {
                    spawnTimer += Time.deltaTime;
                    if (spawnTimer >= daySpawnInterval)
                    {
                        spawnTimer = 0f;
                        if (Random.Range(0, 100) < daySpawnChance)
                        {
                            // W dzie� spawnujemy losowego s�abego wroga (zwiadowc�)
                            if (availableEnemies.Count > 0)
                            {
                                // Zak�adamy, �e pierwszy na li�cie to najs�abszy (np. Szkielet)
                                // lub losujemy dowolnego
                                var scout = availableEnemies[Random.Range(0, Mathf.Min(2, availableEnemies.Count))];
                                SpawnEnemy(scout);
                                Debug.Log("[Spawner] Zwiadowca.");
                            }
                        }
                    }
                }
            }

            yield return null; // Czekamy klatk�
        }
    }

    // --- SPAWNOWANIE FIZYCZNE ---

    void SpawnEnemy(EnemyData data)
    {
        // 1. Walidacja
        if (activeRoutes.Count == 0 || data.prefab == null) return;

        // 2. Wyb�r Trasy (Weighted Random)
        // Losujemy tras� na podstawie % szans wyliczonych w RecalculateActiveRoutes
        SpawnRoute selectedRoute = null;
        float randomVal = Random.Range(0f, 100f);
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
        // Zabezpieczenie (fallback do pierwszej trasy)
        if (selectedRoute == null) selectedRoute = activeRoutes[0];

        // 3. Instancjonowanie Wroga
        // Spawnujemy w 'currentSpawnPoint', czyli na granicy odkrytego terenu
        GameObject newEnemy = Instantiate(data.prefab, selectedRoute.currentSpawnPoint, Quaternion.identity);

        // 4. Pobieranie modyfikator�w z Beacona (Wp�yw ognia na wrog�w)
        float countMod = 1f; // Nieu�ywane przy pojedynczym spawnie, ale metoda zwraca
        float hpMod = 1f;
        float speedMod = 1f;
        float eliteMod = 1f;

        if (BeaconEntity.Instance != null)
        {
            BeaconEntity.Instance.GetEnemyModifiers(out countMod, out hpMod, out speedMod, out eliteMod);
        }

        // 5. Konfiguracja Statystyk
        EnemyStats stats = newEnemy.GetComponent<EnemyStats>();
        if (stats != null)
        {
            // Przekazujemy Dane + Modyfikatory z Beacona
            // UWAGA: Upewnij si�, �e w EnemyStats masz zaktualizowan� metod� Initialize
            // kt�ra przyjmuje te floaty!
            stats.Initialize(data, hpMod, speedMod, eliteMod);
        }

        // 6. Konfiguracja Ruchu
        EnemyWalker walker = newEnemy.GetComponent<EnemyWalker>();
        if (walker != null)
        {
            // Przekazujemy tylko aktywny odcinek trasy (od spawnu do bazy)
            walker.Initialize(selectedRoute.currentActivePath);
        }
    }

    // --- LOGIKA TRAS I MG�Y ---

    void InitializeRoutes()
    {
        allPotentialRoutes.Clear();
        List<List<Vector3>> rawPaths = mapGenerator.GetAllSpawnPaths();

        if (rawPaths == null)
        {
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

        // Sortujemy malej�co (Najd�u�sza = G��wna)
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
        RecalculateActiveRoutes();
    }

    void RecalculateActiveRoutes()
    {
        activeRoutes.Clear();

        foreach (var route in allPotentialRoutes)
        {
            // Szukamy granicy mg�y na trasie
            int foundIndex = -1;

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

            // Znaleziono punkt startowy z marginesem od bazy
            if (foundIndex != -1 && foundIndex < route.fullPath.Count - 2)
            {
                route.currentSpawnPoint = route.fullPath[foundIndex];
                route.currentActivePath = route.fullPath.GetRange(foundIndex, route.fullPath.Count - foundIndex);

                Vector2Int coords = mapGenerator.GetChunkCoordFromWorldPosition(route.currentSpawnPoint);
                route.name = $"Spawn at {coords}";

                activeRoutes.Add(route);
            }
        }

        if (activeRoutes.Count == 0) return;

        // Obliczanie szans (Wag)
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

    // --- UTILS ---

    List<T> ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
        return list;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || activeRoutes == null) return;

        // Trasy aktywne
        foreach (var route in activeRoutes)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(route.currentSpawnPoint, 0.8f);

            Gizmos.color = Color.cyan;
            if (route.currentActivePath != null)
            {
                for (int i = 0; i < route.currentActivePath.Count - 1; i++)
                {
                    Gizmos.DrawLine(route.currentActivePath[i], route.currentActivePath[i + 1]);
                }
            }
        }
    }
}