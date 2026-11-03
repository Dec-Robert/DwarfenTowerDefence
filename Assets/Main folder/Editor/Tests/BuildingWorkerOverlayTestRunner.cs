using UnityEditor;
using UnityEngine;

public static class BuildingWorkerOverlayTestRunner
{
    [MenuItem("Tools/Tests/Run Building Worker Overlay Tests", priority = 101)]
    public static void RunTests()
    {
        var test = new BuildingWorkerOverlayTests();
        int passed = 0;
        int failed = 0;

        void Run(System.Action action, string name)
        {
            test.SetUp();
            try
            {
                action();
                passed++;
                Debug.Log($"[OverlayTest PASS] {name}");
            }
            catch (System.Exception ex)
            {
                failed++;
                Debug.LogError($"[OverlayTest FAIL] {name}: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                test.TearDown();
            }
        }

        Run(test.Test01_IsEligibleBuilding_NullOrHousing_ReturnsFalse, "Test01_IsEligibleBuilding_NullOrHousing_ReturnsFalse");
        Run(test.Test02_IsEligibleBuilding_ProductionWithWorkers_ReturnsTrue, "Test02_IsEligibleBuilding_ProductionWithWorkers_ReturnsTrue");
        Run(test.Test03_ToggleVisibility_FiresEventAndUpdatesState, "Test03_ToggleVisibility_FiresEventAndUpdatesState");
        Run(test.Test04_RaceColors_MatchDesignGuidelines, "Test04_RaceColors_MatchDesignGuidelines");
        Run(test.Test05_ProceduralSprites_CreatedWith9SliceBorders, "Test05_ProceduralSprites_CreatedWith9SliceBorders");
        Run(test.Test06_ActiveBuilding_ZeroWorkers_ShowsWarningCapsuleOnly, "Test06_ActiveBuilding_ZeroWorkers_ShowsWarningCapsuleOnly");
        Run(test.Test07_ActiveBuilding_WithWorkers_ShowsOnlyAllocatedRaces, "Test07_ActiveBuilding_WithWorkers_ShowsOnlyAllocatedRaces");
        Run(test.Test08_PausedBuilding_ShowsWarningCapsule, "Test08_PausedBuilding_ShowsWarningCapsule");

        Debug.Log($"[BuildingWorkerOverlayTests] Summary: {passed} PASSED, {failed} FAILED");
    }
}
