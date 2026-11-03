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

        return new RingFeatureRule(ring, ring, 20, 15, 15, 0, 5, 45);
    }

    /// <summary>
    /// Wypełnia instancję domyślnymi regułami i gwarancjami zgodnymi ze zbalansowaną krzywą wolnej przestrzeni.
    /// </summary>
    public void InitializeDefaults()
    {
        ringRules = new List<RingFeatureRule>
        {
            // Pierścień 1: Strefa startowa przetrwania (wysoka gęstość lasu i gleby, 0 GÓR, ~20% trawy)
            new RingFeatureRule(
                min: 1, max: 1,
                forest: 35, fertile: 25, hill: 15, mountain: 0, sinkhole: 5, empty: 20,
                clusterF: true, clusterFChance: 35, minFClusters: 1, maxFClusters: 2,
                clusterM: false, clusterMChance: 0, minMClusters: 0, maxMClusters: 0,
                name: "Ring 1 (Starting Core)"
            ),

            // Pierścień 2: Rozszerzenie startowe (~35% trawy, brak klastrów gór, zwiastuny gór ze wzgórz max level)
            new RingFeatureRule(
                min: 2, max: 2,
                forest: 25, fertile: 20, hill: 15, mountain: 0, sinkhole: 5, empty: 35,
                clusterF: true, clusterFChance: 30, minFClusters: 1, maxFClusters: 2,
                clusterM: false, clusterMChance: 0, minMClusters: 0, maxMClusters: 0,
                name: "Ring 2 (Settlement Outskirts)"
            ),

            // Pierścień 3: Początek doliny wolnej przestrzeni (~50% trawy, pierwsze klastry gór 2-6)
            new RingFeatureRule(
                min: 3, max: 3,
                forest: 15, fertile: 15, hill: 15, mountain: 0, sinkhole: 5, empty: 50,
                clusterF: true, clusterFChance: 20, minFClusters: 1, maxFClusters: 2,
                clusterM: true, clusterMChance: 35, minMClusters: 1, maxMClusters: 1,
                name: "Ring 3 (Expansion Valley Entry)"
            ),

            // Pierścień 4: Szczyt polany / doliny (~65% trawy, rzadsze skupiska surowców pod kopalnie i tartaki)
            new RingFeatureRule(
                min: 4, max: 4,
                forest: 10, fertile: 10, hill: 10, mountain: 0, sinkhole: 5, empty: 65,
                clusterF: true, clusterFChance: 15, minFClusters: 1, maxFClusters: 1,
                clusterM: true, clusterMChance: 35, minMClusters: 1, maxMClusters: 1,
                name: "Ring 4 (Great Glade Peak)"
            ),

            // Pierścień 5: Wyjście z doliny w stronę dziczy (~50% trawy)
            new RingFeatureRule(
                min: 5, max: 5,
                forest: 15, fertile: 15, hill: 15, mountain: 0, sinkhole: 5, empty: 50,
                clusterF: true, clusterFChance: 20, minFClusters: 1, maxFClusters: 2,
                clusterM: true, clusterMChance: 35, minMClusters: 1, maxMClusters: 1,
                name: "Ring 5 (Frontier Valley Exit)"
            ),

            // Pierścień 6: Kres cywilizacji (~35% trawy, gęstsze wzgórza i lasy)
            new RingFeatureRule(
                min: 6, max: 6,
                forest: 20, fertile: 15, hill: 20, mountain: 0, sinkhole: 10, empty: 35,
                clusterF: true, clusterFChance: 25, minFClusters: 1, maxFClusters: 2,
                clusterM: true, clusterMChance: 40, minMClusters: 1, maxMClusters: 1,
                name: "Ring 6 (Wilderness Edge)"
            ),

            // Pierścień 7+: Głęboka dzicz (wysoka gęstość surowców, wzgórz i zapadlisk, ~20% trawy)
            new RingFeatureRule(
                min: 7, max: 99,
                forest: 20, fertile: 15, hill: 25, mountain: 0, sinkhole: 15, empty: 20,
                clusterF: true, clusterFChance: 30, minFClusters: 1, maxFClusters: 2,
                clusterM: true, clusterMChance: 45, minMClusters: 1, maxMClusters: 1,
                name: "Rings 7+ (Deep Wilderness)"
            )
        };

        guarantees = new List<ResourceGuarantee>
        {
            // Gwarancje Pierścienia 1 (Ścisły zakaz gór w samym rdzeniu bazy)
            new ResourceGuarantee(1, 1, HexFeatureType.Mountain, minCnt: 0, minDistinct: 0, maxAll: 0, name: "R1 Mountain == 0 (Zero Tolerance)"),

            // Gwarancje Pierścieni 1–2 (Zasoby startowe przetrwania)
            new ResourceGuarantee(1, 2, HexFeatureType.Forest, minCnt: 10, minDistinct: 1, maxAll: -1, name: "R1-2 Forest >= 10"),
            new ResourceGuarantee(1, 2, HexFeatureType.FertileSoil, minCnt: 6, minDistinct: 1, maxAll: -1, name: "R1-2 FertileSoil >= 6"),
            new ResourceGuarantee(1, 2, HexFeatureType.Hill, minCnt: 6, minDistinct: 1, maxAll: -1, name: "R1-2 Hill >= 6"),

            // Gwarancje Pierścieni 3–4 (Pasma górnicze w co najmniej 2 różnych chunkach)
            new ResourceGuarantee(3, 4, HexFeatureType.Mountain, minCnt: 10, minDistinct: 2, maxAll: -1, name: "R3-4 Mountain >= 10 (across >= 2 chunks)")
        };
    }

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
