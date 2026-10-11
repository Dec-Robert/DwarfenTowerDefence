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
        float armorPenMulti = registry != null ? registry.GetMultiplier(TowerStatType.ArmorPenetration, this) : 1f;
        float magicPenMulti = registry != null ? registry.GetMultiplier(TowerStatType.MagicPenetration, this) : 1f;
        float critChanMulti = registry != null ? registry.GetMultiplier(TowerStatType.CriticalChance, this) : 1f;
        float critDmgMulti = registry != null ? registry.GetMultiplier(TowerStatType.CriticalDamage, this) : 1f;

        float flatRange = registry != null ? registry.GetFlat(TowerStatType.Range, this) : 0f;
        float flatDamage = registry != null ? registry.GetFlat(TowerStatType.Damage, this) : 0f;
        float flatFireRate = registry != null ? registry.GetFlat(TowerStatType.FireRate, this) : 0f;

        float flatArmorPen = registry != null ? registry.GetFlat(TowerStatType.ArmorPenetration, this) : 0f;
        float flatMagicPen = registry != null ? registry.GetFlat(TowerStatType.MagicPenetration, this) : 0f;
        float flatCritChan = registry != null ? registry.GetFlat(TowerStatType.CriticalChance, this) : 0f;
        float flatCritDmg = registry != null ? registry.GetFlat(TowerStatType.CriticalDamage, this) : 0f;

        float baseCritChance = tData.criticalChancel > 1f ? tData.criticalChancel / 100f : tData.criticalChancel;
        float baseArmorPen = tData.armorPenetration > 1f ? tData.armorPenetration / 100f : tData.armorPenetration;
        float baseMagicPen = tData.magicPenetration > 1f ? tData.magicPenetration / 100f : tData.magicPenetration;
        float normFlatCrit = Mathf.Abs(flatCritChan) >= 1f ? flatCritChan / 100f : flatCritChan;
        float normFlatCritDmg = Mathf.Abs(flatCritDmg) >= 1f ? flatCritDmg / 100f : flatCritDmg;
        float normFlatArmorPen = Mathf.Abs(flatArmorPen) >= 1f ? flatArmorPen / 100f : flatArmorPen;
        float normFlatMagicPen = Mathf.Abs(flatMagicPen) >= 1f ? flatMagicPen / 100f : flatMagicPen;

        TowerStats calculatedStats = new TowerStats
        {
            damage = (tData.baseDamage + flatDamage) * damageMulti * finalEfficiency,
            range = (tData.baseRange + flatRange) * rangeMulti,
            fireRate = (tData.fireRate + flatFireRate) * fireRateMulti * finalEfficiency,
            criticalChancel = Mathf.Clamp01((baseCritChance + normFlatCrit) * critChanMulti),
            criticalDamageMultiplier = Mathf.Max(1.0f, (tData.criticalDamageMultiplier + normFlatCritDmg) * critDmgMulti),
            armorPenetration = Mathf.Clamp01((baseArmorPen + normFlatArmorPen) * armorPenMulti),
            magicPenetration = Mathf.Clamp01((baseMagicPen + normFlatMagicPen) * magicPenMulti),
            damageType = tData.damageType
        };

        if (controller != null)
        {
            controller.canShoot = isBuildingActive;
        }
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