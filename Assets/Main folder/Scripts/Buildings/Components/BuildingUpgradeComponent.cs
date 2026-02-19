using System.Collections.Generic;

/// <summary>
/// Odpowiada za całą logikę ulepszeń budynku:
/// aktualny tier, zastosowane ulepszenia oraz lokalne bonusy wynikające z nich.
/// Trzymana jako pole w BuildingEntity (zwykła klasa C#, nie MonoBehaviour).
/// </summary>
public class BuildingUpgradeComponent
{
    // --- Referencje ---
    private readonly BuildingData data;

    // --- Stan ---
    public int CurrentTier { get; private set; } = 0;
    public List<BuildingUpgradeSO> AppliedUpgrades { get; private set; } = new List<BuildingUpgradeSO>();

    /// <summary>Suma dodatkowych zmian z zastosowanych ulepszeń.</summary>
    public int LocalBonusShifts { get; private set; } = 0;

    /// <summary>Suma dodatkowych pracowników na zmianę z zastosowanych ulepszeń.</summary>
    public int LocalBonusWorkers { get; private set; } = 0;

    // --- Zdarzenie – subskrybuje BuildingEntity i inne komponenty ---
    public event System.Action OnUpgradeApplied;

    // --- Konstruktor ---
    public BuildingUpgradeComponent(BuildingData data)
    {
        this.data = data;
    }

    // --- API Publiczne ---

    /// <summary>Resetuje stan ulepszeń (np. przy Initialize).</summary>
    public void Reset()
    {
        CurrentTier = 0;
        AppliedUpgrades.Clear();
        LocalBonusShifts = 0;
        LocalBonusWorkers = 0;
    }

    /// <summary>
    /// Stosuje wybrane ulepszenie: zwiększa tier, rejestruje bonusy
    /// i powiadamia subskrybentów przez zdarzenie.
    /// </summary>
    public void ApplyUpgrade(BuildingUpgradeSO upgrade)
    {
        if (upgrade == null) return;

        AppliedUpgrades.Add(upgrade);
        CurrentTier++;
        LocalBonusShifts += upgrade.extraShifts;
        LocalBonusWorkers += upgrade.extraWorkersPerShift;

        OnUpgradeApplied?.Invoke();
    }

    /// <summary>
    /// Zwraca listę ulepszeń dostępnych do wybrania w bieżącym tierze.
    /// Tier 0 → opcje startowe z BuildingData.
    /// Tier 1+ → opcje z ostatniego zastosowanego ulepszenia.
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
    public bool HasUpgrade(BuildingUpgradeSO upgrade) =>
        AppliedUpgrades.Contains(upgrade);

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
}
