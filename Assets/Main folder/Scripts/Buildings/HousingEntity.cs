using UnityEngine;
using System.Collections.Generic;

public class HousingEntity : BuildingEntity
{
    // Rzutowanie danych na typ Housing
    public HousingBuildingData housingData => data as HousingBuildingData;

    [Header("Status Mieszka�c�w")]
    public List<Citizen> residents = new List<Citizen>();

    // Postęp wzrostu (punkty/dni)
    public int currentGrowthProgress = 0;

    private void Start()
    {
        // Domy dzia�aj� TYLKO w cyklu dobowym (nie godzinowym)
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

    public override void Initialize(BuildingData _data)
    {
        base.Initialize(_data);

        if (housingData == null)
        {
            Debug.LogError("Z�e dane przypisane do HousingEntity!");
            return;
        }

        // Spawn startowych mieszka�c�w
        for (int i = 0; i < housingData.initialResidents; i++)
        {
            if (residents.Count < housingData.maxResidents)
            {
                Citizen newC = CitizenManager.Instance.SpawnNewCitizen(housingData.housingRace, this);
                residents.Add(newC);
            }
        }
    }

    // --- NADPISANIE LOGIKI GODZINOWEJ ---
    // Zapobiegamy wywo�aniu logiki zmianowej z BuildingEntity dla dom�w
    protected override void HandleHourlyProduction(int currentHour)
    {
        // Domy nie maj� logiki godzinowej
    }

    // --- G��WNA LOGIKA PORANNA (06:00) ---
    private void HandleMorningRoutine(int day)
    {
        Dictionary<ResourceType, float> logChanges = new Dictionary<ResourceType, float>();

        // A. Produkcja Pasywna (Na mieszka�ca)
        if (housingData.productionPerResident != null && housingData.productionPerResident.Count > 0)
        {
            foreach (var prod in housingData.productionPerResident)
            {
                float totalAmount = prod.amount * residents.Count;
                if (totalAmount > 0)
                {
                    ResourceManager.Instance.AddResource(prod.type, totalAmount);
                    if (FloatingTextManager.Instance != null)
                        FloatingTextManager.Instance.ShowGain(transform.position, prod.type.ToString(), Mathf.FloorToInt(totalAmount));
                }
            }
        }

        // B. Utrzymanie (Survival Cost)
        Dictionary<ResourceType, float> survivalCost = CalculateSurvivalCost();
        bool survivalPaid = ResourceManager.Instance.SpendResources(survivalCost);

        if (survivalPaid && FloatingTextManager.Instance != null)
        {
            foreach (var cost in survivalCost)
            {
                if (cost.Value >= 1.0f)
                    FloatingTextManager.Instance.ShowLoss(transform.position, cost.Key.ToString(), Mathf.FloorToInt(cost.Value));
            }
        }

        // C. Logika Wzrostu
        if (!survivalPaid)
        {
            Debug.Log($"<color=red>G��d w {name}! Populacja stagnuje/maleje.</color>");
            if (currentGrowthProgress > 0) currentGrowthProgress--;
        }
        else
        {
            if (residents.Count < housingData.maxResidents)
            {
                Dictionary<ResourceType, float> growthCost = CalculateGrowthCost();

                if (ResourceManager.Instance.SpendResources(growthCost))
                {
                    currentGrowthProgress++;
                    CheckForNewResident();
                }
            }
        }

        foreach (var cost in survivalCost)
        {
            if (logChanges.ContainsKey(cost.Key)) logChanges[cost.Key] -= cost.Value;
            else logChanges.Add(cost.Key, -cost.Value);
        }
        if (UIBuildingInspector.Instance != null) UIBuildingInspector.Instance.RefreshContent();

        if (logChanges.Count > 0 && ResourceLogger.Instance != null)
        {
            ResourceLogger.Instance.LogTransaction($"Bilans Poranny: {data.buildingName}", logChanges);
        }
    }

    void CheckForNewResident()
    {
        int requiredTicks = CalculateRequiredGrowthTicks();

        if (currentGrowthProgress >= requiredTicks)
        {
            Citizen newC = CitizenManager.Instance.SpawnNewCitizen(housingData.housingRace, this);
            residents.Add(newC);
            currentGrowthProgress = 0;
        }
    }

    // --- MATEMATYKA ---

    public int CalculateRequiredGrowthTicks()
    {
        float val = housingData.baseGrowthTicks + (residents.Count * housingData.growthDifficultyMultiplier);
        return Mathf.FloorToInt(val);
    }

    Dictionary<ResourceType, float> CalculateSurvivalCost()
    {
        Dictionary<ResourceType, float> total = new Dictionary<ResourceType, float>();
        foreach (var c in housingData.baseDailyUpkeep) AddToDict(total, c.type, c.amount);
        foreach (var c in housingData.upkeepPerResident) AddToDict(total, c.type, c.amount * residents.Count);
        return total;
    }

    Dictionary<ResourceType, float> CalculateGrowthCost()
    {
        Dictionary<ResourceType, float> total = new Dictionary<ResourceType, float>();
        foreach (var c in housingData.growthSurplusCost) AddToDict(total, c.type, c.amount);
        return total;
    }

    void AddToDict(Dictionary<ResourceType, float> d, ResourceType t, float a)
    {
        if (d.ContainsKey(t)) d[t] += a; else d.Add(t, a);
    }

    // --- API DLA UI (NAPRAWIONE) ---

    // Zwraca post�p od 0.0 do 1.0 dla paska
    public float GetGrowthProgress()
    {
        if (residents.Count >= housingData.maxResidents) return 1f;

        int required = CalculateRequiredGrowthTicks();
        if (required == 0) return 1f; // Zabezpieczenie dzielenia przez zero

        return (float)currentGrowthProgress / required;
    }

    // Zwraca ile dni (tick�w) zosta�o do narodzin
    public int GetDaysRemaining()
    {
        if (residents.Count >= housingData.maxResidents) return 0;

        int required = CalculateRequiredGrowthTicks();
        return Mathf.Max(0, required - currentGrowthProgress);
    }

    public float GetProjectedUpkeep()
    {
        float total = 0;
        foreach (var cost in housingData.baseDailyUpkeep) total += cost.amount;
        foreach (var cost in housingData.upkeepPerResident) total += cost.amount * residents.Count;
        return total;
    }

    public string GetGrowthStatus()
    {
        if (residents.Count >= housingData.maxResidents) return "PE�NY";

        Dictionary<ResourceType, float> cost = CalculateSurvivalCost();
        foreach (var kvp in cost)
        {
            if (!ResourceManager.Instance.CanAfford(kvp.Key, kvp.Value)) return "BRAK ZASOB�W";
        }

        return "ROSN�CY";
    }
}