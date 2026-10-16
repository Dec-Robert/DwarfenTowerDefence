using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public class StageOneImplementationVerificationTests
{
    private List<GameObject> createdObjects;

    [SetUp]
    public void SetUp()
    {
        createdObjects = new List<GameObject>();
    }

    [TearDown]
    public void TearDown()
    {
        if (createdObjects != null)
        {
            for (int i = 0; i < createdObjects.Count; i++)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }
            createdObjects.Clear();
        }
    }

    // =========================================================================
    // 1. TOWER REBALANCING & UNLOCKS
    // =========================================================================

    [Test]
    public void Test01_SentinelArbalest_RangeReducedBy20Percent_AndUnlocked()
    {
        TowerData data = AssetDatabase.LoadAssetAtPath<TowerData>("Assets/Main folder/ScriptableObjects/Towers/1. Sentinel Arbalest.asset");
        Assert.IsNotNull(data, "1. Sentinel Arbalest.asset should exist");
        Assert.IsTrue(data.isUnlocked, "Sentinel Arbalest should be unlocked");
        Assert.AreEqual(12f, data.baseRange, 0.01f, "Sentinel Arbalest baseRange should be 12 (reduced by 20% from 15)");
    }

    [Test]
    public void Test02_RoyalBombard_RangeReducedBy20Percent_AndUnlocked()
    {
        TowerData data = AssetDatabase.LoadAssetAtPath<TowerData>("Assets/Main folder/ScriptableObjects/Towers/6. Royal Bombard.asset");
        Assert.IsNotNull(data, "6. Royal Bombard.asset should exist");
        Assert.IsTrue(data.isUnlocked, "Royal Bombard should be unlocked");
        Assert.AreEqual(8f, data.baseRange, 0.01f, "Royal Bombard baseRange should be 8 (reduced by 20% from 10)");
    }

    [Test]
    public void Test03_SteamRepeater_BalancingAndCosts_Accurate()
    {
        TowerData data = AssetDatabase.LoadAssetAtPath<TowerData>("Assets/Main folder/ScriptableObjects/Towers/3. Steam Repeater.asset");
        Assert.IsNotNull(data, "3. Steam Repeater.asset should exist");
        Assert.IsTrue(data.isUnlocked, "Steam Repeater should be unlocked");
        Assert.AreEqual(10.4f, data.baseRange, 0.01f, "Steam Repeater baseRange should be 10.4 (-20% from 13)");
        Assert.AreEqual(12f, data.baseDamage, 0.01f, "Steam Repeater baseDamage should be 12");
        Assert.AreEqual(7.5f, data.fireRate, 0.01f, "Steam Repeater fireRate should be 7.5");

        // Costs: 12 Ether, 10 Stone, 6 Coal, 4 Iron
        Assert.IsNotNull(data.constructionCost, "Construction cost should exist");
        Dictionary<ResourceType, int> costs = new Dictionary<ResourceType, int>();
        foreach (var c in data.constructionCost) costs[c.type] = c.amount;

        Assert.AreEqual(12, costs.GetValueOrDefault(ResourceType.Ether, 0), "Cost Ether should be 12");
        Assert.AreEqual(10, costs.GetValueOrDefault(ResourceType.Stone, 0), "Cost Stone should be 10");
        Assert.AreEqual(6, costs.GetValueOrDefault(ResourceType.Coal, 0), "Cost Coal should be 6");
        Assert.AreEqual(4, costs.GetValueOrDefault(ResourceType.Iron, 0), "Cost Iron should be 4");

        // Upkeep: 1 Stone, 2 Coal
        Dictionary<ResourceType, int> upkeep = new Dictionary<ResourceType, int>();
        foreach (var u in data.upkeepPerCycle) upkeep[u.type] = u.amount;
        Assert.AreEqual(1, upkeep.GetValueOrDefault(ResourceType.Stone, 0), "Upkeep Stone should be 1");
        Assert.AreEqual(2, upkeep.GetValueOrDefault(ResourceType.Coal, 0), "Upkeep Coal should be 2");
    }

    [Test]
    public void Test04_ShadowSpire_BalancingAndCosts_Accurate()
    {
        TowerData data = AssetDatabase.LoadAssetAtPath<TowerData>("Assets/Main folder/ScriptableObjects/Towers/5. Shadow Spire.asset");
        Assert.IsNotNull(data, "5. Shadow Spire.asset should exist");
        Assert.IsTrue(data.isUnlocked, "Shadow Spire should be unlocked");
        Assert.AreEqual(19.2f, data.baseRange, 0.01f, "Shadow Spire baseRange should be 19.2 (-20% from 24)");
        Assert.AreEqual(140f, data.baseDamage, 0.01f, "Shadow Spire baseDamage should be 140");
        Assert.AreEqual(0.5f, data.fireRate, 0.01f, "Shadow Spire fireRate should be 0.5");
        Assert.AreEqual(0.20f, data.criticalChancel, 0.01f, "Shadow Spire crit chance should be 0.20");
        Assert.AreEqual(2.5f, data.criticalDamageMultiplier, 0.01f, "Shadow Spire crit mult should be 2.5");
        Assert.AreEqual(DamageType.Magic, data.damageType, "Shadow Spire damageType should be Magic (1)");

        // Costs: 18 Ether, 25 Wood, 15 Stone, 6 Iron
        Dictionary<ResourceType, int> costs = new Dictionary<ResourceType, int>();
        foreach (var c in data.constructionCost) costs[c.type] = c.amount;

        Assert.AreEqual(18, costs.GetValueOrDefault(ResourceType.Ether, 0), "Cost Ether should be 18");
        Assert.AreEqual(25, costs.GetValueOrDefault(ResourceType.Wood, 0), "Cost Wood should be 25");
        Assert.AreEqual(15, costs.GetValueOrDefault(ResourceType.Stone, 0), "Cost Stone should be 15");
        Assert.AreEqual(6, costs.GetValueOrDefault(ResourceType.Iron, 0), "Cost Iron should be 6");

        // Upkeep: 3 Wood, 1 Ether
        Dictionary<ResourceType, int> upkeep = new Dictionary<ResourceType, int>();
        foreach (var u in data.upkeepPerCycle) upkeep[u.type] = u.amount;
        Assert.AreEqual(3, upkeep.GetValueOrDefault(ResourceType.Wood, 0), "Upkeep Wood should be 3");
        Assert.AreEqual(1, upkeep.GetValueOrDefault(ResourceType.Ether, 0), "Upkeep Ether should be 1");
    }

    [Test]
    public void Test05_OtherTowers_RemainLocked()
    {
        TowerData t2 = AssetDatabase.LoadAssetAtPath<TowerData>("Assets/Main folder/ScriptableObjects/Towers/2. Thunderbuss.asset");
        TowerData t4 = AssetDatabase.LoadAssetAtPath<TowerData>("Assets/Main folder/ScriptableObjects/Towers/4. Crucible of Cinders.asset");
        TowerData t7 = AssetDatabase.LoadAssetAtPath<TowerData>("Assets/Main folder/ScriptableObjects/Towers/7. Soul Monolith.asset");

        Assert.IsNotNull(t2, "2. Thunderbuss.asset should exist");
        Assert.IsNotNull(t4, "4. Crucible of Cinders.asset should exist");
        Assert.IsNotNull(t7, "7. Soul Monolith.asset should exist");

        Assert.IsFalse(t2.isUnlocked, "Thunderbuss should be locked");
        Assert.IsFalse(t4.isUnlocked, "Crucible of Cinders should be locked");
        Assert.IsFalse(t7.isUnlocked, "Soul Monolith should be locked");
    }

    // =========================================================================
    // 2. NEW ENEMIES & STATS
    // =========================================================================

    [Test]
    public void Test06_Abberracja_StatsAndHyperArmorSkill_Correct()
    {
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Main folder/ScriptableObjects/Enemies/Abberracja.asset");
        Assert.IsNotNull(data, "Abberracja.asset should exist");
        Assert.AreEqual("Abberracja", data.enemyName);
        Assert.AreEqual(380f, data.baseHp, 0.01f);
        Assert.AreEqual(0.75f, data.moveSpeed, 0.01f);
        Assert.AreEqual(50f, data.armor, 0.01f);
        Assert.AreEqual(0f, data.magicResist, 0.01f);
        Assert.AreEqual(0f, data.dodgeChance, 0.01f);
        Assert.AreEqual(4, data.threatCost);

        Assert.IsNotNull(data.skillPrefabs);
        Assert.AreEqual(1, data.skillPrefabs.Count);
        HyperArmorSkill skill = data.skillPrefabs[0].GetComponent<HyperArmorSkill>();
        Assert.IsNotNull(skill, "Abberracja skill should be HyperArmorSkill");
        Assert.AreEqual(8, skill.charges, "HyperArmorSkill charges must be 8");
    }

    [Test]
    public void Test07_MistStalker_StatsAndNoSkills_Correct()
    {
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Main folder/ScriptableObjects/Enemies/MistStalker.asset");
        Assert.IsNotNull(data, "MistStalker.asset should exist");
        Assert.AreEqual("Mist Stalker", data.enemyName);
        Assert.AreEqual(140f, data.baseHp, 0.01f);
        Assert.AreEqual(2.0f, data.moveSpeed, 0.01f);
        Assert.AreEqual(0f, data.armor, 0.01f);
        Assert.AreEqual(35f, data.magicResist, 0.01f);
        Assert.AreEqual(25f, data.dodgeChance, 0.01f);
        Assert.AreEqual(2, data.threatCost);

        Assert.IsTrue(data.skillPrefabs == null || data.skillPrefabs.Count == 0, "Mist Stalker must have no active skills");
    }

    [Test]
    public void Test08_HeraldOfTheMist_BossRankAndMaddeningGazeOnly_Correct()
    {
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Main folder/ScriptableObjects/Enemies/HeraldOfTheMist.asset");
        Assert.IsNotNull(data, "HeraldOfTheMist.asset should exist");
        Assert.AreEqual("Herald of the mist", data.enemyName);
        Assert.AreEqual(1500f, data.baseHp, 0.01f);
        Assert.AreEqual(0.65f, data.moveSpeed, 0.01f);
        Assert.AreEqual(30f, data.armor, 0.01f);
        Assert.AreEqual(30f, data.magicResist, 0.01f);
        Assert.AreEqual(0f, data.dodgeChance, 0.01f);
        Assert.AreEqual(10, data.threatCost);
        Assert.AreEqual(EnemyRank.Boss, data.rank, "Herald rank should be Boss");
        Assert.IsTrue(data.isBoss, "Herald isBoss should be true");

        Assert.IsNotNull(data.skillPrefabs);
        Assert.AreEqual(1, data.skillPrefabs.Count, "Herald should only have 1 skill (Maddening Gaze, no Barrier)");
        MaddeningGazeSkill gaze = data.skillPrefabs[0].GetComponent<MaddeningGazeSkill>();
        Assert.IsNotNull(gaze, "Herald skill should be MaddeningGazeSkill");
        Assert.AreEqual(8f, gaze.auraRadius, 0.01f, "MaddeningGazeSkill auraRadius must be 8");
    }

    // =========================================================================
    // 3. SKILL BEHAVIOR VERIFICATION
    // =========================================================================

    [Test]
    public void Test09_HyperArmorSkill_BlocksExact8Hits_ThenAllowsDamage()
    {
        GameObject go = new GameObject("TestEnemy_HyperArmor");
        createdObjects.Add(go);

        HyperArmorSkill skill = go.AddComponent<HyperArmorSkill>();
        skill.charges = 8;

        for (int i = 1; i <= 8; i++)
        {
            float dmg = skill.OnBeforeDamageCalculation(50f, DamageType.Physical);
            Assert.AreEqual(0f, dmg, $"Hit #{i} should be completely blocked by HyperArmor");
            Assert.AreEqual(8 - i, skill.charges, $"Charges remaining after hit #{i}");
        }

        // 9th hit should go through
        float unmitigated = skill.OnBeforeDamageCalculation(50f, DamageType.Physical);
        Assert.AreEqual(50f, unmitigated, "Hit #9 should not be blocked after charges depleted");
    }

    [Test]
    public void Test10_MaddeningGazeSkill_FearsTowersInRadius_AndClearsOnOutOfRange()
    {
        // Utwórz wieżę w zasięgu (odległość 4)
        GameObject towerNearGo = new GameObject("TowerNear");
        towerNearGo.transform.position = new Vector3(4f, 0f, 0f);
        TowerEntity towerNearEntity = towerNearGo.AddComponent<TowerEntity>();
        TowerController towerNearCtrl = towerNearGo.AddComponent<TowerController>();
        towerNearEntity.controller = towerNearCtrl;
        createdObjects.Add(towerNearGo);

        // Utwórz wieżę poza zasięgiem (odległość 15)
        GameObject towerFarGo = new GameObject("TowerFar");
        towerFarGo.transform.position = new Vector3(15f, 0f, 0f);
        TowerEntity towerFarEntity = towerFarGo.AddComponent<TowerEntity>();
        TowerController towerFarCtrl = towerFarGo.AddComponent<TowerController>();
        towerFarEntity.controller = towerFarCtrl;
        createdObjects.Add(towerFarGo);

        // Utwórz wroga z MaddeningGazeSkill
        GameObject bossGo = new GameObject("BossHerald");
        bossGo.transform.position = Vector3.zero;
        MaddeningGazeSkill gaze = bossGo.AddComponent<MaddeningGazeSkill>();
        gaze.auraRadius = 8f;
        createdObjects.Add(bossGo);

        // Wykonaj skanowanie
        gaze.ScanTowers();

        Assert.IsTrue(towerNearCtrl.isFeared, "Near tower (dist 4 <= 8) should be feared");
        Assert.IsFalse(towerFarCtrl.isFeared, "Far tower (dist 15 > 8) should NOT be feared");

        // Przestaw wroga daleko
        bossGo.transform.position = new Vector3(100f, 0f, 0f);
        gaze.ScanTowers();

        Assert.IsFalse(towerNearCtrl.isFeared, "Near tower should recover when boss moves away");
        Assert.AreEqual(0, gaze.FearedTowers.Count, "No towers should be feared when all are out of range");
    }

    // =========================================================================
    // 4. PREDEFINED WAVES 7-20 VERIFICATION
    // =========================================================================

    [Test]
    public void Test11_Waves7To10_ContainNewEnemiesAndWave10Boss()
    {
        WaveDefinition w7 = AssetDatabase.LoadAssetAtPath<WaveDefinition>("Assets/Main folder/ScriptableObjects/PredefinedWaves/Wave day 7.asset");
        WaveDefinition w10 = AssetDatabase.LoadAssetAtPath<WaveDefinition>("Assets/Main folder/ScriptableObjects/PredefinedWaves/Wave day 10.asset");

        Assert.IsNotNull(w7, "Wave day 7 should exist");
        Assert.IsNotNull(w10, "Wave day 10 should exist");

        bool w7HasNewEnemy = false;
        foreach (var group in w7.enemies)
        {
            if (group.enemy != null && (group.enemy.enemyName == "Abberracja" || group.enemy.enemyName == "Mist Stalker"))
            {
                w7HasNewEnemy = true;
                break;
            }
        }
        Assert.IsTrue(w7HasNewEnemy, "Wave day 7 should contain newly added enemies");

        bool w10HasHerald = false;
        foreach (var group in w10.enemies)
        {
            if (group.enemy != null && group.enemy.isBoss)
            {
                w10HasHerald = true;
                break;
            }
        }
        Assert.IsTrue(w10HasHerald, "Wave day 10 must contain Herald of the mist (boss)");
    }

    [Test]
    public void Test12_Waves11To20_ExistAndWave20HasFinalBoss()
    {
        for (int day = 11; day <= 20; day++)
        {
            string path = $"Assets/Main folder/ScriptableObjects/PredefinedWaves/Wave day {day}.asset";
            WaveDefinition wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path);
            Assert.IsNotNull(wave, $"Wave day {day}.asset should exist");
            Assert.AreEqual(day, wave.dayNumber, $"Wave day {day} should have dayNumber {day}");
            Assert.IsNotNull(wave.enemies, $"Wave day {day} should have enemies list");
            Assert.Greater(wave.enemies.Count, 0, $"Wave day {day} should have at least one enemy group");
        }

        WaveDefinition w20 = AssetDatabase.LoadAssetAtPath<WaveDefinition>("Assets/Main folder/ScriptableObjects/PredefinedWaves/Wave day 20.asset");
        bool w20HasBoss = false;
        foreach (var group in w20.enemies)
        {
            if (group.enemy != null && group.enemy.isBoss)
            {
                w20HasBoss = true;
                break;
            }
        }
        Assert.IsTrue(w20HasBoss, "Wave day 20 must feature boss encounter");
    }

    // =========================================================================
    // 5. UI CONTROLLERS & GEMINI.MD STANDARDS
    // =========================================================================

    [Test]
    public void Test13_MainMenuDemoUI_Has4GuideCardsWithExpectedHeaders()
    {
        GameObject go = new GameObject("MainMenuDemoUI_Test");
        createdObjects.Add(go);

        MainMenuDemoUI ui = go.AddComponent<MainMenuDemoUI>();
        Assert.AreEqual(4, ui.CardsCount, "MainMenuDemoUI must have exactly 4 guide cards");
        Assert.AreEqual("Cykl Dnia", ui.Cards[0].tabTitle);
        Assert.AreEqual("Rasy i Ekspedycje", ui.Cards[1].tabTitle);
        Assert.AreEqual("Palenisko", ui.Cards[2].tabTitle);
        Assert.AreEqual("Wieże i Amunicja", ui.Cards[3].tabTitle);

        ui.ShowCard(1);
        Assert.AreEqual(1, ui.CurrentCardIndex);
        ui.NextCard();
        Assert.AreEqual(2, ui.CurrentCardIndex);
        ui.PrevCard();
        Assert.AreEqual(1, ui.CurrentCardIndex);
    }

    [Test]
    public void Test14_CanvasMainMenuUI_PauseAndResumeStateToggle()
    {
        GameObject go = new GameObject("CanvasMainMenuUI_Test");
        createdObjects.Add(go);

        CanvasMainMenuUI ui = go.AddComponent<CanvasMainMenuUI>();
        Assert.IsFalse(ui.IsPaused, "Initial state should be unpaused");

        ui.PauseGame();
        Assert.IsTrue(ui.IsPaused, "State should be paused after PauseGame()");
        Assert.AreEqual(0f, Time.timeScale, 0.001f, "Time.timeScale should be 0 when paused");

        ui.ResumeGame();
        Assert.IsFalse(ui.IsPaused, "State should be unpaused after ResumeGame()");
        Assert.AreEqual(1f, Time.timeScale, 0.001f, "Time.timeScale should be restored after ResumeGame()");
    }
}
