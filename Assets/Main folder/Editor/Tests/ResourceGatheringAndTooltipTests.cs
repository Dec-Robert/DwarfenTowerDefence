using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

[TestFixture]
public class ResourceGatheringAndTooltipTests
{
    private List<GameObject> createdObjects;
    private HexMapGenerator mapGenerator;
    private MapGenerationContext context;
    private BuildingPlacer buildingPlacer;

    [SetUp]
    public void SetUp()
    {
        createdObjects = new List<GameObject>();
        BuildingProductionRegistry.Clear();

        GameObject mapGenGo = new GameObject("TestMapGen");
        createdObjects.Add(mapGenGo);
        mapGenerator = mapGenGo.AddComponent<HexMapGenerator>();

        context = new MapGenerationContext(12345);
        var ctxField = typeof(HexMapGenerator).GetField("ctx", BindingFlags.NonPublic | BindingFlags.Instance);
        ctxField?.SetValue(mapGenerator, context);

        buildingPlacer = new BuildingPlacer(mapGenerator);
    }

    [TearDown]
    public void TearDown()
    {
        BuildingProductionRegistry.Clear();

        if (createdObjects != null)
        {
            for (int i = 0; i < createdObjects.Count; i++)
            {
                if (createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObjects[i]);
                }
            }
            createdObjects.Clear();
        }
    }

    private HexCell CreateCell(Vector2Int chunkCoord, Vector2Int localCoord, HexFeatureType feature = HexFeatureType.None, int elevation = 0)
    {
        GameObject go = new GameObject($"Cell_{chunkCoord}_{localCoord}");
        createdObjects.Add(go);

        HexCell cell = go.AddComponent<HexCell>();
        HexCellData data = new HexCellData
        {
            chunkCoord = chunkCoord,
            localCoord = localCoord,
            feature = feature,
            elevation = elevation,
            featureLevel = elevation
        };

        cell.Initialize(data);

        if (!context.worldData.ContainsKey(chunkCoord))
        {
            context.worldData[chunkCoord] = new Dictionary<Vector2Int, HexCellData>();
        }
        context.worldData[chunkCoord][localCoord] = data;

        return cell;
    }

    private BuildingData CreateEconomicBuilding(string name, List<HexFeatureType> allowed = null)
    {
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        data.buildingName = name;
        data.type = BuildingType.Economic;
        data.allowedTerrain = allowed ?? new List<HexFeatureType>();
        return data;
    }

    private BuildingData CreateMineData()
    {
        BuildingData data = ScriptableObject.CreateInstance<BuildingData>();
        data.buildingName = "Mines";
        data.type = BuildingType.Economic;
        data.allowedTerrain = new List<HexFeatureType> { HexFeatureType.Mountain };
        return data;
    }

    // =========================================================================
    // 1. REGUŁY STAWIANIA BUDYNKÓW EKONOMICZNYCH (PLACEMENT VALIDATION)
    // =========================================================================

    [Test]
    public void Test01_EconomicBuildings_BlockedOnMountain_ExceptMines()
    {
        Vector2Int chunk = Vector2Int.zero;
        HexCell mountainCell = CreateCell(chunk, new Vector2Int(0, 0), HexFeatureType.Mountain);

        BuildingData sawmill = CreateEconomicBuilding("Sawmill");
        BuildingData farm = CreateEconomicBuilding("Farm");
        BuildingData quarry = CreateEconomicBuilding("Quarry");
        BuildingData retort = CreateEconomicBuilding("Retort");
        BuildingData mines = CreateMineData();

        Assert.IsFalse(buildingPlacer.IsPlacementValid(mountainCell, sawmill), "Sawmill cannot be built on Mountain");
        Assert.IsFalse(buildingPlacer.IsPlacementValid(mountainCell, farm), "Farm cannot be built on Mountain");
        Assert.IsFalse(buildingPlacer.IsPlacementValid(mountainCell, quarry), "Quarry cannot be built on Mountain");
        Assert.IsFalse(buildingPlacer.IsPlacementValid(mountainCell, retort), "Retort cannot be built on Mountain");

        Assert.IsTrue(buildingPlacer.IsPlacementValid(mountainCell, mines), "Mines CAN and MUST be built on Mountain");
    }

    [Test]
    public void Test02_EconomicBuildings_AllowedOnAnyTerrainExceptMountain()
    {
        Vector2Int chunk = Vector2Int.zero;
        HexCell plainCell = CreateCell(chunk, new Vector2Int(0, 1), HexFeatureType.None);
        HexCell meadowCell = CreateCell(chunk, new Vector2Int(0, 2), HexFeatureType.Meadow);
        HexCell forestCell = CreateCell(chunk, new Vector2Int(0, 3), HexFeatureType.Forest);
        HexCell hillCell = CreateCell(chunk, new Vector2Int(0, 4), HexFeatureType.Hill, elevation: 2);

        BuildingData sawmill = CreateEconomicBuilding("Sawmill", new List<HexFeatureType> { HexFeatureType.Forest });
        BuildingData mines = CreateMineData();

        // Tartak może stać na dowolnym polu niebędącym górą
        Assert.IsTrue(buildingPlacer.IsPlacementValid(plainCell, sawmill), "Sawmill allowed on Plain");
        Assert.IsTrue(buildingPlacer.IsPlacementValid(meadowCell, sawmill), "Sawmill allowed on Meadow");
        Assert.IsTrue(buildingPlacer.IsPlacementValid(forestCell, sawmill), "Sawmill allowed on Forest");
        Assert.IsTrue(buildingPlacer.IsPlacementValid(hillCell, sawmill), "Sawmill allowed on Hill");

        // Kopalnia NIE może stać na tych polach
        Assert.IsFalse(buildingPlacer.IsPlacementValid(plainCell, mines), "Mines blocked on Plain");
        Assert.IsFalse(buildingPlacer.IsPlacementValid(meadowCell, mines), "Mines blocked on Meadow");
        Assert.IsFalse(buildingPlacer.IsPlacementValid(forestCell, mines), "Mines blocked on Forest");
        Assert.IsFalse(buildingPlacer.IsPlacementValid(hillCell, mines), "Mines blocked on Hill");
    }

    // =========================================================================
    // 2. KAMIENIOŁOM (QUARRY) I BONUSY WYSOKOŚCIOWE
    // =========================================================================

    [Test]
    public void Test03_Quarry_ElevationBonus_CalculatesCorrectly()
    {
        Vector2Int chunk = Vector2Int.zero;
        HexCell quarryCell = CreateCell(chunk, new Vector2Int(0, 0), HexFeatureType.Hill, elevation: 2);
        HexCell neighborHill1 = CreateCell(chunk, new Vector2Int(1, 0), HexFeatureType.Hill, elevation: 1);
        HexCell neighborHill2 = CreateCell(chunk, new Vector2Int(0, 1), HexFeatureType.Hill, elevation: 3);

        BuildingData quarryData = ScriptableObject.CreateInstance<BuildingData>();
        quarryData.buildingName = "Quarry";
        quarryData.type = BuildingType.Economic;
        quarryData.productionPerCycle = new List<ResourceCost>
        {
            new ResourceCost { type = ResourceType.Stone, amount = 8 }
        };
        quarryData.elevationBonus = new ElevationBonusRule
        {
            enabled = true,
            resourceType = ResourceType.Stone,
            onTopBonusPerLevel = 1.0f,
            adjacentBonusPerLevel = 0.5f,
            penaltyPerExtraBuilding = 3.0f,
            searchRadius = 1
        };

        // Tworzymy instancję budynku na heksie quarryCell
        GameObject bGo = new GameObject("QuarryEntity");
        createdObjects.Add(bGo);
        bGo.transform.parent = quarryCell.transform;
        BuildingEntity entity = bGo.AddComponent<BuildingEntity>();
        quarryCell.SetBuilding(entity);

        BuildingTerrainComponent terrain = new BuildingTerrainComponent(quarryData, bGo.transform, null);

        // Rejestracja w BuildingProductionRegistry:
        BuildingProductionRegistry.RegisterUsage(quarryCell);
        BuildingProductionRegistry.RegisterUsage(neighborHill1);
        BuildingProductionRegistry.RegisterUsage(neighborHill2);

        var regField = typeof(BuildingTerrainComponent).GetField("registeredOwnCell", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        regField?.SetValue(terrain, quarryCell);

        var adjField = typeof(BuildingTerrainComponent).GetField("adjacentElevationHexes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var adjSet = adjField?.GetValue(terrain) as HashSet<HexCell>;
        adjSet?.Add(neighborHill1);
        adjSet?.Add(neighborHill2);

        var bonuses = terrain.CalculateTerrainBonuses();

        Assert.IsTrue(bonuses.ContainsKey(ResourceType.Stone));
        // Oczekiwany bonus: 2.0 (on-top) + 0.5 (sąsiad1) + 1.5 (sąsiad2) = 4.0
        Assert.AreEqual(4.0f, bonuses[ResourceType.Stone], 0.001f);
    }

    [Test]
    public void Test04_Quarry_ElevationContention_PenalizesMultipleQuarries()
    {
        Vector2Int chunk = Vector2Int.zero;
        HexCell sharedHill = CreateCell(chunk, new Vector2Int(1, 0), HexFeatureType.Hill, elevation: 4);

        BuildingData quarryData = ScriptableObject.CreateInstance<BuildingData>();
        quarryData.elevationBonus = new ElevationBonusRule
        {
            enabled = true,
            resourceType = ResourceType.Stone,
            onTopBonusPerLevel = 1.0f,
            adjacentBonusPerLevel = 0.5f,
            penaltyPerExtraBuilding = 3.0f,
            searchRadius = 1
        };

        // Gdy z tego wzniesienia ciągną 2 kamieniołomy:
        // poziom efektywny = max(0, 4 - 3 * (2 - 1)) = max(0, 1) = 1
        BuildingProductionRegistry.RegisterUsage(sharedHill);
        BuildingProductionRegistry.RegisterUsage(sharedHill);
        Assert.AreEqual(2, BuildingProductionRegistry.GetUsageCount(sharedHill));

        GameObject bGo = new GameObject("QuarryEntity");
        createdObjects.Add(bGo);
        BuildingTerrainComponent terrain = new BuildingTerrainComponent(quarryData, bGo.transform, null);

        var adjField = typeof(BuildingTerrainComponent).GetField("adjacentElevationHexes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var adjSet = adjField?.GetValue(terrain) as HashSet<HexCell>;
        adjSet?.Add(sharedHill);

        var bonuses = terrain.GetAdjacentBonuses();
        // 1 * 0.5 = 0.5
        Assert.AreEqual(0.5f, bonuses[ResourceType.Stone], 0.001f);

        // Gdy ciągną 3 kamieniołomy:
        // poziom efektywny = max(0, 4 - 3 * (3 - 1)) = max(0, -2) = 0
        BuildingProductionRegistry.RegisterUsage(sharedHill);
        Assert.AreEqual(3, BuildingProductionRegistry.GetUsageCount(sharedHill));

        bonuses = terrain.GetAdjacentBonuses();
        Assert.IsFalse(bonuses.ContainsKey(ResourceType.Stone), "Level reduced to 0, no bonus generated");
    }

    // =========================================================================
    // 3. FORMATOWANIE TOOLTIPA (UI TOOLTIP BONUS LINES)
    // =========================================================================

    [Test]
    public void Test05_TooltipFormatting_ShowsRequired3Lines_ForProductionBuildings()
    {
        GameObject tooltipGo = new GameObject("TooltipRoot");
        createdObjects.Add(tooltipGo);

        Tooltip_BuildingExtraInfo tooltip = tooltipGo.AddComponent<Tooltip_BuildingExtraInfo>();

        GameObject pnlPlacing = new GameObject("Txt_BonusForPlacing");
        pnlPlacing.transform.parent = tooltipGo.transform;
        TextMeshProUGUI txtPlacing = pnlPlacing.AddComponent<TextMeshProUGUI>();

        GameObject pnlSurround = new GameObject("Txt_BonusForSurround");
        pnlSurround.transform.parent = tooltipGo.transform;
        TextMeshProUGUI txtSurround = pnlSurround.AddComponent<TextMeshProUGUI>();

        var fPlacing = typeof(Tooltip_BuildingExtraInfo).GetField("txtBonusForPlacing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var fSurround = typeof(Tooltip_BuildingExtraInfo).GetField("txtBonusForSurround", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        fPlacing?.SetValue(tooltip, txtPlacing);
        fSurround?.SetValue(tooltip, txtSurround);

        // Test dla Tartaku (Sawmill)
        BuildingData sawmill = CreateEconomicBuilding("Tartak");
        sawmill.bonusRules = new List<TerrainBonusRule>
        {
            new TerrainBonusRule
            {
                requiredFeature = HexFeatureType.Forest,
                searchRadius = 1,
                resourceYields = new List<TerrainResourceYield>
                {
                    new TerrainResourceYield
                    {
                        resourceType = ResourceType.Wood,
                        onTopProductionBonus = 4f,
                        baseBonusPerHex = 2f
                    }
                }
            }
        };

        var method = typeof(Tooltip_BuildingExtraInfo).GetMethod("SetupProductionSection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(tooltip, new object[] { sawmill });

        Assert.IsTrue(txtPlacing.text.Contains("Bonus do postawienia na terenie: LAS"), "Contains terrain line");
        Assert.IsTrue(txtPlacing.text.Contains("Bonus za postawienie na danym surowcu: +4 Drewno"), "Contains on-top line");
        Assert.IsTrue(txtSurround.text.Contains("Bonus za każdy surowiec wokół: +2 Drewno / pole"), "Contains surround line");

        // Test dla Kamieniołomu (Quarry)
        BuildingData quarry = CreateEconomicBuilding("Quarry");
        quarry.elevationBonus = new ElevationBonusRule
        {
            enabled = true,
            resourceType = ResourceType.Stone,
            onTopBonusPerLevel = 1.0f,
            adjacentBonusPerLevel = 0.5f,
            penaltyPerExtraBuilding = 3.0f,
            searchRadius = 1
        };

        method?.Invoke(tooltip, new object[] { quarry });

        Assert.IsTrue(txtPlacing.text.Contains("Bonus do postawienia na terenie: WZGÓRZA"), "Quarry lists WZGÓRZA");
        Assert.IsTrue(txtPlacing.text.Contains("Bonus za postawienie na danym surowcu: +1 Kamień / poziom wzgórza"), "Quarry on-top line");
        Assert.IsTrue(txtSurround.text.Contains("Bonus za każdy surowiec wokół: +0.5 Kamień / poziom wokół") || txtSurround.text.Contains("Bonus za każdy surowiec wokół: +0,5 Kamień / poziom wokół"), "Quarry surround line");

        // Test dla Retorty (Brak bonusów)
        BuildingData retort = CreateEconomicBuilding("Retort");
        retort.bonusRules = new List<TerrainBonusRule>();
        retort.elevationBonus = new ElevationBonusRule { enabled = false };

        method?.Invoke(tooltip, new object[] { retort });

        Assert.AreEqual("Brak bonusów terenowych", txtPlacing.text);
        Assert.AreEqual("", txtSurround.text);
    }

    // =========================================================================
    // AUTOMATIC RUNNER FOR EDITMODE / LOG VERIFICATION
    // =========================================================================

    [MenuItem("Tools/Run Resource Gathering and Tooltip Tests")]
    [InitializeOnLoadMethod]
    public static void RunTestsOnDomainReload()
    {
        RunAllTestsEmpirically(out _, out _, out _);
    }

    public static bool RunAllTestsEmpirically(out int passed, out int failed, out List<string> errors)
    {
        passed = 0;
        failed = 0;
        errors = new List<string>();

        ResourceGatheringAndTooltipTests suite = new ResourceGatheringAndTooltipTests();
        MethodInfo[] methods = typeof(ResourceGatheringAndTooltipTests).GetMethods(BindingFlags.Public | BindingFlags.Instance);

        Debug.Log("<color=cyan>[ResourceGatheringAndTooltipTests] Rozpoczynanie weryfikacji empirycznej testów NUnit...</color>");

        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo m = methods[i];
            if (!m.IsDefined(typeof(TestAttribute), false)) continue;

            try
            {
                suite.SetUp();
                m.Invoke(suite, null);
                suite.TearDown();
                passed++;
                Debug.Log($"<color=green>[PASS]</color> {m.Name}");
            }
            catch (Exception ex)
            {
                failed++;
                string err = $"{m.Name}: {ex.InnerException?.Message ?? ex.Message}";
                errors.Add(err);
                Debug.LogError($"<color=red>[FAIL]</color> {err}");
                try { suite.TearDown(); } catch { }
            }
        }

        Debug.Log($"<color=yellow>[ResourceGatheringAndTooltipTests] Zakończono: {passed} PASSED, {failed} FAILED z {passed + failed} testów.</color>");
        return failed == 0;
    }
}
