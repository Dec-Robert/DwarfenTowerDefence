using System;
using UnityEngine;

public class CenterInfoPanelControllerUI : MonoBehaviour
{
    public static CenterInfoPanelControllerUI Instance { get; private set; }
    
    [Header("Panels")]
    public GameObject productionPanel;
    public GameObject housingPanel;
    public GameObject towerPanel;
    
    void Awake()
    {
        Instance = this;
        
        HideAll();
    }

    public void Setup(BuildingEntity buildingEntity)
    {
        HideAll();
        if (buildingEntity is TowerEntity towerEntity)
        {
                towerPanel.SetActive(true);
                towerPanel.GetComponent<TowerInfoPanelUI>().Setup(towerEntity );
            
        }
    }
    
    private void HideAll()
    {
        productionPanel.SetActive(false);
        housingPanel.SetActive(false);
        towerPanel.SetActive(false);
    }
}
