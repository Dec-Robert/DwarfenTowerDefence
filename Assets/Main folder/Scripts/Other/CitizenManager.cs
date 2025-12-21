using System.Collections.Generic;
using UnityEngine;

public class CitizenManager : MonoBehaviour
{
    public static CitizenManager Instance { get; private set; }

    public List<Citizen> citizens = new List<Citizen>();

    [SerializeField] private bool debugMode = false;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        if (debugMode)
        {
            for (int i = 0; i < 10; i++)
            {
                Citizen newCitizen = new Citizen();
                if (i % 3 == 0)
                {
                    newCitizen.InitializeCitizen(Race.Humans);
                }
                else if (i % 3 == 1)
                {
                    newCitizen.InitializeCitizen(Race.Elves);
                }
                else
                {
                    newCitizen.InitializeCitizen(Race.Dwarves);
                }

                citizens.Add(newCitizen);
            }
        }
    }



    public Citizen FindAndAssignCitizen(Race race, BuildingEntity workplace)
    {
        Citizen availableCitizen = citizens.Find(c => c.race == race && c.workState == WorkState.Idle);

        if (availableCitizen != null)
        {
            availableCitizen.AssignToWorkplace(workplace);
            return availableCitizen;
        }

        Debug.LogWarning($"Brak wolnych pracowników rasy {race}!");
        return null;
    }

    public void FreeCitizen(Citizen citizen)
    {
        if (citizen != null) citizen.RemoveFromWorkplace();
    }

}
