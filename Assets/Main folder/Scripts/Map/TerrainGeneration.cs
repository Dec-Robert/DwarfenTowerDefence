using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Generuje teren wewnątrz chunków:
/// tworzy HexCellData, oznacza ścieżki, rozstawia featuresy biomów,
/// aplikuje layouty meta chunków oraz oznacza punkty specjalne (Base, Beacon).
/// </summary>
public class TerrainGenerator
{
    private readonly MapGenerationContext ctx;
    private readonly ChunkRegistry registry;
    private readonly MetaMapConfig metaConfig;
    private readonly List<HexMapGenerator.BiomeSettings> biomeSettings;
    private readonly int chunkRadius;

    public TerrainGenerator(MapGenerationContext ctx, ChunkRegistry registry,
        MetaMapConfig metaConfig, List<HexMapGenerator.BiomeSettings> biomeSettings,
        int chunkRadius)
    {
        this.ctx          = ctx;
        this.registry     = registry;
        this.metaConfig   = metaConfig;
        this.biomeSettings = biomeSettings;
        this.chunkRadius  = chunkRadius;
    }

    // =========================================================================
    // Etapy generacji
    // =========================================================================

    /// <summary>Krok 1 – Aplikuje layouty meta chunków (baza, ekspansje, modyfikatory).</summary>
    public void ApplyMetaChunks(Vector2Int baseRoadEndLocal)
    {
        if (metaConfig == null) return;

        // Baza (0,0)
        if (metaConfig.startingChunkBase != null)
        {
            CreateEmptyDataForChunk(Vector2Int.zero);
            ApplyLayoutToChunk(Vector2Int.zero, metaConfig.startingChunkBase);

            if (!ctx.chunkBiomes.ContainsKey(Vector2Int.zero))
                ctx.chunkBiomes[Vector2Int.zero] = BiomeType.Plains;

            if (ctx.worldData[Vector2Int.zero].ContainsKey(baseRoadEndLocal))
                ctx.worldData[Vector2Int.zero][baseRoadEndLocal].isPath = true;
        }

        // Ekspansje meta
        foreach (var exp in metaConfig.expansions)
        {
            if (exp.requiredUpgrade == null || !exp.requiredUpgrade.isUnlocked) continue;

            Vector2Int coord = exp.chunkCoordinate;
            if (!ctx.allValidChunks.Contains(coord)) ctx.allValidChunks.Add(coord);

            CreateEmptyDataForChunk(coord);
            ApplyLayoutToChunk(coord, exp.baseLayout);

            if (!ctx.chunkBiomes.ContainsKey(coord))
                ctx.chunkBiomes[coord] = BiomeType.Plains;
        }

        // Globalne modyfikatory (np. z ulepszeń meta)
        if (metaConfig.globalModifiers != null)
        {
            foreach (var mod in metaConfig.globalModifiers)
            {
                if (mod.requiredUpgrade == null || !mod.requiredUpgrade.isUnlocked) continue;
                if (!ctx.worldData.ContainsKey(mod.targetChunk)) continue;
                ApplyLayoutToChunk(mod.targetChunk, mod.modifierLayout);
            }
        }
    }

    /// <summary>Krok 2 – Generuje teren dla wszystkich chunków.</summary>
    public void GenerateTerrain()
    {
        foreach (var chunkCoord in ctx.allValidChunks)
        {
            EnsureChunkData(chunkCoord);

            // Nanosi ścieżki na dane terenu
            if (ctx.chunkPaths.TryGetValue(chunkCoord, out var pathData) && pathData.internalPath != null)
                foreach (var pathHex in pathData.internalPath)
                    if (ctx.worldData[chunkCoord].ContainsKey(pathHex))
                        ctx.worldData[chunkCoord][pathHex].isPath = true;

            // Featuresy tylko dla nie-meta chunków
            if (!registry.IsMetaChunk(chunkCoord))
                FillChunkFeatures(chunkCoord);
        }

        // Punkty specjalne bazy
        SetFeature(Vector2Int.zero, Vector2Int.zero, HexFeatureType.Base);
        Vector2Int beaconPos = new Vector2Int(0, 1);
        SetFeature(Vector2Int.zero,
            IsPath(Vector2Int.zero, beaconPos) ? new Vector2Int(1, 0) : beaconPos,
            HexFeatureType.Beacon);
    }

    /// <summary>Krok 3 – Aplikuje gwarantowane zasoby specjalnych chunków.</summary>
    public void ApplySpecialChunkResources()
    {
        foreach (var kvp in ctx.specialChunkData)
        {
            Vector2Int chunkCoord    = kvp.Key;
            var definition           = kvp.Value;

            if (!ctx.worldData.ContainsKey(chunkCoord)) continue;
            if (definition.guaranteedResources == null) continue;

            var chunkCells = ctx.worldData[chunkCoord];

            foreach (var deposit in definition.guaranteedResources)
                PlaceResourceDeposit(chunkCoord, chunkCells, deposit);

            // Opcjonalny layout override
            if (definition.layoutOverride != null)
                ApplyLayoutToChunk(chunkCoord, definition.layoutOverride);
        }
    }

    // =========================================================================
    // Gettery
    // =========================================================================

    public void SetFeature(Vector2Int chunk, Vector2Int local, HexFeatureType type)
    {
        if (ctx.worldData.ContainsKey(chunk) && ctx.worldData[chunk].ContainsKey(local))
            ctx.worldData[chunk][local].feature = type;
    }

    public bool IsPath(Vector2Int chunk, Vector2Int local)
    {
        if (ctx.worldData.ContainsKey(chunk) && ctx.worldData[chunk].ContainsKey(local))
            return ctx.worldData[chunk][local].isPath;
        return false;
    }

    // =========================================================================
    // Prywatne
    // =========================================================================

    private void FillChunkFeatures(Vector2Int chunkCoord)
    {
        if (!ctx.chunkBiomes.TryGetValue(chunkCoord, out var biome)) return;

        var chunkCells = ctx.worldData[chunkCoord];
        var settings   = biomeSettings.Find(s => s.type == biome) ?? biomeSettings.FirstOrDefault();
        if (settings == null) return;

        var available = chunkCells.Keys
            .Where(k => !chunkCells[k].isPath && chunkCells[k].feature == HexFeatureType.None)
            .ToList();

        PlaceForestClusters(chunkCells, available, settings);

        available = chunkCells.Keys
            .Where(k => !chunkCells[k].isPath && chunkCells[k].feature == HexFeatureType.None)
            .ToList();

        PlaceScatteredFeatures(chunkCells, available, settings);
    }

    private void PlaceForestClusters(
        Dictionary<Vector2Int, HexCellData> chunkCells,
        List<Vector2Int> available,
        HexMapGenerator.BiomeSettings settings)
    {
        int[] neighborChances = { 50, 40, 30, 20, 10, 0 };
        int numClusters = Random.Range(1, 4);

        for (int i = 0; i < numClusters; i++)
        {
            if (available.Count == 0) break;
            if (Random.Range(0, 100) >= settings.forestClusterChance) continue;

            Vector2Int center = available[Random.Range(0, available.Count)];
            if (!chunkCells.ContainsKey(center) || chunkCells[center].isPath
                || chunkCells[center].feature != HexFeatureType.None) continue;

            chunkCells[center].feature      = HexFeatureType.Forest;
            chunkCells[center].featureLevel = 1;
            available.Remove(center);

            var neighbors = HexGridMath.GetNeighbors(center);
            ShuffleList(neighbors);

            for (int nIdx = 0; nIdx < neighbors.Count; nIdx++)
            {
                var neighbor = neighbors[nIdx];
                if (!chunkCells.ContainsKey(neighbor)) continue;
                if (chunkCells[neighbor].isPath || chunkCells[neighbor].feature != HexFeatureType.None) continue;

                int chance = nIdx < neighborChances.Length ? neighborChances[nIdx] : 0;
                if (Random.Range(0, 100) < chance)
                {
                    chunkCells[neighbor].feature      = HexFeatureType.Forest;
                    chunkCells[neighbor].featureLevel = 0;
                    available.Remove(neighbor);
                }
                else break;
            }
        }
    }

    private void PlaceScatteredFeatures(
        Dictionary<Vector2Int, HexCellData> chunkCells,
        List<Vector2Int> available,
        HexMapGenerator.BiomeSettings settings)
    {
        foreach (var hex in available)
        {
            int roll = Random.Range(0, 100);
            int threshold = settings.mountainChance;
            if (roll < threshold) { chunkCells[hex].feature = HexFeatureType.Mountain; continue; }

            threshold += settings.hillChance;
            if (roll < threshold) { chunkCells[hex].feature = HexFeatureType.Hill; chunkCells[hex].featureLevel = Random.Range(1, 6); continue; }

            threshold += settings.sinkholeChance;
            if (roll < threshold) { chunkCells[hex].feature = HexFeatureType.Sinkhole; chunkCells[hex].featureLevel = Random.Range(-5, 0); continue; }

            threshold += settings.fertileSoilChance;
            if (roll < threshold) { chunkCells[hex].feature = HexFeatureType.FertileSoil; }
        }
    }

    private void PlaceResourceDeposit(
        Vector2Int chunkCoord,
        Dictionary<Vector2Int, HexCellData> chunkCells,
        SpecialChunkDefinition.ResourceDeposit deposit)
    {
        var candidates = chunkCells.Keys
            .Where(k => !chunkCells[k].isPath && chunkCells[k].feature == HexFeatureType.None)
            .ToList();

        int placed = 0;
        ShuffleList(candidates);

        foreach (var coord in candidates)
        {
            if (placed >= deposit.count) break;
            chunkCells[coord].feature = deposit.featureType;
            placed++;
        }
    }

    public void EnsureChunkData(Vector2Int chunkCoord)
    {
        if (ctx.worldData.ContainsKey(chunkCoord)) return;

        ctx.worldData[chunkCoord] = new Dictionary<Vector2Int, HexCellData>();
        GenerateHexGridPoints(chunkRadius, (q, r) =>
        {
            var coord = new Vector2Int(q, r);
            ctx.worldData[chunkCoord][coord] = new HexCellData
            {
                chunkCoord = chunkCoord,
                localCoord = coord
            };
        });
    }

    public void CreateEmptyDataForChunk(Vector2Int coord) => EnsureChunkData(coord);

    private void ApplyLayoutToChunk(Vector2Int chunkCoord, ChunkLayoutSO layout)
    {
        if (layout?.hexes == null) return;
        var chunkData = ctx.worldData[chunkCoord];

        foreach (var hexDef in layout.hexes)
        {
            if (!chunkData.ContainsKey(hexDef.localCoord)) continue;

            var cell = chunkData[hexDef.localCoord];
            if (hexDef.feature != HexFeatureType.None)
            {
                cell.feature      = hexDef.feature;
                cell.featureLevel = hexDef.featureLevel;
            }

            if (hexDef.building != null)
            {
                if (hexDef.building.allowedTerrain.Contains(cell.feature))
                    cell.startingBuilding = hexDef.building;
                else
                    Debug.LogError($"[TerrainGenerator] Budynek {hexDef.building.buildingName} na złym terenie {cell.feature} w chunku {chunkCoord}!");
            }
        }
    }

    private void GenerateHexGridPoints(int radius, System.Action<int, int> action)
    {
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Mathf.Max(-radius, -q - radius);
            int r2 = Mathf.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++) action(q, r);
        }
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int r  = Random.Range(i, list.Count);
            list[i] = list[r];
            list[r] = temp;
        }
    }
}