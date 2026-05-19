using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Cienki koordynator budynku.
///     Odpowiada wyłącznie za:
///     - stworzenie i powiązanie komponentów logicznych,
///     - subskrypcję zdarzeń czasowych (TimeCycleManager),
///     - delegowanie wywołań do odpowiednich komponentów,
///     - globalny rejestr budynków (AllBuildings).
///     Cała logika produkcji, paliwa, pracowników, terenu i ulepszeń
///     żyje w dedykowanych komponentach.
/// </summary>
public class BuildingEntity : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Dane i konfiguracja (Inspector)
    // -------------------------------------------------------------------------

    [Header("Dane Bazowe")] public BuildingData data;

    [Header("Zarządzanie")] public bool isBuildingActive = true;

    [Header("Ustawienia Czasu Pracy")] public int shiftStartHour = 6;

    public int shiftLength = 8;

    [Header("Stan zwrotu kosztów")]
    [Tooltip("Czy budynek został użyty? Jeśli nie = 100% zwrotu. Jeśli tak = 50% zwrotu.")]
    public bool hasBeenUsed;

    // Flaga zabezpieczająca przed wielokrotną inicjalizacją
    private bool isInitialized;

    // -------------------------------------------------------------------------
    // Komponenty logiczne (tworzone przez new, nie MonoBehaviour)
    // -------------------------------------------------------------------------

    public BuildingUpgradeComponent Upgrades { get; private set; }
    public BuildingWorkerComponent Workers { get; private set; }
    public BuildingTerrainComponent Terrain { get; private set; }
    public BuildingFuelComponent Fuel { get; private set; }
    public BuildingProductionComponent Production { get; private set; }

    /// <summary>
    ///     Przekaźnik dla UI – zachowuje stare API bez ujawniania komponentu.
    /// </summary>
    public int currentTier => Upgrades?.CurrentTier ?? 0;

    // -------------------------------------------------------------------------
    // Globalny rejestr (delegujemy do BuildingRegistry)
    // -------------------------------------------------------------------------

    /// <summary>
    ///     Skrót dla wygody – zamiast BuildingRegistry.Instance.AllBuildings.
    ///     Jeśli rejestr nie istnieje zwraca pustą listę zamiast rzucać wyjątek.
    /// </summary>
    public static IReadOnlyList<BuildingEntity> AllBuildings =>
        BuildingRegistry.Instance != null
            ? BuildingRegistry.Instance.AllBuildings
            : Array.Empty<BuildingEntity>();
    // -------------------------------------------------------------------------
    // Długość zmiany (obliczana, uwzględnia globalne modyfikatory)
    // -------------------------------------------------------------------------

    public float CurrentShiftLength { get; private set; }

    protected virtual void Start()
    {
        // Inicjalizuj tylko jeśli jeszcze nie została wywołana
        if (data != null && !isInitialized) Initialize(data);
    }

    // =========================================================================
    // Cykl życia Unity
    // =========================================================================

    protected virtual void OnEnable()
    {
        BuildingRegistry.Instance?.Register(this);
    }

    protected virtual void OnDisable()
    {
        BuildingRegistry.Instance?.Unregister(this);
    }

    protected virtual void OnDestroy()
    {
        UnsubscribeFromTime();
        Terrain?.ReleaseAll();
    }

    // =========================================================================
    // Inicjalizacja
    // =========================================================================

    public virtual void Initialize(BuildingData buildingData)
    {
        // Zabezpieczenie przed wielokrotną inicjalizacją
        if (isInitialized)
        {
            Debug.LogWarning($"[BuildingEntity] {name} już został zainicjalizowany! Pomijam kolejne wywołanie.");
            return;
        }

        data = buildingData;

        CreateComponents();
        CalculateFinalShiftLength();

        Terrain.FindAdjacentResources();
        Fuel.InitialFill();

        SubscribeToTime();

        isInitialized = true;
    }

    /// <summary>
    ///     Tworzy wszystkie komponenty logiczne i przekazuje im wzajemne zależności.
    ///     Kolejność ma znaczenie – Upgrades musi powstać pierwszy.
    /// </summary>
    private void CreateComponents()
    {
        // 1. Upgrade – brak zależności zewnętrznych
        Upgrades = new BuildingUpgradeComponent(data);
        Upgrades.OnUpgradeApplied += CalculateFinalShiftLength;

        // 2. Worker – potrzebuje Upgrades i globalnych modyfikatorów
        Workers = new BuildingWorkerComponent(
            data,
            Upgrades,
            () => CityStatsManager.Instance?.globalBonusShifts ?? 0,
            () => CityStatsManager.Instance?.globalBonusWorkersPerShift ?? 0);

        // 3. Terrain – potrzebuje tylko danych i pozycji
        Terrain = new BuildingTerrainComponent(data, transform, Upgrades);

        // 4. Fuel – potrzebuje Upgrades + getterów z Workers
        Fuel = new BuildingFuelComponent(
            data,
            Upgrades,
            () => Workers.GetMaxShifts(),
            () => Workers.GetMaxWorkersPerShift(),
            () => CurrentShiftLength);

        // 5. Production – agreguje wszystkie pozostałe
        Production = new BuildingProductionComponent(
            data,
            Upgrades,
            Terrain,
            Fuel,
            Workers,
            transform);

        Production.CurrentShiftLength = CurrentShiftLength;
    }

    // =========================================================================
    // Zarządzanie czasem
    // =========================================================================

    private void SubscribeToTime()
    {
        if (TimeCycleManager.Instance == null) return;
        TimeCycleManager.Instance.OnHourTick += HandleHourlyProduction;
        TimeCycleManager.Instance.OnDayChanged += HandleDayReset;
    }

    private void UnsubscribeFromTime()
    {
        if (TimeCycleManager.Instance == null) return;
        TimeCycleManager.Instance.OnHourTick -= HandleHourlyProduction;
        TimeCycleManager.Instance.OnDayChanged -= HandleDayReset;
    }

    // =========================================================================
    // Handlery zdarzeń czasowych
    // =========================================================================

    protected virtual void HandleHourlyProduction(int currentHour)
    {
        if (isBuildingActive) Production.ProcessHour(currentHour, shiftStartHour);
        RefreshInspectorUI();
    }

    protected virtual void HandleDayReset(int day)
    {
        Workers.ResetWorkersForNewDay();
        // Fuel.RefillForNewDay(); <--- USUŃ TO
        if (Terrain != null) Terrain.ProcessDailyTerrainModifiers();
        RefreshInspectorUI();
    }

    // =========================================================================
    // Publiczne API – delegowanie do komponentów
    // =========================================================================

    // --- Pracownicy ---

    public virtual bool TryAddWorker(Race race)
    {
        if (!isBuildingActive) return false; // NOWE: Zablokowane przypisywanie

        var success = Workers.TryAddWorker(race);
        if (success)
        {
            hasBeenUsed = true;
            RefreshInspectorUI();
        }

        return success;
    }

    public virtual void RemoveWorker(Race race)
    {
        Workers.RemoveWorker(race);
        RefreshInspectorUI();
    }

    public List<Citizen> GetAssignedCitizens()
    {
        return Workers.GetAssignedCitizens();
    }

    public int GetWorkerCount(Race race)
    {
        return Workers.GetWorkerCount(race);
    }

    public int getMaxShifts()
    {
        return Workers.GetMaxShifts();
    }

    public int getMaxWorkersPerShift()
    {
        return Workers.GetMaxWorkersPerShift();
    }

    // --- Ulepszenia ---

    public List<BuildingUpgradeSO> GetAvailableUpgrades()
    {
        return Upgrades.GetAvailableUpgrades();
    }

    public void ApplyUpgrade(BuildingUpgradeSO upgrade)
    {
        Upgrades.ApplyUpgrade(upgrade); // CalculateFinalShiftLength wywoła się przez OnUpgradeApplied
        RefreshInspectorUI();
    }

    // --- Produkcja i paliwo ---

    public Dictionary<ResourceType, float> GetCurrentProduction()
    {
        return Production.GetCurrentProduction();
    }

    public Dictionary<ResourceType, float> GetCurrentUpkeep()
    {
        return Fuel.GetCurrentUpkeep();
    }

    public Dictionary<ResourceType, float> CalculateMaxShiftConsumption()
    {
        return Fuel.CalculateMaxShiftConsumption();
    }

    public Dictionary<ResourceType, float> GetCurrentBudget()
    {
        return Fuel.GetCurrentBudget();
    }

    // --- Teren ---

    public void ForceRescan()
    {
        Terrain.FindAdjacentResources();
        if (this is TowerEntity tower) tower.RecalculateStats();
    }

    // --- Rozbiórka ---

    public void Demolish()
    {
        Workers.ReleaseAllWorkers();

        // 1. ZWROT KOSZTÓW BUDOWY
        if (data != null && data.constructionCost != null && ResourceManager.Instance != null)
        {
            var refundMultiplier = hasBeenUsed ? 0.5f : 1.0f;

            foreach (var cost in data.constructionCost)
            {
                var amountToRefund = cost.amount * refundMultiplier;

                // Zabezpieczenie przed ułamkami (np. z 5 drewna odda 2 zamiast 2.5)
                var intRefund = Mathf.FloorToInt(amountToRefund);

                if (intRefund > 0)
                {
                    ResourceManager.Instance.AddResource(cost.type, intRefund);

                    if (FloatingTextManager.Instance != null)
                        FloatingTextManager.Instance.ShowGain(transform.position, cost.type.ToString(), intRefund);
                }
            }
        }

        if (Terrain != null) Terrain.DestroyArtificialFeatures();

        Destroy(gameObject);
    }

    // =========================================================================
    // Helpers prywatne
    // =========================================================================

    private void CalculateFinalShiftLength()
    {
        var globalMod = CityStatsManager.Instance?.globalShiftLengthModifier ?? 0;
        CurrentShiftLength = Mathf.Max(1f, shiftLength + globalMod);

        // Synchronizacja z komponentem produkcji (może jeszcze nie istnieć przy pierwszym wywołaniu)
        if (Production != null)
            Production.CurrentShiftLength = CurrentShiftLength;
    }

    private void RefreshInspectorUI()
    {

    }

    public void ToggleBuildingActive()
    {
        isBuildingActive = !isBuildingActive;

        // Kiedy wyłączamy budynek - wywal wszystkich, którzy nie zaczęli pracować (Assigned)
        // Opcjonalnie wywal też pracujących (Working), robiąc ich od razu Exhausted.
        if (!isBuildingActive)
        {
            var workers = Workers.GetAssignedCitizens();
            // Tworzymy kopię listy do iteracji
            foreach (var w in new List<Citizen>(workers))
                if (w.workState == WorkState.Assigned)
                    Workers.RemoveSpecificWorker(w);
        }

        RefreshInspectorUI();
    }
}