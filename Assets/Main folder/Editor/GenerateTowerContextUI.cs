using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SetupTowerContextWindowComponent
{
    [MenuItem("Tools/Attach TowerContextWindowUI To Scene")]
    public static void AttachAndConfigure()
    {
        string scenePath = "Assets/Main folder/_Scenes/TESTSCENE_UI.unity";
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != scenePath)
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        GameObject root = GameObject.Find("Canvas_ContextWindows/Pnl_TowerContext");
        if (root == null)
        {
            Debug.LogError("[TowerContextUI] Nie znaleziono Pnl_TowerContext w Canvas_ContextWindows!");
            return;
        }

        TowerContextWindowUI ui = root.GetComponent<TowerContextWindowUI>();
        if (ui == null)
        {
            ui = root.AddComponent<TowerContextWindowUI>();
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
            centerController.towerPanel = root;
        }

        EditorUtility.SetDirty(root);
        if (centerController != null) EditorUtility.SetDirty(centerController);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[TowerContextUI] Pomyślnie podpięto TowerContextWindowUI do Pnl_TowerContext i skonfigurowano CenterInfoPanelControllerUI!");
    }
}

public static class GenerateTowerContextUI
{
    [MenuItem("Tools/Generate Tower Context UI")]
    public static void BuildAll()
    {
        Debug.Log("[TowerContextUI] Rozpoczynanie generowania UI...");

        // 1. Sprawdź i załaduj scenę TESTSCENE_UI
        string scenePath = "Assets/Main folder/_Scenes/TESTSCENE_UI.unity";
        var currentScene = EditorSceneManager.GetActiveScene();
        if (currentScene.path != scenePath)
        {
            currentScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        // 2. Znajdź domyślny font TextMeshPro
        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
        if (defaultFont == null)
        {
            defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        }

        // 3. Utwórz prefab Pnl_CombatStatItem
        GameObject statPrefab = CreateCombatStatItemPrefab(defaultFont);

        // 4. Znajdź Canvas_ContextWindows
        GameObject canvasObj = GameObject.Find("Canvas_ContextWindows");
        if (canvasObj == null)
        {
            Debug.LogError("[TowerContextUI] Nie znaleziono Canvas_ContextWindows na scenie!");
            return;
        }

        // Usuń stary Pnl_TowerContext jeśli istnieje (żeby nie duplikować)
        Transform oldPanel = canvasObj.transform.Find("Pnl_TowerContext");
        if (oldPanel != null)
        {
            Undo.DestroyObjectImmediate(oldPanel.gameObject);
        }

        // 5. Zbuduj całe okno Pnl_TowerContext
        GameObject towerContext = BuildTowerContextHierarchy(canvasObj.transform, defaultFont, statPrefab);

        // 6. Zapisz scenę i assety
        Undo.RegisterCreatedObjectUndo(towerContext, "Generate Tower Context UI");
        EditorSceneManager.MarkSceneDirty(currentScene);
        EditorSceneManager.SaveScene(currentScene);
        AssetDatabase.SaveAssets();

        Debug.Log("[TowerContextUI] Pomyślnie wygenerowano Pnl_TowerContext na scenie TESTSCENE_UI!");
    }

    private static GameObject CreateCombatStatItemPrefab(TMP_FontAsset font)
    {
        string folderPath = "Assets/Main folder/Prefabs/UI";
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
        string prefabPath = Path.Combine(folderPath, "Pnl_CombatStatItem.prefab").Replace("\\", "/");

        GameObject item = new GameObject("Pnl_CombatStatItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        RectTransform rt = item.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160, 36);

        LayoutElement le = item.GetComponent<LayoutElement>();
        le.preferredHeight = 36;
        le.flexibleWidth = 1;

        Image bgImage = item.GetComponent<Image>();
        bgImage.sprite = null; // Zgodnie z poleceniem: brak source image
        bgImage.color = new Color(0.12f, 0.13f, 0.16f, 0.85f);

        HorizontalLayoutGroup hlg = item.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(6, 6, 4, 4);
        hlg.spacing = 6;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // Ikonka (24px)
        GameObject iconObj = new GameObject("Img_StatIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconObj.transform.SetParent(item.transform, false);
        RectTransform iconRt = iconObj.GetComponent<RectTransform>();
        iconRt.sizeDelta = new Vector2(24, 24);
        LayoutElement iconLe = iconObj.GetComponent<LayoutElement>();
        iconLe.preferredWidth = 24;
        iconLe.preferredHeight = 24;
        Image iconImg = iconObj.GetComponent<Image>();
        iconImg.sprite = null;
        iconImg.color = new Color(1f, 1f, 1f, 0.4f);

        // Nazwa statystyki (14 pt)
        GameObject nameObj = new GameObject("Txt_StatName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        nameObj.transform.SetParent(item.transform, false);
        RectTransform nameRt = nameObj.GetComponent<RectTransform>();
        nameRt.sizeDelta = new Vector2(60, 24);
        LayoutElement nameLe = nameObj.GetComponent<LayoutElement>();
        nameLe.flexibleWidth = 1;
        TextMeshProUGUI nameTmp = nameObj.GetComponent<TextMeshProUGUI>();
        nameTmp.font = font;
        nameTmp.fontSize = 14;
        nameTmp.text = "Obrażenia";
        nameTmp.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        nameTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Kontener wartości (przekreślona bazowa + aktualna)
        GameObject valuesObj = new GameObject("Pnl_Values", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        valuesObj.transform.SetParent(item.transform, false);
        RectTransform valRt = valuesObj.GetComponent<RectTransform>();
        valRt.sizeDelta = new Vector2(64, 24);
        LayoutElement valLe = valuesObj.GetComponent<LayoutElement>();
        valLe.preferredWidth = 64;
        HorizontalLayoutGroup valHlg = valuesObj.GetComponent<HorizontalLayoutGroup>();
        valHlg.spacing = 4;
        valHlg.childAlignment = TextAnchor.MiddleRight;
        valHlg.childControlWidth = false;
        valHlg.childControlHeight = false;
        valHlg.childForceExpandWidth = false;
        valHlg.childForceExpandHeight = false;

        // Wartość bazowa przekreślona
        GameObject baseValObj = new GameObject("Txt_BaseValue", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        baseValObj.transform.SetParent(valuesObj.transform, false);
        RectTransform baseValRt = baseValObj.GetComponent<RectTransform>();
        baseValRt.sizeDelta = new Vector2(24, 24);
        TextMeshProUGUI baseValTmp = baseValObj.GetComponent<TextMeshProUGUI>();
        baseValTmp.font = font;
        baseValTmp.fontSize = 12;
        baseValTmp.text = "<s>145</s>";
        baseValTmp.color = new Color(0.55f, 0.55f, 0.55f, 1f);
        baseValTmp.alignment = TextAlignmentOptions.MidlineRight;

        // Wartość aktualna
        GameObject curValObj = new GameObject("Txt_CurrentValue", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        curValObj.transform.SetParent(valuesObj.transform, false);
        RectTransform curValRt = curValObj.GetComponent<RectTransform>();
        curValRt.sizeDelta = new Vector2(36, 24);
        TextMeshProUGUI curValTmp = curValObj.GetComponent<TextMeshProUGUI>();
        curValTmp.font = font;
        curValTmp.fontSize = 14;
        curValTmp.fontStyle = FontStyles.Bold;
        curValTmp.text = "73";
        curValTmp.color = new Color(0.95f, 0.35f, 0.35f, 1f);
        curValTmp.alignment = TextAlignmentOptions.MidlineRight;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(item, prefabPath);
        Object.DestroyImmediate(item);
        return prefab;
    }

    private static GameObject BuildTowerContextHierarchy(Transform parent, TMP_FontAsset font, GameObject statPrefab)
    {
        // Główny obiekt: Pnl_TowerContext
        GameObject root = new GameObject("Pnl_TowerContext", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        root.transform.SetParent(parent, false);

        RectTransform rootRt = root.GetComponent<RectTransform>();
        // Zakotwiczenie po prawej stronie ekranu: szerokość 540px, elastyczna wysokość
        rootRt.anchorMin = new Vector2(1f, 0.5f);
        rootRt.anchorMax = new Vector2(1f, 0.5f);
        rootRt.pivot = new Vector2(1f, 0.5f);
        rootRt.anchoredPosition = new Vector2(-20f, 0f);
        rootRt.sizeDelta = new Vector2(540f, 0f);

        Image rootImg = root.GetComponent<Image>();
        rootImg.sprite = null; // Brak source image
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

        // ── Pasek górny ──────────────────────────────────────────────────────────
        BuildTopBar(root.transform, font);

        // ── Kontener na 2 kolumny ────────────────────────────────────────────────
        GameObject colsContainer = new GameObject("Pnl_ColumnsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        colsContainer.transform.SetParent(root.transform, false);

        HorizontalLayoutGroup colsHlg = colsContainer.GetComponent<HorizontalLayoutGroup>();
        colsHlg.spacing = 12;
        colsHlg.childAlignment = TextAnchor.UpperLeft;
        colsHlg.childControlWidth = false;
        colsHlg.childControlHeight = false;
        colsHlg.childForceExpandWidth = false;
        colsHlg.childForceExpandHeight = false;

        ContentSizeFitter colsCsf = colsContainer.GetComponent<ContentSizeFitter>();
        colsCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        colsCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── Lewa kolumna (szeroka: 334px) ────────────────────────────────────────
        BuildLeftColumn(colsContainer.transform, font, statPrefab);

        // ── Prawa kolumna (wąska: 166px) ─────────────────────────────────────────
        BuildRightColumn(colsContainer.transform, font);

        return root;
    }

    private static void BuildTopBar(Transform parent, TMP_FontAsset font)
    {
        GameObject topBar = new GameObject("Pnl_TopBar", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        topBar.transform.SetParent(parent, false);

        RectTransform tbRt = topBar.GetComponent<RectTransform>();
        tbRt.sizeDelta = new Vector2(0, 36);

        LayoutElement tbLe = topBar.GetComponent<LayoutElement>();
        tbLe.minHeight = 36;
        tbLe.preferredHeight = 36;

        HorizontalLayoutGroup tbHlg = topBar.GetComponent<HorizontalLayoutGroup>();
        tbHlg.childAlignment = TextAnchor.MiddleLeft;
        tbHlg.childControlWidth = false;
        tbHlg.childControlHeight = false;
        tbHlg.childForceExpandWidth = false;
        tbHlg.childForceExpandHeight = false;

        // Tytuł okna (20 pt)
        GameObject titleObj = new GameObject("Txt_TowerName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        titleObj.transform.SetParent(topBar.transform, false);
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.sizeDelta = new Vector2(460, 36);
        LayoutElement titleLe = titleObj.GetComponent<LayoutElement>();
        titleLe.flexibleWidth = 1;
        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.fontSize = 20; // 20 pt: główne informacje
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.text = "WIEŻA ARBALEST";
        titleTmp.color = new Color(1f, 0.85f, 0.4f, 1f);
        titleTmp.alignment = TextAlignmentOptions.MidlineLeft;

        // Przycisk zamknięcia
        GameObject closeBtnObj = new GameObject("Btn_Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        closeBtnObj.transform.SetParent(topBar.transform, false);
        RectTransform closeRt = closeBtnObj.GetComponent<RectTransform>();
        closeRt.sizeDelta = new Vector2(32, 32);
        LayoutElement closeLe = closeBtnObj.GetComponent<LayoutElement>();
        closeLe.preferredWidth = 32;
        closeLe.preferredHeight = 32;
        Image closeImg = closeBtnObj.GetComponent<Image>();
        closeImg.sprite = null;
        closeImg.color = new Color(0.35f, 0.15f, 0.15f, 0.9f);

        GameObject closeTxtObj = new GameObject("Txt_Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
        RectTransform closeTxtRt = closeTxtObj.GetComponent<RectTransform>();
        closeTxtRt.anchorMin = Vector2.zero;
        closeTxtRt.anchorMax = Vector2.one;
        closeTxtRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI closeTmp = closeTxtObj.GetComponent<TextMeshProUGUI>();
        closeTmp.font = font;
        closeTmp.fontSize = 16;
        closeTmp.text = "✕";
        closeTmp.color = Color.white;
        closeTmp.alignment = TextAlignmentOptions.Center;
    }

    private static void BuildLeftColumn(Transform parent, TMP_FontAsset font, GameObject statPrefab)
    {
        GameObject leftCol = new GameObject("Pnl_LeftColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        leftCol.transform.SetParent(parent, false);

        RectTransform lcRt = leftCol.GetComponent<RectTransform>();
        lcRt.sizeDelta = new Vector2(334, 0);

        LayoutElement lcLe = leftCol.GetComponent<LayoutElement>();
        lcLe.preferredWidth = 334;

        VerticalLayoutGroup lcVlg = leftCol.GetComponent<VerticalLayoutGroup>();
        lcVlg.spacing = 10;
        lcVlg.childAlignment = TextAnchor.UpperLeft;
        lcVlg.childControlWidth = true;
        lcVlg.childControlHeight = false;
        lcVlg.childForceExpandWidth = true;
        lcVlg.childForceExpandHeight = false;

        ContentSizeFitter lcCsf = leftCol.GetComponent<ContentSizeFitter>();
        lcCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        lcCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── 1. Sekcja Upkeep (Utrzymanie) ─────────────────────────────────────────
        BuildUpkeepSection(leftCol.transform, font);

        // ── 2. Sekcja Obsady Wieży ───────────────────────────────────────────────
        BuildWorkersSection(leftCol.transform, font);

        // ── 3. Sekcja Statystyk Bojowych ──────────────────────────────────────────
        BuildCombatStatsSection(leftCol.transform, font, statPrefab);
    }

    private static void BuildUpkeepSection(Transform parent, TMP_FontAsset font)
    {
        GameObject sec = new GameObject("Sec_Upkeep", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        sec.transform.SetParent(parent, false);

        VerticalLayoutGroup vlg = sec.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 4;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = sec.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Nagłówek (16 pt)
        GameObject titleObj = new GameObject("Txt_UpkeepTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        titleObj.transform.SetParent(sec.transform, false);
        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.fontSize = 16;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.text = "UTRZYMANIE";
        titleTmp.color = new Color(0.7f, 0.85f, 1f, 1f);

        // Kontener zasobów poziomo
        GameObject resContainer = new GameObject("Pnl_UpkeepResources", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        resContainer.transform.SetParent(sec.transform, false);

        HorizontalLayoutGroup hlg = resContainer.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        ContentSizeFitter resCsf = resContainer.GetComponent<ContentSizeFitter>();
        resCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        resCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Dwa przykładowe elementy zapotrzebowania
        CreateUpkeepItem(resContainer.transform, font, "Złoto", "-2");
        CreateUpkeepItem(resContainer.transform, font, "Ruda", "-1");
    }

    private static void CreateUpkeepItem(Transform parent, TMP_FontAsset font, string name, string amount)
    {
        GameObject item = new GameObject("UpkeepItem_" + name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        item.transform.SetParent(parent, false);

        RectTransform rt = item.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(72, 28);
        LayoutElement le = item.GetComponent<LayoutElement>();
        le.preferredWidth = 72;
        le.preferredHeight = 28;

        Image img = item.GetComponent<Image>();
        img.sprite = null;
        img.color = new Color(0.15f, 0.16f, 0.2f, 0.9f);

        HorizontalLayoutGroup hlg = item.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(4, 4, 2, 2);
        hlg.spacing = 4;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        // Ikonka (24px)
        GameObject iconObj = new GameObject("Img_ResourceIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconObj.transform.SetParent(item.transform, false);
        RectTransform iconRt = iconObj.GetComponent<RectTransform>();
        iconRt.sizeDelta = new Vector2(24, 24);
        LayoutElement iconLe = iconObj.GetComponent<LayoutElement>();
        iconLe.preferredWidth = 24;
        iconLe.preferredHeight = 24;
        Image iconImg = iconObj.GetComponent<Image>();
        iconImg.sprite = null;
        iconImg.color = new Color(1f, 1f, 1f, 0.5f);

        // Ilość (14 pt)
        GameObject txtObj = new GameObject("Txt_Amount", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(item.transform, false);
        RectTransform txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.sizeDelta = new Vector2(36, 24);
        TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = 14;
        tmp.fontStyle = FontStyles.Bold;
        tmp.text = amount;
        tmp.color = new Color(1f, 0.4f, 0.4f, 1f);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private static void BuildWorkersSection(Transform parent, TMP_FontAsset font)
    {
        GameObject sec = new GameObject("Sec_Workers", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        sec.transform.SetParent(parent, false);

        VerticalLayoutGroup vlg = sec.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = sec.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Nagłówek (16 pt)
        GameObject titleObj = new GameObject("Txt_WorkersTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(sec.transform, false);
        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.fontSize = 16;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.text = "OBSADA WIEŻY";
        titleTmp.color = new Color(0.7f, 0.85f, 1f, 1f);

        // 1. Wiersz podsumowania: Pracownicy / Max + Wydajność %
        GameObject summaryObj = new GameObject("Pnl_WorkersSummary", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        summaryObj.transform.SetParent(sec.transform, false);

        HorizontalLayoutGroup sumHlg = summaryObj.GetComponent<HorizontalLayoutGroup>();
        sumHlg.childControlWidth = false;
        sumHlg.childControlHeight = false;
        sumHlg.childForceExpandWidth = false;

        ContentSizeFitter sumCsf = summaryObj.GetComponent<ContentSizeFitter>();
        sumCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject workersTxtObj = new GameObject("Txt_WorkerCount", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        workersTxtObj.transform.SetParent(summaryObj.transform, false);
        RectTransform wRt = workersTxtObj.GetComponent<RectTransform>();
        wRt.sizeDelta = new Vector2(160, 22);
        LayoutElement wLe = workersTxtObj.GetComponent<LayoutElement>();
        wLe.flexibleWidth = 1;
        TextMeshProUGUI wTmp = workersTxtObj.GetComponent<TextMeshProUGUI>();
        wTmp.font = font;
        wTmp.fontSize = 14;
        wTmp.text = "Pracownicy: 0 / 4";
        wTmp.color = Color.white;
        wTmp.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject effTxtObj = new GameObject("Txt_EfficiencyPercentage", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        effTxtObj.transform.SetParent(summaryObj.transform, false);
        RectTransform eRt = effTxtObj.GetComponent<RectTransform>();
        eRt.sizeDelta = new Vector2(160, 22);
        LayoutElement eLe = effTxtObj.GetComponent<LayoutElement>();
        eLe.flexibleWidth = 1;
        TextMeshProUGUI eTmp = effTxtObj.GetComponent<TextMeshProUGUI>();
        eTmp.font = font;
        eTmp.fontSize = 14;
        eTmp.fontStyle = FontStyles.Bold;
        eTmp.text = "Wydajność: 50%";
        eTmp.color = new Color(1f, 0.8f, 0.2f, 1f);
        eTmp.alignment = TextAlignmentOptions.MidlineRight;

        // 2. Trzy kafelki ras obok siebie w poziomie
        GameObject raceRow = new GameObject("Pnl_RaceControllers", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        raceRow.transform.SetParent(sec.transform, false);

        HorizontalLayoutGroup raceHlg = raceRow.GetComponent<HorizontalLayoutGroup>();
        raceHlg.spacing = 6;
        raceHlg.childControlWidth = true;
        raceHlg.childControlHeight = false;
        raceHlg.childForceExpandWidth = true;
        raceHlg.childForceExpandHeight = false;

        ContentSizeFitter raceCsf = raceRow.GetComponent<ContentSizeFitter>();
        raceCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateRaceBox(raceRow.transform, font, "KRASNOLUDY", new Color(1f, 0.8f, 0.2f), "13");
        CreateRaceBox(raceRow.transform, font, "LUDZIE", new Color(0.4f, 0.75f, 1f), "5");
        CreateRaceBox(raceRow.transform, font, "ELFY", new Color(0.4f, 1f, 0.6f), "2");

        // 3. Blok progów wydajności
        BuildEfficiencyThresholds(sec.transform, font);
    }

    private static void CreateRaceBox(Transform parent, TMP_FontAsset font, string raceName, Color accentColor, string freeCount)
    {
        GameObject box = new GameObject("Pnl_Race_" + raceName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        box.transform.SetParent(parent, false);

        LayoutElement le = box.GetComponent<LayoutElement>();
        le.flexibleWidth = 1;

        Image img = box.GetComponent<Image>();
        img.sprite = null;
        img.color = new Color(0.12f, 0.13f, 0.17f, 0.95f);

        VerticalLayoutGroup vlg = box.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(6, 6, 6, 6);
        vlg.spacing = 4;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Tytuł rasy
        GameObject nameObj = new GameObject("Txt_RaceTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        nameObj.transform.SetParent(box.transform, false);
        TextMeshProUGUI nameTmp = nameObj.GetComponent<TextMeshProUGUI>();
        nameTmp.font = font;
        nameTmp.fontSize = 12;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.text = raceName;
        nameTmp.color = accentColor;
        nameTmp.alignment = TextAlignmentOptions.Center;

        // Wiersz kontroli: [-] 0 [+]
        GameObject ctrlRow = new GameObject("Pnl_Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        ctrlRow.transform.SetParent(box.transform, false);
        HorizontalLayoutGroup ctrlHlg = ctrlRow.GetComponent<HorizontalLayoutGroup>();
        ctrlHlg.spacing = 4;
        ctrlHlg.childAlignment = TextAnchor.MiddleCenter;
        ctrlHlg.childControlWidth = false;
        ctrlHlg.childControlHeight = false;

        // Przycisk Minus
        GameObject minusBtn = new GameObject("Btn_Minus", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        minusBtn.transform.SetParent(ctrlRow.transform, false);
        minusBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(22, 22);
        Image minusImg = minusBtn.GetComponent<Image>();
        minusImg.sprite = null;
        minusImg.color = new Color(0.22f, 0.24f, 0.28f, 0.9f);
        GameObject minusTxt = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        minusTxt.transform.SetParent(minusBtn.transform, false);
        minusTxt.GetComponent<RectTransform>().sizeDelta = new Vector2(22, 22);
        TextMeshProUGUI mTmp = minusTxt.GetComponent<TextMeshProUGUI>();
        mTmp.font = font;
        mTmp.fontSize = 14;
        mTmp.text = "-";
        mTmp.alignment = TextAlignmentOptions.Center;

        // Licznik
        GameObject countObj = new GameObject("Txt_Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        countObj.transform.SetParent(ctrlRow.transform, false);
        countObj.GetComponent<RectTransform>().sizeDelta = new Vector2(28, 22);
        TextMeshProUGUI countTmp = countObj.GetComponent<TextMeshProUGUI>();
        countTmp.font = font;
        countTmp.fontSize = 15;
        countTmp.fontStyle = FontStyles.Bold;
        countTmp.text = "0";
        countTmp.alignment = TextAlignmentOptions.Center;

        // Przycisk Plus
        GameObject plusBtn = new GameObject("Btn_Plus", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        plusBtn.transform.SetParent(ctrlRow.transform, false);
        plusBtn.GetComponent<RectTransform>().sizeDelta = new Vector2(22, 22);
        Image plusImg = plusBtn.GetComponent<Image>();
        plusImg.sprite = null;
        plusImg.color = new Color(0.22f, 0.24f, 0.28f, 0.9f);
        GameObject plusTxt = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        plusTxt.transform.SetParent(plusBtn.transform, false);
        plusTxt.GetComponent<RectTransform>().sizeDelta = new Vector2(22, 22);
        TextMeshProUGUI pTmp = plusTxt.GetComponent<TextMeshProUGUI>();
        pTmp.font = font;
        pTmp.fontSize = 14;
        pTmp.text = "+";
        pTmp.alignment = TextAlignmentOptions.Center;

        // Wolni
        GameObject freeObj = new GameObject("Txt_FreeWorkers", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        freeObj.transform.SetParent(box.transform, false);
        TextMeshProUGUI freeTmp = freeObj.GetComponent<TextMeshProUGUI>();
        freeTmp.font = font;
        freeTmp.fontSize = 11;
        freeTmp.text = "Wolni: " + freeCount;
        freeTmp.color = new Color(0.6f, 0.8f, 1f, 0.9f);
        freeTmp.alignment = TextAlignmentOptions.Center;
    }

    private static void BuildEfficiencyThresholds(Transform parent, TMP_FontAsset font)
    {
        GameObject threshObj = new GameObject("Sec_EfficiencyThresholds", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        threshObj.transform.SetParent(parent, false);

        VerticalLayoutGroup vlg = threshObj.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 3;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = threshObj.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Nagłówek progów: "PROGI WYDAJNOŚCI [0-4]:" + "Poziom 0: 50% (tryb bezobsługowy)"
        GameObject headerObj = new GameObject("Pnl_ThresholdHeader", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        headerObj.transform.SetParent(threshObj.transform, false);

        HorizontalLayoutGroup hHlg = headerObj.GetComponent<HorizontalLayoutGroup>();
        hHlg.childControlWidth = false;
        hHlg.childControlHeight = false;
        hHlg.childForceExpandWidth = false;

        ContentSizeFitter hCsf = headerObj.GetComponent<ContentSizeFitter>();
        hCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject titleObj = new GameObject("Txt_ThresholdLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        titleObj.transform.SetParent(headerObj.transform, false);
        RectTransform tRt = titleObj.GetComponent<RectTransform>();
        tRt.sizeDelta = new Vector2(160, 20);
        LayoutElement tLe = titleObj.GetComponent<LayoutElement>();
        tLe.flexibleWidth = 1;
        TextMeshProUGUI tTmp = titleObj.GetComponent<TextMeshProUGUI>();
        tTmp.font = font;
        tTmp.fontSize = 12;
        tTmp.fontStyle = FontStyles.Bold;
        tTmp.text = "PROGI WYDAJNOŚCI [0-4]:";
        tTmp.color = new Color(0.4f, 0.75f, 1f, 1f);

        GameObject statusObj = new GameObject("Txt_CurrentThresholdStatus", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        statusObj.transform.SetParent(headerObj.transform, false);
        RectTransform sRt = statusObj.GetComponent<RectTransform>();
        sRt.sizeDelta = new Vector2(160, 20);
        LayoutElement sLe = statusObj.GetComponent<LayoutElement>();
        sLe.flexibleWidth = 1;
        TextMeshProUGUI sTmp = statusObj.GetComponent<TextMeshProUGUI>();
        sTmp.font = font;
        sTmp.fontSize = 12;
        sTmp.fontStyle = FontStyles.Bold;
        sTmp.text = "Poziom 0: 50% (tryb bezobsługowy)";
        sTmp.color = new Color(1f, 0.7f, 0.1f, 1f);
        sTmp.alignment = TextAlignmentOptions.MidlineRight;

        // Kontener segmentów progów (5 segmentów)
        GameObject barsObj = new GameObject("Pnl_ThresholdBarsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        barsObj.transform.SetParent(threshObj.transform, false);

        HorizontalLayoutGroup barsHlg = barsObj.GetComponent<HorizontalLayoutGroup>();
        barsHlg.spacing = 4;
        barsHlg.childControlWidth = true;
        barsHlg.childControlHeight = false;
        barsHlg.childForceExpandWidth = true;
        barsHlg.childForceExpandHeight = false;

        ContentSizeFitter barsCsf = barsObj.GetComponent<ContentSizeFitter>();
        barsCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateThresholdSegment(barsObj.transform, font, 0, "50%", new Color(0.9f, 0.25f, 0.25f, 1f), true);
        CreateThresholdSegment(barsObj.transform, font, 1, "100%", new Color(0.55f, 0.45f, 0.15f, 0.8f), false);
        CreateThresholdSegment(barsObj.transform, font, 2, "125%", new Color(0.35f, 0.55f, 0.25f, 0.8f), false);
        CreateThresholdSegment(barsObj.transform, font, 3, "145%", new Color(0.2f, 0.5f, 0.3f, 0.8f), false);
        CreateThresholdSegment(barsObj.transform, font, 4, "160%", new Color(0.15f, 0.45f, 0.35f, 0.8f), false);
    }

    private static void CreateThresholdSegment(Transform parent, TMP_FontAsset font, int level, string pct, Color barColor, bool isActive)
    {
        GameObject seg = new GameObject("Segment_" + level, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        seg.transform.SetParent(parent, false);

        LayoutElement le = seg.GetComponent<LayoutElement>();
        le.flexibleWidth = 1;

        VerticalLayoutGroup vlg = seg.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 2;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Strzałka / marker (dla aktywnego progu)
        GameObject markerObj = new GameObject("Img_ActiveMarker", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        markerObj.transform.SetParent(seg.transform, false);
        RectTransform mRt = markerObj.GetComponent<RectTransform>();
        mRt.sizeDelta = new Vector2(0, 10);
        LayoutElement mLe = markerObj.GetComponent<LayoutElement>();
        mLe.preferredHeight = 10;
        TextMeshProUGUI mTmp = markerObj.GetComponent<TextMeshProUGUI>();
        mTmp.font = font;
        mTmp.fontSize = 10;
        mTmp.text = isActive ? "▲" : "";
        mTmp.color = new Color(1f, 0.85f, 0.2f, 1f);
        mTmp.alignment = TextAlignmentOptions.Center;

        // Pasek
        GameObject barObj = new GameObject("Img_Bar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        barObj.transform.SetParent(seg.transform, false);
        RectTransform barRt = barObj.GetComponent<RectTransform>();
        barRt.sizeDelta = new Vector2(0, 12);
        LayoutElement barLe = barObj.GetComponent<LayoutElement>();
        barLe.preferredHeight = 12;
        Image barImg = barObj.GetComponent<Image>();
        barImg.sprite = null;
        barImg.color = barColor;

        // Liczba poziomu (0, 1, 2...)
        GameObject lvlObj = new GameObject("Txt_Level", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        lvlObj.transform.SetParent(seg.transform, false);
        TextMeshProUGUI lvlTmp = lvlObj.GetComponent<TextMeshProUGUI>();
        lvlTmp.font = font;
        lvlTmp.fontSize = 11;
        lvlTmp.text = level.ToString();
        lvlTmp.color = isActive ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(0.6f, 0.6f, 0.6f, 1f);
        lvlTmp.alignment = TextAlignmentOptions.Center;

        // Procent (50%, 100%...)
        GameObject pctObj = new GameObject("Txt_Percentage", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        pctObj.transform.SetParent(seg.transform, false);
        TextMeshProUGUI pctTmp = pctObj.GetComponent<TextMeshProUGUI>();
        pctTmp.font = font;
        pctTmp.fontSize = 11;
        pctTmp.fontStyle = FontStyles.Bold;
        pctTmp.text = pct;
        pctTmp.color = isActive ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(0.7f, 0.7f, 0.7f, 1f);
        pctTmp.alignment = TextAlignmentOptions.Center;
    }

    private static void BuildCombatStatsSection(Transform parent, TMP_FontAsset font, GameObject statPrefab)
    {
        GameObject sec = new GameObject("Sec_CombatStats", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        sec.transform.SetParent(parent, false);

        VerticalLayoutGroup vlg = sec.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 6;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = sec.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Nagłówek (16 pt)
        GameObject titleObj = new GameObject("Txt_CombatStatsTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(sec.transform, false);
        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.fontSize = 14;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.text = "STATYSTYKI BOJOWE (BAZA → POZIOM ZAŁOGI)";
        titleTmp.color = new Color(1f, 0.85f, 0.4f, 1f);

        // Siatka 2 kolumny x 4 wiersze
        GameObject gridObj = new GameObject("Pnl_CombatStatsGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        gridObj.transform.SetParent(sec.transform, false);

        GridLayoutGroup glg = gridObj.GetComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(164, 34);
        glg.spacing = new Vector2(6, 4);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 2;

        ContentSizeFitter gridCsf = gridObj.GetComponent<ContentSizeFitter>();
        gridCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 8 statystyk z prefabu
        CreateStatItemInstance(gridObj.transform, statPrefab, "Obrażenia", "<s>145</s>", "73 PKT", new Color(0.95f, 0.35f, 0.35f));
        CreateStatItemInstance(gridObj.transform, statPrefab, "Prędkość ataku", "<s>0.75</s>", "0.38 /S", new Color(0.95f, 0.35f, 0.35f));
        CreateStatItemInstance(gridObj.transform, statPrefab, "Zasięg", "", "480 M", Color.white);
        CreateStatItemInstance(gridObj.transform, statPrefab, "Typ obrażeń", "", "FIZYCZNE", Color.white);
        CreateStatItemInstance(gridObj.transform, statPrefab, "Szansa kryt.", "", "20%", Color.white);
        CreateStatItemInstance(gridObj.transform, statPrefab, "Mnożnik kryt.", "", "220%", Color.white);
        CreateStatItemInstance(gridObj.transform, statPrefab, "Przeb. pancerza", "", "35%", Color.white);
        CreateStatItemInstance(gridObj.transform, statPrefab, "Przeb. odp. mag.", "", "0%", Color.white);
    }

    private static void CreateStatItemInstance(Transform parent, GameObject prefab, string statName, string baseVal, string curVal, Color curColor)
    {
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        inst.name = "Stat_" + statName;

        Transform nameTr = inst.transform.Find("Txt_StatName");
        if (nameTr != null)
        {
            TextMeshProUGUI tmp = nameTr.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.text = statName;
        }

        Transform valuesTr = inst.transform.Find("Pnl_Values");
        if (valuesTr != null)
        {
            Transform baseTr = valuesTr.Find("Txt_BaseValue");
            if (baseTr != null)
            {
                TextMeshProUGUI tmp = baseTr.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = baseVal;
                    baseTr.gameObject.SetActive(!string.IsNullOrEmpty(baseVal));
                }
            }

            Transform curTr = valuesTr.Find("Txt_CurrentValue");
            if (curTr != null)
            {
                TextMeshProUGUI tmp = curTr.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = curVal;
                    tmp.color = curColor;
                }
            }
        }
    }

    private static void BuildRightColumn(Transform parent, TMP_FontAsset font)
    {
        GameObject rightCol = new GameObject("Pnl_RightColumn", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        rightCol.transform.SetParent(parent, false);

        RectTransform rcRt = rightCol.GetComponent<RectTransform>();
        rcRt.sizeDelta = new Vector2(166, 0);

        LayoutElement rcLe = rightCol.GetComponent<LayoutElement>();
        rcLe.preferredWidth = 166;

        VerticalLayoutGroup rcVlg = rightCol.GetComponent<VerticalLayoutGroup>();
        rcVlg.spacing = 10;
        rcVlg.childAlignment = TextAnchor.UpperLeft;
        rcVlg.childControlWidth = true;
        rcVlg.childControlHeight = false;
        rcVlg.childForceExpandWidth = true;
        rcVlg.childForceExpandHeight = false;

        ContentSizeFitter rcCsf = rightCol.GetComponent<ContentSizeFitter>();
        rcCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        rcCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Nagłówek (16 pt)
        GameObject titleObj = new GameObject("Txt_PriorityTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(rightCol.transform, false);
        TextMeshProUGUI titleTmp = titleObj.GetComponent<TextMeshProUGUI>();
        titleTmp.font = font;
        titleTmp.fontSize = 16;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.text = "PRIORYTET OSTRZAŁU";
        titleTmp.color = new Color(0.7f, 0.85f, 1f, 1f);

        // Opis
        GameObject subObj = new GameObject("Txt_PrioritySlotsLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        subObj.transform.SetParent(rightCol.transform, false);
        TextMeshProUGUI subTmp = subObj.GetComponent<TextMeshProUGUI>();
        subTmp.font = font;
        subTmp.fontSize = 12;
        subTmp.text = "Sloty priorytetów:";
        subTmp.color = new Color(0.7f, 0.7f, 0.7f, 1f);

        // 3 sloty priorytetów poziomo obok siebie
        GameObject slotsRow = new GameObject("Pnl_PrioritySlots", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        slotsRow.transform.SetParent(rightCol.transform, false);

        HorizontalLayoutGroup slotsHlg = slotsRow.GetComponent<HorizontalLayoutGroup>();
        slotsHlg.spacing = 6;
        slotsHlg.childAlignment = TextAnchor.MiddleCenter;
        slotsHlg.childControlWidth = true;
        slotsHlg.childControlHeight = false;
        slotsHlg.childForceExpandWidth = true;
        slotsHlg.childForceExpandHeight = false;

        ContentSizeFitter slotsCsf = slotsRow.GetComponent<ContentSizeFitter>();
        slotsCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Slot 1: Domyślnie wyszarzony Closest
        CreatePrioritySlot(slotsRow.transform, font, 1, "Najbliżej\n(Domyślny)", true);
        // Slot 2: Pusty
        CreatePrioritySlot(slotsRow.transform, font, 2, "[Pusty]", false);
        // Slot 3: Pusty
        CreatePrioritySlot(slotsRow.transform, font, 3, "[Pusty]", false);

        // Etykieta wyboru
        GameObject optLabelObj = new GameObject("Txt_OptionsLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        optLabelObj.transform.SetParent(rightCol.transform, false);
        TextMeshProUGUI optLabelTmp = optLabelObj.GetComponent<TextMeshProUGUI>();
        optLabelTmp.font = font;
        optLabelTmp.fontSize = 12;
        optLabelTmp.text = "Wybierz cel:";
        optLabelTmp.color = new Color(0.7f, 0.7f, 0.7f, 1f);

        // Siatka 3x3 z możliwymi wyborami (TargetingMode)
        GameObject optGrid = new GameObject("Pnl_TargetingOptionsGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        optGrid.transform.SetParent(rightCol.transform, false);

        GridLayoutGroup glg = optGrid.GetComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(50, 50);
        glg.spacing = new Vector2(6, 6);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 3;

        ContentSizeFitter optCsf = optGrid.GetComponent<ContentSizeFitter>();
        optCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 9 opcji TargetingMode
        CreateTargetingOptionButton(optGrid.transform, font, "Closest", "BLISKO");
        CreateTargetingOptionButton(optGrid.transform, font, "Furthest", "DALEKO");
        CreateTargetingOptionButton(optGrid.transform, font, "FurthestOnTrack", "START\nTRASY");
        CreateTargetingOptionButton(optGrid.transform, font, "ClosestOnTrack", "KONIEC\nTRASY");
        CreateTargetingOptionButton(optGrid.transform, font, "MostHP", "MAX\nHP");
        CreateTargetingOptionButton(optGrid.transform, font, "LeastHP", "MIN\nHP");
        CreateTargetingOptionButton(optGrid.transform, font, "HighestThreat", "ELITA\nBOSS");
        CreateTargetingOptionButton(optGrid.transform, font, "Random", "LOSOWY");
        CreateTargetingOptionButton(optGrid.transform, font, "Chunk", "PUNKT");
    }

    private static void CreatePrioritySlot(Transform parent, TMP_FontAsset font, int slotIndex, string slotContent, bool isDefaultClosest)
    {
        GameObject slot = new GameObject("Slot_" + slotIndex, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        slot.transform.SetParent(parent, false);

        LayoutElement le = slot.GetComponent<LayoutElement>();
        le.preferredWidth = 48;
        le.preferredHeight = 52;

        Image img = slot.GetComponent<Image>();
        img.sprite = null;
        img.color = isDefaultClosest ? new Color(0.18f, 0.2f, 0.24f, 0.95f) : new Color(0.11f, 0.12f, 0.15f, 0.95f);

        // Numer slotu u góry
        GameObject numObj = new GameObject("Txt_SlotNumber", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        numObj.transform.SetParent(slot.transform, false);
        RectTransform nRt = numObj.GetComponent<RectTransform>();
        nRt.anchorMin = new Vector2(0, 1);
        nRt.anchorMax = new Vector2(1, 1);
        nRt.pivot = new Vector2(0.5f, 1);
        nRt.anchoredPosition = new Vector2(0, -2);
        nRt.sizeDelta = new Vector2(0, 12);
        TextMeshProUGUI nTmp = numObj.GetComponent<TextMeshProUGUI>();
        nTmp.font = font;
        nTmp.fontSize = 10;
        nTmp.fontStyle = FontStyles.Bold;
        nTmp.text = slotIndex.ToString();
        nTmp.color = new Color(0.6f, 0.7f, 0.8f, 1f);
        nTmp.alignment = TextAlignmentOptions.Center;

        // Zawartość / Placeholder
        GameObject txtObj = new GameObject("Txt_Content", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(slot.transform, false);
        RectTransform tRt = txtObj.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.anchoredPosition = new Vector2(0, -4);
        tRt.sizeDelta = new Vector2(-4, -14);
        TextMeshProUGUI tTmp = txtObj.GetComponent<TextMeshProUGUI>();
        tTmp.font = font;
        tTmp.fontSize = 9;
        tTmp.text = slotContent;
        tTmp.color = isDefaultClosest ? new Color(0.7f, 0.75f, 0.8f, 0.75f) : new Color(0.4f, 0.4f, 0.4f, 0.8f);
        tTmp.alignment = TextAlignmentOptions.Center;
    }

    private static void CreateTargetingOptionButton(Transform parent, TMP_FontAsset font, string modeName, string btnLabel)
    {
        GameObject btnObj = new GameObject("Btn_Target_" + modeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        btnObj.transform.SetParent(parent, false);

        LayoutElement le = btnObj.GetComponent<LayoutElement>();
        le.preferredWidth = 50;
        le.preferredHeight = 50;

        Image img = btnObj.GetComponent<Image>();
        img.sprite = null;
        img.color = new Color(0.18f, 0.2f, 0.24f, 0.95f);

        GameObject txtObj = new GameObject("Txt_Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform rt = txtObj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = 10;
        tmp.fontStyle = FontStyles.Bold;
        tmp.text = btnLabel;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
    }
}
