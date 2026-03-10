using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zarządza podświetlaniem heksów z zasobami podczas trybu budowania i inspekcji.
/// Odpowiada wyłącznie za wizualne wyróżnienie – nie zna logiki budynków.
/// </summary>
public class HexHighlighter
{
    private readonly HexMapGenerator mapGenerator;
    private readonly Material highlightMat;
    private readonly List<HexCell> highlightedHexes = new List<HexCell>();

    public HexHighlighter(HexMapGenerator mapGenerator, Material highlightMat)
    {
        this.mapGenerator = mapGenerator;
        this.highlightMat = highlightMat;
    }

    // =========================================================================
    // API Publiczne
    // =========================================================================

    /// <summary>
    /// Podświetla heksy sąsiednie i środkowy jeśli mają wymagany feature.
    /// </summary>
    public void HighlightFor(BuildingData data, Vector2Int centerChunk, Vector2Int centerLocal, BuildingEntity entity = null)
    {
        Clear();

        if (data.bonusRule.requiredFeature == HexFeatureType.None) return;
        if (!mapGenerator.worldData.ContainsKey(centerChunk)) return;

        var chunkData = mapGenerator.worldData[centerChunk];

        // 1. Ustalenie promienia (Baza z configu)
        int radius = data.bonusRule.searchRadius <= 0 ? 1 : data.bonusRule.searchRadius;

        // 2. Modyfikacja promienia na podstawie ulepszeń (jeśli inspektujemy już wybudowany budynek)
        if (entity != null && entity.Upgrades != null)
        {
            if (entity.Upgrades.HasSpecialEffect("INCREASE_RANGE_1")) radius += 1;
            if (entity.Upgrades.HasSpecialEffect("INCREASE_RANGE_2")) radius += 2;
        }

        // 3. Zbieranie wszystkich kandydatów w promieniu (a nie tylko bezpośrednich sąsiadów)
        var candidates = GetHexesInRadius(centerLocal, radius);

        foreach (var coord in candidates)
        {
            if (!chunkData.ContainsKey(coord)) continue;
            if (chunkData[coord].feature != data.bonusRule.requiredFeature) continue;

            HexCell cell = mapGenerator.visualizer.GetHexCell(centerChunk, coord);
            if (cell == null) continue;

            cell.ToggleHighlight(true, highlightMat);
            highlightedHexes.Add(cell);
        }
    }

    /// <summary>Wyłącza wszystkie aktywne podświetlenia.</summary>
    public void Clear()
    {
        foreach (var cell in highlightedHexes)
            if (cell != null) cell.ToggleHighlight(false);

        highlightedHexes.Clear();
    }
    private List<Vector2Int> GetHexesInRadius(Vector2Int center, int radius)
    {
        var results = new List<Vector2Int>();
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Mathf.Max(-radius, -q - radius);
            int r2 = Mathf.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++)
            {
                results.Add(new Vector2Int(center.x + q, center.y + r));
            }
        }
        return results;
    }
}