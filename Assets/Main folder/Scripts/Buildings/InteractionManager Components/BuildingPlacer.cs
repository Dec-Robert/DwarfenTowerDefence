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

        if (cellData.isPath) return false;
        if (data.allowedTerrain != null && !data.allowedTerrain.Contains(cellData.feature)) return false;

        if (cell.GetComponentInChildren<BuildingEntity>() != null) return false;

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

            if (!hasForgeNeighbor) return false;
        }

        return true;
    }

    public bool TryBuild(HexCell cell, BuildingData data)
    {
        if (!IsPlacementValid(cell, data))
        {
            if (cell == null || data == null) return false;

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
            bool hasFreeCitizen = CitizenManager.Instance != null &&
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
        if (costs != null && ResourceManager.Instance != null && !ResourceManager.Instance.SpendResources(costs))
        {
            Debug.Log("<color=orange>Za mało surowców!</color>");
            return false;
        }

        PerformBuild(cell, data);
        return true;
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

        if (data.type == BuildingType.Defense || data.isOutpost)
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