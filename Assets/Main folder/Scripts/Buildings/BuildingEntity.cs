using UnityEngine;
using System.Collections.Generic;

public class BuildingEntity : MonoBehaviour
{
    [Header("Dane Bazowe")]
    public BuildingData data; // Referencja do ScriptableObject

    [Header("Stan Instancji")]
    public int currentTier = 0; // 0 = Podstawa, 1 = Po pierwszym ulepszeniu, itd.

    // Historia ulepszeñ (co gracz ju¿ kupi³ dla tego konkretnego budynku)
    public List<BuildingUpgradeSO> appliedUpgrades = new List<BuildingUpgradeSO>();

    [Header("Pracownicy")]
    [SerializeField] private int maxShifts = 2;
    [SerializeField] private int maxWorkersPerShift = 3;
    [SerializeField] List<Citizen> assignedCitizens = new List<Citizen>();

    private int currentShift = 0;

    // Metoda inicjalizuj¹ca (wo³ana przy budowie)
    public void Initialize(BuildingData _data)
    {
        data = _data;
        currentTier = 0;
        appliedUpgrades.Clear();
    }

    // --- LOGIKA ULEPSZEÑ ---

    // Zwraca listê opcji dostêpnych do kupienia W TYM MOMENCIE
    public List<BuildingUpgradeSO> GetAvailableUpgrades()
    {
        // 1. Jeœli budynek jest nowy (Tier 0), zwracamy opcje startowe z BuildingData
        if (currentTier == 0)
        {
            if (data.tier1Upgrades != null)
                return data.tier1Upgrades;
        }

        // 2. Jeœli mamy ju¿ jakieœ ulepszenia, sprawdzamy co odblokowa³o OSTATNIE wybrane ulepszenie
        // To tworzy efekt "drzewka" - wybór A odblokowuje B, wybór C odblokowuje D.
        if (appliedUpgrades.Count > 0)
        {
            BuildingUpgradeSO lastUpgrade = appliedUpgrades[appliedUpgrades.Count - 1];

            if (lastUpgrade.nextTierOptions != null)
                return lastUpgrade.nextTierOptions;
        }

        // Brak dostêpnych ulepszeñ (Max level lub œlepa uliczka)
        return new List<BuildingUpgradeSO>();
    }

    // Aplikuje wybrane ulepszenie
    public void ApplyUpgrade(BuildingUpgradeSO upgrade)
    {
        appliedUpgrades.Add(upgrade);
        currentTier++;

        Debug.Log($"Budynek {name} ulepszony do: {upgrade.upgradeName} (Nowy Tier: {currentTier})");

        // Tutaj w przysz³oœci dodasz:
        // - Zmianê modelu 3D (upgrade.newModelPrefab)
        // - Efekty wizualne (partikle)
        // - Obs³ugê specialEffectID (jeœli upgrade robi coœ dziwnego)
    }

    // --- KALKULACJE EKONOMICZNE (Baza + Bonusy) ---

    public Dictionary<ResourceType, int> GetCurrentProduction()
    {
        Dictionary<ResourceType, int> total = new Dictionary<ResourceType, int>();

        // 1. Bazowa produkcja z BuildingData
        if (data.productionPerCycle != null)
        {
            foreach (var res in data.productionPerCycle)
                AddToDict(total, res.type, res.amount);
        }

        // 2. Dodajemy bonusy ze wszystkich posiadanych ulepszeñ
        foreach (var up in appliedUpgrades)
        {
            if (up.productionBonus != null)
            {
                foreach (var res in up.productionBonus)
                    AddToDict(total, res.type, res.amount);
            }
        }
        return total;
    }

    public Dictionary<ResourceType, int> GetCurrentUpkeep()
    {
        Dictionary<ResourceType, int> total = new Dictionary<ResourceType, int>();

        // 1. Bazowe utrzymanie
        if (data.upkeepPerCycle != null)
        {
            foreach (var res in data.upkeepPerCycle)
                AddToDict(total, res.type, res.amount);
        }

        // 2. Kary/Koszty z ulepszeñ (np. lepsza kopalnia zu¿ywa wiêcej z³ota)
        foreach (var up in appliedUpgrades)
        {
            if (up.upkeepIncrease != null)
            {
                foreach (var res in up.upkeepIncrease)
                    AddToDict(total, res.type, res.amount);
            }
        }
        return total;
    }

    // Helper do sumowania wartoœci w s³owniku
    void AddToDict(Dictionary<ResourceType, int> dict, ResourceType type, int amount)
    {
        if (dict.ContainsKey(type)) dict[type] += amount;
        else dict.Add(type, amount);
    }

    // Metoda do niszczenia budynku
    public void Demolish()
    {
        // Tutaj mo¿na dodaæ logikê zwrotu surowców (np. 50% kosztów)
        Debug.Log($"Budynek {data.buildingName} zosta³ zburzony.");
        Destroy(gameObject);
    }

    //// --- ZARZ¥DZANIE PRACOWNIKAMI ---
    ///
    public int GetWorkerCount(Race r)
    {
        int count = 0;
        foreach (var citizen in assignedCitizens)
        {
            if (citizen.race == r)
                count++;
        }

        return count;
    }

    public List<Citizen> GetAssignedCitizens()
    {
        return assignedCitizens;
    }

    public void RemoveWorker(Race race)
    {
        // ZnajdŸ pierwszego pracownika tej rasy w TYM budynku
        Citizen workerToRemove = assignedCitizens.Find(c => c.race == race && c.workState == WorkState.Assigned);
        if (workerToRemove != null)
        {
            workerToRemove.RemoveFromWorkplace(); // Reset stanu pracownika
            assignedCitizens.Remove(workerToRemove); // Usuniêcie z listy budynku
            Debug.Log($"Zwolniono pracownika rasy {race}");
        }
        else
        {
            Debug.Log($"Brak dostêpnych pracowników");
        }
        BuildingCitizenUI.Instance.Refresh(this);
    }

    public bool TryAddWorker(Race race)
    {
        if (assignedCitizens.Count >= (maxShifts * maxWorkersPerShift))
        {
            Debug.Log("Brak wolnych miejsc pracy w tym budynku!");
            return false;
        }

        Citizen newWorker = CitizenManager.Instance.FindAndAssignCitizen(race, this);


        if (newWorker != null)
        {
            assignedCitizens.Add(newWorker);
            newWorker.AssignToWorkplace(this);

            BuildingCitizenUI.Instance.Refresh(this); // Odœwie¿ UI budynku
            return true;
        }
        BuildingCitizenUI.Instance.Refresh(this);
        return false; // Brak bezrobotnych tej rasy
    }

    public void NextShift()
    {
        currentShift = (currentShift + 1) % maxShifts;
        Debug.Log($"Prze³¹czono na zmianê {currentShift + 1}");
    }
    public int getMaxShifts() { return maxShifts; }
    public int getMaxWorkersPerShift() { return maxWorkersPerShift; }
}