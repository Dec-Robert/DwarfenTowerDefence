using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///     Odpowiada za całą logikę ulepszeń budynku:
///     aktualny tier, zastosowane ulepszenia oraz lokalne bonusy wynikające z nich.
///     Trzymana jako pole w BuildingEntity (zwykła klasa C#, nie MonoBehaviour).
/// </summary>
public class BuildingUpgradeComponent
{
    // --- Referencje ---
    private readonly BuildingData data;

    // --- Konstruktor ---
    public BuildingUpgradeComponent(BuildingData data)
    {
        this.data = data;
    }

    // --- Stan ---
    public int CurrentTier { get; private set; }
    public List<BuildingUpgradeSO> AppliedUpgrades { get; } = new();
    

    /// <summary>Suma dodatkowych pracowników na zmianę z zastosowanych ulepszeń.</summary>
    public int LocalBonusWorkers { get; private set; }

    // --- Zdarzenie – subskrybuje BuildingEntity i inne komponenty ---
    public event Action OnUpgradeApplied;

    // --- API Publiczne ---

    /// <summary>Resetuje stan ulepszeń (np. przy Initialize).</summary>
    public void Reset()
    {
        CurrentTier = 0;
        AppliedUpgrades.Clear();
        LocalBonusWorkers = 0;
    }

    /// <summary>
    ///     Stosuje wybrane ulepszenie: zwiększa tier, rejestruje bonusy
    ///     i powiadamia subskrybentów przez zdarzenie.
    /// </summary>
    public void ApplyUpgrade(BuildingUpgradeSO upgrade)
    {
        if (upgrade == null) return;

        AppliedUpgrades.Add(upgrade);
        CurrentTier++;
        LocalBonusWorkers += upgrade.extraWorkersPerShift;

        OnUpgradeApplied?.Invoke();
    }

    /// <summary>
    ///     Zwraca listę ulepszeń dostępnych do wybrania w bieżącym tierze.
    ///     Tier 0 → opcje startowe z BuildingData.
    ///     Tier 1+ → opcje z ostatniego zastosowanego ulepszenia.
    /// </summary>
    public List<BuildingUpgradeSO> GetAvailableUpgrades()
    {
        if (CurrentTier == 0 && data.tier1Upgrades != null)
            return data.tier1Upgrades;

        if (AppliedUpgrades.Count > 0)
        {
            var lastUpgrade = AppliedUpgrades[AppliedUpgrades.Count - 1];
            if (lastUpgrade.nextTierOptions != null)
                return lastUpgrade.nextTierOptions;
        }

        return new List<BuildingUpgradeSO>();
    }

    /// <summary>Sprawdza, czy dane ulepszenie zostało już zastosowane.</summary>
    public bool HasUpgrade(BuildingUpgradeSO upgrade)
    {
        return AppliedUpgrades.Contains(upgrade);
    }

    /// <summary>Zbiera bonusy produkcji ze wszystkich zastosowanych ulepszeń.</summary>
    public Dictionary<ResourceType, float> GetAllProductionBonuses()
    {
        var result = new Dictionary<ResourceType, float>();

        foreach (var upgrade in AppliedUpgrades)
        {
            if (upgrade.productionBonus == null) continue;
            foreach (var res in upgrade.productionBonus)
                AddToDict(result, res.type, res.amount);
        }

        return result;
    }

    /// <summary>Zbiera wzrosty upkeep ze wszystkich zastosowanych ulepszeń.</summary>
    public Dictionary<ResourceType, float> GetAllUpkeepIncreases()
    {
        var result = new Dictionary<ResourceType, float>();

        foreach (var upgrade in AppliedUpgrades)
        {
            if (upgrade.upkeepIncrease == null) continue;
            foreach (var res in upgrade.upkeepIncrease)
                AddToDict(result, res.type, res.amount);
        }

        return result;
    }

    // --- Helpers ---

    private void AddToDict(Dictionary<ResourceType, float> dict, ResourceType type, float amount)
    {
        if (dict.ContainsKey(type)) dict[type] += amount;
        else dict.Add(type, amount);
    }

    /// <summary>
    ///     Sprawdza, czy budynek posiada ulepszenie z danym specjalnym ID.
    ///     Używane do aktywowania unikalnych mechanik (np. "MINE_EXPLOSION").
    /// </summary>
    public bool HasSpecialEffect(string effectID)
    {
        // Sprawdzamy wszystkie zastosowane ulepszenia, a w nich listę ich tagów
        return AppliedUpgrades.Exists(u => u.specialEffectIDs != null && u.specialEffectIDs.Contains(effectID));
    }

    /// <summary>
    ///     Zwraca zsumowaną wartość konkretnego atrybutu ukrytą w ulepszeniach.
    ///     (Przydatne, gdy zrobimy listę modyfikatorów w przyszłości).
    /// </summary>
    public int CountSpecialEffects(string effectID)
    {
        return AppliedUpgrades.Count(u => u.specialEffectIDs != null && u.specialEffectIDs.Contains(effectID));
    }

    /// <summary>Zbiera wszystkie ulepszenia procentowe do terenu i zwraca finalny mnożnik (np. 1.15).</summary>
    public float GetTotalTerrainMultiplier()
    {
        var totalPercent = 0f;
        foreach (var upgrade in AppliedUpgrades) totalPercent += upgrade.terrainPercentBonus;
        return 1f + totalPercent; // 0.15 -> 1.15
    }

    /// <summary>Zbiera wszystkie płaskie bonusy do terenu.</summary>
    public float GetTotalTerrainFlatBonus()
    {
        var totalFlat = 0f;
        foreach (var upgrade in AppliedUpgrades) totalFlat += upgrade.terrainFlatBonus;
        return totalFlat;
    }

    public float GetGlobalProductionMultiplier()
    {
        var total = 0f;
        foreach (var u in AppliedUpgrades) total += u.globalProductionMultiplierBonus;
        return 1f + total;
    }

    public float GetGlobalUpkeepMultiplier()
    {
        var total = 0f;
        foreach (var u in AppliedUpgrades) total += u.globalUpkeepMultiplierBonus;
        return 1f + total;
    }
}