using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public class MetaUnificationAndSystemsTests
{
    private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

    [SetUp]
    public void SetUp()
    {
        DevRunConfig.ClearTestOverride();
        ClearSingletons();
    }

    [TearDown]
    public void TearDown()
    {
        DevRunConfig.ClearTestOverride();
        ClearSingletons();

        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }
        }
        createdObjects.Clear();
    }

    private void ClearSingletons()
    {
        SetStaticProperty(typeof(MetaUpgradeManager), "Instance", null);
        SetStaticProperty(typeof(ResourceManager), "Instance", null);
        SetStaticProperty(typeof(MapExpansionManager), "Instance", null);
        SetStaticProperty(typeof(HexMapGenerator), "Instance", null);
        SetStaticProperty(typeof(BeaconEntity), "Instance", null);
        SetStaticProperty(typeof(CityStatsManager), "Instance", null);
        SetStaticProperty(typeof(PopLifeCycleManager), "Instance", null);
    }

    private MetaUpgradeManager CreateMetaManager(params MetaUpgradeSO[] upgrades)
    {
        var go = new GameObject("TestMetaManager");
        createdObjects.Add(go);
        var mgr = go.AddComponent<MetaUpgradeManager>();
        mgr.allUpgrades = new List<MetaUpgradeSO>(upgrades);
        SetStaticProperty(typeof(MetaUpgradeManager), "Instance", mgr);
        mgr.ReapplyAllUpgrades();
        return mgr;
    }

    private MetaUpgradeSO CreateUpgrade(
        string id,
        MetaEffectType effectType,
        float value = 1f,
        float value2 = 0f,
        BuildingData targetBuilding = null,
        MetaUpgradeCategory category = MetaUpgradeCategory.General,
        bool unlocked = true)
    {
        var so = ScriptableObject.CreateInstance<MetaUpgradeSO>();
        so.id = id;
        so.upgradeName = id;
        so.category = category;
        so.effectType = effectType;
        so.effectValue = value;
        so.effectValue2 = value2;
        so.targetBuilding = targetBuilding;
        so.isUnlocked = unlocked;
        createdObjects.Add(so);
        return so;
    }

    private static void SetStaticProperty(Type type, string propName, object value)
    {
        var prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(null, value);
            return;
        }
        var field = type.GetField($"<{propName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
        field?.SetValue(null, value);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        if (target == null) return;
        var type = target.GetType();
        FieldInfo field = null;
        while (type != null && field == null)
        {
            field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                field = type.GetField($"<{fieldName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            type = type.BaseType;
        }
        field?.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        if (target == null) return default;
        var type = target.GetType();
        FieldInfo field = null;
        while (type != null && field == null)
        {
            field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                field = type.GetField($"<{fieldName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            type = type.BaseType;
        }
        return field != null ? (T)field.GetValue(target) : default;
    }

    // -------------------------------------------------------------------------
    // 1. Zaklęcia: SpellBonusCharges
    // -------------------------------------------------------------------------

    [Test]
    public void SpellBonusCharges_IncreasesMaxChargesForRechargeableSpellsOnly()
    {
        var bonusCharges = CreateUpgrade("ladunki_zaklec_1", MetaEffectType.SpellBonusCharges, 1f, category: MetaUpgradeCategory.Spells);
        CreateMetaManager(bonusCharges);

        var go = new GameObject("TestDruidTower");
        createdObjects.Add(go);
        var druid = go.AddComponent<DruidTowerEntity>();

        var rechargeableSpell = ScriptableObject.CreateInstance<SpellDataSO>();
        rechargeableSpell.castLimitMode = SpellCastLimitMode.RechargeableCharges;
        rechargeableSpell.maxCharges = 2;
        createdObjects.Add(rechargeableSpell);

        SetPrivateField(druid, "<ActiveSpell>k__BackingField", rechargeableSpell);
        Assert.AreEqual(3, druid.GetEffectiveMaxUsesOrCharges(), "Rechargeable spell should receive +1 charge from SpellBonusCharges.");

        var limitedSpell = ScriptableObject.CreateInstance<SpellDataSO>();
        limitedSpell.castLimitMode = SpellCastLimitMode.LimitedUsesPerNight;
        limitedSpell.maxUsesPerNight = 2;
        createdObjects.Add(limitedSpell);

        SetPrivateField(druid, "<ActiveSpell>k__BackingField", limitedSpell);
        Assert.AreEqual(2, druid.GetEffectiveMaxUsesOrCharges(), "LimitedUsesPerNight spell must not get extra charges.");
    }

    // -------------------------------------------------------------------------
    // 2. Palenisko: Hearth 1, 2, 3 i Hearth Producion 1, 2
    // -------------------------------------------------------------------------

    [Test]
    public void HearthSystems_GateHighLevels_DiscountCoal_AndBoostNextDayProduction()
    {
        // Bez Hearth 1: poziomy 4 i 5 zablokowane
        CreateMetaManager();
        Assert.IsTrue(BeaconEntity.IsLevelUnlocked(1));
        Assert.IsTrue(BeaconEntity.IsLevelUnlocked(3));
        Assert.IsFalse(BeaconEntity.IsLevelUnlocked(4));
        Assert.IsFalse(BeaconEntity.IsLevelUnlocked(5));
        Assert.IsFalse(BeaconEntity.TryIgniteAndStartNight(4));

        var config = ScriptableObject.CreateInstance<HearthConfigSO>();
        config.EnsureDefaultLevels();
        createdObjects.Add(config);
        var beaconGo = new GameObject("TestBeacon");
        createdObjects.Add(beaconGo);
        var beacon = beaconGo.AddComponent<BeaconEntity>();
        beacon.hearthConfig = config;
        SetStaticProperty(typeof(BeaconEntity), "Instance", beacon);

        int rawCostLvl2 = BeaconEntity.GetCoalCostForLevel(2);
        int rawCostLvl3 = BeaconEntity.GetCoalCostForLevel(3);
        int rawCostLvl5 = BeaconEntity.GetCoalCostForLevel(5);

        // Odblokuj Hearth 1 + 2 (15%) + 3 (20%) + Producion 1 (L4:+0.05, L5:+0.10) + Producion 2 (L3:+0.05, L4:+0.05)
        var h1 = CreateUpgrade("hearth_1", MetaEffectType.HearthUnlockHighLevels, 1f);
        var h2 = CreateUpgrade("hearth_2", MetaEffectType.HearthHighLevelCoalDiscount, 0.15f);
        var h3 = CreateUpgrade("hearth_3", MetaEffectType.HearthHighLevelCoalDiscount, 0.20f);
        var hp1 = CreateUpgrade("hearth_producion_1", MetaEffectType.HearthNextDayProductionBoost, 0.05f, 0.10f);
        var hp2 = CreateUpgrade("hearth_producion_2", MetaEffectType.HearthNextDayProductionBoost, 0.05f, 0f);
        CreateMetaManager(h1, h2, h3, hp1, hp2);

        Assert.IsTrue(BeaconEntity.IsLevelUnlocked(4));
        Assert.IsTrue(BeaconEntity.IsLevelUnlocked(5));
        Assert.AreEqual(rawCostLvl2, BeaconEntity.GetCoalCostForLevel(2), "Poziom 2 nie powinien dostać zniżki węgla.");
        Assert.AreEqual(Mathf.Max(1, Mathf.RoundToInt(rawCostLvl3 * 0.65f)), BeaconEntity.GetCoalCostForLevel(3));
        Assert.AreEqual(Mathf.Max(1, Mathf.RoundToInt(rawCostLvl5 * 0.65f)), BeaconEntity.GetCoalCostForLevel(5));

        var lvl3 = config.GetLevelData(3);
        var lvl4 = config.GetLevelData(4);
        var lvl5 = config.GetLevelData(5);
        Assert.AreEqual(lvl3.nextDayProductionBonus + 0.05f, BeaconEntity.GetEffectiveNextDayProductionBonus(lvl3), 0.0001f);
        Assert.AreEqual(lvl4.nextDayProductionBonus + 0.10f, BeaconEntity.GetEffectiveNextDayProductionBonus(lvl4), 0.0001f);
        Assert.AreEqual(lvl5.nextDayProductionBonus + 0.10f, BeaconEntity.GetEffectiveNextDayProductionBonus(lvl5), 0.0001f);
    }

    // -------------------------------------------------------------------------
    // 3. Ekspedycje, Zwiad i Zniżka Budowy
    // -------------------------------------------------------------------------

    [Test]
    public void ExpeditionFreeThresholdAndCostReduction_ApplyCorrectly()
    {
        var cfg = ScriptableObject.CreateInstance<ExpansionCostConfig>();
        createdObjects.Add(cfg);

        var chunks = new Dictionary<Vector2Int, ChunkStateData>
        {
            [Vector2Int.zero] = new ChunkStateData { chunkCoord = Vector2Int.zero, baseState = ChunkState.Settled }
        };

        var baseAt4 = ExpansionCostCalculator.CalculateSurveyCostWithModifiers(
            new Vector2Int(1, 0), chunks, false, cfg, 1, extraDiscoveredChunks: 4, costReductionPercent: 0f, freeThresholdBonus: 0);

        var freeAt4WithBonus3 = ExpansionCostCalculator.CalculateSurveyCostWithModifiers(
            new Vector2Int(1, 0), chunks, false, cfg, 1, extraDiscoveredChunks: 4, costReductionPercent: 0f, freeThresholdBonus: 3);

        var discounted = ExpansionCostCalculator.CalculateSurveyCostWithModifiers(
            new Vector2Int(1, 0), chunks, false, cfg, 1, extraDiscoveredChunks: 4, costReductionPercent: 0.15f, freeThresholdBonus: 3);

        Assert.Greater(baseAt4.etherCost, 0, "Without threshold bonus, chunk #5 (index 4) starts costing Ether.");
        Assert.AreEqual(0, freeAt4WithBonus3.etherCost, "With +3 free threshold bonus, threshold shifts from 4 to 7 so index 4 costs 0 Ether.");
        Assert.Less(discounted.foodCost, freeAt4WithBonus3.foodCost, "15% cost reduction lowers Food cost.");
    }

    [Test]
    public void TacticalReconGate_AndNoDoubleGatherBetweenReconAndSurvey()
    {
        var expGo = new GameObject("TestExpansionManager");
        createdObjects.Add(expGo);
        var expMgr = expGo.AddComponent<MapExpansionManager>();
        SetStaticProperty(typeof(MapExpansionManager), "Instance", expMgr);

        var resGo = new GameObject("TestResourceManager");
        createdObjects.Add(resGo);
        var resMgr = resGo.AddComponent<ResourceManager>();
        SetStaticProperty(typeof(ResourceManager), "Instance", resMgr);
        int startWood = resMgr.GetResourceAmount(ResourceType.Wood);
        int startFood = resMgr.GetResourceAmount(ResourceType.Food);
        int startStone = resMgr.GetResourceAmount(ResourceType.Stone);
        int startIron = resMgr.GetResourceAmount(ResourceType.Iron);
        int startCoal = resMgr.GetResourceAmount(ResourceType.Coal);

        var mapGo = new GameObject("TestHexMapGenerator");
        createdObjects.Add(mapGo);
        var mapGen = mapGo.AddComponent<HexMapGenerator>();
        var ctx = new MapGenerationContext(123);
        var targetCoord = new Vector2Int(1, 0);
        ctx.worldData[targetCoord] = new Dictionary<Vector2Int, HexCellData>
        {
            [new Vector2Int(0, 0)] = new HexCellData { feature = HexFeatureType.Forest },
            [new Vector2Int(1, 0)] = new HexCellData { feature = HexFeatureType.Meadow },
            [new Vector2Int(0, 1)] = new HexCellData { feature = HexFeatureType.Mountain },
            [new Vector2Int(-1, 0)] = new HexCellData { feature = HexFeatureType.None, elevation = 2 }
        };
        SetPrivateField(mapGen, "ctx", ctx);
        SetStaticProperty(typeof(HexMapGenerator), "Instance", mapGen);

        CreateMetaManager();
        Assert.IsFalse(MapExpansionManager.IsTacticalReconUnlocked(), "Recon must be locked without Elfy 2.");

        var elfy2 = CreateUpgrade("elfy_2", MetaEffectType.UnlockTacticalRecon, 1f);
        CreateMetaManager(elfy2);
        Assert.IsTrue(MapExpansionManager.IsTacticalReconUnlocked(), "Recon must unlock when Elfy 2 is active.");

        var chunkData = new ChunkStateData { chunkCoord = targetCoord, baseState = ChunkState.Borderlands };
        Assert.IsTrue(expMgr.GrantChunkTerrainResources(chunkData, 1, "Recon"));
        Assert.IsTrue(chunkData.hasGatheredResources);
        Assert.AreEqual(startWood + 1, resMgr.GetResourceAmount(ResourceType.Wood));
        Assert.AreEqual(startFood + 1, resMgr.GetResourceAmount(ResourceType.Food));
        Assert.AreEqual(startStone + 1, resMgr.GetResourceAmount(ResourceType.Stone));
        Assert.AreEqual(startIron + 1, resMgr.GetResourceAmount(ResourceType.Iron));
        Assert.AreEqual(startCoal + 1, resMgr.GetResourceAmount(ResourceType.Coal));

        // Kolejna próba zebrania na tym samym chunku (np. po Survey) nie może zebrać drugi raz
        Assert.IsFalse(expMgr.GrantChunkTerrainResources(chunkData, 3, "Survey"));
        Assert.AreEqual(startWood + 1, resMgr.GetResourceAmount(ResourceType.Wood));
    }

    [Test]
    public void SpecificBuildingConstructionCostReduction_ReducesTargetedBuildingOnly()
    {
        var expCenter = ScriptableObject.CreateInstance<BuildingData>();
        expCenter.name = "ExpeditionCenter";
        expCenter.constructionCost = new List<ResourceCost>
        {
            new ResourceCost { type = ResourceType.Wood, amount = 20 },
            new ResourceCost { type = ResourceType.Stone, amount = 40 }
        };
        createdObjects.Add(expCenter);

        var otherBld = ScriptableObject.CreateInstance<BuildingData>();
        otherBld.name = "OtherBuilding";
        otherBld.constructionCost = new List<ResourceCost>
        {
            new ResourceCost { type = ResourceType.Wood, amount = 20 }
        };
        createdObjects.Add(otherBld);

        var discount = CreateUpgrade("elfy_expedition_center_1", MetaEffectType.SpecificBuildingConstructionCostReduction, 0.15f, targetBuilding: expCenter);
        CreateMetaManager(discount);

        var discountedCosts = expCenter.GetEffectiveConstructionCosts();
        var otherCosts = otherBld.GetEffectiveConstructionCosts();

        Assert.AreEqual(17, discountedCosts[0].amount);
        Assert.AreEqual(34, discountedCosts[1].amount);
        Assert.AreEqual(20, otherCosts[0].amount);
    }

    // -------------------------------------------------------------------------
    // 4. Produkcja budynków, domy startowe, penetracja i synergie
    // -------------------------------------------------------------------------

    [Test]
    public void SpecificBuildingFlatProduction_AndTerrainFlatBonuses_ReturnExpectedValues()
    {
        var farm = AssetDatabase.LoadAssetAtPath<BuildingData>("Assets/Main folder/ScriptableObjects/Buildings/Farm.asset");
        var mines = AssetDatabase.LoadAssetAtPath<BuildingData>("Assets/Main folder/ScriptableObjects/Buildings/Mines.asset");
        var quarry = AssetDatabase.LoadAssetAtPath<BuildingData>("Assets/Main folder/ScriptableObjects/Buildings/Quarry.asset");

        Assert.IsNotNull(farm);
        Assert.IsNotNull(mines);
        Assert.IsNotNull(quarry);

        var p1 = CreateUpgrade("produkcja_budynkow_1", MetaEffectType.SpecificBuildingTerrainFlatBonus, 3f, 1f, farm);
        var p3 = CreateUpgrade("produkcja_budynkow_3", MetaEffectType.SpecificBuildingTerrainFlatBonus, 1f, 0.25f, quarry);
        var p4 = CreateUpgrade("produkcja_budynkow_4", MetaEffectType.SpecificBuildingFlatProduction, 1f, 0f, mines);
        CreateMetaManager(p1, p3, p4);

        Assert.AreEqual(3f, BuildingProductionCalculator.GetMetaTerrainOnTopFlatBonus(farm, ResourceType.Food), 0.001f);
        Assert.AreEqual(1f, BuildingProductionCalculator.GetMetaTerrainAdjacentFlatBonus(farm, ResourceType.Food), 0.001f);
        Assert.AreEqual(0f, BuildingProductionCalculator.GetMetaTerrainOnTopFlatBonus(farm, ResourceType.Wood), 0.001f);

        Assert.AreEqual(1f, BuildingProductionCalculator.GetMetaTerrainOnTopFlatBonus(quarry, ResourceType.Stone), 0.001f);
        Assert.AreEqual(0.25f, BuildingProductionCalculator.GetMetaTerrainAdjacentFlatBonus(quarry, ResourceType.Stone), 0.001f);

        Assert.AreEqual(1f, BuildingProductionCalculator.GetMetaFlatProductionBonus(mines, ResourceType.Iron), 0.001f);
        Assert.AreEqual(1f, BuildingProductionCalculator.GetMetaFlatProductionBonus(mines, ResourceType.Coal), 0.001f);
        Assert.AreEqual(0f, BuildingProductionCalculator.GetMetaFlatProductionBonus(farm, ResourceType.Food), 0.001f);
    }

    [Test]
    public void RestoreStarterRuinedHouses_ReplacesRuinedOnOriginChunkAndPreservesResident()
    {
        var ruinedElf = AssetDatabase.LoadAssetAtPath<HousingBuildingData>("Assets/Main folder/ScriptableObjects/Buildings/Houses/Ruined Elven house.asset");
        var ruinedDwarf = AssetDatabase.LoadAssetAtPath<HousingBuildingData>("Assets/Main folder/ScriptableObjects/Buildings/Houses/Ruined Dwarven house.asset");
        var elfHouse = AssetDatabase.LoadAssetAtPath<HousingBuildingData>("Assets/Main folder/ScriptableObjects/Buildings/Houses/Elf house.asset");
        var dwarfHouse = AssetDatabase.LoadAssetAtPath<HousingBuildingData>("Assets/Main folder/ScriptableObjects/Buildings/Houses/Dwarven house.asset");

        Assert.IsNotNull(ruinedElf);
        Assert.IsNotNull(ruinedDwarf);
        Assert.IsNotNull(elfHouse);
        Assert.IsNotNull(dwarfHouse);

        var ctx = new MapGenerationContext(42);
        ctx.worldData[Vector2Int.zero] = new Dictionary<Vector2Int, HexCellData>
        {
            [new Vector2Int(0, 1)] = new HexCellData { chunkCoord = Vector2Int.zero, localCoord = new Vector2Int(0, 1), startingBuilding = ruinedElf },
            [new Vector2Int(1, 0)] = new HexCellData { chunkCoord = Vector2Int.zero, localCoord = new Vector2Int(1, 0), startingBuilding = ruinedDwarf }
        };

        var gen = new TerrainGenerator(ctx, null, null, 3);
        gen.ApplyRestoreStarterRuinedHouses(elfHouse, dwarfHouse);

        Assert.AreSame(elfHouse, ctx.worldData[Vector2Int.zero][new Vector2Int(0, 1)].startingBuilding);
        Assert.AreEqual(1, ctx.worldData[Vector2Int.zero][new Vector2Int(0, 1)].starterHouseBonusResidents);
        Assert.AreSame(dwarfHouse, ctx.worldData[Vector2Int.zero][new Vector2Int(1, 0)].startingBuilding);
        Assert.AreEqual(1, ctx.worldData[Vector2Int.zero][new Vector2Int(1, 0)].starterHouseBonusResidents);
    }

    [Test]
    public void TowerGlobalPenetrations_AndHumanSynergy15Percent_ApplyCorrectly()
    {
        var armorPen = CreateUpgrade("upg_armor", MetaEffectType.TowerGlobalArmorPenetration, 0.10f);
        var magicPen = CreateUpgrade("upg_magic", MetaEffectType.TowerGlobalMagicPenetration, 0.10f);
        var humanSyn = CreateUpgrade("ludzka_wytrwalosc_2", MetaEffectType.TowerCrewSynergy_Human, 0.15f, 4f);
        CreateMetaManager(armorPen, magicPen, humanSyn);

        var towerData = ScriptableObject.CreateInstance<TowerData>();
        towerData.name = "TestTower";
        towerData.baseDamage = 100f;
        towerData.baseRange = 10f;
        towerData.fireRate = 1f;
        towerData.armorPenetration = 0f;
        towerData.magicPenetration = 0f;
        createdObjects.Add(towerData);

        var (tower, controller, _) = MetaProgressionTests.CreateTestTowerWithCrew(towerData, 0, 0, 4);
        createdObjects.Add(tower);
        createdObjects.Add(controller);

        tower.RecalculateStats();
        Assert.AreEqual(0.10f, GetPrivateField<float>(controller, "currentArmorPen"), 0.001f);
        Assert.AreEqual(0.10f, GetPrivateField<float>(controller, "currentMagicPen"), 0.001f);
        // base efficiency = 1.0 + 0.15 = 1.15 -> fireRate = 1.0 * 1.15 = 1.15
        Assert.AreEqual(1.15f, GetPrivateField<float>(controller, "currentFireRate"), 0.001f);
    }

    // -------------------------------------------------------------------------
    // 5. Integralność zmigrowanych assetów Meta/*.asset
    // -------------------------------------------------------------------------

    [Test]
    public void AllMetaAssets_AreUnifiedWithValidCategoriesAndEffects()
    {
        string[] guids = AssetDatabase.FindAssets("t:MetaUpgradeSO", new[] { "Assets/Main folder/ScriptableObjects/Meta" });
        Assert.AreEqual(88, guids.Length, "Expected 88 MetaUpgradeSO assets in Meta folder.");

        var placeholders = new List<string>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<MetaUpgradeSO>(path);
            Assert.IsNotNull(asset, $"Failed to load {path}");

            if (asset.IsPlaceholder)
            {
                placeholders.Add(asset.id);
                Assert.AreEqual(MetaUpgradeCategory.PLACEHOLDER, asset.category, $"{asset.name} must use PLACEHOLDER category.");
                Assert.AreEqual(MetaEffectType.Placeholder, asset.effectType, $"{asset.name} must use Placeholder effectType.");
                continue;
            }

            if (!asset.hasVariants)
            {
                Assert.AreNotEqual(MetaEffectType.None, asset.effectType, $"Active node {asset.name} must not have MetaEffectType.None.");
            }

            if (asset.RequiresTargetBuilding)
            {
                Assert.IsNotNull(asset.targetBuilding, $"Node {asset.name} ({asset.effectType}) must have targetBuilding assigned.");
            }
        }

        CollectionAssert.AreEquivalent(
            new[] { "elfy_chunki_1", "elfy_chunki_2", "mury_miejsce_4", "krasnoludy_3" },
            placeholders,
            "Exactly the 4 agreed incomplete nodes should be in PLACEHOLDER.");
    }
}
