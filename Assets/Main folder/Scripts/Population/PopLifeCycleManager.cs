using System;
using System.Collections.Generic;
using UnityEngine;

public class PopLifeCycleManager : MonoBehaviour
{
    public static PopLifeCycleManager Instance { get; private set; }

    [SerializeField] private List<Citizen> citizens = new();
    [SerializeField] private bool debugMode;

    private readonly Dictionary<Race, int> totalByRace = new();

    public IReadOnlyList<Citizen> Citizens => citizens;

    public event Action<Citizen> OnCitizenBorn;
    public event Action<Citizen, DeathReason> OnCitizenDied;
    public event Action<Race, int> OnTotalPopulationChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        foreach (var race in (Race[])Enum.GetValues(typeof(Race)))
            totalByRace[race] = 0;
    }

    private void Start()
    {
        if (Instance != this || !debugMode) return;

        var races = (Race[])Enum.GetValues(typeof(Race));
        for (var i = 0; i < 10; i++)
            BirthCitizen(races[i % races.Length], null);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public Citizen BirthCitizen(Race race, HousingEntity home)
    {
        var citizen = new Citizen();
        citizen.InitializeCitizen(race);

        if (home != null)
        {
            citizen.AssignHome(home);
            home.AddResident(citizen);
        }

        citizens.Add(citizen);
        totalByRace[race]++;

        if (HopeSessionManager.Instance != null) HopeSessionManager.Instance.OnCitizenSpawned();

        OnCitizenBorn?.Invoke(citizen);
        OnTotalPopulationChanged?.Invoke(race, totalByRace[race]);
        return citizen;
    }

    public void KillCitizen(Citizen citizen, DeathReason reason)
    {
        if (citizen == null || !citizens.Remove(citizen)) return;

        if (citizen.home != null) citizen.home.RemoveResident(citizen);
        citizen.AssignHome(null);
        totalByRace[citizen.race]--;

        OnCitizenDied?.Invoke(citizen, reason);
        OnTotalPopulationChanged?.Invoke(citizen.race, totalByRace[citizen.race]);
    }

    public void KillAllResidents(HousingEntity home, DeathReason reason)
    {
        if (home == null) return;

        foreach (var resident in new List<Citizen>(home.residents))
            KillCitizen(resident, reason);
    }

    public int GetTotalCount(Race race) => totalByRace.TryGetValue(race, out var count) ? count : 0;
}