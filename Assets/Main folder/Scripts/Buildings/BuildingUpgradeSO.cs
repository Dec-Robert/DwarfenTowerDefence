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
    // Np. je�li tartak produkuje 5, a tu wpiszemy Wood: 2, to b�dzie produkowa� 7.
    public List<BuildingData.ResourceCost> productionBonus;
    public List<BuildingData.ResourceCost> upkeepIncrease;

    // Dodaj te pola do klasy BuildingUpgradeSO:
    [Header("Bonusy do Miejsc Pracy")]
    public int extraShifts = 0;          // Np. +1 Zmiana
    public int extraWorkersPerShift = 0; // Np. +1 Pracownik na zmian�

    [Header("Logika Specjalna")]
    // Np. "AUTO_REPLANT" - ID dla skryptu, �eby wiedzia� co robi�
    public string specialEffectID;

    [Header("Drzewko Rozwoju")]
    // Jakie ulepszenia staną si� dost�pne po wykupieniu tego? (To jest ten Tier + 1)
    public List<BuildingUpgradeSO> nextTierOptions;
}