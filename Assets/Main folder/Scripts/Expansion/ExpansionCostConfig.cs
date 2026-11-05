using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ExpansionCostConfig", menuName = "Game/Economy/Expansion Cost Config")]
public class ExpansionCostConfig : ScriptableObject
{
    [Header("Czas Misji")]
    public int minDays = 1;
    public int maxDays = 7;
    public int maxDistanceForTime = 6;

    [Header("Koszty Bazowe")]
    [FormerlySerializedAs("baseGoldCost")]
    public float baseEtherCost = 30f;
    public float baseFoodCost = 10f;
    [FormerlySerializedAs("goldPerHex")]
    public float etherPerHex = 15f;
    public float foodPerHex = 5f;

    [Header("Modyfikatory Drogi")]
    [Range(0f, 1f)] public float roadCostMultiplier = 0.6f;
    [Range(0f, 1f)] public float roadTimeMultiplier = 0.7f;

    [Header("Skalowanie Elfami (Pełna Ekspedycja)")]
    public int minElvesForSurvey = 1;
    public int daysReductionPerExtraElf = 1;
    public int minSurveyDays = 1;

    [Header("Skalowanie Odkrytymi Terenami")]
    [Tooltip("Mnożnik kosztu za każdy odkryty chunk ponad stan początkowy (np. 0.15 = +15% per chunk) dla pełnej ekspedycji (Survey).")]
    public float costMultiplierPerDiscoveredChunk = 0.15f;

    [Tooltip("Liczba odkrytych chunków ponad stan początkowy, co którą bazowy czas wyprawy wzrasta o 1 dzień (np. 4 = co 4 zbadane chunki +1 dzień). 0 = wyłączone.")]
    public int chunksPerExtraSurveyDay = 4;

    [Header("Krótka Ekspedycja (Recon / Zwiad)")]
    public int reconDays = 1;
    public int reconElfRequirement = 1;
    [FormerlySerializedAs("reconGoldCost")]
    public float reconEtherCost = 0f;
    public float reconFoodCost = 50f;
    public float reconWoodCost = 20f;

    [Header("Przedziały Raportu Zwiadu")]
    [Tooltip("Maksymalna liczba heksów dla poziomu 'Znikome' (np. 1-2).")]
    public int scarceMaxCount = 2;
    [Tooltip("Maksymalna liczba heksów dla poziomu 'Umiarkowane' (np. 3-5). Powyżej jest 'Obfite'.")]
    public int moderateMaxCount = 5;
}

public static class ExpansionCostCalculator
{
    private const int FallbackDistance = 6;

    public static MissionCost Calculate(
        Vector2Int targetChunk,
        Dictionary<Vector2Int, ChunkStateData> activeChunks,
        bool isRoadChunk,
        ExpansionCostConfig cfg)
    {
        int minElves = (object)cfg != null ? cfg.minElvesForSurvey : 2;
        return CalculateSurveyCost(targetChunk, activeChunks, isRoadChunk, cfg, minElves);
    }

    public static MissionCost CalculateSurveyCost(
        Vector2Int targetChunk,
        Dictionary<Vector2Int, ChunkStateData> activeChunks,
        bool isRoadChunk,
        ExpansionCostConfig cfg,
        int elfCount,
        int extraDiscoveredChunks = -1)
    {
        if ((object)cfg == null)
        {
            return new MissionCost
            {
                etherCost = 0,
                foodCost = 10,
                woodCost = 5,
                days = 1,
                distance = 1,
                isRoad = isRoadChunk
            };
        }

        if (extraDiscoveredChunks < 0)
        {
            extraDiscoveredChunks = MapExpansionManager.Instance != null
                ? MapExpansionManager.Instance.DiscoveredOrSurveyingExtraChunks
                : 0;
        }

        int distance = GetMinDistanceToRevealed(targetChunk, activeChunks);

        float timeRatio = Mathf.Clamp01((float)(distance - 1) / Mathf.Max(1, cfg.maxDistanceForTime - 1));
        int baseDays = Mathf.RoundToInt(Mathf.Lerp(cfg.minDays, cfg.maxDays, timeRatio));

        // Obliczanie bazowych kosztów wg liczby odkrytych terenów / terenów w trakcie eksploracji
        int totalExploredOrExploring = extraDiscoveredChunks;
        float food;
        float wood;
        float ether;

        if (totalExploredOrExploring < 4)
        {
            int t = Mathf.Max(0, totalExploredOrExploring);
            food = 10f + t * 5f;
            wood = 5f + t * 2f;
            ether = 0f;
        }
        else
        {
            int n = totalExploredOrExploring - 4;
            food = 30f + n * 4f;
            ether = 4f + n * 2f;
            wood = 15f + n * 1f;
        }

        if (isRoadChunk)
        {
            ether *= cfg.roadCostMultiplier;
            food *= cfg.roadCostMultiplier;
            wood *= cfg.roadCostMultiplier;
            baseDays = Mathf.Max(1, Mathf.RoundToInt(baseDays * cfg.roadTimeMultiplier));
        }

        if (extraDiscoveredChunks > 0 && cfg.chunksPerExtraSurveyDay > 0)
        {
            int extraDays = extraDiscoveredChunks / cfg.chunksPerExtraSurveyDay;
            baseDays += extraDays;
        }

        int clampedElves = Mathf.Max(cfg.minElvesForSurvey, elfCount);
        int extraElves = clampedElves - cfg.minElvesForSurvey;

        int finalDays = Mathf.Max(cfg.minSurveyDays, baseDays - extraElves * cfg.daysReductionPerExtraElf);

        float elfCostMultiplier = (float)clampedElves / Mathf.Max(1, cfg.minElvesForSurvey);
        ether *= elfCostMultiplier;
        food *= elfCostMultiplier;
        wood *= elfCostMultiplier;

        return new MissionCost
        {
            etherCost = Mathf.RoundToInt(ether),
            foodCost = Mathf.RoundToInt(food),
            woodCost = Mathf.RoundToInt(wood),
            days = finalDays,
            distance = distance,
            isRoad = isRoadChunk
        };
    }

    public static MissionCost CalculateReconCost(ExpansionCostConfig cfg)
    {
        if ((object)cfg == null)
        {
            return new MissionCost
            {
                etherCost = 0,
                foodCost = 50,
                woodCost = 20,
                days = 1,
                distance = 1,
                isRoad = false
            };
        }

        return new MissionCost
        {
            etherCost = Mathf.RoundToInt(cfg.reconEtherCost),
            foodCost = Mathf.RoundToInt(cfg.reconFoodCost),
            woodCost = Mathf.RoundToInt(cfg.reconWoodCost),
            days = cfg.reconDays,
            distance = 1,
            isRoad = false
        };
    }

    public static ReconAbundance EvaluateAbundance(int count, ExpansionCostConfig cfg)
    {
        if (count <= 0) return ReconAbundance.None;

        int scarceMax = (object)cfg != null ? cfg.scarceMaxCount : 2;
        int moderateMax = (object)cfg != null ? cfg.moderateMaxCount : 5;

        if (count <= scarceMax) return ReconAbundance.Scarce;
        if (count <= moderateMax) return ReconAbundance.Moderate;
        return ReconAbundance.Abundant;
    }

    private static int GetMinDistanceToRevealed(
        Vector2Int target,
        Dictionary<Vector2Int, ChunkStateData> activeChunks)
    {
        if (activeChunks == null) return FallbackDistance;

        var visited = new HashSet<Vector2Int> { target };
        var queue = new Queue<(Vector2Int coord, int dist)>();
        queue.Enqueue((target, 0));

        while (queue.Count > 0)
        {
            var (current, dist) = queue.Dequeue();

            if (dist > 0 && activeChunks.TryGetValue(current, out var data))
            {
                if (data.baseState == ChunkState.Outskirts || data.baseState == ChunkState.Settled)
                {
                    return dist;
                }
            }

            foreach (var neighbor in HexGridMath.GetNeighbors(current))
            {
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue((neighbor, dist + 1));
                }
            }

            if (dist > 10) break;
        }

        return FallbackDistance;
    }
}

public struct MissionCost
{
    public int etherCost;
    public int foodCost;
    public int woodCost;
    public int days;
    public int distance;
    public bool isRoad;

    public bool CanAfford(float ether, float food, float wood) =>
        ether >= etherCost && food >= foodCost && wood >= woodCost;

    public bool CanAfford(float ether, float food) =>
        ether >= etherCost && food >= foodCost && woodCost <= 0;
}
