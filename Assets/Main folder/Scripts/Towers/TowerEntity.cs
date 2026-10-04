using UnityEngine;
using System.Collections.Generic;

public class TowerEntity : BuildingEntity
{
    [Header("Referencje")]
    public TowerController controller;

    public bool hasSupplies = true;
    private bool isOverdriven = false;

    private static readonly float[] efficiencyTable = new float[] { 0.2f, 1.0f, 1.1f, 1.2f, 1.3f, 1.35f, 1.4f };
    private static readonly float overdriveMod = 1.2f;
    private List<ResourceCost> overdriveCost;

    protected override void Start()
    {
        if (controller == null) controller = GetComponent<TowerController>();
        base.Start();

        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted += EndOverdrive;
        }

        if (controller != null && controller.towerData != null)
        {
            overdriveCost = controller.towerData.upkeepPerCycle;
        }

        RecalculateStats();
    }

    protected override void OnDestroy()
    {
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= EndOverdrive;
        }
        base.OnDestroy();
    }

    public override void ProcessUpkeep()
    {
        if (!isBuildingActive) return;

        if (data != null && data.upkeepPerCycle != null && data.upkeepPerCycle.Count > 0)
        {
            hasSupplies = ResourceManager.Instance != null && ResourceManager.Instance.SpendResources(data.upkeepPerCycle);
        }
        else
        {
            hasSupplies = true;
        }

        RecalculateStats();
    }

    public void StartOverdrive()
    {
        if (isOverdriven) return;

        if (ResourceManager.Instance != null && ResourceManager.Instance.SpendResources(overdriveCost))
        {
            isOverdriven = true;
            RecalculateStats();
        }
    }

    public void RevokeOverdrive()
    {
        if (!isOverdriven) return;

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.AddResources(overdriveCost);
        }

        isOverdriven = false;
        RecalculateStats();
    }

    private void EndOverdrive()
    {
        isOverdriven = false;
        RecalculateStats();
    }

    public void RecalculateStats()
    {
        TowerData tData = controller != null ? controller.towerData : data as TowerData;
        if (tData == null) return;

        int crewCount = GetAssignedCitizens().Count;
        float crewEfficiency = crewCount < efficiencyTable.Length
            ? efficiencyTable[crewCount]
            : efficiencyTable[efficiencyTable.Length - 1];

        float supplyPenalty = hasSupplies ? 0f : 0.5f;
        float finalEfficiency = Mathf.Max(0.1f, crewEfficiency - supplyPenalty);

        if (isOverdriven)
        {
            finalEfficiency *= overdriveMod;
        }

        var registry = GlobalModifierRegistry.Instance;

        float rangeMulti = registry != null ? registry.GetMultiplier(TowerStatType.Range, this) : 1f;
        float damageMulti = registry != null ? registry.GetMultiplier(TowerStatType.Damage, this) : 1f;
        float fireRateMulti = registry != null ? registry.GetMultiplier(TowerStatType.FireRate, this) : 1f;

        float flatRange = registry != null ? registry.GetFlat(TowerStatType.Range, this) : 0f;
        float flatDamage = registry != null ? registry.GetFlat(TowerStatType.Damage, this) : 0f;
        float flatFireRate = registry != null ? registry.GetFlat(TowerStatType.FireRate, this) : 0f;

        float flatArmorPen = registry != null ? registry.GetFlat(TowerStatType.ArmorPenetration, this) : 0f;
        float flatMagicPen = registry != null ? registry.GetFlat(TowerStatType.MagicPenetration, this) : 0f;
        float flatCritChan = registry != null ? registry.GetFlat(TowerStatType.CriticalChance, this) : 0f;
        float flatCritDmg = registry != null ? registry.GetFlat(TowerStatType.CriticalDamage, this) : 0f;

        TowerStats calculatedStats = new TowerStats
        {
            damage = (tData.baseDamage + flatDamage) * damageMulti * finalEfficiency,
            range = (tData.baseRange + flatRange) * rangeMulti,
            fireRate = (tData.fireRate + flatFireRate) * fireRateMulti * finalEfficiency,
            criticalChancel = Mathf.Clamp01(tData.criticalChancel + flatCritChan + (isOverdriven ? 0.1f : 0f)),
            criticalDamageMultiplier = Mathf.Max(1.0f, tData.criticalDamageMultiplier + flatCritDmg),
            armorPenetration = Mathf.Clamp01(tData.armorPenetration + flatArmorPen),
            magicPenetration = Mathf.Clamp01(tData.magicPenetration + flatMagicPen),
            damageType = tData.damageType
        };

        controller?.ApplyCalculatedStats(calculatedStats, finalEfficiency);
    }

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

    public override void HandleWorkerLost(Citizen citizen)
    {
        base.HandleWorkerLost(citizen);
        RecalculateStats();
    }

    public float[] GetEfficencyTable() => efficiencyTable;
}