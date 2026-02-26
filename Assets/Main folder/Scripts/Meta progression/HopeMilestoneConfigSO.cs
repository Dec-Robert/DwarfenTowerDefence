using UnityEngine;
using System.Collections.Generic;

public enum MilestoneType
{
    TotalPopulation,
    SpecificRacePopulation,
    TotalTowers,
    SpecificTower,
    NightsSurvived,
    BossesKilled,
    EnemiesKilled,
    ProductionBuildingsBuilt,
    LegendaryOrCursedRunesObtained,
    RuneCraftingDone // Fuzje i Transmutacje łącznie
}

[CreateAssetMenu(fileName = "HopeMilestones", menuName = "Tower Defense/Hope Milestones Config")]
public class HopeMilestoneConfigSO : ScriptableObject
{
    [System.Serializable]
    public class MilestoneGoal
    {
        public string description;        // np. "Zabij 100 wrogów" (dla UI na ekranie końcowym)
        public MilestoneType type;
        
        [Tooltip("Wymagana wartość do osiągnięcia celu (np. 100 wrogów, 5. noc)")]
        public int targetValue;
        
        [Tooltip("Nagroda w postaci punktów Nadziei")]
        public int hopeReward;

        [Header("Parametry opcjonalne (zależne od typu)")]
        public Race targetRace;           // Jeśli type = SpecificRacePopulation
        public TowerData targetTower;     // Jeśli type = SpecificTower
    }

    public List<MilestoneGoal> milestones = new List<MilestoneGoal>();
}