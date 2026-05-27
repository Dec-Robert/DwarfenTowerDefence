using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using DG.Tweening;
using Unity.VisualScripting;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

//Uniq == Utility
public class BuildMenuUI : MonoBehaviour
{
    public static BuildMenuUI Instance { get; private set; }
    
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
    
    [Header("Build menu panel information")]
    public GameObject buildingMenu;
    public int buildingMenuHiddenPositionY;
    public int buildingMenuShowedPositionY;
    
    [Header("Tooltip information")]
    public GameObject buildingExtraInfo;

    public GameObject resourcePanelPrefab;
    
    [Header("Arrays storing UIResource Panels")] 
        [Header("For all")]
        public GameObject costArrayPanel;
        private List<GameObject> costArray = new List<GameObject>();
        
        public GameObject maintananceArrayPanel;
        private List<GameObject> maintananceArray = new List<GameObject>();
        
        [Header("For production")]
        public GameObject productionArrayPanel;
        private List<GameObject> productionArray = new List<GameObject>();
    
    private void Awake()
    {
        Instance = this;
        
        // Rejestrowanie akcji przycisków
        if(btnBuildTower != null) btnBuildTower.onClick.AddListener(() => OpenWindow(BuildingType.Defense));
        if(btnBuildProduction != null) btnBuildProduction.onClick.AddListener(() => OpenWindow(BuildingType.Economic));
        if(btnBuildHouses != null) btnBuildHouses.onClick.AddListener(() => OpenWindow(BuildingType.Housing));
        if(btnBuildUniq != null) btnBuildUniq.onClick.AddListener(() => OpenWindow(BuildingType.Unique));

        
        //i = 5 couse thats MAX resource for production/consumption/building
        //Inst
        for (int i = 0; i < 5; i++)
        {
            GameObject obj = Instantiate(resourcePanelPrefab,costArrayPanel.transform);
            costArray.Add(obj);
            obj.SetActive(false);
        }

        for (int i = 0; i < 5; i++)
        {
            GameObject obj = Instantiate(resourcePanelPrefab,maintananceArrayPanel.transform);
            maintananceArray.Add(obj);
            obj.SetActive(false);
        }
        
        for (int i = 0; i < 5; i++) {
            GameObject obj = Instantiate(resourcePanelPrefab, productionArrayPanel.transform);
            productionArray.Add(obj);
            obj.SetActive(false);
        }
    

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

                BuildingCellUI cellScript = cell.GetComponent<BuildingCellUI>();
                cellScript.Setup(building);
                
                cell.SetActive(false);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

        }
    }
    
    //TODO: po debugu usunać
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F10)) HideWindow();
        if (Input.GetKeyDown(KeyCode.F11)) OpenExtraInfo();
        if (Input.GetKeyDown(KeyCode.F12)) HideExtraInfo();

        
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
        
        if (!Mathf.Approximately(buildingMenu.transform.position.y, buildingMenuShowedPositionY))
        {
            buildingMenu.transform.DOKill();
            buildingMenu.transform.DOMoveY(buildingMenuShowedPositionY, 0.2f).SetUpdate(true);
        }
    }

    private void HideWindow()
    {
        buildingMenu.transform.DOKill();
        buildingMenu.transform.DOMoveY(buildingMenuHiddenPositionY, 0.2f).SetUpdate(true);
    }

    //Functions called from single UI cell that will have onHover
    //TODO: Zmienić później = null
    public void OpenExtraInfo(BuildingData data= null)
    {
        CanvasGroup canvasGroup = buildingExtraInfo.GetComponent<CanvasGroup>();
        
        canvasGroup.DOKill();
        buildingExtraInfo.SetActive(true);
        
        canvasGroup.DOKill();
        canvasGroup.DOFade(1f, 0.2f).SetUpdate(true); 
        
        SetupExtraInfo(data);
    }
    public void HideExtraInfo()
    {
        CanvasGroup canvasGroup = buildingExtraInfo.GetComponent<CanvasGroup>();
        canvasGroup.DOKill();
        canvasGroup.DOFade(0f, 0.2f).SetUpdate(true).OnComplete(() => 
        {
            buildingExtraInfo.SetActive(false);
        });
        foreach (var variable in costArray) variable.SetActive(false);
        foreach (var variable in productionArray) variable.SetActive(false);
        foreach (var variable in maintananceArray) variable.SetActive(false);

    }

    public void SetupExtraInfo(BuildingData data)
    {
        //Setup for costs
        Dictionary<ResourceType, float> resourceCosts = data.GetCostDictionary();

        int i = 0;
        foreach (var resourceCost in resourceCosts)
        {
            ResourceRowUI resourceRowUI = costArray[i].GetComponent<ResourceRowUI>();
            resourceRowUI.Setup(resourceCost.Key,resourceCost.Value);
            costArray[i].SetActive(true);
            
            i++;
        }
        i = 0;
        //TODO: uzupełnić resztę surowców

    }


}