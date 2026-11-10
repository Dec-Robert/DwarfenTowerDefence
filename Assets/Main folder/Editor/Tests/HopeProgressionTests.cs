using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Zautomatyzowane testy jednostkowe i integracyjne dla systemu przydzielania meta progresji (Nadziei)
/// oraz managera HopeSessionManager i konfiguracji HopeMilestoneConfigSO.
/// </summary>
[TestFixture]
public class HopeProgressionTests
{
    private GameObject testGo;
    private GameObject saveGo;
    private HopeSessionManager sessionManager;
    private HopeMilestoneConfigSO config;
    private SaveManager saveManager;
    private string tempSavePath;

    [SetUp]
    public void SetUp()
    {
        tempSavePath = Path.Combine(Path.GetTempPath(), $"hope_tests_save_{Guid.NewGuid():N}.json");

        // Konfiguracja SaveManager
        saveGo = new GameObject("Test_SaveManager");
        saveManager = saveGo.AddComponent<SaveManager>();
        saveManager.SavePath = tempSavePath;
        saveManager.currentSaveData = new SaveData { totalHope = 100 };
        SetStaticProperty(typeof(SaveManager), "Instance", saveManager);

        // Tworzenie HopeMilestoneConfigSO
        config = ScriptableObject.CreateInstance<HopeMilestoneConfigSO>();
        config.milestones = new List<HopeMilestoneConfigSO.MilestoneGoal>
        {
            // Bosses
            new() { id = "boss_1", description = "Boss 1", type = MilestoneType.BossesKilled, targetValue = 1, hopeReward = 50 },
            new() { id = "boss_2", description = "Boss 2", type = MilestoneType.BossesKilled, targetValue = 2, hopeReward = 200 },
            new() { id = "boss_3", description = "Boss 3", type = MilestoneType.BossesKilled, targetValue = 3, hopeReward = 500 },
            new() { id = "boss_4", description = "Boss 4", type = MilestoneType.BossesKilled, targetValue = 4, hopeReward = 600 },

            // Victory
            new() { id = "victory", description = "Zwycięstwo", type = MilestoneType.Victory, targetValue = 1, hopeReward = 1500 },

            // Expeditions
            new() { id = "exp_2", description = "2 Ekspedycje", type = MilestoneType.ExpeditionsCompleted, targetValue = 2, hopeReward = 25 },
            new() { id = "exp_5", description = "5 Ekspedycji", type = MilestoneType.ExpeditionsCompleted, targetValue = 5, hopeReward = 80 },
            new() { id = "exp_10", description = "10 Ekspedycji", type = MilestoneType.ExpeditionsCompleted, targetValue = 10, hopeReward = 200 },
            new() { id = "exp_15", description = "15 Ekspedycji", type = MilestoneType.ExpeditionsCompleted, targetValue = 15, hopeReward = 350 },

            // Kills
            new() { id = "kills_100", description = "100 Wrogów", type = MilestoneType.EnemiesKilled, targetValue = 100, hopeReward = 20 },
            new() { id = "kills_500", description = "500 Wrogów", type = MilestoneType.EnemiesKilled, targetValue = 500, hopeReward = 60 },
            new() { id = "kills_1500", description = "1500 Wrogów", type = MilestoneType.EnemiesKilled, targetValue = 1500, hopeReward = 150 },
            new() { id = "kills_3000", description = "3000 Wrogów", type = MilestoneType.EnemiesKilled, targetValue = 3000, hopeReward = 250 },

            // Population
            new() { id = "pop_15", description = "15 Populacji", type = MilestoneType.TotalPopulation, targetValue = 15, hopeReward = 20 },
            new() { id = "pop_30", description = "30 Populacji", type = MilestoneType.TotalPopulation, targetValue = 30, hopeReward = 60 },
            new() { id = "pop_60", description = "60 Populacji", type = MilestoneType.TotalPopulation, targetValue = 60, hopeReward = 150 },
            new() { id = "pop_100", description = "100 Populacji", type = MilestoneType.TotalPopulation, targetValue = 100, hopeReward = 250 },

            // Towers
            new() { id = "towers_5", description = "5 Wież", type = MilestoneType.TotalTowers, targetValue = 5, hopeReward = 15 },
            new() { id = "towers_12", description = "12 Wież", type = MilestoneType.TotalTowers, targetValue = 12, hopeReward = 50 },
            new() { id = "towers_25", description = "25 Wież", type = MilestoneType.TotalTowers, targetValue = 25, hopeReward = 120 },
            new() { id = "towers_40", description = "40 Wież", type = MilestoneType.TotalTowers, targetValue = 40, hopeReward = 250 },

            // Production
            new() { id = "prod_4", description = "4 Budynki Prod", type = MilestoneType.ProductionBuildingsBuilt, targetValue = 4, hopeReward = 15 },
            new() { id = "prod_8", description = "8 Budynków Prod", type = MilestoneType.ProductionBuildingsBuilt, targetValue = 8, hopeReward = 50 },
            new() { id = "prod_15", description = "15 Budynków Prod", type = MilestoneType.ProductionBuildingsBuilt, targetValue = 15, hopeReward = 120 },

            // Runes
            new() { id = "rune_craft_1", description = "1 Runa", type = MilestoneType.RuneCraftingDone, targetValue = 1, hopeReward = 20 },
            new() { id = "rune_craft_5", description = "5 Run", type = MilestoneType.RuneCraftingDone, targetValue = 5, hopeReward = 60 },
            new() { id = "rune_craft_15", description = "15 Run", type = MilestoneType.RuneCraftingDone, targetValue = 15, hopeReward = 150 },
            new() { id = "rune_rare_1", description = "Rzadka Runa", type = MilestoneType.LegendaryOrCursedRunesObtained, targetValue = 1, hopeReward = 60 },
            new() { id = "rune_legendary_1", description = "Legendarna Runa", type = MilestoneType.LegendaryOrCursedRunesObtained, targetValue = 2, hopeReward = 200 }
        };

        // Tworzenie HopeSessionManager
        testGo = new GameObject("Test_HopeSessionManager");
        sessionManager = testGo.AddComponent<HopeSessionManager>();
        sessionManager.milestoneConfig = config;
        sessionManager.currentMultiplier = 1.0f;
    }

    [TearDown]
    public void TearDown()
    {
        if (testGo != null) UnityEngine.Object.DestroyImmediate(testGo);
        if (saveGo != null) UnityEngine.Object.DestroyImmediate(saveGo);
        if (config != null) UnityEngine.Object.DestroyImmediate(config);

        SetStaticProperty(typeof(SaveManager), "Instance", null);
        SetStaticProperty(typeof(HopeSessionManager), "Instance", null);

        if (File.Exists(tempSavePath))
        {
            try { File.Delete(tempSavePath); } catch { }
        }
    }

    [Test]
    public void WavePointsSchedule_CalculatesExpectedBaselines()
    {
        Assert.AreEqual(20, HopeSessionManager.CalculateCumulativeWavePoints(15), "Do fali 15 powinno być dokładnie 20 pkt za fale.");
        Assert.AreEqual(30, HopeSessionManager.CalculateCumulativeWavePoints(20), "Do fali 20 powinno być dokładnie 30 pkt za fale.");
        Assert.AreEqual(370, HopeSessionManager.CalculateCumulativeWavePoints(40), "Do fali 40 powinno być dokładnie 370 pkt za fale.");
        Assert.AreEqual(670, HopeSessionManager.CalculateCumulativeWavePoints(50), "Do fali 50 powinno być dokładnie 670 pkt za fale.");
    }

    [Test]
    public void SimulateRun_Wave15_Approximates185Hope()
    {
        // Symulacja wczesnego runu do 15 fali:
        // 15 nocy przetrwanych
        for (int i = 1; i <= 15; i++)
        {
            sessionManager.AddRawHope(HopeSessionManager.GetPointsForWave(i), $"Noc {i}");
        }
        // 1 Boss
        sessionManager.OnEnemyKilled(EnemyRank.Boss);
        // 100 wrogów zwykłych
        for (int i = 0; i < 99; i++) sessionManager.OnEnemyKilled(EnemyRank.Normal);
        // 15 populacji
        InvokeCitizenSpawned(15);
        // 2 ekspedycje
        sessionManager.OnExpeditionCompleted();
        sessionManager.OnExpeditionCompleted();
        // 1 wykuta runa
        sessionManager.OnRuneCrafted();

        HopeSessionManager.HopeSessionSummary summary = sessionManager.EndSession(isVictory: false);

        Assert.AreEqual(185, summary.finalTotalHope, "Run do 15 fali powinien dać dokładnie 185 Nadziei.");
        Assert.IsFalse(summary.isVictory);
    }

    [Test]
    public void SimulateRun_Wave40_Reaches3000HopeTarget()
    {
        // 40 nocy
        for (int i = 1; i <= 40; i++)
        {
            sessionManager.AddRawHope(HopeSessionManager.GetPointsForWave(i), $"Noc {i}");
        } // 370

        // 3 Bossów (50 + 200 + 500 = 750)
        sessionManager.OnEnemyKilled(EnemyRank.Boss);
        sessionManager.OnEnemyKilled(EnemyRank.Boss);
        sessionManager.OnEnemyKilled(EnemyRank.Boss);

        // 1500 wrogów (20 + 60 + 150 = 230)
        for (int i = 0; i < 1497; i++) sessionManager.OnEnemyKilled(EnemyRank.Normal);

        // 60 populacji (20 + 60 + 150 = 230)
        InvokeCitizenSpawned(60);

        // 10 ekspedycji (25 + 80 + 200 = 305)
        for (int i = 0; i < 10; i++) sessionManager.OnExpeditionCompleted();

        // 25 wież (15 + 50 + 120 = 185)
        SimulateTowersBuilt(25);

        // 8 budynków produkcyjnych (15 + 50 = 65)
        SimulateProductionBuildingsBuilt(8);

        // Runy (1 + 5 run wykutych = 20 + 60 = 80; rzadka + legendarna = 60 + 200 = 260 -> łącznie 340)
        for (int i = 0; i < 5; i++) sessionManager.OnRuneCrafted();
        sessionManager.OnRuneObtained(RuneRarity.Rare);
        sessionManager.OnRuneObtained(RuneRarity.Legendary);

        // Ewentualnie 5 run dodatkowych lub lekka kalibracja
        HopeSessionManager.HopeSessionSummary summary = sessionManager.EndSession(isVictory: false);

        // Sprawdzamy czy suma oscyluje wokół docelowych 3000 pkt (z tolerancją +/- 5%)
        Assert.GreaterOrEqual(summary.finalTotalHope, 2800);
        Assert.LessOrEqual(summary.finalTotalHope, 3200);
    }

    [Test]
    public void SimulateRun_Wave50_Victory_Reaches5500HopeTarget()
    {
        // 50 nocy
        for (int i = 1; i <= 50; i++)
        {
            sessionManager.AddRawHope(HopeSessionManager.GetPointsForWave(i), $"Noc {i}");
        } // 670

        // 4 Bossów (50 + 200 + 500 + 600 = 1350)
        for (int i = 0; i < 4; i++) sessionManager.OnEnemyKilled(EnemyRank.Boss);

        // 3000 wrogów (20 + 60 + 150 + 250 = 480)
        for (int i = 0; i < 2996; i++) sessionManager.OnEnemyKilled(EnemyRank.Normal);

        // 100 populacji (20 + 60 + 150 + 250 = 480)
        InvokeCitizenSpawned(100);

        // 15 ekspedycji (25 + 80 + 200 + 350 = 655)
        for (int i = 0; i < 15; i++) sessionManager.OnExpeditionCompleted();

        // 40 wież (15 + 50 + 120 + 250 = 435)
        SimulateTowersBuilt(40);

        // 15 budynków produkcyjnych (15 + 50 + 120 = 185)
        SimulateProductionBuildingsBuilt(15);

        // Runy (15 wykutych + rare + legendary = 20 + 60 + 150 + 60 + 200 = 490)
        for (int i = 0; i < 15; i++) sessionManager.OnRuneCrafted();
        sessionManager.OnRuneObtained(RuneRarity.Rare);
        sessionManager.OnRuneObtained(RuneRarity.Legendary);

        // Zwycięstwo (1500)
        HopeSessionManager.HopeSessionSummary summary = sessionManager.EndSession(isVictory: true);

        Assert.IsTrue(summary.isVictory);
        // Oczekiwana suma: ~5745 pkt (cel: >= 5500)
        Assert.GreaterOrEqual(summary.finalTotalHope, 5500);
    }

    [Test]
    public void EndSession_PersistsTotalHope_ToSaveManager()
    {
        int initialHope = saveManager.currentSaveData.totalHope;

        sessionManager.AddRawHope(500, "Bonus");
        HopeSessionManager.HopeSessionSummary summary = sessionManager.EndSession(isVictory: false);

        Assert.AreEqual(500, summary.finalTotalHope);
        Assert.AreEqual(initialHope + 500, saveManager.currentSaveData.totalHope, "SaveData.totalHope powinno powiększyć się o wynik sesji.");
    }

    [Test]
    public void Milestones_DoNotAwardDuplicates_InSingleRun()
    {
        // Zabijamy pierwszego bossa
        sessionManager.OnEnemyKilled(EnemyRank.Boss);
        int pointsAfterFirst = sessionManager.milestonePointsEarned;
        Assert.AreEqual(50, pointsAfterFirst);

        // Kolejny normalny kill nie powinien powielić nagrody pierwszego bossa
        sessionManager.OnEnemyKilled(EnemyRank.Normal);
        Assert.AreEqual(50, sessionManager.milestonePointsEarned);
    }

    // --- METODY POMOCNICZE ---

    private void InvokeCitizenSpawned(int targetCount)
    {
        // Wywołujemy ewaluację przez OnCitizenSpawned lub refleksję
        var evalMethod = typeof(HopeSessionManager).GetMethod("EvaluateMilestones", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        evalMethod?.Invoke(sessionManager, new object[] { MilestoneType.TotalPopulation, targetCount });
    }

    private void SimulateTowersBuilt(int count)
    {
        var evalMethod = typeof(HopeSessionManager).GetMethod("EvaluateMilestones", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        evalMethod?.Invoke(sessionManager, new object[] { MilestoneType.TotalTowers, count });
    }

    private void SimulateProductionBuildingsBuilt(int count)
    {
        var evalMethod = typeof(HopeSessionManager).GetMethod("EvaluateMilestones", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        evalMethod?.Invoke(sessionManager, new object[] { MilestoneType.ProductionBuildingsBuilt, count });
    }

    private static void SetStaticProperty(Type type, string propertyName, object value)
    {
        var prop = type.GetProperty(propertyName, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(null, value);
        }
    }
}
