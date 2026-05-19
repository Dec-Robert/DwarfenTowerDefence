using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class CitizenManager : MonoBehaviour
{
    [System.Serializable]
    public class RaceStats
    {
        public int Total;
        public int Idle;
        public int Working;
    }
    
    public static CitizenManager Instance { get; private set; }

    public List<Citizen> citizens = new List<Citizen>();
    public Dictionary<Race, RaceStats> PopulationStats = new Dictionary<Race, RaceStats>();

    public event Action<RaceStats, Race> PopulationChange;
    
    [SerializeField] private bool debugMode = false;

    
    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;
        
        PopulationStats[Race.Humans] = new RaceStats();
        PopulationStats[Race.Elves] = new RaceStats();
        PopulationStats[Race.Dwarves] = new RaceStats();
    }
    //
    void Start()
    {
        if (debugMode)
        {
            // Tworzymy 10 testowych obywateli
            for (int i = 0; i < 10; i++)
            {
                Citizen newCitizen = new Citizen();
                if (i % 3 == 0) newCitizen.InitializeCitizen(Race.Humans);
                else if (i % 3 == 1) newCitizen.InitializeCitizen(Race.Elves);
                else newCitizen.InitializeCitizen(Race.Dwarves);

                citizens.Add(newCitizen);
            }
        }

        RefreshStats();
    }

    // --- POPRAWKA 2: Metoda do tworzenia nowych ludzi (dla Domow) ---
    public Citizen SpawnNewCitizen(Race race, HousingEntity home)
    {
        Citizen newCitizen = new Citizen();
        newCitizen.InitializeCitizen(race);
        newCitizen.AssignHome(home); // Przypisujemy dom (zdefiniowane w Citizen.cs)

        citizens.Add(newCitizen);

        Debug.Log($"Narodzi� si� nowy {race} w {home.name}");
        if (HopeSessionManager.Instance != null) HopeSessionManager.Instance.OnCitizenSpawned();
        RefreshStats();
        return newCitizen;
    }

    // Metoda pomocnicza dla budynkow (szukanie pracownika)
    public Citizen FindAndAssignCitizen(Race race, BuildingEntity workplace)
    {
        Citizen availableCitizen = citizens.Find(c => c.race == race && c.workState == WorkState.Idle);

        if (availableCitizen != null)
        {
            availableCitizen.AssignToWorkplace(workplace);
            RefreshStats();
            return availableCitizen;
        }
        
        return null;
    }
    
    // Refreshing stats in PopulationStats for UI
    public void RefreshStats()
    {
        // Szybkie zerowanie
        foreach (var stat in PopulationStats.Values)
        {
            stat.Total = stat.Idle = stat.Working = 0;
        }

        // Jedno przejście przez populację (bardzo wydajne)
        foreach (var c in citizens)
        {
            PopulationStats[c.race].Total++;
            
            if (c.workState == WorkState.Idle)
                PopulationStats[c.race].Idle++;
            else
                PopulationStats[c.race].Working++;
            
            
        }

        foreach (var populationStat in PopulationStats)
        {
            PopulationChange?.Invoke(populationStat.Value, populationStat.Key);
        }
    }
}