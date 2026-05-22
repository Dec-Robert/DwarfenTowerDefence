using System;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class BuildMenuUI : MonoBehaviour
{
    private class UIBuildingData 
    {
        public GameObject uiItem;
        public BuildingData data;
        public bool isAvailable;
        
        public UIBuildingData(GameObject uiItem, BuildingData data, bool isAvailable = true)
        {
            this.uiItem = uiItem;
            this.data = data;
            this.isAvailable = isAvailable;
        }
    }
    
    //Uniq == Utility
    
    public static BuildMenuUI Instance { get; private set; }

    [Header("Place to insert new buildings")]
    public GameObject content;
    
    [Header("List of all avaible buildings")]
    public List<BuildingData> allBuildingsDatabase = new List<BuildingData>();
    private List<UIBuildingData> uiBuildings = new List<UIBuildingData>(); //List of all bulding in menu
    
    
    [Header("Referencje do przycisków")]
    public Button btnBuildTower;
    public Button btnBuildProduction;
    public Button btnBuildHouses;
    public Button btnBuildUniq;

    [Header("Prefab of an building cell")]
    public GameObject buildingCell;
    

    
    private void Awake()
    {
        Instance = this;
        
        // Rejestrowanie akcji przycisków
        if(btnBuildTower != null) btnBuildTower.onClick.AddListener(() => OpenWindow(BuildingType.Defense));
        if(btnBuildProduction != null) btnBuildProduction.onClick.AddListener(() => OpenWindow(BuildingType.Economic));
        if(btnBuildHouses != null) btnBuildHouses.onClick.AddListener(() => OpenWindow(BuildingType.Housing));
        if(btnBuildUniq != null) btnBuildUniq.onClick.AddListener(() => OpenWindow(BuildingType.Unique));
    }

    private void Start()
    {
        foreach (var building in allBuildingsDatabase)
        {
            try
            {
                GameObject cell = Instantiate(buildingCell, content.transform);
                
                cell.GetComponentInChildren<TextMeshProUGUI>().text = building.buildingName;
                cell.transform.Find("Img_Icon_Background/Img_Icon_Foreground").GetComponent<Image>().sprite = building.icon;

                Button cellBtn = cell.GetComponent<Button>();
                BuildingData dataRef = building; 
                cellBtn.onClick.AddListener(() => InteractionManager.Instance?.SelectBuildingToBuild(dataRef));
                
                UIBuildingData data = new UIBuildingData(cell, building);
                uiBuildings.Add(data);
                
                cell.SetActive(false);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

        }
    }
    
    private void OnDestroy()
    {
        // Wyrejestrowanie akcji
        if(btnBuildTower != null) btnBuildTower.onClick.RemoveAllListeners();
        if(btnBuildProduction != null) btnBuildProduction.onClick.RemoveAllListeners();
        if(btnBuildHouses != null) btnBuildHouses.onClick.RemoveAllListeners();
        if(btnBuildUniq != null) btnBuildUniq.onClick.RemoveAllListeners();
    }

    private void OpenWindow(BuildingType buildingType)
    {
        foreach (var building in uiBuildings)
        {
            bool shouldBeVisible = (buildingType is BuildingType.Utility or BuildingType.Unique) 
                ? (building.data.type is BuildingType.Utility or BuildingType.Unique) 
                : (building.data.type == buildingType);
            
            building.uiItem.SetActive(shouldBeVisible);
        }
    }
        
    
}