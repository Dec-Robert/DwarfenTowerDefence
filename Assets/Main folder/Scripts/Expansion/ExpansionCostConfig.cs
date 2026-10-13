using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ExpansionCostConfig", menuName = "Tower Defense/Expansion Cost Config")]
public class ExpansionCostConfig : ScriptableObject
{
    [Header("Czas Misji")]
    public int minDays = 1;
    public int maxDays = 7;
    public int maxDistanceForTime = 6;

    [Header("Koszty Bazowe")]
    public float baseGoldCost = 30f;
    public float baseFoodCost = 10f;
    public float goldPerHex = 15f;
    public float foodPerHex = 5f;

    [Header("Modyfikatory Drogi")]
    [Range(0f, 1f)] public float roadCostMultiplier = 0.6f;
    [Range(0f, 1f)] public float roadTimeMultiplier = 0.7f;

    [Header("Skalowanie Elfami (Pełna Ekspedycja)")]
    public int minElvesForSurvey = 2;
    public int daysReductionPerExtraElf = 1;
    public int minSurveyDays = 1;

    [Header("Krótka Ekspedycja (Recon / Zwiad)")]
    public int reconDays = 1;
    public int reconElfRequirement = 1;
    public float reconGoldCost = 15f;
    public float reconFoodCost = 10f;

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
        int minElves = cfg != null ? cfg.minElvesForSurvey : 2;
        return CalculateSurveyCost(targetChunk, activeChunks, isRoadChunk, cfg, minElves);
    }

    public static MissionCost CalculateSurveyCost(
        Vector2Int targetChunk,
        Dictionary<Vector2Int, ChunkStateData> activeChunks,
        bool isRoadChunk,
        ExpansionCostConfig cfg,
        int elfCount)
    {
        if (cfg == null)
        {
            return new MissionCost
            {
                goldCost = 30,
                foodCost = 10,
                days = 1,
                distance = 1,
                isRoad = isRoadChunk
            };
        }

        int distance = GetMinDistanceToRevealed(targetChunk, activeChunks);

        float timeRatio = Mathf.Clamp01((float)(distance - 1) / Mathf.Max(1, cfg.maxDistanceForTime - 1));
        int baseDays = Mathf.RoundToInt(Mathf.Lerp(cfg.minDays, cfg.maxDays, timeRatio));

        float gold = cfg.baseGoldCost + cfg.goldPerHex * (distance - 1);
        float food = cfg.baseFoodCost + cfg.foodPerHex * (distance - 1);

        if (isRoadChunk)
        {
            gold *= cfg.roadCostMultiplier;
            food *= cfg.roadCostMultiplier;
            baseDays = Mathf.Max(1, Mathf.RoundToInt(baseDays * cfg.roadTimeMultiplier));
        }

        int clampedElves = Mathf.Max(cfg.minElvesForSurvey, elfCount);
        int extraElves = clampedElves - cfg.minElvesForSurvey;

        int finalDays = Mathf.Max(cfg.minSurveyDays, baseDays - extraElves * cfg.daysReductionPerExtraElf);

        float elfCostMultiplier = (float)clampedElves / Mathf.Max(1, cfg.minElvesForSurvey);
        gold *= elfCostMultiplier;
        food *= elfCostMultiplier;

        return new MissionCost
        {
            goldCost = Mathf.RoundToInt(gold),
            foodCost = Mathf.RoundToInt(food),
            days = finalDays,
            distance = distance,
            isRoad = isRoadChunk
        };
    }

    public static MissionCost CalculateReconCost(ExpansionCostConfig cfg)
    {
        if (cfg == null)
        {
            return new MissionCost
            {
                goldCost = 15,
                foodCost = 10,
                days = 1,
                distance = 1,
                isRoad = false
            };
        }

        return new MissionCost
        {
            goldCost = Mathf.RoundToInt(cfg.reconGoldCost),
            foodCost = Mathf.RoundToInt(cfg.reconFoodCost),
            days = cfg.reconDays,
            distance = 1,
            isRoad = false
        };
    }

    public static ReconAbundance EvaluateAbundance(int count, ExpansionCostConfig cfg)
    {
        if (count <= 0) return ReconAbundance.None;

        int scarceMax = cfg != null ? cfg.scarceMaxCount : 2;
        int moderateMax = cfg != null ? cfg.moderateMaxCount : 5;

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
    public int goldCost;
    public int foodCost;
    public int days;
    public int distance;
    public bool isRoad;

    public bool CanAfford(float gold, float food) => gold >= goldCost && food >= foodCost;
}