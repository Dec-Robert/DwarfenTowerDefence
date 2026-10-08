using System;
using UnityEngine;

public class CenterInfoPanelControllerUI : MonoBehaviour
{
    private static CenterInfoPanelControllerUI _instance;
    public static CenterInfoPanelControllerUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CenterInfoPanelControllerUI>();
            }
            return _instance;
        }
        private set => _instance = value;
    }
    
    [Header("Panels")]
    public GameObject productionPanel;
    public GameObject housingPanel;
    public GameObject towerPanel;
    
    void Awake()
    {
        _instance = this;

        if (towerPanel == null)
        {
            var pnl = transform.Find("Pnl_TowerContext");
            if (pnl != null) towerPanel = pnl.gameObject;
        }

        HideAll();
    }

    public void Setup(BuildingEntity buildingEntity)
    {
        HideAll();
        if (buildingEntity == null) return;

        if (buildingEntity is TowerEntity towerEntity)
        {
            if (towerPanel != null)
            {
                towerPanel.SetActive(true);
                var newContextUI = towerPanel.GetComponent<TowerContextWindowUI>();
                if (newContextUI != null)
                {
                    newContextUI.Setup(towerEntity);
                }
                else
                {
                    towerPanel.GetComponent<TowerInfoPanelUI>()?.Setup(towerEntity);
                }
            }
        }

        if (buildingEntity.data != null && buildingEntity.data.type == BuildingType.Economic)
        {
            if (productionPanel != null)
            {
                productionPanel.SetActive(true);
                productionPanel.GetComponent<ProductionInfoPanelUI>()?.Setup(buildingEntity);
            }
        }
    }
    
    public void HideAll()
    {
        if (productionPanel != null) productionPanel.SetActive(false);
        if (housingPanel != null) housingPanel.SetActive(false);
        if (towerPanel != null) towerPanel.SetActive(false);
    }
}
