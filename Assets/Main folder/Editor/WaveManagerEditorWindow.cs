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

    private List<WaveDefinition> allWaves = new List<WaveDefinition>();
    private List<EnemyData> allEnemies = new List<EnemyData>();

    private WaveDefinition selectedWave;
    private Vector2 leftScrollPos;
    private Vector2 rightScrollPos;

    [MenuItem("Tools/Waves/Wave Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<WaveManagerEditorWindow>("Wave Manager");
        window.minSize = new Vector2(900, 580);
    }

    [MenuItem("Tools/Waves/Migrate Enemy Tiers and Waves")]
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
            if (enemy != null) allEnemies.Add(enemy);
        }

        allEnemies = allEnemies.OrderBy(e => e.threatCost).ThenBy(e => e.name).ToList();

        if (selectedWave == null && allWaves.Count > 0)
        {
            selectedWave = allWaves[0];
        }
    }

    private void OnGUI()
    {
        DrawTopToolbar();

        EditorGUILayout.BeginHorizontal();
        DrawWaveListPanel();
        DrawWaveDetailsPanel();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawTopToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Odśwież Assety", EditorStyles.toolbarButton, GUILayout.Width(110)))
        {
            RefreshAssets();
        }

        if (GUILayout.Button("+ Nowa Fala", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            CreateNewWaveAsset();
        }

        if (selectedWave != null && GUILayout.Button("Duplikuj Wybraną", EditorStyles.toolbarButton, GUILayout.Width(130)))
        {
            DuplicateSelectedWave();
        }

        if (GUILayout.Button("Migracja Tierów", EditorStyles.toolbarButton, GUILayout.Width(110)))
        {
            EnemyTierMigrationTool.MigrateAll();
            RefreshAssets();
        }

        GUILayout.FlexibleSpace();

        GUI.backgroundColor = new Color(0.35f, 0.85f, 0.45f);
        if (GUILayout.Button("Synchronizuj z EnemySpawner na Scenie", EditorStyles.toolbarButton, GUILayout.Width(240)))
        {
            SyncWithSceneSpawner();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        DrawValidationBanner();
    }

    private void DrawValidationBanner()
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
        EditorGUILayout.BeginVertical("box", GUILayout.Width(280), GUILayout.ExpandHeight(true));
        EditorGUILayout.LabelField($"Predefiniowane Fale ({allWaves.Count})", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        leftScrollPos = EditorGUILayout.BeginScrollView(leftScrollPos);

        foreach (var wave in allWaves)
        {
            if (wave == null) continue;

            int totalCount = wave.enemies != null ? wave.enemies.Sum(e => Mathf.Max(0, e.count)) : 0;
            bool isSelected = selectedWave == wave;

            GUI.backgroundColor = isSelected ? new Color(0.25f, 0.55f, 0.9f) : Color.white;
            EditorGUILayout.BeginHorizontal("button");

            if (GUILayout.Button($"Dzień {wave.dayNumber}  ({totalCount} wrogów)", EditorStyles.label, GUILayout.ExpandWidth(true)))
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

        rightScrollPos = EditorGUILayout.BeginScrollView(rightScrollPos);

        EditorGUI.BeginChangeCheck();
        Undo.RecordObject(selectedWave, "Edit Wave Definition");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Edycja Fali: {selectedWave.name}", EditorStyles.boldLabel);
        if (GUILayout.Button("Zmień nazwę pliku na 'Wave day X'", GUILayout.Width(210)))
        {
            RenameWaveAssetToMatchDay(selectedWave);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // Sekcja 1: Podstawowe parametry
        EditorGUILayout.BeginVertical("helpbox");
        selectedWave.dayNumber = Mathf.Max(1, EditorGUILayout.IntField("Numer Dnia (dayNumber)", selectedWave.dayNumber));
        selectedWave.waveMessage = EditorGUILayout.TextField("Komunikat Fali (waveMessage)", selectedWave.waveMessage);
        selectedWave.etherPool = Mathf.Max(0, EditorGUILayout.IntField("Pula Etheru Fali (etherPool)", selectedWave.etherPool));
        selectedWave.customSpawnInterval = Mathf.Max(0f, EditorGUILayout.FloatField("Interwał Spawnu (0 = domyślny)", selectedWave.customSpawnInterval));
        selectedWave.shuffleEnemies = EditorGUILayout.Toggle("Wymieszaj Kolejność (Shuffle)", selectedWave.shuffleEnemies);
        selectedWave.allowRandomElites = EditorGUILayout.Toggle("Losowe Elity wg EnemyData", selectedWave.allowRandomElites);

        EditorGUILayout.Space(2);
        EditorGUILayout.HelpBox("Tryb Tierowy: Statystyki wrogów wynikają z wybranego Tieru (T1-T4). Skalowanie (1.1^a) włącza się po ostatniej zdefiniowanej fali.", MessageType.None);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        // Sekcja 2: Podsumowanie statystyk fali
        DrawWaveSummaryBox(selectedWave);

        EditorGUILayout.Space(8);

        // Sekcja 3: Szybkie dodawanie grup wrogów
        EditorGUILayout.LabelField("Szybkie dodawanie grupy wrogów (Domyślnie Tier 1):", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        foreach (var enemy in allEnemies)
        {
            if (enemy == null) continue;
            string label = string.IsNullOrEmpty(enemy.enemyName) ? enemy.name : enemy.enemyName;
            float t1Hp = enemy.GetTier(1).baseHp;
            if (GUILayout.Button($"+ {label} (HP:{t1Hp:0.#})", GUILayout.Height(24)))
            {
                if (selectedWave.enemies == null) selectedWave.enemies = new List<WaveDefinition.WaveEntry>();
                selectedWave.enemies.Add(new WaveDefinition.WaveEntry
                {
                    enemy = enemy,
                    tier = 1,
                    count = 5,
                    forceElite = false
                });
                EditorUtility.SetDirty(selectedWave);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // Sekcja 4: Lista grup w fali
        EditorGUILayout.LabelField("Skład Fali (Grupy w kolejności spawnu):", EditorStyles.boldLabel);

        if (selectedWave.enemies == null)
        {
            selectedWave.enemies = new List<WaveDefinition.WaveEntry>();
        }

        int removeIndex = -1;
        int moveUpIndex = -1;
        int moveDownIndex = -1;

        for (int i = 0; i < selectedWave.enemies.Count; i++)
        {
            var entry = selectedWave.enemies[i];
            EditorGUILayout.BeginHorizontal("box");

            EditorGUILayout.LabelField($"#{i + 1}", GUILayout.Width(28));
            entry.enemy = (EnemyData)EditorGUILayout.ObjectField(entry.enemy, typeof(EnemyData), false, GUILayout.Width(170));

            EditorGUILayout.LabelField("Tier:", GUILayout.Width(30));
            int currentTier = entry.Tier;
            entry.tier = EditorGUILayout.IntSlider(currentTier, 1, 4, GUILayout.Width(90));

            EditorGUILayout.LabelField("Ilość:", GUILayout.Width(36));
            entry.count = Mathf.Max(1, EditorGUILayout.IntField(entry.count, GUILayout.Width(45)));

            entry.forceElite = EditorGUILayout.ToggleLeft("ELITE", entry.forceElite, GUILayout.Width(55));

            if (entry.enemy != null)
            {
                var tStats = entry.enemy.GetTier(entry.Tier);
                float baseHp = tStats != null ? tStats.baseHp : entry.enemy.baseHp;
                float unitHp = baseHp * (entry.forceElite ? entry.enemy.eliteHpMultiplier : 1f);
                EditorGUILayout.LabelField($"HP: {unitHp:0.#} (Σ {unitHp * entry.count:0.#})", EditorStyles.miniLabel);
            }

            GUILayout.FlexibleSpace();

            GUI.enabled = i > 0;
            if (GUILayout.Button("▲", GUILayout.Width(24))) moveUpIndex = i;
            GUI.enabled = i < selectedWave.enemies.Count - 1;
            if (GUILayout.Button("▼", GUILayout.Width(24))) moveDownIndex = i;
            GUI.enabled = true;

            GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);
            if (GUILayout.Button("X", GUILayout.Width(24))) removeIndex = i;
            GUI.backgroundColor = Color.white;

            selectedWave.enemies[i] = entry;
            EditorGUILayout.EndHorizontal();
        }

        if (moveUpIndex > 0)
        {
            var tmp = selectedWave.enemies[moveUpIndex - 1];
            selectedWave.enemies[moveUpIndex - 1] = selectedWave.enemies[moveUpIndex];
            selectedWave.enemies[moveUpIndex] = tmp;
        }
        if (moveDownIndex >= 0 && moveDownIndex < selectedWave.enemies.Count - 1)
        {
            var tmp = selectedWave.enemies[moveDownIndex + 1];
            selectedWave.enemies[moveDownIndex + 1] = selectedWave.enemies[moveDownIndex];
            selectedWave.enemies[moveDownIndex] = tmp;
        }
        if (removeIndex >= 0)
        {
            selectedWave.enemies.RemoveAt(removeIndex);
        }

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(selectedWave);
            allWaves = allWaves.OrderBy(w => w.dayNumber).ThenBy(w => w.name).ToList();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawWaveSummaryBox(WaveDefinition wave)
    {
        int totalEnemies = 0;
        int totalElites = 0;
        float totalHp = 0f;

        if (wave.enemies != null)
        {
            foreach (var e in wave.enemies)
            {
                if (e.enemy == null || e.count <= 0) continue;
                totalEnemies += e.count;
                if (e.forceElite) totalElites += e.count;

                var tierStats = e.enemy.GetTier(e.Tier);
                float baseHp = tierStats != null ? tierStats.baseHp : e.enemy.baseHp;
                float mult = (e.forceElite ? e.enemy.eliteHpMultiplier : 1f);
                totalHp += baseHp * mult * e.count;
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
        EditorGUILayout.LabelField("Podsumowanie Fali", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Łącznie wrogów: {totalEnemies} (w tym gwarantowanych Elit: {totalElites})  |  Łączna pula HP: {totalHp:0.#}  |  Czas spawnowania: ~{spawnDuration:0.#}s");
        EditorGUILayout.LabelField($"Pula Etheru: {wave.etherPool} szt.  |  Gwarant wg Ognia: Lvl 1: {eth80} (80%)  •  Lvl 2: {eth60} (60%)  •  Lvl 3: {eth40} (40%)  •  Lvl 4: {eth20} (20%)  •  Lvl 5: 0 (0%)");

        if (maxDefinedDay > 0)
        {
            float hpPlus1 = totalHp * Mathf.Pow(1.1f, 1);
            float hpPlus5 = totalHp * Mathf.Pow(1.1f, 5);
            float hpPlus10 = totalHp * Mathf.Pow(1.1f, 10);
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField($"Skalowanie late-game (wzór 1.1^a po Dniu {maxDefinedDay}):", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"Dzień {maxDefinedDay + 1} (a=1): ×1.10 HP (~{hpPlus1:0.#})  |  Dzień {maxDefinedDay + 5} (a=5): ×1.61 HP (~{hpPlus5:0.#})  |  Dzień {maxDefinedDay + 10} (a=10): ×2.59 HP (~{hpPlus10:0.#})", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
    }

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
