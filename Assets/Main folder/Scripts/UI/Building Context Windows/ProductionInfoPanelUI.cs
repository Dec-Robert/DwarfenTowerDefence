using System;
using UnityEngine.UI;
using UnityEngine;

public class ProductionInfoPanelUI : MonoBehaviour
{
    public GameObject shiftPanels;
    
    
    public GameObject singleShiftPanelPrefab;
    
    private BuildingEntity currentBuilding;
    
    private void Awake()
    {
        Initialize();
    }

    

    private void Initialize()
    {
        //Initialize shift info
        for (int i = 0; i < 5; i++)
        {
            GameObject gameObject = Instantiate(singleShiftPanelPrefab, shiftPanels.transform);
            gameObject.GetComponent<ShiftInfoPanelUI>().Initialize(i+1);
            gameObject.SetActive(false);
        }
        
        Canvas.ForceUpdateCanvases(); 
        LayoutRebuilder.ForceRebuildLayoutImmediate(this.gameObject.GetComponent<RectTransform>());
        


    }

    public void Setup(BuildingEntity _entity)
    {
        currentBuilding = _entity;
        hideALLShiftPanels();

        for (int i = 0; i < currentBuilding.getMaxShifts(); i++)
        {
            GameObject panel = shiftPanels.transform.GetChild(i).gameObject;
            panel.SetActive(true);
            panel.GetComponent<ShiftInfoPanelUI>().Setup(currentBuilding.GetWorkersFromShift(i),currentBuilding.getMaxWorkersPerShift());
        }
        Canvas.ForceUpdateCanvases(); 
        LayoutRebuilder.ForceRebuildLayoutImmediate(shiftPanels.GetComponent<RectTransform>());
       
    }

    private void hideALLShiftPanels()
    {
        foreach (Transform panel in shiftPanels.gameObject.transform)
        {
            panel.gameObject.SetActive(false);
        }
    }
    
}
