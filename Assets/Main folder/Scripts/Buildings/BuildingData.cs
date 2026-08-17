using System;
using System.Collections.Generic;
using UnityEngine;

// Typy budynków dla łatwiejszej identyfikacji
public enum BuildingType
{
    Economic, // Tartak, Kopalnia (Produkcja)
    Defense, // Wieża (Obrona)
    Housing, // Domy (Elfy, Krasnoludy, ludzie)
    Utility, // Domy, Magazyny
    Unique // Kapitol, Beacon   BUDYNKI NIE BUDOWALNE LUB BUDOWALNE BARDZO SPECYFICZNYCH KONDYCJI
}

[CreateAssetMenu(fileName = "NewBuilding", menuName = "City Builder/Building Data")]
public class BuildingData : ScriptableObject
{
    [Header("Główne Informacje")] public string buildingName;

    [TextArea] public string description;
    public Sprite icon;
    public BuildingType type;

    [Header("Ekonomia Produkcji")]
    [Tooltip("Ile każdy DODATKOWY pracownik zwiększa zużycie/produkcję względem bazy. 0.25 = 25%")]
    public float workerScalingFactor = 0.25f;

    [Tooltip("Ile produkcji zapewnia pierwszy pracownik")]
    public float firstWorkerProduction = 0.7f;
    
    public List<float> shiftEffectivenese = new List<float>();

    [Header("Wymagania Terenu")] public List<HexFeatureType> allowedTerrain;

    public bool requiresOccupiedSpace;

    [Header("Wymagania Specjalne")]
    public bool requiresSpecificCitizen;

    public Race requiredCitizenRace = Race.Elves;

    [Tooltip("Czy to jest Posterunek? Posterunek może być budowany na MilitaryOnly (tak jak wieże).")]
    public bool isOutpost;

    [Header("Ekonomia (Koszt i Produkcja Bazowa)")]
    public List<ResourceCost> constructionCost;
    public List<ResourceCost> productionPerCycle;
    public List<ResourceCost> upkeepPerCycle;

    [Header("System Ulepszeń")] 
    public List<BuildingUpgradeSO> tier1Upgrades;

    [Header("Prefab")] 
    public GameObject prefab;

    [Header("Zasady Produkcji Terenowej")] 
    public TerrainBonusRule bonusRule;

    [Header("Pracownicy (Baza)")]
    public int baseShifts = 1;

    public int baseWorkersPerShift = 1;

    public Dictionary<ResourceType, float> GetCostDictionary()
    {
        var dict = new Dictionary<ResourceType, float>();
        foreach (var cost in constructionCost)
            if (dict.ContainsKey(cost.type)) dict[cost.type] += cost.amount;
            else dict.Add(cost.type, cost.amount);
        return dict;
    }

    [Serializable]
    public struct ResourceCost
    {
        public ResourceType type;
        public float amount;
    }

    [Header("Is building unlocked to be build")]
    public bool isUnlocked = true;
}