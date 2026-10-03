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

    }

    // =========================================================================
    // API DO ZARZĄDZANIA WYNIKIEM
    // =========================================================================

    public void AddMultiplier(float value)
    {

    }

    private void GrantHope(HopeMilestoneConfigSO.MilestoneGoal milestone)
    {

    }

    /// <summary>Wywoływane na ekranie GameOver. Zwraca całkowitą sumę do zapisania.</summary>
    public int CalculateFinalHope()
    {
        return 1;
    }
    

    private void EvaluateMilestones(MilestoneType typeToCheck)
    {
        
    }

    // --- Nasłuchiwanie na konkretne zdarzenia ---

    public void OnEnemyKilled(EnemyRank rank)
    {

    }

    public void OnRuneCrafted()
    {

    }

    public void OnRuneObtained(RuneRarity rarity)
    {

    }

    public void OnCitizenSpawned()
    {

    }

    private void CheckTimeMilestones(int newDay) => EvaluateMilestones(MilestoneType.NightsSurvived);

    private void CheckBuildingMilestones(BuildingEntity building)
    {

    }
}