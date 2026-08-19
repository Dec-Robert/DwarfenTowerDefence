using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Central coordinator for wave progression and enemy spawning during the Night phase.
/// Key Responsibilities:
/// - Determines wave composition and budget using WavePaletteSystem and WaveThreatCalculator.
/// - Scales enemy attributes and elite chances based on the Holy Flame's light level (100%, 50%, 0%).
/// - Manages active spawn routes dynamically adjusted by Fog of War reveals.
/// - Safely tracks all living enemies using a collection to detect full wave clearance (via elimination or base breach).
/// - Triggers the transition to the Dawn Report / next phase upon wave completion.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Map References")]
    public HexMapGenerator mapGenerator;
    public FogOfWarManager fogManager;

    [Header("Wave Configuration")]
    public WaveScalingConfig waveConfig;

    [Header("Enemy Pool")]
    public List<EnemyData> availableEnemies;

    [Header("Scripted Waves")]
    public List<WaveDefinition> predefinedWaves;

    [Header("Spawn Settings")]
    public float nightSpawnInterval = 1.0f;

    [Header("Route Weights")]
    public float baseFormulaConst = 3f;
    public float perSpawnerMultiplier = 2.5f;

    [Header("Active Routes Preview")]
    public List<SpawnRoute> activeRoutes = new List<SpawnRoute>();

    private WavePaletteSystem paletteSystem;
    private WaveThreatCalculator threatCalculator;

    private Queue<EnemyData> enemiesToSpawnQueue = new Queue<EnemyData>();
    private List<EnemyStats> activeEnemies = new List<EnemyStats>();
    
    private float spawnTimer;
    private int currentWaveNumber = 0;
    private Coroutine spawnCoroutine;
    private List<SpawnRoute> allPotentialRoutes = new List<SpawnRoute>();

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

    void Awake()
    {
        if (waveConfig == null)
        {
            Debug.LogError("[Spawner] WaveScalingConfig is missing!");
            return;
        }

        paletteSystem = new WavePaletteSystem(waveConfig, availableEnemies);
        threatCalculator = new WaveThreatCalculator(waveConfig);
    }

    void Start()
    {
        if (fogManager != null) fogManager.OnChunkRevealed += OnChunkRevealedHandler;
        if (TimePhaseManager.Instance != null) TimePhaseManager.Instance.OnNightStarted += HandleNightPhase;

        StartCoroutine(WaitForMapAndInitRoutes());
    }

    void OnDestroy()
    {
        if (fogManager != null) fogManager.OnChunkRevealed -= OnChunkRevealedHandler;
        if (TimePhaseManager.Instance != null) TimePhaseManager.Instance.OnNightStarted -= HandleNightPhase;
    }

    private void HandleNightPhase()
    {
        spawnTimer = 0f;
        currentWaveNumber = TimePhaseManager.Instance != null ? TimePhaseManager.Instance.CurrentDay : 1;

        PrepareWave(currentWaveNumber);

        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnWaveRoutine());
    }

    void PrepareWave(int waveNumber)
    {
        enemiesToSpawnQueue.Clear();
        activeEnemies.Clear();

        var scriptedWave = predefinedWaves?.Find(w => w.dayNumber == waveNumber);
        if (scriptedWave != null)
        {
            PrepareScriptedWave(scriptedWave, waveNumber);
            return;
        }

        paletteSystem.EnsurePaletteForWave(waveNumber);

        float budget = threatCalculator.CalculateBudget(waveNumber, out WaveThreatCalculator.WaveDifficultyType diffType);
        enemiesToSpawnQueue = threatCalculator.SpendBudget(budget, paletteSystem, null, 0f);

        Debug.Log($"[Spawner] Wave {waveNumber} ({diffType}): {enemiesToSpawnQueue.Count} enemies queued | Palette: {paletteSystem.GetPaletteDebugString()}");
    }

    void PrepareScriptedWave(WaveDefinition scriptedWave, int waveNumber)
    {
        Debug.Log($"[Spawner] SCRIPTED WAVE (Wave {waveNumber}): {scriptedWave.waveMessage}");
        foreach (var group in scriptedWave.enemies)
        {
            if (group.enemy == null) continue;
            for (int i = 0; i < group.count; i++)
            {
                enemiesToSpawnQueue.Enqueue(group.enemy);
            }
        }
    }

    private IEnumerator SpawnWaveRoutine()
    {
        while (enemiesToSpawnQueue.Count > 0)
        {
            if (activeRoutes.Count > 0)
            {
                spawnTimer += Time.deltaTime;
                if (spawnTimer >= nightSpawnInterval)
                {
                    SpawnEnemy(enemiesToSpawnQueue.Dequeue());
                    spawnTimer = 0f;
                }
            }
            yield return null;
        }

        StartCoroutine(WaitForWaveClearRoutine());
    }

    private IEnumerator WaitForWaveClearRoutine()
    {
        while (true)
        {
            activeEnemies.RemoveAll(enemy => enemy == null);

            if (activeEnemies.Count == 0 && enemiesToSpawnQueue.Count == 0)
            {
                break;
            }

            yield return new WaitForSeconds(0.5f);
        }

        FinishWave();
    }

    private void FinishWave()
    {
        Debug.Log($"<color=green>[Spawner] Night {currentWaveNumber} survived! Preparing Dawn Report.</color>");
        
        TimePhaseManager.Instance?.ChangeCurrentPhase();
    }

    void SpawnEnemy(EnemyData data)
    {
        if (activeRoutes.Count == 0 || data?.prefab == null) return;

        SpawnRoute route = SelectRoute();
        GameObject obj = Instantiate(data.prefab, route.currentSpawnPoint, Quaternion.identity);
        
        
        

        EnemyStats stats = obj.GetComponent<EnemyStats>();
        if (stats != null)
        {
            stats.Initialize(data);
            stats.OnDeath += RemoveEnemyFromAlive;
            activeEnemies.Add(stats);
        }

        EnemyWalker walker = obj.GetComponent<EnemyWalker>();
        if (walker != null)
        {
            walker.Initialize(route.currentActivePath, stats);
        }
    }

    void RemoveEnemyFromAlive(EnemyStats stats)
    {
        activeEnemies.Remove(stats);
    }
    
    SpawnRoute SelectRoute()
    {
        float roll = Random.Range(0f, 100f);
        float sum = 0f;

        foreach (var r in activeRoutes)
        {
            sum += r.currentSpawnChance;
            if (roll <= sum) return r;
        }
        return activeRoutes[0];
    }

    IEnumerator WaitForMapAndInitRoutes()
    {
        yield return null; 
        yield return null; 
        yield return null;

        if (mapGenerator == null || fogManager == null)
        {
            Debug.LogError("[Spawner] Missing Map or Fog references!");
            yield break;
        }

        InitializeRoutes();
    }

    void InitializeRoutes()
    {
        allPotentialRoutes.Clear();
        var rawPaths = mapGenerator.GetAllSpawnPaths();

        if (rawPaths == null) 
        { 
            StartCoroutine(RetryInitialize()); 
            return; 
        }

        foreach (var path in rawPaths)
        {
            if (path == null || path.Count < 2) continue;
            float len = 0;
            for (int i = 0; i < path.Count - 1; i++)
            {
                len += Vector3.Distance(path[i], path[i + 1]);
            }

            allPotentialRoutes.Add(new SpawnRoute
            {
                fullPath = path,
                fullLength = len,
                name = "Route " + allPotentialRoutes.Count
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
                if (fogManager.IsChunkRevealed(chunk)) 
                { 
                    foundIndex = i; 
                    break; 
                }
            }

            if (foundIndex >= 0 && foundIndex < route.fullPath.Count - 2)
            {
                route.currentSpawnPoint = route.fullPath[foundIndex];
                route.currentActivePath = route.fullPath.GetRange(foundIndex, route.fullPath.Count - foundIndex);
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
            float deduction = baseFormulaConst + perSpawnerMultiplier * count;
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
            {
                Gizmos.DrawLine(route.currentActivePath[i], route.currentActivePath[i + 1]);
            }
        }
    }
}