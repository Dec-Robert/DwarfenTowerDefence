using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class AllAutomatedTestsRunner
{
    [MenuItem("Tools/Tests/Run All Automated Tests", priority = 100)]
    public static void RunAllTests()
    {
        Debug.Log("==========================================================================");
        Debug.Log("<color=cyan><b>[AllAutomatedTestsRunner] STARTING FULL AUTOMATED TEST SUITE</b></color>");
        Debug.Log("==========================================================================");

        int totalPassed = 0;
        int totalFailed = 0;

        // 1. Combat Optimization Tests
        if (CombatOptimizationTests.RunAllTestsEmpirically(out int coPassed, out int coFailed, out List<string> coErrors))
        {
            totalPassed += coPassed;
            totalFailed += coFailed;
        }

        // 2. Enemy Health Bar Tests
        if (EnemyHealthBarTests.RunAllTestsEmpirically(out int ehPassed, out int ehFailed, out List<string> ehErrors))
        {
            totalPassed += ehPassed;
            totalFailed += ehFailed;
        }

        // 3. Resource Gathering & Tooltip Tests
        if (ResourceGatheringAndTooltipTests.RunAllTestsEmpirically(out int rgPassed, out int rgFailed, out List<string> rgErrors))
        {
            totalPassed += rgPassed;
            totalFailed += rgFailed;
        }

        // 4. Building Worker Overlay Tests
        BuildingWorkerOverlayTestRunner.RunTests();

        // 5. Daily Resource Balance Tests
        DailyResourceBalanceTestRunner.RunTests();

        // 6. Dusk Economy Cycle Tests
        RunNUnitSuiteEmpirically<DuskEconomyCycleTests>("DuskEconomyCycleTests", ref totalPassed, ref totalFailed);

        // 7. Day/Night Lighting & Building Lights Tests
        RunNUnitSuiteEmpirically<DayNightAndBuildingLightTests>("DayNightAndBuildingLightTests", ref totalPassed, ref totalFailed);

        Debug.Log("==========================================================================");
        if (totalFailed == 0)
        {
            Debug.Log($"<color=green><b>[AllAutomatedTestsRunner] ALL TEST SUITES EXECUTED SUCCESSFULLY!</b></color>");
        }
        else
        {
            Debug.LogError($"<color=red><b>[AllAutomatedTestsRunner] FINISHED WITH FAILURES ({totalFailed} failed across test suites)</b></color>");
        }
        Debug.Log("==========================================================================");
    }

    private static void RunNUnitSuiteEmpirically<T>(string suiteName, ref int totalPassed, ref int totalFailed) where T : new()
    {
        var suite = new T();
        var setUp = typeof(T).GetMethod("SetUp");
        var tearDown = typeof(T).GetMethod("TearDown");
        var methods = typeof(T).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        int passed = 0;
        int failed = 0;

        foreach (var m in methods)
        {
            if (!m.IsDefined(typeof(NUnit.Framework.TestAttribute), false)) continue;
            try
            {
                setUp?.Invoke(suite, null);
                m.Invoke(suite, null);
                passed++;
            }
            catch (System.Exception ex)
            {
                failed++;
                Debug.LogError($"<color=red>[{suiteName} FAIL]</color> {m.Name}: {ex.InnerException?.Message ?? ex.Message}");
            }
            finally
            {
                try { tearDown?.Invoke(suite, null); } catch { }
            }
        }

        totalPassed += passed;
        totalFailed += failed;
        Debug.Log($"[{suiteName}] Finished: {passed} PASSED, {failed} FAILED");
    }
}
