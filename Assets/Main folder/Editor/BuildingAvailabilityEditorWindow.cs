#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Minimalistyczne narzędzie edytora wyświetlające wszystkie budynki (ScriptableObject)
/// i pozwalające włączyć/wyłączyć możliwość ich budowy.
/// </summary>
public class BuildingAvailabilityEditorWindow : EditorWindow
{
    private readonly List<BuildingData> allBuildings = new List<BuildingData>();
    private Vector2 scrollPos;

    private GUIStyle rowEvenStyle;
    private GUIStyle rowOddStyle;
    private GUIStyle headerLabelStyle;
    private bool stylesInitialized;

    [MenuItem("Tools/Buildings/Building Availability Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<BuildingAvailabilityEditorWindow>("Budynki (Włącz/Wyłącz)");
        window.minSize = new Vector2(460, 420);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshBuildings();
    }

    private void OnFocus()
    {
        RefreshBuildings();
    }

    private void RefreshBuildings()
    {
        allBuildings.Clear();
        string[] guids = AssetDatabase.FindAssets("t:BuildingData");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BuildingData data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
            if (data != null && !allBuildings.Contains(data))
            {
                allBuildings.Add(data);
            }
        }

        var sorted = allBuildings
            .OrderBy(b => (int)b.type)
            .ThenBy(b => string.IsNullOrEmpty(b.buildingName) ? b.name : b.buildingName)
            .ToList();

        allBuildings.Clear();
        allBuildings.AddRange(sorted);
    }

    private void InitStylesIfNeeded()
    {
        if (stylesInitialized && rowEvenStyle != null) return;

        rowEvenStyle = new GUIStyle(GUI.skin.box);
        rowEvenStyle.normal.background = MakeTex(1, 1, new Color(0.22f, 0.22f, 0.22f, 0.45f));

        rowOddStyle = new GUIStyle(GUI.skin.box);
        rowOddStyle.normal.background = MakeTex(1, 1, new Color(0.28f, 0.28f, 0.28f, 0.45f));

        headerLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleLeft
        };

        stylesInitialized = true;
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        Texture2D tex = new Texture2D(width, height);
        tex.SetPixels(pix);
        tex.Apply();
        return tex;
    }

    private void OnGUI()
    {
        InitStylesIfNeeded();

        const float iconColWidth = 36f;
        const float categoryColWidth = 100f;
        const float toggleColWidth = 80f;

        // Nagłówek tabeli
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(24));
        GUILayout.Label("Ikona", headerLabelStyle, GUILayout.Width(iconColWidth));
        GUILayout.Label("Nazwa budynku", headerLabelStyle, GUILayout.ExpandWidth(true));
        GUILayout.Label("Kategoria", headerLabelStyle, GUILayout.Width(categoryColWidth));
        GUILayout.Label("Budowalny", headerLabelStyle, GUILayout.Width(toggleColWidth));
        EditorGUILayout.EndHorizontal();

        // Lista budynków
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        for (int i = 0; i < allBuildings.Count; i++)
        {
            BuildingData b = allBuildings[i];
            if (b == null) continue;

            GUIStyle rowStyle = (i % 2 == 0) ? rowEvenStyle : rowOddStyle;
            EditorGUILayout.BeginHorizontal(rowStyle, GUILayout.Height(28));

            // 1. Ikona
            if (b.icon != null && b.icon.texture != null)
            {
                Rect iconRect = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(iconColWidth), GUILayout.Height(24));
                GUI.DrawTexture(new Rect(iconRect.x + 4, iconRect.y + 2, 24, 24), b.icon.texture, ScaleMode.ScaleToFit);
            }
            else
            {
                GUILayout.Label("—", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(iconColWidth), GUILayout.Height(24));
            }

            // 2. Nazwa budynku
            string displayName = string.IsNullOrEmpty(b.buildingName) ? b.name : $"{b.buildingName} ({b.name})";
            if (GUILayout.Button(displayName, EditorStyles.label, GUILayout.ExpandWidth(true), GUILayout.Height(24)))
            {
                Selection.activeObject = b;
                EditorGUIUtility.PingObject(b);
            }

            // 3. Kategoria
            GUILayout.Label(b.type.ToString(), EditorStyles.miniLabel, GUILayout.Width(categoryColWidth), GUILayout.Height(24));

            // 4. Checkbox Włącz/Wyłącz
            GUILayout.Space(24);
            bool newUnlocked = EditorGUILayout.Toggle(b.isUnlocked, GUILayout.Width(toggleColWidth - 24), GUILayout.Height(24));
            if (newUnlocked != b.isUnlocked)
            {
                SetBuildingUnlocked(b, newUnlocked);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// Ustawia flagę isUnlocked na ScriptableObject budynku i automatycznie synchronizuje
    /// listę allBuildingsDatabase w ExtendedBuildingUI na otwartej scenie (oraz odświeża UI w Play Mode).
    /// </summary>
    public static void SetBuildingUnlocked(BuildingData building, bool unlocked)
    {
        if (building == null) return;

        Undo.RecordObject(building, "Toggle Building Availability");
        building.isUnlocked = unlocked;
        EditorUtility.SetDirty(building);
        AssetDatabase.SaveAssetIfDirty(building);

        SyncWithSceneExtendedBuildingUI(building, unlocked);
    }

    public static void SyncWithSceneExtendedBuildingUI(BuildingData building, bool unlocked)
    {
        if (building == null) return;

        var uiComponents = Object.FindObjectsByType<ExtendedBuildingUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ui in uiComponents)
        {
            if (ui == null) continue;

            var db = ui.AllBuildingsDatabase;
            if (db == null) continue;

            bool modified = false;

            if (unlocked)
            {
                if (!db.Contains(building))
                {
                    Undo.RecordObject(ui, "Add Building to ExtendedBuildingUI");
                    db.Add(building);
                    modified = true;
                }
            }
            else
            {
                if (db.Contains(building))
                {
                    Undo.RecordObject(ui, "Remove Building from ExtendedBuildingUI");
                    db.Remove(building);
                    modified = true;
                }
            }

            if (modified)
            {
                EditorUtility.SetDirty(ui);
                if (!Application.isPlaying && ui.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
                }
            }

            if (Application.isPlaying)
            {
                ui.RefreshAvailableBuildings();
            }
        }
    }
}
#endif
