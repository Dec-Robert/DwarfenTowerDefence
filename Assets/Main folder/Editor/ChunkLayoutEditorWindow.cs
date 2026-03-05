#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class MetaMapConfigEditorWindow : EditorWindow
{
    // --- GŁÓWNE REFERENCJE ---
    private MetaMapConfig targetConfig;
    private ChunkLayoutSO targetLayout;
    
    // --- USTAWIENIA SIATKI ---
    private int chunkRadius = 4;
    private float hexSize = 35f;
    private Vector2 panOffset = new Vector2(300, 300);

    // --- STAN UI ---
    private Vector2Int? selectedHex = null;
    private bool hasOverrideSelected = false;
    private HexOverride currentOverride;

    private Vector2 leftScrollPos;
    private Vector2 rightScrollPos;
    
    // Rozwijane zakładki
    private bool showConfigBase = true;
    private bool showConfigExpansions = true;
    private bool showConfigModifiers = true;
    private bool showLibrary = true;

    // Biblioteka layoutów grupowana po folderach
    private Dictionary<string, List<ChunkLayoutSO>> layoutLibrary = new Dictionary<string, List<ChunkLayoutSO>>();

    [MenuItem("Tools/Map/Meta Map Config & Layout Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<MetaMapConfigEditorWindow>("Meta Map Editor");
        window.minSize = new Vector2(1100, 600);
        window.Show();
    }

    private void OnEnable()
    {
        // Spróbuj automatycznie znaleźć główny config
        string[] configGuids = AssetDatabase.FindAssets("t:MetaMapConfig");
        if (configGuids.Length > 0)
        {
            targetConfig = AssetDatabase.LoadAssetAtPath<MetaMapConfig>(AssetDatabase.GUIDToAssetPath(configGuids[0]));
        }

        RefreshLibrary();
    }

    private void OnGUI()
    {
        DrawTopBar();

        EditorGUILayout.BeginHorizontal();

        // 1. LEWY PANEL (Zarządzanie Configiem i Plikami)
        DrawLeftPanel(new Rect(0, 30, 350, position.height - 30));

        // 2. ŚRODKOWY PANEL (Siatka Hexów)
        Rect gridRect = new Rect(350, 30, position.width - 350 - 280, position.height - 30);
        DrawGrid(gridRect);
        HandleInput(gridRect);

        // 3. PRAWY PANEL (Inspektor Hexa)
        DrawRightPanel(new Rect(position.width - 280, 30, 280, position.height - 30));

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================================
    // GÓRNY PASEK
    // =========================================================================
    private void DrawTopBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(30));
        
        GUILayout.Label("Meta Map Config:", GUILayout.Width(110));
        targetConfig = (MetaMapConfig)EditorGUILayout.ObjectField(targetConfig, typeof(MetaMapConfig), false, GUILayout.Width(250));

        GUILayout.Space(20);
        GUILayout.Label($"Edytowany Layout: {(targetLayout != null ? targetLayout.name : "BRAK")}", EditorStyles.boldLabel, GUILayout.Width(250));

        GUILayout.FlexibleSpace();
        
        GUILayout.Label("Promień chunku:", GUILayout.Width(100));
        chunkRadius = EditorGUILayout.IntSlider(chunkRadius, 1, 10, GUILayout.Width(150));
        
        if (GUILayout.Button("Wyśrodkuj Widok", EditorStyles.toolbarButton))
        {
            panOffset = new Vector2((position.width - 350 - 280) / 2, (position.height - 30) / 2);
            Repaint();
        }

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================================
    // LEWY PANEL (Config + Biblioteka Plików)
    // =========================================================================
    private void DrawLeftPanel(Rect rect)
    {
        GUILayout.BeginArea(rect, EditorStyles.helpBox);
        leftScrollPos = EditorGUILayout.BeginScrollView(leftScrollPos);

        // ---------------------------------------------------------
        // SEKCJA 1: META MAP CONFIG
        // ---------------------------------------------------------
        if (targetConfig != null)
        {
            GUILayout.Label("Modyfikacje Globalne (MetaMapConfig)", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            // -- BAZA (0,0) --
            showConfigBase = EditorGUILayout.Foldout(showConfigBase, "Baza Startowa (0,0)", true, EditorStyles.foldoutHeader);
            if (showConfigBase)
            {
                EditorGUILayout.BeginHorizontal();
                targetConfig.startingChunkBase = (ChunkLayoutSO)EditorGUILayout.ObjectField(targetConfig.startingChunkBase, typeof(ChunkLayoutSO), false);
                if (GUILayout.Button("Edytuj", EditorStyles.miniButton, GUILayout.Width(50))) SelectLayout(targetConfig.startingChunkBase);
                EditorGUILayout.EndHorizontal();
            }

            // -- EKSPANSJE --
            showConfigExpansions = EditorGUILayout.Foldout(showConfigExpansions, $"Ekspansje ({targetConfig.expansions?.Count ?? 0})", true, EditorStyles.foldoutHeader);
            if (showConfigExpansions)
            {
                if (targetConfig.expansions == null) targetConfig.expansions = new List<MetaMapConfig.ExpansionChunkLink>();

                for (int i = 0; i < targetConfig.expansions.Count; i++)
                {
                    var exp = targetConfig.expansions[i];
                    EditorGUILayout.BeginVertical("box");
                    
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Koordynaty:", GUILayout.Width(80));
                    exp.chunkCoordinate = EditorGUILayout.Vector2IntField("", exp.chunkCoordinate);
                    if (GUILayout.Button("X", EditorStyles.miniButtonRight, GUILayout.Width(25))) { targetConfig.expansions.RemoveAt(i); i--; continue; }
                    EditorGUILayout.EndHorizontal();

                    exp.requiredUpgrade = (MetaUpgradeSO)EditorGUILayout.ObjectField("Wymaga", exp.requiredUpgrade, typeof(MetaUpgradeSO), false);
                    
                    EditorGUILayout.BeginHorizontal();
                    exp.baseLayout = (ChunkLayoutSO)EditorGUILayout.ObjectField("Layout", exp.baseLayout, typeof(ChunkLayoutSO), false);
                    if (GUILayout.Button("Edytuj", EditorStyles.miniButton, GUILayout.Width(50))) SelectLayout(exp.baseLayout);
                    EditorGUILayout.EndHorizontal();

                    targetConfig.expansions[i] = exp;
                    EditorGUILayout.EndVertical();
                }
                if (GUILayout.Button("+ Dodaj Ekspansję", EditorStyles.miniButton)) targetConfig.expansions.Add(new MetaMapConfig.ExpansionChunkLink());
            }

            // -- MODYFIKATORY --
            showConfigModifiers = EditorGUILayout.Foldout(showConfigModifiers, $"Globalne Modyfikatory ({targetConfig.globalModifiers?.Count ?? 0})", true, EditorStyles.foldoutHeader);
            if (showConfigModifiers)
            {
                if (targetConfig.globalModifiers == null) targetConfig.globalModifiers = new List<MetaMapConfig.ChunkModifierLink>();

                for (int i = 0; i < targetConfig.globalModifiers.Count; i++)
                {
                    var mod = targetConfig.globalModifiers[i];
                    EditorGUILayout.BeginVertical("box");
                    
                    EditorGUILayout.BeginHorizontal();
                    mod.name = EditorGUILayout.TextField(mod.name);
                    if (GUILayout.Button("X", EditorStyles.miniButtonRight, GUILayout.Width(25))) { targetConfig.globalModifiers.RemoveAt(i); i--; continue; }
                    EditorGUILayout.EndHorizontal();

                    mod.targetChunk = EditorGUILayout.Vector2IntField("Target Chunk", mod.targetChunk);
                    mod.requiredUpgrade = (MetaUpgradeSO)EditorGUILayout.ObjectField("Wymaga", mod.requiredUpgrade, typeof(MetaUpgradeSO), false);
                    
                    EditorGUILayout.BeginHorizontal();
                    mod.modifierLayout = (ChunkLayoutSO)EditorGUILayout.ObjectField("Layout", mod.modifierLayout, typeof(ChunkLayoutSO), false);
                    if (GUILayout.Button("Edytuj", EditorStyles.miniButton, GUILayout.Width(50))) SelectLayout(mod.modifierLayout);
                    EditorGUILayout.EndHorizontal();

                    targetConfig.globalModifiers[i] = mod;
                    EditorGUILayout.EndVertical();
                }
                if (GUILayout.Button("+ Dodaj Modyfikator", EditorStyles.miniButton)) targetConfig.globalModifiers.Add(new MetaMapConfig.ChunkModifierLink());
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(targetConfig);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Brak podpiętego MetaMapConfig. Wybierz go na górnym pasku.", MessageType.Warning);
        }

        EditorGUILayout.Space(20);

        // ---------------------------------------------------------
        // SEKCJA 2: BIBLIOTEKA PLIKÓW
        // ---------------------------------------------------------
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Biblioteka Plików Layoutów", EditorStyles.boldLabel);
        if (GUILayout.Button("🔄 Odśwież", EditorStyles.miniButtonRight, GUILayout.Width(70))) RefreshLibrary();
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("➕ Utwórz Nowy Layout", GUILayout.Height(30)))
        {
            CreateNewLayout();
        }

        EditorGUILayout.Space(10);

        foreach (var kvp in layoutLibrary)
        {
            string folderName = kvp.Key;
            List<ChunkLayoutSO> layouts = kvp.Value;

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label($"📁 {folderName} ({layouts.Count})", EditorStyles.boldLabel);
            
            foreach (var layout in layouts)
            {
                EditorGUILayout.BeginHorizontal();
                
                // Zaznaczenie aktywnego na niebiesko
                GUI.backgroundColor = (targetLayout == layout) ? new Color(0.3f, 0.6f, 1f) : Color.white;
                if (GUILayout.Button(layout.name, EditorStyles.miniButtonLeft))
                {
                    SelectLayout(layout);
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = new Color(0.9f, 0.3f, 0.3f);
                if (GUILayout.Button("X", EditorStyles.miniButtonRight, GUILayout.Width(25)))
                {
                    DeleteLayout(layout);
                    GUIUtility.ExitGUI(); // Zapobiega błędom GUI po usunięciu pliku w pętli
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(5);
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // =========================================================================
    // ŚRODEK: RYSOWANIE SIATKI WIZUALNEJ
    // =========================================================================
    private void DrawGrid(Rect rect)
    {
        GUI.BeginGroup(rect);
        EditorGUI.DrawRect(new Rect(0, 0, rect.width, rect.height), new Color(0.12f, 0.12f, 0.12f));

        if (targetLayout != null)
        {
            for (int q = -chunkRadius; q <= chunkRadius; q++)
            {
                int r1 = Mathf.Max(-chunkRadius, -q - chunkRadius);
                int r2 = Mathf.Min(chunkRadius, -q + chunkRadius);
                for (int r = r1; r <= r2; r++)
                {
                    DrawSingleHex(new Vector2Int(q, r));
                }
            }
        }
        else
        {
            GUI.Label(new Rect(rect.width/2 - 100, rect.height/2 - 20, 200, 40), "Wybierz layout z lewego panelu.", EditorStyles.centeredGreyMiniLabel);
        }

        GUI.EndGroup();
    }

    private void DrawSingleHex(Vector2Int coord)
    {
        Vector2 center = AxialToScreen(coord) + panOffset;

        bool hasOverride = false;
        HexOverride hexData = default;

        if (targetLayout != null && targetLayout.hexes != null)
        {
            var match = targetLayout.hexes.Where(h => h.localCoord == coord).ToList();
            if (match.Count > 0)
            {
                hasOverride = true;
                hexData = match[0];
            }
        }

        // Kolorowanie
        Color fillColor = new Color(0.2f, 0.2f, 0.2f);
        if (hasOverride)
        {
            fillColor = hexData.feature switch
            {
                HexFeatureType.Forest => new Color(0.1f, 0.5f, 0.1f),
                HexFeatureType.Mountain => new Color(0.4f, 0.4f, 0.4f),
                HexFeatureType.FertileSoil => new Color(0.5f, 0.8f, 0.1f),
                HexFeatureType.Hill => new Color(0.6f, 0.5f, 0.2f),
                HexFeatureType.Sinkhole => new Color(0.3f, 0.2f, 0.4f),
                HexFeatureType.Base => new Color(0.1f, 0.2f, 0.8f),
                HexFeatureType.Beacon => new Color(0.9f, 0.6f, 0.1f),
                _ => new Color(0.25f, 0.3f, 0.25f)
            };
        }

        bool isSelected = selectedHex.HasValue && selectedHex.Value == coord;
        Color outlineColor = isSelected ? Color.yellow : new Color(0f,0f,0f, 0.5f);
        float outlineThickness = isSelected ? 3f : 1f;

        if (isSelected) fillColor = Color.Lerp(fillColor, Color.yellow, 0.3f);

        Vector3[] corners = GetHexCorners(center, hexSize * 0.95f);
        Handles.color = fillColor;
        Handles.DrawAAConvexPolygon(corners);

        Handles.color = outlineColor;
        Handles.DrawAAPolyLine(outlineThickness, corners[0], corners[1], corners[2], corners[3], corners[4], corners[5], corners[0]);

        // Napisy
        var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }, richText = true };
        string labelText = $"<color=#888888>{coord.x},{coord.y}</color>";
        if (hasOverride)
        {
            if (hexData.feature != HexFeatureType.None) labelText += $"\n{hexData.feature}";
            if (hexData.building != null) labelText += $"\n<color=#ffaa00>[{hexData.building.name}]</color>";
        }

        Handles.Label(center + new Vector2(0, -10), labelText, style);
    }

    private void HandleInput(Rect gridRect)
    {
        if (targetLayout == null) return;

        Event e = Event.current;
        if (!gridRect.Contains(e.mousePosition)) return;

        // Przesuwanie kamery (ŚPM)
        if (e.type == EventType.MouseDrag && e.button == 2)
        {
            panOffset += e.delta;
            Repaint();
        }

        // Kliknięcie w heks (LPM)
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Vector2 mousePos = e.mousePosition - new Vector2(gridRect.x, gridRect.y) - panOffset;
            Vector2Int clickedHex = ScreenToAxial(mousePos);

            if (HexDistance(Vector2Int.zero, clickedHex) <= chunkRadius)
            {
                selectedHex = clickedHex;
                LoadOverrideForSelected();
                GUI.FocusControl(null);
                Repaint();
            }
        }
    }

    // =========================================================================
    // PRAWY PANEL (Inspektor Heksa)
    // =========================================================================
    private void DrawRightPanel(Rect rect)
    {
        GUILayout.BeginArea(rect, EditorStyles.helpBox);
        rightScrollPos = EditorGUILayout.BeginScrollView(rightScrollPos);

        if (targetLayout == null)
        {
            GUILayout.Label("Wybierz Layout.", EditorStyles.centeredGreyMiniLabel);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            return;
        }

        if (!selectedHex.HasValue)
        {
            GUILayout.Label("Kliknij heks na mapie.", EditorStyles.centeredGreyMiniLabel);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            return;
        }

        Vector2Int coord = selectedHex.Value;
        GUILayout.Label($"Wybrany Heks: [ {coord.x} , {coord.y} ]", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();

        HexFeatureType newFeature = (HexFeatureType)EditorGUILayout.EnumPopup("Typ Terenu", currentOverride.feature);
        int newLevel = EditorGUILayout.IntSlider("Poziom Terenu", currentOverride.featureLevel, -5, 5);
        BuildingData newBuilding = (BuildingData)EditorGUILayout.ObjectField("Budynek Startowy", currentOverride.building, typeof(BuildingData), false);

        if (EditorGUI.EndChangeCheck())
        {
            currentOverride.localCoord = coord;
            currentOverride.feature = newFeature;
            currentOverride.featureLevel = newLevel;
            currentOverride.building = newBuilding;
            
            SaveOverrideToSO();
        }

        EditorGUILayout.Space(20);

        if (hasOverrideSelected)
        {
            GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
            if (GUILayout.Button("Wyczyść (Resetuj Heks)", GUILayout.Height(30)))
            {
                RemoveOverrideFromSO();
            }
            GUI.backgroundColor = Color.white;
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // =========================================================================
    // LOGIKA PLIKÓW I DANYCH
    // =========================================================================
    private void SelectLayout(ChunkLayoutSO layout)
    {
        targetLayout = layout;
        selectedHex = null;
        EditorGUIUtility.PingObject(layout);
        Repaint();
    }

    private void RefreshLibrary()
    {
        layoutLibrary.Clear();
        string[] guids = AssetDatabase.FindAssets("t:ChunkLayoutSO");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ChunkLayoutSO layout = AssetDatabase.LoadAssetAtPath<ChunkLayoutSO>(path);
            if (layout != null)
            {
                string folder = Path.GetFileName(Path.GetDirectoryName(path));
                if (!layoutLibrary.ContainsKey(folder)) layoutLibrary[folder] = new List<ChunkLayoutSO>();
                layoutLibrary[folder].Add(layout);
            }
        }
        
        // Sortowanie słownika i list
        layoutLibrary = layoutLibrary.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value.OrderBy(l => l.name).ToList());
    }

    private void CreateNewLayout()
    {
        string defaultPath = "Assets/Main folder/ScriptableObjects/Starting chunks"; // Domyślna lokalizacja do okienka
        if (!Directory.Exists(defaultPath)) defaultPath = "Assets";

        string path = EditorUtility.SaveFilePanelInProject("Zapisz Nowy Layout", "NewChunkLayout", "asset", "Wybierz lokalizację", defaultPath);
        
        if (!string.IsNullOrEmpty(path))
        {
            ChunkLayoutSO newLayout = ScriptableObject.CreateInstance<ChunkLayoutSO>();
            AssetDatabase.CreateAsset(newLayout, path);
            AssetDatabase.SaveAssets();
            
            RefreshLibrary();
            SelectLayout(newLayout);
        }
    }

    private void DeleteLayout(ChunkLayoutSO layout)
    {
        if (layout == null) return;

        bool confirm = EditorUtility.DisplayDialog("Usuń Layout", $"Czy na pewno chcesz usunąć plik '{layout.name}' z projektu?\n(Tej operacji nie można cofnąć!)", "Tak, usuń", "Anuluj");
        if (confirm)
        {
            if (targetLayout == layout) targetLayout = null;
            
            // Usunięcie powiązań z configu (zapobiega Missing Reference)
            if (targetConfig != null)
            {
                if (targetConfig.startingChunkBase == layout) targetConfig.startingChunkBase = null;
                if (targetConfig.expansions != null) targetConfig.expansions.ForEach(e => { if(e.baseLayout == layout) e.baseLayout = null; });
                if (targetConfig.globalModifiers != null) targetConfig.globalModifiers.ForEach(m => { if(m.modifierLayout == layout) m.modifierLayout = null; });
                EditorUtility.SetDirty(targetConfig);
            }

            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(layout));
            AssetDatabase.SaveAssets();
            RefreshLibrary();
        }
    }

    private void LoadOverrideForSelected()
    {
        if (targetLayout.hexes == null) targetLayout.hexes = new List<HexOverride>();
        var match = targetLayout.hexes.Where(h => h.localCoord == selectedHex.Value).ToList();
        if (match.Count > 0)
        {
            hasOverrideSelected = true;
            currentOverride = match[0];
        }
        else
        {
            hasOverrideSelected = false;
            currentOverride = new HexOverride { localCoord = selectedHex.Value, feature = HexFeatureType.None, featureLevel = 0, building = null };
        }
    }

    private void SaveOverrideToSO()
    {
        Undo.RecordObject(targetLayout, "Edycja HexLayout");
        if (targetLayout.hexes == null) targetLayout.hexes = new List<HexOverride>();
        targetLayout.hexes.RemoveAll(h => h.localCoord == selectedHex.Value);
        
        if (currentOverride.feature != HexFeatureType.None || currentOverride.building != null || currentOverride.featureLevel != 0)
        {
            targetLayout.hexes.Add(currentOverride);
            hasOverrideSelected = true;
        }
        else hasOverrideSelected = false;

        EditorUtility.SetDirty(targetLayout);
        Repaint();
    }

    private void RemoveOverrideFromSO()
    {
        Undo.RecordObject(targetLayout, "Wyczyszczenie HexLayout");
        targetLayout.hexes.RemoveAll(h => h.localCoord == selectedHex.Value);
        EditorUtility.SetDirty(targetLayout);
        LoadOverrideForSelected();
        Repaint();
    }

    // =========================================================================
    // MATEMATYKA HEKSÓW
    // =========================================================================
    private Vector2 AxialToScreen(Vector2Int coord)
    {
        float x = hexSize * Mathf.Sqrt(3) * (coord.x + coord.y / 2f);
        float y = hexSize * 3f / 2f * coord.y;
        return new Vector2(x, y);
    }

    private Vector2Int ScreenToAxial(Vector2 screenPos)
    {
        float q = (Mathf.Sqrt(3f) / 3f * screenPos.x - 1f / 3f * screenPos.y) / hexSize;
        float r = (2f / 3f * screenPos.y) / hexSize;
        return AxialRound(q, r);
    }

    private Vector2Int AxialRound(float x, float z)
    {
        float y = -x - z;
        int rx = Mathf.RoundToInt(x); int ry = Mathf.RoundToInt(y); int rz = Mathf.RoundToInt(z);
        float x_diff = Mathf.Abs(rx - x); float y_diff = Mathf.Abs(ry - y); float z_diff = Mathf.Abs(rz - z);

        if (x_diff > y_diff && x_diff > z_diff) rx = -ry - rz;
        else if (y_diff > z_diff) ry = -rx - rz;
        else rz = -rx - ry;
        return new Vector2Int(rx, rz);
    }

    private int HexDistance(Vector2Int a, Vector2Int b) => (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.x + a.y - b.x - b.y) + Mathf.Abs(a.y - b.y)) / 2;

    private Vector3[] GetHexCorners(Vector2 center, float size)
    {
        Vector3[] corners = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float angle_rad = Mathf.PI / 180 * (60 * i - 30);
            corners[i] = new Vector3(center.x + size * Mathf.Cos(angle_rad), center.y + size * Mathf.Sin(angle_rad), 0);
        }
        return corners;
    }
}
#endif