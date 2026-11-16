using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Narzędzie Edytora budujące kompletny layout ekranu końcowego (Pnl_EndGame)
/// na scenie TESTSCENE_ENDSCRENE zgodnie ze standardami GEMINI.md i układem z The Last Spell.
/// </summary>
public static class EndGameScreenSceneBuilder
{
    private const string TargetScenePath = "Assets/Main folder/_Scenes/TESTSCENE_ENDSCRENE.unity";
    private const string FontPath = "Assets/Resources/Fonts/PirataOne-Regular SDF.asset";
    private const string UiSpriteRoot = "Assets/Main folder/Icons/_PACKAGES/emberhold-free/sfx/ui/png/";

    [MenuItem("Tools/UI/Build EndGame Screen (TESTSCENE_ENDSCRENE)")]
    public static void BuildOnTestScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path != TargetScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            activeScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        }

        // Usuń poprzedni Canvas_EndGame jeśli istnieje (idempotentne budowanie)
        GameObject existing = GameObject.Find("Canvas_EndGame");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Sprite panelOrnate = LoadUiSprite("panel_ornate.png");
        Sprite panelInset = LoadUiSprite("panel_inset.png");
        Sprite panelPlain = LoadUiSprite("panel_plain.png");
        Sprite headerBanner = LoadUiSprite("header_banner.png");
        Sprite slotEmpty = LoadUiSprite("slot_empty.png");
        Sprite btnNormal = LoadUiSprite("btn_standard_normal.png");
        Sprite btnIcon = LoadUiSprite("btn_icon_normal.png");
        Sprite tooltipBg = LoadUiSprite("tooltip.png");

        // 1. Canvas
        GameObject canvasGo = new GameObject("Canvas_EndGame");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        EndGameScreenUI screenUI = canvasGo.AddComponent<EndGameScreenUI>();

        // 2. Backdrop + Root
        GameObject rootGo = CreateUIObject("Pnl_EndGameRoot", canvasGo.transform);
        StretchFull(rootGo.GetComponent<RectTransform>());
        Image backdropImg = rootGo.AddComponent<Image>();
        backdropImg.color = new Color(0.03f, 0.03f, 0.05f, 0.82f);
        CanvasGroup canvasGroup = rootGo.AddComponent<CanvasGroup>();

        // 3. Główne okno (980 x 700)
        GameObject windowGo = CreateUIObject("Pnl_Window", rootGo.transform);
        RectTransform windowRt = windowGo.GetComponent<RectTransform>();
        windowRt.anchorMin = new Vector2(0.5f, 0.5f);
        windowRt.anchorMax = new Vector2(0.5f, 0.5f);
        windowRt.sizeDelta = new Vector2(980f, 700f);
        Image windowImg = windowGo.AddComponent<Image>();
        ApplySprite(windowImg, panelOrnate, new Color(0.14f, 0.12f, 0.15f, 0.98f));

        VerticalLayoutGroup windowVlg = windowGo.AddComponent<VerticalLayoutGroup>();
        windowVlg.padding = new RectOffset(28, 28, 24, 24);
        windowVlg.spacing = 14f;
        windowVlg.childControlWidth = true;
        windowVlg.childControlHeight = true;
        windowVlg.childForceExpandWidth = true;
        windowVlg.childForceExpandHeight = false;

        // 4. Nagłówek (Baner wyniku + Podtytuł + Przetrwane noce)
        GameObject headerGo = CreateUIObject("Pnl_Header", windowGo.transform);
        AddLayoutElement(headerGo, preferredHeight: 88f);
        Image headerImg = headerGo.AddComponent<Image>();
        ApplySprite(headerImg, headerBanner, new Color(0.38f, 0.12f, 0.14f, 1f));

        VerticalLayoutGroup headerVlg = headerGo.AddComponent<VerticalLayoutGroup>();
        headerVlg.padding = new RectOffset(16, 16, 8, 8);
        headerVlg.spacing = 2f;
        headerVlg.childAlignment = TextAnchor.MiddleCenter;
        headerVlg.childControlWidth = true;
        headerVlg.childControlHeight = true;

        TextMeshProUGUI txtHeaderTitle = CreateText("Txt_HeaderTitle", headerGo.transform, "TWIERDZA UPADŁA", 20f, font, Color.white, TextAlignmentOptions.Center);
        TextMeshProUGUI txtHeaderSubtitle = CreateText("Txt_HeaderSubtitle", headerGo.transform, "Brama została zniszczona podczas 1. nocy", 12f, font, new Color(0.92f, 0.86f, 0.8f, 1f), TextAlignmentOptions.Center);
        TextMeshProUGUI txtNightsSurvived = CreateText("Txt_NightsSurvived", headerGo.transform, "Przetrwano nocy: 0", 14f, font, new Color(0.95f, 0.85f, 0.6f, 1f), TextAlignmentOptions.Center);

        // 5. Górna Sekcja - Statystyki Runu (zamiast City Protection)
        GameObject statsSection = CreateSectionPanel("Pnl_RunStatsSection", windowGo.transform, "STATYSTYKI OBRONY", panelInset, font, 165f);
        GameObject statsGridGo = CreateUIObject("Pnl_StatsGrid", statsSection.transform);
        AddLayoutElement(statsGridGo, flexibleHeight: 1f);
        GridLayoutGroup statsGrid = statsGridGo.AddComponent<GridLayoutGroup>();
        statsGrid.cellSize = new Vector2(215f, 44f);
        statsGrid.spacing = new Vector2(12f, 8f);
        statsGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        statsGrid.constraintCount = 4;
        statsGrid.childAlignment = TextAnchor.MiddleCenter;

        TextMeshProUGUI txtEnemiesKilled = CreateStatItem(statsGridGo.transform, "Stat_Enemies", "Zabici wrogowie", panelPlain, font);
        TextMeshProUGUI txtBossesKilled = CreateStatItem(statsGridGo.transform, "Stat_Bosses", "Zabici bossowie", panelPlain, font);
        TextMeshProUGUI txtTowersBuilt = CreateStatItem(statsGridGo.transform, "Stat_Towers", "Zbudowane wieże", panelPlain, font);
        TextMeshProUGUI txtProductionBuilt = CreateStatItem(statsGridGo.transform, "Stat_Production", "Budynki prod.", panelPlain, font);
        TextMeshProUGUI txtExpeditions = CreateStatItem(statsGridGo.transform, "Stat_Expeditions", "Ekspedycje", panelPlain, font);
        TextMeshProUGUI txtRunesCrafted = CreateStatItem(statsGridGo.transform, "Stat_Runes", "Wytworzone runy", panelPlain, font);
        TextMeshProUGUI txtFinalPop = CreateStatItem(statsGridGo.transform, "Stat_Population", "Populacja", panelPlain, font);

        // 6. Środkowa Sekcja - Karuzela Kart Nadziei + Prawy Panel Podsumowania
        GameObject middleGo = CreateUIObject("Pnl_Middle", windowGo.transform);
        AddLayoutElement(middleGo, preferredHeight: 300f, flexibleHeight: 1f);
        HorizontalLayoutGroup middleHlg = middleGo.AddComponent<HorizontalLayoutGroup>();
        middleHlg.spacing = 14f;
        middleHlg.childControlWidth = true;
        middleHlg.childControlHeight = true;
        middleHlg.childForceExpandHeight = true;

        // 6A. Karuzela Kart (Lewo/Środek)
        GameObject hopeCarouselSection = CreateSectionPanel("Pnl_HopeCarouselSection", middleGo.transform, "ZDOBYTA NADZIEJA", panelInset, font, 0f);
        AddLayoutElement(hopeCarouselSection, flexibleWidth: 1f);

        GameObject carouselRow = CreateUIObject("Pnl_CarouselRow", hopeCarouselSection.transform);
        AddLayoutElement(carouselRow, flexibleHeight: 1f);
        HorizontalLayoutGroup carouselHlg = carouselRow.AddComponent<HorizontalLayoutGroup>();
        carouselHlg.spacing = 10f;
        carouselHlg.childAlignment = TextAnchor.MiddleCenter;
        carouselHlg.childControlWidth = true;
        carouselHlg.childControlHeight = true;
        carouselHlg.childForceExpandWidth = false;
        carouselHlg.childForceExpandHeight = false;

        Button btnPrev = CreateArrowButton("Btn_PrevPage", carouselRow.transform, "<", btnIcon, font);

        GameObject cardsContainer = CreateUIObject("Pnl_CardsContainer", carouselRow.transform);
        AddLayoutElement(cardsContainer, flexibleWidth: 1f, preferredHeight: 205f);
        HorizontalLayoutGroup cardsHlg = cardsContainer.AddComponent<HorizontalLayoutGroup>();
        cardsHlg.spacing = 10f;
        cardsHlg.childAlignment = TextAnchor.MiddleCenter;
        cardsHlg.childControlWidth = true;
        cardsHlg.childControlHeight = true;
        cardsHlg.childForceExpandWidth = true;
        cardsHlg.childForceExpandHeight = true;

        EndGameHopeCardUI[] cardSlots = new EndGameHopeCardUI[4];
        for (int i = 0; i < 4; i++)
        {
            cardSlots[i] = CreateHopeCard($"Card_Hope_{i}", cardsContainer.transform, slotEmpty, panelPlain, font);
        }

        GameObject emptyInfoGo = CreateUIObject("Txt_EmptyCardsInfo", cardsContainer.transform);
        AddLayoutElement(emptyInfoGo, ignoreLayout: true);
        StretchFull(emptyInfoGo.GetComponent<RectTransform>());
        TextMeshProUGUI emptyText = emptyInfoGo.AddComponent<TextMeshProUGUI>();
        ApplyTextStyle(emptyText, "Brak zdobytych osiągnięć w tym runie", 14f, font, new Color(0.75f, 0.75f, 0.75f, 0.8f), TextAlignmentOptions.Center);
        emptyInfoGo.SetActive(false);

        Button btnNext = CreateArrowButton("Btn_NextPage", carouselRow.transform, ">", btnIcon, font);

        TextMeshProUGUI txtPageIndicator = CreateText("Txt_PageIndicator", hopeCarouselSection.transform, "1 / 1", 10f, font, new Color(0.8f, 0.8f, 0.8f, 1f), TextAlignmentOptions.Center);
        AddLayoutElement(txtPageIndicator.gameObject, preferredHeight: 16f);

        // 6B. Prawy Panel Podsumowania Nadziei (Run + Total na koncie)
        GameObject summarySection = CreateSectionPanel("Pnl_HopeSummarySection", middleGo.transform, "PODSUMOWANIE", panelInset, font, 0f);
        AddLayoutElement(summarySection, preferredWidth: 230f);

        GameObject summaryContent = CreateUIObject("Pnl_SummaryContent", summarySection.transform);
        AddLayoutElement(summaryContent, flexibleHeight: 1f);
        VerticalLayoutGroup sumVlg = summaryContent.AddComponent<VerticalLayoutGroup>();
        sumVlg.spacing = 6f;
        sumVlg.childAlignment = TextAnchor.MiddleCenter;
        sumVlg.childControlWidth = true;
        sumVlg.childControlHeight = true;

        CreateText("Lbl_RunHope", summaryContent.transform, "Z tego runu:", 12f, font, new Color(0.82f, 0.8f, 0.9f, 1f), TextAlignmentOptions.Center);
        TextMeshProUGUI txtRunHopeEarned = CreateText("Txt_RunHopeEarned", summaryContent.transform, "+0", 20f, font, new Color(0.76f, 0.45f, 1f, 1f), TextAlignmentOptions.Center);
        TextMeshProUGUI txtMultiplier = CreateText("Txt_MultiplierInfo", summaryContent.transform, "Mnożnik: x1.0", 12f, font, new Color(0.95f, 0.8f, 0.35f, 1f), TextAlignmentOptions.Center);

        GameObject totalBox = CreateUIObject("Pnl_TotalAccountHope", summaryContent.transform);
        AddLayoutElement(totalBox, preferredHeight: 82f);
        Image totalBoxImg = totalBox.AddComponent<Image>();
        ApplySprite(totalBoxImg, panelPlain, new Color(0.18f, 0.1f, 0.28f, 0.9f));
        VerticalLayoutGroup totalVlg = totalBox.AddComponent<VerticalLayoutGroup>();
        totalVlg.padding = new RectOffset(8, 8, 8, 8);
        totalVlg.spacing = 2f;
        totalVlg.childAlignment = TextAnchor.MiddleCenter;
        totalVlg.childControlWidth = true;
        totalVlg.childControlHeight = true;

        CreateText("Lbl_AccountHope", totalBox.transform, "Łączna Nadzieja", 12f, font, new Color(0.85f, 0.85f, 0.9f, 1f), TextAlignmentOptions.Center);
        TextMeshProUGUI txtTotalAccountHope = CreateText("Txt_TotalAccountHope", totalBox.transform, "0", 20f, font, Color.white, TextAlignmentOptions.Center);

        // 7. Dolna Sekcja - Czas/Rekord (lewo) + MENU / POWTÓRZ OBRONĘ (prawo)
        GameObject footerGo = CreateUIObject("Pnl_Footer", windowGo.transform);
        AddLayoutElement(footerGo, preferredHeight: 68f);
        HorizontalLayoutGroup footerHlg = footerGo.AddComponent<HorizontalLayoutGroup>();
        footerHlg.spacing = 14f;
        footerHlg.childAlignment = TextAnchor.MiddleCenter;
        footerHlg.childControlWidth = true;
        footerHlg.childControlHeight = true;
        footerHlg.childForceExpandHeight = true;

        // 7A. Lewa strona (Czas gry + Rekord)
        GameObject recordBox = CreateUIObject("Pnl_RunRecord", footerGo.transform);
        AddLayoutElement(recordBox, flexibleWidth: 1f);
        Image recordImg = recordBox.AddComponent<Image>();
        ApplySprite(recordImg, panelInset, new Color(0.11f, 0.1f, 0.13f, 0.95f));
        HorizontalLayoutGroup recHlg = recordBox.AddComponent<HorizontalLayoutGroup>();
        recHlg.padding = new RectOffset(16, 16, 8, 8);
        recHlg.spacing = 18f;
        recHlg.childAlignment = TextAnchor.MiddleLeft;
        recHlg.childControlWidth = false;
        recHlg.childControlHeight = true;

        TextMeshProUGUI txtRunDuration = CreateText("Txt_RunDuration", recordBox.transform, "Czas gry: 00:00", 14f, font, new Color(0.9f, 0.9f, 0.9f, 1f), TextAlignmentOptions.Left);
        AddLayoutElement(txtRunDuration.gameObject, preferredWidth: 160f);
        TextMeshProUGUI txtBestRecord = CreateText("Txt_BestRecord", recordBox.transform, "Rekord: 0 nocy", 14f, font, new Color(0.95f, 0.82f, 0.45f, 1f), TextAlignmentOptions.Left);
        AddLayoutElement(txtBestRecord.gameObject, preferredWidth: 160f);

        GameObject badgeGo = CreateUIObject("Badge_NewRecord", recordBox.transform);
        AddLayoutElement(badgeGo, preferredWidth: 120f, preferredHeight: 28f);
        Image badgeImg = badgeGo.AddComponent<Image>();
        badgeImg.color = new Color(0.85f, 0.55f, 0.1f, 1f);
        TextMeshProUGUI badgeTxt = CreateText("Txt_NewRecord", badgeGo.transform, "NOWY REKORD!", 10f, font, Color.white, TextAlignmentOptions.Center);
        StretchFull(badgeTxt.GetComponent<RectTransform>());

        // 7B. Prawa strona (MENU + POWTÓRZ OBRONĘ)
        GameObject buttonsGo = CreateUIObject("Pnl_ActionButtons", footerGo.transform);
        AddLayoutElement(buttonsGo, preferredWidth: 380f);
        HorizontalLayoutGroup btnHlg = buttonsGo.AddComponent<HorizontalLayoutGroup>();
        btnHlg.spacing = 12f;
        btnHlg.childControlWidth = true;
        btnHlg.childControlHeight = true;
        btnHlg.childForceExpandWidth = true;
        btnHlg.childForceExpandHeight = true;

        Button btnMenu = CreateActionButton("Btn_Menu", buttonsGo.transform, "MENU", btnNormal, new Color(0.55f, 0.25f, 0.25f, 1f), font);

        GameObject metaBadgeGo = CreateUIObject("Badge_MetaAvailable", btnMenu.transform);
        RectTransform metaBadgeRt = metaBadgeGo.GetComponent<RectTransform>();
        metaBadgeRt.anchorMin = new Vector2(1f, 1f);
        metaBadgeRt.anchorMax = new Vector2(1f, 1f);
        metaBadgeRt.pivot = new Vector2(0.85f, 0.5f);
        metaBadgeRt.anchoredPosition = new Vector2(-6f, 2f);
        metaBadgeRt.sizeDelta = new Vector2(106f, 22f);
        Image metaBadgeImg = metaBadgeGo.AddComponent<Image>();
        metaBadgeImg.color = new Color(0.85f, 0.62f, 0.12f, 1f);
        metaBadgeImg.raycastTarget = false;
        TextMeshProUGUI metaBadgeTxt = CreateText("Txt_MetaAvailable", metaBadgeGo.transform, "ULEPSZENIA!", 10f, font, Color.white, TextAlignmentOptions.Center);
        metaBadgeTxt.raycastTarget = false;
        StretchFull(metaBadgeTxt.GetComponent<RectTransform>());
        metaBadgeGo.SetActive(false);

        Button btnRetry = CreateActionButton("Btn_Retry", buttonsGo.transform, "POWTÓRZ OBRONĘ", btnNormal, new Color(0.22f, 0.45f, 0.65f, 1f), font);

        // 7C. Tooltip Karty Nadziei (nad wszystkimi elementami w oknie)
        GameObject tooltipGo = CreateUIObject("Pnl_CardTooltip", rootGo.transform);
        RectTransform tooltipRt = tooltipGo.GetComponent<RectTransform>();
        tooltipRt.pivot = new Vector2(0.5f, 0f);
        tooltipRt.sizeDelta = new Vector2(260f, 120f);
        Image tooltipImg = tooltipGo.AddComponent<Image>();
        ApplySprite(tooltipImg, tooltipBg, new Color(0.08f, 0.06f, 0.12f, 0.96f));
        tooltipImg.raycastTarget = false;

        VerticalLayoutGroup tipVlg = tooltipGo.AddComponent<VerticalLayoutGroup>();
        tipVlg.padding = new RectOffset(12, 12, 10, 10);
        tipVlg.spacing = 4f;
        tipVlg.childControlWidth = true;
        tipVlg.childControlHeight = true;
        tipVlg.childForceExpandWidth = true;
        tipVlg.childForceExpandHeight = false;

        ContentSizeFitter tipFitter = tooltipGo.AddComponent<ContentSizeFitter>();
        tipFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        tipFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI txtTooltipTitle = CreateText("Txt_TooltipTitle", tooltipGo.transform, "Survivor (+300)", 14f, font, Color.white, TextAlignmentOptions.Left);
        txtTooltipTitle.raycastTarget = false;
        TextMeshProUGUI txtTooltipRarity = CreateText("Txt_TooltipRarity", tooltipGo.transform, "Ranga: Rzadka", 12f, font, new Color(0.3f, 0.85f, 0.4f, 1f), TextAlignmentOptions.Left);
        txtTooltipRarity.raycastTarget = false;
        TextMeshProUGUI txtTooltipBreakdown = CreateText("Txt_TooltipBreakdown", tooltipGo.transform, "• Przetrwano noc 5 (+100)", 12f, font, new Color(0.85f, 0.85f, 0.9f, 1f), TextAlignmentOptions.Left);
        txtTooltipBreakdown.raycastTarget = false;
        tooltipGo.SetActive(false);

        // 8. Podpięcie wszystkich [SerializeField] przez SerializedObject
        SerializedObject so = new SerializedObject(screenUI);
        so.FindProperty("panelRoot").objectReferenceValue = rootGo;
        so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        so.FindProperty("animatedWindow").objectReferenceValue = windowRt;
        so.FindProperty("txtHeaderTitle").objectReferenceValue = txtHeaderTitle;
        so.FindProperty("txtHeaderSubtitle").objectReferenceValue = txtHeaderSubtitle;
        so.FindProperty("txtNightsSurvived").objectReferenceValue = txtNightsSurvived;
        so.FindProperty("imgHeaderBanner").objectReferenceValue = headerImg;

        so.FindProperty("txtEnemiesKilled").objectReferenceValue = txtEnemiesKilled;
        so.FindProperty("txtBossesKilled").objectReferenceValue = txtBossesKilled;
        so.FindProperty("txtTowersBuilt").objectReferenceValue = txtTowersBuilt;
        so.FindProperty("txtProductionBuilt").objectReferenceValue = txtProductionBuilt;
        so.FindProperty("txtExpeditionsCompleted").objectReferenceValue = txtExpeditions;
        so.FindProperty("txtRunesCrafted").objectReferenceValue = txtRunesCrafted;
        so.FindProperty("txtFinalPopulation").objectReferenceValue = txtFinalPop;

        SerializedProperty slotsProp = so.FindProperty("cardSlots");
        slotsProp.arraySize = cardSlots.Length;
        for (int i = 0; i < cardSlots.Length; i++)
        {
            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = cardSlots[i];
        }

        so.FindProperty("btnPrevPage").objectReferenceValue = btnPrev;
        so.FindProperty("btnNextPage").objectReferenceValue = btnNext;
        so.FindProperty("txtPageIndicator").objectReferenceValue = txtPageIndicator;
        so.FindProperty("objEmptyCardsInfo").objectReferenceValue = emptyInfoGo;

        so.FindProperty("pnlCardTooltip").objectReferenceValue = tooltipRt;
        so.FindProperty("txtTooltipTitle").objectReferenceValue = txtTooltipTitle;
        so.FindProperty("txtTooltipRarity").objectReferenceValue = txtTooltipRarity;
        so.FindProperty("txtTooltipBreakdown").objectReferenceValue = txtTooltipBreakdown;

        so.FindProperty("txtRunHopeEarned").objectReferenceValue = txtRunHopeEarned;
        so.FindProperty("txtMultiplierInfo").objectReferenceValue = txtMultiplier;
        so.FindProperty("txtTotalAccountHope").objectReferenceValue = txtTotalAccountHope;

        so.FindProperty("txtRunDuration").objectReferenceValue = txtRunDuration;
        so.FindProperty("txtBestRecord").objectReferenceValue = txtBestRecord;
        so.FindProperty("objNewRecordBadge").objectReferenceValue = badgeGo;
        so.FindProperty("btnMenu").objectReferenceValue = btnMenu;
        so.FindProperty("objMetaUpgradeBadge").objectReferenceValue = metaBadgeGo;
        so.FindProperty("btnRetry").objectReferenceValue = btnRetry;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Selection.activeGameObject = canvasGo;

        Debug.Log("[EndGameScreenSceneBuilder] Zbudowano layout Canvas_EndGame na scenie TESTSCENE_ENDSCRENE!");
    }

    private static Sprite LoadUiSprite(string fileName)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(UiSpriteRoot + fileName);
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void AddLayoutElement(GameObject go, float preferredWidth = -1f, float preferredHeight = -1f, float flexibleWidth = -1f, float flexibleHeight = -1f, bool ignoreLayout = false)
    {
        LayoutElement le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.ignoreLayout = ignoreLayout;
        if (preferredWidth >= 0f) le.preferredWidth = preferredWidth;
        if (preferredHeight >= 0f) le.preferredHeight = preferredHeight;
        if (flexibleWidth >= 0f) le.flexibleWidth = flexibleWidth;
        if (flexibleHeight >= 0f) le.flexibleHeight = flexibleHeight;
    }

    private static void ApplySprite(Image img, Sprite sprite, Color color)
    {
        img.sprite = sprite;
        img.color = color;
        if (sprite != null)
        {
            img.type = Image.Type.Sliced;
        }
    }

    private static void ApplyTextStyle(TextMeshProUGUI tmp, string text, float fontSize, TMP_FontAsset font, Color color, TextAlignmentOptions alignment)
    {
        tmp.text = text;
        tmp.fontSize = fontSize;
        if (font != null) tmp.font = font;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, TMP_FontAsset font, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = CreateUIObject(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        ApplyTextStyle(tmp, text, fontSize, font, color, alignment);
        return tmp;
    }

    private static GameObject CreateSectionPanel(string name, Transform parent, string title, Sprite bgSprite, TMP_FontAsset font, float preferredHeight)
    {
        GameObject section = CreateUIObject(name, parent);
        if (preferredHeight > 0f) AddLayoutElement(section, preferredHeight: preferredHeight);

        Image bg = section.AddComponent<Image>();
        ApplySprite(bg, bgSprite, new Color(0.09f, 0.08f, 0.11f, 0.95f));

        VerticalLayoutGroup vlg = section.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 10, 10);
        vlg.spacing = 8f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        TextMeshProUGUI titleTmp = CreateText("Txt_SectionTitle", section.transform, title, 16f, font, new Color(0.9f, 0.82f, 0.65f, 1f), TextAlignmentOptions.Left);
        AddLayoutElement(titleTmp.gameObject, preferredHeight: 22f);

        return section;
    }

    private static TextMeshProUGUI CreateStatItem(Transform parent, string name, string label, Sprite bgSprite, TMP_FontAsset font)
    {
        GameObject item = CreateUIObject(name, parent);
        Image bg = item.AddComponent<Image>();
        ApplySprite(bg, bgSprite, new Color(0.15f, 0.14f, 0.18f, 0.9f));

        HorizontalLayoutGroup hlg = item.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(10, 10, 4, 4);
        hlg.spacing = 6f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        TextMeshProUGUI lbl = CreateText("Lbl", item.transform, label, 12f, font, new Color(0.78f, 0.78f, 0.82f, 1f), TextAlignmentOptions.Left);
        AddLayoutElement(lbl.gameObject, flexibleWidth: 1f);

        TextMeshProUGUI val = CreateText("Val", item.transform, "0", 14f, font, Color.white, TextAlignmentOptions.Right);
        AddLayoutElement(val.gameObject, preferredWidth: 50f);

        return val;
    }

    private static EndGameHopeCardUI CreateHopeCard(string name, Transform parent, Sprite frameSprite, Sprite innerSprite, TMP_FontAsset font)
    {
        GameObject cardGo = CreateUIObject(name, parent);
        AddLayoutElement(cardGo, preferredWidth: 132f, preferredHeight: 196f);
        CanvasGroup cardCg = cardGo.AddComponent<CanvasGroup>();

        Image cardBg = cardGo.AddComponent<Image>();
        ApplySprite(cardBg, frameSprite, new Color(0.18f, 0.14f, 0.22f, 0.98f));

        VerticalLayoutGroup vlg = cardGo.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.spacing = 6f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;

        TextMeshProUGUI txtAmount = CreateText("Txt_Amount", cardGo.transform, "+100", 16f, font, new Color(0.85f, 0.55f, 1f, 1f), TextAlignmentOptions.Center);
        AddLayoutElement(txtAmount.gameObject, preferredHeight: 24f);

        GameObject iconFrame = CreateUIObject("Img_IconFrame", cardGo.transform);
        AddLayoutElement(iconFrame, preferredHeight: 74f);
        Image frameImg = iconFrame.AddComponent<Image>();
        ApplySprite(frameImg, innerSprite, new Color(0.1f, 0.08f, 0.14f, 1f));

        GameObject iconGo = CreateUIObject("Img_CategoryIcon", iconFrame.transform);
        RectTransform iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.sizeDelta = new Vector2(32f, 32f); // GEMINI.md Standard icon size
        Image iconImg = iconGo.AddComponent<Image>();
        iconImg.preserveAspect = true;

        TextMeshProUGUI txtTitle = CreateText("Txt_Title", cardGo.transform, "Survivor", 12f, font, Color.white, TextAlignmentOptions.Center);
        AddLayoutElement(txtTitle.gameObject, flexibleHeight: 1f);

        GameObject gemGo = CreateUIObject("Img_RarityGem", cardGo.transform);
        AddLayoutElement(gemGo, preferredWidth: 24f, preferredHeight: 24f); // GEMINI.md Small icon size
        Image gemImg = gemGo.AddComponent<Image>();
        gemImg.preserveAspect = true;

        EndGameHopeCardUI cardUI = cardGo.AddComponent<EndGameHopeCardUI>();
        SerializedObject cardSo = new SerializedObject(cardUI);
        cardSo.FindProperty("txtTitle").objectReferenceValue = txtTitle;
        cardSo.FindProperty("txtAmount").objectReferenceValue = txtAmount;
        cardSo.FindProperty("imgCategoryIcon").objectReferenceValue = iconImg;
        cardSo.FindProperty("imgRarityGem").objectReferenceValue = gemImg;
        cardSo.FindProperty("canvasGroup").objectReferenceValue = cardCg;
        cardSo.ApplyModifiedPropertiesWithoutUndo();

        return cardUI;
    }

    private static Button CreateArrowButton(string name, Transform parent, string label, Sprite bgSprite, TMP_FontAsset font)
    {
        GameObject btnGo = CreateUIObject(name, parent);
        AddLayoutElement(btnGo, preferredWidth: 36f, preferredHeight: 54f);
        Image img = btnGo.AddComponent<Image>();
        ApplySprite(img, bgSprite, new Color(0.32f, 0.22f, 0.42f, 1f));
        Button btn = btnGo.AddComponent<Button>();

        TextMeshProUGUI txt = CreateText("Txt", btnGo.transform, label, 16f, font, Color.white, TextAlignmentOptions.Center);
        StretchFull(txt.GetComponent<RectTransform>());
        return btn;
    }

    private static Button CreateActionButton(string name, Transform parent, string label, Sprite bgSprite, Color color, TMP_FontAsset font)
    {
        GameObject btnGo = CreateUIObject(name, parent);
        Image img = btnGo.AddComponent<Image>();
        ApplySprite(img, bgSprite, color);
        Button btn = btnGo.AddComponent<Button>();

        TextMeshProUGUI txt = CreateText("Txt", btnGo.transform, label, 16f, font, Color.white, TextAlignmentOptions.Center);
        StretchFull(txt.GetComponent<RectTransform>());
        return btn;
    }
}
