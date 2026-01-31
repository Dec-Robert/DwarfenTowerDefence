using UnityEngine;
using System.Collections.Generic;

public class BeaconEntity : BuildingEntity
{
    public static BeaconEntity Instance { get; private set; }

    [Header("Konfiguracja Paliwa")]
    public float maxFuel = 100f;
    public float currentFuel = 60f; // Startujemy na poziomie 3
    public int coalToFuelRatio = 5; // 1 Wêgiel = 5 Paliwa

    [Header("Konfiguracja Poziomów (Zu¿ycie)")]
    // Indeks 0 = Poziom 1, Indeks 4 = Poziom 5
    public int[] fuelConsumptionPerLevel = { 5, 10, 15, 20, 25 };

    [Header("Sterowanie Gracza")]
    [Range(0, 20)] public int dailyCoalInput = 0; // Ile wêgla gracz chce wrzucaæ

    // Aktualny poziom (1-5)
    public int CurrentFireLevel { get; private set; }

    // Event zmiany (dla UI)
    public event System.Action OnBeaconStateChanged;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Inicjalizacja jak zwyk³y budynek
        base.Initialize(data);

        if (TimeCycleManager.Instance != null)
        {
            TimeCycleManager.Instance.OnDayChanged += HandleDayNightCycle;
        }

        CalculateLevel();
    }

    private void OnDestroy()
    {
        if (TimeCycleManager.Instance != null)
            TimeCycleManager.Instance.OnDayChanged -= HandleDayNightCycle;
    }

    // Wywo³ywane o œwicie
    void HandleDayNightCycle(int day)
    {
        // 1. KONSUMPCJA (Za minion¹ noc)
        int consumption = GetConsumptionForCurrentLevel();
        currentFuel -= consumption;
        if (currentFuel < 0) currentFuel = 0;

        Debug.Log($"[Beacon] Zu¿yto {consumption} paliwa. Pozosta³o: {currentFuel}");

        // 2. UZUPE£NIANIE (Na nowy dzieñ)
        float fuelSpace = maxFuel - currentFuel;
        int maxCoalUsable = Mathf.CeilToInt(fuelSpace / coalToFuelRatio);
        int actualCoalToUse = Mathf.Min(dailyCoalInput, maxCoalUsable);

        if (actualCoalToUse > 0)
        {
            // --- POPRAWKA: Tworzymy s³ownik kosztów ---
            var costDict = new Dictionary<ResourceType, int>
            {
                { ResourceType.Coal, actualCoalToUse }
            };

            // Próba pobrania z magazynu
            if (ResourceManager.Instance.SpendResources(costDict))
            {
                currentFuel += actualCoalToUse * coalToFuelRatio;
            }
            else
            {
                // Jeœli nie staæ na ca³oœæ, pobierz tyle ile jest
                float availableCoal = ResourceManager.Instance.GetResourceAmount(ResourceType.Coal);
                if (availableCoal > 0)
                {
                    var partialCost = new Dictionary<ResourceType, float>
                    {
                        { ResourceType.Coal, availableCoal }
                    };

                    ResourceManager.Instance.SpendResources(partialCost);
                    currentFuel += availableCoal * coalToFuelRatio;
                }
                Debug.LogWarning("[Beacon] Brak wystarczaj¹cej iloœci wêgla w magazynie na pe³ne do³adowanie!");
            }
        }

        // Clamp i przeliczenie poziomu
        currentFuel = Mathf.Clamp(currentFuel, 0, maxFuel);
        CalculateLevel();

        OnBeaconStateChanged?.Invoke();
    }

    void CalculateLevel()
    {
        // [1] 0-20, [2] 21-40, [3] 41-60, [4] 61-80, [5] 81-100
        if (currentFuel <= 20) CurrentFireLevel = 1;
        else if (currentFuel <= 40) CurrentFireLevel = 2;
        else if (currentFuel <= 60) CurrentFireLevel = 3;
        else if (currentFuel <= 80) CurrentFireLevel = 4;
        else CurrentFireLevel = 5;

        OnBeaconStateChanged?.Invoke();
    }

    int GetConsumptionForCurrentLevel()
    {
        return fuelConsumptionPerLevel[Mathf.Clamp(CurrentFireLevel - 1, 0, 4)];
    }

    // --- API DLA INNYCH SYSTEMÓW ---

    public float GetGlobalProductionMultiplier()
    {
        switch (CurrentFireLevel)
        {
            case 1: return 0.7f; // -30%
            case 2: return 0.9f; // -10%
            case 3: return 1.0f;
            case 4: return 1.1f; // +10%
            case 5: return 1.2f; // +20%
            default: return 1.0f;
        }
    }

    public float GetTowerRangeMultiplier()
    {
        switch (CurrentFireLevel)
        {
            case 1: return 0.7f;
            case 2: return 0.9f;
            case 3: return 1.0f;
            case 4: return 1.1f;
            case 5: return 1.15f;
            default: return 1.0f;
        }
    }

    public float GetTowerDamageMultiplier()
    {
        if (CurrentFireLevel == 5) return 1.05f;
        return 1.0f;
    }

    public void GetEnemyModifiers(out float countMod, out float hpMod, out float speedMod, out float eliteMod)
    {
        countMod = 1f; hpMod = 1f; speedMod = 1f; eliteMod = 1f;

        if (CurrentFireLevel == 1)
        {
            countMod = 1.3f;
            hpMod = 1.2f;
            speedMod = 1.1f;
            eliteMod = 2.0f;
        }
        else if (CurrentFireLevel == 2)
        {
            hpMod = 1.1f;
        }
    }
}