using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Główny kontroler grafu meta-progresji 2D w Menu Głównym (Master Controller).
/// Odpowiada za:
/// - Inicjalizację i pozycjonowanie 88 węzłów MetaNodeItemUI na bazie upgrade.nodePosition
/// - Dynamiczną ewaluację stanów Mgły Odkrywania (Fog of War) w całym DAG
/// - Generowanie i odświeżanie procedury linii połączeń w UILineRenderer
/// - Aktualizację stanu waluty Nadziei w TopBarze
/// - Połączenie z panelem szczegółów (MetaNodeDetailsPanelUI) oraz modalem draftu wież (TowerDraftModalUI)
/// </summary>
public class MetaGraphControllerUI : MonoBehaviour
{
    public static MetaGraphControllerUI Instance { get; private set; }

    [Header("Referencje Główne")]
    [SerializeField] private GameObject graphRoot;
    [SerializeField] private RectTransform viewportRect;
    [SerializeField] private RectTransform contentRect;
    [SerializeField] private RectTransform nodesContainer;
    [SerializeField] private UILineRenderer lineRenderer;
    [SerializeField] private MetaGraphPanZoomUI panZoomUI;
    [SerializeField] private MetaNodeDetailsPanelUI detailsPanel;

    [Header("Pasek Górny (TopBar)")]
    [SerializeField] private TextMeshProUGUI txtGraphTitle;
    [SerializeField] private TextMeshProUGUI txtHopeCurrency;
    [SerializeField] private Image imgHopeIcon;
    [SerializeField] private Button btnClose;
    [SerializeField] private Button btnResetFocus;

    [Header("Dane Meta-Ulepszeń")]
    [SerializeField] private List<MetaUpgradeSO> allUpgrades = new List<MetaUpgradeSO>();

    private readonly Dictionary<MetaUpgradeSO, MetaNodeItemUI> spawnedNodeItems = new Dictionary<MetaUpgradeSO, MetaNodeItemUI>();
    private bool isInitialized = false;

    public IReadOnlyDictionary<MetaUpgradeSO, MetaNodeItemUI> SpawnedNodeItems => spawnedNodeItems;
    public MetaGraphPanZoomUI PanZoom => panZoomUI;
    public UILineRenderer LineRenderer => lineRenderer;
    public MetaNodeDetailsPanelUI DetailsPanel => detailsPanel;
    public TextMeshProUGUI TxtHopeCurrency => txtHopeCurrency;
    public Button BtnClose => btnClose;
    public Button BtnResetFocus => btnResetFocus;
    public List<MetaUpgradeSO> AllUpgrades => allUpgrades;
    public bool IsOpen => graphRoot != null && graphRoot.activeSelf;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureHierarchyBuilt();
    }

    private void Start()
    {
        EnsureHierarchyBuilt();
        if (!isInitialized)
        {
            InitializeFromAvailableData();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Pobiera istniejącą instancję kontrolera lub buduje procedurę UI na Canvasie.
    /// </summary>
    public static MetaGraphControllerUI GetOrCreateInstance()
    {
        if (Instance != null) return Instance;

        Instance = FindFirstObjectByType<MetaGraphControllerUI>();
        if (Instance != null) return Instance;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas_MainMenu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        GameObject graphObj = new GameObject("Pnl_MetaGraphRoot", typeof(RectTransform));
        graphObj.transform.SetParent(canvas.transform, false);
        Instance = graphObj.AddComponent<MetaGraphControllerUI>();
        Instance.EnsureHierarchyBuilt();
        return Instance;
    }

    /// <summary>
    /// Inicjalizuje graf na bazie listy 88 ulepszeń.
    /// </summary>
    public void InitializeGraph(List<MetaUpgradeSO> upgrades)
    {
        EnsureHierarchyBuilt();

        allUpgrades = (upgrades != null) ? new List<MetaUpgradeSO>(upgrades) : new List<MetaUpgradeSO>();

        // Synchronizuj stan odblokowań z SaveManagerem
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SyncUpgradesWithSave(allUpgrades);
        }

        BuildOrUpdateNodes();
        RefreshGraph();

        // Znajdź węzeł początkowy i ustaw domyślne centrum
        MetaUpgradeSO startNode = allUpgrades.Find(u => u != null && (u.id == "start" || u.upgradeName == "Start"));
        if (startNode != null && panZoomUI != null)
        {
            panZoomUI.DefaultFocusPosition = startNode.nodePosition;
            panZoomUI.ResetViewInstant();
        }

        isInitialized = true;
    }

    /// <summary>
    /// Tworzy instancje węzłów MetaNodeItemUI dla wszystkich 88 ulepszeń.
    /// </summary>
    private void BuildOrUpdateNodes()
    {
        if (nodesContainer == null) return;

        // Usuń stare instancje jeśli istnieją
        foreach (var kvp in spawnedNodeItems)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value.gameObject);
            }
        }
        spawnedNodeItems.Clear();

        foreach (var upgrade in allUpgrades)
        {
            if (upgrade == null) continue;
            if (upgrade.isVariantOption) continue;

            GameObject nodeObj = new GameObject($"Node_{upgrade.id}", typeof(RectTransform), typeof(CanvasRenderer));
            nodeObj.transform.SetParent(nodesContainer, false);

            MetaNodeItemUI itemUI = nodeObj.AddComponent<MetaNodeItemUI>();
            itemUI.Setup(upgrade, OnNodeSelected);
            itemUI.RectTransform.anchoredPosition = upgrade.nodePosition;

            spawnedNodeItems[upgrade] = itemUI;
        }
    }

    /// <summary>
    /// Odświeża stany Mgły Odkrywania dla wszystkich węzłów oraz przelicza linie połączeń.
    /// </summary>
    public void RefreshGraph()
    {
        // 1. Zaktualizuj stan waluty Nadziei w TopBarze
        UpdateHopeCurrency();

        // 2. Przelicz stan Mgły Odkrywania (Fog of War) dla każdego węzła
        foreach (var kvp in spawnedNodeItems)
        {
            MetaUpgradeSO upgrade = kvp.Key;
            MetaNodeItemUI item = kvp.Value;
            if (upgrade == null || item == null) continue;

            MetaFogState state = MetaNodeItemUI.CalculateFogState(upgrade);
            item.UpdateVisuals(state);
        }

        // 3. Wygeneruj połączenia Beziera dla UILineRenderer
        List<GraphConnection> connections = new List<GraphConnection>();

        foreach (var kvp in spawnedNodeItems)
        {
            MetaUpgradeSO childUpgrade = kvp.Key;
            MetaNodeItemUI childItem = kvp.Value;
            if (childUpgrade == null || childItem == null) continue;

            MetaFogState childState = childItem.CurrentFogState;
            if (childState == MetaFogState.Hidden) continue;

            if (childUpgrade.prerequisites == null || childUpgrade.prerequisites.Count == 0) continue;

            foreach (var parentUpgrade in childUpgrade.prerequisites)
            {
                if (parentUpgrade == null) continue;
                if (!spawnedNodeItems.TryGetValue(parentUpgrade, out MetaNodeItemUI parentItem) || parentItem == null) continue;

                ConnectionStyle style;
                if (parentUpgrade.isUnlocked && childUpgrade.isUnlocked)
                {
                    style = ConnectionStyle.Active;
                }
                else if (parentUpgrade.isUnlocked && childState == MetaFogState.Available)
                {
                    style = ConnectionStyle.Available;
                }
                else if (childState == MetaFogState.Mystery)
                {
                    style = ConnectionStyle.Mystery;
                }
                else
                {
                    style = ConnectionStyle.Hidden;
                }

                if (style != ConnectionStyle.Hidden)
                {
                    // Łączenie od prawej krawędzi rodzica do lewej krawędzi dziecka
                    Vector2 startPt = parentUpgrade.nodePosition + new Vector2(MetaNodeItemUI.NodeWidth * 0.5f, 0f);
                    Vector2 endPt = childUpgrade.nodePosition - new Vector2(MetaNodeItemUI.NodeWidth * 0.5f, 0f);

                    connections.Add(new GraphConnection(startPt, endPt, style));
                }
            }
        }

        if (lineRenderer != null)
        {
            lineRenderer.SetConnections(connections);
        }

        // 4. Jeśli panel szczegółów jest otwarty, odśwież aktualny węzeł
        if (detailsPanel != null && detailsPanel.CurrentUpgrade != null)
        {
            MetaFogState curState = MetaNodeItemUI.CalculateFogState(detailsPanel.CurrentUpgrade);
            detailsPanel.DisplayNode(detailsPanel.CurrentUpgrade, curState);
        }
    }

    /// <summary>
    /// Wybór węzła przez gracza – otwiera panel szczegółów po prawej stronie ekranu.
    /// </summary>
    public void OnNodeSelected(MetaUpgradeSO upgrade)
    {
        if (upgrade == null) return;
        if (!spawnedNodeItems.TryGetValue(upgrade, out MetaNodeItemUI item)) return;

        if (item.CurrentFogState == MetaFogState.Hidden) return;

        if (detailsPanel != null)
        {
            detailsPanel.DisplayNode(upgrade, item.CurrentFogState);
        }
    }

    /// <summary>
    /// Aktualizuje wskaźnik Nadziei w pasku górnym.
    /// </summary>
    public void UpdateHopeCurrency()
    {
        int hope = (SaveManager.Instance != null && SaveManager.Instance.currentSaveData != null)
            ? SaveManager.Instance.currentSaveData.totalHope
            : 0;

        if (txtHopeCurrency != null)
        {
            txtHopeCurrency.text = LocalizationManager.GetFormat("UI_META_HOPE_CURRENCY_FMT", hope);
        }
    }

    /// <summary>
    /// Otwiera widok grafu meta-progresji i centruje widok.
    /// </summary>
    public void OpenGraph()
    {
        EnsureHierarchyBuilt();

        if (graphRoot != null)
        {
            graphRoot.SetActive(true);
        }
        gameObject.SetActive(true);

        if (!isInitialized)
        {
            InitializeFromAvailableData();
        }
        else
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SyncUpgradesWithSave(allUpgrades);
            }
            RefreshGraph();
        }

        if (panZoomUI != null)
        {
            panZoomUI.ResetViewInstant();
        }
    }

    /// <summary>
    /// Zamyka widok grafu i powraca do menu głównego.
    /// </summary>
    public void CloseGraph()
    {
        if (detailsPanel != null)
        {
            detailsPanel.ClosePanel();
        }

        if (graphRoot != null)
        {
            graphRoot.SetActive(false);
        }
        gameObject.SetActive(false);
    }

    private void InitializeFromAvailableData()
    {
        List<MetaUpgradeSO> upgradesToUse = null;

        if (MetaUpgradeManager.Instance != null && MetaUpgradeManager.Instance.allUpgrades != null && MetaUpgradeManager.Instance.allUpgrades.Count > 0)
        {
            upgradesToUse = MetaUpgradeManager.Instance.allUpgrades;
        }
        else
        {
            upgradesToUse = LoadAllUpgradeAssets();
        }

        InitializeGraph(upgradesToUse);
    }

    private List<MetaUpgradeSO> LoadAllUpgradeAssets()
    {
        List<MetaUpgradeSO> list = new List<MetaUpgradeSO>();

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:MetaUpgradeSO");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            MetaUpgradeSO so = UnityEditor.AssetDatabase.LoadAssetAtPath<MetaUpgradeSO>(path);
            if (so != null && !list.Contains(so))
            {
                list.Add(so);
            }
        }
#else
        MetaUpgradeSO[] loaded = Resources.FindObjectsOfTypeAll<MetaUpgradeSO>();
        if (loaded != null)
        {
            list.AddRange(loaded);
        }
#endif
        return list;
    }

    /// <summary>
    /// Buduje proceduralną hierarchię grafu UGUI spełniającą GEMINI.md, jeśli brakuje przypisanych referencji.
    /// </summary>
    public void EnsureHierarchyBuilt()
    {
        if (graphRoot == null) graphRoot = gameObject;

        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        // Ciemne tło całego panelu grafu
        Image rootImg = GetComponent<Image>();
        if (rootImg == null)
        {
            rootImg = gameObject.AddComponent<Image>();
            rootImg.color = new Color(0.05f, 0.06f, 0.08f, 0.98f);
        }

        // 1. Pasek Górny (TopBar)
        Transform topBarTrans = transform.Find("TopBar");
        GameObject topBarObj;
        if (topBarTrans == null)
        {
            topBarObj = new GameObject("TopBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            topBarObj.transform.SetParent(transform, false);
            RectTransform tbRect = topBarObj.GetComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(0f, 1f);
            tbRect.anchorMax = new Vector2(1f, 1f);
            tbRect.pivot = new Vector2(0.5f, 1f);
            tbRect.anchoredPosition = Vector2.zero;
            tbRect.sizeDelta = new Vector2(0f, 60f);

            Image tbImg = topBarObj.GetComponent<Image>();
            tbImg.color = new Color(0.08f, 0.09f, 0.12f, 0.95f);
        }
        else
        {
            topBarObj = topBarTrans.gameObject;
        }

        // Tytuł grafu (20 pt)
        Transform titleTrans = topBarObj.transform.Find("Txt_GraphTitle");
        if (titleTrans == null)
        {
            GameObject titleObj = new GameObject("Txt_GraphTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(topBarObj.transform, false);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.5f);
            titleRect.anchorMax = new Vector2(0f, 0.5f);
            titleRect.pivot = new Vector2(0f, 0.5f);
            titleRect.anchoredPosition = new Vector2(25f, 0f);
            titleRect.sizeDelta = new Vector2(400f, 40f);

            txtGraphTitle = titleObj.GetComponent<TextMeshProUGUI>();
            txtGraphTitle.fontSize = MetaNodeDetailsPanelUI.FontSizeHeader; // 20 pt
            txtGraphTitle.fontStyle = FontStyles.Bold;
            txtGraphTitle.color = new Color(0.95f, 0.85f, 0.45f);
            txtGraphTitle.text = LocalizationManager.Get("UI_META_GRAPH_TITLE", "DRZEWO ROZWOJU BASTIONU");
            txtGraphTitle.alignment = TextAlignmentOptions.MidlineLeft;
        }
        else if (txtGraphTitle == null)
        {
            txtGraphTitle = titleTrans.GetComponent<TextMeshProUGUI>();
        }

        // Licznik Nadziei (16 pt) + Ikona (24 px)
        Transform hopeTrans = topBarObj.transform.Find("Txt_HopeCurrency");
        if (hopeTrans == null)
        {
            GameObject hopeObj = new GameObject("Txt_HopeCurrency", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            hopeObj.transform.SetParent(topBarObj.transform, false);
            RectTransform hopeRect = hopeObj.GetComponent<RectTransform>();
            hopeRect.anchorMin = new Vector2(0.5f, 0.5f);
            hopeRect.anchorMax = new Vector2(0.5f, 0.5f);
            hopeRect.pivot = new Vector2(0.5f, 0.5f);
            hopeRect.anchoredPosition = new Vector2(0f, 0f);
            hopeRect.sizeDelta = new Vector2(240f, 36f);

            txtHopeCurrency = hopeObj.GetComponent<TextMeshProUGUI>();
            txtHopeCurrency.fontSize = MetaNodeDetailsPanelUI.FontSizeSection; // 16 pt
            txtHopeCurrency.fontStyle = FontStyles.Bold;
            txtHopeCurrency.color = new Color(0.35f, 0.85f, 1.0f);
            txtHopeCurrency.text = LocalizationManager.GetFormat("UI_META_HOPE_CURRENCY_FMT", 0);
            txtHopeCurrency.alignment = TextAlignmentOptions.Center;
        }
        else if (txtHopeCurrency == null)
        {
            txtHopeCurrency = hopeTrans.GetComponent<TextMeshProUGUI>();
        }

        // Przycisk "CENTRUM [F]" (16 pt)
        Transform resetTrans = topBarObj.transform.Find("Btn_ResetFocus");
        if (resetTrans == null)
        {
            GameObject resetObj = new GameObject("Btn_ResetFocus", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            resetObj.transform.SetParent(topBarObj.transform, false);
            RectTransform resRect = resetObj.GetComponent<RectTransform>();
            resRect.anchorMin = new Vector2(1f, 0.5f);
            resRect.anchorMax = new Vector2(1f, 0.5f);
            resRect.pivot = new Vector2(1f, 0.5f);
            resRect.anchoredPosition = new Vector2(-160f, 0f);
            resRect.sizeDelta = new Vector2(140f, 40f);

            Image resImg = resetObj.GetComponent<Image>();
            resImg.color = new Color(0.18f, 0.22f, 0.28f, 1f);

            btnResetFocus = resetObj.GetComponent<Button>();
            btnResetFocus.onClick.RemoveAllListeners();
            btnResetFocus.onClick.AddListener(() => panZoomUI?.ResetView());

            GameObject lblObj = new GameObject("Txt_Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            lblObj.transform.SetParent(resetObj.transform, false);
            RectTransform lblRect = lblObj.GetComponent<RectTransform>();
            lblRect.anchorMin = Vector2.zero;
            lblRect.anchorMax = Vector2.one;
            lblRect.offsetMin = Vector2.zero;
            lblRect.offsetMax = Vector2.zero;

            TextMeshProUGUI lbl = lblObj.GetComponent<TextMeshProUGUI>();
            lbl.fontSize = MetaNodeDetailsPanelUI.FontSizeButton; // 16 pt
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.text = LocalizationManager.Get("UI_META_BTN_RECENTER", "CENTRUM [F]");
            lbl.color = Color.white;
        }
        else if (btnResetFocus == null)
        {
            btnResetFocus = resetTrans.GetComponent<Button>();
        }

        // Przycisk "POWRÓT" (16 pt)
        Transform closeTrans = topBarObj.transform.Find("Btn_Close");
        if (closeTrans == null)
        {
            GameObject closeObj = new GameObject("Btn_Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeObj.transform.SetParent(topBarObj.transform, false);
            RectTransform clRect = closeObj.GetComponent<RectTransform>();
            clRect.anchorMin = new Vector2(1f, 0.5f);
            clRect.anchorMax = new Vector2(1f, 0.5f);
            clRect.pivot = new Vector2(1f, 0.5f);
            clRect.anchoredPosition = new Vector2(-15f, 0f);
            clRect.sizeDelta = new Vector2(130f, 40f);

            Image clImg = closeObj.GetComponent<Image>();
            clImg.color = new Color(0.55f, 0.20f, 0.20f, 1f);

            btnClose = closeObj.GetComponent<Button>();
            btnClose.onClick.RemoveAllListeners();
            btnClose.onClick.AddListener(CloseGraph);

            GameObject lblObj = new GameObject("Txt_Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            lblObj.transform.SetParent(closeObj.transform, false);
            RectTransform lblRect = lblObj.GetComponent<RectTransform>();
            lblRect.anchorMin = Vector2.zero;
            lblRect.anchorMax = Vector2.one;
            lblRect.offsetMin = Vector2.zero;
            lblRect.offsetMax = Vector2.zero;

            TextMeshProUGUI lbl = lblObj.GetComponent<TextMeshProUGUI>();
            lbl.fontSize = MetaNodeDetailsPanelUI.FontSizeButton; // 16 pt
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.text = LocalizationManager.Get("UI_RETURN", "POWRÓT");
            lbl.color = Color.white;
        }
        else if (btnClose == null)
        {
            btnClose = closeTrans.GetComponent<Button>();
        }

        // 2. Viewport (z RectMask2D)
        Transform vpTrans = transform.Find("Viewport");
        GameObject vpObj;
        if (vpTrans == null)
        {
            vpObj = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(RectMask2D), typeof(Image));
            vpObj.transform.SetParent(transform, false);
            viewportRect = vpObj.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = new Vector2(-360f, -60f); // Miejsce na prawy panel i górny pasek

            Image vpImg = vpObj.GetComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0.01f); // Cienki raycast target dla Pan & Zoom
        }
        else
        {
            vpObj = vpTrans.gameObject;
            viewportRect = vpObj.GetComponent<RectTransform>();
        }

        // 3. Content (z MetaGraphPanZoomUI)
        Transform contentTrans = vpObj.transform.Find("Content");
        GameObject contentObj;
        if (contentTrans == null)
        {
            contentObj = new GameObject("Content", typeof(RectTransform));
            contentObj.transform.SetParent(vpObj.transform, false);
            contentRect = contentObj.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.zero;
            contentRect.pivot = Vector2.zero;
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(6000f, 5000f);

            panZoomUI = vpObj.AddComponent<MetaGraphPanZoomUI>();
            panZoomUI.Initialize(contentRect, viewportRect);
        }
        else
        {
            contentObj = contentTrans.gameObject;
            contentRect = contentObj.GetComponent<RectTransform>();
            if (panZoomUI == null) panZoomUI = vpObj.GetComponent<MetaGraphPanZoomUI>();
            if (panZoomUI != null) panZoomUI.Initialize(contentRect, viewportRect);
        }

        // 4. UILineRenderer (Proceduralne linie w 1 draw call)
        Transform lineTrans = contentObj.transform.Find("LinesRenderer");
        if (lineTrans == null)
        {
            GameObject lineObj = new GameObject("LinesRenderer", typeof(RectTransform), typeof(CanvasRenderer), typeof(UILineRenderer));
            lineObj.transform.SetParent(contentObj.transform, false);
            RectTransform lrRect = lineObj.GetComponent<RectTransform>();
            lrRect.anchorMin = Vector2.zero;
            lrRect.anchorMax = Vector2.zero;
            lrRect.pivot = Vector2.zero;
            lrRect.anchoredPosition = Vector2.zero;
            lrRect.sizeDelta = contentRect.sizeDelta;

            lineRenderer = lineObj.GetComponent<UILineRenderer>();
        }
        else
        {
            if (lineTrans.GetComponent<CanvasRenderer>() == null)
            {
                lineTrans.gameObject.AddComponent<CanvasRenderer>();
            }

            if (lineRenderer == null)
            {
                lineRenderer = lineTrans.GetComponent<UILineRenderer>();
                if (lineRenderer == null)
                {
                    lineRenderer = lineTrans.gameObject.AddComponent<UILineRenderer>();
                }
            }
        }
        if (lineRenderer != null)
        {
            if (lineRenderer.GetComponent<CanvasRenderer>() == null)
            {
                lineRenderer.gameObject.AddComponent<CanvasRenderer>();
            }

            RectTransform lrRect = lineRenderer.GetComponent<RectTransform>();
            if (lrRect != null)
            {
                lrRect.anchorMin = Vector2.zero;
                lrRect.anchorMax = Vector2.zero;
                lrRect.pivot = Vector2.zero;
                lrRect.anchoredPosition = Vector2.zero;
                lrRect.sizeDelta = contentRect.sizeDelta;
            }
        }

        // 5. Nodes Container
        Transform nodesTrans = contentObj.transform.Find("NodesContainer");
        if (nodesTrans == null)
        {
            GameObject ncObj = new GameObject("NodesContainer", typeof(RectTransform));
            ncObj.transform.SetParent(contentObj.transform, false);
            nodesContainer = ncObj.GetComponent<RectTransform>();
            nodesContainer.anchorMin = Vector2.zero;
            nodesContainer.anchorMax = Vector2.zero;
            nodesContainer.pivot = Vector2.zero;
            nodesContainer.anchoredPosition = Vector2.zero;
            nodesContainer.sizeDelta = contentRect.sizeDelta;
        }
        else if (nodesContainer == null)
        {
            nodesContainer = nodesTrans.GetComponent<RectTransform>();
        }
        if (nodesContainer != null)
        {
            nodesContainer.anchorMin = Vector2.zero;
            nodesContainer.anchorMax = Vector2.zero;
            nodesContainer.pivot = Vector2.zero;
            nodesContainer.anchoredPosition = Vector2.zero;
            nodesContainer.sizeDelta = contentRect.sizeDelta;
        }

        // 6. Panel Szczegółów (Prawy panel, 360 px)
        Transform detailsTrans = transform.Find("Pnl_NodeDetails");
        if (detailsTrans == null)
        {
            GameObject detailsObj = new GameObject("Pnl_NodeDetails", typeof(RectTransform));
            detailsObj.transform.SetParent(transform, false);
            RectTransform dtRect = detailsObj.GetComponent<RectTransform>();
            dtRect.anchorMin = new Vector2(1f, 0f);
            dtRect.anchorMax = new Vector2(1f, 1f);
            dtRect.pivot = new Vector2(1f, 0.5f);
            dtRect.anchoredPosition = Vector2.zero;
            dtRect.sizeDelta = new Vector2(360f, -60f); // Poniżej topbara

            detailsPanel = detailsObj.AddComponent<MetaNodeDetailsPanelUI>();
            detailsPanel.EnsureHierarchyBuilt();
            detailsPanel.SetOnPurchasedCallback(OnUpgradePurchased);
        }
        else if (detailsPanel == null)
        {
            detailsPanel = detailsTrans.GetComponent<MetaNodeDetailsPanelUI>();
            detailsPanel.SetOnPurchasedCallback(OnUpgradePurchased);
        }
    }

    private void OnUpgradePurchased(MetaUpgradeSO upgrade)
    {
        RefreshGraph();
    }
}
