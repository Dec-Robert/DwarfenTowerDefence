using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dane jednej aktywnej misji zwiadowczej (Survey lub Recon).
/// Tworzona i zarządzana przez MapExpansionManager w activeMissions.
/// Tickowana raz na dzień podczas poranka (TimePhaseManager.OnMorningStarted).
/// </summary>
[System.Serializable]
public class ScoutingMission
{
    // ─── Typ misji i cel ─────────────────────────────────────────────────────
    public ScoutingMissionType missionType = ScoutingMissionType.Survey;
    public Vector2Int targetChunk;

    // ─── Czas ────────────────────────────────────────────────────────────────
    public int totalDays;
    public int daysRemaining;

    // ─── Zasoby i siła robocza ────────────────────────────────────────────────
    public int elfCount;
    public int equipmentUsed;
    public List<Citizen> assignedElves = new List<Citizen>();

    public float goldPaid;
    public float foodPaid;

    // ─── Powiązany budynek (opcjonalny, zachowany dla kompatybilności) ───────
    public ExpeditionCenterEntity sourceCenter;

    // ─── Stan ─────────────────────────────────────────────────────────────────
    public bool IsComplete => daysRemaining <= 0;

    // ─── Fabryki ─────────────────────────────────────────────────────────────
    public static ScoutingMission CreateSurvey(
        Vector2Int target,
        int days,
        int elves,
        float gold,
        float food,
        List<Citizen> elvesList,
        ExpeditionCenterEntity center = null)
    {
        return new ScoutingMission
        {
            missionType   = ScoutingMissionType.Survey,
            targetChunk   = target,
            totalDays     = days,
            daysRemaining = days,
            elfCount      = elves,
            equipmentUsed = elves,
            assignedElves = elvesList != null ? new List<Citizen>(elvesList) : new List<Citizen>(),
            goldPaid      = gold,
            foodPaid      = food,
            sourceCenter  = center
        };
    }

    public static ScoutingMission CreateRecon(
        Vector2Int target,
        int days,
        int elves,
        float gold,
        float food,
        List<Citizen> elvesList,
        ExpeditionCenterEntity center = null)
    {
        return new ScoutingMission
        {
            missionType   = ScoutingMissionType.Recon,
            targetChunk   = target,
            totalDays     = days,
            daysRemaining = days,
            elfCount      = elves,
            equipmentUsed = elves,
            assignedElves = elvesList != null ? new List<Citizen>(elvesList) : new List<Citizen>(),
            goldPaid      = gold,
            foodPaid      = food,
            sourceCenter  = center
        };
    }

    /// <summary>Zachowane dla kompatybilności wstecznej.</summary>
    public static ScoutingMission Create(
        Vector2Int target,
        int days,
        float gold,
        float food,
        ExpeditionCenterEntity center)
    {
        return CreateSurvey(target, days, 2, gold, food, null, center);
    }

    /// <summary>Tick dzienny. Zwraca true jeśli misja właśnie się skończyła.</summary>
    public bool Tick()
    {
        if (daysRemaining <= 0) return true;
        daysRemaining--;
        return daysRemaining <= 0;
    }

    public override string ToString()
        => $"Misja {missionType} [{targetChunk}] ({elfCount} elfów) – {daysRemaining}/{totalDays} dni";
}