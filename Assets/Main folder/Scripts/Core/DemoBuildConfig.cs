using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralna konfiguracja trybu Demo v2 (itch.io).
/// Skupia w jednym miejscu reguly blokady meta-progresji, dostepne wieze,
/// limity pracownikow oraz link do ankiety.
/// </summary>
public static class DemoBuildConfig
{
    /// <summary>
    /// Glowny przelacznik trybu Demo v2.
    /// </summary>
    public static bool IsDemoV2Active { get; set; } = true;

    public const int DemoBonusProductionWorkers = 1;
    public const int DemoBonusTowerCrewSlots = 1;
    public const int DemoRuneTowerLimit = 4;
    public const string FeedbackSurveyUrl = "https://forms.gle/fT4BABcE6PJvNr5f7";

    public static readonly string[] AllowedDemoTowerIDs = new string[]
    {
        "1. Sentinel Arbalest",
        "3. Steam Repeater",
        "4. Crucible of Conflagrations",
        "5. Shadow Spire",
        "6. Royal Bombard"
    };

    private static readonly HashSet<string> AllowedTowerLookup = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "1. Sentinel Arbalest",
        "Arbalet",
        "3. Steam Repeater",
        "Repetier Parowy",
        "4. Crucible of Conflagrations",
        "Tygiel Pożogi",
        "5. Shadow Spire",
        "Iglica Cieni",
        "6. Royal Bombard",
        "Bombarda"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsDemoV2Active = true;
    }

    public static bool IsTowerAllowedInDemo(TowerData tower)
    {
        if (tower == null) return false;

        string assetName = null;
        try { assetName = tower.name; } catch { }

        if (!string.IsNullOrEmpty(assetName) && AllowedTowerLookup.Contains(assetName))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(tower.buildingName) && AllowedTowerLookup.Contains(tower.buildingName))
        {
            return true;
        }

        return false;
    }
}
