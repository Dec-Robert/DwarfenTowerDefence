using System;
using System.Collections.Generic;
using UnityEngine;

public enum DruidTowerState
{
    Ruined,
    RebuiltPendingDraft,
    Active
}

public enum MoneySinkTarget
{
    Damage,
    Range
}

public class DruidTowerEntity : BuildingEntity
{
    public static DruidTowerEntity Instance { get; private set; }

    [Header("Konfiguracja Wieży Druida")]
    public List<ResourceCost> rebuildCosts = new List<ResourceCost>
    {
        new ResourceCost { type = ResourceType.Ether, amount = 40 }
    };
    public SpellDatabaseSO spellDatabase;

    [Header("Wizualia Stanów (Opcjonalne)")]
    public GameObject ruinedVisual;
    public GameObject activeVisual;

    public DruidTowerState State { get; private set; } = DruidTowerState.Ruined;
    public List<SpellDataSO> DraftedOptions { get; } = new List<SpellDataSO>();
    public SpellDataSO ActiveSpell { get; private set; }

    public int CurrentLinearTier { get; private set; } = 0;
    public int SelectedSpecializationIndex { get; private set; } = -1;

    public int MoneySinkCurrentPoints { get; private set; } = 0;
    public int MoneySinkDamageLevels { get; private set; } = 0;
    public int MoneySinkRangeLevels { get; private set; } = 0;
    public MoneySinkTarget TargetMoneySinkStat { get; set; } = MoneySinkTarget.Damage;

    public event Action OnDruidTowerStateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticInstance()
    {
        Instance = null;
    }

    private void Awake()
    {
        Instance = this;
        EnsureDatabase();
    }

    protected override void Start()
    {
        base.Start();
        EnsureDatabase();
        RefreshVisualState();
    }

    protected override void SetupAtmosphericLighting()
    {
        base.SetupAtmosphericLighting();
        RefreshVisualState();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void EnsureDatabase()
    {
        if (spellDatabase == null)
        {
            spellDatabase = Resources.Load<SpellDatabaseSO>("SpellDatabase");
        }
        if (spellDatabase == null)
        {
            spellDatabase = SpellDatabaseSO.CreateRuntimeDefault();
        }
    }

    public void SetStateForTesting(DruidTowerState state)
    {
        State = state;
        RefreshVisualState();
    }

    public bool IsChunkSettled()
    {
        var myCell = GetComponentInParent<HexCell>();
        if (myCell == null || MapExpansionManager.Instance == null || MapExpansionManager.Instance.activeChunks == null)
            return false;

        return MapExpansionManager.Instance.activeChunks.TryGetValue(myCell.chunkCoord, out var chunkData)
            && chunkData.baseState == ChunkState.Settled;
    }

    public bool CanRebuild(out string blockReason)
    {
        if (State != DruidTowerState.Ruined)
        {
            blockReason = "Jama została już odbudowana.";
            return false;
        }

        if (!IsChunkSettled())
        {
            blockReason = "Zasiedl ten teren posterunkiem, aby odbudować jamę.";
            return false;
        }

        if (ResourceManager.Instance == null)
        {
            blockReason = "Brak menedżera zasobów.";
            return false;
        }

        if (rebuildCosts != null)
        {
            for (int i = 0; i < rebuildCosts.Count; i++)
            {
                var cost = rebuildCosts[i];
                if (cost.amount <= 0) continue;

                if (!ResourceManager.Instance.CanAfford(cost.type, cost.amount))
                {
                    blockReason = $"Brak wystarczających surowców ({cost.type}: {cost.amount}).";
                    return false;
                }
            }
        }

        blockReason = string.Empty;
        return true;
    }

    public bool TryRebuild(System.Random rng = null)
    {
        if (!CanRebuild(out _)) return false;

        if (rebuildCosts != null)
        {
            for (int i = 0; i < rebuildCosts.Count; i++)
            {
                var cost = rebuildCosts[i];
                if (cost.amount <= 0) continue;

                if (!ResourceManager.Instance.SpendResource(cost.type, cost.amount))
                    return false;
            }
        }

        EnsureDatabase();
        DraftedOptions.Clear();
        DraftedOptions.AddRange(spellDatabase.DrawRandomSpells(2, rng));

        State = DruidTowerState.RebuiltPendingDraft;
        RefreshVisualState();
        OnDruidTowerStateChanged?.Invoke();
        return true;
    }

    public bool SelectDraftedSpell(int optionIndex)
    {
        if (State != DruidTowerState.RebuiltPendingDraft) return false;
        if (optionIndex < 0 || optionIndex >= DraftedOptions.Count) return false;

        ActiveSpell = DraftedOptions[optionIndex];
        CurrentLinearTier = 0;
        SelectedSpecializationIndex = -1;
        MoneySinkCurrentPoints = 0;
        MoneySinkDamageLevels = 0;
        MoneySinkRangeLevels = 0;
        State = DruidTowerState.Active;

        RefreshVisualState();
        SpellCastManager.Instance?.RegisterDruidTower(this);
        OnDruidTowerStateChanged?.Invoke();
        return true;
    }

    public bool IsLinearTreeCompleted()
    {
        if (ActiveSpell == null) return false;
        return CurrentLinearTier >= ActiveSpell.linearTiers.Count;
    }

    public bool IsTreeCompleted()
    {
        return IsLinearTreeCompleted() && SelectedSpecializationIndex >= 0;
    }

    public bool CanUpgradeNextLinearTier()
    {
        if (State != DruidTowerState.Active || ActiveSpell == null) return false;
        if (IsLinearTreeCompleted()) return false;
        if (ResourceManager.Instance == null) return false;

        var node = ActiveSpell.linearTiers[CurrentLinearTier];
        return ResourceManager.Instance.CanAfford(node.costs);
    }

    public bool TryUpgradeNextLinearTier()
    {
        if (!CanUpgradeNextLinearTier()) return false;

        var node = ActiveSpell.linearTiers[CurrentLinearTier];
        if (!ResourceManager.Instance.SpendResources(node.costs)) return false;

        CurrentLinearTier++;
        SpellCastManager.Instance?.OnSpellStatsChanged();
        OnDruidTowerStateChanged?.Invoke();
        return true;
    }

    public bool CanSelectSpecialization(int specIndex)
    {
        if (State != DruidTowerState.Active || ActiveSpell == null) return false;
        if (!IsLinearTreeCompleted() || SelectedSpecializationIndex >= 0) return false;
        if (specIndex < 0 || specIndex >= ActiveSpell.specializations.Count) return false;
        if (ResourceManager.Instance == null) return false;

        return ResourceManager.Instance.CanAfford(ActiveSpell.specializations[specIndex].costs);
    }

    public bool TrySelectSpecialization(int specIndex)
    {
        if (!CanSelectSpecialization(specIndex)) return false;

        var spec = ActiveSpell.specializations[specIndex];
        if (!ResourceManager.Instance.SpendResources(spec.costs)) return false;

        SelectedSpecializationIndex = specIndex;
        SpellCastManager.Instance?.OnSpellStatsChanged();
        OnDruidTowerStateChanged?.Invoke();
        return true;
    }

    public int TotalMoneySinkLevels => MoneySinkDamageLevels + MoneySinkRangeLevels;

    public int GetRequiredMoneySinkPoints()
    {
        if (ActiveSpell == null) return 100;
        return ActiveSpell.GetMoneySinkRequiredPoints(TotalMoneySinkLevels);
    }

    public bool TryContributeToMoneySink(ResourceType type, int amount)
    {
        if (State != DruidTowerState.Active || !IsTreeCompleted() || ActiveSpell == null) return false;
        if (amount <= 0 || ResourceManager.Instance == null) return false;

        int available = ResourceManager.Instance.GetResourceAmount(type);
        int toSpend = Mathf.Min(amount, available);
        if (toSpend <= 0) return false;

        if (!ResourceManager.Instance.SpendResource(type, toSpend)) return false;

        EnsureDatabase();
        int pointValue = spellDatabase.GetResourcePointValue(type);
        MoneySinkCurrentPoints += toSpend * pointValue;

        int required = GetRequiredMoneySinkPoints();
        while (MoneySinkCurrentPoints >= required)
        {
            MoneySinkCurrentPoints -= required;
            if (TargetMoneySinkStat == MoneySinkTarget.Damage)
                MoneySinkDamageLevels++;
            else
                MoneySinkRangeLevels++;

            required = GetRequiredMoneySinkPoints();
        }

        SpellCastManager.Instance?.OnSpellStatsChanged();
        OnDruidTowerStateChanged?.Invoke();
        return true;
    }

    // =========================================================================
    // Wyliczanie Efektywnych Statystyk Zaklęcia
    // =========================================================================

    public SpellSpecializationData GetActiveSpecialization()
    {
        if (ActiveSpell == null || SelectedSpecializationIndex < 0 || SelectedSpecializationIndex >= ActiveSpell.specializations.Count)
            return null;
        return ActiveSpell.specializations[SelectedSpecializationIndex];
    }

    private void SumModifiers(SpellUpgradeStatType statType, out float flatSum)
    {
        flatSum = 0f;
        if (ActiveSpell == null) return;

        int tierCount = Mathf.Min(CurrentLinearTier, ActiveSpell.linearTiers.Count);
        for (int i = 0; i < tierCount; i++)
        {
            foreach (var mod in ActiveSpell.linearTiers[i].modifiers)
            {
                if (mod.statType == statType) flatSum += mod.value;
            }
        }

        var spec = GetActiveSpecialization();
        if (spec != null)
        {
            foreach (var mod in spec.modifiers)
            {
                if (mod.statType == statType) flatSum += mod.value;
            }
        }
    }

    public float GetEffectiveDamage()
    {
        if (ActiveSpell == null) return 0f;
        SumModifiers(SpellUpgradeStatType.DamageFlat, out float dmgFlat);
        SumModifiers(SpellUpgradeStatType.DamageMultiplier, out float dmgMult);

        float sinkMult = MoneySinkDamageLevels * ActiveSpell.moneySinkDamageMultiplierPerLevel;
        return Mathf.Max(0f, (ActiveSpell.baseDamage + dmgFlat) * (1f + dmgMult + sinkMult));
    }

    public float GetEffectiveRadius()
    {
        if (ActiveSpell == null) return 0f;
        SumModifiers(SpellUpgradeStatType.RadiusFlat, out float radiusFlat);
        float sinkRadius = MoneySinkRangeLevels * ActiveSpell.moneySinkRadiusPerLevel;
        return Mathf.Max(0.5f, ActiveSpell.baseRadius + radiusFlat + sinkRadius);
    }

    public float GetEffectiveCooldown()
    {
        if (ActiveSpell == null) return 0f;
        SumModifiers(SpellUpgradeStatType.CooldownReduction, out float cdRed);
        float baseCd = ActiveSpell.castLimitMode == SpellCastLimitMode.RechargeableCharges
            ? ActiveSpell.chargeRechargeSeconds
            : ActiveSpell.cooldownSeconds;
        return Mathf.Max(2f, baseCd - cdRed);
    }

    public int GetEffectiveMaxUsesOrCharges()
    {
        if (ActiveSpell == null) return 0;
        SumModifiers(SpellUpgradeStatType.BonusCharges, out float bonus);
        int baseVal = ActiveSpell.castLimitMode == SpellCastLimitMode.LimitedUsesPerNight
            ? ActiveSpell.maxUsesPerNight
            : ActiveSpell.maxCharges;
        return Mathf.Max(1, baseVal + Mathf.RoundToInt(bonus));
    }

    private void RefreshVisualState()
    {
        if (ruinedVisual != null) ruinedVisual.SetActive(State == DruidTowerState.Ruined);
        if (activeVisual != null) activeVisual.SetActive(State != DruidTowerState.Ruined);

        var nightLight = GetComponentInChildren<SettlementLight>(true);
        if (nightLight != null)
        {
            nightLight.SetOperationalEnabled(State != DruidTowerState.Ruined);
        }
    }
}
