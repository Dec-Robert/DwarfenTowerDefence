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

        // --- NOWE: TANKOWANIE NA POCZĄTKU ZMIANY ---
        // Sprawdzamy czy to dokładny start zmiany
        if (currentHour == shiftStartHour)
        {
            fuelComponent.RefillForShift();
        }
        // ------------------------------------------

        workerComponent.ActivateWorkersForShift();
        var activeWorkers = workerComponent.GetActiveWorkers();
        int workerCount = activeWorkers.Count;
        
        if (workerCount == 0) return false;

        // ── 1. ZAAWANSOWANA KALKULACJA WYDAJNOŚCI (Na podstawie Twoich notatek) ──
        float efficiency = CalculateDynamicEfficiency(workerCount);

        // ── 2. EKSPLOZJA KOPALNI (RYZYKO) ──
        if (upgradeComponent.HasSpecialEffect("MINE_EXPLOSION_RISK"))
        {
            // Niewielka szansa na wybuch każdej godziny pracy (np. 0.5% co godzinę)
            if (Random.value < 0.005f) 
            {
                Debug.LogWarning($"<color=red>KATASTROFA! Kopalnia {data.buildingName} zawaliła się!</color>");
                // Zabijamy pracowników
                foreach(var w in activeWorkers)
                {
                    CitizenManager.Instance.citizens.Remove(w); // Śmierć
                }
                // Niszczymy budynek (Wymaga referencji do entity w tym komponencie, użyjemy buildingTransform)
                var entity = buildingTransform.GetComponent<BuildingEntity>();
                if (entity != null) entity.Demolish(); 
                return false; 
            }
        }

        // ── 3. SPRAWDZENIE PALIWA ──
        var upkeep = fuelComponent.GetCurrentUpkeep();
        if (!fuelComponent.HasFuelForHour(upkeep, efficiency)) return false;
        fuelComponent.ConsumeFuelForHour(upkeep, efficiency);

        float beaconBonus = GlobalModifierRegistry.Instance != null ? GlobalModifierRegistry.Instance.GetGlobalProductionMultiplier() : 1f;
        var production = GetCurrentProduction();
        var loggedProduction = new Dictionary<ResourceType, float>();

        // ── 4. PRODUKCJA WŁAŚCIWA ──
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
                if (amountToGive > 0) AddToDict(loggedProduction, kvp.Key, amountToGive);

                if (FloatingTextManager.Instance != null)
                    FloatingTextManager.Instance.ShowGain(buildingTransform.position, kvp.Key.ToString(), amountToGive);
            }
        }

        // ── 5. DROP RUN Z ODPADÓW (Ulepszenie Kopalni T3) ──
        if (upgradeComponent.HasSpecialEffect("MINE_RUNE_DROP"))
        {
            // Pod koniec zmiany szansa na znalezienie runy (np. 15% na koniec dnia)
            if (currentHour == shiftEndHour - 1 && Random.value < 0.15f)
            {
                if (RuneManager.Instance != null)
                {
                    var r = RuneManager.Instance.GenerateRandomRune();
                    RuneManager.Instance.playerRunes.Add(r);
                    RuneManager.Instance.NotifyInventoryChanged();
                    Debug.Log($"<color=magenta>Górnicy znaleźli runę w odpadach: {r.definition.runeName}!</color>");
                }
            }
        }

        if (loggedProduction.Count > 0 && ResourceLogger.Instance != null)
            ResourceLogger.Instance.LogTransaction($"Produkcja: {data.buildingName}", loggedProduction);

        if (currentHour == shiftEndHour - 1)
            workerComponent.ExhaustWorkers();

        return true;
    }

    // Nowa potężna metoda uelastyczniająca wydajność
    private float CalculateDynamicEfficiency(int workerCount)
    {
        float scale = data.workerScalingFactor;
        float eff = 1f;
        float firstWorkerEff = data.firstWorkerProduction;

        // SPRAWDZAMY ULEPSZENIA:

        // Farma Tier 1: 1 pracownik daje od razu 80% (zamiast standardu)
        if (upgradeComponent.HasSpecialEffect("FARM_80_PERCENT_START") && workerCount == 1)
        {
            return 0.9f; 
        }
        
        // Kopalnia Wiertła Parowe: 1 pracownik = 100%, 
        if (upgradeComponent.HasSpecialEffect("MINE_ONE_WORKER_100"))
        {
            eff = 1.0f + ((workerCount - 1) * scale);
            return eff;
        }

        // Farma Tier 3 (Full Shift Bonus) lub Kopalnia Jądro Planety
        if (upgradeComponent.HasSpecialEffect("FULL_SHIFT_MEGA_BONUS"))
        {
            int maxWorkers = workerComponent.GetMaxWorkersPerShift();
            eff = 1f + ((workerCount - 1) * scale);
            
            if (workerCount >= maxWorkers) 
            {
                eff *= 2.0f; // Potężny x2 mnożnik za pełną zmianę
            }
            return eff;
        }

        if (upgradeComponent.HasSpecialEffect("SAWMILL_ONE_WORKER_100_NEXT_15"))
        {
            return 1.0f + ((workerCount - 1) * ( scale + 0.15f));
        }

        return workerCount > 1 ? 1f + ((workerCount - 1) * scale) : firstWorkerEff;
    }

    /// <summary>
    /// Oblicza bieżącą produkcję per cykl (baza + ulepszenia + teren).
    /// Używane też przez UI do podglądu.
    /// </summary>
    /// <summary>
    /// Oblicza bieżącą produkcję per cykl (baza + ulepszenia + teren + meta progresja specyficzna).
    /// </summary>
    public Dictionary<ResourceType, float> GetCurrentProduction()
    {
        var total = new Dictionary<ResourceType, float>();

        // 1. Baza z BuildingData
        if (data.productionPerCycle != null)
            foreach (var res in data.productionPerCycle)
                AddToDict(total, res.type, res.amount);

        // 2. Bonusy z ulepszeń w grze (Tier 1 -> Tier 2)
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

        // 4. NOWE: Bonus ze specyficznej Meta-Progresji (tylko dla tego konkretnego budynku)
        if (MetaUpgradeManager.Instance != null && data.productionPerCycle != null)
        {
            // Pobieramy wartość. Zwraca np. 0.15 (jeśli daliśmy 15% boosta do Tartaku)
            float metaSpecificBonus = MetaUpgradeManager.Instance.GetBuildingValue(MetaEffectType.SpecificBuildingProductionBonus, data);
            
            if (metaSpecificBonus > 0f)
            {
                // Aplikujemy bonus (mnożnik) do KAŻDEGO bazowego surowca produkowanego przez ten budynek
                foreach (var res in data.productionPerCycle)
                {
                    float extraAmount = res.amount * metaSpecificBonus;
                    AddToDict(total, res.type, extraAmount);
                }
            }
        }

        float globalUpgradeMulti = upgradeComponent.GetGlobalProductionMultiplier();
        if (globalUpgradeMulti != 1f)
        {
            var keys = new List<ResourceType>(total.Keys);
            foreach (var k in keys) total[k] *= globalUpgradeMulti;
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