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
        int distance = GetMinDistanceToRevealed(targetChunk, activeChunks);

        float timeRatio = Mathf.Clamp01((float)(distance - 1) / Mathf.Max(1, cfg.maxDistanceForTime - 1));
        int days = Mathf.RoundToInt(Mathf.Lerp(cfg.minDays, cfg.maxDays, timeRatio));

        float gold = cfg.baseGoldCost + cfg.goldPerHex * (distance - 1);
        float food = cfg.baseFoodCost + cfg.foodPerHex * (distance - 1);

        if (isRoadChunk)
        {
            gold *= cfg.roadCostMultiplier;
            food *= cfg.roadCostMultiplier;
            days = Mathf.Max(1, Mathf.RoundToInt(days * cfg.roadTimeMultiplier));
        }

        return new MissionCost
        {
            goldCost = Mathf.RoundToInt(gold),
            foodCost = Mathf.RoundToInt(food),
            days = days,
            distance = distance,
            isRoad = isRoadChunk
        };
    }

    private static int GetMinDistanceToRevealed(
        Vector2Int target,
        Dictionary<Vector2Int, ChunkStateData> activeChunks)
    {
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