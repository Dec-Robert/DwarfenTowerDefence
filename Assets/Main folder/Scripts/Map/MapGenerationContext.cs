using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Kontener współdzielonego stanu generacji mapy.
/// Przekazywany przez konstruktor do każdego generatora – zamiast pól w HexMapGenerator.
///
/// Generatory TYLKO CZYTAJĄ dane innych generatorów, nigdy nie piszą do cudzych sekcji.
/// Wyjątek: worldData jest wspólne – PathGenerator oznacza ścieżki, TerrainGenerator dodaje featuresy.
/// </summary>
public class MapGenerationContext
{
    // --- Dane świata (TerrainGenerator + PathGenerator) ---
    public readonly Dictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>> worldData
        = new Dictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>>();

    // --- Dane biomów (BiomeGenerator → TerrainGenerator, Visualizer) ---
    public readonly Dictionary<Vector2Int, BiomeType> chunkBiomes
        = new Dictionary<Vector2Int, BiomeType>();

    // --- Dane ścieżek (PathGenerator → TerrainGenerator, EnemySpawner) ---
    public readonly Dictionary<Vector2Int, ChunkPathData> chunkPaths
        = new Dictionary<Vector2Int, ChunkPathData>();

    public readonly Dictionary<Vector2Int, Dictionary<Vector2Int, int>> chunkInternalCosts
        = new Dictionary<Vector2Int, Dictionary<Vector2Int, int>>();

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

    /// <summary>Czyści cały stan przed nową generacją.</summary>
    public void Reset()
    {
        worldData.Clear();
        chunkBiomes.Clear();
        chunkPaths.Clear();
        chunkInternalCosts.Clear();
        allValidChunks.Clear();
        generatedChunkSequence.Clear();
        extraSpawnerChunks.Clear();
        chunkTypes.Clear();
        specialChunkData.Clear();
        mainSpawnerChunk = default;
    }
}