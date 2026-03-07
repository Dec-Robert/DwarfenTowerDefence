using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Koordynator systemu fal. Łączy 4 moduły:
///   1. WavePaletteSystem     – era + paleta operacyjna
///   2. WaveThreatCalculator  – budżet + typ trudności
///   3. EnemyStatScaler       – skalowanie statystyk per fala (statyczna klasa)
///   4. AdaptiveFeedbackSystem– echo błędów (tracking + priorytet)
/// 
/// Sam Spawner jest cienką warstwą: zarządza fizycznym spawnowaniem,
/// trasami i cyklem gry. Logika fal żyje w modułach.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    // =========================================================================
    // REFERENCJE I KONFIGURACJA
    // =========================================================================

    [Header("── Referencje Mapy ──────────────────────")]
    public HexMapGenerator mapGenerator;
    public FogOfWarManager fogManager;

    [Header("── Konfiguracja Systemu Fal ─────────────")]
    [Tooltip("ScriptableObject z całą konfiguracją liczbową")]
    public WaveScalingConfig waveConfig;

    [Header("── Pula Wrogów ──────────────────────────")]
    [Tooltip("KOLEJNOŚĆ MA ZNACZENIE – kolejne indeksy odblokowywane przez ery")]
    public List<EnemyData> availableEnemies;

    [Header("── Fale Fabularne ─────────────────────────")]
    [Tooltip("Sztywne rozpiski na konkretne fale (Boss, eventy)")]
    public List<WaveDefinition> predefinedWaves;

    [Header("── Interwały Spawnu ──────────────────────")]
    public float nightSpawnInterval = 1.0f;
    public float daySpawnInterval   = 10.0f;
    [Range(0, 100)] public int daySpawnChance = 30;

    [Header("── Wagi Tras ────────────────────────────")]
    public float baseFormulaConst      = 3f;
    public float perSpawnerMultiplier  = 2.5f;

    // =========================================================================
    // STAN WEWNĘTRZNY
    // =========================================================================

    // Moduły
    private WavePaletteSystem        paletteSystem;
    private WaveThreatCalculator     threatCalculator;
    private AdaptiveFeedbackSystem   echoSystem;

    // Kolejka spawnu bieżącej fali
    private Queue<EnemyData> enemiesToSpawnQueue = new Queue<EnemyData>();
    private float spawnTimer;

    // Numer bieżącej fali (cache – żeby nie pytać GameManager co klatkę)
    private int currentWaveNumber = 0;

    // ─── Trasy ───────────────────────────────────────────────────────────────

    [System.Serializable]
    public class SpawnRoute
    {
        public string name;
        [Range(0, 100)] public float currentSpawnChance;
        public List<Vector3> fullPath;
        public Vector3       currentSpawnPoint;
        public List<Vector3> currentActivePath;
        public float         fullLength;
    }

    private List<SpawnRoute> allPotentialRoutes = new List<SpawnRoute>();

    [Header("── Podgląd Aktywnych Tras ─────────────────")]
    public List<SpawnRoute> activeRoutes = new List<SpawnRoute>();

    // =========================================================================
    // CYKL ŻYCIA
    // =========================================================================

    void Awake()
    {
        if (waveConfig == null)
        {
            Debug.LogError("[Spawner] Brak WaveScalingConfig! Przypisz asset w Inspectorze.");
            return;
        }

        paletteSystem    = new WavePaletteSystem(waveConfig, availableEnemies);
        threatCalculator = new WaveThreatCalculator(waveConfig);
        echoSystem       = new AdaptiveFeedbackSystem(waveConfig);
    }

    void Start()
    {
        if (fogManager != null)  fogManager.OnChunkRevealed     += OnChunkRevealedHandler;
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += HandleStateChanged;

        StartCoroutine(WaitForMapAndInit());
    }

    void OnDestroy()
    {
        if (fogManager != null)  fogManager.OnChunkRevealed     -= OnChunkRevealedHandler;
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    // =========================================================================
    // OBSŁUGA ZMIANY STANU GRY
    // =========================================================================

    void HandleStateChanged(GameManager.gameStates newState)
    {
        if (newState == GameManager.gameStates.InWave)
        {
            spawnTimer        = 0f;
            currentWaveNumber = GameManager.Instance.waveNumber;
            PrepareWave(currentWaveNumber);
        }
    }

    // =========================================================================
    // PRZYGOTOWANIE FALI
    // =========================================================================

    void PrepareWave(int waveNumber)
    {
        enemiesToSpawnQueue.Clear();

        if (TimeCycleManager.Instance != null && waveNumber <= TimeCycleManager.Instance.gracePeriodDays)
        {
            Debug.Log($"<color=green>[Spawner] Dzień {waveNumber} to Grace Period. Pomijam generowanie fali.</color>");
            return; 
        }
        // Moduł Echo – aktualizacja zagrożeń na początku fali
        echoSystem.OnWaveStarted(waveNumber);

        // 1. Czy mamy fabularne (predefiniowane) przypisanie?
        var scriptedWave = predefinedWaves?.Find(w => w.dayNumber == waveNumber);
        if (scriptedWave != null)
        {
            PrepareScriptedWave(scriptedWave, waveNumber);
            return;
        }

        // 2. Paleta operacyjna (nowa jeśli nowy blok)
        paletteSystem.EnsurePaletteForWave(waveNumber);

        // 3. Wymuś zagrożone typy do palety (Echo)
        foreach (var threat in echoSystem.GetAllThreats())
            paletteSystem.ForceAddToMain(threat);

        // 4. Budżet
        float budget = threatCalculator.CalculateBudget(
            waveNumber,
            out WaveThreatCalculator.WaveDifficultyType diffType);

        // 5. Echo – priorytetowy typ i jego udział w budżecie
        EnemyData echoPriority  = echoSystem.GetPriorityThreat();
        float     echoBudgetShare = (echoPriority != null) ? waveConfig.echoPriorityBudgetShare : 0f;

        // 6. Zakup wrogów
        enemiesToSpawnQueue = threatCalculator.SpendBudget(
            budget,
            paletteSystem,
            echoPriority,
            echoBudgetShare);

        Debug.Log($"<color=yellow>[Spawner] Fala {waveNumber} ({diffType}): " +
                  $"{enemiesToSpawnQueue.Count} wrogów | " +
                  $"Paleta: {paletteSystem.GetPaletteDebugString()}</color>");
    }

    void PrepareScriptedWave(WaveDefinition scriptedWave, int waveNumber)
    {
        Debug.Log($"<color=cyan><b>[Spawner] FALA FABULARNA (Fala {waveNumber}): {scriptedWave.waveMessage}</b></color>");
        foreach (var group in scriptedWave.enemies)
        {
            if (group.enemy == null) continue;
            for (int i = 0; i < group.count; i++)
                enemiesToSpawnQueue.Enqueue(group.enemy);
        }
    }

    // =========================================================================
    // GŁÓWNA PĘTLA SPAWNU
    // =========================================================================

    IEnumerator WaitForMapAndInit()
    {
        yield return null; yield return null; yield return null;

        if (mapGenerator == null || fogManager == null)
        {
            Debug.LogError("[Spawner] Brak referencji do Mapy lub Mgły!");
            yield break;
        }

        InitializeRoutes();

        while (true)
        {
            if (activeRoutes.Count > 0)
            {
                var state = GameManager.Instance.currentGameState;

                if (state == GameManager.gameStates.InWave)
                {
                    if (enemiesToSpawnQueue.Count > 0)
                    {
                        spawnTimer += Time.deltaTime;
                        if (spawnTimer >= nightSpawnInterval)
                        {
                            SpawnEnemy(enemiesToSpawnQueue.Dequeue());
                            spawnTimer = 0f;
                        }
                    }
                    else
                    {
                        // Koniec fali – kara dla ocalałych wrogów (meta upgrade)
                        MetaUpgradeManager.Instance?.ApplySurvivorPenaltiesToAll();

                        // Powiadamiamy Echo i GameManager
                        echoSystem.OnWaveEnded(currentWaveNumber);
                        GameManager.Instance.EndWave();
                    }
                }
                else if (state == GameManager.gameStates.PreparePhase)
                {
                    spawnTimer += Time.deltaTime;
                    if (spawnTimer >= daySpawnInterval)
                    {
                        spawnTimer = 0f;
                        if (Random.Range(0, 100) < daySpawnChance && availableEnemies.Count > 0)
                        {
                            // Scout: losowy z pierwszych 2 wrogów (najsłabsi)
                            var scout = availableEnemies[Random.Range(0, Mathf.Min(2, availableEnemies.Count))];
                            SpawnEnemy(scout);
                        }
                    }
                }
            }

            yield return null;
        }
    }

    // =========================================================================
    // FIZYCZNE SPAWNOWANIE
    // =========================================================================

    void SpawnEnemy(EnemyData data)
    {
        if (activeRoutes.Count == 0 || 
            data?.prefab == null || 
            TimeCycleManager.Instance.gracePeriodDays > TimeCycleManager.Instance.dayCount) return;

        // Wybór trasy (weighted random)
        SpawnRoute route = SelectRoute();

        // Instancja
        GameObject obj = Instantiate(data.prefab, route.currentSpawnPoint, Quaternion.identity);

        // ── Skalowanie statystyk ──────────────────────────────────────────────
        float beaconHp    = 1f;
        float beaconSpeed = 1f;
        float beaconElite = 1f;

        if (BeaconEntity.Instance != null)
        {
            BeaconEntity.Instance.GetEnemyModifiers(out _, out beaconHp, out beaconSpeed, out beaconElite);
        }

        ScaledEnemyStats scaled = EnemyStatScaler.Calculate(
            data, currentWaveNumber, waveConfig,
            beaconHp, beaconSpeed, beaconElite);

        // ── Inicjalizacja EnemyStats ──────────────────────────────────────────
        EnemyStats stats = obj.GetComponent<EnemyStats>();
        if (stats != null)
            stats.Initialize(data, scaled);

        // ── Inicjalizacja Walkera + podpięcie Echo callback ───────────────────
        EnemyWalker walker = obj.GetComponent<EnemyWalker>();
        if (walker != null)
        {
            walker.Initialize(route.currentActivePath, data);
            walker.OnReachedBase += echoSystem.RegisterBreach;
        }

        // ── Rejestracja spawnu w Echo ─────────────────────────────────────────
        echoSystem.RegisterSpawn(data);
    }

    SpawnRoute SelectRoute()
    {
        float roll = Random.Range(0f, 100f);
        float sum  = 0f;

        foreach (var r in activeRoutes)
        {
            sum += r.currentSpawnChance;
            if (roll <= sum) return r;
        }
        return activeRoutes[0];
    }

    // =========================================================================
    // TRASY I MGŁA
    // =========================================================================

    void InitializeRoutes()
    {
        allPotentialRoutes.Clear();
        var rawPaths = mapGenerator.GetAllSpawnPaths();

        if (rawPaths == null) { StartCoroutine(RetryInitialize()); return; }

        foreach (var path in rawPaths)
        {
            if (path == null || path.Count < 2) continue;
            float len = 0;
            for (int i = 0; i < path.Count - 1; i++)
                len += Vector3.Distance(path[i], path[i + 1]);

            allPotentialRoutes.Add(new SpawnRoute
            {
                fullPath   = path,
                fullLength = len,
                name       = "Route " + allPotentialRoutes.Count
            });
        }

        allPotentialRoutes = allPotentialRoutes.OrderByDescending(r => r.fullLength).ToList();
        RecalculateActiveRoutes();
    }

    IEnumerator RetryInitialize()
    {
        yield return new WaitForSeconds(1f);
        InitializeRoutes();
    }

    void OnChunkRevealedHandler(Vector2Int _) => RecalculateActiveRoutes();

    void RecalculateActiveRoutes()
    {
        activeRoutes.Clear();

        foreach (var route in allPotentialRoutes)
        {
            int foundIndex = -1;
            for (int i = 0; i < route.fullPath.Count; i++)
            {
                var chunk = mapGenerator.GetChunkCoordFromWorldPosition(route.fullPath[i]);
                if (fogManager.IsChunkRevealed(chunk)) { foundIndex = i; break; }
            }

            if (foundIndex >= 0 && foundIndex < route.fullPath.Count - 2)
            {
                route.currentSpawnPoint  = route.fullPath[foundIndex];
                route.currentActivePath  = route.fullPath.GetRange(foundIndex, route.fullPath.Count - foundIndex);
                route.name = $"Spawn at {mapGenerator.GetChunkCoordFromWorldPosition(route.currentSpawnPoint)}";
                activeRoutes.Add(route);
            }
        }

        if (activeRoutes.Count == 0) return;

        activeRoutes = activeRoutes.OrderByDescending(r => r.fullLength).ToList();
        int count = activeRoutes.Count;

        if (count == 1)
        {
            activeRoutes[0].currentSpawnChance = 100f;
            activeRoutes[0].name += " [MAIN]";
        }
        else
        {
            float deduction  = baseFormulaConst + perSpawnerMultiplier * count;
            float mainChance = Mathf.Max(40f, 100f - deduction);
            deduction = 100f - mainChance;

            activeRoutes[0].currentSpawnChance = mainChance;
            activeRoutes[0].name += " [MAIN]";

            float totalMinor = activeRoutes.Skip(1).Sum(r => r.fullLength);
            for (int i = 1; i < count; i++)
            {
                float share = totalMinor > 0 ? activeRoutes[i].fullLength / totalMinor : 1f / (count - 1);
                activeRoutes[i].currentSpawnChance = share * deduction;
            }
        }
    }

    // =========================================================================
    // GIZMOS
    // =========================================================================

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || activeRoutes == null) return;

        foreach (var route in activeRoutes)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(route.currentSpawnPoint, 0.8f);

            Gizmos.color = Color.cyan;
            if (route.currentActivePath == null) continue;
            for (int i = 0; i < route.currentActivePath.Count - 1; i++)
                Gizmos.DrawLine(route.currentActivePath[i], route.currentActivePath[i + 1]);
        }
    }
}