using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kontroler rozbudowanego tooltipa dobowego bilansu surowców w górnym HUD.
/// Całkowicie pomija ikonę surowca (zgodnie z wytycznymi).
/// Rozwija się ZAWSZE w dół z pivotem u góry, poniżej najechanego kafelka.
/// Wszystkie elementy mają wyłączony raycastTarget, dzięki czemu tooltip nie blokuje myszy.
/// </summary>
public class ResourceTooltipUI : MonoBehaviour
{
    public static ResourceTooltipUI Instance { get; private set; }

    [Header("Główny Kontener i Canvas")]
    [SerializeField] private GameObject tooltipRoot;
    [SerializeField] private RectTransform tooltipRect;
    [SerializeField] private Canvas parentCanvas;

    [Header("Nagłówek")]
    [SerializeField] private TextMeshProUGUI txtResourceTitle;
    [SerializeField] private TextMeshProUGUI txtDailyNetBalance;

    [Header("Sekcja Produkcji")]
    [SerializeField] private GameObject pnlProductionSection;
    [SerializeField] private TextMeshProUGUI txtProductionHeader;
    [SerializeField] private Transform productionContainer;
    [SerializeField] private TextMeshProUGUI txtProductionEmpty;

    [Header("Sekcja Zużycia")]
    [SerializeField] private GameObject pnlConsumptionSection;
    [SerializeField] private TextMeshProUGUI txtConsumptionHeader;
    [SerializeField] private Transform consumptionContainer;
    [SerializeField] private TextMeshProUGUI txtConsumptionEmpty;

    [Header("Separatory (4px)")]
    [SerializeField] private GameObject dividerHeaderToProd;
    [SerializeField] private GameObject dividerProdToCons;

    [Header("Kolorystyka (UI Guidelines)")]
    [SerializeField] private Color positiveColor = new Color32(74, 222, 128, 255); // #4ADE80
    [SerializeField] private Color negativeColor = new Color32(248, 113, 113, 255); // #F87171
    [SerializeField] private Color dividerColor = new Color32(55, 65, 81, 255);     // #374151
    [SerializeField] private Color panelBgColor = new Color32(24, 24, 28, 250);     // #18181C

    private readonly List<TextMeshProUGUI> productionItemPool = new();
    private readonly List<TextMeshProUGUI> consumptionItemPool = new();

    private ResourceType currentResourceType;
    private RectTransform currentTargetRect;
    private bool isTooltipOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoEnsureInstance()
    {
        if (Instance == null)
        {
            var existing = FindFirstObjectByType<ResourceTooltipUI>();
            if (existing == null)
            {
                var go = new GameObject("ResourceTooltipUI_AutoManager");
                go.AddComponent<ResourceTooltipUI>();
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        EnsureUI();
        Hide();
    }

    private void Start()
    {
        SubscribeEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
        if (Instance == this) Instance = null;
    }

    private void SubscribeEvents()
    {
        BuildingEntity.OnAnyBuildingEconomyChanged += HandleEconomyChanged;
        PhaseResourceForecastManager.OnForecastDirty += HandleEconomyChanged;
    }

    private void UnsubscribeEvents()
    {
        BuildingEntity.OnAnyBuildingEconomyChanged -= HandleEconomyChanged;
        PhaseResourceForecastManager.OnForecastDirty -= HandleEconomyChanged;
    }

    private void HandleEconomyChanged()
    {
        if (isTooltipOpen)
        {
            Refresh();
        }
    }

    /// <summary>
    /// Wyświetla tooltip dla danego surowca, pozycjonując go zawsze PONIŻEJ wskazanego targetu.
    /// </summary>
    public void Show(ResourceType resType, RectTransform target)
    {
        EnsureUI();
        if (tooltipRoot == null) return;

        currentResourceType = resType;
        currentTargetRect = target;
        isTooltipOpen = true;

        Refresh();
        PositionTooltip(target);

        tooltipRoot.SetActive(true);
    }

    /// <summary>
    /// Ukrywa tooltip.
    /// </summary>
    public void Hide()
    {
        isTooltipOpen = false;
        currentTargetRect = null;
        if (tooltipRoot != null)
        {
            tooltipRoot.SetActive(false);
        }
    }

    /// <summary>
    /// Odświeża zawartość otwartego tooltipa.
    /// </summary>
    public void Refresh()
    {
        if (!isTooltipOpen) return;

        var report = DailyResourceCalculator.CalculateDailyReport(currentResourceType);

        // 1. Tytuł surowca
        string resName = DailyResourceCalculator.GetResourceDisplayName(currentResourceType);
        if (txtResourceTitle != null)
        {
            txtResourceTitle.text = resName;
        }

        // 2. Bilans Dobowy Netto
        if (txtDailyNetBalance != null)
        {
            int net = report.netDailyBalance;
            string sign = net > 0 ? "+" : (net < 0 ? "-" : "");
            int absNet = Mathf.Abs(net);

            txtDailyNetBalance.text = $"Bilans dobowy: {sign}{absNet}";
            txtDailyNetBalance.color = net >= 0 ? positiveColor : negativeColor;
        }

        // 3. Sekcja Produkcji
        UpdateProductionSection(report);

        // 4. Sekcja Zużycia
        UpdateConsumptionSection(report);

        // Wymuszenie aktualizacji layoutu i ponowne dopasowanie pozycji
        if (tooltipRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRect);
        }

        if (currentTargetRect != null)
        {
            PositionTooltip(currentTargetRect);
        }
    }

    private void UpdateProductionSection(DailyResourceCalculator.DailyResourceReport report)
    {
        if (productionContainer == null) return;

        foreach (var item in productionItemPool)
        {
            if (item != null) item.gameObject.SetActive(false);
        }

        int count = report.productionSources.Count;
        if (count == 0)
        {
            if (txtProductionEmpty != null) txtProductionEmpty.gameObject.SetActive(true);
        }
        else
        {
            if (txtProductionEmpty != null) txtProductionEmpty.gameObject.SetActive(false);

            for (int i = 0; i < count; i++)
            {
                var src = report.productionSources[i];
                var tmp = GetOrCreatePoolItem(productionItemPool, productionContainer);
                tmp.text = src.GetFormattedProduction();
                tmp.color = positiveColor;
                tmp.gameObject.SetActive(true);
            }
        }
    }

    private void UpdateConsumptionSection(DailyResourceCalculator.DailyResourceReport report)
    {
        if (consumptionContainer == null) return;

        foreach (var item in consumptionItemPool)
        {
            if (item != null) item.gameObject.SetActive(false);
        }

        int count = report.consumptionSources.Count;
        if (count == 0)
        {
            if (txtConsumptionEmpty != null) txtConsumptionEmpty.gameObject.SetActive(true);
        }
        else
        {
            if (txtConsumptionEmpty != null) txtConsumptionEmpty.gameObject.SetActive(false);

            for (int i = 0; i < count; i++)
            {
                var src = report.consumptionSources[i];
                var tmp = GetOrCreatePoolItem(consumptionItemPool, consumptionContainer);
                tmp.text = src.GetFormattedConsumption();
                tmp.color = negativeColor;
                tmp.gameObject.SetActive(true);
            }
        }
    }

    private TextMeshProUGUI GetOrCreatePoolItem(List<TextMeshProUGUI> pool, Transform container)
    {
        foreach (var item in pool)
        {
            if (item != null && !item.gameObject.activeSelf)
            {
                return item;
            }
        }

        var go = new GameObject("Txt_ItemRow", typeof(RectTransform));
        go.transform.SetParent(container, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 13f;
        tmp.font = TMP_Settings.defaultFontAsset;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false; // Nigdy nie blokuje myszy

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 20f);

        pool.Add(tmp);
        return tmp;
    }

    /// <summary>
    /// Pozycjonuje tooltip ZAWSZE W DÓŁ (poniżej najechanego obiektu).
    /// </summary>
    private void PositionTooltip(RectTransform target)
    {
        if (tooltipRect == null || target == null || parentCanvas == null) return;

        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        // corners[0] = bot-left, corners[3] = bot-right
        Vector3 targetBottomCenter = (corners[0] + corners[3]) * 0.5f;

        Camera cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        RectTransform canvasRT = parentCanvas.transform as RectTransform;
        if (canvasRT == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRT,
            RectTransformUtility.WorldToScreenPoint(cam, targetBottomCenter),
            cam,
            out Vector2 localPoint);

        // ZAWSZE pivot u góry (0.5, 1.0) -> rozwija się w dół do wnętrza ekranu!
        tooltipRect.pivot = new Vector2(0.5f, 1f);

        // Umieszczamy 6 pikseli poniżej dolnej krawędzi celu
        float posY = localPoint.y - 6f;

        // Ograniczamy w osi X do obszaru Canvasu
        Rect canvasRect = canvasRT.rect;
        float halfWidth = tooltipRect.rect.width * 0.5f;
        float clampedX = Mathf.Clamp(localPoint.x, canvasRect.xMin + halfWidth + 8f, canvasRect.xMax - halfWidth - 8f);

        tooltipRect.anchoredPosition = new Vector2(clampedX, posY);
    }

    /// <summary>
    /// Sprawdza czy UI istnieje w scenie / na Canvasie. Jeśli nie (lub jeśli jest stare/zepsute), generuje je w locie.
    /// </summary>
    public void EnsureUI()
    {
        if (tooltipRoot != null && tooltipRect != null) return;

        if (parentCanvas == null)
        {
            var topBonusUI = FindFirstObjectByType<TopBonusInfoUI>();
            if (topBonusUI != null)
            {
                parentCanvas = topBonusUI.GetComponentInParent<Canvas>();
            }

            if (parentCanvas == null)
            {
                parentCanvas = FindFirstObjectByType<Canvas>();
            }
        }

        if (parentCanvas == null) return;

        // Jeśli w scenie jest stary obiekt Pnl_DailyResourceTooltip, usuwamy go, aby zaktualizować strukturę
        Transform existing = parentCanvas.transform.Find("Pnl_DailyResourceTooltip");
        if (existing != null)
        {
            if (existing.Find("Header_Row") != null || existing.GetComponent<CanvasGroup>() == null)
            {
                DestroyImmediate(existing.gameObject);
            }
            else
            {
                tooltipRoot = existing.gameObject;
                tooltipRect = existing.GetComponent<RectTransform>();
                BindExistingHierarchy(existing);
                return;
            }
        }

        BuildProceduralUI();
    }

    private void BuildProceduralUI()
    {
        // 1. Root Panel z CanvasGroup (blocksRaycasts = false gwarantuje, że tooltip nie przechwytuje zdarzeń myszy)
        var rootGO = new GameObject("Pnl_DailyResourceTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(CanvasGroup));
        rootGO.transform.SetParent(parentCanvas.transform, false);
        tooltipRoot = rootGO;
        tooltipRect = rootGO.GetComponent<RectTransform>();

        var cg = rootGO.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
        tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
        tooltipRect.pivot = new Vector2(0.5f, 1f); // TOP CENTER pivot -> zawsze rozwija się w dół
        tooltipRect.sizeDelta = new Vector2(260f, 0f);

        var bgImage = rootGO.GetComponent<Image>();
        bgImage.color = panelBgColor;
        bgImage.raycastTarget = false;

        var rootLayout = rootGO.GetComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(14, 14, 10, 12);
        rootLayout.spacing = 6f;
        rootLayout.childAlignment = TextAnchor.UpperLeft;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        var fitter = rootGO.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 2. Tytuł surowca (20 pt, Bold) - brak ikony zgodnie z życzeniem gracza
        var titleGO = new GameObject("Txt_ResourceTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(rootGO.transform, false);
        txtResourceTitle = titleGO.GetComponent<TextMeshProUGUI>();
        txtResourceTitle.fontSize = 20f;
        txtResourceTitle.fontStyle = FontStyles.Bold;
        txtResourceTitle.font = TMP_Settings.defaultFontAsset;
        txtResourceTitle.color = Color.white;
        txtResourceTitle.raycastTarget = false;
        txtResourceTitle.textWrappingMode = TextWrappingModes.NoWrap;

        // 3. Bilans Dobowy Netto (20 pt, Bold)
        var netGO = new GameObject("Txt_DailyNetBalance", typeof(RectTransform), typeof(TextMeshProUGUI));
        netGO.transform.SetParent(rootGO.transform, false);
        txtDailyNetBalance = netGO.GetComponent<TextMeshProUGUI>();
        txtDailyNetBalance.fontSize = 20f;
        txtDailyNetBalance.fontStyle = FontStyles.Bold;
        txtDailyNetBalance.font = TMP_Settings.defaultFontAsset;
        txtDailyNetBalance.color = positiveColor;
        txtDailyNetBalance.raycastTarget = false;
        txtDailyNetBalance.text = "Bilans dobowy: +0";

        // --- SEPARATOR 1 (Kreska 4px) ---
        dividerHeaderToProd = CreateDivider4px(rootGO.transform, "Divider_4px_HeaderToProd");

        // 4. Sekcja Produkcji
        pnlProductionSection = new GameObject("Section_Production", typeof(RectTransform), typeof(VerticalLayoutGroup));
        pnlProductionSection.transform.SetParent(rootGO.transform, false);
        var prodLayout = pnlProductionSection.GetComponent<VerticalLayoutGroup>();
        prodLayout.spacing = 3f;
        prodLayout.childControlWidth = true;
        prodLayout.childControlHeight = true;
        prodLayout.childForceExpandWidth = true;
        prodLayout.childForceExpandHeight = false;

        // Nagłówek sekcji Produkcja (16 pt)
        var prodHeaderGO = new GameObject("Txt_ProductionHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
        prodHeaderGO.transform.SetParent(pnlProductionSection.transform, false);
        txtProductionHeader = prodHeaderGO.GetComponent<TextMeshProUGUI>();
        txtProductionHeader.text = "PRODUKCJA";
        txtProductionHeader.fontSize = 16f;
        txtProductionHeader.fontStyle = FontStyles.Bold;
        txtProductionHeader.font = TMP_Settings.defaultFontAsset;
        txtProductionHeader.color = positiveColor;
        txtProductionHeader.raycastTarget = false;

        // Kontener elementów produkcji
        var prodContainerGO = new GameObject("Container_ProductionItems", typeof(RectTransform), typeof(VerticalLayoutGroup));
        prodContainerGO.transform.SetParent(pnlProductionSection.transform, false);
        productionContainer = prodContainerGO.transform;
        var pItemsLayout = prodContainerGO.GetComponent<VerticalLayoutGroup>();
        pItemsLayout.spacing = 2f;
        pItemsLayout.childControlWidth = true;
        pItemsLayout.childControlHeight = true;
        pItemsLayout.childForceExpandWidth = true;
        pItemsLayout.childForceExpandHeight = false;

        // Fallback pusty
        var prodEmptyGO = new GameObject("Txt_ProductionEmpty", typeof(RectTransform), typeof(TextMeshProUGUI));
        prodEmptyGO.transform.SetParent(pnlProductionSection.transform, false);
        txtProductionEmpty = prodEmptyGO.GetComponent<TextMeshProUGUI>();
        txtProductionEmpty.text = "(Brak produkcji)";
        txtProductionEmpty.fontSize = 12f;
        txtProductionEmpty.fontStyle = FontStyles.Italic;
        txtProductionEmpty.font = TMP_Settings.defaultFontAsset;
        txtProductionEmpty.color = new Color(0.6f, 0.65f, 0.72f, 1f);
        txtProductionEmpty.raycastTarget = false;

        // --- SEPARATOR 2 (Kreska 4px) ---
        dividerProdToCons = CreateDivider4px(rootGO.transform, "Divider_4px_ProdToCons");

        // 5. Sekcja Zużycia
        pnlConsumptionSection = new GameObject("Section_Consumption", typeof(RectTransform), typeof(VerticalLayoutGroup));
        pnlConsumptionSection.transform.SetParent(rootGO.transform, false);
        var consLayout = pnlConsumptionSection.GetComponent<VerticalLayoutGroup>();
        consLayout.spacing = 3f;
        consLayout.childControlWidth = true;
        consLayout.childControlHeight = true;
        consLayout.childForceExpandWidth = true;
        consLayout.childForceExpandHeight = false;

        // Nagłówek sekcji Zużycie (16 pt)
        var consHeaderGO = new GameObject("Txt_ConsumptionHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
        consHeaderGO.transform.SetParent(pnlConsumptionSection.transform, false);
        txtConsumptionHeader = consHeaderGO.GetComponent<TextMeshProUGUI>();
        txtConsumptionHeader.text = "ZUŻYCIE";
        txtConsumptionHeader.fontSize = 16f;
        txtConsumptionHeader.fontStyle = FontStyles.Bold;
        txtConsumptionHeader.font = TMP_Settings.defaultFontAsset;
        txtConsumptionHeader.color = negativeColor;
        txtConsumptionHeader.raycastTarget = false;

        // Kontener elementów zużycia
        var consContainerGO = new GameObject("Container_ConsumptionItems", typeof(RectTransform), typeof(VerticalLayoutGroup));
        consContainerGO.transform.SetParent(pnlConsumptionSection.transform, false);
        consumptionContainer = consContainerGO.transform;
        var cItemsLayout = consContainerGO.GetComponent<VerticalLayoutGroup>();
        cItemsLayout.spacing = 2f;
        cItemsLayout.childControlWidth = true;
        cItemsLayout.childControlHeight = true;
        cItemsLayout.childForceExpandWidth = true;
        cItemsLayout.childForceExpandHeight = false;

        // Fallback pusty
        var consEmptyGO = new GameObject("Txt_ConsumptionEmpty", typeof(RectTransform), typeof(TextMeshProUGUI));
        consEmptyGO.transform.SetParent(pnlConsumptionSection.transform, false);
        txtConsumptionEmpty = consEmptyGO.GetComponent<TextMeshProUGUI>();
        txtConsumptionEmpty.text = "(Brak zużycia)";
        txtConsumptionEmpty.fontSize = 12f;
        txtConsumptionEmpty.fontStyle = FontStyles.Italic;
        txtConsumptionEmpty.font = TMP_Settings.defaultFontAsset;
        txtConsumptionEmpty.color = new Color(0.6f, 0.65f, 0.72f, 1f);
        txtConsumptionEmpty.raycastTarget = false;

        rootGO.SetActive(false);
    }

    private GameObject CreateDivider4px(Transform parent, string name)
    {
        var divGO = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        divGO.transform.SetParent(parent, false);

        var img = divGO.GetComponent<Image>();
        img.color = dividerColor;
        img.raycastTarget = false;

        var le = divGO.GetComponent<LayoutElement>();
        le.minHeight = 4f;
        le.preferredHeight = 4f;
        le.flexibleHeight = 0f;

        var rt = divGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 4f);

        return divGO;
    }

    private void BindExistingHierarchy(Transform root)
    {
        txtResourceTitle = root.Find("Txt_ResourceTitle")?.GetComponent<TextMeshProUGUI>();
        txtDailyNetBalance = root.Find("Txt_DailyNetBalance")?.GetComponent<TextMeshProUGUI>();

        pnlProductionSection = root.Find("Section_Production")?.gameObject;
        txtProductionHeader = root.Find("Section_Production/Txt_ProductionHeader")?.GetComponent<TextMeshProUGUI>();
        productionContainer = root.Find("Section_Production/Container_ProductionItems");
        txtProductionEmpty = root.Find("Section_Production/Txt_ProductionEmpty")?.GetComponent<TextMeshProUGUI>();

        pnlConsumptionSection = root.Find("Section_Consumption")?.gameObject;
        txtConsumptionHeader = root.Find("Section_Consumption/Txt_ConsumptionHeader")?.GetComponent<TextMeshProUGUI>();
        consumptionContainer = root.Find("Section_Consumption/Container_ConsumptionItems");
        txtConsumptionEmpty = root.Find("Section_Consumption/Txt_ConsumptionEmpty")?.GetComponent<TextMeshProUGUI>();

        dividerHeaderToProd = root.Find("Divider_4px_HeaderToProd")?.gameObject;
        dividerProdToCons = root.Find("Divider_4px_ProdToCons")?.gameObject;
    }
}
