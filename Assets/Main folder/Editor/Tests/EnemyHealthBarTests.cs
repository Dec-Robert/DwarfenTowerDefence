using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public class EnemyHealthBarTests
{
    private List<GameObject> testGameObjects;

    [SetUp]
    public void SetUp()
    {
        testGameObjects = new List<GameObject>();
        EnemyRegistry.Clear();
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
    }

    // =========================================================================
    // 1. TIER & SEGMENT EVALUATION TESTS
    // =========================================================================

    [Test]
    public void Test01_TierRed_Below100Hp_FractionalSegment_NoDividers()
    {
        // Skeleton: 50 HP (czerwony <= 1k, segment = 100 HP, 0.5 segmentu)
        EnemyHealthBarManager.EvaluateTier(50f, out Color color, out float segValue, out float segCount);

        Assert.AreEqual(100f, segValue, 0.001f);
        Assert.AreEqual(0.5f, segCount, 0.001f);
        Assert.Greater(color.r, color.b);
        Assert.Greater(color.r, color.g);
    }

    [Test]
    public void Test02_TierRed_IntermediateHp_380Hp_Has3SegmentsAndFraction()
    {
        // Abberracja: 380 HP -> 3.8 segmentu po 100 HP
        EnemyHealthBarManager.EvaluateTier(380f, out Color color, out float segValue, out float segCount);

        Assert.AreEqual(100f, segValue, 0.001f);
        Assert.AreEqual(3.8f, segCount, 0.001f);
        Assert.Greater(color.r, color.b);
    }

    [Test]
    public void Test03_TierRed_Boundary1000Hp_Exactly10Segments()
    {
        // 1000 HP -> dokładnie 10 segmentów po 100 HP
        EnemyHealthBarManager.EvaluateTier(1000f, out Color color, out float segValue, out float segCount);

        Assert.AreEqual(100f, segValue, 0.001f);
        Assert.AreEqual(10f, segCount, 0.001f);
        Assert.Greater(color.r, color.b);
    }

    [Test]
    public void Test04_TierBlue_Herald1500Hp_BlueColorAnd1Point5Segments()
    {
        // Herald of the Mist: 1500 HP (niebieski <= 10k, segment = 1 000 HP)
        EnemyHealthBarManager.EvaluateTier(1500f, out Color color, out float segValue, out float segCount);

        Assert.AreEqual(1000f, segValue, 0.001f);
        Assert.AreEqual(1.5f, segCount, 0.001f);
        Assert.Greater(color.b, color.r);
        Assert.Greater(color.b, color.g);
    }

    [Test]
    public void Test05_TierBlue_Boundary10000Hp_Exactly10Segments()
    {
        EnemyHealthBarManager.EvaluateTier(10000f, out Color color, out float segValue, out float segCount);

        Assert.AreEqual(1000f, segValue, 0.001f);
        Assert.AreEqual(10f, segCount, 0.001f);
        Assert.Greater(color.b, color.r);
    }

    [Test]
    public void Test06_TierGreen_45000Hp_GreenColorAnd4Point5Segments()
    {
        EnemyHealthBarManager.EvaluateTier(45000f, out Color color, out float segValue, out float segCount);

        Assert.AreEqual(10000f, segValue, 0.001f);
        Assert.AreEqual(4.5f, segCount, 0.001f);
        Assert.Greater(color.g, color.r);
        Assert.Greater(color.g, color.b);
    }

    [Test]
    public void Test07_TierGreen_Boundary100000Hp_Exactly10Segments()
    {
        EnemyHealthBarManager.EvaluateTier(100000f, out Color color, out float segValue, out float segCount);

        Assert.AreEqual(10000f, segValue, 0.001f);
        Assert.AreEqual(10f, segCount, 0.001f);
        Assert.Greater(color.g, color.r);
    }

    [Test]
    public void Test08_TierPurple_250000Hp_PurpleColorAnd2Point5Segments()
    {
        // Fioletowy > 100k, segment = 100 000 HP
        EnemyHealthBarManager.EvaluateTier(250000f, out Color color, out float segValue, out float segCount);

        Assert.AreEqual(100000f, segValue, 0.001f);
        Assert.AreEqual(2.5f, segCount, 0.001f);
        Assert.Greater(color.r, color.g);
        Assert.Greater(color.b, color.g);
    }

    [Test]
    public void Test09_TierPurple_ReadabilityCap_5MillionHp_CappedAt20Segments()
    {
        // 5 000 000 HP = 50 segmentów, ale limit czytelności ogranicza do 20 widocznych
        EnemyHealthBarManager.EvaluateTier(5000000f, out Color color, out float segValue, out float segCount);

        Assert.AreEqual(100000f, segValue, 0.001f);
        Assert.AreEqual(20f, segCount, 0.001f, "Liczba widocznych podziałek musi być ograniczona do maxVisiblePurpleSegments (20)");
    }

    // =========================================================================
    // 2. ORCHESTRATOR VISIBILITY FLAG TESTS
    // =========================================================================

    [Test]
    public void Test10_VisibilityFlag_DefaultIsTrue_ToggleDisablesHealthBar()
    {
        GameObject mgrObj = new GameObject("HealthBarManager");
        EnemyHealthBarManager mgr = mgrObj.AddComponent<EnemyHealthBarManager>();
        testGameObjects.Add(mgrObj);

        Assert.IsTrue(mgr.IsVisible);

        GameObject enemyObj = new GameObject("Enemy");
        EnemyStats stats = enemyObj.AddComponent<EnemyStats>();
        stats.SetHealthManually(500f);

        GameObject barObj = new GameObject("Healthbar");
        barObj.transform.SetParent(enemyObj.transform);
        EnemyHealthBar bar = barObj.AddComponent<EnemyHealthBar>();
        bar.stats = stats;
        bar.InitializeComponents();
        bar.OnEnable();
        testGameObjects.Add(enemyObj);

        MeshRenderer mr = barObj.GetComponent<MeshRenderer>();
        Assert.IsNotNull(mr);
        Assert.IsTrue(mr.enabled);

        // Toggle to false
        mgr.SetVisible(false);
        Assert.IsFalse(mr.enabled, "Wyłączenie flagi isVisible w managerze musi natychmiast ukryć pasek");

        // Toggle to true
        mgr.SetVisible(true);
        Assert.IsTrue(mr.enabled, "Włączenie flagi isVisible w managerze musi przywrócić widoczność paska");
    }

    // =========================================================================
    // 3. SCALE COMPENSATION (STATIC WORLD-SPACE SIZE)
    // =========================================================================

    [Test]
    public void Test11_ScaleCompensation_DifferentParentScales_ProduceSameWorldSize()
    {
        // 1. Zwykły wróg (skala 0.3)
        GameObject enemyNormal = new GameObject("EnemyNormal");
        enemyNormal.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        GameObject barNormalObj = new GameObject("Healthbar");
        barNormalObj.transform.SetParent(enemyNormal.transform);
        EnemyHealthBar barNormal = barNormalObj.AddComponent<EnemyHealthBar>();
        barNormal.InitializeComponents();
        barNormal.UpdateTransform();
        testGameObjects.Add(enemyNormal);

        // 2. Elitarny wróg (skala 0.36)
        GameObject enemyElite = new GameObject("EnemyElite");
        enemyElite.transform.localScale = new Vector3(0.36f, 0.36f, 0.36f);
        GameObject barEliteObj = new GameObject("Healthbar");
        barEliteObj.transform.SetParent(enemyElite.transform);
        EnemyHealthBar barElite = barEliteObj.AddComponent<EnemyHealthBar>();
        barElite.InitializeComponents();
        barElite.UpdateTransform();
        testGameObjects.Add(enemyElite);

        // 3. Pomniejszony Blob (skala 0.18)
        GameObject enemyBlob = new GameObject("EnemyBlob");
        enemyBlob.transform.localScale = new Vector3(0.18f, 0.18f, 0.18f);
        GameObject barBlobObj = new GameObject("Healthbar");
        barBlobObj.transform.SetParent(enemyBlob.transform);
        EnemyHealthBar barBlob = barBlobObj.AddComponent<EnemyHealthBar>();
        barBlob.InitializeComponents();
        barBlob.UpdateTransform();
        testGameObjects.Add(enemyBlob);

        // Weryfikacja: rozmiar w świecie (lossyScale) wynosi dokładnie 0.96 x 0.24
        Assert.AreEqual(0.96f, barNormalObj.transform.lossyScale.x, 0.005f);
        Assert.AreEqual(0.24f, barNormalObj.transform.lossyScale.y, 0.005f);

        Assert.AreEqual(0.96f, barEliteObj.transform.lossyScale.x, 0.005f);
        Assert.AreEqual(0.24f, barEliteObj.transform.lossyScale.y, 0.005f);

        Assert.AreEqual(0.96f, barBlobObj.transform.lossyScale.x, 0.005f);
        Assert.AreEqual(0.24f, barBlobObj.transform.lossyScale.y, 0.005f);
    }

    // =========================================================================
    // 4. MATERIAL PROPERTY BLOCK ISOLATION TESTS
    // =========================================================================

    [Test]
    public void Test12_MaterialPropertyBlock_DamageAndResetColor_DoesNotWipeHealthBar()
    {
        GameObject enemyObj = new GameObject("EnemyMulti");
        MeshRenderer bodyRenderer = enemyObj.AddComponent<MeshRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard")
                     ?? Shader.Find("Sprites/Default");
        bodyRenderer.sharedMaterial = new Material(shader);

        EnemyStats stats = enemyObj.AddComponent<EnemyStats>();
        stats.SetHealthManually(500f);

        GameObject barObj = new GameObject("Healthbar");
        barObj.transform.SetParent(enemyObj.transform);
        EnemyHealthBar bar = barObj.AddComponent<EnemyHealthBar>();
        bar.stats = stats;
        bar.InitializeComponents();
        bar.OnEnable();
        testGameObjects.Add(enemyObj);

        MethodInfo awakeStats = typeof(EnemyStats).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
        awakeStats.Invoke(stats, null);

        // Weź renderer paska
        MeshRenderer barRenderer = barObj.GetComponent<MeshRenderer>();
        Assert.IsNotNull(barRenderer);
        Assert.IsTrue(barRenderer.HasPropertyBlock());

        // Zadaj obrażenia (wywoła FlashColor i ResetColor)
        stats.TakeDamage(100f, DamageType.Physical, 0f, 0f, false, 1f);
        stats.ResetColor();

        // Pasek zdrowia nadal musi mieć swój PropertyBlock i zaktualizowaną wartość HP
        Assert.IsTrue(barRenderer.HasPropertyBlock(), "Pasek zdrowia nie może utracić PropertyBlocka po ResetColor()");
        Assert.AreEqual(400f, stats.GetCurrentHealth(), 0.1f);
    }

    // =========================================================================
    // AUTOMATIC RUNNER FOR EDITMODE / LOG VERIFICATION
    // =========================================================================

    [MenuItem("Tools/Run Enemy Health Bar Tests")]
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

        EnemyHealthBarTests suite = new EnemyHealthBarTests();
        MethodInfo[] methods = typeof(EnemyHealthBarTests).GetMethods(BindingFlags.Public | BindingFlags.Instance);

        Debug.Log("<color=cyan>[EnemyHealthBarTests] Rozpoczynanie weryfikacji empirycznej testów NUnit...</color>");

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

        Debug.Log($"<color=yellow>[EnemyHealthBarTests] Zakończono: {passed} PASSED, {failed} FAILED z {passed + failed} testów.</color>");
        return failed == 0;
    }
}
