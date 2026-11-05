using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class EnemyTierAndWaveTests
{
    [Test]
    public void Test01_EnemyData_TiersInitializationAndGetTier()
    {
        EnemyData enemy = ScriptableObject.CreateInstance<EnemyData>();
        enemy.enemyName = "TestEnemy";
        enemy.baseHp = 250f;
        enemy.moveSpeed = 2.5f;
        enemy.armor = 20f;
        enemy.magicResist = 15f;
        enemy.dodgeChance = 10f;

        enemy.EnsureTiers();

        Assert.IsNotNull(enemy.tiers);
        Assert.AreEqual(4, enemy.tiers.Length);

        // Clamp checks
        Assert.AreEqual(enemy.tiers[0], enemy.GetTier(1));
        Assert.AreEqual(enemy.tiers[0], enemy.GetTier(0)); // clamped to 1
        Assert.AreEqual(enemy.tiers[0], enemy.GetTier(-5)); // clamped to 1
        Assert.AreEqual(enemy.tiers[3], enemy.GetTier(4));
        Assert.AreEqual(enemy.tiers[3], enemy.GetTier(10)); // clamped to 4

        // Check values copied to tier 1
        Assert.AreEqual(250f, enemy.GetTier(1).baseHp);
        Assert.AreEqual(2.5f, enemy.GetTier(1).moveSpeed);
        Assert.AreEqual(20f, enemy.GetTier(1).armor);
        Assert.AreEqual(15f, enemy.GetTier(1).magicResist);
        Assert.AreEqual(10f, enemy.GetTier(1).dodgeChance);
    }

    [Test]
    public void Test02_EnemyData_TierSpecificPrefabAndSprite()
    {
        EnemyData enemy = ScriptableObject.CreateInstance<EnemyData>();
        GameObject defaultPrefab = new GameObject("DefaultPrefab");
        GameObject t2Prefab = new GameObject("T2Prefab");

        enemy.prefab = defaultPrefab;
        enemy.EnsureTiers();

        // Tier 1 uses default
        Assert.AreEqual(defaultPrefab, enemy.GetPrefabForTier(1));

        // Tier 2 has custom prefab
        enemy.GetTier(2).prefab = t2Prefab;
        Assert.AreEqual(t2Prefab, enemy.GetPrefabForTier(2));

        // Tier 3 falls back to default if null
        enemy.GetTier(3).prefab = null;
        Assert.AreEqual(defaultPrefab, enemy.GetPrefabForTier(3));

        Object.DestroyImmediate(defaultPrefab);
        Object.DestroyImmediate(t2Prefab);
    }

    [Test]
    public void Test03_WaveEntry_TierClamping()
    {
        var entryDefault = new WaveDefinition.WaveEntry();
        Assert.AreEqual(1, entryDefault.Tier, "Uninitialized tier should default to 1");

        var entryExplicitT2 = new WaveDefinition.WaveEntry { tier = 2 };
        Assert.AreEqual(2, entryExplicitT2.Tier);

        var entryOutOfRangeHigh = new WaveDefinition.WaveEntry { tier = 99 };
        Assert.AreEqual(4, entryOutOfRangeHigh.Tier);

        var entryOutOfRangeLow = new WaveDefinition.WaveEntry { tier = -1 };
        Assert.AreEqual(1, entryOutOfRangeLow.Tier);
    }

    [Test]
    public void Test04_Spawner_FindWaveForDay_EndlessScalingFormula()
    {
        GameObject spawnerGo = new GameObject("TestSpawner");
        EnemySpawner spawner = spawnerGo.AddComponent<EnemySpawner>();

        WaveDefinition w1 = ScriptableObject.CreateInstance<WaveDefinition>();
        w1.dayNumber = 1;
        WaveDefinition w2 = ScriptableObject.CreateInstance<WaveDefinition>();
        w2.dayNumber = 2;
        WaveDefinition w20 = ScriptableObject.CreateInstance<WaveDefinition>();
        w20.dayNumber = 20;

        spawner.predefinedWaves = new List<WaveDefinition> { w1, w2, w20 };

        // Dzień 1: dokładnie w1, daysBeyondLast = 0
        int daysBeyond;
        var foundW1 = spawner.FindWaveForDay(1, out daysBeyond);
        Assert.AreEqual(w1, foundW1);
        Assert.AreEqual(0, daysBeyond);

        // Dzień 5: brak fali, fallback do w2, daysBeyondLast = 0 (bo w zakresie <= 20)
        var foundW5 = spawner.FindWaveForDay(5, out daysBeyond);
        Assert.AreEqual(w2, foundW5);
        Assert.AreEqual(0, daysBeyond);

        // Dzień 21: a = 21 - 20 = 1, fala w20
        var foundW21 = spawner.FindWaveForDay(21, out daysBeyond);
        Assert.AreEqual(w20, foundW21);
        Assert.AreEqual(1, daysBeyond);
        Assert.AreEqual(1.1f, Mathf.Pow(spawner.lateScalingBase, daysBeyond), 0.001f);

        // Dzień 22: a = 22 - 20 = 2, fala w20, mnożnik 1.21
        var foundW22 = spawner.FindWaveForDay(22, out daysBeyond);
        Assert.AreEqual(w20, foundW22);
        Assert.AreEqual(2, daysBeyond);
        Assert.AreEqual(1.21f, Mathf.Pow(spawner.lateScalingBase, daysBeyond), 0.001f);

        // Dzień 30: a = 30 - 20 = 10, fala w20, mnożnik 1.1^10 (~2.5937)
        var foundW30 = spawner.FindWaveForDay(30, out daysBeyond);
        Assert.AreEqual(w20, foundW30);
        Assert.AreEqual(10, daysBeyond);
        Assert.AreEqual(Mathf.Pow(1.1f, 10), Mathf.Pow(spawner.lateScalingBase, daysBeyond), 0.001f);

        Object.DestroyImmediate(spawnerGo);
    }

    [Test]
    public void Test05_EnemyStats_InitializeWithTiers()
    {
        EnemyData enemy = ScriptableObject.CreateInstance<EnemyData>();
        enemy.enemyName = "TierTester";
        enemy.EnsureTiers();

        // T1
        enemy.GetTier(1).baseHp = 100f;
        enemy.GetTier(1).moveSpeed = 2f;
        enemy.GetTier(1).armor = 10f;

        // T2
        enemy.GetTier(2).baseHp = 250f;
        enemy.GetTier(2).moveSpeed = 2.8f;
        enemy.GetTier(2).armor = 25f;

        GameObject enemyGo = new GameObject("EnemyInstance");
        EnemyStats stats = enemyGo.AddComponent<EnemyStats>();

        // Init as Tier 1
        stats.Initialize(enemy, 1, 1f, 1f);
        Assert.AreEqual(1, stats.Tier);
        Assert.AreEqual(100f, stats.GetMaxHealth(), 0.1f);
        Assert.AreEqual(0.1f, stats.CurrentArmor, 0.01f);

        // Init as Tier 2
        stats.Initialize(enemy, 2, 1f, 1f);
        Assert.AreEqual(2, stats.Tier);
        Assert.AreEqual(250f, stats.GetMaxHealth(), 0.1f);
        Assert.AreEqual(0.25f, stats.CurrentArmor, 0.01f);

        // Init as Tier 2 with late HP multiplier 1.21 (Day 22)
        stats.Initialize(enemy, 2, 1.21f, 1f);
        Assert.AreEqual(2, stats.Tier);
        Assert.AreEqual(250f * 1.21f, stats.GetMaxHealth(), 0.1f);

        Object.DestroyImmediate(enemyGo);
    }

    [Test]
    public void Test06_PerformMigrationOnProjectAssets_Successful()
    {
        int enemiesMigrated = EnemyTierMigrationTool.MigrateEnemies();
        int wavesMigrated = EnemyTierMigrationTool.MigrateWaves();

        Assert.GreaterOrEqual(enemiesMigrated, 6, "At least 6 EnemyData assets should be migrated");
        Assert.GreaterOrEqual(wavesMigrated, 20, "At least 20 WaveDefinition assets should be migrated");
    }

    [Test]
    public void Test07_EnemyTierStats_CalculateEffectiveHP()
    {
        var tier = new EnemyTierStats
        {
            baseHp = 100f,
            armor = 0f,
            magicResist = 0f,
            dodgeChance = 0f
        };

        // 1. Bez redukcji
        Assert.AreEqual(100f, tier.CalculatePhysicalEHP(), 0.01f);
        Assert.AreEqual(100f, tier.CalculateMagicEHP(), 0.01f);

        // 2. Sam pancerz (50% armor)
        tier.armor = 50f;
        Assert.AreEqual(200f, tier.CalculatePhysicalEHP(), 0.01f);
        Assert.AreEqual(100f, tier.CalculateMagicEHP(), 0.01f);

        // 3. Sama odporność na magię (20% MR)
        tier.armor = 0f;
        tier.magicResist = 20f;
        Assert.AreEqual(100f, tier.CalculatePhysicalEHP(), 0.01f);
        Assert.AreEqual(125f, tier.CalculateMagicEHP(), 0.01f);

        // 4. Sam unik (20% dodge)
        tier.magicResist = 0f;
        tier.dodgeChance = 20f;
        Assert.AreEqual(125f, tier.CalculatePhysicalEHP(), 0.01f);
        Assert.AreEqual(125f, tier.CalculateMagicEHP(), 0.01f);

        // 5. Kombinacja: Armor 50%, MR 25%, Dodge 20%
        tier.armor = 50f;
        tier.magicResist = 25f;
        tier.dodgeChance = 20f;
        // Phys: 100 / ((1 - 0.5) * (1 - 0.2)) = 100 / 0.4 = 250
        Assert.AreEqual(250f, tier.CalculatePhysicalEHP(), 0.01f);
        // Magic: 100 / ((1 - 0.25) * (1 - 0.2)) = 100 / 0.6 = 166.67
        Assert.AreEqual(166.67f, tier.CalculateMagicEHP(), 0.05f);

        // 6. Skrajność: 100% Armor lub 100% Dodge
        tier.armor = 100f;
        Assert.IsTrue(float.IsPositiveInfinity(tier.CalculatePhysicalEHP()));

        tier.armor = 0f;
        tier.dodgeChance = 100f;
        Assert.IsTrue(float.IsPositiveInfinity(tier.CalculatePhysicalEHP()));
        Assert.IsTrue(float.IsPositiveInfinity(tier.CalculateMagicEHP()));
    }

    [Test]
    public void Test08_EnemyData_CalculateEffectiveHP_MultiTier()
    {
        EnemyData enemy = ScriptableObject.CreateInstance<EnemyData>();
        enemy.EnsureTiers();

        // T1: 100 HP, 0% Armor, 0% MR, 0% Dodge
        enemy.GetTier(1).baseHp = 100f;
        enemy.GetTier(1).armor = 0f;
        enemy.GetTier(1).magicResist = 0f;
        enemy.GetTier(1).dodgeChance = 0f;

        // T2: 200 HP, 20% Armor, 10% MR, 10% Dodge
        enemy.GetTier(2).baseHp = 200f;
        enemy.GetTier(2).armor = 20f;
        enemy.GetTier(2).magicResist = 10f;
        enemy.GetTier(2).dodgeChance = 10f;

        Assert.AreEqual(100f, enemy.CalculatePhysicalEHP(1), 0.01f);
        Assert.AreEqual(100f, enemy.CalculateMagicEHP(1), 0.01f);

        // T2 Phys: 200 / ((1 - 0.2) * (1 - 0.1)) = 200 / (0.8 * 0.9) = 200 / 0.72 = 277.78
        Assert.AreEqual(277.78f, enemy.CalculatePhysicalEHP(2), 0.05f);
        // T2 Magic: 200 / ((1 - 0.1) * (1 - 0.1)) = 200 / (0.9 * 0.9) = 200 / 0.81 = 246.91
        Assert.AreEqual(246.91f, enemy.CalculateMagicEHP(2), 0.05f);
    }
}
