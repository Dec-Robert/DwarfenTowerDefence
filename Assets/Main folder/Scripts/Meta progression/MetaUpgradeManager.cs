using UnityEngine;
using UnityEngine.SceneManagement; // Wymagane do wykrywania zmiany sceny
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Centralny manager meta-ulepszeń.
///
/// ── ODPOWIEDZIALNOŚĆ ────────────────────────────────────────────────────────
///   1. Trzyma listę WSZYSTKICH MetaUpgradeSO (przypisanych w Inspectorze).
///   2. Synchronizuje stan isUnlocked z SaveManager przy starcie.
///   3. Na początku sesji aplikuje efekty wszystkich odblokowanych upgradów.
///   4. Wystawia API do odpytywania wartości efektów w runtime.
///
/// ── KOLEJNOŚĆ INICJALIZACJI (Script Execution Order) ────────────────────────
///   MetaUpgradeManager musi uruchomić się PRZED:
///     - GameManager (zdrowie miasta)
///     - CityStatsManager (zmiany/pracownicy)
///     - ResourceManager (surowce startowe)
///     - ChunkWallManager (system murów)
///   Ustaw Script Execution Order w Project Settings lub dodaj [DefaultExecutionOrder(-50)].
///
/// ── SETUP ────────────────────────────────────────────────────────────────────
///   1. Dodaj komponent na pusty GameObject "MetaUpgradeManager" na scenie gry.
///   2. Przeciągnij WSZYSTKIE MetaUpgradeSO do listy allUpgrades w Inspektorze.
///      KOLEJNOŚĆ MA ZNACZENIE dla nadpisywania (ostatni odblokowany wygrywa).
///   3. Jeśli MainMenu też używa tej listy — wskaż ten sam manager lub
///      przekaż listę przez SaveManager.SyncUpgradesWithSave().
/// </summary>
[DefaultExecutionOrder(-50)]
public class MetaUpgradeManager : MonoBehaviour
{
    public static MetaUpgradeManager Instance { get; private set; }

    [Header("── Lista Wszystkich Upgradów ────────────")]
    public List<MetaUpgradeSO> allUpgrades = new List<MetaUpgradeSO>();

    [Header("── Konfiguracja Scen ────────────────────")]
    [Tooltip("Nazwa sceny, w której manager ma zaaplikować efekty (np. GameScene)")]
    public string gameSceneName = "Game scene";

    private readonly Dictionary<MetaEffectType, float> appliedValues = new Dictionary<MetaEffectType, float>();
    private readonly Dictionary<(MetaEffectType, string), float> buildingSpecificValues = new Dictionary<(MetaEffectType, string), float>();

    // =========================================================================
    // UNITY CYKL ŻYCIA
    // =========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) 
        { 
            Destroy(gameObject); 
            return; 
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject); // Manager przeżywa zmianę sceny!
    }

    private void Start()
    {
        // Na samym starcie gry (w Main Menu), synchronizujemy stan z SaveManagerem
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SyncUpgradesWithSave(allUpgrades);
            Debug.Log("[MetaUpgradeManager] Zsynchronizowano upgrady z SaveManagerem.");
        }
    }

    private void OnEnable()
    {
        // Subskrybujemy event ładowania sceny
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == gameSceneName)
        {
            Debug.Log("[MetaUpgradeManager] Wykryto scenę gry.");
            
            // --- POPRAWKA: WYMUŚ SYNCHRONIZACJĘ JESZCZE RAZ ---
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SyncUpgradesWithSave(allUpgrades);
                Debug.Log("[MetaUpgradeManager] Wymuszono synchronizację save'a przed grą.");
            }
            // ----------------------------------------------------

            CollectValues();
            ApplyAllEffects();
        }
    }

    // =========================================================================
    // ZBIERANIE WARTOŚCI
    // =========================================================================

    /// <summary>
    /// Iteruje po wszystkich odblokowanych upgradach i zapisuje ich wartości.
    /// Nadpisywanie: każdy kolejny odblokowany upgrade z tym samym effectType
    /// zastępuje poprzednią wartość.
    /// </summary>
    private void CollectValues()
    {
        Debug.LogWarning("[MetaUpgradeManager] Rozpoczynam CollectValues()...");
        appliedValues.Clear();
        buildingSpecificValues.Clear();

        foreach (var upgrade in allUpgrades)
        {
            if (!upgrade.isUnlocked)           continue;
            if (upgrade.effectType == MetaEffectType.None) continue;

            if (upgrade.RequiresTargetBuilding && upgrade.targetBuilding != null)
            {
                var key = (upgrade.effectType, upgrade.targetBuilding.name);
                
                // SUMOWANIE WARTOŚCI
                if (buildingSpecificValues.ContainsKey(key))
                    buildingSpecificValues[key] += upgrade.effectValue;
                else
                    buildingSpecificValues[key] = upgrade.effectValue;
            }
            else
            {
                // SUMOWANIE WARTOŚCI GLOBALNYCH
                if (appliedValues.ContainsKey(upgrade.effectType))
                    appliedValues[upgrade.effectType] += upgrade.effectValue;
                else
                    appliedValues[upgrade.effectType] = upgrade.effectValue;
            }
            Debug.Log($"[MetaUpgradeManager] Dodano efekt: {upgrade.effectType} o wartości {upgrade.effectValue}. Suma: {appliedValues[upgrade.effectType]}");
        }

        Debug.Log($"[MetaUpgradeManager] Zebrano zsumowane efekty. Globalne: {appliedValues.Count}, Specyficzne dla budynków: {buildingSpecificValues.Count}.");
    }

    // =========================================================================
    // APLIKOWANIE EFEKTÓW
    // =========================================================================

    private void ApplyAllEffects()
    {
        foreach (var kvp in appliedValues)
            ApplyEffect(kvp.Key, kvp.Value);
    }

    private void ApplyEffect(MetaEffectType type, float value)
    {
        switch (type)
        {
            // ── Surowce startowe ──────────────────────────────────────────────
            case MetaEffectType.StartingGold:
                ResourceManager.Instance?.AddResource(ResourceType.Gold, (int)value);
                break;
            case MetaEffectType.StartingWood:
                ResourceManager.Instance?.AddResource(ResourceType.Wood, (int)value);
                break;
            case MetaEffectType.StartingStone:
                ResourceManager.Instance?.AddResource(ResourceType.Stone, (int)value);
                break;
            case MetaEffectType.StartingIron:
                ResourceManager.Instance?.AddResource(ResourceType.Iron, (int)value);
                break;
            case MetaEffectType.StartingCoal:
                ResourceManager.Instance?.AddResource(ResourceType.Coal, (int)value);
                break;
            case MetaEffectType.StartingFood:
                ResourceManager.Instance?.AddResource(ResourceType.Food, (int)value);
                break;

            // ── Pracownicy i zmiany ───────────────────────────────────────────
            case MetaEffectType.BonusShifts:
                CityStatsManager.Instance?.AddGlobalShift(Mathf.RoundToInt(value));
                break;
            case MetaEffectType.BonusWorkersPerShift:
                CityStatsManager.Instance?.AddGlobalWorkerSlot(Mathf.RoundToInt(value));
                break;

            // ── Budynki — continuous modifiers → GlobalModifierRegistry ───────
            case MetaEffectType.BuildingPassiveEfficiency:
                GlobalModifierRegistry.Instance?.Register(new StatModifier(
                    "MetaUpgrade_PassiveEff",
                    TowerStatType.BuildingPassiveEfficiency,
                    flat: value));
                break;

            case MetaEffectType.BuildingWorkerEfficiencyBonus:
                GlobalModifierRegistry.Instance?.Register(new StatModifier(
                    "MetaUpgrade_WorkerEff",
                    TowerStatType.BuildingWorkerEfficiencyBonus,
                    flat: value));
                break;

            // ── Wrogowie ──────────────────────────────────────────────────────
            case MetaEffectType.EliteChanceBoost:
                GlobalModifierRegistry.Instance?.Register(new StatModifier(
                    "MetaUpgrade_EliteChance",
                    TowerStatType.EliteChanceBoost,
                    flat: value));
                break;

            case MetaEffectType.EnemySurvivorPenaltyArmor:
                GlobalModifierRegistry.Instance?.Register(new StatModifier(
                    "MetaUpgrade_SurvivorArmor",
                    TowerStatType.EnemySurvivorPenaltyArmor,
                    flat: value));
                break;

            case MetaEffectType.EnemySurvivorPenaltySpeed:
                GlobalModifierRegistry.Instance?.Register(new StatModifier(
                    "MetaUpgrade_SurvivorSpeed",
                    TowerStatType.EnemySurvivorPenaltySpeed,
                    flat: value));
                break;

            case MetaEffectType.EnemySurvivorPenaltyDodge:
                GlobalModifierRegistry.Instance?.Register(new StatModifier(
                    "MetaUpgrade_SurvivorDodge",
                    TowerStatType.EnemySurvivorPenaltyDodge,
                    flat: value));
                break;

            // ── Wieże ─────────────────────────────────────────────────────────
            case MetaEffectType.TowerNoAmmoNightPenalty:
                GlobalModifierRegistry.Instance?.Register(new StatModifier(
                    "MetaUpgrade_NoAmmoPenalty",
                    TowerStatType.TowerNoAmmoNightPenalty,
                    multiplier: value));
                break;

            // ── Eksploracja ───────────────────────────────────────────────────
            case MetaEffectType.ExpansionCostReduction:
                GlobalModifierRegistry.Instance?.Register(new StatModifier(
                    "MetaUpgrade_ExpansionCost",
                    TowerStatType.ExpansionCostReduction,
                    flat: value));
                break;

            // ── Miasto ────────────────────────────────────────────────────────
            case MetaEffectType.CityBaseHealth:
                // GameManager.startingHp jest prywatne — dodajemy HP po starcie
                GameManager.Instance?.ModifyBaseHealth(Mathf.RoundToInt(value));
                break;

            case MetaEffectType.GateBaseHP:
                // ChunkWallManager aplikuje to przy spawnie bram przez GetMetaGateHP()
                // Nie musimy tu nic robić — bramy odpytują GetValue() przy tworzeniu
                break;

            // ── Domy ──────────────────────────────────────────────────────────
            case MetaEffectType.HousingStartPopulation:
                // HousingEntity odpytuje GetValue() przy Initialize()
                break;

            // ── Limit mieszkańców ─────────────────────────────────────────────
            case MetaEffectType.HousingMaxResidents:
            case MetaEffectType.HousingMaxResidents_Humans:
            case MetaEffectType.HousingMaxResidents_Elves:
            case MetaEffectType.HousingMaxResidents_Dwarves:
                // HousingEntity.GetEffectiveMaxResidents() odpytuje GetValue() w runtime.
                // Nie musimy tu nic robić — wartość jest już w appliedValues.
                break;

            // ── System murów ──────────────────────────────────────────────────
            case MetaEffectType.WallSystem_Solution1:
                ChunkWallManager.Instance?.SetMode(WallSystemMode.Solution1_BaseOnly);
                break;

            case MetaEffectType.WallSystem_Solution2:
                ChunkWallManager.Instance?.SetMode(WallSystemMode.Solution2_FrontLine);
                break;

            // ── Efekty budynkowe (targetBuilding) — odpytywane z zewnątrz ─────
            case MetaEffectType.BuildingTerrainBonusMultiplier:
            case MetaEffectType.SpecificBuildingUpgradeCostReduction:
                // Obsługiwane przez buildingSpecificValues, odpytywane przez GetBuildingValue()
                break;

            default:
                Debug.LogWarning($"[MetaUpgradeManager] Nieobsługiwany efekt: {type}");
                break;
        }
    }

    // =========================================================================
    // PUBLICZNE API — odpytywanie wartości w runtime
    // =========================================================================

    /// <summary>
    /// Zwraca wartość globalnego efektu. 0f jeśli nie odblokowany.
    /// Używaj w systemach które nie korzystają z GlobalModifierRegistry.
    /// </summary>
    public float GetValue(MetaEffectType type)
    {
        return appliedValues.TryGetValue(type, out var v) ? v : 0f;
    }

    /// <summary>Czy dany efekt jest odblokowany?</summary>
    public bool IsUnlocked(MetaEffectType type)
    {
        return appliedValues.ContainsKey(type);
    }

    /// <summary>
    /// Zwraca wartość efektu specyficznego dla danego budynku.
    /// Używaj w BuildingUpgradeComponent i BuildingProductionComponent.
    /// </summary>
    public float GetBuildingValue(MetaEffectType type, BuildingData building)
    {
        if (building == null) return 0f;
        var key = (type, building.name);
        return buildingSpecificValues.TryGetValue(key, out var v) ? v : 0f;
    }

    /// <summary>
    /// Skrót: bazowe HP bram z meta-upgrade.
    /// GateEntity wywołuje to przy inicjalizacji.
    /// </summary>
    public float GetMetaGateHP() => GetValue(MetaEffectType.GateBaseHP);

    /// <summary>
    /// Skrót: dodatkowa populacja startowa domów.
    /// HousingEntity wywołuje to przy Initialize().
    /// </summary>
    public int GetHousingStartPopulationBonus()
        => Mathf.RoundToInt(GetValue(MetaEffectType.HousingStartPopulation));
    
    // =========================================================================
    // DEBUG
    // =========================================================================

#if UNITY_EDITOR
    [ContextMenu("Debug: Print All Applied Values")]
    private void DebugPrintValues()
    {
        Debug.Log("[MetaUpgradeManager] === Zastosowane efekty ===");
        foreach (var kvp in appliedValues)
            Debug.Log($"  {kvp.Key} = {kvp.Value}");
        foreach (var kvp in buildingSpecificValues)
            Debug.Log($"  [{kvp.Key.Item1} / {kvp.Key.Item2}] = {kvp.Value}");
    }

    [ContextMenu("Debug: Reapply All")]
    private void DebugReapply()
    {
        CollectValues();
        ApplyAllEffects();
    }
#endif
}