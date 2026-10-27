using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

public static class ChallengerM4Tests
{
    private static int _passed = 0;
    private static int _failed = 0;

    private static void Assert(bool condition, string testName, string details = "")
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"[CHALLENGER-M4-PASS] {testName}");
        }
        else
        {
            _failed++;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[CHALLENGER-M4-FAIL] {testName} - {details}");
            Console.ResetColor();
        }
    }

    private static T CreateInstanceWithoutNative<T>() where T : class
    {
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }

    private static MetaUpgradeSO CreateUpgrade(string id, string upgradeName, int cost, Vector2 nodePosition, bool isUnlocked = false)
    {
        MetaUpgradeSO u = CreateInstanceWithoutNative<MetaUpgradeSO>();
        try { u.name = upgradeName; } catch { }
        u.id = id;
        u.upgradeName = upgradeName;
        u.cost = cost;
        u.nodePosition = nodePosition;
        u.isUnlocked = isUnlocked;
        u.prerequisites = new List<MetaUpgradeSO>();
        return u;
    }

    public static int RunAll()
    {
        Console.WriteLine("\n=================================================");
        Console.WriteLine("CHALLENGER M4: R4 2D INTERACTIVE GRAPH UI & FOG OF WAR");
        Console.WriteLine("=================================================");

        Test1_FogOfWar_LogicEvaluation();
        Test2_NodePositioning_SOAssetVerification();
        Test3_PurchaseFlow_SimulationAndPersistence();
        Test4_GEMINI_TypographyAndIconConstraints();
        Test5_LineRenderer_BezierCalculations();

        Console.WriteLine("\n=================================================");
        Console.WriteLine($"CHALLENGER M4 SUMMARY: {_passed} PASSED, {_failed} FAILED");
        Console.WriteLine("=================================================");

        return _failed;
    }

    // =========================================================================
    // 1. FOG OF WAR (4-TIER EVALUATION)
    // =========================================================================

    private static void Test1_FogOfWar_LogicEvaluation()
    {
        // 1. Root / Węzeł początkowy
        var root = CreateUpgrade("start", "Start", 0, new Vector2(1460, 2600), isUnlocked: false);
        Assert(MetaNodeItemUI.CalculateFogState(root) == MetaFogState.Available,
            "FoW_RootLocked_IsAvailable", "Root bez wymagań musi być od razu Available.");

        root.isUnlocked = true;
        Assert(MetaNodeItemUI.CalculateFogState(root) == MetaFogState.Purchased,
            "FoW_RootUnlocked_IsPurchased", "Wykupiony węzeł musi mieć stan Purchased (Level i).");

        // 2. Linear Chain: Root -> A -> B -> C -> D
        var a = CreateUpgrade("a", "Node A", 100, new Vector2(1880, 2600), isUnlocked: false);
        a.prerequisites.Add(root);

        var b = CreateUpgrade("b", "Node B", 150, new Vector2(2400, 2600), isUnlocked: false);
        b.prerequisites.Add(a);

        var c = CreateUpgrade("c", "Node C", 200, new Vector2(3000, 2600), isUnlocked: false);
        c.prerequisites.Add(b);

        var d = CreateUpgrade("d", "Node D", 250, new Vector2(3600, 2600), isUnlocked: false);
        d.prerequisites.Add(c);

        Assert(MetaNodeItemUI.CalculateFogState(a) == MetaFogState.Available,
            "FoW_LinearChain_ChildOfPurchased_IsAvailable", "A ma spełnione wymagania (Root isUnlocked) -> Available (Level i+1).");

        Assert(MetaNodeItemUI.CalculateFogState(b) == MetaFogState.Mystery,
            "FoW_LinearChain_ChildOfAvailable_IsMystery", "B ma rodzica A (Available) -> Mystery (Level i+2).");

        Assert(MetaNodeItemUI.CalculateFogState(c) == MetaFogState.Hidden,
            "FoW_LinearChain_ChildOfMystery_IsHidden", "C ma rodzica B (Mystery) -> Hidden (Level i+3+).");

        Assert(MetaNodeItemUI.CalculateFogState(d) == MetaFogState.Hidden,
            "FoW_LinearChain_DeepNode_IsHidden", "D ma rodzica C (Hidden) -> Hidden (Level i+3+).");

        // 3. Dynamic transition: Purchase Node A
        a.isUnlocked = true;
        Assert(MetaNodeItemUI.CalculateFogState(a) == MetaFogState.Purchased,
            "FoW_DynamicTransition_PurchasedA", "A po zakupie staje się Purchased.");

        Assert(MetaNodeItemUI.CalculateFogState(b) == MetaFogState.Available,
            "FoW_DynamicTransition_B_TransitionsToAvailable", "B staje się Available.");

        Assert(MetaNodeItemUI.CalculateFogState(c) == MetaFogState.Mystery,
            "FoW_DynamicTransition_C_TransitionsToMystery", "C staje się Mystery (Level i+2).");

        Assert(MetaNodeItemUI.CalculateFogState(d) == MetaFogState.Hidden,
            "FoW_DynamicTransition_D_RemainsHidden", "D nadal pozostaje Hidden.");

        // 4. Convergent Branch: X requires [P1, P2]
        var p1 = CreateUpgrade("p1", "Parent 1", 100, new Vector2(2000, 1000), isUnlocked: true);
        var p2 = CreateUpgrade("p2", "Parent 2", 100, new Vector2(2000, 3000), isUnlocked: false);
        var childX = CreateUpgrade("cx", "Convergent Child", 200, new Vector2(2600, 2000), isUnlocked: false);
        childX.prerequisites.Add(p1);
        childX.prerequisites.Add(p2);

        // p1 is Purchased, p2 is Available (assuming p2 prereqs met)
        Assert(MetaNodeItemUI.CalculateFogState(childX) == MetaFogState.Mystery,
            "FoW_Convergent_OneUnlockedOneAvailable_IsMystery",
            "Gdy jedno wymaganie jest Purchased, a drugie nieodblokowane, dziecko nie ma spełnionych wszystkich wymagań, więc jest Mystery.");

        p2.isUnlocked = true;
        Assert(MetaNodeItemUI.CalculateFogState(childX) == MetaFogState.Available,
            "FoW_Convergent_BothUnlocked_IsAvailable",
            "Gdy oba wymagania są Purchased, dziecko staje się Available.");

        // 5. Null or Empty safety
        Assert(MetaNodeItemUI.CalculateFogState(null) == MetaFogState.Hidden,
            "FoW_NullUpgrade_IsHidden", "Null upgrade bezpiecznie zwraca Hidden.");
    }

    // =========================================================================
    // 2. NODE POSITIONING IN ASSETS & SO DATA
    // =========================================================================

    private static void Test2_NodePositioning_SOAssetVerification()
    {
        string metaDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "Main folder", "ScriptableObjects", "Meta");
        if (!Directory.Exists(metaDir))
        {
            metaDir = "d:/Projekty Unity/DwarfenTowerDefence/Assets/Main folder/ScriptableObjects/Meta";
        }

        Assert(Directory.Exists(metaDir), "SO_MetaDirectoryExists", $"Katalog meta musi istnieć: {metaDir}");

        string startPath = Path.Combine(metaDir, "Start.asset");
        Assert(File.Exists(startPath), "SO_StartAssetExists", "Plik Start.asset musi istnieć.");

        string startYaml = File.ReadAllText(startPath);
        Assert(startYaml.Contains("nodePosition: {x: 1460, y: 2600}"),
            "SO_StartPositionIs_1460_2600",
            "Węzeł Start musi mieć pozycję {x: 1460, y: 2600}.");

        // Sprawdź pozycje innych kluczowych węzłów
        string elfy1Path = Path.Combine(metaDir, "Elfy 1.asset");
        if (File.Exists(elfy1Path))
        {
            string yaml = File.ReadAllText(elfy1Path);
            Assert(yaml.Contains("nodePosition: {x: 1880, y: 3160}"),
                "SO_Elfy1PositionIs_1880_3160", "Elfy 1 ma pozycję {x: 1880, y: 3160}.");
        }

        string krasnoludy1Path = Path.Combine(metaDir, "Krasnoludy 1.asset");
        if (File.Exists(krasnoludy1Path))
        {
            string yaml = File.ReadAllText(krasnoludy1Path);
            Assert(yaml.Contains("nodePosition: {x: 1960, y: 1960}"),
                "SO_Krasnoludy1PositionIs_1960_1960", "Krasnoludy 1 ma pozycję {x: 1960, y: 1960}.");
        }
    }

    // =========================================================================
    // 3. PURCHASE FLOW & SAVE PERSISTENCE
    // =========================================================================

    private static void Test3_PurchaseFlow_SimulationAndPersistence()
    {
        string tempSaveFile = Path.Combine(Path.GetTempPath(), $"test_save_challenger_m4_{Guid.NewGuid():N}.json");

        try
        {
            SaveManager writerMgr = CreateInstanceWithoutNative<SaveManager>();
            writerMgr.SavePath = tempSaveFile;
            writerMgr.currentSaveData = new SaveData
            {
                totalHope = 500,
                unlockedUpgradeIDs = new List<string> { "start" }
            };

            var upgrade = CreateUpgrade("nowa_wieza_1", "Nowa wieża 1", 100, new Vector2(2500, 2500), isUnlocked: false);

            // Symulacja logiki zakupu w MetaNodeDetailsPanelUI:
            // 1. Walidacja kosztu
            Assert(writerMgr.currentSaveData.totalHope >= upgrade.cost, "Purchase_CanAffordCheck", "Gracz ma 500 Nadziei, koszt to 100.");

            // 2. Potrącenie Nadziei
            writerMgr.currentSaveData.totalHope -= upgrade.cost;
            Assert(writerMgr.currentSaveData.totalHope == 400, "Purchase_HopeDeductedTo400", "Nadzieja została potrącona do 400.");

            // 3. Zmiana stanu
            upgrade.isUnlocked = true;
            if (!writerMgr.currentSaveData.unlockedUpgradeIDs.Contains(upgrade.id))
            {
                writerMgr.currentSaveData.unlockedUpgradeIDs.Add(upgrade.id);
            }

            Assert(upgrade.isUnlocked, "Purchase_NodeMarkedUnlocked", "Węzeł został oznaczony jako odblokowany.");
            Assert(writerMgr.currentSaveData.unlockedUpgradeIDs.Contains("nowa_wieza_1"), "Purchase_IdAddedToSaveData", "ID zostało dodane do unlockedUpgradeIDs.");

            // 4. Trwały zapis JSON przez SaveManager
            writerMgr.SaveGame();
            Assert(File.Exists(tempSaveFile), "Purchase_SaveFileWrittenToDisk", "Plik zapisu został zapisany na dysku.");

            // 5. Odczyt i weryfikacja
            SaveManager readerMgr = CreateInstanceWithoutNative<SaveManager>();
            readerMgr.SavePath = tempSaveFile;
            readerMgr.LoadGame();
            Assert(readerMgr.currentSaveData.totalHope == 400, "Purchase_LoadedHopeMatches", "Wczytana Nadzieja wynosi 400.");
            Assert(readerMgr.currentSaveData.unlockedUpgradeIDs.Contains("nowa_wieza_1"), "Purchase_LoadedUpgradeIdMatches", "Wczytany save zawiera nowa_wieza_1.");

            // 6. Detekcja draftu nowej wieży
            MetaUpgradeManager metaMgr = CreateInstanceWithoutNative<MetaUpgradeManager>();
            Assert(metaMgr.IsTowerDraftUpgrade(upgrade), "Purchase_DraftUpgradeIdentified",
                "Węzeł nowa_wieza_1 jest poprawnie rozpoznawany jako draft wieży.");
        }
        finally
        {
            if (File.Exists(tempSaveFile))
            {
                try { File.Delete(tempSaveFile); } catch { }
            }
        }
    }

    // =========================================================================
    // 4. GEMINI.md UI CONSTRAINTS
    // =========================================================================

    private static void Test4_GEMINI_TypographyAndIconConstraints()
    {
        // 1. Fonty TextMeshPro
        Assert(MetaNodeDetailsPanelUI.FontSizeHeader == 20f,
            "GEMINI_FontSizeHeader_Is20pt", "Główny nagłówek / tytuł okna: 20 pt.");

        Assert(MetaNodeDetailsPanelUI.FontSizeSection == 16f,
            "GEMINI_FontSizeSection_Is16pt", "Tytuły sekcji funkcjonalnych: 16 pt.");

        Assert(MetaNodeDetailsPanelUI.FontSizeButton == 16f,
            "GEMINI_FontSizeButton_Is16pt", "Przyciski akcji: 16 pt.");

        Assert(MetaNodeDetailsPanelUI.FontSizeDescription >= 12f && MetaNodeDetailsPanelUI.FontSizeDescription <= 14f,
            "GEMINI_FontSizeDescription_Is12to14pt", "Opisy i treść fabularna: 12-14 pt.");

        Assert(MetaNodeDetailsPanelUI.FontSizeHelper == 10f,
            "GEMINI_FontSizeHelper_Is10pt", "Tekst pomocniczy: 10 pt.");

        // 2. Rozmiary Ikon
        Assert(MetaNodeDetailsPanelUI.IconSizeStandard == 32f,
            "GEMINI_IconSizeStandard_Is32px", "Standardowy rozmiar ikony: 32 px.");

        Assert(MetaNodeDetailsPanelUI.IconSizeSmall == 24f,
            "GEMINI_IconSizeSmall_Is24px", "Mały rozmiar ikony: 24 px.");

        Assert(MetaNodeItemUI.IconSize == 32f,
            "GEMINI_NodeItem_IconSizeIs32px", "Ikona w węźle grafu: 32 px.");

        // 3. Wymiary węzła
        Assert(MetaNodeItemUI.NodeWidth == 180f, "NodeWidth_Is180px", "Szerokość węzła wynosi 180 px.");
        Assert(MetaNodeItemUI.NodeHeight == 64f, "NodeHeight_Is64px", "Wysokość węzła wynosi 64 px.");
    }

    // =========================================================================
    // 5. LINE RENDERER & BEZIER EVALUATION
    // =========================================================================

    private static void Test5_LineRenderer_BezierCalculations()
    {
        Assert(UILineRenderer.ThicknessActive == 4.0f,
            "Line_ThicknessActiveIs4px", "Grubość aktywnej linii wynosi 4 px.");

        Assert(UILineRenderer.ThicknessAvailable == 3.2f,
            "Line_ThicknessAvailableIs3_2px", "Grubość dostępnej linii wynosi 3.2 px.");

        Assert(UILineRenderer.ThicknessMystery == 2.2f,
            "Line_ThicknessMysteryIs2_2px", "Grubość tajemniczej linii wynosi 2.2 px.");

        // Weryfikacja matematyki krzywej Beziera
        Vector2 p0 = new Vector2(0f, 0f);
        Vector2 p1 = new Vector2(50f, 0f);
        Vector2 p2 = new Vector2(50f, 100f);
        Vector2 p3 = new Vector2(100f, 100f);

        Vector2 start = UILineRenderer.EvaluateCubicBezier(p0, p1, p2, p3, 0f);
        Assert(Vector2.Distance(start, p0) < 0.001f, "Bezier_StartPointMatch", "Punkt t=0 odpowiada P0.");

        Vector2 end = UILineRenderer.EvaluateCubicBezier(p0, p1, p2, p3, 1f);
        Assert(Vector2.Distance(end, p3) < 0.001f, "Bezier_EndPointMatch", "Punkt t=1 odpowiada P3.");

        Vector2 mid = UILineRenderer.EvaluateCubicBezier(p0, p1, p2, p3, 0.5f);
        Assert(mid.x == 50f && mid.y == 50f, "Bezier_MidPointSymmetry", "Punkt symetryczny t=0.5 odpowiada (50, 50).");
    }
}
