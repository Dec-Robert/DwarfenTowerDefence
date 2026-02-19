using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Typy statystyk które można modyfikować.
/// </summary>
public enum TowerStatType
{
    Range,
    Damage,
    FireRate,
    GlobalProduction  // Dla budynków ekonomicznych (Beacon)
}

/// <summary>
/// Zakres działania modyfikatora.
/// </summary>
public enum ModifierScope
{
    /// <summary>Dotyczy wszystkich wież.</summary>
    Global,
    /// <summary>Dotyczy wież konkretnego typu (TowerData).</summary>
    PerTowerData,
    /// <summary>Dotyczy konkretnej instancji wieży.</summary>
    PerInstance
}

/// <summary>
/// Pojedynczy modyfikator statystyki.
/// Może być multiplikatywny (mnożnik) lub addytywny (flat bonus).
/// </summary>
public class StatModifier
{
    public readonly string        sourceId;    // Kto dodał (np. "Beacon", "ResearchLab")
    public readonly TowerStatType stat;
    public readonly ModifierScope scope;
    public readonly float         multiplier;  // np. 1.1f = +10%
    public readonly float         flat;        // np. +5 flat do zasięgu

    // Scope: Global
    public StatModifier(string sourceId, TowerStatType stat, float multiplier = 1f, float flat = 0f)
    {
        this.sourceId   = sourceId;
        this.stat       = stat;
        this.scope      = ModifierScope.Global;
        this.multiplier = multiplier;
        this.flat       = flat;
    }

    // Scope: PerTowerData
    public StatModifier(string sourceId, TowerStatType stat, TowerData towerData, float multiplier = 1f, float flat = 0f)
    {
        this.sourceId       = sourceId;
        this.stat           = stat;
        this.scope          = ModifierScope.PerTowerData;
        this.multiplier     = multiplier;
        this.flat           = flat;
        this.targetTowerData = towerData;
    }

    // Scope: PerInstance
    public StatModifier(string sourceId, TowerStatType stat, TowerEntity instance, float multiplier = 1f, float flat = 0f)
    {
        this.sourceId        = sourceId;
        this.stat            = stat;
        this.scope           = ModifierScope.PerInstance;
        this.multiplier      = multiplier;
        this.flat            = flat;
        this.targetInstance  = instance;
    }

    // Opcjonalne targety (null jeśli Global)
    public readonly TowerData   targetTowerData;
    public readonly TowerEntity targetInstance;
}

/// <summary>
/// Pasywny centralny rejestr modyfikatorów statystyk wież i budynków.
///
/// ZASADY:
/// - Rejestr TYLKO przechowuje i sumuje. Nie subskrybuje żadnych eventów.
/// - Każdy system (Beacon, badania, buffy) sam się rejestruje i wyrejestrowuje.
/// - Wieże pytają rejestr przy RecalculateStats().
///
/// UŻYCIE:
///   // Rejestracja (np. w BeaconEntity gdy zmienia się poziom)
///   GlobalModifierRegistry.Instance.Register(new StatModifier("Beacon", TowerStatType.Range, multiplier: 1.1f));
///
///   // Odpytywanie (np. w TowerEntity.RecalculateStats)
///   float rangeMulti = GlobalModifierRegistry.Instance.GetMultiplier(TowerStatType.Range, this);
/// </summary>
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

    // =========================================================================
    // Rejestracja
    // =========================================================================

    /// <summary>Dodaje modyfikator do rejestru. Jeśli sourceId już istnieje dla tej statystyki – zastępuje.</summary>
    public void Register(StatModifier modifier)
    {
        // Usuwamy stary modyfikator z tego samego źródła dla tej samej statystyki i scope'u
        modifiers.RemoveAll(m =>
            m.sourceId == modifier.sourceId &&
            m.stat     == modifier.stat     &&
            m.scope    == modifier.scope);

        modifiers.Add(modifier);
    }

    /// <summary>Usuwa wszystkie modyfikatory z danego źródła.</summary>
    public void Unregister(string sourceId)
    {
        modifiers.RemoveAll(m => m.sourceId == sourceId);
    }

    /// <summary>Usuwa modyfikatory konkretnej statystyki z danego źródła.</summary>
    public void Unregister(string sourceId, TowerStatType stat)
    {
        modifiers.RemoveAll(m => m.sourceId == sourceId && m.stat == stat);
    }

    // =========================================================================
    // Odpytywanie
    // =========================================================================

    /// <summary>
    /// Zwraca łączny mnożnik dla danej statystyki i konkretnej instancji wieży.
    /// Uwzględnia modyfikatory Global + PerTowerData + PerInstance.
    /// </summary>
    public float GetMultiplier(TowerStatType stat, TowerEntity tower)
    {
        float result = 1f;

        foreach (var mod in modifiers)
        {
            if (mod.stat != stat) continue;
            if (!AppliesToTower(mod, tower)) continue;
            result *= mod.multiplier;
        }

        return result;
    }

    /// <summary>
    /// Zwraca łączny flat bonus dla danej statystyki i konkretnej instancji wieży.
    /// </summary>
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

    /// <summary>
    /// Skrót – zwraca globalny mnożnik produkcji (dla budynków ekonomicznych).
    /// </summary>
    public float GetGlobalProductionMultiplier()
    {
        float result = 1f;
        foreach (var mod in modifiers)
        {
            if (mod.stat != TowerStatType.GlobalProduction) continue;
            if (mod.scope != ModifierScope.Global) continue;
            result *= mod.multiplier;
        }
        return result;
    }

    // =========================================================================
    // Debug
    // =========================================================================

    public void LogAll()
    {
        Debug.Log($"[GlobalModifierRegistry] Aktywnych modyfikatorów: {modifiers.Count}");
        foreach (var m in modifiers)
            Debug.Log($"  [{m.sourceId}] {m.stat} | scope={m.scope} | x{m.multiplier} +{m.flat}");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private bool AppliesToTower(StatModifier mod, TowerEntity tower)
    {
        return mod.scope switch
        {
            ModifierScope.Global       => true,
            ModifierScope.PerTowerData => tower != null && mod.targetTowerData != null
                                          && tower.data == mod.targetTowerData,
            ModifierScope.PerInstance  => tower != null && mod.targetInstance == tower,
            _                          => false
        };
    }
}
