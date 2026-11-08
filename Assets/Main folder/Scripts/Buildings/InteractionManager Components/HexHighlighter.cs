using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Zarządza podświetlaniem heksów z zasobami oraz wzniesień podczas trybu budowania i inspekcji.
///     Odpowiada wyłącznie za wizualne wyróżnienie i unoszenie terenu – nie zna logiki budynków.
/// </summary>
public class HexHighlighter
{
    private readonly List<HexCell> highlightedHexes = new();
    private readonly Material highlightMat;
    private readonly HexMapGenerator mapGenerator;

    public HexHighlighter(HexMapGenerator mapGenerator, Material highlightMat)
    {
        this.mapGenerator = mapGenerator;
        this.highlightMat = highlightMat;
    }

    // =========================================================================
    // API Publiczne
    // =========================================================================

    /// <summary>
    ///     Podświetla i unosi heksy sąsiednie oraz środkowy, jeśli mają wymagany feature lub wzniesienie.
    /// </summary>
    public void HighlightFor(BuildingData data, Vector2Int centerChunk, Vector2Int centerLocal,
        BuildingEntity entity = null, Material overrideMat = null)
    {
        Clear();

        if (data == null) return;
        if (mapGenerator == null || mapGenerator.worldData == null || !mapGenerator.worldData.ContainsKey(centerChunk)) return;

        var matToUse = overrideMat != null ? overrideMat : highlightMat;
        var chunkData = mapGenerator.worldData[centerChunk];

        var rules = data.GetEffectiveBonusRules();
        bool hasBonusRules = rules != null && rules.Count > 0;
        bool hasElevationBonus = data.elevationBonus.enabled;

        if (!hasBonusRules && !hasElevationBonus) return;

        int chunkRadius = mapGenerator.chunkRadius;

        // 1. Cechy terenu (np. Las dla Tartaku, Góra dla Kopalni)
        if (hasBonusRules)
        {
            foreach (var rule in rules)
            {
                if (rule.requiredFeature == HexFeatureType.None) continue;

                var radius = rule.searchRadius <= 0 ? 1 : rule.searchRadius;

                if (entity != null && entity.Upgrades != null)
                {
                    if (entity.Upgrades.HasSpecialEffect("INCREASE_RANGE_1")) radius += 1;
                    if (entity.Upgrades.HasSpecialEffect("INCREASE_RANGE_2")) radius += 2;
                }

                var candidates = HexGridMath.GetHexesInRadiusCrossChunk(centerChunk, centerLocal, radius, chunkRadius);

                foreach (var nInfo in candidates)
                {
                    if (nInfo.chunkCoord != centerChunk && !HexGridMath.IsChunkRevealed(nInfo.chunkCoord)) continue;
                    if (!mapGenerator.worldData.TryGetValue(nInfo.chunkCoord, out var targetChunkData)) continue;
                    if (!targetChunkData.TryGetValue(nInfo.localCoord, out var hexData)) continue;
                    if (hexData.feature != rule.requiredFeature) continue;

                    var cell = mapGenerator.visualizer != null
                        ? mapGenerator.visualizer.GetHexCell(nInfo.chunkCoord, nInfo.localCoord)
                        : null;
                    if (cell == null || highlightedHexes.Contains(cell)) continue;

                    cell.ToggleHighlight(true, matToUse);
                    highlightedHexes.Add(cell);
                }
            }
        }

        // 2. Bonusy wysokościowe (np. Quarry / Kamieniołom)
        if (hasElevationBonus)
        {
            var radius = data.elevationBonus.searchRadius <= 0 ? 1 : data.elevationBonus.searchRadius;

            if (entity != null && entity.Upgrades != null)
            {
                if (entity.Upgrades.HasSpecialEffect("INCREASE_RANGE_1")) radius += 1;
                if (entity.Upgrades.HasSpecialEffect("INCREASE_RANGE_2")) radius += 2;
            }

            var candidates = HexGridMath.GetHexesInRadiusCrossChunk(centerChunk, centerLocal, radius, chunkRadius);

            foreach (var nInfo in candidates)
            {
                if (nInfo.chunkCoord != centerChunk && !HexGridMath.IsChunkRevealed(nInfo.chunkCoord)) continue;
                if (!mapGenerator.worldData.TryGetValue(nInfo.chunkCoord, out var targetChunkData)) continue;
                if (!targetChunkData.TryGetValue(nInfo.localCoord, out var hexData)) continue;

                var cell = mapGenerator.visualizer != null
                    ? mapGenerator.visualizer.GetHexCell(nInfo.chunkCoord, nInfo.localCoord)
                    : null;
                if (cell == null || highlightedHexes.Contains(cell)) continue;

                int elevation = cell.Elevation;
                if (elevation <= 0)
                {
                    elevation = hexData.elevation;
                }
                if (elevation <= 0) continue;

                cell.ToggleHighlight(true, matToUse);
                highlightedHexes.Add(cell);
            }
        }
    }

    /// <summary>Wyłącza wszystkie aktywne podświetlenia.</summary>
    public void Clear()
    {
        foreach (var cell in highlightedHexes)
            if (cell != null)
                cell.ToggleHighlight(false);

        highlightedHexes.Clear();
    }

    private List<Vector2Int> GetHexesInRadius(Vector2Int center, int radius)
    {
        var results = new List<Vector2Int>();
        for (var q = -radius; q <= radius; q++)
        {
            var r1 = Mathf.Max(-radius, -q - radius);
            var r2 = Mathf.Min(radius, -q + radius);
            for (var r = r1; r <= r2; r++) results.Add(new Vector2Int(center.x + q, center.y + r));
        }

        return results;
    }
}
