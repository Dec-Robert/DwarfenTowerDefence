using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Zarządza początkowym odkrywaniem chunków i inicjalizacją FogOfWar.
/// Odpowiada wyłącznie za: które chunki są widoczne na starcie i
/// przekazanie listy do MapExpansionManager.
/// </summary>
public class ChunkRevealHandler
{
    private readonly MapGenerationContext ctx;
    private readonly ChunkRegistry registry;
    private readonly FogOfWarManager fogManager;
    private readonly MapExpansionManager expansionManager;

    public ChunkRevealHandler(
        MapGenerationContext ctx,
        ChunkRegistry registry,
        FogOfWarManager fogManager,
        MapExpansionManager expansionManager)
    {
        this.ctx              = ctx;
        this.registry         = registry;
        this.fogManager       = fogManager;
        this.expansionManager = expansionManager;
    }

    // =========================================================================
    // API Publiczne
    // =========================================================================

    /// <summary>
    /// Inicjalizuje mgłę i odkrywa chunki startowe.
    /// Wywołaj po VisualizeWorld().
    /// </summary>
    public void InitializeAndReveal(
        HashSet<Vector2Int> allValidChunks,
        int chunkRadius, float hexSize, float padding)
    {
        if (fogManager == null) return;

        fogManager.InitializeFog(allValidChunks, chunkRadius, hexSize, padding);

        var revealed = new List<Vector2Int>();

        RevealBase(revealed);
        RevealFirstRoadChunk(revealed);
        RevealMetaChunks(revealed);

       // expansionManager?.Initialize(revealed);
    }

    // =========================================================================
    // Prywatne
    // =========================================================================

    private void RevealBase(List<Vector2Int> revealed)
    {
        Reveal(Vector2Int.zero, revealed);
    }

    private void RevealFirstRoadChunk(List<Vector2Int> revealed)
    {
        // Pierwszy chunk na drodze (bezpośredni sąsiad bazy)
        for (int i = ctx.generatedChunkSequence.Count - 1; i >= 0; i--)
        {
            var chunk = ctx.generatedChunkSequence[i];
            if (HexGridMath.GetDistance(chunk, Vector2Int.zero) == 1)
            {
                Reveal(chunk, revealed);
                break;
            }
        }
    }

    private void RevealMetaChunks(List<Vector2Int> revealed)
    {
        foreach (var chunk in ctx.allValidChunks)
            if (registry.IsMetaChunk(chunk))
                Reveal(chunk, revealed);
    }

    private void Reveal(Vector2Int chunk, List<Vector2Int> revealed)
    {
        if (revealed.Contains(chunk)) return;
        fogManager.RevealChunk(chunk);
        revealed.Add(chunk);
    }
}