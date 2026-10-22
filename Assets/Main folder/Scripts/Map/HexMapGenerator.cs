using System;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Cienki koordynator generacji mapy.
///
/// ODPOWIADA TYLKO ZA:
///   1. Trzymanie konfiguracji (pola publiczne dla Inspectora)
///   2. Tworzenie generatorów z odpowiednimi zależnościami
///   3. Wywołanie kroków generacji w odpowiedniej kolejności
///   4. Udostępnienie publicznego API dla innych systemów (GetAllSpawnPaths, GetRoadRevealDependencies itd.)
///
/// CAŁA LOGIKA żyje w:
///   ChunkRegistry, PathGenerator,
///   TerrainGenerator, SpecialChunkPlacer, ChunkRevealHandler
/// </summary>
public class HexMapGenerator : MonoBehaviour
{
    public static HexMapGenerator Instance { get; private set; }

    [Header("Seed Generacji")]
    [Tooltip("0 = losowy seed przy każdym uruchomieniu; >0 = deterministyczny seed")]
    public int seed = 0;

    /// <summary>Aktualny kontekst generacji mapy (dane w pamięci).</summary>
    public MapGenerationContext Context => ctx;

    /// <summary>Seed faktycznie użyty w ostatniej generacji mapy.</summary>
    public int CurrentSeed => ctx != null ? ctx.seed : seed;
    
    [Header("Referencje")]
    public HexMapVisualizer       visualizer;
    public FogOfWarManager        fogManager;

    [Header("Rozmiar Mapy")]
    public int mapWidth  = 8;
    public int mapMinY   = -6;
    public int mapMaxY   = 6;

    [Header("Ustawienia Chunku")]
    public int   chunkRadius = 3;
    public float hexSize     = 1f;
    public float padding     = 0.02f;

    [Header("Logika Drogi")]
    public int         minChunkDistance       = 11;
    public int         maxGenerationAttempts  = 50;
    public bool        allowExtraSpawners     = true;
    public int[]       extraSpawnerChances    = { 80, 60, 40, 20 };
    public Vector2Int  extraSpawnDistanceRange = new Vector2Int(2, 6);
    [Range(0, 100)] public int chanceForMoreBranching = 30;
    [Range(0, 100)] public int globalObstacleChance   = 40;

    [Header("Topologia Ścieżek (M2)")]
    public RoadTopologySettings topologySettings = new RoadTopologySettings();

    [Header("Meta Progresja Mapy")]
    public MetaMapConfig metaConfig;

    [Header("Konfiguracja Terenu (M3)")]
    public TerrainGenerationConfig terrainConfig;

    [Header("Specjalne Chunki")]
    public List<SpecialChunkDefinition> specialChunkTypes = new List<SpecialChunkDefinition>();

    [Header("Punkty Strategiczne")]
    public Vector2Int baseRoadEndLocal = new Vector2Int(3, -2);

    [Header("Styl Ścieżek")]
    public ChunkStyleSettings internalSettings = new ChunkStyleSettings();

    // =========================================================================
    // Publiczne dane (dostęp dla innych systemów)
    // =========================================================================

    /// <summary>Pełne dane terenu świata. Tylko do odczytu z zewnątrz.</summary>
    public IReadOnlyDictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>> worldData
        => ctx.worldData;

    // =========================================================================
    // Stan wewnętrzny
    // =========================================================================

    private MapGenerationContext ctx;
    private ChunkRegistry        registry;
    private PathGenerator        pathGen;
    private TerrainGenerator     terrainGen;
    private SpecialChunkPlacer   specialPlacer;

    // =========================================================================
    // Klasy konfiguracji (Serializable – widoczne w Inspectorze)
    // =========================================================================

    [System.Serializable]
    public class ChunkStyleSettings
    {
        [Range(0, 100)] public int highwayWeight    = 20;
        [Range(0, 100)] public int windingWeight    = 40;
        [Range(0, 100)] public int mazeWeight       = 40;
        public int wallCost          = 50;
        public int windingWallCount  = 2;
        public int windingWallLength = 3;
        public int mazeWallCount     = 5;
        public int mazeWallLength    = 3;
    }

    [System.Serializable]
    public class RoadTopologySettings
    {
        [Tooltip("Liczba początkowych chunków od strony bazy stanowiących strefę ochronną (brak rozdroży)")]
        public int safeStartChunks = 2;

        [Tooltip("Minimalna wymagana długość drogi (w heksach) na każdym z początkowych chunków")]
        public int minStartRoadLength = 6;

        [Tooltip("Minimalna liczba zwykłych chunków ścieżki pomiędzy kolejnymi rozdrożami")]
        public int minChunksBetweenCrossroads = 1;

        [Tooltip("Gwarantowana liczba pętli")]
        public int guaranteedLoops = 1;

        [Range(0, 100)]
        [Tooltip("Szansa procentowa na wygenerowanie drugiej pętli (0-100)")]
        public int extraLoopChance = 30;

        [Tooltip("Minimalna i maksymalna rozpiętość pętli na Ścieżce A (w chunkach pośrednich)")]
        public Vector2Int loopSpanRange = new Vector2Int(2, 4);

        [Range(0f, 1f)]
        [Tooltip("Ułamek natężenia przepływu wrogów kierowany w pętlę (domyślnie 0.30 = 30%)")]
        public float loopFlowRatio = 0.30f;

        [Tooltip("Szanse procentowe na kolejne boczne ścieżki (domyślnie 2 ścieżki: 100% i 85%)")]
        public int[] sideBranchChances = new int[] { 100, 85 };

        [Range(0f, 1f)]
        [Tooltip("Ułamek natężenia przepływu wrogów kierowany w boczną ścieżkę (domyślnie 0.30 = 30%)")]
        public float branchFlowRatio = 0.30f;
    }

    // =========================================================================
    // Unity Lifecycle
    // =========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Start()
    {
        GenerateMap();
    }

    // =========================================================================
    // Główna metoda generacji
    // =========================================================================

    /// <summary>
    /// Buduje czyste dane mapy w pamięci (bez wizualizacji GameObjectów i bez logiki sceny).
    /// Zwraca MapGenerationContext gotowy do inspekcji w symulatorze i testach EditMode.
    /// </summary>
    public MapGenerationContext BuildMapData(int seedOverride = -1)
    {
        int effectiveSeed = (seedOverride >= 0) ? seedOverride : seed;

        ctx = new MapGenerationContext(effectiveSeed);
        CreateGenerators();

        // 2. Rejestracja chunków
        registry.RegisterAllChunks();

        // 3. Meta chunki (baza, ekspansje)
        terrainGen.ApplyMetaChunks(baseRoadEndLocal);

        // 4. Generowanie ścieżek (wiele prób jeśli losowo nie wyjdzie)
        bool pathSuccess = false;
        for (int i = 0; i < maxGenerationAttempts; i++)
        {
            if (pathGen.CalculateMainPath(baseRoadEndLocal) && pathGen.GenerateLoops())
            {
                if (allowExtraSpawners) pathGen.GenerateSideBranches();
                pathGen.RebuildAllCrossroadsInternalPaths();
                pathSuccess = true;
                break;
            }
        }
        if (!pathSuccess) Debug.LogWarning("[HexMapGenerator] Nie udało się wygenerować tras po " + maxGenerationAttempts + " próbach.");

        // 5. Specjalne chunki
        specialPlacer.PlaceSpecialChunks();

        // 6. Teren (featuresy, ścieżki na hexach)
        terrainGen.GenerateTerrain();

        // 7. Gwarancje zasobów (M3)
        terrainGen.EnsureGuarantees();

        // 8. Zasoby specjalnych chunków (nadpisują część terenu)
        terrainGen.ApplySpecialChunkResources();

        return ctx;
    }

    [ContextMenu("Generuj Mapę")]
    public void GenerateMap()
    {
        if (visualizer == null) { Debug.LogError("[HexMapGenerator] Brak przypisanego Visualizera!"); return; }

        BuildMapData(seed);

        // Wizualizacja
        visualizer.VisualizeWorld(ctx.worldData, ctx.allValidChunks, chunkRadius, hexSize, padding);

        // Mgła i odkrywanie
        if (MapExpansionManager.Instance != null && registry != null)
        {
            List<Vector2Int> startingSettled = registry.GetUnlockedMetaChunks();
            MapExpansionManager.Instance.Initialize(ctx.worldData, startingSettled);
        }

        if (fogManager != null)
        {
            fogManager.InitializeFog(ctx.allValidChunks, chunkRadius, hexSize, padding);
        }

        Debug.Log($"<color=cyan>[HexMapGenerator] Generacja kompletna (Seed: {ctx.seed}).</color>");
    }

    // =========================================================================
    // Publiczne API dla innych systemów
    // =========================================================================

    public Dictionary<Vector2Int, Vector2Int> GetRoadRevealDependencies()
        => pathGen.GetRoadRevealDependencies();

    public Dictionary<Vector2Int, List<Vector2Int>> GetRoadRevealDependenciesMulti()
        => pathGen.GetRoadRevealDependenciesMulti();

    public List<SpawnPathVariant> GetAllSpawnRoutes()
        => pathGen.GetAllSpawnRoutes(baseRoadEndLocal);

    public List<List<Vector3>> GetAllSpawnPaths()
        => pathGen.GetAllSpawnPaths(baseRoadEndLocal);

    public bool IsChunkInMap(Vector2Int coord)
        => registry.IsChunkInMap(coord);

    public Vector2Int GetChunkCoordFromWorldPosition(Vector3 worldPos)
        => registry.GetChunkCoordFromWorldPosition(worldPos, chunkRadius, hexSize, padding);

    public ChunkType GetChunkType(Vector2Int coord)
        => registry.GetChunkType(coord);

    /// <summary>Zwraca definicję specjalnego chunku lub null jeśli chunk jest zwykły.</summary>
    public SpecialChunkDefinition GetSpecialChunkDefinition(Vector2Int coord)
        => specialPlacer.GetDefinition(coord);

    // =========================================================================
    // Prywatne helpers
    // =========================================================================

    private void CreateGenerators()
    {
        if ((object)terrainConfig == null)
            terrainConfig = TerrainGenerationConfig.CreateDefault();
        if (internalSettings == null)
            internalSettings = new ChunkStyleSettings();
        if (topologySettings == null)
            topologySettings = new RoadTopologySettings();
        if (specialChunkTypes == null)
            specialChunkTypes = new List<SpecialChunkDefinition>();
        if (extraSpawnerChances == null)
            extraSpawnerChances = new int[] { 80, 60, 40, 20 };

        var pathfinder = new HexPathfinder(chunkRadius);

        registry = new ChunkRegistry(ctx, metaConfig, mapWidth, mapMinY, mapMaxY);

        pathGen = new PathGenerator(
            ctx, registry, pathfinder,
            chunkRadius, hexSize, padding,
            minChunkDistance, globalObstacleChance, chanceForMoreBranching,
            extraSpawnerChances, extraSpawnDistanceRange,
            internalSettings,
            topologySettings);

        terrainGen = new TerrainGenerator(ctx, registry, metaConfig, chunkRadius, terrainConfig);

        specialPlacer = new SpecialChunkPlacer(ctx, registry, specialChunkTypes);
    }
}