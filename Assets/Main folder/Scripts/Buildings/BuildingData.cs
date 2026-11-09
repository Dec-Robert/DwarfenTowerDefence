using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Typy budynków dla łatwiejszej identyfikacji
public enum BuildingType
{
    Economic, // Tartak, Kopalnia (Produkcja)
    Defense, // Wieża (Obrona)
    Housing, // Domy (Elfy, Krasnoludy, ludzie)
    Utility, // Domy, Magazyny
    Unique // Kapitol, Beacon   BUDYNKI NIE BUDOWALNE LUB BUDOWALNE BARDZO SPECYFICZNYCH KONDYCJI
}

public enum ElevationFilterMode
{
    Any = 0,            // Dowolna wysokość (domyślnie, bezpieczne dla obecnych budynków)
    OnlyFlat = 1,       // Tylko płasko (elevation == 0)
    OnlyHills = 2,      // Tylko wzgórza (elevation > 0)
    OnlyCavities = 3,   // Tylko zapadliska (elevation < 0)
    CustomRange = 4,    // W przedziale od minElevation do maxElevation
    SpecificValues = 5  // Tylko wybrane konkretne wartości z listy allowedElevations
}

[System.Serializable]
public struct ElevationRequirement
{
    public ElevationFilterMode mode;
    public int minElevation;
    public int maxElevation;
    public List<int> allowedElevations;

    public bool IsValid(int elevation)
    {
        switch (mode)
        {
            case ElevationFilterMode.Any:
                return true;
            case ElevationFilterMode.OnlyFlat:
                return elevation == 0;
            case ElevationFilterMode.OnlyHills:
                return elevation > 0;
            case ElevationFilterMode.OnlyCavities:
                return elevation < 0;
            case ElevationFilterMode.CustomRange:
                return elevation >= minElevation && elevation <= maxElevation;
            case ElevationFilterMode.SpecificValues:
                return allowedElevations != null && allowedElevations.Contains(elevation);
            default:
                return true;
        }
    }
}

[CreateAssetMenu(fileName = "NewBuilding", menuName = "Game/Buildings/Building Data")]
public class BuildingData : ScriptableObject
{
    private static readonly float[] DefaultTowerEfficiency = { 0.2f, 1.0f, 1.1f, 1.2f, 1.3f, 1.35f, 1.4f };

    [Header("Główne Informacje")] public string buildingName;

    [TextArea] public string description;
    public Sprite icon;
    public BuildingType type;

    public string DisplayName => LocalizationManager.Get($"BUILDING_{GetLocalizationId()}_NAME", buildingName);
    public string DisplayDescription => LocalizationManager.Get($"BUILDING_{GetLocalizationId()}_DESC", description);

    public string GetLocalizationId()
    {
        string raw = !string.IsNullOrEmpty(buildingName) ? buildingName : name;
        var sb = new System.Text.StringBuilder();
        foreach (char c in raw)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
            else if (c == ' ' || c == '_' || c == '-') sb.Append('_');
        }
        string clean = sb.ToString();
        while (clean.Contains("__")) clean = clean.Replace("__", "_");
        return clean.Trim('_');
    }

    [Header("Ekonomia Produkcji")]
    [HideInInspector]
    [Tooltip("Ile każdy DODATKOWY pracownik zwiększa zużycie/produkcję względem bazy. 0.25 = 25%")]
    public float workerScalingFactor = 0.25f;

    [HideInInspector]
    [Tooltip("Ile produkcji zapewnia pierwszy pracownik")]
    public float firstWorkerProduction = 0.7f;

    [FormerlySerializedAs("shiftEffectivenese")]
    [Tooltip("Tabela wydajności względem liczby pracowników (indeks 0 = 0 pracowników, 1 = 1 pracownik itd.). 1.0 = 100%.")]
    public List<float> efficiencyTable = new List<float>();

    [Header("Wymagania Terenu")] public List<HexFeatureType> allowedTerrain;

    [Header("Wymagania Wysokości (Elevation)")]
    public ElevationRequirement elevationRequirement = new ElevationRequirement { mode = ElevationFilterMode.Any };

    [Header("Wymagania Dróg")]
    public bool allowOnRoad = false;

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
    public List<TerrainBonusRule> bonusRules = new List<TerrainBonusRule>();

    /// <summary>
    /// Zwraca listę reguł bonusów terenowych dla danego budynku.
    /// </summary>
    public List<TerrainBonusRule> GetEffectiveBonusRules()
    {
        return bonusRules ?? new List<TerrainBonusRule>();
    }

    [Header("Zasady Bonusu Wysokosciowego")]
    public ElevationBonusRule elevationBonus;

    /// <summary>
    /// Sprawdza, czy budynek posiada jakiekolwiek bonusy terenowe lub wysokosciowe.
    /// </summary>
    public bool HasAnyTerrainBonuses()
    {
        bool hasRules = bonusRules != null && bonusRules.Count > 0;
        return hasRules || elevationBonus.enabled;
    }

    [Header("Pracownicy (Baza)")] public int baseMaxWorkers = 1;

    [Header("Is building unlocked to be build")]
    public bool isUnlocked = true;

    /// <summary>
    /// Inicjalizuje domyślną tabelę wydajności, jeśli lista jest pusta.
    /// </summary>
    public void EnsureDefaultEfficiencyTable()
    {
        if (type == BuildingType.Housing) return;
        if (efficiencyTable != null && efficiencyTable.Count > 0) return;

        if (efficiencyTable == null)
        {
            efficiencyTable = new List<float>();
        }

        if (this is TowerData || type == BuildingType.Defense)
        {
            efficiencyTable.AddRange(DefaultTowerEfficiency);
        }
        else
        {
            int defaultSlots = Mathf.Max(baseMaxWorkers + 3, 5);
            efficiencyTable.Add(0f); // 0 pracowników
            for (int i = 1; i <= defaultSlots; i++)
            {
                float val = 1f + (i - 1) * workerScalingFactor;
                efficiencyTable.Add(Mathf.Round(val * 100f) / 100f);
            }
        }
    }

    /// <summary>
    /// Zwraca bazowy mnożnik wydajności z tabeli dla podanej liczby pracowników (bez bonusów Meta).
    /// </summary>
    public float GetBaseEfficiencyForWorkers(int workerCount)
    {
        EnsureDefaultEfficiencyTable();

        if (efficiencyTable == null || efficiencyTable.Count == 0)
        {
            if (workerCount <= 0) return 0f;
            return 1f + (workerCount - 1) * workerScalingFactor;
        }

        if (workerCount < 0) return 0f;

        if (workerCount < efficiencyTable.Count)
        {
            return efficiencyTable[workerCount];
        }

        // Ekstrapolacja dynamiczna dla pracowników powyżej rozmiaru tabeli
        float lastVal = efficiencyTable[efficiencyTable.Count - 1];
        int extraWorkers = workerCount - (efficiencyTable.Count - 1);
        float scaling = workerScalingFactor > 0f ? workerScalingFactor : 0.15f;
        return lastVal + (extraWorkers * scaling);
    }

    /// <summary>
    /// Zwraca finalny mnożnik wydajności załogi uwzględniający tabelę budynku oraz bonusy z MetaUpgradeManager.
    /// </summary>
    public float GetEffectiveEfficiencyForWorkers(int workerCount)
    {
        float baseEff = GetBaseEfficiencyForWorkers(workerCount);

        if (!ReferenceEquals(MetaUpgradeManager.Instance, null))
        {
            if (workerCount <= 0)
            {
                baseEff += MetaUpgradeManager.Instance.GetValue(MetaEffectType.BuildingPassiveEfficiency);
            }
            else
            {
                baseEff += workerCount * MetaUpgradeManager.Instance.GetValue(MetaEffectType.BuildingWorkerEfficiencyBonus);
            }
        }

        return Mathf.Max(0f, baseEff);
    }

    /// <summary>
    /// Zwraca tablicę efektywnej wydajności (od 0 do wymaganej liczby segmentów) na potrzeby UI oraz logiki.
    /// </summary>
    public float[] GetEffectiveEfficiencyTable(int minLength = 0)
    {
        EnsureDefaultEfficiencyTable();

        int tableCount = efficiencyTable != null ? efficiencyTable.Count : 0;
        int length = Mathf.Max(tableCount, minLength, baseMaxWorkers + 1);
        float[] result = new float[length];

        for (int i = 0; i < length; i++)
        {
            result[i] = GetEffectiveEfficiencyForWorkers(i);
        }

        return result;
    }
}