using UnityEngine;
using System.Collections.Generic;

// Typy budynków dla łatwiejszej identyfikacji
public enum BuildingType
{
    Economic,   // Tartak, Kopalnia (Produkcja)
    Defense,    // Wieża (Obrona)
    Utility,    // Domy, Magazyny
    Unique      // Kapitol, Beacon
}

[CreateAssetMenu(fileName = "NewBuilding", menuName = "City Builder/Building Data")]
public class BuildingData : ScriptableObject
{
    [Header("Główne Informacje")]
    public string buildingName;
    [TextArea] public string description;
    public Sprite icon;
    public BuildingType type;

    [Header("Ekonomia Produkcji")]
    [Tooltip("Ile każdy DODATKOWY pracownik zwiększa zużycie/produkcję względem bazy. 0.25 = 25%")]
    public float workerScalingFactor = 0.25f;

    [Header("Wymagania Terenu")]
    public List<HexFeatureType> allowedTerrain;
    public bool requiresOccupiedSpace = false; // Czy wymaga np. Lasu (kt�ry technicznie zajmuje heks)

    [Header("Ekonomia (Koszt i Produkcja Bazowa)")]
    public List<ResourceCost> constructionCost;
    public List<ResourceCost> productionPerCycle; // Co produkuje (np. Wood: 5)
    public List<ResourceCost> upkeepPerCycle;     // Co zużywa (np. Food: 1)

    [Header("System Ulepsze�")]

    public List<BuildingUpgradeSO> tier1Upgrades;

    [Header("Prefab")]
    public GameObject prefab;

    [Header("Zasady Produkcji Terenowej")]
    public TerrainBonusRule bonusRule;

    // Dodaj te pola do klasy BuildingData:

    [Header("Pracownicy (Baza)")]
    public int baseShifts = 1;         // Domy�lnie 1 zmiana
    public int baseWorkersPerShift = 1; // Domy�lnie 1 pracownik
    // Struktura pomocnicza do edytora
    [System.Serializable]
    public struct ResourceCost
    {
        public ResourceType type;
        public float amount;
    }

    // Pomocnicza metoda do konwersji listy na s�ownik (dla ResourceManagera)
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