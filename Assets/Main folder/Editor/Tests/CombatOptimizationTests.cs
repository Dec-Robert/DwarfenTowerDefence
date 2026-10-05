using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public class CombatOptimizationTests
{
    private List<GameObject> testGameObjects;
    private Material testSharedMaterial;

    [SetUp]
    public void SetUp()
    {
        testGameObjects = new List<GameObject>();
        EnemyRegistry.Clear();

        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard")
                     ?? Shader.Find("Sprites/Default")
                     ?? Shader.Find("Unlit/Color");
        testSharedMaterial = new Material(shader != null ? shader : Shader.Find("Hidden/InternalErrorShader"));
        testSharedMaterial.name = "TestEnemyMasterMat";
    }

    [TearDown]
    public void TearDown()
    {
        EnemyRegistry.Clear();

        if (testGameObjects != null)
        {
            for (int i = 0; i < testGameObjects.Count; i++)
            {
                if (testGameObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(testGameObjects[i]);
                }
            }
            testGameObjects.Clear();
        }

        if (testSharedMaterial != null)
        {
            UnityEngine.Object.DestroyImmediate(testSharedMaterial);
            testSharedMaterial = null;
        }
    }

    // =========================================================================
    // FACTORY & REFLECTION HELPERS
    // =========================================================================

    private GameObject CreateEnemy(string name, Vector3 position, float hp, int progress, EnemyRank rank, Material mat = null)
    {
        GameObject go = new GameObject(name);
        go.transform.position = position;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat != null ? mat : testSharedMaterial;

        EnemyStats stats = go.AddComponent<EnemyStats>();
        stats.rank = rank;
        stats.SetHealthManually(hp);

        EnemyWalker walker = go.AddComponent<EnemyWalker>();
        SetWalkerProgress(walker, progress);

        InvokeAwake(stats);
        testGameObjects.Add(go);
        return go;
    }

    private TowerController CreateTower(Vector3 position, float range, params TargetingMode[] rules)
    {
        GameObject go = new GameObject("TestTower");
        go.transform.position = position;

        TowerEntity entity = go.AddComponent<TowerEntity>();
        TowerController tower = go.AddComponent<TowerController>();
        entity.controller = tower;

        tower.activeTargetingRules = new List<TargetingMode>(rules);
        tower.canShoot = true;
        SetTowerRange(tower, range);

        testGameObjects.Add(go);
        return tower;
    }

    private static void SetWalkerProgress(EnemyWalker walker, int progress)
    {
        FieldInfo field = typeof(EnemyWalker).GetField("targetIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(walker, progress);
    }

    private static void InvokeAwake(EnemyStats stats)
    {
        MethodInfo method = typeof(EnemyStats).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
        method?.Invoke(stats, null);
    }

    private static void InvokeOnEnable(EnemyStats stats)
    {
        MethodInfo method = typeof(EnemyStats).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance);
        method?.Invoke(stats, null);
    }

    private static void InvokeOnDisable(EnemyStats stats)
    {
        MethodInfo method = typeof(EnemyStats).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance);
        method?.Invoke(stats, null);
    }

    private static void InvokeFlashColor(EnemyStats stats, Color color, float duration)
    {
        MethodInfo method = typeof(EnemyStats).GetMethod("FlashColor", BindingFlags.NonPublic | BindingFlags.Instance);
        method?.Invoke(stats, new object[] { color, duration });
    }

    private static void SetTowerRange(TowerController tower, float range)
    {
        FieldInfo field = typeof(TowerController).GetField("currentRange", BindingFlags.NonPublic | BindingFlags.Instance);
        field?.SetValue(tower, range);
    }

    private static void InvokeUpdateTarget(TowerController tower)
    {
        MethodInfo method = typeof(TowerController).GetMethod("UpdateTarget", BindingFlags.NonPublic | BindingFlags.Instance);
        method?.Invoke(tower, null);
    }

    private static Transform GetTarget(TowerController tower)
    {
        FieldInfo field = typeof(TowerController).GetField("target", BindingFlags.NonPublic | BindingFlags.Instance);
        return (Transform)field?.GetValue(tower);
    }

    private static Transform GetForcedTarget(TowerController tower)
    {
        FieldInfo field = typeof(TowerController).GetField("forcedTarget", BindingFlags.NonPublic | BindingFlags.Instance);
        return (Transform)field?.GetValue(tower);
    }

    private static List<EnemyStats> GetBufferA(TowerController tower)
    {
        FieldInfo field = typeof(TowerController).GetField("candidateBufferA", BindingFlags.NonPublic | BindingFlags.Instance);
        return (List<EnemyStats>)field?.GetValue(tower);
    }

    private static List<EnemyStats> GetBufferB(TowerController tower)
    {
        FieldInfo field = typeof(TowerController).GetField("candidateBufferB", BindingFlags.NonPublic | BindingFlags.Instance);
        return (List<EnemyStats>)field?.GetValue(tower);
    }

    private static void InvokeApplyFilter(TowerController tower, List<EnemyStats> input, List<EnemyStats> output, TargetingMode mode)
    {
        MethodInfo method = typeof(TowerController).GetMethod("ApplyFilter", BindingFlags.NonPublic | BindingFlags.Instance);
        method?.Invoke(tower, new object[] { input, output, mode });
    }

    private static void InvokeSkillOnDisable(EnemySkill skill)
    {
        MethodInfo method = skill.GetType().GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance);
        method?.Invoke(skill, null);
    }

    private static void AddFearedTower(MaddeningGazeSkill skill, TowerController tower)
    {
        FieldInfo field = typeof(MaddeningGazeSkill).GetField("fearedTowers", BindingFlags.NonPublic | BindingFlags.Instance);
        List<TowerController> list = (List<TowerController>)field?.GetValue(skill);
        list?.Add(tower);
    }

    private static void AddTauntedTower(ShadeMinionSkill skill, TowerController tower)
    {
        FieldInfo field = typeof(ShadeMinionSkill).GetField("tauntedTowers", BindingFlags.NonPublic | BindingFlags.Instance);
        List<TowerController> list = (List<TowerController>)field?.GetValue(skill);
        list?.Add(tower);
    }

    // =========================================================================
    // 1. ENEMY REGISTRY TESTS
    // =========================================================================

    [Test]
    public void Test01_EnemyRegistry_Register_SingleEnemy_TrackedInActiveEnemies()
    {
        GameObject enemyObj = CreateEnemy("E1", Vector3.zero, 100f, 0, EnemyRank.Normal);
        EnemyStats stats = enemyObj.GetComponent<EnemyStats>();

        EnemyRegistry.Register(stats);

        Assert.AreEqual(1, EnemyRegistry.ActiveEnemies.Count);
        Assert.IsTrue(EnemyRegistry.ActiveEnemies.Contains(stats));
    }

    [Test]
    public void Test02_EnemyRegistry_Register_DuplicateEnemy_Prevented()
    {
        GameObject enemyObj = CreateEnemy("E1", Vector3.zero, 100f, 0, EnemyRank.Normal);
        EnemyStats stats = enemyObj.GetComponent<EnemyStats>();

        EnemyRegistry.Register(stats);
        EnemyRegistry.Register(stats);
        EnemyRegistry.Register(stats);

        Assert.AreEqual(1, EnemyRegistry.ActiveEnemies.Count, "Duplicate registration must be prevented!");
    }

    [Test]
    public void Test03_EnemyRegistry_Register_NullEnemy_IgnoredSafely()
    {
        Assert.DoesNotThrow(() => EnemyRegistry.Register(null));
        Assert.AreEqual(0, EnemyRegistry.ActiveEnemies.Count);
    }

    [Test]
    public void Test04_EnemyRegistry_Unregister_RemovesEnemy()
    {
        GameObject e1 = CreateEnemy("E1", Vector3.zero, 100f, 0, EnemyRank.Normal);
        GameObject e2 = CreateEnemy("E2", Vector3.zero, 100f, 0, EnemyRank.Normal);
        EnemyStats s1 = e1.GetComponent<EnemyStats>();
        EnemyStats s2 = e2.GetComponent<EnemyStats>();

        EnemyRegistry.Register(s1);
        EnemyRegistry.Register(s2);
        Assert.AreEqual(2, EnemyRegistry.ActiveEnemies.Count);

        EnemyRegistry.Unregister(s1);
        Assert.AreEqual(1, EnemyRegistry.ActiveEnemies.Count);
        Assert.IsFalse(EnemyRegistry.ActiveEnemies.Contains(s1));
        Assert.IsTrue(EnemyRegistry.ActiveEnemies.Contains(s2));
    }

    [Test]
    public void Test05_EnemyRegistry_Unregister_NullEnemy_IgnoredSafely()
    {
        Assert.DoesNotThrow(() => EnemyRegistry.Unregister(null));
    }

    [Test]
    public void Test06_EnemyRegistry_Clear_RemovesAllEnemies()
    {
        for (int i = 0; i < 5; i++)
        {
            GameObject e = CreateEnemy($"E{i}", Vector3.zero, 100f, 0, EnemyRank.Normal);
            EnemyRegistry.Register(e.GetComponent<EnemyStats>());
        }

        Assert.AreEqual(5, EnemyRegistry.ActiveEnemies.Count);
        EnemyRegistry.Clear();
        Assert.AreEqual(0, EnemyRegistry.ActiveEnemies.Count);
    }

    [Test]
    public void Test07_EnemyRegistry_MultipleRegistrations_LifecycleIntegrity()
    {
        List<EnemyStats> enemies = new List<EnemyStats>();
        for (int i = 0; i < 50; i++)
        {
            GameObject e = CreateEnemy($"E{i}", new Vector3(i, 0, 0), 100f, i, EnemyRank.Normal);
            EnemyStats s = e.GetComponent<EnemyStats>();
            enemies.Add(s);
            EnemyRegistry.Register(s);
        }

        Assert.AreEqual(50, EnemyRegistry.ActiveEnemies.Count);

        // Remove first, middle, last
        EnemyRegistry.Unregister(enemies[0]);
        EnemyRegistry.Unregister(enemies[25]);
        EnemyRegistry.Unregister(enemies[49]);

        Assert.AreEqual(47, EnemyRegistry.ActiveEnemies.Count);
        Assert.IsFalse(EnemyRegistry.ActiveEnemies.Contains(enemies[0]));
        Assert.IsFalse(EnemyRegistry.ActiveEnemies.Contains(enemies[25]));
        Assert.IsFalse(EnemyRegistry.ActiveEnemies.Contains(enemies[49]));
    }

    [Test]
    public void Test08_EnemyStats_OnEnableAndOnDisable_AutoUpdatesRegistry()
    {
        GameObject enemyObj = CreateEnemy("E1", Vector3.zero, 100f, 0, EnemyRank.Normal);
        EnemyStats stats = enemyObj.GetComponent<EnemyStats>();

        InvokeOnEnable(stats);
        Assert.AreEqual(1, EnemyRegistry.ActiveEnemies.Count);
        Assert.IsTrue(EnemyRegistry.ActiveEnemies.Contains(stats));

        InvokeOnDisable(stats);
        Assert.AreEqual(0, EnemyRegistry.ActiveEnemies.Count);
    }

    // =========================================================================
    // 2. TOWER CONTROLLER TARGETING TESTS (ALL 8 MODES + TIE BREAKERS + TAUNT)
    // =========================================================================

    [Test]
    public void Test09_TargetingMode_Closest_SelectsNearest()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.Closest);
        GameObject e1 = CreateEnemy("Far", new Vector3(20, 0, 0), 100f, 1, EnemyRank.Normal);
        GameObject e2 = CreateEnemy("Near", new Vector3(5, 0, 0), 100f, 1, EnemyRank.Normal);
        GameObject e3 = CreateEnemy("Mid", new Vector3(12, 0, 0), 100f, 1, EnemyRank.Normal);

        EnemyRegistry.Register(e1.GetComponent<EnemyStats>());
        EnemyRegistry.Register(e2.GetComponent<EnemyStats>());
        EnemyRegistry.Register(e3.GetComponent<EnemyStats>());

        InvokeUpdateTarget(tower);

        Assert.AreEqual(e2.transform, GetTarget(tower), "Closest mode should select enemy at distance 5");
    }

    [Test]
    public void Test10_TargetingMode_Furthest_SelectsFurthest()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.Furthest);
        GameObject e1 = CreateEnemy("Near", new Vector3(5, 0, 0), 100f, 1, EnemyRank.Normal);
        GameObject e2 = CreateEnemy("Far", new Vector3(25, 0, 0), 100f, 1, EnemyRank.Normal);
        GameObject e3 = CreateEnemy("Mid", new Vector3(15, 0, 0), 100f, 1, EnemyRank.Normal);

        EnemyRegistry.Register(e1.GetComponent<EnemyStats>());
        EnemyRegistry.Register(e2.GetComponent<EnemyStats>());
        EnemyRegistry.Register(e3.GetComponent<EnemyStats>());

        InvokeUpdateTarget(tower);

        Assert.AreEqual(e2.transform, GetTarget(tower), "Furthest mode should select enemy at distance 25");
    }

    [Test]
    public void Test11_TargetingMode_ClosestOnTrack_SelectsHighestTrackProgress()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.ClosestOnTrack);
        GameObject e1 = CreateEnemy("Lead", new Vector3(10, 0, 0), 100f, 15, EnemyRank.Normal);
        GameObject e2 = CreateEnemy("Tail", new Vector3(10, 0, 0), 100f, 2, EnemyRank.Normal);
        GameObject e3 = CreateEnemy("Mid", new Vector3(10, 0, 0), 100f, 8, EnemyRank.Normal);

        EnemyRegistry.Register(e1.GetComponent<EnemyStats>());
        EnemyRegistry.Register(e2.GetComponent<EnemyStats>());
        EnemyRegistry.Register(e3.GetComponent<EnemyStats>());

        InvokeUpdateTarget(tower);

        Assert.AreEqual(e1.transform, GetTarget(tower), "ClosestOnTrack should select enemy with highest track progress (15)");
    }

    [Test]
    public void Test12_TargetingMode_FurthestOnTrack_SelectsLowestTrackProgress()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.FurthestOnTrack);
        GameObject e1 = CreateEnemy("Lead", new Vector3(10, 0, 0), 100f, 15, EnemyRank.Normal);
        GameObject e2 = CreateEnemy("Tail", new Vector3(10, 0, 0), 100f, 2, EnemyRank.Normal);
        GameObject e3 = CreateEnemy("Mid", new Vector3(10, 0, 0), 100f, 8, EnemyRank.Normal);

        EnemyRegistry.Register(e1.GetComponent<EnemyStats>());
        EnemyRegistry.Register(e2.GetComponent<EnemyStats>());
        EnemyRegistry.Register(e3.GetComponent<EnemyStats>());

        InvokeUpdateTarget(tower);

        Assert.AreEqual(e2.transform, GetTarget(tower), "FurthestOnTrack should select enemy with lowest track progress (2)");
    }

    [Test]
    public void Test13_TargetingMode_MostHP_SelectsHighestHealth()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.MostHP);
        GameObject e1 = CreateEnemy("Low", new Vector3(10, 0, 0), 50f, 1, EnemyRank.Normal);
        GameObject e2 = CreateEnemy("High", new Vector3(10, 0, 0), 300f, 1, EnemyRank.Normal);
        GameObject e3 = CreateEnemy("Mid", new Vector3(10, 0, 0), 150f, 1, EnemyRank.Normal);

        EnemyRegistry.Register(e1.GetComponent<EnemyStats>());
        EnemyRegistry.Register(e2.GetComponent<EnemyStats>());
        EnemyRegistry.Register(e3.GetComponent<EnemyStats>());

        InvokeUpdateTarget(tower);

        Assert.AreEqual(e2.transform, GetTarget(tower), "MostHP should select enemy with 300 HP");
    }

    [Test]
    public void Test14_TargetingMode_LeastHP_SelectsLowestHealth()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.LeastHP);
        GameObject e1 = CreateEnemy("Low", new Vector3(10, 0, 0), 50f, 1, EnemyRank.Normal);
        GameObject e2 = CreateEnemy("High", new Vector3(10, 0, 0), 300f, 1, EnemyRank.Normal);
        GameObject e3 = CreateEnemy("Mid", new Vector3(10, 0, 0), 150f, 1, EnemyRank.Normal);

        EnemyRegistry.Register(e1.GetComponent<EnemyStats>());
        EnemyRegistry.Register(e2.GetComponent<EnemyStats>());
        EnemyRegistry.Register(e3.GetComponent<EnemyStats>());

        InvokeUpdateTarget(tower);

        Assert.AreEqual(e1.transform, GetTarget(tower), "LeastHP should select enemy with 50 HP");
    }

    [Test]
    public void Test15_TargetingMode_HighestThreat_SelectsBossOverEliteAndNormal()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.HighestThreat);
        GameObject normal = CreateEnemy("Normal", new Vector3(5, 0, 0), 100f, 1, EnemyRank.Normal);
        GameObject elite = CreateEnemy("Elite", new Vector3(10, 0, 0), 200f, 1, EnemyRank.Elite);
        GameObject boss = CreateEnemy("Boss", new Vector3(15, 0, 0), 500f, 1, EnemyRank.Boss);

        EnemyRegistry.Register(normal.GetComponent<EnemyStats>());
        EnemyRegistry.Register(elite.GetComponent<EnemyStats>());
        EnemyRegistry.Register(boss.GetComponent<EnemyStats>());

        InvokeUpdateTarget(tower);

        Assert.AreEqual(boss.transform, GetTarget(tower), "HighestThreat should select Boss (threat 3)");
    }

    [Test]
    public void Test16_TargetingMode_HighestThreat_SelectsEliteOverNormal()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.HighestThreat);
        GameObject normal = CreateEnemy("Normal", new Vector3(5, 0, 0), 100f, 1, EnemyRank.Normal);
        GameObject elite = CreateEnemy("Elite", new Vector3(10, 0, 0), 200f, 1, EnemyRank.Elite);

        EnemyRegistry.Register(normal.GetComponent<EnemyStats>());
        EnemyRegistry.Register(elite.GetComponent<EnemyStats>());

        InvokeUpdateTarget(tower);

        Assert.AreEqual(elite.transform, GetTarget(tower), "HighestThreat should select Elite (threat 2) over Normal");
    }

    [Test]
    public void Test17_TargetingMode_Random_SelectsCandidateFromInput()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.Random);
        GameObject e1 = CreateEnemy("E1", new Vector3(5, 0, 0), 100f, 1, EnemyRank.Normal);
        GameObject e2 = CreateEnemy("E2", new Vector3(10, 0, 0), 100f, 1, EnemyRank.Normal);

        List<EnemyStats> input = new List<EnemyStats> { e1.GetComponent<EnemyStats>(), e2.GetComponent<EnemyStats>() };
        List<EnemyStats> output = new List<EnemyStats>();

        InvokeApplyFilter(tower, input, output, TargetingMode.Random);

        Assert.AreEqual(1, output.Count, "Random should select exactly 1 candidate");
        Assert.IsTrue(output[0] == input[0] || output[0] == input[1]);
    }

    [Test]
    public void Test18_Targeting_TieBreaking_ClosestEpsilonTie_ResolvedByTrackProgress()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.Closest);
        // Distances: 5.000 vs 5.020 (diff 0.02 <= FilterEpsilon 0.05)
        GameObject e1 = CreateEnemy("E1", new Vector3(5.00f, 0, 0), 100f, 3, EnemyRank.Normal);
        GameObject e2 = CreateEnemy("E2", new Vector3(5.02f, 0, 0), 100f, 9, EnemyRank.Normal);

        EnemyRegistry.Register(e1.GetComponent<EnemyStats>());
        EnemyRegistry.Register(e2.GetComponent<EnemyStats>());

        InvokeUpdateTarget(tower);

        Assert.AreEqual(e2.transform, GetTarget(tower), "Closest tie within 0.05 must be broken by ClosestOnTrack (progress 9 vs 3)");
    }

    [Test]
    public void Test19_Targeting_TieBreaking_FurthestEpsilonTie_ResolvedByTrackProgress()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.Furthest);
        // Distances: 19.98 vs 20.00 (diff 0.02 <= 0.05)
        GameObject e1 = CreateEnemy("E1", new Vector3(19.98f, 0, 0), 100f, 8, EnemyRank.Normal);
        GameObject e2 = CreateEnemy("E2", new Vector3(20.00f, 0, 0), 100f, 2, EnemyRank.Normal);

        EnemyRegistry.Register(e1.GetComponent<EnemyStats>());
        EnemyRegistry.Register(e2.GetComponent<EnemyStats>());

        InvokeUpdateTarget(tower);

        Assert.AreEqual(e1.transform, GetTarget(tower), "Furthest tie within 0.05 must be broken by ClosestOnTrack (progress 8 vs 2)");
    }

    [Test]
    public void Test20_Targeting_TieBreaking_MostHPEpsilonTie_ResolvedByTrackProgress()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.MostHP);
        // HPs: 200.00 vs 200.03 (diff <= 0.05)
        GameObject e1 = CreateEnemy("E1", new Vector3(5, 0, 0), 200.00f, 4, EnemyRank.Normal);
        GameObject e2 = CreateEnemy("E2", new Vector3(5, 0, 0), 200.03f, 11, EnemyRank.Normal);

        EnemyRegistry.Register(e1.GetComponent<EnemyStats>());
        EnemyRegistry.Register(e2.GetComponent<EnemyStats>());

        InvokeUpdateTarget(tower);

        Assert.AreEqual(e2.transform, GetTarget(tower), "MostHP tie within 0.05 must be broken by ClosestOnTrack");
    }

    [Test]
    public void Test21_Targeting_TieBreaking_LeastHPEpsilonTie_ResolvedByTrackProgress()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.LeastHP);
        // HPs: 40.01 vs 40.04
        GameObject e1 = CreateEnemy("E1", new Vector3(5, 0, 0), 40.01f, 14, EnemyRank.Normal);
        GameObject e2 = CreateEnemy("E2", new Vector3(5, 0, 0), 40.04f, 2, EnemyRank.Normal);

        EnemyRegistry.Register(e1.GetComponent<EnemyStats>());
        EnemyRegistry.Register(e2.GetComponent<EnemyStats>());

        InvokeUpdateTarget(tower);

        Assert.AreEqual(e1.transform, GetTarget(tower), "LeastHP tie within 0.05 must be broken by ClosestOnTrack");
    }

    [Test]
    public void Test22_Targeting_MultiRuleCascade_FiltersSequentially()
    {
        // Rule: MostHP, then Closest
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.MostHP, TargetingMode.Closest);
        GameObject e1 = CreateEnemy("HighHpNear", new Vector3(6, 0, 0), 300f, 1, EnemyRank.Normal);
        GameObject e2 = CreateEnemy("HighHpFar", new Vector3(18, 0, 0), 300f, 1, EnemyRank.Normal);
        GameObject e3 = CreateEnemy("LowHpNear", new Vector3(2, 0, 0), 50f, 1, EnemyRank.Normal);

        EnemyRegistry.Register(e1.GetComponent<EnemyStats>());
        EnemyRegistry.Register(e2.GetComponent<EnemyStats>());
        EnemyRegistry.Register(e3.GetComponent<EnemyStats>());

        InvokeUpdateTarget(tower);

        Assert.AreEqual(e1.transform, GetTarget(tower), "MostHP narrows to E1 and E2; Closest narrows to E1");
    }

    [Test]
    public void Test23_Targeting_MultiRuleCascade_ThreatThenClosest()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.HighestThreat, TargetingMode.Closest);
        GameObject normalNear = CreateEnemy("NormalNear", new Vector3(2, 0, 0), 100f, 1, EnemyRank.Normal);
        GameObject eliteFar = CreateEnemy("EliteFar", new Vector3(15, 0, 0), 200f, 1, EnemyRank.Elite);
        GameObject eliteNear = CreateEnemy("EliteNear", new Vector3(8, 0, 0), 200f, 1, EnemyRank.Elite);

        EnemyRegistry.Register(normalNear.GetComponent<EnemyStats>());
        EnemyRegistry.Register(eliteFar.GetComponent<EnemyStats>());
        EnemyRegistry.Register(eliteNear.GetComponent<EnemyStats>());

        InvokeUpdateTarget(tower);

        Assert.AreEqual(eliteNear.transform, GetTarget(tower), "Threat filters to elites; Closest selects elite at distance 8");
    }

    [Test]
    public void Test24_Targeting_ForcedTarget_TauntOverridesRegularRules()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.Closest);
        GameObject regularEnemy = CreateEnemy("Regular", new Vector3(3, 0, 0), 100f, 10, EnemyRank.Normal);
        GameObject taunter = CreateEnemy("Taunter", new Vector3(12, 0, 0), 50f, 1, EnemyRank.Normal);

        EnemyRegistry.Register(regularEnemy.GetComponent<EnemyStats>());
        EnemyRegistry.Register(taunter.GetComponent<EnemyStats>());

        tower.OverrideTargeting(taunter.transform);

        InvokeUpdateTarget(tower);

        Assert.AreEqual(taunter.transform, GetTarget(tower), "Forced target (taunt) must override regular rule targeting!");
    }

    [Test]
    public void Test25_Targeting_ForcedTarget_OutOfRange_ClearsForcedTarget()
    {
        TowerController tower = CreateTower(Vector3.zero, 10f, TargetingMode.Closest);
        GameObject regularEnemy = CreateEnemy("Regular", new Vector3(4, 0, 0), 100f, 1, EnemyRank.Normal);
        GameObject taunter = CreateEnemy("TaunterFar", new Vector3(25, 0, 0), 50f, 1, EnemyRank.Normal);

        EnemyRegistry.Register(regularEnemy.GetComponent<EnemyStats>());
        EnemyRegistry.Register(taunter.GetComponent<EnemyStats>());

        tower.OverrideTargeting(taunter.transform);

        InvokeUpdateTarget(tower);

        Assert.IsNull(GetForcedTarget(tower), "Out of range forced target must be cleared to null!");
        Assert.AreEqual(regularEnemy.transform, GetTarget(tower), "Tower should fall back to targeting in-range regular enemy");
    }

    [Test]
    public void Test26_Targeting_ForcedTarget_ClearOverride_RevertsToRules()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.Closest);
        GameObject near = CreateEnemy("Near", new Vector3(2, 0, 0), 100f, 1, EnemyRank.Normal);
        GameObject farTaunter = CreateEnemy("FarTaunter", new Vector3(10, 0, 0), 100f, 1, EnemyRank.Normal);

        EnemyRegistry.Register(near.GetComponent<EnemyStats>());
        EnemyRegistry.Register(farTaunter.GetComponent<EnemyStats>());

        tower.OverrideTargeting(farTaunter.transform);
        tower.ClearOverride();

        InvokeUpdateTarget(tower);

        Assert.AreEqual(near.transform, GetTarget(tower), "Clearing override should return to normal Closest priority");
    }

    [Test]
    public void Test27_Targeting_OutOfRange_EnemyIgnored()
    {
        TowerController tower = CreateTower(Vector3.zero, 10f, TargetingMode.Closest);
        GameObject outOfRange = CreateEnemy("OutOfRange", new Vector3(15, 0, 0), 100f, 1, EnemyRank.Normal);

        EnemyRegistry.Register(outOfRange.GetComponent<EnemyStats>());

        InvokeUpdateTarget(tower);

        Assert.IsNull(GetTarget(tower), "Enemy outside range (15 > 10) must not be targeted");
    }

    [Test]
    public void Test28_Targeting_ZeroCandidateRetention_BuffersCleared()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.Closest);
        for (int i = 0; i < 5; i++)
        {
            GameObject e = CreateEnemy($"E{i}", new Vector3(5 + i, 0, 0), 100f, i, EnemyRank.Normal);
            EnemyRegistry.Register(e.GetComponent<EnemyStats>());
        }

        InvokeUpdateTarget(tower);

        List<EnemyStats> bufA = GetBufferA(tower);
        List<EnemyStats> bufB = GetBufferB(tower);

        Assert.AreEqual(0, bufA.Count, "candidateBufferA must be cleared after targeting tick!");
        Assert.AreEqual(0, bufB.Count, "candidateBufferB must be cleared after targeting tick!");
    }

    // =========================================================================
    // 3. MATERIAL PROPERTY BLOCK IN ENEMY STATS TESTS
    // =========================================================================

    [Test]
    public void Test29_EnemyStats_ZeroMaterialCloning_SharedMaterialRemainsClean()
    {
        GameObject enemyObj = CreateEnemy("E1", Vector3.zero, 100f, 0, EnemyRank.Normal);
        EnemyStats stats = enemyObj.GetComponent<EnemyStats>();
        MeshRenderer mr = enemyObj.GetComponent<MeshRenderer>();

        string initialMatName = mr.sharedMaterial.name;
        Assert.IsFalse(initialMatName.Contains("(Instance)"), "Initial sharedMaterial should not be an instance");

        // Simulate damage taking and flashes
        stats.TakeDamage(10f, DamageType.Physical, 0f, 0f, false, 1f);
        stats.TakeDamage(10f, DamageType.Physical, 0f, 0f, false, 1f);
        stats.ResetColor();

        string finalMatName = mr.sharedMaterial.name;
        Assert.IsFalse(finalMatName.Contains("(Instance)"), "Damage flashes must NEVER clone material into (Instance)!");
        Assert.AreEqual(initialMatName, finalMatName, "sharedMaterial reference must be fully preserved");
    }

    [Test]
    public void Test30_EnemyStats_TakeDamage_HitFlash_AppliesPropertyBlock()
    {
        GameObject enemyObj = CreateEnemy("E1", Vector3.zero, 100f, 0, EnemyRank.Normal);
        EnemyStats stats = enemyObj.GetComponent<EnemyStats>();
        MeshRenderer mr = enemyObj.GetComponent<MeshRenderer>();

        InvokeFlashColor(stats, Color.white, 0.1f);

        Assert.IsTrue(mr.HasPropertyBlock(), "FlashColor must apply MaterialPropertyBlock override to renderer");
    }

    [Test]
    public void Test31_EnemyStats_TakeDamage_DodgeFlash_AppliesPropertyBlock()
    {
        GameObject enemyObj = CreateEnemy("E1", Vector3.zero, 100f, 0, EnemyRank.Normal);
        EnemyStats stats = enemyObj.GetComponent<EnemyStats>();
        MeshRenderer mr = enemyObj.GetComponent<MeshRenderer>();

        InvokeFlashColor(stats, Color.yellow, 0.1f);

        Assert.IsTrue(mr.HasPropertyBlock(), "Dodge flash must apply MaterialPropertyBlock override");
    }

    [Test]
    public void Test32_EnemyStats_ResetColor_NormalEnemy_RestoresSharedMaterialAndClearsMPB()
    {
        GameObject enemyObj = CreateEnemy("E1", Vector3.zero, 100f, 0, EnemyRank.Normal);
        EnemyStats stats = enemyObj.GetComponent<EnemyStats>();
        MeshRenderer mr = enemyObj.GetComponent<MeshRenderer>();

        InvokeFlashColor(stats, Color.white, 0.1f);
        Assert.IsTrue(mr.HasPropertyBlock());

        stats.ResetColor();

        Assert.IsFalse(mr.HasPropertyBlock(), "ResetColor on normal enemy must call SetPropertyBlock(null) to restore SRP batching!");
    }

    [Test]
    public void Test33_EnemyStats_ResetColor_EliteEnemy_MaintainsRedPropertyBlock()
    {
        GameObject enemyObj = CreateEnemy("Elite", Vector3.zero, 100f, 0, EnemyRank.Elite);
        EnemyStats stats = enemyObj.GetComponent<EnemyStats>();
        MeshRenderer mr = enemyObj.GetComponent<MeshRenderer>();

        stats.ResetColor();

        Assert.IsTrue(mr.HasPropertyBlock(), "ResetColor on elite enemy must apply red MaterialPropertyBlock");
    }

    [Test]
    public void Test34_EnemyStats_OnDisable_ClearsPropertyBlockOverrides()
    {
        GameObject enemyObj = CreateEnemy("E1", Vector3.zero, 100f, 0, EnemyRank.Normal);
        EnemyStats stats = enemyObj.GetComponent<EnemyStats>();
        MeshRenderer mr = enemyObj.GetComponent<MeshRenderer>();

        InvokeFlashColor(stats, Color.white, 0.1f);
        Assert.IsTrue(mr.HasPropertyBlock());

        InvokeOnDisable(stats);

        Assert.IsFalse(mr.HasPropertyBlock(), "OnDisable must clear property block overrides via SetPropertyBlock(null)");
    }

    [Test]
    public void Test35_EnemyStats_MultipleChildRenderers_AllReceivePropertyBlock()
    {
        GameObject enemyObj = CreateEnemy("MultiPart", Vector3.zero, 100f, 0, EnemyRank.Normal);
        GameObject child = new GameObject("Part2");
        child.transform.SetParent(enemyObj.transform);
        MeshRenderer mrChild = child.AddComponent<MeshRenderer>();
        mrChild.sharedMaterial = testSharedMaterial;

        EnemyStats stats = enemyObj.GetComponent<EnemyStats>();
        MeshRenderer mrRoot = enemyObj.GetComponent<MeshRenderer>();
        InvokeAwake(stats);

        InvokeFlashColor(stats, Color.white, 0.1f);

        Assert.IsTrue(mrRoot.HasPropertyBlock(), "Root renderer must receive property block");
        Assert.IsTrue(mrChild.HasPropertyBlock(), "Child renderer must receive property block");

        stats.ResetColor();

        Assert.IsFalse(mrRoot.HasPropertyBlock(), "Root renderer block must be cleared on ResetColor");
        Assert.IsFalse(mrChild.HasPropertyBlock(), "Child renderer block must be cleared on ResetColor");
    }

    // =========================================================================
    // 4. ENEMY SKILLS TESTS (GAZE, SHADE MINION, POINT DEFENSE)
    // =========================================================================

    [Test]
    public void Test36_MaddeningGazeSkill_ScanInterval_ThrottledAt02Seconds()
    {
        FieldInfo intervalField = typeof(MaddeningGazeSkill).GetField("ScanInterval", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(intervalField, "ScanInterval constant must exist on MaddeningGazeSkill");
        float interval = (float)intervalField.GetValue(null);
        Assert.AreEqual(0.2f, interval, 0.001f, "ScanInterval must be exactly 0.2s");
    }

    [Test]
    public void Test37_MaddeningGazeSkill_HitBuffer_IsStaticNonAlloc64()
    {
        FieldInfo bufferField = typeof(MaddeningGazeSkill).GetField("hitBuffer", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(bufferField, "hitBuffer must exist on MaddeningGazeSkill");
        Collider[] buffer = (Collider[])bufferField.GetValue(null);
        Assert.AreEqual(64, buffer.Length, "hitBuffer must be a pre-allocated Collider array of length 64");
    }

    [Test]
    public void Test38_MaddeningGazeSkill_FearDebuff_ReducesDamageAndFireRateByHalf()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.Closest);
        TowerStats baseStats = new TowerStats
        {
            damage = 100f,
            fireRate = 2f,
            range = 30f
        };

        tower.isFeared = false;
        tower.ApplyCalculatedStats(baseStats, 1f);
        Assert.AreEqual(100f, tower.GetCurrentDamage(), 0.01f);
        Assert.AreEqual(2f, tower.GetCurrentFireRate(), 0.01f);

        tower.isFeared = true;
        tower.ApplyCalculatedStats(baseStats, 1f);
        Assert.AreEqual(50f, tower.GetCurrentDamage(), 0.01f, "Damage must be halved (50%) when feared!");
        Assert.AreEqual(1f, tower.GetCurrentFireRate(), 0.01f, "FireRate must be halved (50%) when feared!");
    }

    [Test]
    public void Test39_MaddeningGazeSkill_OnDisable_CleansUpFearedTowersAndRestoresStats()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.Closest);
        tower.isFeared = true;

        GameObject bossObj = new GameObject("BossGaze");
        MaddeningGazeSkill gazeSkill = bossObj.AddComponent<MaddeningGazeSkill>();
        testGameObjects.Add(bossObj);

        AddFearedTower(gazeSkill, tower);

        InvokeSkillOnDisable(gazeSkill);

        Assert.IsFalse(tower.isFeared, "MaddeningGazeSkill.OnDisable must reset tower.isFeared to false!");
    }

    [Test]
    public void Test40_ShadeMinionSkill_ScanInterval_ThrottledAt01Seconds()
    {
        FieldInfo intervalField = typeof(ShadeMinionSkill).GetField("ScanInterval", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(intervalField, "ScanInterval constant must exist on ShadeMinionSkill");
        float interval = (float)intervalField.GetValue(null);
        Assert.AreEqual(0.1f, interval, 0.001f, "ScanInterval must be exactly 0.1s");
    }

    [Test]
    public void Test41_ShadeMinionSkill_TauntOverride_And_OnDisableCleanup()
    {
        TowerController tower = CreateTower(Vector3.zero, 30f, TargetingMode.Closest);
        GameObject minionObj = new GameObject("ShadeMinion");
        ShadeMinionSkill minionSkill = minionObj.AddComponent<ShadeMinionSkill>();
        testGameObjects.Add(minionObj);

        tower.OverrideTargeting(minionObj.transform);
        AddTauntedTower(minionSkill, tower);
        Assert.AreEqual(minionObj.transform, GetForcedTarget(tower));

        InvokeSkillOnDisable(minionSkill);

        Assert.IsNull(GetForcedTarget(tower), "ShadeMinionSkill.OnDisable must clear taunt override via ClearOverride()!");
    }

    [Test]
    public void Test42_PointDefenseSkill_CooldownAndNonAllocBuffer()
    {
        GameObject pdObj = new GameObject("PD");
        PointDefenseSkill pdSkill = pdObj.AddComponent<PointDefenseSkill>();
        testGameObjects.Add(pdObj);

        Assert.AreEqual(4f, pdSkill.cooldown, 0.01f, "PointDefenseSkill cooldown must be 4 seconds");

        FieldInfo bufferField = typeof(PointDefenseSkill).GetField("hitBuffer", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(bufferField, "hitBuffer must exist on PointDefenseSkill");
        Collider[] buffer = (Collider[])bufferField.GetValue(null);
        Assert.AreEqual(64, buffer.Length, "hitBuffer must be a pre-allocated Collider array of length 64");
    }

    // =========================================================================
    // 5. ADVERSARIAL STRESS TESTS
    // =========================================================================

    [Test]
    public void Test43_Stress_HighFrequencyTargetingLoop_50Enemies100Ticks()
    {
        TowerController tower = CreateTower(Vector3.zero, 50f, TargetingMode.HighestThreat, TargetingMode.Closest, TargetingMode.ClosestOnTrack);

        for (int i = 0; i < 50; i++)
        {
            EnemyRank rank = (i % 10 == 0) ? EnemyRank.Boss : (i % 3 == 0) ? EnemyRank.Elite : EnemyRank.Normal;
            GameObject e = CreateEnemy($"StressE_{i}", new Vector3(5 + (i * 0.8f), 0, 0), 100f + i, i, rank);
            EnemyRegistry.Register(e.GetComponent<EnemyStats>());
        }

        Assert.AreEqual(50, EnemyRegistry.ActiveEnemies.Count);

        // Run 100 rapid targeting ticks
        for (int tick = 0; tick < 100; tick++)
        {
            InvokeUpdateTarget(tower);
            Assert.IsNotNull(GetTarget(tower), $"Target must be found on tick {tick}");
            Assert.AreEqual(0, GetBufferA(tower).Count);
            Assert.AreEqual(0, GetBufferB(tower).Count);
        }
    }

    [Test]
    public void Test44_Stress_RapidRegistryChurn_500Operations()
    {
        List<EnemyStats> list = new List<EnemyStats>();
        for (int i = 0; i < 50; i++)
        {
            GameObject e = CreateEnemy($"Churn_{i}", Vector3.zero, 100f, i, EnemyRank.Normal);
            list.Add(e.GetComponent<EnemyStats>());
        }

        for (int op = 0; op < 500; op++)
        {
            int idx = op % list.Count;
            if (op % 3 == 0)
                EnemyRegistry.Register(list[idx]);
            else if (op % 3 == 1)
                EnemyRegistry.Register(list[idx]); // Duplicate attempt
            else
                EnemyRegistry.Unregister(list[idx]);
        }

        // Integrity check: ActiveEnemies must not have duplicates or nulls
        HashSet<EnemyStats> uniqueSet = new HashSet<EnemyStats>();
        for (int i = 0; i < EnemyRegistry.ActiveEnemies.Count; i++)
        {
            EnemyStats s = EnemyRegistry.ActiveEnemies[i];
            Assert.IsNotNull(s, "Registry must not contain null references");
            Assert.IsTrue(uniqueSet.Add(s), "Registry must never contain duplicate entries");
        }
    }

    [Test]
    public void Test45_Stress_RapidTakeDamageFlashes_NoInvokeStall()
    {
        GameObject enemyObj = CreateEnemy("FlashStress", Vector3.zero, 1000f, 0, EnemyRank.Normal);
        EnemyStats stats = enemyObj.GetComponent<EnemyStats>();

        for (int i = 0; i < 50; i++)
        {
            stats.TakeDamage(5f, DamageType.Physical, 0f, 0f, false, 1f);
        }

        Assert.AreEqual(750f, stats.GetCurrentHealth(), 0.1f);
        stats.ResetColor();

        MeshRenderer mr = enemyObj.GetComponent<MeshRenderer>();
        Assert.IsFalse(mr.HasPropertyBlock(), "After rapid damage and ResetColor, property block must be cleared!");
        Assert.IsFalse(mr.sharedMaterial.name.Contains("(Instance)"), "No material leaks after 50 rapid damage calls!");
    }

    // =========================================================================
    // AUTOMATIC RUNNER FOR EDITMODE / LOG VERIFICATION
    // =========================================================================

    [MenuItem("Tools/Run Combat Optimization Tests")]
    [InitializeOnLoadMethod]
    public static void RunTestsOnDomainReload()
    {
        RunAllTestsEmpirically(out int passed, out int failed, out List<string> errors);
    }

    public static bool RunAllTestsEmpirically(out int passed, out int failed, out List<string> errors)
    {
        passed = 0;
        failed = 0;
        errors = new List<string>();

        CombatOptimizationTests suite = new CombatOptimizationTests();
        MethodInfo[] methods = typeof(CombatOptimizationTests).GetMethods(BindingFlags.Public | BindingFlags.Instance);

        Debug.Log("<color=cyan>[CombatOptimizationTests] Rozpoczynanie weryfikacji empirycznej testów NUnit...</color>");

        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo m = methods[i];
            if (!m.IsDefined(typeof(TestAttribute), false)) continue;

            try
            {
                suite.SetUp();
                m.Invoke(suite, null);
                suite.TearDown();
                passed++;
                Debug.Log($"<color=green>[PASS]</color> {m.Name}");
            }
            catch (Exception ex)
            {
                failed++;
                string err = $"{m.Name}: {ex.InnerException?.Message ?? ex.Message}";
                errors.Add(err);
                Debug.LogError($"<color=red>[FAIL]</color> {err}");
                try { suite.TearDown(); } catch { }
            }
        }

        Debug.Log($"<color=yellow>[CombatOptimizationTests] Zakończono: {passed} PASSED, {failed} FAILED z {passed + failed} testów.</color>");
        return failed == 0;
    }
}
