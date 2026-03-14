using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "NewHouse", menuName = "City Builder/Housing Data")]
public class HousingBuildingData : BuildingData
{
    [Header("Konfiguracja Rasy")] public Race housingRace;

    public int maxResidents = 5;
    public int initialResidents = 2;

    [Header("Formu�a Wzrostu")] public float growthDifficultyMultiplier = 2.0f;

    public int baseGrowthTicks = 1;

    [Header("Produkcja Pasywna (Na mieszka�ca)")]
    public List<ResourceCost> productionPerResident;

    [Header("Koszty Utrzymania (Baza + Per Pop)")]
    public List<ResourceCost> baseDailyUpkeep;

    public List<PerCapitaCost> upkeepPerResident;

    [Header("Koszty Wzrostu (Nadmiarowe)")]
    public List<ResourceCost> growthSurplusCost;

    private void OnValidate()
    {
        type = BuildingType.Housing; // Zmiana z Utility na Housing
    }

    [Serializable]
    public struct PerCapitaCost
    {
        public ResourceType type;
        public float amount;
    }
}

// =========================================================
//            TUTAJ ZACZYNA SIę KOD EDYTORA
// =========================================================
