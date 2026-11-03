using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class WaveManagerEditorWindow : EditorWindow
{
    private const string WavesFolderPath = "Assets/Main folder/ScriptableObjects/PredefinedWaves";
    private const string EnemiesFolderPath = "Assets/Main folder/ScriptableObjects/Enemies";

    private enum WindowTab
    {
        WaveManager = 0,
        EnemyAndTierEditor = 1,
        DifficultyOverview = 2
    }

    [SerializeField] private WindowTab currentTab = WindowTab.WaveManager;

    // Assets cache
    private List<WaveDefinition> allWaves = new List<WaveDefinition>();
    private List<EnemyData> allEnemies = new List<EnemyData>();

    // Wave Manager state
    private WaveDefinition selectedWave;
    private Vector2 waveListScrollPos;
    private Vector2 waveDetailsScrollPos;
    private bool showSpawnSimulation = false;
    private int simulationSeed = 42;

    // Quick-add palette state
    private int quickAddEnemyIndex = 0;
    private int quickAddTier = 1;
    private int quickAddCount = 5;
    private bool quickAddForceElite = false;

    // Enemy Editor state
    private EnemyData selectedEnemy;
    private Vector2 enemyListScrollPos;
    private Vector2 enemyDetailsScrollPos;
    private string enemySearchText = "";
    private float autoScaleHpMultiplier = 1.5f;
    private float autoScaleArmorStep = 5f;
    private float autoScaleMrStep = 5f;

    // Difficulty Overview state
    private Vector2 overviewScrollPos;

    [MenuItem("Tools/Enemies & Waves/Wave & Enemy Manager", priority = 30)]
    public static void ShowWindow()
    {
        var window = GetWindow<WaveManagerEditorWindow>("Wave & Enemy Manager");
        window.minSize = new Vector2(980, 640);
    }

    [MenuItem("Tools/Enemies & Waves/Migrate Enemy Tiers and Waves", priority = 31)]
    public static void RunMigrationMenuItem()
    {
        EnemyTierMigrationTool.MigrateAll();
    }

    private void OnEnable()
    {
        RefreshAssets();
    }

    private void RefreshAssets()
    {
        // 1. Load Waves
        allWaves.Clear();
        string[] waveGuids = AssetDatabase.FindAssets("t:WaveDefinition", new[] { WavesFolderPath });
        if (waveGuids.Length == 0)
        {
            waveGuids = AssetDatabase.FindAssets("t:WaveDefinition");
        }

        foreach (string guid in waveGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var wave = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path);
            if (wave != null) allWaves.Add(wave);
        }
        allWaves = allWaves.OrderBy(w => w.dayNumber).ThenBy(w => w.name).ToList();

        // 2. Load Enemies
        allEnemies.Clear();
        string[] enemyGuids = AssetDatabase.FindAssets("t:EnemyData", new[] { EnemiesFolderPath });
        if (enemyGuids.Length == 0)
        {
            enemyGuids = AssetDatabase.FindAssets("t:EnemyData");
        }

        foreach (string guid in enemyGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (enemy != null)
            {
                enemy.EnsureTiers();
                allEnemies.Add(enemy);
            }
        }
        allEnemies = allEnemies.OrderBy(e => e.threatCost).ThenBy(e => e.name).ToList();

        // Defaults selection
        if (selectedWave == null && allWaves.Count > 0)
        {
            selectedWave = allWaves[0];
        }
        if (selectedEnemy == null && allEnemies.Count > 0)
        {
            selectedEnemy = allEnemies[0];
        }
    }

    private void OnGUI()
    {
        DrawTopToolbar();

        switch (currentTab)
        {
            case WindowTab.WaveManager:
                DrawWaveManagerTab();
                break;
            case WindowTab.EnemyAndTierEditor:
                DrawEnemyEditorTab();
                break;
            case WindowTab.DifficultyOverview:
                DrawDifficultyOverviewTab();
                break;
        }
    }

    // =========================================================================
    // TOP TOOLBAR
    // =========================================================================
    private void DrawTopToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Tab selection buttons
        GUI.backgroundColor = currentTab == WindowTab.WaveManager ? new Color(0.35f, 0.65f, 1f) : Color.white;
        if (GUILayout.Button("🌊 Zarządzanie Falami", EditorStyles.toolbarButton, GUILayout.Width(170)))
        {
            currentTab = WindowTab.WaveManager;
        }

        GUI.backgroundColor = currentTab == WindowTab.EnemyAndTierEditor ? new Color(0.35f, 0.65f, 1f) : Color.white;
        if (GUILayout.Button("👾 Wrogowie i Tiery", EditorStyles.toolbarButton, GUILayout.Width(170)))
        {
            currentTab = WindowTab.EnemyAndTierEditor;
        }

        GUI.backgroundColor = currentTab == WindowTab.DifficultyOverview ? new Color(0.35f, 0.65f, 1f) : Color.white;
        if (GUILayout.Button("📊 Krzywa Trudności", EditorStyles.toolbarButton, GUILayout.Width(170)))
        {
            currentTab = WindowTab.DifficultyOverview;
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(20);

        if (GUILayout.Button("Odśwież Assety", EditorStyles.toolbarButton, GUILayout.Width(110)))
        {
            RefreshAssets();
        }

        if (GUILayout.Button("Migracja Tierów", EditorStyles.toolbarButton, GUILayout.Width(110)))
        {
            EnemyTierMigrationTool.MigrateAll();
            RefreshAssets();
        }

        GUILayout.FlexibleSpace();

        GUI.backgroundColor = new Color(0.35f, 0.85f, 0.45f);
        if (GUILayout.Button("Synchronizuj z EnemySpawner na Scenie", EditorStyles.toolbarButton, GUILayout.Width(250)))
        {
            SyncWithSceneSpawner();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================================
    // TAB 1: ZARZĄDZANIE FALAMI (WAVE MANAGER)
    // =========================================================================
    private void DrawWaveManagerTab()
    {
        DrawWaveValidationBanner();

        EditorGUILayout.BeginHorizontal();
        DrawWaveListPanel();
        DrawWaveDetailsPanel();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawWaveValidationBanner()
    {
        if (allWaves.Count == 0)
        {
            EditorGUILayout.HelpBox("Brak zdefiniowanych fal w projekcie. Kliknij '+ Nowa Fala', aby utworzyć pierwszą falę.", MessageType.Warning);
            return;
        }

        var duplicateDays = allWaves.GroupBy(w => w.dayNumber).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        int maxDay = allWaves.Max(w => w.dayNumber);
        var existingDays = new HashSet<int>(allWaves.Select(w => w.dayNumber));
        var missingDays = new List<int>();
        for (int d = 1; d <= maxDay; d++)
        {
            if (!existingDays.Contains(d)) missingDays.Add(d);
        }

        if (duplicateDays.Count > 0)
        {
            EditorGUILayout.HelpBox($"Uwaga! Zduplikowane numery dni w falach: Dzień {string.Join(", ", duplicateDays)}", MessageType.Error);
        }
        else if (missingDays.Count > 0)
        {
            EditorGUILayout.HelpBox($"Brakujące definicje dla dni: {string.Join(", ", missingDays)} (Spawner użyje wcześniejszej fali jako fallbacku).", MessageType.Info);
        }
    }

    private void DrawWaveListPanel()
    {
        EditorGUILayout.BeginVertical("box", GUILayout.Width(290), GUILayout.ExpandHeight(true));

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Predefiniowane Fale ({allWaves.Count})", EditorStyles.boldLabel);
        if (GUILayout.Button("+ Nowa", GUILayout.Width(65)))
        {
            CreateNewWaveAsset();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        waveListScrollPos = EditorGUILayout.BeginScrollView(waveListScrollPos);

        foreach (var wave in allWaves)
        {
            if (wave == null) continue;

            int totalCount = wave.enemies != null ? wave.enemies.Sum(e => Mathf.Max(0, e.count)) : 0;
            float totalHp = CalculateWaveTotalHp(wave);
            bool isSelected = selectedWave == wave;

            GUI.backgroundColor = isSelected ? new Color(0.25f, 0.55f, 0.9f) : Color.white;
            EditorGUILayout.BeginHorizontal("button");

            string btnText = $"Dzień {wave.dayNumber}  ({totalCount} wrogów • {totalHp:0.#} HP)";
            if (GUILayout.Button(btnText, EditorStyles.label, GUILayout.ExpandWidth(true)))
            {
                selectedWave = wave;
                GUI.FocusControl(null);
            }

            if (GUILayout.Button("◎", GUILayout.Width(24)))
            {
                EditorGUIUtility.PingObject(wave);
            }

            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawWaveDetailsPanel()
    {
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (selectedWave == null)
        {
            EditorGUILayout.LabelField("Wybierz falę z listy po lewej stronie.", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
            return;
        }

        waveDetailsScrollPos = EditorGUILayout.BeginScrollView(waveDetailsScrollPos);

        EditorGUI.BeginChangeCheck();
        Undo.RecordObject(selectedWave, "Edit Wave Definition");

        // Header
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Edycja Fali: {selectedWave.name}", EditorStyles.boldLabel);

        if (GUILayout.Button("Duplikuj falę", GUILayout.Width(110)))
        {
            DuplicateSelectedWave();
        }

        if (GUILayout.Button("Zmień nazwę na 'Wave day X'", GUILayout.Width(190)))
        {
            RenameWaveAssetToMatchDay(selectedWave);
        }

        GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);
        if (GUILayout.Button("Usuń falę", GUILayout.Width(90)))
        {
            if (EditorUtility.DisplayDialog("Usuwanie fali", $"Czy na pewno usunąć {selectedWave.name}?", "Tak, usuń", "Anuluj"))
            {
                DeleteWaveAsset(selectedWave);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // Section 1: Basic wave parameters
        EditorGUILayout.BeginVertical("helpbox");
        EditorGUILayout.LabelField("Parametry Fali i Środowiska", EditorStyles.boldLabel);
        selectedWave.dayNumber = Mathf.Max(1, EditorGUILayout.IntField("Numer Dnia (dayNumber)", selectedWave.dayNumber));
        selectedWave.waveMessage = EditorGUILayout.TextField("Komunikat Fali (waveMessage)", selectedWave.waveMessage);
        selectedWave.etherPool = Mathf.Max(0, EditorGUILayout.IntField("Pula Etheru Fali (etherPool)", selectedWave.etherPool));
        selectedWave.customSpawnInterval = Mathf.Max(0f, EditorGUILayout.FloatField("Interwał Spawnu (0 = domyślny 1s)", selectedWave.customSpawnInterval));
        selectedWave.shuffleEnemies = EditorGUILayout.Toggle("Wymieszaj Kolejność (Shuffle)", selectedWave.shuffleEnemies);
        selectedWave.allowRandomElites = EditorGUILayout.Toggle("Losowe Elity wg EnemyData", selectedWave.allowRandomElites);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        // Section 2: Summary, EHP and Balance Box
        DrawWaveSummaryAndBalanceBox(selectedWave);

        EditorGUILayout.Space(8);

        // Section 3: Quick-add Palette
        DrawQuickAddPalette(selectedWave);

        EditorGUILayout.Space(8);

        // Section 4: Batch Operations Bar
        DrawBatchOperationsBar(selectedWave);

        EditorGUILayout.Space(8);

        // Section 5: Wave entries list
        DrawWaveEntriesList(selectedWave);

        EditorGUILayout.Space(8);

        // Section 6: Simulation / Order Preview
        DrawSpawnSimulationSection(selectedWave);

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(selectedWave);
            allWaves = allWaves.OrderBy(w => w.dayNumber).ThenBy(w => w.name).ToList();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawWaveSummaryAndBalanceBox(WaveDefinition wave)
    {
        int totalEnemies = 0;
        int totalElites = 0;
        float totalRawHp = 0f;
        float totalEhp = 0f;
        Dictionary<string, int> countByType = new Dictionary<string, int>();

        if (wave.enemies != null)
        {
            foreach (var e in wave.enemies)
            {
                if (e.enemy == null || e.count <= 0) continue;
                totalEnemies += e.count;
                if (e.forceElite) totalElites += e.count;

                var tierStats = e.enemy.GetTier(e.Tier);
                float baseHp = tierStats != null ? tierStats.baseHp : e.enemy.baseHp;
                float armor = tierStats != null ? tierStats.armor : e.enemy.armor;
                float mr = tierStats != null ? tierStats.magicResist : e.enemy.magicResist;
                float mult = e.forceElite ? e.enemy.eliteHpMultiplier : 1f;

                float unitHp = baseHp * mult;
                totalRawHp += unitHp * e.count;

                // EHP factoring avg mitigation (physical and magical)
                float avgMitigation = Mathf.Clamp01((armor + mr) / 200f);
                float ehpMultiplier = avgMitigation >= 0.95f ? 20f : 1f / (1f - avgMitigation);
                totalEhp += unitHp * ehpMultiplier * e.count;

                string enemyName = !string.IsNullOrEmpty(e.enemy.enemyName) ? e.enemy.enemyName : e.enemy.name;
                if (!countByType.ContainsKey(enemyName)) countByType[enemyName] = 0;
                countByType[enemyName] += e.count;
            }
        }

        float interval = wave.customSpawnInterval > 0f ? wave.customSpawnInterval : 1.0f;
        float spawnDuration = totalEnemies * interval;

        int eth80 = Mathf.RoundToInt(wave.etherPool * 0.8f);
        int eth60 = Mathf.RoundToInt(wave.etherPool * 0.6f);
        int eth40 = Mathf.RoundToInt(wave.etherPool * 0.4f);
        int eth20 = Mathf.RoundToInt(wave.etherPool * 0.2f);

        int maxDefinedDay = allWaves.Count > 0 ? allWaves.Max(w => w.dayNumber) : 0;

        EditorGUILayout.BeginVertical("helpbox");
        EditorGUILayout.LabelField("Podsumowanie Balansu Fali", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Łącznie wrogów: {totalEnemies} (Elit: {totalElites})", GUILayout.Width(220));
        EditorGUILayout.LabelField($"Surowe HP: {totalRawHp:0.#}", GUILayout.Width(180));
        EditorGUILayout.LabelField($"Efektywne EHP: ~{totalEhp:0.#}", GUILayout.Width(200));
        EditorGUILayout.LabelField($"Czas spawnu: ~{spawnDuration:0.#}s ({interval:0.##}s/wroga)");
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField($"Pula Etheru: {wave.etherPool} szt.  |  Gwarant wg Ognia: L1: {eth80} (80%)  •  L2: {eth60} (60%)  •  L3: {eth40} (40%)  •  L4: {eth20} (20%)  •  L5: 0");

        // Enemy type proportions
        if (totalEnemies > 0)
        {
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Rozkład typów wrogów:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            foreach (var kvp in countByType)
            {
                float pct = (float)kvp.Value / totalEnemies * 100f;
                EditorGUILayout.LabelField($"• {kvp.Key}: {kvp.Value} ({pct:0}%)", EditorStyles.miniLabel, GUILayout.Width(150));
            }
            EditorGUILayout.EndHorizontal();
        }

        if (maxDefinedDay > 0 && wave.dayNumber == maxDefinedDay)
        {
            float hpPlus1 = totalRawHp * Mathf.Pow(1.1f, 1);
            float hpPlus5 = totalRawHp * Mathf.Pow(1.1f, 5);
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"Skalowanie late-game (1.1^a po Dniu {maxDefinedDay}): Dzień {maxDefinedDay + 1}: ×1.10 HP (~{hpPlus1:0.#})  |  Dzień {maxDefinedDay + 5}: ×1.61 HP (~{hpPlus5:0.#})", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawQuickAddPalette(WaveDefinition wave)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("⚡ Paleta Dodawania Jednostek do Fali", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (allEnemies.Count > 0)
        {
            string[] enemyNames = allEnemies.Select(e => !string.IsNullOrEmpty(e.enemyName) ? e.enemyName : e.name).ToArray();
            quickAddEnemyIndex = Mathf.Clamp(quickAddEnemyIndex, 0, allEnemies.Count - 1);

            EditorGUILayout.LabelField("Wróg:", GUILayout.Width(35));
            quickAddEnemyIndex = EditorGUILayout.Popup(quickAddEnemyIndex, enemyNames, GUILayout.Width(160));

            EditorGUILayout.LabelField("Tier:", GUILayout.Width(32));
            quickAddTier = Mathf.Clamp(EditorGUILayout.IntField(quickAddTier, GUILayout.Width(40)), 1, 4);

            EditorGUILayout.LabelField("Ilość:", GUILayout.Width(35));
            quickAddCount = Mathf.Max(1, EditorGUILayout.IntField(quickAddCount, GUILayout.Width(45)));

            quickAddForceElite = EditorGUILayout.ToggleLeft("Elite", quickAddForceElite, GUILayout.Width(55));

            GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
            if (GUILayout.Button("+ Dodaj grupę", GUILayout.Height(22)))
            {
                if (wave.enemies == null) wave.enemies = new List<WaveDefinition.WaveEntry>();
                wave.enemies.Add(new WaveDefinition.WaveEntry
                {
                    enemy = allEnemies[quickAddEnemyIndex],
                    tier = quickAddTier,
                    count = quickAddCount,
                    forceElite = quickAddForceElite
                });
                EditorUtility.SetDirty(wave);
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            EditorGUILayout.HelpBox("Brak dostępnych wrogów w projekcie. Utwórz wrogów w zakładce 'Wrogowie i Tiery'.", MessageType.Info);
        }

        EditorGUILayout.EndHorizontal();

        // Quick buttons for one-click add
        if (allEnemies.Count > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Szybki klik (z wybranym Tierem i Ilością):", EditorStyles.miniLabel, GUILayout.Width(240));
            foreach (var enemy in allEnemies)
            {
                if (enemy == null) continue;
                string label = !string.IsNullOrEmpty(enemy.enemyName) ? enemy.enemyName : enemy.name;
                var tStats = enemy.GetTier(quickAddTier);
                float hp = tStats != null ? tStats.baseHp : 100f;

                if (GUILayout.Button($"+ {label} (T{quickAddTier})", EditorStyles.miniButton, GUILayout.Height(20)))
                {
                    if (wave.enemies == null) wave.enemies = new List<WaveDefinition.WaveEntry>();
                    wave.enemies.Add(new WaveDefinition.WaveEntry
                    {
                        enemy = enemy,
                        tier = quickAddTier,
                        count = quickAddCount,
                        forceElite = quickAddForceElite
                    });
                    EditorUtility.SetDirty(wave);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBatchOperationsBar(WaveDefinition wave)
    {
        EditorGUILayout.BeginVertical("helpbox");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("🛠 Narzędzia Masowej Edycji Fali:", EditorStyles.boldLabel, GUILayout.Width(220));

        // Scale counts
        if (GUILayout.Button("+10% Liczebności", EditorStyles.miniButton))
        {
            BatchScaleEnemyCount(wave, 1.10f);
        }
        if (GUILayout.Button("-10% Liczebności", EditorStyles.miniButton))
        {
            BatchScaleEnemyCount(wave, 0.90f);
        }
        if (GUILayout.Button("+25% Liczebności", EditorStyles.miniButton))
        {
            BatchScaleEnemyCount(wave, 1.25f);
        }
        if (GUILayout.Button("x2 Liczebność", EditorStyles.miniButton))
        {
            BatchScaleEnemyCount(wave, 2.0f);
        }

        GUILayout.Space(10);

        // Shift tiers
        if (GUILayout.Button("Wszystkie +1 Tier", EditorStyles.miniButton))
        {
            BatchShiftTiers(wave, 1);
        }
        if (GUILayout.Button("Wszystkie -1 Tier", EditorStyles.miniButton))
        {
            BatchShiftTiers(wave, -1);
        }

        GUILayout.Space(10);

        // Elite toggles
        if (GUILayout.Button("Wszystkie na Elite", EditorStyles.miniButton))
        {
            BatchSetElite(wave, true);
        }
        if (GUILayout.Button("Zdejmij Elite", EditorStyles.miniButton))
        {
            BatchSetElite(wave, false);
        }

        GUILayout.Space(10);
        GUI.backgroundColor = new Color(0.95f, 0.4f, 0.4f);
        if (GUILayout.Button("Wyczyść falę", EditorStyles.miniButton))
        {
            if (EditorUtility.DisplayDialog("Wyczyść falę", "Czy na pewno usunąć wszystkie grupy jednostek z tej fali?", "Wyczyść", "Anuluj"))
            {
                if (wave.enemies != null) wave.enemies.Clear();
                EditorUtility.SetDirty(wave);
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawWaveEntriesList(WaveDefinition wave)
    {
        EditorGUILayout.LabelField("Skład Fali (Grupy jednostek w kolejności spawnu):", EditorStyles.boldLabel);

        if (wave.enemies == null) wave.enemies = new List<WaveDefinition.WaveEntry>();

        if (wave.enemies.Count == 0)
        {
            EditorGUILayout.HelpBox("Fala nie posiada jeszcze żadnych jednostek. Użyj palety powyżej, aby dodać grupy wrogów.", MessageType.Info);
            return;
        }

        int removeIndex = -1;
        int moveUpIndex = -1;
        int moveDownIndex = -1;
        int duplicateIndex = -1;

        for (int i = 0; i < wave.enemies.Count; i++)
        {
            var entry = wave.enemies[i];
            EditorGUILayout.BeginHorizontal("box");

            EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(28));

            // Enemy Sprite thumbnail
            if (entry.enemy != null)
            {
                Sprite s = entry.enemy.GetSpriteForTier(entry.Tier);
                if (s != null && s.texture != null)
                {
                    GUILayout.Label(s.texture, GUILayout.Width(24), GUILayout.Height(24));
                }
                else
                {
                    GUILayout.Space(28);
                }
            }
            else
            {
                GUILayout.Space(28);
            }

            entry.enemy = (EnemyData)EditorGUILayout.ObjectField(entry.enemy, typeof(EnemyData), false, GUILayout.Width(170));

            EditorGUILayout.LabelField("Tier:", GUILayout.Width(32));
            entry.tier = Mathf.Clamp(EditorGUILayout.IntField(entry.Tier, GUILayout.Width(40)), 1, 4);

            EditorGUILayout.LabelField("Ilość:", GUILayout.Width(35));
            entry.count = Mathf.Max(1, EditorGUILayout.IntField(entry.count, GUILayout.Width(45)));

            entry.forceElite = EditorGUILayout.ToggleLeft("ELITE", entry.forceElite, GUILayout.Width(55));

            if (entry.enemy != null)
            {
                var tStats = entry.enemy.GetTier(entry.Tier);
                float baseHp = tStats != null ? tStats.baseHp : entry.enemy.baseHp;
                float armor = tStats != null ? tStats.armor : entry.enemy.armor;
                float unitHp = baseHp * (entry.forceElite ? entry.enemy.eliteHpMultiplier : 1f);
                EditorGUILayout.LabelField($"HP: {unitHp:0.#} (Σ {unitHp * entry.count:0.#}) | Arm: {armor:0}%", EditorStyles.miniLabel);
            }

            GUILayout.FlexibleSpace();

            // Action buttons
            GUI.enabled = i > 0;
            if (GUILayout.Button("▲", GUILayout.Width(24))) moveUpIndex = i;
            GUI.enabled = i < wave.enemies.Count - 1;
            if (GUILayout.Button("▼", GUILayout.Width(24))) moveDownIndex = i;
            GUI.enabled = true;

            if (GUILayout.Button("⧉", GUILayout.Width(24))) duplicateIndex = i;

            GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);
            if (GUILayout.Button("X", GUILayout.Width(24))) removeIndex = i;
            GUI.backgroundColor = Color.white;

            wave.enemies[i] = entry;
            EditorGUILayout.EndHorizontal();
        }

        // Handle reordering / mutation
        if (moveUpIndex > 0)
        {
            var tmp = wave.enemies[moveUpIndex - 1];
            wave.enemies[moveUpIndex - 1] = wave.enemies[moveUpIndex];
            wave.enemies[moveUpIndex] = tmp;
        }
        if (moveDownIndex >= 0 && moveDownIndex < wave.enemies.Count - 1)
        {
            var tmp = wave.enemies[moveDownIndex + 1];
            wave.enemies[moveDownIndex + 1] = wave.enemies[moveDownIndex];
            wave.enemies[moveDownIndex] = tmp;
        }
        if (duplicateIndex >= 0 && duplicateIndex < wave.enemies.Count)
        {
            var orig = wave.enemies[duplicateIndex];
            wave.enemies.Insert(duplicateIndex + 1, new WaveDefinition.WaveEntry
            {
                enemy = orig.enemy,
                tier = orig.tier,
                count = orig.count,
                forceElite = orig.forceElite
            });
        }
        if (removeIndex >= 0 && removeIndex < wave.enemies.Count)
        {
            wave.enemies.RemoveAt(removeIndex);
        }
    }

    private void DrawSpawnSimulationSection(WaveDefinition wave)
    {
        EditorGUILayout.BeginVertical("helpbox");
        EditorGUILayout.BeginHorizontal();
        showSpawnSimulation = EditorGUILayout.Foldout(showSpawnSimulation, "👁 Podgląd Rzeczywistej Sekwencji Spawnu (Symulacja)", true, EditorStyles.foldoutHeader);
        if (showSpawnSimulation && wave.shuffleEnemies)
        {
            EditorGUILayout.LabelField("Ziarno losowania (Seed):", GUILayout.Width(140));
            simulationSeed = EditorGUILayout.IntField(simulationSeed, GUILayout.Width(60));
            if (GUILayout.Button("Nowe losowanie", EditorStyles.miniButton, GUILayout.Width(100)))
            {
                simulationSeed = UnityEngine.Random.Range(1, 9999);
            }
        }
        EditorGUILayout.EndHorizontal();

        if (showSpawnSimulation)
        {
            List<string> sequence = GenerateSimulatedSpawnList(wave, simulationSeed);
            if (sequence.Count == 0)
            {
                EditorGUILayout.LabelField("Brak jednostek do symulacji spawnu.", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField($"Całkowita liczba jednostek w kolejce spawnu: {sequence.Count} (tryb: {(wave.shuffleEnemies ? "Losowy / Wymieszany" : "Sekwencyjny wg grup")})", EditorStyles.miniBoldLabel);
                EditorGUILayout.Space(2);

                int itemsPerRow = 8;
                for (int i = 0; i < sequence.Count; i += itemsPerRow)
                {
                    EditorGUILayout.BeginHorizontal();
                    for (int j = 0; j < itemsPerRow && (i + j) < sequence.Count; j++)
                    {
                        int index = i + j;
                        GUILayout.Label($"[{index + 1}] {sequence[index]}", EditorStyles.miniLabel, GUILayout.Width(105));
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }
        EditorGUILayout.EndVertical();
    }

    private List<string> GenerateSimulatedSpawnList(WaveDefinition wave, int seed)
    {
        var list = new List<string>();
        if (wave == null || wave.enemies == null) return list;

        foreach (var entry in wave.enemies)
        {
            if (entry.enemy == null || entry.count <= 0) continue;
            string name = !string.IsNullOrEmpty(entry.enemy.enemyName) ? entry.enemy.enemyName : entry.enemy.name;
            string tag = $"{name} T{entry.Tier}{(entry.forceElite ? "★" : "")}";
            for (int i = 0; i < entry.count; i++)
            {
                list.Add(tag);
            }
        }

        if (wave.shuffleEnemies && list.Count > 1)
        {
            var rng = new System.Random(seed);
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                string val = list[k];
                list[k] = list[n];
                list[n] = val;
            }
        }

        return list;
    }

    private void BatchScaleEnemyCount(WaveDefinition wave, float multiplier)
    {
        if (wave == null || wave.enemies == null) return;
        Undo.RecordObject(wave, "Batch Scale Enemy Count");
        for (int i = 0; i < wave.enemies.Count; i++)
        {
            var e = wave.enemies[i];
            e.count = Mathf.Max(1, Mathf.RoundToInt(e.count * multiplier));
            wave.enemies[i] = e;
        }
        EditorUtility.SetDirty(wave);
    }

    private void BatchShiftTiers(WaveDefinition wave, int delta)
    {
        if (wave == null || wave.enemies == null) return;
        Undo.RecordObject(wave, "Batch Shift Tiers");
        for (int i = 0; i < wave.enemies.Count; i++)
        {
            var e = wave.enemies[i];
            e.tier = Mathf.Clamp(e.Tier + delta, 1, 4);
            wave.enemies[i] = e;
        }
        EditorUtility.SetDirty(wave);
    }

    private void BatchSetElite(WaveDefinition wave, bool isElite)
    {
        if (wave == null || wave.enemies == null) return;
        Undo.RecordObject(wave, "Batch Set Elite");
        for (int i = 0; i < wave.enemies.Count; i++)
        {
            var e = wave.enemies[i];
            e.forceElite = isElite;
            wave.enemies[i] = e;
        }
        EditorUtility.SetDirty(wave);
    }

    // =========================================================================
    // TAB 2: WROGOWIE I TIERY (ENEMY & TIER EDITOR)
    // =========================================================================
    private void DrawEnemyEditorTab()
    {
        EditorGUILayout.BeginHorizontal();
        DrawEnemyListPanel();
        DrawEnemyDetailsPanel();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawEnemyListPanel()
    {
        EditorGUILayout.BeginVertical("box", GUILayout.Width(280), GUILayout.ExpandHeight(true));

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Wrogowie ({allEnemies.Count})", EditorStyles.boldLabel);
        if (GUILayout.Button("+ Nowy Wróg", GUILayout.Width(85)))
        {
            CreateNewEnemyAsset();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // Search bar
        enemySearchText = EditorGUILayout.TextField("Szukaj:", enemySearchText);
        EditorGUILayout.Space(4);

        enemyListScrollPos = EditorGUILayout.BeginScrollView(enemyListScrollPos);

        foreach (var enemy in allEnemies)
        {
            if (enemy == null) continue;
            string displayName = !string.IsNullOrEmpty(enemy.enemyName) ? enemy.enemyName : enemy.name;
            if (!string.IsNullOrEmpty(enemySearchText) && !displayName.ToLower().Contains(enemySearchText.ToLower()))
            {
                continue;
            }

            bool isSelected = selectedEnemy == enemy;
            GUI.backgroundColor = isSelected ? new Color(0.25f, 0.55f, 0.9f) : Color.white;
            EditorGUILayout.BeginHorizontal("button");

            // Thumbnail
            Sprite s = enemy.GetSpriteForTier(1);
            if (s != null && s.texture != null)
            {
                GUILayout.Label(s.texture, GUILayout.Width(20), GUILayout.Height(20));
            }

            string rankTag = enemy.isBoss ? " [BOSS]" : "";
            if (GUILayout.Button($"{displayName}{rankTag} (Koszt: {enemy.threatCost})", EditorStyles.label, GUILayout.ExpandWidth(true)))
            {
                selectedEnemy = enemy;
                GUI.FocusControl(null);
            }

            if (GUILayout.Button("◎", GUILayout.Width(24)))
            {
                EditorGUIUtility.PingObject(enemy);
            }

            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawEnemyDetailsPanel()
    {
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (selectedEnemy == null)
        {
            EditorGUILayout.LabelField("Wybierz wroga z listy po lewej stronie lub utwórz nowego.", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
            return;
        }

        selectedEnemy.EnsureTiers();

        enemyDetailsScrollPos = EditorGUILayout.BeginScrollView(enemyDetailsScrollPos);

        EditorGUI.BeginChangeCheck();
        Undo.RecordObject(selectedEnemy, "Edit Enemy Data");

        // Header
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Edycja Wroga: {selectedEnemy.name}", EditorStyles.boldLabel);

        if (GUILayout.Button("Duplikuj wroga", GUILayout.Width(110)))
        {
            DuplicateSelectedEnemy();
        }

        GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);
        if (GUILayout.Button("Usuń wroga", GUILayout.Width(90)))
        {
            if (EditorUtility.DisplayDialog("Usuwanie wroga", $"Czy na pewno usunąć {selectedEnemy.name}?", "Tak, usuń", "Anuluj"))
            {
                DeleteEnemyAsset(selectedEnemy);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // Section 1: Basic Stats & Rank
        EditorGUILayout.BeginVertical("helpbox");
        EditorGUILayout.LabelField("Podstawowe Informacje i Klasyfikacja", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        selectedEnemy.enemyName = EditorGUILayout.TextField("Nazwa w grze:", selectedEnemy.enemyName);
        selectedEnemy.threatCost = EditorGUILayout.IntField("Koszt Zagrożenia (Threat Cost):", selectedEnemy.threatCost);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        selectedEnemy.rank = (EnemyRank)EditorGUILayout.EnumPopup("Ranga wroga:", selectedEnemy.rank);
        selectedEnemy.canBeElite = EditorGUILayout.Toggle("Może być Elitą:", selectedEnemy.canBeElite);
        EditorGUILayout.EndHorizontal();

        if (selectedEnemy.canBeElite)
        {
            EditorGUILayout.BeginHorizontal();
            selectedEnemy.eliteSpawnChance = EditorGUILayout.Slider("Szansa na Elitę (%):", selectedEnemy.eliteSpawnChance, 0f, 100f);
            selectedEnemy.eliteHpMultiplier = EditorGUILayout.FloatField("Mnożnik HP Elity:", selectedEnemy.eliteHpMultiplier);
            selectedEnemy.eliteSpeedMultiplier = EditorGUILayout.FloatField("Mnożnik Speed Elity:", selectedEnemy.eliteSpeedMultiplier);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        selectedEnemy.etherDropElite = EditorGUILayout.IntField("Drop Etheru (Elita):", selectedEnemy.etherDropElite);
        selectedEnemy.etherDropBoss = EditorGUILayout.IntField("Drop Etheru (Boss):", selectedEnemy.etherDropBoss);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        selectedEnemy.prefab = (GameObject)EditorGUILayout.ObjectField("Domyślny Prefab (Fallback):", selectedEnemy.prefab, typeof(GameObject), false);
        selectedEnemy.sprite = (Sprite)EditorGUILayout.ObjectField("Domyślny Sprite (Fallback):", selectedEnemy.sprite, typeof(Sprite), false);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        // Section 2: 4-Column Side-by-Side Tier Matrix Editor
        DrawTiersMatrixEditor(selectedEnemy);

        EditorGUILayout.Space(8);

        // Section 3: Tier Auto-scaling Tool
        DrawTierAutoScalingToolbar(selectedEnemy);

        EditorGUILayout.Space(8);

        // Section 4: Skills list
        DrawSkillsSection(selectedEnemy);

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(selectedEnemy);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawTiersMatrixEditor(EnemyData enemy)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("⚔️ Konfiguracja i Porównanie Tierów (Tier 1 – Tier 4)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Każdy tier posiada niezależny Prefab, Sprite oraz statystyki bazowe. Wrogowie w falach korzystają z wartości dla wybranego tieru.", MessageType.None);
        EditorGUILayout.Space(4);

        float labelColWidth = 140f;
        float tierColWidth = 160f;

        // Row 0: Headers
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Właściwość", EditorStyles.boldLabel, GUILayout.Width(labelColWidth));
        for (int t = 0; t < 4; t++)
        {
            GUI.backgroundColor = GetTierColor(t + 1);
            EditorGUILayout.LabelField($"Tier {t + 1}", EditorStyles.toolbarButton, GUILayout.Width(tierColWidth));
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(3);

        // Row 1: Sprite Preview
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Podgląd Ikony", GUILayout.Width(labelColWidth));
        for (int t = 0; t < 4; t++)
        {
            var tierStats = enemy.tiers[t];
            Sprite s = tierStats.sprite != null ? tierStats.sprite : enemy.sprite;
            EditorGUILayout.BeginHorizontal(GUILayout.Width(tierColWidth));
            if (s != null && s.texture != null)
            {
                GUILayout.Label(s.texture, GUILayout.Width(32), GUILayout.Height(32));
            }
            else
            {
                GUILayout.Box("Brak", GUILayout.Width(32), GUILayout.Height(32));
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndHorizontal();

        // Row 2: Sprite Field
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Sprite (Ikonka)", GUILayout.Width(labelColWidth));
        for (int t = 0; t < 4; t++)
        {
            enemy.tiers[t].sprite = (Sprite)EditorGUILayout.ObjectField(enemy.tiers[t].sprite, typeof(Sprite), false, GUILayout.Width(tierColWidth));
        }
        EditorGUILayout.EndHorizontal();

        // Row 3: Prefab Field
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Prefab Wroga", GUILayout.Width(labelColWidth));
        for (int t = 0; t < 4; t++)
        {
            enemy.tiers[t].prefab = (GameObject)EditorGUILayout.ObjectField(enemy.tiers[t].prefab, typeof(GameObject), false, GUILayout.Width(tierColWidth));
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // Row 4: Base HP
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Bazowe HP", GUILayout.Width(labelColWidth));
        for (int t = 0; t < 4; t++)
        {
            enemy.tiers[t].baseHp = Mathf.Max(1f, EditorGUILayout.FloatField(enemy.tiers[t].baseHp, GUILayout.Width(tierColWidth)));
        }
        EditorGUILayout.EndHorizontal();

        // Row 5: Move Speed
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Szybkość Ruchu", GUILayout.Width(labelColWidth));
        for (int t = 0; t < 4; t++)
        {
            enemy.tiers[t].moveSpeed = Mathf.Max(0.1f, EditorGUILayout.FloatField(enemy.tiers[t].moveSpeed, GUILayout.Width(tierColWidth)));
        }
        EditorGUILayout.EndHorizontal();

        // Row 6: Armor
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Pancerz (Armor %)", GUILayout.Width(labelColWidth));
        for (int t = 0; t < 4; t++)
        {
            enemy.tiers[t].armor = EditorGUILayout.Slider(enemy.tiers[t].armor, 0f, 100f, GUILayout.Width(tierColWidth));
        }
        EditorGUILayout.EndHorizontal();

        // Row 7: Magic Resist
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Odporność (MR %)", GUILayout.Width(labelColWidth));
        for (int t = 0; t < 4; t++)
        {
            enemy.tiers[t].magicResist = EditorGUILayout.Slider(enemy.tiers[t].magicResist, 0f, 100f, GUILayout.Width(tierColWidth));
        }
        EditorGUILayout.EndHorizontal();

        // Row 8: Dodge Chance
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Unik (Dodge %)", GUILayout.Width(labelColWidth));
        for (int t = 0; t < 4; t++)
        {
            enemy.tiers[t].dodgeChance = EditorGUILayout.Slider(enemy.tiers[t].dodgeChance, 0f, 100f, GUILayout.Width(tierColWidth));
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawTierAutoScalingToolbar(EnemyData enemy)
    {
        EditorGUILayout.BeginVertical("helpbox");
        EditorGUILayout.LabelField("⚙️ Narzędzia Auto-Skalowania i Balansowania Tierów:", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Kopiuj T1 do T2-T4", GUILayout.Width(170)))
        {
            CopyTier1ToAll(enemy);
        }

        GUILayout.Space(15);
        EditorGUILayout.LabelField("Mnożnik HP / Tier:", GUILayout.Width(120));
        autoScaleHpMultiplier = EditorGUILayout.FloatField(autoScaleHpMultiplier, GUILayout.Width(50));

        if (GUILayout.Button($"Zastosuj Skalowanie HP (×{autoScaleHpMultiplier})", GUILayout.Width(220)))
        {
            AutoScaleTierHp(enemy, autoScaleHpMultiplier);
        }

        GUILayout.Space(15);
        EditorGUILayout.LabelField("+Pancerz / Tier:", GUILayout.Width(110));
        autoScaleArmorStep = EditorGUILayout.FloatField(autoScaleArmorStep, GUILayout.Width(45));

        if (GUILayout.Button($"+ Krok Pancerza (+{autoScaleArmorStep}%)", GUILayout.Width(170)))
        {
            AutoScaleTierArmor(enemy, autoScaleArmorStep);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void CopyTier1ToAll(EnemyData enemy)
    {
        if (enemy == null || enemy.tiers == null || enemy.tiers.Length < 4) return;
        Undo.RecordObject(enemy, "Copy Tier 1 to All");
        var t1 = enemy.tiers[0];
        for (int i = 1; i < 4; i++)
        {
            enemy.tiers[i].baseHp = t1.baseHp;
            enemy.tiers[i].moveSpeed = t1.moveSpeed;
            enemy.tiers[i].armor = t1.armor;
            enemy.tiers[i].magicResist = t1.magicResist;
            enemy.tiers[i].dodgeChance = t1.dodgeChance;
            if (enemy.tiers[i].prefab == null) enemy.tiers[i].prefab = t1.prefab;
            if (enemy.tiers[i].sprite == null) enemy.tiers[i].sprite = t1.sprite;
        }
        EditorUtility.SetDirty(enemy);
    }

    private void AutoScaleTierHp(EnemyData enemy, float multiplier)
    {
        if (enemy == null || enemy.tiers == null || enemy.tiers.Length < 4) return;
        Undo.RecordObject(enemy, "Auto Scale Tier HP");
        float baseHp = enemy.tiers[0].baseHp;
        for (int i = 1; i < 4; i++)
        {
            enemy.tiers[i].baseHp = Mathf.Round(baseHp * Mathf.Pow(multiplier, i));
        }
        EditorUtility.SetDirty(enemy);
    }

    private void AutoScaleTierArmor(EnemyData enemy, float step)
    {
        if (enemy == null || enemy.tiers == null || enemy.tiers.Length < 4) return;
        Undo.RecordObject(enemy, "Auto Scale Tier Armor");
        float baseArmor = enemy.tiers[0].armor;
        for (int i = 1; i < 4; i++)
        {
            enemy.tiers[i].armor = Mathf.Clamp(baseArmor + step * i, 0f, 90f);
        }
        EditorUtility.SetDirty(enemy);
    }

    private void DrawSkillsSection(EnemyData enemy)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("✨ Prefaby Umiejętności (EnemySkill)", EditorStyles.boldLabel);

        if (enemy.skillPrefabs == null) enemy.skillPrefabs = new List<GameObject>();

        int removeSkillIndex = -1;
        for (int i = 0; i < enemy.skillPrefabs.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Umiejętność #{i + 1}:", GUILayout.Width(110));
            enemy.skillPrefabs[i] = (GameObject)EditorGUILayout.ObjectField(enemy.skillPrefabs[i], typeof(GameObject), false);

            GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);
            if (GUILayout.Button("X", GUILayout.Width(24))) removeSkillIndex = i;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        if (removeSkillIndex >= 0)
        {
            enemy.skillPrefabs.RemoveAt(removeSkillIndex);
        }

        if (GUILayout.Button("+ Dodaj slot na umiejętność", GUILayout.Height(20)))
        {
            enemy.skillPrefabs.Add(null);
        }
        EditorGUILayout.EndVertical();
    }

    private Color GetTierColor(int tier)
    {
        switch (tier)
        {
            case 1: return new Color(0.85f, 0.85f, 0.85f);
            case 2: return new Color(0.5f, 0.8f, 0.5f);
            case 3: return new Color(0.45f, 0.65f, 1f);
            case 4: return new Color(0.9f, 0.55f, 0.95f);
            default: return Color.white;
        }
    }

    // =========================================================================
    // TAB 3: KRZYWA TRUDNOŚCI (DIFFICULTY OVERVIEW)
    // =========================================================================
    private void DrawDifficultyOverviewTab()
    {
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        EditorGUILayout.LabelField("📈 Przegląd Krzywej Trudności Fal (Dzień po Dniu)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Tabela sumaryczna wszystkich zdefiniowanych fal w projekcie. Pozwala ocenić tempo przyrostu HP oraz wykryć ewentualne dysproporcje trudności.", MessageType.Info);
        EditorGUILayout.Space(6);

        overviewScrollPos = EditorGUILayout.BeginScrollView(overviewScrollPos);

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("Dzień", EditorStyles.boldLabel, GUILayout.Width(60));
        EditorGUILayout.LabelField("Liczba Wrogów", EditorStyles.boldLabel, GUILayout.Width(110));
        EditorGUILayout.LabelField("Elity", EditorStyles.boldLabel, GUILayout.Width(60));
        EditorGUILayout.LabelField("Łączne HP", EditorStyles.boldLabel, GUILayout.Width(120));
        EditorGUILayout.LabelField("Szacowane EHP", EditorStyles.boldLabel, GUILayout.Width(120));
        EditorGUILayout.LabelField("Pula Etheru", EditorStyles.boldLabel, GUILayout.Width(90));
        EditorGUILayout.LabelField("Przyrost HP vs Poprz.", EditorStyles.boldLabel, GUILayout.Width(140));
        EditorGUILayout.LabelField("Skład Jednostek", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("Akcja", EditorStyles.boldLabel, GUILayout.Width(70));
        EditorGUILayout.EndHorizontal();

        float prevHp = 0f;

        for (int i = 0; i < allWaves.Count; i++)
        {
            var wave = allWaves[i];
            if (wave == null) continue;

            int totalCount = 0;
            int totalElites = 0;
            float totalRawHp = 0f;
            float totalEhp = 0f;
            Dictionary<string, int> unitsSummary = new Dictionary<string, int>();

            if (wave.enemies != null)
            {
                foreach (var e in wave.enemies)
                {
                    if (e.enemy == null || e.count <= 0) continue;
                    totalCount += e.count;
                    if (e.forceElite) totalElites += e.count;

                    var tStats = e.enemy.GetTier(e.Tier);
                    float baseHp = tStats != null ? tStats.baseHp : e.enemy.baseHp;
                    float armor = tStats != null ? tStats.armor : e.enemy.armor;
                    float mr = tStats != null ? tStats.magicResist : e.enemy.magicResist;
                    float mult = e.forceElite ? e.enemy.eliteHpMultiplier : 1f;

                    float unitHp = baseHp * mult;
                    totalRawHp += unitHp * e.count;

                    float avgMitigation = Mathf.Clamp01((armor + mr) / 200f);
                    float ehpMultiplier = avgMitigation >= 0.95f ? 20f : 1f / (1f - avgMitigation);
                    totalEhp += unitHp * ehpMultiplier * e.count;

                    string label = $"{(!string.IsNullOrEmpty(e.enemy.enemyName) ? e.enemy.enemyName : e.enemy.name)} T{e.Tier}";
                    if (!unitsSummary.ContainsKey(label)) unitsSummary[label] = 0;
                    unitsSummary[label] += e.count;
                }
            }

            // Delta HP
            string deltaHpStr = "-";
            Color deltaColor = Color.white;
            if (prevHp > 0f)
            {
                float diffPct = ((totalRawHp - prevHp) / prevHp) * 100f;
                deltaHpStr = $"{(diffPct >= 0 ? "+" : "")}{diffPct:0.#}%";
                if (diffPct > 50f) deltaColor = new Color(1f, 0.45f, 0.45f);
                else if (diffPct < 0f) deltaColor = new Color(1f, 0.85f, 0.3f);
            }
            prevHp = totalRawHp;

            GUI.backgroundColor = (i % 2 == 0) ? new Color(0.92f, 0.92f, 0.92f) : Color.white;
            EditorGUILayout.BeginHorizontal("box");

            EditorGUILayout.LabelField($"Dzień {wave.dayNumber}", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField($"{totalCount}", GUILayout.Width(110));
            EditorGUILayout.LabelField($"{totalElites}", GUILayout.Width(60));
            EditorGUILayout.LabelField($"{totalRawHp:0.#}", GUILayout.Width(120));
            EditorGUILayout.LabelField($"~{totalEhp:0.#}", GUILayout.Width(120));
            EditorGUILayout.LabelField($"{wave.etherPool} eth", GUILayout.Width(90));

            GUI.contentColor = deltaColor;
            EditorGUILayout.LabelField(deltaHpStr, GUILayout.Width(140));
            GUI.contentColor = Color.white;

            string unitsText = string.Join(", ", unitsSummary.Select(u => $"{u.Value}x {u.Key}"));
            EditorGUILayout.LabelField(unitsText, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Edytuj", GUILayout.Width(65)))
            {
                selectedWave = wave;
                currentTab = WindowTab.WaveManager;
            }

            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // =========================================================================
    // ASSET CREATION & UTILITIES
    // =========================================================================
    private void CreateNewWaveAsset()
    {
        if (!Directory.Exists(WavesFolderPath))
        {
            Directory.CreateDirectory(WavesFolderPath);
        }

        int nextDay = 1;
        var existingDays = new HashSet<int>(allWaves.Where(w => w != null).Select(w => w.dayNumber));
        while (existingDays.Contains(nextDay)) nextDay++;

        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{WavesFolderPath}/Wave day {nextDay}.asset");
        WaveDefinition newWave = CreateInstance<WaveDefinition>();
        newWave.dayNumber = nextDay;
        newWave.etherPool = Mathf.Max(3, 2 + nextDay);
        newWave.hpMultiplier = 1f;
        newWave.speedMultiplier = 1f;
        newWave.enemies = new List<WaveDefinition.WaveEntry>();

        if (allEnemies.Count > 0)
        {
            newWave.enemies.Add(new WaveDefinition.WaveEntry
            {
                enemy = allEnemies[0],
                tier = 1,
                count = 5 + nextDay * 2,
                forceElite = false
            });
        }

        AssetDatabase.CreateAsset(newWave, assetPath);
        AssetDatabase.SaveAssets();
        RefreshAssets();
        selectedWave = newWave;
    }

    private void DuplicateSelectedWave()
    {
        if (selectedWave == null) return;

        int nextDay = selectedWave.dayNumber + 1;
        var existingDays = new HashSet<int>(allWaves.Where(w => w != null).Select(w => w.dayNumber));
        while (existingDays.Contains(nextDay)) nextDay++;

        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{WavesFolderPath}/Wave day {nextDay}.asset");
        WaveDefinition copy = Instantiate(selectedWave);
        copy.dayNumber = nextDay;

        AssetDatabase.CreateAsset(copy, assetPath);
        AssetDatabase.SaveAssets();
        RefreshAssets();
        selectedWave = copy;
    }

    private void DeleteWaveAsset(WaveDefinition wave)
    {
        if (wave == null) return;
        string path = AssetDatabase.GetAssetPath(wave);
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();
        selectedWave = null;
        RefreshAssets();
    }

    private void RenameWaveAssetToMatchDay(WaveDefinition wave)
    {
        if (wave == null) return;
        string path = AssetDatabase.GetAssetPath(wave);
        string targetName = $"Wave day {wave.dayNumber}";
        string err = AssetDatabase.RenameAsset(path, targetName);
        if (!string.IsNullOrEmpty(err))
        {
            Debug.LogWarning($"[WaveManager] Nie udało się zmienić nazwy pliku: {err}");
        }
        else
        {
            AssetDatabase.SaveAssets();
            RefreshAssets();
        }
    }

    private void CreateNewEnemyAsset()
    {
        if (!Directory.Exists(EnemiesFolderPath))
        {
            Directory.CreateDirectory(EnemiesFolderPath);
        }

        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{EnemiesFolderPath}/NewEnemy.asset");
        EnemyData newEnemy = CreateInstance<EnemyData>();
        newEnemy.enemyName = "Nowy Wróg";
        newEnemy.threatCost = 1;
        newEnemy.rank = EnemyRank.Normal;
        newEnemy.canBeElite = true;
        newEnemy.eliteSpawnChance = 10f;
        newEnemy.eliteHpMultiplier = 2.0f;
        newEnemy.eliteSpeedMultiplier = 1.3f;
        newEnemy.etherDropElite = 1;
        newEnemy.etherDropBoss = 5;

        // Initialize 4 tiers with clean default stats: 100 HP, 3 speed
        newEnemy.tiers = new EnemyTierStats[4];
        for (int i = 0; i < 4; i++)
        {
            newEnemy.tiers[i] = new EnemyTierStats
            {
                baseHp = 100f,
                moveSpeed = 3f,
                armor = 0f,
                magicResist = 0f,
                dodgeChance = 0f
            };
        }

        AssetDatabase.CreateAsset(newEnemy, assetPath);
        AssetDatabase.SaveAssets();
        RefreshAssets();
        selectedEnemy = newEnemy;
    }

    private void DuplicateSelectedEnemy()
    {
        if (selectedEnemy == null) return;

        string originalPath = AssetDatabase.GetAssetPath(selectedEnemy);
        string dir = Path.GetDirectoryName(originalPath);
        string fileName = Path.GetFileNameWithoutExtension(originalPath);
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{fileName}_Copy.asset");

        EnemyData copy = Instantiate(selectedEnemy);
        copy.enemyName = $"{selectedEnemy.enemyName} (Kopia)";

        AssetDatabase.CreateAsset(copy, assetPath);
        AssetDatabase.SaveAssets();
        RefreshAssets();
        selectedEnemy = copy;
    }

    private void DeleteEnemyAsset(EnemyData enemy)
    {
        if (enemy == null) return;
        string path = AssetDatabase.GetAssetPath(enemy);
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();
        selectedEnemy = null;
        RefreshAssets();
    }

    private float CalculateWaveTotalHp(WaveDefinition wave)
    {
        if (wave == null || wave.enemies == null) return 0f;
        float total = 0f;
        foreach (var e in wave.enemies)
        {
            if (e.enemy == null || e.count <= 0) continue;
            var tStats = e.enemy.GetTier(e.Tier);
            float baseHp = tStats != null ? tStats.baseHp : e.enemy.baseHp;
            float mult = e.forceElite ? e.enemy.eliteHpMultiplier : 1f;
            total += baseHp * mult * e.count;
        }
        return total;
    }

    private void SyncWithSceneSpawner()
    {
        RefreshAssets();
        var spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner == null)
        {
            EditorUtility.DisplayDialog("Wave Manager", "Nie znaleziono obiektu EnemySpawner na aktywnej scenie!", "OK");
            return;
        }

        Undo.RecordObject(spawner, "Sync EnemySpawner Waves");
        spawner.predefinedWaves = new List<WaveDefinition>(allWaves);
        spawner.availableEnemies = new List<EnemyData>(allEnemies);
        EditorUtility.SetDirty(spawner);

        Debug.Log($"[WaveManager] Zsynchronizowano {allWaves.Count} fal oraz {allEnemies.Count} typów wrogów z EnemySpawner na scenie.");
    }
}
