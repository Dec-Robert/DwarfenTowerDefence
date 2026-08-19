using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Wieża obronna. Dziedziczy z BuildingEntity.
/// Nie zna BeaconEntity – zamiast tego odpytuje GlobalModifierRegistry
/// o aktualne mnożniki statystyk.
/// </summary>

public class TowerEntity : BuildingEntity
{
    [Header("Referencje")]
    public TowerController controller;

    private bool isOverdriven = false;
    

    private static readonly float[] efficencyTable = new float[] { 0.2f, 1f, 1.1f, 1.2f, 1.3f, 1.35f, 1.4f };
    private static readonly float overdriveMod = 1.2f;
    private List<ResourceCost> overdriveCost;
    
    protected override void Start()
    {
        if (controller == null) controller = GetComponent<TowerController>();
        base.Start();
        TimePhaseManager.Instance.OnMorningStarted += EndOverdrive;
        overdriveCost = controller.towerData.upkeepPerCycle;
        RecalculateStats();
    }

    protected override void OnDestroy()
    {
        TimePhaseManager.Instance.OnMorningStarted -= EndOverdrive;
        base.OnDestroy();
    }

    public void StartOverdrive()
    {
        if (ResourceManager.Instance.SpendResources(overdriveCost) && isOverdriven == false)
        {
            isOverdriven = true;
            RecalculateStats();
        }
    }

    public void RevokeOverdrive()
    {
        if (isOverdriven)
        {
            ResourceManager.Instance.AddResources(overdriveCost);
            isOverdriven = false;
            RecalculateStats();
        }
        
    }

    public void RecalculateStats()
    {
        int crewCount = GetAssignedCitizens().Count;

        // 2. Efficency based on working pop
        float efficiency = crewCount < efficencyTable.Length
            ? efficencyTable[crewCount] 
            : efficencyTable[efficencyTable.Length-1];

        // 3. Rasowe bonusy załogi (flat, per pracownik)
        float rangeBonusFlat    = 0f;
        float damageBonusFlat   = 0f;
        float fireRateBonusFlat = 0f;
        
        // 4. Globalne mnożniki i flaty z rejestru (Runy, Beacon, Badania)
        var registry = GlobalModifierRegistry.Instance;

        // Mnożniki (Multipliers)
        float rangeMulti    = (1f + rangeBonusFlat)    * (registry != null ? registry.GetMultiplier(TowerStatType.Range, this) : 1f);
        float damageMulti   = (1f + damageBonusFlat)   * (registry != null ? registry.GetMultiplier(TowerStatType.Damage, this) : 1f);
        float fireRateMulti = (1f + fireRateBonusFlat) * (registry != null ? registry.GetMultiplier(TowerStatType.FireRate, this) : 1f);

        // Płaskie wartości (Flat) 
        float flatRange    = registry != null ? registry.GetFlat(TowerStatType.Range, this) : 0f;
        float flatDamage   = registry != null ? registry.GetFlat(TowerStatType.Damage, this) : 0f;
        float flatFireRate = registry != null ? registry.GetFlat(TowerStatType.FireRate, this) : 0f;

        // Nowe statystyki ryniczne
        float flatArmorPen = registry != null ? registry.GetFlat(TowerStatType.ArmorPenetration, this) : 0f;
        float flatMagicPen = registry != null ? registry.GetFlat(TowerStatType.MagicPenetration, this) : 0f;
        float flatCritChan = registry != null ? registry.GetFlat(TowerStatType.CriticalChance, this) : 0f;
        float flatCritDmg  = registry != null ? registry.GetFlat(TowerStatType.CriticalDamage, this) : 0f;

        // 6. Przekazanie DO KONTROLERA (zmieniamy parametry)
        controller?.UpdateCombatStats(
            efficiency,
            isOverdriven, overdriveMod,
            rangeMulti, flatRange, 
            damageMulti, flatDamage, 
            fireRateMulti, flatFireRate,
            flatArmorPen, flatMagicPen, flatCritChan, flatCritDmg
            );
    }

    public override bool TryAddWorker(Race race)
    {
        bool success = base.TryAddWorker(race);
        if (success) RecalculateStats();
        return success;
    }
    
    private void EndOverdrive()
    {
        isOverdriven = false;
        RecalculateStats();
    }

    public override void RemoveWorker(Race race)
    {
        base.RemoveWorker(race);
        RecalculateStats();
    }
    
    public float[] GetEfficencyTable() {return efficencyTable;}
}