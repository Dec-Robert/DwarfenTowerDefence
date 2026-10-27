using UnityEngine;
using System.Collections.Generic;

public class RuneManager : MonoBehaviour
{
    public static RuneManager Instance { get; private set; }

    [Header("Konfiguracja")]
    public RuneSystemConfigSO config;

    [Header("Ekwipunek Gracza")]
    public List<RuneItem> playerRunes = new List<RuneItem>();

    public event System.Action OnInventoryChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private static readonly System.Random sysRng = new System.Random();

    private static float SafeRandomRange(float min, float max)
    {
        return (float)(min + sysRng.NextDouble() * (max - min));
    }

    private static int SafeRandomRange(int min, int max)
    {
        if (min >= max) return min;
        return sysRng.Next(min, max);
    }

    private static float SafeRandomValue()
    {
        return (float)sysRng.NextDouble();
    }

    private static void SafeLog(string msg)
    {
        try { Debug.Log(msg); } catch { }
    }

    private static void SafeLogWarning(string msg)
    {
        try { Debug.LogWarning(msg); } catch { }
    }

    private static void SafeLogError(string msg)
    {
        try { Debug.LogError(msg); } catch { }
    }

    // =========================================================================
    // API Dropu z Wrogów
    // =========================================================================

    public void RollRuneDrop(EnemyRank rank)
    {
        if (ReferenceEquals(config, null)) return;

        float hearthRuneMult = BeaconEntity.GetActiveNightRuneMultiplier();
        int runesToDrop = 0;
        switch (rank)
        {
            case EnemyRank.Normal:
                if (SafeRandomRange(0f, 100f) <= config.dropChanceNormal * hearthRuneMult) runesToDrop = 1;
                break;
            case EnemyRank.Elite:
                if (SafeRandomRange(0f, 100f) <= config.dropChanceElite * hearthRuneMult) runesToDrop = 1;
                break;
            case EnemyRank.Boss:
                runesToDrop = SafeRandomRange(config.minRunesFromBoss, config.maxRunesFromBoss + 1);
                break;
        }

        for (int i = 0; i < runesToDrop; i++)
        {
            RuneItem newRune = GenerateRandomRune();
            if (!ReferenceEquals(HopeSessionManager.Instance, null)) HopeSessionManager.Instance.OnRuneObtained(newRune.definition.rarity);
            if (newRune != null)
            {
                playerRunes.Add(newRune);
                SafeLog($"<color=magenta>[RuneManager] Drop: {newRune.definition.runeName} | {newRune.GetPrimaryText()} {newRune.GetPenaltyText()}</color>");
            }
        }

        if (runesToDrop > 0) OnInventoryChanged?.Invoke();
    }

    // =========================================================================
    // Algorytm Generacji Run
    // =========================================================================

    public RuneItem GenerateRandomRune(RuneRarity? forcedRarity = null)
    {
        RuneRarity rarity = forcedRarity ?? RollRarity();
        
        // 1. Pobierz definicję runy dla wylosowanej rzadkości
        RuneDefinitionSO definition = GetRandomDefinition(rarity);
        if (ReferenceEquals(definition, null)) return null;

        // 2. Wylosuj główną wartość
        float primaryVal = SafeRandomRange(definition.primaryValueRange.x, definition.primaryValueRange.y);
        primaryVal = FormatValue(primaryVal, definition.valueType);

        RuneItem rune = new RuneItem(definition, primaryVal);

        // 3. Jeśli to runa Przeklęta, wylosuj jedną karę z jej listy
        if (rarity == RuneRarity.Cursed && definition.possiblePenalties != null && definition.possiblePenalties.Count > 0)
        {
            var penalty = definition.possiblePenalties[SafeRandomRange(0, definition.possiblePenalties.Count)];
            
            float penVal = SafeRandomRange(penalty.penaltyRange.x, penalty.penaltyRange.y);
            penVal = FormatValue(penVal, penalty.valueType);
            
            // Nakładamy wartość jako ujemną!
            rune.SetPenalty(penalty.stat, penalty.valueType, -penVal);
        }

        return rune;
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private RuneRarity RollRarity()
    {
        float hearthRuneMult = BeaconEntity.GetActiveNightRuneMultiplier();
        float wCommon = config.weightCommon;

        bool uncUnlocked = MetaUpgradeManager.Instance == null || MetaUpgradeManager.Instance.IsUnlocked(MetaEffectType.UnlockRuneRarity_Uncommon);
        bool rareUnlocked = MetaUpgradeManager.Instance == null || MetaUpgradeManager.Instance.IsUnlocked(MetaEffectType.UnlockRuneRarity_Rare);
        bool legUnlocked = MetaUpgradeManager.Instance == null || MetaUpgradeManager.Instance.IsUnlocked(MetaEffectType.UnlockRuneRarity_Legendary);
        bool cursedUnlocked = MetaUpgradeManager.Instance == null || MetaUpgradeManager.Instance.IsUnlocked(MetaEffectType.UnlockRuneRarity_Cursed);

        float wUnc = uncUnlocked ? Mathf.Max(0f, (config.weightUncommon + GetMetaBonus(MetaEffectType.RuneDropChance_Uncommon)) * hearthRuneMult) : 0f;
        float wRare = rareUnlocked ? Mathf.Max(0f, (config.weightRare + GetMetaBonus(MetaEffectType.RuneDropChance_Rare)) * hearthRuneMult) : 0f;
        float wLeg = legUnlocked ? Mathf.Max(0f, (config.weightLegendary + GetMetaBonus(MetaEffectType.RuneDropChance_Legendary)) * hearthRuneMult) : 0f;
        float wCursed = cursedUnlocked ? Mathf.Max(0f, (config.weightCursed + GetMetaBonus(MetaEffectType.RuneDropChance_Cursed)) * hearthRuneMult) : 0f;

        float totalBonus = 0f;
        if (uncUnlocked && wUnc > config.weightUncommon) totalBonus += (wUnc - config.weightUncommon);
        if (rareUnlocked && wRare > config.weightRare) totalBonus += (wRare - config.weightRare);
        if (legUnlocked && wLeg > config.weightLegendary) totalBonus += (wLeg - config.weightLegendary);
        if (cursedUnlocked && wCursed > config.weightCursed) totalBonus += (wCursed - config.weightCursed);
        wCommon = Mathf.Max(0f, wCommon - totalBonus);

        float totalWeight = wCommon + wUnc + wRare + wLeg + wCursed;
        if (totalWeight <= 0f) return RuneRarity.Common;

        float roll = SafeRandomRange(0f, totalWeight);

        if (roll <= wCommon) return RuneRarity.Common; roll -= wCommon;
        if (roll <= wUnc) return RuneRarity.Uncommon; roll -= wUnc;
        if (roll <= wRare) return RuneRarity.Rare; roll -= wRare;
        if (roll <= wLeg) return RuneRarity.Legendary;
        
        return cursedUnlocked ? RuneRarity.Cursed : RuneRarity.Common;
    }

    private RuneDefinitionSO GetRandomDefinition(RuneRarity rarity)
    {
        List<RuneDefinitionSO> pool = rarity switch
        {
            RuneRarity.Common => config.commonRunes,
            RuneRarity.Uncommon => config.uncommonRunes,
            RuneRarity.Rare => config.rareRunes,
            RuneRarity.Legendary => config.legendaryRunes,
            RuneRarity.Cursed => config.cursedRunes,
            _ => null
        };

        if (pool == null || pool.Count == 0)
        {
            SafeLogWarning($"[RuneManager] Pula run dla rzadkości {rarity} jest PUSTA w konfiguracji!");
            return null;
        }

        return pool[SafeRandomRange(0, pool.Count)];
    }

    private float FormatValue(float val, RuneValueType type)
    {
        // Wartości procentowe oraz stałe punkty procentowe (>= 1) zaokrąglamy do pełnych liczb całkowitych
        if (type == RuneValueType.Percent || Mathf.Abs(val) >= 1f) return Mathf.Round(val);
        
        return (float)System.Math.Round(val, 1);
    }

    private float GetMetaBonus(MetaEffectType type)
    {
        return !ReferenceEquals(MetaUpgradeManager.Instance, null) ? MetaUpgradeManager.Instance.GetValue(type) : 0f;
    }
    
    // =========================================================================
    // SYSTEM FUZJI (2 takie same runy)
    // =========================================================================
    public bool TryFuseRunes(RuneItem r1, RuneItem r2)
    {
        // 1. Walidacja
        if (r1 == null || r2 == null || r1 == r2) return false;
        if (r1.definition.rarity != r2.definition.rarity) return false; // Ten sam poziom
        if (r1.definition.primaryStat != r2.definition.primaryStat) return false; // Ta sama statystyka
        if (r1.definition.rarity == RuneRarity.Cursed) return false; // Cursed nie można łączyć

        // 2. Określenie nowej rzadkości
        RuneRarity nextRarity = r1.definition.rarity + 1;
        if (nextRarity == RuneRarity.Cursed)
        {
            bool canCraftCursed = ReferenceEquals(MetaUpgradeManager.Instance, null) || MetaUpgradeManager.Instance.IsUnlocked(MetaEffectType.UnlockCursedCrafting);
            if (!canCraftCursed)
            {
                SafeLogWarning("[RuneManager] Wytwarzanie run Cursed podczas fuzji jest zablokowane w meta-progresji!");
                return false;
            }
        }

        // 3. Ustalenie typu wartości (Flat czy Percent)
        RuneValueType newValueType;
        if (r1.definition.valueType == r2.definition.valueType)
        {
            newValueType = r1.definition.valueType; // Takie same, zachowujemy
        }
        else
        {
            // Różne: 60% na Flat, 40% na Percent
            newValueType = SafeRandomValue() <= 0.60f ? RuneValueType.Flat : RuneValueType.Percent;
        }

        // 4. Szukamy odpowiedniego szablonu dla nowej runy
        RuneDefinitionSO newDef = FindSpecificDefinition(nextRarity, r1.definition.primaryStat, newValueType);
        if (ReferenceEquals(newDef, null))
        {
            SafeLogError($"[RuneManager] Fuzja nieudana. Brak definicji dla {nextRarity} {r1.definition.primaryStat} {newValueType}!");
            return false;
        }

        // 5. Losujemy nową wartość i tworzymy runę
        float newVal = SafeRandomRange(newDef.primaryValueRange.x, newDef.primaryValueRange.y);
        newVal = FormatValue(newVal, newDef.valueType);
        RuneItem newRune = new RuneItem(newDef, newVal);

        // 6. Finalizacja (usuń stare, dodaj nową)
        playerRunes.Remove(r1);
        playerRunes.Remove(r2);
        playerRunes.Add(newRune);
        OnInventoryChanged?.Invoke();

        SafeLog($"[RuneManager] Fuzja udana! Otrzymano: {newRune.GetPrimaryText()} ({newRune.definition.rarity})");
        if (!ReferenceEquals(HopeSessionManager.Instance, null))
        {
            HopeSessionManager.Instance.OnRuneCrafted();
            HopeSessionManager.Instance.OnRuneObtained(newRune.definition.rarity);
        }
        
        return true;
    }

    // =========================================================================
    // SYSTEM TRANSMUTACJI (3 różne runy)
    // =========================================================================
    public bool TryTransmuteRunes(RuneItem r1, RuneItem r2, RuneItem r3)
    {
        // 1. Walidacja
        if (r1 == null || r2 == null || r3 == null) return false;
        if (r1 == r2 || r1 == r3 || r2 == r3) return false;
        if (r1.definition.rarity != r2.definition.rarity || r1.definition.rarity != r3.definition.rarity) return false;
        if (r1.definition.rarity == RuneRarity.Cursed) return false;

        RuneRarity currentRarity = r1.definition.rarity;
        RuneRarity nextRarity = currentRarity + 1;

        bool canCraftCursed = ReferenceEquals(MetaUpgradeManager.Instance, null) || MetaUpgradeManager.Instance.IsUnlocked(MetaEffectType.UnlockCursedCrafting);

        // Specjalne zasady dla Rare i Legendary
        if (currentRarity == RuneRarity.Rare)
        {
            // 20% szansy na przeskok od razu do Cursed (tylko jeśli odblokowano crafting Cursed)
            if (canCraftCursed && SafeRandomValue() <= 0.20f)
            {
                nextRarity = RuneRarity.Cursed;
            }
        }
        else if (currentRarity == RuneRarity.Legendary)
        {
            if (!canCraftCursed)
            {
                SafeLogWarning("[RuneManager] Wytwarzanie run Cursed podczas transmutacji jest zablokowane w meta-progresji!");
                return false;
            }
            nextRarity = RuneRarity.Cursed; // Gwarantowany Cursed z 3 Legendarek
        }

        // 2. Generujemy całkowicie nową losową runę z docelowego poziomu
        RuneItem newRune = GenerateRandomRune(nextRarity);

        if (newRune != null)
        {
            playerRunes.Remove(r1);
            playerRunes.Remove(r2);
            playerRunes.Remove(r3);
            playerRunes.Add(newRune);
            OnInventoryChanged?.Invoke();

            SafeLog($"[RuneManager] Transmutacja udana! Otrzymano: {newRune.definition.runeName} ({newRune.definition.rarity})");
            if (!ReferenceEquals(HopeSessionManager.Instance, null))
            {
                HopeSessionManager.Instance.OnRuneCrafted();
                HopeSessionManager.Instance.OnRuneObtained(newRune.definition.rarity);
            }
            return true;
        }
        return false;
    }

    // Pomocnicza metoda szukająca konkretnego SO (używana przy Fuzji)
    private RuneDefinitionSO FindSpecificDefinition(RuneRarity rarity, RuneStatType stat, RuneValueType vType)
    {
        List<RuneDefinitionSO> pool = rarity switch
        {
            RuneRarity.Common => config.commonRunes,
            RuneRarity.Uncommon => config.uncommonRunes,
            RuneRarity.Rare => config.rareRunes,
            RuneRarity.Legendary => config.legendaryRunes,
            RuneRarity.Cursed => config.cursedRunes,
            _ => null
        };

        if (pool == null) return null;
        return pool.Find(r => r != null && r.primaryStat == stat && r.valueType == vType)
            ?? pool.Find(r => r != null && r.primaryStat == stat);
    }
    public RuneItem GenerateRuneWithStat(RuneRarity rarity, RuneStatType stat)
    {
        List<RuneDefinitionSO> pool = rarity switch
        {
            RuneRarity.Common => config.commonRunes,
            RuneRarity.Uncommon => config.uncommonRunes,
            RuneRarity.Rare => config.rareRunes,
            RuneRarity.Legendary => config.legendaryRunes,
            RuneRarity.Cursed => config.cursedRunes,
            _ => null
        };

        if (pool == null || pool.Count == 0) return null;

        var matchingDefs = pool.FindAll(r => r.primaryStat == stat);
        if (matchingDefs.Count == 0) return null;

        RuneDefinitionSO definition = matchingDefs[SafeRandomRange(0, matchingDefs.Count)];
        float primaryVal = SafeRandomRange(definition.primaryValueRange.x, definition.primaryValueRange.y);
        primaryVal = FormatValue(primaryVal, definition.valueType);

        RuneItem rune = new RuneItem(definition, primaryVal);

        if (rarity == RuneRarity.Cursed && definition.possiblePenalties != null && definition.possiblePenalties.Count > 0)
        {
            var penalty = definition.possiblePenalties[SafeRandomRange(0, definition.possiblePenalties.Count)];
            float penVal = SafeRandomRange(penalty.penaltyRange.x, penalty.penaltyRange.y);
            penVal = FormatValue(penVal, penalty.valueType);
            rune.SetPenalty(penalty.stat, penalty.valueType, -penVal);
        }

        playerRunes.Add(rune);
        OnInventoryChanged?.Invoke();
        return rune;
    }

    public void ClearInventory()
    {
        playerRunes.Clear();
        OnInventoryChanged?.Invoke();
    }

    public void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }
}