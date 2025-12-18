using UnityEngine;
using System.Collections.Generic;

// Typy budynków dla ³atwiejszej identyfikacji
public enum BuildingType
{
    Economic,   // Tartak, Kopalnia (Produkcja)
    Defense,    // Wie¿a (Obrona)
    Utility,    // Domy, Magazyny
    Unique      // Kapitol, Beacon
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
    // Na czym mo¿na to postawiæ? (np. Forest dla Tartaku)
    public List<HexFeatureType> allowedTerrain;
    public bool requiresOccupiedSpace = false; // Czy wymaga np. Lasu (który technicznie zajmuje heks)

    [Header("Ekonomia (Koszt i Produkcja Bazowa)")]
    public List<ResourceCost> constructionCost;
    public List<ResourceCost> productionPerCycle; // Co produkuje (np. Wood: 5)
    public List<ResourceCost> upkeepPerCycle;     // Co zu¿ywa (np. Food: 1)

    [Header("System Ulepszeñ")]
    // Lista dostêpnych ulepszeñ na start (Tier 1).
    // Kolejne tiery wynikaj¹ z tego, co wybierzesz tutaj.
    public List<BuildingUpgradeSO> tier1Upgrades;

    [Header("Prefab")]
    public GameObject prefab;

    // Struktura pomocnicza do edytora
    [System.Serializable]
    public struct ResourceCost
    {
        public ResourceType type;
        public int amount;
    }

    // Pomocnicza metoda do konwersji listy na s³ownik (dla ResourceManagera)
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