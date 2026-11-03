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
}
