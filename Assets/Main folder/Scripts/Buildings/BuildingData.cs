using UnityEngine;
using System.Collections.Generic;

public enum BuildingType
{
    Economic,
    Defense,
    Special,
    Utility // <--- DODANO BRAKUJ¥C¥ DEFINICJÊ
}

[CreateAssetMenu(fileName = "NewBuilding", menuName = "City Builder/Building Data")]
public class BuildingData : ScriptableObject
{
    [Header("G³ówne Informacje")]
    public string buildingName;
    [TextArea] public string description;
    public Sprite icon;
    public BuildingType type;

    [Header("Wymagania Terenu")]
    public List<HexFeatureType> allowedTerrain;
    public bool requiresOccupiedSpace = false;

    [Header("Ekonomia (Budowa)")]
    public List<ResourceCost> constructionCost;

    // --- DODANO BRAKUJ¥CE POLA PRODUKCJI I UTRZYMANIA ---
    [Header("Ekonomia (Cykl)")]
    public List<ResourceCost> productionPerCycle;
    public List<ResourceCost> upkeepPerCycle;

    [Header("Drzewko Rozwoju")]
    public List<UpgradeTier> upgradeTiers;

    [Header("Prefab")]
    public GameObject prefab;

    [System.Serializable]
    public struct ResourceCost
    {
        public ResourceType type;
        public int amount;
    }

    [System.Serializable]
    public struct UpgradeTier
    {
        public string tierName;
        public List<BuildingUpgradeSO> availableUpgrades;
    }

    public Dictionary<ResourceType, int> GetCostDictionary()
    {
        Dictionary<ResourceType, int> dict = new Dictionary<ResourceType, int>();
        foreach (var cost in constructionCost)
        {
            if (dict.ContainsKey(cost.type)) dict[cost.type] += cost.amount;
            else dict.Add(cost.type, cost.amount);
        }
        return dict;
    }
}