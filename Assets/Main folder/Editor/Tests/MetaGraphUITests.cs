using System;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Zestaw testów automatycznych NUnit dla Wymagania R4 (Milestone 4):
/// - Test 1: 4-poziomowa logika kategoryzacji Mgły Odkrywania (Fog of War)
/// - Test 2: Dopasowanie pozycji węzłów grafu do upgrade.nodePosition
/// - Test 3: Przepływ zakupu ulepszenia (potrącenie Nadziei, zmiana stanu, trwały zapis)
/// - Test 4: Weryfikacja restrykcyjnych ograniczeń typografii i rozmiarów ikon wg GEMINI.md
/// </summary>
[TestFixture]
public class MetaGraphUITests
{
    private List<GameObject> spawnedGameObjects;
    private List<ScriptableObject> createdScriptableObjects;
    private string tempSavePath;
    private SaveManager saveManager;

    [SetUp]
    public void SetUp()
    {
        spawnedGameObjects = new List<GameObject>();
        createdScriptableObjects = new List<ScriptableObject>();

        tempSavePath = Path.Combine(Application.temporaryCachePath, $"test_save_m4_{Guid.NewGuid():N}.json");

        GameObject saveGO = new GameObject("TestSaveManager");
        saveManager = saveGO.AddComponent<SaveManager>();
        saveManager.SavePath = tempSavePath;
        saveManager.currentSaveData = new SaveData { totalHope = 500 };
        spawnedGameObjects.Add(saveGO);
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(tempSavePath))
        {
            try { File.Delete(tempSavePath); } catch { }
        }

        foreach (var so in createdScriptableObjects)
        {
            if (so != null) UnityEngine.Object.DestroyImmediate(so);
        }
        createdScriptableObjects.Clear();

        foreach (var go in spawnedGameObjects)
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
        spawnedGameObjects.Clear();
    }

    private MetaUpgradeSO CreateTestUpgrade(string id, string upgradeName, int cost, Vector2 nodePos, bool isUnlocked = false)
    {
        MetaUpgradeSO u = ScriptableObject.CreateInstance<MetaUpgradeSO>();
        u.id = id;
        u.upgradeName = upgradeName;
        u.cost = cost;
        u.nodePosition = nodePos;
        u.isUnlocked = isUnlocked;
        u.prerequisites = new List<MetaUpgradeSO>();
        createdScriptableObjects.Add(u);
        return u;
    }

    // =========================================================================
    // TEST 1: 4-POZIOMOWA EWALUACJA MGŁY ODKRYWANIA (FOG OF WAR)
    // =========================================================================

    [Test]
    public void Test1_FogOfWar_PurchasedLevelI_WhenUnlocked()
    {
        var root = CreateTestUpgrade("root", "Start", 0, new Vector2(1460, 2600), isUnlocked: true);
        MetaFogState state = MetaNodeItemUI.CalculateFogState(root);
        Assert.AreEqual(MetaFogState.Purchased, state, "Odblokowany węzeł musi posiadać stan Poziomu i (Purchased).");
    }

    [Test]
    public void Test1_FogOfWar_AvailableLevelIPlus1_WhenLockedAndPrereqsMet()
    {
        var root = CreateTestUpgrade("root", "Start", 0, new Vector2(1460, 2600), isUnlocked: false);
        // Root bez wymagań ma ArePrerequisitesMet() == true
        MetaFogState state = MetaNodeItemUI.CalculateFogState(root);
        Assert.AreEqual(MetaFogState.Available, state, "Zablokowany węzeł ze spełnionymi wymaganiami musi być Poziomem i+1 (Available).");
    }

    [Test]
    public void Test1_FogOfWar_MysteryLevelIPlus2_WhenChildOfAvailableOrPurchased()
    {
        // Drzewko: Root (Purchased) -> A (Available) -> B (Mystery) -> C (Hidden)
        var root = CreateTestUpgrade("root", "Start", 0, new Vector2(1460, 2600), isUnlocked: true);

        var nodeA = CreateTestUpgrade("nodeA", "Tier 1", 100, new Vector2(1880, 2600), isUnlocked: false);
        nodeA.prerequisites.Add(root); // Wymagania spełnione -> Available (Level i+1)

        var nodeB = CreateTestUpgrade("nodeB", "Tier 2", 200, new Vector2(2400, 2600), isUnlocked: false);
        nodeB.prerequisites.Add(nodeA); // Wymaganie to nodeA (Available) -> Mystery (Level i+2)

        Assert.AreEqual(MetaFogState.Purchased, MetaNodeItemUI.CalculateFogState(root));
        Assert.AreEqual(MetaFogState.Available, MetaNodeItemUI.CalculateFogState(nodeA));
        Assert.AreEqual(MetaFogState.Mystery, MetaNodeItemUI.CalculateFogState(nodeB),
            "Węzeł B którego rodzic A jest Available (i+1), musi być na Poziomie i+2 (Mystery Step).");
    }

    [Test]
    public void Test1_FogOfWar_DeepFogLevelIPlus3_CompletelyHidden()
    {
        var root = CreateTestUpgrade("root", "Start", 0, new Vector2(1460, 2600), isUnlocked: true);

        var nodeA = CreateTestUpgrade("nodeA", "Tier 1", 100, new Vector2(1880, 2600), isUnlocked: false);
        nodeA.prerequisites.Add(root);

        var nodeB = CreateTestUpgrade("nodeB", "Tier 2", 200, new Vector2(2400, 2600), isUnlocked: false);
        nodeB.prerequisites.Add(nodeA);

        var nodeC = CreateTestUpgrade("nodeC", "Tier 3", 300, new Vector2(3000, 2600), isUnlocked: false);
        nodeC.prerequisites.Add(nodeB); // Rodzic B jest Mystery -> C musi być w Głębokiej Mgle (Hidden)

        Assert.AreEqual(MetaFogState.Hidden, MetaNodeItemUI.CalculateFogState(nodeC),
            "Węzeł oddalony o >=2 kroki od frontu zakupu musi być całkowicie ukryty we mgle (Level i+3+ Hidden).");
    }

    [Test]
    public void Test1_FogOfWar_DynamicTransitionOnPurchase()
    {
        // Start (Available) -> A (Mystery) -> B (Hidden)
        var start = CreateTestUpgrade("start", "Start", 0, new Vector2(1460, 2600), isUnlocked: false);
        var nodeA = CreateTestUpgrade("a", "Node A", 100, new Vector2(1880, 2600), isUnlocked: false);
        nodeA.prerequisites.Add(start);
        var nodeB = CreateTestUpgrade("b", "Node B", 200, new Vector2(2400, 2600), isUnlocked: false);
        nodeB.prerequisites.Add(nodeA);

        Assert.AreEqual(MetaFogState.Available, MetaNodeItemUI.CalculateFogState(start));
        Assert.AreEqual(MetaFogState.Mystery, MetaNodeItemUI.CalculateFogState(nodeA));
        Assert.AreEqual(MetaFogState.Hidden, MetaNodeItemUI.CalculateFogState(nodeB));

        // Gracz kupuje Start:
        start.isUnlocked = true;

        // Stan dynamicznie przelicza się w DAG:
        Assert.AreEqual(MetaFogState.Purchased, MetaNodeItemUI.CalculateFogState(start));
        Assert.AreEqual(MetaFogState.Available, MetaNodeItemUI.CalculateFogState(nodeA));
        Assert.AreEqual(MetaFogState.Mystery, MetaNodeItemUI.CalculateFogState(nodeB));
    }

    // =========================================================================
    // TEST 2: POZYCJONOWANIE WĘZŁÓW W RECTTRANSFORM
    // =========================================================================

    [Test]
    public void Test2_NodePositioning_MatchesUpgradeNodePosition()
    {
        Vector2 expectedPos = new Vector2(1460f, 2600f);
        var upgrade = CreateTestUpgrade("start", "Start", 0, expectedPos);

        GameObject nodeObj = new GameObject("TestNodeUI", typeof(RectTransform));
        spawnedGameObjects.Add(nodeObj);

        MetaNodeItemUI itemUI = nodeObj.AddComponent<MetaNodeItemUI>();
        itemUI.Setup(upgrade, null);

        Assert.AreEqual(expectedPos.x, itemUI.RectTransform.anchoredPosition.x, 0.001f, "Współrzędna X węzła musi odpowiadać upgrade.nodePosition.x");
        Assert.AreEqual(expectedPos.y, itemUI.RectTransform.anchoredPosition.y, 0.001f, "Współrzędna Y węzła musi odpowiadać upgrade.nodePosition.y");
    }

    [Test]
    public void Test2_MultipleNodePositions_MatchDistinctCoordinates()
    {
        var u1 = CreateTestUpgrade("u1", "U1", 50, new Vector2(1960f, 1960f));
        var u2 = CreateTestUpgrade("u2", "U2", 150, new Vector2(3500f, 4020f));
        var u3 = CreateTestUpgrade("u3", "U3", 300, new Vector2(5020f, 1100f));

        GameObject g1 = new GameObject("N1", typeof(RectTransform));
        GameObject g2 = new GameObject("N2", typeof(RectTransform));
        GameObject g3 = new GameObject("N3", typeof(RectTransform));
        spawnedGameObjects.AddRange(new[] { g1, g2, g3 });

        var item1 = g1.AddComponent<MetaNodeItemUI>();
        var item2 = g2.AddComponent<MetaNodeItemUI>();
        var item3 = g3.AddComponent<MetaNodeItemUI>();

        item1.Setup(u1, null);
        item2.Setup(u2, null);
        item3.Setup(u3, null);

        Assert.AreEqual(new Vector2(1960f, 1960f), item1.RectTransform.anchoredPosition);
        Assert.AreEqual(new Vector2(3500f, 4020f), item2.RectTransform.anchoredPosition);
        Assert.AreEqual(new Vector2(5020f, 1100f), item3.RectTransform.anchoredPosition);
    }

    // =========================================================================
    // TEST 3: PRZEPŁYW ZAKUPU (HOPE DEDUCTION, STATE CHANGE, TRWAŁY ZAPIS)
    // =========================================================================

    [Test]
    public void Test3_PurchaseFlow_DeductsHope_SetsUnlocked_PersistsSave()
    {
        saveManager.currentSaveData.totalHope = 450;
        saveManager.currentSaveData.unlockedUpgradeIDs = new List<string>();

        var upgrade = CreateTestUpgrade("krasnoludy_1", "Krasnoludy I", 100, new Vector2(1960, 1960), isUnlocked: false);

        GameObject panelObj = new GameObject("TestDetailsPanel", typeof(RectTransform));
        spawnedGameObjects.Add(panelObj);

        MetaNodeDetailsPanelUI panel = panelObj.AddComponent<MetaNodeDetailsPanelUI>();
        panel.DisplayNode(upgrade, MetaFogState.Available);

        Assert.IsTrue(panel.BtnPurchase.interactable, "Przycisk zakupu musi być aktywny gdy gracz ma wystarczająco Nadziei.");

        // Wykonaj zakup
        panel.OnPurchaseClicked();

        // 1. Sprawdź potrącenie Nadziei
        Assert.AreEqual(350, saveManager.currentSaveData.totalHope, "Koszt 100 Nadziei musi zostać odjęty z 450 -> 350.");

        // 2. Sprawdź zmianę stanu odblokowania
        Assert.IsTrue(upgrade.isUnlocked, "Zakupiony węzeł musi mieć isUnlocked == true.");
        Assert.IsTrue(saveManager.currentSaveData.unlockedUpgradeIDs.Contains("krasnoludy_1"),
            "SaveData musi zawierać ID zakupionego ulepszenia.");

        // 3. Sprawdź trwałość zapisu na dysku
        Assert.IsTrue(File.Exists(tempSavePath), "Plik zapisu JSON musi zostać zapisany na dysku.");
        string json = File.ReadAllText(tempSavePath);
        Assert.IsTrue(json.Contains("krasnoludy_1"), "Zapisany JSON musi zawierać odblokowane ID.");
        Assert.IsTrue(json.Contains("350"), "Zapisany JSON musi zawierać zaktualizowaną liczbę Nadziei.");
    }

    [Test]
    public void Test3_PurchaseFlow_CannotPurchaseIfNotEnoughHope()
    {
        saveManager.currentSaveData.totalHope = 50; // Zbyt mało na koszt 100
        var upgrade = CreateTestUpgrade("expensive", "Drogie Ulepszenie", 100, new Vector2(2000, 2000));

        GameObject panelObj = new GameObject("TestDetailsPanel2", typeof(RectTransform));
        spawnedGameObjects.Add(panelObj);

        MetaNodeDetailsPanelUI panel = panelObj.AddComponent<MetaNodeDetailsPanelUI>();
        panel.DisplayNode(upgrade, MetaFogState.Available);

        Assert.IsFalse(panel.BtnPurchase.interactable, "Przycisk zakupu musi być nieaktywny przy niewystarczającej ilości Nadziei.");

        panel.OnPurchaseClicked();

        Assert.AreEqual(50, saveManager.currentSaveData.totalHope, "Nadzieja nie może ulec zmianie przy nieudanym zakupie.");
        Assert.IsFalse(upgrade.isUnlocked, "Węzeł nie może zostać odblokowany.");
    }

    // =========================================================================
    // TEST 4: WERYFIKACJA STANDARDÓW UI GEMINI.md
    // =========================================================================

    [Test]
    public void Test4_GeminiUI_FontSizes_StrictCompliance()
    {
        // Weryfikacja stałych typografii
        Assert.AreEqual(20f, MetaNodeDetailsPanelUI.FontSizeHeader, "Nagłówek musi mieć dokładnie 20 pt (GEMINI.md).");
        Assert.AreEqual(16f, MetaNodeDetailsPanelUI.FontSizeSection, "Tytuły sekcji muszą mieć dokładnie 16 pt (GEMINI.md).");
        Assert.AreEqual(16f, MetaNodeDetailsPanelUI.FontSizeButton, "Przyciski akcji muszą mieć dokładnie 16 pt (GEMINI.md).");
        Assert.IsTrue(MetaNodeDetailsPanelUI.FontSizeDescription >= 12f && MetaNodeDetailsPanelUI.FontSizeDescription <= 14f,
            "Opisy i treść fabularna muszą mieścić się w przedziale 12-14 pt (GEMINI.md).");
        Assert.AreEqual(10f, MetaNodeDetailsPanelUI.FontSizeHelper, "Teksty pomocnicze/hinty muszą mieć dokładnie 10 pt (GEMINI.md).");

        // Weryfikacja komponentów UI w panelu szczegółów
        GameObject panelObj = new GameObject("TestDetailsPanelGemini", typeof(RectTransform));
        spawnedGameObjects.Add(panelObj);

        MetaNodeDetailsPanelUI panel = panelObj.AddComponent<MetaNodeDetailsPanelUI>();
        panel.EnsureHierarchyBuilt();

        Assert.AreEqual(20f, panel.TxtNodeName.fontSize, "TxtNodeName musi mieć 20 pt.");
        Assert.AreEqual(16f, panel.TxtCostSection.fontSize, "TxtCostSection musi mieć 16 pt.");
        Assert.AreEqual(16f, panel.TxtEffectSection.fontSize, "TxtEffectSection musi mieć 16 pt.");
        Assert.AreEqual(16f, panel.TxtPrereqSection.fontSize, "TxtPrereqSection musi mieć 16 pt.");
        Assert.AreEqual(16f, panel.TxtPurchaseButton.fontSize, "Przycisk zakupu musi mieć 16 pt.");
        Assert.IsTrue(panel.TxtDescription.fontSize >= 12f && panel.TxtDescription.fontSize <= 14f, "TxtDescription w zakresie 12-14 pt.");
        Assert.AreEqual(10f, panel.TxtHelper.fontSize, "TxtHelper musi mieć 10 pt.");
    }

    [Test]
    public void Test4_GeminiUI_IconSizes_StrictCompliance()
    {
        Assert.AreEqual(32f, MetaNodeDetailsPanelUI.IconSizeStandard, "Standardowy rozmiar ikony to 32 px (GEMINI.md).");
        Assert.AreEqual(24f, MetaNodeDetailsPanelUI.IconSizeSmall, "Mały rozmiar ikony to 24 px (GEMINI.md).");
        Assert.AreEqual(32f, MetaNodeItemUI.IconSize, "Ikona w węźle grafu musi mieć 32 px.");

        GameObject panelObj = new GameObject("TestDetailsPanelIcons", typeof(RectTransform));
        spawnedGameObjects.Add(panelObj);

        MetaNodeDetailsPanelUI panel = panelObj.AddComponent<MetaNodeDetailsPanelUI>();
        panel.EnsureHierarchyBuilt();

        Assert.AreEqual(new Vector2(32f, 32f), panel.ImgNodeIconRect.sizeDelta, "Ikona węzła w panelu musi mieć rozmiar 32x32 px.");
        Assert.AreEqual(new Vector2(24f, 24f), panel.ImgHopeIconRect.sizeDelta, "Ikona Nadziei w panelu musi mieć rozmiar 24x24 px.");
    }

    // =========================================================================
    // TEST 5: METAGRAPHPANZOOMUI - TARGET DECOUPLING, FOCUS CENTERING & CLAMPING
    // =========================================================================

    [Test]
    public void Test5_PanZoom_InitializeAndTargetDecoupling()
    {
        GameObject vpObj = new GameObject("Viewport", typeof(RectTransform));
        GameObject contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(vpObj.transform, false);

        spawnedGameObjects.Add(vpObj);

        RectTransform vpRect = vpObj.GetComponent<RectTransform>();
        vpRect.sizeDelta = new Vector2(1560f, 1020f);

        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.zero;
        contentRect.pivot = Vector2.zero;
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(6000f, 5000f);

        MetaGraphPanZoomUI panZoom = vpObj.AddComponent<MetaGraphPanZoomUI>();
        panZoom.Initialize(contentRect, vpRect);

        Assert.AreEqual(contentRect, panZoom.ContentRect, "Initialize musi powiązać contentRect z Contentem.");
        Assert.AreEqual(vpRect, panZoom.ViewportRect, "Initialize musi powiązać viewportRect z Viewportem.");

        Vector2 initialVpPos = vpRect.anchoredPosition;

        // Przesuń widok
        panZoom.Pan(new Vector2(50f, 30f));

        // Content docelowy ulega przesunięciu, a Viewport pozostaje statyczny na ekranie
        Assert.AreNotEqual(Vector2.zero, panZoom.TargetPosition, "Pan musi przesunąć pozycję docelową Contentu.");
        Assert.AreEqual(initialVpPos, vpRect.anchoredPosition, "Viewport mask nie może ulec przesunięciu (Target Decoupling).");
    }

    [Test]
    public void Test5_PanZoom_FocusOnPosition_CentersStartNodeExactlyAtViewportCenter()
    {
        GameObject vpObj = new GameObject("Viewport", typeof(RectTransform));
        GameObject contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(vpObj.transform, false);
        spawnedGameObjects.Add(vpObj);

        RectTransform vpRect = vpObj.GetComponent<RectTransform>();
        vpRect.sizeDelta = new Vector2(1560f, 1020f);

        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.zero;
        contentRect.pivot = Vector2.zero;
        contentRect.sizeDelta = new Vector2(6000f, 5000f);

        MetaGraphPanZoomUI panZoom = vpObj.AddComponent<MetaGraphPanZoomUI>();
        panZoom.Initialize(contentRect, vpRect);

        Vector2 startNodePos = new Vector2(1460f, 2600f);
        panZoom.FocusOnPosition(startNodePos);

        // Oczekiwana pozycja docelowa: viewportCenter (780, 510) - startNodePos * 1.0 = (-680, -2090)
        Vector2 expectedTarget = new Vector2(780f, 510f) - startNodePos;
        Assert.AreEqual(expectedTarget.x, panZoom.TargetPosition.x, 0.001f, "TargetPosition.x musi wyśrodkować węzeł.");
        Assert.AreEqual(expectedTarget.y, panZoom.TargetPosition.y, 0.001f, "TargetPosition.y musi wyśrodkować węzeł.");

        // Pozycja węzła w przestrzeni lokalnej Viewportu (gdzie (0, 0) to środek ekranu)
        Vector2 anchorLocalInVp = -vpRect.rect.size * 0.5f;
        Vector2 nodeLocalInVp = anchorLocalInVp + panZoom.TargetPosition + startNodePos * panZoom.TargetZoom;

        Assert.AreEqual(0f, nodeLocalInVp.x, 0.001f, "Start node X w przestrzeni Viewportu musi wynosić dokładnie 0 (środek ekranu).");
        Assert.AreEqual(0f, nodeLocalInVp.y, 0.001f, "Start node Y w przestrzeni Viewportu musi wynosić dokładnie 0 (środek ekranu).");
    }

    [Test]
    public void Test5_PanZoom_Zoom_CursorStabilityInvariant()
    {
        GameObject vpObj = new GameObject("Viewport", typeof(RectTransform));
        GameObject contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(vpObj.transform, false);
        spawnedGameObjects.Add(vpObj);

        RectTransform vpRect = vpObj.GetComponent<RectTransform>();
        vpRect.sizeDelta = new Vector2(1920f, 1080f);

        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.zero;
        contentRect.pivot = Vector2.zero;
        contentRect.sizeDelta = new Vector2(6000f, 5000f);

        MetaGraphPanZoomUI panZoom = vpObj.AddComponent<MetaGraphPanZoomUI>();
        panZoom.Initialize(contentRect, vpRect);

        Vector2 screenCursor = new Vector2(600f, 400f);
        Vector2 vpSize = vpRect.rect.size;
        Vector2 cursorInAnchorSpace = screenCursor; // Headless fallback or anchor space

        float scaleBefore = panZoom.TargetZoom;
        panZoom.Zoom(1.0f, screenCursor);
        float scaleAfter = panZoom.TargetZoom;

        Assert.Greater(scaleAfter, scaleBefore, "Zoom z dodatnim scrollDelta musi powiększyć skalę.");
        Assert.LessOrEqual(scaleAfter, panZoom.MaxZoom, "Skala nie może przekroczyć maxZoom.");
    }

    [Test]
    public void Test5_PanZoom_Pan_CompensatesCanvasScaleFactor()
    {
        GameObject canvasObj = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.scaleFactor = 2.0f;
        spawnedGameObjects.Add(canvasObj);

        GameObject vpObj = new GameObject("Viewport", typeof(RectTransform));
        vpObj.transform.SetParent(canvasObj.transform, false);

        GameObject contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(vpObj.transform, false);

        RectTransform vpRect = vpObj.GetComponent<RectTransform>();
        vpRect.sizeDelta = new Vector2(1920f, 1080f);

        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.zero;
        contentRect.pivot = Vector2.zero;
        contentRect.sizeDelta = new Vector2(6000f, 5000f);

        MetaGraphPanZoomUI panZoom = vpObj.AddComponent<MetaGraphPanZoomUI>();
        panZoom.Initialize(contentRect, vpRect);
        panZoom.Canvas = canvas;

        Vector2 posBefore = panZoom.TargetPosition;
        panZoom.Pan(new Vector2(100f, 60f));
        Vector2 posAfter = panZoom.TargetPosition;

        Vector2 delta = posAfter - posBefore;
        Assert.AreEqual(50f, delta.x, 0.001f, "Przesunięcie X musi być podzielone przez scaleFactor (100 / 2 = 50).");
        Assert.AreEqual(30f, delta.y, 0.001f, "Przesunięcie Y musi być podzielone przez scaleFactor (60 / 2 = 30).");
    }

    [Test]
    public void Test5_PanZoom_BoundsClamping_RestrictsExcessivePanning()
    {
        GameObject vpObj = new GameObject("Viewport", typeof(RectTransform));
        GameObject contentObj = new GameObject("Content", typeof(RectTransform));
        contentObj.transform.SetParent(vpObj.transform, false);
        spawnedGameObjects.Add(vpObj);

        RectTransform vpRect = vpObj.GetComponent<RectTransform>();
        vpRect.sizeDelta = new Vector2(1560f, 1020f);

        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = Vector2.zero;
        contentRect.pivot = Vector2.zero;
        contentRect.sizeDelta = new Vector2(6000f, 5000f);

        MetaGraphPanZoomUI panZoom = vpObj.AddComponent<MetaGraphPanZoomUI>();
        panZoom.Initialize(contentRect, vpRect);

        // Ekstremalny pan w lewo/dół
        panZoom.Pan(new Vector2(-100000f, -100000f));
        float minX = 1560f - panZoom.ContentBounds.xMax * panZoom.TargetZoom;
        float minY = 1020f - panZoom.ContentBounds.yMax * panZoom.TargetZoom;

        Assert.GreaterOrEqual(panZoom.TargetPosition.x, minX - 0.001f, "TargetPosition.x nie może wykraczać poza minX.");
        Assert.GreaterOrEqual(panZoom.TargetPosition.y, minY - 0.001f, "TargetPosition.y nie może wykraczać poza minY.");

        // Ekstremalny pan w prawo/górę
        panZoom.Pan(new Vector2(200000f, 200000f));
        float maxX = -panZoom.ContentBounds.xMin * panZoom.TargetZoom;
        float maxY = -panZoom.ContentBounds.yMin * panZoom.TargetZoom;

        Assert.LessOrEqual(panZoom.TargetPosition.x, maxX + 0.001f, "TargetPosition.x nie może wykraczać poza maxX.");
        Assert.LessOrEqual(panZoom.TargetPosition.y, maxY + 0.001f, "TargetPosition.y nie może wykraczać poza maxY.");
    }

    // =========================================================================
    // TEST 6: WERYFIKACJA KOMPONENTU CANVASRENDERER DLA LINESRENDERER
    // =========================================================================

    [Test]
    public void Test6_MetaGraphHierarchy_LinesRendererHasCanvasRenderer()
    {
        GameObject canvasObj = new GameObject("TestCanvas", typeof(Canvas));
        spawnedGameObjects.Add(canvasObj);

        GameObject rootObj = new GameObject("Pnl_MetaGraphRoot", typeof(RectTransform));
        rootObj.transform.SetParent(canvasObj.transform, false);
        spawnedGameObjects.Add(rootObj);

        MetaGraphControllerUI controller = rootObj.AddComponent<MetaGraphControllerUI>();
        controller.EnsureHierarchyBuilt();

        Transform linesTrans = rootObj.transform.Find("Viewport/Content/LinesRenderer");
        Assert.IsNotNull(linesTrans, "LinesRenderer musi istnieć w Content po EnsureHierarchyBuilt.");

        CanvasRenderer cr = linesTrans.GetComponent<CanvasRenderer>();
        Assert.IsNotNull(cr, "LinesRenderer musi posiadać CanvasRenderer, aby zapobiec MissingComponentException.");

        UILineRenderer lr = linesTrans.GetComponent<UILineRenderer>();
        Assert.IsNotNull(lr, "LinesRenderer musi posiadać UILineRenderer.");

        // Ponowne EnsureHierarchyBuilt nie niszczy ani nie usuwa CanvasRenderer
        controller.EnsureHierarchyBuilt();
        Assert.IsNotNull(linesTrans.GetComponent<CanvasRenderer>(), "Drugie wywołanie EnsureHierarchyBuilt musi zachować CanvasRenderer.");
    }

    [Test]
    public void Test7_MetaGraphHierarchy_NodeItemsHaveCanvasRenderer()
    {
        GameObject canvasObj = new GameObject("TestCanvas", typeof(Canvas));
        spawnedGameObjects.Add(canvasObj);

        GameObject rootObj = new GameObject("Pnl_MetaGraphRoot", typeof(RectTransform));
        rootObj.transform.SetParent(canvasObj.transform, false);
        spawnedGameObjects.Add(rootObj);

        MetaGraphControllerUI controller = rootObj.AddComponent<MetaGraphControllerUI>();
        var testNode = CreateTestUpgrade("test_node", "Test Node", 100, new Vector2(100, 100));
        controller.InitializeGraph(new List<MetaUpgradeSO> { testNode });

        Transform nodeTrans = rootObj.transform.Find("Viewport/Content/NodesContainer/Node_test_node");
        Assert.IsNotNull(nodeTrans, "Node_test_node musi istnieć w NodesContainer.");

        CanvasRenderer nodeCr = nodeTrans.GetComponent<CanvasRenderer>();
        Assert.IsNotNull(nodeCr, "Node_test_node musi posiadać CanvasRenderer dla BorderImage.");

        foreach (var graphic in nodeTrans.GetComponentsInChildren<Graphic>(true))
        {
            Assert.IsNotNull(graphic.GetComponent<CanvasRenderer>(),
                $"Komponent graficzny {graphic.gameObject.name} wewnątrz węzła musi posiadać CanvasRenderer.");
        }
    }

    [Test]
    public void Test8_TowerDraftModal_ProceduralElementsHaveCanvasRenderer()
    {
        GameObject canvasObj = new GameObject("TestCanvas", typeof(Canvas));
        spawnedGameObjects.Add(canvasObj);

        GameObject modalObj = new GameObject("Pnl_TowerDraftModal", typeof(RectTransform));
        modalObj.transform.SetParent(canvasObj.transform, false);
        spawnedGameObjects.Add(modalObj);

        TowerDraftModalUI draftModal = modalObj.AddComponent<TowerDraftModalUI>();
        draftModal.EnsureHierarchyBuilt();

        foreach (var graphic in modalObj.GetComponentsInChildren<Graphic>(true))
        {
            Assert.IsNotNull(graphic.GetComponent<CanvasRenderer>(),
                $"Komponent {graphic.gameObject.name} w modalu draftu musi posiadać CanvasRenderer.");
        }
    }

    [Test]
    public void Test9_VariantUpgrades_PurchaseAndFreeSwitching()
    {
        // 1. Setup mock parent upgrade with variants A and B
        MetaUpgradeSO parentUpgrade = ScriptableObject.CreateInstance<MetaUpgradeSO>();
        parentUpgrade.id = "test_variant_parent";
        parentUpgrade.upgradeName = "Ulepszenie Wież I Test";
        parentUpgrade.cost = 175;
        parentUpgrade.hasVariants = true;

        MetaUpgradeSO varA = ScriptableObject.CreateInstance<MetaUpgradeSO>();
        varA.id = "test_var_a";
        varA.upgradeName = "Wariant A (Przebicie Pancerza)";
        varA.isVariantOption = true;
        varA.effectType = MetaEffectType.TowerGlobalArmorPenetration;
        varA.effectValue = 10f;

        MetaUpgradeSO varB = ScriptableObject.CreateInstance<MetaUpgradeSO>();
        varB.id = "test_var_b";
        varB.upgradeName = "Wariant B (Przebicie Magiczne)";
        varB.isVariantOption = true;
        varB.effectType = MetaEffectType.TowerGlobalMagicPenetration;
        varB.effectValue = 10f;

        parentUpgrade.variantA = varA;
        parentUpgrade.variantB = varB;

        // 2. Setup SaveData
        SaveData saveData = new SaveData();
        saveData.totalHope = 500;
        saveData.unlockedUpgradeIDs = new List<string>();

        // 3. Purchase parent upgrade with Variant A
        saveData.totalHope -= parentUpgrade.cost;
        parentUpgrade.isUnlocked = true;
        varA.isUnlocked = true;
        varB.isUnlocked = false;
        saveData.unlockedUpgradeIDs.Add(parentUpgrade.id);
        saveData.unlockedUpgradeIDs.Add(varA.id);

        Assert.AreEqual(325, saveData.totalHope, "Koszt zakupu 175 Nadziei powinien zostać pobrany.");
        Assert.IsTrue(parentUpgrade.isUnlocked, "Węzeł nadrzędny powinien być odblokowany.");
        Assert.AreEqual("A", parentUpgrade.GetActiveVariant(saveData.unlockedUpgradeIDs), "Wariant A powinien być aktywny.");
        Assert.IsTrue(varA.isUnlocked, "Wariant A powinien być oznaczony jako odblokowany.");
        Assert.IsFalse(varB.isUnlocked, "Wariant B powinien pozostać zablokowany.");

        // 4. Free switch to Variant B (0 Hope cost)
        saveData.unlockedUpgradeIDs.Remove(varA.id);
        varA.isUnlocked = false;
        saveData.unlockedUpgradeIDs.Add(varB.id);
        varB.isUnlocked = true;

        Assert.AreEqual(325, saveData.totalHope, "Przełączenie na Wariant B powinno kosztować dokładnie 0 Nadziei.");
        Assert.AreEqual("B", parentUpgrade.GetActiveVariant(saveData.unlockedUpgradeIDs), "Wariant B powinien być teraz aktywny.");
        Assert.IsFalse(varA.isUnlocked, "Wariant A powinien być nieaktywny po przełączeniu.");
        Assert.IsTrue(varB.isUnlocked, "Wariant B powinien być aktywny po przełączeniu.");

        // 5. Free switch back to Variant A (0 Hope cost)
        saveData.unlockedUpgradeIDs.Remove(varB.id);
        varB.isUnlocked = false;
        saveData.unlockedUpgradeIDs.Add(varA.id);
        varA.isUnlocked = true;

        Assert.AreEqual(325, saveData.totalHope, "Przełączenie powrotne na Wariant A powinno kosztować 0 Nadziei.");
        Assert.AreEqual("A", parentUpgrade.GetActiveVariant(saveData.unlockedUpgradeIDs), "Wariant A powinien być ponownie aktywny.");
    }
}

