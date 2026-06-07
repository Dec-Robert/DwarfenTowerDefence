using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.iOS;
using UnityEngine.UI;

public class BuildingTooltipManagerUI : MonoBehaviour
{
    public static BuildingTooltipManagerUI Instance;
    [Header("Resource and battle stats icon DB")]
    public ResourceVisualsSO resourceIconDB;

    [Header("Collection of pnl")] 
    public GameObject pnlInfo;
    public GameObject pnlDescription;
    
    [Header("Single panels")]
    public GameObject pnlCollective;
    public GameObject pnlProduction;
    public GameObject pnlTower;
    public GameObject pnlHousing;
    public GameObject pnlUtility;

    [Header("Arrays")] 
    public GameObject pnlCostArray;
    public GameObject pnlMaintananceArray;
    public GameObject pnlProductionArray;
    public GameObject pnlStatArray;

    [Header("Population specific")] 
    public GameObject pnlHousingSpots;
    public GameObject pnlPopConsumption;
    public GameObject pnlPopProduction;
    [Tooltip("0 is prodution, 1 is consumption")]public ResourceRowUI[] resourceRowUis;

    //Arrays of resources
    private List<GameObject> costArray = new List<GameObject>();
    private List<GameObject> maintananceArray = new List<GameObject>();
    private List<GameObject> productionArray = new List<GameObject>();
    private List<GameObject> statArray = new List<GameObject>();
    private List<GameObject> housingSpotArray = new List<GameObject>();
    
    [Header("Prefabs")]
    public GameObject pnlSingleResource;
    public GameObject pnlPopulationSpot;
    
    //Colors
    private Color houseKept = new Color(50f / 255f, 255f / 255f, 50f / 255f);
    private Color houseAvaible = new Color(255f / 255f, 255f / 255f, 255f / 255f);
    
    public void Awake()
    {
        Instance = this;
        
        // Creating 5 empty resource for cost,maintanace and produtnio array.
        // 5 is max available resources to be used at the same time
        for (int i = 0; i < 5; i++)
        {
            costArray.Add(Instantiate(pnlSingleResource, pnlCostArray.transform));
            costArray[i].SetActive(false);
        }
        for (int i = 0; i < 5; i++)
        {
            maintananceArray.Add(Instantiate(pnlSingleResource, pnlMaintananceArray.transform));
            maintananceArray[i].SetActive(false);
        }
        for (int i = 0; i < 5; i++)
        {
            productionArray.Add(Instantiate(pnlSingleResource, pnlProductionArray.transform));
            productionArray[i].SetActive(false);
        }

        for (int i = 0; i < 10; i++)
        {
            housingSpotArray.Add(Instantiate(pnlPopulationSpot, pnlHousingSpots.transform));
            housingSpotArray[i].GetComponent<Image>().color = houseAvaible;
            housingSpotArray[i].SetActive(false);
        }
        
        HideTooltip();
    }
     
    
    public void ShowTooltip(BuildingData data)
    {
        HideTooltip();
        SetupCollectivePanel(data);
        pnlDescription.SetActive(true);
        switch (data.type)
        {
            case BuildingType.Economic:
                SetupProductionPanel(data);
                break;
            case BuildingType.Defense:
                TowerData towerData = data as TowerData;
                SetupTowerPanel(towerData);
                break;
            case BuildingType.Housing:
                HousingBuildingData buildingData = data as HousingBuildingData;
                SetupHousingPanel(buildingData);
                break;
            case BuildingType.Unique or BuildingType.Utility:
                Debug.Log("ło cholera co się stało");
                break;
        }
    }
    
    //Hidding all tooltip panels
    public void HideTooltip()
    {
        pnlCollective.SetActive(false);
        pnlProduction.SetActive(false);
        pnlTower.SetActive(false);
        pnlHousing.SetActive(false);
        pnlUtility.SetActive(false);
        pnlDescription.SetActive(false);
    }
    
    //Showing collective panel
    private void SetupCollectivePanel(BuildingData data)
    {
        foreach (var element in costArray) element.SetActive(false);
        foreach (var element in maintananceArray) element.SetActive(false);
        
        pnlCollective.SetActive(true);
        
        //Setting up cost
        int i = 0;
        foreach (var element in data.constructionCost)
        {
            
            costArray[i].SetActive(true);
            costArray[i].GetComponent<ResourceRowUI>().Setup(resourceIconDB.GetIcon(element.type),element.amount);
            
            i++;
        }

        //Setting up maintanance
        i = 0;
        foreach (var element in data.upkeepPerCycle)
        {
            maintananceArray[i].SetActive(true);
            maintananceArray[i].GetComponent<ResourceRowUI>().Setup(resourceIconDB.GetIcon(element.type),element.amount);
            i++;
        }
        
        //Setting up description
        string tmp = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Proin dictum commodo diam id efficitur. Fusce id vestibulum massa, sit amet convallis sem. Fusce et euismod lacus.";
        if (data.description == null) pnlDescription.GetComponentInChildren<TextMeshProUGUI>().text = tmp;
        else pnlDescription.GetComponentInChildren<TextMeshProUGUI>().text = data.description;

    }

    private void SetupProductionPanel(BuildingData data)
    {
        foreach (var element in productionArray) element.SetActive(false);
        pnlProduction.SetActive(true);
        
        int i = 0;
        foreach (var element in data.productionPerCycle)
        {
            productionArray[i].SetActive(true);
            productionArray[i].GetComponentInChildren<ResourceRowUI>().Setup(resourceIconDB.GetIcon(element.type),element.amount);
            i++;
        }
    }

    private void SetupTowerPanel(TowerData data)
    {
        pnlTower.SetActive(true);
        AttackStatUI[] statPanels = pnlStatArray.gameObject.GetComponentsInChildren<AttackStatUI>();
        
        foreach (AttackStatUI element in statPanels)
        {
            element.Setup(data);
        }
    }

    private void SetupHousingPanel(HousingBuildingData data)
    {
        foreach (var element in housingSpotArray) element.SetActive(false);
        foreach (var element in housingSpotArray) element.GetComponent<Image>().color = houseAvaible;
        int j = 0;
        for (int i = 0; i < data.maxResidents; i++)
        {
            housingSpotArray[i].SetActive(true);
            if (j < data.initialResidents)
            {
                housingSpotArray[j].GetComponent<Image>().color = houseKept;
                j++;
            }

        }
    
        pnlHousing.SetActive(true);
        resourceRowUis[0].Setup(resourceIconDB.GetIcon(data.productionPerResident[0].type),data.productionPerResident[0].amount);
        resourceRowUis[1].Setup(resourceIconDB.GetIcon(data.upkeepPerResident[0].type),data.upkeepPerResident[0].amount);

    }
    
    
}
