using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public class DemoV2ConfigTests
{
    private bool prevDemoFlag;

    [SetUp]
    public void SetUp()
    {
        prevDemoFlag = DemoBuildConfig.IsDemoV2Active;
        DemoBuildConfig.IsDemoV2Active = true;
    }

    [TearDown]
    public void TearDown()
    {
        DemoBuildConfig.IsDemoV2Active = prevDemoFlag;
    }

    [Test]
    public void DemoV2_AllowedTowers_AreExactlyFiveExpectedTowers()
    {
        string[] guids = AssetDatabase.FindAssets("t:TowerData");
        Assert.AreEqual(7, guids.Length, "W projekcie powinno byc 7 assetow TowerData");

        var allowedNames = new List<string>();
        var blockedNames = new List<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TowerData tower = AssetDatabase.LoadAssetAtPath<TowerData>(path);
            if (DemoBuildConfig.IsTowerAllowedInDemo(tower))
                allowedNames.Add(tower.name);
            else
                blockedNames.Add(tower.name);
        }

        Assert.AreEqual(5, allowedNames.Count, "W Demie v2 powinno byc odblokowanych dokladnie 5 wiez");
        Assert.AreEqual(2, blockedNames.Count, "W Demie v2 powinny byc zablokowane dokladnie 2 wieze");
        Assert.IsTrue(blockedNames.Contains("2. Thunderbuss"), "2. Thunderbuss powinien byc zablokowany");
        Assert.IsTrue(blockedNames.Contains("7. Soul Monolith"), "7. Soul Monolith powinien byc zablokowany");
    }

    [Test]
    public void DemoV2_CityBaseConfig_HasExpectedStartingBonuses()
    {
        var config = AssetDatabase.LoadAssetAtPath<CityBaseConfigSO>("Assets/Main folder/ScriptableObjects/Configs/CityBaseConfig.asset");
        Assert.IsNotNull(config);
        Assert.AreEqual(30, config.baseCityHP, "Bazowe HP Twierdzy powinno wynosic 30");
        Assert.AreEqual(55, config.startWood, "Startowe drewno powinno wynosic 55");
        Assert.AreEqual(60, config.startCoal, "Startowy wegiel powinien wynosic 60");
        Assert.AreEqual(30, config.startStone, "Startowy kamien powinien wynosic 30");
        Assert.AreEqual(55, config.startFood, "Startowe jedzenie powinno wynosic 55");
        Assert.AreEqual(8, config.startEther, "Startowy eter powinien pozostac 8");
        Assert.AreEqual(0, config.startIron, "Startowe zelazo powinno pozostac 0");
    }

    [Test]
    public void DemoV2_RuneSmith_IronCostReducedBy5To15()
    {
        var runeSmith = AssetDatabase.LoadAssetAtPath<BuildingData>("Assets/Main folder/ScriptableObjects/Buildings/RuneSmith.asset");
        Assert.IsNotNull(runeSmith);

        int ironCost = 0;
        foreach (var cost in runeSmith.constructionCost)
        {
            if (cost.type == ResourceType.Iron)
                ironCost = cost.amount;
        }

        Assert.AreEqual(15, ironCost, "Koszt Kuzni Runicznej w metalu/zelazie powinien wynosic 15 (20 - 5)");
    }

    [Test]
    public void DemoV2_SpecialBuildings_RuneUnlockedAndToolWorkshopLocked()
    {
        var runeSmith = AssetDatabase.LoadAssetAtPath<BuildingData>("Assets/Main folder/ScriptableObjects/Buildings/RuneSmith.asset");
        var runeTower = AssetDatabase.LoadAssetAtPath<BuildingData>("Assets/Main folder/ScriptableObjects/Buildings/RuneSmithTower.asset");
        var toolWorkshop = AssetDatabase.LoadAssetAtPath<BuildingData>("Assets/Main folder/ScriptableObjects/Buildings/ToolWorkshop.asset");

        Assert.IsNotNull(runeSmith);
        Assert.IsNotNull(runeTower);
        Assert.IsNotNull(toolWorkshop);

        MethodInfo isUnlockedMethod = typeof(ExtendedBuildingUI).GetMethod("IsBuildingUnlocked", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(isUnlockedMethod);

        Assert.IsTrue((bool)isUnlockedMethod.Invoke(null, new object[] { runeSmith, false }), "Kuznia Runiczna powinna byc odblokowana");
        Assert.IsTrue((bool)isUnlockedMethod.Invoke(null, new object[] { runeTower, false }), "Wieza Runiczna powinna byc odblokowana");
        Assert.IsFalse((bool)isUnlockedMethod.Invoke(null, new object[] { toolWorkshop, true }), "Warsztat Narzedziowy musi byc calkowicie zablokowany");

        Assert.IsFalse(ToolWorkshopResearchManager.IsWorkshopUnlockedInMeta(), "ToolWorkshopResearchManager powinien zwracac false w Demie v2");
        Assert.IsFalse(DruidTowerSpawner.IsMetaUnlocked(), "Wieza Druida powinna byc zablokowana w Demie v2");
    }

    [Test]
    public void DemoV2_MetaUpgradeManager_AppliesFixedPresetAndIgnoresUpgrades()
    {
        var go = new GameObject("Test_MetaUpgradeManager");
        try
        {
            var mgr = go.AddComponent<MetaUpgradeManager>();
            var wallUpgrade = ScriptableObject.CreateInstance<MetaUpgradeSO>();
            wallUpgrade.id = "wall_test";
            wallUpgrade.effectType = MetaEffectType.WallSystem_Solution2;
            wallUpgrade.isUnlocked = true;
            mgr.allUpgrades.Add(wallUpgrade);

            MethodInfo collectMethod = typeof(MetaUpgradeManager).GetMethod("CollectValues", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(collectMethod);
            collectMethod.Invoke(mgr, null);

            Assert.AreEqual(1f, mgr.GetValue(MetaEffectType.BonusProductionWorkers));
            Assert.AreEqual(1f, mgr.GetValue(MetaEffectType.BonusTowerCrewSlots));
            Assert.AreEqual(4f, mgr.GetValue(MetaEffectType.RuneTowerLimit));
            Assert.IsTrue(mgr.IsUnlocked(MetaEffectType.UnlockRuneFusion));
            Assert.IsTrue(mgr.IsUnlocked(MetaEffectType.UnlockRuneTransmutation));
            Assert.IsTrue(mgr.IsUnlocked(MetaEffectType.UnlockCursedCrafting));
            Assert.IsTrue(mgr.IsUnlocked(MetaEffectType.UnlockRuneRarity_Legendary));
            Assert.IsTrue(mgr.IsUnlocked(MetaEffectType.UnlockRuneRarity_Cursed));
            Assert.IsFalse(mgr.IsUnlocked(MetaEffectType.WallSystem_Solution2), "Mury musza pozostac wylaczone");
            Assert.IsFalse(mgr.IsUnlocked(MetaEffectType.UnlockDruidTower), "Wieza Druida musi pozostac zablokowana");

            Object.DestroyImmediate(wallUpgrade);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
