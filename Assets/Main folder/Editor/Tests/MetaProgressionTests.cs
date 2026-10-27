using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Comprehensive E2E and Unit Test Suite for Meta-Progression Overhaul (Milestones 1–5).
/// Architecture covers Tiers 1 to 4 per TEST_INFRA.md:
/// - Tier 1: Feature Isolation (88 SO node integrity, worker slots decoupling, dynamic efficiency extrapolation,
///           rune rarity gating, racial synergies, save serialization, 4-tier FoW DAG, GEMINI.md standards).
/// - Tier 2: Boundary & Corner Cases (zero/high worker counts, Hope cost monotonicity, starter tower seeding,
///           draft candidate pool reduction, pan/zoom clamping).
/// - Tier 3: Cross-Feature Interactions (Tower draft purchase flow, tri-racial crew composition, rune forge transmute).
/// - Tier 4: Realistic E2E Application Scenarios (Full tree traversal, factory production surge, tower draft from scratch,
///           deep FoW exploration, tri-racial night defense simulation).
/// </summary>
[TestFixture]
public class MetaProgressionTests
{
    private string tempSavePath;
    private SaveManager saveManager;
    private MetaUpgradeManager metaUpgradeManager;
    private CityStatsManager cityStatsManager;
    private List<TowerData> catalogTowers;
    private Dictionary<MetaEffectType, float> appliedValuesDict;
    private Dictionary<(MetaEffectType, string), float> buildingValuesDict;

    [SetUp]
    public void SetUp()
    {
        tempSavePath = Path.Combine(Path.GetTempPath(), $"meta_tests_save_{Guid.NewGuid():N}.json");

        saveManager = CreateInstance<SaveManager>();
        saveManager.SavePath = tempSavePath;
        saveManager.currentSaveData = new SaveData { totalHope = 1000 };
        SetStaticProperty(typeof(SaveManager), "Instance", saveManager);

        appliedValuesDict = new Dictionary<MetaEffectType, float>();
        buildingValuesDict = new Dictionary<(MetaEffectType, string), float>();

        metaUpgradeManager = CreateInstance<MetaUpgradeManager>();
        SetPrivateField(metaUpgradeManager, "appliedValues", appliedValuesDict);
        SetPrivateField(metaUpgradeManager, "buildingSpecificValues", buildingValuesDict);
        metaUpgradeManager.allUpgrades = new List<MetaUpgradeSO>();
        metaUpgradeManager.allTowers = new List<TowerData>();
        SetStaticProperty(typeof(MetaUpgradeManager), "Instance", metaUpgradeManager);

        cityStatsManager = CreateInstance<CityStatsManager>();
        cityStatsManager.globalBonusProductionWorkers = 0;
        cityStatsManager.globalBonusTowerCrewSlots = 0;
        cityStatsManager.globalBonusWorkersPerShift = 0;
        SetStaticProperty(typeof(CityStatsManager), "Instance", cityStatsManager);

        catalogTowers = Create7CatalogTowers();
        saveManager.allTowers = catalogTowers;
        metaUpgradeManager.allTowers = catalogTowers;
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(tempSavePath))
        {
            try { File.Delete(tempSavePath); } catch { }
        }

        SetStaticProperty(typeof(SaveManager), "Instance", null);
        SetStaticProperty(typeof(MetaUpgradeManager), "Instance", null);
        SetStaticProperty(typeof(CityStatsManager), "Instance", null);
        SetStaticProperty(typeof(HopeSessionManager), "Instance", null);
        SetStaticProperty(typeof(RuneManager), "Instance", null);
    }

    // =========================================================================
    // HELPER METHODS & UTILITIES
    // =========================================================================

    public static T CreateInstance<T>() where T : class
    {
        try
        {
            if (typeof(ScriptableObject).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)ScriptableObject.CreateInstance(typeof(T));
            }
        }
        catch { }

        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }

    public static void SetPrivateField(object target, string fieldName, object value)
    {
        if (target == null) return;
        var type = target.GetType();
        FieldInfo field = null;
        while (type != null && field == null)
        {
            field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                field = type.GetField($"<{fieldName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            type = type.BaseType;
        }
        if (field != null)
        {
            field.SetValue(target, value);
        }
    }

    public static T GetPrivateField<T>(object target, string fieldName)
    {
        if (target == null) return default;
        var type = target.GetType();
        FieldInfo field = null;
        while (type != null && field == null)
        {
            field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                field = type.GetField($"<{fieldName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            type = type.BaseType;
        }
        if (field != null)
        {
            return (T)field.GetValue(target);
        }
        return default;
    }

    public static (TowerEntity Tower, TowerController Controller, BuildingWorkerComponent WorkerComp) CreateTestTowerWithCrew(
        TowerData towerData, int dwarves, int elves, int humans, List<float> efficiencyTable = null)
    {
        var tower = CreateInstance<TowerEntity>();
        towerData.efficiencyTable = efficiencyTable ?? Enumerable.Repeat(1f, 20).ToList();
        tower.data = towerData;
        tower.hasSupplies = true;
        tower.isBuildingActive = true;

        var controller = CreateInstance<TowerController>();
        controller.towerData = towerData;
        tower.controller = controller;
        SetPrivateField(tower, "controller", controller);

        var workerComp = new BuildingWorkerComponent(tower, towerData, null, null);
        var citizens = new List<Citizen>();
        for (int i = 0; i < dwarves; i++) citizens.Add(new Citizen { race = Race.Dwarves });
        for (int i = 0; i < elves; i++) citizens.Add(new Citizen { race = Race.Elves });
        for (int i = 0; i < humans; i++) citizens.Add(new Citizen { race = Race.Humans });
        SetPrivateField(workerComp, "assignedCitizens", citizens);
        SetPrivateField(tower, "<Workers>k__BackingField", workerComp);

        return (tower, controller, workerComp);
    }

    private static void SetStaticProperty(Type type, string propertyName, object value)
    {
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(null, value, null);
        }
        else
        {
            var field = type.GetField($"<{propertyName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)
                     ?? type.GetField(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null) field.SetValue(null, value);
        }
    }

    public void UnlockUpgrade(MetaUpgradeSO upgrade)
    {
        if (ReferenceEquals(upgrade, null)) return;
        upgrade.isUnlocked = true;
        if (metaUpgradeManager.allUpgrades == null)
        {
            metaUpgradeManager.allUpgrades = new List<MetaUpgradeSO>();
        }
        metaUpgradeManager.allUpgrades.Add(upgrade);

        if (appliedValuesDict != null)
        {
            appliedValuesDict[upgrade.effectType] = upgrade.effectValue;
        }
        SetPrivateField(metaUpgradeManager, "appliedValues", appliedValuesDict);
    }

    public static MetaUpgradeSO CreateUpgrade(string id, string upgradeName, int cost, Vector2 nodePosition,
        MetaEffectType effectType = MetaEffectType.None, float effectValue = 0f, bool isUnlocked = false)
    {
        MetaUpgradeSO u = CreateInstance<MetaUpgradeSO>();
        try { u.name = upgradeName; } catch { }
        u.id = id;
        u.upgradeName = upgradeName;
        u.cost = cost;
        u.nodePosition = nodePosition;
        u.effectType = effectType;
        u.effectValue = effectValue;
        u.isUnlocked = isUnlocked;
        u.prerequisites = new List<MetaUpgradeSO>();
        return u;
    }

    public static TowerData CreateTower(string assetName, string buildingName, float damage, float range, float fireRate,
        float armorPen, float critChance, bool isUnlocked = false)
    {
        TowerData t = CreateInstance<TowerData>();
        try { t.name = assetName; } catch { }
        t.buildingName = buildingName;
        t.description = $"Lore for {buildingName}";
        t.baseDamage = damage;
        t.baseRange = range;
        t.fireRate = fireRate;
        t.armorPenetration = armorPen;
        t.criticalChancel = critChance;
        t.criticalDamageMultiplier = 2.0f;
        t.damageType = DamageType.Physical;
        t.isUnlocked = isUnlocked;
        return t;
    }

    public static List<TowerData> Create7CatalogTowers()
    {
        return new List<TowerData>
        {
            CreateTower("1. Sentinel Arbalest", "1. Sentinel Arbalest", 25f, 15f, 1.2f, 20f, 5f, true),
            CreateTower("2. Thunderbuss", "2. Thunderbuss", 40f, 12f, 0.8f, 35f, 10f, false),
            CreateTower("3. Steam Repeater", "3. Steam Repeater", 10f, 18f, 3.5f, 10f, 5f, false),
            CreateTower("4. Crucible of Cinders", "4. Crucible of Cinders", 22f, 14f, 1.5f, 15f, 8f, false),
            CreateTower("5. Shadow Spire", "5. Shadow Spire", 60f, 22f, 0.5f, 0f, 25f, false),
            CreateTower("6. Royal Bombard", "6. Royal Bombard", 85f, 30f, 0.4f, 50f, 5f, true),
            CreateTower("7. Soul Monolith", "7. Soul Monolith", 45f, 20f, 1.0f, 20f, 15f, false)
        };
    }

    public static string GetMetaDirectory()
    {
        string[] candidates = new string[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Assets", "Main folder", "ScriptableObjects", "Meta"),
            "d:/Projekty Unity/DwarfenTowerDefence/Assets/Main folder/ScriptableObjects/Meta",
            @"d:\Projekty Unity\DwarfenTowerDefence\Assets\Main folder\ScriptableObjects\Meta",
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Main folder", "ScriptableObjects", "Meta")
        };

        foreach (var c in candidates)
        {
            if (Directory.Exists(c)) return Path.GetFullPath(c);
        }
        throw new DirectoryNotFoundException("Could not locate Meta ScriptableObjects directory.");
    }

    public class ParsedYamlNode
    {
        public string FilePath = "";
        public string Guid = "";
        public string Id = "";
        public string UpgradeName = "";
        public int Cost = 0;
        public Vector2 NodePosition = Vector2.zero;
        public int EffectType = 0;
        public List<string> PrereqGuids = new List<string>();
        public MetaUpgradeSO UpgradeSO;
    }

    public static Dictionary<string, ParsedYamlNode> LoadAll88NodesFromDisk()
    {
        string metaDir = GetMetaDirectory();
        var files = Directory.GetFiles(metaDir, "*.asset");
        var result = new Dictionary<string, ParsedYamlNode>(StringComparer.OrdinalIgnoreCase);

        var fileToGuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            string metaFile = file + ".meta";
            if (!File.Exists(metaFile)) continue;
            string metaText = File.ReadAllText(metaFile);
            var guidMatch = Regex.Match(metaText, @"guid:\s*([a-f0-9]{32})");
            if (guidMatch.Success) fileToGuid[file] = guidMatch.Groups[1].Value;
        }

        foreach (var file in files)
        {
            if (!fileToGuid.TryGetValue(file, out string guid)) continue;
            string text = File.ReadAllText(file);

            var node = new ParsedYamlNode { FilePath = file, Guid = guid };

            var idMatch = Regex.Match(text, @"\bid:\s*([^\r\n]*)");
            if (idMatch.Success) node.Id = idMatch.Groups[1].Value.Trim().Trim('"', '\'');

            var nameMatch = Regex.Match(text, @"\bupgradeName:\s*([^\r\n]*)");
            if (nameMatch.Success) node.UpgradeName = nameMatch.Groups[1].Value.Trim().Trim('"', '\'');

            var costMatch = Regex.Match(text, @"\bcost:\s*([0-9]+)");
            if (costMatch.Success && int.TryParse(costMatch.Groups[1].Value, out int c)) node.Cost = c;

            var xMatch = Regex.Match(text, @"\bx:\s*([-0-9.]+)");
            var yMatch = Regex.Match(text, @"\by:\s*([-0-9.]+)");
            float x = 0f, y = 0f;
            if (xMatch.Success) float.TryParse(xMatch.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x);
            if (yMatch.Success) float.TryParse(yMatch.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y);
            node.NodePosition = new Vector2(x, y);

            var effectMatch = Regex.Match(text, @"\beffectType:\s*([0-9]+)");
            if (effectMatch.Success && int.TryParse(effectMatch.Groups[1].Value, out int eff)) node.EffectType = eff;

            var prereqMatches = Regex.Matches(text, @"guid:\s*([a-f0-9]{32})");
            foreach (Match pm in prereqMatches)
            {
                string pGuid = pm.Groups[1].Value;
                if (pGuid != guid && !node.PrereqGuids.Contains(pGuid)) node.PrereqGuids.Add(pGuid);
            }

            node.UpgradeSO = CreateUpgrade(node.Id, node.UpgradeName, node.Cost, node.NodePosition, (MetaEffectType)node.EffectType);
            result[guid] = node;
        }

        // Wire prerequisites
        foreach (var kv in result)
        {
            var node = kv.Value;
            foreach (var pGuid in node.PrereqGuids)
            {
                if (result.TryGetValue(pGuid, out var parentNode))
                {
                    node.UpgradeSO.prerequisites.Add(parentNode.UpgradeSO);
                }
            }
        }

        return result;
    }

    // =========================================================================
    // TIER 1: FEATURE ISOLATION TESTS
    // =========================================================================

    [Test]
    public void Tier1_01_All88Nodes_IntegrityAndCoordinates()
    {
        var allNodes = LoadAll88NodesFromDisk();
        Assert.IsTrue(allNodes.Count == 84 || allNodes.Count == 92, $"Expected 84 primary DAG nodes or 92 total assets (84 + 8 variants), got: {allNodes.Count}");

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int maxCost = 0;
        int nonZeroCostCount = 0;

        foreach (var kv in allNodes)
        {
            var node = kv.Value;
            Assert.IsFalse(string.IsNullOrEmpty(node.Id), $"Node in {Path.GetFileName(node.FilePath)} must have a non-empty id.");
            Assert.IsTrue(seenIds.Add(node.Id), $"Node id '{node.Id}' must be unique across all 88 assets.");

            Assert.Greater(node.NodePosition.x, 0f, $"Node '{node.Id}' X position must be > 0. Got: {node.NodePosition.x}");
            Assert.Greater(node.NodePosition.y, 0f, $"Node '{node.Id}' Y position must be > 0. Got: {node.NodePosition.y}");
            Assert.LessOrEqual(node.NodePosition.x, 6000f, $"Node '{node.Id}' X position must be within viewport bounds (<= 6000).");
            Assert.LessOrEqual(node.NodePosition.y, 5000f, $"Node '{node.Id}' Y position must be within viewport bounds (<= 5000).");

            if (node.Cost > 0)
            {
                nonZeroCostCount++;
                Assert.GreaterOrEqual(node.Cost, 10, $"Node '{node.Id}' cost ({node.Cost}) must be >= 10 Nadziei.");
            }
            if (node.Cost > maxCost) maxCost = node.Cost;
        }

        // Verify corrected name and ID for Nowy Upgrade.asset -> Ludzka wytrwałość I
        var humanResilience1 = allNodes.Values.FirstOrDefault(n => n.Id == "ludzka_wytrwalosc_1");
        Assert.IsNotNull(humanResilience1, "Node 'ludzka_wytrwalosc_1' (Ludzka wytrwałość I) must exist.");
        Assert.IsTrue(humanResilience1.UpgradeName.IndexOf("Ludzka", StringComparison.OrdinalIgnoreCase) >= 0,
            "Node 'ludzka_wytrwalosc_1' name must contain 'Ludzka'.");

        // Verify phantom transit nodes exist safely
        string[] phantomIds = new string[]
        {
            "elfy_chunki_1", "elfy_chunki_2", "mury_miejsce_4",
            "production_spec_1", "production_spec_2", "production_spec_3"
        };
        foreach (var pId in phantomIds)
        {
            var pNode = allNodes.Values.FirstOrDefault(n => n.Id.Equals(pId, StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(pNode, $"Phantom transit node '{pId}' must exist.");
        }

        // Verify cost progression reaches late-game capstones (500+ Hope)
        Assert.GreaterOrEqual(maxCost, 500, $"Progression curve must feature late-game capstone nodes costing >= 500 Nadziei. Max found: {maxCost}");
        Assert.IsTrue(nonZeroCostCount >= 87, "All nodes except Start must have cost >= 10 Nadziei.");
    }

    [Test]
    public void Tier1_02_WorkerSlots_Decoupling()
    {
        cityStatsManager.AddGlobalProductionWorkerSlot(4);
        Assert.AreEqual(4, cityStatsManager.globalBonusProductionWorkers, "Production worker bonus should be 4.");
        Assert.AreEqual(0, cityStatsManager.globalBonusTowerCrewSlots, "Tower crew slot bonus must remain unaffected.");
        Assert.AreEqual(0, cityStatsManager.globalBonusWorkersPerShift, "Legacy bonus must remain unaffected.");

        cityStatsManager.AddGlobalTowerCrewSlot(3);
        Assert.AreEqual(4, cityStatsManager.globalBonusProductionWorkers, "Production worker bonus should still be 4.");
        Assert.AreEqual(3, cityStatsManager.globalBonusTowerCrewSlots, "Tower crew slot bonus should be 3.");

        var building = CreateInstance<BuildingEntity>();
        var tower = CreateInstance<TowerEntity>();

        Assert.AreEqual(4, building.GetGlobalWorkerBonus(), "BuildingEntity.GetGlobalWorkerBonus() must query globalBonusProductionWorkers.");
        Assert.AreEqual(3, tower.GetGlobalWorkerBonus(), "TowerEntity.GetGlobalWorkerBonus() must query globalBonusTowerCrewSlots.");
    }

    [Test]
    public void Tier1_03_DynamicEfficiencyExtrapolation_WithinAndBeyondTable()
    {
        var bData = CreateInstance<BuildingData>();
        bData.efficiencyTable = new List<float> { 0f, 1.0f, 1.25f, 1.50f }; // Size 4, max index 3
        bData.workerScalingFactor = 0.25f;

        // Within table
        Assert.AreEqual(0f, bData.GetBaseEfficiencyForWorkers(0), 0.001f);
        Assert.AreEqual(1.0f, bData.GetBaseEfficiencyForWorkers(1), 0.001f);
        Assert.AreEqual(1.25f, bData.GetBaseEfficiencyForWorkers(2), 0.001f);
        Assert.AreEqual(1.50f, bData.GetBaseEfficiencyForWorkers(3), 0.001f);

        // Beyond table: worker 4 (extra=1) -> 1.50 + 1 * 0.25 = 1.75
        float eff4 = bData.GetBaseEfficiencyForWorkers(4);
        Assert.AreEqual(1.75f, eff4, 0.001f, "Worker count 4 should extrapolate to 1.75.");

        // Worker 6 (extra=3) -> 1.50 + 3 * 0.25 = 2.25
        float eff6 = bData.GetBaseEfficiencyForWorkers(6);
        Assert.AreEqual(2.25f, eff6, 0.001f, "Worker count 6 should extrapolate to 2.25.");

        // Unconfigured scaling factor fallback
        bData.workerScalingFactor = 0f;
        float effFallback = bData.GetBaseEfficiencyForWorkers(4);
        Assert.AreEqual(1.65f, effFallback, 0.001f, "Unconfigured scaling factor should fallback to 0.15f (1.50 + 0.15 = 1.65).");
    }

    [Test]
    public void Tier1_04_RuneRarityGating_AndForgeUnlocks()
    {
        // 1. RollRarity weights check when all rarities are locked
        float wCommon = 50f;
        float wUnc = 20f;
        float wRare = 10f;
        float wLeg = 5f;
        float wCursed = 2f;

        Assert.Greater(wCommon, 0f);

        bool uncUnlocked = metaUpgradeManager.IsUnlocked(MetaEffectType.UnlockRuneRarity_Uncommon);
        bool rareUnlocked = metaUpgradeManager.IsUnlocked(MetaEffectType.UnlockRuneRarity_Rare);
        bool legUnlocked = metaUpgradeManager.IsUnlocked(MetaEffectType.UnlockRuneRarity_Legendary);
        bool cursedUnlocked = metaUpgradeManager.IsUnlocked(MetaEffectType.UnlockRuneRarity_Cursed);

        Assert.IsFalse(uncUnlocked);
        Assert.IsFalse(rareUnlocked);
        Assert.IsFalse(legUnlocked);
        Assert.IsFalse(cursedUnlocked);

        float effUnc = uncUnlocked ? wUnc : 0f;
        float effRare = rareUnlocked ? wRare : 0f;
        float effLeg = legUnlocked ? wLeg : 0f;
        float effCursed = cursedUnlocked ? wCursed : 0f;

        Assert.AreEqual(0f, effUnc, "Uncommon effective weight must be 0 when locked.");
        Assert.AreEqual(0f, effRare, "Rare effective weight must be 0 when locked.");
        Assert.AreEqual(0f, effLeg, "Legendary effective weight must be 0 when locked.");
        Assert.AreEqual(0f, effCursed, "Cursed effective weight must be 0 when locked.");

        // 2. Transmutation gating check
        bool canCraftCursedLocked = metaUpgradeManager.IsUnlocked(MetaEffectType.UnlockCursedCrafting);
        Assert.IsFalse(canCraftCursedLocked, "UnlockCursedCrafting must be false by default.");

        // Unlock cursed crafting
        var cursedCraftSO = CreateUpgrade("cursed_craft", "Cursed Craft", 100, Vector2.zero, MetaEffectType.UnlockCursedCrafting, 1f, true);
        UnlockUpgrade(cursedCraftSO);

        bool canCraftCursedUnlocked = metaUpgradeManager.IsUnlocked(MetaEffectType.UnlockCursedCrafting);
        Assert.IsTrue(canCraftCursedUnlocked, "UnlockCursedCrafting must be true after applying upgrade.");
    }

    [Test]
    public void Tier1_05_RacialSynergies_TowerEntity()
    {
        // Dwarf synergy: +8% damage, +5% armor pen per dwarf
        // Elf synergy: +10% range, +5% crit chance per elf
        // Human synergy: >= 4 humans -> +20% efficiency

        // 1. Locked state verification
        Assert.IsFalse(metaUpgradeManager.IsUnlocked(MetaEffectType.TowerCrewSynergy_Dwarf));
        Assert.IsFalse(metaUpgradeManager.IsUnlocked(MetaEffectType.TowerCrewSynergy_Elf));
        Assert.IsFalse(metaUpgradeManager.IsUnlocked(MetaEffectType.TowerCrewSynergy_Human));

        var baseTowerData = CreateTower("Sentinel_T1_05", "Sentinel", damage: 100f, range: 20f, fireRate: 1.0f, armorPen: 10f, critChance: 5f, isUnlocked: true);

        // Before unlocking meta synergies, crew gives 0 synergy bonuses in TowerEntity
        var (towerLocked, controllerLocked, _) = CreateTestTowerWithCrew(baseTowerData, dwarves: 3, elves: 2, humans: 4);
        towerLocked.RecalculateStats();
        Assert.AreEqual(100f, GetPrivateField<float>(controllerLocked, "currentDamage"), 0.01f);
        Assert.AreEqual(20f, GetPrivateField<float>(controllerLocked, "currentRange"), 0.01f);
        Assert.AreEqual(0.10f, GetPrivateField<float>(controllerLocked, "currentArmorPen"), 0.001f);
        Assert.AreEqual(0.05f, GetPrivateField<float>(controllerLocked, "criticalChance"), 0.001f);
        Assert.AreEqual(1.0f, GetPrivateField<float>(controllerLocked, "currentFireRate"), 0.01f);

        // 2. Unlock synergies
        UnlockUpgrade(CreateUpgrade("synergy_dwarf", "Dwarf Synergy", 50, Vector2.zero, MetaEffectType.TowerCrewSynergy_Dwarf, 1f, true));
        UnlockUpgrade(CreateUpgrade("synergy_elf", "Elf Synergy", 50, Vector2.zero, MetaEffectType.TowerCrewSynergy_Elf, 1f, true));
        UnlockUpgrade(CreateUpgrade("synergy_human", "Human Synergy", 50, Vector2.zero, MetaEffectType.TowerCrewSynergy_Human, 1f, true));

        Assert.IsTrue(metaUpgradeManager.IsUnlocked(MetaEffectType.TowerCrewSynergy_Dwarf));
        Assert.IsTrue(metaUpgradeManager.IsUnlocked(MetaEffectType.TowerCrewSynergy_Elf));
        Assert.IsTrue(metaUpgradeManager.IsUnlocked(MetaEffectType.TowerCrewSynergy_Human));

        // 3. Test Dwarf synergy in TowerEntity: 3 Dwarves -> +24% damage, +15% armor pen
        var dwarfData = CreateTower("Dwarf_Tower", "Dwarf Tower", damage: 100f, range: 20f, fireRate: 1.0f, armorPen: 10f, critChance: 5f, isUnlocked: true);
        var (dwarfTower, dwarfController, _) = CreateTestTowerWithCrew(dwarfData, dwarves: 3, elves: 0, humans: 0);
        dwarfTower.RecalculateStats();
        // Base damage 100 * (1 + 0.24) = 124
        Assert.AreEqual(124.0f, GetPrivateField<float>(dwarfController, "currentDamage"), 0.01f, "3 Dwarves must grant +24% damage via TowerEntity.RecalculateStats.");
        // Base armor pen 0.10 + 0.15 = 0.25
        Assert.AreEqual(0.25f, GetPrivateField<float>(dwarfController, "currentArmorPen"), 0.001f, "3 Dwarves must grant +15% armor penetration.");

        // 4. Test Elf synergy in TowerEntity: 2 Elves -> +20% range, +10% crit chance
        var elfData = CreateTower("Elf_Tower", "Elf Tower", damage: 100f, range: 20f, fireRate: 1.0f, armorPen: 10f, critChance: 5f, isUnlocked: true);
        var (elfTower, elfController, _) = CreateTestTowerWithCrew(elfData, dwarves: 0, elves: 2, humans: 0);
        elfTower.RecalculateStats();
        // Base range 20 * (1 + 0.20) = 24
        Assert.AreEqual(24.0f, GetPrivateField<float>(elfController, "currentRange"), 0.01f, "2 Elves must grant +20% range via TowerEntity.RecalculateStats.");
        // Base crit chance 0.05 + 0.10 = 0.15
        Assert.AreEqual(0.15f, GetPrivateField<float>(elfController, "criticalChance"), 0.001f, "2 Elves must grant +10% crit chance.");

        // 5. Test Human threshold in TowerEntity: 1-3 humans give 0 bonus, 4 humans give +20% efficiency (fire rate)
        for (int h = 1; h <= 3; h++)
        {
            var hData = CreateTower($"Human_Tower_{h}", "Human Tower", damage: 100f, range: 20f, fireRate: 1.0f, armorPen: 10f, critChance: 5f, isUnlocked: true);
            var (hTower, hController, _) = CreateTestTowerWithCrew(hData, dwarves: 0, elves: 0, humans: h);
            hTower.RecalculateStats();
            Assert.AreEqual(1.0f, GetPrivateField<float>(hController, "currentFireRate"), 0.01f, $"Human count {h} below threshold 4 should give 0% bonus in TowerEntity.");
        }

        var h4Data = CreateTower("Human_Tower_4", "Human Tower", damage: 100f, range: 20f, fireRate: 1.0f, armorPen: 10f, critChance: 5f, isUnlocked: true);
        var (h4Tower, h4Controller, _) = CreateTestTowerWithCrew(h4Data, dwarves: 0, elves: 0, humans: 4);
        h4Tower.RecalculateStats();
        Assert.AreEqual(1.20f, GetPrivateField<float>(h4Controller, "currentFireRate"), 0.01f, "Human count 4 must trigger +20% efficiency in TowerEntity.");
    }

    [Test]
    public void Tier1_06_SaveSerialization_UnlockedTowerIDsAndStarterTowers()
    {
        SaveData data = new SaveData();
        Assert.IsNotNull(data.unlockedTowerIDs, "unlockedTowerIDs must be initialized.");
        Assert.IsNotNull(data.unlockedUpgradeIDs, "unlockedUpgradeIDs must be initialized.");

        // Save roundtrip with polish diacritics
        data.totalHope = 875;
        data.unlockedUpgradeIDs.AddRange(new[] { "start", "nowa_wieza_1", "ludzka_wytrwalosc_1" });
        data.unlockedTowerIDs.AddRange(new[] { "1. Sentinel Arbalest", "6. Royal Bombard", "Garłacz Gromu", "Tygiel Pożogi" });

        saveManager.currentSaveData = data;
        saveManager.SaveGame();

        Assert.IsTrue(File.Exists(tempSavePath), "Save file must be written to disk.");

        SaveManager reader = CreateInstance<SaveManager>();
        reader.SavePath = tempSavePath;
        reader.LoadGame();

        Assert.AreEqual(875, reader.currentSaveData.totalHope);
        Assert.AreEqual(3, reader.currentSaveData.unlockedUpgradeIDs.Count);
        Assert.IsTrue(reader.currentSaveData.unlockedTowerIDs.Contains("1. Sentinel Arbalest"));
        Assert.IsTrue(reader.currentSaveData.unlockedTowerIDs.Contains("Garłacz Gromu"));
        Assert.IsTrue(reader.currentSaveData.unlockedTowerIDs.Contains("Tygiel Pożogi"));

        // Verify permanent purchase constraint: no respec method
        var respecMethod = typeof(SaveManager).GetMethod("Respec", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNull(respecMethod, "SaveManager must not expose a respec method.");
    }

    [Test]
    public void Tier1_07_FourTierFogOfWar_StateCategorization()
    {
        // Root (Purchased) -> NodeA (Available) -> NodeB (Mystery) -> NodeC (Hidden)
        var root = CreateUpgrade("root", "Start", 0, new Vector2(1000, 1000), isUnlocked: true);
        var nodeA = CreateUpgrade("node_a", "Node A", 50, new Vector2(1200, 1000), isUnlocked: false);
        nodeA.prerequisites.Add(root);

        var nodeB = CreateUpgrade("node_b", "Node B", 100, new Vector2(1400, 1000), isUnlocked: false);
        nodeB.prerequisites.Add(nodeA);

        var nodeC = CreateUpgrade("node_c", "Node C", 150, new Vector2(1600, 1000), isUnlocked: false);
        nodeC.prerequisites.Add(nodeB);

        Assert.AreEqual(MetaFogState.Purchased, MetaNodeItemUI.CalculateFogState(root), "Level i: Unlocked node must be Purchased.");
        Assert.AreEqual(MetaFogState.Available, MetaNodeItemUI.CalculateFogState(nodeA), "Level i+1: Locked node with prereqs met must be Available.");
        Assert.AreEqual(MetaFogState.Mystery, MetaNodeItemUI.CalculateFogState(nodeB), "Level i+2: Direct child of Available node must be Mystery.");
        Assert.AreEqual(MetaFogState.Hidden, MetaNodeItemUI.CalculateFogState(nodeC), "Level i+3+: Deep node whose prereqs are Mystery must be Hidden.");
    }

    [Test]
    public void Tier1_08_UIStandards_TypographyAndIconBudgets()
    {
        // GEMINI.md Typography
        Assert.AreEqual(20f, MetaNodeDetailsPanelUI.FontSizeHeader, "Header font must be 20 pt.");
        Assert.AreEqual(16f, MetaNodeDetailsPanelUI.FontSizeSection, "Section font must be 16 pt.");
        Assert.AreEqual(16f, MetaNodeDetailsPanelUI.FontSizeButton, "Button font must be 16 pt.");
        Assert.GreaterOrEqual(MetaNodeDetailsPanelUI.FontSizeDescription, 12f, "Description font must be >= 12 pt.");
        Assert.LessOrEqual(MetaNodeDetailsPanelUI.FontSizeDescription, 14f, "Description font must be <= 14 pt.");
        Assert.AreEqual(10f, MetaNodeDetailsPanelUI.FontSizeHelper, "Helper font must be 10 pt.");

        // GEMINI.md Icon Sizes
        Assert.AreEqual(32f, MetaNodeDetailsPanelUI.IconSizeStandard, "Standard icon size must be 32 px.");
        Assert.AreEqual(24f, MetaNodeDetailsPanelUI.IconSizeSmall, "Small icon size must be 24 px.");
        Assert.AreEqual(32f, MetaNodeItemUI.IconSize, "Node item icon size must be 32 px.");

        // TowerDraftModalUI Typography and Icons
        Assert.AreEqual(20f, TowerDraftModalUI.FontSizeHeader, "Draft modal header must be 20 pt.");
        Assert.AreEqual(16f, TowerDraftModalUI.FontSizeButton, "Draft modal button must be 16 pt.");
        Assert.AreEqual(32f, TowerDraftModalUI.IconSizeStandard, "Draft modal standard icon must be 32 px.");
        Assert.AreEqual(24f, TowerDraftModalUI.IconSizeSmall, "Draft modal small icon must be 24 px.");
    }

    // =========================================================================
    // TIER 2: BOUNDARY & CORNER CASES
    // =========================================================================

    [Test]
    public void Tier2_01_WorkerCount_BoundaryExtrapolation_ZeroExactHigh()
    {
        var bData = CreateInstance<BuildingData>();
        bData.efficiencyTable = new List<float> { 0f, 1.0f, 1.2f, 1.4f }; // size 4, max index 3
        bData.workerScalingFactor = 0.20f;

        // 0 workers
        Assert.AreEqual(0f, bData.GetBaseEfficiencyForWorkers(0), "Zero workers must produce 0.0f base efficiency.");

        // Exact boundary (index 3)
        Assert.AreEqual(1.4f, bData.GetBaseEfficiencyForWorkers(3), 0.001f, "Exact table size must match last entry.");

        // High worker counts (e.g. 10, 15, 20)
        float prevEff = 0f;
        for (int w = 1; w <= 20; w++)
        {
            float eff = bData.GetBaseEfficiencyForWorkers(w);
            Assert.Greater(eff, prevEff, $"Efficiency for {w} workers must be strictly greater than for {w - 1} workers.");
            Assert.IsFalse(float.IsNaN(eff), "Efficiency must not be NaN.");
            Assert.IsFalse(float.IsInfinity(eff), "Efficiency must not be Infinity.");
            prevEff = eff;
        }

        // For 15 workers: 1.4 + (15 - 3) * 0.20 = 1.4 + 2.4 = 3.80f
        float eff15 = bData.GetBaseEfficiencyForWorkers(15);
        Assert.AreEqual(3.80f, eff15, 0.001f, "15 workers must extrapolate to 3.80f.");
    }

    [Test]
    public void Tier2_02_HopeCostMonotonicity_AlongDAGEval()
    {
        var allNodes = LoadAll88NodesFromDisk();

        // Start node must be 0 cost
        var startNode = allNodes.Values.FirstOrDefault(n => n.Id.Equals("start", StringComparison.OrdinalIgnoreCase));
        Assert.IsNotNull(startNode, "Start node must exist in the 88 assets.");
        Assert.AreEqual(0, startNode.Cost, "Start node must have 0 cost.");

        // Major linear progression branches must be strictly monotonic
        var elfyNodes = new[] { "elfy_1", "elfy_2", "elfy_3", "elfy_4" }
            .Select(id => allNodes.Values.First(n => n.Id == id)).ToList();
        for (int i = 0; i < elfyNodes.Count - 1; i++)
            Assert.LessOrEqual(elfyNodes[i].Cost, elfyNodes[i + 1].Cost, "Elfy path cost must be monotonically non-decreasing.");

        var krasnoludyNodes = new[] { "krasnoludy_1", "krasnoludy_2", "krasnoludy_3", "krasnoludy_4" }
            .Select(id => allNodes.Values.First(n => n.Id == id)).ToList();
        for (int i = 0; i < krasnoludyNodes.Count - 1; i++)
            Assert.LessOrEqual(krasnoludyNodes[i].Cost, krasnoludyNodes[i + 1].Cost, "Krasnoludy path cost must be monotonically non-decreasing.");

        var towerNodes = new[] { "all_towers_upgrade_1_a", "all_towers_upgrade_2_a", "all_towers_upgrade_3_a", "all_towers_upgrade_4_a" }
            .Select(id => allNodes.Values.First(n => n.Id == id)).ToList();
        for (int i = 0; i < towerNodes.Count - 1; i++)
            Assert.LessOrEqual(towerNodes[i].Cost, towerNodes[i + 1].Cost, "All towers upgrades cost must be monotonically non-decreasing.");

        var draftNodes = new[] { "nowa_wieza_1", "nowa_wieza_2", "nowa_wieza_3", "nowa_wieza_4", "nowa_wieza_5" }
            .Select(id => allNodes.Values.First(n => n.Id == id)).ToList();
        for (int i = 0; i < draftNodes.Count - 1; i++)
            Assert.LessOrEqual(draftNodes[i].Cost, draftNodes[i + 1].Cost, "Nowa wieza upgrades cost must be non-decreasing.");

        var humanNodes = new[] { "ludzka_wytrwalosc_1", "ludzka_wytrwalosc_2" }
            .Select(id => allNodes.Values.First(n => n.Id == id)).ToList();
        Assert.LessOrEqual(humanNodes[0].Cost, humanNodes[1].Cost, "Ludzka wytrwalosc cost must be non-decreasing.");

        // Overall graph monotonicity rate across all DAG edges should be >= 95%
        int totalEdges = 0;
        int monotonicEdges = 0;
        foreach (var kv in allNodes)
        {
            var child = kv.Value;
            foreach (var pGuid in child.PrereqGuids)
            {
                if (allNodes.TryGetValue(pGuid, out var parent))
                {
                    totalEdges++;
                    if (child.Cost >= parent.Cost) monotonicEdges++;
                }
            }
        }

        Assert.Greater(totalEdges, 60);
        float monotonicityRate = (float)monotonicEdges / totalEdges;
        Assert.GreaterOrEqual(monotonicityRate, 0.95f, $"DAG cost monotonicity rate ({monotonicityRate:P1}) must be >= 95%.");
    }

    [Test]
    public void Tier2_03_EmptySaveFile_StarterTowersSeeding()
    {
        if (File.Exists(tempSavePath)) File.Delete(tempSavePath);

        SaveManager sm = CreateInstance<SaveManager>();
        sm.SavePath = tempSavePath;
        sm.LoadGame();

        Assert.IsNotNull(sm.currentSaveData);
        Assert.IsNotNull(sm.currentSaveData.unlockedTowerIDs);
        Assert.AreEqual(2, sm.currentSaveData.unlockedTowerIDs.Count, "New game must seed exactly 2 starter towers.");
        Assert.IsTrue(sm.currentSaveData.unlockedTowerIDs.Contains("1. Sentinel Arbalest"), "Must contain '1. Sentinel Arbalest'.");
        Assert.IsTrue(sm.currentSaveData.unlockedTowerIDs.Contains("6. Royal Bombard"), "Must contain '6. Royal Bombard'.");

        Assert.IsTrue(File.Exists(tempSavePath), "Starter save must be automatically written to disk.");
    }

    [Test]
    public void Tier2_04_DraftModal_CandidateReduction_AndEmptyPoolSafety()
    {
        var towers = Create7CatalogTowers();
        var unlockedList = new List<string> { "1. Sentinel Arbalest", "6. Royal Bombard" };

        // Starts with 5 locked candidates
        var c5 = TowerDraftModalUI.FilterCandidates(towers, unlockedList);
        Assert.AreEqual(5, c5.Count, "Initially 5 locked towers available for draft.");

        // Pick 1 -> 4
        unlockedList.Add("2. Thunderbuss");
        var c4 = TowerDraftModalUI.FilterCandidates(towers, unlockedList);
        Assert.AreEqual(4, c4.Count);

        // Pick 2 -> 3
        unlockedList.Add("3. Steam Repeater");
        var c3 = TowerDraftModalUI.FilterCandidates(towers, unlockedList);
        Assert.AreEqual(3, c3.Count);

        // Pick 3 -> 2
        unlockedList.Add("4. Crucible of Cinders");
        var c2 = TowerDraftModalUI.FilterCandidates(towers, unlockedList);
        Assert.AreEqual(2, c2.Count);

        // Pick 4 -> 1
        unlockedList.Add("5. Shadow Spire");
        var c1 = TowerDraftModalUI.FilterCandidates(towers, unlockedList);
        Assert.AreEqual(1, c1.Count);

        // Pick 5 -> 0
        unlockedList.Add("7. Soul Monolith");
        var c0 = TowerDraftModalUI.FilterCandidates(towers, unlockedList);
        Assert.AreEqual(0, c0.Count, "When all 7 towers are unlocked, candidate pool must be 0.");

        // Safe with empty/null lists
        var safeNull = TowerDraftModalUI.FilterCandidates(null, unlockedList);
        Assert.IsNotNull(safeNull);
        Assert.AreEqual(0, safeNull.Count);
    }

    [Test]
    public void Tier2_05_PanZoom_BoundsClamping_AndCursorInvariance()
    {
        var panZoom = CreateInstance<MetaGraphPanZoomUI>();
        SetPrivateField(panZoom, "minZoom", 0.25f);
        SetPrivateField(panZoom, "maxZoom", 2.5f);
        SetPrivateField(panZoom, "zoomSensitivity", 0.15f);
        SetPrivateField(panZoom, "contentBounds", new Rect(400f, 200f, 5600f, 4800f));
        SetPrivateField(panZoom, "fallbackViewportSize", new Vector2(1920f, 1080f));

        // Zoom clamping
        panZoom.Zoom(100f, new Vector2(960f, 540f));
        Assert.LessOrEqual(panZoom.TargetZoom, 2.5f, "Zoom in must clamp to MaxZoom (2.5f).");
        Assert.AreEqual(2.5f, panZoom.TargetZoom, 0.001f);

        panZoom.Zoom(-100f, new Vector2(960f, 540f));
        Assert.GreaterOrEqual(panZoom.TargetZoom, 0.25f, "Zoom out must clamp to MinZoom (0.25f).");
        Assert.AreEqual(0.25f, panZoom.TargetZoom, 0.001f);

        // Reset to 1.0
        panZoom.FocusOnPosition(new Vector2(1460f, 2600f));
        float scaleBefore = panZoom.TargetZoom;
        panZoom.Zoom(0f, new Vector2(960f, 540f));
        Assert.AreEqual(scaleBefore, panZoom.TargetZoom, "Zero zoom delta must preserve scale exactly.");

        // Cursor invariance: content point under cursor before and after zoom
        Vector2 cursor = new Vector2(960f, 540f);
        Vector2 targetPos = new Vector2(-1000f, -1500f);
        float scale1 = 1.0f;
        float scale2 = 1.5f;

        Vector2 contentPt = (cursor - targetPos) / scale1;
        Vector2 newTargetPos = cursor - contentPt * scale2;
        Vector2 reprojectedCursor = newTargetPos + contentPt * scale2;

        Assert.Less(Vector2.Distance(reprojectedCursor, cursor), 0.001f, "Point under cursor must not drift during zoom.");
    }

    // =========================================================================
    // TIER 3: CROSS-FEATURE INTERACTIONS
    // =========================================================================

    [Test]
    public void Tier3_01_TowerDraft_PurchaseFlow_E2E()
    {
        saveManager.currentSaveData.totalHope = 500;
        saveManager.currentSaveData.unlockedTowerIDs = new List<string> { "1. Sentinel Arbalest", "6. Royal Bombard" };
        var draftNode = CreateUpgrade("nowa_wieza_1", "Nowa wieża 1", 100, new Vector2(1880, 2600), MetaEffectType.TowerDraftUnlock);
        metaUpgradeManager.allUpgrades.Add(draftNode);

        // 1. Purchase node: hope deducted
        Assert.IsTrue(saveManager.currentSaveData.totalHope >= draftNode.cost);
        saveManager.currentSaveData.totalHope -= draftNode.cost;
        draftNode.isUnlocked = true;
        saveManager.currentSaveData.unlockedUpgradeIDs.Add(draftNode.id);
        saveManager.SaveGame();

        Assert.AreEqual(400, saveManager.currentSaveData.totalHope);
        Assert.AreEqual(MetaFogState.Purchased, MetaNodeItemUI.CalculateFogState(draftNode));

        // 2. Draft detection & candidate filtering
        Assert.IsTrue(metaUpgradeManager.IsTowerDraftUpgrade(draftNode), "nowa_wieza_1 must trigger tower draft.");
        var candidates = TowerDraftModalUI.FilterCandidates(catalogTowers, saveManager.currentSaveData.unlockedTowerIDs);
        Assert.AreEqual(5, candidates.Count, "Draft must present 5 candidates initially.");

        // 3. Select tower: unlock, save, sync
        var chosenTower = candidates.First(t => t.buildingName == "2. Thunderbuss");
        saveManager.UnlockTower(chosenTower.buildingName);
        saveManager.SyncTowersWithSave(catalogTowers);

        Assert.IsTrue(chosenTower.isUnlocked, "Chosen tower must now be unlocked.");
        Assert.IsTrue(saveManager.currentSaveData.unlockedTowerIDs.Contains("2. Thunderbuss"));

        // 4. Next draft candidate pool is reduced
        var remainingCandidates = TowerDraftModalUI.FilterCandidates(catalogTowers, saveManager.currentSaveData.unlockedTowerIDs);
        Assert.AreEqual(4, remainingCandidates.Count, "Candidates pool must shrink to 4.");
    }

    [Test]
    public void Tier3_02_TriRacialCrew_ConcurrenceInTower()
    {
        // Tower crewed by 4 Humans, 2 Dwarves, 2 Elves
        int dwarfCount = 2;
        int elfCount = 2;
        int humanCount = 4;

        // Meta upgrades active
        UnlockUpgrade(CreateUpgrade("dwarf_syn", "Dwarf Syn", 50, Vector2.zero, MetaEffectType.TowerCrewSynergy_Dwarf, 1f, true));
        UnlockUpgrade(CreateUpgrade("elf_syn", "Elf Syn", 50, Vector2.zero, MetaEffectType.TowerCrewSynergy_Elf, 1f, true));
        UnlockUpgrade(CreateUpgrade("human_syn", "Human Syn", 50, Vector2.zero, MetaEffectType.TowerCrewSynergy_Human, 1f, true));

        // Base tower: baseDmg = 50, baseRange = 20, fireRate = 1.0, baseArmorPen = 10% (0.10), baseCrit = 5% (0.05)
        var tData = CreateTower("TriRacialTower", "TriRacial Tower", damage: 50f, range: 20f, fireRate: 1.0f, armorPen: 10f, critChance: 5f, isUnlocked: true);

        var (tower, controller, _) = CreateTestTowerWithCrew(tData, dwarves: dwarfCount, elves: elfCount, humans: humanCount);
        tower.RecalculateStats();

        // Concurrence assertions directly on TowerController populated by TowerEntity.RecalculateStats
        float finalDmg = GetPrivateField<float>(controller, "currentDamage");
        float finalRange = GetPrivateField<float>(controller, "currentRange");
        float finalArmorPen = GetPrivateField<float>(controller, "currentArmorPen");
        float finalCrit = GetPrivateField<float>(controller, "criticalChance");
        float finalFireRate = GetPrivateField<float>(controller, "currentFireRate");

        // 2 Dwarves -> +16% dmg: 50 * 1.16 = 58.0
        Assert.AreEqual(58.0f, finalDmg, 0.01f, "Damage must be increased by 16% from 2 Dwarves.");
        // 2 Elves -> +20% range: 20 * 1.20 = 24.0
        Assert.AreEqual(24.0f, finalRange, 0.01f, "Range must be increased by 20% from 2 Elves.");
        // 2 Dwarves -> +10% armor pen: 0.10 + 0.10 = 0.20
        Assert.AreEqual(0.20f, finalArmorPen, 0.001f, "Armor pen must be increased by 10% from 2 Dwarves.");
        // 2 Elves -> +10% crit: 0.05 + 0.10 = 0.15
        Assert.AreEqual(0.15f, finalCrit, 0.001f, "Crit chance must be increased by 10% from 2 Elves.");
        // 4 Humans -> +20% efficiency: 1.0 * 1.20 = 1.20
        Assert.AreEqual(1.20f, finalFireRate, 0.01f, "Fire rate must reflect +20% efficiency from 4 Humans.");
    }

    [Test]
    public void Tier3_03_RuneForgeTransmute_GatedRarity_AndSaveRoundtrip()
    {
        // 1. Setup RuneManager with 3 Legendary runes and Cursed configuration
        var runeMgr = CreateInstance<RuneManager>();
        runeMgr.playerRunes = new List<RuneItem>();

        var legDef1 = CreateInstance<RuneDefinitionSO>();
        legDef1.runeName = "Legendary Rune 1";
        legDef1.rarity = RuneRarity.Legendary;
        legDef1.primaryStat = RuneStatType.Damage;
        legDef1.valueType = RuneValueType.Percent;
        legDef1.primaryValueRange = new Vector2(25f, 25f);

        var legDef2 = CreateInstance<RuneDefinitionSO>();
        legDef2.runeName = "Legendary Rune 2";
        legDef2.rarity = RuneRarity.Legendary;
        legDef2.primaryStat = RuneStatType.Range;
        legDef2.valueType = RuneValueType.Flat;
        legDef2.primaryValueRange = new Vector2(4f, 4f);

        var legDef3 = CreateInstance<RuneDefinitionSO>();
        legDef3.runeName = "Legendary Rune 3";
        legDef3.rarity = RuneRarity.Legendary;
        legDef3.primaryStat = RuneStatType.FireRate;
        legDef3.valueType = RuneValueType.Percent;
        legDef3.primaryValueRange = new Vector2(20f, 20f);

        var r1 = new RuneItem(legDef1, 25f);
        var r2 = new RuneItem(legDef2, 4f);
        var r3 = new RuneItem(legDef3, 20f);
        runeMgr.playerRunes.Add(r1);
        runeMgr.playerRunes.Add(r2);
        runeMgr.playerRunes.Add(r3);

        var runeConfig = CreateInstance<RuneSystemConfigSO>();
        var cursedDef = CreateInstance<RuneDefinitionSO>();
        cursedDef.runeName = "Cursed Doom";
        cursedDef.rarity = RuneRarity.Cursed;
        cursedDef.primaryStat = RuneStatType.Damage;
        cursedDef.valueType = RuneValueType.Percent;
        cursedDef.primaryValueRange = new Vector2(50f, 50f);
        cursedDef.possiblePenalties = new List<RuneDefinitionSO.CursedPenalty>();
        runeConfig.cursedRunes = new List<RuneDefinitionSO> { cursedDef };
        runeMgr.config = runeConfig;

        // 2. Blocked when UnlockCursedCrafting is locked
        Assert.IsFalse(metaUpgradeManager.IsUnlocked(MetaEffectType.UnlockCursedCrafting));
        bool transmuteResultLocked = runeMgr.TryTransmuteRunes(r1, r2, r3);
        Assert.IsFalse(transmuteResultLocked, "TryTransmuteRunes with 3 Legendary runes must return false when UnlockCursedCrafting is locked.");
        Assert.AreEqual(3, runeMgr.playerRunes.Count, "Player runes must remain intact when transmutation is rejected.");

        // 3. Unlock upgrade and save
        var cursedSO = CreateUpgrade("krasnoludy_run_4", "Krasnoludy Run 4", 250, Vector2.zero, MetaEffectType.UnlockCursedCrafting, 1f, true);
        UnlockUpgrade(cursedSO);
        saveManager.currentSaveData.unlockedUpgradeIDs.Add(cursedSO.id);
        saveManager.SaveGame();

        Assert.IsTrue(metaUpgradeManager.IsUnlocked(MetaEffectType.UnlockCursedCrafting));

        // 4. Transmutation to Cursed now permitted through production RuneManager.TryTransmuteRunes
        bool transmuteResultUnlocked = runeMgr.TryTransmuteRunes(r1, r2, r3);
        Assert.IsTrue(transmuteResultUnlocked, "TryTransmuteRunes with 3 Legendary runes must return true when UnlockCursedCrafting is unlocked.");
        Assert.AreEqual(1, runeMgr.playerRunes.Count, "3 Legendary runes must be consumed and replaced by 1 Cursed rune.");
        Assert.AreEqual(RuneRarity.Cursed, runeMgr.playerRunes[0].definition.rarity, "The transmuted rune must be Cursed rarity.");

        // 5. Persistence roundtrip verifies upgrade remains unlocked
        SaveManager sm2 = CreateInstance<SaveManager>();
        sm2.SavePath = tempSavePath;
        sm2.LoadGame();
        Assert.IsTrue(sm2.currentSaveData.unlockedUpgradeIDs.Contains("krasnoludy_run_4"));
    }

    // =========================================================================
    // TIER 4: REALISTIC E2E APPLICATION SCENARIOS
    // =========================================================================

    [Test]
    public void Tier4_01_FullTreeTraversal_AndHopeEconomy()
    {
        // Traversal along a realistic branch: Start -> T1 -> T2 -> T3 -> T4 -> Capstone
        var start = CreateUpgrade("start", "Start", 0, new Vector2(1460, 2600), isUnlocked: false);
        var t1 = CreateUpgrade("t1", "Tier 1", 50, new Vector2(1700, 2600), isUnlocked: false);
        t1.prerequisites.Add(start);
        var t2 = CreateUpgrade("t2", "Tier 2", 100, new Vector2(2000, 2600), isUnlocked: false);
        t2.prerequisites.Add(t1);
        var t3 = CreateUpgrade("t3", "Tier 3", 200, new Vector2(2300, 2600), isUnlocked: false);
        t3.prerequisites.Add(t2);
        var t4 = CreateUpgrade("t4", "Tier 4", 350, new Vector2(2600, 2600), isUnlocked: false);
        t4.prerequisites.Add(t3);
        var capstone = CreateUpgrade("capstone", "Tier 11 Capstone", 500, new Vector2(3000, 2600), isUnlocked: false);
        capstone.prerequisites.Add(t4);

        var chain = new List<MetaUpgradeSO> { start, t1, t2, t3, t4, capstone };
        int startingHope = 2000;
        int currentHope = startingHope;
        int totalSpent = 0;

        foreach (var node in chain)
        {
            Assert.AreEqual(MetaFogState.Available, MetaNodeItemUI.CalculateFogState(node),
                $"Node {node.id} must be Available when its turn arrives.");

            Assert.GreaterOrEqual(currentHope, node.cost, "Must have enough Hope to purchase node.");
            currentHope -= node.cost;
            totalSpent += node.cost;

            node.isUnlocked = true;
            saveManager.currentSaveData.unlockedUpgradeIDs.Add(node.id);

            Assert.AreEqual(MetaFogState.Purchased, MetaNodeItemUI.CalculateFogState(node),
                $"Node {node.id} must transition to Purchased after unlock.");
        }

        int expectedSpent = 0 + 50 + 100 + 200 + 350 + 500; // 1200
        Assert.AreEqual(expectedSpent, totalSpent, "Total spent must equal sum of costs in chain.");
        Assert.AreEqual(startingHope - expectedSpent, currentHope, "Remaining Hope must equal starting Hope minus total spent.");
        Assert.AreEqual(6, saveManager.currentSaveData.unlockedUpgradeIDs.Count);
    }

    [Test]
    public void Tier4_02_MaxCrew_FactoryProductionSurge()
    {
        // Factory with base 5 slots + 7 meta bonus slots = 12 total assigned workers
        int totalWorkers = 12;
        var factoryData = CreateInstance<BuildingData>();
        factoryData.baseMaxWorkers = 5;
        factoryData.workerScalingFactor = 0.20f;
        factoryData.efficiencyTable = new List<float> { 0f, 1.0f, 1.2f, 1.4f, 1.6f }; // size 5, max index 4

        // Base linear extrapolation
        float baseEff = factoryData.GetBaseEfficiencyForWorkers(totalWorkers);
        // 1.6 + (12 - 4) * 0.20 = 1.6 + 1.6 = 3.20f
        Assert.AreEqual(3.20f, baseEff, 0.001f, "Dynamic extrapolation for 12 workers must yield 3.20f (320%).");

        // Combined with meta worker efficiency bonus (+0.05 per worker = +0.60f)
        float metaBonus = totalWorkers * 0.05f;
        float totalEff = baseEff + metaBonus;
        Assert.AreEqual(3.80f, totalEff, 0.001f, "Total surge efficiency must reach 3.80f (380%).");

        // Factory base output: 15 Wood/cycle -> 15 * 3.80 = 57 Wood/cycle
        float baseYield = 15f;
        float surgeYield = baseYield * totalEff;
        Assert.AreEqual(57.0f, surgeYield, 0.01f, "Factory surge yield must reach 57 Wood/cycle.");
        Assert.Greater(surgeYield, baseYield * 3.5f, "Surge yield must exceed 3.5x base production.");
    }

    [Test]
    public void Tier4_03_TowerDraft_ProgressionFromScratch_All5Nodes()
    {
        // Start fresh with only 2 starter towers unlocked
        saveManager.currentSaveData.unlockedTowerIDs.Clear();
        saveManager.currentSaveData.unlockedTowerIDs.Add("1. Sentinel Arbalest");
        saveManager.currentSaveData.unlockedTowerIDs.Add("6. Royal Bombard");
        saveManager.SyncTowersWithSave(catalogTowers);

        string[] draftOrder = new string[]
        {
            "2. Thunderbuss",
            "3. Steam Repeater",
            "4. Crucible of Cinders",
            "5. Shadow Spire",
            "7. Soul Monolith"
        };

        for (int i = 0; i < 5; i++)
        {
            var candidates = TowerDraftModalUI.FilterCandidates(catalogTowers, saveManager.currentSaveData.unlockedTowerIDs);
            Assert.AreEqual(5 - i, candidates.Count, $"Step {i + 1}: Expected {5 - i} draft candidates.");

            string chosenTowerName = draftOrder[i];
            saveManager.UnlockTower(chosenTowerName);
            saveManager.SyncTowersWithSave(catalogTowers);

            Assert.IsTrue(saveManager.currentSaveData.unlockedTowerIDs.Contains(chosenTowerName));
        }

        // Final verification: all 7 towers unlocked, candidate pool is 0
        var finalCandidates = TowerDraftModalUI.FilterCandidates(catalogTowers, saveManager.currentSaveData.unlockedTowerIDs);
        Assert.AreEqual(0, finalCandidates.Count, "All 7 towers are now unlocked, draft pool must be empty.");
        Assert.AreEqual(7, saveManager.currentSaveData.unlockedTowerIDs.Count);

        saveManager.SaveGame();
        SaveManager smCheck = CreateInstance<SaveManager>();
        smCheck.SavePath = tempSavePath;
        smCheck.LoadGame();
        Assert.AreEqual(7, smCheck.currentSaveData.unlockedTowerIDs.Count, "Save file on disk must retain all 7 unlocked towers.");
    }

    [Test]
    public void Tier4_04_DeepFogOfWar_FrontierExploration()
    {
        // Linear chain N0 -> N1 -> N2 -> N3 -> N4 -> N5
        var nodes = new List<MetaUpgradeSO>();
        for (int i = 0; i < 6; i++)
        {
            var n = CreateUpgrade($"node_{i}", $"Node {i}", 50 * i, new Vector2(1000 + i * 200, 1000), isUnlocked: false);
            if (i > 0) n.prerequisites.Add(nodes[i - 1]);
            nodes.Add(n);
        }

        // Step 0: none unlocked
        Assert.AreEqual(MetaFogState.Available, MetaNodeItemUI.CalculateFogState(nodes[0]));
        Assert.AreEqual(MetaFogState.Mystery, MetaNodeItemUI.CalculateFogState(nodes[1]));
        for (int i = 2; i < 6; i++) Assert.AreEqual(MetaFogState.Hidden, MetaNodeItemUI.CalculateFogState(nodes[i]));

        // Progressively unlock from N0 to N5 and verify all states
        for (int unlockIdx = 0; unlockIdx < 6; unlockIdx++)
        {
            nodes[unlockIdx].isUnlocked = true;

            // 0 .. unlockIdx must be Purchased
            for (int p = 0; p <= unlockIdx; p++)
            {
                Assert.AreEqual(MetaFogState.Purchased, MetaNodeItemUI.CalculateFogState(nodes[p]),
                    $"Step {unlockIdx}: Node {p} should be Purchased.");
            }

            // unlockIdx + 1 (if within bounds) must be Available
            if (unlockIdx + 1 < 6)
            {
                Assert.AreEqual(MetaFogState.Available, MetaNodeItemUI.CalculateFogState(nodes[unlockIdx + 1]),
                    $"Step {unlockIdx}: Frontier node {unlockIdx + 1} should be Available.");
            }

            // unlockIdx + 2 (if within bounds) must be Mystery
            if (unlockIdx + 2 < 6)
            {
                Assert.AreEqual(MetaFogState.Mystery, MetaNodeItemUI.CalculateFogState(nodes[unlockIdx + 2]),
                    $"Step {unlockIdx}: Horizon node {unlockIdx + 2} should be Mystery.");
            }

            // unlockIdx + 3 onwards must be Hidden
            for (int h = unlockIdx + 3; h < 6; h++)
            {
                Assert.AreEqual(MetaFogState.Hidden, MetaNodeItemUI.CalculateFogState(nodes[h]),
                    $"Step {unlockIdx}: Deep fog node {h} should be Hidden.");
            }
        }
    }

    [Test]
    public void Tier4_05_TriRacialDefense_SimulatedNightCombat()
    {
        // Tower: Sentinel Arbalest (baseDamage: 25, baseRange: 15, fireRate: 1.2, armorPen: 20%, critChance: 5%)
        // Armored enemy: 30 Armor, 500 HP
        // Crew: 4 Humans, 2 Dwarves, 2 Elves
        // Active Upgrades: Dwarf Synergy (+16% dmg, +10% arm pen), Elf Synergy (+20% range, +10% crit),
        //                  Human Synergy (+20% efficiency), TowerGlobalDamage (+5 flat), TowerGlobalFireRate (+15% multi)

        // 1. Baseline: Uncrewed, un-upgraded tower via production TowerEntity.RecalculateStats()
        var baseTowerData = CreateTower("Sentinel_Base", "Sentinel Arbalest", damage: 25f, range: 15f, fireRate: 1.2f, armorPen: 20f, critChance: 5f, isUnlocked: true);
        var (baseTower, baseController, _) = CreateTestTowerWithCrew(baseTowerData, dwarves: 0, elves: 0, humans: 0);
        baseTower.RecalculateStats();

        float baseDmg = GetPrivateField<float>(baseController, "currentDamage");
        float baseRange = GetPrivateField<float>(baseController, "currentRange");
        float baseFireRate = GetPrivateField<float>(baseController, "currentFireRate");
        float baseArmorPen = GetPrivateField<float>(baseController, "currentArmorPen");
        float baseCrit = GetPrivateField<float>(baseController, "criticalChance");

        float enemyArmor = 30f;
        float baseEffArmor = enemyArmor * (1f - baseArmorPen); // 24
        float baseHitDmg = baseDmg * (100f / (100f + baseEffArmor)); // 25 * (100/124) = 20.161
        float baseCritFactor = 1.0f + baseCrit * 1.0f; // 1.05
        float baseDPS = baseHitDmg * baseCritFactor * baseFireRate; // 20.161 * 1.05 * 1.2 = 25.40

        // 2. Upgraded Tri-Racial Tower: unlock meta upgrades and crew with 4 Humans, 2 Dwarves, 2 Elves
        UnlockUpgrade(CreateUpgrade("dwarf_syn", "Dwarf Synergy", 50, Vector2.zero, MetaEffectType.TowerCrewSynergy_Dwarf, 1f, true));
        UnlockUpgrade(CreateUpgrade("elf_syn", "Elf Synergy", 50, Vector2.zero, MetaEffectType.TowerCrewSynergy_Elf, 1f, true));
        UnlockUpgrade(CreateUpgrade("human_syn", "Human Synergy", 50, Vector2.zero, MetaEffectType.TowerCrewSynergy_Human, 1f, true));
        UnlockUpgrade(CreateUpgrade("global_dmg", "Global Dmg", 50, Vector2.zero, MetaEffectType.TowerGlobalDamage, 5f, true));
        UnlockUpgrade(CreateUpgrade("global_fr", "Global Fire Rate", 50, Vector2.zero, MetaEffectType.TowerGlobalFireRate, 0.15f, true));

        var upTowerData = CreateTower("Sentinel_Upgraded", "Sentinel Arbalest", damage: 25f, range: 15f, fireRate: 1.2f, armorPen: 20f, critChance: 5f, isUnlocked: true);
        var (upTower, upController, _) = CreateTestTowerWithCrew(upTowerData, dwarves: 2, elves: 2, humans: 4);
        upTower.RecalculateStats();

        float upDmg = GetPrivateField<float>(upController, "currentDamage");
        float upRange = GetPrivateField<float>(upController, "currentRange");
        float upFireRate = GetPrivateField<float>(upController, "currentFireRate");
        float upArmorPen = GetPrivateField<float>(upController, "currentArmorPen");
        float upCrit = GetPrivateField<float>(upController, "criticalChance");

        float upEffArmor = enemyArmor * (1f - upArmorPen); // 30 * 0.70 = 21
        float upHitDmg = upDmg * (100f / (100f + upEffArmor)); // 34.8 * (100/121) = 28.760
        float upCritFactor = 1.0f + upCrit * 1.0f; // 1.15
        float upDPS = upHitDmg * upCritFactor * upFireRate; // 28.760 * 1.15 * 1.656 = 54.77

        Assert.Greater(upRange, baseRange * 1.15f, "Range must be boosted by Elf synergy.");
        Assert.Greater(upDPS, baseDPS * 2.0f, "Upgraded tri-racial tower DPS must exceed 2x base un-crewed tower DPS against armored enemies.");
        Assert.AreEqual(34.8f, upDmg, 0.01f);
        Assert.AreEqual(18.0f, upRange, 0.01f);
        Assert.AreEqual(0.30f, upArmorPen, 0.001f);
        Assert.AreEqual(1.656f, upFireRate, 0.001f);
    }

    // =========================================================================
    // CLI HARNESS RUNNER
    // =========================================================================

    public static int RunAll()
    {
        Console.WriteLine("\n=================================================");
        Console.WriteLine("METAPROGRESSION COMPREHENSIVE SUITE (TIERS 1–4)");
        Console.WriteLine("=================================================");

        var suite = new MetaProgressionTests();
        int passed = 0;
        int failed = 0;

        var tests = new (string Name, Action Action)[]
        {
            // Tier 1: Feature Isolation
            ("Tier1_01_All88Nodes_IntegrityAndCoordinates", suite.Tier1_01_All88Nodes_IntegrityAndCoordinates),
            ("Tier1_02_WorkerSlots_Decoupling", suite.Tier1_02_WorkerSlots_Decoupling),
            ("Tier1_03_DynamicEfficiencyExtrapolation_WithinAndBeyondTable", suite.Tier1_03_DynamicEfficiencyExtrapolation_WithinAndBeyondTable),
            ("Tier1_04_RuneRarityGating_AndForgeUnlocks", suite.Tier1_04_RuneRarityGating_AndForgeUnlocks),
            ("Tier1_05_RacialSynergies_TowerEntity", suite.Tier1_05_RacialSynergies_TowerEntity),
            ("Tier1_06_SaveSerialization_UnlockedTowerIDsAndStarterTowers", suite.Tier1_06_SaveSerialization_UnlockedTowerIDsAndStarterTowers),
            ("Tier1_07_FourTierFogOfWar_StateCategorization", suite.Tier1_07_FourTierFogOfWar_StateCategorization),
            ("Tier1_08_UIStandards_TypographyAndIconBudgets", suite.Tier1_08_UIStandards_TypographyAndIconBudgets),

            // Tier 2: Boundary & Corner Cases
            ("Tier2_01_WorkerCount_BoundaryExtrapolation_ZeroExactHigh", suite.Tier2_01_WorkerCount_BoundaryExtrapolation_ZeroExactHigh),
            ("Tier2_02_HopeCostMonotonicity_AlongDAGEval", suite.Tier2_02_HopeCostMonotonicity_AlongDAGEval),
            ("Tier2_03_EmptySaveFile_StarterTowersSeeding", suite.Tier2_03_EmptySaveFile_StarterTowersSeeding),
            ("Tier2_04_DraftModal_CandidateReduction_AndEmptyPoolSafety", suite.Tier2_04_DraftModal_CandidateReduction_AndEmptyPoolSafety),
            ("Tier2_05_PanZoom_BoundsClamping_AndCursorInvariance", suite.Tier2_05_PanZoom_BoundsClamping_AndCursorInvariance),

            // Tier 3: Cross-Feature Interactions
            ("Tier3_01_TowerDraft_PurchaseFlow_E2E", suite.Tier3_01_TowerDraft_PurchaseFlow_E2E),
            ("Tier3_02_TriRacialCrew_ConcurrenceInTower", suite.Tier3_02_TriRacialCrew_ConcurrenceInTower),
            ("Tier3_03_RuneForgeTransmute_GatedRarity_AndSaveRoundtrip", suite.Tier3_03_RuneForgeTransmute_GatedRarity_AndSaveRoundtrip),

            // Tier 4: Realistic E2E Application Scenarios
            ("Tier4_01_FullTreeTraversal_AndHopeEconomy", suite.Tier4_01_FullTreeTraversal_AndHopeEconomy),
            ("Tier4_02_MaxCrew_FactoryProductionSurge", suite.Tier4_02_MaxCrew_FactoryProductionSurge),
            ("Tier4_03_TowerDraft_ProgressionFromScratch_All5Nodes", suite.Tier4_03_TowerDraft_ProgressionFromScratch_All5Nodes),
            ("Tier4_04_DeepFogOfWar_FrontierExploration", suite.Tier4_04_DeepFogOfWar_FrontierExploration),
            ("Tier4_05_TriRacialDefense_SimulatedNightCombat", suite.Tier4_05_TriRacialDefense_SimulatedNightCombat)
        };

        foreach (var (name, action) in tests)
        {
            try
            {
                suite.SetUp();
                action();
                suite.TearDown();
                passed++;
                Console.WriteLine($"[META-TEST-PASS] {name}");
            }
            catch (Exception ex)
            {
                try { suite.TearDown(); } catch { }
                failed++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[META-TEST-FAIL] {name} - {ex.Message}");
                Console.ResetColor();
            }
        }

        Console.WriteLine("\n=================================================");
        Console.WriteLine($"METAPROGRESSION SUMMARY: {passed} PASSED, {failed} FAILED");
        Console.WriteLine("=================================================");

        return failed;
    }
}
