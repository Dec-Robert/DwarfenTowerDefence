#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Narzędzia edytorskie do zarządzania MetaUpgradeSO.
///
/// ── DOSTĘP ───────────────────────────────────────────────────────────────────
///   Menu: Tools → Meta Upgrades → ...
///
/// ── NARZĘDZIA ────────────────────────────────────────────────────────────────
///   1. Nadaj Unikatowe ID       – skanuje folder Meta i nadaje ID każdemu SO
///                                 które go nie ma lub ma duplikat.
///                                 Format ID: snake_case z nazwy pliku, np.
///                                 "WoodStorage_I" → "wood_storage_i"
///
///   2. Przypisz do Managera     – znajduje WSZYSTKIE MetaUpgradeSO w folderze
///                                 i przypisuje je do listy allUpgrades
///                                 w zaznaczonym MetaUpgradeManager (lub
///                                 pierwszym znalezionym na scenie).
///                                 Sortuje wg ścieżki pliku (= alphabetycznie).
///
///   3. Zrób oba na raz          – wywołuje 1 i 2 w kolejności.
///
/// ── FOLDER SKANOWANIA ────────────────────────────────────────────────────────
///   const string SCAN_PATH = "Assets/Main folder/ScriptableObjects/Meta"
///   Zmień jeśli struktura projektu jest inna.
/// </summary>
public static class MetaUpgradeTools
{
    private const string SCAN_PATH = "Assets/Main folder/ScriptableObjects/Meta";

    // =========================================================================
    // MENU ITEMS
    // =========================================================================

    [MenuItem("Tools/Meta Upgrades/1. Nadaj Unikatowe ID")]
    public static void AssignUniqueIDs()
    {
        var upgrades = LoadAllUpgrades();
        if (upgrades.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Brak Upgradów",
                $"Nie znaleziono żadnych MetaUpgradeSO w:\n{SCAN_PATH}",
                "OK");
            return;
        }

        int assigned  = 0;
        int conflicts = 0;

        // Zbierz istniejące IDs żeby wykryć duplikaty
        var seenIds = new Dictionary<string, MetaUpgradeSO>();

        // Pierwsza pętla: zbierz oryginalne IDs
        foreach (var upgrade in upgrades)
        {
            if (!string.IsNullOrWhiteSpace(upgrade.id))
            {
                if (seenIds.ContainsKey(upgrade.id))
                {
                    // Duplikat — wyczyść żeby w drugiej pętli dostał nowe ID
                    upgrade.id = "";
                    conflicts++;
                }
                else
                {
                    seenIds[upgrade.id] = upgrade;
                }
            }
        }

        // Druga pętla: nadaj ID tym co nie mają lub miały duplikat
        foreach (var upgrade in upgrades)
        {
            if (!string.IsNullOrWhiteSpace(upgrade.id)) continue;

            string assetPath = AssetDatabase.GetAssetPath(upgrade);
            string fileName  = Path.GetFileNameWithoutExtension(assetPath);
            string baseId    = ToSnakeCase(fileName);
            string finalId   = baseId;

            // Jeśli i ten ID jest zajęty — dodaj suffix _2, _3 itd.
            int suffix = 2;
            while (seenIds.ContainsKey(finalId))
            {
                finalId = $"{baseId}_{suffix}";
                suffix++;
            }

            upgrade.id   = finalId;
            seenIds[finalId] = upgrade;

            EditorUtility.SetDirty(upgrade);
            assigned++;
        }

        AssetDatabase.SaveAssets();

        string summary =
            $"Nadano ID: {assigned} upgradów.\n" +
            (conflicts > 0 ? $"Naprawiono duplikatów: {conflicts}.\n" : "") +
            $"\nSkanowany folder:\n{SCAN_PATH}";

        Debug.Log($"[MetaUpgradeTools] {summary.Replace("\n", " ")}");
        EditorUtility.DisplayDialog("Gotowe — Unikatowe ID", summary, "OK");
    }

    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Meta Upgrades/2. Przypisz do MetaUpgradeManager")]
    public static void AssignToManager()
    {
        var upgrades = LoadAllUpgrades();
        if (upgrades.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Brak Upgradów",
                $"Nie znaleziono żadnych MetaUpgradeSO w:\n{SCAN_PATH}",
                "OK");
            return;
        }

        // Znajdź manager — preferuj zaznaczony obiekt w Hierarchy
        MetaUpgradeManager manager = null;

        if (Selection.activeGameObject != null)
            manager = Selection.activeGameObject.GetComponent<MetaUpgradeManager>();

        if (manager == null)
            manager = Object.FindObjectOfType<MetaUpgradeManager>();

        if (manager == null)
        {
            EditorUtility.DisplayDialog(
                "Brak MetaUpgradeManager",
                "Nie znaleziono MetaUpgradeManager na scenie.\n\n" +
                "Dodaj komponent MetaUpgradeManager do GameObject'u na scenie " +
                "i spróbuj ponownie.",
                "OK");
            return;
        }

        // Sortuj wg ścieżki (= alphabetycznie = tier I przed II przed III)
        upgrades.Sort((a, b) =>
        {
            string pathA = AssetDatabase.GetAssetPath(a);
            string pathB = AssetDatabase.GetAssetPath(b);
            return string.Compare(pathA, pathB, System.StringComparison.Ordinal);
        });

        Undo.RecordObject(manager, "Przypisz MetaUpgradeSO do Manager");
        manager.allUpgrades = upgrades;
        EditorUtility.SetDirty(manager);

        // Zapisz scenę (żeby lista nie zniknęła po zamknięciu bez save)
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            manager.gameObject.scene);

        string summary =
            $"Przypisano {upgrades.Count} upgradów\n" +
            $"do: {manager.gameObject.name}\n\n" +
            $"Folder: {SCAN_PATH}";

        Debug.Log($"[MetaUpgradeTools] Przypisano {upgrades.Count} upgradów do {manager.gameObject.name}.");
        EditorUtility.DisplayDialog("Gotowe — Manager Zaktualizowany", summary, "OK");

        // Zaznacz manager w Hierarchy
        Selection.activeGameObject = manager.gameObject;
    }

    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Meta Upgrades/3. Zrób oba na raz")]
    public static void AssignIDsAndManager()
    {
        AssignUniqueIDs();
        AssignToManager();
    }

    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Meta Upgrades/── Otwórz Graf Zależności")]
    public static void OpenGraph()
    {
        MetaUpgradeEditorWindow.OpenWindow();
    }

    // =========================================================================
    // WALIDACJA (pokazuje ilość upgradów w menu)
    // =========================================================================

    [MenuItem("Tools/Meta Upgrades/1. Nadaj Unikatowe ID", true)]
    [MenuItem("Tools/Meta Upgrades/2. Przypisz do MetaUpgradeManager", true)]
    [MenuItem("Tools/Meta Upgrades/3. Zrób oba na raz", true)]
    private static bool ValidateMenu()
    {
        // Szybki check — czy folder istnieje
        return AssetDatabase.IsValidFolder(SCAN_PATH);
    }

    // =========================================================================
    // CORE — ładowanie wszystkich upgradów z folderu
    // =========================================================================

    /// <summary>
    /// Ładuje wszystkie MetaUpgradeSO z folderu SCAN_PATH (rekurencyjnie).
    /// </summary>
    public static List<MetaUpgradeSO> LoadAllUpgrades()
    {
        var result = new List<MetaUpgradeSO>();

        if (!AssetDatabase.IsValidFolder(SCAN_PATH))
        {
            Debug.LogWarning($"[MetaUpgradeTools] Folder nie istnieje: {SCAN_PATH}");
            return result;
        }

        // Znajdź wszystkie pliki .asset w folderze (rekurencyjnie)
        string[] guids = AssetDatabase.FindAssets("t:MetaUpgradeSO", new[] { SCAN_PATH });

        foreach (string guid in guids)
        {
            string path    = AssetDatabase.GUIDToAssetPath(guid);
            var    upgrade = AssetDatabase.LoadAssetAtPath<MetaUpgradeSO>(path);
            if (upgrade != null)
                result.Add(upgrade);
        }

        Debug.Log($"[MetaUpgradeTools] Znaleziono {result.Count} MetaUpgradeSO w '{SCAN_PATH}'.");
        return result;
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    /// <summary>
    /// Konwertuje nazwę pliku na snake_case.
    /// "WoodStorage_I" → "wood_storage_i"
    /// "Meta Wood Bonus" → "meta_wood_bonus"
    /// </summary>
    /// <summary>
    /// Konwertuje nazwę pliku na snake_case.
    /// "WoodStorage_I" → "wood_storage_i"
    /// "Meta Wood Bonus" → "meta_wood_bonus"
    /// Publiczna żeby MetaUpgradeEditorWindow mogło jej użyć przy tworzeniu nowych SO.
    /// </summary>
    public static string ToSnakeCasePublic(string input) => ToSnakeCase(input);

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return "upgrade";

        // Wstaw _ przed wielkimi literami poprzedzonymi małymi (CamelCase)
        var result = System.Text.RegularExpressions.Regex.Replace(
            input,
            @"(?<=[a-z0-9])([A-Z])",
            "_$1");

        // Zamień spacje i myślniki na _
        result = result.Replace(' ', '_').Replace('-', '_');

        // Usuń niedozwolone znaki (tylko alfanumeryczne i _)
        result = System.Text.RegularExpressions.Regex.Replace(
            result, @"[^a-zA-Z0-9_]", "");

        // Usuń podwójne podkreślniki
        result = System.Text.RegularExpressions.Regex.Replace(
            result, @"_{2,}", "_");

        return result.ToLower().Trim('_');
    }
}

/// <summary>
/// Rozszerzenie Inspektora dla MetaUpgradeManager —
/// dodaje przyciski "Nadaj ID" i "Załaduj Upgrady" bezpośrednio w komponencie.
/// </summary>
[CustomEditor(typeof(MetaUpgradeManager))]
public class MetaUpgradeManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("── Narzędzia ─────────────────────────", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("📁 Załaduj wszystkie SO", GUILayout.Height(30)))
            MetaUpgradeTools.AssignToManager();

        if (GUILayout.Button("🔑 Nadaj ID", GUILayout.Height(30)))
            MetaUpgradeTools.AssignUniqueIDs();

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("🔄 Zrób oba na raz", GUILayout.Height(28)))
            MetaUpgradeTools.AssignIDsAndManager();

        EditorGUILayout.Space(4);

        if (GUILayout.Button("📊 Otwórz Graf Zależności", GUILayout.Height(28)))
            MetaUpgradeEditorWindow.OpenWindow();

        // Podgląd statystyk
        var manager = (MetaUpgradeManager)target;
        if (manager.allUpgrades != null && manager.allUpgrades.Count > 0)
        {
            EditorGUILayout.Space(4);
            int unlocked = manager.allUpgrades.Count(u => u != null && u.isUnlocked);
            int noId     = manager.allUpgrades.Count(u => u != null && string.IsNullOrWhiteSpace(u.id));

            EditorGUILayout.HelpBox(
                $"Łącznie: {manager.allUpgrades.Count} upgradów  |  " +
                $"Odblokowanych: {unlocked}  |  " +
                $"Bez ID: {noId}",
                noId > 0 ? MessageType.Warning : MessageType.Info);
        }
    }
}
#endif