using System;
using UnityEngine;

public enum ResourceType
{
    Gold,
    Wood,
    Stone,
    Coal,
    Food,
    Population,
    Iron,
    Hope
}

public enum Race
{
    Humans,
    Elves,
    Dwarves
}

public enum WorkState
{
    Idle, //Nie pracuje   
    Assigned, //Przydzielony do pracy, czeka na rozpocz�cie pracy, mozna nim porusza� mi�dzy budynkami
    Working, //Pracuje zablokowany do konca zmiany
    Exhausted //Zm�czony po zmianie
}

public enum DayPhase
{
    Day,
    Night
}

public enum DamageType
{
    Physical, // Zwyk�e wie�e (�ucznik, Armata)
    Magic, // Wie�e magiczne (L�d, Ogie�)
    True // Typ obra�e� osi�galny tylko dzi�ki niekt�rym run�S
}

public enum EnemyRank
{
    Normal,
    Elite,
    Boss
}

/// <summary>
///     Tryb systemu murów — kontrolowany przez MetaUpgrade.
///     Disabled     → brak murów (domyślnie przed zakupem)
///     Solution1    → tylko chunki startowe otoczone murem (tani upgrade, wczesna gra)
///     Solution2    → żywa linia frontu z bramami (drogi upgrade, późna gra)
/// </summary>
public enum WallSystemMode
{
    Disabled,
    Solution1_BaseOnly,
    Solution2_FrontLine
}

/// <summary>
///     Wszystkie możliwe efekty meta-ulepszeń.
///     Każdy MetaUpgradeSO ma jeden MetaEffectType.
///     ── Surowce startowe ────────────────────────────────
///     StartingGold / Wood / Stone / Iron / Coal / Food
///     effectValue = ile dodać na start sesji
///     ── Pracownicy i zmiany ─────────────────────────────
///     BonusShifts / BonusWorkersPerShift
///     effectValue = ile dodać do globalnego bonusu
///     ── Produkcja budynków ──────────────────────────────
///     BuildingPassiveEfficiency
///     effectValue = mnożnik produkcji bez pracownika (np. 0.1 = 10%)
///     BuildingWorkerEfficiencyBonus
///     effectValue = dodatkowy % produkcji za każdego pracownika (np. 0.2 = +20%)
///     BuildingTerrainBonusMultiplier
///     effectValue = mnożnik bonusu terenowego (targetBuilding wymagany)
///     SpecificBuildingUpgradeCostReduction
///     effectValue = % redukcji kosztu upgradu (targetBuilding wymagany)
///     HousingStartPopulation
///     effectValue = ile dodatkowych mieszkańców na start w budynkach mieszkalnych
///     ── Wrogowie ────────────────────────────────────────
///     EliteChanceBoost
///     effectValue = szansa procentowa (+X%) od fali effectValue2
///     EnemySurvivorPenaltyArmor / EnemySurvivorPenaltySpeed / EnemySurvivorPenaltyDodge
///     effectValue = kara procentowa (np. 0.3 = -30%) dla wrogów przeżywających do dnia
///     ── Wieże ───────────────────────────────────────────
///     TowerNoAmmoNightPenalty
///     effectValue = mnożnik statystyk bez amunicji nocą (np. 0.5 = 50%)
///     ── Eksploracja ─────────────────────────────────────
///     ExpansionCostReduction
///     effectValue = % redukcji kosztu odkrywania (np. 0.2 = -20%)
///     ── Miasto i bramy ──────────────────────────────────
///     CityBaseHealth
///     effectValue = ile HP dodać do bazowego HP miasta
///     GateBaseHP
///     effectValue = ile HP dodać do bazowego HP bram frontowych
///     ── System murów ────────────────────────────────────
///     WallSystem_Solution1 / WallSystem_Solution2
///     effectValue nieużywane
///     ── Przyszłe / zarezerwowane ────────────────────────
///     UniqueChunkUnlock
///     effectValue = ID chunku (do rozbudowy)
///     Reserved_A / Reserved_B / Reserved_C
///     Zarezerwowane dla przyszłych mechanik
/// </summary>
public enum MetaEffectType
{
    None = 0,

    // ── Surowce startowe ──────────────────────────────────────────────────────
    StartingGold = 10,
    StartingWood = 11,
    StartingStone = 12,
    StartingIron = 13,
    StartingCoal = 14,
    StartingFood = 15,

    // ── Pracownicy i zmiany ───────────────────────────────────────────────────
    BonusShifts = 20,
    BonusWorkersPerShift = 21,

    // ── Produkcja budynków ────────────────────────────────────────────────────
    BuildingPassiveEfficiency = 30,
    BuildingWorkerEfficiencyBonus = 31,
    BuildingTerrainBonusMultiplier = 32,
    SpecificBuildingUpgradeCostReduction = 33,
    HousingStartPopulation = 34,

    /// <summary>Zwiększa maxResidents dla WSZYSTKICH domów (niezależnie od rasy).</summary>
    HousingMaxResidents = 35,

    /// <summary>Zwiększa maxResidents tylko dla domów ludzi (Race.Humans).</summary>
    HousingMaxResidents_Humans = 36,

    /// <summary>Zwiększa maxResidents tylko dla domów elfów (Race.Elves).</summary>
    HousingMaxResidents_Elves = 37,

    /// <summary>Zwiększa maxResidents tylko dla domów krasnoludów (Race.Dwarves).</summary>
    HousingMaxResidents_Dwarves = 38,

    /// <summary>Zwiększa produktywnośc konkretnego typu budynku </summary>
    SpecificBuildingProductionBonus = 39,

    // ── Wrogowie ──────────────────────────────────────────────────────────────
    EliteChanceBoost = 40,
    EnemySurvivorPenaltyArmor = 41,
    EnemySurvivorPenaltySpeed = 42,
    EnemySurvivorPenaltyDodge = 43,

    // ── Wieże ─────────────────────────────────────────────────────────────────
    TowerNoAmmoNightPenalty = 50,

    // ── Eksploracja ───────────────────────────────────────────────────────────
    ExpansionCostReduction = 60,

    // ── Miasto i bramy ────────────────────────────────────────────────────────
    CityBaseHealth = 70,
    GateBaseHP = 71,

    // ── System murów ──────────────────────────────────────────────────────────
    WallSystem_Solution1 = 80,
    WallSystem_Solution2 = 81,

    // ── Przyszłe / zarezerwowane ──────────────────────────────────────────────
    UniqueChunkUnlock = 90,
    Reserved_A = 100,
    Reserved_B = 101,
    Reserved_C = 102,

    // ── System Run (Drop) ─────────────────────────────────────────────────────
    RuneDropChance_Uncommon = 110,
    RuneDropChance_Rare = 111,
    RuneDropChance_Legendary = 112,
    RuneDropChance_Cursed = 113,

    // Zwiększa maksymalną liczbę wież runicznych, które gracz może wybudować (bazowo 0)
    RuneTowerLimit = 114
}

[Serializable]
public struct TerrainBonusRule
{
    [Tooltip("Jaki teren daje bonus? (np. Forest dla Tartaku)")]
    public HexFeatureType requiredFeature;

    [Tooltip("Zasięg poszukiwania (1 = tylko sąsiedzi, 2 = sąsiedzi sąsiadów)")]
    public int searchRadius;

    [Header("Matematyka")] public float baseBonusPerHex;
    public float penaltyPerUser;
    public float minBonus;
    public float onTopProductionBonus;
}


public enum RuneRarity
{
    Common, // Białe
    Uncommon, // Zielone
    Rare, // Niebieskie
    Legendary, // Złote (Ultimate)
    Cursed // Czerwone (Potężny buff + losowy debuff)
}

public enum RuneValueType
{
    Flat, // Wartość stała (np. +1 Zasięgu)
    Percent // Wartość procentowa (np. +10% Zasięgu)
}

// TYLKO te statystyki będą widoczne przy tworzeniu run:
public enum RuneStatType
{
    Range,
    Damage,
    FireRate,
    ArmorPenetration,
    MagicPenetration,
    CriticalChance,
    CriticalDamage
}

public enum TargetingMode
{
    Closest,          // Najbliżej wieży
    Furthest,         // Najdalej od wieży
    FurthestOnTrack,  // Najdalej od głównej bramy (dopiero zespawnowany)
    ClosestOnTrack,   // Najbliżej głównej bramy (największe zagrożenie dla bazy)
    MostHP,           // Najwięcej aktualnego HP
    LeastHP,          // Najmniej aktualnego HP
    HighestThreat,    // Boss -> Elita -> Normal
    Random,           // Losowy z przefiltrowanej puli
    Chunk             // Ignoruje wrogów, wali w konkretny punkt na mapie
}