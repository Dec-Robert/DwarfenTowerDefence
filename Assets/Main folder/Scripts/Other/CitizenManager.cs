using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class CitizenManager : MonoBehaviour
{
    public static CitizenManager Instance { get; private set; }

    public List<Citizen> citizens = new List<Citizen>();

    [SerializeField] private bool debugMode = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        else Instance = this;
    }

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

            // --- POPRAWKA 1: Powiadom ResourceManager o debugowych ludziach ---
            // Musimy to zrobiæ, ¿eby UI odœwie¿y³o siê na starcie
            if (ResourceManager.Instance != null)
            {
                // Dodajemy ich "wirtualnie" do banku zasobów, co wywo³a odœwie¿enie UI
                ResourceManager.Instance.AddResource(ResourceType.Population, citizens.Count);
            }
        }
    }

    // --- POPRAWKA 2: Metoda do tworzenia nowych ludzi (dla Domów) ---
    public Citizen SpawnNewCitizen(Race race, HousingEntity home)
    {
        Citizen newCitizen = new Citizen();
        newCitizen.InitializeCitizen(race);
        newCitizen.AssignHome(home); // Przypisujemy dom (zdefiniowane w Citizen.cs)

        citizens.Add(newCitizen);

        // KLUCZOWE: Mówimy systemowi ekonomii, ¿e przyby³ cz³owiek.
        // To wywo³a event OnResourceChanged, który zaktualizuje UI na H:X | E:Y | D:Z
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.AddResource(ResourceType.Population, 1);
        }

        Debug.Log($"Narodzi³ siê nowy {race} w {home.name}");
        return newCitizen;
    }

    // Metoda pomocnicza dla budynków (szukanie pracownika)
    public Citizen FindAndAssignCitizen(Race race, BuildingEntity workplace)
    {
        Citizen availableCitizen = citizens.Find(c => c.race == race && c.workState == WorkState.Idle);

        if (availableCitizen != null)
        {
            availableCitizen.AssignToWorkplace(workplace);
            return availableCitizen;
        }
        return null;
    }

    // Metoda dla UIManager do zliczania ras
    public int GetRaceCount(Race race)
    {
        return citizens.Count(c => c.race == race);
    }
}