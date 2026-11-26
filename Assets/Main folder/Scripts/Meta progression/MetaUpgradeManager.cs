using System;
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

    [Header("── Katalog Wież (Draft) ───────────────────")]
    public List<TowerData> allTowers = new List<TowerData>();

    public event Action<MetaUpgradeSO> OnUpgradePurchased;

    [Header("── Konfiguracja Scen ────────────────────")]
    [Tooltip("Nazwa sceny, w której manager ma zaaplikować efekty (np. GameScene)")]
    public string gameSceneName = "Game scene";

    private readonly Dictionary<MetaEffectType, float> appliedValues = new Dictionary<MetaEffectType, float>();
    private readonly Dictionary<MetaEffectType, float> appliedSecondaryValues = new Dictionary<MetaEffectType, float>();
    private readonly Dictionary<(MetaEffectType, string), float> buildingSpecificValues = new Dictionary<(MetaEffectType, string), float>();
    private readonly Dictionary<(MetaEffectType, string), float> buildingSpecificSecondaryValues = new Dictionary<(MetaEffectType, string), float>();
    private readonly Dictionary<int, float> hearthLevelProductionBonuses = new Dictionary<int, float>();
    private int appliedGameSceneHandle = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitializeForDevRun()
    {
        if (!DevRunConfig.IsActive) return;

        if (Instance == null)
        {
            var existing = FindAnyObjectByType<MetaUpgradeManager>();
            if (existing != null)
            {
                Instance = existing;
            }
            else
            {
                var go = new GameObject("[MetaUpgradeManager - DEV RUN]");
                Instance = go.AddComponent<MetaUpgradeManager>();
            }
        }

        Instance.InitializeForDevRunIfNeeded();
    }

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
        if (DevRunConfig.IsActive)
        {
            InitializeForDevRunIfNeeded();
            return;
        }

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
            if (appliedGameSceneHandle == scene.handle) return;
            appliedGameSceneHandle = scene.handle;

            Debug.Log("[MetaUpgradeManager] Wykryto scenę gry.");

            if (DevRunConfig.IsActive)
            {
                LoadAllEditorUpgradesIfNeeded();
            }
            
            // --- POPRAWKA: WYMUŚ SYNCHRONIZACJĘ JESZCZE RAZ ---
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SyncUpgradesWithSave(allUpgrades);
                SaveManager.Instance.SyncTowersWithSave(GetAllTowers());
                Debug.Log("[MetaUpgradeManager] Wymuszono synchronizację save'a przed grą.");
            }
            else if (DevRunConfig.IsActive)
            {
                DevRunConfig.ApplyToUpgrades(allUpgrades, DevRunConfig.Mode);
                DevRunConfig.ApplyToTowers(GetAllTowers(), DevRunConfig.Mode, SaveManager.DefaultStarterTowerIDs);
            }
            // ----------------------------------------------------

            CollectValues();
            ApplyAllEffects();
        }
        else
        {
            appliedGameSceneHandle = -1;
        }
    }

    private void InitializeForDevRunIfNeeded()
    {
        if (!DevRunConfig.IsActive) return;

        LoadAllEditorUpgradesIfNeeded();
        List<TowerData> towers = GetAllTowers();

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SyncUpgradesWithSave(allUpgrades);
            SaveManager.Instance.SyncTowersWithSave(towers);
        }
        else
        {
            DevRunConfig.ApplyToUpgrades(allUpgrades, DevRunConfig.Mode);
            DevRunConfig.ApplyToTowers(towers, DevRunConfig.Mode, SaveManager.DefaultStarterTowerIDs);
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name == gameSceneName && appliedGameSceneHandle != activeScene.handle)
        {
            appliedGameSceneHandle = activeScene.handle;
            CollectValues();
            ApplyAllEffects();
        }
    }

    private void LoadAllEditorUpgradesIfNeeded()
    {
        if (allUpgrades == null) allUpgrades = new List<MetaUpgradeSO>();

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:MetaUpgradeSO");
        if (guids != null && guids.Length > 0)
        {
            var existingSet = new HashSet<MetaUpgradeSO>(allUpgrades);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                MetaUpgradeSO upgrade = UnityEditor.AssetDatabase.LoadAssetAtPath<MetaUpgradeSO>(path);
                if (upgrade != null && existingSet.Add(upgrade))
                {
                    allUpgrades.Add(upgrade);
                }
            }
        }
#endif
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
        appliedSecondaryValues.Clear();
        buildingSpecificValues.Clear();
        buildingSpecificSecondaryValues.Clear();
        hearthLevelProductionBonuses.Clear();

        foreach (var upgrade in allUpgrades)
        {
            if (upgrade == null || !upgrade.isUnlocked) continue;
            if (upgrade.IsPlaceholder) continue;
            if (upgrade.effectType == MetaEffectType.None || upgrade.effectType == MetaEffectType.Placeholder) continue;

            if (upgrade.RequiresTargetBuilding && upgrade.targetBuilding != null)
            {
                var key = (upgrade.effectType, upgrade.targetBuilding.name);

                // SUMOWANIE WARTOŚCI
                if (buildingSpecificValues.ContainsKey(key))
                    buildingSpecificValues[key] += upgrade.effectValue;
                else
                    buildingSpecificValues[key] = upgrade.effectValue;

                if (buildingSpecificSecondaryValues.ContainsKey(key))
                    buildingSpecificSecondaryValues[key] += upgrade.effectValue2;
                else
                    buildingSpecificSecondaryValues[key] = upgrade.effectValue2;

                Debug.Log($"[MetaUpgradeManager] Dodano efekt budynkowy ({upgrade.targetBuilding.name}): {upgrade.effectType} o wartości {upgrade.effectValue} (v2={upgrade.effectValue2}). Suma: {buildingSpecificValues[key]}");
            }
            else
            {
                // SUMOWANIE WARTOŚCI GLOBALNYCH
                if (appliedValues.ContainsKey(upgrade.effectType))
                    appliedValues[upgrade.effectType] += upgrade.effectValue;
                else
                    appliedValues[upgrade.effectType] = upgrade.effectValue;

                if (appliedSecondaryValues.ContainsKey(upgrade.effectType))
                    appliedSecondaryValues[upgrade.effectType] += upgrade.effectValue2;
                else
                    appliedSecondaryValues[upgrade.effectType] = upgrade.effectValue2;

                if (upgrade.effectType == MetaEffectType.HearthNextDayProductionBoost)
                {
                    AccumulateHearthProductionBoost(upgrade);
                }

                Debug.Log($"[MetaUpgradeManager] Dodano efekt: {upgrade.effectType} o wartości {upgrade.effectValue}. Suma: {appliedValues[upgrade.effectType]}");
            }
        }

        if (DevRunConfig.IsActive && DevRunConfig.ShouldInjectDruidTowerUnlock(DevRunConfig.Mode))
        {
            if (!appliedValues.ContainsKey(MetaEffectType.UnlockDruidTower))
            {
                appliedValues[MetaEffectType.UnlockDruidTower] = 1f;
            }
        }

        Debug.Log($"[MetaUpgradeManager] Zebrano zsumowane efekty. Globalne: {appliedValues.Count}, Specyficzne dla budynków: {buildingSpecificValues.Count}.");
    }

    private void AccumulateHearthProductionBoost(MetaUpgradeSO upgrade)
    {
        if (upgrade.effectValue2 > 0.0001f)
        {
            // Konwencja Hearth Producion 1: effectValue -> poziom 4, effectValue2 -> poziom 5
            AddHearthLevelBonus(4, upgrade.effectValue);
            AddHearthLevelBonus(5, upgrade.effectValue2);
        }
        else if (upgrade.effectValue > 0.0001f)
        {
            // Konwencja Hearth Producion 2: effectValue -> poziomy 3 i 4
            AddHearthLevelBonus(3, upgrade.effectValue);
            AddHearthLevelBonus(4, upgrade.effectValue);
        }
    }

    private void AddHearthLevelBonus(int levelIndex, float bonus)
    {
        if (Mathf.Approximately(bonus, 0f)) return;
        hearthLevelProductionBonuses[levelIndex] = hearthLevelProductionBonuses.TryGetValue(levelIndex, out float cur)
            ? cur + bonus
            : bonus;
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
            case MetaEffectType.StartingEther:
                ResourceManager.Instance?.AddResource(ResourceType.Ether, (int)value);
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
            case MetaEffectType.BonusProductionWorkers:
                CityStatsManager.Instance?.AddGlobalProductionWorkerSlot(Mathf.RoundToInt(value));
                break;
            case MetaEffectType.BonusTowerCrewSlots:
                CityStatsManager.Instance?.AddGlobalTowerCrewSlot(Mathf.RoundToInt(value));
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
            case MetaEffectType.HousingStartPopulation_Dwarves:
            case MetaEffectType.RestoreStarterRuinedHouses:
                // HousingEntity / TerrainGenerator odpytują w runtime
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

            // ── Efekty odpytywane w runtime (budynki, wieże, ekspedycje, palenisko, runy, zaklęcia) ─────
            case MetaEffectType.ProductionSynergy_Elf:
            case MetaEffectType.ProductionSynergy_Human:
            case MetaEffectType.BuildingPassiveEfficiency:
            case MetaEffectType.BuildingWorkerEfficiencyBonus:
            case MetaEffectType.BuildingTerrainBonusMultiplier:
            case MetaEffectType.SpecificBuildingConstructionCostReduction:
            case MetaEffectType.SpecificBuildingUpgradeCostReduction:
            case MetaEffectType.SpecificBuildingProductionBonus:
            case MetaEffectType.SpecificBuildingTerrainFlatBonus:
            case MetaEffectType.SpecificBuildingFlatProduction:
            case MetaEffectType.TowerGlobalArmorPenetration:
            case MetaEffectType.TowerGlobalMagicPenetration:
            case MetaEffectType.TowerGlobalFireRate:
            case MetaEffectType.TowerGlobalDamage:
            case MetaEffectType.TowerDraftUnlock:
            case MetaEffectType.TowerCrewSynergy_Dwarf:
            case MetaEffectType.TowerCrewSynergy_Elf:
            case MetaEffectType.TowerCrewSynergy_Human:
            case MetaEffectType.ExpansionCostReduction:
            case MetaEffectType.ExpeditionFreeChunkThresholdBonus:
            case MetaEffectType.ExpeditionSurveyResourceProfit:
            case MetaEffectType.UnlockTacticalRecon:
            case MetaEffectType.ReconResourceGatherChance:
            case MetaEffectType.ExpeditionExtraTerrainChance:
            case MetaEffectType.HearthUnlockHighLevels:
            case MetaEffectType.HearthHighLevelCoalDiscount:
            case MetaEffectType.HearthNextDayProductionBoost:
            case MetaEffectType.UniqueChunkUnlock:
            case MetaEffectType.MapChunkModifier:
            case MetaEffectType.RuneDropChance_Uncommon:
            case MetaEffectType.RuneDropChance_Rare:
            case MetaEffectType.RuneDropChance_Legendary:
            case MetaEffectType.RuneDropChance_Cursed:
            case MetaEffectType.RuneTowerLimit:
            case MetaEffectType.UnlockRuneRarity_Uncommon:
            case MetaEffectType.UnlockRuneRarity_Rare:
            case MetaEffectType.UnlockRuneRarity_Legendary:
            case MetaEffectType.UnlockRuneRarity_Cursed:
            case MetaEffectType.UnlockRuneFusion:
            case MetaEffectType.UnlockRuneTransmutation:
            case MetaEffectType.UnlockCursedCrafting:
            case MetaEffectType.UnlockDruidTower:
            case MetaEffectType.UnlockTrinityForce:
            case MetaEffectType.SpellBonusCharges:
            case MetaEffectType.Placeholder:
                // Obsługiwane przez odpytywanie IsUnlocked() / GetValue() / GetBuildingValue()
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

    /// <summary>Zwraca drugi parametr (effectValue2) globalnego efektu. 0f jeśli brak.</summary>
    public float GetSecondaryValue(MetaEffectType type)
    {
        return appliedSecondaryValues.TryGetValue(type, out var v) ? v : 0f;
    }

    /// <summary>Zwraca dodatkowy bonus produkcji następnego dnia dla danego poziomu Paleniska (1..5).</summary>
    public float GetHearthNextDayProductionBonus(int levelIndex)
    {
        return hearthLevelProductionBonuses.TryGetValue(levelIndex, out float bonus) ? bonus : 0f;
    }

    /// <summary>Czy dany efekt jest odblokowany?</summary>
    public bool IsUnlocked(MetaEffectType type)
    {
        return appliedValues.ContainsKey(type);
    }

    /// <summary>Czy dany efekt jest odblokowany i aktywny?</summary>
    public bool HasEffect(MetaEffectType type) => IsUnlocked(type);

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

    /// <summary>Zwraca drugi parametr (effectValue2) efektu specyficznego dla danego budynku.</summary>
    public float GetBuildingSecondaryValue(MetaEffectType type, BuildingData building)
    {
        if (building == null) return 0f;
        var key = (type, building.name);
        return buildingSpecificSecondaryValues.TryGetValue(key, out var v) ? v : 0f;
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
    
    /// <summary>
    /// Przelicza i ponownie aplikuje wszystkie odblokowane meta-upgrady w runtime.
    /// </summary>
    public void ReapplyAllUpgrades()
    {
        CollectValues();
        ApplyAllEffects();
    }

    // =========================================================================
    // OBSŁUGA ZAKUPU I DRAFT WIEŻ
    // =========================================================================

    /// <summary>
    /// Wywoływane przy zakupie węzła ulepszenia.
    /// Oznacza węzeł jako odblokowany, aktualizuje SaveManager i wyzwala TowerDraftModalUI jeśli to draft wieży.
    /// </summary>
    public void OnNodePurchased(MetaUpgradeSO node)
    {
        if (ReferenceEquals(node, null)) return;

        if (!node.isUnlocked)
        {
            node.isUnlocked = true;
        }

        if (SaveManager.Instance != null && SaveManager.Instance.currentSaveData != null)
        {
            if (SaveManager.Instance.currentSaveData.unlockedUpgradeIDs == null)
            {
                SaveManager.Instance.currentSaveData.unlockedUpgradeIDs = new List<string>();
            }

            if (!SaveManager.Instance.currentSaveData.unlockedUpgradeIDs.Contains(node.id))
            {
                SaveManager.Instance.currentSaveData.unlockedUpgradeIDs.Add(node.id);
                SaveManager.Instance.SaveGame();
            }
        }

        OnUpgradePurchased?.Invoke(node);

        if (IsTowerDraftUpgrade(node))
        {
            TriggerTowerDraft();
        }
    }

    /// <summary>
    /// Sprawdza czy dany węzeł jest węzłem wyboru nowej wieży (Nowa wieża 1..5 lub MetaEffectType.TowerDraftUnlock).
    /// </summary>
    public bool IsTowerDraftUpgrade(MetaUpgradeSO node)
    {
        if (ReferenceEquals(node, null)) return false;
        if (node.effectType == MetaEffectType.TowerDraftUnlock) return true;
        if (!string.IsNullOrEmpty(node.id) && node.id.StartsWith("nowa_wie", StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrEmpty(node.upgradeName) && node.upgradeName.StartsWith("Nowa wieża", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Uruchamia przepływ okna wyboru nowej wieży (Tower Draft).
    /// </summary>
    public void TriggerTowerDraft(Action onComplete = null)
    {
        List<TowerData> towers = GetAllTowers();
        TowerDraftModalUI draftModal = TowerDraftModalUI.Instance ?? TowerDraftModalUI.GetOrCreateInstance();
        if (draftModal != null)
        {
            draftModal.ShowDraft(towers, onComplete);
        }
        else
        {
            Debug.LogWarning("[MetaUpgradeManager] Nie znaleziono instancji TowerDraftModalUI!");
        }
    }

    /// <summary>
    /// Pobiera listę wszystkich wież z managera, SaveManagera lub bazy assetów w edytorze.
    /// </summary>
    public List<TowerData> GetAllTowers()
    {
        if (allTowers != null && allTowers.Count > 0)
        {
            return allTowers;
        }

        if (SaveManager.Instance != null && SaveManager.Instance.allTowers != null && SaveManager.Instance.allTowers.Count > 0)
        {
            return SaveManager.Instance.allTowers;
        }

        var assetDatabaseType = Type.GetType("UnityEditor.AssetDatabase, UnityEditor.CoreModule") ??
                                Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
        if (assetDatabaseType != null)
        {
            try
            {
                var findAssetsMethod = assetDatabaseType.GetMethod("FindAssets", new Type[] { typeof(string) });
                var guidToPathMethod = assetDatabaseType.GetMethod("GUIDToAssetPath", new Type[] { typeof(string) });
                var loadAssetMethod = assetDatabaseType.GetMethod("LoadAssetAtPath", new Type[] { typeof(string), typeof(Type) });

                if (findAssetsMethod != null && guidToPathMethod != null && loadAssetMethod != null)
                {
                    string[] guids = (string[])findAssetsMethod.Invoke(null, new object[] { "t:TowerData" });
                    if (guids != null && guids.Length > 0)
                    {
                        List<TowerData> loaded = new List<TowerData>();
                        foreach (string guid in guids)
                        {
                            string path = (string)guidToPathMethod.Invoke(null, new object[] { guid });
                            TowerData tower = (TowerData)loadAssetMethod.Invoke(null, new object[] { path, typeof(TowerData) });
                            if (tower != null) loaded.Add(tower);
                        }
                        if (loaded.Count > 0)
                        {
                            allTowers = loaded;
                            return loaded;
                        }
                    }
                }
            }
            catch { }
        }
        return new List<TowerData>();
    }

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