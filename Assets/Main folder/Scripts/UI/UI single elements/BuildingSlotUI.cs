using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class BuildingSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private BuildingData buildingData;

    [Header("For All")]
    [FormerlySerializedAs("name")]
    [SerializeField] private TextMeshProUGUI buildingNameText;
    [SerializeField] private Image icon;
    [FormerlySerializedAs("description")]
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private GameObject resourceSlotPrefab;
    [SerializeField] private Transform costContainer;
    
    [Header("Panels")]
    [SerializeField] private GameObject battleStatsPanel;
    [SerializeField] private GameObject housingStatsPanel;
    [SerializeField] private GameObject productionStatsPanel;
    
    [Header("Tower")]
    [SerializeField] private TextMeshProUGUI dmgText;
    [SerializeField] private TextMeshProUGUI attackSpeedText;

    [Header("Production")]
    [SerializeField] private TextMeshProUGUI productionInfoPanel;
    [SerializeField] private Transform productionResourcesContainer;
    
    [Header("Housing")]
    [SerializeField] private TextMeshProUGUI housingStartingState;
    [SerializeField] private TextMeshProUGUI cyclesToGrowth;

    [Header("Interaction")]
    [SerializeField] private Button selectButton;

    private void Awake()
    {
        if (selectButton == null)
        {
            selectButton = GetComponent<Button>();
        }

        if (selectButton != null)
        {
            selectButton.onClick.AddListener(OnSlotClicked);
        }
    }
    private void OnEnable()
    {
        StartCoroutine(DelayedLayoutRefresh());
    }

    private void OnDisable()
    {
        if (Tooltip_BuildingExtraInfo.Instance != null)
        {
            Tooltip_BuildingExtraInfo.Instance.HideTooltip();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Tooltip_BuildingExtraInfo.Instance != null && buildingData != null)
        {
            Tooltip_BuildingExtraInfo.Instance.ShowTooltip(buildingData);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (Tooltip_BuildingExtraInfo.Instance != null)
        {
            Tooltip_BuildingExtraInfo.Instance.HideTooltip();
        }
    }

    private void OnSlotClicked()
    {
        if (Tooltip_BuildingExtraInfo.Instance != null)
        {
            Tooltip_BuildingExtraInfo.Instance.HideTooltip();
        }

        if (buildingData != null)
        {
            InteractionManager.Instance?.SelectBuildingToBuild(buildingData);
        }
    }

    public void SetUpPanel(BuildingData data)
    {
        buildingData = data;
        if (buildingData == null) return;
        
        // 1. Nazwa, ikona, opis
        if (buildingNameText != null)
        {
            buildingNameText.text = buildingData.buildingName;
        }

        if (icon != null)
        {
            if (buildingData.icon != null)
            {
                icon.sprite = buildingData.icon;
                icon.gameObject.SetActive(true);
            }
            else
            {
                icon.gameObject.SetActive(false);
            }
        }

        if (descriptionText != null)
        {
            descriptionText.text = string.IsNullOrEmpty(buildingData.description) ? "" : buildingData.description;
        }
        
        SetUpCosts();
        
        if (battleStatsPanel != null) battleStatsPanel.SetActive(false);
        if (housingStatsPanel != null) housingStatsPanel.SetActive(false);
        if (productionStatsPanel != null) productionStatsPanel.SetActive(false);

        switch (buildingData.type)
        {
            case BuildingType.Defense:
                if (battleStatsPanel != null) battleStatsPanel.SetActive(true);
                SetUpBattle();
                break;
            
            case BuildingType.Economic: 
                if (productionStatsPanel != null) productionStatsPanel.SetActive(true);
                SetUpProduction();
                break;
            
            case BuildingType.Housing:
                if (housingStatsPanel != null) housingStatsPanel.SetActive(true);
                SetUpHousing();
                break;
        }
    }

    private void SetUpCosts()
    {
        if (costContainer == null) return;

        ClearContainer(costContainer);

        if (buildingData.constructionCost != null && resourceSlotPrefab != null)
        {
            foreach (var cost in buildingData.constructionCost)
            {
                GameObject slot = Instantiate(resourceSlotPrefab, costContainer);
                slot.SetActive(true);
                SingleResourceUI resourceUI = slot.GetComponent<SingleResourceUI>();
                if (resourceUI != null)
                {
                    resourceUI.Setup(cost.type, cost.amount);
                }
            }
        }
    }

    private void SetUpBattle()
    {
        if (buildingData is TowerData towerData)
        {
            if (dmgText != null)
            {
                string damageTypeName;
                string colorHex;

                switch (towerData.damageType)
                {
                    case DamageType.Physical:
                        damageTypeName = "Fizyczne";
                        colorHex = "#F54727"; 
                        break;
                    case DamageType.Magic:
                        damageTypeName = "Magiczne";
                        colorHex = "#3B82F6"; 
                        break;
                    case DamageType.True:
                        damageTypeName = "Czyste";
                        colorHex = "#FFFFFF";
                        break;
                    default:
                        damageTypeName = towerData.damageType.ToString();
                        colorHex = "#FFFFFF";
                        break;
                }

                dmgText.text = $"Obrażenia: {towerData.baseDamage} (<color={colorHex}>{damageTypeName}</color>)";
            }

            if (attackSpeedText != null)
            {
                attackSpeedText.text = $"Prędkość ataku: {towerData.fireRate:F1}";
            }
        }
        else
        {
            if (dmgText != null) dmgText.text = "Obrażenia: -";
            if (attackSpeedText != null) attackSpeedText.text = "Prędkość ataku: -";
        }
    }

    private void SetUpProduction()
    {
        if (productionInfoPanel != null)
        {
            productionInfoPanel.text = "Produkcja bazowa";
        }

        if (productionResourcesContainer != null)
        {
            ClearContainer(productionResourcesContainer);

            if (buildingData.productionPerCycle != null && resourceSlotPrefab != null)
            {
                foreach (var prod in buildingData.productionPerCycle)
                {
                    GameObject slot = Instantiate(resourceSlotPrefab, productionResourcesContainer);
                    slot.SetActive(true);
                    SingleResourceUI resourceUI = slot.GetComponent<SingleResourceUI>();
                    if (resourceUI != null)
                    {
                        resourceUI.Setup(prod.type, prod.amount);
                    }
                }
            }
        }
    }

    private void SetUpHousing()
    {
        if (buildingData is HousingBuildingData housingData)
        {
            if (housingStartingState != null)
            {
                housingStartingState.text = $"Populacja: {housingData.initialResidents}/{housingData.maxResidents}";
            }

            if (cyclesToGrowth != null)
            {
                cyclesToGrowth.text = $"Prędkość rozwoju: {housingData.baseGrowthTicks}";
            }
        }
        else
        {
            if (housingStartingState != null) housingStartingState.text = "Populacja: -";
            if (cyclesToGrowth != null) cyclesToGrowth.text = "Prędkość rozwoju: -";
        }
    }

    private void ClearContainer(Transform container)
    {
        if (container == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }
    
    private System.Collections.IEnumerator DelayedLayoutRefresh()
    {
        yield return null;
        RefreshLayout();
    }
    
    public void RefreshLayout()
    {
        if (!gameObject.activeInHierarchy) return;
        if (costContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(costContainer as RectTransform);
        }
        if (productionResourcesContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(productionResourcesContainer as RectTransform);
        }
        if (battleStatsPanel != null && battleStatsPanel.activeSelf)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(battleStatsPanel.transform as RectTransform);
        }
        if (productionStatsPanel != null && productionStatsPanel.activeSelf)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(productionStatsPanel.transform as RectTransform);
        }
        if (housingStatsPanel != null && housingStatsPanel.activeSelf)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(housingStatsPanel.transform as RectTransform);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }
}
