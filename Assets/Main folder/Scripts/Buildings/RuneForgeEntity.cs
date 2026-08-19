using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages the Rune Forge building, allowing dwarves to equip runes
/// and providing modifiers to specific tower types via the global registry.
/// </summary>
public class RuneForgeEntity : BuildingEntity
{
    private const string SOURCE_PREFIX = "Forge_";

    [Header("Konfiguracja Kuzni")]
    public TowerData infusedTowerData;

    [Header("Stan")] 
    [SerializeField] private bool dwarfAssigned;

    public List<RuneItem> equippedRunes = new();

    private string myRegistrySourceID;
    private Citizen reservedDwarf;

    protected override void OnDestroy()
    {
        base.OnDestroy();
        ReleaseDwarf();

        if (RuneManager.Instance != null)
        {
            foreach (var rune in equippedRunes)
                RuneManager.Instance.playerRunes.Add(rune);
            RuneManager.Instance.NotifyInventoryChanged();
        }

        GlobalModifierRegistry.Instance?.Unregister(myRegistrySourceID);
    }

    public override void Initialize(BuildingData buildingData)
    {
        base.Initialize(buildingData);
        myRegistrySourceID = SOURCE_PREFIX + gameObject.GetInstanceID();

        TryReserveDwarf();
        RecalculateSlotsAndModifiers();
    }

    public new void ForceRescan()
    {
        base.ForceRescan();
        RecalculateSlotsAndModifiers();
    }

    private void TryReserveDwarf()
    {
        if (CitizenManager.Instance == null) return;

        reservedDwarf = CitizenManager.Instance.citizens.Find(c => c.race == Race.Dwarves && c.workState == WorkState.Idle);

        if (reservedDwarf != null)
        {
            reservedDwarf.workState = WorkState.Assigned;
            dwarfAssigned = true;
        }
        else
        {
            dwarfAssigned = false;
        }
    }

    private void ReleaseDwarf()
    {
        if (reservedDwarf == null || !dwarfAssigned) return;
        reservedDwarf.workState = WorkState.Idle;
        reservedDwarf = null;
        dwarfAssigned = false;
    }

    public int GetMaxSlots()
    {
        var baseSlots = 1;
        var runeTowersCount = 0;

        var myCell = GetComponentInParent<HexCell>();
        if (myCell != null && HexMapVisualizer.Instance != null)
        {
            var neighbors = HexGridMath.GetNeighbors(myCell.localCoord);
            foreach (var n in neighbors)
            {
                var neighborCell = HexMapVisualizer.Instance.GetHexCell(myCell.chunkCoord, n);
                if (neighborCell != null)
                    if (neighborCell.GetComponentInChildren<RuneTowerEntity>() != null)
                        runeTowersCount++;
            }
        }

        return Mathf.Min(4, baseSlots + runeTowersCount);
    }

    public bool CanModifyRunes()
    {
        return dwarfAssigned &&
               TimePhaseManager.Instance != null &&
               TimePhaseManager.Instance.CurrentTimePhase != TimePhases.Night;
    }

    public bool EquipRune(RuneItem rune)
    {
        if (!CanModifyRunes()) return false;
        if (equippedRunes.Count >= GetMaxSlots()) return false;
        if (!RuneManager.Instance.playerRunes.Contains(rune)) return false;

        RuneManager.Instance.playerRunes.Remove(rune);
        equippedRunes.Add(rune);

        RecalculateSlotsAndModifiers();
        RuneManager.Instance.NotifyInventoryChanged();
        return true;
    }

    public bool UnequipRune(RuneItem rune)
    {
        if (!CanModifyRunes()) return false;
        if (!equippedRunes.Contains(rune)) return false;

        equippedRunes.Remove(rune);
        RuneManager.Instance.playerRunes.Add(rune);

        RecalculateSlotsAndModifiers();
        RuneManager.Instance.NotifyInventoryChanged();
        return true;
    }

    public void SetInfusedTower(TowerData tower)
    {
        infusedTowerData = tower;
        RecalculateSlotsAndModifiers();
    }

    private void RecalculateSlotsAndModifiers()
    {
        var maxSlots = GetMaxSlots();
        while (equippedRunes.Count > maxSlots)
        {
            var runeToEject = equippedRunes.Last();
            equippedRunes.Remove(runeToEject);
            if (RuneManager.Instance != null) RuneManager.Instance.playerRunes.Add(runeToEject);
        }

        if (GlobalModifierRegistry.Instance == null) return;

        GlobalModifierRegistry.Instance.Unregister(myRegistrySourceID);

        if (infusedTowerData == null || !dwarfAssigned) return;

        var sums = new Dictionary<TowerStatType, (float flat, float percent)>();

        foreach (var rune in equippedRunes)
        {
            AddStatToSum(sums, rune.definition.primaryStat, rune.definition.valueType, rune.primaryValue);

            if (rune.hasPenalty) AddStatToSum(sums, rune.penaltyStat, rune.penaltyValueType, rune.penaltyValue);
        }

        foreach (var kvp in sums)
        {
            var stat = kvp.Key;
            var totalFlat = kvp.Value.flat;
            var totalPercent = kvp.Value.percent;

            var multiplier = 1f + totalPercent / 100f;

            var modifier = new StatModifier(
                myRegistrySourceID,
                stat,
                infusedTowerData,
                multiplier,
                totalFlat);

            GlobalModifierRegistry.Instance.Register(modifier);
        }

        foreach (var building in BuildingRegistry.Instance.AllBuildings)
            if (building is TowerEntity tower && tower.data == infusedTowerData)
                tower.RecalculateStats();
    }

    private void AddStatToSum(Dictionary<TowerStatType, (float flat, float pct)> dict, RuneStatType runeStat,
        RuneValueType type, float value)
    {
        var towerStat = MapRuneStatToTowerStat(runeStat);

        if (!dict.ContainsKey(towerStat)) dict[towerStat] = (0f, 0f);

        var current = dict[towerStat];
        if (type == RuneValueType.Flat) current.flat += value;
        else current.pct += value;

        dict[towerStat] = current;
    }

    private TowerStatType MapRuneStatToTowerStat(RuneStatType rStat)
    {
        return rStat switch
        {
            RuneStatType.Range => TowerStatType.Range,
            RuneStatType.Damage => TowerStatType.Damage,
            RuneStatType.FireRate => TowerStatType.FireRate,
            RuneStatType.ArmorPenetration => TowerStatType.ArmorPenetration,
            RuneStatType.MagicPenetration => TowerStatType.MagicPenetration,
            RuneStatType.CriticalChance => TowerStatType.CriticalChance,
            RuneStatType.CriticalDamage => TowerStatType.CriticalDamage,
            _ => TowerStatType.Damage
        };
    }
}