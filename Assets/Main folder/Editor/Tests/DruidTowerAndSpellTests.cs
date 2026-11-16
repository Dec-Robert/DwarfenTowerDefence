using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class DruidTowerAndSpellTests
{
    private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in createdObjects)
        {
            if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
        }
        createdObjects.Clear();
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        createdObjects.Add(obj);
        return obj;
    }

    [Test]
    public void SpawnChance_FollowsDayCurve_10PercentPerDayAfterDay10()
    {
        Assert.AreEqual(0, DruidTowerSpawner.CalculateSpawnChance(1));
        Assert.AreEqual(0, DruidTowerSpawner.CalculateSpawnChance(10));
        Assert.AreEqual(10, DruidTowerSpawner.CalculateSpawnChance(11));
        Assert.AreEqual(20, DruidTowerSpawner.CalculateSpawnChance(12));
        Assert.AreEqual(50, DruidTowerSpawner.CalculateSpawnChance(15));
        Assert.AreEqual(100, DruidTowerSpawner.CalculateSpawnChance(20));
        Assert.AreEqual(100, DruidTowerSpawner.CalculateSpawnChance(25));
    }

    [Test]
    public void SelectTargetCellInChunk_PrefersEmptyGrassland_AndNeverPicksRoad()
    {
        var chunk = new Dictionary<Vector2Int, HexCellData>
        {
            { new Vector2Int(0, 0), new HexCellData { localCoord = new Vector2Int(0, 0), isRoad = true, feature = HexFeatureType.None, elevation = 0 } },
            { new Vector2Int(1, 0), new HexCellData { localCoord = new Vector2Int(1, 0), isRoad = false, feature = HexFeatureType.Forest, elevation = 1 } },
            { new Vector2Int(0, 1), new HexCellData { localCoord = new Vector2Int(0, 1), isRoad = false, feature = HexFeatureType.None, elevation = 0 } }
        };

        var rng = new System.Random(42);
        var chosen = DruidTowerSpawner.SelectTargetCellInChunk(chunk, rng);
        Assert.IsNotNull(chosen);
        Assert.AreEqual(new Vector2Int(0, 1), chosen.localCoord);

        // Usuwamy puste pole Grassland -> powinien wybrac Forest (nigdy droge)
        chunk.Remove(new Vector2Int(0, 1));
        chosen = DruidTowerSpawner.SelectTargetCellInChunk(chunk, rng);
        Assert.IsNotNull(chosen);
        Assert.IsFalse(chosen.isRoad);
        Assert.AreEqual(new Vector2Int(1, 0), chosen.localCoord);
    }

    [Test]
    public void SpellTreeAndMoneySink_ProgressionAndStatScaling()
    {
        var cityConfig = Track(ScriptableObject.CreateInstance<CityBaseConfigSO>());
        cityConfig.startEther = 0;
        cityConfig.startWood = 0;
        cityConfig.startStone = 0;
        cityConfig.startIron = 0;
        cityConfig.startCoal = 0;
        cityConfig.startFood = 0;

        var rmGo = Track(new GameObject("TestResourceManager"));
        var rm = rmGo.AddComponent<ResourceManager>();
        typeof(ResourceManager)
            .GetField("cityConfig", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(rm, cityConfig);
        InvokePrivateAwake(rm);
        rm.AddResource(ResourceType.Ether, 1000);
        rm.AddResource(ResourceType.Wood, 1000);
        rm.AddResource(ResourceType.Stone, 1000);
        rm.AddResource(ResourceType.Iron, 1000);
        rm.AddResource(ResourceType.Coal, 1000);

        var towerGo = Track(new GameObject("TestDruidTower"));
        var tower = towerGo.AddComponent<DruidTowerEntity>();
        InvokePrivateAwake(tower);
        tower.EnsureDatabase();

        static void InvokePrivateAwake(object target)
        {
            target.GetType()
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(target, null);
        }

        // Symulacja stanu po drafcie i wybraniu Fireballa
        tower.SetStateForTesting(DruidTowerState.RebuiltPendingDraft);
        tower.DraftedOptions.Add(SpellDataSO.CreateDefaultFireball());
        tower.DraftedOptions.Add(SpellDataSO.CreateDefaultFrostNova());
        Assert.IsTrue(tower.SelectDraftedSpell(0));
        Assert.AreEqual("fireball", tower.ActiveSpell.spellId);

        // Specjalizacja powinna być zablokowana przed ukończeniem 4 poziomów liniowych
        Assert.IsFalse(tower.CanSelectSpecialization(0));

        float baseDmg = tower.GetEffectiveDamage();
        for (int i = 0; i < 4; i++)
        {
            Assert.IsTrue(tower.TryUpgradeNextLinearTier(), $"Tier {i} upgrade failed");
        }
        Assert.IsTrue(tower.IsLinearTreeCompleted());
        Assert.Greater(tower.GetEffectiveDamage(), baseDmg);

        // Wybór specjalizacji A (Niszcząca Eksplozja +100% DMG)
        float preSpecDmg = tower.GetEffectiveDamage();
        Assert.IsTrue(tower.TrySelectSpecialization(0));
        Assert.IsTrue(tower.IsTreeCompleted());
        Assert.Greater(tower.GetEffectiveDamage(), preSpecDmg);

        // Money Sink: 20 Eteru * 5 pkt = 100 pkt -> dokładnie 1 poziom DMG
        tower.TargetMoneySinkStat = MoneySinkTarget.Damage;
        float preSinkDmg = tower.GetEffectiveDamage();
        Assert.IsTrue(tower.TryContributeToMoneySink(ResourceType.Ether, 20));
        Assert.AreEqual(1, tower.MoneySinkDamageLevels);
        Assert.Greater(tower.GetEffectiveDamage(), preSinkDmg);
    }

    [Test]
    public void SpellDatabase_DrawsTwoDistinctSpells()
    {
        var db = Track(SpellDatabaseSO.CreateRuntimeDefault());
        var drawn = db.DrawRandomSpells(2, new System.Random(123));
        Assert.AreEqual(2, drawn.Count);
        Assert.AreNotEqual(drawn[0].spellId, drawn[1].spellId);
    }

    [Test]
    public void RebuildCosts_DefaultContains40Ether_AndRejectsWhenMissingAnyResource()
    {
        var cityConfig = Track(ScriptableObject.CreateInstance<CityBaseConfigSO>());
        cityConfig.startEther = 0;
        cityConfig.startWood = 0;
        cityConfig.startStone = 0;
        cityConfig.startIron = 0;
        cityConfig.startCoal = 0;
        cityConfig.startFood = 0;

        var rmGo = Track(new GameObject("TestResourceManager_Rebuild"));
        var rm = rmGo.AddComponent<ResourceManager>();
        typeof(ResourceManager)
            .GetField("cityConfig", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.SetValue(rm, cityConfig);
        typeof(ResourceManager)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(rm, null);

        var towerGo = Track(new GameObject("TestDruidTower_Rebuild"));
        var tower = towerGo.AddComponent<DruidTowerEntity>();

        Assert.IsNotNull(tower.rebuildCosts);
        Assert.AreEqual(1, tower.rebuildCosts.Count);
        Assert.AreEqual(ResourceType.Ether, tower.rebuildCosts[0].type);
        Assert.AreEqual(40, tower.rebuildCosts[0].amount);

        // Nawet przy braku zasiedlenia CanRebuild zwraca false
        Assert.IsFalse(tower.CanRebuild(out _));
        Assert.IsFalse(tower.TryRebuild());
    }
}
