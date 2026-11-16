using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WorkshopTierEntry
{
    [Range(1, 4)]
    public int tier = 1;
    public BuildingUpgradeSO optionA;
    public BuildingUpgradeSO optionB;

    public BuildingUpgradeSO GetOption(bool isOptionA)
    {
        return isOptionA ? optionA : optionB;
    }
}

[Serializable]
public class WorkshopBuildingTree
{
    public BuildingData targetBuilding;
    public List<WorkshopTierEntry> tiers = new List<WorkshopTierEntry>();

    public WorkshopTierEntry GetTier(int tier)
    {
        if (tiers == null) return null;
        for (int i = 0; i < tiers.Count; i++)
        {
            if (tiers[i] != null && tiers[i].tier == tier)
                return tiers[i];
        }
        return null;
    }
}

[CreateAssetMenu(fileName = "WorkshopTechTreeCatalog", menuName = "Game/Buildings/Workshop Tech Tree Catalog")]
public class WorkshopTechTreeCatalogSO : ScriptableObject
{
    public List<WorkshopBuildingTree> buildingTrees = new List<WorkshopBuildingTree>();

    public bool TryGetTree(BuildingData building, out WorkshopBuildingTree tree)
    {
        tree = null;
        if (building == null || buildingTrees == null) return false;

        for (int i = 0; i < buildingTrees.Count; i++)
        {
            var candidate = buildingTrees[i];
            if (candidate == null || candidate.targetBuilding == null) continue;
            if (candidate.targetBuilding == building ||
                string.Equals(candidate.targetBuilding.name, building.name, StringComparison.OrdinalIgnoreCase))
            {
                tree = candidate;
                return true;
            }
        }

        return false;
    }
}
