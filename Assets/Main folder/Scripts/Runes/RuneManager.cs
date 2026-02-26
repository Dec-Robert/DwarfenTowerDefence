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

    // =========================================================================
    // API Dropu z Wrogów
    // =========================================================================

    public void RollRuneDrop(EnemyRank rank)
    {
        if (config == null) return;

        int runesToDrop = 0;
        switch (rank)
        {
            case EnemyRank.Normal:
                if (Random.Range(0f, 100f) <= config.dropChanceNormal) runesToDrop = 1;
                break;
            case EnemyRank.Elite:
                if (Random.Range(0f, 100f) <= config.dropChanceElite) runesToDrop = 1;
                break;
            case EnemyRank.Boss:
                runesToDrop = Random.Range(config.minRunesFromBoss, config.maxRunesFromBoss + 1);
                break;
        }

        for (int i = 0; i < runesToDrop; i++)
        {
            RuneItem newRune = GenerateRandomRune();
            if (HopeSessionManager.Instance != null) HopeSessionManager.Instance.OnRuneObtained(newRune.definition.rarity);
            if (newRune != null)
            {
                playerRunes.Add(newRune);
                Debug.Log($"<color=magenta>[RuneManager] Drop: {newRune.definition.runeName} | {newRune.GetPrimaryText()} {newRune.GetPenaltyText()}</color>");
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
        if (definition == null) return null;

        // 2. Wylosuj główną wartość
        float primaryVal = Random.Range(definition.primaryValueRange.x, definition.primaryValueRange.y);
        primaryVal = FormatValue(primaryVal, definition.valueType);

        RuneItem rune = new RuneItem(definition, primaryVal);

        // 3. Jeśli to runa Przeklęta, wylosuj jedną karę z jej listy
        if (rarity == RuneRarity.Cursed && definition.possiblePenalties != null && definition.possiblePenalties.Count > 0)
        {
            var penalty = definition.possiblePenalties[Random.Range(0, definition.possiblePenalties.Count)];
            
            float penVal = Random.Range(penalty.penaltyRange.x, penalty.penaltyRange.y);
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
        float wCommon = config.weightCommon;
        float wUnc = config.weightUncommon + GetMetaBonus(MetaEffectType.RuneDropChance_Uncommon);
        float wRare = config.weightRare + GetMetaBonus(MetaEffectType.RuneDropChance_Rare);
        float wLeg = config.weightLegendary + GetMetaBonus(MetaEffectType.RuneDropChance_Legendary);
        float wCursed = config.weightCursed + GetMetaBonus(MetaEffectType.RuneDropChance_Cursed);

        float totalBonus = (wUnc - config.weightUncommon) + (wRare - config.weightRare) + 
                           (wLeg - config.weightLegendary) + (wCursed - config.weightCursed);
        wCommon = Mathf.Max(0f, wCommon - totalBonus);

        float totalWeight = wCommon + wUnc + wRare + wLeg + wCursed;
        float roll = Random.Range(0f, totalWeight);

        if (roll <= wCommon) return RuneRarity.Common; roll -= wCommon;
        if (roll <= wUnc) return RuneRarity.Uncommon; roll -= wUnc;
        if (roll <= wRare) return RuneRarity.Rare; roll -= wRare;
        if (roll <= wLeg) return RuneRarity.Legendary;
        
        return RuneRarity.Cursed;
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
            Debug.LogWarning($"[RuneManager] Pula run dla rzadkości {rarity} jest PUSTA w konfiguracji!");
            return null;
        }

        return pool[Random.Range(0, pool.Count)];
    }

    private float FormatValue(float val, RuneValueType type)
    {
        // Jeśli wartość procentowa - zostawiamy równe liczby całkowite (np. 15%)
        if (type == RuneValueType.Percent) return Mathf.Round(val);
        
        // ZMIANA: Jeśli flat (np. Zasięg, Obrażenia) - zaokrąglamy do 1 miejsca po przecinku (np. 9.2)
        return (float)System.Math.Round(val, 1);
    }

    private float GetMetaBonus(MetaEffectType type)
    {
        return MetaUpgradeManager.Instance != null ? MetaUpgradeManager.Instance.GetValue(type) : 0f;
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

        // 3. Ustalenie typu wartości (Flat czy Percent)
        RuneValueType newValueType;
        if (r1.definition.valueType == r2.definition.valueType)
        {
            newValueType = r1.definition.valueType; // Takie same, zachowujemy
        }
        else
        {
            // Różne: 60% na Flat, 40% na Percent
            newValueType = Random.value <= 0.60f ? RuneValueType.Flat : RuneValueType.Percent;
        }

        // 4. Szukamy odpowiedniego szablonu dla nowej runy
        RuneDefinitionSO newDef = FindSpecificDefinition(nextRarity, r1.definition.primaryStat, newValueType);
        if (newDef == null)
        {
            Debug.LogError($"[RuneManager] Fuzja nieudana. Brak definicji dla {nextRarity} {r1.definition.primaryStat} {newValueType}!");
            return false;
        }

        // 5. Losujemy nową wartość i tworzymy runę
        float newVal = Random.Range(newDef.primaryValueRange.x, newDef.primaryValueRange.y);
        newVal = FormatValue(newVal, newDef.valueType);
        RuneItem newRune = new RuneItem(newDef, newVal);

        // 6. Finalizacja (usuń stare, dodaj nową)
        playerRunes.Remove(r1);
        playerRunes.Remove(r2);
        playerRunes.Add(newRune);
        OnInventoryChanged?.Invoke();

        Debug.Log($"[RuneManager] Fuzja udana! Otrzymano: {newRune.GetPrimaryText()} ({newRune.definition.rarity})");
        HopeSessionManager.Instance.OnRuneCrafted();
        HopeSessionManager.Instance.OnRuneObtained(newRune.definition.rarity);
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

        // Specjalne zasady dla Rare i Legendary
        if (currentRarity == RuneRarity.Rare)
        {
            // 20% szansy na przeskok od razu do Cursed
            if (Random.value <= 0.20f) nextRarity = RuneRarity.Cursed;
        }
        else if (currentRarity == RuneRarity.Legendary)
        {
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

            Debug.Log($"[RuneManager] Transmutacja udana! Otrzymano: {newRune.definition.runeName} ({newRune.definition.rarity})");
            HopeSessionManager.Instance.OnRuneCrafted();
            HopeSessionManager.Instance.OnRuneObtained(newRune.definition.rarity);
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
        return pool.Find(r => r.primaryStat == stat && r.valueType == vType);
    }
    public void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }
}