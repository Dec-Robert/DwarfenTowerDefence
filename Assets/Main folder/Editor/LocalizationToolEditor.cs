using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Narzędzie edytorskie do zarządzania słownikami lokalizacji, ekstrakcji tekstów z ScriptableObjects i walidacji tłumaczeń.
/// Dostępne z menu: Tools > Localization > Translation Manager
/// </summary>
public class LocalizationToolEditor : EditorWindow
{
    private const string LANGUAGES_DIR = "Assets/Resources/Localization/Languages";
    private const string PL_FILE = "Assets/Resources/Localization/Languages/pl.json";
    private const string EN_FILE = "Assets/Resources/Localization/Languages/en.json";

    private Dictionary<string, string> plDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> enDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private Vector2 scrollPos;
    private string searchFilter = "";
    private bool showOnlyMissing = false;
    private string newKeyName = "";
    private string newKeyPl = "";
    private string newKeyEn = "";
    private string statusMessage = "";
    private MessageType statusMessageType = MessageType.Info;

    [MenuItem("Tools/Localization/Translation Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<LocalizationToolEditor>("Localization Manager");
        window.minSize = new Vector2(700, 500);
        window.LoadDictionaries();
    }

    private void OnEnable()
    {
        LoadDictionaries();
    }

    private void LoadDictionaries()
    {
        plDict.Clear();
        enDict.Clear();

        if (File.Exists(PL_FILE))
        {
            string json = File.ReadAllText(PL_FILE, Encoding.UTF8);
            LocalizationManager.ParseJsonIntoDictionary(json, plDict);
        }

        if (File.Exists(EN_FILE))
        {
            string json = File.ReadAllText(EN_FILE, Encoding.UTF8);
            LocalizationManager.ParseJsonIntoDictionary(json, enDict);
        }

        statusMessage = $"Załadowano słowniki: PL ({plDict.Count} kluczy), EN ({enDict.Count} kluczy).";
        statusMessageType = MessageType.Info;
    }

    private void SaveDictionaries()
    {
        if (!Directory.Exists(LANGUAGES_DIR))
        {
            Directory.CreateDirectory(LANGUAGES_DIR);
        }

        SaveDictionaryToFile(PL_FILE, plDict);
        SaveDictionaryToFile(EN_FILE, enDict);

        AssetDatabase.Refresh();
        LocalizationManager.Initialize();

        statusMessage = $"Zapisano słowniki pomyślnie. PL ({plDict.Count}), EN ({enDict.Count}).";
        statusMessageType = MessageType.Info;
    }

    private static void SaveDictionaryToFile(string path, Dictionary<string, string> dict)
    {
        // Posortuj alfabetycznie po kluczach
        var sorted = dict.OrderBy(k => k.Key).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("{");

        for (int i = 0; i < sorted.Count; i++)
        {
            var kvp = sorted[i];
            string escapedKey = EscapeJsonString(kvp.Key);
            string escapedVal = EscapeJsonString(kvp.Value ?? "");
            string comma = (i < sorted.Count - 1) ? "," : "";
            sb.AppendLine($"  \"{escapedKey}\": \"{escapedVal}\"{comma}");
        }

        sb.AppendLine("}");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static string EscapeJsonString(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Replace("\\", "\\\\")
                   .Replace("\"", "\\\"")
                   .Replace("\n", "\\n")
                   .Replace("\r", "\\r")
                   .Replace("\t", "\\t");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Menedżer Lokalizacji (PL / EN)", EditorStyles.boldLabel);

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, statusMessageType);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Załaduj ponownie pliki", GUILayout.Height(26)))
        {
            LoadDictionaries();
        }
        if (GUILayout.Button("Zapisz wszystkie zmiany", GUILayout.Height(26)))
        {
            SaveDictionaries();
        }
        if (GUILayout.Button("Skanuj ScriptableObjects (Auto-Extract)", GUILayout.Height(26)))
        {
            ScanAndExtractScriptableObjects();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // Pasek wyszukiwania i filtry
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("Szukaj:", GUILayout.Width(50));
        searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);
        if (GUILayout.Button("Wyczyść", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            searchFilter = "";
        }
        showOnlyMissing = EditorGUILayout.ToggleLeft("Tylko brakujące tłumaczenia EN", showOnlyMissing, GUILayout.Width(220));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // Sekcja dodawania nowego klucza
        DrawNewKeySection();

        EditorGUILayout.Space(6);

        // Lista kluczy
        DrawKeyList();
    }

    private void DrawNewKeySection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Dodaj nowy wpis lokalizacyjny", EditorStyles.miniBoldLabel);
        EditorGUILayout.BeginHorizontal();
        newKeyName = EditorGUILayout.TextField("Klucz:", newKeyName);
        if (GUILayout.Button("Dodaj", GUILayout.Width(70)))
        {
            if (!string.IsNullOrWhiteSpace(newKeyName))
            {
                string cleanKey = newKeyName.Trim().ToUpperInvariant();
                if (!plDict.ContainsKey(cleanKey)) plDict[cleanKey] = newKeyPl;
                if (!enDict.ContainsKey(cleanKey)) enDict[cleanKey] = newKeyEn;
                newKeyName = "";
                newKeyPl = "";
                newKeyEn = "";
                GUI.FocusControl(null);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("PL:", GUILayout.Width(25));
        newKeyPl = EditorGUILayout.TextField(newKeyPl);
        EditorGUILayout.LabelField("EN:", GUILayout.Width(25));
        newKeyEn = EditorGUILayout.TextField(newKeyEn);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawKeyList()
    {
        // Zbierz wszystkie unikalne klucze
        var allKeys = new HashSet<string>(plDict.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var k in enDict.Keys) allKeys.Add(k);

        var filteredKeys = allKeys.Where(k =>
        {
            if (showOnlyMissing)
            {
                bool hasEn = enDict.TryGetValue(k, out string v) && !string.IsNullOrWhiteSpace(v);
                if (hasEn) return false;
            }

            if (!string.IsNullOrEmpty(searchFilter))
            {
                bool matchKey = k.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                bool matchPl = plDict.TryGetValue(k, out string plVal) && plVal.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                bool matchEn = enDict.TryGetValue(k, out string enVal) && enVal.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                return matchKey || matchPl || matchEn;
            }
            return true;
        }).OrderBy(k => k).ToList();

        EditorGUILayout.LabelField($"Wyświetlono wpisów: {filteredKeys.Count} (ze wszystkich: {allKeys.Count})", EditorStyles.miniLabel);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        string keyToDelete = null;

        foreach (var key in filteredKeys)
        {
            plDict.TryGetValue(key, out string plVal);
            enDict.TryGetValue(key, out string enVal);

            bool isMissingEn = string.IsNullOrWhiteSpace(enVal);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(key, isMissingEn ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(260));

            if (isMissingEn)
            {
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField("[Brak EN]", GUILayout.Width(70));
                GUI.color = Color.white;
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Usuń", EditorStyles.miniButton, GUILayout.Width(45)))
            {
                keyToDelete = key;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("PL:", GUILayout.Width(25));
            string newPl = EditorGUILayout.TextField(plVal ?? "");
            if (newPl != plVal) plDict[key] = newPl;

            EditorGUILayout.LabelField("EN:", GUILayout.Width(25));
            string newEn = EditorGUILayout.TextField(enVal ?? "");
            if (newEn != enVal) enDict[key] = newEn;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        if (keyToDelete != null)
        {
            plDict.Remove(keyToDelete);
            enDict.Remove(keyToDelete);
        }

        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// Skanuje wszystkie obiekty ScriptableObject i wydobywa teksty polskie, tworząc klucze lokalizacji.
    /// </summary>
    public void ScanAndExtractScriptableObjects()
    {
        int addedCount = 0;

        // 1. Budynki i Wieże (BuildingData)
        string[] buildingGuids = AssetDatabase.FindAssets("t:BuildingData");
        foreach (string guid in buildingGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BuildingData b = AssetDatabase.LoadAssetAtPath<BuildingData>(path);
            if (b == null) continue;

            string id = !string.IsNullOrEmpty(b.buildingName) ? b.buildingName.Trim() : b.name.Trim();
            string cleanId = SanitizeKey(id);

            string nameKey = $"BUILDING_{cleanId}_NAME";
            string descKey = $"BUILDING_{cleanId}_DESC";

            if (!plDict.ContainsKey(nameKey) && !string.IsNullOrWhiteSpace(b.buildingName))
            {
                plDict[nameKey] = b.buildingName;
                if (!enDict.ContainsKey(nameKey)) enDict[nameKey] = "";
                addedCount++;
            }

            if (!plDict.ContainsKey(descKey) && !string.IsNullOrWhiteSpace(b.description))
            {
                plDict[descKey] = b.description;
                if (!enDict.ContainsKey(descKey)) enDict[descKey] = "";
                addedCount++;
            }
        }

        // 2. Ulepszenia Budynków (BuildingUpgradeSO)
        string[] upgradeGuids = AssetDatabase.FindAssets("t:BuildingUpgradeSO");
        foreach (string guid in upgradeGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BuildingUpgradeSO u = AssetDatabase.LoadAssetAtPath<BuildingUpgradeSO>(path);
            if (u == null) continue;

            string id = !string.IsNullOrEmpty(u.upgradeName) ? u.upgradeName.Trim() : u.name.Trim();
            string cleanId = SanitizeKey(id);

            string nameKey = $"UPGRADE_{cleanId}_NAME";
            string descKey = $"UPGRADE_{cleanId}_DESC";

            if (!plDict.ContainsKey(nameKey) && !string.IsNullOrWhiteSpace(u.upgradeName))
            {
                plDict[nameKey] = u.upgradeName;
                if (!enDict.ContainsKey(nameKey)) enDict[nameKey] = "";
                addedCount++;
            }

            if (!plDict.ContainsKey(descKey) && !string.IsNullOrWhiteSpace(u.description))
            {
                plDict[descKey] = u.description;
                if (!enDict.ContainsKey(descKey)) enDict[descKey] = "";
                addedCount++;
            }
        }

        // 3. Runy (RuneDefinitionSO)
        string[] runeGuids = AssetDatabase.FindAssets("t:RuneDefinitionSO");
        foreach (string guid in runeGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            RuneDefinitionSO r = AssetDatabase.LoadAssetAtPath<RuneDefinitionSO>(path);
            if (r == null) continue;

            string id = !string.IsNullOrEmpty(r.runeName) ? r.runeName.Trim() : r.name.Trim();
            string cleanId = SanitizeKey(id);

            string nameKey = $"RUNE_{cleanId}_NAME";

            if (!plDict.ContainsKey(nameKey) && !string.IsNullOrWhiteSpace(r.runeName))
            {
                plDict[nameKey] = r.runeName;
                if (!enDict.ContainsKey(nameKey)) enDict[nameKey] = "";
                addedCount++;
            }
        }

        // 4. Meta Progresja (MetaUpgradeSO)
        string[] metaGuids = AssetDatabase.FindAssets("t:MetaUpgradeSO");
        foreach (string guid in metaGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MetaUpgradeSO m = AssetDatabase.LoadAssetAtPath<MetaUpgradeSO>(path);
            if (m == null) continue;

            string id = !string.IsNullOrEmpty(m.id) ? m.id.Trim() : m.name.Trim();
            string cleanId = SanitizeKey(id);

            string nameKey = $"META_{cleanId}_NAME";
            string descKey = $"META_{cleanId}_DESC";

            if (!plDict.ContainsKey(nameKey) && !string.IsNullOrWhiteSpace(m.upgradeName))
            {
                plDict[nameKey] = m.upgradeName;
                if (!enDict.ContainsKey(nameKey)) enDict[nameKey] = "";
                addedCount++;
            }

            if (!plDict.ContainsKey(descKey) && !string.IsNullOrWhiteSpace(m.description))
            {
                plDict[descKey] = m.description;
                if (!enDict.ContainsKey(descKey)) enDict[descKey] = "";
                addedCount++;
            }
        }

        // 5. Wrogowie (EnemyData)
        string[] enemyGuids = AssetDatabase.FindAssets("t:EnemyData");
        foreach (string guid in enemyGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            EnemyData e = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (e == null) continue;

            string id = !string.IsNullOrEmpty(e.enemyName) ? e.enemyName.Trim() : e.name.Trim();
            string cleanId = SanitizeKey(id);

            string nameKey = $"ENEMY_{cleanId}_NAME";

            if (!plDict.ContainsKey(nameKey) && !string.IsNullOrWhiteSpace(e.enemyName))
            {
                plDict[nameKey] = e.enemyName;
                if (!enDict.ContainsKey(nameKey)) enDict[nameKey] = "";
                addedCount++;
            }
        }

        SaveDictionaries();
        statusMessage = $"Zakończono skanowanie ScriptableObjects. Dodano/odświeżono {addedCount} nowych wpisów.";
        statusMessageType = MessageType.Info;
    }

    private static string SanitizeKey(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "UNKNOWN";
        var sb = new StringBuilder();
        foreach (char c in raw)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToUpperInvariant(c));
            }
            else if (c == ' ' || c == '_' || c == '-')
            {
                sb.Append('_');
            }
        }
        string result = sb.ToString();
        while (result.Contains("__")) result = result.Replace("__", "_");
        return result.Trim('_');
    }
}
