using System;
using System.Collections.Generic;
using UnityEngine;

public enum TowerStatType
{
    Range,
    Damage,
    FireRate,
    ArmorPenetration,
    MagicPenetration,
    CriticalChance,
    CriticalDamage
}

public enum ModifierScope
{
    Global,
    PerTowerData,
    PerInstance
}

public class StatModifier
{
    public readonly string sourceId;
    public readonly TowerStatType stat;
    public readonly ModifierScope scope;
    public readonly float multiplier;
    public readonly float flat;
    public readonly TowerData targetTowerData;
    public readonly TowerEntity targetInstance;

    public StatModifier(string sourceId, TowerStatType stat, float multiplier = 1f, float flat = 0f)
    {
        this.sourceId = sourceId;
        this.stat = stat;
        this.scope = ModifierScope.Global;
        this.multiplier = multiplier;
        this.flat = flat;
    }

    public StatModifier(string sourceId, TowerStatType stat, TowerData towerData, float multiplier = 1f, float flat = 0f)
    {
        this.sourceId = sourceId;
        this.stat = stat;
        this.scope = ModifierScope.PerTowerData;
        this.multiplier = multiplier;
        this.flat = flat;
        this.targetTowerData = towerData;
    }

    public StatModifier(string sourceId, TowerStatType stat, TowerEntity instance, float multiplier = 1f, float flat = 0f)
    {
        this.sourceId = sourceId;
        this.stat = stat;
        this.scope = ModifierScope.PerInstance;
        this.multiplier = multiplier;
        this.flat = flat;
        this.targetInstance = instance;
    }
}

public class GlobalModifierRegistry : MonoBehaviour
{
    public static GlobalModifierRegistry Instance { get; private set; }

    private readonly List<StatModifier> modifiers = new List<StatModifier>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Register(StatModifier modifier)
    {
        modifiers.RemoveAll(m =>
            m.sourceId == modifier.sourceId &&
            m.stat == modifier.stat &&
            m.scope == modifier.scope);

        modifiers.Add(modifier);
    }

    public void Unregister(string sourceId)
    {
        modifiers.RemoveAll(m => m.sourceId == sourceId);
    }

    public void Unregister(string sourceId, TowerStatType stat)
    {
        modifiers.RemoveAll(m => m.sourceId == sourceId && m.stat == stat);
    }

    public float GetMultiplier(TowerStatType stat, TowerEntity tower)
    {
        float totalPercentBonus = 0f;

        foreach (var mod in modifiers)
        {
            if (mod.stat != stat) continue;
            if (!AppliesToTower(mod, tower)) continue;

            totalPercentBonus += (mod.multiplier - 1f);
        }

        return Mathf.Max(0.1f, 1f + totalPercentBonus);
    }

    public float GetFlat(TowerStatType stat, TowerEntity tower)
    {
        float result = 0f;

        foreach (var mod in modifiers)
        {
            if (mod.stat != stat) continue;
            if (!AppliesToTower(mod, tower)) continue;
            result += mod.flat;
        }

        return result;
    }

    private bool AppliesToTower(StatModifier mod, TowerEntity tower)
    {
        return mod.scope switch
        {
            ModifierScope.Global => true,
            ModifierScope.PerTowerData => tower != null && mod.targetTowerData != null && tower.data == mod.targetTowerData,
            ModifierScope.PerInstance => tower != null && mod.targetInstance == tower,
            _ => false
        };
    }
}