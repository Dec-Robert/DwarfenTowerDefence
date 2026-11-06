using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewHouse", menuName = "Game/Buildings/Housing Building Data")]
public class HousingBuildingData : BuildingData
{
    [Header("Konfiguracja Rasy")] public Race housingRace;

    public int maxResidents = 5;
    public int initialResidents = 2;

    [Header("Cykle Wzrostu Populacji")]
    [Tooltip("Liczba cykli/dni potrzebnych na pojawienie się kolejnego mieszkańca w zależności od obecnej populacji (indeks 0 = dla 0 mieszkańców, indeks 1 = dla 1 itd.).")]
    public int[] growthCycles = new int[] { 2, 3, 4, 5, 5 };

    public int GetGrowthCyclesRequired(int currentResidents)
    {
        if (growthCycles == null || growthCycles.Length == 0) return 1;
        if (currentResidents < 0) return Mathf.Max(1, growthCycles[0]);
        if (currentResidents >= growthCycles.Length) return Mathf.Max(1, growthCycles[growthCycles.Length - 1]);
        return Mathf.Max(1, growthCycles[currentResidents]);
    }

    [Header("Produkcja Pasywna (Na mieszkańca)")]
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
