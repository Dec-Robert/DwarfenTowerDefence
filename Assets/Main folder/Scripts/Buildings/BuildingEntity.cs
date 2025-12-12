using UnityEngine;
using System.Collections.Generic;

public class BuildingEntity : MonoBehaviour
{
    public BuildingData data;

    // Stan instancji
    public int currentTierIndex = 0;
    public bool isMaxLevel = false;

    // Lista zastosowanych ulepszeñ (¿eby móc je zsumowaæ)
    private List<BuildingUpgradeSO> appliedUpgrades = new List<BuildingUpgradeSO>();

    // Aktualne statystyki (Bazowe + Ulepszenia)
    private Dictionary<ResourceType, int> currentProduction = new Dictionary<ResourceType, int>();
    private Dictionary<ResourceType, int> currentUpkeep = new Dictionary<ResourceType, int>();

    public void Initialize(BuildingData _data)
    {
        data = _data;
        currentTierIndex = 0;
        appliedUpgrades.Clear();
        RecalculateStats();
    }

    public void ApplyUpgrade(BuildingUpgradeSO upgrade)
    {
        Debug.Log($"Zastosowano ulepszenie: {upgrade.upgradeName} na {gameObject.name}");

        // Dodajemy do listy posiadanych ulepszeñ
        appliedUpgrades.Add(upgrade);

        // Zwiêkszamy Tier
        currentTierIndex++;
        if (data.upgradeTiers != null && currentTierIndex >= data.upgradeTiers.Count)
        {
            isMaxLevel = true;
        }

        // Przeliczamy statystyki na nowo
        RecalculateStats();
    }

    // --- NAPRAWIONA METODA PRZELICZANIA ---
    private void RecalculateStats()
    {
        currentProduction.Clear();
        currentUpkeep.Clear();

        // 1. Wczytaj bazowe wartoœci z BuildingData
        if (data.productionPerCycle != null)
        {
            foreach (var item in data.productionPerCycle)
                AddToIntDict(currentProduction, item.type, item.amount);
        }

        if (data.upkeepPerCycle != null)
        {
            foreach (var item in data.upkeepPerCycle)
                AddToIntDict(currentUpkeep, item.type, item.amount);
        }

        // 2. Dodaj modyfikatory ze wszystkich ulepszeñ
        foreach (var upgrade in appliedUpgrades)
        {
            if (upgrade.productionModifier != null)
            {
                foreach (var mod in upgrade.productionModifier)
                    AddToIntDict(currentProduction, mod.type, mod.amount);
            }

            if (upgrade.upkeepModifier != null)
            {
                foreach (var mod in upgrade.upkeepModifier)
                    AddToIntDict(currentUpkeep, mod.type, mod.amount);
            }
        }
    }

    // Helper do bezpiecznego dodawania do s³ownika
    void AddToIntDict(Dictionary<ResourceType, int> dict, ResourceType type, int amount)
    {
        if (dict.ContainsKey(type)) dict[type] += amount;
        else dict[type] = amount;
    }

    // Metody publiczne dla UI
    public Dictionary<ResourceType, int> GetCurrentProduction()
    {
        return currentProduction;
    }

    public Dictionary<ResourceType, int> GetCurrentUpkeep()
    {
        return currentUpkeep;
    }
}