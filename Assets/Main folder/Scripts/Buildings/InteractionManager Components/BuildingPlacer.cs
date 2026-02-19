using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Odpowiada za walidację miejsca pod budynek, fizyczną budowę
/// i powiadamianie sąsiednich budynków o zmianie terenu.
/// </summary>
public class BuildingPlacer
{
    private readonly HexMapGenerator mapGenerator;

    public BuildingPlacer(HexMapGenerator mapGenerator)
    {
        this.mapGenerator = mapGenerator;
    }

    // =========================================================================
    // API Publiczne
    // =========================================================================

    /// <summary>Sprawdza czy można postawić budynek na danym heksie.</summary>
    public bool IsPlacementValid(HexCell cell, BuildingData data)
    {
        if (!mapGenerator.worldData.ContainsKey(cell.chunkCoord)) return false;

        HexCellData cellData = mapGenerator.worldData[cell.chunkCoord][cell.localCoord];

        if (cellData.isPath)                                          return false;
        if (!data.allowedTerrain.Contains(cellData.feature))         return false;
        if (cell.GetComponentInChildren<BuildingEntity>() != null)   return false;

        return true;
    }

    /// <summary>
    /// Próbuje pobrać zasoby i postawić budynek.
    /// Zwraca true jeśli budowa się powiodła.
    /// </summary>
    public bool TryBuild(HexCell cell, BuildingData data)
    {
        if (!IsPlacementValid(cell, data)) return false;

        var costs = data.GetCostDictionary();
        if (!ResourceManager.Instance.SpendResources(costs))
        {
            Debug.Log("<color=orange>Za mało surowców!</color>");
            return false;
        }

        PerformBuild(cell, data);
        return true;
    }

    // =========================================================================
    // Prywatne
    // =========================================================================

    private void PerformBuild(HexCell cell, BuildingData data)
    {
        // Usuń dekoracje terenu z heksa
        foreach (Transform child in cell.transform)
            Object.Destroy(child.gameObject);

        // Postaw budynek
        GameObject newObj = Object.Instantiate(data.prefab, cell.transform.position, Quaternion.identity);
        newObj.transform.parent = cell.transform;

        // Inicjalizacja encji
        var entity = newObj.GetComponent<BuildingEntity>() ?? newObj.AddComponent<BuildingEntity>();
        entity.Initialize(data);

        // Konfiguracja kontrolera wieży jeśli potrzeba
        if (data is TowerData towerData)
        {
            var controller = newObj.GetComponent<TowerController>();
            if (controller != null) controller.towerData = towerData;
        }

        NotifyNeighbors(cell);
    }

    private void NotifyNeighbors(HexCell centerCell)
    {
        if (HexMapVisualizer.Instance == null) return;

        var neighbors = HexGridMath.GetNeighbors(centerCell.localCoord);

        foreach (var nCoord in neighbors)
        {
            HexCell neighborHex = HexMapVisualizer.Instance.GetHexCell(centerCell.chunkCoord, nCoord);
            if (neighborHex == null) continue;

            var neighborBuilding = neighborHex.GetComponentInChildren<BuildingEntity>();
            neighborBuilding?.ForceRescan();
        }
    }
}