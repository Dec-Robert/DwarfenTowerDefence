using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Generuje mapę biomów – przypisuje każdemu chunkowi typ biomu
/// metodą Voronoi z losowymi centrami.
/// </summary>
public class BiomeGenerator
{
    private readonly MapGenerationContext ctx;
    private readonly MetaMapConfig metaConfig;
    private readonly List<HexMapGenerator.BiomeSettings> biomeSettings;

    public BiomeGenerator(MapGenerationContext ctx, MetaMapConfig metaConfig,
        List<HexMapGenerator.BiomeSettings> biomeSettings)
    {
        this.ctx           = ctx;
        this.metaConfig    = metaConfig;
        this.biomeSettings = biomeSettings;
    }

    // =========================================================================
    // API Publiczne
    // =========================================================================

    public void GenerateBiomeMap()
    {
        var centerTypes = new Dictionary<Vector2Int, BiomeType>();
        var centers     = new List<Vector2Int>();

        // Baza zawsze Plains
        centers.Add(Vector2Int.zero);
        centerTypes[Vector2Int.zero] = BiomeType.Plains;

        // Losowe centra biomów
        var availableBiomes = biomeSettings
            .Where(bs => bs.type != BiomeType.Plains)
            .Select(bs => bs.type)
            .ToList();

        Shuffle(availableBiomes);

        // Kandydaci na centra – nie-meta
        var validCandidates = ctx.allValidChunks.ToList();
        validCandidates.Remove(Vector2Int.zero);
        if (metaConfig != null)
            foreach (var exp in metaConfig.expansions)
                if (exp.requiredUpgrade != null && exp.requiredUpgrade.isUnlocked)
                    validCandidates.Remove(exp.chunkCoordinate);

        int biomesToPick = Mathf.Min(2, availableBiomes.Count);
        for (int i = 0; i < biomesToPick; i++)
        {
            if (validCandidates.Count == 0) break;
            var rndChunk = validCandidates[Random.Range(0, validCandidates.Count)];
            validCandidates.Remove(rndChunk);
            centers.Add(rndChunk);
            centerTypes[rndChunk] = availableBiomes[i];
        }

        // Voronoi – każdy chunk dostaje biom najbliższego centrum
        foreach (var chunk in ctx.allValidChunks)
        {
            if (ctx.chunkBiomes.ContainsKey(chunk)) continue;

            Vector2Int closestCenter = Vector2Int.zero;
            float minDst = float.MaxValue;

            foreach (var center in centers)
            {
                float d = HexGridMath.GetDistance(chunk, center) + Random.Range(-0.5f, 0.5f);
                if (d < minDst) { minDst = d; closestCenter = center; }
            }

            ctx.chunkBiomes[chunk] = centerTypes[closestCenter];
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int r = Random.Range(i, list.Count);
            list[i] = list[r];
            list[r] = temp;
        }
    }
}