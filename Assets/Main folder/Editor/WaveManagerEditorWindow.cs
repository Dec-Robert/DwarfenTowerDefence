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
        window.minSize = new Vector2(880, 560);
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

        if (GUILayout.Button("+ Nowa Fala (Kolejny Dzień)", EditorStyles.toolbarButton, GUILayout.Width(175)))
        {
            CreateNewWaveAsset();
        }

        if (selectedWave != null && GUILayout.Button("Duplikuj Wybraną Falę", EditorStyles.toolbarButton, GUILayout.Width(150)))
        {
            DuplicateSelectedWave();
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
        selectedWave.hpMultiplier = Mathf.Max(0.1f, EditorGUILayout.FloatField("Mnożnik HP Fali", selectedWave.hpMultiplier <= 0f ? 1f : selectedWave.hpMultiplier));
        selectedWave.speedMultiplier = Mathf.Max(0.1f, EditorGUILayout.FloatField("Mnożnik Prędkości Fali", selectedWave.speedMultiplier <= 0f ? 1f : selectedWave.speedMultiplier));
        selectedWave.shuffleEnemies = EditorGUILayout.Toggle("Wymieszaj Kolejność (Shuffle)", selectedWave.shuffleEnemies);
        selectedWave.allowRandomElites = EditorGUILayout.Toggle("Losowe Elity wg EnemyData", selectedWave.allowRandomElites);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        // Sekcja 2: Podsumowanie statystyk fali
        DrawWaveSummaryBox(selectedWave);

        EditorGUILayout.Space(8);

        // Sekcja 3: Szybkie dodawanie grup wrogów
        EditorGUILayout.LabelField("Szybkie dodawanie grupy wrogów:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        foreach (var enemy in allEnemies)
        {
            if (enemy == null) continue;
            string label = string.IsNullOrEmpty(enemy.enemyName) ? enemy.name : enemy.enemyName;
            if (GUILayout.Button($"+ {label} (HP:{enemy.baseHp})", GUILayout.Height(24)))
            {
                if (selectedWave.enemies == null) selectedWave.enemies = new List<WaveDefinition.WaveEntry>();
                selectedWave.enemies.Add(new WaveDefinition.WaveEntry
                {
                    enemy = enemy,
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
            entry.enemy = (EnemyData)EditorGUILayout.ObjectField(entry.enemy, typeof(EnemyData), false, GUILayout.Width(180));
            EditorGUILayout.LabelField("Ilość:", GUILayout.Width(38));
            entry.count = Mathf.Max(1, EditorGUILayout.IntField(entry.count, GUILayout.Width(50)));

            entry.forceElite = EditorGUILayout.ToggleLeft("Cała grupa ELITE", entry.forceElite, GUILayout.Width(125));

            if (entry.enemy != null)
            {
                float hpMult = (selectedWave.hpMultiplier > 0f ? selectedWave.hpMultiplier : 1f) * (entry.forceElite ? entry.enemy.eliteHpMultiplier : 1f);
                float unitHp = entry.enemy.baseHp * hpMult;
                EditorGUILayout.LabelField($"HP jedn.: {unitHp:0.#} | Suma: {unitHp * entry.count:0.#}", EditorStyles.miniLabel);
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
        float waveHpMult = wave.hpMultiplier > 0f ? wave.hpMultiplier : 1f;

        if (wave.enemies != null)
        {
            foreach (var e in wave.enemies)
            {
                if (e.enemy == null || e.count <= 0) continue;
                totalEnemies += e.count;
                if (e.forceElite) totalElites += e.count;
                float mult = waveHpMult * (e.forceElite ? e.enemy.eliteHpMultiplier : 1f);
                totalHp += e.enemy.baseHp * mult * e.count;
            }
        }

        float interval = wave.customSpawnInterval > 0f ? wave.customSpawnInterval : 1.0f;
        float spawnDuration = totalEnemies * interval;

        int eth80 = Mathf.RoundToInt(wave.etherPool * 0.8f);
        int eth60 = Mathf.RoundToInt(wave.etherPool * 0.6f);
        int eth40 = Mathf.RoundToInt(wave.etherPool * 0.4f);
        int eth20 = Mathf.RoundToInt(wave.etherPool * 0.2f);

        EditorGUILayout.BeginVertical("helpbox");
        EditorGUILayout.LabelField("Podsumowanie Fali", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Łącznie wrogów: {totalEnemies} (w tym gwarantowanych Elit: {totalElites})  |  Łączna pula HP: {totalHp:0.#}  |  Czas spawnowania: ~{spawnDuration:0.#}s");
        EditorGUILayout.LabelField($"Pula Etheru: {wave.etherPool} szt.  |  Gwarant wg Ognia: Lvl 1: {eth80} (80%)  •  Lvl 2: {eth60} (60%)  •  Lvl 3: {eth40} (40%)  •  Lvl 4: {eth20} (20%)  •  Lvl 5: 0 (0%)");
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
