using System;
using System.Collections.Generic;
using UnityEngine;

public enum DevRunMode
{
    All = 0,
    KeyNodes = 1,
    None = 2
}

/// <summary>
/// Centralna konfiguracja trybu DEV RUN (aktywna wyłącznie w Unity Editor).
/// Pozwala w czasie Play Mode nadpisać stan meta-progresji oraz odblokowań wież/budynków/run
/// bez modyfikowania pliku zapisu gracza na dysku.
/// </summary>
public static class DevRunConfig
{
    private const string PrefKeyPrefix = "DwarfenTD_DevRun_";
    private const string EnabledPrefSuffix = "Enabled";
    private const string ModePrefSuffix = "Mode";

    private static bool? s_TestEnabledOverride;
    private static DevRunMode? s_TestModeOverride;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetTestOverrides()
    {
        s_TestEnabledOverride = null;
        s_TestModeOverride = null;
    }

#if UNITY_EDITOR
    private static string ProjectPrefKey(string suffix)
    {
        return PrefKeyPrefix + UnityEngine.Application.dataPath.GetHashCode().ToString("X8") + "_" + suffix;
    }

    public static bool IsEnabled
    {
        get => s_TestEnabledOverride ?? UnityEditor.EditorPrefs.GetBool(ProjectPrefKey(EnabledPrefSuffix), false);
        set => UnityEditor.EditorPrefs.SetBool(ProjectPrefKey(EnabledPrefSuffix), value);
    }

    public static DevRunMode Mode
    {
        get
        {
            if (s_TestModeOverride.HasValue) return s_TestModeOverride.Value;
            int raw = UnityEditor.EditorPrefs.GetInt(ProjectPrefKey(ModePrefSuffix), (int)DevRunMode.All);
            return Enum.IsDefined(typeof(DevRunMode), raw) ? (DevRunMode)raw : DevRunMode.All;
        }
        set => UnityEditor.EditorPrefs.SetInt(ProjectPrefKey(ModePrefSuffix), (int)value);
    }
#else
    public static bool IsEnabled
    {
        get => s_TestEnabledOverride ?? false;
        set { }
    }

    public static DevRunMode Mode
    {
        get => s_TestModeOverride ?? DevRunMode.None;
        set { }
    }
#endif

    /// <summary>Czy tryb DEV RUN aktywnie nadpisuje progresję w bieżącym uruchomieniu.</summary>
    public static bool IsActive => IsEnabled;

    /// <summary>Umożliwia bezpieczne testowanie logiki DEV RUN bez zmiany EditorPrefs.</summary>
    public static void SetTestOverride(bool enabled, DevRunMode mode)
    {
        s_TestEnabledOverride = enabled;
        s_TestModeOverride = mode;
    }

    public static void ClearTestOverride()
    {
        s_TestEnabledOverride = null;
        s_TestModeOverride = null;
    }

    public static bool ShouldUnlockAllTowers() => ShouldUnlockAllTowers(Mode);

    public static bool ShouldUnlockAllTowers(DevRunMode mode)
    {
        return mode == DevRunMode.All || mode == DevRunMode.KeyNodes;
    }

    public static bool ShouldInjectDruidTowerUnlock(DevRunMode mode)
    {
        return mode == DevRunMode.All || mode == DevRunMode.KeyNodes;
    }

    /// <summary>
    /// Zwraca true, jeśli ulepszenie jest węzłem kluczowym (budynki, wieże, funkcje Kuźni Runicznej i dropy rzadkości run).
    /// </summary>
    public static bool IsKeyNode(MetaUpgradeSO upgrade)
    {
        if (ReferenceEquals(upgrade, null)) return false;

        switch (upgrade.effectType)
        {
            case MetaEffectType.UnlockTrinityForce:
            case MetaEffectType.RuneTowerLimit:
            case MetaEffectType.UnlockDruidTower:
            case MetaEffectType.TowerDraftUnlock:
            case MetaEffectType.UnlockRuneRarity_Uncommon:
            case MetaEffectType.UnlockRuneRarity_Rare:
            case MetaEffectType.UnlockRuneRarity_Legendary:
            case MetaEffectType.UnlockRuneRarity_Cursed:
            case MetaEffectType.UnlockRuneFusion:
            case MetaEffectType.UnlockRuneTransmutation:
            case MetaEffectType.UnlockCursedCrafting:
                return true;
        }

        if (!string.IsNullOrEmpty(upgrade.id) && upgrade.id.StartsWith("nowa_wie", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrEmpty(upgrade.upgradeName) && upgrade.upgradeName.StartsWith("Nowa wieża", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Zbiera wszystkie warianty B z podanej listy węzłów, aby w trybie All nie odblokowywać obu wykluczających się wariantów naraz.
    /// </summary>
    public static HashSet<MetaUpgradeSO> CollectSecondaryVariants(IEnumerable<MetaUpgradeSO> upgrades)
    {
        var secondary = new HashSet<MetaUpgradeSO>();
        if (upgrades == null) return secondary;

        foreach (var u in upgrades)
        {
            if (ReferenceEquals(u, null)) continue;
            if (u.hasVariants && !ReferenceEquals(u.variantB, null))
            {
                secondary.Add(u.variantB);
            }
        }
        return secondary;
    }

    public static bool ShouldUnlockUpgrade(MetaUpgradeSO upgrade, DevRunMode mode, HashSet<MetaUpgradeSO> secondaryVariants = null)
    {
        if (ReferenceEquals(upgrade, null)) return false;
        if (upgrade.IsPlaceholder) return false;

        switch (mode)
        {
            case DevRunMode.All:
                if (secondaryVariants != null && secondaryVariants.Contains(upgrade))
                    return false;
                // Fallback gdyby węzeł nadrzędny nie był w kolekcji, a nazwa/ID wskazywały na wariant B
                if (upgrade.isVariantOption && secondaryVariants == null &&
                    !string.IsNullOrEmpty(upgrade.id) && upgrade.id.EndsWith("_b", StringComparison.OrdinalIgnoreCase))
                    return false;
                return true;

            case DevRunMode.KeyNodes:
                return IsKeyNode(upgrade);

            case DevRunMode.None:
            default:
                return false;
        }
    }

    /// <summary>
    /// Aplikuje wybrany tryb DEV RUN do listy MetaUpgradeSO i zwraca listę odblokowanych ID.
    /// </summary>
    public static List<string> ApplyToUpgrades(IList<MetaUpgradeSO> upgrades, DevRunMode mode)
    {
        var unlockedIds = new List<string>();
        if (upgrades == null) return unlockedIds;

        var secondaryVariants = CollectSecondaryVariants(upgrades);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < upgrades.Count; i++)
        {
            MetaUpgradeSO upgrade = upgrades[i];
            if (ReferenceEquals(upgrade, null)) continue;

            bool unlock = ShouldUnlockUpgrade(upgrade, mode, secondaryVariants);
            upgrade.isUnlocked = unlock;

            if (unlock && !string.IsNullOrEmpty(upgrade.id) && seenIds.Add(upgrade.id))
            {
                unlockedIds.Add(upgrade.id);
            }

            if (upgrade.hasVariants)
            {
                if (!ReferenceEquals(upgrade.variantA, null))
                {
                    bool unlockA = ShouldUnlockUpgrade(upgrade.variantA, mode, secondaryVariants);
                    upgrade.variantA.isUnlocked = unlockA;
                    if (unlockA && !string.IsNullOrEmpty(upgrade.variantA.id) && seenIds.Add(upgrade.variantA.id))
                    {
                        unlockedIds.Add(upgrade.variantA.id);
                    }
                }
                if (!ReferenceEquals(upgrade.variantB, null))
                {
                    bool unlockB = ShouldUnlockUpgrade(upgrade.variantB, mode, secondaryVariants);
                    upgrade.variantB.isUnlocked = unlockB;
                    if (unlockB && !string.IsNullOrEmpty(upgrade.variantB.id) && seenIds.Add(upgrade.variantB.id))
                    {
                        unlockedIds.Add(upgrade.variantB.id);
                    }
                }
            }
        }

        return unlockedIds;
    }

    /// <summary>
    /// Aplikuje wybrany tryb DEV RUN do listy TowerData i zwraca listę odblokowanych ID wież.
    /// </summary>
    public static List<string> ApplyToTowers(IList<TowerData> towers, DevRunMode mode, IEnumerable<string> starterTowerIds)
    {
        bool unlockAll = ShouldUnlockAllTowers(mode);
        var starterSet = starterTowerIds != null
            ? new HashSet<string>(starterTowerIds, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var unlockedIds = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (towers == null || towers.Count == 0)
        {
            if (!unlockAll)
            {
                unlockedIds.AddRange(starterSet);
            }
            return unlockedIds;
        }

        for (int i = 0; i < towers.Count; i++)
        {
            TowerData tower = towers[i];
            if (ReferenceEquals(tower, null)) continue;

            string tName = null;
            try { tName = tower.name; } catch { }

            bool unlocked = unlockAll ||
                            (!string.IsNullOrEmpty(tName) && starterSet.Contains(tName)) ||
                            (!string.IsNullOrEmpty(tower.buildingName) && starterSet.Contains(tower.buildingName));

            tower.isUnlocked = unlocked;

            if (unlocked)
            {
                string id = !string.IsNullOrEmpty(tName) ? tName : tower.buildingName;
                if (!string.IsNullOrEmpty(id) && seen.Add(id))
                {
                    unlockedIds.Add(id);
                }
            }
        }

        if (!unlockAll)
        {
            foreach (string starter in starterSet)
            {
                if (seen.Add(starter))
                {
                    unlockedIds.Add(starter);
                }
            }
        }

        return unlockedIds;
    }

    /// <summary>
    /// Tworzy czysty obiekt SaveData w pamięci odpowiadający bieżącemu trybowi DEV RUN.
    /// </summary>
    public static SaveData BuildInMemorySaveData(IList<MetaUpgradeSO> upgrades, IList<TowerData> towers, IEnumerable<string> starterTowerIds, DevRunMode mode)
    {
        var data = new SaveData
        {
            totalHope = 0,
            unlockedUpgradeIDs = ApplyToUpgrades(upgrades, mode),
            upgradeInvestments = new List<UpgradeInvestmentData>(),
            unlockedTowerIDs = ApplyToTowers(towers, mode, starterTowerIds),
            isTutorialCompleted = mode != DevRunMode.None,
            bestNightsSurvived = 0,
            totalVictories = 0
        };
        return data;
    }
}
