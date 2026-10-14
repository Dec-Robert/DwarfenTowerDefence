using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Tooltip_BuildingExtraInfo : MonoBehaviour
{
    public static Tooltip_BuildingExtraInfo Instance { get; private set; }

    [Header("Root & Canvas")]
    [SerializeField] private GameObject tooltipRoot;
    [SerializeField] private RectTransform tooltipRect;
    [SerializeField] private Canvas parentCanvas;

    [Header("Shared Section")]
    [SerializeField] private GameObject pnlShared;
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private TextMeshProUGUI txtDescription;

    [Header("Building Cost Section")]
    [SerializeField] private GameObject pnlBuildingCost;
    [SerializeField] private Transform costContainer;

    [Header("Upkeep Section")]
    [SerializeField] private GameObject pnlUpkeep;
    [SerializeField] private Transform upkeepContainer;

    [Header("Towers / Combat Section")]
    [SerializeField] private GameObject pnlTowers;
    [SerializeField] private Transform statsContainer;

    [Header("Production Section")]
    [SerializeField] private GameObject pnlProduction;
    [SerializeField] private Transform productionContainer;
    [SerializeField] private TextMeshProUGUI txtBonusForPlacing;
    [SerializeField] private TextMeshProUGUI txtBonusForSurround;

    [Header("Housing Section")]
    [SerializeField] private GameObject pnlHousing;
    [SerializeField] private TextMeshProUGUI txtPop;
    [SerializeField] private TextMeshProUGUI txtGrowthCycles;
    [SerializeField] private TextMeshProUGUI txtToSurvive;
    [SerializeField] private TextMeshProUGUI txtToGrow;

    [Header("Special / Unique Section")]
    [SerializeField] private GameObject pnlSpecial;
    [SerializeField] private TextMeshProUGUI txtSpecial;
    [SerializeField] private TextMeshProUGUI txtSpecialDesc;

    [Header("Prefabs & Databases")]
    [SerializeField] private GameObject pnlSingleResourcePrefab;
    [SerializeField] private GameObject pnlSingleStatPrefab;
    [SerializeField] private ResourceVisualsSO resourceVisualsDB;

    [Header("Pooling Capacities")]
    [SerializeField] private int maxCostSlots = 5;
    [SerializeField] private int maxUpkeepSlots = 5;
    [SerializeField] private int maxProductionSlots = 5;
    [SerializeField] private int maxStatSlots = 8;

    [Header("Cursor Following")]
    [SerializeField] private bool followCursor = true;
    [SerializeField] private Vector2 cursorOffset = new Vector2(25f, -25f);
    [SerializeField] private Vector2 padding = new Vector2(15f, 15f);

    // Object Pools
    private readonly List<SingleResourceUI> costSlots = new();
    private readonly List<SingleResourceUI> upkeepSlots = new();
    private readonly List<SingleResourceUI> productionSlots = new();
    private readonly List<SingleStatUI> statSlots = new();

    private bool isVisible = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (tooltipRoot == null) tooltipRoot = gameObject;
        if (tooltipRect == null) tooltipRect = GetComponent<RectTransform>();
        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();

        AutoFindReferences();
        InitializePools();
        HideTooltip();
    }

    private void Update()
    {
        if (isVisible && followCursor)
        {
            UpdateTooltipPosition();
        }
    }

    private void AutoFindReferences()
    {
        // Auto-assign references if not set in Inspector
        if (pnlShared == null) pnlShared = transform.Find("Pnl_Shared")?.gameObject;
        if (pnlShared != null)
        {
            if (txtName == null) txtName = pnlShared.transform.Find("Txt_Name")?.GetComponent<TextMeshProUGUI>();
            if (txtDescription == null) txtDescription = pnlShared.transform.Find("Txt_Description")?.GetComponent<TextMeshProUGUI>();
        }

        if (pnlBuildingCost == null) pnlBuildingCost = transform.Find("Pnl_BuildingCost")?.gameObject;
        if (costContainer == null && pnlBuildingCost != null) costContainer = pnlBuildingCost.transform.Find("Pnl_BuildingRequired");

        if (pnlUpkeep == null) pnlUpkeep = transform.Find("Pnl_Upkeep")?.gameObject;
        if (upkeepContainer == null && pnlUpkeep != null) upkeepContainer = pnlUpkeep.transform.Find("Pnl_RequiredUpkeep");

        if (pnlTowers == null) pnlTowers = transform.Find("Pnl_Towers")?.gameObject;
        if (statsContainer == null && pnlTowers != null) statsContainer = pnlTowers.transform.Find("Pnl_Stats");

        if (pnlProduction == null) pnlProduction = transform.Find("Pnl_Production")?.gameObject;
        if (pnlProduction != null)
        {
            Transform baseProd = pnlProduction.transform.Find("Pnl_BaseProduction");
            if (productionContainer == null && baseProd != null) productionContainer = baseProd.Find("Pnl_BaseProduced");

            Transform bonuses = pnlProduction.transform.Find("Pnl_Bonuses");
            if (bonuses != null)
            {
                if (txtBonusForPlacing == null) txtBonusForPlacing = bonuses.Find("Txt_BonusForPlacing")?.GetComponent<TextMeshProUGUI>();
                if (txtBonusForSurround == null) txtBonusForSurround = bonuses.Find("Txt_BonusForSurround")?.GetComponent<TextMeshProUGUI>();
            }
        }

        if (pnlHousing == null) pnlHousing = transform.Find("Pnl_Housing")?.gameObject;
        if (pnlHousing != null)
        {
            Transform info = pnlHousing.transform.Find("Pnl_HousingInfo");
            if (info != null)
            {
                if (txtPop == null) txtPop = info.Find("Txt_Pop")?.GetComponent<TextMeshProUGUI>();
                if (txtGrowthCycles == null) txtGrowthCycles = info.Find("Txt_GrowthCycles")?.GetComponent<TextMeshProUGUI>();
            }

            Transform upkeep = pnlHousing.transform.Find("Pnl_PopUpkeep");
            if (upkeep != null)
            {
                if (txtToSurvive == null) txtToSurvive = upkeep.Find("Txt_ToSurvive")?.GetComponent<TextMeshProUGUI>();
                if (txtToGrow == null) txtToGrow = upkeep.Find("Txt_ToGrow")?.GetComponent<TextMeshProUGUI>();
            }
        }

        if (pnlSpecial == null) pnlSpecial = transform.Find("Pnl_Special")?.gameObject;
        if (pnlSpecial != null)
        {
            if (txtSpecial == null) txtSpecial = pnlSpecial.transform.Find("Txt_Special")?.GetComponent<TextMeshProUGUI>();
            if (txtSpecialDesc == null) txtSpecialDesc = pnlSpecial.transform.Find("Txt_SpecialDesc")?.GetComponent<TextMeshProUGUI>();
        }
    }

    private void InitializePools()
    {
        // 1. Clear existing placeholder/mockup items from scene containers
        ClearContainer(costContainer);
        ClearContainer(upkeepContainer);
        ClearContainer(productionContainer);
        ClearContainer(statsContainer);

        // 2. Generate clean pools from prefabs
        costSlots.Clear();
        if (pnlSingleResourcePrefab != null && costContainer != null)
        {
            for (int i = 0; i < maxCostSlots; i++)
            {
                GameObject item = Instantiate(pnlSingleResourcePrefab, costContainer);
                item.SetActive(false);
                SingleResourceUI resUI = item.GetComponent<SingleResourceUI>();
                if (resUI != null) costSlots.Add(resUI);
            }
        }

        upkeepSlots.Clear();
        if (pnlSingleResourcePrefab != null && upkeepContainer != null)
        {
            for (int i = 0; i < maxUpkeepSlots; i++)
            {
                GameObject item = Instantiate(pnlSingleResourcePrefab, upkeepContainer);
                item.SetActive(false);
                SingleResourceUI resUI = item.GetComponent<SingleResourceUI>();
                if (resUI != null) upkeepSlots.Add(resUI);
            }
        }

        productionSlots.Clear();
        if (pnlSingleResourcePrefab != null && productionContainer != null)
        {
            for (int i = 0; i < maxProductionSlots; i++)
            {
                GameObject item = Instantiate(pnlSingleResourcePrefab, productionContainer);
                item.SetActive(false);
                SingleResourceUI resUI = item.GetComponent<SingleResourceUI>();
                if (resUI != null) productionSlots.Add(resUI);
            }
        }

        statSlots.Clear();
        if (pnlSingleStatPrefab != null && statsContainer != null)
        {
            for (int i = 0; i < maxStatSlots; i++)
            {
                GameObject item = Instantiate(pnlSingleStatPrefab, statsContainer);
                item.SetActive(false);
                SingleStatUI statUI = item.GetComponent<SingleStatUI>();
                if (statUI == null) statUI = item.AddComponent<SingleStatUI>();
                statSlots.Add(statUI);
            }
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

    public void ShowTooltip(BuildingData data)
    {
        if (data == null)
        {
            HideTooltip();
            return;
        }

        if (tooltipRoot != null) tooltipRoot.SetActive(true);
        isVisible = true;

        // Shared Info
        if (txtName != null) txtName.text = data.buildingName;
        if (txtDescription != null) txtDescription.text = string.IsNullOrEmpty(data.description) ? "" : data.description;

        // Building Cost
        SetupResourcePool(costSlots, data.constructionCost);

        // Upkeep
        bool hasUpkeep = data.upkeepPerCycle != null && data.upkeepPerCycle.Count > 0;
        if (pnlUpkeep != null) pnlUpkeep.SetActive(hasUpkeep);
        if (hasUpkeep)
        {
            SetupResourcePool(upkeepSlots, data.upkeepPerCycle);
        }

        // Category-Specific Information
        if (data is TowerData towerData || data.type == BuildingType.Defense)
        {
            SetupTowerSection(data as TowerData);
        }
        else if (data.type == BuildingType.Economic)
        {
            SetupProductionSection(data);
        }
        else if (data is HousingBuildingData housingData || data.type == BuildingType.Housing)
        {
            SetupHousingSection(data as HousingBuildingData);
        }
        else if (data.type is BuildingType.Unique or BuildingType.Utility)
        {
            SetupSpecialSection(data);
        }

        // Refresh Layout and Position
        if (tooltipRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
        }
        UpdateTooltipPosition();
    }

    public void HideTooltip()
    {
        if (tooltipRoot != null) tooltipRoot.SetActive(false);
        isVisible = false;
    }

    private void SetupResourcePool(List<SingleResourceUI> slots, List<ResourceCost> costs)
    {
        int count = costs != null ? costs.Count : 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (i < count)
            {
                slots[i].gameObject.SetActive(true);
                slots[i].Setup(costs[i].type, costs[i].amount, resourceVisualsDB);
            }
            else
            {
                slots[i].gameObject.SetActive(false);
            }
        }
    }

    private void SetupTowerSection(TowerData tower)
    {
        SetSectionVisibility(towers: true, production: false, housing: false, special: false);

        if (tower == null || statSlots.Count < 8) return;

        // Stat 0: Zasięg
        statSlots[0].gameObject.SetActive(true);
        statSlots[0].Setup("Zasięg:", tower.baseRange.ToString("F1"));

        // Stat 1: Obrażenia
        statSlots[1].gameObject.SetActive(true);
        statSlots[1].Setup("Obrażenia:", tower.baseDamage.ToString("F0"));

        // Stat 2: Szybkostrzelność
        statSlots[2].gameObject.SetActive(true);
        statSlots[2].Setup("Atak/s:", tower.fireRate.ToString("F2"));

        // Stat 3: Szansa na kryt
        statSlots[3].gameObject.SetActive(true);
        statSlots[3].Setup("Szansa kryt:", (tower.criticalChancel * 100f).ToString("F0") + "%");

        // Stat 4: Mnożnik kryta
        statSlots[4].gameObject.SetActive(true);
        statSlots[4].Setup("Mnożnik kryt:", (tower.criticalDamageMultiplier * 100f).ToString("F0") + "%");

        // Stat 5: Penetracja pancerza
        statSlots[5].gameObject.SetActive(true);
        statSlots[5].Setup("Pen. pancerza:", (tower.armorPenetration * 100f).ToString("F0") + "%");

        // Stat 6: Penetracja magii
        statSlots[6].gameObject.SetActive(true);
        statSlots[6].Setup("Pen. magii:", (tower.magicPenetration * 100f).ToString("F0") + "%");

        // Stat 7: Typ obrażeń
        statSlots[7].gameObject.SetActive(true);
        string typeName = tower.damageType.ToString();
        Color typeColor = Color.white;
        switch (tower.damageType)
        {
            case DamageType.Physical:
                typeName = "Fizyczne";
                typeColor = new Color32(245, 71, 39, 255);
                break;
            case DamageType.Magic:
                typeName = "Magiczne";
                typeColor = new Color32(59, 130, 246, 255);
                break;
            case DamageType.True:
                typeName = "Czyste";
                typeColor = Color.white;
                break;
        }
        statSlots[7].Setup("Typ:", typeName, typeColor);

        // Deactivate unused stat slots if pool > 8
        for (int i = 8; i < statSlots.Count; i++)
        {
            statSlots[i].gameObject.SetActive(false);
        }
    }

    private void SetupProductionSection(BuildingData data)
    {
        SetSectionVisibility(towers: false, production: true, housing: false, special: false);

        SetupResourcePool(productionSlots, data.productionPerCycle);

        if (txtBonusForPlacing != null)
        {
            if (data.allowedTerrain != null && data.allowedTerrain.Count > 0)
            {
                txtBonusForPlacing.text = $"Budowa na: {string.Join(", ", data.allowedTerrain)}";
            }
            else
            {
                txtBonusForPlacing.text = "Budowa na: Dowolny teren";
            }
        }

        if (txtBonusForSurround != null)
        {
            var rules = data.GetEffectiveBonusRules();
            List<string> ruleDescriptions = new List<string>();

            if (rules != null)
            {
                foreach (var rule in rules)
                {
                    if (rule.requiredFeature == HexFeatureType.None || rule.resourceYields == null || rule.resourceYields.Count == 0) continue;

                    List<string> yieldParts = new List<string>();
                    foreach (var y in rule.resourceYields)
                    {
                        if (y.baseBonusPerHex > 0f)
                        {
                            yieldParts.Add($"+{y.baseBonusPerHex:0.#} {y.resourceType}");
                        }
                    }

                    if (yieldParts.Count > 0)
                    {
                        ruleDescriptions.Add($"Sąsiedztwo [{rule.requiredFeature}]: {string.Join(", ", yieldParts)}");
                    }
                }
            }

            if (ruleDescriptions.Count > 0)
            {
                txtBonusForSurround.text = string.Join("\n", ruleDescriptions);
            }
            else
            {
                txtBonusForSurround.text = "Bonus sąsiedztwa: Brak";
            }
        }
    }

    private void SetupHousingSection(HousingBuildingData housing)
    {
        SetSectionVisibility(towers: false, production: false, housing: true, special: false);

        if (housing != null)
        {
            if (txtPop != null) txtPop.text = $"Populacja: {housing.initialResidents}/{housing.maxResidents}";
            if (txtGrowthCycles != null) txtGrowthCycles.text = $"Wzrost: co {housing.baseGrowthTicks} cykli";

            if (txtToSurvive != null)
            {
                if (housing.upkeepPerResident != null && housing.upkeepPerResident.Count > 0)
                {
                    txtToSurvive.text = $"Utrzymanie: {housing.upkeepPerResident[0].amount} {housing.upkeepPerResident[0].type}/osobę";
                }
                else
                {
                    txtToSurvive.text = "Utrzymanie: Brak";
                }
            }

            if (txtToGrow != null)
            {
                if (housing.growthSurplusCost != null && housing.growthSurplusCost.Count > 0)
                {
                    txtToGrow.text = $"Koszt wzrostu: {housing.growthSurplusCost[0].amount} {housing.growthSurplusCost[0].type}";
                }
                else
                {
                    txtToGrow.text = "Koszt wzrostu: Brak";
                }
            }
        }
    }

    private void SetupSpecialSection(BuildingData data)
    {
        SetSectionVisibility(towers: false, production: false, housing: false, special: true);

        if (txtSpecialDesc != null)
        {
            txtSpecialDesc.text = string.IsNullOrEmpty(data.description) ? "Budynek unikalny / użytkowy" : data.description;
        }
    }

    private void SetSectionVisibility(bool towers, bool production, bool housing, bool special)
    {
        if (pnlTowers != null) pnlTowers.SetActive(towers);
        if (pnlProduction != null) pnlProduction.SetActive(production);
        if (pnlHousing != null) pnlHousing.SetActive(housing);
        if (pnlSpecial != null) pnlSpecial.SetActive(special);
    }

    private void UpdateTooltipPosition()
    {
        if (tooltipRect == null || parentCanvas == null) return;

        RectTransform canvasRect = parentCanvas.transform as RectTransform;
        if (canvasRect == null) return;

        Camera cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Input.mousePosition, cam, out Vector2 localPoint))
        {
            Vector2 size = tooltipRect.rect.size;
            if (size.x <= 0 || size.y <= 0) size = tooltipRect.sizeDelta;

            Vector2 pivot = tooltipRect.pivot;
            Vector2 targetPos = localPoint + cursorOffset;

            float halfWidth = canvasRect.rect.width * 0.5f;
            float halfHeight = canvasRect.rect.height * 0.5f;

            // Clamp X
            float minX = -halfWidth + padding.x + (pivot.x * size.x);
            float maxX = halfWidth - padding.x - ((1f - pivot.x) * size.x);

            // Flip to left if exceeding right screen border
            if (targetPos.x > maxX)
            {
                targetPos.x = localPoint.x - Mathf.Abs(cursorOffset.x) - ((1f - pivot.x) * size.x);
            }
            targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);

            // Clamp Y
            float minY = -halfHeight + padding.y + (pivot.y * size.y);
            float maxY = halfHeight - padding.y - ((1f - pivot.y) * size.y);

            // Flip upward if exceeding bottom screen border
            if (targetPos.y < minY)
            {
                targetPos.y = localPoint.y + Mathf.Abs(cursorOffset.y) + (pivot.y * size.y);
            }
            targetPos.y = Mathf.Clamp(targetPos.y, minY, maxY);

            tooltipRect.anchoredPosition = targetPos;
        }
    }
}
