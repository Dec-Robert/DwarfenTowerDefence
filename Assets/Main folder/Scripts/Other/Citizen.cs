using UnityEngine;

[System.Serializable]
public class Citizen
{
    public string citizenID;
    public Race race;
    public WorkState workState;
    public BuildingEntity currentWorkplace;
    public HousingEntity home; 

    // Metoda przypisania do domu
    public void AssignHome(HousingEntity newHome)
    {
        home = newHome;
    }
    public void InitializeCitizen(Race r)
    {
        citizenID = System.Guid.NewGuid().ToString();
        race = r;
        workState = WorkState.Idle;
        currentWorkplace = null;
    }

    public void AssignToWorkplace(BuildingEntity workplace)
    {
        currentWorkplace = workplace;
        workState = WorkState.Assigned;
    }

    public void RemoveFromWorkplace()
    {
        currentWorkplace = null;
        workState = WorkState.Idle;
    }

}
