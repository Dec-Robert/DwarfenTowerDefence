#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class PredefinedMapEditorWindow : EditorWindow
{
    // =========================================================================
    // ENUMY NARZĘDZI
    // =========================================================================
    public enum EditorToolMode
    {
        Select,
        Terrain,
        Road,
        Building,
        SetBase,
        SetSpawner,
        AddChunk,
        Eraser
    }

    // =========================================================================
    // POLA STANU EDYTORA
    // =========================================================================
    private PredefinedMapLayoutSO targetMap;

    // Widok i nawigacja
    private float hexSize = 28f;
    private Vector2 panOffset = new Vector2(400, 350);
    private bool isPanning = false;

    // Narzędzie i pędzel
    private EditorToolMode currentTool = EditorToolMode.Select;
    private HexFeatureType brushFeature = HexFeatureType.Forest;
    private int brushFeatureLevel = 1;
    private int brushElevation = 0;
    private bool roadBrushMode = true; // true = dodaj drogę, false = usuń
    private BuildingData brushBuilding;

    // Zaznaczenie
    private PredefinedHexRef? selectedHex = null;
    private Vector2Int? selectedChunkCoord = null;

    // Scrollery paneli bocznych
    private Vector2 leftScrollPos;
    private Vector2 rightScrollPos;

    // Walidacja
    private bool hasValidated = false;
    private List<string> lastValidationErrors = new List<string>();
    private List<string> lastValidationWarnings = new List<string>();

    // Dodawanie chunków
    private Vector2Int newChunkCoordInput = Vector2Int.zero;

    // =========================================================================
    // MENU ITEM
    // =========================================================================
    [MenuItem("Tools/Map/Predefined Map Editor", priority = 62)]
    public static void ShowWindow()
    {
        var window = GetWindow<PredefinedMapEditorWindow>("Predefined Map Editor");
        window.minSize = new Vector2(1180, 680);
        window.Show();
    }

    private void OnEnable()
    {
        // Spróbuj automatycznie załadować istniejący layout tutorialowy
        if (targetMap == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:PredefinedMapLayoutSO");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                targetMap = AssetDatabase.LoadAssetAtPath<PredefinedMapLayoutSO>(path);
            }
        }
    }

    // =========================================================================
    // GŁÓWNA PĘTLA GUI
    // =========================================================================
    private void OnGUI()
    {
        DrawTopToolbar();

        EditorGUILayout.BeginHorizontal();

        // 1. Lewy panel: Paleta narzędzi, pędzle, lista chunków (280px)
        float leftWidth = 290f;
        DrawLeftPanel(new Rect(0, 32, leftWidth, position.height - 32));

        // 2. Środek: Interaktywna siatka całej mapy (Canvas)
        float rightWidth = 260f;
        Rect canvasRect = new Rect(leftWidth, 32, position.width - leftWidth - rightWidth, position.height - 32);
        DrawCanvas(canvasRect);
        HandleCanvasInput(canvasRect);

        // 3. Prawy panel: Inspektor zaznaczonego heksa / chunka (260px)
        DrawRightPanel(new Rect(position.width - rightWidth, 32, rightWidth, position.height - 32));

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================================
    // GÓRNY PASEK NARZĘDZI (TOOLBAR)
    // =========================================================================
    private void DrawTopToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(30));

        GUILayout.Label("Mapa (SO):", GUILayout.Width(70));
        var newMap = (PredefinedMapLayoutSO)EditorGUILayout.ObjectField(targetMap, typeof(PredefinedMapLayoutSO), false, GUILayout.Width(240));
        if (newMap != targetMap)
        {
            targetMap = newMap;
            hasValidated = false;
        }

        if (GUILayout.Button("+ Nowa Mapa", EditorStyles.toolbarButton, GUILayout.Width(90)))
        {
            CreateNewMapAssetDialog();
        }

        GUILayout.Space(15);

        if (targetMap != null)
        {
            GUILayout.Label($"Promień chunku: {targetMap.chunkRadius}", EditorStyles.miniLabel, GUILayout.Width(110));
        }

        GUILayout.FlexibleSpace();

        // Skala / Zoom
        GUILayout.Label("Zoom:", GUILayout.Width(45));
        hexSize = EditorGUILayout.Slider(hexSize, 14f, 65f, GUILayout.Width(130));

        if (GUILayout.Button("Wyśrodkuj", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            panOffset = new Vector2((position.width - 290 - 260) / 2f, (position.height - 32) / 2f);
            Repaint();
        }

        GUI.backgroundColor = new Color(0.3f, 0.75f, 0.9f);
        if (GUILayout.Button("✔ Waliduj Mapę", EditorStyles.toolbarButton, GUILayout.Width(110)))
        {
            RunMapValidation();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================================
    // LEWY PANEL (PALETA I CHUNKI)
    // =========================================================================
    private void DrawLeftPanel(Rect rect)
    {
        GUILayout.BeginArea(rect, EditorStyles.helpBox);
        leftScrollPos = GUILayout.BeginScrollView(leftScrollPos);

        GUILayout.Label("TRYB EDYTORA (PĘDZEL)", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        DrawToolSelectorButton(EditorToolMode.Select, "🔍 Zaznacz / Inspektor", "Podgląd i inspekcja konkretnego heksa");
        DrawToolSelectorButton(EditorToolMode.Road, "🛣 Pędzel Drogi", "Rysuj trasę wrogów (isRoad)");
        DrawToolSelectorButton(EditorToolMode.Terrain, "🌲 Pędzel Terenu", "Nakładaj las, kamień, wodę, rudy i wysokość");
        DrawToolSelectorButton(EditorToolMode.Building, "🏠 Stawianie Budynku", "Stawiaj początkowe budynki na planszy");
        DrawToolSelectorButton(EditorToolMode.SetBase, "⭐ Ustaw Bazę Gracza", "Oznacz heks jako Główną Bazę (HQ)");
        DrawToolSelectorButton(EditorToolMode.SetSpawner, "💀 Ustaw Spawner Wrogów", "Dodaj lub usuń spawner wrogów na heksie");
        DrawToolSelectorButton(EditorToolMode.AddChunk, "➕ Dodaj Nowy Chunk", "Kliknij puste pole na siatce, by stworzyć chunk");
        DrawToolSelectorButton(EditorToolMode.Eraser, "🧹 Gumka (Reset Heksa)", "Czyści heks do czystej trawy");

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Parametry aktywnego narzędzia
        DrawCurrentToolParameters();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Lista i zarządzanie chunkami
        DrawChunkManagementSection();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        // Raport walidacji
        DrawValidationSection();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawToolSelectorButton(EditorToolMode mode, string label, string tooltip)
    {
        bool isActive = currentTool == mode;
        if (isActive) GUI.backgroundColor = new Color(0.4f, 0.8f, 1.0f);
        if (GUILayout.Button(new GUIContent(label, tooltip), GUILayout.Height(26)))
        {
            currentTool = mode;
        }
        GUI.backgroundColor = Color.white;
    }

    private void DrawCurrentToolParameters()
    {
        GUILayout.Label("USTAWIENIA PĘDZLA", EditorStyles.boldLabel);

        switch (currentTool)
        {
            case EditorToolMode.Road:
                EditorGUILayout.HelpBox("LPM maluje drogę. Możesz też przeciągać kursor, aby rysować ciągłą trasę.", MessageType.Info);
                roadBrushMode = EditorGUILayout.Toggle("Tryb rysowania (ON)", roadBrushMode);
                break;

            case EditorToolMode.Terrain:
                brushFeature = (HexFeatureType)EditorGUILayout.EnumPopup("Cecha Terenu", brushFeature);
                brushFeatureLevel = EditorGUILayout.IntSlider("Poziom cechy", brushFeatureLevel, 0, 3);
                brushElevation = EditorGUILayout.IntSlider("Wysokość (Elevation)", brushElevation, -3, 4);

                // Szybka paleta przycisków
                EditorGUILayout.Space(4);
                GUILayout.Label("Szybki wybór:", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Trawa")) { brushFeature = HexFeatureType.None; brushElevation = 0; }
                if (GUILayout.Button("Las")) { brushFeature = HexFeatureType.Forest; brushFeatureLevel = 1; }
                if (GUILayout.Button("Łąka")) { brushFeature = HexFeatureType.Meadow; brushFeatureLevel = 1; }
                if (GUILayout.Button("Góra")) { brushFeature = HexFeatureType.Mountain; brushElevation = 4; }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Wzgórze (Elev +1)")) { brushElevation = 1; }
                if (GUILayout.Button("Wzgórze (Elev +2)")) { brushElevation = 2; }
                if (GUILayout.Button("Zapadlisko (Elev -1)")) { brushElevation = -1; }
                if (GUILayout.Button("Płasko (Elev 0)")) { brushElevation = 0; }
                EditorGUILayout.EndHorizontal();
                break;

            case EditorToolMode.Building:
                brushBuilding = (BuildingData)EditorGUILayout.ObjectField("Budynek", brushBuilding, typeof(BuildingData), false);
                EditorGUILayout.HelpBox("Kliknij dowolny heks, aby postawić wybrany budynek startowy.", MessageType.Info);
                break;

            case EditorToolMode.SetBase:
                if (targetMap != null)
                {
                    targetMap.startingBaseBuilding = (BuildingData)EditorGUILayout.ObjectField("Budynek Bazy", targetMap.startingBaseBuilding, typeof(BuildingData), false);
                    EditorGUILayout.HelpBox($"Aktualna Baza: {targetMap.playerBase}\nKliknij dowolny heks, aby przenieść Bazę.", MessageType.Info);
                }
                break;

            case EditorToolMode.SetSpawner:
                if (targetMap != null)
                {
                    int spawnerCount = targetMap.enemySpawners != null ? targetMap.enemySpawners.Count : 0;
                    EditorGUILayout.HelpBox($"Zdefiniowane Spawnery ({spawnerCount}):\nKliknięcie dodaje nowy spawner lub usuwa istniejący.", MessageType.Info);
                }
                break;

            case EditorToolMode.AddChunk:
                EditorGUILayout.HelpBox("Kliknij w puste miejsce w pobliżu istniejących chunków na siatce lub użyj sekcji zarządzania chunkami poniżej.", MessageType.Info);
                break;

            case EditorToolMode.Eraser:
                EditorGUILayout.HelpBox("Kliknij lub przeciągnij po heksie, aby przywrócić domyślną płaską trawę bez drogi i budynków.", MessageType.Warning);
                break;

            default:
                EditorGUILayout.HelpBox("Kliknij heks na mapie, aby wyświetlić i edytować jego parametry w prawym panelu.", MessageType.None);
                break;
        }
    }

    private void DrawChunkManagementSection()
    {
        GUILayout.Label("ZARZĄDZANIE CHUNKAMI", EditorStyles.boldLabel);

        if (targetMap == null || targetMap.chunks == null)
        {
            GUILayout.Label("Brak aktywnej mapy.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        GUILayout.Label($"Liczba chunków na mapie: {targetMap.chunks.Count}");

        // Dodawanie konkretnego chunka
        EditorGUILayout.BeginHorizontal();
        newChunkCoordInput = EditorGUILayout.Vector2IntField("Koordynaty chunka", newChunkCoordInput);
        if (GUILayout.Button("+ Dodaj", GUILayout.Width(65)))
        {
            Undo.RecordObject(targetMap, "Dodanie chunka");
            targetMap.GetOrCreateChunk(newChunkCoordInput, false);
            EditorUtility.SetDirty(targetMap);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        GUILayout.Label("Lista chunków:", EditorStyles.miniBoldLabel);

        for (int i = 0; i < targetMap.chunks.Count; i++)
        {
            var chunk = targetMap.chunks[i];
            bool isCurrent = selectedChunkCoord.HasValue && selectedChunkCoord.Value == chunk.chunkCoord;

            EditorGUILayout.BeginHorizontal(isCurrent ? EditorStyles.selectionRect : EditorStyles.helpBox);

            string status = chunk.isUnlockedAtStart ? "<color=#66ff66>[START]</color>" : "<color=#aaaaaa>[MGŁA]</color>";
            if (GUILayout.Button($"Chunk ({chunk.chunkCoord.x}, {chunk.chunkCoord.y}) {status}", new GUIStyle(EditorStyles.label) { richText = true }))
            {
                selectedChunkCoord = chunk.chunkCoord;
                Repaint();
            }

            // Przełącznik mgły
            bool newUnlocked = GUILayout.Toggle(chunk.isUnlockedAtStart, "Odkryty", GUILayout.Width(65));
            if (newUnlocked != chunk.isUnlockedAtStart)
            {
                Undo.RecordObject(targetMap, "Zmiana statusu mgły chunka");
                chunk.isUnlockedAtStart = newUnlocked;
                EditorUtility.SetDirty(targetMap);
            }

            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("X", GUILayout.Width(22)))
            {
                if (EditorUtility.DisplayDialog("Usuń Chunk", $"Czy na pewno chcesz usunąć chunk ({chunk.chunkCoord.x}, {chunk.chunkCoord.y}) i wszystkie jego heksy?", "Tak", "Anuluj"))
                {
                    Undo.RecordObject(targetMap, "Usunięcie chunka");
                    targetMap.chunks.RemoveAt(i);
                    EditorUtility.SetDirty(targetMap);
                    GUIUtility.ExitGUI();
                }
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawValidationSection()
    {
        GUILayout.Label("KONTROLA JAKOŚCI (WALIDACJA)", EditorStyles.boldLabel);

        if (!hasValidated)
        {
            GUILayout.Label("Kliknij 'Waliduj Mapę' na górnym pasku.", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        if (lastValidationErrors.Count == 0 && lastValidationWarnings.Count == 0)
        {
            EditorGUILayout.HelpBox("✔ Mapa jest w pełni poprawna!\n- Baza jest zdefiniowana.\n- Ścieżki ze wszystkich spawnerów są nieprzerwane.", MessageType.Info);
            return;
        }

        if (lastValidationErrors.Count > 0)
        {
            string errText = "BŁĘDY:\n• " + string.Join("\n• ", lastValidationErrors);
            EditorGUILayout.HelpBox(errText, MessageType.Error);
        }

        if (lastValidationWarnings.Count > 0)
        {
            string warnText = "OSTRZEŻENIA:\n• " + string.Join("\n• ", lastValidationWarnings);
            EditorGUILayout.HelpBox(warnText, MessageType.Warning);
        }
    }

    // =========================================================================
    // ŚRODEK: WIELOCHUNKOWY CANVAS SIATKI
    // =========================================================================
    private void DrawCanvas(Rect rect)
    {
        GUI.BeginGroup(rect);
        EditorGUI.DrawRect(new Rect(0, 0, rect.width, rect.height), new Color(0.11f, 0.12f, 0.13f));

        if (targetMap == null)
        {
            var centeredStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.gray } };
            GUI.Label(new Rect(0, 0, rect.width, rect.height), "Wybierz lub stwórz PredefinedMapLayoutSO na górnym pasku.", centeredStyle);
            GUI.EndGroup();
            return;
        }

        // Siatka tła (orientacyjna)
        DrawGridBackground(rect);

        // Rysowanie wszystkich chunków mapy
        int radius = targetMap.chunkRadius > 0 ? targetMap.chunkRadius : 3;

        foreach (var chunk in targetMap.chunks)
        {
            DrawChunk(chunk, radius);
        }

        // Rysowanie połączeń i linii dróg pomiędzy heksami
        DrawRoadFlowOverlay(radius);

        GUI.EndGroup();
    }

    private void DrawGridBackground(Rect rect)
    {
        Handles.color = new Color(1f, 1f, 1f, 0.03f);
        float step = 50f;
        for (float x = 0; x < rect.width; x += step)
        {
            Handles.DrawLine(new Vector3(x, 0, 0), new Vector3(x, rect.height, 0));
        }
        for (float y = 0; y < rect.height; y += step)
        {
            Handles.DrawLine(new Vector3(0, y, 0), new Vector3(rect.width, y, 0));
        }
    }

    private void DrawChunk(PredefinedChunkData chunk, int radius)
    {
        Vector2 chunkCenterScreen = panOffset + GetChunkCenterScreen(chunk.chunkCoord, radius, hexSize);

        // Obwódka chunka i informacja
        bool isSelectedChunk = selectedChunkCoord.HasValue && selectedChunkCoord.Value == chunk.chunkCoord;
        Color chunkBorderColor = chunk.isUnlockedAtStart
            ? new Color(0.2f, 0.9f, 0.3f, 0.6f)
            : new Color(0.4f, 0.5f, 0.7f, 0.4f);

        if (isSelectedChunk) chunkBorderColor = Color.cyan;

        // Rysowanie wszystkich heksów w obrębie promienia chunka
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Mathf.Max(-radius, -q - radius);
            int r2 = Mathf.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++)
            {
                Vector2Int local = new Vector2Int(q, r);
                DrawHex(chunk, local, chunkCenterScreen);
            }
        }

        // Etykieta chunka
        string tag = chunk.isUnlockedAtStart ? " [START]" : "";
        var labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = chunkBorderColor }
        };
        Vector2 labelPos = chunkCenterScreen + new Vector2(0, -GetChunkRadiusPixel(radius, hexSize) - 8);
        Handles.Label(labelPos, $"Chunk ({chunk.chunkCoord.x}, {chunk.chunkCoord.y}){tag}", labelStyle);
    }

    private void DrawHex(PredefinedChunkData chunk, Vector2Int localCoord, Vector2 chunkCenterScreen)
    {
        Vector2 hexCenter = chunkCenterScreen + AxialToScreen(localCoord, hexSize);
        PredefinedHexData hexData = chunk.GetHex(localCoord);

        bool isSelected = selectedHex.HasValue &&
                          selectedHex.Value.chunkCoord == chunk.chunkCoord &&
                          selectedHex.Value.localCoord == localCoord;

        bool isBase = targetMap.playerBase.chunkCoord == chunk.chunkCoord && targetMap.playerBase.localCoord == localCoord;
        bool isSpawner = targetMap.enemySpawners != null &&
                         targetMap.enemySpawners.Any(s => s.chunkCoord == chunk.chunkCoord && s.localCoord == localCoord);

        // Kolor wypełnienia heksa
        Color fillColor = GetHexBaseColor(hexData);

        if (hexData != null && hexData.isRoad)
        {
            // Droga ma ciepły, piaskowo-kamienny odcień
            fillColor = new Color(0.86f, 0.76f, 0.48f);
        }

        if (isSelected)
        {
            fillColor = Color.Lerp(fillColor, Color.yellow, 0.35f);
        }

        Vector3[] corners = GetHexCorners(hexCenter, hexSize * 0.94f);
        Handles.color = fillColor;
        Handles.DrawAAConvexPolygon(corners);

        // Krawędzie heksa
        Color outlineColor = isSelected ? Color.yellow : new Color(0f, 0f, 0f, 0.45f);
        float outlineWidth = isSelected ? 3f : 1f;

        if (isBase)
        {
            outlineColor = new Color(1.0f, 0.85f, 0.1f);
            outlineWidth = 3.5f;
        }
        else if (isSpawner)
        {
            outlineColor = new Color(1.0f, 0.25f, 0.25f);
            outlineWidth = 3.5f;
        }

        Handles.color = outlineColor;
        Handles.DrawAAPolyLine(outlineWidth, corners[0], corners[1], corners[2], corners[3], corners[4], corners[5], corners[0]);

        // Oznaczenia tekstowe i ikony
        string text = "";
        Color textColor = Color.white;

        if (isBase)
        {
            text = "★ BAZA";
            textColor = new Color(1f, 0.9f, 0.2f);
        }
        else if (isSpawner)
        {
            text = "💀 SPAWN";
            textColor = new Color(1f, 0.3f, 0.3f);
        }
        else if (hexData != null)
        {
            if (hexData.startingBuilding != null)
            {
                text = $"[{hexData.startingBuilding.buildingName}]";
                textColor = new Color(0.3f, 0.85f, 1f);
            }
            else if (hexData.feature != HexFeatureType.None)
            {
                text = hexData.feature.ToString();
                int eff = hexData.elevation;
                if (eff == 0)
                {
                    if (hexData.feature == HexFeatureType.Hill) eff = Mathf.Max(1, hexData.featureLevel);
                    else if (hexData.feature == HexFeatureType.Sinkhole) eff = hexData.featureLevel < 0 ? hexData.featureLevel : -1;
                    else if (hexData.feature == HexFeatureType.Mountain) eff = 4;
                }
                if (eff > 0) text += $"\n▲+{eff}";
                else if (eff < 0) text += $"\n▼{eff}";
            }
            else if (hexData.elevation != 0)
            {
                text = hexData.elevation > 0 ? $"▲+{hexData.elevation}" : $"▼{hexData.elevation}";
            }
            else if (hexData.isRoad)
            {
                text = "•";
                textColor = new Color(0.35f, 0.25f, 0.15f);
            }
        }

        if (!string.IsNullOrEmpty(text))
        {
            var textStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = textColor },
                fontSize = hexSize > 25 ? 9 : 8
            };
            Handles.Label(hexCenter + new Vector2(0, -6), text, textStyle);
        }
    }

    private Color GetHexBaseColor(PredefinedHexData hexData)
    {
        if (hexData == null)
        {
            // Czysta trawa
            return new Color(0.33f, 0.52f, 0.28f);
        }

        Color baseCol = hexData.feature switch
        {
            HexFeatureType.Forest => new Color(0.14f, 0.40f, 0.16f),
            HexFeatureType.Mountain => new Color(0.42f, 0.36f, 0.30f),
            HexFeatureType.Meadow => new Color(0.45f, 0.68f, 0.25f),
            HexFeatureType.Hill => new Color(0.50f, 0.55f, 0.30f),
            HexFeatureType.Sinkhole => new Color(0.24f, 0.28f, 0.24f),
            _ => new Color(0.33f, 0.52f, 0.28f)
        };

        // Cieniowanie w oparciu o wysokość (Elevation):
        int effectiveElev = hexData.elevation;
        if (effectiveElev == 0)
        {
            if (hexData.feature == HexFeatureType.Hill) effectiveElev = Mathf.Max(1, hexData.featureLevel);
            else if (hexData.feature == HexFeatureType.Sinkhole) effectiveElev = hexData.featureLevel < 0 ? hexData.featureLevel : -1;
            else if (hexData.feature == HexFeatureType.Mountain) effectiveElev = 4;
        }

        if (effectiveElev > 0)
        {
            // Wyraźne podświetlenie wzniesień (tarasy)
            float t = Mathf.Clamp01(effectiveElev * 0.14f);
            baseCol = Color.Lerp(baseCol, Color.white, t);
        }
        else if (effectiveElev < 0)
        {
            // Wyraźne przyciemnienie zapadlin
            float t = Mathf.Clamp01(Mathf.Abs(effectiveElev) * 0.18f);
            baseCol = Color.Lerp(baseCol, Color.black, t);
        }

        return baseCol;
    }

    private void DrawRoadFlowOverlay(int radius)
    {
        // Rysuje subtelne linie połączeń wzdłuż dróg
        if (targetMap == null || targetMap.chunks == null) return;

        var roadCenters = new Dictionary<Vector2Int, Vector2>();
        foreach (var c in targetMap.chunks)
        {
            Vector2 cCenter = panOffset + GetChunkCenterScreen(c.chunkCoord, radius, hexSize);
            if (c.hexes == null) continue;
            foreach (var h in c.hexes)
            {
                if (h.isRoad)
                {
                    Vector2Int g = PredefinedMapLayoutSO.PredefinedToGlobalHex(c.chunkCoord, h.localCoord, radius);
                    roadCenters[g] = cCenter + AxialToScreen(h.localCoord, hexSize);
                }
            }
        }

        Handles.color = new Color(0.95f, 0.90f, 0.65f, 0.45f);
        foreach (var kvp in roadCenters)
        {
            Vector2Int hex = kvp.Key;
            Vector2 p1 = kvp.Value;
            foreach (var neighbor in HexGridMath.GetNeighbors(hex))
            {
                if (roadCenters.TryGetValue(neighbor, out var p2))
                {
                    // Rysujemy tylko raz dla każdej pary
                    if (hex.x < neighbor.x || (hex.x == neighbor.x && hex.y < neighbor.y))
                    {
                        Handles.DrawLine(p1, p2);
                    }
                }
            }
        }
    }

    // =========================================================================
    // OBSŁUGA INPUTU (MYSZKA, MALOWANIE, PAN, ZOOM)
    // =========================================================================
    private void HandleCanvasInput(Rect canvasRect)
    {
        Event e = Event.current;
        if (!canvasRect.Contains(e.mousePosition)) return;

        int controlID = GUIUtility.GetControlID(FocusType.Passive);

        // 1. Panowanie (PPM lub ŚPM)
        if ((e.type == EventType.MouseDown && (e.button == 1 || e.button == 2)))
        {
            isPanning = true;
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && isPanning)
        {
            panOffset += e.delta;
            e.Use();
            Repaint();
        }
        else if (e.type == EventType.MouseUp && isPanning)
        {
            isPanning = false;
            e.Use();
        }

        // 2. Zoom kółkiem myszy
        if (e.type == EventType.ScrollWheel)
        {
            float oldSize = hexSize;
            hexSize = Mathf.Clamp(hexSize - e.delta.y * 1.5f, 14f, 65f);
            // Zoom względem kursora myszy
            Vector2 mouseRel = e.mousePosition - canvasRect.position;
            panOffset -= (mouseRel - panOffset) * ((hexSize - oldSize) / oldSize);
            e.Use();
            Repaint();
        }

        // 3. Kliknięcie lub przeciąganie LPM (Malowanie / Wybór)
        if (targetMap != null && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
        {
            Vector2 localMouse = e.mousePosition - canvasRect.position;
            if (TryPickHexAtScreenPos(localMouse, out var hitRef))
            {
                ApplyBrushToHex(hitRef);
                e.Use();
                Repaint();
            }
            else if (currentTool == EditorToolMode.AddChunk && e.type == EventType.MouseDown)
            {
                // Kliknięcie w puste pole w trybie AddChunk
                if (TryPickEmptyChunkSlot(localMouse, out var chunkCoord))
                {
                    Undo.RecordObject(targetMap, "Dodanie nowego chunka");
                    targetMap.GetOrCreateChunk(chunkCoord, false);
                    EditorUtility.SetDirty(targetMap);
                    e.Use();
                    Repaint();
                }
            }
        }
    }

    private bool TryPickHexAtScreenPos(Vector2 screenPos, out PredefinedHexRef pickedHex)
    {
        pickedHex = default;
        if (targetMap == null || targetMap.chunks == null) return false;

        int radius = targetMap.chunkRadius > 0 ? targetMap.chunkRadius : 3;

        foreach (var chunk in targetMap.chunks)
        {
            Vector2 chunkCenter = panOffset + GetChunkCenterScreen(chunk.chunkCoord, radius, hexSize);
            Vector2 offsetFromCenter = screenPos - chunkCenter;

            Vector2Int localAxial = ScreenToAxial(offsetFromCenter, hexSize);
            if (HexGridMath.GetDistance(Vector2Int.zero, localAxial) <= radius)
            {
                pickedHex = new PredefinedHexRef(chunk.chunkCoord, localAxial);
                return true;
            }
        }

        return false;
    }

    private bool TryPickEmptyChunkSlot(Vector2 screenPos, out Vector2Int emptyChunk)
    {
        emptyChunk = default;
        int radius = targetMap.chunkRadius > 0 ? targetMap.chunkRadius : 3;

        // Szukamy najbliższego wolnego sąsiada istniejących chunków
        var existingCoords = new HashSet<Vector2Int>(targetMap.chunks.Select(c => c.chunkCoord));
        float closestDist = float.MaxValue;
        Vector2Int candidate = default;
        bool found = false;

        foreach (var c in targetMap.chunks)
        {
            foreach (var n in HexGridMath.GetNeighbors(c.chunkCoord))
            {
                if (existingCoords.Contains(n)) continue;

                Vector2 center = panOffset + GetChunkCenterScreen(n, radius, hexSize);
                float dist = Vector2.Distance(screenPos, center);
                if (dist < GetChunkRadiusPixel(radius, hexSize) * 1.2f && dist < closestDist)
                {
                    closestDist = dist;
                    candidate = n;
                    found = true;
                }
            }
        }

        if (found)
        {
            emptyChunk = candidate;
            return true;
        }

        return false;
    }

    private void ApplyBrushToHex(PredefinedHexRef hexRef)
    {
        if (targetMap == null) return;
        Undo.RecordObject(targetMap, "Modyfikacja heksa mapy");

        var chunk = targetMap.GetOrCreateChunk(hexRef.chunkCoord);
        var hex = chunk.GetOrCreateHex(hexRef.localCoord);

        switch (currentTool)
        {
            case EditorToolMode.Select:
                selectedHex = hexRef;
                selectedChunkCoord = hexRef.chunkCoord;
                break;

            case EditorToolMode.Road:
                hex.isRoad = roadBrushMode;
                selectedHex = hexRef;
                break;

            case EditorToolMode.Terrain:
                hex.feature = brushFeature;
                hex.featureLevel = brushFeatureLevel;
                hex.elevation = brushElevation;
                selectedHex = hexRef;
                break;

            case EditorToolMode.Building:
                hex.startingBuilding = brushBuilding;
                selectedHex = hexRef;
                break;

            case EditorToolMode.SetBase:
                targetMap.playerBase = hexRef;
                selectedHex = hexRef;
                break;

            case EditorToolMode.SetSpawner:
                if (targetMap.enemySpawners == null) targetMap.enemySpawners = new List<PredefinedHexRef>();
                if (targetMap.enemySpawners.Contains(hexRef))
                {
                    targetMap.enemySpawners.Remove(hexRef);
                }
                else
                {
                    targetMap.enemySpawners.Add(hexRef);
                    hex.isRoad = true; // Spawner leży na drodze
                }
                selectedHex = hexRef;
                break;

            case EditorToolMode.Eraser:
                hex.feature = HexFeatureType.None;
                hex.featureLevel = 0;
                hex.elevation = 0;
                hex.isRoad = false;
                hex.startingBuilding = null;
                selectedHex = hexRef;
                break;
        }

        EditorUtility.SetDirty(targetMap);
    }

    // =========================================================================
    // PRAWY PANEL (INSPEKTOR HE موضوعKSA)
    // =========================================================================
    private void DrawRightPanel(Rect rect)
    {
        GUILayout.BeginArea(rect, EditorStyles.helpBox);
        rightScrollPos = GUILayout.BeginScrollView(rightScrollPos);

        GUILayout.Label("INSPEKTOR ZAZNACZENIA", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        if (targetMap == null)
        {
            GUILayout.Label("Brak wybranej mapy.", EditorStyles.centeredGreyMiniLabel);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            return;
        }

        if (!selectedHex.HasValue)
        {
            GUILayout.Label("Kliknij dowolny heks na mapie,\naby zobaczyć i edytować jego dane.", EditorStyles.centeredGreyMiniLabel);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            return;
        }

        var hRef = selectedHex.Value;
        var chunk = targetMap.GetChunk(hRef.chunkCoord);
        var hexData = chunk?.GetHex(hRef.localCoord);

        GUILayout.Label($"Chunk: ({hRef.chunkCoord.x}, {hRef.chunkCoord.y})", EditorStyles.boldLabel);
        GUILayout.Label($"Heks Lokalny: ({hRef.localCoord.x}, {hRef.localCoord.y})", EditorStyles.boldLabel);

        Vector2Int globalHex = PredefinedMapLayoutSO.PredefinedToGlobalHex(hRef.chunkCoord, hRef.localCoord, targetMap.chunkRadius);
        GUILayout.Label($"Global Axial: ({globalHex.x}, {globalHex.y})", EditorStyles.miniLabel);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        if (hexData == null)
        {
            GUILayout.Label("Ten heks ma domyślne parametry (czysta trawa).");
            if (GUILayout.Button("+ Dodaj dane dla heksa"))
            {
                Undo.RecordObject(targetMap, "Utworzenie danych heksa");
                chunk.GetOrCreateHex(hRef.localCoord);
                EditorUtility.SetDirty(targetMap);
            }
        }
        else
        {
            EditorGUI.BeginChangeCheck();

            bool isRoad = EditorGUILayout.Toggle("Droga (isRoad)", hexData.isRoad);
            HexFeatureType feature = (HexFeatureType)EditorGUILayout.EnumPopup("Cecha Terenu", hexData.feature);
            int level = EditorGUILayout.IntSlider("Poziom cechy", hexData.featureLevel, 0, 3);
            int elevation = EditorGUILayout.IntSlider("Wysokość (Elevation)", hexData.elevation, -3, 4);
            BuildingData bld = (BuildingData)EditorGUILayout.ObjectField("Budynek", hexData.startingBuilding, typeof(BuildingData), false);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(targetMap, "Edycja heksa");
                hexData.isRoad = isRoad;
                hexData.feature = feature;
                hexData.featureLevel = level;
                hexData.elevation = elevation;
                hexData.startingBuilding = bld;
                EditorUtility.SetDirty(targetMap);
            }

            EditorGUILayout.Space(12);

            // Przyciski strategiczne
            bool isBase = targetMap.playerBase.Equals(hRef);
            if (isBase)
            {
                GUI.backgroundColor = new Color(1f, 0.9f, 0.3f);
                GUILayout.Label("★ TEN HEKS TO BAZA GRACZA ★", EditorStyles.boldLabel);
                GUI.backgroundColor = Color.white;
            }
            else
            {
                if (GUILayout.Button("Ustaw jako Bazę Gracza (HQ)"))
                {
                    Undo.RecordObject(targetMap, "Ustawienie bazy");
                    targetMap.playerBase = hRef;
                    EditorUtility.SetDirty(targetMap);
                }
            }

            EditorGUILayout.Space(4);

            bool isSpawner = targetMap.enemySpawners != null && targetMap.enemySpawners.Contains(hRef);
            if (isSpawner)
            {
                GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
                if (GUILayout.Button("Usuń ze Spawnerów"))
                {
                    Undo.RecordObject(targetMap, "Usunięcie spawnera");
                    targetMap.enemySpawners.Remove(hRef);
                    EditorUtility.SetDirty(targetMap);
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                if (GUILayout.Button("+ Ustaw jako Spawner Wrogów"))
                {
                    Undo.RecordObject(targetMap, "Dodanie spawnera");
                    if (targetMap.enemySpawners == null) targetMap.enemySpawners = new List<PredefinedHexRef>();
                    targetMap.enemySpawners.Add(hRef);
                    hexData.isRoad = true;
                    EditorUtility.SetDirty(targetMap);
                }
            }

            EditorGUILayout.Space(12);
            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("Wyczyść heks (Gumka)"))
            {
                Undo.RecordObject(targetMap, "Wyczyszczenie heksa");
                chunk.RemoveHex(hRef.localCoord);
                EditorUtility.SetDirty(targetMap);
            }
            GUI.backgroundColor = Color.white;
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // =========================================================================
    // AKCJE I DIALOGI
    // =========================================================================
    private void CreateNewMapAssetDialog()
    {
        string defaultPath = "Assets/Main folder/ScriptableObjects/Configs";
        if (!Directory.Exists(defaultPath)) Directory.CreateDirectory(defaultPath);

        string fullPath = EditorUtility.SaveFilePanelInProject("Utwórz nową mapę predefiniowaną", "TutorialMapLayout", "asset", "Podaj nazwę pliku", defaultPath);
        if (string.IsNullOrEmpty(fullPath)) return;

        var newAsset = CreateInstance<PredefinedMapLayoutSO>();
        newAsset.mapName = Path.GetFileNameWithoutExtension(fullPath);
        newAsset.chunkRadius = 3;

        // Tworzymy domyślny chunk środkowy (0,0)
        var centerChunk = newAsset.GetOrCreateChunk(Vector2Int.zero, true);
        newAsset.playerBase = new PredefinedHexRef(Vector2Int.zero, Vector2Int.zero);

        AssetDatabase.CreateAsset(newAsset, fullPath);
        AssetDatabase.SaveAssets();

        targetMap = newAsset;
        hasValidated = false;
        Repaint();
    }

    private void RunMapValidation()
    {
        if (targetMap == null) return;
        hasValidated = true;
        targetMap.ValidateMap(out lastValidationErrors, out lastValidationWarnings);
        Repaint();
    }

    // =========================================================================
    // GEOMETRIA I MATEMATYKA EKRANOWA
    // =========================================================================
    public static Vector2 AxialToScreen(Vector2Int coord, float size)
    {
        float x = size * Mathf.Sqrt(3f) * (coord.x + coord.y / 2f);
        float y = size * 3f / 2f * coord.y;
        return new Vector2(x, y);
    }

    public static Vector2Int ScreenToAxial(Vector2 screenOffset, float size)
    {
        float q = (Mathf.Sqrt(3f) / 3f * screenOffset.x - 1f / 3f * screenOffset.y) / size;
        float r = (2f / 3f * screenOffset.y) / size;
        return AxialRound(q, r);
    }

    public static Vector2 GetChunkCenterScreen(Vector2Int chunkCoord, int radius, float size)
    {
        int cq = chunkCoord.x;
        int cr = chunkCoord.y;
        int centerQ = cq * (2 * radius + 1) + cr * radius;
        int centerR = cq * -radius + cr * (radius + 1);
        return AxialToScreen(new Vector2Int(centerQ, centerR), size);
    }

    public static float GetChunkRadiusPixel(int radius, float size)
    {
        return size * Mathf.Sqrt(3f) * (radius + 0.5f);
    }

    private static Vector2Int AxialRound(float x, float z)
    {
        float y = -x - z;
        int rx = Mathf.RoundToInt(x);
        int ry = Mathf.RoundToInt(y);
        int rz = Mathf.RoundToInt(z);
        float x_diff = Mathf.Abs(rx - x);
        float y_diff = Mathf.Abs(ry - y);
        float z_diff = Mathf.Abs(rz - z);

        if (x_diff > y_diff && x_diff > z_diff) rx = -ry - rz;
        else if (y_diff > z_diff) ry = -rx - rz;
        else rz = -rx - ry;
        return new Vector2Int(rx, rz);
    }

    private static Vector3[] GetHexCorners(Vector2 center, float size)
    {
        Vector3[] corners = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float angleRad = Mathf.PI / 180f * (60f * i - 30f);
            corners[i] = new Vector3(center.x + size * Mathf.Cos(angleRad), center.y + size * Mathf.Sin(angleRad), 0);
        }
        return corners;
    }
}
#endif
