using UnityEngine;

public class Citizen 
{
    public string citizenID { get; set; }
    public string name { get; set; }
    public Race race { get; set; }
    public WorkState workState { get; set; }
    public BuildingEntity currentWorkplace { get; set; }

    void Start()
    {
        name = Random.Range(0, 1000).ToString();

        citizenID = System.Guid.NewGuid().ToString();
    }

}
