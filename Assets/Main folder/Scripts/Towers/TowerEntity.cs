using UnityEngine;
using System.Collections.Generic;

public class TowerEntity : BuildingEntity
{
    [Header("Referencje")]
    public TowerController controller; // Fizyczna wie¿a strzelaj¹ca

    [Header("Status Bojowy")]
    public bool isCombatActive = false; // Czy wie¿a mo¿e strzelaæ?
    public bool hasAmmo = false;        // Czy op³acono upkeep na noc?
    private bool firedShotsTonight = false; // Czy oddano strza³ w nocy?

    // Zmienne do przechowywania kosztu z ostatniej nocy (¿eby wiedzieæ ile zwróciæ)
    private Dictionary<ResourceType, int> paidUpkeepCache = new Dictionary<ResourceType, int>();

    // Godziny specjalne dla wie¿
    private const int NIGHT_START_HOUR = 20;
    private const int NIGHT_END_HOUR = 6;

    private void Start()
    {
        if (controller == null) controller = GetComponent<TowerController>();

        if (TimeCycleManager.Instance != null)
        {
            TimeCycleManager.Instance.OnHourTick += HandleTowerLogic;
            TimeCycleManager.Instance.OnDayChanged += HandleDayReset;
        }

        // Pierwsze przeliczenie
        RecalculateStats();
    }

    private void OnDestroy()
    {
        if (TimeCycleManager.Instance != null)
        {
            TimeCycleManager.Instance.OnHourTick -= HandleTowerLogic;
            TimeCycleManager.Instance.OnDayChanged -= HandleDayReset;
        }
    }

    // --- LOGIKA CZASOWA ---

    private void HandleTowerLogic(int hour)
    {
        // 1. Obs³uga Amunicji (Pocz¹tek Nocy)
        if (hour == NIGHT_START_HOUR)
        {
            TryPayAmmoCost();
        }

        // 2. Obs³uga Zwrotu (Koniec Nocy / Œwit)
        if (hour == NIGHT_END_HOUR)
        {
            ProcessAmmoRefund();
        }

        // 3. Aktualizacja statystyk i pracowników
        // (Base class obs³uguje blokowanie pracowników w HandleHourlyProduction, 
        // ale wie¿e nie produkuj¹, wiêc musimy wywo³aæ logikê zmian rêcznie lub bazowaæ na tym co jest)
        // Dla uproszczenia: Wie¿a aktualizuje swoj¹ skutecznoœæ co godzinê
        RecalculateStats();
    }

    private void HandleDayReset(int day)
    {
        // Reset flagi strzelania na nowy dzieñ
        firedShotsTonight = false;

        // Wywo³ujemy reset zmêczenia z klasy bazowej
        // (Musimy to zrobiæ rêcznie, bo BuildingEntity robi to w swoim OnDayChanged, 
        // a my chcemy mieæ pewnoœæ kolejnoœci lub po prostu polegamy na dziedziczeniu jeœli metoda jest wirtualna)
        // W Twoim BuildingEntity HandleDayReset jest prywatne, wiêc:
        // Zalecam zmieniæ w BuildingEntity: protected virtual void HandleDayReset
        // Jeœli nie, to kod w BuildingEntity i tak siê wykona, bo on te¿ subskrybuje event.
    }

    // --- LOGIKA AMUNICJI ---

    void TryPayAmmoCost()
    {
        // Pobieramy koszt utrzymania (zdefiniowany w BuildingData jako upkeep)
        Dictionary<ResourceType, int> upkeepCost = GetCurrentUpkeep();

        if (ResourceManager.Instance.SpendResources(upkeepCost))
        {
            hasAmmo = true;
            paidUpkeepCache = new Dictionary<ResourceType, int>(upkeepCost); // Kopia dla zwrotu
            Debug.Log($"[Tower] {name} za³adowana na noc.");
        }
        else
        {
            hasAmmo = false;
            paidUpkeepCache.Clear();
            Debug.LogWarning($"[Tower] {name} BRAK AMUNICJI! Wy³¹czona na noc.");
        }
    }

    void ProcessAmmoRefund()
    {
        // Jeœli mieliœmy amunicjê, ale nie strzelaliœmy -> Zwrot 50%
        if (hasAmmo && !firedShotsTonight && paidUpkeepCache.Count > 0)
        {
            foreach (var kvp in paidUpkeepCache)
            {
                int refundAmount = Mathf.CeilToInt(kvp.Value * 0.5f); // Zaokr¹glamy w górê
                if (refundAmount > 0)
                {
                    ResourceManager.Instance.AddResource(kvp.Key, refundAmount);
                }
            }
            Debug.Log($"[Tower] {name} - Noc spokojna. Zwrot 50% amunicji.");
        }

        // Reset amunicji na dzieñ (w dzieñ wie¿e mog¹ strzelaæ "za darmo" lub wymagaj¹ nowej logiki)
        // Zak³adamy, ¿e w dzieñ jest bezpiecznie, wiêc hasAmmo = true (trening) lub false (oszczêdzanie).
        // W Twoim designie fale s¹ w nocy, wiêc w dzieñ ammo nie jest krytyczne.
        hasAmmo = false; // Wymaga ponownego op³acenia kolejnej nocy
    }

    // Wywo³ywane przez TowerController gdy padnie strza³
    public void RegisterShot()
    {
        firedShotsTonight = true;
    }

    // --- LOGIKA STATYSTYK ---

    public void RecalculateStats()
    {
        // 1. SprawdŸ pracowników (tylko ci na zmianie, nie exhausted)
        List<Citizen> activeCrew = GetAssignedCitizens().FindAll(c => c.workState != WorkState.Exhausted);
        int crewCount = activeCrew.Count;

        // 2. Bazowe mno¿niki
        float efficiency = 0f;

        if (crewCount == 0) efficiency = 0f;       // Brak ludzi = nie dzia³a
        else if (crewCount == 1) efficiency = 0.7f; // 1 osoba = 70%
        else efficiency = 1.0f;                    // 2+ osoby = 100%

        // 3. Bonusy Rasowe
        float rangeBonus = 1.0f;
        float damageBonus = 1.0f;
        float fireRateBonus = 1.0f;

        foreach (var worker in activeCrew)
        {
            if (worker.race == Race.Elves) rangeBonus += 0.1f;       // Elf = +10% zasiêgu
            if (worker.race == Race.Dwarves) damageBonus += 0.1f;    // Krasnolud = +10% dmg
            if (worker.race == Race.Humans) fireRateBonus += 0.1f;   // Cz³owiek = +10% speed
        }

        // 4. Decyzja czy dzia³a
        // Wie¿a dzia³a jeœli: Ma ludzi ORAZ (Jest dzieñ LUB (Jest noc i ma amunicje))
        bool isNight = TimeCycleManager.Instance.currentHour >= NIGHT_START_HOUR || TimeCycleManager.Instance.currentHour < NIGHT_END_HOUR;

        // W dzieñ dzia³a (chyba ¿e chcesz inaczej), w nocy wymaga Ammo
        if (isNight && !hasAmmo)
        {
            isCombatActive = false;
        }
        else
        {
            isCombatActive = (crewCount > 0);
        }

        // 5. Przekazanie danych do kontrolera
        if (controller != null)
        {
            controller.UpdateCombatStats(efficiency, rangeBonus, damageBonus, fireRateBonus, isCombatActive);
        }


    }

    public override bool TryAddWorker(Race race)
    {
        // 1. Wykonaj standardow¹ logikê (przypisanie, szukanie w managerze)
        bool success = base.TryAddWorker(race);

        // 2. Jeœli siê uda³o, NATYCHMIAST przelicz statystyki
        if (success)
        {
            RecalculateStats();
        }

        return success;
    }

    public override void RemoveWorker(Race race)
    {
        // 1. Wykonaj standardowe usuwanie
        base.RemoveWorker(race);

        // 2. NATYCHMIAST przelicz statystyki
        RecalculateStats();
    }
}