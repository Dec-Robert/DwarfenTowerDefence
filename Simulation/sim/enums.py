from enum import IntEnum


class ResourceType(IntEnum):
    Ether = 0
    Wood = 1
    Stone = 2
    Coal = 3
    Food = 4
    Iron = 5
    Hope = 6


class BuildingType(IntEnum):
    Economic = 0
    Defense = 1
    Housing = 2
    Utility = 3
    Unique = 4


class Race(IntEnum):
    Humans = 0
    Elves = 1
    Dwarves = 2


class HexFeatureType(IntEnum):
    NoneFeature = 0
    Forest = 1
    Mountain = 2
    Meadow = 3
    Hill = 10
    Sinkhole = 11
    Base = 12
    Beacon = 13
    Wall = 14


class ChunkState(IntEnum):
    Wilderness = 0
    Borderlands = 1
    Surveying = 2
    Outskirts = 3
    Settled = 4


class ChunkType(IntEnum):
    Normal = 0
    Spawner = 1
    MetaSafe = 2
    Special = 3


class DamageType(IntEnum):
    Physical = 0
    Magic = 1
    TrueDmg = 2


class EnemyRank(IntEnum):
    Normal = 0
    Elite = 1
    Boss = 2


class ElevationFilterMode(IntEnum):
    Any = 0
    OnlyFlat = 1
    OnlyHills = 2
    OnlyCavities = 3
    CustomRange = 4
    SpecificValues = 5


class RuneRarity(IntEnum):
    Common = 0
    Uncommon = 1
    Rare = 2
    Legendary = 3
    Cursed = 4


class RuneStatType(IntEnum):
    Range = 0
    Damage = 1
    FireRate = 2
    ArmorPenetration = 3
    MagicPenetration = 4
    CriticalChance = 5
    CriticalDamage = 6


class RuneValueType(IntEnum):
    Flat = 0
    Percent = 1


class TargetingMode(IntEnum):
    Closest = 0
    Furthest = 1
    FurthestOnTrack = 2
    ClosestOnTrack = 3
    MostHP = 4
    LeastHP = 5
    HighestThreat = 6
    Random = 7
    Chunk = 8


class MilestoneType(IntEnum):
    TotalPopulation = 0
    SpecificRacePopulation = 1
    TotalTowers = 2
    SpecificTower = 3
    NightsSurvived = 4
    BossesKilled = 5
    EnemiesKilled = 6
    ProductionBuildingsBuilt = 7
    LegendaryOrCursedRunesObtained = 8
    RuneCraftingDone = 9
    Victory = 10
    ExpeditionsCompleted = 11


class MetaEffectType(IntEnum):
    NoneEffect = 0
    StartingEther = 10
    StartingWood = 11
    StartingStone = 12
    StartingIron = 13
    StartingCoal = 14
    StartingFood = 15
    BonusShifts = 20
    BonusWorkersPerShift = 21
    BonusProductionWorkers = 22
    ProductionSynergy_Elf = 23
    ProductionSynergy_Human = 24
    BuildingPassiveEfficiency = 30
    BuildingWorkerEfficiencyBonus = 31
    BuildingTerrainBonusMultiplier = 32
    SpecificBuildingUpgradeCostReduction = 33
    HousingStartPopulation = 34
    HousingMaxResidents = 35
    HousingMaxResidents_Humans = 36
    HousingMaxResidents_Elves = 37
    HousingMaxResidents_Dwarves = 38
    SpecificBuildingProductionBonus = 39
    EliteChanceBoost = 40
    EnemySurvivorPenaltyArmor = 41
    EnemySurvivorPenaltySpeed = 42
    EnemySurvivorPenaltyDodge = 43
    TowerNoAmmoNightPenalty = 50
    BonusTowerCrewSlots = 51
    TowerGlobalArmorPenetration = 52
    TowerGlobalMagicPenetration = 53
    TowerGlobalFireRate = 54
    TowerGlobalDamage = 55
    TowerDraftUnlock = 56
    TowerCrewSynergy_Dwarf = 57
    TowerCrewSynergy_Elf = 58
    TowerCrewSynergy_Human = 59
    ExpansionCostReduction = 60
    CityBaseHealth = 70
    GateBaseHP = 71
    WallSystem_Solution1 = 80
    WallSystem_Solution2 = 81
    UniqueChunkUnlock = 90
    Reserved_A = 100
    Reserved_B = 101
    Reserved_C = 102
    RuneDropChance_Uncommon = 110
    RuneDropChance_Rare = 111
    RuneDropChance_Legendary = 112
    RuneDropChance_Cursed = 113
    RuneTowerLimit = 114
    UnlockRuneRarity_Uncommon = 115
    UnlockRuneRarity_Rare = 116
    UnlockRuneRarity_Legendary = 117
    UnlockRuneRarity_Cursed = 118
    UnlockRuneFusion = 119
    UnlockRuneTransmutation = 120
    UnlockCursedCrafting = 121
    UnlockDruidTower = 130
