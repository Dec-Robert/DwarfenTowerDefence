using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class MetaGameplayLogicR2Tests
{
    private List<GameObject> spawnedObjects;
    private GameObject cityStatsGO;
    private CityStatsManager cityStats;
    private GameObject metaUpgradeGO;
    private MetaUpgradeManager metaUpgradeManager;

    [SetUp]
    public void SetUp()
    {
        spawnedObjects = new List<GameObject>();

        cityStatsGO = new GameObject("TestCityStatsManager");
        cityStats = cityStatsGO.AddComponent<CityStatsManager>();
        spawnedObjects.Add(cityStatsGO);

        metaUpgradeGO = new GameObject("TestMetaUpgradeManager");
        metaUpgradeManager = metaUpgradeGO.AddComponent<MetaUpgradeManager>();
        spawnedObjects.Add(metaUpgradeGO);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in spawnedObjects)
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
        spawnedObjects.Clear();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Worker Decoupling Tests
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public void CityStatsManager_DecoupledWorkerSlots_AreIndependent()
    {
        cityStats.AddGlobalProductionWorkerSlot(3);
        cityStats.AddGlobalTowerCrewSlot(2);

        Assert.AreEqual(3, cityStats.globalBonusProductionWorkers, "Production worker bonus should be 3");
        Assert.AreEqual(2, cityStats.globalBonusTowerCrewSlots, "Tower crew bonus should be 2");
        Assert.AreEqual(0, cityStats.globalBonusWorkersPerShift, "Legacy bonus should remain untouched");
    }

    [Test]
    public void BuildingEntity_And_TowerEntity_QueryRespectiveDecoupledWorkerBonuses()
    {
        cityStats.globalBonusProductionWorkers = 4;
        cityStats.globalBonusTowerCrewSlots = 1;

        var buildingGO = new GameObject("BuildingEntityTest");
        var building = buildingGO.AddComponent<BuildingEntity>();
        spawnedObjects.Add(buildingGO);

        var towerGO = new GameObject("TowerEntityTest");
        var tower = towerGO.AddComponent<TowerEntity>();
        spawnedObjects.Add(towerGO);

        Assert.AreEqual(4, building.GetGlobalWorkerBonus(), "BuildingEntity must query globalBonusProductionWorkers");
        Assert.AreEqual(1, tower.GetGlobalWorkerBonus(), "TowerEntity must query globalBonusTowerCrewSlots");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Dynamic Efficiency Extrapolation in BuildingData Tests
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public void BuildingData_GetBaseEfficiencyForWorkers_WithinTable_ReturnsTableValue()
    {
        var bData = ScriptableObject.CreateInstance<BuildingData>();
        bData.efficiencyTable = new List<float> { 0f, 1.0f, 1.25f, 1.5f };
        bData.workerScalingFactor = 0.25f;

        Assert.AreEqual(0f, bData.GetBaseEfficiencyForWorkers(0));
        Assert.AreEqual(1.0f, bData.GetBaseEfficiencyForWorkers(1));
        Assert.AreEqual(1.25f, bData.GetBaseEfficiencyForWorkers(2));
        Assert.AreEqual(1.5f, bData.GetBaseEfficiencyForWorkers(3));
    }

    [Test]
    public void BuildingData_GetBaseEfficiencyForWorkers_BeyondTable_ExtrapolatesLinearly()
    {
        var bData = ScriptableObject.CreateInstance<BuildingData>();
        bData.efficiencyTable = new List<float> { 0f, 1.0f, 1.2f, 1.4f }; // count = 4, max index = 3
        bData.workerScalingFactor = 0.2f;

        // 4 workers -> extra = 1, lastVal = 1.4 -> 1.6
        float eff4 = bData.GetBaseEfficiencyForWorkers(4);
        Assert.AreEqual(1.6f, eff4, 0.001f, "4 workers should extrapolate to 1.6");

        // 5 workers -> extra = 2, lastVal = 1.4 -> 1.8
        float eff5 = bData.GetBaseEfficiencyForWorkers(5);
        Assert.AreEqual(1.8f, eff5, 0.001f, "5 workers should extrapolate to 1.8");

        // 7 workers -> extra = 4, lastVal = 1.4 -> 2.2
        float eff7 = bData.GetBaseEfficiencyForWorkers(7);
        Assert.AreEqual(2.2f, eff7, 0.001f, "7 workers should extrapolate to 2.2");
    }

    [Test]
    public void BuildingData_GetBaseEfficiencyForWorkers_NeverZerosOut_WhenScalingFactorZero()
    {
        var bData = ScriptableObject.CreateInstance<BuildingData>();
        bData.efficiencyTable = new List<float> { 0f, 1.0f, 1.2f }; // count = 3
        bData.workerScalingFactor = 0f; // Unconfigured scaling factor

        // Should default to 0.15f
        float eff3 = bData.GetBaseEfficiencyForWorkers(3);
        Assert.AreEqual(1.35f, eff3, 0.001f, "Fallback scaling of 0.15f should yield 1.35f");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. MetaUpgradeManager Wiring Tests
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public void MetaUpgradeManager_Applies_DecoupledWorkerUpgrades()
    {
        cityStats.globalBonusProductionWorkers = 0;
        cityStats.globalBonusTowerCrewSlots = 0;

        var prodUpgrade = ScriptableObject.CreateInstance<MetaUpgradeSO>();
        prodUpgrade.id = "test_prod_worker";
        prodUpgrade.isUnlocked = true;
        prodUpgrade.effectType = MetaEffectType.BonusProductionWorkers;
        prodUpgrade.effectValue = 2f;

        var towerUpgrade = ScriptableObject.CreateInstance<MetaUpgradeSO>();
        towerUpgrade.id = "test_tower_crew";
        towerUpgrade.isUnlocked = true;
        towerUpgrade.effectType = MetaEffectType.BonusTowerCrewSlots;
        towerUpgrade.effectValue = 3f;

        metaUpgradeManager.allUpgrades = new List<MetaUpgradeSO> { prodUpgrade, towerUpgrade };
        metaUpgradeManager.ReapplyAllUpgrades();

        Assert.AreEqual(2, cityStats.globalBonusProductionWorkers);
        Assert.AreEqual(3, cityStats.globalBonusTowerCrewSlots);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Rune Gating Tests
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public void RuneManager_RarityUnlocks_GateHigherRarities()
    {
        var runeMgrGO = new GameObject("TestRuneManager");
        var runeMgr = runeMgrGO.AddComponent<RuneManager>();
        spawnedObjects.Add(runeMgrGO);

        var config = ScriptableObject.CreateInstance<RuneSystemConfigSO>();
        config.weightCommon = 50f;
        config.weightUncommon = 25f;
        config.weightRare = 15f;
        config.weightLegendary = 10f;
        config.weightCursed = 5f;
        runeMgr.config = config;

        // With no meta upgrades unlocked, Uncommon, Rare, Legendary, Cursed must have 0 effective weight
        metaUpgradeManager.allUpgrades = new List<MetaUpgradeSO>();
        metaUpgradeManager.ReapplyAllUpgrades();

        MethodInfo rollMethod = typeof(RuneManager).GetMethod("RollRarity", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(rollMethod, "RollRarity method must exist");

        for (int i = 0; i < 50; i++)
        {
            RuneRarity result = (RuneRarity)rollMethod.Invoke(runeMgr, null);
            Assert.AreEqual(RuneRarity.Common, result, "Only Common should roll when higher tiers are locked");
        }
    }

    [Test]
    public void RuneManager_TryTransmute_GatesCursedCrafting()
    {
        var runeMgrGO = new GameObject("TestRuneManager");
        var runeMgr = runeMgrGO.AddComponent<RuneManager>();
        spawnedObjects.Add(runeMgrGO);

        var config = ScriptableObject.CreateInstance<RuneSystemConfigSO>();
        runeMgr.config = config;

        var legDef = ScriptableObject.CreateInstance<RuneDefinitionSO>();
        legDef.rarity = RuneRarity.Legendary;
        legDef.primaryStat = RuneStatType.Damage;
        legDef.valueType = RuneValueType.Flat;

        var r1 = new RuneItem(legDef, 10f);
        var r2 = new RuneItem(legDef, 10f);
        var r3 = new RuneItem(legDef, 10f);

        // When UnlockCursedCrafting is NOT unlocked
        metaUpgradeManager.allUpgrades = new List<MetaUpgradeSO>();
        metaUpgradeManager.ReapplyAllUpgrades();

        bool successLocked = runeMgr.TryTransmuteRunes(r1, r2, r3);
        Assert.IsFalse(successLocked, "Transmuting 3 Legendaries to Cursed must fail when UnlockCursedCrafting is locked");

        // When UnlockCursedCrafting IS unlocked
        var cursedCraftUpgrade = ScriptableObject.CreateInstance<MetaUpgradeSO>();
        cursedCraftUpgrade.id = "krasnoludy_run_4";
        cursedCraftUpgrade.isUnlocked = true;
        cursedCraftUpgrade.effectType = MetaEffectType.UnlockCursedCrafting;
        cursedCraftUpgrade.effectValue = 1f;

        // Provide a cursed definition so GenerateRandomRune succeeds
        var cursedDef = ScriptableObject.CreateInstance<RuneDefinitionSO>();
        cursedDef.rarity = RuneRarity.Cursed;
        cursedDef.primaryStat = RuneStatType.Damage;
        cursedDef.valueType = RuneValueType.Flat;
        config.cursedRunes = new List<RuneDefinitionSO> { cursedDef };

        metaUpgradeManager.allUpgrades = new List<MetaUpgradeSO> { cursedCraftUpgrade };
        metaUpgradeManager.ReapplyAllUpgrades();

        runeMgr.playerRunes.Add(r1);
        runeMgr.playerRunes.Add(r2);
        runeMgr.playerRunes.Add(r3);

        bool successUnlocked = runeMgr.TryTransmuteRunes(r1, r2, r3);
        Assert.IsTrue(successUnlocked, "Transmuting 3 Legendaries to Cursed must succeed when UnlockCursedCrafting is unlocked");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Permanent Purchase Constraint
    // ─────────────────────────────────────────────────────────────────────────

    [Test]
    public void SaveData_PersistsUnlockedUpgradeIDs_WithoutRespec()
    {
        var saveData = new SaveData();
        saveData.unlockedUpgradeIDs.Add("upgrade_1");
        saveData.unlockedUpgradeIDs.Add("upgrade_2");

        Assert.IsTrue(saveData.unlockedUpgradeIDs.Contains("upgrade_1"));
        Assert.IsTrue(saveData.unlockedUpgradeIDs.Contains("upgrade_2"));
        Assert.AreEqual(2, saveData.unlockedUpgradeIDs.Count);

        // Verify SaveManager has no respec method
        MethodInfo respecMethod = typeof(SaveManager).GetMethod("Respec", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNull(respecMethod, "SaveManager must not expose a respec method");
    }
}
