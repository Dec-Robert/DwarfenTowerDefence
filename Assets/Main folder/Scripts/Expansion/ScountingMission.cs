using UnityEngine;

/// <summary>
/// Dane jednej aktywnej misji zwiadowczej.
/// Tworzony przez MapExpansionManager, przechowywany w activeMissions.
/// Tickowany raz na dzień przez GameManager.
/// </summary>
[System.Serializable]
public class ScoutingMission
{
    // ─── Cel ─────────────────────────────────────────────────────────────────
    public Vector2Int targetChunk;

    // ─── Czas ────────────────────────────────────────────────────────────────
    /// <summary>Łączna liczba dni misji (1-7).</summary>
    public int totalDays;
    /// <summary>Ile dni pozostało.</summary>
    public int daysRemaining;

    // ─── Zasoby zapłacone przy wysłaniu ──────────────────────────────────────
    public float goldPaid;
    public float foodPaid;

    // ─── Powiązany budynek ────────────────────────────────────────────────────
    /// <summary>Centrum Ekspedycyjne które wysłało tę misję.</summary>
    public ExpeditionCenterEntity sourceCenter;

    // ─── Stan ─────────────────────────────────────────────────────────────────
    public bool IsComplete => daysRemaining <= 0;

    // ─── Fabryka ─────────────────────────────────────────────────────────────
    public static ScoutingMission Create(
        Vector2Int target,
        int days,
        float gold,
        float food,
        ExpeditionCenterEntity center)
    {
        return new ScoutingMission
        {
            targetChunk   = target,
            totalDays     = days,
            daysRemaining = days,
            goldPaid      = gold,
            foodPaid      = food,
            sourceCenter  = center
        };
    }

    /// <summary>Tick dzienny. Zwraca true jeśli misja właśnie się skończyła.</summary>
    public bool Tick()
    {
        if (daysRemaining <= 0) return true;
        daysRemaining--;
        return daysRemaining <= 0;
    }

    public override string ToString()
        => $"Misja [{targetChunk}] – {daysRemaining}/{totalDays} dni";
}