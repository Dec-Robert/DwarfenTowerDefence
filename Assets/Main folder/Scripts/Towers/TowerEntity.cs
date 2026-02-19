using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Wieża obronna. Dziedziczy z BuildingEntity.
///
/// Nie zna BeaconEntity – zamiast tego odpytuje GlobalModifierRegistry
/// o aktualne mnożniki statystyk.
/// </summary>
public class TowerEntity : BuildingEntity
{
    [Header("Referencje")]
    public TowerController controller;

    [Header("Status Bojowy")]
    public bool isCombatActive    = false;
    public bool hasAmmo           = false;
    private bool firedShotsTonight = false;

    private Dictionary<ResourceType, float> paidUpkeepCache =
        new Dictionary<ResourceType, float>();

    private const int NIGHT_START_HOUR = 20;
    private const int NIGHT_END_HOUR   = 6;

    // =========================================================================
    // Cykl życia
    // =========================================================================

    protected override void Start()
    {
        if (controller == null) controller = GetComponent<TowerController>();

        base.Start(); // Wywołuje Initialize(data) i subskrybuje eventy

        if (TimeCycleManager.Instance != null)
        {
            TimeCycleManager.Instance.OnHourTick  += HandleTowerLogic;
            TimeCycleManager.Instance.OnDayChanged += HandleTowerDayReset;
        }

        RecalculateStats();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (TimeCycleManager.Instance != null)
        {
            TimeCycleManager.Instance.OnHourTick   -= HandleTowerLogic;
            TimeCycleManager.Instance.OnDayChanged -= HandleTowerDayReset;
        }
    }

    // Wieża nie ma godzinowej logiki produkcji
    protected override void HandleHourlyProduction(int currentHour) { }

    // =========================================================================
    // Logika czasowa
    // =========================================================================

    private void HandleTowerLogic(int hour)
    {
        if (hour == NIGHT_START_HOUR) TryPayAmmoCost();
        if (hour == NIGHT_END_HOUR)   ProcessAmmoRefund();

        RecalculateStats();
    }

    private void HandleTowerDayReset(int day)
    {
        firedShotsTonight = false;
        // Resetowanie zmęczenia pracowników obsługuje base przez HandleDayReset
    }

    // =========================================================================
    // Logika amunicji
    // =========================================================================

    private void TryPayAmmoCost()
    {
        var upkeepCost = GetCurrentUpkeep();

        if (ResourceManager.Instance.SpendResources(upkeepCost))
        {
            hasAmmo        = true;
            paidUpkeepCache = new Dictionary<ResourceType, float>(upkeepCost);
            Debug.Log($"[Tower] {name} załadowana na noc.");
        }
        else
        {
            hasAmmo = false;
            paidUpkeepCache.Clear();
            Debug.LogWarning($"[Tower] {name} BRAK AMUNICJI!");
        }
    }

    private void ProcessAmmoRefund()
    {
        if (hasAmmo && !firedShotsTonight && paidUpkeepCache.Count > 0)
        {
            foreach (var kvp in paidUpkeepCache)
            {
                int refund = Mathf.CeilToInt(kvp.Value * 0.5f);
                if (refund > 0) ResourceManager.Instance.AddResource(kvp.Key, refund);
            }
            Debug.Log($"[Tower] {name} – noc spokojna, zwrot 50% amunicji.");
        }

        hasAmmo = false;
    }

    /// <summary>Wywoływane przez TowerController gdy pada strzał.</summary>
    public void RegisterShot() => firedShotsTonight = true;

    // =========================================================================
    // Przeliczanie statystyk
    // =========================================================================

    public void RecalculateStats()
    {
        // 1. Aktywna załoga
        var activeCrew = GetAssignedCitizens()
            .FindAll(c => c.workState != WorkState.Exhausted);
        int crewCount = activeCrew.Count;

        // 2. Wydajność bazowa od liczby załogi
        float efficiency = crewCount == 0 ? 0f
                         : crewCount == 1 ? 0.7f
                         : 1.0f;

        // 3. Rasowe bonusy załogi (flat, per pracownik)
        float rangeBonusFlat    = 0f;
        float damageBonusFlat   = 0f;
        float fireRateBonusFlat = 0f;

        foreach (var worker in activeCrew)
        {
            if (worker.race == Race.Elves)   rangeBonusFlat    += 0.1f;
            if (worker.race == Race.Dwarves) damageBonusFlat   += 0.1f;
            if (worker.race == Race.Humans)  fireRateBonusFlat += 0.1f;
        }

        // 4. Globalne mnożniki z rejestru (Beacon, badania, buffy itd.)
        var registry = GlobalModifierRegistry.Instance;

        float rangeMulti    = (1f + rangeBonusFlat)    * (registry != null ? registry.GetMultiplier(TowerStatType.Range,    this) : 1f);
        float damageMulti   = (1f + damageBonusFlat)   * (registry != null ? registry.GetMultiplier(TowerStatType.Damage,   this) : 1f);
        float fireRateMulti = (1f + fireRateBonusFlat) * (registry != null ? registry.GetMultiplier(TowerStatType.FireRate, this) : 1f);

        // 5. Czy wieża jest aktywna bojowo?
        bool isNight = TimeCycleManager.Instance.currentHour >= NIGHT_START_HOUR
                    || TimeCycleManager.Instance.currentHour <  NIGHT_END_HOUR;

        isCombatActive = crewCount > 0 && (!isNight || hasAmmo);

        // 6. Przekazanie do kontrolera
        controller?.UpdateCombatStats(efficiency, rangeMulti, damageMulti, fireRateMulti, isCombatActive);
    }

    // =========================================================================
    // Nadpisanie zarządzania pracownikami (przelicz statystyki po zmianie)
    // =========================================================================

    public override bool TryAddWorker(Race race)
    {
        bool success = base.TryAddWorker(race);
        if (success) RecalculateStats();
        return success;
    }

    public override void RemoveWorker(Race race)
    {
        base.RemoveWorker(race);
        RecalculateStats();
    }
}