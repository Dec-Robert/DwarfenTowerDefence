using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Uniq building – Beacon of Hope (Wielkie Palenisko Twierdzy).
///     Przy rozpaleniu na wybrany poziom (1–5) przy przejściu Dusk -> Night:
///     - pobiera węgiel z ResourceManager,
///     - rejestruje nocne modyfikatory wież w GlobalModifierRegistry i przelicza TowerEntity,
///     - wystawia mnożniki nocne dla EnemyStats i RuneManager,
///     - zapisuje modyfikator produkcji na kolejny dzień, który aktywuje się o poranku (Morning)
///       i trwa przez rozliczenie ekonomii o zmierzchu (Dusk).
/// </summary>
public class BeaconEntity : BuildingEntity
{
    public const string ModifierSourceId = "Beacon";

    public static BeaconEntity Instance { get; private set; }

    [Header("Konfiguracja Wielkiego Paleniska")]
    public HearthConfigSO hearthConfig;

    private static HearthConfigSO fallbackConfig;
    private static bool isSubscribedToTimeManager;
    private static TimePhaseManager subscribedTimeManager;

    public static int SelectedDraftLevel { get; set; } = 3;
    public static int ActiveNightLevel { get; private set; } = 0;

    public static float ActiveNightEnemyStatsBonus { get; private set; } = 0f;
    public static float ActiveNightRuneBonus { get; private set; } = 0f;

    public static float PendingNextDayProductionBonus { get; private set; } = 0f;
    public static float ActiveDayProductionBonus { get; private set; } = 0f;

    public static event Action OnHearthStateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        fallbackConfig = null;
        isSubscribedToTimeManager = false;
        subscribedTimeManager = null;
        SelectedDraftLevel = 3;
        ActiveNightLevel = 0;
        ActiveNightEnemyStatsBonus = 0f;
        ActiveNightRuneBonus = 0f;
        PendingNextDayProductionBonus = 0f;
        ActiveDayProductionBonus = 0f;
        OnHearthStateChanged = null;
    }

    private void Awake()
    {
        Instance = this;
        EnsureTimeSubscription();
    }

    protected override void Start()
    {
        base.Start();
        EnsureTimeSubscription();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void EnsureTimeSubscription()
    {
        if (TimePhaseManager.Instance == null) return;

        if (!isSubscribedToTimeManager || subscribedTimeManager != TimePhaseManager.Instance)
        {
            if (subscribedTimeManager != null)
            {
                subscribedTimeManager.OnMorningStarted -= HandleMorningStartedStatic;
            }

            subscribedTimeManager = TimePhaseManager.Instance;
            subscribedTimeManager.OnMorningStarted += HandleMorningStartedStatic;
            isSubscribedToTimeManager = true;
        }
    }

    public static HearthConfigSO GetConfig()
    {
        if (Instance != null && Instance.hearthConfig != null)
        {
            Instance.hearthConfig.EnsureDefaultLevels();
            return Instance.hearthConfig;
        }

        if (fallbackConfig == null)
        {
            fallbackConfig = ScriptableObject.CreateInstance<HearthConfigSO>();
            fallbackConfig.EnsureDefaultLevels();
        }

        return fallbackConfig;
    }

    /// <summary>
    ///     Rozpala Palenisko na wybrany poziom (1–5), pobiera węgiel, nakłada efekty nocne
    ///     i przełącza fazę gry z Dusk na Night.
    /// </summary>
    public static bool TryIgniteAndStartNight(int levelIndex)
    {
        EnsureTimeSubscription();

        HearthConfigSO config = GetConfig();
        HearthLevelData levelData = config.GetLevelData(levelIndex);

        if (levelData.coalCost > 0 && ResourceManager.Instance != null)
        {
            var costList = new List<ResourceCost>
            {
                new ResourceCost { type = ResourceType.Coal, amount = levelData.coalCost }
            };

            if (!ResourceManager.Instance.SpendResources(costList))
            {
                Debug.LogWarning($"[BeaconEntity] Brak wystarczającej ilości węgla ({levelData.coalCost}) na rozpalenie poziomu {levelIndex}!");
                return false;
            }
        }

        SelectedDraftLevel = levelData.levelIndex;
        ActiveNightLevel = levelData.levelIndex;

        // Poprzedni dzień został już rozliczony przy wejściu w Dusk; kolejna produkcja nastąpi jutro
        ActiveDayProductionBonus = 0f;
        PendingNextDayProductionBonus = levelData.nextDayProductionBonus;

        // Modyfikatory wrogów i run na nadchodzącą noc
        ActiveNightEnemyStatsBonus = levelData.enemyStatsBonus;
        ActiveNightRuneBonus = levelData.runeDropAndRarityBonus;

        // Modyfikatory wież w GlobalModifierRegistry
        ApplyTowerNightModifiers(levelData);

        Debug.Log($"[BeaconEntity] Rozpalono Wielkie Palenisko – Poziom {levelData.levelIndex} ({levelData.title}). " +
                  $"Koszt Węgla: {levelData.coalCost}, Wieże Obrażenia: {levelData.towerDamageBonus:+0%;-0%;0%}, " +
                  $"Wieże Zasięg/Szybkość: {levelData.towerRangeAndRateBonus:+0%;-0%;0%}, " +
                  $"Wrogowie: {levelData.enemyStatsBonus:+0%;-0%;0%}, Runy: {levelData.runeDropAndRarityBonus:+0%;-0%;0%}, " +
                  $"Produkcja jutra: {levelData.nextDayProductionBonus:+0%;-0%;0%}.");

        OnHearthStateChanged?.Invoke();

        if (TimePhaseManager.Instance != null && TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Dusk)
        {
            TimePhaseManager.Instance.ChangeCurrentPhase();
        }

        return true;
    }

    private static void ApplyTowerNightModifiers(HearthLevelData levelData)
    {
        if (GlobalModifierRegistry.Instance != null)
        {
            GlobalModifierRegistry.Instance.Unregister(ModifierSourceId);

            if (!Mathf.Approximately(levelData.towerDamageBonus, 0f))
            {
                GlobalModifierRegistry.Instance.Register(
                    new StatModifier(ModifierSourceId, TowerStatType.Damage, 1f + levelData.towerDamageBonus));
            }

            if (!Mathf.Approximately(levelData.towerRangeAndRateBonus, 0f))
            {
                GlobalModifierRegistry.Instance.Register(
                    new StatModifier(ModifierSourceId, TowerStatType.Range, 1f + levelData.towerRangeAndRateBonus));
                GlobalModifierRegistry.Instance.Register(
                    new StatModifier(ModifierSourceId, TowerStatType.FireRate, 1f + levelData.towerRangeAndRateBonus));
            }
        }

        RecalculateAllTowers();
    }

    private static void HandleMorningStartedStatic()
    {
        // O poranku (Night -> Morning) wygaszamy efekty nocne (wieże, wrogowie, runy),
        // a obiecany bonus/karę do produkcji aktywujemy na cały rozpoczynający się dzień (Morning + Dusk)
        if (GlobalModifierRegistry.Instance != null)
        {
            GlobalModifierRegistry.Instance.Unregister(ModifierSourceId);
        }

        RecalculateAllTowers();

        ActiveNightLevel = 0;
        ActiveNightEnemyStatsBonus = 0f;
        ActiveNightRuneBonus = 0f;

        ActiveDayProductionBonus = PendingNextDayProductionBonus;
        PendingNextDayProductionBonus = 0f;

        OnHearthStateChanged?.Invoke();
    }

    private static void RecalculateAllTowers()
    {
        if (BuildingRegistry.Instance == null) return;

        var towers = BuildingRegistry.Instance.GetAllOfType<TowerEntity>();
        foreach (var tower in towers)
        {
            if (tower != null)
            {
                tower.RecalculateStats();
            }
        }
    }

    /// <summary>
    ///     Zwraca aktywny bonus/karę procentową do produkcji budynków ekonomicznych w bieżącym dniu (np. -0.25f lub +0.35f).
    /// </summary>
    public static float GetActiveDayProductionBonus()
    {
        return ActiveDayProductionBonus;
    }

    /// <summary>
    ///     Zwraca mnożnik statystyk wrogów (HP oraz MoveSpeed) obowiązujący w bieżącej nocy (np. 1.15f na Lvl 1, 0.85f na Lvl 5).
    /// </summary>
    public static float GetActiveNightEnemyMultiplier()
    {
        return Mathf.Max(0.1f, 1f + ActiveNightEnemyStatsBonus);
    }

    /// <summary>
    ///     Zwraca mnożnik szansy na drop run i wagi rzadkich run obowiązujący w bieżącej nocy (np. 1.35f na Lvl 1, 0.75f na Lvl 5).
    /// </summary>
    public static float GetActiveNightRuneMultiplier()
    {
        return Mathf.Max(0.1f, 1f + ActiveNightRuneBonus);
    }
}