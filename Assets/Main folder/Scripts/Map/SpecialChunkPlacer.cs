using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Losuje i konfiguruje specjalne chunki na mapie.
/// Działa PO GenerateBiomeMap() i PO CalculateMainPath() –
/// bo musi znać biomy i nie może nadpisywać chunków ze ścieżkami.
///
/// Wyniki trafiają do ctx.specialChunkData i ctx.chunkTypes,
/// skąd TerrainGenerator i Visualizer je odczytują.
/// </summary>
public class SpecialChunkPlacer
{
    private readonly MapGenerationContext ctx;
    private readonly ChunkRegistry registry;
    private readonly List<SpecialChunkDefinition> definitions;

    public SpecialChunkPlacer(
        MapGenerationContext ctx,
        ChunkRegistry registry,
        List<SpecialChunkDefinition> definitions)
    {
        this.ctx         = ctx;
        this.registry    = registry;
        this.definitions = definitions;
    }

    // =========================================================================
    // API Publiczne
    // =========================================================================

    /// <summary>
    /// Przechodzi przez wszystkie definicje i losuje które chunki będą specjalne.
    /// </summary>
    public void PlaceSpecialChunks()
    {
        if (definitions == null || definitions.Count == 0) return;

        var placedUnique = new HashSet<string>(); // Pilnuje uniquePerMap

        foreach (var definition in definitions)
        {
            if (definition == null) continue;

            // Unique – pomijamy jeśli już postawiony
            if (definition.uniquePerMap && placedUnique.Contains(definition.chunkId))
                continue;

            // Losuj czy w ogóle się pojawi
            if (Random.Range(0, 100) >= definition.spawnChance) continue;

            // Znajdź kandydatów
            var candidates = registry.GetSpecialChunkCandidates(
                definition.distanceRange,
                definition.allowedBiomes);

            // Wyklucz chunki ze ścieżkami wrogów
            candidates.RemoveAll(c => ctx.chunkPaths.ContainsKey(c));

            if (candidates.Count == 0)
            {
                Debug.LogWarning($"[SpecialChunkPlacer] Brak kandydatów dla '{definition.chunkId}'.");
                continue;
            }

            Vector2Int chosen = candidates[Random.Range(0, candidates.Count)];
            ctx.chunkTypes[chosen]    = ChunkType.Special;
            ctx.specialChunkData[chosen] = definition;

            if (definition.uniquePerMap) placedUnique.Add(definition.chunkId);

            Debug.Log($"[SpecialChunkPlacer] Chunk specjalny '{definition.chunkId}' → {chosen}");
        }
    }

    /// <summary>
    /// Zwraca definicję specjalnego chunku lub null.
    /// </summary>
    public SpecialChunkDefinition GetDefinition(Vector2Int chunkCoord)
    {
        ctx.specialChunkData.TryGetValue(chunkCoord, out var def);
        return def;
    }
}