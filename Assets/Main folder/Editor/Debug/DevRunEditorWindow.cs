#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Okno edytorskie (Tools -> DEV RUN) oraz automatyczny mechanizm przywracania stanu
/// ScriptableObjectów po zakończeniu Play Mode.
/// </summary>
[InitializeOnLoad]
public class DevRunEditorWindow : EditorWindow
{
    private const string SnapshotFlagKey = "DwarfenTD_DevRun_HasSnapshot";
    private const string SnapshotMetaKey = "DwarfenTD_DevRun_MetaSnapshot";
    private const string SnapshotTowerKey = "DwarfenTD_DevRun_TowerSnapshot";

    private Vector2 scrollPos;
    private bool showDetails = true;
    private List<MetaUpgradeSO> cachedUpgrades = new List<MetaUpgradeSO>();
    private List<TowerData> cachedTowers = new List<TowerData>();

    [Serializable]
    private struct AssetBoolEntry
    {
        public string guid;
        public bool value;
    }

    [Serializable]
    private class AssetBoolSnapshot
    {
        public List<AssetBoolEntry> entries = new List<AssetBoolEntry>();
    }

    static DevRunEditorWindow()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Tools/DEV RUN", priority = 10)]
    public static void OpenWindow()
    {
        var window = GetWindow<DevRunEditorWindow>("DEV RUN");
        window.minSize = new Vector2(460f, 420f);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshAssetCache();
    }

    private void OnFocus()
    {
        RefreshAssetCache();
    }

    private void RefreshAssetCache()
    {
        cachedUpgrades = LoadAllAssets<MetaUpgradeSO>("t:MetaUpgradeSO")
            .OrderBy(u => u.upgradeName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        cachedTowers = LoadAllAssets<TowerData>("t:TowerData")
            .OrderBy(t => t.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void OnGUI()
    {
        DrawHeaderBanner();

        EditorGUILayout.Space(8);

        bool prevEnabled = DevRunConfig.IsEnabled;
        bool newEnabled = EditorGUILayout.ToggleLeft(
            new GUIContent(
                "Aktywuj DEV RUN (nadpisz wszystko przy starcie Play Mode)",
                "Gdy zaznaczone, nadpisuje progresję meta, wieże oraz budynki w pamięci na czas Play Mode bez ruszania pliku player_progress.json."),
            prevEnabled,
            EditorStyles.boldLabel);

        if (newEnabled != prevEnabled)
        {
            DevRunConfig.IsEnabled = newEnabled;
        }

        EditorGUILayout.Space(8);

        using (new EditorGUI.DisabledScope(!newEnabled))
        {
            EditorGUILayout.LabelField("Wybierz preset DEV RUN:", EditorStyles.boldLabel);
            DevRunMode currentMode = DevRunConfig.Mode;
            string[] modeLabels = new[] { "Wszystkie", "Kluczowe", "Żadne" };
            int selectedIdx = EditorGUILayout.Popup("Tryb Meta Progresji", (int)currentMode, modeLabels);
            if (selectedIdx != (int)currentMode)
            {
                currentMode = (DevRunMode)selectedIdx;
                DevRunConfig.Mode = currentMode;
            }

            EditorGUILayout.Space(4);
            DrawModeButtons(currentMode);
            EditorGUILayout.Space(8);
            DrawModeDescription(currentMode);
            EditorGUILayout.Space(8);
            DrawPreviewList(currentMode);
        }

        if (!newEnabled)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "DEV RUN jest wyłączony. Gra uruchomi się normalnie z pliku zapisu (player_progress.json) i ustawień na scenie.",
                MessageType.Info);
        }

        if (Application.isPlaying && newEnabled)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Zmiany trybu DEV RUN są nakładane przy uruchomieniu Play Mode. Zrestartuj Play Mode, aby zastosować nowy preset.",
                MessageType.Warning);
        }
    }

    private void DrawHeaderBanner()
    {
        bool active = DevRunConfig.IsEnabled;
        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = active ? new Color(1.0f, 0.72f, 0.25f) : new Color(0.75f, 0.78f, 0.82f);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUI.backgroundColor = prevBg;
            string title = active
                ? $"DEV RUN: AKTYWNY ({GetModeDisplayName(DevRunConfig.Mode)})"
                : "DEV RUN: WYŁĄCZONY";
            EditorGUILayout.LabelField(title, EditorStyles.largeLabel);
            EditorGUILayout.LabelField(
                "Działa z MainMenu i bezpośrednio z Game scene. Nie modyfikuje pliku player_progress.json ani assetów na dysku.",
                EditorStyles.wordWrappedMiniLabel);
        }
    }

    private void DrawModeButtons(DevRunMode currentMode)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawPresetButton("Wszystkie", DevRunMode.All, currentMode);
            DrawPresetButton("Kluczowe", DevRunMode.KeyNodes, currentMode);
            DrawPresetButton("Żadne", DevRunMode.None, currentMode);
        }
    }

    private void DrawPresetButton(string label, DevRunMode mode, DevRunMode currentMode)
    {
        bool isSelected = mode == currentMode;
        Color prev = GUI.backgroundColor;
        if (isSelected) GUI.backgroundColor = new Color(0.35f, 0.85f, 0.45f);

        if (GUILayout.Button(label, GUILayout.Height(28)))
        {
            DevRunConfig.Mode = mode;
        }

        GUI.backgroundColor = prev;
    }

    private void DrawModeDescription(DevRunMode mode)
    {
        string desc = mode switch
        {
            DevRunMode.All =>
                "WSZYSTKIE: Odblokowuje całe drzewko meta-progresji (węzły A/B używają wariantu A, aby nie dublować statystyk), wszystkie wieże obronne oraz Wieżę Druida.",
            DevRunMode.KeyNodes =>
                "KLUCZOWE: Odblokowuje wszystkie budynki i ich funkcje (Moc Trójcy: Kuźnia Runiczna, Wieża Runiczna, Warsztat Narzędziowy; limit Wież Runicznych; wszystkie wieże obronne; Wieża Druida) oraz pełny system run (wszystkie rzadkości dropu + Fuzja, Transmutacja, Cursed Crafting). Bez statystyk pasywnych, surowców startowych i murów.",
            DevRunMode.None =>
                "ŻADNE: Czysty start jak nowa gra. Wszystkie węzły meta wyłączone, drop wyłącznie run Common, nadpisuje flagi unlockAllTowersForDebug (zostają tylko 2 wieże startowe).",
            _ => string.Empty
        };

        EditorGUILayout.HelpBox(desc, MessageType.None);
    }

    private void DrawPreviewList(DevRunMode mode)
    {
        if (cachedUpgrades.Count == 0 && cachedTowers.Count == 0)
        {
            RefreshAssetCache();
        }

        var secondary = DevRunConfig.CollectSecondaryVariants(cachedUpgrades);
        var unlockedNodes = new List<MetaUpgradeSO>();
        var lockedNodes = new List<MetaUpgradeSO>();
        var placeholderNodes = new List<MetaUpgradeSO>();

        for (int i = 0; i < cachedUpgrades.Count; i++)
        {
            var u = cachedUpgrades[i];
            if (u == null) continue;
            if (u.IsPlaceholder)
                placeholderNodes.Add(u);
            else if (DevRunConfig.ShouldUnlockUpgrade(u, mode, secondary))
                unlockedNodes.Add(u);
            else
                lockedNodes.Add(u);
        }

        int unlockedTowerCount = DevRunConfig.ShouldUnlockAllTowers(mode)
            ? cachedTowers.Count
            : cachedTowers.Count(t => t != null &&
                (Array.IndexOf(SaveManager.DefaultStarterTowerIDs, t.name) >= 0 ||
                 Array.IndexOf(SaveManager.DefaultStarterTowerIDs, t.buildingName) >= 0));

        EditorGUILayout.BeginHorizontal();
        showDetails = EditorGUILayout.Foldout(
            showDetails,
            $"Podgląd: Meta {unlockedNodes.Count}/{cachedUpgrades.Count - placeholderNodes.Count}  |  Placeholdery {placeholderNodes.Count}  |  Wieże {unlockedTowerCount}/{cachedTowers.Count}",
            true);
        if (GUILayout.Button("Odśwież listę", GUILayout.Width(100)))
        {
            RefreshAssetCache();
        }
        EditorGUILayout.EndHorizontal();

        if (!showDetails) return;

        using (var scroll = new EditorGUILayout.ScrollViewScope(scrollPos, EditorStyles.helpBox))
        {
            scrollPos = scroll.scrollPosition;

            if (DevRunConfig.ShouldInjectDruidTowerUnlock(mode))
            {
                EditorGUILayout.LabelField("[+] UnlockDruidTower (wstrzykiwane w runtime dla Wieży Druida)", EditorStyles.miniLabel);
            }

            EditorGUILayout.LabelField($"Odblokowane węzły ({unlockedNodes.Count}):", EditorStyles.boldLabel);
            if (unlockedNodes.Count == 0)
            {
                EditorGUILayout.LabelField("  (brak)", EditorStyles.miniLabel);
            }
            else
            {
                for (int i = 0; i < unlockedNodes.Count; i++)
                {
                    var u = unlockedNodes[i];
                    EditorGUILayout.LabelField($"  [ON]  {u.upgradeName}  ({u.effectType})", EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField($"Zablokowane węzły ({lockedNodes.Count}):", EditorStyles.boldLabel);
            for (int i = 0; i < lockedNodes.Count; i++)
            {
                var u = lockedNodes[i];
                EditorGUILayout.LabelField($"  [OFF] {u.upgradeName}  ({u.effectType})", EditorStyles.miniLabel);
            }

            if (placeholderNodes.Count > 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField($"Placeholdery - zawsze pomijane ({placeholderNodes.Count}):", EditorStyles.boldLabel);
                for (int i = 0; i < placeholderNodes.Count; i++)
                {
                    var u = placeholderNodes[i];
                    EditorGUILayout.LabelField($"  [PLACEHOLDER] {u.upgradeName}", EditorStyles.miniLabel);
                }
            }
        }
    }

    private static string GetModeDisplayName(DevRunMode mode) => mode switch
    {
        DevRunMode.All => "Wszystkie",
        DevRunMode.KeyNodes => "Kluczowe",
        DevRunMode.None => "Żadne",
        _ => mode.ToString()
    };

    // =========================================================================
    // SNAPSHOT I PRZYWRACANIE STANU SCRIPTABLEOBJECTÓW PO PLAY MODE
    // =========================================================================

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            if (DevRunConfig.IsEnabled)
            {
                SaveAssetSnapshots();
            }
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            RestoreAssetSnapshotsIfPresent();
        }
    }

    private static void SaveAssetSnapshots()
    {
        var metaSnapshot = new AssetBoolSnapshot();
        foreach (string guid in AssetDatabase.FindAssets("t:MetaUpgradeSO"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<MetaUpgradeSO>(path);
            if (asset != null)
            {
                metaSnapshot.entries.Add(new AssetBoolEntry { guid = guid, value = asset.isUnlocked });
            }
        }

        var towerSnapshot = new AssetBoolSnapshot();
        foreach (string guid in AssetDatabase.FindAssets("t:TowerData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<TowerData>(path);
            if (asset != null)
            {
                towerSnapshot.entries.Add(new AssetBoolEntry { guid = guid, value = asset.isUnlocked });
            }
        }

        SessionState.SetString(SnapshotMetaKey, JsonUtility.ToJson(metaSnapshot));
        SessionState.SetString(SnapshotTowerKey, JsonUtility.ToJson(towerSnapshot));
        SessionState.SetBool(SnapshotFlagKey, true);
    }

    private static void RestoreAssetSnapshotsIfPresent()
    {
        if (!SessionState.GetBool(SnapshotFlagKey, false)) return;

        string metaJson = SessionState.GetString(SnapshotMetaKey, string.Empty);
        if (!string.IsNullOrEmpty(metaJson))
        {
            var metaSnapshot = JsonUtility.FromJson<AssetBoolSnapshot>(metaJson);
            if (metaSnapshot?.entries != null)
            {
                foreach (var entry in metaSnapshot.entries)
                {
                    string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                    if (string.IsNullOrEmpty(path)) continue;
                    var asset = AssetDatabase.LoadAssetAtPath<MetaUpgradeSO>(path);
                    if (asset != null)
                    {
                        asset.isUnlocked = entry.value;
                    }
                }
            }
        }

        string towerJson = SessionState.GetString(SnapshotTowerKey, string.Empty);
        if (!string.IsNullOrEmpty(towerJson))
        {
            var towerSnapshot = JsonUtility.FromJson<AssetBoolSnapshot>(towerJson);
            if (towerSnapshot?.entries != null)
            {
                foreach (var entry in towerSnapshot.entries)
                {
                    string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                    if (string.IsNullOrEmpty(path)) continue;
                    var asset = AssetDatabase.LoadAssetAtPath<TowerData>(path);
                    if (asset != null)
                    {
                        asset.isUnlocked = entry.value;
                    }
                }
            }
        }

        SessionState.EraseBool(SnapshotFlagKey);
        SessionState.EraseString(SnapshotMetaKey);
        SessionState.EraseString(SnapshotTowerKey);
    }

    private static List<T> LoadAllAssets<T>(string filter) where T : UnityEngine.Object
    {
        var result = new List<T>();
        string[] guids = AssetDatabase.FindAssets(filter);
        if (guids == null) return result;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) result.Add(asset);
        }
        return result;
    }
}
#endif
