#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class RuneManagerEditorWindow : EditorWindow
{
    // Ścieżka bazowa, o którą prosiłeś
    private const string BASE_PATH = "Assets/Main folder/ScriptableObjects/Runes/Runes";

    // Mapowanie folderów dla poszczególnych rzadkości
    private readonly Dictionary<RuneRarity, string> folderMap = new Dictionary<RuneRarity, string>
    {
        { RuneRarity.Common, "1.Common" },
        { RuneRarity.Uncommon, "2.Uncommon" },
        { RuneRarity.Rare, "3.Rare" },
        { RuneRarity.Legendary, "4.Legendary" },
        { RuneRarity.Cursed, "5.Cursed" }
    };

    private RuneSystemConfigSO systemConfig;
    
    // Pule załadowanych run
    private Dictionary<RuneRarity, List<RuneDefinitionSO>> allRunes = new Dictionary<RuneRarity, List<RuneDefinitionSO>>();

    // Stan UI
    private RuneDefinitionSO selectedRune;
    private SerializedObject serializedRune;
    
    private Vector2 listScrollPos;
    private Vector2 editorScrollPos;
    private Vector2 refScrollPos;
    
    // Do tworzenia nowych run (automatyczne nazewnictwo)
    private RuneRarity newRuneRarity = RuneRarity.Common;
    private RuneStatType newRuneStat = RuneStatType.Damage;
    private RuneValueType newRuneValueType = RuneValueType.Flat;

    [MenuItem("Tools/Rune Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<RuneManagerEditorWindow>("Zarządzanie Runami");
        window.minSize = new Vector2(900, 600);
        window.Show();
    }

    private void OnEnable()
    {
        LoadAllRunes();

        // Automatyczne znalezienie RuneSystemConfigSO w projekcie
        string[] configGuids = AssetDatabase.FindAssets("t:RuneSystemConfigSO");
        if (configGuids.Length > 0)
        {
            string configPath = AssetDatabase.GUIDToAssetPath(configGuids[0]);
            systemConfig = AssetDatabase.LoadAssetAtPath<RuneSystemConfigSO>(configPath);
        }
    }

    private void LoadAllRunes()
    {
        allRunes.Clear();
        foreach (RuneRarity rarity in System.Enum.GetValues(typeof(RuneRarity)))
        {
            allRunes[rarity] = new List<RuneDefinitionSO>();
            
            string fullPath = $"{BASE_PATH}/{folderMap[rarity]}";
            
            // Tworzenie folderów jeśli nie istnieją
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                AssetDatabase.Refresh();
            }

            // Szukanie assetów w folderze
            string[] guids = AssetDatabase.FindAssets("t:RuneDefinitionSO", new[] { fullPath });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                RuneDefinitionSO rune = AssetDatabase.LoadAssetAtPath<RuneDefinitionSO>(assetPath);
                if (rune != null)
                {
                    allRunes[rarity].Add(rune);
                }
            }
            
            // Sortowanie po nazwie dla porządku
            allRunes[rarity] = allRunes[rarity].OrderBy(r => r.primaryStat).ThenBy(r => r.name).ToList();
        }
    }

    private void OnGUI()
    {
        DrawTopBar();

        EditorGUILayout.BeginHorizontal();
        
        DrawReferencePanel(); // Lewy panel (Poprzedni Tier)
        DrawRuneListPanel();  // Środkowy panel (Lista)
        DrawEditorPanel();    // Prawy panel (Edycja)

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================================
    // GÓRNY PASEK (Tworzenie i Odświeżanie)
    // =========================================================================
    private void DrawTopBar()
    {
        // --- LINIA 1: KREATOR RUN ---
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        if (GUILayout.Button("🔄 Odśwież bazę plików", EditorStyles.toolbarButton, GUILayout.Width(140)))
        {
            LoadAllRunes();
        }

        GUILayout.Space(20);
        GUILayout.Label("Kreator Nowej Runy:", GUILayout.Width(130));
        
        newRuneRarity = (RuneRarity)EditorGUILayout.EnumPopup(newRuneRarity, GUILayout.Width(90));
        newRuneValueType = (RuneValueType)EditorGUILayout.EnumPopup(newRuneValueType, GUILayout.Width(70));
        newRuneStat = (RuneStatType)EditorGUILayout.EnumPopup(newRuneStat, GUILayout.Width(130));

        string generatedName = $"{newRuneRarity} {newRuneValueType} {newRuneStat}";
        
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField(generatedName, GUILayout.ExpandWidth(true));
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("➕ Utwórz nową runę", EditorStyles.toolbarButton, GUILayout.Width(150)))
        {
            CreateNewRune(generatedName);
        }

        EditorGUILayout.EndHorizontal();

        // --- LINIA 2: SYNCHRONIZACJA Z CONFIGIEM ---
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        GUILayout.Label("Rune System Config:", GUILayout.Width(140));
        systemConfig = (RuneSystemConfigSO)EditorGUILayout.ObjectField(systemConfig, typeof(RuneSystemConfigSO), false, GUILayout.Width(250));

        // Guzik aktywny tylko, jeśli mamy podpięty Config
        EditorGUI.BeginDisabledGroup(systemConfig == null);
        if (GUILayout.Button("⚡ Automatycznie przypisz wszystkie runy do Configu", EditorStyles.toolbarButton))
        {
            SyncRunesToConfig();
        }
        EditorGUI.EndDisabledGroup();

        if (systemConfig != null)
        {
            if (GUILayout.Button("Pokaż w plikach", EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
                EditorGUIUtility.PingObject(systemConfig);
                Selection.activeObject = systemConfig;
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================================
    // LEWY PANEL (Porównanie z niższym tierem)
    // =========================================================================
    private void DrawReferencePanel()
    {
        EditorGUILayout.BeginVertical("box", GUILayout.Width(220));
        GUILayout.Label("Porównanie (Niższy Tier)", EditorStyles.boldLabel);
        DrawSeparator();

        refScrollPos = EditorGUILayout.BeginScrollView(refScrollPos);

        if (selectedRune == null)
        {
            GUILayout.Label("Wybierz runę z listy...", EditorStyles.wordWrappedLabel);
        }
        else if (selectedRune.rarity == RuneRarity.Common)
        {
            GUILayout.Label("Runa Common to najniższy tier.\nBrak niższych run do porównania.", EditorStyles.wordWrappedLabel);
        }
        else
        {
            RuneRarity lowerTier = selectedRune.rarity - 1;
            
            // Szukamy run z niższej rzadkości, które mają tę samą statystykę (np. Zasięg) i typ wartości (Flat/%)
            var matches = allRunes[lowerTier]
                .Where(r => r.primaryStat == selectedRune.primaryStat && r.valueType == selectedRune.valueType)
                .ToList();

            if (matches.Count == 0)
            {
                GUILayout.Label($"Brak run {lowerTier} \no statystyce {selectedRune.primaryStat} ({selectedRune.valueType}).", EditorStyles.helpBox);
            }
            else
            {
                foreach (var match in matches)
                {
                    EditorGUILayout.BeginVertical("helpbox");
                    GUILayout.Label(match.runeName, EditorStyles.boldLabel);
                    GUILayout.Label($"Stat: {match.primaryStat}");
                    GUILayout.Label($"Typ: {match.valueType}");
                    
                    // Podkolorowanie wartości na zielono dla lepszej czytelności
                    GUIStyle valStyle = new GUIStyle(EditorStyles.label);
                    valStyle.normal.textColor = new Color(0.2f, 0.8f, 0.2f);
                    valStyle.fontStyle = FontStyle.Bold;

                    GUILayout.Label($"MIN: {match.primaryValueRange.x}", valStyle);
                    GUILayout.Label($"MAX: {match.primaryValueRange.y}", valStyle);
                    
                    if (GUILayout.Button("Wybierz tę runę", EditorStyles.miniButton))
                    {
                        SelectRune(match);
                    }
                    EditorGUILayout.EndVertical();
                    GUILayout.Space(5);
                }
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // =========================================================================
    // ŚRODKOWY PANEL (Lista wszystkich run)
    // =========================================================================
    private void DrawRuneListPanel()
    {
        EditorGUILayout.BeginVertical("box", GUILayout.Width(250));
        listScrollPos = EditorGUILayout.BeginScrollView(listScrollPos);

        foreach (RuneRarity rarity in System.Enum.GetValues(typeof(RuneRarity)))
        {
            // Kolorowanie nagłówków kategorii
            GUI.backgroundColor = GetColorForRarity(rarity);
            GUILayout.Label($"=== {rarity.ToString().ToUpper()} ({allRunes[rarity].Count}) ===", EditorStyles.toolbarButton);
            GUI.backgroundColor = Color.white;

            foreach (var rune in allRunes[rarity])
            {
                if (rune == null) continue;

                GUI.backgroundColor = (selectedRune == rune) ? new Color(0.5f, 0.8f, 1f) : Color.white;
                
                string shortType = rune.valueType == RuneValueType.Percent ? "%" : "F";
                string btnText = $"{rune.name} [{rune.primaryStat} {shortType}]";

                if (GUILayout.Button(btnText, EditorStyles.miniButton))
                {
                    SelectRune(rune);
                }
                GUI.backgroundColor = Color.white;
            }
            GUILayout.Space(10);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // =========================================================================
    // PRAWY PANEL (Edytor zaznaczonej runy)
    // =========================================================================
    private void DrawEditorPanel()
    {
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true));
        
        if (selectedRune == null || serializedRune == null)
        {
            GUILayout.Label("Nie wybrano żadnej runy.", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
            return;
        }

        editorScrollPos = EditorGUILayout.BeginScrollView(editorScrollPos);

        serializedRune.Update();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"Edycja: {selectedRune.name}", EditorStyles.largeLabel);
        if (GUILayout.Button("Pokaż w plikach", GUILayout.Width(100)))
        {
            EditorGUIUtility.PingObject(selectedRune);
        }
        EditorGUILayout.EndHorizontal();
        DrawSeparator();

        // Podstawowe dane
        EditorGUILayout.PropertyField(serializedRune.FindProperty("runeName"));
        EditorGUILayout.PropertyField(serializedRune.FindProperty("icon"));
        
        EditorGUI.BeginDisabledGroup(true); // Rarity blokujemy, bo wpływa na folder. Twórz nowe zamiast zmieniać tu.
        EditorGUILayout.PropertyField(serializedRune.FindProperty("rarity"));
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(10);

        // Główne statystyki (Buff)
        EditorGUILayout.LabelField("--- Statystyki Główne (Buff) ---", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedRune.FindProperty("primaryStat"));
        EditorGUILayout.PropertyField(serializedRune.FindProperty("valueType"));
        
        SerializedProperty rangeProp = serializedRune.FindProperty("primaryValueRange");
        Vector2 rangeVal = rangeProp.vector2Value;
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Zakres Wartości (X=Min, Y=Max):", GUILayout.Width(200));
        rangeVal.x = EditorGUILayout.FloatField(rangeVal.x);
        rangeVal.y = EditorGUILayout.FloatField(rangeVal.y);
        EditorGUILayout.EndHorizontal();
        rangeProp.vector2Value = rangeVal;

        EditorGUILayout.Space(15);

        // Kary (Tylko dla Cursed)
        if (selectedRune.rarity == RuneRarity.Cursed)
        {
            EditorGUILayout.LabelField("--- Kary Przeklętej Runy (Debuff) ---", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Wartości kar wpisuj jako dodatnie! System automatycznie odejmie je od statystyk gracza.", MessageType.Warning);
            
            SerializedProperty penaltiesProp = serializedRune.FindProperty("possiblePenalties");
            EditorGUILayout.PropertyField(penaltiesProp, new GUIContent("Lista Kar (Losowana 1)"), true);
        }

        serializedRune.ApplyModifiedProperties();

        // ── NOWE: SEKCJA USUWANIA ────────────────────────────────────────────────
        GUILayout.Space(30);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace(); // Wypycha guzik do prawej strony
        
        GUI.backgroundColor = new Color(0.9f, 0.2f, 0.2f); // Czerwony kolor przycisku
        if (GUILayout.Button("🗑 Usuń tę runę", GUILayout.Width(150), GUILayout.Height(30)))
        {
            DeleteSelectedRune();
        }
        GUI.backgroundColor = Color.white; // Reset koloru
        
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(10);
        // ─────────────────────────────────────────────────────────────────────────

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // =========================================================================
    // LOGIKA
    // =========================================================================

    private void SelectRune(RuneDefinitionSO rune)
    {
        selectedRune = rune;
        serializedRune = new SerializedObject(selectedRune);
        GUI.FocusControl(null); // Odznacza text fieldy po zmianie
    }

    private void CreateNewRune(string generatedName)
    {
        // Spacja zamieniana na podkreślnik do nazwy pliku, np: Common_Flat_Damage.asset
        string safeFileName = generatedName.Replace(" ", "_");
        string path = $"{BASE_PATH}/{folderMap[newRuneRarity]}/{safeFileName}.asset";

        if (File.Exists(path))
        {
            // Jeśli plik już istnieje, dodajemy (1), (2) itd., żeby nie nadpisać
            int suffix = 1;
            string altPath;
            do
            {
                altPath = $"{BASE_PATH}/{folderMap[newRuneRarity]}/{safeFileName}_{suffix}.asset";
                suffix++;
            } 
            while (File.Exists(altPath));
            
            path = altPath;
            
        }

        RuneDefinitionSO newRune = CreateInstance<RuneDefinitionSO>();
        newRune.runeName = generatedName; // Nazwa wyświetlana w grze (ze spacjami)
        newRune.rarity = newRuneRarity;
        
        // Automatyczne uzupełnienie statystyk na podstawie kreatora!
        newRune.primaryStat = newRuneStat;
        newRune.valueType = newRuneValueType;
        newRune.primaryValueRange = new Vector2(1, 5); // Domyślne wartości

        AssetDatabase.CreateAsset(newRune, path);
        AssetDatabase.SaveAssets();

        LoadAllRunes(); // Odśwież listę po lewej
        SelectRune(newRune); // Zaznacz nowo utworzoną, żeby od razu edytować jej widełki
    }

    private void DrawSeparator()
    {
        EditorGUILayout.Space(5);
        Rect rect = EditorGUILayout.GetControlRect(false, 2);
        EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f));
        EditorGUILayout.Space(5);
    }

    private Color GetColorForRarity(RuneRarity rarity)
    {
        return rarity switch
        {
            RuneRarity.Common => new Color(0.8f, 0.8f, 0.8f),
            RuneRarity.Uncommon => new Color(0.3f, 0.8f, 0.3f),
            RuneRarity.Rare => new Color(0.3f, 0.6f, 1f),
            RuneRarity.Legendary => new Color(1f, 0.8f, 0.2f),
            RuneRarity.Cursed => new Color(0.9f, 0.2f, 0.2f),
            _ => Color.white
        };
    }
    
    private void DeleteSelectedRune()
    {
        if (selectedRune == null) return;

        // Okienko z ostrzeżeniem
        bool confirm = EditorUtility.DisplayDialog(
            "Usuń Runę",
            $"Czy na pewno chcesz TRWALE USUNĄĆ runę '{selectedRune.runeName}'?\n\nTej operacji nie można cofnąć!",
            "Tak, usuń", 
            "Anuluj"
        );

        if (confirm)
        {
            // Pobranie ścieżki do pliku
            string path = AssetDatabase.GetAssetPath(selectedRune);
            
            // Czyszczenie zaznaczenia w UI (żeby uniknąć błędów odświeżania na usuniętym obiekcie)
            selectedRune = null;
            serializedRune = null;

            // Usunięcie assetu
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Przeładowanie listy
            LoadAllRunes();
            
            Debug.Log($"[RuneManager] Usunięto runę z lokalizacji: {path}");
        }
    }
    private void SyncRunesToConfig()
    {
        if (systemConfig == null) return;

        // Najpierw upewnijmy się, że narzędzie ma załadowane najnowsze pliki
        LoadAllRunes();

        // Rejestrujemy operację dla systemu cofania (Ctrl+Z) w Unity
        Undo.RecordObject(systemConfig, "Zaktualizowano listy run w Configu");

        // Wypełnianie list
        systemConfig.commonRunes    = new List<RuneDefinitionSO>(allRunes[RuneRarity.Common]);
        systemConfig.uncommonRunes  = new List<RuneDefinitionSO>(allRunes[RuneRarity.Uncommon]);
        systemConfig.rareRunes      = new List<RuneDefinitionSO>(allRunes[RuneRarity.Rare]);
        systemConfig.legendaryRunes = new List<RuneDefinitionSO>(allRunes[RuneRarity.Legendary]);
        systemConfig.cursedRunes    = new List<RuneDefinitionSO>(allRunes[RuneRarity.Cursed]);

        // Oznaczamy plik jako zmieniony i zapisujemy na dysk
        EditorUtility.SetDirty(systemConfig);
        AssetDatabase.SaveAssets();

        int totalRunes = systemConfig.commonRunes.Count + systemConfig.uncommonRunes.Count + 
                         systemConfig.rareRunes.Count + systemConfig.legendaryRunes.Count + 
                         systemConfig.cursedRunes.Count;

        Debug.Log($"<color=green>[RuneManager] Pomyślnie przypisano {totalRunes} run do konfiguracji!</color>");
        EditorUtility.DisplayDialog("Sukces", $"Zsynchronizowano {totalRunes} run z plikiem konfiguracyjnym.", "OK");
    }
    
}
#endif