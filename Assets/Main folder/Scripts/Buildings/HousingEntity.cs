using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Budynek mieszkalny. Dziedziczy po BuildingEntity wyłącznie po to,
///     by żyć na mapie i mieć dane bazowe – NIE używa komponentów
///     Worker / Fuel / Production (nie ma zmian, pracowników ani paliwa).
///     Własna logika: pasywna produkcja per mieszkaniec + wzrost populacji,
///     odpalane raz dziennie przez OnDayChanged.
/// </summary>
public class HousingEntity : BuildingEntity
{
    [Header("Status Mieszkańców")] public List<Citizen> residents = new();

    public int currentGrowthProgress;

    [Header("Sterowanie Graczem")] public bool stopGrowth; // Czy gracz zatrzymał przyrost demograficzny?

    // Rzutowanie danych na typ Housing
    public HousingBuildingData housingData => data as HousingBuildingData;
    // =========================================================================
    // Cykl życia
    // =========================================================================

    protected override void OnEnable()
    {
        base.OnEnable(); // Rejestracja w AllBuildings
    }

    protected override void OnDisable()
    {
        base.OnDisable();
    }


    protected override void OnDestroy() // 'new' żeby nie ukryć base.OnDestroy przypadkowo
    {
        base.OnDestroy(); // Odpina OnHourTick / OnDayChanged z base + zwalnia Terrain

        if (TimeCycleManager.Instance != null)
            TimeCycleManager.Instance.OnDayChanged -= HandleMorningRoutine;
    }

    // Start() z BuildingEntity wywoła Initialize(data) – tego chcemy.
    // Nie nadpisujemy Start() – zamiast tego nadpisujemy Initialize().

    // =========================================================================
    // Inicjalizacja
    // =========================================================================

    // Wymagany dostęp do configu z poziomu całego systemu domów
    private CityBaseConfigSO GetConfig()
    {
        return ResourceManager.Instance?.cityConfig;
    }

    public override void Initialize(BuildingData buildingData)
    {
        base.Initialize(buildingData);

        if (housingData == null) return;

        // 1. OBLICZAMY POCZĄTKOWĄ POPULACJĘ (Baza + Meta)
        var baseStartPop = housingData.initialResidents;
        if (GetConfig() != null && GetConfig().overrideHousingData)
        {
            if (housingData.housingRace == Race.Humans) baseStartPop = GetConfig().humanStartPop;
            else if (housingData.housingRace == Race.Elves) baseStartPop = GetConfig().elfStartPop;
            else if (housingData.housingRace == Race.Dwarves) baseStartPop = GetConfig().dwarfStartPop;
        }

        var metaBonus = MetaUpgradeManager.Instance != null
            ? Mathf.RoundToInt(MetaUpgradeManager.Instance.GetValue(MetaEffectType.HousingStartPopulation))
            : 0;
        var finalStartPop = baseStartPop + metaBonus;

        // Spawn startowych mieszkańców
        for (var i = 0; i < finalStartPop; i++)
        {
            if (residents.Count >= GetEffectiveMaxResidents()) break;
            var newC = CitizenManager.Instance.SpawnNewCitizen(housingData.housingRace, this);
            residents.Add(newC);
        }

        if (TimeCycleManager.Instance != null)
            TimeCycleManager.Instance.OnDayChanged += HandleMorningRoutine;
    }

    // =========================================================================
    // Nadpisanie logiki godzinowej – domy jej nie mają
    // =========================================================================

    protected override void HandleHourlyProduction(int currentHour)
    {
        // Intentionally empty – domy działają tylko w cyklu dobowym.
    }

    // =========================================================================
    // Główna logika poranna
    // =========================================================================

    private void HandleMorningRoutine(int day)
    {
        var logChanges = new Dictionary<ResourceType, float>();

        // --- SPRAWDZANIE CZY DOM ZOSTAŁ "UŻYTY" ---
        if (!hasBeenUsed)
            foreach (var citizen in residents)
                // Jeśli chociaż 1 osoba poszła do pracy / została przypisana - dom traci 100% gwarancję zwrotu
                if (citizen.workState != WorkState.Idle)
                {
                    hasBeenUsed = true;
                    break;
                }
        // ------------------------------------------

        // A. Produkcja pasywna (na mieszkańca)
        ProducePerResident(logChanges);

        // B. Utrzymanie (Survival Cost)
        var survivalCost = CalculateSurvivalCost();
        var survivalPaid = ResourceManager.Instance.SpendResources(survivalCost);

        ShowLossFeedback(survivalCost, survivalPaid);

        // C. Logika wzrostu (Z uwzględnieniem Stop Growth!)
        if (!survivalPaid)
        {
            Debug.Log($"<color=red>Głód w {name}! Populacja stagnuje.</color>");
            if (currentGrowthProgress > 0) currentGrowthProgress--;
        }
        else if (!stopGrowth) // NOWE: Jeśli gracz nie zablokował wzrostu
        {
            TryGrow();
        }

        // Logowanie do ResourceLogger
        foreach (var cost in survivalCost)
            AddToDict(logChanges, cost.Key, -cost.Value);

        if (logChanges.Count > 0 && ResourceLogger.Instance != null)
            ResourceLogger.Instance.LogTransaction($"Bilans Poranny: {data.buildingName}", logChanges);

        RefreshUI();
    }

    // =========================================================================
    // Pomocnicze metody logiki mieszkaniowej
    // =========================================================================

    private void ProducePerResident(Dictionary<ResourceType, float> logChanges)
    {
        if (housingData.productionPerResident == null) return;

        foreach (var prod in housingData.productionPerResident)
        {
            var totalAmount = prod.amount * residents.Count;
            if (totalAmount <= 0f) continue;

            ResourceManager.Instance.AddResource(prod.type, totalAmount);
            AddToDict(logChanges, prod.type, totalAmount);

            if (FloatingTextManager.Instance != null)
                FloatingTextManager.Instance.ShowGain(
                    transform.position, prod.type.ToString(), Mathf.FloorToInt(totalAmount));
        }
    }

    private void ShowLossFeedback(Dictionary<ResourceType, float> costs, bool paid)
    {
        if (!paid || FloatingTextManager.Instance == null) return;

        foreach (var cost in costs)
            if (cost.Value >= 1f)
                FloatingTextManager.Instance.ShowLoss(
                    transform.position, cost.Key.ToString(), Mathf.FloorToInt(cost.Value));
    }

    private void TryGrow()
    {
        if (residents.Count >= GetEffectiveMaxResidents()) return;

        var growthCost = CalculateGrowthCost();
        if (!ResourceManager.Instance.SpendResources(growthCost)) return;

        currentGrowthProgress++;
        CheckForNewResident();
    }

    private void CheckForNewResident()
    {
        var required = CalculateRequiredGrowthTicks();
        if (currentGrowthProgress < required) return;

        var newC = CitizenManager.Instance.SpawnNewCitizen(housingData.housingRace, this);
        residents.Add(newC);
        currentGrowthProgress = 0;
    }

    // =========================================================================
    // Efektywny limit mieszkańców (bazowy + meta upgrade)
    // =========================================================================

    /// <summary>
    ///     Zwraca rzeczywisty limit mieszkańców uwzględniający meta-upgrady.
    ///     Sprawdza bonus ogólny (HousingMaxResidents) oraz bonus per-rasa.
    /// </summary>
    public int GetEffectiveMaxResidents()
    {
        var baseMaxPop = housingData.maxResidents;

        // Zastąpienie wartości przez globalny Config
        if (GetConfig() != null && GetConfig().overrideHousingData)
        {
            if (housingData.housingRace == Race.Humans) baseMaxPop = GetConfig().humanMaxPop;
            else if (housingData.housingRace == Race.Elves) baseMaxPop = GetConfig().elfMaxPop;
            else if (housingData.housingRace == Race.Dwarves) baseMaxPop = GetConfig().dwarfMaxPop;
        }

        var bonus = 0;

        if (MetaUpgradeManager.Instance != null)
        {
            // Bonus ogólny 
            bonus += Mathf.RoundToInt(MetaUpgradeManager.Instance.GetValue(MetaEffectType.HousingMaxResidents));

            // Bonus per rasa 
            var raceEffect = housingData.housingRace switch
            {
                Race.Humans => MetaEffectType.HousingMaxResidents_Humans,
                Race.Elves => MetaEffectType.HousingMaxResidents_Elves,
                Race.Dwarves => MetaEffectType.HousingMaxResidents_Dwarves,
                _ => MetaEffectType.None
            };

            if (raceEffect != MetaEffectType.None)
                bonus += Mathf.RoundToInt(MetaUpgradeManager.Instance.GetValue(raceEffect));
        }

        return baseMaxPop + bonus;
    }

    // =========================================================================
    // Matematyka
    // =========================================================================

    public int CalculateRequiredGrowthTicks()
    {
        return Mathf.FloorToInt(housingData.baseGrowthTicks + residents.Count * housingData.growthDifficultyMultiplier);
    }

    private Dictionary<ResourceType, float> CalculateSurvivalCost()
    {
        var total = new Dictionary<ResourceType, float>();
        foreach (var c in housingData.baseDailyUpkeep) AddToDict(total, c.type, c.amount);
        foreach (var c in housingData.upkeepPerResident) AddToDict(total, c.type, c.amount * residents.Count);
        return total;
    }

    private Dictionary<ResourceType, float> CalculateGrowthCost()
    {
        var total = new Dictionary<ResourceType, float>();
        foreach (var c in housingData.growthSurplusCost) AddToDict(total, c.type, c.amount);
        return total;
    }

    private void AddToDict(Dictionary<ResourceType, float> d, ResourceType t, float a)
    {
        if (d.ContainsKey(t)) d[t] += a;
        else d.Add(t, a);
    }

    private void RefreshUI()
    {

    }

    // =========================================================================
    // API dla UI
    // =========================================================================

    public float GetGrowthProgress()
    {
        if (residents.Count >= GetEffectiveMaxResidents()) return 1f;
        var required = CalculateRequiredGrowthTicks();
        return required == 0 ? 1f : (float)currentGrowthProgress / required;
    }

    public int GetDaysRemaining()
    {
        if (residents.Count >= GetEffectiveMaxResidents()) return 0;
        return Mathf.Max(0, CalculateRequiredGrowthTicks() - currentGrowthProgress);
    }

    public float GetProjectedUpkeep()
    {
        var total = 0f;
        foreach (var cost in housingData.baseDailyUpkeep) total += cost.amount;
        foreach (var cost in housingData.upkeepPerResident) total += cost.amount * residents.Count;
        return total;
    }

    public string GetGrowthStatus()
    {
        if (residents.Count >= GetEffectiveMaxResidents()) return "PEŁNY";

        // NOWE:
        if (stopGrowth) return "WSTRZYMANY";

        foreach (var kvp in CalculateSurvivalCost())
            if (!ResourceManager.Instance.CanAfford(kvp.Key, kvp.Value))
                return "BRAK ZASOBÓW";

        return "ROSNĄCY";
    }
}