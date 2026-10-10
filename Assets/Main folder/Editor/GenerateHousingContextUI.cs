using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SetupHousingContextWindowComponent
{
    [MenuItem("Tools/Attach HousingContextWindowUI To Scene")]
    public static void AttachAndConfigure()
    {
        string scenePath = "Assets/Main folder/_Scenes/TESTSCENE_UI.unity";
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != scenePath)
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        GameObject root = GameObject.Find("Canvas_ContextWindows/Pnl_HousingContext");
        if (root == null)
        {
            Debug.LogError("[HousingContextUI] Nie znaleziono Pnl_HousingContext w Canvas_ContextWindows!");
            return;
        }

        HousingContextWindowUI ui = root.GetComponent<HousingContextWindowUI>();
        if (ui == null)
        {
            ui = root.AddComponent<HousingContextWindowUI>();
        }

        ui.AutoBindReferences();

        CenterInfoPanelControllerUI centerController = Object.FindAnyObjectByType<CenterInfoPanelControllerUI>();
        if (centerController == null)
        {
            GameObject canvas = GameObject.Find("Canvas_ContextWindows");
            if (canvas != null)
            {
                centerController = canvas.GetComponent<CenterInfoPanelControllerUI>();
                if (centerController == null)
                {
                    centerController = canvas.AddComponent<CenterInfoPanelControllerUI>();
                }
            }
        }

        if (centerController != null)
        {
            centerController.housingPanel = root;
        }

        EditorUtility.SetDirty(root);
        if (centerController != null) EditorUtility.SetDirty(centerController);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[HousingContextUI] Pomyślnie podpięto HousingContextWindowUI do Pnl_HousingContext i skonfigurowano CenterInfoPanelControllerUI!");
    }
}

[InitializeOnLoad]
public static class GenerateHousingContextUI
{
    private const string SessionKey = "HousingContextUI_Built_v1";
    private const string ScenePath = "Assets/Main folder/_Scenes/TESTSCENE_UI.unity";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    static GenerateHousingContextUI()
    {
        EditorApplication.delayCall += OnReady;
    }

    private static void OnReady()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        BuildAll();
    }

    [MenuItem("Tools/Generate Housing Context UI")]
    public static void BuildAll()
    {
        Debug.Log("[HousingContextUI] Rozpoczynanie generowania szkieletu UI budynków mieszkalnych...");

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

        // 3. Znajdź Canvas_ContextWindows
        GameObject canvasObj = GameObject.Find("Canvas_ContextWindows");
        if (canvasObj == null)
        {
            Debug.LogError("[HousingContextUI] Nie znaleziono Canvas_ContextWindows na scenie!");
            return;
        }

        // Usuń stary Pnl_HousingContext jeśli istnieje
        Transform oldPanel = canvasObj.transform.Find("Pnl_HousingContext");
        if (oldPanel != null)
        {
            Undo.DestroyObjectImmediate(oldPanel.gameObject);
        }

        // 4. Zbuduj całe okno Pnl_HousingContext
        GameObject housingContext = BuildHousingContextHierarchy(canvasObj.transform, font);

        // 5. Podepnij i skonfiguruj HousingContextWindowUI
        HousingContextWindowUI housingUI = housingContext.GetComponent<HousingContextWindowUI>();
        if (housingUI == null)
        {
            housingUI = housingContext.AddComponent<HousingContextWindowUI>();
        }
        housingUI.AutoBindReferences();
        EditorUtility.SetDirty(housingContext);

        // 6. Podepnij do CenterInfoPanelControllerUI
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
            centerController.housingPanel = housingContext;
            EditorUtility.SetDirty(centerController);
        }

        // Domyślnie okno jest ukryte (SetActive false)
        housingContext.SetActive(false);

        // 6. Zapisz scenę i assety
        Undo.RegisterCreatedObjectUndo(housingContext, "Generate Housing Context UI");
        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);
        AssetDatabase.SaveAssets();

        Debug.Log("[HousingContextUI] Pomyślnie wygenerowano Pnl_HousingContext na scenie TESTSCENE_UI!");
    }

    /// <summary>
    /// Buduje pełną hierarchię UI okna Pnl_HousingContext.
    /// </summary>
    private static GameObject BuildHousingContextHierarchy(Transform parent, TMP_FontAsset font)
    {
        // ── Główny obiekt: Pnl_HousingContext (szerokość 480 px, prawy brzeg) ──
        GameObject root = new GameObject("Pnl_HousingContext", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        root.transform.SetParent(parent, false);

        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(1f, 0.5f);
        rootRt.anchorMax = new Vector2(1f, 0.5f);
        rootRt.pivot = new Vector2(1f, 0.5f);
        rootRt.anchoredPosition = new Vector2(-20f, 0f);
        rootRt.sizeDelta = new Vector2(480f, 0f);

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
        nameTmp.text = "CHATA KRASNOLUDÓW";
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

        // ── 2. Sekcja Populacji i Pojemności (Sec_Population) ──────────────
        BuildPopulationSection(root.transform, font);

        // ── 3. Sekcja Kosztów Utrzymania (Sec_Upkeep) ──────────────────────
        BuildUpkeepSection(root.transform, font);

        // ── 4. Sekcja Progresji Wzrostu (Sec_GrowthProgress) ──────────────
        BuildGrowthProgressSection(root.transform, font);

        // ── 5. Dolna Belka Akcji: WYBURZ (Pnl_BottomBar) ───────────────────
        BuildBottomBar(root.transform, font);

        return root;
    }

    /// <summary>
    /// Buduje sekcję populacji i pojemności (ikona rasy, nazwa, licznik 2/5, pasek zapełnienia).
    /// </summary>
    private static void BuildPopulationSection(Transform parent, TMP_FontAsset font)
    {
        GameObject secPop = new GameObject("Sec_Population", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        secPop.transform.SetParent(parent, false);

        Image secPopImg = secPop.GetComponent<Image>();
        secPopImg.sprite = null;
        secPopImg.color = new Color(0.12f, 0.13f, 0.17f, 0.95f);

        VerticalLayoutGroup secPopVlg = secPop.GetComponent<VerticalLayoutGroup>();
        secPopVlg.padding = new RectOffset(12, 12, 10, 12);
        secPopVlg.spacing = 8;
        secPopVlg.childAlignment = TextAnchor.UpperLeft;
        secPopVlg.childControlWidth = true;
        secPopVlg.childControlHeight = false;
        secPopVlg.childForceExpandWidth = true;
        secPopVlg.childForceExpandHeight = false;

        ContentSizeFitter secPopCsf = secPop.GetComponent<ContentSizeFitter>();
        secPopCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        secPopCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Wiersz nagłówka populacji: Ikona Rasy (24px) + Nazwa Rasy (16 pt) + Licznik (16 pt)
        GameObject headerRow = new GameObject("Pnl_PopHeaderRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        headerRow.transform.SetParent(secPop.transform, false);

        LayoutElement headerRowLe = headerRow.GetComponent<LayoutElement>();
        headerRowLe.preferredHeight = 28;

        HorizontalLayoutGroup headerHlg = headerRow.GetComponent<HorizontalLayoutGroup>();
        headerHlg.spacing = 8;
        headerHlg.childAlignment = TextAnchor.MiddleLeft;
        headerHlg.childControlWidth = false;
        headerHlg.childControlHeight = false;
        headerHlg.childForceExpandWidth = false;
        headerHlg.childForceExpandHeight = false;

        // Ikona rasy (24x24 px - zgodnie ze standardem Small Icon)
        GameObject iconObj = new GameObject("Img_RaceIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconObj.transform.SetParent(headerRow.transform, false);
        RectTransform iconRt = iconObj.GetComponent<RectTransform>();
        iconRt.sizeDelta = new Vector2(24, 24);
        LayoutElement iconLe = iconObj.GetComponent<LayoutElement>();
        iconLe.preferredWidth = 24;
        iconLe.preferredHeight = 24;
        Image iconImg = iconObj.GetComponent<Image>();
        iconImg.sprite = null;
        iconImg.color = new Color(1f, 1f, 1f, 0.4f);

        // Nazwa rasy (16 pt Bold)
        GameObject raceObj = new GameObject("Txt_RaceName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        raceObj.transform.SetParent(headerRow.transform, false);
        LayoutElement raceLe = raceObj.GetComponent<LayoutElement>();
        raceLe.flexibleWidth = 1;
        raceLe.preferredHeight = 26;
        TextMeshProUGUI raceTmp = raceObj.GetComponent<TextMeshProUGUI>();
        raceTmp.font = font;
        raceTmp.fontSize = 16;
        raceTmp.fontStyle = FontStyles.Bold;
        raceTmp.text = "KRASNOLUDY";
        raceTmp.color = new Color(0.92f, 0.86f, 0.69f, 1f);
        raceTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Licznik mieszkańców (16 pt Bold)
        GameObject countObj = new GameObject("Txt_PopulationCount", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        countObj.transform.SetParent(headerRow.transform, false);
        LayoutElement countLe = countObj.GetComponent<LayoutElement>();
        countLe.preferredWidth = 160;
        countLe.preferredHeight = 26;
        TextMeshProUGUI countTmp = countObj.GetComponent<TextMeshProUGUI>();
        countTmp.font = font;
        countTmp.fontSize = 16;
        countTmp.fontStyle = FontStyles.Bold;
        countTmp.text = "Mieszkańcy: 2 / 5";
        countTmp.color = Color.white;
        countTmp.alignment = TextAlignmentOptions.MidlineRight;

        // Pasek zapełnienia miejsc (Capacity Bar: wysokość 8 px)
        GameObject barObj = new GameObject("Pnl_CapacityBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        barObj.transform.SetParent(secPop.transform, false);

        LayoutElement barLe = barObj.GetComponent<LayoutElement>();
        barLe.preferredHeight = 8;

        Image barBg = barObj.GetComponent<Image>();
        barBg.sprite = null;
        barBg.color = new Color(0.18f, 0.2f, 0.25f, 0.9f);

        // Wypełnienie paska
        GameObject fillObj = new GameObject("Img_CapacityFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObj.transform.SetParent(barObj.transform, false);
        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0.4f, 1f); // 40% (2 z 5)
        fillRt.sizeDelta = Vector2.zero;
        fillRt.anchoredPosition = Vector2.zero;

        Image fillImg = fillObj.GetComponent<Image>();
        fillImg.sprite = null;
        fillImg.color = new Color(0.29f, 0.66f, 0.85f, 1f); // Błękit / Turkus
    }

    /// <summary>
    /// Buduje sekcję kosztów utrzymania z dwoma podblokami (Utrzymanie mieszkańców + Koszty wzrostu).
    /// </summary>
    private static void BuildUpkeepSection(Transform parent, TMP_FontAsset font)
    {
        GameObject secUpkeep = new GameObject("Sec_Upkeep", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        secUpkeep.transform.SetParent(parent, false);

        Image secUpkeepImg = secUpkeep.GetComponent<Image>();
        secUpkeepImg.sprite = null;
        secUpkeepImg.color = new Color(0.12f, 0.13f, 0.17f, 0.95f);

        VerticalLayoutGroup secUpkeepVlg = secUpkeep.GetComponent<VerticalLayoutGroup>();
        secUpkeepVlg.padding = new RectOffset(12, 12, 10, 12);
        secUpkeepVlg.spacing = 10;
        secUpkeepVlg.childAlignment = TextAnchor.UpperLeft;
        secUpkeepVlg.childControlWidth = true;
        secUpkeepVlg.childControlHeight = false;
        secUpkeepVlg.childForceExpandWidth = true;
        secUpkeepVlg.childForceExpandHeight = false;

        ContentSizeFitter secUpkeepCsf = secUpkeep.GetComponent<ContentSizeFitter>();
        secUpkeepCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        secUpkeepCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Tytuł sekcji (16 pt Bold)
        GameObject titleObj = new GameObject("Txt_UpkeepTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        titleObj.transform.SetParent(secUpkeep.transform, false);
        LayoutElement titleLe = titleObj.GetComponent<LayoutElement>();
        titleLe.preferredHeight = 22;
        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.fontSize = 16;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.text = "KOSZTY UTRZYMANIA";
        titleTmp.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        titleTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // ── Podblok 1: Utrzymanie mieszkańców ─────────────────────────────
        GameObject residentSub = new GameObject("Pnl_ResidentUpkeep", typeof(RectTransform), typeof(VerticalLayoutGroup));
        residentSub.transform.SetParent(secUpkeep.transform, false);

        VerticalLayoutGroup resVlg = residentSub.GetComponent<VerticalLayoutGroup>();
        resVlg.spacing = 6;
        resVlg.childAlignment = TextAnchor.UpperLeft;
        resVlg.childControlWidth = true;
        resVlg.childControlHeight = false;
        resVlg.childForceExpandWidth = true;
        resVlg.childForceExpandHeight = false;

        // Etykieta (13 pt)
        GameObject resLblObj = new GameObject("Txt_ResidentUpkeepLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        resLblObj.transform.SetParent(residentSub.transform, false);
        LayoutElement resLblLe = resLblObj.GetComponent<LayoutElement>();
        resLblLe.preferredHeight = 18;
        TextMeshProUGUI resLblTmp = resLblObj.GetComponent<TextMeshProUGUI>();
        resLblTmp.font = font;
        resLblTmp.fontSize = 13;
        resLblTmp.text = "Utrzymanie mieszkańców (na osobę / świt):";
        resLblTmp.color = new Color(0.7f, 0.72f, 0.75f, 1f);
        resLblTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Kontener kafelków kosztów per pop
        GameObject resItemsObj = new GameObject("Pnl_ResidentUpkeepItems", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        resItemsObj.transform.SetParent(residentSub.transform, false);
        HorizontalLayoutGroup resItemsHlg = resItemsObj.GetComponent<HorizontalLayoutGroup>();
        resItemsHlg.spacing = 8;
        resItemsHlg.childAlignment = TextAnchor.MiddleLeft;
        resItemsHlg.childControlWidth = false;
        resItemsHlg.childControlHeight = false;
        resItemsHlg.childForceExpandWidth = false;
        resItemsHlg.childForceExpandHeight = false;

        // Przykładowy kafelek kosztu (-1)
        CreateCostItem(resItemsObj.transform, font, "Pnl_CostItem_Food", "-1");

        // Etykieta sumaryczna łącznego kosztu
        GameObject resTotalObj = new GameObject("Txt_ResidentUpkeepTotal", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        resTotalObj.transform.SetParent(residentSub.transform, false);
        LayoutElement resTotalLe = resTotalObj.GetComponent<LayoutElement>();
        resTotalLe.preferredHeight = 18;
        TextMeshProUGUI resTotalTmp = resTotalObj.GetComponent<TextMeshProUGUI>();
        resTotalTmp.font = font;
        resTotalTmp.fontSize = 12;
        resTotalTmp.text = "Łączny koszt: -2 Żywność / świt";
        resTotalTmp.color = new Color(0.85f, 0.65f, 0.65f, 1f);
        resTotalTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Linia oddzielająca (Divider)
        GameObject divObj = new GameObject("Img_DividerUpkeep", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        divObj.transform.SetParent(secUpkeep.transform, false);
        LayoutElement divLe = divObj.GetComponent<LayoutElement>();
        divLe.preferredHeight = 1;
        Image divImg = divObj.GetComponent<Image>();
        divImg.sprite = null;
        divImg.color = new Color(1f, 1f, 1f, 0.12f);

        // ── Podblok 2: Koszty wzrostu populacji ───────────────────────────
        GameObject growthSub = new GameObject("Pnl_GrowthUpkeep", typeof(RectTransform), typeof(VerticalLayoutGroup));
        growthSub.transform.SetParent(secUpkeep.transform, false);

        VerticalLayoutGroup growthVlg = growthSub.GetComponent<VerticalLayoutGroup>();
        growthVlg.spacing = 6;
        growthVlg.childAlignment = TextAnchor.UpperLeft;
        growthVlg.childControlWidth = true;
        growthVlg.childControlHeight = false;
        growthVlg.childForceExpandWidth = true;
        growthVlg.childForceExpandHeight = false;

        // Wiersz nagłówkowy wzrostu: Etykieta (13 pt) + Przycisk WZROST: AKTYWNY
        GameObject growthHeaderRow = new GameObject("Pnl_GrowthHeaderRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        growthHeaderRow.transform.SetParent(growthSub.transform, false);

        LayoutElement growthHeaderLe = growthHeaderRow.GetComponent<LayoutElement>();
        growthHeaderLe.preferredHeight = 28;

        HorizontalLayoutGroup growthHeaderHlg = growthHeaderRow.GetComponent<HorizontalLayoutGroup>();
        growthHeaderHlg.spacing = 8;
        growthHeaderHlg.childAlignment = TextAnchor.MiddleLeft;
        growthHeaderHlg.childControlWidth = false;
        growthHeaderHlg.childControlHeight = false;
        growthHeaderHlg.childForceExpandWidth = false;
        growthHeaderHlg.childForceExpandHeight = false;

        GameObject growthLblObj = new GameObject("Txt_GrowthUpkeepLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        growthLblObj.transform.SetParent(growthHeaderRow.transform, false);
        LayoutElement growthLblLe = growthLblObj.GetComponent<LayoutElement>();
        growthLblLe.flexibleWidth = 1;
        growthLblLe.preferredHeight = 24;
        TextMeshProUGUI growthLblTmp = growthLblObj.GetComponent<TextMeshProUGUI>();
        growthLblTmp.font = font;
        growthLblTmp.fontSize = 13;
        growthLblTmp.text = "Koszty wzrostu populacji (na cykl):";
        growthLblTmp.color = new Color(0.7f, 0.72f, 0.75f, 1f);
        growthLblTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Przycisk stanu wzrostu (Btn_ToggleGrowth)
        GameObject growthBtnObj = new GameObject("Btn_ToggleGrowth", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        growthBtnObj.transform.SetParent(growthHeaderRow.transform, false);
        RectTransform growthBtnRt = growthBtnObj.GetComponent<RectTransform>();
        growthBtnRt.sizeDelta = new Vector2(160, 26);
        LayoutElement growthBtnLe = growthBtnObj.GetComponent<LayoutElement>();
        growthBtnLe.preferredWidth = 160;
        growthBtnLe.preferredHeight = 26;
        Image growthBtnImg = growthBtnObj.GetComponent<Image>();
        growthBtnImg.sprite = null;
        growthBtnImg.color = new Color(0.14f, 0.28f, 0.18f, 0.95f); // Ciemnozielony aktywny

        GameObject growthBtnTxtObj = new GameObject("Txt_GrowthStatusLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        growthBtnTxtObj.transform.SetParent(growthBtnObj.transform, false);
        RectTransform growthBtnTxtRt = growthBtnTxtObj.GetComponent<RectTransform>();
        growthBtnTxtRt.anchorMin = Vector2.zero;
        growthBtnTxtRt.anchorMax = Vector2.one;
        growthBtnTxtRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI growthBtnTmp = growthBtnTxtObj.GetComponent<TextMeshProUGUI>();
        growthBtnTmp.font = font;
        growthBtnTmp.fontSize = 12;
        growthBtnTmp.fontStyle = FontStyles.Bold;
        growthBtnTmp.text = "WZROST: AKTYWNY";
        growthBtnTmp.color = new Color(0.45f, 0.95f, 0.55f, 1f);
        growthBtnTmp.alignment = TextAlignmentOptions.Center;

        // Kontener kafelków kosztów wzrostu
        GameObject growthItemsObj = new GameObject("Pnl_GrowthCostItems", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        growthItemsObj.transform.SetParent(growthSub.transform, false);
        HorizontalLayoutGroup growthItemsHlg = growthItemsObj.GetComponent<HorizontalLayoutGroup>();
        growthItemsHlg.spacing = 8;
        growthItemsHlg.childAlignment = TextAnchor.MiddleLeft;
        growthItemsHlg.childControlWidth = false;
        growthItemsHlg.childControlHeight = false;
        growthItemsHlg.childForceExpandWidth = false;
        growthItemsHlg.childForceExpandHeight = false;

        // Przykładowe kafelki kosztu wzrostu (-2, -1)
        CreateCostItem(growthItemsObj.transform, font, "Pnl_CostItem_Growth1", "-2");
        CreateCostItem(growthItemsObj.transform, font, "Pnl_CostItem_Growth2", "-1");
    }

    /// <summary>
    /// Buduje sekcję paska progresji wzrostu populacji (bez sfer).
    /// </summary>
    private static void BuildGrowthProgressSection(Transform parent, TMP_FontAsset font)
    {
        GameObject secProgress = new GameObject("Sec_GrowthProgress", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        secProgress.transform.SetParent(parent, false);

        Image secProgressImg = secProgress.GetComponent<Image>();
        secProgressImg.sprite = null;
        secProgressImg.color = new Color(0.12f, 0.13f, 0.17f, 0.95f);

        VerticalLayoutGroup secProgVlg = secProgress.GetComponent<VerticalLayoutGroup>();
        secProgVlg.padding = new RectOffset(12, 12, 10, 12);
        secProgVlg.spacing = 8;
        secProgVlg.childAlignment = TextAnchor.UpperLeft;
        secProgVlg.childControlWidth = true;
        secProgVlg.childControlHeight = false;
        secProgVlg.childForceExpandWidth = true;
        secProgVlg.childForceExpandHeight = false;

        ContentSizeFitter secProgCsf = secProgress.GetComponent<ContentSizeFitter>();
        secProgCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        secProgCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Wiersz nagłówka progresji: Tytuł (16 pt) + Wymagane Cykle (14 pt)
        GameObject progHeaderRow = new GameObject("Pnl_ProgHeaderRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        progHeaderRow.transform.SetParent(secProgress.transform, false);

        LayoutElement progHeaderLe = progHeaderRow.GetComponent<LayoutElement>();
        progHeaderLe.preferredHeight = 26;

        HorizontalLayoutGroup progHeaderHlg = progHeaderRow.GetComponent<HorizontalLayoutGroup>();
        progHeaderHlg.spacing = 8;
        progHeaderHlg.childAlignment = TextAnchor.MiddleLeft;
        progHeaderHlg.childControlWidth = false;
        progHeaderHlg.childControlHeight = false;
        progHeaderHlg.childForceExpandWidth = false;
        progHeaderHlg.childForceExpandHeight = false;

        // Tytuł sekcji (16 pt Bold)
        GameObject progTitleObj = new GameObject("Txt_ProgTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        progTitleObj.transform.SetParent(progHeaderRow.transform, false);
        LayoutElement progTitleLe = progTitleObj.GetComponent<LayoutElement>();
        progTitleLe.flexibleWidth = 1;
        progTitleLe.preferredHeight = 24;
        TextMeshProUGUI progTitleTmp = progTitleObj.GetComponent<TextMeshProUGUI>();
        progTitleTmp.font = font;
        progTitleTmp.fontSize = 16;
        progTitleTmp.fontStyle = FontStyles.Bold;
        progTitleTmp.text = "PROGRESJA WZROSTU POPULACJI";
        progTitleTmp.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        progTitleTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Wymagane cykle (14 pt Bold)
        GameObject cyclesReqObj = new GameObject("Txt_CyclesRequired", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        cyclesReqObj.transform.SetParent(progHeaderRow.transform, false);
        LayoutElement cyclesReqLe = cyclesReqObj.GetComponent<LayoutElement>();
        cyclesReqLe.preferredWidth = 150;
        cyclesReqLe.preferredHeight = 24;
        TextMeshProUGUI cyclesReqTmp = cyclesReqObj.GetComponent<TextMeshProUGUI>();
        cyclesReqTmp.font = font;
        cyclesReqTmp.fontSize = 14;
        cyclesReqTmp.fontStyle = FontStyles.Bold;
        cyclesReqTmp.text = "WYMAGANE: 4 CYKLE";
        cyclesReqTmp.color = new Color(0.96f, 0.77f, 0.26f, 1f); // Złocisty akcent
        cyclesReqTmp.alignment = TextAlignmentOptions.MidlineRight;

        // Poziomy pasek postępu (Pnl_GrowthProgressBar: wysokość 14 px)
        GameObject barObj = new GameObject("Pnl_GrowthProgressBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        barObj.transform.SetParent(secProgress.transform, false);

        LayoutElement barLe = barObj.GetComponent<LayoutElement>();
        barLe.preferredHeight = 14;

        Image barBg = barObj.GetComponent<Image>();
        barBg.sprite = null;
        barBg.color = new Color(0.18f, 0.2f, 0.25f, 0.9f);

        // Wypełnienie paska (Img_GrowthProgressFill)
        GameObject fillObj = new GameObject("Img_GrowthProgressFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObj.transform.SetParent(barObj.transform, false);
        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0.5f, 1f); // 50% postępu (2 z 4)
        fillRt.sizeDelta = Vector2.zero;
        fillRt.anchoredPosition = Vector2.zero;

        Image fillImg = fillObj.GetComponent<Image>();
        fillImg.sprite = null;
        fillImg.color = new Color(0.28f, 0.78f, 0.45f, 1f); // Zieleń akcentu

        // Wiersz podpisów pod paskiem: Status z lewej (13 pt bold) + Tempo z prawej (11 pt italic)
        GameObject labelsRow = new GameObject("Pnl_ProgressLabels", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        labelsRow.transform.SetParent(secProgress.transform, false);

        LayoutElement labelsRowLe = labelsRow.GetComponent<LayoutElement>();
        labelsRowLe.preferredHeight = 20;

        HorizontalLayoutGroup labelsHlg = labelsRow.GetComponent<HorizontalLayoutGroup>();
        labelsHlg.spacing = 8;
        labelsHlg.childAlignment = TextAnchor.MiddleLeft;
        labelsHlg.childControlWidth = false;
        labelsHlg.childControlHeight = false;
        labelsHlg.childForceExpandWidth = false;
        labelsHlg.childForceExpandHeight = false;

        // Lewa etykieta (13 pt bold)
        GameObject statusObj = new GameObject("Txt_ProgressStatus", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        statusObj.transform.SetParent(labelsRow.transform, false);
        LayoutElement statusLe = statusObj.GetComponent<LayoutElement>();
        statusLe.flexibleWidth = 1;
        statusLe.preferredHeight = 20;
        TextMeshProUGUI statusTmp = statusObj.GetComponent<TextMeshProUGUI>();
        statusTmp.font = font;
        statusTmp.fontSize = 13;
        statusTmp.fontStyle = FontStyles.Bold;
        statusTmp.text = "Postęp: 2 z 4 cykli (50%)";
        statusTmp.color = Color.white;
        statusTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Prawa etykieta pomocnicza (11 pt italic)
        GameObject rateObj = new GameObject("Txt_ProgressRate", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        rateObj.transform.SetParent(labelsRow.transform, false);
        LayoutElement rateLe = rateObj.GetComponent<LayoutElement>();
        rateLe.preferredWidth = 210;
        rateLe.preferredHeight = 20;
        TextMeshProUGUI rateTmp = rateObj.GetComponent<TextMeshProUGUI>();
        rateTmp.font = font;
        rateTmp.fontSize = 11;
        rateTmp.fontStyle = FontStyles.Italic;
        rateTmp.text = "1 cykl / świt (gdy zapewnione zasoby)";
        rateTmp.color = new Color(0.55f, 0.58f, 0.65f, 1f);
        rateTmp.alignment = TextAlignmentOptions.MidlineRight;
    }

    /// <summary>
    /// Buduje dolną belkę akcji z przyciskiem WYBURZ.
    /// </summary>
    private static void BuildBottomBar(Transform parent, TMP_FontAsset font)
    {
        GameObject bottomBar = new GameObject("Pnl_BottomBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        bottomBar.transform.SetParent(parent, false);

        LayoutElement bottomBarLe = bottomBar.GetComponent<LayoutElement>();
        bottomBarLe.preferredHeight = 36;

        HorizontalLayoutGroup bottomHlg = bottomBar.GetComponent<HorizontalLayoutGroup>();
        bottomHlg.spacing = 10;
        bottomHlg.childAlignment = TextAnchor.MiddleCenter;
        bottomHlg.childControlWidth = true;
        bottomHlg.childControlHeight = true;
        bottomHlg.childForceExpandWidth = true;
        bottomHlg.childForceExpandHeight = true;

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
    }

    /// <summary>
    /// Tworzy pojedynczy kafelek kosztu surowca (ikona 24px + kwota 13 pt bold).
    /// </summary>
    private static void CreateCostItem(Transform parent, TMP_FontAsset font, string itemName, string amountText)
    {
        GameObject costItem = new GameObject(itemName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HorizontalLayoutGroup));
        costItem.transform.SetParent(parent, false);

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

        // Ikona surowca (24x24 px - zgodnie ze standardem Small Icon)
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

        // Kwota (13 pt bold)
        GameObject amtObj = new GameObject("Txt_Amount", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        amtObj.transform.SetParent(costItem.transform, false);
        LayoutElement amtLe = amtObj.GetComponent<LayoutElement>();
        amtLe.preferredHeight = 20;
        TextMeshProUGUI amtTmp = amtObj.GetComponent<TextMeshProUGUI>();
        amtTmp.font = font;
        amtTmp.fontSize = 13;
        amtTmp.fontStyle = FontStyles.Bold;
        amtTmp.text = amountText;
        amtTmp.color = new Color(0.95f, 0.4f, 0.4f, 1f); // Czerwony koszt
        amtTmp.alignment = TextAlignmentOptions.MidlineLeft;
    }
}
