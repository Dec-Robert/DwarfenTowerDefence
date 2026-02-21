using UnityEngine;

/// <summary>
/// Stan ekspansji konkretnego chunka mapy.
///
/// Locked        – za daleko lub zablokowany przez niedokończoną drogę
/// Unlocked      – widoczny w UI, można wysłać zwiadowcę
/// Scouting      – zwiadowca w drodze (misja aktywna)
/// MilitaryOnly  – odkryty, można budować TYLKO wieże
/// FullyUnlocked – pełne prawa budowlane
/// </summary>
public enum ChunkState
{
    Locked,
    Unlocked,
    Scouting,
    MilitaryOnly,
    FullyUnlocked
}

[System.Serializable]
public class ChunkStateData
{
    public ChunkState state = ChunkState.Locked;

    // ─── Timer osadnictwa (MilitaryOnly → FullyUnlocked) ─────────────────────
    /// <summary>Ile dni pozostało do automatycznego przejścia MilitaryOnly → FullyUnlocked.</summary>
    public int settlementDaysRemaining = 0;

    /// <summary>Czy na tym chunku stoi Posterunek (skraca czas osadnictwa do 0).</summary>
    public bool hasOutpost = false;

    // ─── Metadane odkrycia ────────────────────────────────────────────────────
    /// <summary>Czy chunk jest odcinkiem drogi (tańsze odkrywanie).</summary>
    public bool isRoadChunk = false;

    /// <summary>Numer fali/dnia w którym chunk został odkryty.</summary>
    public int discoveredOnDay = -1;

    // ─── Pomocnicze ───────────────────────────────────────────────────────────
    public bool IsBuildbale => state == ChunkState.FullyUnlocked;
    public bool IsMilitaryAllowed => state == ChunkState.MilitaryOnly || state == ChunkState.FullyUnlocked;
    public bool CanScout => state == ChunkState.Unlocked;
}