using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class AttachRuneForgeScripts
{
    private const string SessionKey = "AttachRuneForgeScripts_Done_v3";
    private const string ScenePath = "Assets/Main folder/_Scenes/TESTSCENE_UI.unity";
    private const string VisualsDBPath = "Assets/Main folder/ScriptableObjects/Configs/ResourceVisualDatabase.asset";
    private const string CardPrefabPath = "Assets/Main folder/Prefabs/UI/Card_Rune_Item.prefab";
    private const string StatInfoPrefabPath = "Assets/Main folder/Prefabs/UI/Pnl_StatInfo.prefab";

    static AttachRuneForgeScripts()
    {
        EditorApplication.delayCall += Execute;
    }

    [MenuItem("Tools/Attach Rune Forge UI Scripts")]
    public static void Execute()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);

        // 1. Aktualizacja prefabu Card_Rune_Item
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        if (cardPrefab != null)
        {
            var cardScript = cardPrefab.GetComponent<RuneCardItemUI>();
            if (cardScript == null) cardScript = cardPrefab.AddComponent<RuneCardItemUI>();

            var visualsDB = AssetDatabase.LoadAssetAtPath<ResourceVisualsSO>(VisualsDBPath);
            var statInfoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StatInfoPrefabPath);

            var so = new SerializedObject(cardScript);
            so.Update();

            var imgIconProp = so.FindProperty("imgRuneIcon");
            if (imgIconProp != null && imgIconProp.objectReferenceValue == null)
            {
                imgIconProp.objectReferenceValue = cardPrefab.transform.Find("Img_RuneIcon")?.GetComponent<UnityEngine.UI.Image>();
            }

            var imgBgProp = so.FindProperty("imgCardBackground");
            if (imgBgProp != null && imgBgProp.objectReferenceValue == null)
            {
                imgBgProp.objectReferenceValue = cardPrefab.GetComponent<UnityEngine.UI.Image>();
            }

            var pnlContainerProp = so.FindProperty("pnlStatContainer");
            if (pnlContainerProp != null && pnlContainerProp.objectReferenceValue == null)
            {
                pnlContainerProp.objectReferenceValue = cardPrefab.transform.Find("Pnl_StatContainer");
            }

            var statInfoProp = so.FindProperty("statInfoPrefab");
            if (statInfoProp != null && statInfoProp.objectReferenceValue == null)
            {
                statInfoProp.objectReferenceValue = statInfoPrefab;
            }

            var visualsProp = so.FindProperty("resourceVisuals");
            if (visualsProp != null && visualsProp.objectReferenceValue == null)
            {
                visualsProp.objectReferenceValue = visualsDB;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(cardPrefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[AttachRuneForgeScripts] Zaktualizowano prefab Card_Rune_Item z komponentem RuneCardItemUI.");
        }

        // 2. Obsługa sceny
        var currentScene = EditorSceneManager.GetActiveScene();
        if (currentScene.path != ScenePath)
        {
            currentScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject canvasObj = GameObject.Find("Canvas_ContextWindows");
        if (canvasObj == null)
        {
            Debug.LogError("[AttachRuneForgeScripts] Nie znaleziono Canvas_ContextWindows!");
            return;
        }

        bool dirty = false;

        // Modal
        Transform modalTr = canvasObj.transform.Find("Pnl_RuneTowerSelectionModal");
        if (modalTr != null)
        {
            var modalScript = modalTr.GetComponent<RuneTowerSelectionModalUI>();
            if (modalScript == null) modalScript = modalTr.gameObject.AddComponent<RuneTowerSelectionModalUI>();
            modalScript.AutoBindReferences();
            modalScript.InitializeTowers();
            EditorUtility.SetDirty(modalTr.gameObject);
            dirty = true;
        }

        // Fullscreen Forge
        Transform forgeTr = canvasObj.transform.Find("Pnl_RuneForgeFullscreen");
        if (forgeTr != null)
        {
            var forgeScript = forgeTr.GetComponent<RuneForgeWindowUI>();
            if (forgeScript == null) forgeScript = forgeTr.gameObject.AddComponent<RuneForgeWindowUI>();
            forgeScript.AutoBindReferences();

            var dragController = forgeTr.GetComponent<RuneDragDropController>();
            if (dragController == null) dragController = forgeTr.gameObject.AddComponent<RuneDragDropController>();

            // Przypisanie bazy wizualiów do okna kuźni
            var visualsDB = AssetDatabase.LoadAssetAtPath<ResourceVisualsSO>(VisualsDBPath);
            if (visualsDB != null)
            {
                var forgeSo = new SerializedObject(forgeScript);
                forgeSo.Update();
                var visProp = forgeSo.FindProperty("resourceVisualsDB");
                if (visProp != null)
                {
                    visProp.objectReferenceValue = visualsDB;
                    forgeSo.ApplyModifiedProperties();
                }
            }

            EditorUtility.SetDirty(forgeTr.gameObject);
            dirty = true;
        }

        // Center controller
        CenterInfoPanelControllerUI centerController = Object.FindAnyObjectByType<CenterInfoPanelControllerUI>();
        if (centerController == null)
        {
            centerController = canvasObj.GetComponent<CenterInfoPanelControllerUI>();
            if (centerController == null) centerController = canvasObj.AddComponent<CenterInfoPanelControllerUI>();
        }

        if (centerController != null)
        {
            if (forgeTr != null) centerController.runeForgePanel = forgeTr.gameObject;
            if (modalTr != null) centerController.runeTowerSelectionModal = modalTr.gameObject;
            EditorUtility.SetDirty(centerController);
            dirty = true;
        }

        if (dirty)
        {
            EditorSceneManager.MarkSceneDirty(currentScene);
            EditorSceneManager.SaveScene(currentScene);
            AssetDatabase.SaveAssets();
            Debug.Log("[AttachRuneForgeScripts] Pomyślnie zaktualizowano referencje w scenie TESTSCENE_UI!");
        }
    }
}
