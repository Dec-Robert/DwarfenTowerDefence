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
///   ChunkRegistry, PathGenerator, BiomeGenerator,
///   TerrainGenerator, SpecialChunkPlacer, ChunkRevealHandler
/// </summary>
public class HexMapGenerator : MonoBehaviour
{
    [Header("Referencje")]
    public HexMapVisualizer       visualizer;
    public FogOfWarManager        fogManager;
    public MapExpansionManager    expansionManager;

    [Header("Rozmiar Mapy")]
    public int mapWidth  = 8;
    public int mapMinY   = -6;
    public int mapMaxY   = 6;

    [Header("Ustawienia Chunku")]
    public int   chunkRadius = 4;
    public float hexSize     = 1f;
    public float padding     = 0.02f;

    [Header("Logika Drogi")]
    public int         minChunkDistance       = 6;
    public int         maxGenerationAttempts  = 50;
    public bool        allowExtraSpawners     = true;
    public int[]       extraSpawnerChances    = { 80, 60, 40, 20 };
    public Vector2Int  extraSpawnDistanceRange = new Vector2Int(2, 6);
    [Range(0, 100)] public int chanceForMoreBranching = 30;
    [Range(0, 100)] public int globalObstacleChance   = 40;

    [Header("Meta Progresja Mapy")]
    public MetaMapConfig metaConfig;

    [Header("Konfiguracja Biomów")]
    public List<BiomeSettings> biomeSettings = new List<BiomeSettings>();

    [Header("Specjalne Chunki")]
    public List<SpecialChunkDefinition> specialChunkTypes = new List<SpecialChunkDefinition>();

    [Header("Punkty Strategiczne")]
    public Vector2Int baseRoadEndLocal = new Vector2Int(3, -2);

    [Header("Styl Ścieżek")]
    public ChunkStyleSettings internalSettings;

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
    private BiomeGenerator       biomeGen;
    private TerrainGenerator     terrainGen;
    private SpecialChunkPlacer   specialPlacer;
    private ChunkRevealHandler   revealHandler;

    // =========================================================================
    // Klasy konfiguracji (Serializable – widoczne w Inspectorze)
    // =========================================================================

    [System.Serializable]
    public class BiomeSettings
    {
        public string   name;
        public BiomeType type;
        public Material  biomeGroundMaterial;
        [Range(0, 100)] public int forestClusterChance;
        [Range(0, 100)] public int mountainChance;
        [Range(0, 100)] public int hillChance;
        [Range(0, 100)] public int sinkholeChance;
        [Range(0, 100)] public int fertileSoilChance;
    }

    [System.Serializable]
    public class ChunkStyleSettings
    {
        [Range(0, 100)] public int highwayWeight    = 20;
        [Range(0, 100)] public int windingWeight    = 40;
        [Range(0, 100)] public int mazeWeight       = 40;
        public int wallCost          = 50;
        public int windingWallCount  = 3;
        public int windingWallLength = 3;
        public int mazeWallCount     = 10;
        public int mazeWallLength    = 4;
    }

    // =========================================================================
    // Unity Lifecycle
    // =========================================================================

    private void Reset()
    {
        biomeSettings.Clear();
        biomeSettings.Add(new BiomeSettings { name = "Plains",    type = BiomeType.Plains,    forestClusterChance = 30, mountainChance = 5,  hillChance = 10, sinkholeChance = 5, fertileSoilChance = 20 });
        biomeSettings.Add(new BiomeSettings { name = "Forest",    type = BiomeType.Forest,    forestClusterChance = 80, mountainChance = 5,  hillChance = 10, sinkholeChance = 5, fertileSoilChance = 10 });
        biomeSettings.Add(new BiomeSettings { name = "Mountains", type = BiomeType.Mountains, forestClusterChance = 10, mountainChance = 40, hillChance = 30, sinkholeChance = 5, fertileSoilChance = 0  });
    }

    private void Start()
    {
        GenerateMap();
    }

    // =========================================================================
    // Główna metoda generacji
    // =========================================================================

    [ContextMenu("Generuj Mapę")]
    public void GenerateMap()
    {
        if (visualizer == null) { Debug.LogError("[HexMapGenerator] Brak przypisanego Visualizera!"); return; }

        // 1. Kontekst i generatory
        ctx = new MapGenerationContext();
        CreateGenerators();

        // 2. Rejestracja chunków
        registry.RegisterAllChunks();

        // 3. Meta chunki (baza, ekspansje) – muszą być przed biomami
        terrainGen.ApplyMetaChunks(baseRoadEndLocal);

        // 4. Generowanie ścieżek (wiele prób jeśli losowo nie wyjdzie)
        bool pathSuccess = false;
        for (int i = 0; i < maxGenerationAttempts; i++)
        {
            if (pathGen.CalculateMainPath(baseRoadEndLocal))
            {
                if (allowExtraSpawners) pathGen.GenerateRecursiveBranches();
                pathSuccess = true;
                Debug.Log("<color=green>[HexMapGenerator] Ścieżki gotowe.</color>");
                break;
            }
        }
        if (!pathSuccess) Debug.LogWarning("[HexMapGenerator] Nie udało się wygenerować tras po " + maxGenerationAttempts + " próbach.");

        // 5. Biomy (po ścieżkach – żeby znać które chunki są zajęte)
        biomeGen.GenerateBiomeMap();

        // 6. Specjalne chunki (po biomach – warunki biomu, po ścieżkach – nie koliduje)
        specialPlacer.PlaceSpecialChunks();

        // 7. Teren (featuresy, ścieżki na hexach)
        terrainGen.GenerateTerrain();

        // 8. Zasoby specjalnych chunków (nadpisują część terenu)
        terrainGen.ApplySpecialChunkResources();

        // 9. Wizualizacja
        var biomeMatDict = BuildBiomeMaterialDict();
        visualizer.VisualizeWorld(ctx.worldData, ctx.allValidChunks, ctx.chunkBiomes, biomeMatDict, chunkRadius, hexSize, padding);

        // 10. Mgła i odkrywanie
        revealHandler.InitializeAndReveal(ctx.allValidChunks, chunkRadius, hexSize, padding);

        Debug.Log("<color=cyan>[HexMapGenerator] Generacja kompletna.</color>");
    }

    // =========================================================================
    // Publiczne API dla innych systemów
    // =========================================================================

    public Dictionary<Vector2Int, Vector2Int> GetRoadRevealDependencies()
        => pathGen.GetRoadRevealDependencies();

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
        var pathfinder = new HexPathfinder(chunkRadius);

        registry = new ChunkRegistry(ctx, metaConfig, mapWidth, mapMinY, mapMaxY);

        pathGen = new PathGenerator(
            ctx, registry, pathfinder,
            chunkRadius, hexSize, padding,
            minChunkDistance, globalObstacleChance, chanceForMoreBranching,
            extraSpawnerChances, extraSpawnDistanceRange,
            internalSettings);

        biomeGen = new BiomeGenerator(ctx, metaConfig, biomeSettings);

        terrainGen = new TerrainGenerator(ctx, registry, metaConfig, biomeSettings, chunkRadius);

        specialPlacer = new SpecialChunkPlacer(ctx, registry, specialChunkTypes);

        revealHandler = new ChunkRevealHandler(ctx, registry, fogManager, expansionManager);
    }

    private Dictionary<BiomeType, Material> BuildBiomeMaterialDict()
    {
        var dict = new Dictionary<BiomeType, Material>();
        foreach (var bs in biomeSettings)
            if (bs.biomeGroundMaterial != null && !dict.ContainsKey(bs.type))
                dict.Add(bs.type, bs.biomeGroundMaterial);
        return dict;
    }
}