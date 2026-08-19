using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SingleWorkerManagerUI : MonoBehaviour
{
    public Race race;
    public ResourceVisualsSO raceVisuals;

    public Image icon;
    public TextMeshProUGUI countText;
    public Button buttonAdd;
    public Button buttonRemove;
    private BuildingEntity currentBuilding;

    private void Awake()
    {
        icon.sprite = raceVisuals.GetIcon(race);
    }

    public void Setup(BuildingEntity currentBuilding)
    {
        this.currentBuilding = currentBuilding;
        buttonAdd.onClick.RemoveAllListeners();
        buttonAdd.onClick.AddListener(() =>
        {
            currentBuilding.TryAddWorker(race);
            
            RefreshUI();
            
        });
        
        buttonRemove.onClick.RemoveAllListeners();
        buttonRemove.onClick.AddListener(() =>
        {
            currentBuilding.RemoveWorker(race);
            
            RefreshUI();
        });

        RefreshUI();
    }
    
    public void RefreshUI()
    {
        if (currentBuilding == null) return;
        
        int currentWorkers = currentBuilding.GetWorkerCount(race);
        countText.text = currentWorkers.ToString();
        
        buttonRemove.interactable = (currentWorkers > 0);
        
        bool isBuildingFull = currentBuilding.GetTotalWorkerCount() >= currentBuilding.GetTotalWorkerCount();
        bool hasFreeCitizen = CitizenManager.Instance.PopulationStats[race].Idle > 0;
        
        buttonAdd.interactable = (!isBuildingFull && hasFreeCitizen);
    }
    
    
}
