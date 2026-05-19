using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class HopeSessionManager : MonoBehaviour
{
    public static HopeSessionManager Instance { get; private set; }

    [Header("Konfiguracja")]
    public HopeMilestoneConfigSO milestoneConfig;

    [Header("Stan obecnej gry (Podgląd)")]
    public float currentMultiplier = 1.0f;

    // Struktura przechowująca informację za co gracz dostał nadzieję
    [System.Serializable]
    public struct HopeRecord
    {
        public string reason;
        public int amount;
    }
    public List<HopeRecord> earnedHopeList = new List<HopeRecord>();

    // --- WEWNĘTRZNE LICZNIKI RUNU ---
    private int enemiesKilled = 0;
    private int bossesKilled = 0;
    private int runesCrafted = 0;
    private int legendaryCursedRunes = 0;
    
    // Zapamiętuje które kamienie milowe już zrealizowano, by nie dać nagrody 2x
    private HashSet<HopeMilestoneConfigSO.MilestoneGoal> completedMilestones = new HashSet<HopeMilestoneConfigSO.MilestoneGoal>();

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Start()
    {
        // Subskrypcja eventów (istniejących)
        if (TimeCycleManager.Instance != null)
            TimeCycleManager.Instance.OnDayChanged += CheckTimeMilestones;

        if (BuildingRegistry.Instance != null)
            BuildingRegistry.Instance.OnBuildingRegistered += CheckBuildingMilestones;
    }

    // =========================================================================
    // API DO ZARZĄDZANIA WYNIKIEM
    // =========================================================================

    public void AddMultiplier(float value)
    {
        currentMultiplier += value;
        // Zabezpieczenie przed ujemnym mnożnikiem
        if (currentMultiplier < 0.1f) currentMultiplier = 0.1f; 
        Debug.Log($"[HopeManager] Mnożnik nadziei zmieniony na: {currentMultiplier}x");
    }

    private void GrantHope(HopeMilestoneConfigSO.MilestoneGoal milestone)
    {
        if (completedMilestones.Contains(milestone)) return;

        completedMilestones.Add(milestone);
        earnedHopeList.Add(new HopeRecord { reason = milestone.description, amount = milestone.hopeReward });
        
        Debug.Log($"<color=cyan>[HopeManager] Osiągnięcie: {milestone.description}! +{milestone.hopeReward} Nadziei.</color>");
    }

    /// <summary>Wywoływane na ekranie GameOver. Zwraca całkowitą sumę do zapisania.</summary>
    public int CalculateFinalHope()
    {
        int sum = earnedHopeList.Sum(record => record.amount);
        int finalAmount = Mathf.RoundToInt(sum * currentMultiplier);
        return finalAmount;
    }

    // =========================================================================
    // SYSTEM ŚLEDZENIA (TRACKERY) I EWALUACJI
    // =========================================================================

    private void EvaluateMilestones(MilestoneType typeToCheck)
    {
        if (milestoneConfig == null) return;

        foreach (var m in milestoneConfig.milestones)
        {
            if (m.type != typeToCheck || completedMilestones.Contains(m)) continue;

            bool isAchieved = false;

            switch (m.type)
            {
                case MilestoneType.TotalPopulation:
                    isAchieved = CitizenManager.Instance.citizens.Count >= m.targetValue;
                    break;
                /*
                case MilestoneType.SpecificRacePopulation:
                    isAchieved = CitizenManager.Instance.GetRaceCount(m.targetRace) >= m.targetValue;
                    break;
                */
                case MilestoneType.EnemiesKilled:
                    isAchieved = enemiesKilled >= m.targetValue;
                    break;
                case MilestoneType.BossesKilled:
                    isAchieved = bossesKilled >= m.targetValue;
                    break;
                case MilestoneType.NightsSurvived:
                    // Dzień 2 oznacza przetrwaną 1 noc
                    isAchieved = (TimeCycleManager.Instance.dayCount - 1) >= m.targetValue;
                    break;
                case MilestoneType.TotalTowers:
                    isAchieved = BuildingRegistry.Instance.GetAllOfType<TowerEntity>().Count >= m.targetValue;
                    break;
                case MilestoneType.SpecificTower:
                    isAchieved = BuildingRegistry.Instance.GetAllOfType<TowerEntity>().Count(t => t.data == m.targetTower) >= m.targetValue;
                    break;
                case MilestoneType.ProductionBuildingsBuilt:
                    isAchieved = BuildingRegistry.Instance.AllBuildings.Count(b => b.data.type == BuildingType.Economic) >= m.targetValue;
                    break;
                case MilestoneType.LegendaryOrCursedRunesObtained:
                    isAchieved = legendaryCursedRunes >= m.targetValue;
                    break;
                case MilestoneType.RuneCraftingDone:
                    isAchieved = runesCrafted >= m.targetValue;
                    break;
            }

            if (isAchieved) GrantHope(m);
        }
    }

    // --- Nasłuchiwanie na konkretne zdarzenia ---

    public void OnEnemyKilled(EnemyRank rank)
    {
        enemiesKilled++;
        if (rank == EnemyRank.Boss) bossesKilled++;

        EvaluateMilestones(MilestoneType.EnemiesKilled);
        if (rank == EnemyRank.Boss) EvaluateMilestones(MilestoneType.BossesKilled);
    }

    public void OnRuneCrafted()
    {
        runesCrafted++;
        EvaluateMilestones(MilestoneType.RuneCraftingDone);
    }

    public void OnRuneObtained(RuneRarity rarity)
    {
        if (rarity == RuneRarity.Legendary || rarity == RuneRarity.Cursed)
        {
            legendaryCursedRunes++;
            EvaluateMilestones(MilestoneType.LegendaryOrCursedRunesObtained);
        }
    }

    public void OnCitizenSpawned()
    {
        EvaluateMilestones(MilestoneType.TotalPopulation);
        EvaluateMilestones(MilestoneType.SpecificRacePopulation);
    }

    private void CheckTimeMilestones(int newDay) => EvaluateMilestones(MilestoneType.NightsSurvived);

    private void CheckBuildingMilestones(BuildingEntity building)
    {
        if (building is TowerEntity)
        {
            EvaluateMilestones(MilestoneType.TotalTowers);
            EvaluateMilestones(MilestoneType.SpecificTower);
        }
        else if (building.data.type == BuildingType.Economic)
        {
            EvaluateMilestones(MilestoneType.ProductionBuildingsBuilt);
        }
    }
}