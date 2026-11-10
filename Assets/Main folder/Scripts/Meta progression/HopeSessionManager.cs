using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Menedżer sesyjnego naliczania waluty meta progresji (Nadzieja / Hope).
/// 
/// Odpowiedzialność:
/// 1. Zliczanie osiągnięć i kamieni milowych podczas runu (fale, bossowie, kille, populacja, ekspedycje, wieże, runy).
/// 2. Gwarantowana baza za przetrwane noce (~10% puli).
/// 3. Rejestracja zdarzeń w trakcie rozgrywki (oraz bieżący podgląd w ResourceManager).
/// 4. Finalne podsumowanie po zakończeniu rozgrywki (EndSession) z zapisem do SaveManager.Instance.currentSaveData.totalHope.
/// </summary>
public class HopeSessionManager : MonoBehaviour
{
    public static HopeSessionManager Instance { get; private set; }

    [Header("Konfiguracja")]
    public HopeMilestoneConfigSO milestoneConfig;

    [Header("Stan obecnej gry (Podgląd)")]
    public float currentMultiplier = 1.0f;

    [System.Serializable]
    public struct HopeRecord
    {
        public string reason;
        public int amount;
        public MilestoneType? milestoneType;
    }

    [System.Serializable]
    public class HopeSessionSummary
    {
        public bool isVictory;
        public int nightsSurvived;
        public int wavePoints;
        public int milestonePoints;
        public float multiplier;
        public int rawTotalHope;
        public int finalTotalHope;
        public List<HopeRecord> records = new List<HopeRecord>();
    }

    public List<HopeRecord> earnedHopeList = new List<HopeRecord>();

    // --- WEWNĘTRZNE LICZNIKI RUNU ---
    public int nightsSurvived { get; private set; } = 0;
    public int enemiesKilled { get; private set; } = 0;
    public int bossesKilled { get; private set; } = 0;
    public int expeditionsCompleted { get; private set; } = 0;
    public int runesCrafted { get; private set; } = 0;
    public int highRarityRunesObtained { get; private set; } = 0;
    public int towersBuilt { get; private set; } = 0;
    public int productionBuildingsBuilt { get; private set; } = 0;

    public int wavePointsEarned { get; private set; } = 0;
    public int milestonePointsEarned { get; private set; } = 0;
    private int debugRawHope = 0;

    public bool isSessionEnded { get; private set; } = false;
    public HopeSessionSummary lastSummary { get; private set; }

    public event Action<HopeSessionSummary> OnSessionEnded;
    public event Action<HopeRecord> OnHopeRecordAdded;

    // Zapamiętuje które kamienie milowe już zrealizowano, by nie dać nagrody 2x
    private readonly HashSet<string> completedMilestones = new HashSet<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (milestoneConfig == null)
        {
            milestoneConfig = Resources.Load<HopeMilestoneConfigSO>("HopeMilestonesConfig");
        }
        SubscribeToGameEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromGameEvents();
        if (Instance == this) Instance = null;
    }

    // =========================================================================
    // SUBSKRYPCJA ZDARZEŃ W ROZGRYWCE
    // =========================================================================

    private void SubscribeToGameEvents()
    {
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted += HandleMorningStarted;
        }

        if (BuildingRegistry.Instance != null)
        {
            BuildingRegistry.Instance.OnBuildingRegistered += HandleBuildingRegistered;
        }

        if (MapExpansionManager.Instance != null)
        {
            MapExpansionManager.Instance.OnMissionCompleted += HandleExpeditionCompleted;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver += HandleGameOver;
            GameManager.Instance.OnVictory += HandleVictory;
        }
    }

    private void UnsubscribeFromGameEvents()
    {
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= HandleMorningStarted;
        }

        if (BuildingRegistry.Instance != null)
        {
            BuildingRegistry.Instance.OnBuildingRegistered -= HandleBuildingRegistered;
        }

        if (MapExpansionManager.Instance != null)
        {
            MapExpansionManager.Instance.OnMissionCompleted -= HandleExpeditionCompleted;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= HandleGameOver;
            GameManager.Instance.OnVictory -= HandleVictory;
        }
    }

    // =========================================================================
    // OBSŁUGA ZDARZEŃ CYKLU I ŚWIATA
    // =========================================================================

    private void HandleMorningStarted()
    {
        // Nowy dzień po nocy oznacza ukończoną noc
        if (TimePhaseManager.Instance != null && TimePhaseManager.Instance.CurrentDay > 1)
        {
            int survivedNightIndex = TimePhaseManager.Instance.CurrentDay - 1;
            if (survivedNightIndex > nightsSurvived)
            {
                nightsSurvived = survivedNightIndex;
                int wavePoints = GetPointsForWave(nightsSurvived);
                wavePointsEarned += wavePoints;

                AddRecord($"Przetrwano noc {nightsSurvived}", wavePoints, MilestoneType.NightsSurvived);
                EvaluateMilestones(MilestoneType.NightsSurvived, nightsSurvived);
            }
        }
    }

    private void HandleBuildingRegistered(BuildingEntity building)
    {
        if (building == null || building.data == null) return;

        if (building.data.type == BuildingType.Defense)
        {
            towersBuilt++;
            EvaluateMilestones(MilestoneType.TotalTowers, towersBuilt);
            if (building.data is TowerData towerData)
            {
                EvaluateSpecificTowerMilestones(towerData);
            }
        }
        else if (building.data.type == BuildingType.Economic)
        {
            productionBuildingsBuilt++;
            EvaluateMilestones(MilestoneType.ProductionBuildingsBuilt, productionBuildingsBuilt);
        }
    }

    private void HandleExpeditionCompleted(ScoutingMission mission)
    {
        OnExpeditionCompleted();
    }

    private void HandleGameOver()
    {
        EndSession(isVictory: false);
    }

    private void HandleVictory()
    {
        EndSession(isVictory: true);
    }

    // =========================================================================
    // API PUBLICZNE DO ZGŁASZANIA ZDARZEŃ
    // =========================================================================

    public void OnEnemyKilled(EnemyRank rank)
    {
        enemiesKilled++;
        EvaluateMilestones(MilestoneType.EnemiesKilled, enemiesKilled);

        if (rank == EnemyRank.Boss)
        {
            bossesKilled++;
            EvaluateMilestones(MilestoneType.BossesKilled, bossesKilled);
        }
    }

    public void OnRuneCrafted()
    {
        runesCrafted++;
        EvaluateMilestones(MilestoneType.RuneCraftingDone, runesCrafted);
    }

    public void OnRuneObtained(RuneRarity rarity)
    {
        if (rarity == RuneRarity.Rare || rarity == RuneRarity.Legendary || rarity == RuneRarity.Cursed)
        {
            highRarityRunesObtained++;
            EvaluateMilestones(MilestoneType.LegendaryOrCursedRunesObtained, highRarityRunesObtained);
        }
    }

    public void OnCitizenSpawned()
    {
        int totalPop = 0;
        if (PopLifeCycleManager.Instance != null && PopLifeCycleManager.Instance.Citizens != null)
        {
            totalPop = PopLifeCycleManager.Instance.Citizens.Count;
        }
        EvaluateMilestones(MilestoneType.TotalPopulation, totalPop);
    }

    public void OnExpeditionCompleted()
    {
        expeditionsCompleted++;
        EvaluateMilestones(MilestoneType.ExpeditionsCompleted, expeditionsCompleted);
    }

    public void AddRawHope(int amount, string reason = "Debug")
    {
        debugRawHope += amount;
        AddRecord(reason, amount, null);
    }

    public void AddMultiplier(float value)
    {
        currentMultiplier = Mathf.Max(0.1f, currentMultiplier + value);
    }

    public void SetMultiplier(float value)
    {
        currentMultiplier = Mathf.Max(0.1f, value);
    }

    // =========================================================================
    // KALKULACJA PUNKTÓW ZA FALE (BAZA ~10%)
    // =========================================================================

    /// <summary>
    /// Zwraca liczbę punktów Nadziei przyznawaną za konkretną noc.
    /// Fale 1–10: 1 pkt/noc (10 pkt)
    /// Fale 11–20: 2 pkt/noc (20 pkt)
    /// Fale 21–30: 10 pkt/noc (100 pkt)
    /// Fale 31–40: 24 pkt/noc (240 pkt)
    /// Fale 41–50: 30 pkt/noc (300 pkt)
    /// Łącznie za 50 nocy: 670 pkt
    /// </summary>
    public static int GetPointsForWave(int waveIndex)
    {
        if (waveIndex <= 0) return 0;
        if (waveIndex <= 10) return 1;
        if (waveIndex <= 20) return 2;
        if (waveIndex <= 30) return 10;
        if (waveIndex <= 40) return 24;
        return 30;
    }

    public static int CalculateCumulativeWavePoints(int nightsCount)
    {
        int total = 0;
        for (int i = 1; i <= nightsCount; i++)
        {
            total += GetPointsForWave(i);
        }
        return total;
    }

    // =========================================================================
    // EWALUACJA KAMIENI MILOWYCH
    // =========================================================================

    private void EvaluateMilestones(MilestoneType typeToCheck, int currentValue)
    {
        if (milestoneConfig == null || milestoneConfig.milestones == null) return;

        foreach (var milestone in milestoneConfig.milestones)
        {
            if (milestone == null || milestone.type != typeToCheck) continue;
            string key = GetMilestoneKey(milestone);
            if (completedMilestones.Contains(key)) continue;

            if (currentValue >= milestone.targetValue)
            {
                completedMilestones.Add(key);
                GrantHope(milestone);
            }
        }
    }

    private void EvaluateSpecificTowerMilestones(TowerData towerData)
    {
        if (milestoneConfig == null || milestoneConfig.milestones == null || towerData == null) return;

        foreach (var milestone in milestoneConfig.milestones)
        {
            if (milestone == null || milestone.type != MilestoneType.SpecificTower) continue;
            if (milestone.targetTower != towerData) continue;

            string key = GetMilestoneKey(milestone);
            if (completedMilestones.Contains(key)) continue;

            completedMilestones.Add(key);
            GrantHope(milestone);
        }
    }

    private void GrantHope(HopeMilestoneConfigSO.MilestoneGoal milestone)
    {
        if (milestone == null) return;
        milestonePointsEarned += milestone.hopeReward;
        string label = !string.IsNullOrEmpty(milestone.DisplayDescription) ? milestone.DisplayDescription : milestone.description;
        AddRecord(label, milestone.hopeReward, milestone.type);
    }

    private void AddRecord(string reason, int amount, MilestoneType? type)
    {
        var record = new HopeRecord
        {
            reason = reason,
            amount = amount,
            milestoneType = type
        };
        earnedHopeList.Add(record);

        if (ResourceManager.Instance != null && amount > 0)
        {
            ResourceManager.Instance.AddResource(ResourceType.Hope, amount);
        }

        OnHopeRecordAdded?.Invoke(record);
    }

    private string GetMilestoneKey(HopeMilestoneConfigSO.MilestoneGoal milestone)
    {
        if (!string.IsNullOrEmpty(milestone.id)) return milestone.id;
        return $"{milestone.type}_{milestone.targetValue}_{milestone.hopeReward}";
    }

    // =========================================================================
    // ZAKOŃCZENIE SESJI I ZAPIS
    // =========================================================================

    /// <summary>
    /// Kończy sesję runu, finalizuje kalkulację, zapisuje przyznaną Nadzieję w SaveData i generuje DTO raportu.
    /// </summary>
    public HopeSessionSummary EndSession(bool isVictory)
    {
        if (isSessionEnded && lastSummary != null)
        {
            return lastSummary;
        }

        if (isVictory)
        {
            EvaluateMilestones(MilestoneType.Victory, 1);
        }

        // Końcowa weryfikacja populacji
        if (PopLifeCycleManager.Instance != null && PopLifeCycleManager.Instance.Citizens != null)
        {
            EvaluateMilestones(MilestoneType.TotalPopulation, PopLifeCycleManager.Instance.Citizens.Count);
        }

        int rawTotal = wavePointsEarned + milestonePointsEarned + debugRawHope;
        int finalTotal = Mathf.Max(0, Mathf.RoundToInt(rawTotal * currentMultiplier));

        // Trwały zapis w SaveData
        if (SaveManager.Instance != null && SaveManager.Instance.currentSaveData != null)
        {
            SaveManager.Instance.currentSaveData.totalHope += finalTotal;
            SaveManager.Instance.SaveGame();
            Debug.Log($"<color=cyan>[HopeSessionManager] Run zakończony ({ (isVictory ? "VICTORY" : "GAME OVER") }). Przyznano: {finalTotal} Nadziei. Stan konta: {SaveManager.Instance.currentSaveData.totalHope}</color>");
        }

        lastSummary = new HopeSessionSummary
        {
            isVictory = isVictory,
            nightsSurvived = nightsSurvived,
            wavePoints = wavePointsEarned,
            milestonePoints = milestonePointsEarned,
            multiplier = currentMultiplier,
            rawTotalHope = rawTotal,
            finalTotalHope = finalTotal,
            records = new List<HopeRecord>(earnedHopeList)
        };

        isSessionEnded = true;
        OnSessionEnded?.Invoke(lastSummary);
        return lastSummary;
    }

    /// <summary>
    /// Pomocnicza metoda dla zewnętrznych skryptów lub UI chcących podejrzeć obecną sumę przed zakończeniem sesji.
    /// </summary>
    public int CalculateFinalHope()
    {
        int rawTotal = wavePointsEarned + milestonePointsEarned + debugRawHope;
        return Mathf.Max(0, Mathf.RoundToInt(rawTotal * currentMultiplier));
    }
}
