using System.Linq;
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
    
    [Header("Panels")]
    [SerializeField] private GameObject battleStatsPanel;
    [SerializeField] private GameObject housingStatsPanel;
    [SerializeField] private GameObject productionStatsPanel;
    [SerializeField] private GameObject descriptionPanel;
    [SerializeField] private GameObject costStatsPanel;
    
    [Header("Cost")]
    [SerializeField] private TextMeshProUGUI costInfoText;
    [SerializeField] private Transform costContainer;
    
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

    [Header("Affordability Visuals")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float disabledAlpha = 0.6f;
    [SerializeField] private Color costAvailableColor = new Color(0.63f, 0.63f, 0.63f, 1f); // #A0A0A0 szary
    [SerializeField] private Color costMissingColor = new Color(0.94f, 0.33f, 0.31f, 1f);   // #EF5350 czerwony

    private class CostSlotEntry
    {
        public SingleResourceUI ui;
        public ResourceType type;
        public int amount;
    }

    private readonly List<CostSlotEntry> costEntries = new List<CostSlotEntry>();
    private bool isSubscribedToResource;

    public BuildingData BuildingData => buildingData;
    public GameObject BattleStatsPanel => battleStatsPanel;
    public GameObject HousingStatsPanel => housingStatsPanel;
    public GameObject ProductionStatsPanel => productionStatsPanel;
    public GameObject DescriptionPanel => descriptionPanel;
    public GameObject CostStatsPanel => costStatsPanel;
    public Transform CostContainer => costContainer;
    public TextMeshProUGUI CostInfoText => costInfoText;

    private void Awake()
    {
        AutoBindFields();

        if (selectButton == null)
        {
            selectButton = GetComponent<Button>();
        }

        if (selectButton != null)
        {
            selectButton.onClick.AddListener(OnSlotClicked);
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void OnValidate()
    {
        AutoBindFields();
    }

    private void AutoBindFields()
    {
        // Name & Icon
        if (buildingNameText == null)
        {
            var t = transform.Find("Pnl_NameIcon/Txt_Name");
            if (t != null) buildingNameText = t.GetComponent<TextMeshProUGUI>();
        }
        if (icon == null)
        {
            var t = transform.Find("Pnl_NameIcon/Img_Icon");
            if (t != null) icon = t.GetComponent<Image>();
        }

        // Description
        if (descriptionPanel == null)
        {
            var t = transform.Find("Pnl_Description");
            if (t != null) descriptionPanel = t.gameObject;
        }
        if (descriptionText == null && descriptionPanel != null)
        {
            descriptionText = descriptionPanel.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        // Battle stats
        if (battleStatsPanel == null)
        {
            var t = transform.Find("Pnl_BattleStats");
            if (t != null) battleStatsPanel = t.gameObject;
        }
        if (battleStatsPanel != null)
        {
            if (dmgText == null)
            {
                var t = battleStatsPanel.transform.Find("Txt_BaseDmg");
                if (t != null) dmgText = t.GetComponent<TextMeshProUGUI>();
            }
            if (attackSpeedText == null)
            {
                var t = battleStatsPanel.transform.Find("Txt_BaseAttackSpeed");
                if (t != null) attackSpeedText = t.GetComponent<TextMeshProUGUI>();
            }
        }

        // Housing stats
        if (housingStatsPanel == null)
        {
            var t = transform.Find("Pnl_HousingStats");
            if (t != null) housingStatsPanel = t.gameObject;
        }
        if (housingStatsPanel != null)
        {
            if (housingStartingState == null)
            {
                var t = housingStatsPanel.transform.Find("Txt_StartingPop");
                if (t != null) housingStartingState = t.GetComponent<TextMeshProUGUI>();
            }
            if (cyclesToGrowth == null)
            {
                var t = housingStatsPanel.transform.Find("Txt_GrowthRate");
                if (t != null) cyclesToGrowth = t.GetComponent<TextMeshProUGUI>();
            }
        }

        // Production stats
        if (productionStatsPanel == null)
        {
            var t = transform.Find("Pnl_ProductionStats");
            if (t != null) productionStatsPanel = t.gameObject;
        }
        if (productionStatsPanel != null)
        {
            if (productionInfoPanel == null)
            {
                var t = productionStatsPanel.transform.Find("Txt_ProductionText");
                if (t != null) productionInfoPanel = t.GetComponent<TextMeshProUGUI>();
            }
            if (productionResourcesContainer == null)
            {
                var t = productionStatsPanel.transform.Find("Pnl_Production");
                if (t != null) productionResourcesContainer = t;
            }
        }

        // Cost panel
        if (costStatsPanel == null)
        {
            var t = transform.Find("Pnl_BuildingCost");
            if (t != null) costStatsPanel = t.gameObject;
        }
        if (costStatsPanel != null)
        {
            if (costInfoText == null)
            {
                var t = costStatsPanel.transform.Find("Txt_ProductionText");
                if (t != null) costInfoText = t.GetComponent<TextMeshProUGUI>();
            }
            // If costContainer is null or accidentally points to the cost panel itself, point to child Pnl_Production
            if (costContainer == null || costContainer == costStatsPanel.transform)
            {
                var t = costStatsPanel.transform.Find("Pnl_Production");
                if (t != null) costContainer = t;
            }
        }

        // Interaction & CanvasGroup
        if (selectButton == null) selectButton = GetComponent<Button>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        SubscribeToResourceManager();
        UpdateAffordability();
        LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
        StartCoroutine(DelayedLayoutRefresh());
    }

    private void HandleLanguageChanged()
    {
        if (buildingData != null)
        {
            SetUpPanel(buildingData);
        }
        else if (costInfoText != null)
        {
            costInfoText.text = LocalizationManager.Get("UI_TOOLTIP_COST", "Koszt budowy:");
        }
    }

    private void Start()
    {
        SubscribeToResourceManager();
        UpdateAffordability();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
        UnsubscribeFromResourceManager();
        if (Tooltip_BuildingExtraInfo.Instance != null)
        {
            Tooltip_BuildingExtraInfo.Instance.HideTooltip();
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromResourceManager();
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(OnSlotClicked);
        }
    }

    private void SubscribeToResourceManager()
    {
        if (isSubscribedToResource || ResourceManager.Instance == null) return;
        ResourceManager.Instance.OnResourceChanged += HandleResourceChanged;
        isSubscribedToResource = true;
    }

    private void UnsubscribeFromResourceManager()
    {
        if (!isSubscribedToResource) return;
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged -= HandleResourceChanged;
        }
        isSubscribedToResource = false;
    }

    private void HandleResourceChanged(ResourceType type, int amount)
    {
        UpdateAffordability();
    }

    public bool CanAfford()
    {
        if (buildingData == null || buildingData.constructionCost == null || buildingData.constructionCost.Count == 0)
            return true;

        if (ResourceManager.Instance == null)
            return true;

        foreach (var cost in buildingData.constructionCost)
        {
            if (cost.amount > 0 && !ResourceManager.Instance.CanAfford(cost.type, cost.amount))
                return false;
        }

        return true;
    }

    public void UpdateAffordability()
    {
        if (buildingData == null) return;

        bool canAffordAll = true;

        if (costEntries.Count > 0)
        {
            foreach (var entry in costEntries)
            {
                bool canAffordThis = entry.amount <= 0 || (ResourceManager.Instance != null && ResourceManager.Instance.CanAfford(entry.type, entry.amount));
                if (!canAffordThis)
                {
                    canAffordAll = false;
                }

                if (entry.ui != null)
                {
                    entry.ui.SetTextColor(canAffordThis ? costAvailableColor : costMissingColor);
                }
            }
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null && gameObject != null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = canAffordAll ? 1.0f : disabledAlpha;
            canvasGroup.blocksRaycasts = true;
        }

        if (selectButton != null)
        {
            selectButton.interactable = canAffordAll;
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
        if (!CanAfford()) return;

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
            buildingNameText.text = buildingData.DisplayName;
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
        
        
        SetUpCosts();
        
        if (battleStatsPanel != null) battleStatsPanel.SetActive(false);
        if (housingStatsPanel != null) housingStatsPanel.SetActive(false);
        if (productionStatsPanel != null) productionStatsPanel.SetActive(false);
        if (descriptionPanel != null) descriptionPanel.SetActive(false);

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
            case BuildingType.Utility or BuildingType.Unique:
                if (descriptionPanel != null) descriptionPanel.SetActive(true);
                if (descriptionText != null)
                {
                    descriptionText.text = string.IsNullOrEmpty(buildingData.description) ? "" : buildingData.DisplayDescription;
                }
                break;
        }
    }

    private void SetUpCosts()
    {
        if (costInfoText != null)
        {
            costInfoText.text = LocalizationManager.Get("UI_TOOLTIP_COST", "Koszt budowy:");
        }

        bool hasCosts = buildingData.constructionCost != null && buildingData.constructionCost.Any(c => c.amount > 0);

        if (costStatsPanel != null)
        {
            costStatsPanel.SetActive(hasCosts);
        }

        if (costContainer == null) return;

        ClearContainer(costContainer);
        costEntries.Clear();

        if (hasCosts && resourceSlotPrefab != null)
        {
            var sortedCosts = buildingData.constructionCost
                .Where(c => c.amount > 0)
                .OrderBy(c => ResourceCost.GetPriority(c.type));

            foreach (var cost in sortedCosts)
            {
                GameObject slot = Instantiate(resourceSlotPrefab, costContainer);
                slot.SetActive(true);
                SingleResourceUI resourceUI = slot.GetComponent<SingleResourceUI>();
                if (resourceUI != null)
                {
                    resourceUI.Setup(cost.type, cost.amount);
                    costEntries.Add(new CostSlotEntry { ui = resourceUI, type = cost.type, amount = cost.amount });
                }
            }
        }

        UpdateAffordability();
    }

    private void SetUpBattle()
    {
        if (buildingData is TowerData towerData)
        {
            if (dmgText != null)
            {
                string colorHex;

                switch (towerData.damageType)
                {
                    case DamageType.Physical:
                        colorHex = "#FF8C34"; 
                        break;
                    case DamageType.Magic:
                        colorHex = "#00B0F0"; 
                        break;
                    case DamageType.True:
                        colorHex = "#FFFFFF";
                        break;
                    default:
                        colorHex = "#FFFFFF";
                        break;
                }

                dmgText.text = $"{LocalizationManager.Get("stat_damage_colon", "Obrażenia:")} <color={colorHex}>{towerData.baseDamage}</color>";
            }

            if (attackSpeedText != null)
            {
                attackSpeedText.text = $"{LocalizationManager.Get("stat_attackspeed_colon", "Prędkość ataku:")} {towerData.fireRate:F1}";
            }
        }
        else
        {
            if (dmgText != null) dmgText.text = $"{LocalizationManager.Get("stat_damage_colon", "Obrażenia:")} -";
            if (attackSpeedText != null) attackSpeedText.text = $"{LocalizationManager.Get("stat_attackspeed_colon", "Prędkość ataku:")} -";
        }
    }

    private void SetUpProduction()
    {
        if (productionInfoPanel != null)
        {
            productionInfoPanel.text = LocalizationManager.Get("UI_PRODUCTION_BASE", "Produkcja bazowa");
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
                housingStartingState.text = LocalizationManager.GetFormat("UI_SLOT_POP_FMT", housingData.initialResidents, housingData.maxResidents);
            }

            if (cyclesToGrowth != null)
            {
                cyclesToGrowth.text = $"{LocalizationManager.Get("UI_SLOT_GROWTH_SPEED", "Prędkość rozwoju:")} {housingData.GetGrowthCyclesRequired(housingData.initialResidents)}";
            }
        }
        else
        {
            if (housingStartingState != null) housingStartingState.text = $"{LocalizationManager.Get("tooltip_growth_none", "Populacja: -")}";
            if (cyclesToGrowth != null) cyclesToGrowth.text = $"{LocalizationManager.Get("UI_SLOT_GROWTH_SPEED", "Prędkość rozwoju:")} -";
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
        if (costStatsPanel != null && costStatsPanel.activeSelf)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(costStatsPanel.transform as RectTransform);
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
        if (descriptionPanel != null && descriptionPanel.activeSelf)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(descriptionPanel.transform as RectTransform);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }
}
