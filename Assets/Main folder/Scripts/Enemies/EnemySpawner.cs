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

    [Header("Endless / Late Scaling Settings")]
    [Tooltip("Włącza skalowanie wykładnicze dla dni po ostatniej zdefiniowanej fali")]
    public bool enableLateScaling = true;
    [Tooltip("Baza potęgowania mnożnika HP (np. 1.1 = +10% HP za każdy dzień ponad ostatnią falę: 1.1^a)")]
    public float lateScalingBase = 1.1f;

    [Header("Seed / Determinizm")]
    [Tooltip("Seed losowania spawnu (0 = automatyczny deterministyczny seed zależny od numeru fali)")]
    public int fixedWaveSeed = 0;

    [Header("Route Weights")]
    public float baseFormulaConst = 3f;
    public float perSpawnerMultiplier = 2.5f;

    [Header("Active Routes Preview")]
    public List<SpawnRoute> activeRoutes = new List<SpawnRoute>();

    private struct SpawnInstruction
    {
        public EnemyData data;
        public int tier;
        public EnemyRank rank;
        public float hpMultiplier;
        public float speedMultiplier;
        public float delayBefore;
        public float spawnInterval;
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

        int daysBeyondLast = 0;
        WaveDefinition selectedWave = FindWaveForDay(waveNumber, out daysBeyondLast);
        if (selectedWave != null)
        {
            PreparePredefinedWave(selectedWave, waveNumber, daysBeyondLast);
        }
        else
        {
            Debug.LogWarning($"[Spawner] Brak zdefiniowanej fali dla Dnia {waveNumber} (lista predefinedWaves jest pusta).");
            EnsureEtherDropManager();
            EnemyEtherDropManager.Instance?.InitializeWave(null, waveNumber, 10);
        }
    }

    public WaveDefinition FindWaveForDay(int waveNumber)
    {
        int daysBeyondLast;
        return FindWaveForDay(waveNumber, out daysBeyondLast);
    }

    public WaveDefinition FindWaveForDay(int waveNumber, out int daysBeyondLast)
    {
        daysBeyondLast = 0;
        if (predefinedWaves == null || predefinedWaves.Count == 0) return null;

        int maxDefinedDay = predefinedWaves.Where(w => w != null).Max(w => w.dayNumber);

        if (waveNumber > maxDefinedDay)
        {
            daysBeyondLast = waveNumber - maxDefinedDay;
            return predefinedWaves.Where(w => w != null).OrderBy(w => w.dayNumber).LastOrDefault();
        }

        WaveDefinition exact = predefinedWaves.Find(w => w != null && w.dayNumber == waveNumber);
        if (exact != null) return exact;

        // Fallback: najbliższa wcześniejsza fala w ramach zdefiniowanego zakresu
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

    void PreparePredefinedWave(WaveDefinition waveDef, int waveNumber, int daysBeyondLast = 0)
    {
        if (waveDef.customSpawnInterval > 0f)
        {
            currentWaveSpawnInterval = waveDef.customSpawnInterval;
        }

        // Fale ręczne: mnożnik 1.0 (statystyki definiowane przez tiery wrogów).
        // Fale endless (dni po ostatniej fali): 1.1^a dla HP.
        float hpMult = 1f;
        if (daysBeyondLast > 0 && enableLateScaling)
        {
            hpMult = Mathf.Pow(lateScalingBase, daysBeyondLast);
        }
        float speedMult = 1f;

        // Deterministyczny generator losowy dla tej fali (nie modyfikuje UnityEngine.Random.state)
        int seed = fixedWaveSeed != 0 ? fixedWaveSeed : (waveNumber * 10007 + 42);
        var rng = new System.Random(seed);
        int elitesGenerated = 0;

        List<SpawnInstruction> instructions = new List<SpawnInstruction>();

        // 1. Obsługa grup / segmentów spawnu (jeśli zdefiniowane w WaveDefinition)
        if (waveDef.spawnGroups != null && waveDef.spawnGroups.Count > 0)
        {
            foreach (var group in waveDef.spawnGroups)
            {
                if (group == null || group.enemies == null || group.enemies.Count == 0) continue;

                List<SpawnInstruction> groupInstructions = new List<SpawnInstruction>();
                float baseInterval = group.spawnInterval > 0f 
                    ? group.spawnInterval 
                    : (waveDef.customSpawnInterval > 0f ? waveDef.customSpawnInterval : nightSpawnInterval);
                float jitter = Mathf.Clamp01(group.intervalJitter);

                for (int entryIdx = 0; entryIdx < group.enemies.Count; entryIdx++)
                {
                    var entry = group.enemies[entryIdx];
                    if (entry.enemy == null || entry.count <= 0) continue;

                    int entryTier = entry.Tier;

                    for (int i = 0; i < entry.count; i++)
                    {
                        EnemyRank rank = EnemyRank.Normal;
                        if (entry.enemy != null && (entry.enemy.rank == EnemyRank.Boss || entry.enemy.isBoss))
                        {
                            rank = EnemyRank.Boss;
                        }
                        else if (entry.forceElite)
                        {
                            rank = EnemyRank.Elite;
                        }
                        else if (waveDef.allowRandomElites && entry.enemy.canBeElite)
                        {
                            if (waveDef.maxElitesCount <= 0 || elitesGenerated < waveDef.maxElitesCount)
                            {
                                float chance = entry.enemy.eliteSpawnChance > 1f
                                    ? entry.enemy.eliteSpawnChance / 100f
                                    : entry.enemy.eliteSpawnChance;
                                if (rng.NextDouble() < chance)
                                {
                                    rank = EnemyRank.Elite;
                                    elitesGenerated++;
                                }
                            }
                        }

                        float jitterOffset = jitter > 0f ? (float)(rng.NextDouble() * 2.0 - 1.0) * (baseInterval * jitter) : 0f;
                        float unitInterval = Mathf.Max(0.05f, baseInterval + jitterOffset);

                        groupInstructions.Add(new SpawnInstruction
                        {
                            data = entry.enemy,
                            tier = entryTier,
                            rank = rank,
                            hpMultiplier = hpMult,
                            speedMultiplier = speedMult,
                            delayBefore = 0f,
                            spawnInterval = unitInterval
                        });
                    }
                }

                // Przemieszanie wewnątrz grupy (bez naruszania kolejności całych grup)
                if (group.shuffleInside && groupInstructions.Count > 1)
                {
                    for (int i = 0; i < groupInstructions.Count; i++)
                    {
                        int j = rng.Next(i, groupInstructions.Count);
                        var tmp = groupInstructions[i];
                        groupInstructions[i] = groupInstructions[j];
                        groupInstructions[j] = tmp;
                    }
                }

                // Opóźnienie przed grupą przypisujemy do jej pierwszej jednostki
                if (groupInstructions.Count > 0 && group.delayBefore > 0f)
                {
                    var first = groupInstructions[0];
                    first.delayBefore = group.delayBefore;
                    groupInstructions[0] = first;
                }

                instructions.AddRange(groupInstructions);
            }
        }
        else if (waveDef.enemies != null)
        {
            // 2. Tryb kompatybilności wstecznej (pojedyncza płaska lista enemies)
            foreach (var group in waveDef.enemies)
            {
                if (group.enemy == null || group.count <= 0) continue;

                int groupTier = group.Tier;

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
                        if (waveDef.maxElitesCount <= 0 || elitesGenerated < waveDef.maxElitesCount)
                        {
                            float chance = group.enemy.eliteSpawnChance > 1f
                                ? group.enemy.eliteSpawnChance / 100f
                                : group.enemy.eliteSpawnChance;
                            if (rng.NextDouble() < chance)
                            {
                                rank = EnemyRank.Elite;
                                elitesGenerated++;
                            }
                        }
                    }

                    instructions.Add(new SpawnInstruction
                    {
                        data = group.enemy,
                        tier = groupTier,
                        rank = rank,
                        hpMultiplier = hpMult,
                        speedMultiplier = speedMult,
                        delayBefore = 0f,
                        spawnInterval = currentWaveSpawnInterval
                    });
                }
            }

            if (waveDef.shuffleEnemies && instructions.Count > 1)
            {
                for (int i = 0; i < instructions.Count; i++)
                {
                    int j = rng.Next(i, instructions.Count);
                    var tmp = instructions[i];
                    instructions[i] = instructions[j];
                    instructions[j] = tmp;
                }
            }
        }

        for (int i = 0; i < instructions.Count; i++)
        {
            enemiesToSpawnQueue.Enqueue(instructions[i]);
        }

        string msgInfo = string.IsNullOrEmpty(waveDef.waveMessage) ? "" : $" | \"{waveDef.waveMessage}\"";
        string endlessInfo = daysBeyondLast > 0 ? $" [ENDLESS a={daysBeyondLast}, HP x{hpMult:0.##}]" : "";
        Debug.Log($"[Spawner] Fala Dzień {waveNumber} ({waveDef.name}){endlessInfo}: {enemiesToSpawnQueue.Count} wrogów w kolejce (HP x{hpMult:0.##}, Speed x{speedMult:0.##}, Interwał {currentWaveSpawnInterval:0.##}s){msgInfo}");

        EnsureEtherDropManager();
        if (EnemyEtherDropManager.Instance != null)
        {
            EnemyEtherDropManager.Instance.InitializeWave(waveDef, waveNumber, enemiesToSpawnQueue.Count);
        }
    }

    private IEnumerator SpawnWaveRoutine()
    {
        while (enemiesToSpawnQueue.Count > 0)
        {
            if (activeRoutes.Count > 0)
            {
                var instr = enemiesToSpawnQueue.Peek();
                if (instr.delayBefore > 0f)
                {
                    while (spawnTimer < instr.delayBefore)
                    {
                        spawnTimer += Time.deltaTime;
                        yield return null;
                    }
                    spawnTimer = 0f;
                }

                float waitInterval = instr.spawnInterval > 0f 
                    ? instr.spawnInterval 
                    : Mathf.Max(0.05f, currentWaveSpawnInterval);

                SpawnEnemy(enemiesToSpawnQueue.Dequeue());
                spawnTimer = 0f;

                while (spawnTimer < waitInterval && enemiesToSpawnQueue.Count > 0)
                {
                    spawnTimer += Time.deltaTime;
                    yield return null;
                }
                spawnTimer = 0f;
            }
            else
            {
                yield return null;
            }
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
                tier = 1,
                rank = asElite ? EnemyRank.Elite : EnemyRank.Normal,
                hpMultiplier = 1f,
                speedMultiplier = 1f,
                delayBefore = 0f,
                spawnInterval = nightSpawnInterval
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
        if (activeRoutes.Count == 0 || instr.data == null) return;

        GameObject prefabToSpawn = instr.data.GetPrefabForTier(instr.tier);
        if (prefabToSpawn == null) return;

        SpawnRoute route = SelectRoute();
        GameObject obj = Instantiate(prefabToSpawn, route.currentSpawnPoint, Quaternion.identity);

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
            stats.Initialize(instr.data, instr.tier, instr.hpMultiplier, instr.speedMultiplier);
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
