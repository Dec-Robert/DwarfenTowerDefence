using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reguła wagowego rozkładu cech terenu dla zadanego przedziału pierścieni odległości od bazy.
/// </summary>
[Serializable]
public class RingFeatureRule
{
    public string ruleName = "Ring Rule";

    [Tooltip("Minimalny pierścień odległości od bazy (włącznie)")]
    public int ringMin = 1;

    [Tooltip("Maksymalny pierścień odległości od bazy (włącznie)")]
    public int ringMax = 2;

    [Header("Wagi Cech Terenu")]
    [Tooltip("Waga lasu w losowaniu")]
    public int forestWeight = 30;

    [Tooltip("Waga żyznej gleby w losowaniu")]
    public int fertileSoilWeight = 20;

    [Tooltip("Waga wzgórza w losowaniu")]
    public int hillWeight = 15;

    [Tooltip("Waga góry w losowaniu (0 dla pierścieni 1-2)")]
    public int mountainWeight = 0;

    [Tooltip("Waga zapadliska w losowaniu")]
    public int sinkholeWeight = 5;

    [Tooltip("Waga pustej trawy (brak cechy)")]
    public int emptyWeight = 30;

    [Header("Klastry Lasów")]
    [Tooltip("Czy w tym przedziale pierścieni generować klastry lasu")]
    public bool clusterForests = true;

    [Tooltip("Szansa procentowa na powstanie klastra lasu (0-100)")]
    [Range(0, 100)] public int forestClusterChance = 35;

    [Tooltip("Minimalna liczba prób utworzenia klastra na chunk")]
    public int minForestClusters = 1;

    [Tooltip("Maksymalna liczba prób utworzenia klastra na chunk")]
    public int maxForestClusters = 3;

    [Header("Klastry Gór")]
    [Tooltip("Czy w tym przedziale pierścieni generować klastry gór (wyłączone dla R1-2)")]
    public bool clusterMountains = false;

    [Tooltip("Szansa procentowa na powstanie klastra gór (0-100)")]
    [Range(0, 100)] public int mountainClusterChance = 30;

    [Tooltip("Minimalna liczba prób utworzenia klastra gór")]
    public int minMountainClusters = 1;

    [Tooltip("Maksymalna liczba prób utworzenia klastra gór")]
    public int maxMountainClusters = 2;

    // Kompatybilność z różnymi wariantami nazewnictwa
    public bool enableForestClusters => clusterForests;
    public bool enableMountainClusters => clusterMountains;
    public Vector2Int forestClusterCountRange => new Vector2Int(minForestClusters, maxForestClusters);
    public Vector2Int mountainClusterCountRange => new Vector2Int(minMountainClusters, maxMountainClusters);
    public int weightForest => forestWeight;
    public int weightFertileSoil => fertileSoilWeight;
    public int weightHill => hillWeight;
    public int weightMountain => mountainWeight;
    public int weightSinkhole => sinkholeWeight;
    public int weightNone => emptyWeight;

    public RingFeatureRule() { }

    public RingFeatureRule(
        int min, int max,
        int forest, int fertile, int hill, int mountain, int sinkhole, int empty,
        bool clusterF = true, int clusterFChance = 30, int minFClusters = 1, int maxFClusters = 3,
        bool clusterM = false, int clusterMChance = 30, int minMClusters = 1, int maxMClusters = 2,
        string name = "")
    {
        ringMin = min;
        ringMax = max;
        forestWeight = forest;
        fertileSoilWeight = fertile;
        hillWeight = hill;
        mountainWeight = mountain;
        sinkholeWeight = sinkhole;
        emptyWeight = empty;
        clusterForests = clusterF;
        forestClusterChance = clusterFChance;
        minForestClusters = minFClusters;
        maxForestClusters = maxFClusters;
        clusterMountains = clusterM;
        mountainClusterChance = clusterMChance;
        minMountainClusters = minMClusters;
        maxMountainClusters = maxMClusters;
        ruleName = string.IsNullOrEmpty(name) ? $"Rings {min}-{max}" : name;
    }

    public bool MatchesRing(int ring) => ring >= ringMin && ring <= ringMax;

    public int GetWeight(HexFeatureType feature)
    {
        return feature switch
        {
            HexFeatureType.Forest      => forestWeight,
            HexFeatureType.FertileSoil => fertileSoilWeight,
            HexFeatureType.Hill        => hillWeight,
            HexFeatureType.Mountain    => mountainWeight,
            HexFeatureType.Sinkhole    => sinkholeWeight,
            HexFeatureType.None        => emptyWeight,
            _                          => 0
        };
    }

    public int TotalWeight => Mathf.Max(1,
        forestWeight + fertileSoilWeight + hillWeight + mountainWeight + sinkholeWeight + emptyWeight);
}

/// <summary>
/// Twarda gwarancja budżetowa zasobu/cechy heksa w zadanym przedziale pierścieni.
/// </summary>
[Serializable]
public class ResourceGuarantee
{
    public string guaranteeName = "Resource Guarantee";

    [Tooltip("Minimalny pierścień odległości od bazy (włącznie)")]
    public int ringMin = 1;

    [Tooltip("Maksymalny pierścień odległości od bazy (włącznie)")]
    public int ringMax = 2;

    [Tooltip("Typ gwarantowanej cechy heksa")]
    public HexFeatureType feature = HexFeatureType.Forest;

    [Tooltip("Minimalna łączna liczba heksów z tą cechą w danym przedziale pierścieni")]
    public int minCount = 10;

    [Tooltip("Minimalna liczba różnych chunków, w których cecha musi wystąpić")]
    public int minDistinctChunks = 1;

    [Tooltip("Maksymalna dozwolona liczba heksów (-1 = bez limitu, 0 = całkowity zakaz / zero tolerancji)")]
    public int maxAllowed = -1;

    public ResourceGuarantee() { }

    public ResourceGuarantee(int min, int max, HexFeatureType feat, int minCnt, int minDistinct = 1, int maxAll = -1, string name = "")
    {
        ringMin = min;
        ringMax = max;
        feature = feat;
        minCount = minCnt;
        minDistinctChunks = minDistinct;
        maxAllowed = maxAll;
        guaranteeName = string.IsNullOrEmpty(name) ? $"{feat}_R{min}-{max}" : name;
    }

    public bool MatchesRing(int ring) => ring >= ringMin && ring <= ringMax;
    public bool IsZeroTolerance => maxAllowed == 0;
}

/// <summary>
/// Konfiguracja proceduralnej generacji terenu bez biomów.
/// Zawiera reguły wagowe per pierścień oraz twarde gwarancje zasobów.
/// </summary>
[CreateAssetMenu(fileName = "TerrainGenerationConfig", menuName = "City Builder/Terrain Generation Config")]
public class TerrainGenerationConfig : ScriptableObject
{
    [Header("Reguły Pierścieni (Ring Rules)")]
    public List<RingFeatureRule> ringRules = new List<RingFeatureRule>();

    [Header("Gwarancje Zasobów (Resource Guarantees)")]
    public List<ResourceGuarantee> guarantees = new List<ResourceGuarantee>();

    /// <summary>
    /// Zwraca regułę przypisaną do danego pierścienia odległości od bazy.
    /// Jeśli brak dedykowanej reguły, zwraca pierwszą pasującą lub domyślną.
    /// </summary>
    public RingFeatureRule GetRuleForRing(int ring)
    {
        if (ringRules != null)
        {
            foreach (var rule in ringRules)
            {
                if (rule != null && rule.MatchesRing(ring))
                    return rule;
            }

            if (ringRules.Count > 0 && ringRules[ringRules.Count - 1] != null)
                return ringRules[ringRules.Count - 1];
        }

        return new RingFeatureRule(ring, ring, 25, 20, 15, ring <= 2 ? 0 : 20, 5, 35);
    }

    /// <summary>
    /// Wypełnia instancję domyślnymi regułami i gwarancjami zgodnymi z dokumentem balansu.
    /// </summary>
    public void InitializeDefaults()
    {
        ringRules = new List<RingFeatureRule>
        {
            // Pierścienie 1–2: Strefa przetrwania (wysoki las, żyzna gleba, wzgórza, 0 GÓR)
            new RingFeatureRule(
                min: 1, max: 2,
                forest: 30, fertile: 20, hill: 15, mountain: 0, sinkhole: 5, empty: 30,
                clusterF: true, clusterFChance: 35, minFClusters: 1, maxFClusters: 3,
                clusterM: false, clusterMChance: 0, minMClusters: 0, maxMClusters: 0,
                name: "Rings 1-2 (Early Survival)"
            ),

            // Pierścienie 3–4: Strefa ekspansji i górnictwa (góry pod kopalnie węgla i żelaza)
            new RingFeatureRule(
                min: 3, max: 4,
                forest: 20, fertile: 15, hill: 15, mountain: 25, sinkhole: 5, empty: 20,
                clusterF: true, clusterFChance: 25, minFClusters: 1, maxFClusters: 2,
                clusterM: true, clusterMChance: 30, minMClusters: 1, maxMClusters: 2,
                name: "Rings 3-4 (Expansion & Mining)"
            ),

            // Pierścienie 5+: Dzicz i krawędź mapy (dużo gór, wzgórz, trudny teren)
            new RingFeatureRule(
                min: 5, max: 99,
                forest: 15, fertile: 10, hill: 20, mountain: 30, sinkhole: 10, empty: 15,
                clusterF: false, clusterFChance: 15, minFClusters: 0, maxFClusters: 1,
                clusterM: true, clusterMChance: 30, minMClusters: 1, maxMClusters: 2,
                name: "Rings 5+ (Wilderness Frontier)"
            )
        };

        guarantees = new List<ResourceGuarantee>
        {
            // Gwarancje Pierścieni 1–2
            new ResourceGuarantee(1, 2, HexFeatureType.Forest, minCnt: 10, minDistinct: 1, maxAll: -1, name: "R1-2 Forest >= 10"),
            new ResourceGuarantee(1, 2, HexFeatureType.FertileSoil, minCnt: 6, minDistinct: 1, maxAll: -1, name: "R1-2 FertileSoil >= 6"),
            new ResourceGuarantee(1, 2, HexFeatureType.Hill, minCnt: 6, minDistinct: 1, maxAll: -1, name: "R1-2 Hill >= 6"),
            new ResourceGuarantee(1, 2, HexFeatureType.Mountain, minCnt: 0, minDistinct: 0, maxAll: 0, name: "R1-2 Mountain == 0 (Zero Tolerance)"),

            // Gwarancje Pierścieni 3–4
            new ResourceGuarantee(3, 4, HexFeatureType.Mountain, minCnt: 10, minDistinct: 2, maxAll: -1, name: "R3-4 Mountain >= 10 (across >= 2 chunks)")
        };
    }

    /// <summary>
    /// Fabryka tworząca instancję ScriptableObject ze standardową konfiguracją w pamięci.
    /// Bezpieczna dla środowisk bez uruchomionego silnika Unity (fallback na 'new' w testach CLI).
    /// </summary>
    public static TerrainGenerationConfig CreateDefault()
    {
        TerrainGenerationConfig config = null;
        try
        {
            config = ScriptableObject.CreateInstance<TerrainGenerationConfig>();
        }
        catch
        {
            config = null;
        }

        if (config == null)
        {
            try
            {
                config = (TerrainGenerationConfig)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(TerrainGenerationConfig));
            }
            catch
            {
                config = new TerrainGenerationConfig();
            }
        }

        config.InitializeDefaults();
        return config;
    }
}
