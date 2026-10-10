using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class GenerateRuneForgeUI
{
    private const string SessionKey = "RuneForgeUI_Built_v1";
    private const string ScenePath = "Assets/Main folder/_Scenes/TESTSCENE_UI.unity";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    static GenerateRuneForgeUI()
    {
        EditorApplication.delayCall += OnReady;
    }

    private static void OnReady()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        BuildAll();
    }

    [MenuItem("Tools/Generate Rune Forge UI")]
    public static void BuildAll()
    {
        Debug.Log("[RuneForgeUI] Rozpoczynanie generowania szkieletu UI Kuźni Runicznej...");

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
            Debug.LogError("[RuneForgeUI] Nie znaleziono Canvas_ContextWindows na scenie!");
            return;
        }

        // Usuń stare panele jeśli istnieją
        Transform oldModal = canvasObj.transform.Find("Pnl_RuneTowerSelectionModal");
        if (oldModal != null) Undo.DestroyObjectImmediate(oldModal.gameObject);

        Transform oldForge = canvasObj.transform.Find("Pnl_RuneForgeFullscreen");
        if (oldForge != null) Undo.DestroyObjectImmediate(oldForge.gameObject);

        // 4. Zbuduj Modal Wyboru Wieży (overlay)
        GameObject selectionModal = BuildTowerSelectionModal(canvasObj.transform, font);

        // 5. Zbuduj Pełnoekranowy Warsztat Kuźni Runicznej
        GameObject forgeFullscreen = BuildForgeFullscreenHierarchy(canvasObj.transform, font);

        // 5b. Dołącz komponenty skryptowe UI i zbierz referencje
        var modalScript = selectionModal.GetComponent<RuneTowerSelectionModalUI>();
        if (modalScript == null) modalScript = selectionModal.AddComponent<RuneTowerSelectionModalUI>();
        modalScript.AutoBindReferences();

        var forgeScript = forgeFullscreen.GetComponent<RuneForgeWindowUI>();
        if (forgeScript == null) forgeScript = forgeFullscreen.AddComponent<RuneForgeWindowUI>();
        forgeScript.AutoBindReferences();

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
            centerController.runeForgePanel = forgeFullscreen;
            centerController.runeTowerSelectionModal = selectionModal;
            EditorUtility.SetDirty(centerController);
        }

        // Domyślnie ukryj oba panele
        selectionModal.SetActive(false);
        forgeFullscreen.SetActive(false);

        // 7. Zapisz scenę
        Undo.RegisterCreatedObjectUndo(selectionModal, "Generate Rune Tower Selection Modal");
        Undo.RegisterCreatedObjectUndo(forgeFullscreen, "Generate Rune Forge Fullscreen UI");
        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);
        AssetDatabase.SaveAssets();

        Debug.Log("[RuneForgeUI] Pomyślnie wygenerowano Pnl_RuneForgeFullscreen i Pnl_RuneTowerSelectionModal z podpiętymi skryptami!");
    }

    [MenuItem("Tools/Attach RuneForgeWindowUI To Scene")]
    public static void AttachScriptsToScene()
    {
        Debug.Log("[RuneForgeUI] Podpinanie skryptów RuneForgeWindowUI i RuneTowerSelectionModalUI do sceny...");

        var currentScene = EditorSceneManager.GetActiveScene();
        if (currentScene.path != ScenePath)
        {
            currentScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject canvasObj = GameObject.Find("Canvas_ContextWindows");
        if (canvasObj == null)
        {
            Debug.LogError("[RuneForgeUI] Nie znaleziono Canvas_ContextWindows na scenie!");
            return;
        }

        Transform selectionModal = canvasObj.transform.Find("Pnl_RuneTowerSelectionModal");
        if (selectionModal != null)
        {
            var modalScript = selectionModal.GetComponent<RuneTowerSelectionModalUI>();
            if (modalScript == null) modalScript = selectionModal.gameObject.AddComponent<RuneTowerSelectionModalUI>();
            modalScript.AutoBindReferences();
            EditorUtility.SetDirty(selectionModal.gameObject);
        }
        else
        {
            Debug.LogWarning("[RuneForgeUI] Nie znaleziono Pnl_RuneTowerSelectionModal!");
        }

        Transform forgeFullscreen = canvasObj.transform.Find("Pnl_RuneForgeFullscreen");
        if (forgeFullscreen != null)
        {
            var forgeScript = forgeFullscreen.GetComponent<RuneForgeWindowUI>();
            if (forgeScript == null) forgeScript = forgeFullscreen.gameObject.AddComponent<RuneForgeWindowUI>();
            forgeScript.AutoBindReferences();
            EditorUtility.SetDirty(forgeFullscreen.gameObject);
        }
        else
        {
            Debug.LogWarning("[RuneForgeUI] Nie znaleziono Pnl_RuneForgeFullscreen!");
        }

        CenterInfoPanelControllerUI centerController = Object.FindAnyObjectByType<CenterInfoPanelControllerUI>();
        if (centerController != null)
        {
            if (forgeFullscreen != null) centerController.runeForgePanel = forgeFullscreen.gameObject;
            if (selectionModal != null) centerController.runeTowerSelectionModal = selectionModal.gameObject;
            EditorUtility.SetDirty(centerController);
        }

        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);
        AssetDatabase.SaveAssets();
        Debug.Log("[RuneForgeUI] Pomyślnie podpięto komponenty RuneForgeWindowUI i RuneTowerSelectionModalUI do sceny!");
    }

    // =========================================================================
    // MODUŁ A: MODAL WYBORU WIEŻY DLA KOWALA RUN
    // =========================================================================

    private static GameObject BuildTowerSelectionModal(Transform parent, TMP_FontAsset font)
    {
        // Pełnoekranowy ciemny backdrop
        GameObject root = new GameObject("Pnl_RuneTowerSelectionModal", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(parent, false);

        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        Image rootImg = root.GetComponent<Image>();
        rootImg.sprite = null;
        rootImg.color = new Color(0f, 0f, 0f, 0.75f);

        // Centralne okno modalu (1100 x 700 px)
        GameObject modalWindow = new GameObject("Pnl_ModalWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
        modalWindow.transform.SetParent(root.transform, false);

        RectTransform winRt = modalWindow.GetComponent<RectTransform>();
        winRt.anchorMin = new Vector2(0.5f, 0.5f);
        winRt.anchorMax = new Vector2(0.5f, 0.5f);
        winRt.pivot = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(1100f, 700f);

        Image winImg = modalWindow.GetComponent<Image>();
        winImg.sprite = null;
        winImg.color = new Color(0.08f, 0.09f, 0.12f, 0.98f);

        VerticalLayoutGroup winVlg = modalWindow.GetComponent<VerticalLayoutGroup>();
        winVlg.padding = new RectOffset(24, 24, 20, 24);
        winVlg.spacing = 16;
        winVlg.childAlignment = TextAnchor.UpperCenter;
        winVlg.childControlWidth = true;
        winVlg.childControlHeight = false;
        winVlg.childForceExpandWidth = true;
        winVlg.childForceExpandHeight = false;

        // Pasek górny modalu
        GameObject topBar = new GameObject("Pnl_ModalTopBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        topBar.transform.SetParent(modalWindow.transform, false);
        topBar.GetComponent<LayoutElement>().preferredHeight = 36;

        HorizontalLayoutGroup topHlg = topBar.GetComponent<HorizontalLayoutGroup>();
        topHlg.spacing = 10;
        topHlg.childAlignment = TextAnchor.MiddleLeft;
        topHlg.childControlWidth = false;
        topHlg.childControlHeight = false;

        // Ikona gwiazdy (24 px)
        CreateIcon(topBar.transform, "Img_StarIcon", 24, new Color(0.96f, 0.78f, 0.28f, 1f));

        // Tytuł modalu (20 pt bold - GEMINI.md)
        GameObject titleObj = new GameObject("Txt_Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        titleObj.transform.SetParent(topBar.transform, false);
        titleObj.GetComponent<LayoutElement>().flexibleWidth = 1;
        titleObj.GetComponent<LayoutElement>().preferredHeight = 34;
        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.fontSize = 20;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.text = "WYBÓR WIEŻY DLA KOWALA RUN";
        titleTmp.color = new Color(0.96f, 0.78f, 0.28f, 1f);
        titleTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Przycisk zamknięcia (28x28 px)
        CreateCloseButton(topBar.transform, font, "Btn_Close", 28);

        // Karta wprowadzająca / opis fabularny
        GameObject introCard = new GameObject("Pnl_IntroBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        introCard.transform.SetParent(modalWindow.transform, false);
        introCard.GetComponent<LayoutElement>().preferredHeight = 72;

        Image introImg = introCard.GetComponent<Image>();
        introImg.sprite = null;
        introImg.color = new Color(0.12f, 0.14f, 0.18f, 0.9f);

        VerticalLayoutGroup introVlg = introCard.GetComponent<VerticalLayoutGroup>();
        introVlg.padding = new RectOffset(16, 16, 10, 10);
        introVlg.childAlignment = TextAnchor.MiddleCenter;
        introVlg.childControlWidth = true;
        introVlg.childControlHeight = true;

        GameObject introTxtObj = new GameObject("Txt_IntroDescription", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        introTxtObj.transform.SetParent(introCard.transform, false);
        TextMeshProUGUI introTmp = introTxtObj.GetComponent<TextMeshProUGUI>();
        introTmp.font = font;
        introTmp.fontSize = 13;
        introTmp.text = "Mistrz Runiczny musi poświęcić swój kowalski warsztat <b>jednemu konkretnemu typowi machiny obronnej</b>. Wszystkie osadzone i wykute tu runy będą nasycać wyłącznie wybraną konstrukcję w całej twierdzy!";
        introTmp.color = new Color(0.82f, 0.85f, 0.9f, 1f);
        introTmp.alignment = TextAlignmentOptions.Center;

        // Kontener kart wież (ScrollRect z poziomym przewijaniem)
        GameObject scrollObj = new GameObject("Scroll_Towers", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(LayoutElement));
        scrollObj.transform.SetParent(modalWindow.transform, false);
        scrollObj.GetComponent<LayoutElement>().preferredHeight = 480;
        scrollObj.GetComponent<LayoutElement>().flexibleHeight = 1;

        Image scrollImg = scrollObj.GetComponent<Image>();
        scrollImg.sprite = null;
        scrollImg.color = new Color(0.06f, 0.07f, 0.09f, 0.5f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollObj.transform, false);
        RectTransform vpRt = viewport.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.sizeDelta = Vector2.zero;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform cntRt = content.GetComponent<RectTransform>();
        cntRt.anchorMin = new Vector2(0f, 0f);
        cntRt.anchorMax = new Vector2(0f, 1f);
        cntRt.pivot = new Vector2(0f, 0.5f);
        cntRt.sizeDelta = new Vector2(0f, 0f);

        HorizontalLayoutGroup cntHlg = content.GetComponent<HorizontalLayoutGroup>();
        cntHlg.padding = new RectOffset(10, 10, 10, 10);
        cntHlg.spacing = 18;
        cntHlg.childAlignment = TextAnchor.MiddleLeft;
        cntHlg.childControlWidth = false;
        cntHlg.childControlHeight = true;
        cntHlg.childForceExpandWidth = false;
        cntHlg.childForceExpandHeight = true;

        ContentSizeFitter cntCsf = content.GetComponent<ContentSizeFitter>();
        cntCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        cntCsf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect sr = scrollObj.GetComponent<ScrollRect>();
        sr.content = cntRt;
        sr.viewport = vpRt;
        sr.horizontal = true;
        sr.vertical = false;

        // Karty wież w modalu
        BuildTowerSelectionCard(content.transform, font, "KRASNOLUDZKA BALISTA", "CIĘŻKA ARTYLERIA MIOTAJĄCA", "Ciężkie bełty burzące pancerze i miażdżące opancerzone szeregi wroga z potężną siłą.", "WYBIERZ BALISTĘ");
        BuildTowerSelectionCard(content.transform, font, "WIEŻA RUNICZNA", "MISTYCZNA ENERGIA ŻYWIOŁÓW", "Skupisko pradawnych kryształów rażące łańcuchowymi błyskawicami całe hordy.", "WYBIERZ WIEŻĘ RUNICZNĄ");
        BuildTowerSelectionCard(content.transform, font, "KUSZA SZYBKOSTRZELNA", "AUTOMATYCZNY OSTRZAŁ CIĄGŁY", "Zmechanizowany bęben miotający zabójczy grad lżejszych bełtów z zawrotną prędkością.", "WYBIERZ KUSZĘ");
        BuildTowerSelectionCard(content.transform, font, "MOŹDZIERZ SKAŁOMIOTNY", "OBSZAROWY OGIEŃ OBLĘŻNICZY", "Wybuchowe pociski rozrywające formacje wrogów i tworzące strefy spowolnienia.", "WYBIERZ MOŹDZIERZ");

        // Overlay potwierdzenia ostrzeżenia (Pnl_ConfirmWarningDialog)
        BuildConfirmWarningDialog(root.transform, font);

        return root;
    }

    private static void BuildTowerSelectionCard(Transform parent, TMP_FontAsset font, string towerName, string category, string desc, string buttonText)
    {
        GameObject card = new GameObject("Card_" + towerName.Replace(" ", "_"), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        card.transform.SetParent(parent, false);

        LayoutElement cardLe = card.GetComponent<LayoutElement>();
        cardLe.preferredWidth = 320;
        cardLe.preferredHeight = 440;

        Image cardImg = card.GetComponent<Image>();
        cardImg.sprite = null;
        cardImg.color = new Color(0.11f, 0.13f, 0.17f, 0.98f);

        VerticalLayoutGroup cardVlg = card.GetComponent<VerticalLayoutGroup>();
        cardVlg.padding = new RectOffset(16, 16, 20, 16);
        cardVlg.spacing = 12;
        cardVlg.childAlignment = TextAnchor.UpperCenter;
        cardVlg.childControlWidth = true;
        cardVlg.childControlHeight = false;
        cardVlg.childForceExpandWidth = true;
        cardVlg.childForceExpandHeight = false;

        // Ikona wieży (32x32 px w kwadratowej ramce 56x56)
        GameObject iconFrame = new GameObject("Pnl_IconFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconFrame.transform.SetParent(card.transform, false);
        iconFrame.GetComponent<LayoutElement>().preferredWidth = 56;
        iconFrame.GetComponent<LayoutElement>().preferredHeight = 56;
        Image frameImg = iconFrame.GetComponent<Image>();
        frameImg.sprite = null;
        frameImg.color = new Color(0.18f, 0.2f, 0.26f, 0.95f);

        GameObject iconObj = new GameObject("Img_TowerIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObj.transform.SetParent(iconFrame.transform, false);
        RectTransform iconRt = iconObj.GetComponent<RectTransform>();
        iconRt.sizeDelta = new Vector2(32, 32);
        Image iconImg = iconObj.GetComponent<Image>();
        iconImg.sprite = null;
        iconImg.color = new Color(0.96f, 0.78f, 0.28f, 0.8f);

        // Nazwa wieży (16 pt bold)
        GameObject nameObj = new GameObject("Txt_TowerName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        nameObj.transform.SetParent(card.transform, false);
        nameObj.GetComponent<LayoutElement>().preferredHeight = 24;
        TextMeshProUGUI nameTmp = nameObj.GetComponent<TextMeshProUGUI>();
        nameTmp.font = font;
        nameTmp.fontSize = 16;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.text = towerName;
        nameTmp.color = Color.white;
        nameTmp.alignment = TextAlignmentOptions.Center;

        // Kategoria (12 pt bold, złocisty)
        GameObject catObj = new GameObject("Txt_Category", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        catObj.transform.SetParent(card.transform, false);
        catObj.GetComponent<LayoutElement>().preferredHeight = 20;
        TextMeshProUGUI catTmp = catObj.GetComponent<TextMeshProUGUI>();
        catTmp.font = font;
        catTmp.fontSize = 12;
        catTmp.fontStyle = FontStyles.Bold;
        catTmp.text = category;
        catTmp.color = new Color(0.96f, 0.78f, 0.28f, 1f);
        catTmp.alignment = TextAlignmentOptions.Center;

        // Opis fabularny (12 pt)
        GameObject descObj = new GameObject("Txt_Description", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        descObj.transform.SetParent(card.transform, false);
        LayoutElement descLe = descObj.GetComponent<LayoutElement>();
        descLe.flexibleHeight = 1;
        descLe.preferredHeight = 100;
        TextMeshProUGUI descTmp = descObj.GetComponent<TextMeshProUGUI>();
        descTmp.font = font;
        descTmp.fontSize = 12;
        descTmp.text = desc;
        descTmp.color = new Color(0.72f, 0.75f, 0.8f, 1f);
        descTmp.alignment = TextAlignmentOptions.Top;

        // Przycisk wyboru (14 pt bold)
        GameObject selectBtnObj = new GameObject("Btn_SelectTower", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        selectBtnObj.transform.SetParent(card.transform, false);
        selectBtnObj.GetComponent<LayoutElement>().preferredHeight = 42;
        Image btnImg = selectBtnObj.GetComponent<Image>();
        btnImg.sprite = null;
        btnImg.color = new Color(0.24f, 0.2f, 0.12f, 0.95f);

        GameObject btnTxtObj = new GameObject("Txt_Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        btnTxtObj.transform.SetParent(selectBtnObj.transform, false);
        RectTransform btnTxtRt = btnTxtObj.GetComponent<RectTransform>();
        btnTxtRt.anchorMin = Vector2.zero;
        btnTxtRt.anchorMax = Vector2.one;
        btnTxtRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI btnTmp = btnTxtObj.GetComponent<TextMeshProUGUI>();
        btnTmp.font = font;
        btnTmp.fontSize = 14;
        btnTmp.fontStyle = FontStyles.Bold;
        btnTmp.text = buttonText;
        btnTmp.color = new Color(0.96f, 0.82f, 0.38f, 1f);
        btnTmp.alignment = TextAlignmentOptions.Center;
    }

    private static void BuildConfirmWarningDialog(Transform modalRoot, TMP_FontAsset font)
    {
        GameObject dialogRoot = new GameObject("Pnl_ConfirmWarningDialog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dialogRoot.transform.SetParent(modalRoot, false);

        RectTransform diaRt = dialogRoot.GetComponent<RectTransform>();
        diaRt.anchorMin = Vector2.zero;
        diaRt.anchorMax = Vector2.one;
        diaRt.offsetMin = Vector2.zero;
        diaRt.offsetMax = Vector2.zero;

        Image diaBg = dialogRoot.GetComponent<Image>();
        diaBg.sprite = null;
        diaBg.color = new Color(0f, 0f, 0f, 0.75f);

        // Okienko
        GameObject boxObj = new GameObject("Pnl_DialogBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
        boxObj.transform.SetParent(dialogRoot.transform, false);
        RectTransform boxRt = boxObj.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(560f, 290f);

        Image boxImg = boxObj.GetComponent<Image>();
        boxImg.sprite = null;
        boxImg.color = new Color(0.12f, 0.08f, 0.08f, 0.98f);

        VerticalLayoutGroup boxVlg = boxObj.GetComponent<VerticalLayoutGroup>();
        boxVlg.padding = new RectOffset(24, 24, 20, 20);
        boxVlg.spacing = 14;
        boxVlg.childAlignment = TextAnchor.UpperCenter;
        boxVlg.childControlWidth = true;
        boxVlg.childControlHeight = false;

        // Tytuł ostrzeżenia (16 pt bold czerwony)
        GameObject warnTitleObj = new GameObject("Txt_WarningTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        warnTitleObj.transform.SetParent(boxObj.transform, false);
        warnTitleObj.GetComponent<LayoutElement>().preferredHeight = 26;
        TextMeshProUGUI warnTitleTmp = warnTitleObj.GetComponent<TextMeshProUGUI>();
        warnTitleTmp.font = font;
        warnTitleTmp.fontSize = 16;
        warnTitleTmp.fontStyle = FontStyles.Bold;
        warnTitleTmp.text = "OSTRZEŻENIE: WIĄZANIE PERMANENTNE";
        warnTitleTmp.color = new Color(1f, 0.35f, 0.35f, 1f);
        warnTitleTmp.alignment = TextAlignmentOptions.Center;

        // Komunikat (13 pt)
        GameObject warnMsgObj = new GameObject("Txt_WarningMessage", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        warnMsgObj.transform.SetParent(boxObj.transform, false);
        warnMsgObj.GetComponent<LayoutElement>().preferredHeight = 80;
        TextMeshProUGUI warnMsgTmp = warnMsgObj.GetComponent<TextMeshProUGUI>();
        warnMsgTmp.font = font;
        warnMsgTmp.fontSize = 13;
        warnMsgTmp.text = "Czy na pewno chcesz poświęcić tę Kuźnię dla wskazanego typu wieży?\n\nWszystkie osadzone tu runy będą nasycać wyłącznie wybraną konstrukcję w całej twierdzy. <b>Tej decyzji nie można cofnąć!</b>";
        warnMsgTmp.color = new Color(0.85f, 0.85f, 0.88f, 1f);
        warnMsgTmp.alignment = TextAlignmentOptions.Center;

        // Przyciski akcji (Potwierdź / Anuluj)
        GameObject btnRow = new GameObject("Pnl_ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        btnRow.transform.SetParent(boxObj.transform, false);
        btnRow.GetComponent<LayoutElement>().preferredHeight = 44;
        HorizontalLayoutGroup btnHlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        btnHlg.spacing = 16;
        btnHlg.childAlignment = TextAnchor.MiddleCenter;
        btnHlg.childControlWidth = true;
        btnHlg.childControlHeight = true;

        CreateActionButton(btnRow.transform, font, "Btn_ConfirmChoice", "POTWIERDŹ WYBÓR", new Color(0.2f, 0.45f, 0.22f, 0.95f), new Color(0.5f, 0.95f, 0.55f, 1f), 14);
        CreateActionButton(btnRow.transform, font, "Btn_CancelChoice", "ANULUJ", new Color(0.28f, 0.16f, 0.16f, 0.95f), new Color(1f, 0.6f, 0.6f, 1f), 14);

        // Domyślnie dialog potwierdzenia jest ukryty
        dialogRoot.SetActive(false);
    }

    // =========================================================================
    // MODUŁ B: PEŁNOEKRANOWY GŁÓWNY PANEL KUŹNI RUNICZNEJ
    // =========================================================================

    private static GameObject BuildForgeFullscreenHierarchy(Transform parent, TMP_FontAsset font)
    {
        // Pełnoekranowy główny kontener (1920 x 1080)
        GameObject root = new GameObject("Pnl_RuneForgeFullscreen", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
        root.transform.SetParent(parent, false);

        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        Image rootImg = root.GetComponent<Image>();
        rootImg.sprite = null;
        rootImg.color = new Color(0.06f, 0.07f, 0.09f, 0.98f); // Ciemne, nieprzezroczyste tło przysłaniające mapę

        VerticalLayoutGroup rootVlg = root.GetComponent<VerticalLayoutGroup>();
        rootVlg.padding = new RectOffset(16, 16, 12, 16);
        rootVlg.spacing = 12;
        rootVlg.childAlignment = TextAnchor.UpperCenter;
        rootVlg.childControlWidth = true;
        rootVlg.childControlHeight = true;
        rootVlg.childForceExpandWidth = true;
        rootVlg.childForceExpandHeight = false;

        // 1. Górna Belka (Pnl_TopBar - wysokość 48 px)
        BuildTopBar(root.transform, font);

        // 2. Środkowy wiersz 3-kolumnowy (Pnl_BodyRow - elastyczna wysokość)
        BuildBodyRow(root.transform, font);

        // 3. Dolny Panel: Statystyki + Kowale (Pnl_BottomPanel - wysokość 210 px)
        BuildBottomPanel(root.transform, font);

        return root;
    }

    private static void BuildTopBar(Transform parent, TMP_FontAsset font)
    {
        GameObject topBar = new GameObject("Pnl_TopBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        topBar.transform.SetParent(parent, false);
        topBar.GetComponent<LayoutElement>().preferredHeight = 48;

        HorizontalLayoutGroup topHlg = topBar.GetComponent<HorizontalLayoutGroup>();
        topHlg.spacing = 12;
        topHlg.childAlignment = TextAnchor.MiddleLeft;
        topHlg.childControlWidth = false;
        topHlg.childControlHeight = false;

        // Ikona młota/kowadła (32 px)
        CreateIcon(topBar.transform, "Img_ForgeIcon", 32, new Color(0.96f, 0.78f, 0.28f, 1f));

        // Tytuł kuźni i wzmacnianej wieży (20 pt bold - GEMINI.md)
        GameObject titleObj = new GameObject("Txt_ForgeTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        titleObj.transform.SetParent(topBar.transform, false);
        titleObj.GetComponent<LayoutElement>().flexibleWidth = 1;
        titleObj.GetComponent<LayoutElement>().preferredHeight = 36;
        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.fontSize = 20;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.text = "KUŹNIA RUNICZNA — [KRASNOLUDZKA BALISTA]";
        titleTmp.color = Color.white;
        titleTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Etykieta statusu (13 pt bold złocisty)
        GameObject statusObj = new GameObject("Txt_SubTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        statusObj.transform.SetParent(topBar.transform, false);
        statusObj.GetComponent<LayoutElement>().preferredWidth = 260;
        statusObj.GetComponent<LayoutElement>().preferredHeight = 30;
        TextMeshProUGUI statusTmp = statusObj.GetComponent<TextMeshProUGUI>();
        statusTmp.font = font;
        statusTmp.fontSize = 13;
        statusTmp.fontStyle = FontStyles.Bold;
        statusTmp.text = "GŁÓWNY WARSZTAT RUNICZNY";
        statusTmp.color = new Color(0.96f, 0.78f, 0.28f, 1f);
        statusTmp.alignment = TextAlignmentOptions.MidlineRight;

        // Przycisk zamknięcia (32x32 px)
        CreateCloseButton(topBar.transform, font, "Btn_Close", 32);
    }

    private static void BuildBodyRow(Transform parent, TMP_FontAsset font)
    {
        GameObject bodyRow = new GameObject("Pnl_BodyRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        bodyRow.transform.SetParent(parent, false);

        LayoutElement bodyLe = bodyRow.GetComponent<LayoutElement>();
        bodyLe.flexibleHeight = 1;

        HorizontalLayoutGroup bodyHlg = bodyRow.GetComponent<HorizontalLayoutGroup>();
        bodyHlg.spacing = 14;
        bodyHlg.childAlignment = TextAnchor.UpperLeft;
        bodyHlg.childControlWidth = false;
        bodyHlg.childControlHeight = true;
        bodyHlg.childForceExpandWidth = false;
        bodyHlg.childForceExpandHeight = true;

        // 1. Lewy Panel Nawigacji (Pnl_LeftNav - 260 px)
        BuildLeftNav(bodyRow.transform, font);

        // 2. Środkowy Panel Akcji (Pnl_CenterAction - flex width)
        BuildCenterAction(bodyRow.transform, font);

        // 3. Prawy Panel Ekwipunku (Pnl_RightInventory - 380 px)
        BuildRightInventory(bodyRow.transform, font);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Lewy Panel: Wybór Akcji Runicznej
    // ─────────────────────────────────────────────────────────────────────────
    private static void BuildLeftNav(Transform parent, TMP_FontAsset font)
    {
        GameObject leftNav = new GameObject("Pnl_LeftNav", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        leftNav.transform.SetParent(parent, false);

        LayoutElement navLe = leftNav.GetComponent<LayoutElement>();
        navLe.preferredWidth = 260;
        navLe.flexibleHeight = 1;

        Image navImg = leftNav.GetComponent<Image>();
        navImg.sprite = null;
        navImg.color = new Color(0.1f, 0.11f, 0.14f, 0.95f);

        VerticalLayoutGroup navVlg = leftNav.GetComponent<VerticalLayoutGroup>();
        navVlg.padding = new RectOffset(12, 12, 14, 14);
        navVlg.spacing = 10;
        navVlg.childAlignment = TextAnchor.UpperCenter;
        navVlg.childControlWidth = true;
        navVlg.childControlHeight = false;

        // Tytuł sekcji (16 pt bold - GEMINI.md)
        GameObject titleObj = new GameObject("Txt_NavTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        titleObj.transform.SetParent(leftNav.transform, false);
        titleObj.GetComponent<LayoutElement>().preferredHeight = 24;
        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.fontSize = 16;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.text = "AKCJE RUNICZNE";
        titleTmp.color = new Color(0.9f, 0.9f, 0.92f, 1f);
        titleTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Linia oddzielająca
        CreateDivider(leftNav.transform);

        // Przycisk 1: OSADZANIE RUN (Aktywny domyślnie)
        CreateNavTabButton(leftNav.transform, font, "Btn_TabSocketing", "1. OSADZANIE RUN", true);

        // Przycisk 2: FUZJA RUN
        CreateNavTabButton(leftNav.transform, font, "Btn_TabFusion", "2. FUZJA RUN", false);

        // Przycisk 3: TRANSMUTACJA
        CreateNavTabButton(leftNav.transform, font, "Btn_TabTransmutation", "3. TRANSMUTACJA", false);

        // Elastyczny odstęp
        GameObject spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(leftNav.transform, false);
        spacer.GetComponent<LayoutElement>().flexibleHeight = 1;

        // Karta informacyjna o Wieżach Runicznych
        GameObject towerInfoCard = new GameObject("Pnl_RuneTowersStatus", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        towerInfoCard.transform.SetParent(leftNav.transform, false);
        towerInfoCard.GetComponent<LayoutElement>().preferredHeight = 84;
        towerInfoCard.GetComponent<Image>().sprite = null;
        towerInfoCard.GetComponent<Image>().color = new Color(0.14f, 0.16f, 0.2f, 0.9f);

        VerticalLayoutGroup infoVlg = towerInfoCard.GetComponent<VerticalLayoutGroup>();
        infoVlg.padding = new RectOffset(10, 10, 8, 8);
        infoVlg.spacing = 4;
        infoVlg.childAlignment = TextAnchor.UpperLeft;

        GameObject infoTitleObj = new GameObject("Txt_InfoTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        infoTitleObj.transform.SetParent(towerInfoCard.transform, false);
        TextMeshProUGUI infoTitleTmp = infoTitleObj.GetComponent<TextMeshProUGUI>();
        infoTitleTmp.font = font;
        infoTitleTmp.fontSize = 12;
        infoTitleTmp.fontStyle = FontStyles.Bold;
        infoTitleTmp.text = "WIEŻE RUNICZNE WOKÓŁ:";
        infoTitleTmp.color = new Color(0.96f, 0.78f, 0.28f, 1f);

        GameObject infoDescObj = new GameObject("Txt_InfoDesc", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        infoDescObj.transform.SetParent(towerInfoCard.transform, false);
        TextMeshProUGUI infoDescTmp = infoDescObj.GetComponent<TextMeshProUGUI>();
        infoDescTmp.font = font;
        infoDescTmp.fontSize = 11;
        infoDescTmp.text = "Sąsiedztwo: 0 z 4 wież\nOdblokowują sloty i kowali.";
        infoDescTmp.color = new Color(0.72f, 0.75f, 0.8f, 1f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Środkowy Panel: Dynamiczny Kontener Akcji
    // ─────────────────────────────────────────────────────────────────────────
    private static void BuildCenterAction(Transform parent, TMP_FontAsset font)
    {
        GameObject centerAction = new GameObject("Pnl_CenterAction", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        centerAction.transform.SetParent(parent, false);

        LayoutElement centerLe = centerAction.GetComponent<LayoutElement>();
        centerLe.flexibleWidth = 1;
        centerLe.flexibleHeight = 1;

        Image centerImg = centerAction.GetComponent<Image>();
        centerImg.sprite = null;
        centerImg.color = new Color(0.09f, 0.1f, 0.13f, 0.95f);

        // Widok 1: OSADZANIE RUN (View_Socketing - aktywny domyślnie)
        BuildSocketingView(centerAction.transform, font);

        // Widok 2: FUZJA RUN (View_Fusion - nieaktywny domyślnie)
        BuildFusionView(centerAction.transform, font);

        // Widok 3: TRANSMUTACJA (View_Transmutation - nieaktywny domyślnie)
        BuildTransmutationView(centerAction.transform, font);
    }

    // Widok 1: Osadzanie run
    private static void BuildSocketingView(Transform parent, TMP_FontAsset font)
    {
        GameObject view = new GameObject("View_Socketing", typeof(RectTransform), typeof(VerticalLayoutGroup));
        view.transform.SetParent(parent, false);

        RectTransform viewRt = view.GetComponent<RectTransform>();
        viewRt.anchorMin = Vector2.zero;
        viewRt.anchorMax = Vector2.one;
        viewRt.offsetMin = Vector2.zero;
        viewRt.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = view.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 16, 16);
        vlg.spacing = 16;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        // Karta nagłówkowa wieży
        GameObject headerCard = new GameObject("Pnl_TowerHeaderCard", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        headerCard.transform.SetParent(view.transform, false);
        headerCard.GetComponent<LayoutElement>().preferredHeight = 120;
        VerticalLayoutGroup hVlg = headerCard.GetComponent<VerticalLayoutGroup>();
        hVlg.spacing = 6;
        hVlg.childAlignment = TextAnchor.MiddleCenter;

        // Duża ikona wieży (44x44 px - GEMINI.md Big Icon)
        GameObject iconFrame = new GameObject("Img_TowerIconFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconFrame.transform.SetParent(headerCard.transform, false);
        iconFrame.GetComponent<LayoutElement>().preferredWidth = 44;
        iconFrame.GetComponent<LayoutElement>().preferredHeight = 44;
        Image ifImg = iconFrame.GetComponent<Image>();
        ifImg.sprite = null;
        ifImg.color = new Color(0.96f, 0.78f, 0.28f, 0.9f);

        // Nazwa wieży (18 pt bold) + podtytuł (13 pt bold złocisty)
        GameObject nameObj = new GameObject("Txt_TowerHeaderName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        nameObj.transform.SetParent(headerCard.transform, false);
        nameObj.GetComponent<LayoutElement>().preferredHeight = 24;
        TextMeshProUGUI nameTmp = nameObj.GetComponent<TextMeshProUGUI>();
        nameTmp.font = font;
        nameTmp.fontSize = 18;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.text = "KRASNOLUDZKA BALISTA OGNISTOBRODYCH";
        nameTmp.color = Color.white;
        nameTmp.alignment = TextAlignmentOptions.Center;

        GameObject subObj = new GameObject("Txt_TowerSubTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        subObj.transform.SetParent(headerCard.transform, false);
        subObj.GetComponent<LayoutElement>().preferredHeight = 20;
        TextMeshProUGUI subTmp = subObj.GetComponent<TextMeshProUGUI>();
        subTmp.font = font;
        subTmp.fontSize = 13;
        subTmp.fontStyle = FontStyles.Bold;
        subTmp.text = "GŁÓWNY PUNKT WIĄZANIA RUNICZNEGO";
        subTmp.color = new Color(0.96f, 0.78f, 0.28f, 1f);
        subTmp.alignment = TextAlignmentOptions.Center;

        // Rząd 5 slotów na runy (Pnl_SlotsRow - wysokość 180 px)
        GameObject slotsRow = new GameObject("Pnl_SlotsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        slotsRow.transform.SetParent(view.transform, false);
        slotsRow.GetComponent<LayoutElement>().preferredHeight = 180;
        HorizontalLayoutGroup sHlg = slotsRow.GetComponent<HorizontalLayoutGroup>();
        sHlg.spacing = 16;
        sHlg.childAlignment = TextAnchor.MiddleCenter;
        sHlg.childControlWidth = false;
        sHlg.childControlHeight = false;

        // Slot 1: Odblokowany (ze wstążką "BRAK KOWALA")
        BuildSocketCard(slotsRow.transform, font, 1, true, true);

        // Sloty 2..5: Zablokowane (Wymagają Wieży)
        for (int i = 2; i <= 5; i++)
        {
            BuildSocketCard(slotsRow.transform, font, i, false, false);
        }

        // Elastyczny odstęp
        GameObject spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(view.transform, false);
        spacer.GetComponent<LayoutElement>().flexibleHeight = 1;

        // Dolny pasek podpowiedzi (ikona żarówki + opis 12 pt)
        GameObject tipBar = new GameObject("Pnl_TipBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        tipBar.transform.SetParent(view.transform, false);
        tipBar.GetComponent<LayoutElement>().preferredHeight = 44;
        tipBar.GetComponent<Image>().sprite = null;
        tipBar.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.8f);

        HorizontalLayoutGroup tipHlg = tipBar.GetComponent<HorizontalLayoutGroup>();
        tipHlg.padding = new RectOffset(16, 16, 8, 8);
        tipHlg.spacing = 10;
        tipHlg.childAlignment = TextAnchor.MiddleLeft;

        CreateIcon(tipBar.transform, "Img_BulbIcon", 24, new Color(0.96f, 0.78f, 0.28f, 1f));

        GameObject tipTxtObj = new GameObject("Txt_TipText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        tipTxtObj.transform.SetParent(tipBar.transform, false);
        tipTxtObj.GetComponent<LayoutElement>().flexibleWidth = 1;
        TextMeshProUGUI tipTmp = tipTxtObj.GetComponent<TextMeshProUGUI>();
        tipTmp.font = font;
        tipTmp.fontSize = 12;
        tipTmp.text = "Przeciągnij runę z ekwipunku na odblokowany slot, aby ją osadzić. Nowa runa podmienia starą!";
        tipTmp.color = new Color(0.82f, 0.85f, 0.9f, 1f);
        tipTmp.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private static void BuildSocketCard(Transform parent, TMP_FontAsset font, int slotIndex, bool isUnlocked, bool missingSmith)
    {
        GameObject card = new GameObject($"Slot_{slotIndex}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        card.transform.SetParent(parent, false);

        LayoutElement le = card.GetComponent<LayoutElement>();
        le.preferredWidth = 120;
        le.preferredHeight = 160;

        Image img = card.GetComponent<Image>();
        img.sprite = null;
        img.color = isUnlocked ? new Color(0.13f, 0.15f, 0.2f, 0.95f) : new Color(0.08f, 0.09f, 0.11f, 0.85f);

        VerticalLayoutGroup vlg = card.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.spacing = 6;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        // Jeśli odblokowany ale brak kowala -> baner u góry
        if (isUnlocked && missingSmith)
        {
            GameObject badge = new GameObject("Badge_MissingSmith", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
            badge.transform.SetParent(card.transform, false);
            badge.GetComponent<LayoutElement>().preferredHeight = 20;
            badge.GetComponent<Image>().sprite = null;
            badge.GetComponent<Image>().color = new Color(0.35f, 0.22f, 0.08f, 0.95f);

            GameObject badgeTxt = new GameObject("Txt_Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            badgeTxt.transform.SetParent(badge.transform, false);
            RectTransform btRt = badgeTxt.GetComponent<RectTransform>();
            btRt.anchorMin = Vector2.zero;
            btRt.anchorMax = Vector2.one;
            btRt.sizeDelta = Vector2.zero;
            TextMeshProUGUI btTmp = badgeTxt.GetComponent<TextMeshProUGUI>();
            btTmp.font = font;
            btTmp.fontSize = 10;
            btTmp.fontStyle = FontStyles.Bold;
            btTmp.text = "BRAK KOWALA";
            btTmp.color = new Color(0.96f, 0.78f, 0.28f, 1f);
            btTmp.alignment = TextAlignmentOptions.Center;
        }

        // Ikona slotu lub kłódki (24 px)
        GameObject iconObj = new GameObject(isUnlocked ? "Img_SocketIcon" : "Img_LockIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconObj.transform.SetParent(card.transform, false);
        iconObj.GetComponent<LayoutElement>().preferredWidth = 24;
        iconObj.GetComponent<LayoutElement>().preferredHeight = 24;
        Image icImg = iconObj.GetComponent<Image>();
        icImg.sprite = null;
        icImg.color = isUnlocked ? new Color(0.96f, 0.78f, 0.28f, 0.8f) : new Color(0.55f, 0.58f, 0.65f, 0.6f);

        // Etykieta slotu
        GameObject lblObj = new GameObject("Txt_SlotLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        lblObj.transform.SetParent(card.transform, false);
        lblObj.GetComponent<LayoutElement>().preferredHeight = 36;
        TextMeshProUGUI lblTmp = lblObj.GetComponent<TextMeshProUGUI>();
        lblTmp.font = font;
        lblTmp.fontSize = isUnlocked ? 12 : 11;
        lblTmp.text = isUnlocked ? $"SLOT {slotIndex}\n<size=10><color=#999999>WOLNY SLOT</color></size>" : $"SLOT {slotIndex}\n<size=10><color=#666666>WYMAGA WIEŻY</color></size>";
        lblTmp.color = isUnlocked ? Color.white : new Color(0.6f, 0.62f, 0.68f, 1f);
        lblTmp.alignment = TextAlignmentOptions.Center;
    }

    // Widok 2: Fuzja run
    private static void BuildFusionView(Transform parent, TMP_FontAsset font)
    {
        GameObject view = new GameObject("View_Fusion", typeof(RectTransform), typeof(VerticalLayoutGroup));
        view.transform.SetParent(parent, false);

        RectTransform viewRt = view.GetComponent<RectTransform>();
        viewRt.anchorMin = Vector2.zero;
        viewRt.anchorMax = Vector2.one;
        viewRt.offsetMin = Vector2.zero;
        viewRt.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = view.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 16, 16);
        vlg.spacing = 14;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        // Pasek górny fuzji
        GameObject topBar = new GameObject("Pnl_FusionTopBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        topBar.transform.SetParent(view.transform, false);
        topBar.GetComponent<LayoutElement>().preferredHeight = 36;

        HorizontalLayoutGroup topHlg = topBar.GetComponent<HorizontalLayoutGroup>();
        topHlg.spacing = 12;
        topHlg.childAlignment = TextAnchor.MiddleLeft;

        CreateActionButton(topBar.transform, font, "Btn_BackToTower", "← POWRÓT DO WIEŻY", new Color(0.18f, 0.2f, 0.26f, 0.95f), Color.white, 13);
        CreateIcon(topBar.transform, "Img_LightningIcon", 24, new Color(0.96f, 0.78f, 0.28f, 1f));

        GameObject titleObj = new GameObject("Txt_FusionTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        titleObj.transform.SetParent(topBar.transform, false);
        titleObj.GetComponent<LayoutElement>().flexibleWidth = 1;
        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.fontSize = 16;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.text = "FUZJA RUNICZNA (2 RUNY)";
        titleTmp.color = new Color(0.96f, 0.78f, 0.28f, 1f);

        // Kowadło fuzji (2 wejścia + 1 wynik)
        GameObject anvilBox = new GameObject("Pnl_AnvilFusion", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        anvilBox.transform.SetParent(view.transform, false);
        anvilBox.GetComponent<LayoutElement>().preferredHeight = 220;

        VerticalLayoutGroup aVlg = anvilBox.GetComponent<VerticalLayoutGroup>();
        aVlg.spacing = 12;
        aVlg.childAlignment = TextAnchor.MiddleCenter;

        // Wiersz 2 slotów wejściowych
        GameObject inputRow = new GameObject("Pnl_InputSlots", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        inputRow.transform.SetParent(anvilBox.transform, false);
        HorizontalLayoutGroup inHlg = inputRow.GetComponent<HorizontalLayoutGroup>();
        inHlg.spacing = 16;
        inHlg.childAlignment = TextAnchor.MiddleCenter;

        BuildCraftSlot(inputRow.transform, font, "Slot_Fusion_1", "SLOT 1", "+");
        CreateLabel(inputRow.transform, font, "+", 20, new Color(0.96f, 0.78f, 0.28f, 1f));
        BuildCraftSlot(inputRow.transform, font, "Slot_Fusion_2", "SLOT 2", "+");

        // Slot wynikowy
        BuildCraftSlot(anvilBox.transform, font, "Slot_Fusion_Result", "Wynik", "+");

        // Karta wymagań fuzji
        GameObject reqCard = new GameObject("Pnl_FusionReqs", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        reqCard.transform.SetParent(view.transform, false);
        reqCard.GetComponent<LayoutElement>().preferredHeight = 56;
        reqCard.GetComponent<Image>().sprite = null;
        reqCard.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.9f);

        VerticalLayoutGroup rVlg = reqCard.GetComponent<VerticalLayoutGroup>();
        rVlg.padding = new RectOffset(14, 14, 8, 8);
        rVlg.spacing = 4;

        GameObject reqHdr = new GameObject("Txt_ReqHeader", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        reqHdr.transform.SetParent(reqCard.transform, false);
        TextMeshProUGUI reqHdrTmp = reqHdr.GetComponent<TextMeshProUGUI>();
        reqHdrTmp.font = font;
        reqHdrTmp.fontSize = 13;
        reqHdrTmp.fontStyle = FontStyles.Bold;
        reqHdrTmp.text = "WYMAGANIA FUZJI: <color=#96c628>0 / 2 RUN</color>";
        reqHdrTmp.color = Color.white;

        GameObject reqDesc = new GameObject("Txt_ReqDesc", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        reqDesc.transform.SetParent(reqCard.transform, false);
        TextMeshProUGUI reqDescTmp = reqDesc.GetComponent<TextMeshProUGUI>();
        reqDescTmp.font = font;
        reqDescTmp.fontSize = 12;
        reqDescTmp.text = "Fuzja łączy 2 runy tego samego poziomu w 1 runę o szczebel wyższą (100% szans).";
        reqDescTmp.color = new Color(0.72f, 0.75f, 0.8f, 1f);

        // Przycisk PRZEKUJ FUZJĘ (14 pt bold)
        CreateActionButton(view.transform, font, "Btn_CraftFusion", "PRZEKUJ FUZJĘ", new Color(0.25f, 0.22f, 0.12f, 0.95f), new Color(0.96f, 0.82f, 0.38f, 1f), 14, 46);

        // Podpowiedź na dole
        CreateLabel(view.transform, font, "Przeciągnij lub kliknij runy z ekwipunku po prawej, aby umieścić je na kowadle.", 11, new Color(0.6f, 0.62f, 0.68f, 1f), FontStyles.Italic);

        // Domyślnie widok fuzji ukryty
        view.SetActive(false);
    }

    // Widok 3: Transmutacja run
    private static void BuildTransmutationView(Transform parent, TMP_FontAsset font)
    {
        GameObject view = new GameObject("View_Transmutation", typeof(RectTransform), typeof(VerticalLayoutGroup));
        view.transform.SetParent(parent, false);

        RectTransform viewRt = view.GetComponent<RectTransform>();
        viewRt.anchorMin = Vector2.zero;
        viewRt.anchorMax = Vector2.one;
        viewRt.offsetMin = Vector2.zero;
        viewRt.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = view.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 16, 16);
        vlg.spacing = 14;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        // Pasek górny transmutacji
        GameObject topBar = new GameObject("Pnl_TransmuteTopBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        topBar.transform.SetParent(view.transform, false);
        topBar.GetComponent<LayoutElement>().preferredHeight = 36;

        HorizontalLayoutGroup topHlg = topBar.GetComponent<HorizontalLayoutGroup>();
        topHlg.spacing = 12;
        topHlg.childAlignment = TextAnchor.MiddleLeft;

        CreateActionButton(topBar.transform, font, "Btn_BackToTower", "← POWRÓT DO WIEŻY", new Color(0.18f, 0.2f, 0.26f, 0.95f), Color.white, 13);
        CreateIcon(topBar.transform, "Img_MagicIcon", 24, new Color(0.78f, 0.45f, 0.96f, 1f));

        GameObject titleObj = new GameObject("Txt_TransmuteTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        titleObj.transform.SetParent(topBar.transform, false);
        titleObj.GetComponent<LayoutElement>().flexibleWidth = 1;
        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.fontSize = 16;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.text = "TRANSMUTACJA RUNICZNA (3 RUNY)";
        titleTmp.color = new Color(0.96f, 0.78f, 0.28f, 1f);

        // Kowadło transmutacji (3 wejścia + 1 wynik nieznany ???)
        GameObject anvilBox = new GameObject("Pnl_AnvilTransmute", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        anvilBox.transform.SetParent(view.transform, false);
        anvilBox.GetComponent<LayoutElement>().preferredHeight = 220;

        VerticalLayoutGroup aVlg = anvilBox.GetComponent<VerticalLayoutGroup>();
        aVlg.spacing = 12;
        aVlg.childAlignment = TextAnchor.MiddleCenter;

        // Wiersz 3 slotów wejściowych
        GameObject inputRow = new GameObject("Pnl_InputSlots", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        inputRow.transform.SetParent(anvilBox.transform, false);
        HorizontalLayoutGroup inHlg = inputRow.GetComponent<HorizontalLayoutGroup>();
        inHlg.spacing = 14;
        inHlg.childAlignment = TextAnchor.MiddleCenter;

        BuildCraftSlot(inputRow.transform, font, "Slot_Transmute_1", "SLOT 1", "+");
        CreateLabel(inputRow.transform, font, "+", 18, new Color(0.78f, 0.45f, 0.96f, 1f));
        BuildCraftSlot(inputRow.transform, font, "Slot_Transmute_2", "SLOT 2", "+");
        CreateLabel(inputRow.transform, font, "+", 18, new Color(0.78f, 0.45f, 0.96f, 1f));
        BuildCraftSlot(inputRow.transform, font, "Slot_Transmute_3", "SLOT 3", "+");

        // Slot wynikowy nieznany (Wynik ????)
        BuildCraftSlot(anvilBox.transform, font, "Slot_Transmute_Result", "Wynik\n????", "?");

        // Przycisk PRZEKUJ TRANSMUTACJĘ
        CreateActionButton(view.transform, font, "Btn_CraftTransmutation", "PRZEKUJ TRANSMUTACJĘ", new Color(0.24f, 0.16f, 0.28f, 0.95f), new Color(0.88f, 0.65f, 0.98f, 1f), 14, 46);

        // Podpowiedź na dole
        CreateLabel(view.transform, font, "Przeciągnij lub kliknij runy z ekwipunku po prawej, aby umieścić je na kowadle.", 11, new Color(0.6f, 0.62f, 0.68f, 1f), FontStyles.Italic);

        // Domyślnie widok transmutacji ukryty
        view.SetActive(false);
    }

    private static void BuildCraftSlot(Transform parent, TMP_FontAsset font, string slotName, string label, string plus)
    {
        GameObject slot = new GameObject(slotName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        slot.transform.SetParent(parent, false);

        LayoutElement le = slot.GetComponent<LayoutElement>();
        le.preferredWidth = 96;
        le.preferredHeight = 110;

        Image img = slot.GetComponent<Image>();
        img.sprite = null;
        img.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);

        VerticalLayoutGroup vlg = slot.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(6, 6, 8, 8);
        vlg.spacing = 6;
        vlg.childAlignment = TextAnchor.MiddleCenter;

        GameObject plusObj = new GameObject("Txt_Plus", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        plusObj.transform.SetParent(slot.transform, false);
        TextMeshProUGUI plusTmp = plusObj.GetComponent<TextMeshProUGUI>();
        plusTmp.font = font;
        plusTmp.fontSize = 20;
        plusTmp.text = plus;
        plusTmp.color = new Color(0.6f, 0.65f, 0.75f, 0.8f);
        plusTmp.alignment = TextAlignmentOptions.Center;

        GameObject lblObj = new GameObject("Txt_Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        lblObj.transform.SetParent(slot.transform, false);
        TextMeshProUGUI lblTmp = lblObj.GetComponent<TextMeshProUGUI>();
        lblTmp.font = font;
        lblTmp.fontSize = 11;
        lblTmp.text = label;
        lblTmp.color = new Color(0.72f, 0.75f, 0.8f, 1f);
        lblTmp.alignment = TextAlignmentOptions.Center;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Prawy Panel: Inwentarz Run
    // ─────────────────────────────────────────────────────────────────────────
    private static void BuildRightInventory(Transform parent, TMP_FontAsset font)
    {
        GameObject rightInv = new GameObject("Pnl_RightInventory", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        rightInv.transform.SetParent(parent, false);

        LayoutElement invLe = rightInv.GetComponent<LayoutElement>();
        invLe.preferredWidth = 380;
        invLe.flexibleHeight = 1;

        Image invImg = rightInv.GetComponent<Image>();
        invImg.sprite = null;
        invImg.color = new Color(0.1f, 0.11f, 0.14f, 0.95f);

        VerticalLayoutGroup invVlg = rightInv.GetComponent<VerticalLayoutGroup>();
        invVlg.padding = new RectOffset(12, 12, 12, 12);
        invVlg.spacing = 8;
        invVlg.childAlignment = TextAnchor.UpperCenter;
        invVlg.childControlWidth = true;
        invVlg.childControlHeight = false;

        // Belka nagłówkowa inwentarza
        GameObject header = new GameObject("Pnl_InvHeader", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        header.transform.SetParent(rightInv.transform, false);
        header.GetComponent<LayoutElement>().preferredHeight = 28;

        HorizontalLayoutGroup hHlg = header.GetComponent<HorizontalLayoutGroup>();
        hHlg.spacing = 8;
        hHlg.childAlignment = TextAnchor.MiddleLeft;

        CreateIcon(header.transform, "Img_BagIcon", 24, new Color(0.96f, 0.78f, 0.28f, 1f));

        GameObject titleObj = new GameObject("Txt_InvTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        titleObj.transform.SetParent(header.transform, false);
        titleObj.GetComponent<LayoutElement>().flexibleWidth = 1;
        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.fontSize = 16;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.text = "EKWIPUNEK RUN";
        titleTmp.color = Color.white;

        GameObject countObj = new GameObject("Txt_InvCount", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        countObj.transform.SetParent(header.transform, false);
        countObj.GetComponent<LayoutElement>().preferredWidth = 80;
        TextMeshProUGUI countTmp = countObj.GetComponent<TextMeshProUGUI>();
        countTmp.font = font;
        countTmp.fontSize = 13;
        countTmp.text = "12 / 30";
        countTmp.color = new Color(0.7f, 0.72f, 0.75f, 1f);
        countTmp.alignment = TextAlignmentOptions.MidlineRight;

        // Pasek filtrów rzadkości
        GameObject filterRow = new GameObject("Pnl_FilterRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        filterRow.transform.SetParent(rightInv.transform, false);
        filterRow.GetComponent<LayoutElement>().preferredHeight = 26;
        HorizontalLayoutGroup fHlg = filterRow.GetComponent<HorizontalLayoutGroup>();
        fHlg.spacing = 6;
        fHlg.childAlignment = TextAnchor.MiddleCenter;
        fHlg.childControlWidth = true;
        fHlg.childControlHeight = true;

        CreateActionButton(filterRow.transform, font, "Btn_Filter_All", "Wszystkie", new Color(0.18f, 0.2f, 0.26f, 0.95f), Color.white, 11);
        CreateActionButton(filterRow.transform, font, "Btn_Filter_Common", "Pospolite", new Color(0.14f, 0.16f, 0.2f, 0.95f), new Color(0.8f, 0.8f, 0.8f, 1f), 11);
        CreateActionButton(filterRow.transform, font, "Btn_Filter_Rare", "Rzadkie", new Color(0.12f, 0.18f, 0.28f, 0.95f), new Color(0.35f, 0.65f, 0.95f, 1f), 11);
        CreateActionButton(filterRow.transform, font, "Btn_Filter_Legendary", "Legend.", new Color(0.24f, 0.18f, 0.08f, 0.95f), new Color(0.96f, 0.78f, 0.28f, 1f), 11);

        // Siatka slotów wewnątrz ScrollRect
        GameObject scrollObj = new GameObject("Scroll_Inventory", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(LayoutElement));
        scrollObj.transform.SetParent(rightInv.transform, false);
        scrollObj.GetComponent<LayoutElement>().flexibleHeight = 1;

        Image sImg = scrollObj.GetComponent<Image>();
        sImg.sprite = null;
        sImg.color = new Color(0.06f, 0.07f, 0.09f, 0.4f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(scrollObj.transform, false);
        RectTransform vpRt = viewport.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.sizeDelta = Vector2.zero;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform cntRt = content.GetComponent<RectTransform>();
        cntRt.anchorMin = new Vector2(0f, 1f);
        cntRt.anchorMax = new Vector2(1f, 1f);
        cntRt.pivot = new Vector2(0.5f, 1f);
        cntRt.sizeDelta = new Vector2(0f, 0f);

        GridLayoutGroup glg = content.GetComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(74, 74);
        glg.spacing = new Vector2(8, 8);
        glg.padding = new RectOffset(6, 6, 6, 6);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 4;

        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect sr = scrollObj.GetComponent<ScrollRect>();
        sr.content = cntRt;
        sr.viewport = vpRt;
        sr.horizontal = false;
        sr.vertical = true;

        // Generujemy 16 przykładowych kafelków run
        for (int i = 1; i <= 16; i++)
        {
            BuildRuneCardItem(content.transform, font, i);
        }
    }

    private static void BuildRuneCardItem(Transform parent, TMP_FontAsset font, int index)
    {
        GameObject item = new GameObject($"Rune_Item_{index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
        item.transform.SetParent(parent, false);

        Image img = item.GetComponent<Image>();
        img.sprite = null;
        img.color = new Color(0.14f, 0.16f, 0.2f, 0.95f);

        VerticalLayoutGroup vlg = item.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.spacing = 2;
        vlg.childAlignment = TextAnchor.MiddleCenter;

        CreateIcon(item.transform, "Img_RuneIcon", 24, new Color(0.96f, 0.78f, 0.28f, 0.8f));

        GameObject valObj = new GameObject("Txt_Val", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        valObj.transform.SetParent(item.transform, false);
        TextMeshProUGUI valTmp = valObj.GetComponent<TextMeshProUGUI>();
        valTmp.font = font;
        valTmp.fontSize = 11;
        valTmp.text = index % 2 == 0 ? "+15%" : "+2.5";
        valTmp.color = Color.white;
        valTmp.alignment = TextAlignmentOptions.Center;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Dolny Panel: Statystyki Wieży + Stanowiska Kowali Run
    // ─────────────────────────────────────────────────────────────────────────
    private static void BuildBottomPanel(Transform parent, TMP_FontAsset font)
    {
        GameObject bottomPanel = new GameObject("Pnl_BottomPanel", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        bottomPanel.transform.SetParent(parent, false);

        LayoutElement botLe = bottomPanel.GetComponent<LayoutElement>();
        botLe.preferredHeight = 210;

        HorizontalLayoutGroup botHlg = bottomPanel.GetComponent<HorizontalLayoutGroup>();
        botHlg.spacing = 14;
        botHlg.childAlignment = TextAnchor.UpperLeft;
        botHlg.childControlWidth = true;
        botHlg.childControlHeight = true;

        // Sekcja 1: Zmodyfikowane Statystyki Wieży (flexible width)
        BuildModifiedStatsSection(bottomPanel.transform, font);

        // Sekcja 2: Stanowiska Kowali Run (width 460 px)
        BuildRunesmithsSection(bottomPanel.transform, font);
    }

    private static void BuildModifiedStatsSection(Transform parent, TMP_FontAsset font)
    {
        GameObject secStats = new GameObject("Sec_TowerStats", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        secStats.transform.SetParent(parent, false);

        secStats.GetComponent<LayoutElement>().flexibleWidth = 1;

        Image secImg = secStats.GetComponent<Image>();
        secImg.sprite = null;
        secImg.color = new Color(0.1f, 0.11f, 0.14f, 0.95f);

        VerticalLayoutGroup vlg = secStats.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 12, 12);
        vlg.spacing = 8;
        vlg.childAlignment = TextAnchor.UpperLeft;

        // Tytuł sekcji (16 pt bold - GEMINI.md)
        GameObject titleObj = new GameObject("Txt_StatsTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        titleObj.transform.SetParent(secStats.transform, false);
        titleObj.GetComponent<LayoutElement>().preferredHeight = 22;
        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.fontSize = 16;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.text = "ZMODYFIKOWANE STATYSTYKI WIEŻY";
        titleTmp.color = new Color(0.9f, 0.9f, 0.92f, 1f);

        CreateDivider(secStats.transform);

        // Układ 2 kolumn statystyk
        GameObject statsGrid = new GameObject("Pnl_StatsGrid", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        statsGrid.transform.SetParent(secStats.transform, false);
        statsGrid.GetComponent<LayoutElement>().flexibleHeight = 1;

        HorizontalLayoutGroup gridHlg = statsGrid.GetComponent<HorizontalLayoutGroup>();
        gridHlg.spacing = 24;
        gridHlg.childControlWidth = true;
        gridHlg.childControlHeight = true;

        // Kolumna lewa (Obrażenia, Zasięg, Szybkostrzelność)
        GameObject colLeft = new GameObject("Col_Left", typeof(RectTransform), typeof(VerticalLayoutGroup));
        colLeft.transform.SetParent(statsGrid.transform, false);
        colLeft.GetComponent<VerticalLayoutGroup>().spacing = 6;

        BuildStatRow(colLeft.transform, font, "Obrażenia", "45", "58 (+28%)");
        BuildStatRow(colLeft.transform, font, "Zasięg", "12.0", "14.5 (+2.5)");
        BuildStatRow(colLeft.transform, font, "Szybkostrzelność", "1.2s", "1.0s (-16%)");

        // Kolumna prawa (Przebicie pancerza, Szansa krytyka, Obrażenia krytyczne)
        GameObject colRight = new GameObject("Col_Right", typeof(RectTransform), typeof(VerticalLayoutGroup));
        colRight.transform.SetParent(statsGrid.transform, false);
        colRight.GetComponent<VerticalLayoutGroup>().spacing = 6;

        BuildStatRow(colRight.transform, font, "Przebicie pancerza", "15%", "25% (+10%)");
        BuildStatRow(colRight.transform, font, "Szansa na krytyk", "5%", "12% (+7%)");
        BuildStatRow(colRight.transform, font, "Mnożnik krytyczny", "150%", "180% (+30%)");
    }

    private static void BuildStatRow(Transform parent, TMP_FontAsset font, string statName, string baseVal, string modVal)
    {
        GameObject row = new GameObject("Row_" + statName.Replace(" ", "_"), typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<LayoutElement>().preferredHeight = 22;

        HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        // Etykieta (13 pt)
        GameObject lblObj = new GameObject("Txt_Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        lblObj.transform.SetParent(row.transform, false);
        lblObj.GetComponent<LayoutElement>().preferredWidth = 140;
        TextMeshProUGUI lblTmp = lblObj.GetComponent<TextMeshProUGUI>();
        lblTmp.font = font;
        lblTmp.fontSize = 13;
        lblTmp.text = statName + ":";
        lblTmp.color = new Color(0.72f, 0.75f, 0.8f, 1f);

        // Baza -> Zmodyfikowana
        GameObject valObj = new GameObject("Txt_Value", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        valObj.transform.SetParent(row.transform, false);
        valObj.GetComponent<LayoutElement>().flexibleWidth = 1;
        TextMeshProUGUI valTmp = valObj.GetComponent<TextMeshProUGUI>();
        valTmp.font = font;
        valTmp.fontSize = 13;
        valTmp.fontStyle = FontStyles.Bold;
        valTmp.text = $"<color=#888888>{baseVal}</color> → <color=#45f375>{modVal}</color>";
    }

    private static void BuildRunesmithsSection(Transform parent, TMP_FontAsset font)
    {
        GameObject secSmiths = new GameObject("Sec_Runesmiths", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        secSmiths.transform.SetParent(parent, false);

        LayoutElement secLe = secSmiths.GetComponent<LayoutElement>();
        secLe.preferredWidth = 460;

        Image secImg = secSmiths.GetComponent<Image>();
        secImg.sprite = null;
        secImg.color = new Color(0.1f, 0.11f, 0.14f, 0.95f);

        VerticalLayoutGroup vlg = secSmiths.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 12, 12);
        vlg.spacing = 8;
        vlg.childAlignment = TextAnchor.UpperLeft;

        // Tytuł sekcji (16 pt bold - GEMINI.md)
        GameObject titleObj = new GameObject("Txt_SmithsTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        titleObj.transform.SetParent(secSmiths.transform, false);
        titleObj.GetComponent<LayoutElement>().preferredHeight = 22;
        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.fontSize = 16;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.text = "STANOWISKA KOWALI RUN";
        titleTmp.color = new Color(0.9f, 0.9f, 0.92f, 1f);

        CreateDivider(secSmiths.transform);

        // Stepper kowali ([-] 1 / 5 [+])
        GameObject stepperRow = new GameObject("Pnl_SmithsStepper", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        stepperRow.transform.SetParent(secSmiths.transform, false);
        stepperRow.GetComponent<LayoutElement>().preferredHeight = 32;

        HorizontalLayoutGroup sHlg = stepperRow.GetComponent<HorizontalLayoutGroup>();
        sHlg.spacing = 12;
        sHlg.childAlignment = TextAnchor.MiddleLeft;

        CreateActionButton(stepperRow.transform, font, "Btn_RemoveSmith", "-", new Color(0.24f, 0.14f, 0.14f, 0.95f), Color.white, 16, 32, 32);

        GameObject countObj = new GameObject("Txt_SmithsCount", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        countObj.transform.SetParent(stepperRow.transform, false);
        countObj.GetComponent<LayoutElement>().preferredWidth = 160;
        TextMeshProUGUI countTmp = countObj.GetComponent<TextMeshProUGUI>();
        countTmp.font = font;
        countTmp.fontSize = 16;
        countTmp.fontStyle = FontStyles.Bold;
        countTmp.text = "Kowale: 1 / 5";
        countTmp.color = Color.white;
        countTmp.alignment = TextAlignmentOptions.Center;

        CreateActionButton(stepperRow.transform, font, "Btn_AddSmith", "+", new Color(0.14f, 0.24f, 0.16f, 0.95f), Color.white, 16, 32, 32);

        // Rząd 5 stanowisk kowali
        GameObject stationsRow = new GameObject("Pnl_StationsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        stationsRow.transform.SetParent(secSmiths.transform, false);
        stationsRow.GetComponent<LayoutElement>().preferredHeight = 52;

        HorizontalLayoutGroup stHlg = stationsRow.GetComponent<HorizontalLayoutGroup>();
        stHlg.spacing = 8;
        stHlg.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 1; i <= 5; i++)
        {
            BuildSmithStationCard(stationsRow.transform, font, i, i == 1, i <= 2);
        }

        // Podpowiedź o Wieżach Runicznych
        CreateLabel(secSmiths.transform, font, "Każda sąsiadująca Wieża Runiczna odblokowuje 1 kowala oraz 1 slot na runę.", 11, new Color(0.55f, 0.58f, 0.65f, 1f), FontStyles.Italic);
    }

    private static void BuildSmithStationCard(Transform parent, TMP_FontAsset font, int index, bool hasSmith, bool isUnlocked)
    {
        GameObject card = new GameObject($"Station_{index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        card.transform.SetParent(parent, false);

        LayoutElement le = card.GetComponent<LayoutElement>();
        le.preferredWidth = 72;
        le.preferredHeight = 50;

        Image img = card.GetComponent<Image>();
        img.sprite = null;
        img.color = hasSmith ? new Color(0.18f, 0.28f, 0.18f, 0.95f) : (isUnlocked ? new Color(0.14f, 0.16f, 0.2f, 0.95f) : new Color(0.08f, 0.09f, 0.11f, 0.85f));

        VerticalLayoutGroup vlg = card.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(4, 4, 4, 4);
        vlg.spacing = 2;
        vlg.childAlignment = TextAnchor.MiddleCenter;

        CreateIcon(card.transform, "Img_StationIcon", 24, hasSmith ? new Color(0.45f, 0.95f, 0.55f, 1f) : new Color(0.5f, 0.52f, 0.6f, 0.6f));

        GameObject lblObj = new GameObject("Txt_StationLbl", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        lblObj.transform.SetParent(card.transform, false);
        TextMeshProUGUI lblTmp = lblObj.GetComponent<TextMeshProUGUI>();
        lblTmp.font = font;
        lblTmp.fontSize = 10;
        lblTmp.text = hasSmith ? "Kowal 1" : (isUnlocked ? "Wolny" : "Zablok.");
        lblTmp.color = hasSmith ? new Color(0.45f, 0.95f, 0.55f, 1f) : new Color(0.6f, 0.62f, 0.68f, 1f);
        lblTmp.alignment = TextAlignmentOptions.Center;
    }

    // =========================================================================
    // WSPÓLNE METODY POMOCNICZE
    // =========================================================================

    private static void CreateIcon(Transform parent, string name, float size, Color color)
    {
        GameObject iconObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconObj.transform.SetParent(parent, false);
        RectTransform rt = iconObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        LayoutElement le = iconObj.GetComponent<LayoutElement>();
        le.preferredWidth = size;
        le.preferredHeight = size;
        Image img = iconObj.GetComponent<Image>();
        img.sprite = null;
        img.color = color;
    }

    private static void CreateCloseButton(Transform parent, TMP_FontAsset font, string name, float size)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        LayoutElement le = btnObj.GetComponent<LayoutElement>();
        le.preferredWidth = size;
        le.preferredHeight = size;
        Image img = btnObj.GetComponent<Image>();
        img.sprite = null;
        img.color = new Color(0.25f, 0.12f, 0.12f, 0.85f);

        GameObject txtObj = new GameObject("Txt_Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = 16;
        tmp.fontStyle = FontStyles.Bold;
        tmp.text = "X";
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    private static void CreateActionButton(Transform parent, TMP_FontAsset font, string name, string label, Color bgColor, Color textColor, float fontSize, float height = 36, float width = 0)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        btnObj.transform.SetParent(parent, false);
        LayoutElement le = btnObj.GetComponent<LayoutElement>();
        le.preferredHeight = height;
        if (width > 0) le.preferredWidth = width;

        Image img = btnObj.GetComponent<Image>();
        img.sprite = null;
        img.color = bgColor;

        GameObject txtObj = new GameObject("Txt_Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.text = label;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    private static void CreateNavTabButton(Transform parent, TMP_FontAsset font, string name, string label, bool isActive)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        btnObj.transform.SetParent(parent, false);
        LayoutElement le = btnObj.GetComponent<LayoutElement>();
        le.preferredHeight = 44;

        Image img = btnObj.GetComponent<Image>();
        img.sprite = null;
        img.color = isActive ? new Color(0.24f, 0.2f, 0.12f, 0.95f) : new Color(0.14f, 0.16f, 0.2f, 0.95f);

        GameObject txtObj = new GameObject("Txt_Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = 14;
        tmp.fontStyle = FontStyles.Bold;
        tmp.text = label;
        tmp.color = isActive ? new Color(0.96f, 0.82f, 0.38f, 1f) : new Color(0.75f, 0.78f, 0.82f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
    }

    private static void CreateDivider(Transform parent)
    {
        GameObject divObj = new GameObject("Img_Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        divObj.transform.SetParent(parent, false);
        LayoutElement le = divObj.GetComponent<LayoutElement>();
        le.preferredHeight = 1;
        Image img = divObj.GetComponent<Image>();
        img.sprite = null;
        img.color = new Color(1f, 1f, 1f, 0.12f);
    }

    private static void CreateLabel(Transform parent, TMP_FontAsset font, string text, float fontSize, Color color, FontStyles style = FontStyles.Normal)
    {
        GameObject lblObj = new GameObject("Txt_Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        lblObj.transform.SetParent(parent, false);
        lblObj.GetComponent<LayoutElement>().preferredHeight = 20;
        TextMeshProUGUI tmp = lblObj.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.text = text;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
    }
}
