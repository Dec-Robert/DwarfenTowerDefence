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
        Assert.GreaterOrEqual(wavesMigrated, 50, "At least 50 WaveDefinition assets should be migrated");
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

    [Test]
    public void Test09_50Waves_IntegrityAndSequence()
    {
        string[] waveGuids = AssetDatabase.FindAssets("t:WaveDefinition", new[] { "Assets/Main folder/ScriptableObjects/PredefinedWaves" });
        Assert.AreEqual(50, waveGuids.Length, "Must have exactly 50 wave assets");

        var loadedDays = new HashSet<int>();
        for (int day = 1; day <= 50; day++)
        {
            string path = $"Assets/Main folder/ScriptableObjects/PredefinedWaves/Wave day {day}.asset";
            var wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path);
            Assert.IsNotNull(wave, $"Wave day {day} asset should exist at path: {path}");
            Assert.AreEqual(day, wave.dayNumber, $"Wave day {day} should have dayNumber == {day}");
            loadedDays.Add(wave.dayNumber);

            var entries = wave.GetAllEntries();
            Assert.IsNotNull(entries, $"Entries should not be null for day {day}");
            Assert.Greater(entries.Count, 0, $"Wave day {day} must have at least one enemy entry");

            // Verify no null enemy references and valid tiers
            foreach (var e in entries)
            {
                Assert.IsNotNull(e.enemy, $"Enemy reference in day {day} must not be null");
                Assert.GreaterOrEqual(e.Tier, 1, $"Tier must be >= 1 in day {day}");
                Assert.LessOrEqual(e.Tier, 4, $"Tier must be <= 4 in day {day}");
                Assert.Greater(e.count, 0, $"Count must be > 0 in day {day}");
            }
        }

        Assert.AreEqual(50, loadedDays.Count, "All 50 days from 1 to 50 must be unique and present");
    }

    [Test]
    public void Test10_50Waves_BossAppearsOn15_30_50()
    {
        for (int day = 1; day <= 50; day++)
        {
            string path = $"Assets/Main folder/ScriptableObjects/PredefinedWaves/Wave day {day}.asset";
            var wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path);
            var entries = wave.GetAllEntries();

            bool hasBoss = entries.Exists(e => e.enemy != null && (e.enemy.rank == EnemyRank.Boss || e.enemy.isBoss));

            if (day == 15)
            {
                Assert.IsTrue(hasBoss, "Day 15 must contain Boss Herald of the Mist");
                var bossEntry = entries.Find(e => e.enemy != null && (e.enemy.rank == EnemyRank.Boss || e.enemy.isBoss));
                Assert.AreEqual(1, bossEntry.Tier, "Day 15 Boss must be Tier 1");
            }
            else if (day == 30)
            {
                Assert.IsTrue(hasBoss, "Day 30 must contain Boss Herald of the Mist");
                var bossEntry = entries.Find(e => e.enemy != null && (e.enemy.rank == EnemyRank.Boss || e.enemy.isBoss));
                Assert.AreEqual(2, bossEntry.Tier, "Day 30 Boss must be Tier 2");
            }
            else if (day == 50)
            {
                Assert.IsTrue(hasBoss, "Day 50 must contain Boss Herald of the Mist");
                var bossEntry = entries.Find(e => e.enemy != null && (e.enemy.rank == EnemyRank.Boss || e.enemy.isBoss));
                Assert.AreEqual(3, bossEntry.Tier, "Day 50 Boss must be Tier 3");
            }
            else
            {
                Assert.IsFalse(hasBoss, $"Day {day} must NOT contain a Boss");
            }
        }
    }

    [Test]
    public void Test11_50Waves_EtherPoolAnchorPointsAndMonotonicity()
    {
        int prevPool = 0;
        for (int day = 1; day <= 50; day++)
        {
            string path = $"Assets/Main folder/ScriptableObjects/PredefinedWaves/Wave day {day}.asset";
            var wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path);

            Assert.GreaterOrEqual(wave.etherPool, 1, "etherPool must be >= 1");
            Assert.LessOrEqual(wave.etherPool, 32, "etherPool must be <= 32");
            Assert.GreaterOrEqual(wave.etherPool, prevPool, $"etherPool must be monotonic at day {day}");

            // Anchor points check: (1, 1), (10, 6), (20, 12), (40, 22), (45, 32), (50, 32)
            if (day == 1) Assert.AreEqual(1, wave.etherPool);
            if (day == 10) Assert.AreEqual(6, wave.etherPool);
            if (day == 20) Assert.AreEqual(12, wave.etherPool);
            if (day == 40) Assert.AreEqual(22, wave.etherPool);
            if (day == 45) Assert.AreEqual(32, wave.etherPool);
            if (day == 50) Assert.AreEqual(32, wave.etherPool);

            prevPool = wave.etherPool;
        }
    }

    [Test]
    public void Test12_50Waves_EliteRulesAndNoPredefinedElites()
    {
        for (int day = 1; day <= 50; day++)
        {
            string path = $"Assets/Main folder/ScriptableObjects/PredefinedWaves/Wave day {day}.asset";
            var wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path);
            var entries = wave.GetAllEntries();

            // No predefined elites anywhere
            foreach (var e in entries)
            {
                Assert.IsFalse(e.forceElite, $"Day {day} has forceElite=true which is forbidden (elites must spawn naturally)");
            }

            // Days 1..19: allowRandomElites = false
            if (day < 20)
            {
                Assert.IsFalse(wave.allowRandomElites, $"Day {day} must not allow random elites");
                Assert.AreEqual(0, wave.maxElitesCount, $"Day {day} maxElitesCount should be 0");
            }
            else
            {
                // Days 20..50: allowRandomElites = true
                Assert.IsTrue(wave.allowRandomElites, $"Day {day} must allow random elites");
                Assert.Greater(wave.maxElitesCount, 0, $"Day {day} maxElitesCount must be > 0");
            }
        }
    }

    [Test]
    public void Test13_50Waves_ThreatBudgetAndSpawnGroups()
    {
        float prevThreat = 0f;
        for (int day = 1; day <= 50; day++)
        {
            string path = $"Assets/Main folder/ScriptableObjects/PredefinedWaves/Wave day {day}.asset";
            var wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path);

            Assert.IsNotNull(wave.spawnGroups, $"Wave day {day} should have spawnGroups");
            Assert.Greater(wave.spawnGroups.Count, 0, $"Wave day {day} should have at least 1 spawnGroup");

            float threat = wave.CalculateThreatBudget();
            Assert.Greater(threat, 0f, $"Wave day {day} threat budget should be positive");

            float duration = wave.GetEstimatedSpawnDuration();
            Assert.Greater(duration, 0f, $"Wave day {day} spawn duration should be positive");

            // Breather waves check: days 16 and 31 have lower threat than boss days 15 and 30
            if (day == 16)
            {
                string path15 = $"Assets/Main folder/ScriptableObjects/PredefinedWaves/Wave day 15.asset";
                var wave15 = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path15);
                Assert.Less(threat, wave15.CalculateThreatBudget(), "Day 16 breather wave must have lower threat than Day 15 Boss");
            }
            if (day == 31)
            {
                string path30 = $"Assets/Main folder/ScriptableObjects/PredefinedWaves/Wave day 30.asset";
                var wave30 = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path30);
                Assert.Less(threat, wave30.CalculateThreatBudget(), "Day 31 breather wave must have lower threat than Day 30 Boss");
            }

            // Milestone difficulty spikes check: Day 24 and Day 38
            if (day == 24)
            {
                string path23 = $"Assets/Main folder/ScriptableObjects/PredefinedWaves/Wave day 23.asset";
                var wave23 = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path23);
                Assert.Greater(threat, wave23.CalculateThreatBudget(), "Day 24 difficulty spike must be greater than Day 23");
            }
            if (day == 38)
            {
                string path37 = $"Assets/Main folder/ScriptableObjects/PredefinedWaves/Wave day 37.asset";
                var wave37 = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path37);
                Assert.Greater(threat, wave37.CalculateThreatBudget(), "Day 38 difficulty spike must be greater than Day 37");
            }

            prevThreat = threat;
        }
    }

    [Test]
    public void Test14_EnemyData_TierSpecificSkillsAndOverrides()
    {
        var enemy = ScriptableObject.CreateInstance<EnemyData>();
        var fallbackSkill = new GameObject("FallbackSkill");
        var customSkillT1 = new GameObject("CustomSkillT1");
        var customSkillT2 = new GameObject("CustomSkillT2");

        enemy.skillPrefabs = new List<GameObject> { fallbackSkill };
        enemy.EnsureTiers();

        // 1. Domyślnie brak override -> wszystkie tiery zwracają fallback
        Assert.IsFalse(enemy.GetTier(1).overrideSkills);
        Assert.AreEqual(1, enemy.GetSkillPrefabsForTier(1).Count);
        Assert.AreEqual(fallbackSkill, enemy.GetSkillPrefabsForTier(1)[0]);
        Assert.AreEqual(fallbackSkill, enemy.GetSkillPrefabsForTier(2)[0]);

        // 2. Włączenie override dla T1 i T2
        enemy.GetTier(1).overrideSkills = true;
        enemy.GetTier(1).skillPrefabs = new List<GameObject> { customSkillT1 };

        enemy.GetTier(2).overrideSkills = true;
        enemy.GetTier(2).skillPrefabs = new List<GameObject> { customSkillT2 };

        // 3. T3 i T4 nadal dziedziczą fallback
        Assert.AreEqual(customSkillT1, enemy.GetSkillPrefabsForTier(1)[0]);
        Assert.AreEqual(customSkillT2, enemy.GetSkillPrefabsForTier(2)[0]);
        Assert.AreEqual(fallbackSkill, enemy.GetSkillPrefabsForTier(3)[0]);
        Assert.AreEqual(fallbackSkill, enemy.GetSkillPrefabsForTier(4)[0]);

        // 4. Wyciszenie umiejętności (override = true, ale pusta lista)
        enemy.GetTier(3).overrideSkills = true;
        enemy.GetTier(3).skillPrefabs = new List<GameObject>();
        Assert.AreEqual(0, enemy.GetSkillPrefabsForTier(3).Count);

        Object.DestroyImmediate(fallbackSkill);
        Object.DestroyImmediate(customSkillT1);
        Object.DestroyImmediate(customSkillT2);
    }

    [Test]
    public void Test15_Abberracja_T1toT4_HyperArmorSkills_Assigned()
    {
        var enemy = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Main folder/ScriptableObjects/Enemies/Abberracja.asset");
        Assert.IsNotNull(enemy, "Abberracja.asset should exist");

        // Weryfikacja wstecznej kompatybilności fallbacku
        Assert.IsNotNull(enemy.skillPrefabs);
        Assert.AreEqual(1, enemy.skillPrefabs.Count);
        Assert.IsNotNull(enemy.skillPrefabs[0]);

        // Weryfikacja tierów 1-4 z dedykowanymi umiejętnościami HyperArmorSkillT1..T4
        string[] expectedSkillNames = { "HyperArmorSkillT1", "HyperArmorSkillT2", "HyperArmorSkillT3", "HyperArmorSkillT4" };

        for (int tier = 1; tier <= 4; tier++)
        {
            var tierStats = enemy.GetTier(tier);
            Assert.IsNotNull(tierStats, $"Tier {tier} should not be null");
            Assert.IsTrue(tierStats.overrideSkills, $"Tier {tier} should have overrideSkills = true");

            var skills = enemy.GetSkillPrefabsForTier(tier);
            Assert.IsNotNull(skills, $"Tier {tier} skills should not be null");
            Assert.AreEqual(1, skills.Count, $"Tier {tier} should have exactly 1 skill prefab");
            Assert.IsNotNull(skills[0], $"Tier {tier} skill prefab should not be null");
            Assert.AreEqual(expectedSkillNames[tier - 1], skills[0].name, $"Tier {tier} should have skill {expectedSkillNames[tier - 1]}");
        }
    }

    [Test]
    public void Test16_Blob_T1toT4_BlobRefacturingSkills_Assigned()
    {
        var enemy = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Main folder/ScriptableObjects/Enemies/Blob.asset");
        Assert.IsNotNull(enemy, "Blob.asset should exist");

        // Fallback powinen wskazywać na T1
        Assert.IsNotNull(enemy.skillPrefabs);
        Assert.AreEqual(1, enemy.skillPrefabs.Count);
        Assert.IsNotNull(enemy.skillPrefabs[0]);
        Assert.AreEqual("BlobRefacturingSkillT1", enemy.skillPrefabs[0].name);

        string[] expectedSkillNames = { "BlobRefacturingSkillT1", "BlobRefacturingSkillT2", "BlobRefacturingSkillT3", "BlobRefacturingSkillT4" };
        int[] expectedChildren = { 2, 2, 3, 3 };
        float[] expectedHpPercent = { 0.5f, 0.7f, 0.7f, 0.9f };

        for (int tier = 1; tier <= 4; tier++)
        {
            var tierStats = enemy.GetTier(tier);
            Assert.IsNotNull(tierStats, $"Tier {tier} should not be null");
            Assert.IsTrue(tierStats.overrideSkills, $"Tier {tier} should have overrideSkills = true");

            var skills = enemy.GetSkillPrefabsForTier(tier);
            Assert.IsNotNull(skills, $"Tier {tier} skills should not be null");
            Assert.AreEqual(1, skills.Count, $"Tier {tier} should have exactly 1 skill prefab");
            Assert.IsNotNull(skills[0], $"Tier {tier} skill prefab should not be null");
            Assert.AreEqual(expectedSkillNames[tier - 1], skills[0].name);

            var splitSkill = skills[0].GetComponent<BlobSplitSkill>();
            Assert.IsNotNull(splitSkill, $"Prefab {skills[0].name} must have BlobSplitSkill component");
            Assert.AreEqual(expectedChildren[tier - 1], splitSkill.amountOfChildren);
            Assert.AreEqual(expectedHpPercent[tier - 1], splitSkill.childHpPercent, 0.01f);
            Assert.AreEqual(0.7f, splitSkill.scaleReduction, 0.01f);
        }
    }

    [Test]
    public void Test17_Arcanist_T1toT4_PointDefenceSkills_Assigned()
    {
        var enemy = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Main folder/ScriptableObjects/Enemies/Arcanist.asset");
        Assert.IsNotNull(enemy, "Arcanist.asset should exist");

        // Fallback powinen wskazywać na T1
        Assert.IsNotNull(enemy.skillPrefabs);
        Assert.AreEqual(1, enemy.skillPrefabs.Count);
        Assert.IsNotNull(enemy.skillPrefabs[0]);
        Assert.AreEqual("PointDefenceSkillT1", enemy.skillPrefabs[0].name);

        string[] expectedSkillNames = { "PointDefenceSkillT1", "PointDefenceSkillT2", "PointDefenceSkillT3", "PointDefenceSkillT4" };
        float[] expectedRadius = { 4f, 5f, 6f, 6f };
        float[] expectedCooldown = { 1f, 0.8f, 0.8f, 0.6f };

        for (int tier = 1; tier <= 4; tier++)
        {
            var tierStats = enemy.GetTier(tier);
            Assert.IsNotNull(tierStats, $"Tier {tier} should not be null");
            Assert.IsTrue(tierStats.overrideSkills, $"Tier {tier} should have overrideSkills = true");

            var skills = enemy.GetSkillPrefabsForTier(tier);
            Assert.IsNotNull(skills, $"Tier {tier} skills should not be null");
            Assert.AreEqual(1, skills.Count, $"Tier {tier} should have exactly 1 skill prefab");
            Assert.IsNotNull(skills[0], $"Tier {tier} skill prefab should not be null");
            Assert.AreEqual(expectedSkillNames[tier - 1], skills[0].name);

            var pdSkill = skills[0].GetComponent<PointDefenseSkill>();
            Assert.IsNotNull(pdSkill, $"Prefab {skills[0].name} must have PointDefenseSkill component");
            Assert.AreEqual(expectedRadius[tier - 1], pdSkill.radius, 0.01f);
            Assert.AreEqual(expectedCooldown[tier - 1], pdSkill.cooldown, 0.01f);
        }
    }
}
