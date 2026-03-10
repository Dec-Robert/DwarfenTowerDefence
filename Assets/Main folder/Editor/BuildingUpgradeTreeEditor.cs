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
    // GRAF DRZEWKA (Ulepszony układ dla łączących się ścieżek)
    // =========================================================================
    
    // Klasa pomocnicza do budowania unikalnych węzłów przed narysowaniem
    private class RenderNode
    {
        public BuildingUpgradeSO upgrade;
        public int tier;
        public Rect rect;
        public List<RenderNode> children = new List<RenderNode>();
    }

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

        graphScroll = GUI.BeginScrollView(new Rect(0, 0, rect.width, rect.height), graphScroll, new Rect(0, 0, 3000, 3000));

        // 1. Zbudowanie drzewa relacji (gwarancja unikalności)
        var allNodes = new Dictionary<BuildingUpgradeSO, RenderNode>();
        var rootNodes = new List<RenderNode>();

        if (selectedBuilding.tier1Upgrades == null) selectedBuilding.tier1Upgrades = new List<BuildingUpgradeSO>();

        // Analizujemy graf rekursywnie i wyciągamy unikalne ulepszenia
        foreach (var t1 in selectedBuilding.tier1Upgrades)
        {
            if (t1 != null) rootNodes.Add(BuildNodeTree(t1, 1, allNodes));
        }

        // 2. Grupowanie węzłów według Tierów do poprawnego ułożenia w siatce
        var nodesByTier = new Dictionary<int, List<RenderNode>>();
        int maxTier = 0;

        foreach (var node in allNodes.Values)
        {
            if (!nodesByTier.ContainsKey(node.tier)) 
            {
                nodesByTier[node.tier] = new List<RenderNode>();
            }
            
            nodesByTier[node.tier].Add(node); // POPRAWIONE!
            
            if (node.tier > maxTier) 
            {
                maxTier = node.tier;
            }
        }

        // 3. Pozycjonowanie
        float startY = 120f;
        float spacingX = NODE_WIDTH + 40f;
        float spacingY = NODE_HEIGHT + 70f;
        float centerX = 1500f; // Środek ogromnego ScrollView

        // Rysuj korzeń (Sam Budynek)
        Rect rootRect = new Rect(centerX - NODE_WIDTH / 2, 20, NODE_WIDTH, NODE_HEIGHT);
        DrawNode(rootRect, selectedBuilding.buildingName, "Budynek Bazowy", Color.gray, null);

        if (GUI.Button(new Rect(rootRect.x + 20, rootRect.yMax + 10, NODE_WIDTH - 40, 20), "+ Dodaj Tier 1"))
        {
            CreateNewUpgrade(selectedBuilding.tier1Upgrades, selectedBuilding.buildingName + "_T1_New", selectedBuilding);
        }

        // Ustawianie pozycji X dla unikalnych węzłów w poszczególnych Tierach
        for (int i = 1; i <= maxTier; i++)
        {
            if (!nodesByTier.ContainsKey(i)) continue;
            
            var tierNodes = nodesByTier[i];
            float startX = centerX - ((tierNodes.Count - 1) * spacingX) / 2f;

            for (int j = 0; j < tierNodes.Count; j++)
            {
                tierNodes[j].rect = new Rect(startX + j * spacingX - NODE_WIDTH / 2, startY + (i - 1) * spacingY, NODE_WIDTH, NODE_HEIGHT);
            }
        }

        // 4. Rysowanie linii (od rodziców do dzieci)
        Handles.color = new Color(0.6f, 0.6f, 0.6f);

        // Linie od korzenia do Tier 1
        foreach (var t1 in rootNodes)
        {
            DrawConnection(rootRect, t1.rect);
        }

        // Linie między unikalnymi ulepszeniami
        foreach (var node in allNodes.Values)
        {
            foreach (var child in node.children)
            {
                DrawConnection(node.rect, child.rect);
            }
        }

        // 5. Rysowanie węzłów
        foreach (var node in allNodes.Values)
        {
            Color nodeColor = (selectedUpgrade == node.upgrade) ? new Color(0.2f, 0.6f, 0.2f) : new Color(0.2f, 0.3f, 0.4f);
            DrawNode(node.rect, node.upgrade.upgradeName, $"Tier {node.tier}", nodeColor, node.upgrade);

            // Przycisk "Dodaj dziecko"
            if (GUI.Button(new Rect(node.rect.x + 20, node.rect.yMax + 5, NODE_WIDTH - 40, 16), $"+ Dodaj Tier {node.tier + 1}"))
            {
                if (node.upgrade.nextTierOptions == null) node.upgrade.nextTierOptions = new List<BuildingUpgradeSO>();
                CreateNewUpgrade(node.upgrade.nextTierOptions, $"Tier{node.tier + 1}_New", node.upgrade);
            }
        }

        GUI.EndScrollView();
        GUI.EndGroup();
    }

    // Tworzy strukturę unikalnych węzłów z najgłębszym przypisanym tierem (jeśli ścieżki się krzyżują)
    private RenderNode BuildNodeTree(BuildingUpgradeSO upgrade, int currentTier, Dictionary<BuildingUpgradeSO, RenderNode> allNodes)
    {
        if (upgrade == null) return null;

        if (allNodes.TryGetValue(upgrade, out RenderNode existingNode))
        {
            // Jeśli węzeł już istnieje, ale dotarliśmy do niego dłuższą ścieżką, zaktualizuj jego tier (żeby zepchnąć go niżej)
            if (currentTier > existingNode.tier) existingNode.tier = currentTier;
            return existingNode;
        }

        RenderNode newNode = new RenderNode { upgrade = upgrade, tier = currentTier };
        allNodes[upgrade] = newNode;

        if (upgrade.nextTierOptions != null)
        {
            foreach (var child in upgrade.nextTierOptions)
            {
                var childNode = BuildNodeTree(child, currentTier + 1, allNodes);
                if (childNode != null && !newNode.children.Contains(childNode))
                {
                    newNode.children.Add(childNode);
                }
            }
        }

        return newNode;
    }

    private void DrawConnection(Rect parentRect, Rect childRect)
    {
        Vector3 startPos = new Vector3(parentRect.center.x, parentRect.yMax);
        Vector3 endPos = new Vector3(childRect.center.x, childRect.y);
        
        // Zwykła linia prosta (Dla lepszej czytelności przy krzyżujących się ścieżkach)
        Handles.DrawAAPolyLine(3f, startPos, endPos);
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
       EditorGUILayout.Space();
        EditorGUILayout.LabelField("── Ekonomia ──", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedUpgrade.FindProperty("cost"), new GUIContent("Koszt Zakupu"), true);
        EditorGUILayout.PropertyField(serializedUpgrade.FindProperty("productionBonus"), new GUIContent("Bonus do Produkcji"), true);
        EditorGUILayout.PropertyField(serializedUpgrade.FindProperty("upkeepIncrease"), new GUIContent("Zwiększenie Utrzymania"), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("── Modyfikatory Terenu ──", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Wpływa na zysk z KAŻDEGO heksa (Sąsiada).\nProcenty wpisuj jako ułamki (np. 0.15 to +15%, -0.25 to kara -25%).", MessageType.Info);
        EditorGUILayout.PropertyField(serializedUpgrade.FindProperty("terrainFlatBonus"), new GUIContent("Płaski Bonus (Flat)"));
        EditorGUILayout.PropertyField(serializedUpgrade.FindProperty("terrainPercentBonus"), new GUIContent("Mnożnik (%)"));
        
        // --- ZMIANA DLA LISTY EFEKTÓW SPECJALNYCH ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("── Mechaniki Unikalne ──", EditorStyles.boldLabel);
        
        string[] availableEffects = new string[] 
        {
            "SAWMILL_PLANT_FOREST",
            "SAWMILL_DESTROY_FOREST",
            "INCREASE_RANGE_1",
            "INCREASE_RANGE_2", 
            "RANGE_PENALTY_25",
            "RANGE_PENALTY_50",
            "BONUS_BOOST_200",
            "FARM_CREATE_SOIL",
            "IGNORE_OCCUPIED_PENALTY",
            "MINE_EXPLOSION_RISK",
            "MINE_MOUNTAIN_CHAIN",
            "FULL_SHIFT_MEGA_BONUS",
            "MINE_RUNE_DROP",
            "FARM_80_PERCENT_START",
            "MINE_ONE_WORKER_100",
            "SAWMILL_ONE_WORKER_100_NEXT_15"
        };

        if (selectedUpgrade.specialEffectIDs == null) selectedUpgrade.specialEffectIDs = new List<string>();

        // Budujemy maskę bitową (Dropdown wielokrotnego wyboru)
        int currentMask = 0;
        for (int i = 0; i < availableEffects.Length; i++)
        {
            if (selectedUpgrade.specialEffectIDs.Contains(availableEffects[i]))
            {
                currentMask |= (1 << i);
            }
        }

        int newMask = EditorGUILayout.MaskField("Zdolności", currentMask, availableEffects);

        // Aplikujemy zmiany z maski na z powrotem do listy
        if (newMask != currentMask)
        {
            Undo.RecordObject(selectedUpgrade, "Zmieniono Zdolności Specjalne");
            selectedUpgrade.specialEffectIDs.Clear();
            for (int i = 0; i < availableEffects.Length; i++)
            {
                if ((newMask & (1 << i)) != 0)
                {
                    selectedUpgrade.specialEffectIDs.Add(availableEffects[i]);
                }
            }
            EditorUtility.SetDirty(selectedUpgrade);
        }

        // --- ZMIANA DLA DZIECI / NASTĘPNEGO TIERU ---
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("── Łączenie Węzłów (Co odblokowuje ten upgrade) ──", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Dodaj istniejący upgrade z projektu, jeśli ścieżki w drzewku się krzyżują.", MessageType.Info);

        SerializedProperty nextTierProp = serializedUpgrade.FindProperty("nextTierOptions");
        EditorGUILayout.PropertyField(nextTierProp, new GUIContent("Kolejny Tier (Wnuki)"), true);

        // ... Reszta (zapis i przycisk usuwania) ...
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