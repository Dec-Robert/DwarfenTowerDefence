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
    // Np. jeœli tartak produkuje 5, a tu wpiszemy Wood: 2, to bêdzie produkowa³ 7.
    public List<BuildingData.ResourceCost> productionBonus;
    public List<BuildingData.ResourceCost> upkeepIncrease;

    // Dodaj te pola do klasy BuildingUpgradeSO:
    [Header("Bonusy do Miejsc Pracy")]
    public int extraShifts = 0;          // Np. +1 Zmiana
    public int extraWorkersPerShift = 0; // Np. +1 Pracownik na zmianê

    [Header("Logika Specjalna")]
    // Np. "AUTO_REPLANT" - ID dla skryptu, ¿eby wiedzia³ co robiæ
    public string specialEffectID;

    [Header("Drzewko Rozwoju")]
    // Jakie ulepszenia stan¹ siê dostêpne po wykupieniu tego? (To jest ten Tier + 1)
    public List<BuildingUpgradeSO> nextTierOptions;
}