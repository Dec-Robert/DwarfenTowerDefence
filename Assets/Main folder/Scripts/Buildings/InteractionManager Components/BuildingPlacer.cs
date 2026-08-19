using System;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
///     Odpowiada za walidację miejsca pod budynek, fizyczną budowę
///     i powiadamianie sąsiednich budynków o zmianie terenu.
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
        if (data == null || cell == null) return false;
        if (!mapGenerator.worldData.ContainsKey(cell.chunkCoord)) return false;

        var cellData = mapGenerator.worldData[cell.chunkCoord][cell.localCoord];

        if (cellData.isPath) return false;
        if (!data.allowedTerrain.Contains(cellData.feature)) return false;

        try {var _ = cell.GetComponentInChildren<BuildingEntity>();}
        catch (Exception e) {return false;}
        
        

        // Walidacja stanu chunku
        if (!IsChunkStateValidForBuilding(cell.chunkCoord, data)) return false;

        // ── NOWE: SPECJALNA WALIDACJA DLA WIEŻY RUNICZNEJ ─────────────────────
        if (data.prefab != null && data.prefab.GetComponent<RuneTowerEntity>() != null)
        {
            // 1. Sprawdź limit z Meta-Progresji (Bazowo 0)
            var currentCount = BuildingRegistry.Instance != null
                ? BuildingRegistry.Instance.GetAllOfType<RuneTowerEntity>().Count
                : 0;
            var maxAllowed = MetaUpgradeManager.Instance != null
                ? Mathf.RoundToInt(MetaUpgradeManager.Instance.GetValue(MetaEffectType.RuneTowerLimit))
                : 0;

            if (currentCount >= maxAllowed) return false;

            // 2. Sprawdź, czy sąsiaduje z Kuźnią Runiczną
            var hasForgeNeighbor = false;
            if (HexMapVisualizer.Instance != null)
            {
                var neighbors = HexGridMath.GetNeighbors(cell.localCoord);
                foreach (var nCoord in neighbors)
                {
                    var neighborCell = HexMapVisualizer.Instance.GetHexCell(cell.chunkCoord, nCoord);
                    if (neighborCell != null && neighborCell.GetComponentInChildren<RuneForgeEntity>() != null)
                    {
                        hasForgeNeighbor = true;
                        break;
                    }
                }
            }

            if (!hasForgeNeighbor) return false; // Nie pozwala postawić, jeśli nie ma kuźni obok
        }
        // ───────────────────────────────────────────────────────────────────────

        return true;
    }

    /// <summary>
    ///     Próbuje pobrać zasoby i postawić budynek.
    ///     Zwraca true jeśli budowa się powiodła.
    /// </summary>
    public bool TryBuild(HexCell cell, BuildingData data)
    {
        if (!IsPlacementValid(cell, data))
        {
            if (cell == null || data == null) return false;
            // Opcjonalny feedback dla gracza klikającego "złe" miejsce pod wieżę runiczną
            if (data.prefab != null && data.prefab.GetComponent<RuneTowerEntity>() != null)
            {
                var currentCount = BuildingRegistry.Instance != null
                    ? BuildingRegistry.Instance.GetAllOfType<RuneTowerEntity>().Count
                    : 0;
                var maxAllowed = MetaUpgradeManager.Instance != null
                    ? Mathf.RoundToInt(MetaUpgradeManager.Instance.GetValue(MetaEffectType.RuneTowerLimit))
                    : 0;

                if (currentCount >= maxAllowed)
                    Debug.Log("[Info] <color=orange>Osiągnięto limit Wież Runicznych! Zwiększ go w Meta-Progresji.</color>");
                else
                    Debug.Log("[Info] <color=orange>Wieża Runiczna musi zostać zbudowana tuż obok Kuźni Runicznej!</color>");
            }

            return false;
        }

        // ── Wymaganie: Wolny Obywatel (Elf dla Centrum / Krasnolud dla Kuźni) ──
        if (data.requiresSpecificCitizen)
        {
            var hasFreeCitizen = CitizenManager.Instance != null &&
                                 CitizenManager.Instance.citizens.Exists(c =>
                                     c.race == data.requiredCitizenRace && c.workState == WorkState.Idle);

            if (!hasFreeCitizen)
            {
                Debug.Log(
                    $"<color=orange>Brak wolnego obywatela! Ten budynek wymaga przypisania do niego rasy: {data.requiredCitizenRace}.</color>");
                return false;
            }
        }


        var costs = data.constructionCost;
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
            // Brak managera ekspansji – tryb edytora lub debug, pozwól budować
            return true;

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

        var newObj = Object.Instantiate(data.prefab, cell.transform.position, Quaternion.identity);
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
            var neighborHex = HexMapVisualizer.Instance.GetHexCell(centerCell.chunkCoord, nCoord);
            if (neighborHex == null) continue;

            neighborHex.GetComponentInChildren<BuildingEntity>()?.ForceRescan();
        }
    }
}