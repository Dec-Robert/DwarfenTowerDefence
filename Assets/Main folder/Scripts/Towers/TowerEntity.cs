using UnityEngine;
using System.Collections.Generic;

public class TowerEntity : BuildingEntity
{
    [Header("Referencje")]
    public TowerController controller;

    public bool hasSupplies = true;

    private static readonly float[] efficiencyTable = new float[] { 0.2f, 1.0f, 1.1f, 1.2f, 1.3f, 1.35f, 1.4f };

    protected override void Start()
    {
        if (controller == null) controller = GetComponent<TowerController>();
        base.Start();

        RecalculateStats();
    }

    protected override void HandleDayReset()
    {
        base.HandleDayReset();
        hasSupplies = true;
        RecalculateStats();
    }

    public override void ProcessUpkeep()
    {
        if (!isBuildingActive) return;

        int crewCount = GetTotalWorkerCount();
        if (crewCount <= 0)
        {
            hasSupplies = false;
            RecalculateStats();
            return;
        }

        Workers?.LockWorkers();

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

    public float GetCurrentEfficiency()
    {
        int crewCount = GetTotalWorkerCount();
        BuildingData bData = controller != null && controller.towerData != null ? controller.towerData : data;

        float crewEfficiency;
        if (bData != null)
        {
            crewEfficiency = bData.GetEffectiveEfficiencyForWorkers(crewCount);
        }
        else
        {
            crewEfficiency = crewCount < efficiencyTable.Length
                ? efficiencyTable[crewCount]
                : efficiencyTable[efficiencyTable.Length - 1];
        }

        if (crewCount <= 0)
        {
            return crewEfficiency;
        }

        float supplyPenalty = hasSupplies ? 0f : 0.5f;
        return Mathf.Max(0.1f, crewEfficiency - supplyPenalty);
    }

    public void RecalculateStats()
    {
        TowerData tData = controller != null ? controller.towerData : data as TowerData;
        if (tData == null) return;

        float finalEfficiency = GetCurrentEfficiency();

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
            criticalChancel = Mathf.Clamp01(tData.criticalChancel + flatCritChan),
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
        if (success)
        {
            if (TimePhaseManager.Instance != null && TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Night)
            {
                Workers?.LockWorkers();
            }
            RecalculateStats();
        }
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

    public float[] GetEfficencyTable()
    {
        BuildingData bData = controller != null && controller.towerData != null ? controller.towerData : data;
        if (bData != null)
        {
            int maxWorkers = bData.baseMaxWorkers
                + (Upgrades != null ? Upgrades.LocalBonusWorkers : 0)
                + (CityStatsManager.Instance != null ? CityStatsManager.Instance.globalBonusWorkersPerShift : 0);
            return bData.GetEffectiveEfficiencyTable(maxWorkers + 1);
        }
        return efficiencyTable;
    }
}