using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Proceduralny generator terenu i surowców wewnątrz chunków:
/// - Czysty model pierścieni odległości od bazy (brak biomów).
/// - Determinizm przez MapRng (ctx.Rng).
/// - Twarde gwarancje surowców (EnsureGuarantees):
///   * Ring 1-2: Forest >= 10, FertileSoil >= 6, Hill >= 6, Mountain == 0.
///   * Ring 3-4: Mountain >= 10 w co najmniej 2 unikalnych chunkach.
/// - Ochrona ścieżek wrogów (isPath == true) i chunków specjalnych/meta.
/// </summary>
public class TerrainGenerator
{
    private readonly MapGenerationContext ctx;
    private readonly ChunkRegistry registry;
    private readonly MetaMapConfig metaConfig;
    private readonly int chunkRadius;
    private readonly TerrainGenerationConfig terrainConfig;

    public TerrainGenerator(
        MapGenerationContext ctx,
        ChunkRegistry registry,
        MetaMapConfig metaConfig,
        int chunkRadius,
        TerrainGenerationConfig terrainConfig = null)
    {
        this.ctx           = ctx;
        this.registry      = registry;
        this.metaConfig    = metaConfig;
        this.chunkRadius   = chunkRadius;
        this.terrainConfig = (object)terrainConfig != null ? terrainConfig : TerrainGenerationConfig.CreateDefault();
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
        }

        // Globalne modyfikatory (np. z ulepszeń meta)
        if (metaConfig.globalModifiers != null)
        {
            foreach (var mod in metaConfig.globalModifiers)
            {
                if (mod.requiredUpgrade == null || !mod.requiredUpgrade.isUnlocked) continue;

                if (!ctx.allValidChunks.Contains(mod.targetChunk))
                    ctx.allValidChunks.Add(mod.targetChunk);

                CreateEmptyDataForChunk(mod.targetChunk);
                ApplyLayoutToChunk(mod.targetChunk, mod.modifierLayout);
            }
        }
    }

    /// <summary>Krok 2 – Generuje teren dla wszystkich chunków (reguły pierścieni bez biomów).</summary>
    public void GenerateTerrain()
    {
        foreach (var chunkCoord in ctx.allValidChunks)
        {
            EnsureChunkData(chunkCoord);

            // Nanosi ścieżki na dane terenu
            if (ctx.chunkPaths.TryGetValue(chunkCoord, out var pathData) && pathData.internalPath != null)
            {
                foreach (var pathHex in pathData.internalPath)
                {
                    if (ctx.worldData[chunkCoord].ContainsKey(pathHex))
                        ctx.worldData[chunkCoord][pathHex].isPath = true;
                }
            }

            // Featuresy generowane tylko dla nie-meta chunków
            bool isMeta = registry != null ? registry.IsMetaChunk(chunkCoord) : (chunkCoord == Vector2Int.zero);
            if (!isMeta)
            {
                FillChunkFeatures(chunkCoord);
            }
        }

        // Punkty specjalne bazy
        SetFeature(Vector2Int.zero, Vector2Int.zero, HexFeatureType.Base);
        Vector2Int beaconPos = new Vector2Int(0, 1);
        SetFeature(Vector2Int.zero,
            IsPath(Vector2Int.zero, beaconPos) ? new Vector2Int(1, 0) : beaconPos,
            HexFeatureType.Beacon);
    }

    /// <summary>
    /// Krok 3 – Weryfikuje twarde gwarancje zasobów dla pierścieni i uzupełnia ewentualne deficyty.
    /// Ring 1-2: Forest >= 10, FertileSoil >= 6, Hill >= 6, Mountain == 0.
    /// Ring 3-4: Mountain >= 10 w co najmniej 2 chunkach.
    /// </summary>
    public void EnsureGuarantees(TerrainGenerationConfig configOverride = null)
    {
        var config = (object)configOverride != null ? configOverride : ((object)this.terrainConfig != null ? this.terrainConfig : TerrainGenerationConfig.CreateDefault());
        EnsureGuarantees(ctx, config, registry);
    }

    /// <summary>
    /// Krok 4 – Aplikuje gwarantowane zasoby specjalnych chunków.
    /// LayoutOverride jest aplikowany NAJPIERW, a gwarantowane złoża na pozostałych wolnych polach.
    /// </summary>
    public void ApplySpecialChunkResources()
    {
        foreach (var kvp in ctx.specialChunkData)
        {
            Vector2Int chunkCoord = kvp.Key;
            var definition        = kvp.Value;

            if (!ctx.worldData.ContainsKey(chunkCoord)) continue;
            var chunkCells = ctx.worldData[chunkCoord];

            // 1. Opcjonalny layout override APLIKOWANY NAJPIERW (handcrafted layout)
            if ((object)definition.layoutOverride != null)
                ApplyLayoutToChunk(chunkCoord, definition.layoutOverride);

            // 2. Gwarantowane depozyty zasobów aplikowane na pozostałych wolnych polach
            if (definition.guaranteedResources != null)
            {
                foreach (var deposit in definition.guaranteedResources)
                    PlaceResourceDeposit(chunkCoord, chunkCells, deposit);
            }
        }
    }

    // =========================================================================
    // Statyczna implementacja EnsureGuarantees (Dla testów i headless)
    // =========================================================================

    public static void EnsureGuarantees(
        MapGenerationContext ctx,
        TerrainGenerationConfig config,
        ChunkRegistry registry = null)
    {
        if (ctx == null || ctx.worldData == null) return;
        if ((object)config == null) config = TerrainGenerationConfig.CreateDefault();
        if (ctx.Rng == null) ctx.Initialize(ctx.seed > 0 ? ctx.seed : 12345);

        var guarantees = config.guarantees;
        if (guarantees == null || guarantees.Count == 0) return;

        // Faza 1: Ograniczenia twarde (maxAllowed) - np. Mountain == 0 w Ring 1-2
        foreach (var g in guarantees)
        {
            if (g.maxAllowed >= 0)
            {
                EnforceMaxAllowed(ctx, registry, g);
            }
        }

        // Faza 2: Gwarancje rozłożenia w wielu chunkach (minDistinctChunks > 1) - np. Mountain w Ring 3-4 w >= 2 chunkach
        foreach (var g in guarantees)
        {
            if (g.minDistinctChunks > 1)
            {
                EnforceDistinctChunks(ctx, registry, g);
            }
        }

        // Faza 3: Uzupełnienie deficytów ilościowych (minCount > 0)
        foreach (var g in guarantees)
        {
            if (g.minCount > 0)
            {
                TopUpDeficit(ctx, registry, g, guarantees);
            }
        }
    }

    private static void EnforceMaxAllowed(
        MapGenerationContext ctx,
        ChunkRegistry registry,
        ResourceGuarantee g)
    {
        var targetChunks = GetEligibleChunksInRings(ctx, registry, g.ringMin, g.ringMax);

        var offendingHexes = new List<(Vector2Int chunk, Vector2Int local)>();
        foreach (var chunk in targetChunks)
        {
            if (!ctx.worldData.ContainsKey(chunk)) continue;
            var cells = ctx.worldData[chunk];
            foreach (var kvp in cells)
            {
                if (!kvp.Value.isPath && kvp.Value.feature == g.feature)
                {
                    offendingHexes.Add((chunk, kvp.Key));
                }
            }
        }

        if (offendingHexes.Count > g.maxAllowed)
        {
            int toRemove = offendingHexes.Count - g.maxAllowed;
            offendingHexes.Sort((a, b) =>
            {
                int cmp = a.chunk.x != b.chunk.x ? a.chunk.x.CompareTo(b.chunk.x) : a.chunk.y.CompareTo(b.chunk.y);
                if (cmp != 0) return cmp;
                return a.local.x != b.local.x ? a.local.x.CompareTo(b.local.x) : a.local.y.CompareTo(b.local.y);
            });
            ctx.Rng.Shuffle(offendingHexes);

            for (int i = 0; i < toRemove; i++)
            {
                var hex = offendingHexes[i];
                ctx.worldData[hex.chunk][hex.local].feature = HexFeatureType.None;
                ctx.worldData[hex.chunk][hex.local].featureLevel = 0;
            }
        }
    }

    private static void EnforceDistinctChunks(
        MapGenerationContext ctx,
        ChunkRegistry registry,
        ResourceGuarantee g)
    {
        var eligibleChunks = GetEligibleChunksInRings(ctx, registry, g.ringMin, g.ringMax);
        if (eligibleChunks.Count == 0) return;

        var chunksWithFeature = eligibleChunks
            .Where(c => ChunkHasFeature(ctx, c, g.feature))
            .ToList();

        if (chunksWithFeature.Count < g.minDistinctChunks)
        {
            int chunksNeeded = g.minDistinctChunks - chunksWithFeature.Count;
            var candidateChunks = eligibleChunks
                .Except(chunksWithFeature)
                .OrderBy(c => c.x).ThenBy(c => c.y)
                .ToList();

            ctx.Rng.Shuffle(candidateChunks);

            for (int i = 0; i < chunksNeeded && i < candidateChunks.Count; i++)
            {
                var targetChunk = candidateChunks[i];
                PlaceFeatureInChunk(ctx, targetChunk, g.feature, 3); // Klaster min. 3 heksów w nowym chunku
                chunksWithFeature.Add(targetChunk);
            }
        }
    }

    private static void TopUpDeficit(
        MapGenerationContext ctx,
        ChunkRegistry registry,
        ResourceGuarantee g,
        List<ResourceGuarantee> allGuarantees)
    {
        int currentCount = CountFeatureInRings(ctx, registry, g.ringMin, g.ringMax, g.feature);
        int deficit = g.minCount - currentCount;
        if (deficit <= 0) return;

        var eligibleChunks = GetEligibleChunksInRings(ctx, registry, g.ringMin, g.ringMax);
        if (eligibleChunks.Count == 0) return;

        // Zbiór chronionych featuresów w tym przedziale pierścieni (nie wolno ich nadpisywać!)
        var protectedFeatures = new HashSet<HexFeatureType>();
        foreach (var other in allGuarantees)
        {
            if (other.ringMin <= g.ringMax && other.ringMax >= g.ringMin && other.minCount > 0)
            {
                protectedFeatures.Add(other.feature);
            }
        }

        // Zbierz wszystkie wolne komórki w kwalifikujących się chunkach
        var candidateCells = new List<(Vector2Int chunk, Vector2Int local)>();
        foreach (var chunk in eligibleChunks)
        {
            if (!ctx.worldData.ContainsKey(chunk)) continue;
            var cells = ctx.worldData[chunk];
            foreach (var kvp in cells)
            {
                var cell = kvp.Value;
                if (cell.isPath) continue;
                if (cell.feature == HexFeatureType.Base || cell.feature == HexFeatureType.Beacon || cell.feature == HexFeatureType.Wall) continue;
                if (protectedFeatures.Contains(cell.feature)) continue;
                if (cell.feature == HexFeatureType.None)
                {
                    candidateCells.Add((chunk, kvp.Key));
                }
            }
        }

        if (candidateCells.Count == 0) return;

        candidateCells.Sort((a, b) =>
        {
            int cmp = a.chunk.x != b.chunk.x ? a.chunk.x.CompareTo(b.chunk.x) : a.chunk.y.CompareTo(b.chunk.y);
            if (cmp != 0) return cmp;
            return a.local.x != b.local.x ? a.local.x.CompareTo(b.local.x) : a.local.y.CompareTo(b.local.y);
        });

        ctx.Rng.Shuffle(candidateCells);

        // Sortowanie z preferencją klastrowania z istniejącym zasobem
        candidateCells = candidateCells
            .OrderByDescending(item =>
            {
                return HexGridMath.GetNeighbors(item.local).Count(n =>
                    ctx.worldData[item.chunk].ContainsKey(n) &&
                    ctx.worldData[item.chunk][n].feature == g.feature);
            })
            .ToList();

        int placed = 0;
        foreach (var (chunk, local) in candidateCells)
        {
            if (placed >= deficit) break;

            var cell = ctx.worldData[chunk][local];
            cell.feature = g.feature;
            cell.featureLevel = GetDefaultLevelForFeature(g.feature, ctx.Rng);
            placed++;
        }
    }

    private static List<Vector2Int> GetEligibleChunksInRings(
        MapGenerationContext ctx,
        ChunkRegistry registry,
        int ringMin, int ringMax)
    {
        var list = new List<Vector2Int>();
        foreach (var c in ctx.allValidChunks)
        {
            int dist = HexGridMath.GetDistance(Vector2Int.zero, c);
            if (dist < ringMin || dist > ringMax) continue;

            if (registry != null && registry.IsMetaChunk(c)) continue;
            if (c == Vector2Int.zero) continue;

            if (ctx.specialChunkData.ContainsKey(c)) continue;
            if (ctx.chunkTypes.TryGetValue(c, out var type) && type == ChunkType.Special) continue;

            list.Add(c);
        }
        return list;
    }

    private static bool ChunkHasFeature(MapGenerationContext ctx, Vector2Int chunkCoord, HexFeatureType feat)
    {
        if (!ctx.worldData.ContainsKey(chunkCoord)) return false;
        return ctx.worldData[chunkCoord].Values.Any(cell => !cell.isPath && cell.feature == feat);
    }

    private static int ChunkCountFeature(MapGenerationContext ctx, Vector2Int chunkCoord, HexFeatureType feat)
    {
        if (!ctx.worldData.ContainsKey(chunkCoord)) return 0;
        return ctx.worldData[chunkCoord].Values.Count(cell => !cell.isPath && cell.feature == feat);
    }

    private static int CountFeatureInRings(
        MapGenerationContext ctx,
        ChunkRegistry registry,
        int ringMin, int ringMax,
        HexFeatureType feat)
    {
        var chunks = GetEligibleChunksInRings(ctx, registry, ringMin, ringMax);
        int sum = 0;
        foreach (var c in chunks)
        {
            sum += ChunkCountFeature(ctx, c, feat);
        }
        return sum;
    }

    private static void PlaceFeatureInChunk(
        MapGenerationContext ctx,
        Vector2Int chunkCoord,
        HexFeatureType feat,
        int count)
    {
        if (!ctx.worldData.ContainsKey(chunkCoord)) return;
        var cells = ctx.worldData[chunkCoord];

        var available = cells.Where(kvp => !kvp.Value.isPath && kvp.Value.feature == HexFeatureType.None)
            .Select(kvp => kvp.Key)
            .OrderBy(k => k.x).ThenBy(k => k.y)
            .ToList();

        if (available.Count == 0) return;
        ctx.Rng.Shuffle(available);

        Vector2Int seed = available[0];
        cells[seed].feature = feat;
        cells[seed].featureLevel = GetDefaultLevelForFeature(feat, ctx.Rng);
        available.Remove(seed);
        int placed = 1;

        var neighbors = HexGridMath.GetNeighbors(seed);
        neighbors.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        ctx.Rng.Shuffle(neighbors);
        foreach (var n in neighbors)
        {
            if (placed >= count) break;
            if (available.Contains(n))
            {
                cells[n].feature = feat;
                cells[n].featureLevel = GetDefaultLevelForFeature(feat, ctx.Rng);
                available.Remove(n);
                placed++;
            }
        }

        while (placed < count && available.Count > 0)
        {
            var next = available[0];
            cells[next].feature = feat;
            cells[next].featureLevel = GetDefaultLevelForFeature(feat, ctx.Rng);
            available.RemoveAt(0);
            placed++;
        }
    }

    private static int GetDefaultLevelForFeature(HexFeatureType feature, MapRng rng)
    {
        switch (feature)
        {
            case HexFeatureType.Forest: return 1;
            case HexFeatureType.Hill: return rng != null ? rng.Range(1, 4) : 1;
            case HexFeatureType.Mountain: return rng != null ? rng.Range(1, 4) : 1;
            case HexFeatureType.Sinkhole: return rng != null ? rng.Range(-3, 0) : -1;
            default: return 0;
        }
    }

    // =========================================================================
    // Logika generowania wewnątrz chunku (FillChunkFeatures)
    // =========================================================================

    private void FillChunkFeatures(Vector2Int chunkCoord)
    {
        if (!ctx.worldData.ContainsKey(chunkCoord)) return;

        var chunkCells = ctx.worldData[chunkCoord];

        // Wyznacz pierścień odległości od bazy (0,0)
        int ring = HexGridMath.GetDistance(Vector2Int.zero, chunkCoord);

        var config = (object)terrainConfig != null ? terrainConfig : TerrainGenerationConfig.CreateDefault();
        var rule = config.GetRuleForRing(ring);

        var available = chunkCells.Keys
            .Where(k => !chunkCells[k].isPath && chunkCells[k].feature == HexFeatureType.None)
            .OrderBy(k => k.x).ThenBy(k => k.y)
            .ToList();

        // Krok A: Klastry leśne
        if (rule == null || rule.enableForestClusters)
        {
            PlaceForestClusters(chunkCells, available, rule);
        }

        // Krok B: Klastry górskie (tylko w pierścieniach >= 3, NIGDY w Ring 1-2!)
        if (ring >= 3 && rule != null && rule.enableMountainClusters)
        {
            available = chunkCells.Keys
                .Where(k => !chunkCells[k].isPath && chunkCells[k].feature == HexFeatureType.None)
                .OrderBy(k => k.x).ThenBy(k => k.y)
                .ToList();

            PlaceMountainClusters(chunkCells, available, rule);
        }

        // Krok C: Rozproszone featuresy na pozostałych heksach
        available = chunkCells.Keys
            .Where(k => !chunkCells[k].isPath && chunkCells[k].feature == HexFeatureType.None)
            .OrderBy(k => k.x).ThenBy(k => k.y)
            .ToList();

        PlaceScatteredFeatures(chunkCells, available, rule, ring);
    }

    private void PlaceForestClusters(
        Dictionary<Vector2Int, HexCellData> chunkCells,
        List<Vector2Int> available,
        RingFeatureRule rule)
    {
        int[] neighborChances = { 50, 40, 30, 20, 10, 0 };
        Vector2Int countRange = rule != null ? rule.forestClusterCountRange : new Vector2Int(1, 3);
        int clusterChance = rule != null ? rule.forestClusterChance : 35;

        int numClusters = ctx.Rng != null ? ctx.Rng.Range(countRange.x, countRange.y + 1) : Random.Range(countRange.x, countRange.y + 1);

        for (int i = 0; i < numClusters; i++)
        {
            if (available.Count == 0) break;
            int roll = ctx.Rng != null ? ctx.Rng.Range(0, 100) : Random.Range(0, 100);
            if (roll >= clusterChance) continue;

            int centerIdx = ctx.Rng != null ? ctx.Rng.Range(0, available.Count) : Random.Range(0, available.Count);
            Vector2Int center = available[centerIdx];
            if (!chunkCells.ContainsKey(center) || chunkCells[center].isPath
                || chunkCells[center].feature != HexFeatureType.None) continue;

            chunkCells[center].feature = HexFeatureType.Forest;
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
                int nRoll = ctx.Rng != null ? ctx.Rng.Range(0, 100) : Random.Range(0, 100);
                if (nRoll < chance)
                {
                    chunkCells[neighbor].feature = HexFeatureType.Forest;
                    chunkCells[neighbor].featureLevel = 0;
                    available.Remove(neighbor);
                }
                else break;
            }
        }
    }

    private void PlaceMountainClusters(
        Dictionary<Vector2Int, HexCellData> chunkCells,
        List<Vector2Int> available,
        RingFeatureRule rule)
    {
        int[] neighborChances = { 60, 45, 30, 15, 0 };
        Vector2Int countRange = rule != null ? rule.mountainClusterCountRange : new Vector2Int(1, 2);
        int clusterChance = rule != null ? rule.mountainClusterChance : 30;

        int numClusters = ctx.Rng != null ? ctx.Rng.Range(countRange.x, countRange.y + 1) : Random.Range(countRange.x, countRange.y + 1);

        for (int i = 0; i < numClusters; i++)
        {
            if (available.Count == 0) break;
            int roll = ctx.Rng != null ? ctx.Rng.Range(0, 100) : Random.Range(0, 100);
            if (roll >= clusterChance) continue;

            int centerIdx = ctx.Rng != null ? ctx.Rng.Range(0, available.Count) : Random.Range(0, available.Count);
            Vector2Int center = available[centerIdx];
            if (!chunkCells.ContainsKey(center) || chunkCells[center].isPath
                || chunkCells[center].feature != HexFeatureType.None) continue;

            chunkCells[center].feature = HexFeatureType.Mountain;
            chunkCells[center].featureLevel = ctx.Rng != null ? ctx.Rng.Range(1, 4) : 1;
            available.Remove(center);

            var neighbors = HexGridMath.GetNeighbors(center);
            ShuffleList(neighbors);

            for (int nIdx = 0; nIdx < neighbors.Count; nIdx++)
            {
                var neighbor = neighbors[nIdx];
                if (!chunkCells.ContainsKey(neighbor)) continue;
                if (chunkCells[neighbor].isPath || chunkCells[neighbor].feature != HexFeatureType.None) continue;

                int chance = nIdx < neighborChances.Length ? neighborChances[nIdx] : 0;
                int nRoll = ctx.Rng != null ? ctx.Rng.Range(0, 100) : Random.Range(0, 100);
                if (nRoll < chance)
                {
                    chunkCells[neighbor].feature = HexFeatureType.Mountain;
                    chunkCells[neighbor].featureLevel = ctx.Rng != null ? ctx.Rng.Range(1, 4) : 1;
                    available.Remove(neighbor);
                }
                else break;
            }
        }
    }

    private void PlaceScatteredFeatures(
        Dictionary<Vector2Int, HexCellData> chunkCells,
        List<Vector2Int> available,
        RingFeatureRule rule,
        int ring)
    {
        int wMountain = (ring <= 2) ? 0 : (rule != null ? rule.weightMountain : 5);
        int wHill = rule != null ? rule.weightHill : 15;
        int wSoil = rule != null ? rule.weightFertileSoil : 15;
        int wForest = rule != null ? rule.weightForest : 20;
        int wSinkhole = rule != null ? rule.weightSinkhole : 5;
        int wNone = rule != null ? rule.weightNone : 40;

        int totalWeight = wMountain + wHill + wSoil + wForest + wSinkhole + wNone;
        if (totalWeight <= 0) return;

        foreach (var hex in available)
        {
            int roll = ctx.Rng != null ? ctx.Rng.Range(0, totalWeight) : Random.Range(0, totalWeight);

            int cumulative = 0;

            // Mountain (jeśli ring >= 3 i wMountain > 0)
            cumulative += wMountain;
            if (roll < cumulative)
            {
                chunkCells[hex].feature = HexFeatureType.Mountain;
                chunkCells[hex].featureLevel = ctx.Rng != null ? ctx.Rng.Range(1, 4) : 1;
                continue;
            }

            // Hill
            cumulative += wHill;
            if (roll < cumulative)
            {
                chunkCells[hex].feature = HexFeatureType.Hill;
                chunkCells[hex].featureLevel = ctx.Rng != null ? ctx.Rng.Range(1, 4) : 1;
                continue;
            }

            // FertileSoil
            cumulative += wSoil;
            if (roll < cumulative)
            {
                chunkCells[hex].feature = HexFeatureType.FertileSoil;
                chunkCells[hex].featureLevel = 0;
                continue;
            }

            // Forest
            cumulative += wForest;
            if (roll < cumulative)
            {
                chunkCells[hex].feature = HexFeatureType.Forest;
                chunkCells[hex].featureLevel = 0;
                continue;
            }

            // Sinkhole
            cumulative += wSinkhole;
            if (roll < cumulative)
            {
                chunkCells[hex].feature = HexFeatureType.Sinkhole;
                chunkCells[hex].featureLevel = ctx.Rng != null ? ctx.Rng.Range(-3, 0) : -1;
                continue;
            }

            // None (trawa)
            chunkCells[hex].feature = HexFeatureType.None;
            chunkCells[hex].featureLevel = 0;
        }
    }

    private void PlaceResourceDeposit(
        Vector2Int chunkCoord,
        Dictionary<Vector2Int, HexCellData> chunkCells,
        SpecialChunkDefinition.ResourceDeposit deposit)
    {
        var candidates = chunkCells.Keys
            .Where(k => !chunkCells[k].isPath
                     && chunkCells[k].feature == HexFeatureType.None
                     && chunkCells[k].startingBuilding == null)
            .OrderBy(k => k.x).ThenBy(k => k.y)
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
                cell.startingBuilding = hexDef.building;
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
        if (ctx.Rng != null)
        {
            ctx.Rng.Shuffle(list);
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int r  = Random.Range(i, list.Count);
            list[i] = list[r];
            list[r] = temp;
        }
    }

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
}