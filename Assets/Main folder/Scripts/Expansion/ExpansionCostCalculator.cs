using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Oblicza koszt i czas misji zwiadowczej.
///
/// Zasady:
///   - Bazowy czas: 1-7 dni wg odległości hex od najbliższego odkrytego chunka
///   - Drogi: chunki będące drogą mają niższy koszt złota i krótszy czas
///   - Koszt złota i jedzenia skaluje się z odległością
/// </summary>
[CreateAssetMenu(fileName = "ExpansionCostConfig", menuName = "Tower Defense/Expansion Cost Config")]
public class ExpansionCostConfig : ScriptableObject
{
    [Header("── Czas Misji (dni) ──────────────────────")]
    [Tooltip("Czas misji dla odległości 1 hex od granicy")]
    public int minDays = 1;
    [Tooltip("Czas misji dla odległości maksymalnej")]
    public int maxDays = 7;
    [Tooltip("Od której odległości hex zaczyna się maksymalny czas")]
    public int maxDistanceForTime = 6;

    [Header("── Koszty Bazowe ────────────────────────")]
    public float baseGoldCost  = 30f;
    public float baseFoodCost  = 10f;
    [Tooltip("Koszt rośnie liniowo z odległością")]
    public float goldPerHex    = 15f;
    public float foodPerHex    = 5f;

    [Header("── Zniżka za Drogi ──────────────────────")]
    [Range(0f, 1f)]
    [Tooltip("Drogi są tańsze – mnożnik kosztu (0.6 = 40% taniej)")]
    public float roadCostMultiplier = 0.6f;
    [Tooltip("Drogi zajmują krócej – mnożnik czasu (0.7 = 30% szybciej)")]
    public float roadTimeMultiplier = 0.7f;
}

/// <summary>
/// Statyczna klasa kalkulatora – nie jest MonoBehaviour, nie trzyma stanu.
/// </summary>
public static class ExpansionCostCalculator
{
    /// <summary>
    /// Oblicza koszt i czas misji dla danego chunka.
    /// </summary>
    /// <param name="targetChunk">Cel misji.</param>
    /// <param name="activeChunks">Mapa aktywnych chunków (do wyznaczenia odległości).</param>
    /// <param name="isRoadChunk">Czy cel jest odcinkiem drogi.</param>
    /// <param name="cfg">Konfiguracja kosztów.</param>
    public static MissionCost Calculate(
        Vector2Int targetChunk,
        Dictionary<Vector2Int, ChunkStateData> activeChunks,
        bool isRoadChunk,
        ExpansionCostConfig cfg)
    {
        int distance = GetMinDistanceToRevealed(targetChunk, activeChunks);

        // Czas (1-7 dni)
        float timeRatio = Mathf.Clamp01((float)(distance - 1) / (cfg.maxDistanceForTime - 1));
        int days = Mathf.RoundToInt(Mathf.Lerp(cfg.minDays, cfg.maxDays, timeRatio));

        // Koszt złota i jedzenia
        float gold = cfg.baseGoldCost + cfg.goldPerHex * (distance - 1);
        float food = cfg.baseFoodCost + cfg.foodPerHex * (distance - 1);

        // Zniżka za drogi
        if (isRoadChunk)
        {
            gold *= cfg.roadCostMultiplier;
            food *= cfg.roadCostMultiplier;
            days  = Mathf.Max(1, Mathf.RoundToInt(days * cfg.roadTimeMultiplier));
        }

        return new MissionCost
        {
            goldCost  = Mathf.RoundToInt(gold),
            foodCost  = Mathf.RoundToInt(food),
            days      = days,
            distance  = distance,
            isRoad    = isRoadChunk
        };
    }

    /// <summary>
    /// BFS – minimalna odległość w hexach do najbliższego w pełni odkrytego chunka.
    /// </summary>
    private static int GetMinDistanceToRevealed(
        Vector2Int target,
        Dictionary<Vector2Int, ChunkStateData> activeChunks)
    {
        // BFS od targetu, szukamy pierwszego FullyUnlocked / MilitaryOnly sąsiada.
        // UWAGA: celowo pomijamy sam target (dist==0) żeby uniknąć sytuacji gdzie
        // chunk jest już MilitaryOnly gdy wywołujemy kalkulator – wtedy dystans
        // wynosiłby 0 → days=1 → natychmiastowe przejście do FullyUnlocked.
        var visited = new HashSet<Vector2Int> { target };
        var queue   = new Queue<(Vector2Int coord, int dist)>();
        queue.Enqueue((target, 0));

        while (queue.Count > 0)
        {
            var (current, dist) = queue.Dequeue();

            // Pomiń sam target przy dist==0 – może już być MilitaryOnly
            if (dist > 0 && activeChunks.TryGetValue(current, out var data))
            {
                if (data.state == ChunkState.MilitaryOnly ||
                    data.state == ChunkState.FullyUnlocked)
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

            if (dist > 10) break; // Bezpiecznik
        }

        return cfg_fallbackDistance; // Fallback
    }

    private const int cfg_fallbackDistance = 6;
}

/// <summary>Wynik kalkulacji – koszt i czas misji.</summary>
public struct MissionCost
{
    public int   goldCost;
    public int   foodCost;
    public int   days;
    public int   distance;
    public bool  isRoad;

    public bool CanAfford(float gold, float food) => gold >= goldCost && food >= foodCost;

    public override string ToString()
        => $"{days} dni | {goldCost}G + {foodCost}F{(isRoad ? " [DROGA -40%]" : "")}";
}