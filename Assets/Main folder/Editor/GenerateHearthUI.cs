using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class GenerateHearthUI
{
    private const string SessionKey = "HearthUI_Built_v1";
    private const string ScenePath = "Assets/Main folder/_Scenes/TESTSCENE_UI.unity";
    private const string PrefabFolder = "Assets/Main folder/Prefabs/UI";
    private const string LevelCardPrefabPath = "Assets/Main folder/Prefabs/UI/Card_HearthLevel.prefab";
    private const string EffectItemPrefabPath = "Assets/Main folder/Prefabs/UI/Pnl_HearthEffectItem.prefab";
    private const string SingleResourceSmallPrefabPath = "Assets/Main folder/Prefabs/UI/Pnl_SingleResource_Small.prefab";
    private const string VisualsDBPath = "Assets/Main folder/ScriptableObjects/Configs/ResourceVisualDatabase.asset";
    private const string FreeWorkerIconPath = "Assets/Main folder/Icons/Citizen State/free.png";
    private const string RoundedSpritePath = "Assets/Main folder/Icons/PlaceHolders/kwadrat zaokrolony gid512px.png";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    static GenerateHearthUI()
    {
        EditorApplication.delayCall += OnReady;
    }

    private static void OnReady()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        BuildAll();
    }

    [MenuItem("Tools/Generate Hearth UI")]
    public static void BuildAll()
    {
        Debug.Log("[HearthUI] Rozpoczynanie generowania UI Wielkiego Paleniska Twierdzy...");

        if (!Directory.Exists(PrefabFolder))
        {
            Directory.CreateDirectory(PrefabFolder);
        }

        // 1. Otwórz scenę TESTSCENE_UI
        var currentScene = EditorSceneManager.GetActiveScene();
        if (currentScene.path != ScenePath)
        {
            currentScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // 2. Załaduj czcionkę i zasoby pomocnicze
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
        {
            font = TMP_Settings.defaultFontAsset;
        }

        Sprite roundedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
        Sprite freeWorkerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FreeWorkerIconPath);
        GameObject singleResSmallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SingleResourceSmallPrefabPath);
        ResourceVisualsSO visualsDB = AssetDatabase.LoadAssetAtPath<ResourceVisualsSO>(VisualsDBPath);
        Sprite coalSprite = visualsDB != null ? visualsDB.GetIcon(ResourceType.Coal) : null;

        // 3. Wygeneruj prefaby: Card_HearthLevel.prefab oraz Pnl_HearthEffectItem.prefab
        GameObject levelCardPrefab = CreateHearthLevelCardPrefab(font, roundedSprite, singleResSmallPrefab, visualsDB, coalSprite);
        GameObject effectItemPrefab = CreateHearthEffectItemPrefab(font, roundedSprite);

        // 4. Znajdź Canvas_ContextWindows
        GameObject canvasObj = GameObject.Find("Canvas_ContextWindows");
        if (canvasObj == null)
        {
            Debug.LogError("[HearthUI] Nie znaleziono Canvas_ContextWindows na scenie TESTSCENE_UI!");
            return;
        }

        // Usuń poprzednią wersję Pnl_HearthWindow jeśli istnieje
        Transform oldHearth = canvasObj.transform.Find("Pnl_HearthWindow");
        if (oldHearth != null)
        {
            Undo.DestroyObjectImmediate(oldHearth.gameObject);
        }

        // 5. Zbuduj hierarchię Pnl_HearthWindow -> Pnl_Hearth (1500x900)
        GameObject hearthWindow = BuildHearthWindowHierarchy(
            canvasObj.transform,
            font,
            roundedSprite,
            freeWorkerSprite,
            coalSprite,
            singleResSmallPrefab,
            visualsDB,
            levelCardPrefab,
            effectItemPrefab
        );

        // Domyślnie ukryj okno (tak jak pozostałe okna w Canvas_ContextWindows)
        hearthWindow.SetActive(false);

        // 6. Zapisz scenę i assety
        Undo.RegisterCreatedObjectUndo(hearthWindow, "Generate Hearth UI");
        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);
        AssetDatabase.SaveAssets();

        Debug.Log("[HearthUI] Pomyślnie wygenerowano Card_HearthLevel.prefab, Pnl_HearthEffectItem.prefab oraz Pnl_HearthWindow (1500x900) na scenie TESTSCENE_UI!");
    }

    // =========================================================================
    // 1. PREFAB: Card_HearthLevel.prefab
    // =========================================================================

    private static GameObject CreateHearthLevelCardPrefab(
        TMP_FontAsset font,
        Sprite roundedSprite,
        GameObject singleResSmallPrefab,
        ResourceVisualsSO visualsDB,
        Sprite coalSprite)
    {
        GameObject card = new GameObject(
            "Card_HearthLevel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(Outline),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement)
        );

        RectTransform cardRt = card.GetComponent<RectTransform>();
        cardRt.sizeDelta = new Vector2(260f, 150f);

        LayoutElement cardLe = card.GetComponent<LayoutElement>();
        cardLe.preferredWidth = 260f;
        cardLe.preferredHeight = 150f;
        cardLe.flexibleWidth = 1f;

        Image cardImg = card.GetComponent<Image>();
        ApplyRoundedSprite(cardImg, roundedSprite, new Color(0.08f, 0.09f, 0.12f, 0.96f));

        Outline cardOutline = card.GetComponent<Outline>();
        cardOutline.effectColor = new Color(1f, 1f, 1f, 0.08f);
        cardOutline.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = card.GetComponent<Button>();
        btn.targetGraphic = cardImg;

        VerticalLayoutGroup vlg = card.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 10, 12);
        vlg.spacing = 8f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Górny pasek aktywnego wyboru (IgnoreLayout)
        GameObject topIndicator = new GameObject("Img_SelectedTopBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        topIndicator.transform.SetParent(card.transform, false);
        LayoutElement indLe = topIndicator.GetComponent<LayoutElement>();
        indLe.ignoreLayout = true;
        RectTransform indRt = topIndicator.GetComponent<RectTransform>();
        indRt.anchorMin = new Vector2(0.35f, 1f);
        indRt.anchorMax = new Vector2(0.65f, 1f);
        indRt.pivot = new Vector2(0.5f, 1f);
        indRt.anchoredPosition = Vector2.zero;
        indRt.sizeDelta = new Vector2(0f, 4f);
        Image indImg = topIndicator.GetComponent<Image>();
        indImg.color = new Color(0.98f, 0.78f, 0.25f, 1f);
        topIndicator.SetActive(false);

        // ── Wiersz Górny: POZ. X + Koszt Węgla / DARMOWY ──
        GameObject topRow = new GameObject("Pnl_CardTopRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        topRow.transform.SetParent(card.transform, false);
        RectTransform topRowRt = topRow.GetComponent<RectTransform>();
        topRowRt.sizeDelta = new Vector2(0f, 28f);
        LayoutElement topRowLe = topRow.GetComponent<LayoutElement>();
        topRowLe.preferredHeight = 28f;

        HorizontalLayoutGroup topHlg = topRow.GetComponent<HorizontalLayoutGroup>();
        topHlg.spacing = 6f;
        topHlg.childAlignment = TextAnchor.MiddleLeft;
        topHlg.childControlWidth = true;
        topHlg.childControlHeight = false;
        topHlg.childForceExpandWidth = false;
        topHlg.childForceExpandHeight = false;

        // Etykieta "POZ. X" (12 pt Bold)
        GameObject lvlIdxObj = CreateText(topRow.transform, font, "Txt_LevelIndex", "POZ. 3", 12, FontStyles.Bold, new Color(0.68f, 0.72f, 0.80f, 1f), TextAlignmentOptions.MidlineLeft);
        lvlIdxObj.GetComponent<RectTransform>().sizeDelta = new Vector2(70f, 26f);
        LayoutElement idxLe = lvlIdxObj.AddComponent<LayoutElement>();
        idxLe.flexibleWidth = 1f;
        idxLe.preferredHeight = 26f;

        // Kontener kosztu po prawej
        GameObject costContainer = new GameObject("Pnl_CostContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        costContainer.transform.SetParent(topRow.transform, false);
        costContainer.GetComponent<RectTransform>().sizeDelta = new Vector2(86f, 28f);
        LayoutElement costContLe = costContainer.GetComponent<LayoutElement>();
        costContLe.preferredHeight = 28f;

        HorizontalLayoutGroup costHlg = costContainer.GetComponent<HorizontalLayoutGroup>();
        costHlg.childAlignment = TextAnchor.MiddleRight;
        costHlg.childControlWidth = false;
        costHlg.childControlHeight = false;
        costHlg.childForceExpandWidth = false;
        costHlg.childForceExpandHeight = false;

        // Instancja Pnl_SingleResource_Small dla kosztu węgla
        if (singleResSmallPrefab != null)
        {
            GameObject resInst = (GameObject)PrefabUtility.InstantiatePrefab(singleResSmallPrefab, costContainer.transform);
            resInst.name = "Pnl_CoalCost";
            RectTransform resRt = resInst.GetComponent<RectTransform>();
            if (resRt != null) resRt.sizeDelta = new Vector2(76f, 26f);
            ConfigureSingleResourceSmall(resInst, ResourceType.Coal, 35, visualsDB, coalSprite);
        }
        else
        {
            CreateResourceBadgeFallback(costContainer.transform, font, roundedSprite, "Pnl_CoalCost", coalSprite, "35", 76f, 26f);
        }

        // Pastylka "DARMOWY" (dla Poziomu 1)
        GameObject freeBadge = new GameObject("Badge_Free", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(LayoutElement));
        freeBadge.transform.SetParent(costContainer.transform, false);
        RectTransform freeRt = freeBadge.GetComponent<RectTransform>();
        freeRt.sizeDelta = new Vector2(82f, 24f);
        LayoutElement freeLe = freeBadge.GetComponent<LayoutElement>();
        freeLe.preferredWidth = 82f;
        freeLe.preferredHeight = 24f;
        ApplyRoundedSprite(freeBadge.GetComponent<Image>(), roundedSprite, new Color(0.06f, 0.20f, 0.26f, 0.95f));
        Outline freeOut = freeBadge.GetComponent<Outline>();
        freeOut.effectColor = new Color(0.20f, 0.68f, 0.85f, 0.65f);
        freeOut.effectDistance = new Vector2(1f, -1f);

        GameObject freeTxtObj = CreateText(freeBadge.transform, font, "Txt_Free", "DARMOWY", 10, FontStyles.Bold, new Color(0.35f, 0.85f, 0.98f, 1f), TextAlignmentOptions.Center);
        StretchFull(freeTxtObj.GetComponent<RectTransform>());
        freeBadge.SetActive(false);

        // ── Środek: Pojedyncza Ikona Poziomu (32x32 px wg GEMINI.md) ──
        GameObject iconRow = new GameObject("Pnl_IconContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        iconRow.transform.SetParent(card.transform, false);
        iconRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 36f);
        LayoutElement iconRowLe = iconRow.GetComponent<LayoutElement>();
        iconRowLe.preferredHeight = 36f;

        HorizontalLayoutGroup iconHlg = iconRow.GetComponent<HorizontalLayoutGroup>();
        iconHlg.childAlignment = TextAnchor.MiddleCenter;
        iconHlg.childControlWidth = false;
        iconHlg.childControlHeight = false;
        iconHlg.childForceExpandWidth = false;
        iconHlg.childForceExpandHeight = false;

        CreateIcon(iconRow.transform, "Img_LevelIcon", 32f, new Color(0.98f, 0.56f, 0.16f, 1f));

        // ── Nazwa Poziomu (14 pt Bold) ──
        GameObject titleObj = CreateText(card.transform, font, "Txt_LevelTitle", "ZRÓWNOWAŻONY PŁOMIEŃ", 14, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        titleObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 22f);
        LayoutElement titleLe = titleObj.AddComponent<LayoutElement>();
        titleLe.preferredHeight = 22f;

        // ── Dolna Pastylka Tagu (10 pt Bold) ──
        GameObject tagRow = new GameObject("Pnl_TagRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        tagRow.transform.SetParent(card.transform, false);
        tagRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 24f);
        LayoutElement tagRowLe = tagRow.GetComponent<LayoutElement>();
        tagRowLe.preferredHeight = 24f;

        HorizontalLayoutGroup tagRowHlg = tagRow.GetComponent<HorizontalLayoutGroup>();
        tagRowHlg.childAlignment = TextAnchor.MiddleCenter;
        tagRowHlg.childControlWidth = false;
        tagRowHlg.childControlHeight = false;
        tagRowHlg.childForceExpandWidth = false;
        tagRowHlg.childForceExpandHeight = false;

        GameObject tagBadge = new GameObject("Badge_LevelTag", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        tagBadge.transform.SetParent(tagRow.transform, false);
        tagBadge.GetComponent<RectTransform>().sizeDelta = new Vector2(148f, 22f);
        LayoutElement tagBadgeLe = tagBadge.GetComponent<LayoutElement>();
        tagBadgeLe.preferredWidth = 148f;
        tagBadgeLe.preferredHeight = 22f;

        ApplyRoundedSprite(tagBadge.GetComponent<Image>(), roundedSprite, new Color(0.16f, 0.18f, 0.22f, 0.95f));
        Outline tagOutline = tagBadge.GetComponent<Outline>();
        tagOutline.effectColor = new Color(0.55f, 0.60f, 0.68f, 0.65f);
        tagOutline.effectDistance = new Vector2(1f, -1f);

        HorizontalLayoutGroup badgeInnerHlg = tagBadge.GetComponent<HorizontalLayoutGroup>();
        badgeInnerHlg.padding = new RectOffset(10, 10, 2, 2);
        badgeInnerHlg.childAlignment = TextAnchor.MiddleCenter;
        badgeInnerHlg.childControlWidth = true;
        badgeInnerHlg.childControlHeight = true;

        CreateText(tagBadge.transform, font, "Txt_Tag", "RÓWNOWAGA", 10, FontStyles.Bold, new Color(0.82f, 0.85f, 0.90f, 1f), TextAlignmentOptions.Center);

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(card, LevelCardPrefabPath.Replace("\\", "/"));
        Object.DestroyImmediate(card);
        return savedPrefab;
    }

    // =========================================================================
    // 2. PREFAB: Pnl_HearthEffectItem.prefab
    // =========================================================================

    private static GameObject CreateHearthEffectItemPrefab(TMP_FontAsset font, Sprite roundedSprite)
    {
        GameObject item = new GameObject(
            "Pnl_HearthEffectItem",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement),
            typeof(ContentSizeFitter)
        );

        RectTransform itemRt = item.GetComponent<RectTransform>();
        itemRt.sizeDelta = new Vector2(680f, 68f);

        ApplyRoundedSprite(item.GetComponent<Image>(), roundedSprite, new Color(0.10f, 0.11f, 0.15f, 0.95f));

        Outline itemOutline = item.GetComponent<Outline>();
        itemOutline.effectColor = new Color(1f, 1f, 1f, 0.06f);
        itemOutline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup vlg = item.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 10, 10);
        vlg.spacing = 4f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        LayoutElement le = item.GetComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.minHeight = 46f;
        le.preferredHeight = 68f;

        ContentSizeFitter csf = item.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── Górny wiersz: Ikona 24px + Tytuł Efektu + Wartość Modyfikatora ──
        GameObject topRow = new GameObject("Pnl_TopRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        topRow.transform.SetParent(item.transform, false);
        topRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 24f);
        LayoutElement topRowLe = topRow.GetComponent<LayoutElement>();
        topRowLe.preferredHeight = 24f;

        HorizontalLayoutGroup topHlg = topRow.GetComponent<HorizontalLayoutGroup>();
        topHlg.spacing = 8f;
        topHlg.childAlignment = TextAnchor.MiddleLeft;
        topHlg.childControlWidth = true;
        topHlg.childControlHeight = false;
        topHlg.childForceExpandWidth = false;
        topHlg.childForceExpandHeight = false;

        // Mała ikona 24x24 px (zgodnie z GEMINI.md)
        CreateIcon(topRow.transform, "Img_EffectIcon", 24f, new Color(0.95f, 0.75f, 0.30f, 1f));

        // Nazwa efektu (13 pt Bold)
        GameObject titleObj = CreateText(topRow.transform, font, "Txt_EffectTitle", "OBRONA WIEŻ I MACHINY", 13, FontStyles.Bold, new Color(0.86f, 0.88f, 0.92f, 1f), TextAlignmentOptions.MidlineLeft);
        titleObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 24f);
        LayoutElement titleLe = titleObj.AddComponent<LayoutElement>();
        titleLe.flexibleWidth = 1f;
        titleLe.preferredHeight = 24f;

        // Wartość modyfikatora po prawej (13 pt Bold)
        GameObject valObj = CreateText(topRow.transform, font, "Txt_EffectValue", "100% (Baza)", 13, FontStyles.Bold, new Color(0.75f, 0.78f, 0.84f, 1f), TextAlignmentOptions.MidlineRight);
        valObj.GetComponent<RectTransform>().sizeDelta = new Vector2(210f, 24f);
        LayoutElement valLe = valObj.AddComponent<LayoutElement>();
        valLe.preferredWidth = 210f;
        valLe.preferredHeight = 24f;

        // ── Dolny opis fabularny / pomocniczy (12 pt Regular) ──
        GameObject descObj = CreateText(
            item.transform,
            font,
            "Txt_EffectDesc",
            "Standardowe parametry balist, katapult i załogi. Brak premii bojowych i kar.",
            12,
            FontStyles.Normal,
            new Color(0.60f, 0.63f, 0.70f, 1f),
            TextAlignmentOptions.TopLeft
        );
        descObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 20f);
        LayoutElement descLe = descObj.AddComponent<LayoutElement>();
        descLe.flexibleWidth = 1f;
        descLe.minHeight = 18f;

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(item, EffectItemPrefabPath.Replace("\\", "/"));
        Object.DestroyImmediate(item);
        return savedPrefab;
    }

    // =========================================================================
    // 3. HIERARCHIA GŁÓWNEGO OKNA: Pnl_HearthWindow -> Pnl_Hearth (1500x900)
    // =========================================================================

    private static GameObject BuildHearthWindowHierarchy(
        Transform canvasParent,
        TMP_FontAsset font,
        Sprite roundedSprite,
        Sprite freeWorkerSprite,
        Sprite coalSprite,
        GameObject singleResSmallPrefab,
        ResourceVisualsSO visualsDB,
        GameObject levelCardPrefab,
        GameObject effectItemPrefab)
    {
        // Pełnoekranowy kontener (wzorem Pnl_RuneForgeFullscreen)
        GameObject root = new GameObject("Pnl_HearthWindow", typeof(RectTransform), typeof(CanvasRenderer));
        root.transform.SetParent(canvasParent, false);
        StretchFull(root.GetComponent<RectTransform>());

        // Ciemny blok tła pod spodem (Img_BackgroundBlock)
        GameObject bgBlock = new GameObject("Img_BackgroundBlock", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        bgBlock.transform.SetParent(root.transform, false);
        StretchFull(bgBlock.GetComponent<RectTransform>());
        bgBlock.GetComponent<LayoutElement>().ignoreLayout = true;
        Image bgBlockImg = bgBlock.GetComponent<Image>();
        bgBlockImg.color = new Color(0.02f, 0.02f, 0.04f, 0.82f);

        // Główne okno Pnl_Hearth (1500x900, VerticalLayoutGroup + ContentSizeFitter Preferred Height)
        GameObject hearthModal = new GameObject(
            "Pnl_Hearth",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter)
        );
        hearthModal.transform.SetParent(root.transform, false);

        RectTransform modalRt = hearthModal.GetComponent<RectTransform>();
        modalRt.anchorMin = new Vector2(0.5f, 0.5f);
        modalRt.anchorMax = new Vector2(0.5f, 0.5f);
        modalRt.pivot = new Vector2(0.5f, 0.5f);
        modalRt.anchoredPosition = Vector2.zero;
        modalRt.sizeDelta = new Vector2(1500f, 900f);

        Image modalImg = hearthModal.GetComponent<Image>();
        modalImg.color = new Color(0.06f, 0.07f, 0.09f, 0.98f);

        Outline modalOutline = hearthModal.GetComponent<Outline>();
        modalOutline.effectColor = new Color(0.85f, 0.45f, 0.12f, 0.85f);
        modalOutline.effectDistance = new Vector2(2f, -2f);

        VerticalLayoutGroup modalVlg = hearthModal.GetComponent<VerticalLayoutGroup>();
        modalVlg.padding = new RectOffset(24, 24, 20, 20);
        modalVlg.spacing = 14f;
        modalVlg.childAlignment = TextAnchor.UpperCenter;
        modalVlg.childControlWidth = true;
        modalVlg.childControlHeight = true;
        modalVlg.childForceExpandWidth = true;
        modalVlg.childForceExpandHeight = false;

        ContentSizeFitter modalCsf = hearthModal.GetComponent<ContentSizeFitter>();
        modalCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        modalCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 1. Pasek Górny (Pnl_TopBar)
        BuildTopBar(hearthModal.transform, font, roundedSprite);

        // 2. Pasek Informacyjny + Wolni Pracownicy + Zapas Węgla (Pnl_InfoAndCoalBar)
        BuildInfoAndCoalBar(hearthModal.transform, font, roundedSprite, freeWorkerSprite, coalSprite, singleResSmallPrefab, visualsDB);

        // 3. Centralny Box Wizualizacji Paleniska (Pnl_HearthPreviewBox)
        BuildHearthPreviewBox(hearthModal.transform, font, roundedSprite);

        // 4. Rząd 5 Kart Poziomów Paleniska (Pnl_LevelsRow)
        BuildLevelsRow(hearthModal.transform, levelCardPrefab);

        // 5. Dwie Kolumny Efektów: Tej Nocy i Następnego Dnia (Pnl_EffectsColumnsRow)
        BuildEffectsColumnsRow(hearthModal.transform, font, roundedSprite, effectItemPrefab);

        // 6. Dolna Belka Stopki (Pnl_FooterBar)
        BuildFooterBar(hearthModal.transform, font, roundedSprite);

        return root;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SEKCJA 1: Pnl_TopBar
    // ─────────────────────────────────────────────────────────────────────────
    private static void BuildTopBar(Transform parent, TMP_FontAsset font, Sprite roundedSprite)
    {
        GameObject topBar = new GameObject("Pnl_TopBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        topBar.transform.SetParent(parent, false);
        topBar.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 52f);
        LayoutElement topLe = topBar.GetComponent<LayoutElement>();
        topLe.preferredHeight = 52f;

        HorizontalLayoutGroup hlg = topBar.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 14f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // Ikona płomienia w nagłówku (32x32 px - Standard Icon wg GEMINI.md)
        CreateIcon(topBar.transform, "Img_HearthHeaderIcon", 32f, new Color(0.98f, 0.48f, 0.14f, 1f));

        // Kolumna Tytułu i Podtytułu
        GameObject titleCol = new GameObject("Pnl_TitleColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        titleCol.transform.SetParent(topBar.transform, false);
        titleCol.GetComponent<RectTransform>().sizeDelta = new Vector2(900f, 50f);
        LayoutElement titleColLe = titleCol.GetComponent<LayoutElement>();
        titleColLe.flexibleWidth = 1f;
        titleColLe.preferredWidth = 900f;
        titleColLe.preferredHeight = 50f;

        VerticalLayoutGroup titleVlg = titleCol.GetComponent<VerticalLayoutGroup>();
        titleVlg.spacing = 2f;
        titleVlg.childAlignment = TextAnchor.MiddleLeft;
        titleVlg.childControlWidth = true;
        titleVlg.childControlHeight = false;
        titleVlg.childForceExpandWidth = false;
        titleVlg.childForceExpandHeight = false;

        // Wiersz tytułu głównego + pastylki MENU PALENISKA
        GameObject titleRow = new GameObject("Pnl_TitleRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        titleRow.transform.SetParent(titleCol.transform, false);
        titleRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 28f);
        titleRow.GetComponent<LayoutElement>().preferredHeight = 28f;

        HorizontalLayoutGroup titleRowHlg = titleRow.GetComponent<HorizontalLayoutGroup>();
        titleRowHlg.spacing = 14f;
        titleRowHlg.childAlignment = TextAnchor.MiddleLeft;
        titleRowHlg.childControlWidth = false;
        titleRowHlg.childControlHeight = false;
        titleRowHlg.childForceExpandWidth = false;
        titleRowHlg.childForceExpandHeight = false;

        // Tytuł okna (20 pt Bold - GEMINI.md)
        GameObject titleObj = CreateText(titleRow.transform, font, "Txt_WindowTitle", "WIELKIE PALENISKO TWIERDZY", 20, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft);
        titleObj.GetComponent<RectTransform>().sizeDelta = new Vector2(390f, 28f);
        LayoutElement titleTxtLe = titleObj.AddComponent<LayoutElement>();
        titleTxtLe.preferredWidth = 390f;
        titleTxtLe.preferredHeight = 28f;

        // Pastylka "MENU PALENISKA" (10 pt Bold)
        GameObject menuBadge = new GameObject("Badge_Menu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(LayoutElement));
        menuBadge.transform.SetParent(titleRow.transform, false);
        menuBadge.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 24f);
        LayoutElement mbLe = menuBadge.GetComponent<LayoutElement>();
        mbLe.preferredWidth = 140f;
        mbLe.preferredHeight = 24f;
        ApplyRoundedSprite(menuBadge.GetComponent<Image>(), roundedSprite, new Color(0.24f, 0.14f, 0.06f, 0.95f));
        Outline mbOut = menuBadge.GetComponent<Outline>();
        mbOut.effectColor = new Color(0.92f, 0.50f, 0.14f, 0.85f);
        mbOut.effectDistance = new Vector2(1f, -1f);

        GameObject mbTxt = CreateText(menuBadge.transform, font, "Txt_MenuBadge", "MENU PALENISKA", 10, FontStyles.Bold, new Color(0.98f, 0.62f, 0.20f, 1f), TextAlignmentOptions.Center);
        StretchFull(mbTxt.GetComponent<RectTransform>());

        // Podtytuł (12 pt Bold)
        GameObject subObj = CreateText(
            titleCol.transform,
            font,
            "Txt_WindowSubtitle",
            "PRZEJŚCIE ZE ZMIERZCHU NA NOC • FAZA PRZYGOTOWANIA",
            12,
            FontStyles.Bold,
            new Color(0.95f, 0.55f, 0.18f, 1f),
            TextAlignmentOptions.MidlineLeft
        );
        subObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 18f);
        subObj.AddComponent<LayoutElement>().preferredHeight = 18f;

        // Elastyczny odstęp wypychający przycisk X do prawej
        GameObject spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(topBar.transform, false);
        spacer.GetComponent<LayoutElement>().flexibleWidth = 1f;

        // Przycisk zamknięcia X (32x32 px)
        CreateCloseButton(topBar.transform, font, "Btn_Close", 32f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SEKCJA 2: Pnl_InfoAndCoalBar (Opis + Wolni Pracownicy + Zapas Węgla)
    // ─────────────────────────────────────────────────────────────────────────
    private static void BuildInfoAndCoalBar(
        Transform parent,
        TMP_FontAsset font,
        Sprite roundedSprite,
        Sprite freeWorkerSprite,
        Sprite coalSprite,
        GameObject singleResSmallPrefab,
        ResourceVisualsSO visualsDB)
    {
        GameObject infoBar = new GameObject(
            "Pnl_InfoAndCoalBar",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement)
        );
        infoBar.transform.SetParent(parent, false);
        infoBar.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 58f);
        LayoutElement infoLe = infoBar.GetComponent<LayoutElement>();
        infoLe.preferredHeight = 58f;

        ApplyRoundedSprite(infoBar.GetComponent<Image>(), roundedSprite, new Color(0.09f, 0.10f, 0.13f, 0.96f));
        Outline infoOut = infoBar.GetComponent<Outline>();
        infoOut.effectColor = new Color(1f, 1f, 1f, 0.06f);
        infoOut.effectDistance = new Vector2(1f, -1f);

        HorizontalLayoutGroup hlg = infoBar.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(16, 18, 8, 8);
        hlg.spacing = 20f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // Lewy pasek akcentu (czerwono-pomarańczowy)
        GameObject leftAccent = new GameObject("Img_LeftAccent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        leftAccent.transform.SetParent(infoBar.transform, false);
        leftAccent.GetComponent<LayoutElement>().ignoreLayout = true;
        RectTransform accRt = leftAccent.GetComponent<RectTransform>();
        accRt.anchorMin = new Vector2(0f, 0f);
        accRt.anchorMax = new Vector2(0f, 1f);
        accRt.pivot = new Vector2(0f, 0.5f);
        accRt.anchoredPosition = Vector2.zero;
        accRt.sizeDelta = new Vector2(4f, 0f);
        leftAccent.GetComponent<Image>().color = new Color(0.92f, 0.36f, 0.12f, 1f);

        // Tekst wprowadzający (13 pt)
        GameObject descObj = CreateText(
            infoBar.transform,
            font,
            "Txt_IntroDesc",
            "Zmierzch dobiega końca. Ogień w Wielkim Palenisku decyduje o sile obrony tej nocy, łupach z bestii oraz jutrzejszej produkcji.",
            13,
            FontStyles.Normal,
            new Color(0.86f, 0.88f, 0.92f, 1f),
            TextAlignmentOptions.MidlineLeft
        );
        descObj.GetComponent<RectTransform>().sizeDelta = new Vector2(820f, 40f);
        LayoutElement descLe = descObj.AddComponent<LayoutElement>();
        descLe.flexibleWidth = 1f;
        descLe.preferredWidth = 820f;
        descLe.preferredHeight = 40f;

        // ── Sekcja 2A: WOLNI PRACOWNICY (ogółem, bez podziału na rasy) ──
        GameObject freeWorkersGroup = new GameObject("Pnl_FreeWorkers", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        freeWorkersGroup.transform.SetParent(infoBar.transform, false);
        freeWorkersGroup.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 36f);
        LayoutElement fwLe = freeWorkersGroup.GetComponent<LayoutElement>();
        fwLe.preferredWidth = 260f;
        fwLe.preferredHeight = 36f;

        HorizontalLayoutGroup fwHlg = freeWorkersGroup.GetComponent<HorizontalLayoutGroup>();
        fwHlg.spacing = 10f;
        fwHlg.childAlignment = TextAnchor.MiddleRight;
        fwHlg.childControlWidth = false;
        fwHlg.childControlHeight = false;
        fwHlg.childForceExpandWidth = false;
        fwHlg.childForceExpandHeight = false;

        GameObject fwLbl = CreateText(freeWorkersGroup.transform, font, "Txt_FreeWorkersLabel", "WOLNI PRACOWNICY:", 12, FontStyles.Bold, new Color(0.68f, 0.72f, 0.80f, 1f), TextAlignmentOptions.MidlineRight);
        fwLbl.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 32f);
        fwLbl.AddComponent<LayoutElement>().preferredWidth = 150f;

        // Pastylka wolnych pracowników (w stylu Pnl_SingleResource_Small: ikona free.png 24px + liczba)
        CreateResourceBadgeFallback(freeWorkersGroup.transform, font, roundedSprite, "Badge_FreeWorkers", freeWorkerSprite, "4", 90f, 32f);

        // ── Sekcja 2B: ZAPAS WĘGLA ──
        GameObject coalGroup = new GameObject("Pnl_CoalStock", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        coalGroup.transform.SetParent(infoBar.transform, false);
        coalGroup.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 36f);
        LayoutElement coalLe = coalGroup.GetComponent<LayoutElement>();
        coalLe.preferredWidth = 220f;
        coalLe.preferredHeight = 36f;

        HorizontalLayoutGroup coalHlg = coalGroup.GetComponent<HorizontalLayoutGroup>();
        coalHlg.spacing = 10f;
        coalHlg.childAlignment = TextAnchor.MiddleRight;
        coalHlg.childControlWidth = false;
        coalHlg.childControlHeight = false;
        coalHlg.childForceExpandWidth = false;
        coalHlg.childForceExpandHeight = false;

        GameObject coalLbl = CreateText(coalGroup.transform, font, "Txt_CoalLabel", "ZAPAS WĘGLA:", 12, FontStyles.Bold, new Color(0.68f, 0.72f, 0.80f, 1f), TextAlignmentOptions.MidlineRight);
        coalLbl.GetComponent<RectTransform>().sizeDelta = new Vector2(110f, 32f);
        coalLbl.AddComponent<LayoutElement>().preferredWidth = 110f;

        if (singleResSmallPrefab != null)
        {
            GameObject coalInst = (GameObject)PrefabUtility.InstantiatePrefab(singleResSmallPrefab, coalGroup.transform);
            coalInst.name = "Pnl_CoalStockValue";
            RectTransform cRt = coalInst.GetComponent<RectTransform>();
            if (cRt != null) cRt.sizeDelta = new Vector2(96f, 32f);
            ConfigureSingleResourceSmall(coalInst, ResourceType.Coal, 285, visualsDB, coalSprite);
        }
        else
        {
            CreateResourceBadgeFallback(coalGroup.transform, font, roundedSprite, "Pnl_CoalStockValue", coalSprite, "285", 96f, 32f);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SEKCJA 3: Pnl_HearthPreviewBox (Centralny box z ikoną 44px i pastylką)
    // ─────────────────────────────────────────────────────────────────────────
    private static void BuildHearthPreviewBox(Transform parent, TMP_FontAsset font, Sprite roundedSprite)
    {
        GameObject previewBox = new GameObject(
            "Pnl_HearthPreviewBox",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement)
        );
        previewBox.transform.SetParent(parent, false);
        previewBox.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 128f);
        LayoutElement boxLe = previewBox.GetComponent<LayoutElement>();
        boxLe.preferredHeight = 128f;

        ApplyRoundedSprite(previewBox.GetComponent<Image>(), roundedSprite, new Color(0.08f, 0.07f, 0.09f, 0.96f));
        Outline boxOut = previewBox.GetComponent<Outline>();
        boxOut.effectColor = new Color(1f, 1f, 1f, 0.06f);
        boxOut.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup vlg = previewBox.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 16, 14);
        vlg.spacing = 12f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        // Pojedyncza duża ikona podglądu paleniska (44x44 px - Big Icon wg GEMINI.md)
        CreateIcon(previewBox.transform, "Img_HearthPreview", 44f, new Color(0.98f, 0.56f, 0.16f, 1f));

        // Pastylka podsumowania wybranego poziomu i kosztu
        GameObject summaryPill = new GameObject(
            "Badge_CurrentLevelSummary",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement)
        );
        summaryPill.transform.SetParent(previewBox.transform, false);
        summaryPill.GetComponent<RectTransform>().sizeDelta = new Vector2(500f, 34f);
        LayoutElement pillLe = summaryPill.GetComponent<LayoutElement>();
        pillLe.preferredWidth = 500f;
        pillLe.preferredHeight = 34f;

        ApplyRoundedSprite(summaryPill.GetComponent<Image>(), roundedSprite, new Color(0.09f, 0.10f, 0.13f, 0.98f));
        Outline pillOut = summaryPill.GetComponent<Outline>();
        pillOut.effectColor = new Color(0.65f, 0.48f, 0.22f, 0.65f);
        pillOut.effectDistance = new Vector2(1f, -1f);

        HorizontalLayoutGroup pillHlg = summaryPill.GetComponent<HorizontalLayoutGroup>();
        pillHlg.padding = new RectOffset(18, 18, 4, 4);
        pillHlg.spacing = 12f;
        pillHlg.childAlignment = TextAnchor.MiddleCenter;
        pillHlg.childControlWidth = false;
        pillHlg.childControlHeight = false;
        pillHlg.childForceExpandWidth = false;
        pillHlg.childForceExpandHeight = false;

        GameObject lvlNameObj = CreateText(summaryPill.transform, font, "Txt_PreviewLevelName", "POZIOM 3: ZRÓWNOWAŻONY PŁOMIEŃ", 13, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineRight);
        lvlNameObj.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 26f);

        GameObject sepObj = CreateText(summaryPill.transform, font, "Txt_Separator", "|", 13, FontStyles.Normal, new Color(0.45f, 0.48f, 0.55f, 1f), TextAlignmentOptions.Center);
        sepObj.GetComponent<RectTransform>().sizeDelta = new Vector2(12f, 26f);

        GameObject lvlCostObj = CreateText(summaryPill.transform, font, "Txt_PreviewLevelCost", "KOSZT: 35 WĘGLA", 13, FontStyles.Bold, new Color(0.98f, 0.78f, 0.25f, 1f), TextAlignmentOptions.MidlineLeft);
        lvlCostObj.GetComponent<RectTransform>().sizeDelta = new Vector2(150f, 26f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SEKCJA 4: Pnl_LevelsRow (5x instancja Card_HearthLevel.prefab)
    // ─────────────────────────────────────────────────────────────────────────
    private static void BuildLevelsRow(Transform parent, GameObject levelCardPrefab)
    {
        GameObject levelsRow = new GameObject("Pnl_LevelsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        levelsRow.transform.SetParent(parent, false);
        levelsRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 154f);
        LayoutElement rowLe = levelsRow.GetComponent<LayoutElement>();
        rowLe.preferredHeight = 154f;

        HorizontalLayoutGroup hlg = levelsRow.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        var tiers = new (int level, int cost, bool isFree, string title, string tag, Color iconColor, Color tagBg, Color tagBorder, Color tagText, bool isSelected)[]
        {
            (1, 0,   true,  "WYGASŁY POPIÓŁ",       "MROK & TROFEA",   new Color(0.72f, 0.74f, 0.82f, 1f), new Color(0.28f, 0.08f, 0.10f, 0.95f), new Color(0.85f, 0.25f, 0.28f, 0.8f), new Color(1.00f, 0.55f, 0.58f, 1f), false),
            (2, 15,  false, "TLĄCY SIĘ ŻAR",        "OSZCZĘDNY ŻAR",   new Color(0.95f, 0.50f, 0.18f, 1f), new Color(0.26f, 0.16f, 0.06f, 0.95f), new Color(0.88f, 0.58f, 0.16f, 0.8f), new Color(0.98f, 0.74f, 0.28f, 1f), false),
            (3, 35,  false, "ZRÓWNOWAŻONY PŁOMIEŃ", "RÓWNOWAGA",       new Color(0.98f, 0.58f, 0.16f, 1f), new Color(0.16f, 0.18f, 0.22f, 0.95f), new Color(0.55f, 0.60f, 0.68f, 0.8f), new Color(0.84f, 0.86f, 0.92f, 1f), true),
            (4, 65,  false, "JASNY OGIEŃ",          "MOCNY OGIEŃ",     new Color(1.00f, 0.66f, 0.18f, 1f), new Color(0.08f, 0.18f, 0.32f, 0.95f), new Color(0.24f, 0.58f, 0.95f, 0.8f), new Color(0.48f, 0.78f, 1.00f, 1f), false),
            (5, 110, false, "PALENISKO MAGMY",      "LEGENDARNY ŻAR",  new Color(1.00f, 0.42f, 0.12f, 1f), new Color(0.28f, 0.14f, 0.05f, 0.95f), new Color(0.96f, 0.54f, 0.14f, 0.8f), new Color(1.00f, 0.68f, 0.24f, 1f), false)
        };

        foreach (var t in tiers)
        {
            GameObject cardInst = (GameObject)PrefabUtility.InstantiatePrefab(levelCardPrefab, levelsRow.transform);
            cardInst.name = $"Card_HearthLevel_{t.level}";

            var idxTmp = cardInst.transform.Find("Pnl_CardTopRow/Txt_LevelIndex")?.GetComponent<TextMeshProUGUI>();
            if (idxTmp != null)
            {
                idxTmp.text = $"POZ. {t.level}";
                idxTmp.color = t.isSelected ? new Color(0.98f, 0.72f, 0.24f, 1f) : new Color(0.68f, 0.72f, 0.80f, 1f);
            }

            Transform coalCostTr = cardInst.transform.Find("Pnl_CardTopRow/Pnl_CostContainer/Pnl_CoalCost");
            Transform freeBadgeTr = cardInst.transform.Find("Pnl_CardTopRow/Pnl_CostContainer/Badge_Free");
            if (coalCostTr != null)
            {
                coalCostTr.gameObject.SetActive(!t.isFree);
                if (!t.isFree)
                {
                    var amtTmp = coalCostTr.Find("Txt_Amount")?.GetComponent<TextMeshProUGUI>();
                    if (amtTmp != null) amtTmp.text = t.cost.ToString();
                }
            }
            if (freeBadgeTr != null)
            {
                freeBadgeTr.gameObject.SetActive(t.isFree);
            }

            var iconImg = cardInst.transform.Find("Pnl_IconContainer/Img_LevelIcon")?.GetComponent<Image>();
            if (iconImg != null) iconImg.color = t.iconColor;

            var titleTmp = cardInst.transform.Find("Txt_LevelTitle")?.GetComponent<TextMeshProUGUI>();
            if (titleTmp != null) titleTmp.text = t.title;

            Transform badgeTr = cardInst.transform.Find("Pnl_TagRow/Badge_LevelTag");
            if (badgeTr != null)
            {
                var badgeImg = badgeTr.GetComponent<Image>();
                if (badgeImg != null) badgeImg.color = t.tagBg;

                var badgeOut = badgeTr.GetComponent<Outline>();
                if (badgeOut != null) badgeOut.effectColor = t.tagBorder;

                var tagTmp = badgeTr.Find("Txt_Tag")?.GetComponent<TextMeshProUGUI>();
                if (tagTmp != null)
                {
                    tagTmp.text = t.tag;
                    tagTmp.color = t.tagText;
                }
            }

            // Styl zaznaczonej karty (Poziom 3 domyślnie w podglądzie)
            Transform topBarTr = cardInst.transform.Find("Img_SelectedTopBar");
            if (topBarTr != null) topBarTr.gameObject.SetActive(t.isSelected);

            var cardImg = cardInst.GetComponent<Image>();
            if (cardImg != null)
            {
                cardImg.color = t.isSelected
                    ? new Color(0.16f, 0.11f, 0.06f, 0.98f)
                    : new Color(0.08f, 0.09f, 0.12f, 0.96f);
            }

            var cardOut = cardInst.GetComponent<Outline>();
            if (cardOut != null)
            {
                cardOut.effectColor = t.isSelected
                    ? new Color(0.95f, 0.52f, 0.14f, 0.95f)
                    : new Color(1f, 1f, 1f, 0.08f);
                cardOut.effectDistance = t.isSelected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SEKCJA 5: Pnl_EffectsColumnsRow (Efekty Tej Nocy + Efekty Następnego Dnia)
    // ─────────────────────────────────────────────────────────────────────────
    private static void BuildEffectsColumnsRow(
        Transform parent,
        TMP_FontAsset font,
        Sprite roundedSprite,
        GameObject effectItemPrefab)
    {
        GameObject row = new GameObject("Pnl_EffectsColumnsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 340f);
        LayoutElement rowLe = row.GetComponent<LayoutElement>();
        rowLe.preferredHeight = 340f;

        HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16f;
        hlg.childAlignment = TextAnchor.UpperCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        // ── Lewa Kolumna: EFEKTY TEJ NOCY (OBRONA & ŁUPY) ──
        GameObject colNight = CreateEffectsColumn(
            row.transform,
            font,
            roundedSprite,
            "Sec_NightEffects",
            "EFEKTY TEJ NOCY (OBRONA & ŁUPY)",
            new Color(0.38f, 0.45f, 0.95f, 1f),
            new Color(0.98f, 0.78f, 0.25f, 1f)
        );

        Transform nightList = colNight.transform.Find("Pnl_EffectsList");
        PopulateEffectItem(
            effectItemPrefab,
            nightList,
            "EffectItem_1",
            "SIŁA OBRONY WIEŻ",
            "-15% DMG",
            "Nieprzenikniony mrok ogranicza widoczność celów i osłabia celność machin.",
            new Color(0.95f, 0.35f, 0.38f, 1f)
        );
        PopulateEffectItem(
            effectItemPrefab,
            nightList,
            "EffectItem_2",
            "PRĘDKOŚĆ WROGÓW",
            "+10% SPD",
            "Pomiot nocy czuje się bezkarny w ciemności i szybciej zbliża się do murów.",
            new Color(0.95f, 0.35f, 0.38f, 1f)
        );
        PopulateEffectItem(
            effectItemPrefab,
            nightList,
            "EffectItem_3",
            "ŁUPY ZA ZABIJANIE WROGÓW",
            "+50% Surowców",
            "Większe hordy i elitarne bestie niosą ze sobą bogatsze skarby.",
            new Color(0.30f, 0.88f, 0.48f, 1f)
        );
        PopulateEffectItem(
            effectItemPrefab,
            nightList,
            "EffectItem_4",
            "SZANSA NA RDZENIE RUNICZNE",
            "+40% Drop Run",
            "Mroczna esencja krystalizuje w potężniejsze rdzenie run po bitwie.",
            new Color(0.30f, 0.88f, 0.48f, 1f)
        );

        // ── Prawa Kolumna: EFEKTY NASTĘPNEGO DNIA (PRODUKCJA) ──
        GameObject colDay = CreateEffectsColumn(
            row.transform,
            font,
            roundedSprite,
            "Sec_DayEffects",
            "EFEKTY NASTĘPNEGO DNIA (PRODUKCJA)",
            new Color(0.96f, 0.62f, 0.14f, 1f),
            new Color(0.98f, 0.68f, 0.18f, 1f)
        );

        Transform dayList = colDay.transform.Find("Pnl_EffectsList");
        PopulateEffectItem(
            effectItemPrefab,
            dayList,
            "EffectItem_1",
            "WYDAJNOŚĆ DZIENNA",
            "+30% do produkcji",
            "Najwyższa możliwa temperatura w instalacjach twierdzy gwarantuje maksymalną wydajność manufaktur.",
            new Color(0.30f, 0.88f, 0.48f, 1f)
        );
        PopulateEffectItem(
            effectItemPrefab,
            dayList,
            "EffectItem_2",
            "DODATKOWY MODYFIKATOR DNIA",
            "Brak zmian",
            "Dodatkowy slot na efekty gospodarcze lub populacyjne kolejnego poranka.",
            new Color(0.75f, 0.78f, 0.84f, 1f)
        );
    }

    private static GameObject CreateEffectsColumn(
        Transform parent,
        TMP_FontAsset font,
        Sprite roundedSprite,
        string name,
        string headerTitle,
        Color leftBorderColor,
        Color headerIconColor)
    {
        GameObject col = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline),
            typeof(VerticalLayoutGroup),
            typeof(LayoutElement)
        );
        col.transform.SetParent(parent, false);
        col.GetComponent<RectTransform>().sizeDelta = new Vector2(710f, 340f);

        LayoutElement colLe = col.GetComponent<LayoutElement>();
        colLe.flexibleWidth = 1f;
        colLe.preferredWidth = 710f;
        colLe.preferredHeight = 340f;

        ApplyRoundedSprite(col.GetComponent<Image>(), roundedSprite, new Color(0.08f, 0.09f, 0.12f, 0.96f));
        Outline colOut = col.GetComponent<Outline>();
        colOut.effectColor = new Color(1f, 1f, 1f, 0.06f);
        colOut.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup colVlg = col.GetComponent<VerticalLayoutGroup>();
        colVlg.padding = new RectOffset(18, 16, 14, 14);
        colVlg.spacing = 10f;
        colVlg.childAlignment = TextAnchor.UpperLeft;
        colVlg.childControlWidth = true;
        colVlg.childControlHeight = true;
        colVlg.childForceExpandWidth = true;
        colVlg.childForceExpandHeight = false;

        // Pasek boczny akcentu kolumny (IgnoreLayout)
        GameObject accent = new GameObject("Img_LeftAccent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        accent.transform.SetParent(col.transform, false);
        accent.GetComponent<LayoutElement>().ignoreLayout = true;
        RectTransform accRt = accent.GetComponent<RectTransform>();
        accRt.anchorMin = new Vector2(0f, 0f);
        accRt.anchorMax = new Vector2(0f, 1f);
        accRt.pivot = new Vector2(0f, 0.5f);
        accRt.anchoredPosition = Vector2.zero;
        accRt.sizeDelta = new Vector2(4f, 0f);
        accent.GetComponent<Image>().color = leftBorderColor;

        // Nagłówek Kolumny (Ikona 24px + Tytuł 16pt Bold wg GEMINI.md)
        GameObject hdrRow = new GameObject("Pnl_Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        hdrRow.transform.SetParent(col.transform, false);
        hdrRow.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 28f);
        hdrRow.GetComponent<LayoutElement>().preferredHeight = 28f;

        HorizontalLayoutGroup hdrHlg = hdrRow.GetComponent<HorizontalLayoutGroup>();
        hdrHlg.spacing = 10f;
        hdrHlg.childAlignment = TextAnchor.MiddleLeft;
        hdrHlg.childControlWidth = true;
        hdrHlg.childControlHeight = false;
        hdrHlg.childForceExpandWidth = false;
        hdrHlg.childForceExpandHeight = false;

        CreateIcon(hdrRow.transform, "Img_SectionIcon", 24f, headerIconColor);

        GameObject hdrTxtObj = CreateText(hdrRow.transform, font, "Txt_SectionTitle", headerTitle, 16, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft);
        hdrTxtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 26f);
        LayoutElement hdrTxtLe = hdrTxtObj.AddComponent<LayoutElement>();
        hdrTxtLe.flexibleWidth = 1f;
        hdrTxtLe.preferredHeight = 26f;

        // Cienka linia oddzielająca
        GameObject div = new GameObject("Img_Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        div.transform.SetParent(col.transform, false);
        div.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 1f);
        div.GetComponent<LayoutElement>().preferredHeight = 1f;
        div.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

        // Kontener listy kafelków efektów
        GameObject listObj = new GameObject("Pnl_EffectsList", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        listObj.transform.SetParent(col.transform, false);
        LayoutElement listLe = listObj.GetComponent<LayoutElement>();
        listLe.flexibleWidth = 1f;
        listLe.flexibleHeight = 1f;

        VerticalLayoutGroup listVlg = listObj.GetComponent<VerticalLayoutGroup>();
        listVlg.spacing = 8f;
        listVlg.childAlignment = TextAnchor.UpperCenter;
        listVlg.childControlWidth = true;
        listVlg.childControlHeight = true;
        listVlg.childForceExpandWidth = true;
        listVlg.childForceExpandHeight = false;

        return col;
    }

    private static void PopulateEffectItem(
        GameObject effectItemPrefab,
        Transform listParent,
        string instanceName,
        string effectTitle,
        string effectValue,
        string effectDesc,
        Color valueColor)
    {
        if (effectItemPrefab == null || listParent == null) return;

        GameObject item = (GameObject)PrefabUtility.InstantiatePrefab(effectItemPrefab, listParent);
        item.name = instanceName;

        var titleTmp = item.transform.Find("Pnl_TopRow/Txt_EffectTitle")?.GetComponent<TextMeshProUGUI>();
        if (titleTmp != null) titleTmp.text = effectTitle;

        var valTmp = item.transform.Find("Pnl_TopRow/Txt_EffectValue")?.GetComponent<TextMeshProUGUI>();
        if (valTmp != null)
        {
            valTmp.text = effectValue;
            valTmp.color = valueColor;
        }

        var descTmp = item.transform.Find("Txt_EffectDesc")?.GetComponent<TextMeshProUGUI>();
        if (descTmp != null) descTmp.text = effectDesc;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SEKCJA 6: Pnl_FooterBar (Podsumowanie Opału + ANULUJ + ROZPAL I ROZPOCZNIJ)
    // ─────────────────────────────────────────────────────────────────────────
    private static void BuildFooterBar(Transform parent, TMP_FontAsset font, Sprite roundedSprite)
    {
        GameObject footer = new GameObject("Pnl_FooterBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        footer.transform.SetParent(parent, false);
        footer.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 54f);
        LayoutElement footerLe = footer.GetComponent<LayoutElement>();
        footerLe.preferredHeight = 54f;

        HorizontalLayoutGroup hlg = footer.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 14f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // Lewa strona: Podsumowanie opału
        GameObject fuelSummary = new GameObject("Pnl_FuelSummary", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        fuelSummary.transform.SetParent(footer.transform, false);
        fuelSummary.GetComponent<RectTransform>().sizeDelta = new Vector2(600f, 48f);
        LayoutElement fsLe = fuelSummary.GetComponent<LayoutElement>();
        fsLe.flexibleWidth = 1f;
        fsLe.preferredWidth = 600f;
        fsLe.preferredHeight = 48f;

        VerticalLayoutGroup fsVlg = fuelSummary.GetComponent<VerticalLayoutGroup>();
        fsVlg.spacing = 2f;
        fsVlg.childAlignment = TextAnchor.MiddleLeft;
        fsVlg.childControlWidth = true;
        fsVlg.childControlHeight = false;
        fsVlg.childForceExpandWidth = true;
        fsVlg.childForceExpandHeight = false;

        GameObject hdrObj = CreateText(fuelSummary.transform, font, "Txt_SummaryHeader", "PODSUMOWANIE OPAŁU:", 10, FontStyles.Bold, new Color(0.60f, 0.63f, 0.70f, 1f), TextAlignmentOptions.MidlineLeft);
        hdrObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 16f);

        GameObject detObj = CreateText(
            fuelSummary.transform,
            font,
            "Txt_SummaryDetails",
            "Zużycie: <color=#F8B830><b>35 Węgla</b></color>  •  Pozostanie w twierdzy: <color=#FFFFFF><b>250 Węgla</b></color>",
            13,
            FontStyles.Normal,
            new Color(0.84f, 0.86f, 0.90f, 1f),
            TextAlignmentOptions.MidlineLeft
        );
        detObj.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 22f);

        // Elastyczny odstęp
        GameObject spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(footer.transform, false);
        spacer.GetComponent<LayoutElement>().flexibleWidth = 1f;

        // Przycisk ANULUJ (14 pt Bold)
        GameObject cancelBtn = new GameObject("Btn_Cancel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline), typeof(LayoutElement));
        cancelBtn.transform.SetParent(footer.transform, false);
        cancelBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(120f, 48f);
        LayoutElement cancelLe = cancelBtn.GetComponent<LayoutElement>();
        cancelLe.preferredWidth = 120f;
        cancelLe.preferredHeight = 48f;

        ApplyRoundedSprite(cancelBtn.GetComponent<Image>(), roundedSprite, new Color(0.12f, 0.14f, 0.18f, 0.96f));
        Outline cancelOut = cancelBtn.GetComponent<Outline>();
        cancelOut.effectColor = new Color(1f, 1f, 1f, 0.14f);
        cancelOut.effectDistance = new Vector2(1f, -1f);

        GameObject cancelTxt = CreateText(cancelBtn.transform, font, "Txt_CancelLabel", "ANULUJ", 14, FontStyles.Bold, new Color(0.82f, 0.85f, 0.90f, 1f), TextAlignmentOptions.Center);
        StretchFull(cancelTxt.GetComponent<RectTransform>());

        // Główny przycisk akcji: ROZPAL POZIOM X I ROZPOCZNIJ NOC (16 pt Bold)
        GameObject igniteBtn = new GameObject(
            "Btn_IgniteAndStart",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(Outline),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement)
        );
        igniteBtn.transform.SetParent(footer.transform, false);
        igniteBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(390f, 48f);
        LayoutElement igLe = igniteBtn.GetComponent<LayoutElement>();
        igLe.preferredWidth = 390f;
        igLe.preferredHeight = 48f;

        ApplyRoundedSprite(igniteBtn.GetComponent<Image>(), roundedSprite, new Color(0.88f, 0.36f, 0.08f, 0.98f));
        Outline igOut = igniteBtn.GetComponent<Outline>();
        igOut.effectColor = new Color(1.00f, 0.68f, 0.25f, 0.85f);
        igOut.effectDistance = new Vector2(1.5f, -1.5f);

        HorizontalLayoutGroup igHlg = igniteBtn.GetComponent<HorizontalLayoutGroup>();
        igHlg.padding = new RectOffset(20, 20, 6, 6);
        igHlg.spacing = 10f;
        igHlg.childAlignment = TextAnchor.MiddleCenter;
        igHlg.childControlWidth = false;
        igHlg.childControlHeight = false;
        igHlg.childForceExpandWidth = false;
        igHlg.childForceExpandHeight = false;

        CreateIcon(igniteBtn.transform, "Img_FlameIcon", 24f, new Color(1.00f, 0.85f, 0.35f, 1f));

        GameObject igTxt = CreateText(igniteBtn.transform, font, "Txt_IgniteLabel", "ROZPAL POZIOM 3 I ROZPOCZNIJ NOC", 16, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft);
        igTxt.GetComponent<RectTransform>().sizeDelta = new Vector2(310f, 32f);
    }

    // =========================================================================
    // METODY POMOCNICZE
    // =========================================================================

    private static void ConfigureSingleResourceSmall(
        GameObject instance,
        ResourceType type,
        int value,
        ResourceVisualsSO visualsDB,
        Sprite iconSprite)
    {
        if (instance == null) return;

        var singleRes = instance.GetComponent<SingleResourceUI>();
        if (singleRes != null)
        {
            SerializedObject so = new SerializedObject(singleRes);
            so.Update();
            var resTypeProp = so.FindProperty("resourceType");
            if (resTypeProp != null) resTypeProp.enumValueIndex = (int)type;
            var visProp = so.FindProperty("ResourceVisuals");
            if (visProp != null && visualsDB != null) visProp.objectReferenceValue = visualsDB;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var iconImg = instance.transform.Find("Img_Icon")?.GetComponent<Image>();
        if (iconImg != null && iconSprite != null)
        {
            iconImg.sprite = iconSprite;
            iconImg.color = Color.white;
        }

        var amtTmp = instance.transform.Find("Txt_Amount")?.GetComponent<TextMeshProUGUI>();
        if (amtTmp != null)
        {
            amtTmp.text = value.ToString();
        }
    }

    private static GameObject CreateResourceBadgeFallback(
        Transform parent,
        TMP_FontAsset font,
        Sprite roundedSprite,
        string name,
        Sprite iconSprite,
        string amountText,
        float width,
        float height)
    {
        GameObject pill = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        pill.transform.SetParent(parent, false);
        RectTransform rt = pill.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);
        LayoutElement le = pill.GetComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;

        ApplyRoundedSprite(pill.GetComponent<Image>(), roundedSprite, new Color(0.125f, 0.125f, 0.141f, 0.85f));

        HorizontalLayoutGroup hlg = pill.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(8, 8, 0, 0);
        hlg.spacing = 6f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        GameObject iconObj = CreateIcon(pill.transform, "Img_Icon", 24f, Color.white);
        if (iconSprite != null)
        {
            Image img = iconObj.GetComponent<Image>();
            img.sprite = iconSprite;
            img.preserveAspect = true;
        }

        GameObject txtObj = CreateText(pill.transform, font, "Txt_Amount", amountText, 14, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft);
        txtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(width - 46f, height);

        return pill;
    }

    private static void ApplyRoundedSprite(Image img, Sprite roundedSprite, Color color)
    {
        if (img == null) return;
        img.color = color;
        if (roundedSprite != null)
        {
            img.sprite = roundedSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 8f;
        }
    }

    private static GameObject CreateIcon(Transform parent, string name, float size, Color color)
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
        return iconObj;
    }

    private static GameObject CreateText(
        Transform parent,
        TMP_FontAsset font,
        string name,
        string text,
        float fontSize,
        FontStyles style,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = obj.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.text = text;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return obj;
    }

    private static void CreateCloseButton(Transform parent, TMP_FontAsset font, string name, float size)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(Outline), typeof(LayoutElement));
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        LayoutElement le = btnObj.GetComponent<LayoutElement>();
        le.preferredWidth = size;
        le.preferredHeight = size;

        Image img = btnObj.GetComponent<Image>();
        img.color = new Color(0.18f, 0.12f, 0.12f, 0.90f);

        Outline outl = btnObj.GetComponent<Outline>();
        outl.effectColor = new Color(1f, 1f, 1f, 0.15f);
        outl.effectDistance = new Vector2(1f, -1f);

        GameObject txtObj = CreateText(btnObj.transform, font, "Txt_Close", "X", 16, FontStyles.Bold, new Color(0.95f, 0.75f, 0.35f, 1f), TextAlignmentOptions.Center);
        StretchFull(txtObj.GetComponent<RectTransform>());
    }

    private static void StretchFull(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
