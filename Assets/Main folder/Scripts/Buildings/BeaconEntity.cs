using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Unikalny budynek – Beacon (Latarnia). Singleton.
/// Zarządza własnym paliwem (węgiel → paliwo) niezależnie od BuildingFuelComponent,
/// bo jego mechanika jest zupełnie inna od standardowych budynków produkcyjnych.
///
/// Nadpisuje HandleHourlyProduction aby ją wyłączyć – Beacon nie produkuje zasobów,
/// tylko modyfikuje globalnie produkcję i wieże przez swoje API.
/// </summary>
public class BeaconEntity : BuildingEntity
{
    public static BeaconEntity Instance { get; private set; }

    [Header("Konfiguracja Paliwa")]
    public float maxFuel = 100f;
    public float currentFuel = 60f;
    public int coalToFuelRatio = 5; // 1 Węgiel = 5 Paliwa

    [Header("Konfiguracja Poziomów (Zużycie na dobę)")]
    public int[] fuelConsumptionPerLevel = { 5, 10, 15, 20, 25 };

    [Header("Sterowanie Gracza")]
    [Range(0, 20)] public int dailyCoalInput = 0;

    public int CurrentFireLevel { get; private set; }

    // Event zmiany (dla UI)
    public event System.Action OnBeaconStateChanged;

    // =========================================================================
    // Cykl życia
    // =========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[BeaconEntity] Duplikat Beacona – niszczę nadmiarowy.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Start() z BuildingEntity wywoła Initialize(data) automatycznie.
    // Nie nadpisujemy – zamiast tego nadpisujemy Initialize().

    public override void Initialize(BuildingData buildingData)
    {
        base.Initialize(buildingData);

        // Podpinamy własny cykl dobowy OSOBNO od base (który podpiął HandleHourlyProduction i HandleDayReset)
        if (TimeCycleManager.Instance != null)
            TimeCycleManager.Instance.OnDayChanged += HandleDayNightCycle;

        CalculateLevel();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (TimeCycleManager.Instance != null)
            TimeCycleManager.Instance.OnDayChanged -= HandleDayNightCycle;

        if (Instance == this) Instance = null;
    }

    // =========================================================================
    // Wyłączenie standardowej logiki produkcyjnej
    // =========================================================================

    protected override void HandleHourlyProduction(int currentHour)
    {
        // Beacon nie produkuje zasobów w godzinowych tickach.
    }

    // =========================================================================
    // Własna logika dobowa
    // =========================================================================

    private void HandleDayNightCycle(int day)
    {
        ConsumeFuel();
        RefillFuelFromCoal();

        currentFuel = Mathf.Clamp(currentFuel, 0f, maxFuel);
        CalculateLevel();

        OnBeaconStateChanged?.Invoke();
    }

    private void ConsumeFuel()
    {
        int consumption = fuelConsumptionPerLevel[Mathf.Clamp(CurrentFireLevel - 1, 0, 4)];
        currentFuel -= consumption;
        if (currentFuel < 0f) currentFuel = 0f;

        Debug.Log($"[Beacon] Zużyto {consumption} paliwa. Pozostało: {currentFuel}");
    }

    private void RefillFuelFromCoal()
    {
        if (dailyCoalInput <= 0) return;

        float fuelSpace = maxFuel - currentFuel;
        int maxCoalUsable = Mathf.CeilToInt(fuelSpace / coalToFuelRatio);
        int actualCoal = Mathf.Min(dailyCoalInput, maxCoalUsable);
        if (actualCoal <= 0) return;

        if (ResourceManager.Instance.SpendResource(ResourceType.Coal, actualCoal))
        {
            currentFuel += actualCoal * coalToFuelRatio;
            return;
        }

        // Nie stać na całość – bierz ile jest
        float availableCoal = ResourceManager.Instance.GetResourceAmount(ResourceType.Coal);
        if (availableCoal <= 0f) return;

        ResourceManager.Instance.SpendResource(ResourceType.Coal, availableCoal);
        currentFuel += availableCoal * coalToFuelRatio;

        Debug.LogWarning("[Beacon] Brak wystarczającej ilości węgla na pełne doładowanie.");
    }

    private void CalculateLevel()
    {
        // Progi: [1] 0-20, [2] 21-40, [3] 41-60, [4] 61-80, [5] 81-100
        if      (currentFuel <= 20f) CurrentFireLevel = 1;
        else if (currentFuel <= 40f) CurrentFireLevel = 2;
        else if (currentFuel <= 60f) CurrentFireLevel = 3;
        else if (currentFuel <= 80f) CurrentFireLevel = 4;
        else                         CurrentFireLevel = 5;

        OnBeaconStateChanged?.Invoke();
    }

    // =========================================================================
    // API dla innych systemów
    // =========================================================================

    public float GetGlobalProductionMultiplier() => CurrentFireLevel switch
    {
        1 => 0.7f,
        2 => 0.9f,
        4 => 1.1f,
        5 => 1.2f,
        _ => 1.0f
    };

    public float GetTowerRangeMultiplier() => CurrentFireLevel switch
    {
        1 => 0.7f,
        2 => 0.9f,
        4 => 1.1f,
        5 => 1.15f,
        _ => 1.0f
    };

    public float GetTowerDamageMultiplier() =>
        CurrentFireLevel == 5 ? 1.05f : 1.0f;

    public void GetEnemyModifiers(
        out float countMod, out float hpMod, out float speedMod, out float eliteMod)
    {
        countMod = 1f; hpMod = 1f; speedMod = 1f; eliteMod = 1f;

        if (CurrentFireLevel == 1)
        {
            countMod = 1.3f; hpMod = 1.2f; speedMod = 1.1f; eliteMod = 2.0f;
        }
        else if (CurrentFireLevel == 2)
        {
            hpMod = 1.1f;
        }
    }

    // Procent paliwa (dla UI paska)
    public float GetFuelPercent() => maxFuel > 0f ? currentFuel / maxFuel : 0f;
}