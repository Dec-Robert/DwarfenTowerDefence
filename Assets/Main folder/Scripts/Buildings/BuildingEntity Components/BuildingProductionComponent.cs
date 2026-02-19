using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Odpowiada za całą logikę godzinowej produkcji zasobów:
/// obliczanie wydajności, pobieranie paliwa, akumulowanie bufora
/// i przekazywanie gotowych jednostek do ResourceManager.
/// Trzymana jako pole w BuildingEntity (zwykła klasa C#, nie MonoBehaviour).
/// </summary>
public class BuildingProductionComponent
{
    // --- Referencje ---
    private readonly BuildingData data;
    private readonly BuildingUpgradeComponent upgradeComponent;
    private readonly BuildingTerrainComponent terrainComponent;
    private readonly BuildingFuelComponent fuelComponent;
    private readonly BuildingWorkerComponent workerComponent;
    private readonly Transform buildingTransform;

    // --- Stan ---
    private readonly Dictionary<ResourceType, float> productionBuffer =
        new Dictionary<ResourceType, float>();

    // Czas trwania bieżącej zmiany (obliczany przez BuildingEntity)
    public float CurrentShiftLength { get; set; } = 8f;

    // --- Konstruktor ---
    public BuildingProductionComponent(
        BuildingData data,
        BuildingUpgradeComponent upgradeComponent,
        BuildingTerrainComponent terrainComponent,
        BuildingFuelComponent fuelComponent,
        BuildingWorkerComponent workerComponent,
        Transform buildingTransform)
    {
        this.data = data;
        this.upgradeComponent = upgradeComponent;
        this.terrainComponent = terrainComponent;
        this.fuelComponent = fuelComponent;
        this.workerComponent = workerComponent;
        this.buildingTransform = buildingTransform;
    }

    // --- API Publiczne ---

    /// <summary>
    /// Główna metoda wywoływana co godzinę przez BuildingEntity.
    /// Zwraca true jeśli produkcja odbyła się pomyślnie.
    /// </summary>
    public bool ProcessHour(int currentHour, int shiftStartHour)
    {
        int shiftEndHour = shiftStartHour + (int)CurrentShiftLength;
        bool isWorkingHours = currentHour >= shiftStartHour && currentHour < shiftEndHour;
        if (!isWorkingHours) return false;

        // Aktywacja pracowników na początku zmiany
        workerComponent.ActivateWorkersForShift();

        var activeWorkers = workerComponent.GetActiveWorkers();
        int workerCount = activeWorkers.Count;
        if (workerCount == 0) return false;

        // Wydajność skaluje się z liczbą pracowników
        float scale = data.workerScalingFactor > 0 ? data.workerScalingFactor : 0.1f;
        float efficiency = 1f + (workerCount - 1) * scale;

        // Sprawdzenie paliwa
        var upkeep = fuelComponent.GetCurrentUpkeep();
        if (!fuelComponent.HasFuelForHour(upkeep, efficiency))
            return false;

        // Pobranie paliwa i produkcja
        fuelComponent.ConsumeFuelForHour(upkeep, efficiency);

        float beaconBonus = GlobalModifierRegistry.Instance != null
            ? GlobalModifierRegistry.Instance.GetGlobalProductionMultiplier()
            : 1f;

        var production = GetCurrentProduction();
        var loggedProduction = new Dictionary<ResourceType, float>();

        foreach (var kvp in production)
        {
            float hourlyAmount = (kvp.Value / CurrentShiftLength) * efficiency * beaconBonus;

            if (!productionBuffer.ContainsKey(kvp.Key)) productionBuffer[kvp.Key] = 0f;
            productionBuffer[kvp.Key] += hourlyAmount;

            if (productionBuffer[kvp.Key] >= 1f)
            {
                int amountToGive = Mathf.FloorToInt(productionBuffer[kvp.Key]);
                productionBuffer[kvp.Key] -= amountToGive;

                ResourceManager.Instance.AddResource(kvp.Key, amountToGive);

                if (amountToGive > 0)
                    AddToDict(loggedProduction, kvp.Key, amountToGive);

                if (FloatingTextManager.Instance != null)
                    FloatingTextManager.Instance.ShowGain(
                        buildingTransform.position, kvp.Key.ToString(), amountToGive);
            }
        }

        // Logowanie
        if (loggedProduction.Count > 0 && ResourceLogger.Instance != null)
            ResourceLogger.Instance.LogTransaction($"Produkcja: {data.buildingName}", loggedProduction);

        // Wyczerpanie pracowników na końcu zmiany
        if (currentHour == shiftEndHour - 1)
            workerComponent.ExhaustWorkers();

        return true;
    }

    /// <summary>
    /// Oblicza bieżącą produkcję per cykl (baza + ulepszenia + teren).
    /// Używane też przez UI do podglądu.
    /// </summary>
    public Dictionary<ResourceType, float> GetCurrentProduction()
    {
        var total = new Dictionary<ResourceType, float>();

        // 1. Baza z BuildingData
        if (data.productionPerCycle != null)
            foreach (var res in data.productionPerCycle)
                AddToDict(total, res.type, res.amount);

        // 2. Bonusy z ulepszeń
        var upgradeBonus = upgradeComponent.GetAllProductionBonuses();
        foreach (var kvp in upgradeBonus)
            AddToDict(total, kvp.Key, kvp.Value);

        // 3. Bonus terenowy doliczany do głównego surowca
        float terrainBonus = terrainComponent.CalculateTerrainBonus();
        if (terrainBonus > 0f
            && data.productionPerCycle != null
            && data.productionPerCycle.Count > 0)
        {
            ResourceType mainRes = data.productionPerCycle[0].type;
            AddToDict(total, mainRes, terrainBonus);
        }

        return total;
    }

    /// <summary>Czyści bufor produkcji (np. przy Initialize).</summary>
    public void ResetBuffer() => productionBuffer.Clear();

    // --- Helpers ---

    private void AddToDict(Dictionary<ResourceType, float> dict, ResourceType type, float amount)
    {
        if (dict.ContainsKey(type)) dict[type] += amount;
        else dict.Add(type, amount);
    }
}