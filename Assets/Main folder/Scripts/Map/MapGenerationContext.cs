using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministyczny generator liczb pseudolosowych oparty na System.Random.
/// Zapewnia pełną powtarzalność generacji mapy dla zadanego seeda.
/// Bezpieczny dla wątków pobocznych i testów EditMode.
/// </summary>
public class MapRng
{
    private readonly System.Random _random;

    /// <summary>Seed użyty do zainicjalizowania tego generatora.</summary>
    public int Seed { get; }

    public MapRng(int seed)
    {
        Seed = seed;
        _random = new System.Random(seed);
    }

    /// <summary>
    /// Zwraca losową liczbę całkowitą z przedziału [min, maxExclusive).
    /// Jeśli min >= maxExclusive, zwraca min.
    /// </summary>
    public int Range(int min, int maxExclusive)
    {
        if (min >= maxExclusive) return min;
        return _random.Next(min, maxExclusive);
    }

    /// <summary>
    /// Zwraca losową liczbę zmiennoprzecinkową z przedziału [min, max).
    /// Jeśli min >= max, zwraca min.
    /// </summary>
    public float Range(float min, float max)
    {
        if (min >= max) return min;
        return (float)(min + _random.NextDouble() * (max - min));
    }

    /// <summary>
    /// Zwraca losową wartość zmiennoprzecinkową z przedziału [0.0, 1.0).
    /// Odpowiednik UnityEngine.Random.value.
    /// </summary>
    public float Value => (float)_random.NextDouble();

    /// <summary>
    /// Losowo tasuje elementy listy lub tablicy za pomocą algorytmu Fishera-Yatesa (Knuth Shuffle).
    /// </summary>
    public void Shuffle<T>(IList<T> list)
    {
        if (list == null || list.Count <= 1) return;
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _random.Next(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}

/// <summary>
/// Definicja topologiczna pętli drogowej.
/// Łączy Rozdroże 2 (punkt odgałęzienia dalej od bazy) z Rozdrożem 1 (punkt powrotu bliżej bazy).
/// </summary>
[Serializable]
public class LoopDefinition
{
    /// <summary>Rozdroże 2 (dalej od bazy) – chunk, w którym pętla odgałęzia się od głównej trasy.</summary>
    public Vector2Int splitCrossroads;

    /// <summary>Rozdroże 1 (bliżej bazy) – chunk, w którym pętla ponownie łączy się ze ścieżką A.</summary>
    public Vector2Int mergeCrossroads;

    /// <summary>Lista chunków głównej ścieżki A omijanych przez tę pętlę.</summary>
    public List<Vector2Int> mainSegmentChunks = new List<Vector2Int>();

    /// <summary>Lista dedykowanych chunków tworzących obejście pętli.</summary>
    public List<Vector2Int> loopChunks = new List<Vector2Int>();

    /// <summary>Ułamek natężenia ruchu wrogów kierowany w pętlę (domyślnie 0.30f = 30%).</summary>
    public float loopFlowRatio = 0.3f;

    public LoopDefinition()
    {
        mainSegmentChunks = new List<Vector2Int>();
        loopChunks = new List<Vector2Int>();
    }

    public LoopDefinition(Vector2Int split, Vector2Int merge, float flowRatio = 0.3f)
    {
        splitCrossroads = split;
        mergeCrossroads = merge;
        loopFlowRatio = flowRatio;
        mainSegmentChunks = new List<Vector2Int>();
        loopChunks = new List<Vector2Int>();
    }
}

/// <summary>
/// Kontener współdzielonego stanu generacji mapy.
/// Przekazywany przez konstruktor do każdego generatora – zamiast pól w HexMapGenerator.
///
/// Generatory TYLKO CZYTAJĄ dane innych generatorów, nigdy nie piszą do cudzych sekcji.
/// Wyjątek: worldData jest wspólne – PathGenerator oznacza ścieżki, TerrainGenerator dodaje featuresy.
/// </summary>
public class MapGenerationContext
{
    // --- Seed i Determinizm ---
    public int seed { get; set; }
    public MapRng Rng { get; set; }

    // --- Dane świata (TerrainGenerator + PathGenerator) ---
    public readonly Dictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>> worldData
        = new Dictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>>();

    // --- Dane ścieżek (PathGenerator → TerrainGenerator, EnemySpawner) ---
    public readonly Dictionary<Vector2Int, ChunkPathData> chunkPaths
        = new Dictionary<Vector2Int, ChunkPathData>();

    public readonly Dictionary<Vector2Int, Dictionary<Vector2Int, int>> chunkInternalCosts
        = new Dictionary<Vector2Int, Dictionary<Vector2Int, int>>();

    // --- Topologia rozdroży i pętli (PathGenerator → EnemySpawner, MapExpansionManager) ---
    public readonly List<LoopDefinition> loops = new List<LoopDefinition>();
    public readonly HashSet<Vector2Int> crossroadsChunks = new HashSet<Vector2Int>();

    // --- Dane chunków (ChunkRegistry → wszystko) ---
    public readonly HashSet<Vector2Int> allValidChunks
        = new HashSet<Vector2Int>();

    // --- Wyniki generacji ścieżki ---
    public List<Vector2Int> generatedChunkSequence = new List<Vector2Int>();
    public Vector2Int mainSpawnerChunk;
    public readonly List<Vector2Int> extraSpawnerChunks = new List<Vector2Int>();

    // --- Typy chunków (ChunkRegistry + SpecialChunkPlacer) ---
    public readonly Dictionary<Vector2Int, ChunkType> chunkTypes
        = new Dictionary<Vector2Int, ChunkType>();

    // --- Specjalne chunki (SpecialChunkPlacer → TerrainGenerator, Visualizer) ---
    public readonly Dictionary<Vector2Int, SpecialChunkDefinition> specialChunkData
        = new Dictionary<Vector2Int, SpecialChunkDefinition>();

    public MapGenerationContext(int inputSeed = 0)
    {
        Initialize(inputSeed);
    }

    /// <summary>
    /// Inicjalizuje generator liczb pseudolosowych dla zadanego seeda.
    /// Jeśli inputSeed == 0, losuje nową wartość za pomocą System.Random.
    /// </summary>
    public void Initialize(int inputSeed)
    {
        seed = (inputSeed == 0) ? new System.Random().Next(1, int.MaxValue) : inputSeed;
        Rng = new MapRng(seed);
    }

    /// <summary>Czyści cały stan przed nową generacją.</summary>
    public void Reset(int newSeed = 0)
    {
        worldData.Clear();
        chunkPaths.Clear();
        chunkInternalCosts.Clear();
        allValidChunks.Clear();
        generatedChunkSequence.Clear();
        extraSpawnerChunks.Clear();
        chunkTypes.Clear();
        specialChunkData.Clear();
        mainSpawnerChunk = default;
        loops.Clear();
        crossroadsChunks.Clear();

        Initialize(newSeed);
    }
}