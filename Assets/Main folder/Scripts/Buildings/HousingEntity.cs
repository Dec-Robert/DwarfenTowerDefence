using UnityEngine;
using System.Collections.Generic;

public class HousingEntity : BuildingEntity
{
    // Rzutowanie danych na typ Housing
    public HousingBuildingData housingData => data as HousingBuildingData;

    [Header("Status Mieszkañców")]
    public List<Citizen> residents = new List<Citizen>();

    // Liczniki
    private int daysSinceLastGrowth = 0;

    // Bufor zu¿ycia (bo 0.2 jedzenia * 3 osoby = 0.6, musimy to zbieraæ a¿ uzbiera siê 1.0)
    private Dictionary<ResourceType, float> upkeepBuffer = new Dictionary<ResourceType, float>();

    private void Start()
    {
        // Subskrypcja tylko do zmiany dnia (jedzenie pobieramy rano)
        if (TimeCycleManager.Instance != null)
        {
            TimeCycleManager.Instance.OnDayChanged += HandleMorningRoutine;
        }
    }

    private void OnDestroy()
    {
        if (TimeCycleManager.Instance != null)
        {
            TimeCycleManager.Instance.OnDayChanged -= HandleMorningRoutine;
        }
    }

    // Nadpisujemy Initialize, ¿eby dodaæ startow¹ populacjê
    public override void Initialize(BuildingData _data)
    {
        base.Initialize(_data); // Wywo³aj bazow¹ inicjalizacjê

        if (housingData == null)
        {
            Debug.LogError("Z³e dane przypisane do HousingEntity!");
            return;
        }

        // Spawn startowych mieszkañców
        for (int i = 0; i < housingData.initialResidents; i++)
        {
            TrySpawnCitizen();
        }
    }

    // --- LOGIKA PORANNA (KONSUMPCJA + WZROST) ---
    private void HandleMorningRoutine(int day)
    {
        // 1. Oblicz ca³kowity koszt utrzymania na dziœ
        // Koszt = Baza + (IloœæMieszkañców * KosztNaG³owê)

        // S³ownik zapotrzebowania na ten poranek (w Intach, bo ResourceManager to Int)
        Dictionary<ResourceType, int> resourcesToPay = new Dictionary<ResourceType, int>();

        // A. Dodaj koszty bazowe do bufora
        if (housingData.baseDailyUpkeep != null)
        {
            foreach (var cost in housingData.baseDailyUpkeep)
            {
                AddUpkeepToBuffer(cost.type, cost.amount);
            }
        }

        // B. Dodaj koszty per capita do bufora
        if (housingData.upkeepPerResident != null)
        {
            foreach (var cost in housingData.upkeepPerResident)
            {
                float totalForRes = cost.amount * residents.Count;
                AddUpkeepToBuffer(cost.type, totalForRes);
            }
        }

        // C. Przelicz bufor na inty do zap³aty
        // Tworzymy kopiê kluczy, ¿eby móc modyfikowaæ s³ownik w pêtli
        List<ResourceType> keys = new List<ResourceType>(upkeepBuffer.Keys);

        bool canAffordEverything = true;

        foreach (var type in keys)
        {
            if (upkeepBuffer[type] >= 1.0f)
            {
                int toPay = Mathf.FloorToInt(upkeepBuffer[type]);

                // SprawdŸ czy staæ (bez pobierania)
                if (!ResourceManager.Instance.CanAfford(type, toPay))
                {
                    canAffordEverything = false;
                    Debug.Log($"<color=red>G³ód w {name}! Brakuje {type}</color>");
                    // Tutaj mo¿na dodaæ logikê kary (np. ktoœ umiera, brak wzrostu)
                }
                else
                {
                    // Dodaj do rachunku
                    if (resourcesToPay.ContainsKey(type)) resourcesToPay[type] += toPay;
                    else resourcesToPay.Add(type, toPay);
                }
            }
        }

        // 2. Pobierz op³atê i obs³u¿ Wzrost
        if (canAffordEverything && resourcesToPay.Count > 0)
        {
            // P³acimy
            ResourceManager.Instance.SpendResources(resourcesToPay);

            if (FloatingTextManager.Instance != null)
            {
                foreach (var kvp in resourcesToPay)
                {
                    FloatingTextManager.Instance.ShowLoss(transform.position, kvp.Key.ToString(), kvp.Value);
                }
            }

            // Odejmujemy zap³acone z bufora
            foreach (var kvp in resourcesToPay)
            {
                upkeepBuffer[kvp.Key] -= kvp.Value;
            }

            // Sukces -> Próbujemy urosn¹æ
            ProcessGrowth();
        }
        else if (canAffordEverything && resourcesToPay.Count == 0)
        {
            // Nic do zap³acenia (ma³e u³amki), ale warunki spe³nione -> roœniemy
            ProcessGrowth();
        }
        else
        {
            // Brak surowców -> Wzrost wstrzymany
            Debug.Log($"Wzrost w {name} wstrzymany z braku zasobów.");
        }
    }

    void ProcessGrowth()
    {
        if (residents.Count >= housingData.maxResidents) return;

        daysSinceLastGrowth++;

        if (daysSinceLastGrowth >= housingData.daysPerGrowth)
        {
            TrySpawnCitizen();
            daysSinceLastGrowth = 0;
        }
    }

    void TrySpawnCitizen()
    {
        if (residents.Count < housingData.maxResidents)
        {
            Citizen newC = CitizenManager.Instance.SpawnNewCitizen(housingData.housingRace, this);
            residents.Add(newC);
        }
    }

    void AddUpkeepToBuffer(ResourceType type, float amount)
    {
        if (upkeepBuffer.ContainsKey(type)) upkeepBuffer[type] += amount;
        else upkeepBuffer.Add(type, amount);
    }
}