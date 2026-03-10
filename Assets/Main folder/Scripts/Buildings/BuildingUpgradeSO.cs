using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewUpgrade", menuName = "City Builder/Building Upgrade")]
public class BuildingUpgradeSO : ScriptableObject
{
    [Header("Informacje")]
    public string upgradeName;
    public Sprite icon;
    [TextArea] public string description;

    [Header("Koszt")]
    public List<BuildingData.ResourceCost> cost;

    [Header("Efekty Statystyczne (Addytywne)")]
    // Np. jeśli tartak produkuje 5, a tu wpiszemy Wood: 2, to będzie produkować 7.
    public List<BuildingData.ResourceCost> productionBonus;
    public List<BuildingData.ResourceCost> upkeepIncrease;
    
    [Tooltip("Globalny mnożnik dodawany do CAŁEJ produkcji budynku (baza + teren). Np. 0.2 to +20% ogólnej wydajności.")]
    public float globalProductionMultiplierBonus = 0f;
    [Tooltip("Globalny mnożnik dodawany do CAŁYCH kosztów utrzymania. Np. 0.5 to +50% kosztów węgla i złota.")]
    public float globalUpkeepMultiplierBonus = 0f;
    
    [Header("Modyfikatory Terenu")]
    [Tooltip("Płaski bonus DODAWANY do wartości każdego sąsiadującego heksa (np. +1.5 drewna za las)")]
    public float terrainFlatBonus = 0f;

    [Tooltip("Mnożnik PROCENTOWY zysku z terenu (np. 0.15 = +15% mocy, -0.25 = -25% mocy)")]
    public float terrainPercentBonus = 0f;

    // Dodaj te pola do klasy BuildingUpgradeSO:
    [Header("Bonusy do Miejsc Pracy")]
    public int extraShifts = 0;          // Np. +1 Zmiana
    public int extraWorkersPerShift = 0; // Np. +1 Pracownik na zmian�

    [Header("Logika Specjalna")]
    // Np. "AUTO_REPLANT" - ID dla skryptu, �eby wiedzia� co robi�
    public List<string> specialEffectIDs = new List<string>();


    [Header("Drzewko Rozwoju")]
    // Jakie ulepszenia staną si� dost�pne po wykupieniu tego? (To jest ten Tier + 1)
    public List<BuildingUpgradeSO> nextTierOptions;
}