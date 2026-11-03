using UnityEditor;
using UnityEngine;

public static class DailyResourceBalanceTestRunner
{
    [MenuItem("Tools/Tests/Run Daily Resource Balance Tests", priority = 103)]
    public static void RunTests()
    {
        var test = new DailyResourceBalanceTests();
        int passed = 0;
        int failed = 0;

        void Run(System.Action action, string name)
        {
            test.SetUp();
            try
            {
                action();
                passed++;
                Debug.Log($"[DailyTest PASS] {name}");
            }
            catch (System.Exception ex)
            {
                failed++;
                Debug.LogError($"[DailyTest FAIL] {name}: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                test.TearDown();
            }
        }

        Run(test.Test01_EconomicBuildings_AggregatedByDisplayName, "Test01_EconomicBuildings_AggregatedByDisplayName");
        Run(test.Test02_EconomicUpkeep_AggregatedByDisplayName, "Test02_EconomicUpkeep_AggregatedByDisplayName");
        Run(test.Test03_HousingSurvivalAndGrowth_SeparatedInConsumption, "Test03_HousingSurvivalAndGrowth_SeparatedInConsumption");
        Run(test.Test04_TowerDefenseUpkeep_OnlyWhenHasWorkers, "Test04_TowerDefenseUpkeep_OnlyWhenHasWorkers");
        Run(test.Test05_NetDailyBalance_CalculationCorrectness, "Test05_NetDailyBalance_CalculationCorrectness");

        Debug.Log($"[DailyResourceBalanceTests] Finished: {passed} PASSED, {failed} FAILED");
    }
}
