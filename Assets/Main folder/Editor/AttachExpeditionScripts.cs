using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class AttachExpeditionScripts
{
    private const string SessionKey = "AttachExpeditionScripts_Done_v1";
    private const string ScenePath = "Assets/Main folder/_Scenes/TESTSCENE_UI.unity";
    private const string HudPrefabPath = "Assets/Main folder/Prefabs/UI/HUD_UI.prefab";
    private const string VisualsDBPath = "Assets/Main folder/ScriptableObjects/Configs/ResourceVisualDatabase.asset";
    private const string EqSlotPrefabPath = "Assets/Main folder/Prefabs/UI/Pnl_ExpeditionEq_Slot.prefab";
    private const string MissionPrefabPath = "Assets/Main folder/Prefabs/UI/Pnl_Expedition_Mission.prefab";
    private const string FeatureTilePrefabPath = "Assets/Main folder/Prefabs/UI/Pnl_ReconFeatureTile.prefab";

    static AttachExpeditionScripts()
    {
        // EditorApplication.delayCall += Execute;
    }

    [MenuItem("Tools/Attach Expedition UI Scripts")]
    public static void Execute()
    {
        var visualsDB = AssetDatabase.LoadAssetAtPath<ResourceVisualsSO>(VisualsDBPath);
        var eqSlotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EqSlotPrefabPath);
        var missionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MissionPrefabPath);
        var featureTilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FeatureTilePrefabPath);

        // 1. Zaktualizuj prefab HUD_UI
        GameObject hudRoot = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
        if (hudRoot != null)
        {
            GameObject hudInstance = PrefabUtility.LoadPrefabContents(HudPrefabPath);
            bool hudDirty = false;

            Transform expCenterTr = hudInstance.transform.Find("Canvas_ContextWindows/Pnl_ExpeditionCenterWindow")
                                ?? FindRecursive(hudInstance.transform, "Pnl_ExpeditionCenterWindow");
            if (expCenterTr != null)
            {
                var expScript = expCenterTr.GetComponent<ExpeditionCenterWindowUI>();
                if (expScript == null) expScript = expCenterTr.gameObject.AddComponent<ExpeditionCenterWindowUI>();

                var so = new SerializedObject(expScript);
                so.Update();
                if (eqSlotPrefab != null) so.FindProperty("slotBadgePrefab").objectReferenceValue = eqSlotPrefab;
                if (missionPrefab != null) so.FindProperty("missionCardPrefab").objectReferenceValue = missionPrefab;
                so.ApplyModifiedProperties();

                expScript.AutoBindReferences();
                hudDirty = true;
            }

            Transform chunkModalTr = hudInstance.transform.Find("Canvas_ContextWindows/Pnl_ChunkExpeditionModal")
                                 ?? FindRecursive(hudInstance.transform, "Pnl_ChunkExpeditionModal");
            if (chunkModalTr != null)
            {
                var modalScript = chunkModalTr.GetComponent<ChunkExpeditionModalUI>();
                if (modalScript == null) modalScript = chunkModalTr.gameObject.AddComponent<ChunkExpeditionModalUI>();

                var so = new SerializedObject(modalScript);
                so.Update();
                if (featureTilePrefab != null) so.FindProperty("featureTilePrefab").objectReferenceValue = featureTilePrefab;
                if (eqSlotPrefab != null) so.FindProperty("slotBadgePrefab").objectReferenceValue = eqSlotPrefab;
                if (visualsDB != null) so.FindProperty("resourceVisuals").objectReferenceValue = visualsDB;
                so.ApplyModifiedProperties();

                modalScript.AutoBindReferences();
                hudDirty = true;
            }

            CenterInfoPanelControllerUI centerCtrl = hudInstance.GetComponentInChildren<CenterInfoPanelControllerUI>(true);
            if (centerCtrl != null)
            {
                var soCenter = new SerializedObject(centerCtrl);
                soCenter.Update();
                if (expCenterTr != null) soCenter.FindProperty("expeditionCenterPanel").objectReferenceValue = expCenterTr.gameObject;
                if (chunkModalTr != null) soCenter.FindProperty("chunkExpeditionModal").objectReferenceValue = chunkModalTr.gameObject;
                soCenter.ApplyModifiedProperties();
                hudDirty = true;
            }

            if (hudDirty)
            {
                PrefabUtility.SaveAsPrefabAsset(hudInstance, HudPrefabPath);
                Debug.Log("[AttachExpeditionScripts] Zaktualizowano HUD_UI.prefab z komponentami wypraw.");
            }
            PrefabUtility.UnloadPrefabContents(hudInstance);
        }

        // 2. Zaktualizuj aktywną scenę TESTSCENE_UI
        var currentScene = EditorSceneManager.GetActiveScene();
        if (currentScene.path != ScenePath)
        {
            currentScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject canvasObj = GameObject.Find("Canvas_ContextWindows");
        if (canvasObj == null)
        {
            Debug.LogError("[AttachExpeditionScripts] Nie znaleziono Canvas_ContextWindows w scenie!");
            return;
        }

        bool dirty = false;

        Transform expCenterSceneTr = canvasObj.transform.Find("Pnl_ExpeditionCenterWindow");
        if (expCenterSceneTr != null)
        {
            var expScript = expCenterSceneTr.GetComponent<ExpeditionCenterWindowUI>();
            if (expScript == null) expScript = expCenterSceneTr.gameObject.AddComponent<ExpeditionCenterWindowUI>();

            var so = new SerializedObject(expScript);
            so.Update();
            if (eqSlotPrefab != null) so.FindProperty("slotBadgePrefab").objectReferenceValue = eqSlotPrefab;
            if (missionPrefab != null) so.FindProperty("missionCardPrefab").objectReferenceValue = missionPrefab;
            so.ApplyModifiedProperties();

            expScript.AutoBindReferences();
            EditorUtility.SetDirty(expCenterSceneTr.gameObject);
            dirty = true;
        }

        Transform chunkModalSceneTr = canvasObj.transform.Find("Pnl_ChunkExpeditionModal");
        if (chunkModalSceneTr != null)
        {
            var modalScript = chunkModalSceneTr.GetComponent<ChunkExpeditionModalUI>();
            if (modalScript == null) modalScript = chunkModalSceneTr.gameObject.AddComponent<ChunkExpeditionModalUI>();

            var so = new SerializedObject(modalScript);
            so.Update();
            if (featureTilePrefab != null) so.FindProperty("featureTilePrefab").objectReferenceValue = featureTilePrefab;
            if (eqSlotPrefab != null) so.FindProperty("slotBadgePrefab").objectReferenceValue = eqSlotPrefab;
            if (visualsDB != null) so.FindProperty("resourceVisuals").objectReferenceValue = visualsDB;
            so.ApplyModifiedProperties();

            modalScript.AutoBindReferences();
            EditorUtility.SetDirty(chunkModalSceneTr.gameObject);
            dirty = true;
        }

        CenterInfoPanelControllerUI centerController = Object.FindAnyObjectByType<CenterInfoPanelControllerUI>();
        if (centerController == null)
        {
            centerController = canvasObj.GetComponent<CenterInfoPanelControllerUI>();
        }

        if (centerController != null)
        {
            var soCenter = new SerializedObject(centerController);
            soCenter.Update();
            if (expCenterSceneTr != null) soCenter.FindProperty("expeditionCenterPanel").objectReferenceValue = expCenterSceneTr.gameObject;
            if (chunkModalSceneTr != null) soCenter.FindProperty("chunkExpeditionModal").objectReferenceValue = chunkModalSceneTr.gameObject;
            soCenter.ApplyModifiedProperties();
            EditorUtility.SetDirty(centerController);
            dirty = true;
        }

        if (dirty)
        {
            EditorSceneManager.MarkSceneDirty(currentScene);
            EditorSceneManager.SaveScene(currentScene);
            AssetDatabase.SaveAssets();
            Debug.Log("[AttachExpeditionScripts] Pomyślnie zaktualizowano scenę TESTSCENE_UI z komponentami wypraw!");
        }
    }

    private static Transform FindRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var res = FindRecursive(parent.GetChild(i), name);
            if (res != null) return res;
        }
        return null;
    }
}
