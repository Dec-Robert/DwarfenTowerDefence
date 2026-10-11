#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class BuildingEfficiencyEditorWindow : EditorWindow
{
    private enum ViewTab
    {
        DetailedEditor = 0,
        MatrixComparison = 1
    }

    // --- Stan okna ---
    private ViewTab currentTab = ViewTab.DetailedEditor;
    private List<BuildingData> allBuildings = new List<BuildingData>();
    private BuildingData selectedBuilding;

    // --- Filtry listy ---
    private string searchFilter = "";
    private int categoryFilterIndex = 0; // 0 = Wszystkie, 1 = Economic, 2 = Defense, 3 = Utility, 4 = Unique
    private readonly string[] categoryFilterNames = { "Wszystkie", "Economic", "Defense (Wieże)", "Utility", "Unique" };

    // --- Scroll pozycje ---
    private Vector2 leftListScroll;
    private Vector2 centerScroll;
    private Vector2 rightSimulatorScroll;
    private Vector2 matrixScroll;

    // --- Generator krzywej ---
    private float genPassivePercent = 0f;
    private float genFirstWorkerPercent = 100f;
    private float genStepPercent = 25f;
    private float genDiminishingFactor = 1.0f;
    private int genMaxWorkers = 5;

    // --- Schowek (Copy / Paste) ---
    private List<float> clipboardTable;
    private string clipboardSourceName = "";

    // --- Symulator na żywo ---
    private bool simHasSupplies = true;
    private float simTerrainBonusFlat = 0f;
    private float simGlobalMultiplierPercent = 0f;
    private float simMetaPassivePercent = 0f;
    private float simMetaPerWorkerPercent = 0f;

    // --- Wymiary układu ---
    private const float LEFT_PANEL_WIDTH = 250f;
    private const float RIGHT_PANEL_WIDTH = 370f;

    [InitializeOnLoadMethod]
    private static void AutoMigrateEmptyTablesOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            string[] guids = AssetDatabase.FindAssets("t:BuildingData");
            bool anyModified = false;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                BuildingData data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
                if (data == null || data is HousingBuildingData || data.type == BuildingType.Housing)
                    continue;

                if (data.efficiencyTable == null || data.efficiencyTable.Count == 0)
                {
                    data.EnsureDefaultEfficiencyTable();
                    EditorUtility.SetDirty(data);
                    anyModified = true;
                }
            }

            if (anyModified)
            {
                AssetDatabase.SaveAssets();
            }
        };
    }

    [MenuItem("Tools/Buildings/Efficiency Table Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<BuildingEfficiencyEditorWindow>("Efficiency Table Editor");
        window.minSize = new Vector2(1120, 620);
        window.Show();
    }

    public static void OpenForBuilding(BuildingData building)
    {
        var window = GetWindow<BuildingEfficiencyEditorWindow>("Efficiency Table Editor");
        window.minSize = new Vector2(1120, 620);
        window.RefreshBuildingsList();
        if (building != null)
        {
            window.SelectBuilding(building);
            window.currentTab = ViewTab.DetailedEditor;
        }
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        RefreshBuildingsList();
    }

    private void RefreshBuildingsList()
    {
        allBuildings.Clear();

        string[] guids = AssetDatabase.FindAssets("t:BuildingData");
        bool anyInitialized = false;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BuildingData data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
            if (data == null) continue;

            // Pomijamy budynki mieszkalne (Housing ma własny system populacji bez pracowników)
            if (data is HousingBuildingData || data.type == BuildingType.Housing) continue;

            if (data.efficiencyTable == null || data.efficiencyTable.Count == 0)
            {
                Undo.RecordObject(data, "Inicjalizacja domyślnej tabeli wydajności");
                data.EnsureDefaultEfficiencyTable();
                EditorUtility.SetDirty(data);
                anyInitialized = true;
            }

            allBuildings.Add(data);
        }

        if (anyInitialized)
        {
            AssetDatabase.SaveAssets();
        }

        allBuildings = allBuildings
            .OrderBy(b => GetCategorySortOrder(b))
            .ThenBy(b => string.IsNullOrEmpty(b.buildingName) ? b.name : b.buildingName)
            .ToList();

        if (selectedBuilding == null && allBuildings.Count > 0)
        {
            SelectBuilding(allBuildings[0]);
        }
    }

    private int GetCategorySortOrder(BuildingData b)
    {
        if (b is TowerData || b.type == BuildingType.Defense) return 1;
        if (b.type == BuildingType.Economic) return 0;
        if (b.type == BuildingType.Utility) return 2;
        return 3;
    }

    private void SelectBuilding(BuildingData building)
    {
        selectedBuilding = building;
        GUI.FocusControl(null);

        if (selectedBuilding != null)
        {
            selectedBuilding.EnsureDefaultEfficiencyTable();
            if (selectedBuilding.efficiencyTable.Count > 0)
                genPassivePercent = Mathf.Round(selectedBuilding.efficiencyTable[0] * 100f);
            if (selectedBuilding.efficiencyTable.Count > 1)
                genFirstWorkerPercent = Mathf.Round(selectedBuilding.efficiencyTable[1] * 100f);
            if (selectedBuilding.efficiencyTable.Count > 2)
                genStepPercent = Mathf.Round((selectedBuilding.efficiencyTable[2] - selectedBuilding.efficiencyTable[1]) * 100f);
            genMaxWorkers = Mathf.Max(1, selectedBuilding.efficiencyTable.Count - 1);
        }
    }

    private void OnGUI()
    {
        DrawTopToolbar();

        float contentY = 32f;
        float contentHeight = position.height - contentY;

        if (currentTab == ViewTab.DetailedEditor)
        {
            Rect leftRect = new Rect(0, contentY, LEFT_PANEL_WIDTH, contentHeight);
            float centerWidth = Mathf.Max(380f, position.width - LEFT_PANEL_WIDTH - RIGHT_PANEL_WIDTH);
            Rect centerRect = new Rect(LEFT_PANEL_WIDTH, contentY, centerWidth, contentHeight);
            Rect rightRect = new Rect(LEFT_PANEL_WIDTH + centerWidth, contentY, RIGHT_PANEL_WIDTH, contentHeight);

            DrawBuildingListPanel(leftRect);
            DrawCenterEditorPanel(centerRect);
            DrawRightSimulatorPanel(rightRect);
        }
        else
        {
            Rect matrixRect = new Rect(0, contentY, position.width, contentHeight);
            DrawMatrixComparisonView(matrixRect);
        }
    }

    // =========================================================================
    // GÓRNY PASEK NARZĘDZI
    // =========================================================================
    private void DrawTopToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(30));

        if (GUILayout.Button("🔄 Odśwież", EditorStyles.toolbarButton, GUILayout.Width(85)))
        {
            RefreshBuildingsList();
        }

        GUILayout.Space(8);

        // Zakładki widoku
        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = currentTab == ViewTab.DetailedEditor ? new Color(0.3f, 0.65f, 1f) : Color.white;
        if (GUILayout.Button("📊 Edytor Szczegółowy + Symulator", EditorStyles.toolbarButton, GUILayout.Width(210)))
        {
            currentTab = ViewTab.DetailedEditor;
        }

        GUI.backgroundColor = currentTab == ViewTab.MatrixComparison ? new Color(0.3f, 0.65f, 1f) : Color.white;
        if (GUILayout.Button("📋 Widok Zbiorczy (Matrix)", EditorStyles.toolbarButton, GUILayout.Width(180)))
        {
            currentTab = ViewTab.MatrixComparison;
        }
        GUI.backgroundColor = prevBg;

        GUILayout.FlexibleSpace();

        if (!string.IsNullOrEmpty(clipboardSourceName) && clipboardTable != null)
        {
            GUILayout.Label($"📋 Schowek: {clipboardSourceName} ({clipboardTable.Count} progów)", EditorStyles.miniLabel);
            GUILayout.Space(10);
        }

        if (GUILayout.Button("💾 Zapisz Assety", EditorStyles.toolbarButton, GUILayout.Width(115)))
        {
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("Zapisano wszystkie zmiany w assetach!"));
        }

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================================
    // LEWY PANEL: LISTA BUDYNKÓW
    // =========================================================================
    private void DrawBuildingListPanel(Rect rect)
    {
        GUILayout.BeginArea(rect, EditorStyles.helpBox);

        EditorGUILayout.LabelField("Budynki i Wieże", EditorStyles.boldLabel);

        // Wyszukiwarka
        EditorGUILayout.BeginHorizontal();
        searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(22)))
        {
            searchFilter = "";
            GUI.FocusControl(null);
        }
        EditorGUILayout.EndHorizontal();

        // Filtr kategorii
        categoryFilterIndex = EditorGUILayout.Popup("Kategoria", categoryFilterIndex, categoryFilterNames);

        EditorGUILayout.Space(4);
        leftListScroll = EditorGUILayout.BeginScrollView(leftListScroll);

        string lastCategoryHeader = "";

        foreach (var b in allBuildings)
        {
            if (b == null) continue;
            if (!PassesFilter(b)) continue;

            string catName = (b is TowerData || b.type == BuildingType.Defense) ? "DEFENSE (WIEŻE)" : b.type.ToString().ToUpper();
            if (catName != lastCategoryHeader)
            {
                lastCategoryHeader = catName;
                EditorGUILayout.Space(6);
                GUI.backgroundColor = new Color(0.35f, 0.35f, 0.35f);
                GUILayout.Label($"── {catName} ──", EditorStyles.toolbarButton);
                GUI.backgroundColor = Color.white;
            }

            bool isSelected = (selectedBuilding == b);
            GUI.backgroundColor = isSelected ? new Color(0.28f, 0.58f, 0.95f) : new Color(0.88f, 0.88f, 0.88f);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(36));

            // Ikona
            Rect iconRect = GUILayoutUtility.GetRect(28, 28, GUILayout.Width(28), GUILayout.Height(28));
            if (b.icon != null)
            {
                DrawSpritePreview(iconRect, b.icon);
            }
            else
            {
                EditorGUI.DrawRect(iconRect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            }

            // Nazwa i mini-info o tabeli
            EditorGUILayout.BeginVertical();
            string displayName = string.IsNullOrEmpty(b.buildingName) ? b.name : $"{b.buildingName} ({b.name})";
            if (GUILayout.Button(displayName, EditorStyles.boldLabel))
            {
                SelectBuilding(b);
            }

            string summary = GetQuickTableSummary(b);
            if (GUILayout.Button(summary, EditorStyles.miniLabel))
            {
                SelectBuilding(b);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private bool PassesFilter(BuildingData b)
    {
        if (!string.IsNullOrEmpty(searchFilter))
        {
            bool matchName = (b.buildingName != null && b.buildingName.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                             || b.name.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
            if (!matchName) return false;
        }

        switch (categoryFilterIndex)
        {
            case 1: return b.type == BuildingType.Economic && !(b is TowerData);
            case 2: return b is TowerData || b.type == BuildingType.Defense;
            case 3: return b.type == BuildingType.Utility;
            case 4: return b.type == BuildingType.Unique;
            default: return true;
        }
    }

    private string GetQuickTableSummary(BuildingData b)
    {
        if (b.efficiencyTable == null || b.efficiencyTable.Count == 0)
            return $"Max: {b.baseMaxWorkers} | Brak tabeli";

        float p0 = b.efficiencyTable[0] * 100f;
        float p1 = b.efficiencyTable.Count > 1 ? b.efficiencyTable[1] * 100f : p0;
        float pLast = b.efficiencyTable[b.efficiencyTable.Count - 1] * 100f;
        return $"Bazowo: {b.baseMaxWorkers} prac. | 0:{p0:F0}% → 1:{p1:F0}% → Max:{pLast:F0}%";
    }

    // =========================================================================
    // ŚRODKOWY PANEL: EDYTOR SZCZEGÓŁOWY TABELI I GENERATOR
    // =========================================================================
    private void DrawCenterEditorPanel(Rect rect)
    {
        GUILayout.BeginArea(rect);
        centerScroll = EditorGUILayout.BeginScrollView(centerScroll);

        if (selectedBuilding == null)
        {
            EditorGUILayout.HelpBox("Wybierz budynek z listy po lewej stronie.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            return;
        }

        selectedBuilding.EnsureDefaultEfficiencyTable();

        // 1. Nagłówek budynku
        DrawSelectedBuildingHeader();

        EditorGUILayout.Space(6);

        // 2. Sekcja Limitów Pracowników
        int maxTreeExtra = CalculateMaxExtraWorkersFromTree(selectedBuilding);
        int maxMetaExtra = CalculateMaxExtraWorkersFromMeta();
        int totalPossibleWorkers = selectedBuilding.baseMaxWorkers + maxTreeExtra + maxMetaExtra;

        DrawWorkerLimitsSection(maxTreeExtra, maxMetaExtra, totalPossibleWorkers);

        EditorGUILayout.Space(8);

        // 3. Wizualny wykres słupkowy progów
        DrawVisualEfficiencyChart(selectedBuilding, totalPossibleWorkers);

        EditorGUILayout.Space(8);

        // 4. Interaktywna lista progów (Suwaki + % + Mnożnik + Delta)
        DrawInteractiveTableRows(selectedBuilding, totalPossibleWorkers);

        EditorGUILayout.Space(10);

        // 5. Generator Krzywej i Narzędzia Schowka / Presety
        DrawGeneratorAndPresetsSection(selectedBuilding);

        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawSelectedBuildingHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        Rect iconRect = GUILayoutUtility.GetRect(42, 42, GUILayout.Width(42), GUILayout.Height(42));
        if (selectedBuilding.icon != null)
        {
            DrawSpritePreview(iconRect, selectedBuilding.icon);
        }
        else
        {
            EditorGUI.DrawRect(iconRect, new Color(0.2f, 0.2f, 0.2f));
        }

        EditorGUILayout.BeginVertical();
        string title = string.IsNullOrEmpty(selectedBuilding.buildingName)
            ? selectedBuilding.name
            : $"{selectedBuilding.buildingName} ({selectedBuilding.name})";
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        string typeLabel = (selectedBuilding is TowerData || selectedBuilding.type == BuildingType.Defense)
            ? "Typ: Obrona (TowerData)"
            : $"Typ: {selectedBuilding.type}";
        EditorGUILayout.LabelField(typeLabel, EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("🔍 Pokaż Asset", GUILayout.Width(105), GUILayout.Height(28)))
        {
            Selection.activeObject = selectedBuilding;
            EditorGUIUtility.PingObject(selectedBuilding);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawWorkerLimitsSection(int maxTreeExtra, int maxMetaExtra, int totalPossibleWorkers)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("── Limity Pracowników i Rozmiar Tabeli ──", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        int newBaseMax = EditorGUILayout.IntSlider(
            new GUIContent("Bazowi Pracownicy (baseMaxWorkers)", "Maksymalna liczba pracowników w budynku bez ulepszeń i bez bonusów Meta."),
            selectedBuilding.baseMaxWorkers, 1, 10);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(selectedBuilding, "Zmiana baseMaxWorkers");
            selectedBuilding.baseMaxWorkers = newBaseMax;
            // Upewnij się, że tabela ma przynajmniej baseMaxWorkers + 1 wpisów
            EnsureTableHasAtLeast(selectedBuilding, newBaseMax + 1);
            EditorUtility.SetDirty(selectedBuilding);
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(
            $"Bazowo: {selectedBuilding.baseMaxWorkers}  |  Max z drzewka ulepszeń: +{maxTreeExtra}  |  Z Meta-Progresji: +{maxMetaExtra}  →  Maks. w grze: {totalPossibleWorkers} prac.",
            EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button($"Dopasuj tabelę do limitu bazowego (0..{selectedBuilding.baseMaxWorkers})", EditorStyles.miniButtonLeft))
        {
            ResizeTable(selectedBuilding, selectedBuilding.baseMaxWorkers + 1);
        }
        if (GUILayout.Button($"Dopasuj do maks. z ulepszeniami/Meta (0..{totalPossibleWorkers})", EditorStyles.miniButtonRight))
        {
            ResizeTable(selectedBuilding, totalPossibleWorkers + 1);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // =========================================================================
    // WIZUALNY WYKRES PROGÓW (BAR CHART)
    // =========================================================================
    private void DrawVisualEfficiencyChart(BuildingData b, int totalPossibleWorkers)
    {
        var table = b.efficiencyTable;
        if (table == null || table.Count == 0) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("── Wizualizacja Progów Wydajności ──", EditorStyles.boldLabel);

        float maxVal = Mathf.Max(1.5f, table.Max() * 1.15f);
        Rect chartRect = GUILayoutUtility.GetRect(100, 115, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(chartRect, new Color(0.14f, 0.15f, 0.17f, 1f));

        // Linia referencyjna 100%
        float y100 = chartRect.yMax - 22f - Mathf.Clamp01(1.0f / maxVal) * (chartRect.height - 38f);
        EditorGUI.DrawRect(new Rect(chartRect.x + 8, y100, chartRect.width - 16, 1f), new Color(1f, 0.85f, 0.2f, 0.35f));
        GUI.Label(new Rect(chartRect.x + 10, y100 - 14, 50, 14), "100%", EditorStyles.miniLabel);

        int count = table.Count;
        float padding = 12f;
        float spacing = 6f;
        float availableWidth = chartRect.width - padding * 2 - spacing * (count - 1);
        float barWidth = Mathf.Max(24f, availableWidth / count);
        float maxBarHeight = chartRect.height - 40f;

        GUIStyle centeredMini = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        for (int i = 0; i < count; i++)
        {
            float eff = Mathf.Max(0f, table[i]);
            float normalized = Mathf.Clamp01(eff / maxVal);
            float barH = Mathf.Max(4f, normalized * maxBarHeight);

            float x = chartRect.x + padding + i * (barWidth + spacing);
            float y = chartRect.yMax - 20f - barH;

            Color barCol = GetSlotColor(i, b.baseMaxWorkers, totalPossibleWorkers);
            EditorGUI.DrawRect(new Rect(x, y, barWidth, barH), barCol);

            // Etykieta % nad słupkiem
            string pctText = $"{Mathf.RoundToInt(eff * 100f)}%";
            GUI.Label(new Rect(x - 4, y - 16, barWidth + 8, 15), pctText, centeredMini);

            // Podpis liczby pracowników pod słupkiem
            string slotLabel = i == 0 ? "0 (Pas.)" : $"{i} prac.";
            GUI.Label(new Rect(x - 4, chartRect.yMax - 18, barWidth + 8, 16), slotLabel, centeredMini);
        }

        EditorGUILayout.EndVertical();
    }

    private Color GetSlotColor(int workerIndex, int baseMaxWorkers, int totalPossibleWorkers)
    {
        if (workerIndex == 0)
            return new Color(0.82f, 0.32f, 0.25f, 0.9f); // Pasywny (0 prac.) - czerwono-pomarańczowy
        if (workerIndex <= baseMaxWorkers)
            return new Color(0.22f, 0.72f, 0.35f, 0.95f); // Bazowi pracownicy - zielony
        if (workerIndex <= totalPossibleWorkers)
            return new Color(0.25f, 0.58f, 0.92f, 0.95f); // Slot z ulepszeń / Meta - niebieski
        return new Color(0.55f, 0.45f, 0.78f, 0.9f); // Ponad obecny limit - fioletowy
    }

    // =========================================================================
    // INTERAKTYWNA TABELA WIERSZY (0 .. N PRACOWNIKÓW)
    // =========================================================================
    private void DrawInteractiveTableRows(BuildingData b, int totalPossibleWorkers)
    {
        var table = b.efficiencyTable;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("── Tabela Wydajności (Indeks = Liczba Pracowników) ──", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+ Dodaj próg", EditorStyles.miniButtonLeft, GUILayout.Width(95)))
        {
            Undo.RecordObject(b, "Dodaj próg wydajności");
            float lastVal = table.Count > 0 ? table[table.Count - 1] : 1f;
            float prevVal = table.Count > 1 ? table[table.Count - 2] : lastVal - 0.25f;
            float step = Mathf.Max(0f, lastVal - prevVal);
            table.Add(Mathf.Round((lastVal + step) * 100f) / 100f);
            EditorUtility.SetDirty(b);
        }
        GUI.enabled = table.Count > 2;
        if (GUILayout.Button("- Usuń ostatni", EditorStyles.miniButtonRight, GUILayout.Width(95)))
        {
            Undo.RecordObject(b, "Usuń próg wydajności");
            table.RemoveAt(table.Count - 1);
            EditorUtility.SetDirty(b);
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        for (int i = 0; i < table.Count; i++)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Kolorowy znacznik typu progu
            Color badgeColor = GetSlotColor(i, b.baseMaxWorkers, totalPossibleWorkers);
            Rect colorBar = GUILayoutUtility.GetRect(6, 22, GUILayout.Width(6), GUILayout.Height(22));
            EditorGUI.DrawRect(colorBar, badgeColor);

            // Opis progu
            string roleTag;
            if (i == 0) roleTag = "0 prac. [Pasywny]";
            else if (i <= b.baseMaxWorkers) roleTag = $"{i} prac. [Bazowy]";
            else if (i <= totalPossibleWorkers) roleTag = $"{i} prac. [Bonus Upgrade/Meta]";
            else roleTag = $"{i} prac. [Zapasowy]";

            EditorGUILayout.LabelField(roleTag, GUILayout.Width(165));

            // Suwak w procentach (0% - 300%)
            float currentMultiplier = table[i];
            float currentPercent = Mathf.Round(currentMultiplier * 100f);

            EditorGUI.BeginChangeCheck();
            float sliderPercent = GUILayout.HorizontalSlider(currentPercent, 0f, 300f, GUILayout.MinWidth(90), GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(b, "Zmiana progu wydajności");
                table[i] = Mathf.Round(sliderPercent) / 100f;
                EditorUtility.SetDirty(b);
                currentMultiplier = table[i];
                currentPercent = Mathf.Round(currentMultiplier * 100f);
            }

            // Pole w %
            EditorGUI.BeginChangeCheck();
            float typedPercent = EditorGUILayout.FloatField(currentPercent, GUILayout.Width(50));
            EditorGUILayout.LabelField("%", GUILayout.Width(14));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(b, "Zmiana procentu wydajności");
                table[i] = Mathf.Max(0f, typedPercent / 100f);
                EditorUtility.SetDirty(b);
                currentMultiplier = table[i];
            }

            // Pole w mnożniku (x)
            EditorGUI.BeginChangeCheck();
            float typedMult = EditorGUILayout.FloatField(currentMultiplier, GUILayout.Width(48));
            EditorGUILayout.LabelField("x", GUILayout.Width(12));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(b, "Zmiana mnożnika wydajności");
                table[i] = Mathf.Max(0f, typedMult);
                EditorUtility.SetDirty(b);
            }

            // Delta względem poprzedniego poziomu
            if (i == 0)
            {
                EditorGUILayout.LabelField("(Start)", EditorStyles.miniLabel, GUILayout.Width(64));
            }
            else
            {
                float deltaPct = (table[i] - table[i - 1]) * 100f;
                string sign = deltaPct >= 0 ? "+" : "";
                GUIStyle deltaStyle = new GUIStyle(EditorStyles.miniBoldLabel);
                deltaStyle.normal.textColor = deltaPct > 0.01f
                    ? new Color(0.35f, 0.9f, 0.45f)
                    : (deltaPct < -0.01f ? new Color(0.95f, 0.35f, 0.35f) : Color.gray);
                EditorGUILayout.LabelField($"Δ {sign}{deltaPct:F0}%", deltaStyle, GUILayout.Width(64));
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    // =========================================================================
    // GENERATOR KRZYWEJ, PRESETY I SCHOWEK
    // =========================================================================
    private void DrawGeneratorAndPresetsSection(BuildingData b)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("── Szybki Generator Krzywej i Presety ──", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        genPassivePercent = EditorGUILayout.FloatField("0 prac. (Pasywny %)", genPassivePercent);
        genFirstWorkerPercent = EditorGUILayout.FloatField("1. Pracownik (%)", genFirstWorkerPercent);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        genStepPercent = EditorGUILayout.FloatField("Krok co pracownika (+%)", genStepPercent);
        genDiminishingFactor = EditorGUILayout.Slider("Wygaszanie kroku", genDiminishingFactor, 0.4f, 1.2f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        genMaxWorkers = EditorGUILayout.IntSlider("Liczba progów (1..N)", genMaxWorkers, 1, 10);
        if (GUILayout.Button("⚡ Wygeneruj do tego budynku", GUILayout.Height(22), GUILayout.Width(200)))
        {
            ApplyGeneratorToBuilding(b);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Gotowe Presety:", EditorStyles.miniBoldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Ekonomiczny (+25% liniowo)", EditorStyles.miniButtonLeft))
        {
            genPassivePercent = 0f;
            genFirstWorkerPercent = 100f;
            genStepPercent = 25f;
            genDiminishingFactor = 1.0f;
            genMaxWorkers = Mathf.Max(b.baseMaxWorkers + 3, 5);
            ApplyGeneratorToBuilding(b);
        }
        if (GUILayout.Button("Wieża Standard (20% / 100..140%)", EditorStyles.miniButtonMid))
        {
            Undo.RecordObject(b, "Preset Wieży");
            b.efficiencyTable = new List<float> { 0.2f, 1.0f, 1.1f, 1.2f, 1.3f, 1.35f, 1.4f };
            EditorUtility.SetDirty(b);
        }
        if (GUILayout.Button("Malejące korzyści (x0.75)", EditorStyles.miniButtonMid))
        {
            genPassivePercent = 0f;
            genFirstWorkerPercent = 100f;
            genStepPercent = 30f;
            genDiminishingFactor = 0.75f;
            genMaxWorkers = Mathf.Max(b.baseMaxWorkers + 3, 6);
            ApplyGeneratorToBuilding(b);
        }
        if (GUILayout.Button("Stałe 100% (1 prac.)", EditorStyles.miniButtonRight))
        {
            Undo.RecordObject(b, "Preset Stałe 100%");
            b.efficiencyTable = new List<float> { 0f, 1.0f };
            EditorUtility.SetDirty(b);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Schowek i Operacje Zbiorcze:", EditorStyles.miniBoldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("📋 Kopiuj tabelę", GUILayout.Height(24)))
        {
            clipboardTable = new List<float>(b.efficiencyTable);
            clipboardSourceName = string.IsNullOrEmpty(b.buildingName) ? b.name : b.buildingName;
        }

        GUI.enabled = clipboardTable != null && clipboardTable.Count > 0;
        if (GUILayout.Button($"📥 Wklej tabelę {(string.IsNullOrEmpty(clipboardSourceName) ? "" : $"({clipboardSourceName})")}", GUILayout.Height(24)))
        {
            Undo.RecordObject(b, "Wklej tabelę wydajności");
            b.efficiencyTable = new List<float>(clipboardTable);
            EditorUtility.SetDirty(b);
        }
        GUI.enabled = true;

        if (GUILayout.Button($"🌐 Zastosuj do całej kategorii ({b.type})", GUILayout.Height(24)))
        {
            ApplyTableToCategory(b);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void ApplyGeneratorToBuilding(BuildingData b)
    {
        Undo.RecordObject(b, "Wygeneruj tabelę wydajności");
        b.efficiencyTable.Clear();
        b.efficiencyTable.Add(Mathf.Round(genPassivePercent) / 100f);

        float currentVal = genFirstWorkerPercent;
        float currentStep = genStepPercent;

        for (int i = 1; i <= genMaxWorkers; i++)
        {
            if (i == 1)
            {
                b.efficiencyTable.Add(Mathf.Round(currentVal) / 100f);
            }
            else
            {
                currentVal += currentStep;
                b.efficiencyTable.Add(Mathf.Round(currentVal) / 100f);
                currentStep *= genDiminishingFactor;
            }
        }

        EditorUtility.SetDirty(b);
    }

    private void ApplyTableToCategory(BuildingData source)
    {
        bool isTowerSource = source is TowerData || source.type == BuildingType.Defense;
        var targets = allBuildings.Where(x =>
            x != null && x != source &&
            ((isTowerSource && (x is TowerData || x.type == BuildingType.Defense)) ||
             (!isTowerSource && x.type == source.type && !(x is TowerData)))).ToList();

        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("Brak innych budynków", "Nie znaleziono innych budynków w tej samej kategorii.", "OK");
            return;
        }

        if (EditorUtility.DisplayDialog(
            "Zastosuj tabelę do kategorii",
            $"Czy na pewno chcesz skopiować tabelę z '{source.buildingName}' do wszystkich ({targets.Count}) budynków z kategorii {(isTowerSource ? "Defense (Wieże)" : source.type.ToString())}?",
            "Tak, zastosuj", "Anuluj"))
        {
            foreach (var t in targets)
            {
                Undo.RecordObject(t, "Masowa aktualizacja tabeli wydajności");
                t.efficiencyTable = new List<float>(source.efficiencyTable);
                EditorUtility.SetDirty(t);
            }
            AssetDatabase.SaveAssets();
        }
    }

    // =========================================================================
    // PRAWY PANEL: SYMULATOR NA ŻYWO (PRODUKCJA / DPS WIEŻY)
    // =========================================================================
    private void DrawRightSimulatorPanel(Rect rect)
    {
        GUILayout.BeginArea(rect, EditorStyles.helpBox);
        rightSimulatorScroll = EditorGUILayout.BeginScrollView(rightSimulatorScroll);

        EditorGUILayout.LabelField("⚡ Symulator na Żywo (Live Preview)", EditorStyles.boldLabel);

        if (selectedBuilding == null)
        {
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
            return;
        }

        // Parametry symulacji
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Warunki Testowe:", EditorStyles.miniBoldLabel);

        simHasSupplies = EditorGUILayout.Toggle("Upkeep opłacony (Surowce)", simHasSupplies);
        if (!simHasSupplies)
        {
            EditorGUILayout.HelpBox("Brak surowców na utrzymanie nakłada karę -50% do wydajności (minimum 10%).", MessageType.Warning);
        }

        simMetaPassivePercent = EditorGUILayout.Slider("Meta: Pasywny (0 prac. %)", simMetaPassivePercent, 0f, 100f);
        simMetaPerWorkerPercent = EditorGUILayout.Slider("Meta: Bonus / prac. (%)", simMetaPerWorkerPercent, 0f, 50f);

        bool isTower = selectedBuilding is TowerData || selectedBuilding.type == BuildingType.Defense;
        if (!isTower)
        {
            simTerrainBonusFlat = EditorGUILayout.Slider("Bonus z terenu (+Flat)", simTerrainBonusFlat, 0f, 40f);
            simGlobalMultiplierPercent = EditorGUILayout.Slider("Mnożnik globalny (+%)", simGlobalMultiplierPercent, -50f, 200f);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        if (isTower && selectedBuilding is TowerData towerData)
        {
            DrawTowerSimulationTable(towerData);
        }
        else
        {
            DrawProductionSimulationTable(selectedBuilding);
        }

        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawTowerSimulationTable(TowerData tData)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Statystyki Bazowe Wieży:", EditorStyles.miniBoldLabel);
        float critChance01 = tData.criticalChancel > 1f ? Mathf.Clamp01(tData.criticalChancel / 100f) : Mathf.Clamp01(tData.criticalChancel);
        float critMult = Mathf.Max(1f, tData.criticalDamageMultiplier);
        EditorGUILayout.LabelField(
            $"DMG: {tData.baseDamage} | FireRate: {tData.fireRate}/s | Zasięg: {tData.baseRange} | Crit: {critChance01 * 100f:F0}% (x{critMult:F1})",
            EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // Nagłówek tabeli
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Prac.", EditorStyles.miniBoldLabel, GUILayout.Width(42));
        GUILayout.Label("Wydajność", EditorStyles.miniBoldLabel, GUILayout.Width(68));
        GUILayout.Label("DMG", EditorStyles.miniBoldLabel, GUILayout.Width(55));
        GUILayout.Label("Atak/s", EditorStyles.miniBoldLabel, GUILayout.Width(52));
        GUILayout.Label("Śr. DPS", EditorStyles.miniBoldLabel, GUILayout.Width(68));
        GUILayout.Label("Δ DPS", EditorStyles.miniBoldLabel, GUILayout.Width(55));
        EditorGUILayout.EndHorizontal();

        float prevDps = 0f;
        var table = tData.efficiencyTable;

        for (int i = 0; i < table.Count; i++)
        {
            float eff = CalculateSimulatedEfficiency(table[i], i, true);
            float dmg = tData.baseDamage * eff;
            float rate = tData.fireRate * eff;
            float avgCritFactor = 1f + critChance01 * (critMult - 1f);
            float dps = dmg * rate * avgCritFactor;

            bool isBaseCap = (i == tData.baseMaxWorkers);
            GUI.backgroundColor = isBaseCap ? new Color(0.3f, 0.7f, 0.4f, 0.35f) : Color.white;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(i == tData.baseMaxWorkers ? $"{i} ★" : i.ToString(), GUILayout.Width(42));
            GUILayout.Label($"{Mathf.RoundToInt(eff * 100f)}%", GUILayout.Width(68));
            GUILayout.Label($"{dmg:F1}", GUILayout.Width(55));
            GUILayout.Label($"{rate:F2}/s", GUILayout.Width(52));
            GUILayout.Label($"{dps:F1}", EditorStyles.boldLabel, GUILayout.Width(68));

            if (i == 0)
            {
                GUILayout.Label("-", EditorStyles.miniLabel, GUILayout.Width(55));
            }
            else
            {
                float diff = dps - prevDps;
                GUILayout.Label($"{(diff >= 0 ? "+" : "")}{diff:F1}", EditorStyles.miniLabel, GUILayout.Width(55));
            }

            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;
            prevDps = dps;
        }
    }

    private void DrawProductionSimulationTable(BuildingData bData)
    {
        var prodList = bData.productionPerCycle;
        if (prodList == null || prodList.Count == 0)
        {
            EditorGUILayout.HelpBox("Ten budynek nie ma zdefiniowanej produkcji bazowej (productionPerCycle jest puste).", MessageType.Info);
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        string prodSummary = string.Join(", ", prodList.Select(p => $"{p.type}: {p.amount}"));
        EditorGUILayout.LabelField($"Baza na cykl: {prodSummary}", EditorStyles.miniLabel);
        if (bData.upkeepPerCycle != null && bData.upkeepPerCycle.Count > 0)
        {
            string upkeepSummary = string.Join(", ", bData.upkeepPerCycle.Select(u => $"{u.type}: -{u.amount}"));
            EditorGUILayout.LabelField($"Upkeep na cykl: {upkeepSummary}", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // Nagłówek tabeli
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Prac.", EditorStyles.miniBoldLabel, GUILayout.Width(42));
        GUILayout.Label("Wydajność", EditorStyles.miniBoldLabel, GUILayout.Width(68));
        foreach (var res in prodList)
        {
            GUILayout.Label($"{res.type}/cykl", EditorStyles.miniBoldLabel, GUILayout.MinWidth(70));
        }
        EditorGUILayout.EndHorizontal();

        float globalMult = Mathf.Max(0.1f, 1f + simGlobalMultiplierPercent / 100f);
        var table = bData.efficiencyTable;

        for (int i = 0; i < table.Count; i++)
        {
            float eff = CalculateSimulatedEfficiency(table[i], i, false);
            bool isBaseCap = (i == bData.baseMaxWorkers);
            GUI.backgroundColor = isBaseCap ? new Color(0.3f, 0.7f, 0.4f, 0.35f) : Color.white;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label(i == bData.baseMaxWorkers ? $"{i} ★" : i.ToString(), GUILayout.Width(42));
            GUILayout.Label($"{Mathf.RoundToInt(eff * 100f)}%", GUILayout.Width(68));

            for (int r = 0; r < prodList.Count; r++)
            {
                float baseAmt = prodList[r].amount + (r == 0 ? simTerrainBonusFlat : 0f);
                float rawYield = eff <= 0f ? 0f : baseAmt * globalMult * eff;
                GUILayout.Label($"{rawYield:F2}", EditorStyles.boldLabel, GUILayout.MinWidth(70));
            }

            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.LabelField("★ = Limit bazowy (baseMaxWorkers)", EditorStyles.centeredGreyMiniLabel);
    }

    private float CalculateSimulatedEfficiency(float baseTableVal, int workerCount, bool isTower)
    {
        float eff = baseTableVal;
        if (workerCount <= 0)
        {
            eff += simMetaPassivePercent / 100f;
            if (!isTower && eff <= 0f) return 0f;
            if (isTower) return eff;
        }
        else
        {
            eff += workerCount * (simMetaPerWorkerPercent / 100f);
        }

        if (!simHasSupplies)
        {
            eff = Mathf.Max(0.1f, eff - 0.5f);
        }

        return eff;
    }

    // =========================================================================
    // WIDOK ZBIORCZY (MATRIX / ARKUSZ PORÓWNAWCZY)
    // =========================================================================
    private void DrawMatrixComparisonView(Rect rect)
    {
        GUILayout.BeginArea(rect, EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("📋 Zbiorcza Macierz Wydajności Wszystkich Budynków (Wartości w %)", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("Zielone = Bazowe sloty (1..baseMaxWorkers) | Niebieskie = Bonusowe (>baseMaxWorkers)", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        int maxCols = 7;
        foreach (var b in allBuildings)
        {
            if (b != null && b.efficiencyTable != null && b.efficiencyTable.Count > maxCols)
                maxCols = b.efficiencyTable.Count;
        }

        // Nagłówek kolumn
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Budynek", EditorStyles.miniBoldLabel, GUILayout.Width(175));
        GUILayout.Label("Typ", EditorStyles.miniBoldLabel, GUILayout.Width(75));
        GUILayout.Label("Base Max", EditorStyles.miniBoldLabel, GUILayout.Width(65));
        for (int c = 0; c < maxCols; c++)
        {
            GUILayout.Label(c == 0 ? "0 (Pas.)" : $"{c} prac.", EditorStyles.miniBoldLabel, GUILayout.Width(62));
        }
        GUILayout.Label("Akcje", EditorStyles.miniBoldLabel, GUILayout.Width(95));
        EditorGUILayout.EndHorizontal();

        matrixScroll = EditorGUILayout.BeginScrollView(matrixScroll);

        foreach (var b in allBuildings)
        {
            if (b == null) continue;
            b.EnsureDefaultEfficiencyTable();

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Przycisk wyboru budynku (przechodzi do widoku szczegółowego)
            string displayName = string.IsNullOrEmpty(b.buildingName) ? b.name : b.buildingName;
            if (GUILayout.Button(displayName, EditorStyles.miniButtonLeft, GUILayout.Width(175)))
            {
                SelectBuilding(b);
                currentTab = ViewTab.DetailedEditor;
            }

            string typeStr = (b is TowerData || b.type == BuildingType.Defense) ? "Defense" : b.type.ToString();
            GUILayout.Label(typeStr, EditorStyles.miniLabel, GUILayout.Width(75));

            // Edycja baseMaxWorkers
            EditorGUI.BeginChangeCheck();
            int newBase = EditorGUILayout.IntField(b.baseMaxWorkers, GUILayout.Width(65));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(b, "Zmiana baseMaxWorkers");
                b.baseMaxWorkers = Mathf.Clamp(newBase, 1, 12);
                EnsureTableHasAtLeast(b, b.baseMaxWorkers + 1);
                EditorUtility.SetDirty(b);
            }

            // Komórki 0 .. maxCols - 1
            for (int c = 0; c < maxCols; c++)
            {
                if (c < b.efficiencyTable.Count)
                {
                    Color prevCol = GUI.backgroundColor;
                    if (c == 0) GUI.backgroundColor = new Color(1f, 0.65f, 0.55f);
                    else if (c <= b.baseMaxWorkers) GUI.backgroundColor = new Color(0.6f, 1f, 0.68f);
                    else GUI.backgroundColor = new Color(0.65f, 0.85f, 1f);

                    float pct = Mathf.Round(b.efficiencyTable[c] * 100f);
                    EditorGUI.BeginChangeCheck();
                    float newPct = EditorGUILayout.FloatField(pct, GUILayout.Width(62));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(b, "Edycja komórki wydajności");
                        b.efficiencyTable[c] = Mathf.Max(0f, newPct / 100f);
                        EditorUtility.SetDirty(b);
                    }

                    GUI.backgroundColor = prevCol;
                }
                else
                {
                    GUILayout.Label("-", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(62));
                }
            }

            // Przyciski + / - progów
            if (GUILayout.Button("+Próg", EditorStyles.miniButtonLeft, GUILayout.Width(48)))
            {
                Undo.RecordObject(b, "Dodaj próg");
                float last = b.efficiencyTable.Count > 0 ? b.efficiencyTable[b.efficiencyTable.Count - 1] : 1f;
                b.efficiencyTable.Add(Mathf.Round((last + 0.15f) * 100f) / 100f);
                EditorUtility.SetDirty(b);
            }
            GUI.enabled = b.efficiencyTable.Count > 2;
            if (GUILayout.Button("-Próg", EditorStyles.miniButtonRight, GUILayout.Width(45)))
            {
                Undo.RecordObject(b, "Usuń próg");
                b.efficiencyTable.RemoveAt(b.efficiencyTable.Count - 1);
                EditorUtility.SetDirty(b);
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // =========================================================================
    // METODY POMOCNICZE (WYLICZANIE BONUSÓW Z DRZEWKA I META)
    // =========================================================================
    private void EnsureTableHasAtLeast(BuildingData b, int minCount)
    {
        if (b.efficiencyTable == null) b.efficiencyTable = new List<float>();
        while (b.efficiencyTable.Count < minCount)
        {
            if (b.efficiencyTable.Count == 0) b.efficiencyTable.Add(0f);
            else if (b.efficiencyTable.Count == 1) b.efficiencyTable.Add(1f);
            else
            {
                float last = b.efficiencyTable[b.efficiencyTable.Count - 1];
                float prev = b.efficiencyTable[b.efficiencyTable.Count - 2];
                float delta = Mathf.Max(0.1f, last - prev);
                b.efficiencyTable.Add(Mathf.Round((last + delta) * 100f) / 100f);
            }
        }
    }

    private void ResizeTable(BuildingData b, int targetCount)
    {
        targetCount = Mathf.Max(2, targetCount);
        Undo.RecordObject(b, "Zmiana rozmiaru tabeli wydajności");
        EnsureTableHasAtLeast(b, targetCount);
        while (b.efficiencyTable.Count > targetCount)
        {
            b.efficiencyTable.RemoveAt(b.efficiencyTable.Count - 1);
        }
        EditorUtility.SetDirty(b);
    }

    private int CalculateMaxExtraWorkersFromTree(BuildingData b)
    {
        if (b == null || b.tier1Upgrades == null || b.tier1Upgrades.Count == 0) return 0;
        var visited = new HashSet<BuildingUpgradeSO>();
        int maxBranchExtra = 0;
        foreach (var t1 in b.tier1Upgrades)
        {
            if (t1 == null) continue;
            int branch = GetMaxExtraWorkersRecursive(t1, visited);
            if (branch > maxBranchExtra) maxBranchExtra = branch;
        }
        return maxBranchExtra;
    }

    private int GetMaxExtraWorkersRecursive(BuildingUpgradeSO node, HashSet<BuildingUpgradeSO> visited)
    {
        if (node == null || visited.Contains(node)) return 0;
        visited.Add(node);

        int maxChild = 0;
        if (node.nextTierOptions != null)
        {
            foreach (var child in node.nextTierOptions)
            {
                int c = GetMaxExtraWorkersRecursive(child, new HashSet<BuildingUpgradeSO>(visited));
                if (c > maxChild) maxChild = c;
            }
        }

        return Mathf.Max(0, node.extraWorkersPerShift) + maxChild;
    }

    private int CalculateMaxExtraWorkersFromMeta()
    {
        string[] guids = AssetDatabase.FindAssets("t:MetaUpgradeSO");
        int totalBonus = 0;
        foreach (string guid in guids)
        {
            var meta = AssetDatabase.LoadAssetAtPath<MetaUpgradeSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (meta != null && meta.effectType == MetaEffectType.BonusWorkersPerShift)
            {
                totalBonus += Mathf.RoundToInt(meta.effectValue);
            }
        }
        return totalBonus;
    }

    private void DrawSpritePreview(Rect rect, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null) return;
        Texture2D tex = sprite.texture;
        Rect texCoords = sprite.textureRect;
        texCoords.x /= tex.width;
        texCoords.y /= tex.height;
        texCoords.width /= tex.width;
        texCoords.height /= tex.height;
        GUI.DrawTextureWithTexCoords(rect, tex, texCoords);
    }
}
#endif
