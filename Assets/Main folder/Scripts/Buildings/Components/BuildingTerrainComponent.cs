using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Odpowiada za wykrywanie sąsiednich hexów z zasobami
/// oraz obliczanie bonusów produkcji wynikających z terenu.
/// Trzymana jako pole w BuildingEntity (zwykła klasa C#, nie MonoBehaviour).
/// </summary>
public class BuildingTerrainComponent
{
    // --- Referencje ---
    private readonly BuildingData data;
    private readonly Transform buildingTransform;

    // --- Stan ---
    private readonly List<HexCell> adjacentResourceHexes = new List<HexCell>();
    private float cachedOnTopBonus = 0f;

    // --- Konstruktor ---
    /// <param name="buildingTransform">Transform budynku – potrzebny do GetComponentInParent.</param>
    public BuildingTerrainComponent(BuildingData data, Transform buildingTransform)
    {
        this.data = data;
        this.buildingTransform = buildingTransform;
    }

    // --- API Publiczne ---

    /// <summary>
    /// Skanuje sąsiednie heksy i rejestruje te z wymaganym zasobem.
    /// Należy wywołać po umieszczeniu budynku na mapie.
    /// </summary>
    public void FindAdjacentResources()
    {
        UnregisterAll();

        if (data.bonusRule.requiredFeature == HexFeatureType.None) return;

        HexCell myCell = buildingTransform.GetComponentInParent<HexCell>();
        if (myCell == null) return;

        HexMapGenerator mapGen = Object.FindObjectOfType<HexMapGenerator>();
        if (mapGen == null) return;

        // 1. Sprawdzenie „on top" – budynek stoi bezpośrednio na zasobie
        cachedOnTopBonus = 0f;
        if (mapGen.worldData.TryGetValue(myCell.chunkCoord, out var chunkData)
            && chunkData.TryGetValue(myCell.localCoord, out var myHexData))
        {
            if (myHexData.feature == data.bonusRule.requiredFeature)
                cachedOnTopBonus = data.bonusRule.onTopProductionBonus;
        }

        // 2. Sprawdzenie sąsiadów
        List<Vector2Int> neighbors = HexGridMath.GetNeighbors(myCell.localCoord);

        foreach (var neighborCoord in neighbors)
        {
            if (!chunkData.TryGetValue(neighborCoord, out var neighborData)) continue;
            if (neighborData.feature != data.bonusRule.requiredFeature) continue;

            if (HexMapVisualizer.Instance == null) continue;

            HexCell neighborCell = HexMapVisualizer.Instance.GetHexCell(myCell.chunkCoord, neighborCoord);
            if (neighborCell == null) continue;

            // Pomijamy heksy zajęte przez inny budynek
            if (neighborCell.HasBuilding()) continue;

            RegisterHex(neighborCell);
        }
    }

    /// <summary>
    /// Oblicza łączny bonus produkcji z terenu (on-top + sąsiedzi, z uwzględnieniem współdzielenia).
    /// </summary>
    public float CalculateTerrainBonus()
    {
        if (data.bonusRule.requiredFeature == HexFeatureType.None) return 0f;

        float totalBonus = cachedOnTopBonus;
        var rule = data.bonusRule;

        foreach (var cell in adjacentResourceHexes)
        {
            if (cell == null) continue;

            int usersCount = BuildingProductionRegistry.GetUsageCount(cell);
            // Wzór: Base - (Penalty × (n - 1)), nie mniej niż minBonus
            float bonus = rule.baseBonusPerHex - rule.penaltyPerUser * (usersCount - 1);
            if (bonus < rule.minBonus) bonus = rule.minBonus;

            totalBonus += bonus;
        }

        return totalBonus;
    }

    /// <summary>Zwolnienie wszystkich zarejestrowanych hexów (OnDestroy / Demolish).</summary>
    public void ReleaseAll() => UnregisterAll();

    /// <summary>Liczba aktywnie monitorowanych sąsiednich hexów z zasobem.</summary>
    public int AdjacentResourceCount => adjacentResourceHexes.Count;

    /// <summary>Aktualny bonus „on top" (tylko do odczytu, np. w UI).</summary>
    public float OnTopBonus => cachedOnTopBonus;

    // --- Helpers ---

    private void RegisterHex(HexCell cell)
    {
        BuildingProductionRegistry.RegisterUsage(cell);
        adjacentResourceHexes.Add(cell);
    }

    private void UnregisterAll()
    {
        foreach (var cell in adjacentResourceHexes)
        {
            if (cell != null) BuildingProductionRegistry.UnregisterUsage(cell);
        }
        adjacentResourceHexes.Clear();
        cachedOnTopBonus = 0f;
    }
}
