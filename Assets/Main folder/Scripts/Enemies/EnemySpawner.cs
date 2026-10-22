using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Central coordinator for wave progression and enemy spawning during the Night phase.
/// Uses predefined WaveDefinition assets configured per day.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Map References")]
    public HexMapGenerator mapGenerator;
    public FogOfWarManager fogManager;

    [Header("Enemy Pool")]
    public List<EnemyData> availableEnemies = new List<EnemyData>();

    [Header("Predefined Waves")]
    public List<WaveDefinition> predefinedWaves = new List<WaveDefinition>();

    [Header("Spawn Settings")]
    public float nightSpawnInterval = 1.0f;

    [Header("Route Weights")]
    public float baseFormulaConst = 3f;
    public float perSpawnerMultiplier = 2.5f;

    [Header("Active Routes Preview")]
    public List<SpawnRoute> activeRoutes = new List<SpawnRoute>();

    private struct SpawnInstruction
    {
        public EnemyData data;
        public EnemyRank rank;
        public float hpMultiplier;
        public float speedMultiplier;
    }

    private readonly Queue<SpawnInstruction> enemiesToSpawnQueue = new Queue<SpawnInstruction>();
    private readonly List<EnemyStats> activeEnemies = new List<EnemyStats>();

    private float spawnTimer;
    private float currentWaveSpawnInterval = 1.0f;
    private int currentWaveNumber = 0;
    private Coroutine spawnCoroutine;
    private List<SpawnRoute> allPotentialRoutes = new List<SpawnRoute>();

    [System.Serializable]
    public class SpawnRoute
    {
        public string name;
        [Range(0, 100)] public float currentSpawnChance;
        [Tooltip("Relative flow weight assigned by topological flow model")]
        public float flowWeight = 1.0f;
        public List<Vector3> fullPath;
        public Vector3 currentSpawnPoint;
        public List<Vector3> currentActivePath;
        public float fullLength;
    }

    void Start()
    {
        EnsureEtherDropManager();
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
        currentWaveSpawnInterval = nightSpawnInterval;

        WaveDefinition selectedWave = FindWaveForDay(waveNumber);
        if (selectedWave != null)
        {
            PreparePredefinedWave(selectedWave, waveNumber);
        }
        else
        {
            Debug.LogWarning($"[Spawner] Brak zdefiniowanej fali dla Dnia {waveNumber} (lista predefinedWaves jest pusta).");
            EnsureEtherDropManager();
            EnemyEtherDropManager.Instance?.InitializeWave(null, waveNumber, 10);
        }
    }

    private WaveDefinition FindWaveForDay(int waveNumber)
    {
        if (predefinedWaves == null || predefinedWaves.Count == 0) return null;

        WaveDefinition exact = predefinedWaves.Find(w => w != null && w.dayNumber == waveNumber);
        if (exact != null) return exact;

        // Fallback: najbliższa wcześniejsza fala (lub pierwsza zdefiniowana)
        WaveDefinition fallback = null;
        foreach (var w in predefinedWaves)
        {
            if (w == null) continue;
            if (w.dayNumber <= waveNumber)
            {
                if (fallback == null || w.dayNumber > fallback.dayNumber)
                    fallback = w;
            }
        }

        if (fallback == null)
        {
            fallback = predefinedWaves.FirstOrDefault(w => w != null);
        }

        if (fallback != null)
        {
            Debug.LogWarning($"[Spawner] Brak dedykowanego assetu fali dla Dnia {waveNumber} – użyto fallbacku z Dnia {fallback.dayNumber} ({fallback.name}).");
        }

        return fallback;
    }

    void PreparePredefinedWave(WaveDefinition waveDef, int waveNumber)
    {
        if (waveDef.customSpawnInterval > 0f)
        {
            currentWaveSpawnInterval = waveDef.customSpawnInterval;
        }

        float hpMult = waveDef.hpMultiplier > 0f ? waveDef.hpMultiplier : 1f;
        float speedMult = waveDef.speedMultiplier > 0f ? waveDef.speedMultiplier : 1f;

        List<SpawnInstruction> instructions = new List<SpawnInstruction>();

        if (waveDef.enemies != null)
        {
            foreach (var group in waveDef.enemies)
            {
                if (group.enemy == null || group.count <= 0) continue;

                for (int i = 0; i < group.count; i++)
                {
                    EnemyRank rank = EnemyRank.Normal;
                    if (group.enemy != null && (group.enemy.rank == EnemyRank.Boss || group.enemy.isBoss))
                    {
                        rank = EnemyRank.Boss;
                    }
                    else if (group.forceElite)
                    {
                        rank = EnemyRank.Elite;
                    }
                    else if (waveDef.allowRandomElites && group.enemy.canBeElite)
                    {
                        float chance = group.enemy.eliteSpawnChance > 1f
                            ? group.enemy.eliteSpawnChance / 100f
                            : group.enemy.eliteSpawnChance;
                        if (Random.value < chance)
                        {
                            rank = EnemyRank.Elite;
                        }
                    }

                    instructions.Add(new SpawnInstruction
                    {
                        data = group.enemy,
                        rank = rank,
                        hpMultiplier = hpMult,
                        speedMultiplier = speedMult
                    });
                }
            }
        }

        if (waveDef.shuffleEnemies && instructions.Count > 1)
        {
            for (int i = 0; i < instructions.Count; i++)
            {
                int j = Random.Range(i, instructions.Count);
                var tmp = instructions[i];
                instructions[i] = instructions[j];
                instructions[j] = tmp;
            }
        }

        for (int i = 0; i < instructions.Count; i++)
        {
            enemiesToSpawnQueue.Enqueue(instructions[i]);
        }

        string msgInfo = string.IsNullOrEmpty(waveDef.waveMessage) ? "" : $" | \"{waveDef.waveMessage}\"";
        Debug.Log($"[Spawner] Fala Dzień {waveNumber} ({waveDef.name}): {enemiesToSpawnQueue.Count} wrogów w kolejce (HP x{hpMult:0.##}, Speed x{speedMult:0.##}, Interwał {currentWaveSpawnInterval:0.##}s){msgInfo}");

        EnsureEtherDropManager();
        if (EnemyEtherDropManager.Instance != null)
        {
            EnemyEtherDropManager.Instance.InitializeWave(waveDef, waveNumber, enemiesToSpawnQueue.Count);
        }
    }

    private IEnumerator SpawnWaveRoutine()
    {
        float interval = Mathf.Max(0.05f, currentWaveSpawnInterval);
        while (enemiesToSpawnQueue.Count > 0)
        {
            if (activeRoutes.Count > 0)
            {
                spawnTimer += Time.deltaTime;
                if (spawnTimer >= interval)
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

            if (activeEnemies.Count == 0 && EnemyRegistry.ActiveEnemies.Count == 0 && enemiesToSpawnQueue.Count == 0)
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

    public bool SpawnEnemyByName(string enemyName, int count = 1, bool asElite = false)
    {
        if (availableEnemies == null || availableEnemies.Count == 0) return false;

        EnemyData data = availableEnemies.Find(e =>
            e != null && (
                string.Equals(e.enemyName, enemyName, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.name, enemyName, System.StringComparison.OrdinalIgnoreCase)
            ));

        if (data == null)
        {
            data = availableEnemies.Find(e =>
                e != null && (
                    (!string.IsNullOrEmpty(e.enemyName) && e.enemyName.IndexOf(enemyName, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                    e.name.IndexOf(enemyName, System.StringComparison.OrdinalIgnoreCase) >= 0
                ));
        }

        if (data == null) return false;

        if (activeRoutes.Count == 0)
        {
            RecalculateActiveRoutes();
        }

        if (activeRoutes.Count == 0 && allPotentialRoutes.Count > 0)
        {
            activeRoutes.Add(new SpawnRoute
            {
                name = allPotentialRoutes[0].name,
                currentSpawnChance = 100f,
                fullPath = allPotentialRoutes[0].fullPath,
                currentSpawnPoint = allPotentialRoutes[0].fullPath[0],
                currentActivePath = allPotentialRoutes[0].fullPath,
                fullLength = allPotentialRoutes[0].fullLength
            });
        }

        for (int i = 0; i < count; i++)
        {
            SpawnEnemy(new SpawnInstruction
            {
                data = data,
                rank = asElite ? EnemyRank.Elite : EnemyRank.Normal,
                hpMultiplier = 1f,
                speedMultiplier = 1f
            });
        }

        return true;
    }

    public void ForceCompleteWave()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        enemiesToSpawnQueue.Clear();

        var enemies = new List<EnemyStats>(EnemyRegistry.ActiveEnemies);
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                enemy.TakeDamage(999999f, DamageType.True, 1f, 1f, true, 1000f);
            }
        }
        activeEnemies.Clear();

        FinishWave();
    }

    private void SpawnEnemy(SpawnInstruction instr)
    {
        if (activeRoutes.Count == 0 || instr.data?.prefab == null) return;

        SpawnRoute route = SelectRoute();
        GameObject obj = Instantiate(instr.data.prefab, route.currentSpawnPoint, Quaternion.identity);

        EnemyStats stats = obj.GetComponent<EnemyStats>();
        if (stats != null)
        {
            if (stats.rank == EnemyRank.Boss || (instr.data != null && (instr.data.rank == EnemyRank.Boss || instr.data.isBoss)))
            {
                stats.rank = EnemyRank.Boss;
            }
            else
            {
                stats.rank = instr.rank;
            }
            stats.Initialize(instr.data, instr.hpMultiplier, instr.speedMultiplier);
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

    public void InitializeRoutes()
    {
        allPotentialRoutes.Clear();

        // 1. Sprawdź czy dostępny jest Model Przepływu (GetAllSpawnRoutes)
        var routeVariants = mapGenerator != null ? mapGenerator.GetAllSpawnRoutes() : null;

        if (routeVariants != null && routeVariants.Count > 0)
        {
            foreach (var variant in routeVariants)
            {
                if (variant.fullPath == null || variant.fullPath.Count < 2) continue;
                float len = 0;
                for (int i = 0; i < variant.fullPath.Count - 1; i++)
                {
                    len += Vector3.Distance(variant.fullPath[i], variant.fullPath[i + 1]);
                }

                allPotentialRoutes.Add(new SpawnRoute
                {
                    fullPath = variant.fullPath,
                    flowWeight = variant.flowWeight > 0f ? variant.flowWeight : 1.0f,
                    fullLength = len,
                    name = !string.IsNullOrEmpty(variant.name) ? variant.name : $"Route {allPotentialRoutes.Count}"
                });
            }
        }
        else
        {
            // Wsteczna kompatybilność z GetAllSpawnPaths
            var rawPaths = mapGenerator != null ? mapGenerator.GetAllSpawnPaths() : null;
            if (rawPaths == null || rawPaths.Count == 0)
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
                    flowWeight = 1.0f / rawPaths.Count,
                    fullLength = len,
                    name = "Route " + allPotentialRoutes.Count
                });
            }
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

    public void RecalculateActiveRoutes()
    {
        activeRoutes.Clear();

        if (allPotentialRoutes == null || allPotentialRoutes.Count == 0) return;
        if (mapGenerator == null || fogManager == null) return;

        // 1. Skanowanie wstecz od Bazy (koniec trasy) w stronę Spawnera (początek trasy)
        var candidateRoutes = new List<SpawnRoute>();

        foreach (var route in allPotentialRoutes)
        {
            if (route.fullPath == null || route.fullPath.Count < 2) continue;

            int activeSpawnIndex = route.fullPath.Count - 1;
            for (int i = route.fullPath.Count - 1; i >= 0; i--)
            {
                var chunk = mapGenerator.GetChunkCoordFromWorldPosition(route.fullPath[i]);
                if (fogManager.IsChunkRevealed(chunk))
                {
                    activeSpawnIndex = i;
                }
                else
                {
                    // Natrafiono na mgłę – ostatni ujawniony punkt jest aktywnym spawnerem na granicy mgły
                    break;
                }
            }

            // Wymagaj co najmniej 2 punktów na aktywnej ścieżce (spawner -> punkt kolejny)
            if (activeSpawnIndex >= 0 && activeSpawnIndex < route.fullPath.Count - 1)
            {
                var activePath = route.fullPath.GetRange(activeSpawnIndex, route.fullPath.Count - activeSpawnIndex);
                var spawnPoint = route.fullPath[activeSpawnIndex];

                candidateRoutes.Add(new SpawnRoute
                {
                    fullPath = route.fullPath,
                    fullLength = route.fullLength,
                    flowWeight = route.flowWeight,
                    currentSpawnPoint = spawnPoint,
                    currentActivePath = activePath,
                    name = $"Spawn at {mapGenerator.GetChunkCoordFromWorldPosition(spawnPoint)}"
                });
            }
        }

        if (candidateRoutes.Count == 0) return;

        // 2. Scalanie tras o identycznym odkrytym przebiegu (przed rozdrożem ukrytym w mgle)
        var mergedRoutes = new List<SpawnRoute>();

        foreach (var candidate in candidateRoutes)
        {
            SpawnRoute existingGroup = null;
            foreach (var merged in mergedRoutes)
            {
                if (AreActivePathsIdentical(merged.currentActivePath, candidate.currentActivePath))
                {
                    existingGroup = merged;
                    break;
                }
            }

            if (existingGroup != null)
            {
                existingGroup.flowWeight += candidate.flowWeight;
            }
            else
            {
                mergedRoutes.Add(new SpawnRoute
                {
                    name = candidate.name,
                    fullPath = candidate.fullPath,
                    fullLength = candidate.fullLength,
                    flowWeight = candidate.flowWeight,
                    currentSpawnPoint = candidate.currentSpawnPoint,
                    currentActivePath = new List<Vector3>(candidate.currentActivePath)
                });
            }
        }

        // 3. Normalizacja wag przepływu (suma currentSpawnChance = 100%)
        float totalFlow = mergedRoutes.Sum(r => r.flowWeight);

        if (totalFlow > 0.0001f)
        {
            foreach (var r in mergedRoutes)
            {
                r.currentSpawnChance = (r.flowWeight / totalFlow) * 100f;
            }
        }
        else
        {
            float equalShare = 100f / mergedRoutes.Count;
            foreach (var r in mergedRoutes)
            {
                r.currentSpawnChance = equalShare;
            }
        }

        activeRoutes = mergedRoutes.OrderByDescending(r => r.currentSpawnChance).ToList();

        if (activeRoutes.Count > 0)
        {
            activeRoutes[0].name += " [MAIN]";
        }
    }

    private static bool AreActivePathsIdentical(List<Vector3> pathA, List<Vector3> pathB)
    {
        if (pathA == null || pathB == null) return false;
        if (pathA.Count != pathB.Count) return false;

        const float toleranceSq = 0.001f;
        for (int i = 0; i < pathA.Count; i++)
        {
            if ((pathA[i] - pathB[i]).sqrMagnitude > toleranceSq)
                return false;
        }

        return true;
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

    private void EnsureEtherDropManager()
    {
        if (EnemyEtherDropManager.Instance == null)
        {
            var existing = FindFirstObjectByType<EnemyEtherDropManager>();
            if (existing == null)
            {
                var go = new GameObject("EnemyEtherDropManager");
                go.AddComponent<EnemyEtherDropManager>();
            }
        }
    }
}