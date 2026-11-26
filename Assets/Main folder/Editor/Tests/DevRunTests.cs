using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class DevRunTests
{
    private readonly List<Object> createdObjects = new List<Object>();
    private string tempSavePath;

    [SetUp]
    public void SetUp()
    {
        DevRunConfig.ClearTestOverride();
        tempSavePath = Path.Combine(Path.GetTempPath(), $"dev_run_test_{System.Guid.NewGuid():N}.json");
    }

    [TearDown]
    public void TearDown()
    {
        DevRunConfig.ClearTestOverride();

        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }
        createdObjects.Clear();

        if (!string.IsNullOrEmpty(tempSavePath) && File.Exists(tempSavePath))
        {
            File.Delete(tempSavePath);
        }
    }

    private MetaUpgradeSO CreateUpgrade(string id, MetaEffectType effectType, float value = 1f, bool hasVariants = false, bool isVariantOption = false)
    {
        var so = ScriptableObject.CreateInstance<MetaUpgradeSO>();
        so.id = id;
        so.upgradeName = id;
        so.effectType = effectType;
        so.effectValue = value;
        so.hasVariants = hasVariants;
        so.isVariantOption = isVariantOption;
        createdObjects.Add(so);
        return so;
    }

    private TowerData CreateTower(string assetName, string buildingName, bool initialUnlocked)
    {
        var tower = ScriptableObject.CreateInstance<TowerData>();
        tower.name = assetName;
        tower.buildingName = buildingName;
        tower.isUnlocked = initialUnlocked;
        createdObjects.Add(tower);
        return tower;
    }

    [Test]
    public void AllMode_UnlocksAllNodesExceptVariantB_AndUnlocksAllTowers()
    {
        var startWood = CreateUpgrade("wood_1", MetaEffectType.StartingWood, 50f);
        var trinity = CreateUpgrade("trinity_force", MetaEffectType.UnlockTrinityForce);
        var varA = CreateUpgrade("tower_upg_1_a", MetaEffectType.TowerGlobalDamage, 5f, isVariantOption: true);
        var varB = CreateUpgrade("tower_upg_1_b", MetaEffectType.TowerGlobalFireRate, 0.2f, isVariantOption: true);
        var parentVar = CreateUpgrade("tower_upg_1", MetaEffectType.None, 0f, hasVariants: true);
        parentVar.variantA = varA;
        parentVar.variantB = varB;

        var starter = CreateTower("1. Sentinel Arbalest", "Arbalet", false);
        var extraTower = CreateTower("3. Steam Repeater", "Repetier Parowy", false);

        var upgrades = new List<MetaUpgradeSO> { startWood, trinity, parentVar, varA, varB };
        var towers = new List<TowerData> { starter, extraTower };

        SaveData save = DevRunConfig.BuildInMemorySaveData(upgrades, towers, SaveManager.DefaultStarterTowerIDs, DevRunMode.All);

        Assert.IsTrue(startWood.isUnlocked);
        Assert.IsTrue(trinity.isUnlocked);
        Assert.IsTrue(parentVar.isUnlocked);
        Assert.IsTrue(varA.isUnlocked, "Variant A should be unlocked in All mode.");
        Assert.IsFalse(varB.isUnlocked, "Variant B should remain locked in All mode to avoid doubling stats.");
        Assert.IsTrue(starter.isUnlocked);
        Assert.IsTrue(extraTower.isUnlocked);
        Assert.IsFalse(save.unlockedUpgradeIDs.Contains(varB.id));
    }

    [Test]
    public void KeyNodesMode_UnlocksOnlyBuildingsAndRuneSystems()
    {
        var trinity = CreateUpgrade("trinity_force", MetaEffectType.UnlockTrinityForce);
        var runeLimit = CreateUpgrade("krasnoludy_wieza_1", MetaEffectType.RuneTowerLimit, 1f);
        var draftTower = CreateUpgrade("nowa_wieza_1", MetaEffectType.TowerDraftUnlock);
        var runeRare = CreateUpgrade("krasnoludy_run_1", MetaEffectType.UnlockRuneRarity_Rare);
        var runeFusion = CreateUpgrade("krasnoludy_kuznia_1", MetaEffectType.UnlockRuneFusion);
        var runeTransmute = CreateUpgrade("krasnoludy_kuznia_2", MetaEffectType.UnlockRuneTransmutation);
        var runeCursedCraft = CreateUpgrade("krasnoludy_run_4", MetaEffectType.UnlockCursedCrafting);

        var startWood = CreateUpgrade("wood_1", MetaEffectType.StartingWood, 50f);
        var towerDmg = CreateUpgrade("tower_dmg", MetaEffectType.TowerGlobalDamage, 10f);
        var wall = CreateUpgrade("wall_1", MetaEffectType.WallSystem_Solution2);
        var chunk = CreateUpgrade("chunk_1", MetaEffectType.UniqueChunkUnlock);

        var starter = CreateTower("1. Sentinel Arbalest", "Arbalet", false);
        var extraTower = CreateTower("5. Shadow Spire", "Iglica Cieni", false);

        var upgrades = new List<MetaUpgradeSO>
        {
            trinity, runeLimit, draftTower, runeRare, runeFusion, runeTransmute, runeCursedCraft,
            startWood, towerDmg, wall, chunk
        };
        var towers = new List<TowerData> { starter, extraTower };

        DevRunConfig.BuildInMemorySaveData(upgrades, towers, SaveManager.DefaultStarterTowerIDs, DevRunMode.KeyNodes);

        Assert.IsTrue(trinity.isUnlocked);
        Assert.IsTrue(runeLimit.isUnlocked);
        Assert.IsTrue(draftTower.isUnlocked);
        Assert.IsTrue(runeRare.isUnlocked);
        Assert.IsTrue(runeFusion.isUnlocked);
        Assert.IsTrue(runeTransmute.isUnlocked);
        Assert.IsTrue(runeCursedCraft.isUnlocked);

        Assert.IsFalse(startWood.isUnlocked);
        Assert.IsFalse(towerDmg.isUnlocked);
        Assert.IsFalse(wall.isUnlocked);
        Assert.IsFalse(chunk.isUnlocked);

        Assert.IsTrue(starter.isUnlocked);
        Assert.IsTrue(extraTower.isUnlocked);
    }

    [Test]
    public void NoneMode_LocksAllUpgrades_AndKeepsOnlyStarterTowers()
    {
        var trinity = CreateUpgrade("trinity_force", MetaEffectType.UnlockTrinityForce);
        trinity.isUnlocked = true;
        var runeRare = CreateUpgrade("krasnoludy_run_1", MetaEffectType.UnlockRuneRarity_Rare);
        runeRare.isUnlocked = true;

        var starterA = CreateTower("1. Sentinel Arbalest", "Arbalet", false);
        var starterB = CreateTower("6. Royal Bombard", "Bombarda", false);
        var nonStarter = CreateTower("7. Soul Monolith", "Monolit dusz", true);

        DevRunConfig.SetTestOverride(true, DevRunMode.None);

        var saveGo = new GameObject("TestSaveManager");
        createdObjects.Add(saveGo);
        var saveMgr = saveGo.AddComponent<SaveManager>();
        saveMgr.SavePath = tempSavePath;
        saveMgr.unlockAllTowersForDebug = true;
        saveMgr.allTowers = new List<TowerData> { starterA, starterB, nonStarter };

        saveMgr.SyncUpgradesWithSave(new List<MetaUpgradeSO> { trinity, runeRare });
        saveMgr.SyncTowersWithSave(saveMgr.allTowers);

        Assert.IsFalse(trinity.isUnlocked);
        Assert.IsFalse(runeRare.isUnlocked);
        Assert.IsTrue(starterA.isUnlocked);
        Assert.IsTrue(starterB.isUnlocked);
        Assert.IsFalse(nonStarter.isUnlocked, "Non-starter tower must remain locked in None mode even if unlockAllTowersForDebug is true.");
        Assert.IsFalse(File.Exists(tempSavePath), "SaveManager must not write to disk while DEV RUN is active.");
    }

    [Test]
    public void MetaUpgradeManager_InjectsDruidTowerUnlock_BasedOnDevRunMode()
    {
        var metaGo = new GameObject("TestMetaUpgradeManager");
        createdObjects.Add(metaGo);
        var mgr = metaGo.AddComponent<MetaUpgradeManager>();
        mgr.allUpgrades = new List<MetaUpgradeSO>();

        DevRunConfig.SetTestOverride(true, DevRunMode.KeyNodes);
        mgr.ReapplyAllUpgrades();
        Assert.IsTrue(mgr.IsUnlocked(MetaEffectType.UnlockDruidTower));

        DevRunConfig.SetTestOverride(true, DevRunMode.All);
        mgr.ReapplyAllUpgrades();
        Assert.IsTrue(mgr.IsUnlocked(MetaEffectType.UnlockDruidTower));

        DevRunConfig.SetTestOverride(true, DevRunMode.None);
        mgr.ReapplyAllUpgrades();
        Assert.IsFalse(mgr.IsUnlocked(MetaEffectType.UnlockDruidTower));
    }

    [Test]
    public void AllAndKeyNodesModes_SkipPlaceholderNodes()
    {
        var placeholderByCat = CreateUpgrade("elfy_chunki_1", MetaEffectType.StartingWood, 50f);
        placeholderByCat.category = MetaUpgradeCategory.PLACEHOLDER;

        var placeholderByEffect = CreateUpgrade("krasnoludy_3", MetaEffectType.Placeholder, 1f);
        placeholderByEffect.category = MetaUpgradeCategory.Runes;

        var upgrades = new List<MetaUpgradeSO> { placeholderByCat, placeholderByEffect };
        var towers = new List<TowerData>();

        SaveData allSave = DevRunConfig.BuildInMemorySaveData(upgrades, towers, SaveManager.DefaultStarterTowerIDs, DevRunMode.All);
        Assert.IsFalse(placeholderByCat.isUnlocked, "PLACEHOLDER node must stay locked in All mode.");
        Assert.IsFalse(placeholderByEffect.isUnlocked, "Placeholder effect node must stay locked in All mode.");
        Assert.IsFalse(allSave.unlockedUpgradeIDs.Contains(placeholderByCat.id));
        Assert.IsFalse(allSave.unlockedUpgradeIDs.Contains(placeholderByEffect.id));

        DevRunConfig.BuildInMemorySaveData(upgrades, towers, SaveManager.DefaultStarterTowerIDs, DevRunMode.KeyNodes);
        Assert.IsFalse(placeholderByCat.isUnlocked, "PLACEHOLDER node must stay locked in KeyNodes mode.");
        Assert.IsFalse(placeholderByEffect.isUnlocked, "Placeholder effect node must stay locked in KeyNodes mode.");

        var metaGo = new GameObject("TestMetaManager_Placeholder");
        createdObjects.Add(metaGo);
        var mgr = metaGo.AddComponent<MetaUpgradeManager>();
        placeholderByCat.isUnlocked = true;
        placeholderByEffect.isUnlocked = true;
        mgr.allUpgrades = upgrades;
        mgr.ReapplyAllUpgrades();

        Assert.AreEqual(0f, mgr.GetValue(MetaEffectType.StartingWood), 0.001f);
        Assert.IsFalse(mgr.IsUnlocked(MetaEffectType.Placeholder));
    }
}
