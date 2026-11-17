using System;
using System.Collections.Generic;
using UnityEngine;

public class ToolWorkshopResearchManager : MonoBehaviour
{
    [Serializable]
    public class ActiveResearchState
    {
        public BuildingData targetBuilding;
        public string buildingKey;
        public int tier;
        public bool isOptionA;
        public BuildingUpgradeSO upgrade;
        public float currentProgress;
        public float requiredProgress;

        public bool IsActive => upgrade != null && !string.IsNullOrEmpty(buildingKey);
        public float NormalizedProgress => requiredProgress > 0f ? Mathf.Clamp01(currentProgress / requiredProgress) : 0f;
    }

    [Serializable]
    public class UnlockedUpgradeRecord
    {
        public int tier;
        public bool isOptionA;
        public BuildingUpgradeSO upgrade;
    }

    public static ToolWorkshopResearchManager Instance { get; private set; }

    [Header("Konfiguracja Drzewek Badań")]
    [SerializeField] private WorkshopTechTreeCatalogSO techCatalog;

    private readonly List<ToolWorkshopEntity> registeredWorkshops = new List<ToolWorkshopEntity>();
    private readonly Dictionary<string, List<UnlockedUpgradeRecord>> unlockedByBuilding =
        new Dictionary<string, List<UnlockedUpgradeRecord>>(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> activeSpecialEffects = new HashSet<string>(StringComparer.Ordinal);
    private ActiveResearchState activeResearch;
    private bool isSubscribedToTime;

    public WorkshopTechTreeCatalogSO TechCatalog => techCatalog;
    public ActiveResearchState ActiveResearch => activeResearch;

    public event Action OnResearchStateChanged;
    public event Action<BuildingData, int, BuildingUpgradeSO> OnUpgradeCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureCatalogLoaded();
    }

    private void Start()
    {
        SubscribeToTime();
    }

    private void OnEnable()
    {
        if (Instance == this) SubscribeToTime();
    }

    private void OnDisable()
    {
        UnsubscribeFromTime();
    }

    private void OnDestroy()
    {
        UnsubscribeFromTime();
        if (Instance == this) Instance = null;
    }

    private void SubscribeToTime()
    {
        if (isSubscribedToTime || TimePhaseManager.Instance == null) return;
        TimePhaseManager.Instance.OnMorningStarted += ProcessMorningTick;
        isSubscribedToTime = true;
    }

    private void UnsubscribeFromTime()
    {
        if (!isSubscribedToTime || TimePhaseManager.Instance == null) return;
        TimePhaseManager.Instance.OnMorningStarted -= ProcessMorningTick;
        isSubscribedToTime = false;
    }

    public void EnsureCatalogLoaded()
    {
        if (techCatalog != null) return;
#if UNITY_EDITOR
        techCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<WorkshopTechTreeCatalogSO>(
            "Assets/Main folder/ScriptableObjects/BuildingUpgrades/Workshop/WorkshopTechTreeCatalog.asset");
#endif
    }

    public void SetCatalogForTesting(WorkshopTechTreeCatalogSO catalog)
    {
        techCatalog = catalog;
    }

    public static bool IsWorkshopUnlockedInMeta()
    {
        if (DemoBuildConfig.IsDemoV2Active) return false;
        if (MetaUpgradeManager.Instance == null) return true;
        return MetaUpgradeManager.Instance.IsUnlocked(MetaEffectType.UnlockTrinityForce);
    }

    // =========================================================================
    // REJESTRACJA WARSZTATÓW I KALKULACJA CZASU T(n)
    // =========================================================================

    public void RegisterWorkshop(ToolWorkshopEntity workshop)
    {
        if (workshop == null || registeredWorkshops.Contains(workshop)) return;
        registeredWorkshops.Add(workshop);
        OnResearchStateChanged?.Invoke();
    }

    public void UnregisterWorkshop(ToolWorkshopEntity workshop)
    {
        if (workshop == null) return;
        if (registeredWorkshops.Remove(workshop))
        {
            OnResearchStateChanged?.Invoke();
        }
    }

    public void NotifyWorkshopsChanged()
    {
        OnResearchStateChanged?.Invoke();
    }

    public int ActiveFullyStaffedWorkshopsCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < registeredWorkshops.Count; i++)
            {
                var w = registeredWorkshops[i];
                if (w != null && w.IsContributingToResearch)
                    count++;
            }
            return count;
        }
    }

    public int TotalWorkshopsCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < registeredWorkshops.Count; i++)
            {
                if (registeredWorkshops[i] != null) count++;
            }
            return count;
        }
    }

    public int DiscoveredUpgradesCount
    {
        get
        {
            int total = 0;
            foreach (var kvp in unlockedByBuilding)
            {
                total += kvp.Value.Count;
            }
            return total;
        }
    }

    public static int CalculateBaseDuration(int tier, int discoveredUpgradesCount)
    {
        return Mathf.Clamp(tier, 1, 4) + Mathf.Max(0, discoveredUpgradesCount);
    }

    public static float CalculateDailyResearchSpeed(int activeWorkshops)
    {
        if (activeWorkshops <= 0) return 0f;
        return 1f + 0.25f * Mathf.Sqrt(activeWorkshops - 1);
    }

    public static float CalculateEstimatedDuration(int tier, int discoveredUpgradesCount, int activeWorkshops)
    {
        if (activeWorkshops <= 0) return float.PositiveInfinity;
        float baseDuration = CalculateBaseDuration(tier, discoveredUpgradesCount);
        float speed = CalculateDailyResearchSpeed(activeWorkshops);
        return baseDuration / speed;
    }

    public float GetEstimatedDurationForTier(int tier)
    {
        return CalculateEstimatedDuration(tier, DiscoveredUpgradesCount, ActiveFullyStaffedWorkshopsCount);
    }

    public float GetRemainingDaysEstimate()
    {
        if (activeResearch == null || !activeResearch.IsActive) return 0f;
        int n = ActiveFullyStaffedWorkshopsCount;
        if (n <= 0) return float.PositiveInfinity;
        float remainingWork = Mathf.Max(0f, activeResearch.requiredProgress - activeResearch.currentProgress);
        return remainingWork / CalculateDailyResearchSpeed(n);
    }

    // =========================================================================
    // ZARZĄDZANIE BADANIAMI
    // =========================================================================

    public bool TryStartResearch(BuildingData targetBuilding, int tier, bool isOptionA)
    {
        EnsureCatalogLoaded();
        if (targetBuilding == null || techCatalog == null) return false;
        if (!techCatalog.TryGetTree(targetBuilding, out var tree)) return false;

        int nextAllowedTier = GetUnlockedTierCount(targetBuilding) + 1;
        if (tier != nextAllowedTier || tier < 1 || tier > 4) return false;

        var tierEntry = tree.GetTier(tier);
        if (tierEntry == null) return false;

        var upgrade = tierEntry.GetOption(isOptionA);
        if (upgrade == null) return false;

        string key = GetBuildingKey(targetBuilding);
        activeResearch = new ActiveResearchState
        {
            targetBuilding = targetBuilding,
            buildingKey = key,
            tier = tier,
            isOptionA = isOptionA,
            upgrade = upgrade,
            currentProgress = 0f,
            requiredProgress = CalculateBaseDuration(tier, DiscoveredUpgradesCount)
        };

        OnResearchStateChanged?.Invoke();
        return true;
    }

    public void CancelCurrentResearch()
    {
        if (activeResearch == null || !activeResearch.IsActive) return;
        activeResearch = null;
        OnResearchStateChanged?.Invoke();
    }

    public void ProcessMorningTick()
    {
        ApplyDailyQuarryGateReinforcement();

        if (activeResearch == null || !activeResearch.IsActive) return;

        int activeWorkshops = ActiveFullyStaffedWorkshopsCount;
        if (activeWorkshops <= 0)
        {
            // Wstrzymane bez utraty postępu
            return;
        }

        float speed = CalculateDailyResearchSpeed(activeWorkshops);
        activeResearch.currentProgress += speed;

        if (activeResearch.currentProgress >= activeResearch.requiredProgress - 0.0001f)
        {
            CompleteActiveResearch();
        }
        else
        {
            OnResearchStateChanged?.Invoke();
        }
    }

    private void CompleteActiveResearch()
    {
        if (activeResearch == null || !activeResearch.IsActive) return;

        string key = activeResearch.buildingKey;
        if (!unlockedByBuilding.TryGetValue(key, out var list))
        {
            list = new List<UnlockedUpgradeRecord>();
            unlockedByBuilding[key] = list;
        }

        var completedRecord = new UnlockedUpgradeRecord
        {
            tier = activeResearch.tier,
            isOptionA = activeResearch.isOptionA,
            upgrade = activeResearch.upgrade
        };
        list.Add(completedRecord);

        if (activeResearch.upgrade.specialEffectIDs != null)
        {
            for (int i = 0; i < activeResearch.upgrade.specialEffectIDs.Count; i++)
            {
                string eff = activeResearch.upgrade.specialEffectIDs[i];
                if (!string.IsNullOrEmpty(eff))
                    activeSpecialEffects.Add(eff);
            }
        }

        var completedBuilding = activeResearch.targetBuilding;
        int completedTier = activeResearch.tier;
        var completedUpgrade = activeResearch.upgrade;

        activeResearch = null;

        PropagateUpgradesToWorld();

        OnUpgradeCompleted?.Invoke(completedBuilding, completedTier, completedUpgrade);
        OnResearchStateChanged?.Invoke();
    }

    private void PropagateUpgradesToWorld()
    {
        if (BuildingRegistry.Instance != null)
        {
            var allBuildings = BuildingRegistry.Instance.AllBuildings;
            for (int i = 0; i < allBuildings.Count; i++)
            {
                var b = allBuildings[i];
                if (b == null) continue;
                b.Upgrades?.NotifyExternalUpgradesChanged();
                if (b is TowerEntity tower)
                {
                    tower.RecalculateStats();
                }
            }
            BuildingRegistry.Instance.RescanAllBuildings();
        }

        BuildingEntity.NotifyEconomyChanged();
        PhaseResourceForecastManager.NotifyForecastDirty();
    }

    // =========================================================================
    // ZAPYTANIA O STAN DRZEWKA I EFEKTY GLOBALNE
    // =========================================================================

    public static string GetBuildingKey(BuildingData data)
    {
        return data != null ? data.name : string.Empty;
    }

    public int GetUnlockedTierCount(BuildingData data)
    {
        string key = GetBuildingKey(data);
        if (string.IsNullOrEmpty(key)) return 0;
        return unlockedByBuilding.TryGetValue(key, out var list) ? list.Count : 0;
    }

    public List<BuildingUpgradeSO> GetUnlockedUpgrades(BuildingData data)
    {
        var result = new List<BuildingUpgradeSO>();
        string key = GetBuildingKey(data);
        if (string.IsNullOrEmpty(key)) return result;

        if (unlockedByBuilding.TryGetValue(key, out var list))
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].upgrade != null)
                    result.Add(list[i].upgrade);
            }
        }
        return result;
    }

    public bool IsOptionResearched(BuildingData data, int tier, bool isOptionA)
    {
        string key = GetBuildingKey(data);
        if (string.IsNullOrEmpty(key) || !unlockedByBuilding.TryGetValue(key, out var list)) return false;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].tier == tier && list[i].isOptionA == isOptionA)
                return true;
        }
        return false;
    }

    public bool IsTierLockedByAlternative(BuildingData data, int tier, bool isOptionA)
    {
        return IsOptionResearched(data, tier, !isOptionA);
    }

    public bool IsTierAvailableToResearch(BuildingData data, int tier)
    {
        if (data == null || tier < 1 || tier > 4) return false;
        return tier == GetUnlockedTierCount(data) + 1;
    }

    public bool HasSpecialEffect(string effectId)
    {
        return !string.IsNullOrEmpty(effectId) && activeSpecialEffects.Contains(effectId);
    }

    public int CountActiveBuildingsByName(string buildingDataName)
    {
        if (BuildingRegistry.Instance == null || string.IsNullOrEmpty(buildingDataName)) return 0;

        int count = 0;
        var all = BuildingRegistry.Instance.AllBuildings;
        for (int i = 0; i < all.Count; i++)
        {
            var b = all[i];
            if (b == null || !b.isBuildingActive || b.data == null) continue;
            if (string.Equals(b.data.name, buildingDataName, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }
        return count;
    }

    // ── Efekty Między-systemowe Tier 4 ────────────────────────────────────────

    /// <summary>Farma T4-A (Królewskie spichlerze): -25% kosztu jedzenia dla przetrwania mieszkańców.</summary>
    public float GetHousingFoodSurvivalMultiplier()
    {
        return HasSpecialEffect("FARM_REDUCE_FOOD_SURVIVAL") ? 0.75f : 1f;
    }

    /// <summary>Kopalnia T4-B (Hartownia kruszców): +5% obrażeń wież za każdą aktywną Kopalnię (maks. +25%).</summary>
    public float GetGlobalTowerDamageBonusFromMines()
    {
        if (!HasSpecialEffect("MINES_TOWER_DAMAGE_SYNERGY")) return 0f;
        int activeMines = CountActiveBuildingsByName("Mines");
        return Mathf.Min(0.25f, activeMines * 0.05f);
    }

    /// <summary>Retorta T4-A (Rafinacja paliwa parowego): -25% nocnego upkeepu wież (lub -40% przy >= 2 aktywnych Retortach).</summary>
    public float GetTowerNightUpkeepMultiplier()
    {
        if (!HasSpecialEffect("RETORT_REDUCE_TOWER_UPKEEP")) return 1f;
        int activeRetorts = CountActiveBuildingsByName("Retort");
        if (activeRetorts >= 2) return 0.60f;
        if (activeRetorts >= 1) return 0.75f;
        return 1f;
    }

    private void ApplyDailyQuarryGateReinforcement()
    {
        if (!HasSpecialEffect("QUARRY_FORTIFY_GATES")) return;
        int activeQuarries = CountActiveBuildingsByName("Quarry");
        if (activeQuarries <= 0) return;

        var gates = FindObjectsByType<GateEntity>(FindObjectsSortMode.None);
        if (gates == null || gates.Length == 0) return;

        float hpGain = 15f * activeQuarries;
        for (int i = 0; i < gates.Length; i++)
        {
            if (gates[i] != null)
            {
                gates[i].ApplyWorkshopReinforcement(hpGain, 100f);
            }
        }
    }
}
