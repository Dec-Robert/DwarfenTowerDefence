using UnityEngine;

/// <summary>
/// Unikalny budynek – Beacon (Latarnia). Singleton.
///
/// Przy zmianie poziomu rejestruje swoje modyfikatory w GlobalModifierRegistry.
/// TowerEntity i BuildingProductionComponent czytają stamtąd – Beacon nie jest
/// już znany bezpośrednio żadnemu innemu systemowi poza UI.
/// </summary>
public class BeaconEntity : BuildingEntity
{
    public static BeaconEntity Instance { get; private set; }

    private const string SOURCE_ID = "Beacon";

    [Header("Konfiguracja Paliwa")]
    public float maxFuel = 100f;
    public float currentFuel = 60f;
    public int coalToFuelRatio = 5;

    [Header("Konfiguracja Poziomów (Zużycie na dobę)")]
    public int[] fuelConsumptionPerLevel = { 5, 10, 15, 20, 25 };

    [Header("Sterowanie Gracza")]
    [Range(0, 20)] public int dailyCoalInput = 0;

    public int CurrentFireLevel { get; private set; }

    public event System.Action OnBeaconStateChanged;

    // =========================================================================
    // Modyfikatory per poziom – edytowalne w Inspectorze
    // =========================================================================

    [Header("Modyfikatory Per Poziom (indeks 0 = poziom 1)")]
    public float[] productionMultipliers  = { 0.7f, 0.9f, 1.0f, 1.1f, 1.2f  };
    public float[] towerRangeMultipliers  = { 0.7f, 0.9f, 1.0f, 1.1f, 1.15f };
    public float[] towerDamageMultipliers = { 1.0f, 1.0f, 1.0f, 1.0f, 1.05f };

    // =========================================================================
    // Cykl życia
    // =========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[BeaconEntity] Duplikat – niszczę nadmiarowy.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void Initialize(BuildingData buildingData)
    {
        base.Initialize(buildingData);

        if (TimeCycleManager.Instance != null)
            TimeCycleManager.Instance.OnDayChanged += HandleDayNightCycle;

        CalculateLevel();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (TimeCycleManager.Instance != null)
            TimeCycleManager.Instance.OnDayChanged -= HandleDayNightCycle;

        GlobalModifierRegistry.Instance?.Unregister(SOURCE_ID);

        if (Instance == this) Instance = null;
    }

    // Beacon nie produkuje zasobów godzinowo
    protected override void HandleHourlyProduction(int currentHour) { }

    // =========================================================================
    // Logika dobowa
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
        currentFuel = Mathf.Max(0f, currentFuel - consumption);
        Debug.Log($"[Beacon] Zużyto {consumption} paliwa. Pozostało: {currentFuel}");
    }

    private void RefillFuelFromCoal()
    {
        if (dailyCoalInput <= 0) return;

        float fuelSpace   = maxFuel - currentFuel;
        int maxCoalUsable = Mathf.CeilToInt(fuelSpace / coalToFuelRatio);
        int actualCoal    = Mathf.Min(dailyCoalInput, maxCoalUsable);
        if (actualCoal <= 0) return;

        if (ResourceManager.Instance.SpendResource(ResourceType.Coal, actualCoal))
        {
            currentFuel += actualCoal * coalToFuelRatio;
            return;
        }

        float available = ResourceManager.Instance.GetResourceAmount(ResourceType.Coal);
        if (available <= 0f) return;

        ResourceManager.Instance.SpendResource(ResourceType.Coal, available);
        currentFuel += available * coalToFuelRatio;
        Debug.LogWarning("[Beacon] Brak wystarczającej ilości węgla.");
    }

    private void CalculateLevel()
    {
        if      (currentFuel <= 20f) CurrentFireLevel = 1;
        else if (currentFuel <= 40f) CurrentFireLevel = 2;
        else if (currentFuel <= 60f) CurrentFireLevel = 3;
        else if (currentFuel <= 80f) CurrentFireLevel = 4;
        else                          CurrentFireLevel = 5;

        RegisterModifiers();
        OnBeaconStateChanged?.Invoke();
    }

    // =========================================================================
    // Rejestracja w GlobalModifierRegistry
    // =========================================================================

    private void RegisterModifiers()
    {
        if (GlobalModifierRegistry.Instance == null) return;

        int lvl = Mathf.Clamp(CurrentFireLevel - 1, 0, 4);

        GlobalModifierRegistry.Instance.Register(
            new StatModifier(SOURCE_ID, TowerStatType.GlobalProduction,
                multiplier: productionMultipliers[lvl]));

        GlobalModifierRegistry.Instance.Register(
            new StatModifier(SOURCE_ID, TowerStatType.Range,
                multiplier: towerRangeMultipliers[lvl]));

        GlobalModifierRegistry.Instance.Register(
            new StatModifier(SOURCE_ID, TowerStatType.Damage,
                multiplier: towerDamageMultipliers[lvl]));
    }

    // =========================================================================
    // API publiczne
    // =========================================================================

    /// <summary>Procent paliwa (0-1) dla UI paska.</summary>
    public float GetFuelPercent() =>
        maxFuel > 0f ? currentFuel / maxFuel : 0f;

    /// <summary>Modyfikatory wrogów – Beacon jest jedynym źródłem, nie wymagają rejestru.</summary>
    public void GetEnemyModifiers(
        out float countMod, out float hpMod, out float speedMod, out float eliteMod)
    {
        countMod = 1f; hpMod = 1f; speedMod = 1f; eliteMod = 1f;

        if (CurrentFireLevel == 1)
        { countMod = 1.3f; hpMod = 1.2f; speedMod = 1.1f; eliteMod = 2.0f; }
        else if (CurrentFireLevel == 2)
        { hpMod = 1.1f; }
    }
}