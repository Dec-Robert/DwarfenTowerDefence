using System;
using UnityEngine;
using Object = UnityEngine.Object;

public class BuildingPlacer
{
    private readonly HexMapGenerator mapGenerator;

    public BuildingPlacer(HexMapGenerator mapGenerator)
    {
        this.mapGenerator = mapGenerator;
    }

    public bool IsPlacementValid(HexCell cell, BuildingData data)
    {
        if (data == null || cell == null) return false;
        if (mapGenerator == null || mapGenerator.worldData == null || !mapGenerator.worldData.ContainsKey(cell.chunkCoord)) return false;

        var chunkDataMap = mapGenerator.worldData[cell.chunkCoord];
        if (!chunkDataMap.TryGetValue(cell.localCoord, out var cellData)) return false;

        if (cell.IsRoad && !data.allowOnRoad) return false;
        if (!data.elevationRequirement.IsValid(cell.Elevation)) return false;

        bool isMine = data.type == BuildingType.Economic && (
            (data.allowedTerrain != null && data.allowedTerrain.Count == 1 && data.allowedTerrain.Contains(HexFeatureType.Mountain)) ||
            (data.buildingName != null && data.buildingName.IndexOf("Mine", StringComparison.OrdinalIgnoreCase) >= 0)
        );

        if (data.type == BuildingType.Economic)
        {
            if (isMine)
            {
                if (cell.Feature != HexFeatureType.Mountain) return false;
            }
            else
            {
                if (cell.Feature == HexFeatureType.Mountain) return false;
            }
        }
        else
        {
            bool isAllowed = data.allowedTerrain == null || data.allowedTerrain.Count == 0 || data.allowedTerrain.Contains(cell.Feature);
            if (!isAllowed)
            {
                // Wzgórza (Hill) i Zapadliska (Sinkhole) to poziomy wysokości (+ i -) na podłożu trawiastym (None)
                if ((cell.Feature == HexFeatureType.Hill || cell.Feature == HexFeatureType.Sinkhole) && data.allowedTerrain.Contains(HexFeatureType.None))
                {
                    isAllowed = true;
                }
            }

            if (!isAllowed) return false;
        }

        if (cell.HasBuilding() || (cellData != null && cellData.occupyingBuilding != null)) return false;

        bool isOutpost = data.isOutpost || (data.prefab != null && data.prefab.GetComponent<OutpostEntity>() != null);
        if (isOutpost)
        {
            if (cell.OccupyingBuilding != null || cell.GetComponentInChildren<OutpostEntity>() != null)
                return false;

            if (HasOutpostOnChunk(cell.chunkCoord))
                return false;
        }

        if (!IsChunkStateValidForBuilding(cell.chunkCoord, data)) return false;

        if (data.prefab != null && data.prefab.GetComponent<RuneTowerEntity>() != null)
        {
            int currentCount = BuildingRegistry.Instance != null
                ? BuildingRegistry.Instance.GetAllOfType<RuneTowerEntity>().Count
                : 0;
            int maxAllowed = MetaUpgradeManager.Instance != null
                ? Mathf.RoundToInt(MetaUpgradeManager.Instance.GetValue(MetaEffectType.RuneTowerLimit))
                : 0;

            if (currentCount >= maxAllowed) return false;

            bool hasForgeNeighbor = false;
            if (HexMapVisualizer.Instance != null)
            {
                int chunkRadius = mapGenerator != null ? mapGenerator.chunkRadius : 3;
                var neighbors = HexGridMath.GetNeighborsCrossChunk(cell.chunkCoord, cell.localCoord, chunkRadius);
                foreach (var nInfo in neighbors)
                {
                    var neighborCell = HexMapVisualizer.Instance.GetHexCell(nInfo.chunkCoord, nInfo.localCoord);
                    if (neighborCell != null && neighborCell.GetComponentInChildren<RuneForgeEntity>() != null)
                    {
                        hasForgeNeighbor = true;
                        break;
                    }
                }
            }

            if (!hasForgeNeighbor) return false;
        }

        return true;
    }

    public bool TryBuild(HexCell cell, BuildingData data)
    {
        if (!IsPlacementValid(cell, data))
        {
            if (cell == null || data == null) return false;

            bool isOutpost = data.isOutpost || (data.prefab != null && data.prefab.GetComponent<OutpostEntity>() != null);
            if (isOutpost)
            {
                if (cell.HasBuilding() || cell.OccupyingBuilding != null || cell.GetComponentInChildren<OutpostEntity>() != null)
                {
                    Debug.Log("<color=orange>Ten heks jest już zajęty!</color>");
                    return false;
                }

                var expansion = MapExpansionManager.Instance;
                var chunkData = expansion != null ? expansion.GetChunkData(cell.chunkCoord) : null;
                if (chunkData != null)
                {
                    if (chunkData.baseState == ChunkState.Settled || chunkData.EffectiveState == ChunkState.Settled)
                    {
                        Debug.Log("<color=orange>Nie można postawić Posterunku na terenie już przejętym!</color>");
                        return false;
                    }

                    if (chunkData.activeOutpost != null || HasOutpostOnChunk(cell.chunkCoord))
                    {
                        Debug.Log("<color=orange>Na tym chunku stoi już Posterunek!</color>");
                        return false;
                    }

                    if (chunkData.baseState != ChunkState.Outskirts)
                    {
                        Debug.Log("<color=orange>Posterunek można stawiać tylko na świeżo zbadanym Pograniczu (Outskirts)!</color>");
                        return false;
                    }
                }
            }

            if (data.prefab != null && data.prefab.GetComponent<RuneTowerEntity>() != null)
            {
                int currentCount = BuildingRegistry.Instance != null
                    ? BuildingRegistry.Instance.GetAllOfType<RuneTowerEntity>().Count
                    : 0;
                int maxAllowed = MetaUpgradeManager.Instance != null
                    ? Mathf.RoundToInt(MetaUpgradeManager.Instance.GetValue(MetaEffectType.RuneTowerLimit))
                    : 0;

                if (currentCount >= maxAllowed)
                    Debug.Log("[Info] <color=orange>Osiągnięto limit Wież Runicznych! Zwiększ go w Meta-Progresji.</color>");
                else
                    Debug.Log("[Info] <color=orange>Wieża Runiczna musi zostać zbudowana tuż obok Kuźni Runicznej!</color>");
            }

            return false;
        }

        if (data.requiresSpecificCitizen)
        {
            bool hasFreeCitizen = WorkforceManager.Instance != null &&
                                 WorkforceManager.Instance.HasIdleWorker(data.requiredCitizenRace);

            if (!hasFreeCitizen)
            {
                Debug.Log(
                    $"<color=orange>Brak wolnego obywatela! Ten budynek wymaga przypisania do niego rasy: {data.requiredCitizenRace}.</color>");
                return false;
            }
        }

        var costs = data.constructionCost;
        if (costs != null && ResourceManager.Instance != null && !ResourceManager.Instance.SpendResources(costs))
        {
            Debug.Log("<color=orange>Za mało surowców!</color>");
            return false;
        }

        PerformBuild(cell, data);
        return true;
    }

    public bool HasOutpostOnChunk(Vector2Int chunkCoord)
    {
        var expansion = MapExpansionManager.Instance;
        if (expansion != null)
        {
            var chunkData = expansion.GetChunkData(chunkCoord);
            if (chunkData != null && chunkData.activeOutpost != null)
                return true;
        }

        if (BuildingRegistry.Instance != null)
        {
            var outposts = BuildingRegistry.Instance.GetAllOfType<OutpostEntity>();
            for (int i = 0; i < outposts.Count; i++)
            {
                var outpost = outposts[i];
                if (outpost == null) continue;

                if (outpost.ChunkCoord == chunkCoord)
                    return true;

                var hexCell = outpost.GetComponentInParent<HexCell>();
                if (hexCell != null && hexCell.chunkCoord == chunkCoord)
                    return true;
            }
        }

        return false;
    }

    private bool IsChunkStateValidForBuilding(Vector2Int chunkCoord, BuildingData data)
    {
        var expansion = MapExpansionManager.Instance;
        if (expansion == null) return true;

        var chunkStateData = expansion.GetChunkData(chunkCoord);
        if (chunkStateData == null)
        {
            Debug.Log("<color=orange>Ten teren nie jest jeszcze w zasięgu ekspansji!</color>");
            return false;
        }

        bool isOutpost = data.isOutpost || (data.prefab != null && data.prefab.GetComponent<OutpostEntity>() != null);
        if (isOutpost)
        {
            // Posterunek można stawiać WYŁĄCZNIE na Pograniczu (Outskirts).
            // Blokada 1: Teren jest już przejęty (Settled)
            if (chunkStateData.baseState == ChunkState.Settled || chunkStateData.EffectiveState == ChunkState.Settled)
            {
                return false;
            }

            // Blokada 2: Na danym chunku stoi już posterunek
            if (chunkStateData.activeOutpost != null || HasOutpostOnChunk(chunkCoord))
            {
                return false;
            }

            // Blokada 3: Teren nie jest Pograniczem (np. Dzicz / Borderlands / Surveying)
            if (chunkStateData.baseState != ChunkState.Outskirts)
            {
                return false;
            }

            return true;
        }

        if (data.type == BuildingType.Defense)
        {
            if (!chunkStateData.CanBuildDefense)
            {
                Debug.Log("<color=orange>Ten teren nie został jeszcze odkryty zwiadem!</color>");
                return false;
            }
            return true;
        }

        if (!chunkStateData.CanBuildEconomic)
        {
            Debug.Log("<color=orange>Ten teren wymaga pełnej kontroli (Settled) lub aktywnego Posterunku, aby stawiać budynki cywilne!</color>");
            return false;
        }

        return true;
    }

    private void PerformBuild(HexCell cell, BuildingData data)
    {
        var newObj = Object.Instantiate(data.prefab, cell.transform.position, Quaternion.identity);
        newObj.transform.parent = cell.transform;

        var entity = newObj.GetComponent<BuildingEntity>() ?? newObj.AddComponent<BuildingEntity>();
        entity.Initialize(data);

        cell.SetBuilding(entity);

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
        int chunkRadius = mapGenerator != null ? mapGenerator.chunkRadius : 3;

        foreach (var nInfo in HexGridMath.GetNeighborsCrossChunk(centerCell.chunkCoord, centerCell.localCoord, chunkRadius))
        {
            var neighborHex = HexMapVisualizer.Instance.GetHexCell(nInfo.chunkCoord, nInfo.localCoord);
            if (neighborHex == null) continue;

            neighborHex.GetComponentInChildren<BuildingEntity>()?.ForceRescan();
        }
    }
}