using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Zarządza rejestrem wszystkich chunków na mapie.
/// Odpowiada za: rejestrację, walidację, wykrywanie meta chunków i odległości.
/// </summary>
public class ChunkRegistry
{
    private readonly MapGenerationContext ctx;
    private readonly MetaMapConfig metaConfig;
    private readonly int mapWidth;
    private readonly int mapMinY;
    private readonly int mapMaxY;

    public ChunkRegistry(MapGenerationContext ctx, MetaMapConfig metaConfig,
        int mapWidth, int mapMinY, int mapMaxY)
    {
        this.ctx        = ctx;
        this.metaConfig = metaConfig;
        this.mapWidth   = mapWidth;
        this.mapMinY    = mapMinY;
        this.mapMaxY    = mapMaxY;
    }

    // =========================================================================
    // API Publiczne
    // =========================================================================

    /// <summary>Rejestruje wszystkie chunki w siatce mapy.</summary>
    public void RegisterAllChunks()
    {
        ctx.allValidChunks.Clear();
        for (int x = 0; x <= mapWidth; x++)
            for (int y = mapMinY; y <= mapMaxY; y++)
                ctx.allValidChunks.Add(new Vector2Int(x, y - (x / 2)));
    }

    /// <summary>Sprawdza czy chunk jest meta-chronionym obszarem (baza, ekspansje).</summary>
    public bool IsMetaChunk(Vector2Int coord)
    {
        if (coord == Vector2Int.zero) return true;
        if (metaConfig == null) return false;

        foreach (var exp in metaConfig.expansions)
            if (exp.chunkCoordinate == coord && exp.requiredUpgrade != null && exp.requiredUpgrade.isUnlocked)
                return true;

        return false;
    }

    public bool IsChunkInMap(Vector2Int coord) =>
        ctx.allValidChunks.Contains(coord);

    /// <summary>Zwraca typ chunku (Normal, Spawner, MetaSafe, Special).</summary>
    public ChunkType GetChunkType(Vector2Int coord)
    {
        if (ctx.chunkTypes.TryGetValue(coord, out var type)) return type;
        if (IsMetaChunk(coord)) return ChunkType.MetaSafe;
        return ChunkType.Normal;
    }

    /// <summary>Zwraca koordynat chunku w którym leży dana pozycja świata.</summary>
    public Vector2Int GetChunkCoordFromWorldPosition(Vector3 worldPos,
        int chunkRadius, float hexSize, float padding)
    {
        Vector2Int bestChunk = Vector2Int.zero;
        float minDst = float.MaxValue;

        foreach (var chunk in ctx.allValidChunks)
        {
            Vector3 center = HexGridMath.GetChunkCenterWorld(chunk, chunkRadius, hexSize, padding);
            float d = Vector2.Distance(new Vector2(center.x, center.z), new Vector2(worldPos.x, worldPos.z));
            if (d < minDst) { minDst = d; bestChunk = chunk; }
        }

        return bestChunk;
    }

    /// <summary>
    /// Zwraca kandydatów do spawnu specjalnych chunków –
    /// niebędących meta, w zadanym przedziale odległości, z odpowiednim biomem.
    /// </summary>
    public List<Vector2Int> GetSpecialChunkCandidates(
        Vector2Int distanceRange,
        List<BiomeType> allowedBiomes)
    {
        var result = new List<Vector2Int>();

        foreach (var chunk in ctx.allValidChunks)
        {
            if (chunk == Vector2Int.zero) continue;
            if (IsMetaChunk(chunk)) continue;
            if (ctx.chunkTypes.ContainsKey(chunk)) continue; // Już zajęty

            int dist = HexGridMath.GetDistance(Vector2Int.zero, chunk);
            if (dist < distanceRange.x || dist > distanceRange.y) continue;

            // Sprawdzenie biomu (puste = wszystkie)
            if (allowedBiomes != null && allowedBiomes.Count > 0)
            {
                if (!ctx.chunkBiomes.TryGetValue(chunk, out var biome)) continue;
                if (!allowedBiomes.Contains(biome)) continue;
            }

            result.Add(chunk);
        }

        return result;
    }
}