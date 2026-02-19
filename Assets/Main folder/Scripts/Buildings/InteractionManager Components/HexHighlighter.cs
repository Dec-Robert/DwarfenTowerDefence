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
    public void HighlightFor(BuildingData data, Vector2Int centerChunk, Vector2Int centerLocal)
    {
        Clear();

        if (data.bonusRule.requiredFeature == HexFeatureType.None) return;
        if (!mapGenerator.worldData.ContainsKey(centerChunk)) return;

        var chunkData = mapGenerator.worldData[centerChunk];

        // Sąsiedzi + środkowy heks
        var candidates = HexGridMath.GetNeighbors(centerLocal);
        candidates.Add(centerLocal);

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
}