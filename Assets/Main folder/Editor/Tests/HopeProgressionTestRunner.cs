using UnityEditor;
using UnityEngine;

public static class HopeProgressionTestRunner
{
    [MenuItem("Tools/Tests/Run Hope Progression Tests", priority = 105)]
    public static void RunTests()
    {
        var test = new HopeProgressionTests();
        int passed = 0;
        int failed = 0;

        void Run(System.Action action, string name)
        {
            test.SetUp();
            try
            {
                action();
                passed++;
                Debug.Log($"<color=green>[HopeTest PASS]</color> {name}");
            }
            catch (System.Exception ex)
            {
                failed++;
                Debug.LogError($"<color=red>[HopeTest FAIL]</color> {name}: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                test.TearDown();
            }
        }

        Run(test.WavePointsSchedule_CalculatesExpectedBaselines, "WavePointsSchedule_CalculatesExpectedBaselines");
        Run(test.SimulateRun_Wave15_Approximates185Hope, "SimulateRun_Wave15_Approximates185Hope");
        Run(test.SimulateRun_Wave40_Reaches3000HopeTarget, "SimulateRun_Wave40_Reaches3000HopeTarget");
        Run(test.SimulateRun_Wave50_Victory_Reaches5500HopeTarget, "SimulateRun_Wave50_Victory_Reaches5500HopeTarget");
        Run(test.EndSession_PersistsTotalHope_ToSaveManager, "EndSession_PersistsTotalHope_ToSaveManager");
        Run(test.Milestones_DoNotAwardDuplicates_InSingleRun, "Milestones_DoNotAwardDuplicates_InSingleRun");

        Debug.Log($"<b>[HopeProgressionTests] Podsumowanie: {passed} PASSED, {failed} FAILED</b>");
    }
}
