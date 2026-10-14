#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kompleksowe narzędzie edytorskie do balansowania kosztów budowy, kosztów utrzymania (upkeep),
/// produkcji bazowej oraz statystyk dla wszystkich budynków w projekcie.
/// </summary>
public class BuildingBalanceEditorWindow : EditorWindow
{
    private enum MainTab
    {
        SpreadsheetMatrix = 0,
        DetailedInspector = 1
    }

    private enum TableSubView
    {
        ConstructionCost = 0,
        Upkeep = 1,
        Production = 2,
        TerrainBonus = 3,
        ClassStats = 4
    }

    // --- Stan okna ---
    private MainTab currentTab = MainTab.SpreadsheetMatrix;
    private TableSubView currentSubView = TableSubView.ConstructionCost;
    private List<BuildingData> allBuildings = new List<BuildingData>();
    private BuildingData selectedBuilding;

    // --- Filtry i Wyszukiwanie ---
    private string searchFilter = "";
    private int categoryFilterIndex = 0; // 0 = Wszystkie, 1 = Economic, 2 = Defense, 3 = Housing, 4 = Utility, 5 = Unique
    private readonly string[] categoryFilterNames = { "Wszystkie", "Economic", "Defense (Wieże)", "Housing (Domy)", "Utility", "Unique" };
    private bool showOnlyWarnings = false;

    // --- Zaznaczenia wielokrotne (dla operacji masowych) ---
    private readonly HashSet<string> selectedBuildingGuids = new HashSet<string>();

    // --- Pozycje przewijania ---
    private Vector2 matrixScroll;
    private Vector2 leftListScroll;
    private Vector2 rightDetailScroll;

    // --- Narzędzia masowego skalowania ---
    private bool showBatchScaler = false;
    private TableSubView batchTarget = TableSubView.ConstructionCost;
    private int batchResourceIndex = 0; // 0 = Wszystkie surowce, 1..N = konkretny ResourceType
    private float batchMultiplier = 1.10f; // Domyślnie +10%
    private int batchRounding = 1; // 1 = brak zaokrąglenia, 5 = do 5, 10 = do 10

    // --- Schowek (Clipboard) ---
    private static BalanceClipboard clipboard;

    private class BalanceClipboard
    {
        public string sourceName;
        public List<ResourceCost> constructionCost = new List<ResourceCost>();
        public List<ResourceCost> upkeepPerCycle = new List<ResourceCost>();
        public List<ResourceCost> productionPerCycle = new List<ResourceCost>();
        public int baseMaxWorkers;

        // Terrain bonus & Placement
        public List<TerrainBonusRule> bonusRules = new List<TerrainBonusRule>();
        public List<HexFeatureType> allowedTerrain = new List<HexFeatureType>();
        public bool requiresOccupiedSpace;

        // Housing
        public bool isHousing;
        public List<ResourceCost> baseDailyUpkeep = new List<ResourceCost>();
        public List<HousingBuildingData.PerCapitaCost> upkeepPerResident = new List<HousingBuildingData.PerCapitaCost>();
        public List<ResourceCost> productionPerResident = new List<ResourceCost>();
        public int maxResidents;
        public int initialResidents;

        // Tower
        public bool isTower;
        public float baseRange;
        public float baseDamage;
        public float fireRate;
    }

    // --- Zasoby (ResourceType) w kolejności kolumn ---
    private static readonly ResourceType[] MonitoredResources = (ResourceType[])Enum.GetValues(typeof(ResourceType));

    // --- Style IMGUI ---
    private GUIStyle headerBoxStyle;
    private GUIStyle cellHeaderStyle;
    private GUIStyle activeCellStyle;
    private GUIStyle emptyCellStyle;
    private GUIStyle rowEvenStyle;
    private GUIStyle rowOddStyle;
    private GUIStyle warningLabelStyle;
    private bool stylesInitialized = false;

    [MenuItem("Tools/Buildings/Building Balance Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<BuildingBalanceEditorWindow>("Building Balance Editor");
        window.minSize = new Vector2(1100, 600);
        window.Show();
    }

    public static void OpenForBuilding(BuildingData building)
    {
        var window = GetWindow<BuildingBalanceEditorWindow>("Building Balance Editor");
        window.minSize = new Vector2(1100, 600);
        window.RefreshBuildingList();
        if (building != null)
        {
            window.SelectBuilding(building);
            window.currentTab = MainTab.DetailedInspector;
        }
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        RefreshBuildingList();
    }

    private void RefreshBuildingList()
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
        if (b is HousingBuildingData || b.type == BuildingType.Housing) return 2;
        if (b is TowerData || b.type == BuildingType.Defense) return 1;
        switch (b.type)
        {
            case BuildingType.Economic: return 0;
            case BuildingType.Utility: return 3;
            case BuildingType.Unique: return 4;
            default: return 5;
        }
    }

    private void SelectBuilding(BuildingData building)
    {
        selectedBuilding = building;
        GUI.FocusControl(null);
    }

    private void InitStylesIfNeeded()
    {
        if (stylesInitialized && headerBoxStyle != null) return;

        headerBoxStyle = new GUIStyle(EditorStyles.toolbar)
        {
            fixedHeight = 34,
            fontSize = 12,
            fontStyle = FontStyle.Bold
        };

        cellHeaderStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };

        activeCellStyle = new GUIStyle(EditorStyles.numberField)
        {
            alignment = TextAnchor.MiddleRight,
            fontSize = 11,
            fontStyle = FontStyle.Bold
        };

        emptyCellStyle = new GUIStyle(EditorStyles.numberField)
        {
            alignment = TextAnchor.MiddleRight,
            fontSize = 11
        };

        rowEvenStyle = new GUIStyle(GUI.skin.box);
        rowEvenStyle.normal.background = MakeTexture(1, 1, new Color(0.22f, 0.22f, 0.22f, 0.45f));

        rowOddStyle = new GUIStyle(GUI.skin.box);
        rowOddStyle.normal.background = MakeTexture(1, 1, new Color(0.28f, 0.28f, 0.28f, 0.45f));

        warningLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(1f, 0.4f, 0.2f) },
            fontStyle = FontStyle.Bold
        };

        stylesInitialized = true;
    }

    private Texture2D MakeTexture(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    private void OnGUI()
    {
        InitStylesIfNeeded();

        DrawTopBar();

        if (currentTab == MainTab.SpreadsheetMatrix)
        {
            DrawSpreadsheetTab();
        }
        else
        {
            DrawDetailedTab();
        }
    }

    // =========================================================================
    // GÓRNY PASEK NARZĘDZI (TOOLBAR)
    // =========================================================================
    private void DrawTopBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(28));

        // Wybór głównej zakładki
        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = currentTab == MainTab.SpreadsheetMatrix ? new Color(0.4f, 0.85f, 1f) : Color.white;
        if (GUILayout.Button("📊 Tabela Zbiorcza (Arkusz)", EditorStyles.toolbarButton, GUILayout.Width(180), GUILayout.Height(26)))
        {
            currentTab = MainTab.SpreadsheetMatrix;
        }

        GUI.backgroundColor = currentTab == MainTab.DetailedInspector ? new Color(0.4f, 0.85f, 1f) : Color.white;
        if (GUILayout.Button("🔍 Widok Szczegółowy", EditorStyles.toolbarButton, GUILayout.Width(160), GUILayout.Height(26)))
        {
            currentTab = MainTab.DetailedInspector;
        }
        GUI.backgroundColor = prevBg;

        GUILayout.Space(12);

        // Kategoria
        GUILayout.Label("Kategoria:", EditorStyles.miniLabel, GUILayout.Width(58));
        categoryFilterIndex = EditorGUILayout.Popup(categoryFilterIndex, categoryFilterNames, EditorStyles.toolbarDropDown, GUILayout.Width(130));

        GUILayout.Space(8);

        // Wyszukiwarka
        GUILayout.Label("Szukaj:", EditorStyles.miniLabel, GUILayout.Width(42));
        searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(150));
        if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(22)))
        {
            searchFilter = "";
            GUI.FocusControl(null);
        }

        GUILayout.Space(10);

        // Filtr ostrzeżeń
        GUI.backgroundColor = showOnlyWarnings ? new Color(1f, 0.6f, 0.3f) : Color.white;
        showOnlyWarnings = GUILayout.Toggle(showOnlyWarnings, "⚠️ Tylko z ostrzeżeniami", EditorStyles.toolbarButton);
        GUI.backgroundColor = prevBg;

        GUILayout.FlexibleSpace();

        // Licznik niezapisanych zmian
        int dirtyCount = allBuildings.Count(b => b != null && EditorUtility.IsDirty(b));
        if (dirtyCount > 0)
        {
            GUI.backgroundColor = new Color(1f, 0.85f, 0.2f);
            GUILayout.Label($"⚡ Niezapisane: {dirtyCount}", EditorStyles.toolbarButton);
            GUI.backgroundColor = prevBg;
        }

        // Zapisz zmiany
        GUI.backgroundColor = new Color(0.35f, 0.85f, 0.45f);
        if (GUILayout.Button("💾 Zapisz zmiany", EditorStyles.toolbarButton, GUILayout.Width(110)))
        {
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("Pomyślnie zapisano wszystkie assety!"));
        }
        GUI.backgroundColor = prevBg;

        // Odśwież
        if (GUILayout.Button("🔄 Odśwież", EditorStyles.toolbarButton, GUILayout.Width(75)))
        {
            RefreshBuildingList();
        }

        // Eksport / Import CSV
        if (GUILayout.Button("📤 Eksport CSV", EditorStyles.toolbarButton, GUILayout.Width(90)))
        {
            ExportToCsv();
        }
        if (GUILayout.Button("📥 Import CSV", EditorStyles.toolbarButton, GUILayout.Width(85)))
        {
            ImportFromCsv();
        }

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================================
    // ZAKŁADKA 1: TABELA ZBIORCZA (ARKUSZ KALKULACYJNY)
    // =========================================================================
    private void DrawSpreadsheetTab()
    {
        // Pasek wyboru podwidoku tabeli oraz rozwijania masowego skalowania
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        GUILayout.Label("Podwidok danych:", EditorStyles.boldLabel, GUILayout.Width(120));

        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = currentSubView == TableSubView.ConstructionCost ? new Color(0.45f, 0.85f, 1f) : Color.white;
        if (GUILayout.Button("🏗️ Koszty Budowy", EditorStyles.miniButtonLeft, GUILayout.Height(24), GUILayout.Width(140)))
        {
            currentSubView = TableSubView.ConstructionCost;
        }

        GUI.backgroundColor = currentSubView == TableSubView.Upkeep ? new Color(0.45f, 0.85f, 1f) : Color.white;
        if (GUILayout.Button("⚙️ Koszty Utrzymania (Upkeep)", EditorStyles.miniButtonMid, GUILayout.Height(24), GUILayout.Width(190)))
        {
            currentSubView = TableSubView.Upkeep;
        }

        GUI.backgroundColor = currentSubView == TableSubView.Production ? new Color(0.45f, 0.85f, 1f) : Color.white;
        if (GUILayout.Button("🌾 Produkcja Bazowa", EditorStyles.miniButtonMid, GUILayout.Height(24), GUILayout.Width(150)))
        {
            currentSubView = TableSubView.Production;
        }

        GUI.backgroundColor = currentSubView == TableSubView.TerrainBonus ? new Color(0.45f, 0.85f, 1f) : Color.white;
        if (GUILayout.Button("🌲 Bonusy Terenowe", EditorStyles.miniButtonMid, GUILayout.Height(24), GUILayout.Width(150)))
        {
            currentSubView = TableSubView.TerrainBonus;
        }

        GUI.backgroundColor = currentSubView == TableSubView.ClassStats ? new Color(0.45f, 0.85f, 1f) : Color.white;
        if (GUILayout.Button("⚔️ / 👥 Statystyki Klasowe", EditorStyles.miniButtonRight, GUILayout.Height(24), GUILayout.Width(170)))
        {
            currentSubView = TableSubView.ClassStats;
        }
        GUI.backgroundColor = prevBg;

        GUILayout.FlexibleSpace();

        // Przycisk rozwijający panel skalowania hurtowego
        GUI.backgroundColor = showBatchScaler ? new Color(1f, 0.8f, 0.3f) : Color.white;
        if (GUILayout.Button(showBatchScaler ? "▲ Ukryj Skalowanie Hurtowe" : "⚡ Narzędzia Masowe (Batch Scaler)", GUILayout.Height(24)))
        {
            showBatchScaler = !showBatchScaler;
        }
        GUI.backgroundColor = prevBg;

        EditorGUILayout.EndHorizontal();

        if (showBatchScaler)
        {
            DrawBatchScalerPanel();
        }

        // Tabela danych
        var filteredList = GetFilteredBuildings();

        if (filteredList.Count == 0)
        {
            EditorGUILayout.HelpBox("Brak budynków spełniających wybrane kryteria wyszukiwania.", MessageType.Info);
            return;
        }

        matrixScroll = EditorGUILayout.BeginScrollView(matrixScroll);

        if (currentSubView == TableSubView.TerrainBonus)
        {
            DrawTerrainBonusTable(filteredList);
        }
        else if (currentSubView == TableSubView.ClassStats)
        {
            DrawClassStatsTable(filteredList);
        }
        else
        {
            DrawResourceCostTable(filteredList, currentSubView);
        }

        EditorGUILayout.EndScrollView();

        DrawTableFooter(filteredList);
    }

    // =========================================================================
    // PANEL MASOWEGO SKALOWANIA (BATCH SCALER)
    // =========================================================================
    private void DrawBatchScalerPanel()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("⚡ Masowe Skalowanie Wartości dla Zaznaczonych lub Przefiltrowanych Budynków", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        GUILayout.Label("Cel:", GUILayout.Width(35));
        batchTarget = (TableSubView)EditorGUILayout.EnumPopup(batchTarget, GUILayout.Width(130));

        GUILayout.Space(10);
        GUILayout.Label("Surowiec:", GUILayout.Width(65));
        string[] resOptions = new string[MonitoredResources.Length + 1];
        resOptions[0] = "Wszystkie Surowce";
        for (int i = 0; i < MonitoredResources.Length; i++) resOptions[i + 1] = MonitoredResources[i].ToString();
        batchResourceIndex = EditorGUILayout.Popup(batchResourceIndex, resOptions, GUILayout.Width(140));

        GUILayout.Space(10);
        GUILayout.Label("Mnożnik:", GUILayout.Width(55));
        batchMultiplier = EditorGUILayout.FloatField(batchMultiplier, GUILayout.Width(60));

        // Szybkie presety mnożników
        if (GUILayout.Button("-20%", EditorStyles.miniButton, GUILayout.Width(45))) batchMultiplier = 0.80f;
        if (GUILayout.Button("-10%", EditorStyles.miniButton, GUILayout.Width(45))) batchMultiplier = 0.90f;
        if (GUILayout.Button("+10%", EditorStyles.miniButton, GUILayout.Width(45))) batchMultiplier = 1.10f;
        if (GUILayout.Button("+25%", EditorStyles.miniButton, GUILayout.Width(45))) batchMultiplier = 1.25f;
        if (GUILayout.Button("+50%", EditorStyles.miniButton, GUILayout.Width(45))) batchMultiplier = 1.50f;
        if (GUILayout.Button("x2", EditorStyles.miniButton, GUILayout.Width(35))) batchMultiplier = 2.00f;

        GUILayout.Space(10);
        GUILayout.Label("Zaokrąglanie:", GUILayout.Width(80));
        string[] roundOptions = { "Brak (1)", "Do 5", "Do 10" };
        int[] roundValues = { 1, 5, 10 };
        int roundIdx = batchRounding == 10 ? 2 : (batchRounding == 5 ? 1 : 0);
        roundIdx = EditorGUILayout.Popup(roundIdx, roundOptions, GUILayout.Width(80));
        batchRounding = roundValues[roundIdx];

        GUILayout.FlexibleSpace();

        int checkedCount = selectedBuildingGuids.Count;
        string applyBtnText = checkedCount > 0 ? $"⚡ Zastosuj do zaznaczonych ({checkedCount})" : "⚡ Zastosuj do wszystkich widocznych";

        Color prev = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.8f, 1f);
        if (GUILayout.Button(applyBtnText, GUILayout.Height(24), GUILayout.Width(240)))
        {
            ApplyBatchScaling(checkedCount > 0);
        }
        GUI.backgroundColor = prev;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
    }

    private void ApplyBatchScaling(bool onlyChecked)
    {
        var targetList = GetFilteredBuildings();
        if (onlyChecked)
        {
            targetList = targetList.Where(b => selectedBuildingGuids.Contains(GetGuid(b))).ToList();
        }

        if (targetList.Count == 0)
        {
            ShowNotification(new GUIContent("Brak budynków do zmodyfikowania!"));
            return;
        }

        int modifiedBuildings = 0;
        int modifiedValues = 0;

        foreach (var b in targetList)
        {
            if (b == null) continue;

            Undo.RecordObject(b, "Batch Scale Balance");
            bool changed = false;

            List<ResourceCost> targetCostList = null;
            if (batchTarget == TableSubView.ConstructionCost)
            {
                targetCostList = b.constructionCost;
            }
            else if (batchTarget == TableSubView.Upkeep)
            {
                if (b is HousingBuildingData hd)
                {
                    targetCostList = hd.baseDailyUpkeep;
                }
                else
                {
                    targetCostList = b.upkeepPerCycle;
                }
            }
            else if (batchTarget == TableSubView.Production)
            {
                targetCostList = b.productionPerCycle;
            }

            if (targetCostList != null && targetCostList.Count > 0)
            {
                for (int i = 0; i < targetCostList.Count; i++)
                {
                    var item = targetCostList[i];
                    if (batchResourceIndex == 0 || item.type == MonitoredResources[batchResourceIndex - 1])
                    {
                        float newVal = item.amount * batchMultiplier;
                        int finalVal = RoundValue(newVal, batchRounding);
                        if (finalVal != item.amount)
                        {
                            item.amount = Mathf.Max(1, finalVal);
                            targetCostList[i] = item;
                            changed = true;
                            modifiedValues++;
                        }
                    }
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(b);
                modifiedBuildings++;
            }
        }

        ShowNotification(new GUIContent($"Zaktualizowano {modifiedValues} wartości w {modifiedBuildings} budynkach!"));
    }

    private int RoundValue(float val, int roundStep)
    {
        if (roundStep <= 1) return Mathf.RoundToInt(val);
        return Mathf.RoundToInt(val / roundStep) * roundStep;
    }

    // =========================================================================
    // RYSOWANIE TABELI SUROWCÓW (KOSZTY, UPKEEP, PRODUKCJA)
    // =========================================================================
    private void DrawResourceCostTable(List<BuildingData> buildings, TableSubView subView)
    {
        float checkWidth = 28f;
        float iconWidth = 34f;
        float nameWidth = 200f;
        float typeWidth = 105f;
        float colWidth = 68f;
        float totalWidth = 75f;
        float actionWidth = 90f;

        // Nagłówek tabeli
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Zaznacz wszystkie checkbox
        bool allChecked = buildings.Count > 0 && buildings.All(b => selectedBuildingGuids.Contains(GetGuid(b)));
        bool toggleAll = EditorGUILayout.Toggle(allChecked, GUILayout.Width(checkWidth));
        if (toggleAll != allChecked)
        {
            if (toggleAll)
            {
                foreach (var b in buildings) selectedBuildingGuids.Add(GetGuid(b));
            }
            else
            {
                selectedBuildingGuids.Clear();
            }
        }

        GUILayout.Label("Ikona", cellHeaderStyle, GUILayout.Width(iconWidth));
        GUILayout.Label("Nazwa Budynku", EditorStyles.miniBoldLabel, GUILayout.Width(nameWidth));
        GUILayout.Label("Kategoria", cellHeaderStyle, GUILayout.Width(typeWidth));

        // Kolumny surowców
        for (int i = 0; i < MonitoredResources.Length; i++)
        {
            ResourceType res = MonitoredResources[i];
            GUILayout.Label(GetResourceDisplayName(res), cellHeaderStyle, GUILayout.Width(colWidth));
        }

        GUILayout.Label("Suma", cellHeaderStyle, GUILayout.Width(totalWidth));
        GUILayout.Label("Akcje", cellHeaderStyle, GUILayout.Width(actionWidth));

        EditorGUILayout.EndHorizontal();

        // Wiersze budynków
        for (int rowIdx = 0; rowIdx < buildings.Count; rowIdx++)
        {
            BuildingData b = buildings[rowIdx];
            if (b == null) continue;

            string guid = GetGuid(b);
            bool isRowSelected = selectedBuildingGuids.Contains(guid);

            GUIStyle rowStyle = (rowIdx % 2 == 0) ? rowEvenStyle : rowOddStyle;
            EditorGUILayout.BeginHorizontal(rowStyle, GUILayout.Height(26));

            // Checkbox
            bool isChecked = EditorGUILayout.Toggle(isRowSelected, GUILayout.Width(checkWidth));
            if (isChecked != isRowSelected)
            {
                if (isChecked) selectedBuildingGuids.Add(guid);
                else selectedBuildingGuids.Remove(guid);
            }

            // Miniatura ikony
            if (b.icon != null)
            {
                Rect iconRect = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(iconWidth), GUILayout.Height(24));
                GUI.DrawTexture(new Rect(iconRect.x + 4, iconRect.y, 24, 24), b.icon.texture, ScaleMode.ScaleToFit);
            }
            else
            {
                GUILayout.Label("—", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(iconWidth));
            }

            // Nazwa budynku + ewentualny znaczek ostrzeżenia
            string displayName = string.IsNullOrEmpty(b.buildingName) ? b.name : b.buildingName;
            string warningBadge = GetWarningBadge(b);
            if (!string.IsNullOrEmpty(warningBadge))
            {
                displayName += " " + warningBadge;
            }

            if (GUILayout.Button(displayName, EditorStyles.label, GUILayout.Width(nameWidth), GUILayout.Height(22)))
            {
                SelectBuilding(b);
                currentTab = MainTab.DetailedInspector;
            }

            // Kategoria
            GUILayout.Label(GetCategoryBadge(b), EditorStyles.miniLabel, GUILayout.Width(typeWidth));

            // Pobranie odpowiedniej listy kosztów w zależności od podwidoku
            List<ResourceCost> costList = GetResourceCostListForView(b, subView);

            int totalRowSum = 0;

            // Kolumny surowców
            for (int colIdx = 0; colIdx < MonitoredResources.Length; colIdx++)
            {
                ResourceType res = MonitoredResources[colIdx];
                int currentAmount = GetCostAmount(costList, res);
                totalRowSum += currentAmount;

                Color prevColor = GUI.backgroundColor;
                if (currentAmount > 0)
                {
                    GUI.backgroundColor = GetResourceTint(res);
                }
                else
                {
                    GUI.backgroundColor = new Color(0.85f, 0.85f, 0.85f, 0.4f);
                }

                int newAmount = EditorGUILayout.IntField(currentAmount, currentAmount > 0 ? activeCellStyle : emptyCellStyle, GUILayout.Width(colWidth));
                GUI.backgroundColor = prevColor;

                if (newAmount < 0) newAmount = 0;

                if (newAmount != currentAmount)
                {
                    Undo.RecordObject(b, $"Zmień {subView} {res} dla {b.buildingName}");
                    SetCostAmount(ref costList, res, newAmount);
                    SetResourceCostListForView(b, subView, costList);
                    EditorUtility.SetDirty(b);
                }
            }

            // Suma wiersza
            Color prevSumColor = GUI.color;
            if (totalRowSum == 0 && subView == TableSubView.ConstructionCost)
            {
                GUI.color = new Color(1f, 0.45f, 0.35f);
            }
            GUILayout.Label(totalRowSum.ToString(), cellHeaderStyle, GUILayout.Width(totalWidth));
            GUI.color = prevSumColor;

            // Akcje (Szczegóły / Kopiuj)
            if (GUILayout.Button("🔍", EditorStyles.miniButtonLeft, GUILayout.Width(36), GUILayout.Height(20)))
            {
                SelectBuilding(b);
                currentTab = MainTab.DetailedInspector;
            }
            if (GUILayout.Button("📋", EditorStyles.miniButtonRight, GUILayout.Width(36), GUILayout.Height(20)))
            {
                CopyBuildingProfile(b);
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    // =========================================================================
    // RYSOWANIE TABELI STATYSTYK KLASOWYCH (STATYSTYKI BOJOWE / DOMY / PRACOWNICY)
    // =========================================================================
    private void DrawClassStatsTable(List<BuildingData> buildings)
    {
        float checkWidth = 28f;
        float iconWidth = 34f;
        float nameWidth = 180f;
        float typeWidth = 100f;

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Space(checkWidth + iconWidth);
        GUILayout.Label("Nazwa Budynku", EditorStyles.miniBoldLabel, GUILayout.Width(nameWidth));
        GUILayout.Label("Kategoria", cellHeaderStyle, GUILayout.Width(typeWidth));
        GUILayout.Label("Pracownicy Baza", cellHeaderStyle, GUILayout.Width(110));
        GUILayout.Label("Mieszkańcy (Max)", cellHeaderStyle, GUILayout.Width(110));
        GUILayout.Label("Start Pop", cellHeaderStyle, GUILayout.Width(80));
        GUILayout.Label("Zasięg (Wieża)", cellHeaderStyle, GUILayout.Width(95));
        GUILayout.Label("Obrażenia (Dmg)", cellHeaderStyle, GUILayout.Width(105));
        GUILayout.Label("Szybkostrzelność", cellHeaderStyle, GUILayout.Width(110));
        GUILayout.Label("DPS Baza", cellHeaderStyle, GUILayout.Width(85));
        GUILayout.Label("Typ Dmg", cellHeaderStyle, GUILayout.Width(90));
        EditorGUILayout.EndHorizontal();

        for (int rowIdx = 0; rowIdx < buildings.Count; rowIdx++)
        {
            BuildingData b = buildings[rowIdx];
            if (b == null) continue;

            GUIStyle rowStyle = (rowIdx % 2 == 0) ? rowEvenStyle : rowOddStyle;
            EditorGUILayout.BeginHorizontal(rowStyle, GUILayout.Height(26));

            GUILayout.Space(checkWidth);

            if (b.icon != null)
            {
                Rect iconRect = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(iconWidth), GUILayout.Height(24));
                GUI.DrawTexture(new Rect(iconRect.x + 4, iconRect.y, 24, 24), b.icon.texture, ScaleMode.ScaleToFit);
            }
            else
            {
                GUILayout.Label("—", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(iconWidth));
            }

            string displayName = string.IsNullOrEmpty(b.buildingName) ? b.name : b.buildingName;
            if (GUILayout.Button(displayName, EditorStyles.label, GUILayout.Width(nameWidth), GUILayout.Height(22)))
            {
                SelectBuilding(b);
                currentTab = MainTab.DetailedInspector;
            }

            GUILayout.Label(GetCategoryBadge(b), EditorStyles.miniLabel, GUILayout.Width(typeWidth));

            // Pracownicy Baza (tylko budynki niebędące housingiem)
            if (!(b is HousingBuildingData) && b.type != BuildingType.Housing)
            {
                int newWorkers = EditorGUILayout.IntField(b.baseMaxWorkers, activeCellStyle, GUILayout.Width(110));
                if (newWorkers != b.baseMaxWorkers)
                {
                    Undo.RecordObject(b, "Zmień baseMaxWorkers");
                    b.baseMaxWorkers = Mathf.Max(0, newWorkers);
                    EditorUtility.SetDirty(b);
                }
            }
            else
            {
                GUILayout.Label("—", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(110));
            }

            // Statystyki Housing
            if (b is HousingBuildingData hd)
            {
                int newMaxRes = EditorGUILayout.IntField(hd.maxResidents, activeCellStyle, GUILayout.Width(110));
                if (newMaxRes != hd.maxResidents)
                {
                    Undo.RecordObject(hd, "Zmień maxResidents");
                    hd.maxResidents = Mathf.Max(1, newMaxRes);
                    EditorUtility.SetDirty(hd);
                }

                int newInitRes = EditorGUILayout.IntField(hd.initialResidents, activeCellStyle, GUILayout.Width(80));
                if (newInitRes != hd.initialResidents)
                {
                    Undo.RecordObject(hd, "Zmień initialResidents");
                    hd.initialResidents = Mathf.Clamp(newInitRes, 0, hd.maxResidents);
                    EditorUtility.SetDirty(hd);
                }
            }
            else
            {
                GUILayout.Label("—", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(110));
                GUILayout.Label("—", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(80));
            }

            // Statystyki Bojowe Wieży
            if (b is TowerData td)
            {
                float newRange = EditorGUILayout.FloatField(td.baseRange, activeCellStyle, GUILayout.Width(95));
                if (Mathf.Abs(newRange - td.baseRange) > 0.001f)
                {
                    Undo.RecordObject(td, "Zmień baseRange");
                    td.baseRange = Mathf.Max(0f, newRange);
                    EditorUtility.SetDirty(td);
                }

                float newDmg = EditorGUILayout.FloatField(td.baseDamage, activeCellStyle, GUILayout.Width(105));
                if (Mathf.Abs(newDmg - td.baseDamage) > 0.001f)
                {
                    Undo.RecordObject(td, "Zmień baseDamage");
                    td.baseDamage = Mathf.Max(0f, newDmg);
                    EditorUtility.SetDirty(td);
                }

                float newRate = EditorGUILayout.FloatField(td.fireRate, activeCellStyle, GUILayout.Width(110));
                if (Mathf.Abs(newRate - td.fireRate) > 0.001f)
                {
                    Undo.RecordObject(td, "Zmień fireRate");
                    td.fireRate = Mathf.Max(0.01f, newRate);
                    EditorUtility.SetDirty(td);
                }

                float dps = td.baseDamage * td.fireRate;
                GUILayout.Label($"{dps:F1}", cellHeaderStyle, GUILayout.Width(85));

                DamageType newDmgType = (DamageType)EditorGUILayout.EnumPopup(td.damageType, GUILayout.Width(90));
                if (newDmgType != td.damageType)
                {
                    Undo.RecordObject(td, "Zmień damageType");
                    td.damageType = newDmgType;
                    EditorUtility.SetDirty(td);
                }
            }
            else
            {
                GUILayout.Label("—", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(95));
                GUILayout.Label("—", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(105));
                GUILayout.Label("—", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(110));
                GUILayout.Label("—", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(85));
                GUILayout.Label("—", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(90));
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    // =========================================================================
    // RYSOWANIE TABELI BONUSÓW TERENOWYCH I WYMAGAŃ PODŁOŻA
    // =========================================================================
    private void DrawTerrainBonusTable(List<BuildingData> buildings)
    {
        float checkWidth = 28f;
        float iconWidth = 34f;
        float nameWidth = 180f;
        float typeWidth = 90f;
        float featureWidth = 125f;
        float radiusWidth = 70f;
        float bonusWidth = 85f;
        float penaltyWidth = 80f;
        float minBonusWidth = 75f;
        float onTopWidth = 85f;
        float allowedWidth = 160f;
        float actionWidth = 80f;

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Space(checkWidth + iconWidth);
        GUILayout.Label("Nazwa Budynku", EditorStyles.miniBoldLabel, GUILayout.Width(nameWidth));
        GUILayout.Label("Kategoria", cellHeaderStyle, GUILayout.Width(typeWidth));
        GUILayout.Label("Wymagany Teren", cellHeaderStyle, GUILayout.Width(featureWidth));
        GUILayout.Label("Promień", cellHeaderStyle, GUILayout.Width(radiusWidth));
        GUILayout.Label("Bonus/Heks", cellHeaderStyle, GUILayout.Width(bonusWidth));
        GUILayout.Label("Kara/User", cellHeaderStyle, GUILayout.Width(penaltyWidth));
        GUILayout.Label("Min Bonus", cellHeaderStyle, GUILayout.Width(minBonusWidth));
        GUILayout.Label("On-Top", cellHeaderStyle, GUILayout.Width(onTopWidth));
        GUILayout.Label("Dozwolone Podłoże", cellHeaderStyle, GUILayout.Width(allowedWidth));
        GUILayout.Label("Akcje", cellHeaderStyle, GUILayout.Width(actionWidth));
        EditorGUILayout.EndHorizontal();

        for (int rowIdx = 0; rowIdx < buildings.Count; rowIdx++)
        {
            BuildingData b = buildings[rowIdx];
            if (b == null) continue;

            GUIStyle rowStyle = (rowIdx % 2 == 0) ? rowEvenStyle : rowOddStyle;
            EditorGUILayout.BeginHorizontal(rowStyle, GUILayout.Height(26));

            GUILayout.Space(checkWidth);

            if (b.icon != null)
            {
                Rect iconRect = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(iconWidth), GUILayout.Height(24));
                GUI.DrawTexture(new Rect(iconRect.x + 4, iconRect.y, 24, 24), b.icon.texture, ScaleMode.ScaleToFit);
            }
            else
            {
                GUILayout.Label("—", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(iconWidth));
            }

            string displayName = string.IsNullOrEmpty(b.buildingName) ? b.name : b.buildingName;
            if (GUILayout.Button(displayName, EditorStyles.label, GUILayout.Width(nameWidth), GUILayout.Height(22)))
            {
                SelectBuilding(b);
                currentTab = MainTab.DetailedInspector;
            }

            GUILayout.Label(GetCategoryBadge(b), EditorStyles.miniLabel, GUILayout.Width(typeWidth));

            // Wymagany Teren (HexFeatureType)
            if (b.bonusRules == null) b.bonusRules = new List<TerrainBonusRule>();
            TerrainBonusRule primaryRule = b.bonusRules.Count > 0 ? b.bonusRules[0] : null;

            Color prevBg = GUI.backgroundColor;
            bool hasTerrainBonus = primaryRule != null && primaryRule.requiredFeature != HexFeatureType.None;
            if (hasTerrainBonus) GUI.backgroundColor = new Color(0.6f, 0.95f, 0.65f);
            HexFeatureType curFeat = primaryRule != null ? primaryRule.requiredFeature : HexFeatureType.None;
            HexFeatureType newFeat = (HexFeatureType)EditorGUILayout.EnumPopup(curFeat, GUILayout.Width(featureWidth));
            GUI.backgroundColor = prevBg;

            if (newFeat != curFeat)
            {
                Undo.RecordObject(b, "Zmień requiredFeature");
                if (newFeat != HexFeatureType.None)
                {
                    if (primaryRule == null)
                    {
                        primaryRule = new TerrainBonusRule { searchRadius = 1, resourceYields = new List<TerrainResourceYield>() };
                        b.bonusRules.Add(primaryRule);
                    }
                    primaryRule.requiredFeature = newFeat;
                    if (primaryRule.resourceYields == null) primaryRule.resourceYields = new List<TerrainResourceYield>();
                    if (primaryRule.resourceYields.Count == 0)
                    {
                        ResourceType targetRes = (b.productionPerCycle != null && b.productionPerCycle.Count > 0) ? b.productionPerCycle[0].type : ResourceType.Wood;
                        primaryRule.resourceYields.Add(new TerrainResourceYield { resourceType = targetRes, baseBonusPerHex = 1f });
                    }
                }
                else
                {
                    if (primaryRule != null)
                    {
                        b.bonusRules.Remove(primaryRule);
                        primaryRule = null;
                    }
                }
                EditorUtility.SetDirty(b);
            }

            // Promień poszukiwania
            int curRadius = primaryRule != null ? primaryRule.searchRadius : 1;
            int newRadius = EditorGUILayout.IntField(curRadius, activeCellStyle, GUILayout.Width(radiusWidth));
            if (primaryRule != null && newRadius != primaryRule.searchRadius)
            {
                Undo.RecordObject(b, "Zmień searchRadius");
                primaryRule.searchRadius = Mathf.Max(1, newRadius);
                EditorUtility.SetDirty(b);
            }

            // Bazowy bonus per heks
            float curBase = (primaryRule != null && primaryRule.resourceYields != null && primaryRule.resourceYields.Count > 0) ? primaryRule.resourceYields[0].baseBonusPerHex : 0f;
            float newBaseBonus = EditorGUILayout.FloatField(curBase, activeCellStyle, GUILayout.Width(bonusWidth));
            if (primaryRule != null && primaryRule.resourceYields != null && primaryRule.resourceYields.Count > 0 && Mathf.Abs(newBaseBonus - curBase) > 0.001f)
            {
                Undo.RecordObject(b, "Zmień baseBonusPerHex");
                var y = primaryRule.resourceYields[0];
                y.baseBonusPerHex = newBaseBonus;
                primaryRule.resourceYields[0] = y;
                EditorUtility.SetDirty(b);
            }

            // Kara za użytkownika
            float curPenalty = (primaryRule != null && primaryRule.resourceYields != null && primaryRule.resourceYields.Count > 0) ? primaryRule.resourceYields[0].penaltyPerUser : 0f;
            float newPenalty = EditorGUILayout.FloatField(curPenalty, activeCellStyle, GUILayout.Width(penaltyWidth));
            if (primaryRule != null && primaryRule.resourceYields != null && primaryRule.resourceYields.Count > 0 && Mathf.Abs(newPenalty - curPenalty) > 0.001f)
            {
                Undo.RecordObject(b, "Zmień penaltyPerUser");
                var y = primaryRule.resourceYields[0];
                y.penaltyPerUser = newPenalty;
                primaryRule.resourceYields[0] = y;
                EditorUtility.SetDirty(b);
            }

            // Min bonus
            float curMin = (primaryRule != null && primaryRule.resourceYields != null && primaryRule.resourceYields.Count > 0) ? primaryRule.resourceYields[0].minBonus : 0f;
            float newMin = EditorGUILayout.FloatField(curMin, activeCellStyle, GUILayout.Width(minBonusWidth));
            if (primaryRule != null && primaryRule.resourceYields != null && primaryRule.resourceYields.Count > 0 && Mathf.Abs(newMin - curMin) > 0.001f)
            {
                Undo.RecordObject(b, "Zmień minBonus");
                var y = primaryRule.resourceYields[0];
                y.minBonus = newMin;
                primaryRule.resourceYields[0] = y;
                EditorUtility.SetDirty(b);
            }

            // On-Top bonus
            float curOnTop = (primaryRule != null && primaryRule.resourceYields != null && primaryRule.resourceYields.Count > 0) ? primaryRule.resourceYields[0].onTopProductionBonus : 0f;
            float newOnTop = EditorGUILayout.FloatField(curOnTop, activeCellStyle, GUILayout.Width(onTopWidth));
            if (primaryRule != null && primaryRule.resourceYields != null && primaryRule.resourceYields.Count > 0 && Mathf.Abs(newOnTop - curOnTop) > 0.001f)
            {
                Undo.RecordObject(b, "Zmień onTopProductionBonus");
                var y = primaryRule.resourceYields[0];
                y.onTopProductionBonus = newOnTop;
                primaryRule.resourceYields[0] = y;
                EditorUtility.SetDirty(b);
            }

            // Dozwolone heksy do postawienia (Allowed Terrain)
            string allowedSummary = (b.allowedTerrain == null || b.allowedTerrain.Count == 0)
                ? "Dowolny (0)"
                : $"{string.Join(",", b.allowedTerrain.Select(t => t.ToString().Substring(0, Math.Min(3, t.ToString().Length))))} ({b.allowedTerrain.Count})";

            if (GUILayout.Button(allowedSummary, EditorStyles.miniButton, GUILayout.Width(allowedWidth), GUILayout.Height(20)))
            {
                ShowAllowedTerrainMenu(b);
            }

            // Akcje
            if (GUILayout.Button("🔍", EditorStyles.miniButtonLeft, GUILayout.Width(36), GUILayout.Height(20)))
            {
                SelectBuilding(b);
                currentTab = MainTab.DetailedInspector;
            }
            if (GUILayout.Button("📋", EditorStyles.miniButtonRight, GUILayout.Width(36), GUILayout.Height(20)))
            {
                CopyBuildingProfile(b);
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void ShowAllowedTerrainMenu(BuildingData b)
    {
        GenericMenu menu = new GenericMenu();
        HexFeatureType[] allFeatures = (HexFeatureType[])Enum.GetValues(typeof(HexFeatureType));

        if (b.allowedTerrain == null) b.allowedTerrain = new List<HexFeatureType>();

        foreach (var feat in allFeatures)
        {
            bool isAllowed = b.allowedTerrain.Contains(feat);
            menu.AddItem(new GUIContent(feat.ToString()), isAllowed, () =>
            {
                Undo.RecordObject(b, $"Przełącz dozwolony teren {feat}");
                if (b.allowedTerrain.Contains(feat))
                {
                    b.allowedTerrain.Remove(feat);
                }
                else
                {
                    b.allowedTerrain.Add(feat);
                }
                EditorUtility.SetDirty(b);
            });
        }

        menu.ShowAsContext();
    }

    private void DrawTableFooter(List<BuildingData> buildings)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label($"Budynków w widoku: {buildings.Count} z {allBuildings.Count}", EditorStyles.miniBoldLabel);
        GUILayout.FlexibleSpace();

        if (clipboard != null)
        {
            GUILayout.Label($"📋 Schowek: {clipboard.sourceName}", EditorStyles.miniLabel);
            if (GUILayout.Button("Wklej profil do zaznaczonych", EditorStyles.miniButton, GUILayout.Width(180)))
            {
                PasteClipboardToSelected();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    // =========================================================================
    // ZAKŁADKA 2: WIDOK SZCZEGÓŁOWY (MASTER-DETAIL INSPECTOR)
    // =========================================================================
    private void DrawDetailedTab()
    {
        EditorGUILayout.BeginHorizontal();

        // LEWY PANEL: Lista budynków
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(270));
        DrawDetailedLeftList();
        EditorGUILayout.EndVertical();

        // PRAWY PANEL: Inspektor wybranego budynku
        EditorGUILayout.BeginVertical();
        rightDetailScroll = EditorGUILayout.BeginScrollView(rightDetailScroll);
        DrawDetailedInspector();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawDetailedLeftList()
    {
        GUILayout.Label("Lista Budynków", EditorStyles.boldLabel);
        var filteredList = GetFilteredBuildings();

        leftListScroll = EditorGUILayout.BeginScrollView(leftListScroll);

        foreach (var b in filteredList)
        {
            if (b == null) continue;

            bool isSelected = (b == selectedBuilding);
            Color prevBg = GUI.backgroundColor;
            if (isSelected) GUI.backgroundColor = new Color(0.45f, 0.85f, 1f);

            EditorGUILayout.BeginHorizontal(isSelected ? EditorStyles.selectionRect : EditorStyles.helpBox, GUILayout.Height(30));

            if (b.icon != null)
            {
                Rect r = GUILayoutUtility.GetRect(24, 24, GUILayout.Width(28), GUILayout.Height(24));
                GUI.DrawTexture(new Rect(r.x + 2, r.y, 24, 24), b.icon.texture, ScaleMode.ScaleToFit);
            }
            else
            {
                GUILayout.Label("🏢", GUILayout.Width(28));
            }

            string nameStr = string.IsNullOrEmpty(b.buildingName) ? b.name : b.buildingName;
            string badge = GetWarningBadge(b);
            if (!string.IsNullOrEmpty(badge)) nameStr += " " + badge;

            if (GUILayout.Button(nameStr, EditorStyles.label, GUILayout.Height(26)))
            {
                SelectBuilding(b);
            }

            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = prevBg;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawDetailedInspector()
    {
        if (selectedBuilding == null)
        {
            EditorGUILayout.HelpBox("Wybierz budynek z listy po lewej stronie, aby przejść do szczegółowej edycji.", MessageType.Info);
            return;
        }

        BuildingData b = selectedBuilding;

        // Karta nagłówka
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();

        // Ikona preview
        if (b.icon != null)
        {
            Rect iconRect = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(68), GUILayout.Height(68));
            GUI.DrawTexture(new Rect(iconRect.x + 2, iconRect.y + 2, 64, 64), b.icon.texture, ScaleMode.ScaleToFit);
        }
        else
        {
            GUILayout.Box("Brak\nIkony", GUILayout.Width(68), GUILayout.Height(68));
        }

        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        string newName = EditorGUILayout.TextField(b.buildingName, EditorStyles.boldLabel, GUILayout.Height(22));
        if (newName != b.buildingName)
        {
            Undo.RecordObject(b, "Zmień buildingName");
            b.buildingName = newName;
            EditorUtility.SetDirty(b);
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("📋 Kopiuj Profil", EditorStyles.miniButtonLeft, GUILayout.Width(110)))
        {
            CopyBuildingProfile(b);
        }
        if (clipboard != null && GUILayout.Button("📥 Wklej Profil", EditorStyles.miniButtonRight, GUILayout.Width(110)))
        {
            PasteClipboardToBuilding(b);
        }
        EditorGUILayout.EndHorizontal();

        // Typ, Prefab, Odblokowany
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Typ:", GUILayout.Width(35));
        BuildingType newType = (BuildingType)EditorGUILayout.EnumPopup(b.type, GUILayout.Width(110));
        if (newType != b.type)
        {
            Undo.RecordObject(b, "Zmień BuildingType");
            b.type = newType;
            EditorUtility.SetDirty(b);
        }

        GUILayout.Space(10);
        GUILayout.Label("Prefab:", GUILayout.Width(48));
        GameObject newPrefab = (GameObject)EditorGUILayout.ObjectField(b.prefab, typeof(GameObject), false, GUILayout.Width(150));
        if (newPrefab != b.prefab)
        {
            Undo.RecordObject(b, "Zmień Prefab");
            b.prefab = newPrefab;
            EditorUtility.SetDirty(b);
        }

        GUILayout.Space(10);
        bool newUnlocked = EditorGUILayout.ToggleLeft("Odblokowany na start", b.isUnlocked);
        if (newUnlocked != b.isUnlocked)
        {
            Undo.RecordObject(b, "Zmień isUnlocked");
            b.isUnlocked = newUnlocked;
            EditorUtility.SetDirty(b);
        }
        EditorGUILayout.EndHorizontal();

        // Opis
        string newDesc = EditorGUILayout.TextField("Opis:", b.description);
        if (newDesc != b.description)
        {
            Undo.RecordObject(b, "Zmień opis");
            b.description = newDesc;
            EditorUtility.SetDirty(b);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        // Ewentualne ostrzeżenia
        List<string> warnings = GetBuildingWarnings(b);
        if (warnings.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (var w in warnings)
            {
                EditorGUILayout.LabelField(w, warningLabelStyle);
            }
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        // 1. Sekcja Kosztów Budowy
        DrawCostListEditorBox("🏗️ Koszty Budowy (Construction Cost)", b, b.constructionCost, (updated) =>
        {
            b.constructionCost = updated;
        });

        EditorGUILayout.Space(6);

        // 2. Sekcja Upkeepu
        if (b is HousingBuildingData hd)
        {
            DrawCostListEditorBox("⚙️ Baza Utrzymania Dziennego (Base Daily Upkeep)", hd, hd.baseDailyUpkeep, (updated) =>
            {
                hd.baseDailyUpkeep = updated;
            });

            EditorGUILayout.Space(6);
            DrawHousingPerCapitaUpkeepBox(hd);
        }
        else
        {
            DrawCostListEditorBox("⚙️ Koszty Utrzymania per Cykl (Upkeep Per Cycle)", b, b.upkeepPerCycle, (updated) =>
            {
                b.upkeepPerCycle = updated;
            });
        }

        EditorGUILayout.Space(6);

        // 3. Sekcja Produkcji
        if (b is HousingBuildingData hdProd)
        {
            DrawCostListEditorBox("🌾 Produkcja na Mieszkańca (Production Per Resident)", hdProd, hdProd.productionPerResident, (updated) =>
            {
                hdProd.productionPerResident = updated;
            });
        }
        else
        {
            DrawCostListEditorBox("🌾 Produkcja Bazowa per Cykl (Production Per Cycle)", b, b.productionPerCycle, (updated) =>
            {
                b.productionPerCycle = updated;
            });
        }

        EditorGUILayout.Space(6);

        // 4. Sekcja Specyficzna dla Typu
        if (b is TowerData td)
        {
            DrawTowerCombatStatsBox(td);
        }
        else if (b is HousingBuildingData hdStats)
        {
            DrawHousingSpecificsBox(hdStats);
        }
        else
        {
            DrawEconomyWorkersBox(b);
        }

        EditorGUILayout.Space(6);
        DrawTerrainBonusAndPlacementBox(b);
    }

    private void DrawCostListEditorBox(string title, BuildingData owner, List<ResourceCost> costList, Action<List<ResourceCost>> onUpdated)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        // Przycisk dodawania brakującego surowca
        if (GUILayout.Button("+ Dodaj Surowiec", EditorStyles.miniButton, GUILayout.Width(120)))
        {
            GenericMenu menu = new GenericMenu();
            foreach (ResourceType res in MonitoredResources)
            {
                if (costList == null || !costList.Any(c => c.type == res))
                {
                    menu.AddItem(new GUIContent(res.ToString()), false, () =>
                    {
                        Undo.RecordObject(owner, $"Dodaj {res} do {title}");
                        if (costList == null) costList = new List<ResourceCost>();
                        costList.Add(new ResourceCost { type = res, amount = 10 });
                        onUpdated(costList);
                        EditorUtility.SetDirty(owner);
                    });
                }
            }
            if (menu.GetItemCount() == 0)
            {
                menu.AddDisabledItem(new GUIContent("Wszystkie surowce są już dodane"));
            }
            menu.ShowAsContext();
        }
        EditorGUILayout.EndHorizontal();

        if (costList == null || costList.Count == 0)
        {
            EditorGUILayout.LabelField("Brak przypisanych surowców (koszt zerowy).", EditorStyles.miniLabel);
        }
        else
        {
            for (int i = 0; i < costList.Count; i++)
            {
                var item = costList[i];
                EditorGUILayout.BeginHorizontal();

                GUILayout.Label(GetResourceDisplayName(item.type), EditorStyles.boldLabel, GUILayout.Width(120));

                Color prevCol = GUI.backgroundColor;
                GUI.backgroundColor = GetResourceTint(item.type);
                int newAmount = EditorGUILayout.IntField(item.amount, GUILayout.Width(90));
                GUI.backgroundColor = prevCol;

                if (newAmount != item.amount)
                {
                    Undo.RecordObject(owner, $"Zmień {item.type} w {title}");
                    if (newAmount <= 0)
                    {
                        costList.RemoveAt(i);
                        i--;
                    }
                    else
                    {
                        item.amount = newAmount;
                        costList[i] = item;
                    }
                    onUpdated(costList);
                    EditorUtility.SetDirty(owner);
                }

                GUILayout.FlexibleSpace();

                // Przycisk usuń
                if (GUILayout.Button("✕ Usuń", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    Undo.RecordObject(owner, $"Usuń {item.type} z {title}");
                    costList.RemoveAt(i);
                    onUpdated(costList);
                    EditorUtility.SetDirty(owner);
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawTowerCombatStatsBox(TowerData td)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("⚔️ Statystyki Bojowe Wieży (Tower Combat Stats)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        float newRange = EditorGUILayout.FloatField("Zasięg (Range):", td.baseRange);
        if (Mathf.Abs(newRange - td.baseRange) > 0.001f)
        {
            Undo.RecordObject(td, "Zmień baseRange");
            td.baseRange = Mathf.Max(0f, newRange);
            EditorUtility.SetDirty(td);
        }

        float newDmg = EditorGUILayout.FloatField("Obrażenia (Damage):", td.baseDamage);
        if (Mathf.Abs(newDmg - td.baseDamage) > 0.001f)
        {
            Undo.RecordObject(td, "Zmień baseDamage");
            td.baseDamage = Mathf.Max(0f, newDmg);
            EditorUtility.SetDirty(td);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        float newRate = EditorGUILayout.FloatField("Szybkostrzelność (Fire Rate):", td.fireRate);
        if (Mathf.Abs(newRate - td.fireRate) > 0.001f)
        {
            Undo.RecordObject(td, "Zmień fireRate");
            td.fireRate = Mathf.Max(0.01f, newRate);
            EditorUtility.SetDirty(td);
        }

        float dps = td.baseDamage * td.fireRate;
        GUILayout.Label($"Estymowany DPS: {dps:F2}", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        DamageType newDmgType = (DamageType)EditorGUILayout.EnumPopup("Typ Obrażeń:", td.damageType);
        if (newDmgType != td.damageType)
        {
            Undo.RecordObject(td, "Zmień damageType");
            td.damageType = newDmgType;
            EditorUtility.SetDirty(td);
        }

        GameObject newBullet = (GameObject)EditorGUILayout.ObjectField("Pocisk Prefab:", td.bulletPrefab, typeof(GameObject), false);
        if (newBullet != td.bulletPrefab)
        {
            Undo.RecordObject(td, "Zmień bulletPrefab");
            td.bulletPrefab = newBullet;
            EditorUtility.SetDirty(td);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        float newCritChance = EditorGUILayout.FloatField("Szansa na Krytyk (%):", td.criticalChancel);
        if (Mathf.Abs(newCritChance - td.criticalChancel) > 0.001f)
        {
            Undo.RecordObject(td, "Zmień criticalChancel");
            td.criticalChancel = newCritChance;
            EditorUtility.SetDirty(td);
        }

        float newCritMult = EditorGUILayout.FloatField("Mnożnik Krytyka (np. 2 = 200%):", td.criticalDamageMultiplier);
        if (Mathf.Abs(newCritMult - td.criticalDamageMultiplier) > 0.001f)
        {
            Undo.RecordObject(td, "Zmień criticalDamageMultiplier");
            td.criticalDamageMultiplier = newCritMult;
            EditorUtility.SetDirty(td);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        float newArmorPen = EditorGUILayout.FloatField("Penetracja Pancerza (%):", td.armorPenetration);
        if (Mathf.Abs(newArmorPen - td.armorPenetration) > 0.001f)
        {
            Undo.RecordObject(td, "Zmień armorPenetration");
            td.armorPenetration = newArmorPen;
            EditorUtility.SetDirty(td);
        }

        float newMagicPen = EditorGUILayout.FloatField("Penetracja Magii (%):", td.magicPenetration);
        if (Mathf.Abs(newMagicPen - td.magicPenetration) > 0.001f)
        {
            Undo.RecordObject(td, "Zmień magicPenetration");
            td.magicPenetration = newMagicPen;
            EditorUtility.SetDirty(td);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawHousingSpecificsBox(HousingBuildingData hd)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("🏠 Parametry Mieszkalne (Housing Parameters)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        Race newRace = (Race)EditorGUILayout.EnumPopup("Rasa Domostwa:", hd.housingRace);
        if (newRace != hd.housingRace)
        {
            Undo.RecordObject(hd, "Zmień housingRace");
            hd.housingRace = newRace;
            EditorUtility.SetDirty(hd);
        }

        int newMax = EditorGUILayout.IntField("Maksimum Mieszkańców:", hd.maxResidents);
        if (newMax != hd.maxResidents)
        {
            Undo.RecordObject(hd, "Zmień maxResidents");
            hd.maxResidents = Mathf.Max(1, newMax);
            EditorUtility.SetDirty(hd);
        }

        int newInit = EditorGUILayout.IntField("Mieszkańcy Początkowi:", hd.initialResidents);
        if (newInit != hd.initialResidents)
        {
            Undo.RecordObject(hd, "Zmień initialResidents");
            hd.initialResidents = Mathf.Clamp(newInit, 0, hd.maxResidents);
            EditorUtility.SetDirty(hd);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        float newDiff = EditorGUILayout.FloatField("Mnożnik Trudności Wzrostu:", hd.growthDifficultyMultiplier);
        if (Mathf.Abs(newDiff - hd.growthDifficultyMultiplier) > 0.001f)
        {
            Undo.RecordObject(hd, "Zmień growthDifficultyMultiplier");
            hd.growthDifficultyMultiplier = Mathf.Max(0.1f, newDiff);
            EditorUtility.SetDirty(hd);
        }

        int newTicks = EditorGUILayout.IntField("Bazowe Ticki Wzrostu:", hd.baseGrowthTicks);
        if (newTicks != hd.baseGrowthTicks)
        {
            Undo.RecordObject(hd, "Zmień baseGrowthTicks");
            hd.baseGrowthTicks = Mathf.Max(1, newTicks);
            EditorUtility.SetDirty(hd);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawHousingPerCapitaUpkeepBox(HousingBuildingData hd)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("👥 Utrzymanie na Mieszkańca (Per Capita Upkeep)", EditorStyles.boldLabel);

        if (GUILayout.Button("+ Dodaj Surowiec", EditorStyles.miniButton, GUILayout.Width(120)))
        {
            GenericMenu menu = new GenericMenu();
            foreach (ResourceType res in MonitoredResources)
            {
                if (hd.upkeepPerResident == null || !hd.upkeepPerResident.Any(c => c.type == res))
                {
                    menu.AddItem(new GUIContent(res.ToString()), false, () =>
                    {
                        Undo.RecordObject(hd, $"Dodaj {res} do upkeepPerResident");
                        if (hd.upkeepPerResident == null) hd.upkeepPerResident = new List<HousingBuildingData.PerCapitaCost>();
                        hd.upkeepPerResident.Add(new HousingBuildingData.PerCapitaCost { type = res, amount = 1f });
                        EditorUtility.SetDirty(hd);
                    });
                }
            }
            menu.ShowAsContext();
        }
        EditorGUILayout.EndHorizontal();

        if (hd.upkeepPerResident == null || hd.upkeepPerResident.Count == 0)
        {
            EditorGUILayout.LabelField("Brak kosztów na mieszkańca.", EditorStyles.miniLabel);
        }
        else
        {
            for (int i = 0; i < hd.upkeepPerResident.Count; i++)
            {
                var item = hd.upkeepPerResident[i];
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(GetResourceDisplayName(item.type), EditorStyles.boldLabel, GUILayout.Width(120));

                float newAmt = EditorGUILayout.FloatField(item.amount, GUILayout.Width(90));
                if (Mathf.Abs(newAmt - item.amount) > 0.001f)
                {
                    Undo.RecordObject(hd, $"Zmień {item.type} w upkeepPerResident");
                    item.amount = Mathf.Max(0f, newAmt);
                    hd.upkeepPerResident[i] = item;
                    EditorUtility.SetDirty(hd);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("✕ Usuń", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    Undo.RecordObject(hd, $"Usuń {item.type} z upkeepPerResident");
                    hd.upkeepPerResident.RemoveAt(i);
                    EditorUtility.SetDirty(hd);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawEconomyWorkersBox(BuildingData b)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("⚙️ Konfiguracja Pracowników i Skalowania", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        int newWorkers = EditorGUILayout.IntField("Bazowi Pracownicy (baseMaxWorkers):", b.baseMaxWorkers);
        if (newWorkers != b.baseMaxWorkers)
        {
            Undo.RecordObject(b, "Zmień baseMaxWorkers");
            b.baseMaxWorkers = Mathf.Max(0, newWorkers);
            EditorUtility.SetDirty(b);
        }

        if (GUILayout.Button("🛠️ Otwórz w Efficiency Table Editor", EditorStyles.miniButton, GUILayout.Width(220)))
        {
            BuildingEfficiencyEditorWindow.OpenForBuilding(b);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawTerrainBonusAndPlacementBox(BuildingData b)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("🌲 Zasady Produkcji Terenowej i Wymagania Podłoża", EditorStyles.boldLabel);

        // 1. Sekcja Bonusu Terenowego
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Reguły Bonusu Terenowego (Terrain Bonus Rules):", EditorStyles.miniBoldLabel);
        if (GUILayout.Button("➕ Dodaj Regułę Terenu", EditorStyles.miniButton, GUILayout.Width(160)))
        {
            Undo.RecordObject(b, "Dodaj regułę terenu");
            if (b.bonusRules == null) b.bonusRules = new List<TerrainBonusRule>();
            b.bonusRules.Add(new TerrainBonusRule { searchRadius = 1, resourceYields = new List<TerrainResourceYield>() });
            EditorUtility.SetDirty(b);
        }
        EditorGUILayout.EndHorizontal();

        if (b.bonusRules != null && b.bonusRules.Count > 0)
        {
            for (int rIdx = 0; rIdx < b.bonusRules.Count; rIdx++)
            {
                var rule = b.bonusRules[rIdx];
                if (rule == null) continue;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Reguła #{rIdx + 1}: {rule.requiredFeature}", EditorStyles.boldLabel);
                GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
                if (GUILayout.Button("Usuń regułę", EditorStyles.miniButton, GUILayout.Width(90)))
                {
                    Undo.RecordObject(b, "Usuń regułę terenu");
                    b.bonusRules.RemoveAt(rIdx);
                    EditorUtility.SetDirty(b);
                    GUI.backgroundColor = Color.white;
                    break;
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                HexFeatureType newFeat = (HexFeatureType)EditorGUILayout.EnumPopup("Wymagany Teren:", rule.requiredFeature);
                if (newFeat != rule.requiredFeature)
                {
                    Undo.RecordObject(b, "Zmień requiredFeature");
                    rule.requiredFeature = newFeat;
                    EditorUtility.SetDirty(b);
                }

                int newRadius = EditorGUILayout.IntSlider("Promień (Radius):", rule.searchRadius, 1, 5);
                if (newRadius != rule.searchRadius)
                {
                    Undo.RecordObject(b, "Zmień searchRadius");
                    rule.searchRadius = Mathf.Max(1, newRadius);
                    EditorUtility.SetDirty(b);
                }
                EditorGUILayout.EndHorizontal();

                if (rule.resourceYields == null) rule.resourceYields = new List<TerrainResourceYield>();

                // Wyświetlanie surowców generowanych przez tę regułę
                EditorGUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("  Surowce generowane z tego terenu:", EditorStyles.miniBoldLabel);
                if (GUILayout.Button("➕ Dodaj surowiec", EditorStyles.miniButton, GUILayout.Width(130)))
                {
                    Undo.RecordObject(b, "Dodaj surowiec do reguły");
                    rule.resourceYields.Add(new TerrainResourceYield { resourceType = ResourceType.Wood, baseBonusPerHex = 1f });
                    EditorUtility.SetDirty(b);
                }
                EditorGUILayout.EndHorizontal();

                for (int yIdx = 0; yIdx < rule.resourceYields.Count; yIdx++)
                {
                    var y = rule.resourceYields[yIdx];
                    EditorGUILayout.BeginHorizontal();
                    ResourceType newRes = (ResourceType)EditorGUILayout.EnumPopup(y.resourceType, GUILayout.Width(100));
                    float newBase = EditorGUILayout.FloatField("Baza:", y.baseBonusPerHex, GUILayout.Width(120));
                    float newOnTop = EditorGUILayout.FloatField("On-Top:", y.onTopProductionBonus, GUILayout.Width(120));
                    float newPen = EditorGUILayout.FloatField("Kara/gracz:", y.penaltyPerUser, GUILayout.Width(120));
                    float newMin = EditorGUILayout.FloatField("Min:", y.minBonus, GUILayout.Width(100));

                    if (newRes != y.resourceType || Mathf.Abs(newBase - y.baseBonusPerHex) > 0.001f ||
                        Mathf.Abs(newOnTop - y.onTopProductionBonus) > 0.001f || Mathf.Abs(newPen - y.penaltyPerUser) > 0.001f ||
                        Mathf.Abs(newMin - y.minBonus) > 0.001f)
                    {
                        Undo.RecordObject(b, "Edycja surowca terenu");
                        y.resourceType = newRes;
                        y.baseBonusPerHex = newBase;
                        y.onTopProductionBonus = newOnTop;
                        y.penaltyPerUser = newPen;
                        y.minBonus = newMin;
                        rule.resourceYields[yIdx] = y;
                        EditorUtility.SetDirty(b);
                    }

                    GUI.backgroundColor = new Color(1f, 0.7f, 0.7f);
                    if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(24)))
                    {
                        Undo.RecordObject(b, "Usuń surowiec z reguły");
                        rule.resourceYields.RemoveAt(yIdx);
                        EditorUtility.SetDirty(b);
                        GUI.backgroundColor = Color.white;
                        break;
                    }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }
        else
        {
            EditorGUILayout.LabelField("Brak zdefiniowanych reguł bonusu terenowego dla tego budynku.", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // 2. Sekcja Wymagań Podłoża (Allowed Terrain)
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Dozwolone Podłoże do Budowy (Allowed Terrain):", EditorStyles.miniBoldLabel);

        if (GUILayout.Button("+ Dodaj Teren", EditorStyles.miniButton, GUILayout.Width(110)))
        {
            ShowAllowedTerrainMenu(b);
        }
        EditorGUILayout.EndHorizontal();

        bool newReqOccupied = EditorGUILayout.ToggleLeft("Wymaga zajętego miejsca (requiresOccupiedSpace)", b.requiresOccupiedSpace);
        if (newReqOccupied != b.requiresOccupiedSpace)
        {
            Undo.RecordObject(b, "Zmień requiresOccupiedSpace");
            b.requiresOccupiedSpace = newReqOccupied;
            EditorUtility.SetDirty(b);
        }

        if (b.allowedTerrain == null || b.allowedTerrain.Count == 0)
        {
            EditorGUILayout.LabelField("Brak ograniczeń (można stawiać na dowolnym terenie).", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < b.allowedTerrain.Count; i++)
            {
                var feat = b.allowedTerrain[i];
                Color prevColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
                if (GUILayout.Button($"{feat} ✕", EditorStyles.miniButton, GUILayout.Height(20)))
                {
                    Undo.RecordObject(b, $"Usuń dozwolony teren {feat}");
                    b.allowedTerrain.RemoveAt(i);
                    EditorUtility.SetDirty(b);
                    break;
                }
                GUI.backgroundColor = prevColor;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndVertical();
    }

    // =========================================================================
    // KOPIOWANIE I WKLEJANIE (CLIPBOARD)
    // =========================================================================
    private void CopyBuildingProfile(BuildingData b)
    {
        if (b == null) return;

        clipboard = new BalanceClipboard
        {
            sourceName = string.IsNullOrEmpty(b.buildingName) ? b.name : b.buildingName,
            constructionCost = CloneCostList(b.constructionCost),
            upkeepPerCycle = CloneCostList(b.upkeepPerCycle),
            productionPerCycle = CloneCostList(b.productionPerCycle),
            baseMaxWorkers = b.baseMaxWorkers,
            bonusRules = CloneBonusRules(b.bonusRules),
            allowedTerrain = b.allowedTerrain != null ? new List<HexFeatureType>(b.allowedTerrain) : new List<HexFeatureType>(),
            requiresOccupiedSpace = b.requiresOccupiedSpace
        };

        if (b is HousingBuildingData hd)
        {
            clipboard.isHousing = true;
            clipboard.baseDailyUpkeep = CloneCostList(hd.baseDailyUpkeep);
            clipboard.upkeepPerResident = hd.upkeepPerResident != null ? new List<HousingBuildingData.PerCapitaCost>(hd.upkeepPerResident) : new List<HousingBuildingData.PerCapitaCost>();
            clipboard.productionPerResident = CloneCostList(hd.productionPerResident);
            clipboard.maxResidents = hd.maxResidents;
            clipboard.initialResidents = hd.initialResidents;
        }

        if (b is TowerData td)
        {
            clipboard.isTower = true;
            clipboard.baseRange = td.baseRange;
            clipboard.baseDamage = td.baseDamage;
            clipboard.fireRate = td.fireRate;
        }

        ShowNotification(new GUIContent($"Skopiowano profil z: {clipboard.sourceName}"));
    }

    private void PasteClipboardToBuilding(BuildingData target)
    {
        if (clipboard == null || target == null) return;

        Undo.RecordObject(target, "Wklej profil balansu");

        target.constructionCost = CloneCostList(clipboard.constructionCost);
        target.upkeepPerCycle = CloneCostList(clipboard.upkeepPerCycle);
        target.productionPerCycle = CloneCostList(clipboard.productionPerCycle);
        target.baseMaxWorkers = clipboard.baseMaxWorkers;
        target.bonusRules = CloneBonusRules(clipboard.bonusRules);
        target.allowedTerrain = new List<HexFeatureType>(clipboard.allowedTerrain);
        target.requiresOccupiedSpace = clipboard.requiresOccupiedSpace;

        if (target is HousingBuildingData hd && clipboard.isHousing)
        {
            hd.baseDailyUpkeep = CloneCostList(clipboard.baseDailyUpkeep);
            hd.upkeepPerResident = new List<HousingBuildingData.PerCapitaCost>(clipboard.upkeepPerResident);
            hd.productionPerResident = CloneCostList(clipboard.productionPerResident);
            hd.maxResidents = clipboard.maxResidents;
            hd.initialResidents = clipboard.initialResidents;
        }

        if (target is TowerData td && clipboard.isTower)
        {
            td.baseRange = clipboard.baseRange;
            td.baseDamage = clipboard.baseDamage;
            td.fireRate = clipboard.fireRate;
        }

        EditorUtility.SetDirty(target);
        ShowNotification(new GUIContent($"Wklejono profil do: {target.buildingName}"));
    }

    private void PasteClipboardToSelected()
    {
        if (clipboard == null) return;

        var selectedList = allBuildings.Where(b => selectedBuildingGuids.Contains(GetGuid(b))).ToList();
        if (selectedList.Count == 0)
        {
            ShowNotification(new GUIContent("Zaznacz budynki checkboxami, aby wkleić profil!"));
            return;
        }

        foreach (var b in selectedList)
        {
            PasteClipboardToBuilding(b);
        }

        ShowNotification(new GUIContent($"Wklejono profil do {selectedList.Count} budynków!"));
    }

    private List<ResourceCost> CloneCostList(List<ResourceCost> source)
    {
        if (source == null) return new List<ResourceCost>();
        return new List<ResourceCost>(source);
    }

    private static List<TerrainBonusRule> CloneBonusRules(List<TerrainBonusRule> source)
    {
        if (source == null) return new List<TerrainBonusRule>();
        var result = new List<TerrainBonusRule>();
        foreach (var rule in source)
        {
            if (rule == null) continue;
            var newRule = new TerrainBonusRule
            {
                requiredFeature = rule.requiredFeature,
                searchRadius = rule.searchRadius,
                resourceYields = new List<TerrainResourceYield>()
            };
            if (rule.resourceYields != null)
            {
                foreach (var y in rule.resourceYields)
                {
                    newRule.resourceYields.Add(new TerrainResourceYield
                    {
                        resourceType = y.resourceType,
                        baseBonusPerHex = y.baseBonusPerHex,
                        onTopProductionBonus = y.onTopProductionBonus,
                        penaltyPerUser = y.penaltyPerUser,
                        minBonus = y.minBonus
                    });
                }
            }
            result.Add(newRule);
        }
        return result;
    }

    // =========================================================================
    // EKSPORT I IMPORT CSV
    // =========================================================================
    private void ExportToCsv()
    {
        string path = EditorUtility.SaveFilePanel("Eksportuj Balans Budynków do CSV", "", "BuildingBalance.csv", "csv");
        if (string.IsNullOrEmpty(path)) return;

        var sb = new StringBuilder();

        // Nagłówek CSV
        var headers = new List<string> { "AssetGUID", "Nazwa", "Typ" };
        foreach (var res in MonitoredResources) headers.Add($"Cost_{res}");
        foreach (var res in MonitoredResources) headers.Add($"Upkeep_{res}");
        foreach (var res in MonitoredResources) headers.Add($"Prod_{res}");
        headers.Add("BaseMaxWorkers");
        headers.Add("Housing_MaxResidents");
        headers.Add("Tower_Range");
        headers.Add("Tower_Damage");
        headers.Add("Tower_FireRate");
        headers.Add("Terrain_Feature");
        headers.Add("Terrain_SearchRadius");
        headers.Add("Terrain_BaseBonusPerHex");
        headers.Add("Terrain_PenaltyPerUser");
        headers.Add("Terrain_MinBonus");
        headers.Add("Terrain_OnTopBonus");
        headers.Add("Terrain_RequiresOccupied");
        headers.Add("Terrain_Allowed");

        sb.AppendLine(string.Join(";", headers));

        foreach (var b in allBuildings)
        {
            if (b == null) continue;

            var row = new List<string>
            {
                GetGuid(b),
                EscapeCsv(string.IsNullOrEmpty(b.buildingName) ? b.name : b.buildingName),
                b.type.ToString()
            };

            // Koszty budowy
            foreach (var res in MonitoredResources) row.Add(GetCostAmount(b.constructionCost, res).ToString());

            // Upkeep
            List<ResourceCost> upkeepList = (b is HousingBuildingData hd) ? hd.baseDailyUpkeep : b.upkeepPerCycle;
            foreach (var res in MonitoredResources) row.Add(GetCostAmount(upkeepList, res).ToString());

            // Produkcja
            List<ResourceCost> prodList = (b is HousingBuildingData hdp) ? hdp.productionPerResident : b.productionPerCycle;
            foreach (var res in MonitoredResources) row.Add(GetCostAmount(prodList, res).ToString());

            row.Add(b.baseMaxWorkers.ToString());

            if (b is HousingBuildingData hres) row.Add(hres.maxResidents.ToString());
            else row.Add("0");

            if (b is TowerData td)
            {
                row.Add(td.baseRange.ToString("F2"));
                row.Add(td.baseDamage.ToString("F2"));
                row.Add(td.fireRate.ToString("F2"));
            }
            else
            {
                row.Add("0");
                row.Add("0");
                row.Add("0");
            }

            // Pola bonusu terenowego i wymagań podłoża
            var firstRule = (b.bonusRules != null && b.bonusRules.Count > 0) ? b.bonusRules[0] : null;
            var firstYield = (firstRule != null && firstRule.resourceYields != null && firstRule.resourceYields.Count > 0) ? firstRule.resourceYields[0] : (TerrainResourceYield?)null;

            row.Add(firstRule != null ? firstRule.requiredFeature.ToString() : HexFeatureType.None.ToString());
            row.Add(firstRule != null ? firstRule.searchRadius.ToString() : "1");
            row.Add(firstYield.HasValue ? firstYield.Value.baseBonusPerHex.ToString("F2") : "0.00");
            row.Add(firstYield.HasValue ? firstYield.Value.penaltyPerUser.ToString("F2") : "0.00");
            row.Add(firstYield.HasValue ? firstYield.Value.minBonus.ToString("F2") : "0.00");
            row.Add(firstYield.HasValue ? firstYield.Value.onTopProductionBonus.ToString("F2") : "0.00");
            row.Add(b.requiresOccupiedSpace ? "1" : "0");
            row.Add(b.allowedTerrain != null && b.allowedTerrain.Count > 0 ? string.Join("|", b.allowedTerrain) : "");

            sb.AppendLine(string.Join(";", row));
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        EditorUtility.RevealInFinder(path);
        ShowNotification(new GUIContent("Pomyślnie wyeksportowano CSV!"));
    }

    private void ImportFromCsv()
    {
        string path = EditorUtility.OpenFilePanel("Importuj Balans Budynków z CSV", "", "csv");
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        if (lines.Length < 2)
        {
            EditorUtility.DisplayDialog("Błąd", "Plik CSV jest pusty lub niepoprawny.", "OK");
            return;
        }

        // Wykryj separator (; lub ,)
        char sep = lines[0].Contains(";") ? ';' : ',';
        string[] headers = lines[0].Split(sep);

        var headerMap = new Dictionary<string, int>();
        for (int i = 0; i < headers.Length; i++)
        {
            headerMap[headers[i].Trim()] = i;
        }

        int updatedCount = 0;

        for (int rowIdx = 1; rowIdx < lines.Length; rowIdx++)
        {
            string line = lines[rowIdx].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = line.Split(sep);
            if (cols.Length < 3) continue;

            string guid = cols[0].Trim();
            string name = cols[1].Trim();

            BuildingData b = null;
            if (!string.IsNullOrEmpty(guid))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    b = AssetDatabase.LoadAssetAtPath<BuildingData>(assetPath);
                }
            }

            if (b == null)
            {
                b = allBuildings.FirstOrDefault(x => x.name == name || x.buildingName == name);
            }

            if (b == null) continue;

            Undo.RecordObject(b, "Import Balansu z CSV");

            // Koszty budowy
            foreach (var res in MonitoredResources)
            {
                string colName = $"Cost_{res}";
                if (headerMap.TryGetValue(colName, out int colIdx) && colIdx < cols.Length)
                {
                    if (int.TryParse(cols[colIdx], out int val))
                    {
                        var list = b.constructionCost;
                        SetCostAmount(ref list, res, val);
                        b.constructionCost = list;
                    }
                }
            }

            // Upkeep
            foreach (var res in MonitoredResources)
            {
                string colName = $"Upkeep_{res}";
                if (headerMap.TryGetValue(colName, out int colIdx) && colIdx < cols.Length)
                {
                    if (int.TryParse(cols[colIdx], out int val))
                    {
                        if (b is HousingBuildingData hd)
                        {
                            var list = hd.baseDailyUpkeep;
                            SetCostAmount(ref list, res, val);
                            hd.baseDailyUpkeep = list;
                        }
                        else
                        {
                            var list = b.upkeepPerCycle;
                            SetCostAmount(ref list, res, val);
                            b.upkeepPerCycle = list;
                        }
                    }
                }
            }

            // Produkcja
            foreach (var res in MonitoredResources)
            {
                string colName = $"Prod_{res}";
                if (headerMap.TryGetValue(colName, out int colIdx) && colIdx < cols.Length)
                {
                    if (int.TryParse(cols[colIdx], out int val))
                    {
                        if (b is HousingBuildingData hd)
                        {
                            var list = hd.productionPerResident;
                            SetCostAmount(ref list, res, val);
                            hd.productionPerResident = list;
                        }
                        else
                        {
                            var list = b.productionPerCycle;
                            SetCostAmount(ref list, res, val);
                            b.productionPerCycle = list;
                        }
                    }
                }
            }

            // Statystyki
            if (headerMap.TryGetValue("BaseMaxWorkers", out int wCol) && wCol < cols.Length)
            {
                if (int.TryParse(cols[wCol], out int wVal)) b.baseMaxWorkers = Mathf.Max(0, wVal);
            }

            if (b is HousingBuildingData hdRes && headerMap.TryGetValue("Housing_MaxResidents", out int hCol) && hCol < cols.Length)
            {
                if (int.TryParse(cols[hCol], out int hVal)) hdRes.maxResidents = Mathf.Max(1, hVal);
            }

            if (b is TowerData td)
            {
                if (headerMap.TryGetValue("Tower_Range", out int rCol) && rCol < cols.Length)
                {
                    if (float.TryParse(cols[rCol], out float rVal)) td.baseRange = Mathf.Max(0f, rVal);
                }
                if (headerMap.TryGetValue("Tower_Damage", out int dCol) && dCol < cols.Length)
                {
                    if (float.TryParse(cols[dCol], out float dVal)) td.baseDamage = Mathf.Max(0f, dVal);
                }
                if (headerMap.TryGetValue("Tower_FireRate", out int fCol) && fCol < cols.Length)
                {
                    if (float.TryParse(cols[fCol], out float fVal)) td.fireRate = Mathf.Max(0.01f, fVal);
                }
            }

            // Pola bonusu terenowego i wymagań podłoża
            if (b.bonusRules == null) b.bonusRules = new List<TerrainBonusRule>();
            TerrainBonusRule primaryRule = b.bonusRules.Count > 0 ? b.bonusRules[0] : null;

            if (headerMap.TryGetValue("Terrain_Feature", out int tfCol) && tfCol < cols.Length)
            {
                if (Enum.TryParse<HexFeatureType>(cols[tfCol].Trim(), out var feat) && feat != HexFeatureType.None)
                {
                    if (primaryRule == null)
                    {
                        primaryRule = new TerrainBonusRule { searchRadius = 1, resourceYields = new List<TerrainResourceYield>() };
                        b.bonusRules.Add(primaryRule);
                    }
                    primaryRule.requiredFeature = feat;
                }
            }
            if (headerMap.TryGetValue("Terrain_SearchRadius", out int tsCol) && tsCol < cols.Length && primaryRule != null)
            {
                if (int.TryParse(cols[tsCol].Trim(), out int rad)) primaryRule.searchRadius = Mathf.Max(1, rad);
            }
            if (primaryRule != null)
            {
                if (primaryRule.resourceYields == null) primaryRule.resourceYields = new List<TerrainResourceYield>();
                TerrainResourceYield y = primaryRule.resourceYields.Count > 0
                    ? primaryRule.resourceYields[0]
                    : new TerrainResourceYield { resourceType = (b.productionPerCycle != null && b.productionPerCycle.Count > 0) ? b.productionPerCycle[0].type : ResourceType.Wood };

                if (headerMap.TryGetValue("Terrain_BaseBonusPerHex", out int tbCol) && tbCol < cols.Length)
                {
                    if (float.TryParse(cols[tbCol].Trim(), out float bb)) y.baseBonusPerHex = bb;
                }
                if (headerMap.TryGetValue("Terrain_PenaltyPerUser", out int tpCol) && tpCol < cols.Length)
                {
                    if (float.TryParse(cols[tpCol].Trim(), out float pen)) y.penaltyPerUser = pen;
                }
                if (headerMap.TryGetValue("Terrain_MinBonus", out int tmCol) && tmCol < cols.Length)
                {
                    if (float.TryParse(cols[tmCol].Trim(), out float mb)) y.minBonus = mb;
                }
                if (headerMap.TryGetValue("Terrain_OnTopBonus", out int toCol) && toCol < cols.Length)
                {
                    if (float.TryParse(cols[toCol].Trim(), out float ot)) y.onTopProductionBonus = ot;
                }

                if (primaryRule.resourceYields.Count > 0) primaryRule.resourceYields[0] = y;
                else if (y.baseBonusPerHex > 0 || y.onTopProductionBonus > 0) primaryRule.resourceYields.Add(y);
            }
            if (headerMap.TryGetValue("Terrain_RequiresOccupied", out int troCol) && troCol < cols.Length)
            {
                b.requiresOccupiedSpace = (cols[troCol].Trim() == "1" || cols[troCol].Trim().ToLower() == "true");
            }
            if (headerMap.TryGetValue("Terrain_Allowed", out int taCol) && taCol < cols.Length)
            {
                string raw = cols[taCol].Trim();
                if (!string.IsNullOrEmpty(raw))
                {
                    var feats = raw.Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    b.allowedTerrain = new List<HexFeatureType>();
                    foreach (var fStr in feats)
                    {
                        if (Enum.TryParse<HexFeatureType>(fStr.Trim(), out var parsedFeat))
                        {
                            if (!b.allowedTerrain.Contains(parsedFeat)) b.allowedTerrain.Add(parsedFeat);
                        }
                    }
                }
            }

            EditorUtility.SetDirty(b);
            updatedCount++;
        }

        ShowNotification(new GUIContent($"Pomyślnie zaimportowano dane dla {updatedCount} budynków!"));
    }

    private string EscapeCsv(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Contains(";") || text.Contains(",") || text.Contains("\"") || text.Contains("\n"))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }
        return text;
    }

    // =========================================================================
    // METODY POMOCNICZE / FILTROWANIE / WARTOŚCI
    // =========================================================================
    private List<BuildingData> GetFilteredBuildings()
    {
        return allBuildings.Where(b =>
        {
            if (b == null) return false;

            // Filtr kategorii
            if (categoryFilterIndex > 0)
            {
                BuildingType expectedType = (BuildingType)(categoryFilterIndex - 1);
                if (b.type != expectedType)
                {
                    // Specjalna obsługa housing/defense w przypadku dziedziczenia
                    if (expectedType == BuildingType.Housing && !(b is HousingBuildingData || b.type == BuildingType.Housing)) return false;
                    if (expectedType == BuildingType.Defense && !(b is TowerData || b.type == BuildingType.Defense)) return false;
                    if (b.type != expectedType) return false;
                }
            }

            // Wyszukiwanie
            if (!string.IsNullOrEmpty(searchFilter))
            {
                string s = searchFilter.ToLower();
                string bName = (b.buildingName ?? "").ToLower();
                string aName = b.name.ToLower();
                if (!bName.Contains(s) && !aName.Contains(s)) return false;
            }

            // Ostrzeżenia
            if (showOnlyWarnings)
            {
                if (GetBuildingWarnings(b).Count == 0) return false;
            }

            return true;
        }).ToList();
    }

    private List<ResourceCost> GetResourceCostListForView(BuildingData b, TableSubView subView)
    {
        switch (subView)
        {
            case TableSubView.ConstructionCost:
                return b.constructionCost;
            case TableSubView.Upkeep:
                return (b is HousingBuildingData hd) ? hd.baseDailyUpkeep : b.upkeepPerCycle;
            case TableSubView.Production:
                return (b is HousingBuildingData hdp) ? hdp.productionPerResident : b.productionPerCycle;
            default:
                return b.constructionCost;
        }
    }

    private void SetResourceCostListForView(BuildingData b, TableSubView subView, List<ResourceCost> updated)
    {
        switch (subView)
        {
            case TableSubView.ConstructionCost:
                b.constructionCost = updated;
                break;
            case TableSubView.Upkeep:
                if (b is HousingBuildingData hd) hd.baseDailyUpkeep = updated;
                else b.upkeepPerCycle = updated;
                break;
            case TableSubView.Production:
                if (b is HousingBuildingData hdp) hdp.productionPerResident = updated;
                else b.productionPerCycle = updated;
                break;
        }
    }

    private static int GetCostAmount(List<ResourceCost> list, ResourceType type)
    {
        if (list == null) return 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].type == type) return list[i].amount;
        }
        return 0;
    }

    private static void SetCostAmount(ref List<ResourceCost> list, ResourceType type, int amount)
    {
        if (list == null) list = new List<ResourceCost>();

        int index = -1;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].type == type)
            {
                index = i;
                break;
            }
        }

        if (amount <= 0)
        {
            if (index >= 0)
            {
                list.RemoveAt(index);
            }
        }
        else
        {
            if (index >= 0)
            {
                ResourceCost c = list[index];
                c.amount = amount;
                list[index] = c;
            }
            else
            {
                list.Add(new ResourceCost { type = type, amount = amount });
            }
        }
    }

    private string GetGuid(BuildingData b)
    {
        if (b == null) return "";
        string path = AssetDatabase.GetAssetPath(b);
        return AssetDatabase.AssetPathToGUID(path);
    }

    private string GetCategoryBadge(BuildingData b)
    {
        if (b is HousingBuildingData || b.type == BuildingType.Housing) return "🏠 Housing";
        if (b is TowerData || b.type == BuildingType.Defense) return "🏹 Defense";
        switch (b.type)
        {
            case BuildingType.Economic: return "🌾 Economic";
            case BuildingType.Utility: return "📦 Utility";
            case BuildingType.Unique: return "⭐ Unique";
            default: return b.type.ToString();
        }
    }

    private string GetWarningBadge(BuildingData b)
    {
        int count = GetBuildingWarnings(b).Count;
        return count > 0 ? "⚠️" : "";
    }

    private List<string> GetBuildingWarnings(BuildingData b)
    {
        var list = new List<string>();
        if (b == null) return list;

        // Brak kosztu budowy
        if (b.type != BuildingType.Unique && (b.constructionCost == null || b.constructionCost.Count == 0))
        {
            list.Add("⚠️ Brak kosztu budowy (budynek jest darmowy).");
        }

        // Brak ikony
        if (b.icon == null)
        {
            list.Add("⚠️ Brak przypisanej ikony (Sprite).");
        }

        // Brak prefabu
        if (b.prefab == null)
        {
            list.Add("⚠️ Brak przypisanego prefabu budynku.");
        }

        // Wieża bez upkeepu
        if (b is TowerData td)
        {
            if (td.upkeepPerCycle == null || td.upkeepPerCycle.Count == 0)
            {
                list.Add("💡 Wieża obronna nie posiada zdefiniowanego upkeepu.");
            }
            if (td.bulletPrefab == null)
            {
                list.Add("⚠️ Wieża nie ma przypisanego prefabu pocisku (bulletPrefab).");
            }
            if (td.baseDamage <= 0)
            {
                list.Add("⚠️ Obrażenia bazowe wieży wynoszą 0.");
            }
        }

        // Housing bez mieszkańców
        if (b is HousingBuildingData hd)
        {
            if (hd.maxResidents <= 0)
            {
                list.Add("⚠️ Budynek mieszkalny ma maxResidents <= 0.");
            }
        }

        return list;
    }

    private string GetResourceDisplayName(ResourceType res)
    {
        switch (res)
        {
            case ResourceType.Ether: return "💎 Kryształ Eteru";
            case ResourceType.Wood: return "🪵 Drewno";
            case ResourceType.Stone: return "🪨 Kamień";
            case ResourceType.Coal: return "⬛ Węgiel";
            case ResourceType.Food: return "🍞 Jedzenie";
            case ResourceType.Iron: return "⚔️ Żelazo";
            case ResourceType.Hope: return "✨ Nadzieja";
            default: return res.ToString();
        }
    }

    private Color GetResourceTint(ResourceType res)
    {
        switch (res)
        {
            case ResourceType.Ether: return new Color(0.35f, 0.85f, 1f);
            case ResourceType.Wood: return new Color(0.85f, 0.7f, 0.5f);
            case ResourceType.Stone: return new Color(0.8f, 0.85f, 0.9f);
            case ResourceType.Coal: return new Color(0.6f, 0.6f, 0.65f);
            case ResourceType.Food: return new Color(0.7f, 0.95f, 0.6f);
            case ResourceType.Iron: return new Color(0.75f, 0.8f, 1f);
            case ResourceType.Hope: return new Color(1f, 0.75f, 0.95f);
            default: return Color.white;
        }
    }
}
#endif
