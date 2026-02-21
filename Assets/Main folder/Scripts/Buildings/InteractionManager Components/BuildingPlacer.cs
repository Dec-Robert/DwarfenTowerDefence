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

    /// <summary>Sprawdza czy można postawić budynek na danym heksie (teren + stan chunku).</summary>
    public bool IsPlacementValid(HexCell cell, BuildingData data)
    {
        if (!mapGenerator.worldData.ContainsKey(cell.chunkCoord)) return false;

        HexCellData cellData = mapGenerator.worldData[cell.chunkCoord][cell.localCoord];

        if (cellData.isPath)                                        return false;
        if (!data.allowedTerrain.Contains(cellData.feature))       return false;
        if (cell.GetComponentInChildren<BuildingEntity>() != null) return false;

        // Walidacja stanu chunku
        if (!IsChunkStateValidForBuilding(cell.chunkCoord, data)) return false;

        return true;
    }

    /// <summary>
    /// Próbuje pobrać zasoby i postawić budynek.
    /// Zwraca true jeśli budowa się powiodła.
    /// </summary>
    public bool TryBuild(HexCell cell, BuildingData data)
    {
        if (!IsPlacementValid(cell, data)) return false;

        // ── Wymaganie: wolny elf ───────────────────────────────────────────────
        if (data.requiresFreeElf)
        {
            bool hasFreeElf = CitizenManager.Instance != null &&
                              CitizenManager.Instance.citizens.Exists(
                                  c => c.race == Race.Elves && c.workState == WorkState.Idle);

            if (!hasFreeElf)
            {
                Debug.Log("<color=orange>Brak wolnego elfa! Centrum Ekspedycyjne wymaga przypisanego elfa do budowy.</color>");
                return false;
            }
        }

        // ── Koszt surowców ─────────────────────────────────────────────────────
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
    // Walidacja stanu chunku
    // =========================================================================

    private bool IsChunkStateValidForBuilding(Vector2Int chunkCoord, BuildingData data)
    {
        var expansion = MapExpansionManager.Instance;

        if (expansion == null)
        {
            // Brak managera ekspansji – tryb edytora lub debug, pozwól budować
            return true;
        }

        if (!expansion.activeChunks.TryGetValue(chunkCoord, out var chunkData))
        {
            Debug.Log("<color=orange>Ten teren nie jest jeszcze w zasięgu ekspansji!</color>");
            return false;
        }

        switch (chunkData.state)
        {
            case ChunkState.Locked:
            case ChunkState.Unlocked:
            case ChunkState.Scouting:
                Debug.Log("<color=orange>Ten teren nie jest jeszcze odkryty!</color>");
                return false;

            case ChunkState.MilitaryOnly:
                // Dozwolone tylko wieże (Defense) i posterunek
                if (data.type != BuildingType.Defense && !data.isOutpost)
                {
                    Debug.Log("<color=orange>Na tym terenie można budować tylko wieże obronne i Posterunek!</color>");
                    return false;
                }
                return true;

            case ChunkState.FullyUnlocked:
                return true;

            default:
                return false;
        }
    }

    // =========================================================================
    // Prywatne
    // =========================================================================

    private void PerformBuild(HexCell cell, BuildingData data)
    {
        foreach (Transform child in cell.transform)
            Object.Destroy(child.gameObject);

        GameObject newObj = Object.Instantiate(data.prefab, cell.transform.position, Quaternion.identity);
        newObj.transform.parent = cell.transform;

        var entity = newObj.GetComponent<BuildingEntity>() ?? newObj.AddComponent<BuildingEntity>();
        entity.Initialize(data);

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

        foreach (var nCoord in HexGridMath.GetNeighbors(centerCell.localCoord))
        {
            HexCell neighborHex = HexMapVisualizer.Instance.GetHexCell(centerCell.chunkCoord, nCoord);
            if (neighborHex == null) continue;

            neighborHex.GetComponentInChildren<BuildingEntity>()?.ForceRescan();
        }
    }
}