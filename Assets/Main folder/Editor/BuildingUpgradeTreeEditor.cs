#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class BuildingUpgradeTreeEditor : EditorWindow
{
    // --- Ścieżki ---
    private const string UPGRADES_BASE_PATH = "Assets/Main folder/ScriptableObjects/BuildingUpgrades";

    // --- Stan ---
    private List<BuildingData> validBuildings = new List<BuildingData>();
    private BuildingData selectedBuilding;
    private BuildingUpgradeSO selectedUpgrade;
    private SerializedObject serializedUpgrade;

    // --- UI ---
    private Vector2 buildingListScroll;
    private Vector2 graphScroll;
    private Vector2 inspectorScroll;

    private const float LEFT_PANEL_WIDTH = 220f;
    private const float RIGHT_PANEL_WIDTH = 320f;
    private const float NODE_WIDTH = 160f;
    private const float NODE_HEIGHT = 60f;

    [MenuItem("Tools/Buildings/Upgrade Tree Builder")]
    public static void ShowWindow()
    {
        var window = GetWindow<BuildingUpgradeTreeEditor>("Upgrade Tree Builder");
        window.minSize = new Vector2(1000, 500);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshDatabase();
    }

    private void OnGUI()
    {
        DrawTopBar();

        EditorGUILayout.BeginHorizontal();

        // Lewa strona - Lista budynków
        Rect leftRect = new Rect(0, 30, LEFT_PANEL_WIDTH, position.height - 30);
        DrawBuildingList(leftRect);

        // Środek - Drzewo (Graf)
        Rect graphRect = new Rect(LEFT_PANEL_WIDTH, 30, position.width - LEFT_PANEL_WIDTH - RIGHT_PANEL_WIDTH, position.height - 30);
        DrawGraph(graphRect);

        // Prawa strona - Inspektor Ulepszenia
        Rect inspectorRect = new Rect(position.width - RIGHT_PANEL_WIDTH, 30, RIGHT_PANEL_WIDTH, position.height - 30);
        DrawInspector(inspectorRect);

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================================
    // GÓRNY PASEK
    // =========================================================================
    private void DrawTopBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(30));
        
        if (GUILayout.Button("🔄 Odśwież Listę i Foldery", EditorStyles.toolbarButton, GUILayout.Width(180)))
        {
            RefreshDatabase();
        }

        GUILayout.Space(20);
        if (selectedBuilding != null)
        {
            GUILayout.Label($"Edytujesz drzewko: {selectedBuilding.buildingName}", EditorStyles.boldLabel);
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Pokaż bazowy folder ulepszeń", EditorStyles.toolbarButton))
        {
            EnsureBaseFolderExists();
            Object obj = AssetDatabase.LoadAssetAtPath<Object>(UPGRADES_BASE_PATH);
            EditorGUIUtility.PingObject(obj);
        }

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================================
    // LEWY PANEL (Lista budynków)
    // =========================================================================
    private void DrawBuildingList(Rect rect)
    {
        GUILayout.BeginArea(rect, EditorStyles.helpBox);
        
        GUILayout.Label("Dostępne Budynki", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        buildingListScroll = EditorGUILayout.BeginScrollView(buildingListScroll);

        string currentCategory = "";

        foreach (var building in validBuildings)
        {
            if (building == null) continue;

            // Kategoryzacja wizualna
            string category = building.type.ToString();
            if (category != currentCategory)
            {
                currentCategory = category;
                EditorGUILayout.Space(10);
                GUI.backgroundColor = new Color(0.4f, 0.4f, 0.4f);
                GUILayout.Label($"--- {currentCategory.ToUpper()} ---", EditorStyles.toolbarButton);
                GUI.backgroundColor = Color.white;
            }

            // Kolorowanie wybranego
            GUI.backgroundColor = (selectedBuilding == building) ? new Color(0.3f, 0.6f, 1f) : Color.white;
            
            if (GUILayout.Button(building.buildingName, EditorStyles.miniButtonLeft, GUILayout.Height(25)))
            {
                selectedBuilding = building;
                selectedUpgrade = null;
                serializedUpgrade = null;
                GUI.FocusControl(null);
            }
            
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // =========================================================================
    // ŚRODEK (Graf Drzewka)
    // =========================================================================
    private void DrawGraph(Rect rect)
    {
        GUI.BeginGroup(rect);
        EditorGUI.DrawRect(new Rect(0, 0, rect.width, rect.height), new Color(0.15f, 0.15f, 0.15f));

        if (selectedBuilding == null)
        {
            GUI.Label(new Rect(0, rect.height/2 - 20, rect.width, 40), "Wybierz budynek z listy po lewej.", EditorStyles.centeredGreyMiniLabel);
            GUI.EndGroup();
            return;
        }

        graphScroll = GUI.BeginScrollView(new Rect(0, 0, rect.width, rect.height), graphScroll, new Rect(0, 0, 2000, 2000));

        // Rysuj korzeń (Sam Budynek)
        Rect rootRect = new Rect(rect.width / 2 - NODE_WIDTH / 2, 30, NODE_WIDTH, NODE_HEIGHT);
        DrawNode(rootRect, selectedBuilding.buildingName, "Budynek Bazowy", Color.gray, null);

        if (selectedBuilding.tier1Upgrades == null) selectedBuilding.tier1Upgrades = new List<BuildingUpgradeSO>();

        DrawChildren(rootRect, selectedBuilding.tier1Upgrades, 1, rect.width / 2);
            
        // Przycisk "Dodaj opcję Tier 1" pod korzeniem
        if (GUI.Button(new Rect(rootRect.x + 20, rootRect.yMax + 10, NODE_WIDTH - 40, 20), "+ Dodaj Tier 1"))
        {
            CreateNewUpgrade(selectedBuilding.tier1Upgrades, "Tier1_Nowe_Ulepszenie", selectedBuilding);
        }

        GUI.EndScrollView();
        GUI.EndGroup();
    }

    private void DrawChildren(Rect parentRect, List<BuildingUpgradeSO> children, int tier, float parentCenterX)
    {
        if (children == null || children.Count == 0) return;

        float spacingX = NODE_WIDTH * 1.5f;
        float startX = parentCenterX - ((children.Count - 1) * spacingX) / 2f;
        float y = parentRect.yMax + 70f; 

        for (int i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child == null) continue;

            float x = startX + i * spacingX;
            Rect childRect = new Rect(x - NODE_WIDTH / 2, y, NODE_WIDTH, NODE_HEIGHT);

            // Rysuj linię
            Handles.color = new Color(0.7f, 0.7f, 0.7f);
            Handles.DrawAAPolyLine(3f, new Vector3(parentRect.center.x, parentRect.yMax), new Vector3(childRect.center.x, childRect.y));

            // Rysuj węzeł
            Color nodeColor = (selectedUpgrade == child) ? new Color(0.2f, 0.6f, 0.2f) : new Color(0.2f, 0.3f, 0.4f);
            DrawNode(childRect, child.upgradeName, $"Tier {tier}", nodeColor, child);

            if (child.nextTierOptions == null) child.nextTierOptions = new List<BuildingUpgradeSO>();

            DrawChildren(childRect, child.nextTierOptions, tier + 1, childRect.center.x);

            if (GUI.Button(new Rect(childRect.x + 20, childRect.yMax + 5, NODE_WIDTH - 40, 16), $"+ Dodaj Tier {tier + 1}"))
            {
                CreateNewUpgrade(child.nextTierOptions, $"Tier{tier+1}_Nowe_Ulepszenie", child);
            }
        }
    }

    private void DrawNode(Rect rect, string title, string subtitle, Color color, BuildingUpgradeSO linkedSO)
    {
        EditorGUI.DrawRect(rect, color);
        Handles.color = Color.black;
        Handles.DrawWireCube(rect.center, rect.size);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        GUIStyle subStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.8f, 0.8f, 0.8f) } };

        GUI.Label(new Rect(rect.x, rect.y + 5, rect.width, 20), string.IsNullOrEmpty(title) ? "Bez nazwy" : title, titleStyle);
        GUI.Label(new Rect(rect.x, rect.y + 25, rect.width, 15), subtitle, subStyle);

        if (linkedSO != null)
        {
            if (GUI.Button(rect, "", GUIStyle.none))
            {
                selectedUpgrade = linkedSO;
                serializedUpgrade = new SerializedObject(selectedUpgrade);
                GUI.FocusControl(null);
            }
        }
    }

    // =========================================================================
    // PRAWY PANEL (Inspektor Ulepszenia)
    // =========================================================================
    private void DrawInspector(Rect rect)
    {
        GUILayout.BeginArea(rect, EditorStyles.helpBox);
        inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);

        if (selectedUpgrade == null || serializedUpgrade == null)
        {
            GUILayout.Label("Wybierz ulepszenie na grafie.", EditorStyles.centeredGreyMiniLabel);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            return;
        }

        serializedUpgrade.Update();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Edycja Ulepszenia", EditorStyles.boldLabel);
        if (GUILayout.Button("Plik", GUILayout.Width(40))) EditorGUIUtility.PingObject(selectedUpgrade);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);

        EditorGUILayout.PropertyField(serializedUpgrade.FindProperty("upgradeName"), new GUIContent("Nazwa"));
        EditorGUILayout.PropertyField(serializedUpgrade.FindProperty("description"), new GUIContent("Opis"), GUILayout.Height(50));
        EditorGUILayout.PropertyField(serializedUpgrade.FindProperty("icon"), new GUIContent("Ikona"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("── Modyfikacje Miejsc Pracy ──", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedUpgrade.FindProperty("extraShifts"), new GUIContent("Dodatkowe Zmiany"));
        EditorGUILayout.PropertyField(serializedUpgrade.FindProperty("extraWorkersPerShift"), new GUIContent("Miejsca na zmianę"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("── Ekonomia ──", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedUpgrade.FindProperty("cost"), new GUIContent("Koszt Zakupu"), true);
        EditorGUILayout.PropertyField(serializedUpgrade.FindProperty("productionBonus"), new GUIContent("Bonus do Produkcji"), true);
        EditorGUILayout.PropertyField(serializedUpgrade.FindProperty("upkeepIncrease"), new GUIContent("Zwiększenie Utrzymania"), true);

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedUpgrade.FindProperty("specialEffectID"), new GUIContent("Specjalne ID (np. AUTO_REPLANT)"));

        serializedUpgrade.ApplyModifiedProperties();

        EditorGUILayout.Space(20);
        GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
        if (GUILayout.Button("Usuń to ulepszenie (Z dysku)", GUILayout.Height(30)))
        {
            DeleteSelectedUpgrade();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // =========================================================================
    // LOGIKA (Tworzenie i odświeżanie)
    // =========================================================================
    private void RefreshDatabase()
    {
        validBuildings.Clear();

        // 1. Szukamy wszystkich BuildingData w projekcie
        string[] guids = AssetDatabase.FindAssets("t:BuildingData");
        foreach (string guid in guids)
        {
            BuildingData data = AssetDatabase.LoadAssetAtPath<BuildingData>(AssetDatabase.GUIDToAssetPath(guid));
            
            // 2. Filtrujemy tylko Economic, Utility, Housing
            if (data != null && (data.type == BuildingType.Economic || data.type == BuildingType.Utility || data.type == BuildingType.Housing))
            {
                validBuildings.Add(data);
            }
        }

        // Sortowanie po typie, a potem alfabetycznie
        validBuildings = validBuildings.OrderBy(b => b.type).ThenBy(b => b.name).ToList();

        // 3. Tworzymy foldery dla tych budynków w folderze ulepszeń
        EnsureBaseFolderExists();
        foreach (var building in validBuildings)
        {
            string safeName = SanitizeFolderName(building.name);
            string targetFolder = $"{UPGRADES_BASE_PATH}/{safeName}";

            if (!AssetDatabase.IsValidFolder(targetFolder))
            {
                AssetDatabase.CreateFolder(UPGRADES_BASE_PATH, safeName);
            }
        }

        AssetDatabase.Refresh();
    }

    private void CreateNewUpgrade(List<BuildingUpgradeSO> targetList, string defaultName, Object parentObj)
    {
        string safeBuildingName = SanitizeFolderName(selectedBuilding.name);
        string folderPath = $"{UPGRADES_BASE_PATH}/{safeBuildingName}";
        
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder(UPGRADES_BASE_PATH, safeBuildingName);
        }

        string fullPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{defaultName}.asset");

        BuildingUpgradeSO newUpgrade = CreateInstance<BuildingUpgradeSO>();
        newUpgrade.upgradeName = "Nowe Ulepszenie";

        AssetDatabase.CreateAsset(newUpgrade, fullPath);
        AssetDatabase.SaveAssets();

        targetList.Add(newUpgrade);
        EditorUtility.SetDirty(parentObj);
        
        selectedUpgrade = newUpgrade;
        serializedUpgrade = new SerializedObject(selectedUpgrade);
    }

    private void DeleteSelectedUpgrade()
    {
        if (EditorUtility.DisplayDialog("Usuń Ulepszenie", $"Czy na pewno chcesz usunąć plik '{selectedUpgrade.name}' z dysku?\nOperacji nie można cofnąć.", "Tak, usuń", "Anuluj"))
        {
            string path = AssetDatabase.GetAssetPath(selectedUpgrade);
            RemoveReference(selectedBuilding.tier1Upgrades);
            
            selectedUpgrade = null;
            serializedUpgrade = null;
            
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
        }
    }

    private bool RemoveReference(List<BuildingUpgradeSO> list)
    {
        if (list == null) return false;
        
        if (list.Contains(selectedUpgrade))
        {
            list.Remove(selectedUpgrade);
            EditorUtility.SetDirty(selectedBuilding);
            return true;
        }

        foreach (var child in list)
        {
            if (child != null && RemoveReference(child.nextTierOptions))
            {
                EditorUtility.SetDirty(child);
                return true;
            }
        }
        return false;
    }

    private void EnsureBaseFolderExists()
    {
        if (!AssetDatabase.IsValidFolder(UPGRADES_BASE_PATH))
        {
            // Zakładamy, że nadrzędny folder istnieje. Jeśli nie, tworzymy krok po kroku.
            if (!AssetDatabase.IsValidFolder("Assets/Main folder/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets/Main folder", "ScriptableObjects");
                
            AssetDatabase.CreateFolder("Assets/Main folder/ScriptableObjects", "BuildingUpgrades");
        }
    }

    private string SanitizeFolderName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c.ToString(), "");
        }
        return name.Trim();
    }
}
#endif