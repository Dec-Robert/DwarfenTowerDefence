#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class RuneManagerEditorWindow : EditorWindow
{
    private const string BASE_PATH = "Assets/Main folder/ScriptableObjects/Runes/Runes";
    private const string ICONS_PATH = "Assets/Main folder/Icons/Runes";

    private readonly Dictionary<RuneRarity, string> folderMap = new Dictionary<RuneRarity, string>
    {
        { RuneRarity.Common, "1.Common" },
        { RuneRarity.Uncommon, "2.Uncommon" },
        { RuneRarity.Rare, "3.Rare" },
        { RuneRarity.Legendary, "4.Legendary" },
        { RuneRarity.Cursed, "5.Cursed" }
    };

    private static readonly RuneRarity[] AllRarities = (RuneRarity[])Enum.GetValues(typeof(RuneRarity));
    private static readonly RuneStatType[] AllStats = (RuneStatType[])Enum.GetValues(typeof(RuneStatType));

    [Serializable]
    private struct StatRowKey : IEquatable<StatRowKey>
    {
        public RuneStatType stat;
        public RuneValueType valueType;

        public StatRowKey(RuneStatType stat, RuneValueType valueType)
        {
            this.stat = stat;
            this.valueType = valueType;
        }

        public bool Equals(StatRowKey other) => stat == other.stat && valueType == other.valueType;
        public override bool Equals(object obj) => obj is StatRowKey other && Equals(other);
        public override int GetHashCode() => ((int)stat * 397) ^ (int)valueType;
    }

    [Serializable]
    private class GlobalPenaltyEntry
    {
        public bool enabled = true;
        public RuneStatType stat;
        public RuneValueType valueType;
        public Vector2 range = new Vector2(10f, 20f);
    }

    private RuneSystemConfigSO systemConfig;
    private readonly Dictionary<RuneRarity, List<RuneDefinitionSO>> allRunes = new Dictionary<RuneRarity, List<RuneDefinitionSO>>();
    private readonly List<StatRowKey> matrixRows = new List<StatRowKey>();
    private readonly Dictionary<StatRowKey, float> rowScaleMultipliers = new Dictionary<StatRowKey, float>();

    // Wybrana runa w bocznym inspektorze
    private RuneDefinitionSO selectedRune;
    private SerializedObject serializedRune;

    // Schowek dla kar Cursed
    private List<RuneDefinitionSO.CursedPenalty> penaltyClipboard;

    // Globalny szablon kar Cursed
    private readonly List<GlobalPenaltyEntry> globalPenalties = new List<GlobalPenaltyEntry>();

    // Przełączniki widoku i zachowania
    private bool chainMinToPrevMax = true;
    private bool autoSyncConfig = true;
    private bool showConfigSection = false;
    private bool showGlobalPenaltiesSection = true;
    private bool showDetailsPanel = true;

    // Kreator nowej ścieżki / runy
    private RuneStatType creatorStat = RuneStatType.Damage;
    private RuneValueType creatorValueType = RuneValueType.Percent;
    private Vector2 creatorBaseRange = new Vector2(5f, 10f);
    private float creatorSpanMultiplier = 1.4f;

    // Pozycje scrolla
    private Vector2 matrixScrollPos;
    private Vector2 detailsScrollPos;
    private Vector2 globalPenaltiesScrollPos;

    // Status ostatniej operacji (zamiast wyskakujących okienek modalnych)
    private string statusMessage = "";
    private MessageType statusType = MessageType.Info;
    private double statusClearTime;

    [MenuItem("Tools/Rune Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<RuneManagerEditorWindow>("Zarządzanie Runami");
        window.minSize = new Vector2(1180, 680);
        window.Show();
    }

    private void OnEnable()
    {
        FindSystemConfig();
        LoadAllRunes();
        InitGlobalPenaltiesFromExistingCursed();
    }

    private void FindSystemConfig()
    {
        if (systemConfig != null) return;
        string[] configGuids = AssetDatabase.FindAssets("t:RuneSystemConfigSO");
        if (configGuids.Length > 0)
        {
            string configPath = AssetDatabase.GUIDToAssetPath(configGuids[0]);
            systemConfig = AssetDatabase.LoadAssetAtPath<RuneSystemConfigSO>(configPath);
        }
    }

    private void SetStatus(string msg, MessageType type = MessageType.Info)
    {
        statusMessage = msg;
        statusType = type;
        statusClearTime = EditorApplication.timeSinceStartup + 6.0;
        Repaint();
    }

    private void LoadAllRunes()
    {
        allRunes.Clear();
        var rowSet = new HashSet<StatRowKey>();

        foreach (RuneRarity rarity in AllRarities)
        {
            allRunes[rarity] = new List<RuneDefinitionSO>();
            string fullPath = $"{BASE_PATH}/{folderMap[rarity]}";

            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                AssetDatabase.Refresh();
            }

            string[] guids = AssetDatabase.FindAssets("t:RuneDefinitionSO", new[] { fullPath });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                RuneDefinitionSO rune = AssetDatabase.LoadAssetAtPath<RuneDefinitionSO>(assetPath);
                if (rune != null)
                {
                    allRunes[rarity].Add(rune);
                    rowSet.Add(new StatRowKey(rune.primaryStat, rune.valueType));
                }
            }

            allRunes[rarity] = allRunes[rarity]
                .OrderBy(r => r.primaryStat)
                .ThenBy(r => r.valueType)
                .ThenBy(r => r.name)
                .ToList();
        }

        matrixRows.Clear();
        matrixRows.AddRange(rowSet.OrderBy(k => (int)k.stat).ThenBy(k => (int)k.valueType));

        foreach (var row in matrixRows)
        {
            if (!rowScaleMultipliers.ContainsKey(row))
            {
                rowScaleMultipliers[row] = EstimateRowMultiplier(row);
            }
        }
    }

    private float EstimateRowMultiplier(StatRowKey row)
    {
        RuneDefinitionSO common = GetRune(row, RuneRarity.Common);
        RuneDefinitionSO uncommon = GetRune(row, RuneRarity.Uncommon);
        if (common != null && uncommon != null)
        {
            float spanCommon = Mathf.Max(1f, common.primaryValueRange.y - common.primaryValueRange.x);
            float spanUncommon = Mathf.Max(1f, uncommon.primaryValueRange.y - uncommon.primaryValueRange.x);
            float ratio = (float)Math.Round(spanUncommon / spanCommon, 2);
            if (ratio >= 1.05f && ratio <= 4f) return ratio;
        }
        return 1.4f;
    }

    private void InitGlobalPenaltiesFromExistingCursed()
    {
        globalPenalties.Clear();

        // Zbieramy istniejące kary z run Cursed jako wartości początkowe
        var foundByStat = new Dictionary<RuneStatType, RuneDefinitionSO.CursedPenalty>();
        if (allRunes.TryGetValue(RuneRarity.Cursed, out var cursedList))
        {
            foreach (var cRune in cursedList)
            {
                if (cRune == null || cRune.possiblePenalties == null) continue;
                foreach (var p in cRune.possiblePenalties)
                {
                    if (!foundByStat.ContainsKey(p.stat))
                    {
                        foundByStat[p.stat] = p;
                    }
                }
            }
        }

        foreach (RuneStatType stat in AllStats)
        {
            if (foundByStat.TryGetValue(stat, out var existing))
            {
                globalPenalties.Add(new GlobalPenaltyEntry
                {
                    enabled = true,
                    stat = stat,
                    valueType = existing.valueType,
                    range = existing.penaltyRange
                });
            }
            else
            {
                // Domyślny typ wartości na podstawie wiersza w macierzy lub charakteru statystyki
                bool defaultPct = stat == RuneStatType.Range || stat == RuneStatType.Damage || stat == RuneStatType.FireRate;
                globalPenalties.Add(new GlobalPenaltyEntry
                {
                    enabled = true,
                    stat = stat,
                    valueType = defaultPct ? RuneValueType.Percent : RuneValueType.Flat,
                    range = new Vector2(10f, 20f)
                });
            }
        }
    }

    private RuneDefinitionSO GetRune(StatRowKey row, RuneRarity rarity)
    {
        if (!allRunes.TryGetValue(rarity, out var list)) return null;
        return list.FirstOrDefault(r => r != null && r.primaryStat == row.stat && r.valueType == row.valueType);
    }

    // =========================================================================
    // GŁÓWNA PĘTLA GUI
    // =========================================================================
    private void OnGUI()
    {
        DrawTopBar();

        if (showConfigSection)
        {
            DrawConfigDropWeightsSection();
        }

        EditorGUILayout.BeginHorizontal();

        // LEWA / CENTRALNA CZĘŚĆ: Macierz Run + Globalna Tabela Kar Cursed
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        DrawMatrixPanel();

        if (showGlobalPenaltiesSection)
        {
            GUILayout.Space(4);
            DrawGlobalPenaltiesPanel();
        }
        EditorGUILayout.EndVertical();

        // PRAWA CZĘŚĆ: Panel szczegółów zaznaczonej runy
        if (showDetailsPanel)
        {
            DrawDetailsPanel();
        }

        EditorGUILayout.EndHorizontal();

        DrawFooterStatus();
    }

    // =========================================================================
    // GÓRNY PASEK NARZĘDZI (2 Wiersze)
    // =========================================================================
    private void DrawTopBar()
    {
        // --- WIERSZ 1: Odświeżanie, Kreator całej ścieżki (5 tierów), Łańcuch widełek ---
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("🔄 Odśwież", EditorStyles.toolbarButton, GUILayout.Width(85)))
        {
            LoadAllRunes();
            InitGlobalPenaltiesFromExistingCursed();
            SetStatus("Odświeżono bazę plików run.", MessageType.Info);
        }

        GUILayout.Space(10);
        GUILayout.Label("Nowa Ścieżka Statystyki:", EditorStyles.miniBoldLabel, GUILayout.Width(145));

        creatorValueType = (RuneValueType)EditorGUILayout.EnumPopup(creatorValueType, EditorStyles.toolbarPopup, GUILayout.Width(70));
        creatorStat = (RuneStatType)EditorGUILayout.EnumPopup(creatorStat, EditorStyles.toolbarPopup, GUILayout.Width(135));

        GUILayout.Label("Common Min/Max:", EditorStyles.miniLabel, GUILayout.Width(100));
        creatorBaseRange.x = EditorGUILayout.FloatField(creatorBaseRange.x, EditorStyles.toolbarTextField, GUILayout.Width(38));
        creatorBaseRange.y = EditorGUILayout.FloatField(creatorBaseRange.y, EditorStyles.toolbarTextField, GUILayout.Width(38));

        GUILayout.Label("Mnożnik:", EditorStyles.miniLabel, GUILayout.Width(52));
        creatorSpanMultiplier = EditorGUILayout.FloatField(creatorSpanMultiplier, EditorStyles.toolbarTextField, GUILayout.Width(38));

        GUI.backgroundColor = new Color(0.35f, 0.85f, 0.45f);
        if (GUILayout.Button("➕ Utwórz ścieżkę (5 tierów)", EditorStyles.toolbarButton, GUILayout.Width(175)))
        {
            CreateFullStatPath(creatorStat, creatorValueType, creatorBaseRange, creatorSpanMultiplier);
        }
        GUI.backgroundColor = Color.white;

        GUILayout.FlexibleSpace();

        chainMinToPrevMax = GUILayout.Toggle(
            chainMinToPrevMax,
            new GUIContent("🔗 Łańcuch (Min = poprz. Max)", "Gdy włączone, zmiana Max w niższym tierze automatycznie aktualizuje Min w kolejnym tierze."),
            EditorStyles.toolbarButton,
            GUILayout.Width(185)
        );

        EditorGUILayout.EndHorizontal();

        // --- WIERSZ 2: Config, Auto-Ikony, Przełączniki Sekcji ---
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("Config:", GUILayout.Width(48));
        systemConfig = (RuneSystemConfigSO)EditorGUILayout.ObjectField(systemConfig, typeof(RuneSystemConfigSO), false, GUILayout.Width(200));

        autoSyncConfig = GUILayout.Toggle(
            autoSyncConfig,
            new GUIContent("Auto-Sync", "Automatycznie synchronizuj listy w RuneSystemConfigSO po dodaniu lub usunięciu runy."),
            EditorStyles.toolbarButton,
            GUILayout.Width(75)
        );

        EditorGUI.BeginDisabledGroup(systemConfig == null);
        if (GUILayout.Button("⚡ Zsynchronizuj Config", EditorStyles.toolbarButton, GUILayout.Width(145)))
        {
            SyncRunesToConfig(silent: false);
        }
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(8);

        if (GUILayout.Button(new GUIContent("🎨 Auto-przypisz ikony z Icons/Runes", "Dopasowuje ikony z folderu Assets/Main folder/Icons/Runes po rzadkości i statystyce."), EditorStyles.toolbarButton, GUILayout.Width(220)))
        {
            AutoAssignAllIcons();
        }

        GUILayout.FlexibleSpace();

        // Przełączniki widoczności paneli
        GUI.backgroundColor = showConfigSection ? new Color(0.6f, 0.85f, 1f) : Color.white;
        showConfigSection = GUILayout.Toggle(showConfigSection, "📊 Wagi i Szanse Dropu", EditorStyles.toolbarButton, GUILayout.Width(150));

        GUI.backgroundColor = showGlobalPenaltiesSection ? new Color(1f, 0.65f, 0.65f) : Color.white;
        showGlobalPenaltiesSection = GUILayout.Toggle(showGlobalPenaltiesSection, "💀 Globalne Kary Cursed", EditorStyles.toolbarButton, GUILayout.Width(155));

        GUI.backgroundColor = showDetailsPanel ? new Color(0.6f, 0.85f, 1f) : Color.white;
        showDetailsPanel = GUILayout.Toggle(showDetailsPanel, "🔍 Inspektor Runy", EditorStyles.toolbarButton, GUILayout.Width(125));
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================================
    // SEKCJA ZWIJANA: WAGI RZADKOŚCI I SZANSE NA DROP (RuneSystemConfigSO)
    // =========================================================================
    private void DrawConfigDropWeightsSection()
    {
        EditorGUILayout.BeginVertical("box");

        if (systemConfig == null)
        {
            EditorGUILayout.HelpBox("Brak podpiętego pliku RuneSystemConfigSO.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUI.BeginChangeCheck();
        Undo.RecordObject(systemConfig, "Edycja RuneSystemConfigSO");

        EditorGUILayout.BeginHorizontal();

        // Lewa kolumna: Szanse na drop z wrogów
        EditorGUILayout.BeginVertical("helpbox", GUILayout.Width(360));
        GUILayout.Label("Szanse na Drop z Wrogów", EditorStyles.boldLabel);
        systemConfig.dropChanceNormal = EditorGUILayout.Slider("Normalny wróg (%)", systemConfig.dropChanceNormal, 0f, 100f);
        systemConfig.dropChanceElite = EditorGUILayout.Slider("Elitarny wróg (%)", systemConfig.dropChanceElite, 0f, 100f);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Boss (liczba run Min / Max):", GUILayout.Width(170));
        systemConfig.minRunesFromBoss = Mathf.Max(0, EditorGUILayout.IntField(systemConfig.minRunesFromBoss, GUILayout.Width(50)));
        GUILayout.Label("–", GUILayout.Width(12));
        systemConfig.maxRunesFromBoss = Mathf.Max(systemConfig.minRunesFromBoss, EditorGUILayout.IntField(systemConfig.maxRunesFromBoss, GUILayout.Width(50)));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        GUILayout.Space(8);

        // Prawa kolumna: Wagi rzadkości z przeliczeniem na %
        EditorGUILayout.BeginVertical("helpbox", GUILayout.ExpandWidth(true));
        GUILayout.Label("Wagi Rzadkości (Bazowy rozkład prawdopodobieństwa)", EditorStyles.boldLabel);

        float totalWeight = Mathf.Max(0.0001f,
            systemConfig.weightCommon + systemConfig.weightUncommon +
            systemConfig.weightRare + systemConfig.weightLegendary + systemConfig.weightCursed);

        EditorGUILayout.BeginHorizontal();
        DrawWeightBox("Common", ref systemConfig.weightCommon, totalWeight, GetColorForRarity(RuneRarity.Common));
        DrawWeightBox("Uncommon", ref systemConfig.weightUncommon, totalWeight, GetColorForRarity(RuneRarity.Uncommon));
        DrawWeightBox("Rare", ref systemConfig.weightRare, totalWeight, GetColorForRarity(RuneRarity.Rare));
        DrawWeightBox("Legendary", ref systemConfig.weightLegendary, totalWeight, GetColorForRarity(RuneRarity.Legendary));
        DrawWeightBox("Cursed", ref systemConfig.weightCursed, totalWeight, GetColorForRarity(RuneRarity.Cursed));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(systemConfig);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawWeightBox(string label, ref float weight, float totalWeight, Color rarityColor)
    {
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = rarityColor;
        float pct = (weight / totalWeight) * 100f;
        GUILayout.Label($"{label} ({pct:0.0}%)", EditorStyles.miniButton);
        GUI.backgroundColor = Color.white;

        weight = Mathf.Max(0f, EditorGUILayout.FloatField("Waga:", weight));
        EditorGUILayout.EndVertical();
    }

    // =========================================================================
    // GŁÓWNA MACIERZ RUN (Wiersze = Statystyka + Typ, Kolumny = 5 Rzadkości)
    // =========================================================================
    private void DrawMatrixPanel()
    {
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        // Nagłówek tabeli
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Macierz Balansu Run (Edycja na żywo)", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label("Pomarańczowe pole = naruszona progresja względem niższego tieru | Czerwone = Min > Max", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        DrawSeparator();

        // Nagłówki kolumn
        const float statColWidth = 215f;
        EditorGUILayout.BeginHorizontal();
        GUILayout.BeginVertical("box", GUILayout.Width(statColWidth));
        GUILayout.Label("Ścieżka Statystyki / Generator", EditorStyles.boldLabel);
        GUILayout.EndVertical();

        foreach (RuneRarity rarity in AllRarities)
        {
            GUI.backgroundColor = GetColorForRarity(rarity);
            GUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.MinWidth(135));
            int count = allRunes.ContainsKey(rarity) ? allRunes[rarity].Count : 0;
            GUILayout.Label($"{rarity.ToString().ToUpper()} ({count})", EditorStyles.boldLabel);
            GUILayout.EndVertical();
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();

        // Zawartość macierzy
        matrixScrollPos = EditorGUILayout.BeginScrollView(matrixScrollPos, GUILayout.ExpandHeight(true));

        if (matrixRows.Count == 0)
        {
            EditorGUILayout.HelpBox("Brak utworzonych run. Użyj kreatora na górnym pasku, aby utworzyć pierwszą ścieżkę.", MessageType.Info);
        }

        for (int rIdx = 0; rIdx < matrixRows.Count; rIdx++)
        {
            StatRowKey row = matrixRows[rIdx];
            DrawMatrixRow(row, statColWidth);
            GUILayout.Space(2);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawMatrixRow(StatRowKey row, float statColWidth)
    {
        EditorGUILayout.BeginHorizontal("helpbox");

        // --- KOLUMNA 1: Nagłówek wiersza + Szybki Generator ---
        EditorGUILayout.BeginVertical(GUILayout.Width(statColWidth - 8f));

        bool isPct = RuneItem.IsPercentageStat(row.stat, row.valueType);
        string typeTag = row.valueType == RuneValueType.Percent ? "Percent (%)" : (isPct ? "Flat (w %)" : "Flat");

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"{row.stat}", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"[{typeTag}]", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        // Narzędzia szybkiego przeliczania wiersza
        if (!rowScaleMultipliers.ContainsKey(row)) rowScaleMultipliers[row] = 1.4f;

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Skala co tier:", EditorStyles.miniLabel, GUILayout.Width(72));
        rowScaleMultipliers[row] = EditorGUILayout.FloatField(rowScaleMultipliers[row], GUILayout.Width(38));

        if (GUILayout.Button(new GUIContent("⚡ Wylicz", "Wylicza Uncommon -> Cursed na podstawie zakresu Common oraz podanego mnożnika rozpiętości (zachowując Min = poprzedni Max)."), EditorStyles.miniButtonLeft, GUILayout.Width(58)))
        {
            ApplyRowProgression(row, rowScaleMultipliers[row]);
        }

        if (GUILayout.Button(new GUIContent("🔗", "Wyrównaj tylko Min = poprzedni Max w całym wierszu bez zmiany wartości Max."), EditorStyles.miniButtonRight, GUILayout.Width(26)))
        {
            ChainRowMinsToPrevMax(row);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        // --- KOLUMNY 2..6: Komórki dla poszczególnych Rzadkości ---
        RuneDefinitionSO prevRune = null;
        foreach (RuneRarity rarity in AllRarities)
        {
            RuneDefinitionSO rune = GetRune(row, rarity);
            DrawMatrixCell(row, rarity, rune, prevRune);
            if (rune != null) prevRune = rune;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawMatrixCell(StatRowKey row, RuneRarity rarity, RuneDefinitionSO rune, RuneDefinitionSO prevRune)
    {
        if (rune == null)
        {
            // Pusta komórka -> Przycisk szybkiego utworzenia brakującego tieru
            EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.MinWidth(135), GUILayout.Height(54));
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = new Color(0.75f, 0.9f, 0.75f);
            if (GUILayout.Button($"➕ Utwórz {rarity}", EditorStyles.miniButton))
            {
                Vector2 defaultRange = prevRune != null
                    ? new Vector2(prevRune.primaryValueRange.y, Mathf.Round(prevRune.primaryValueRange.y + Mathf.Max(1f, (prevRune.primaryValueRange.y - prevRune.primaryValueRange.x) * 1.35f)))
                    : new Vector2(5f, 10f);

                var created = CreateRuneAsset(rarity, row.stat, row.valueType, defaultRange);
                LoadAllRunes();
                if (autoSyncConfig) SyncRunesToConfig(silent: true);
                SelectRune(created);
            }
            GUI.backgroundColor = Color.white;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            return;
        }

        // Walidacja komórki
        bool invalidRange = rune.primaryValueRange.x > rune.primaryValueRange.y;
        bool brokenProgression = prevRune != null &&
            (rune.primaryValueRange.x < prevRune.primaryValueRange.x ||
             rune.primaryValueRange.y <= prevRune.primaryValueRange.y ||
             (chainMinToPrevMax && rune.primaryValueRange.x < prevRune.primaryValueRange.y));

        bool isSelected = selectedRune == rune;

        Color cellBg = Color.white;
        if (invalidRange) cellBg = new Color(1f, 0.45f, 0.45f);
        else if (brokenProgression) cellBg = new Color(1f, 0.8f, 0.4f);
        else if (isSelected) cellBg = new Color(0.55f, 0.85f, 1f);

        GUI.backgroundColor = cellBg;
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.MinWidth(135));
        GUI.backgroundColor = Color.white;

        // Wiersz górny komórki: Ikona (24x24) + Przycisk wyboru + info o karach Cursed
        EditorGUILayout.BeginHorizontal();

        // Podgląd małej ikony 24x24 px
        Rect iconRect = GUILayoutUtility.GetRect(24f, 24f, GUILayout.Width(24f), GUILayout.Height(24f));
        if (rune.icon != null)
        {
            DrawSpritePreview(iconRect, rune.icon);
        }
        else
        {
            EditorGUI.DrawRect(iconRect, new Color(0.25f, 0.25f, 0.25f));
            GUI.Label(iconRect, "?", EditorStyles.centeredGreyMiniLabel);
        }

        bool isPct = RuneItem.IsPercentageStat(rune.primaryStat, rune.valueType);
        string unit = isPct ? "%" : "";
        string headerBtnLabel = $"{rune.primaryValueRange.x:0.#}–{rune.primaryValueRange.y:0.#}{unit}";
        if (rarity == RuneRarity.Cursed)
        {
            int penCount = rune.possiblePenalties != null ? rune.possiblePenalties.Count : 0;
            headerBtnLabel += $" (💀{penCount})";
        }

        GUI.backgroundColor = isSelected ? new Color(0.4f, 0.75f, 1f) : GetColorForRarity(rarity);
        if (GUILayout.Button(new GUIContent(headerBtnLabel, $"Kliknij, aby edytować szczegóły: {rune.runeName}"), EditorStyles.miniButton, GUILayout.Height(22f)))
        {
            SelectRune(rune);
            showDetailsPanel = true;
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        // Wiersz dolny komórki: Pola Min i Max bezpośrednio w tabeli
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Min", EditorStyles.miniLabel, GUILayout.Width(24));
        float newMin = EditorGUILayout.FloatField(rune.primaryValueRange.x, GUILayout.MinWidth(34));

        GUILayout.Label("Max", EditorStyles.miniLabel, GUILayout.Width(26));
        float newMax = EditorGUILayout.FloatField(rune.primaryValueRange.y, GUILayout.MinWidth(34));
        EditorGUILayout.EndHorizontal();

        if (!Mathf.Approximately(newMin, rune.primaryValueRange.x) || !Mathf.Approximately(newMax, rune.primaryValueRange.y))
        {
            bool maxChanged = !Mathf.Approximately(newMax, rune.primaryValueRange.y);
            Undo.RecordObject(rune, "Zmiana widełek runy");
            rune.primaryValueRange = new Vector2(newMin, newMax);
            EditorUtility.SetDirty(rune);

            // Jeśli włączony jest łańcuch widełek i zmieniliśmy Max, automatycznie aktualizujemy Min kolejnego tieru
            if (chainMinToPrevMax && maxChanged && rarity < RuneRarity.Cursed)
            {
                RuneRarity nextRarity = rarity + 1;
                RuneDefinitionSO nextRune = GetRune(row, nextRarity);
                if (nextRune != null && !Mathf.Approximately(nextRune.primaryValueRange.x, newMax))
                {
                    Undo.RecordObject(nextRune, "Łańcuchowa aktualizacja Min kolejnego tieru");
                    float nextMax = Mathf.Max(newMax, nextRune.primaryValueRange.y);
                    nextRune.primaryValueRange = new Vector2(newMax, nextMax);
                    EditorUtility.SetDirty(nextRune);
                }
            }

            if (selectedRune == rune && serializedRune != null)
            {
                serializedRune.Update();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSpritePreview(Rect rect, Sprite sprite)
    {
        if (sprite == null || sprite.texture == null) return;
        Texture2D tex = sprite.texture;
        Rect texRect = sprite.textureRect;
        Rect uv = new Rect(
            texRect.x / tex.width,
            texRect.y / tex.height,
            texRect.width / tex.width,
            texRect.height / tex.height
        );
        GUI.DrawTextureWithTexCoords(rect, tex, uv, true);
    }

    // =========================================================================
    // SEKCJA DOLNA: GLOBALNY SZABLON KAR CURSED (MASOWE ZARZĄDZANIE)
    // =========================================================================
    private void DrawGlobalPenaltiesPanel()
    {
        EditorGUILayout.BeginVertical("box", GUILayout.Height(195));

        // Pasek tytułowy sekcji Cursed
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("💀 Globalny Szablon Kar dla Run Przeklętych (Cursed)", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button(new GUIContent("📥 Wczytaj z obecnych assetów Cursed", "Pobiera widełki kar z istniejących plików Cursed w projekcie."), EditorStyles.miniButton, GUILayout.Width(215)))
        {
            InitGlobalPenaltiesFromExistingCursed();
            SetStatus("Wczytano szablon kar z obecnych run Cursed.", MessageType.Info);
        }

        GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
        if (GUILayout.Button(new GUIContent("⚡ Zastosuj do WSZYSTKICH run Cursed (pomiń ich główny stat)", "Każda runa Cursed otrzyma pełną listę aktywnych kar poniżej, z automatycznym wykluczeniem jej własnej głównej statystyki."), EditorStyles.miniButton, GUILayout.Width(350)))
        {
            ApplyGlobalPenaltiesToAllCursedRunes();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        GUILayout.Label("Wartości wpisuj jako DODATNIE (np. 10 – 20). System w grze automatycznie zamienia je na ujemne debuffy.", EditorStyles.miniLabel);
        DrawSeparator();

        // Siatka 7 statystyk w 2 kolumnach (bardzo kompaktowa, bez scrollowania lub z lekkim scrollem)
        globalPenaltiesScrollPos = EditorGUILayout.BeginScrollView(globalPenaltiesScrollPos);

        int half = Mathf.CeilToInt(globalPenalties.Count / 2f);
        EditorGUILayout.BeginHorizontal();

        // Kolumna 1
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        for (int i = 0; i < half && i < globalPenalties.Count; i++)
        {
            DrawGlobalPenaltyRow(globalPenalties[i]);
        }
        EditorGUILayout.EndVertical();

        GUILayout.Space(12);

        // Kolumna 2
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        for (int i = half; i < globalPenalties.Count; i++)
        {
            DrawGlobalPenaltyRow(globalPenalties[i]);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
    }

    private void DrawGlobalPenaltyRow(GlobalPenaltyEntry entry)
    {
        EditorGUILayout.BeginHorizontal("helpbox");

        entry.enabled = EditorGUILayout.Toggle(entry.enabled, GUILayout.Width(16));

        EditorGUI.BeginDisabledGroup(!entry.enabled);
        GUILayout.Label(entry.stat.ToString(), EditorStyles.boldLabel, GUILayout.Width(120));
        entry.valueType = (RuneValueType)EditorGUILayout.EnumPopup(entry.valueType, GUILayout.Width(70));

        GUILayout.Label("Min:", EditorStyles.miniLabel, GUILayout.Width(28));
        entry.range.x = Mathf.Max(0f, EditorGUILayout.FloatField(entry.range.x, GUILayout.Width(45)));

        GUILayout.Label("Max:", EditorStyles.miniLabel, GUILayout.Width(30));
        entry.range.y = Mathf.Max(entry.range.x, EditorGUILayout.FloatField(entry.range.y, GUILayout.Width(45)));

        bool isPct = RuneItem.IsPercentageStat(entry.stat, entry.valueType);
        string unit = isPct ? "%" : "";
        GUILayout.Label($"(-{entry.range.x:0.#} do -{entry.range.y:0.#}{unit})", EditorStyles.miniLabel);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================================
    // PRAWY PANEL BOCZNY: SZCZEGÓŁY WYBRANEJ RUNY + PŁASKA TABELA KAR CURSED
    // =========================================================================
    private void DrawDetailsPanel()
    {
        EditorGUILayout.BeginVertical("box", GUILayout.Width(340), GUILayout.ExpandHeight(true));

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Inspektor Wybranej Runy", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("✖", EditorStyles.miniButton, GUILayout.Width(22)))
        {
            showDetailsPanel = false;
        }
        EditorGUILayout.EndHorizontal();
        DrawSeparator();

        if (selectedRune == null || serializedRune == null)
        {
            GUILayout.Label("Kliknij przycisk z wartością w dowolnej komórce macierzy, aby edytować szczegóły pojedynczej runy.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
            return;
        }

        detailsScrollPos = EditorGUILayout.BeginScrollView(detailsScrollPos);
        serializedRune.Update();

        // Nagłówek z kolorem rzadkości
        GUI.backgroundColor = GetColorForRarity(selectedRune.rarity);
        EditorGUILayout.BeginHorizontal("helpbox");
        GUI.backgroundColor = Color.white;

        if (selectedRune.icon != null)
        {
            Rect previewRect = GUILayoutUtility.GetRect(32f, 32f, GUILayout.Width(32f), GUILayout.Height(32f));
            DrawSpritePreview(previewRect, selectedRune.icon);
        }

        EditorGUILayout.BeginVertical();
        GUILayout.Label(selectedRune.runeName, EditorStyles.boldLabel);
        GUILayout.Label($"Plik: {selectedRune.name}", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(42)))
        {
            EditorGUIUtility.PingObject(selectedRune);
            Selection.activeObject = selectedRune;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);

        EditorGUILayout.PropertyField(serializedRune.FindProperty("runeName"), new GUIContent("Nazwa w grze"));
        EditorGUILayout.PropertyField(serializedRune.FindProperty("icon"), new GUIContent("Ikona"));

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.PropertyField(serializedRune.FindProperty("rarity"), new GUIContent("Rzadkość"));
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(8);
        GUILayout.Label("Główna Statystyka (Buff)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedRune.FindProperty("primaryStat"), new GUIContent("Statystyka"));
        EditorGUILayout.PropertyField(serializedRune.FindProperty("valueType"), new GUIContent("Typ Wartości"));

        SerializedProperty rangeProp = serializedRune.FindProperty("primaryValueRange");
        Vector2 rangeVal = rangeProp.vector2Value;
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Zakres (Min / Max):", GUILayout.Width(120));
        rangeVal.x = EditorGUILayout.FloatField(rangeVal.x);
        rangeVal.y = EditorGUILayout.FloatField(rangeVal.y);
        EditorGUILayout.EndHorizontal();
        rangeProp.vector2Value = rangeVal;

        serializedRune.ApplyModifiedProperties();

        // --- PŁASKA TABELA KAR DLA WYBRANEJ RUNY CURSED ---
        if (selectedRune.rarity == RuneRarity.Cursed)
        {
            GUILayout.Space(12);
            DrawSeparator();
            DrawSelectedCursedPenaltiesFlatEditor();
        }

        GUILayout.Space(20);
        DrawSeparator();

        // Usuwanie runy
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🎨 Dopasuj ikonę", GUILayout.Height(24)))
        {
            Sprite matched = FindMatchingIcon(selectedRune.rarity, selectedRune.primaryStat);
            if (matched != null)
            {
                Undo.RecordObject(selectedRune, "Dopasowanie ikony runy");
                selectedRune.icon = matched;
                EditorUtility.SetDirty(selectedRune);
                AssetDatabase.SaveAssets();
                SetStatus($"Przypisano ikonę '{matched.name}' do {selectedRune.runeName}.", MessageType.Info);
            }
            else
            {
                SetStatus("Nie znaleziono pasującej ikony w folderze Icons/Runes.", MessageType.Warning);
            }
        }

        GUILayout.FlexibleSpace();

        GUI.backgroundColor = new Color(0.9f, 0.25f, 0.25f);
        if (GUILayout.Button("🗑 Usuń tę runę", GUILayout.Width(130), GUILayout.Height(24)))
        {
            DeleteSelectedRune();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawSelectedCursedPenaltiesFlatEditor()
    {
        if (selectedRune.possiblePenalties == null)
        {
            selectedRune.possiblePenalties = new List<RuneDefinitionSO.CursedPenalty>();
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"Kary tej runy ({selectedRune.possiblePenalties.Count})", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button(new GUIContent("📋 Kopiuj", "Kopiuj listę kar tej runy do schowka."), EditorStyles.miniButtonLeft, GUILayout.Width(60)))
        {
            penaltyClipboard = new List<RuneDefinitionSO.CursedPenalty>(selectedRune.possiblePenalties);
            SetStatus($"Skopiowano {penaltyClipboard.Count} kar ze {selectedRune.name}.", MessageType.Info);
        }

        EditorGUI.BeginDisabledGroup(penaltyClipboard == null);
        if (GUILayout.Button(new GUIContent("📥 Wklej", "Wklej skopiowane kary (z pominięciem głównej statystyki tej runy)."), EditorStyles.miniButtonRight, GUILayout.Width(55)))
        {
            Undo.RecordObject(selectedRune, "Wklejenie kar Cursed");
            selectedRune.possiblePenalties = penaltyClipboard
                .Where(p => p.stat != selectedRune.primaryStat)
                .ToList();
            EditorUtility.SetDirty(selectedRune);
            serializedRune.Update();
            SetStatus($"Wklejono kary do {selectedRune.name}.", MessageType.Info);
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("🔄 Nadpisz z Globalnego Szablonu (bez głównego statu)", EditorStyles.miniButton))
        {
            ApplyGlobalPenaltiesToSingleRune(selectedRune);
            serializedRune.Update();
            SetStatus($"Zaktualizowano kary w {selectedRune.name} z globalnego szablonu.", MessageType.Info);
        }

        GUILayout.Space(4);

        // Płaska lista wierszy (bez konieczności rozwijania Element 0, Element 1...)
        int removeIndex = -1;
        EditorGUI.BeginChangeCheck();
        Undo.RecordObject(selectedRune, "Edycja listy kar Cursed");

        for (int i = 0; i < selectedRune.possiblePenalties.Count; i++)
        {
            var pen = selectedRune.possiblePenalties[i];
            bool sameAsPrimary = pen.stat == selectedRune.primaryStat;

            GUI.backgroundColor = sameAsPrimary ? new Color(1f, 0.55f, 0.55f) : Color.white;
            EditorGUILayout.BeginHorizontal("helpbox");
            GUI.backgroundColor = Color.white;

            pen.stat = (RuneStatType)EditorGUILayout.EnumPopup(pen.stat, GUILayout.Width(115));
            pen.valueType = (RuneValueType)EditorGUILayout.EnumPopup(pen.valueType, GUILayout.Width(62));
            pen.penaltyRange.x = EditorGUILayout.FloatField(pen.penaltyRange.x, GUILayout.Width(38));
            GUILayout.Label("–", GUILayout.Width(10));
            pen.penaltyRange.y = EditorGUILayout.FloatField(pen.penaltyRange.y, GUILayout.Width(38));

            if (GUILayout.Button("✖", EditorStyles.miniButton, GUILayout.Width(22)))
            {
                removeIndex = i;
            }

            EditorGUILayout.EndHorizontal();
            selectedRune.possiblePenalties[i] = pen;
        }

        if (removeIndex >= 0)
        {
            selectedRune.possiblePenalties.RemoveAt(removeIndex);
            EditorUtility.SetDirty(selectedRune);
        }
        else if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(selectedRune);
        }

        if (GUILayout.Button("➕ Dodaj wiersz kary", EditorStyles.miniButton))
        {
            Undo.RecordObject(selectedRune, "Dodanie kary Cursed");
            selectedRune.possiblePenalties.Add(new RuneDefinitionSO.CursedPenalty
            {
                stat = RuneStatType.Range,
                valueType = RuneValueType.Percent,
                penaltyRange = new Vector2(10f, 20f)
            });
            EditorUtility.SetDirty(selectedRune);
        }
    }

    // =========================================================================
    // PASEK STATUSU NA DOLE OKNA
    // =========================================================================
    private void DrawFooterStatus()
    {
        if (!string.IsNullOrEmpty(statusMessage) && EditorApplication.timeSinceStartup < statusClearTime)
        {
            EditorGUILayout.HelpBox(statusMessage, statusType);
        }
    }

    // =========================================================================
    // LOGIKA BIZNESOWA: GENERATOR WIERSZA, TWORZENIE ŚCIEŻKI, IKONY, KARY, SYNC
    // =========================================================================

    private void SelectRune(RuneDefinitionSO rune)
    {
        selectedRune = rune;
        serializedRune = rune != null ? new SerializedObject(selectedRune) : null;
        GUI.FocusControl(null);
    }

    private void ApplyRowProgression(StatRowKey row, float spanMultiplier)
    {
        RuneDefinitionSO common = GetRune(row, RuneRarity.Common);
        if (common == null)
        {
            SetStatus($"Brak runy Common dla {row.stat} ({row.valueType}) – utwórz najpierw Common.", MessageType.Warning);
            return;
        }

        bool roundToInt = RuneItem.IsPercentageStat(row.stat, row.valueType) || common.primaryValueRange.x >= 1f;
        float currentMin = common.primaryValueRange.x;
        float currentMax = common.primaryValueRange.y;
        float currentSpan = Mathf.Max(0.5f, currentMax - currentMin);

        for (int i = 1; i < AllRarities.Length; i++)
        {
            RuneRarity rarity = AllRarities[i];
            RuneDefinitionSO rune = GetRune(row, rarity);

            currentMin = currentMax;
            currentSpan *= Mathf.Max(1f, spanMultiplier);
            float rawMax = currentMin + currentSpan;
            currentMax = roundToInt ? Mathf.Round(rawMax) : (float)Math.Round(rawMax, 1);
            if (currentMax <= currentMin) currentMax = currentMin + (roundToInt ? 1f : 0.5f);

            if (rune != null)
            {
                Undo.RecordObject(rune, "Przeliczenie progresji wiersza");
                rune.primaryValueRange = new Vector2(currentMin, currentMax);
                EditorUtility.SetDirty(rune);
            }
        }

        AssetDatabase.SaveAssets();
        if (selectedRune != null && serializedRune != null) serializedRune.Update();
        SetStatus($"Przeliczono progresję wiersza {row.stat} ({row.valueType}) przy mnożniku x{spanMultiplier:0.##}.", MessageType.Info);
    }

    private void ChainRowMinsToPrevMax(StatRowKey row)
    {
        RuneDefinitionSO prev = null;
        foreach (RuneRarity rarity in AllRarities)
        {
            RuneDefinitionSO current = GetRune(row, rarity);
            if (current != null && prev != null)
            {
                Undo.RecordObject(current, "Wyrównanie Min = poprzedni Max");
                float newMin = prev.primaryValueRange.y;
                float newMax = Mathf.Max(newMin, current.primaryValueRange.y);
                current.primaryValueRange = new Vector2(newMin, newMax);
                EditorUtility.SetDirty(current);
            }
            if (current != null) prev = current;
        }

        AssetDatabase.SaveAssets();
        if (selectedRune != null && serializedRune != null) serializedRune.Update();
        SetStatus($"Wyrównano łańcuch Min = poprzedni Max w wierszu {row.stat}.", MessageType.Info);
    }

    private void CreateFullStatPath(RuneStatType stat, RuneValueType valueType, Vector2 baseRange, float spanMultiplier)
    {
        var rowKey = new StatRowKey(stat, valueType);
        bool roundToInt = RuneItem.IsPercentageStat(stat, valueType) || baseRange.x >= 1f;

        float currentMin = baseRange.x;
        float currentMax = Mathf.Max(baseRange.x, baseRange.y);
        float currentSpan = Mathf.Max(1f, currentMax - currentMin);

        int createdCount = 0;
        RuneDefinitionSO firstCreated = null;

        for (int i = 0; i < AllRarities.Length; i++)
        {
            RuneRarity rarity = AllRarities[i];
            if (i > 0)
            {
                currentMin = currentMax;
                currentSpan *= Mathf.Max(1f, spanMultiplier);
                float rawMax = currentMin + currentSpan;
                currentMax = roundToInt ? Mathf.Round(rawMax) : (float)Math.Round(rawMax, 1);
            }

            RuneDefinitionSO existing = GetRune(rowKey, rarity);
            if (existing == null)
            {
                var created = CreateRuneAsset(rarity, stat, valueType, new Vector2(currentMin, currentMax));
                if (firstCreated == null) firstCreated = created;
                createdCount++;
            }
        }

        LoadAllRunes();
        if (autoSyncConfig) SyncRunesToConfig(silent: true);
        if (firstCreated != null) SelectRune(firstCreated);

        SetStatus(createdCount > 0
            ? $"Utworzono {createdCount} brakujących tierów dla ścieżki {valueType} {stat} i zsynchronizowano Config."
            : $"Ścieżka {valueType} {stat} posiada już wszystkie 5 tierów.", MessageType.Info);
    }

    private RuneDefinitionSO CreateRuneAsset(RuneRarity rarity, RuneStatType stat, RuneValueType valueType, Vector2 range)
    {
        string generatedName = $"{rarity} {valueType} {stat}";
        string safeFileName = generatedName.Replace(" ", "_");
        string folder = $"{BASE_PATH}/{folderMap[rarity]}";

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string path = $"{folder}/{safeFileName}.asset";
        if (File.Exists(path))
        {
            int suffix = 1;
            string altPath;
            do
            {
                altPath = $"{folder}/{safeFileName}_{suffix}.asset";
                suffix++;
            } while (File.Exists(altPath));
            path = altPath;
        }

        RuneDefinitionSO newRune = CreateInstance<RuneDefinitionSO>();
        newRune.runeName = generatedName;
        newRune.rarity = rarity;
        newRune.primaryStat = stat;
        newRune.valueType = valueType;
        newRune.primaryValueRange = range;
        newRune.icon = FindMatchingIcon(rarity, stat);

        if (rarity == RuneRarity.Cursed)
        {
            ApplyGlobalPenaltiesToSingleRune(newRune, saveImmediate: false);
        }

        AssetDatabase.CreateAsset(newRune, path);
        AssetDatabase.SaveAssets();
        return newRune;
    }

    private void ApplyGlobalPenaltiesToAllCursedRunes()
    {
        if (!allRunes.TryGetValue(RuneRarity.Cursed, out var cursedList) || cursedList.Count == 0)
        {
            SetStatus("Brak run Cursed w projekcie.", MessageType.Warning);
            return;
        }

        int updated = 0;
        foreach (var cRune in cursedList)
        {
            if (cRune == null) continue;
            ApplyGlobalPenaltiesToSingleRune(cRune, saveImmediate: false);
            updated++;
        }

        AssetDatabase.SaveAssets();
        if (selectedRune != null && serializedRune != null) serializedRune.Update();
        SetStatus($"Zastosowano globalny szablon kar do {updated} run Cursed (z automatycznym pominięciem ich własnej statystyki).", MessageType.Info);
    }

    private void ApplyGlobalPenaltiesToSingleRune(RuneDefinitionSO cRune, bool saveImmediate = true)
    {
        if (cRune == null) return;
        Undo.RecordObject(cRune, "Zastosowanie globalnego szablonu kar Cursed");

        cRune.possiblePenalties = new List<RuneDefinitionSO.CursedPenalty>();
        foreach (var entry in globalPenalties)
        {
            if (!entry.enabled) continue;
            if (entry.stat == cRune.primaryStat) continue; // Wykluczamy własną statystykę główną runy

            cRune.possiblePenalties.Add(new RuneDefinitionSO.CursedPenalty
            {
                stat = entry.stat,
                valueType = entry.valueType,
                penaltyRange = entry.range
            });
        }

        EditorUtility.SetDirty(cRune);
        if (saveImmediate) AssetDatabase.SaveAssets();
    }

    private void AutoAssignAllIcons()
    {
        int assignedCount = 0;
        foreach (RuneRarity rarity in AllRarities)
        {
            if (!allRunes.TryGetValue(rarity, out var list)) continue;
            foreach (var rune in list)
            {
                if (rune == null) continue;
                Sprite matched = FindMatchingIcon(rune.rarity, rune.primaryStat);
                if (matched != null && rune.icon != matched)
                {
                    Undo.RecordObject(rune, "Auto-przypisanie ikony runy");
                    rune.icon = matched;
                    EditorUtility.SetDirty(rune);
                    assignedCount++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        if (selectedRune != null && serializedRune != null) serializedRune.Update();
        SetStatus($"Automatycznie zaktualizowano ikony w {assignedCount} runach z folderu {ICONS_PATH}.", MessageType.Info);
    }

    private Sprite FindMatchingIcon(RuneRarity rarity, RuneStatType stat)
    {
        if (!Directory.Exists(ICONS_PATH)) return null;

        // Mapowanie nazw statystyk na nazwy plików w Assets/Main folder/Icons/Runes
        string[] statAliases = stat switch
        {
            RuneStatType.FireRate => new[] { "Attack Speed", "FireRate", "Fire Rate" },
            RuneStatType.CriticalChance => new[] { "Critical Chance", "CriticalChance" },
            RuneStatType.CriticalDamage => new[] { "Critical Damage", "CriticalDamage" },
            RuneStatType.ArmorPenetration => new[] { "Armor Penetration", "ArmorPenetration", "Damage" },
            RuneStatType.MagicPenetration => new[] { "Magic Penetration", "MagicPenetration", "Damage" },
            _ => new[] { stat.ToString() }
        };

        foreach (string alias in statAliases)
        {
            string candidatePath = $"{ICONS_PATH}/{rarity}_{alias}.png";
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(candidatePath);
            if (s != null) return s;
        }

        // Fallback: pierwsza znaleziona ikona danego tieru w folderze
        string[] guids = AssetDatabase.FindAssets($"{rarity}_ t:Sprite", new[] { ICONS_PATH });
        if (guids.Length > 0)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        return null;
    }

    private void DeleteSelectedRune()
    {
        if (selectedRune == null) return;

        bool confirm = EditorUtility.DisplayDialog(
            "Usuń Runę",
            $"Czy na pewno chcesz TRWALE USUNĄĆ runę '{selectedRune.runeName}'?\n\nTej operacji nie można cofnąć!",
            "Tak, usuń",
            "Anuluj"
        );

        if (confirm)
        {
            string removedName = selectedRune.runeName;
            string path = AssetDatabase.GetAssetPath(selectedRune);
            selectedRune = null;
            serializedRune = null;

            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            LoadAllRunes();
            if (autoSyncConfig) SyncRunesToConfig(silent: true);

            SetStatus($"Usunięto runę '{removedName}' i zaktualizowano Config.", MessageType.Info);
        }
    }

    private void SyncRunesToConfig(bool silent)
    {
        if (systemConfig == null) return;

        LoadAllRunes();
        Undo.RecordObject(systemConfig, "Zaktualizowano listy run w Configu");

        systemConfig.commonRunes = new List<RuneDefinitionSO>(allRunes[RuneRarity.Common]);
        systemConfig.uncommonRunes = new List<RuneDefinitionSO>(allRunes[RuneRarity.Uncommon]);
        systemConfig.rareRunes = new List<RuneDefinitionSO>(allRunes[RuneRarity.Rare]);
        systemConfig.legendaryRunes = new List<RuneDefinitionSO>(allRunes[RuneRarity.Legendary]);
        systemConfig.cursedRunes = new List<RuneDefinitionSO>(allRunes[RuneRarity.Cursed]);

        EditorUtility.SetDirty(systemConfig);
        AssetDatabase.SaveAssets();

        int totalRunes = systemConfig.commonRunes.Count + systemConfig.uncommonRunes.Count +
                         systemConfig.rareRunes.Count + systemConfig.legendaryRunes.Count +
                         systemConfig.cursedRunes.Count;

        if (!silent)
        {
            SetStatus($"Zsynchronizowano {totalRunes} run z plikiem {systemConfig.name}.", MessageType.Info);
        }
    }

    private void DrawSeparator()
    {
        EditorGUILayout.Space(3);
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f));
        EditorGUILayout.Space(3);
    }

    private Color GetColorForRarity(RuneRarity rarity)
    {
        return rarity switch
        {
            RuneRarity.Common => new Color(0.82f, 0.82f, 0.82f),
            RuneRarity.Uncommon => new Color(0.35f, 0.85f, 0.35f),
            RuneRarity.Rare => new Color(0.35f, 0.65f, 1f),
            RuneRarity.Legendary => new Color(1f, 0.82f, 0.25f),
            RuneRarity.Cursed => new Color(0.95f, 0.3f, 0.3f),
            _ => Color.white
        };
    }
}
#endif