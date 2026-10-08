using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class GenerateProductionContextUI
{
    private const string SessionKey = "ProductionContextUI_Built_v1";
    private const string ScenePath = "Assets/Main folder/_Scenes/TESTSCENE_UI.unity";
    private const string PrefabFolder = "Assets/Main folder/Prefabs/UI";
    private const string CardPrefabPath = "Assets/Main folder/Prefabs/UI/Pnl_ProductionResourceCard.prefab";
    private const string WorkersPrefabPath = "Assets/Main folder/Prefabs/UI/Sec_Workers.prefab";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    static GenerateProductionContextUI()
    {
        EditorApplication.delayCall += OnReady;
    }

    private static void OnReady()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        BuildAll();
    }

    [MenuItem("Tools/Generate Production Context UI")]
    public static void BuildAll()
    {
        Debug.Log("[ProductionContextUI] Rozpoczynanie generowania szkieletu UI budynków produkcyjnych...");

        // 1. Otwórz scenę TESTSCENE_UI
        var currentScene = EditorSceneManager.GetActiveScene();
        if (currentScene.path != ScenePath)
        {
            currentScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // 2. Pobierz czcionkę LiberationSans SDF
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            font = TMP_Settings.defaultFontAsset;
        }

        // 3. Utwórz prefab Pnl_ProductionResourceCard
        GameObject cardPrefab = CreateProductionResourceCardPrefab(font);

        // 4. Załaduj prefab Sec_Workers
        GameObject workersPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorkersPrefabPath);
        if (workersPrefab == null)
        {
            Debug.LogWarning($"[ProductionContextUI] Nie znaleziono {WorkersPrefabPath}!");
        }

        // 5. Znajdź Canvas_ContextWindows
        GameObject canvasObj = GameObject.Find("Canvas_ContextWindows");
        if (canvasObj == null)
        {
            Debug.LogError("[ProductionContextUI] Nie znaleziono Canvas_ContextWindows na scenie!");
            return;
        }

        // Usuń stary Pnl_ProductionContext jeśli istnieje
        Transform oldPanel = canvasObj.transform.Find("Pnl_ProductionContext");
        if (oldPanel != null)
        {
            Undo.DestroyObjectImmediate(oldPanel.gameObject);
        }

        // 6. Zbuduj całe okno Pnl_ProductionContext
        GameObject prodContext = BuildProductionContextHierarchy(canvasObj.transform, font, cardPrefab, workersPrefab);

        // 7. Podepnij do CenterInfoPanelControllerUI
        CenterInfoPanelControllerUI centerController = Object.FindAnyObjectByType<CenterInfoPanelControllerUI>();
        if (centerController == null)
        {
            centerController = canvasObj.GetComponent<CenterInfoPanelControllerUI>();
            if (centerController == null)
            {
                centerController = canvasObj.AddComponent<CenterInfoPanelControllerUI>();
            }
        }

        if (centerController != null)
        {
            centerController.productionPanel = prodContext;
            EditorUtility.SetDirty(centerController);
        }

        // Domyślnie okno jest ukryte (SetActive false)
        prodContext.SetActive(false);

        // 8. Zapisz scenę i assety
        Undo.RegisterCreatedObjectUndo(prodContext, "Generate Production Context UI");
        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);
        AssetDatabase.SaveAssets();

        Debug.Log("[ProductionContextUI] Pomyślnie wygenerowano Pnl_ProductionContext na scenie TESTSCENE_UI!");
    }

    /// <summary>
    /// Tworzy prefab kafelka surowca z tabelarycznym zestawieniem składników kalkulacji.
    /// </summary>
    private static GameObject CreateProductionResourceCardPrefab(TMP_FontAsset font)
    {
        if (!Directory.Exists(PrefabFolder))
        {
            Directory.CreateDirectory(PrefabFolder);
        }

        // Główny kontener kafelka
        GameObject card = new GameObject("Pnl_ProductionResourceCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
        RectTransform cardRt = card.GetComponent<RectTransform>();
        cardRt.sizeDelta = new Vector2(250, 0);

        LayoutElement cardLe = card.GetComponent<LayoutElement>();
        cardLe.flexibleWidth = 1;
        cardLe.minWidth = 220;

        ContentSizeFitter csf = card.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Image cardImg = card.GetComponent<Image>();
        cardImg.sprite = null;
        cardImg.color = new Color(0.12f, 0.13f, 0.17f, 0.95f);

        VerticalLayoutGroup cardVlg = card.GetComponent<VerticalLayoutGroup>();
        cardVlg.padding = new RectOffset(10, 10, 8, 10);
        cardVlg.spacing = 6;
        cardVlg.childAlignment = TextAnchor.UpperCenter;
        cardVlg.childControlWidth = true;
        cardVlg.childControlHeight = false;
        cardVlg.childForceExpandWidth = true;
        cardVlg.childForceExpandHeight = false;

        // ── 1. Nagłówek kafelka (Ikona 24px + Nazwa surowca + Łączna produkcja) ──
        GameObject headerObj = new GameObject("Pnl_CardHeader", typeof(RectTransform), typeof(CanvasRenderer), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        headerObj.transform.SetParent(card.transform, false);

        LayoutElement headerLe = headerObj.GetComponent<LayoutElement>();
        headerLe.preferredHeight = 28;

        HorizontalLayoutGroup headerHlg = headerObj.GetComponent<HorizontalLayoutGroup>();
        headerHlg.spacing = 8;
        headerHlg.childAlignment = TextAnchor.MiddleLeft;
        headerHlg.childControlWidth = false;
        headerHlg.childControlHeight = false;
        headerHlg.childForceExpandWidth = false;
        headerHlg.childForceExpandHeight = false;

        // Ikona surowca (24x24 px - zgodnie ze standardem Small Icon)
        GameObject iconObj = new GameObject("Img_ResourceIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconObj.transform.SetParent(headerObj.transform, false);
        RectTransform iconRt = iconObj.GetComponent<RectTransform>();
        iconRt.sizeDelta = new Vector2(24, 24);
        LayoutElement iconLe = iconObj.GetComponent<LayoutElement>();
        iconLe.preferredWidth = 24;
        iconLe.preferredHeight = 24;
        Image iconImg = iconObj.GetComponent<Image>();
        iconImg.sprite = null;
        iconImg.color = new Color(1f, 1f, 1f, 0.4f);

        // Nazwa surowca (14 pt bold)
        GameObject nameObj = new GameObject("Txt_ResourceName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        nameObj.transform.SetParent(headerObj.transform, false);
        LayoutElement nameLe = nameObj.GetComponent<LayoutElement>();
        nameLe.flexibleWidth = 1;
        nameLe.preferredHeight = 24;
        TextMeshProUGUI nameTmp = nameObj.GetComponent<TextMeshProUGUI>();
        nameTmp.font = font;
        nameTmp.fontSize = 14;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.text = "DREWNO";
        nameTmp.color = Color.white;
        nameTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Końcowa produkcja na cykl (14 pt bold, zielony)
        GameObject totalObj = new GameObject("Txt_TotalProduction", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        totalObj.transform.SetParent(headerObj.transform, false);
        LayoutElement totalLe = totalObj.GetComponent<LayoutElement>();
        totalLe.preferredWidth = 90;
        totalLe.preferredHeight = 24;
        TextMeshProUGUI totalTmp = totalObj.GetComponent<TextMeshProUGUI>();
        totalTmp.font = font;
        totalTmp.fontSize = 14;
        totalTmp.fontStyle = FontStyles.Bold;
        totalTmp.text = "+12.0 / cykl";
        totalTmp.color = new Color(0.35f, 0.85f, 0.4f, 1f);
        totalTmp.alignment = TextAlignmentOptions.MidlineRight;

        // Cienka linia oddzielająca (Divider)
        GameObject dividerObj = new GameObject("Img_Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        dividerObj.transform.SetParent(card.transform, false);
        LayoutElement divLe = dividerObj.GetComponent<LayoutElement>();
        divLe.preferredHeight = 1;
        Image divImg = dividerObj.GetComponent<Image>();
        divImg.sprite = null;
        divImg.color = new Color(1f, 1f, 1f, 0.12f);

        // ── 2. Lista składników kalkulacji (Wiersze modyfikatorów) ──────────
        GameObject modContainer = new GameObject("Pnl_ModifiersList", typeof(RectTransform), typeof(VerticalLayoutGroup));
        modContainer.transform.SetParent(card.transform, false);

        VerticalLayoutGroup modVlg = modContainer.GetComponent<VerticalLayoutGroup>();
        modVlg.spacing = 3;
        modVlg.childAlignment = TextAnchor.UpperCenter;
        modVlg.childControlWidth = true;
        modVlg.childControlHeight = false;
        modVlg.childForceExpandWidth = true;
        modVlg.childForceExpandHeight = false;

        // Wiersze kalkulacji:
        // 1. Wartość bazowa
        CreateModifierRow(modContainer.transform, font, "Row_Base", "Wartość bazowa:", "+5.0", 12, false, new Color(0.85f, 0.85f, 0.85f, 1f));
        // 2. Bonus za postawienie na surowcu
        CreateModifierRow(modContainer.transform, font, "Row_OnTopBonus", "Złoże (pod budynkiem):", "+2.0", 12, false, new Color(0.4f, 0.85f, 0.45f, 1f));
        // 3. Bonus za surowce w pobliżu
        CreateModifierRow(modContainer.transform, font, "Row_AdjacentBonus", "Złoża w pobliżu:", "+3.0", 12, false, new Color(0.4f, 0.85f, 0.45f, 1f));
        // 4. Mnożniki zewnętrzne (ulepszenia / badania)
        CreateModifierRow(modContainer.transform, font, "Row_ExternalMultipliers", "Mnożnik ulepszeń / badań:", "x1.20", 12, false, new Color(0.45f, 0.75f, 1f, 1f));
        // 5. Wydajność załogi
        CreateModifierRow(modContainer.transform, font, "Row_CrewEfficiency", "Wydajność pracowników:", "100%", 12, false, new Color(1f, 0.85f, 0.3f, 1f));

        // Druga linia oddzielająca przed podsumowaniem
        GameObject divider2Obj = new GameObject("Img_DividerSummary", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        divider2Obj.transform.SetParent(card.transform, false);
        LayoutElement div2Le = divider2Obj.GetComponent<LayoutElement>();
        div2Le.preferredHeight = 1;
        Image div2Img = divider2Obj.GetComponent<Image>();
        div2Img.sprite = null;
        div2Img.color = new Color(1f, 1f, 1f, 0.12f);

        // 6. Wiersz podsumowania netto
        CreateModifierRow(card.transform, font, "Row_NetSummary", "Produkcja netto:", "+12.0 / cykl", 14, true, new Color(0.35f, 0.9f, 0.45f, 1f));

        // Zapisz jako prefab
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(card, CardPrefabPath.Replace("\\", "/"));
        Object.DestroyImmediate(card);
        return prefab;
    }

    /// <summary>
    /// Tworzy pojedynczy wiersz etykieta + wartość w tabeli kalkulacji.
    /// </summary>
    private static void CreateModifierRow(Transform parent, TMP_FontAsset font, string rowName, string labelText, string valueText, int fontSize, bool isBold, Color valueColor)
    {
        GameObject row = new GameObject(rowName, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);

        LayoutElement rowLe = row.GetComponent<LayoutElement>();
        rowLe.preferredHeight = fontSize + 6;

        HorizontalLayoutGroup rowHlg = row.GetComponent<HorizontalLayoutGroup>();
        rowHlg.spacing = 6;
        rowHlg.childAlignment = TextAnchor.MiddleRight;
        rowHlg.childControlWidth = false;
        rowHlg.childControlHeight = false;
        rowHlg.childForceExpandWidth = false;
        rowHlg.childForceExpandHeight = false;

        // Etykieta (12 pt lub 14 pt bold)
        GameObject lblObj = new GameObject("Txt_Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        lblObj.transform.SetParent(row.transform, false);
        LayoutElement lblLe = lblObj.GetComponent<LayoutElement>();
        lblLe.flexibleWidth = 1;
        lblLe.preferredHeight = fontSize + 4;
        TextMeshProUGUI lblTmp = lblObj.GetComponent<TextMeshProUGUI>();
        lblTmp.font = font;
        lblTmp.fontSize = fontSize;
        lblTmp.fontStyle = isBold ? FontStyles.Bold : FontStyles.Normal;
        lblTmp.text = labelText;
        lblTmp.color = isBold ? Color.white : new Color(0.7f, 0.72f, 0.75f, 1f);
        lblTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Wartość (12-14 pt)
        GameObject valObj = new GameObject("Txt_Value", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        valObj.transform.SetParent(row.transform, false);
        LayoutElement valLe = valObj.GetComponent<LayoutElement>();
        valLe.preferredWidth = 80;
        valLe.preferredHeight = fontSize + 4;
        TextMeshProUGUI valTmp = valObj.GetComponent<TextMeshProUGUI>();
        valTmp.font = font;
        valTmp.fontSize = fontSize;
        valTmp.fontStyle = isBold ? FontStyles.Bold : FontStyles.Normal;
        valTmp.text = valueText;
        valTmp.color = valueColor;
        valTmp.alignment = TextAlignmentOptions.MidlineRight;
    }

    /// <summary>
    /// Buduje pełne okno kontekstowe Pnl_ProductionContext.
    /// </summary>
    private static GameObject BuildProductionContextHierarchy(Transform parent, TMP_FontAsset font, GameObject cardPrefab, GameObject workersPrefab)
    {
        // ── Główny obiekt: Pnl_ProductionContext ───────────────────────────
        GameObject root = new GameObject("Pnl_ProductionContext", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        root.transform.SetParent(parent, false);

        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(1f, 0.5f);
        rootRt.anchorMax = new Vector2(1f, 0.5f);
        rootRt.pivot = new Vector2(1f, 0.5f);
        rootRt.anchoredPosition = new Vector2(-20f, 0f);
        rootRt.sizeDelta = new Vector2(560f, 0f);

        Image rootImg = root.GetComponent<Image>();
        rootImg.sprite = null;
        rootImg.color = new Color(0.08f, 0.09f, 0.12f, 0.96f);

        VerticalLayoutGroup rootVlg = root.GetComponent<VerticalLayoutGroup>();
        rootVlg.padding = new RectOffset(14, 14, 12, 14);
        rootVlg.spacing = 10;
        rootVlg.childAlignment = TextAnchor.UpperCenter;
        rootVlg.childControlWidth = true;
        rootVlg.childControlHeight = false;
        rootVlg.childForceExpandWidth = true;
        rootVlg.childForceExpandHeight = false;

        ContentSizeFitter rootCsf = root.GetComponent<ContentSizeFitter>();
        rootCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rootCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── 1. Pasek Górny (Pnl_TopBar) ───────────────────────────────────
        GameObject topBar = new GameObject("Pnl_TopBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        topBar.transform.SetParent(root.transform, false);

        LayoutElement topBarLe = topBar.GetComponent<LayoutElement>();
        topBarLe.preferredHeight = 34;

        HorizontalLayoutGroup topBarHlg = topBar.GetComponent<HorizontalLayoutGroup>();
        topBarHlg.spacing = 8;
        topBarHlg.childAlignment = TextAnchor.MiddleLeft;
        topBarHlg.childControlWidth = false;
        topBarHlg.childControlHeight = false;
        topBarHlg.childForceExpandWidth = false;
        topBarHlg.childForceExpandHeight = false;

        // Nazwa budynku (20 pt - zgodnie z GEMINI.md)
        GameObject nameObj = new GameObject("Txt_BuildingName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        nameObj.transform.SetParent(topBar.transform, false);
        LayoutElement nameLe = nameObj.GetComponent<LayoutElement>();
        nameLe.flexibleWidth = 1;
        nameLe.preferredHeight = 32;
        TextMeshProUGUI nameTmp = nameObj.GetComponent<TextMeshProUGUI>();
        nameTmp.font = font;
        nameTmp.fontSize = 20;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.text = "TARTAK";
        nameTmp.color = Color.white;
        nameTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Przycisk zamknięcia (Btn_Close)
        GameObject closeBtnObj = new GameObject("Btn_Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        closeBtnObj.transform.SetParent(topBar.transform, false);
        RectTransform closeRt = closeBtnObj.GetComponent<RectTransform>();
        closeRt.sizeDelta = new Vector2(28, 28);
        LayoutElement closeLe = closeBtnObj.GetComponent<LayoutElement>();
        closeLe.preferredWidth = 28;
        closeLe.preferredHeight = 28;
        Image closeImg = closeBtnObj.GetComponent<Image>();
        closeImg.sprite = null;
        closeImg.color = new Color(0.25f, 0.12f, 0.12f, 0.85f);

        GameObject closeTxtObj = new GameObject("Txt_Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
        RectTransform closeTxtRt = closeTxtObj.GetComponent<RectTransform>();
        closeTxtRt.anchorMin = Vector2.zero;
        closeTxtRt.anchorMax = Vector2.one;
        closeTxtRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI closeTmp = closeTxtObj.GetComponent<TextMeshProUGUI>();
        closeTmp.font = font;
        closeTmp.fontSize = 16;
        closeTmp.fontStyle = FontStyles.Bold;
        closeTmp.text = "X";
        closeTmp.color = Color.white;
        closeTmp.alignment = TextAlignmentOptions.Center;

        // ── 2. Kontener Kolumn (Pnl_ColumnsContainer) ─────────────────────
        GameObject columns = new GameObject("Pnl_ColumnsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        columns.transform.SetParent(root.transform, false);

        HorizontalLayoutGroup colsHlg = columns.GetComponent<HorizontalLayoutGroup>();
        colsHlg.spacing = 12;
        colsHlg.childAlignment = TextAnchor.UpperLeft;
        colsHlg.childControlWidth = true;
        colsHlg.childControlHeight = false;
        colsHlg.childForceExpandWidth = true;
        colsHlg.childForceExpandHeight = false;

        // ═════════════════════════════════════════════════════════════════
        // LEWA KOLUMNA: Utrzymanie + Załoga (Sec_Workers)
        // ═════════════════════════════════════════════════════════════════
        GameObject leftCol = new GameObject("Pnl_LeftColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        leftCol.transform.SetParent(columns.transform, false);

        LayoutElement leftColLe = leftCol.GetComponent<LayoutElement>();
        leftColLe.flexibleWidth = 1;

        VerticalLayoutGroup leftColVlg = leftCol.GetComponent<VerticalLayoutGroup>();
        leftColVlg.spacing = 8;
        leftColVlg.childAlignment = TextAnchor.UpperCenter;
        leftColVlg.childControlWidth = true;
        leftColVlg.childControlHeight = false;
        leftColVlg.childForceExpandWidth = true;
        leftColVlg.childForceExpandHeight = false;

        // Sekcja Utrzymania (Sec_Upkeep)
        BuildUpkeepSection(leftCol.transform, font);

        // Sekcja Załogi (Instancja Sec_Workers.prefab)
        if (workersPrefab != null)
        {
            GameObject workersInstance = (GameObject)PrefabUtility.InstantiatePrefab(workersPrefab, leftCol.transform);
            workersInstance.name = "Sec_Workers";
        }
        else
        {
            // Fallback placeholder jeśli prefabu nie znaleziono
            GameObject workersPlaceholder = new GameObject("Sec_Workers", typeof(RectTransform), typeof(VerticalLayoutGroup));
            workersPlaceholder.transform.SetParent(leftCol.transform, false);
        }

        // ═════════════════════════════════════════════════════════════════
        // PRAWA KOLUMNA: Bilans Produkcji (Sec_Production)
        // ═════════════════════════════════════════════════════════════════
        GameObject rightCol = new GameObject("Pnl_RightColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        rightCol.transform.SetParent(columns.transform, false);

        LayoutElement rightColLe = rightCol.GetComponent<LayoutElement>();
        rightColLe.flexibleWidth = 1.15f;

        VerticalLayoutGroup rightColVlg = rightCol.GetComponent<VerticalLayoutGroup>();
        rightColVlg.spacing = 8;
        rightColVlg.childAlignment = TextAnchor.UpperCenter;
        rightColVlg.childControlWidth = true;
        rightColVlg.childControlHeight = false;
        rightColVlg.childForceExpandWidth = true;
        rightColVlg.childForceExpandHeight = false;

        // Sekcja Produkcji (Sec_Production)
        GameObject secProd = new GameObject("Sec_Production", typeof(RectTransform), typeof(VerticalLayoutGroup));
        secProd.transform.SetParent(rightCol.transform, false);

        VerticalLayoutGroup secProdVlg = secProd.GetComponent<VerticalLayoutGroup>();
        secProdVlg.spacing = 8;
        secProdVlg.childAlignment = TextAnchor.UpperCenter;
        secProdVlg.childControlWidth = true;
        secProdVlg.childControlHeight = false;
        secProdVlg.childForceExpandWidth = true;
        secProdVlg.childForceExpandHeight = false;

        // Tytuł sekcji (16 pt - zgodnie z GEMINI.md)
        GameObject prodTitleObj = new GameObject("Txt_ProductionTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        prodTitleObj.transform.SetParent(secProd.transform, false);
        LayoutElement prodTitleLe = prodTitleObj.GetComponent<LayoutElement>();
        prodTitleLe.preferredHeight = 22;
        TextMeshProUGUI prodTitleTmp = prodTitleObj.GetComponent<TextMeshProUGUI>();
        prodTitleTmp.font = font;
        prodTitleTmp.fontSize = 16;
        prodTitleTmp.fontStyle = FontStyles.Bold;
        prodTitleTmp.text = "BILANS PRODUKCJI";
        prodTitleTmp.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        prodTitleTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Kontener na kafelki surowców
        GameObject cardsContainer = new GameObject("Pnl_ProductionCardsContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
        cardsContainer.transform.SetParent(secProd.transform, false);

        VerticalLayoutGroup cardsVlg = cardsContainer.GetComponent<VerticalLayoutGroup>();
        cardsVlg.spacing = 8;
        cardsVlg.childAlignment = TextAnchor.UpperCenter;
        cardsVlg.childControlWidth = true;
        cardsVlg.childControlHeight = false;
        cardsVlg.childForceExpandWidth = true;
        cardsVlg.childForceExpandHeight = false;

        // Instancja kafelka z prefabu (przykładowy kafelek 1: Drewno)
        if (cardPrefab != null)
        {
            GameObject cardInstance = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, cardsContainer.transform);
            cardInstance.name = "Pnl_ProductionResourceCard_Main";
        }

        // ── 3. Dolna Belka Akcji (Pnl_BottomBar: WSTRZYMAJ / WYBURZ) ───────
        GameObject bottomBar = new GameObject("Pnl_BottomBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        bottomBar.transform.SetParent(root.transform, false);

        LayoutElement bottomBarLe = bottomBar.GetComponent<LayoutElement>();
        bottomBarLe.preferredHeight = 36;

        HorizontalLayoutGroup bottomHlg = bottomBar.GetComponent<HorizontalLayoutGroup>();
        bottomHlg.spacing = 10;
        bottomHlg.childAlignment = TextAnchor.MiddleCenter;
        bottomHlg.childControlWidth = true;
        bottomHlg.childControlHeight = true;
        bottomHlg.childForceExpandWidth = true;
        bottomHlg.childForceExpandHeight = true;

        // Przycisk WSTRZYMAJ (14 pt bold, bursztynowy)
        GameObject pauseBtnObj = new GameObject("Btn_Pause", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        pauseBtnObj.transform.SetParent(bottomBar.transform, false);
        Image pauseImg = pauseBtnObj.GetComponent<Image>();
        pauseImg.sprite = null;
        pauseImg.color = new Color(0.28f, 0.22f, 0.12f, 0.95f);

        GameObject pauseTxtObj = new GameObject("Txt_Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        pauseTxtObj.transform.SetParent(pauseBtnObj.transform, false);
        RectTransform pauseTxtRt = pauseTxtObj.GetComponent<RectTransform>();
        pauseTxtRt.anchorMin = Vector2.zero;
        pauseTxtRt.anchorMax = Vector2.one;
        pauseTxtRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI pauseTmp = pauseTxtObj.GetComponent<TextMeshProUGUI>();
        pauseTmp.font = font;
        pauseTmp.fontSize = 14;
        pauseTmp.fontStyle = FontStyles.Bold;
        pauseTmp.text = "WSTRZYMAJ";
        pauseTmp.color = new Color(1f, 0.85f, 0.4f, 1f);
        pauseTmp.alignment = TextAlignmentOptions.Center;

        // Przycisk WYBURZ (14 pt bold, czerwony)
        GameObject demolishBtnObj = new GameObject("Btn_Demolish", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        demolishBtnObj.transform.SetParent(bottomBar.transform, false);
        Image demolishImg = demolishBtnObj.GetComponent<Image>();
        demolishImg.sprite = null;
        demolishImg.color = new Color(0.32f, 0.12f, 0.12f, 0.95f);

        GameObject demolishTxtObj = new GameObject("Txt_Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        demolishTxtObj.transform.SetParent(demolishBtnObj.transform, false);
        RectTransform demolishTxtRt = demolishTxtObj.GetComponent<RectTransform>();
        demolishTxtRt.anchorMin = Vector2.zero;
        demolishTxtRt.anchorMax = Vector2.one;
        demolishTxtRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI demolishTmp = demolishTxtObj.GetComponent<TextMeshProUGUI>();
        demolishTmp.font = font;
        demolishTmp.fontSize = 14;
        demolishTmp.fontStyle = FontStyles.Bold;
        demolishTmp.text = "WYBURZ";
        demolishTmp.color = new Color(1f, 0.45f, 0.45f, 1f);
        demolishTmp.alignment = TextAlignmentOptions.Center;

        return root;
    }

    /// <summary>
    /// Buduje sekcję kosztów utrzymania (Sec_Upkeep).
    /// </summary>
    private static void BuildUpkeepSection(Transform parent, TMP_FontAsset font)
    {
        GameObject upkeep = new GameObject("Sec_Upkeep", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
        upkeep.transform.SetParent(parent, false);

        Image upkeepImg = upkeep.GetComponent<Image>();
        upkeepImg.sprite = null;
        upkeepImg.color = new Color(0.12f, 0.13f, 0.16f, 0.85f);

        VerticalLayoutGroup upkeepVlg = upkeep.GetComponent<VerticalLayoutGroup>();
        upkeepVlg.padding = new RectOffset(8, 8, 6, 8);
        upkeepVlg.spacing = 6;
        upkeepVlg.childAlignment = TextAnchor.UpperLeft;
        upkeepVlg.childControlWidth = true;
        upkeepVlg.childControlHeight = false;
        upkeepVlg.childForceExpandWidth = true;
        upkeepVlg.childForceExpandHeight = false;

        // Tytuł sekcji (16 pt)
        GameObject titleObj = new GameObject("Txt_UpkeepTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        titleObj.transform.SetParent(upkeep.transform, false);
        LayoutElement titleLe = titleObj.GetComponent<LayoutElement>();
        titleLe.preferredHeight = 22;
        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.fontSize = 16;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.text = "KOSZTY UTRZYMANIA";
        titleTmp.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        titleTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Kontener na zasoby
        GameObject resContainer = new GameObject("Pnl_UpkeepResources", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        resContainer.transform.SetParent(upkeep.transform, false);

        HorizontalLayoutGroup resHlg = resContainer.GetComponent<HorizontalLayoutGroup>();
        resHlg.spacing = 8;
        resHlg.childAlignment = TextAnchor.MiddleLeft;
        resHlg.childControlWidth = false;
        resHlg.childControlHeight = false;
        resHlg.childForceExpandWidth = false;
        resHlg.childForceExpandHeight = false;

        // Przykładowy kafelek kosztu (Ikona 24px + Wartość 13 pt)
        GameObject costItem = new GameObject("Pnl_UpkeepCostItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HorizontalLayoutGroup));
        costItem.transform.SetParent(resContainer.transform, false);

        Image costBg = costItem.GetComponent<Image>();
        costBg.sprite = null;
        costBg.color = new Color(0.18f, 0.19f, 0.22f, 0.9f);

        HorizontalLayoutGroup costHlg = costItem.GetComponent<HorizontalLayoutGroup>();
        costHlg.padding = new RectOffset(6, 8, 4, 4);
        costHlg.spacing = 6;
        costHlg.childAlignment = TextAnchor.MiddleLeft;
        costHlg.childControlWidth = false;
        costHlg.childControlHeight = false;
        costHlg.childForceExpandWidth = false;
        costHlg.childForceExpandHeight = false;

        // Ikona zasobu (24x24 px)
        GameObject iconObj = new GameObject("Img_ResourceIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconObj.transform.SetParent(costItem.transform, false);
        RectTransform iconRt = iconObj.GetComponent<RectTransform>();
        iconRt.sizeDelta = new Vector2(24, 24);
        LayoutElement iconLe = iconObj.GetComponent<LayoutElement>();
        iconLe.preferredWidth = 24;
        iconLe.preferredHeight = 24;
        Image iconImg = iconObj.GetComponent<Image>();
        iconImg.sprite = null;
        iconImg.color = new Color(1f, 1f, 1f, 0.4f);

        // Kwota
        GameObject amtObj = new GameObject("Txt_Amount", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        amtObj.transform.SetParent(costItem.transform, false);
        LayoutElement amtLe = amtObj.GetComponent<LayoutElement>();
        amtLe.preferredHeight = 20;
        TextMeshProUGUI amtTmp = amtObj.GetComponent<TextMeshProUGUI>();
        amtTmp.font = font;
        amtTmp.fontSize = 13;
        amtTmp.fontStyle = FontStyles.Bold;
        amtTmp.text = "-1";
        amtTmp.color = new Color(0.95f, 0.4f, 0.4f, 1f);
        amtTmp.alignment = TextAlignmentOptions.MidlineLeft;
    }
}
