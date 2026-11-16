using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
    private readonly List<BuildingUpgradeSO> localAppliedUpgrades = new();

    public int CurrentTier
    {
        get
        {
            var manager = ToolWorkshopResearchManager.Instance;
            int globalTier = manager != null && data != null ? manager.GetUnlockedTierCount(data) : 0;
            return Mathf.Max(globalTier, localAppliedUpgrades.Count);
        }
    }

    public List<BuildingUpgradeSO> AppliedUpgrades => GetEffectiveUpgrades();

    /// <summary>Suma dodatkowych pracowników na zmianę z zastosowanych ulepszeń.</summary>
    public int LocalBonusWorkers
    {
        get
        {
            int total = 0;
            var upgrades = GetEffectiveUpgrades();
            for (int i = 0; i < upgrades.Count; i++)
            {
                if (upgrades[i] != null) total += upgrades[i].extraWorkersPerShift;
            }
            return total;
        }
    }

    // --- Zdarzenie – subskrybuje BuildingEntity i inne komponenty ---
    public event Action OnUpgradeApplied;

    // --- API Publiczne ---

    /// <summary>Resetuje lokalny stan ulepszeń (np. przy Initialize).</summary>
    public void Reset()
    {
        localAppliedUpgrades.Clear();
    }

    public void NotifyExternalUpgradesChanged()
    {
        OnUpgradeApplied?.Invoke();
    }

    /// <summary>
    ///     Stosuje wybrane ulepszenie lokalnie (np. w testach) i powiadamia subskrybentów.
    /// </summary>
    public void ApplyUpgrade(BuildingUpgradeSO upgrade)
    {
        if (upgrade == null || localAppliedUpgrades.Contains(upgrade)) return;

        localAppliedUpgrades.Add(upgrade);
        OnUpgradeApplied?.Invoke();
    }

    public List<BuildingUpgradeSO> GetEffectiveUpgrades()
    {
        var result = new List<BuildingUpgradeSO>();
        for (int i = 0; i < localAppliedUpgrades.Count; i++)
        {
            if (localAppliedUpgrades[i] != null && !result.Contains(localAppliedUpgrades[i]))
                result.Add(localAppliedUpgrades[i]);
        }

        var manager = ToolWorkshopResearchManager.Instance;
        if (manager != null && data != null)
        {
            var globalList = manager.GetUnlockedUpgrades(data);
            for (int i = 0; i < globalList.Count; i++)
            {
                if (globalList[i] != null && !result.Contains(globalList[i]))
                    result.Add(globalList[i]);
            }
        }

        return result;
    }

    /// <summary>
    ///     Zwraca listę ulepszeń dostępnych do wybrania w bieżącym tierze (zachowane dla kompatybilności).
    /// </summary>
    public List<BuildingUpgradeSO> GetAvailableUpgrades()
    {
        if (CurrentTier == 0 && data != null && data.tier1Upgrades != null)
            return data.tier1Upgrades;

        var effective = GetEffectiveUpgrades();
        if (effective.Count > 0)
        {
            var lastUpgrade = effective[effective.Count - 1];
            if (lastUpgrade.nextTierOptions != null)
                return lastUpgrade.nextTierOptions;
        }

        return new List<BuildingUpgradeSO>();
    }

    /// <summary>Sprawdza, czy dane ulepszenie zostało już zastosowane.</summary>
    public bool HasUpgrade(BuildingUpgradeSO upgrade)
    {
        return upgrade != null && GetEffectiveUpgrades().Contains(upgrade);
    }

    /// <summary>Zbiera bonusy produkcji ze wszystkich zastosowanych ulepszeń.</summary>
    public Dictionary<ResourceType, float> GetAllProductionBonuses()
    {
        var result = new Dictionary<ResourceType, float>();
        var upgrades = GetEffectiveUpgrades();

        foreach (var upgrade in upgrades)
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
        var upgrades = GetEffectiveUpgrades();

        foreach (var upgrade in upgrades)
        {
            if (upgrade.upkeepIncrease == null) continue;
            foreach (var res in upgrade.upkeepIncrease)
                AddToDict(result, res.type, res.amount);
        }

        return result;
    }

    /// <summary>Zbiera redukcje upkeep ze wszystkich zastosowanych ulepszeń.</summary>
    public Dictionary<ResourceType, float> GetAllUpkeepReductions()
    {
        var result = new Dictionary<ResourceType, float>();
        var upgrades = GetEffectiveUpgrades();

        foreach (var upgrade in upgrades)
        {
            if (upgrade.upkeepReduction == null) continue;
            foreach (var res in upgrade.upkeepReduction)
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
    /// </summary>
    public bool HasSpecialEffect(string effectID)
    {
        if (string.IsNullOrEmpty(effectID)) return false;
        var upgrades = GetEffectiveUpgrades();
        return upgrades.Exists(u => u.specialEffectIDs != null && u.specialEffectIDs.Contains(effectID));
    }

    /// <summary>
    ///     Zwraca zsumowaną liczbę wystąpień konkretnego specjalnego ID.
    /// </summary>
    public int CountSpecialEffects(string effectID)
    {
        if (string.IsNullOrEmpty(effectID)) return 0;
        var upgrades = GetEffectiveUpgrades();
        return upgrades.Count(u => u.specialEffectIDs != null && u.specialEffectIDs.Contains(effectID));
    }

    /// <summary>Zbiera wszystkie ulepszenia procentowe do terenu i zwraca finalny mnożnik (np. 1.15).</summary>
    public float GetTotalTerrainMultiplier()
    {
        var totalPercent = 0f;
        var upgrades = GetEffectiveUpgrades();
        foreach (var upgrade in upgrades) totalPercent += upgrade.terrainPercentBonus;
        return 1f + totalPercent;
    }

    /// <summary>Zbiera wszystkie płaskie bonusy do terenu.</summary>
    public float GetTotalTerrainFlatBonus()
    {
        var totalFlat = 0f;
        var upgrades = GetEffectiveUpgrades();
        foreach (var upgrade in upgrades) totalFlat += upgrade.terrainFlatBonus;
        return totalFlat;
    }

    public float GetGlobalProductionMultiplier()
    {
        var total = 0f;
        var upgrades = GetEffectiveUpgrades();
        foreach (var u in upgrades) total += u.globalProductionMultiplierBonus;
        return 1f + total;
    }

    public float GetGlobalUpkeepMultiplier()
    {
        var total = 0f;
        var upgrades = GetEffectiveUpgrades();
        foreach (var u in upgrades) total += u.globalUpkeepMultiplierBonus;
        return Mathf.Max(0f, 1f + total);
    }

    public int GetExtraSearchRadius()
    {
        int total = 0;
        var upgrades = GetEffectiveUpgrades();
        foreach (var u in upgrades) total += u.extraSearchRadius;
        if (HasSpecialEffect("INCREASE_RANGE_1")) total += 1;
        if (HasSpecialEffect("INCREASE_RANGE_2")) total += 1;
        return total;
    }

    public float GetFirstWorkerBonus()
    {
        float total = 0f;
        var upgrades = GetEffectiveUpgrades();
        foreach (var u in upgrades) total += u.firstWorkerBonusMultiplier;
        return total;
    }

    public float GetPassiveNoWorkerEfficiency()
    {
        float total = 0f;
        var upgrades = GetEffectiveUpgrades();
        foreach (var u in upgrades) total += u.passiveNoWorkerEfficiency;
        return Mathf.Max(0f, total);
    }
}