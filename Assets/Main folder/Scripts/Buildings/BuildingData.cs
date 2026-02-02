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

    [Header("Ekonomia Produkcji")]
    [Tooltip("Ile ka¿dy DODATKOWY pracownik zwiêksza zu¿ycie/produkcjê wzglêdem bazy. 0.25 = 25%")]
    public float workerScalingFactor = 0.25f;

    [Header("Wymagania Terenu")]
    public List<HexFeatureType> allowedTerrain;
    public bool requiresOccupiedSpace = false; // Czy wymaga np. Lasu (który technicznie zajmuje heks)

    [Header("Ekonomia (Koszt i Produkcja Bazowa)")]
    public List<ResourceCost> constructionCost;
    public List<ResourceCost> productionPerCycle; // Co produkuje (np. Wood: 5)
    public List<ResourceCost> upkeepPerCycle;     // Co zu¿ywa (np. Food: 1)

    [Header("System Ulepszeñ")]

    public List<BuildingUpgradeSO> tier1Upgrades;

    [Header("Prefab")]
    public GameObject prefab;

    [Header("Zasady Produkcji Terenowej")]
    public TerrainBonusRule bonusRule;

    // Dodaj te pola do klasy BuildingData:

    [Header("Pracownicy (Baza)")]
    public int baseShifts = 1;         // Domyœlnie 1 zmiana
    public int baseWorkersPerShift = 1; // Domyœlnie 1 pracownik
    // Struktura pomocnicza do edytora
    [System.Serializable]
    public struct ResourceCost
    {
        public ResourceType type;
        public float amount;
    }

    // Pomocnicza metoda do konwersji listy na s³ownik (dla ResourceManagera)
    public Dictionary<ResourceType, float> GetCostDictionary()
    {
        Dictionary<ResourceType, float> dict = new Dictionary<ResourceType, float>();
        foreach (var cost in constructionCost)
        {
            if (dict.ContainsKey(cost.type)) dict[cost.type] += cost.amount;
            else dict.Add(cost.type, cost.amount);
        }
        return dict;
    }



}