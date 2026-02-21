using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Budynek mieszkalny. Dziedziczy po BuildingEntity wyłącznie po to,
/// by żyć na mapie i mieć dane bazowe – NIE używa komponentów
/// Worker / Fuel / Production (nie ma zmian, pracowników ani paliwa).
///
/// Własna logika: pasywna produkcja per mieszkaniec + wzrost populacji,
/// odpalane raz dziennie przez OnDayChanged.
/// </summary>
public class HousingEntity : BuildingEntity
{
    // Rzutowanie danych na typ Housing
    public HousingBuildingData housingData => data as HousingBuildingData;

    [Header("Status Mieszkańców")]
    public List<Citizen> residents = new List<Citizen>();

    public int currentGrowthProgress = 0;

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

    // Start() z BuildingEntity wywoła Initialize(data) – tego chcemy.
    // Nie nadpisujemy Start() – zamiast tego nadpisujemy Initialize().

    // =========================================================================
    // Inicjalizacja
    // =========================================================================

    public override void Initialize(BuildingData buildingData)
    {
        // Tworzy komponenty (Workers, Fuel, Production itd.),
        // ale dla domu nie będą używane – to dopuszczalne,
        // koszt ich stworzenia jest pomijalny.
        base.Initialize(buildingData);

        if (housingData == null)
        {
            Debug.LogError($"[HousingEntity] Złe dane przypisane do {name}!");
            return;
        }

        // Spawn startowych mieszkańców
        for (int i = 0; i < housingData.initialResidents; i++)
        {
            if (residents.Count >= housingData.maxResidents) break;

            Citizen newC = CitizenManager.Instance.SpawnNewCitizen(housingData.housingRace, this);
            residents.Add(newC);
        }
        Debug.Log($"[{name}] Zainicjalizowano {housingData.housingRace} dom z {residents.Count} mieszkańcami (powinno być {housingData.initialResidents})");

        // Subskrybujemy własny handler poranny (base.Initialize już podpiął OnHourTick i OnDayChanged,
        // ale HandleHourlyProduction jest nadpisana jako pusta – patrz niżej)
        if (TimeCycleManager.Instance != null)
            TimeCycleManager.Instance.OnDayChanged += HandleMorningRoutine;
    }

    protected override void OnDestroy()  // 'new' żeby nie ukryć base.OnDestroy przypadkowo
    {
        base.OnDestroy(); // Odpina OnHourTick / OnDayChanged z base + zwalnia Terrain

        if (TimeCycleManager.Instance != null)
            TimeCycleManager.Instance.OnDayChanged -= HandleMorningRoutine;
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

        // A. Produkcja pasywna (na mieszkańca)
        ProducePerResident(logChanges);

        // B. Utrzymanie (Survival Cost)
        Dictionary<ResourceType, float> survivalCost = CalculateSurvivalCost();
        bool survivalPaid = ResourceManager.Instance.SpendResources(survivalCost);

        ShowLossFeedback(survivalCost, survivalPaid);

        // C. Logika wzrostu
        if (!survivalPaid)
        {
            Debug.Log($"<color=red>Głód w {name}! Populacja stagnuje.</color>");
            if (currentGrowthProgress > 0) currentGrowthProgress--;
        }
        else
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
            float totalAmount = prod.amount * residents.Count;
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
        {
            if (cost.Value >= 1f)
                FloatingTextManager.Instance.ShowLoss(
                    transform.position, cost.Key.ToString(), Mathf.FloorToInt(cost.Value));
        }
    }

    private void TryGrow()
    {
        if (residents.Count >= housingData.maxResidents) return;

        var growthCost = CalculateGrowthCost();
        if (!ResourceManager.Instance.SpendResources(growthCost)) return;

        currentGrowthProgress++;
        CheckForNewResident();
    }

    private void CheckForNewResident()
    {
        int required = CalculateRequiredGrowthTicks();
        if (currentGrowthProgress < required) return;

        Citizen newC = CitizenManager.Instance.SpawnNewCitizen(housingData.housingRace, this);
        residents.Add(newC);
        currentGrowthProgress = 0;
    }

    // =========================================================================
    // Matematyka
    // =========================================================================

    public int CalculateRequiredGrowthTicks() =>
        Mathf.FloorToInt(housingData.baseGrowthTicks + residents.Count * housingData.growthDifficultyMultiplier);

    private Dictionary<ResourceType, float> CalculateSurvivalCost()
    {
        var total = new Dictionary<ResourceType, float>();
        foreach (var c in housingData.baseDailyUpkeep)         AddToDict(total, c.type, c.amount);
        foreach (var c in housingData.upkeepPerResident)       AddToDict(total, c.type, c.amount * residents.Count);
        return total;
    }

    private Dictionary<ResourceType, float> CalculateGrowthCost()
    {
        var total = new Dictionary<ResourceType, float>();
        foreach (var c in housingData.growthSurplusCost)       AddToDict(total, c.type, c.amount);
        return total;
    }

    private void AddToDict(Dictionary<ResourceType, float> d, ResourceType t, float a)
    {
        if (d.ContainsKey(t)) d[t] += a; else d.Add(t, a);
    }

    private void RefreshUI()
    {
        if (UIBuildingInspector.Instance != null)
            UIBuildingInspector.Instance.RefreshContent();
    }

    // =========================================================================
    // API dla UI
    // =========================================================================

    public float GetGrowthProgress()
    {
        if (residents.Count >= housingData.maxResidents) return 1f;
        int required = CalculateRequiredGrowthTicks();
        return required == 0 ? 1f : (float)currentGrowthProgress / required;
    }

    public int GetDaysRemaining()
    {
        if (residents.Count >= housingData.maxResidents) return 0;
        return Mathf.Max(0, CalculateRequiredGrowthTicks() - currentGrowthProgress);
    }

    public float GetProjectedUpkeep()
    {
        float total = 0f;
        foreach (var cost in housingData.baseDailyUpkeep)    total += cost.amount;
        foreach (var cost in housingData.upkeepPerResident)  total += cost.amount * residents.Count;
        return total;
    }

    public string GetGrowthStatus()
    {
        if (residents.Count >= housingData.maxResidents) return "PEŁNY";

        foreach (var kvp in CalculateSurvivalCost())
            if (!ResourceManager.Instance.CanAfford(kvp.Key, kvp.Value)) return "BRAK ZASOBÓW";

        return "ROSNĄCY";
    }
}