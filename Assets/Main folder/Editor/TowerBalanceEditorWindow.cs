#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dedykowane okno edytora do kompleksowego zarządzania i balansowania wież (TowerData).
/// Uwzględnia statystyki bojowe, penetrację pancerza i magii, krytyki, DPS oraz symulację Target Dummy.
/// </summary>
public class TowerBalanceEditorWindow : EditorWindow
{
    private const string TowersFolderPath = "Assets/Main folder/ScriptableObjects/Towers";

    private enum SortOption
    {
        Name = 0,
        BaseDps = 1,
        CritDps = 2,
        DummyDps = 3,
        Range = 4,
        Damage = 5,
        FireRate = 6,
        ArmorPen = 7,
        MagicPen = 8
    }

    // --- Stan listy wież ---
    private List<TowerData> allTowers = new List<TowerData>();
    private TowerData selectedTower;
    private string searchFilter = "";
    private SortOption currentSort = SortOption.Name;
    private bool sortDescending = false;

    // --- Przewijanie widoków ---
    private Vector2 tableScroll;
    private Vector2 detailsScroll;

    // --- Symulator Target Dummy ---
    private float dummyArmor = 30f;
    private float dummyMagicResist = 30f;

    [MenuItem("Tools/Towers/Tower Balance Editor")]
    [MenuItem("Tools/Towers/Tower Data Modifier")]
    public static void ShowWindow()
    {
        var window = GetWindow<TowerBalanceEditorWindow>("Tower Balance & Combat Editor");
        window.minSize = new Vector2(1050, 680);
    }

    private void OnEnable()
    {
        RefreshTowers();
    }

    private void RefreshTowers()
    {
        allTowers.Clear();
        string[] guids = AssetDatabase.FindAssets("t:TowerData", new[] { TowersFolderPath });
        if (guids.Length == 0)
        {
            guids = AssetDatabase.FindAssets("t:TowerData");
        }

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var tower = AssetDatabase.LoadAssetAtPath<TowerData>(path);
            if (tower != null) allTowers.Add(tower);
        }

        SortTowersList();

        if (selectedTower == null && allTowers.Count > 0)
        {
            selectedTower = allTowers[0];
        }
    }

    private void SortTowersList()
    {
        switch (currentSort)
        {
            case SortOption.Name:
                allTowers = sortDescending
                    ? allTowers.OrderByDescending(t => !string.IsNullOrEmpty(t.buildingName) ? t.buildingName : t.name).ToList()
                    : allTowers.OrderBy(t => !string.IsNullOrEmpty(t.buildingName) ? t.buildingName : t.name).ToList();
                break;
            case SortOption.BaseDps:
                allTowers = sortDescending
                    ? allTowers.OrderByDescending(CalculateBaseDps).ToList()
                    : allTowers.OrderBy(CalculateBaseDps).ToList();
                break;
            case SortOption.CritDps:
                allTowers = sortDescending
                    ? allTowers.OrderByDescending(CalculateCritDps).ToList()
                    : allTowers.OrderBy(CalculateCritDps).ToList();
                break;
            case SortOption.DummyDps:
                allTowers = sortDescending
                    ? allTowers.OrderByDescending(t => CalculateDummyDps(t, dummyArmor, dummyMagicResist)).ToList()
                    : allTowers.OrderBy(t => CalculateDummyDps(t, dummyArmor, dummyMagicResist)).ToList();
                break;
            case SortOption.Range:
                allTowers = sortDescending
                    ? allTowers.OrderByDescending(t => t.baseRange).ToList()
                    : allTowers.OrderBy(t => t.baseRange).ToList();
                break;
            case SortOption.Damage:
                allTowers = sortDescending
                    ? allTowers.OrderByDescending(t => t.baseDamage).ToList()
                    : allTowers.OrderBy(t => t.baseDamage).ToList();
                break;
            case SortOption.FireRate:
                allTowers = sortDescending
                    ? allTowers.OrderByDescending(t => t.fireRate).ToList()
                    : allTowers.OrderBy(t => t.fireRate).ToList();
                break;
            case SortOption.ArmorPen:
                allTowers = sortDescending
                    ? allTowers.OrderByDescending(t => t.armorPenetration).ToList()
                    : allTowers.OrderBy(t => t.armorPenetration).ToList();
                break;
            case SortOption.MagicPen:
                allTowers = sortDescending
                    ? allTowers.OrderByDescending(t => t.magicPenetration).ToList()
                    : allTowers.OrderBy(t => t.magicPenetration).ToList();
                break;
        }
    }

    private void OnGUI()
    {
        DrawTopToolbar();
        DrawTargetDummyBar();

        EditorGUILayout.Space(2);

        // Układ hybrydowy: górna część (arkusz kalkulacyjny) + dolna część (szczegółowy inspektor)
        float totalHeight = position.height;
        float tableHeight = Mathf.Max(260f, totalHeight * 0.52f);

        DrawTowerSpreadsheetTable(tableHeight);

        EditorGUILayout.Space(4);

        DrawSelectedTowerDetails();
    }

    // =========================================================================
    // 1. TOP TOOLBAR
    // =========================================================================
    private void DrawTopToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUI.backgroundColor = new Color(0.35f, 0.85f, 0.45f);
        if (GUILayout.Button("+ Nowa Wieża", EditorStyles.toolbarButton, GUILayout.Width(110)))
        {
            CreateNewTowerAsset();
        }
        GUI.backgroundColor = Color.white;

        if (selectedTower != null)
        {
            if (GUILayout.Button("Duplikuj Wybraną", EditorStyles.toolbarButton, GUILayout.Width(125)))
            {
                DuplicateSelectedTower();
            }

            GUI.backgroundColor = new Color(0.95f, 0.45f, 0.45f);
            if (GUILayout.Button("Usuń Wieżę", EditorStyles.toolbarButton, GUILayout.Width(95)))
            {
                if (EditorUtility.DisplayDialog("Usuwanie wieży", $"Czy na pewno usunąć wieżę '{selectedTower.name}'?", "Tak, usuń", "Anuluj"))
                {
                    DeleteTowerAsset(selectedTower);
                    return;
                }
            }
            GUI.backgroundColor = Color.white;
        }

        if (GUILayout.Button("Odśwież", EditorStyles.toolbarButton, GUILayout.Width(75)))
        {
            RefreshTowers();
        }

        GUILayout.Space(15);

        // Wyszukiwarka
        EditorGUILayout.LabelField("Szukaj:", GUILayout.Width(45));
        string newFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(140));
        if (newFilter != searchFilter)
        {
            searchFilter = newFilter;
        }

        GUILayout.Space(15);

        // Sortowanie
        EditorGUILayout.LabelField("Sortuj:", GUILayout.Width(45));
        var newSort = (SortOption)EditorGUILayout.EnumPopup(currentSort, EditorStyles.toolbarDropDown, GUILayout.Width(120));
        if (newSort != currentSort)
        {
            currentSort = newSort;
            SortTowersList();
        }

        if (GUILayout.Button(sortDescending ? "▼ Malejąco" : "▲ Rosnąco", EditorStyles.toolbarButton, GUILayout.Width(85)))
        {
            sortDescending = !sortDescending;
            SortTowersList();
        }

        GUILayout.FlexibleSpace();

        EditorGUILayout.LabelField($"Zarejestrowane wieże: {allTowers.Count}", EditorStyles.miniLabel, GUILayout.Width(150));

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================================
    // 2. TARGET DUMMY BAR (SYMULATOR OBRANY I PENETRACJI)
    // =========================================================================
    private void DrawTargetDummyBar()
    {
        EditorGUILayout.BeginVertical("helpbox");
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField("🎯 Target Dummy (Symulator Wroga):", EditorStyles.boldLabel, GUILayout.Width(235));

        EditorGUILayout.LabelField("Pancerz:", GUILayout.Width(55));
        float newArmor = EditorGUILayout.Slider(dummyArmor, 0f, 100f, GUILayout.Width(150));
        if (Math.Abs(newArmor - dummyArmor) > 0.01f)
        {
            dummyArmor = newArmor;
            if (currentSort == SortOption.DummyDps) SortTowersList();
        }
        EditorGUILayout.LabelField($"{dummyArmor:0}%", EditorStyles.miniBoldLabel, GUILayout.Width(35));

        GUILayout.Space(15);

        EditorGUILayout.LabelField("Odporność (MR):", GUILayout.Width(100));
        float newMr = EditorGUILayout.Slider(dummyMagicResist, 0f, 100f, GUILayout.Width(150));
        if (Math.Abs(newMr - dummyMagicResist) > 0.01f)
        {
            dummyMagicResist = newMr;
            if (currentSort == SortOption.DummyDps) SortTowersList();
        }
        EditorGUILayout.LabelField($"{dummyMagicResist:0}%", EditorStyles.miniBoldLabel, GUILayout.Width(35));

        GUILayout.Space(15);

        // Presety testowe
        EditorGUILayout.LabelField("Presety:", EditorStyles.miniLabel, GUILayout.Width(50));
        if (GUILayout.Button("0%", EditorStyles.miniButton, GUILayout.Width(35))) SetDummyPreset(0f, 0f);
        if (GUILayout.Button("Lekki 15%", EditorStyles.miniButton, GUILayout.Width(70))) SetDummyPreset(15f, 15f);
        if (GUILayout.Button("Średni 35%", EditorStyles.miniButton, GUILayout.Width(75))) SetDummyPreset(35f, 35f);
        if (GUILayout.Button("Ciężki 60%", EditorStyles.miniButton, GUILayout.Width(75))) SetDummyPreset(60f, 60f);
        if (GUILayout.Button("Boss 80%", EditorStyles.miniButton, GUILayout.Width(65))) SetDummyPreset(80f, 80f);

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void SetDummyPreset(float armor, float mr)
    {
        dummyArmor = armor;
        dummyMagicResist = mr;
        if (currentSort == SortOption.DummyDps) SortTowersList();
    }

    // =========================================================================
    // 3. SPREADSHEET TABLE (GÓRNA MACIERZ PORÓWNAWCZA WIEŻ)
    // =========================================================================
    private void DrawTowerSpreadsheetTable(float height)
    {
        EditorGUILayout.BeginVertical("box", GUILayout.Height(height));

        tableScroll = EditorGUILayout.BeginScrollView(tableScroll);

        // Nagłówki kolumn
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Space(26); // Na miniaturę
        GUILayout.Label("Wieża", EditorStyles.boldLabel, GUILayout.Width(170));
        GUILayout.Label("Typ Dmg", EditorStyles.boldLabel, GUILayout.Width(85));
        GUILayout.Label("Zasięg", EditorStyles.boldLabel, GUILayout.Width(65));
        GUILayout.Label("Obrażenia", EditorStyles.boldLabel, GUILayout.Width(75));
        GUILayout.Label("Strz./sek", EditorStyles.boldLabel, GUILayout.Width(70));
        GUILayout.Label("Przeb. Panc %", EditorStyles.boldLabel, GUILayout.Width(90));
        GUILayout.Label("Przeb. Mag %", EditorStyles.boldLabel, GUILayout.Width(90));
        GUILayout.Label("Kryt %", EditorStyles.boldLabel, GUILayout.Width(65));
        GUILayout.Label("Mnożnik Kryt", EditorStyles.boldLabel, GUILayout.Width(85));
        GUILayout.Label("Bazowy DPS", EditorStyles.boldLabel, GUILayout.Width(85));
        GUILayout.Label("Crit DPS", EditorStyles.boldLabel, GUILayout.Width(85));
        GUILayout.Label("DPS vs Dummy", EditorStyles.boldLabel, GUILayout.Width(100));
        GUILayout.Label("Pocisk", EditorStyles.boldLabel, GUILayout.Width(110));
        EditorGUILayout.EndHorizontal();

        var filteredTowers = allTowers.Where(t =>
        {
            if (t == null) return false;
            if (string.IsNullOrEmpty(searchFilter)) return true;
            string displayName = !string.IsNullOrEmpty(t.buildingName) ? t.buildingName : t.name;
            return displayName.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        }).ToList();

        for (int i = 0; i < filteredTowers.Count; i++)
        {
            var tower = filteredTowers[i];
            if (tower == null) continue;

            bool isSelected = selectedTower == tower;

            EditorGUI.BeginChangeCheck();
            Undo.RecordObject(tower, "Edit Tower Balance Data");

            GUI.backgroundColor = isSelected ? new Color(0.25f, 0.55f, 0.9f) : (i % 2 == 0 ? new Color(0.93f, 0.93f, 0.93f) : Color.white);
            EditorGUILayout.BeginHorizontal("box");

            // Ikona
            if (tower.icon != null && tower.icon.texture != null)
            {
                GUILayout.Label(tower.icon.texture, GUILayout.Width(22), GUILayout.Height(22));
            }
            else
            {
                GUILayout.Space(26);
            }

            // Nazwa (jako przycisk wyboru)
            string displayName = !string.IsNullOrEmpty(tower.buildingName) ? tower.buildingName : tower.name;
            if (GUILayout.Button(displayName, EditorStyles.boldLabel, GUILayout.Width(170), GUILayout.Height(20)))
            {
                selectedTower = tower;
                GUI.FocusControl(null);
            }

            // Typ Obrażeń
            tower.damageType = (DamageType)EditorGUILayout.EnumPopup(tower.damageType, GUILayout.Width(85));

            // Zasięg
            tower.baseRange = Mathf.Max(0.5f, EditorGUILayout.FloatField(tower.baseRange, GUILayout.Width(65)));

            // Obrażenia
            tower.baseDamage = Mathf.Max(0f, EditorGUILayout.FloatField(tower.baseDamage, GUILayout.Width(75)));

            // Szybkostrzelność
            tower.fireRate = Mathf.Max(0.01f, EditorGUILayout.FloatField(tower.fireRate, GUILayout.Width(70)));

            // Penetracja Pancerza
            tower.armorPenetration = Mathf.Clamp(EditorGUILayout.FloatField(tower.armorPenetration, GUILayout.Width(90)), 0f, 100f);

            // Penetracja Magiczna
            tower.magicPenetration = Mathf.Clamp(EditorGUILayout.FloatField(tower.magicPenetration, GUILayout.Width(90)), 0f, 100f);

            // Szansa na Kryt
            tower.criticalChancel = Mathf.Clamp(EditorGUILayout.FloatField(tower.criticalChancel, GUILayout.Width(65)), 0f, 100f);

            // Mnożnik Kryta
            tower.criticalDamageMultiplier = Mathf.Max(1f, EditorGUILayout.FloatField(tower.criticalDamageMultiplier, GUILayout.Width(85)));

            // Obliczenia DPS
            float baseDps = CalculateBaseDps(tower);
            float critDps = CalculateCritDps(tower);
            float dummyDps = CalculateDummyDps(tower, dummyArmor, dummyMagicResist);

            GUILayout.Label($"{baseDps:0.#}", EditorStyles.miniLabel, GUILayout.Width(85));
            GUILayout.Label($"{critDps:0.#}", EditorStyles.miniBoldLabel, GUILayout.Width(85));

            // Kolorowanie DPS vs Dummy
            float ratio = critDps > 0f ? dummyDps / critDps : 1f;
            Color prevColor = GUI.contentColor;
            if (ratio >= 0.95f) GUI.contentColor = new Color(0.2f, 0.85f, 0.3f);
            else if (ratio >= 0.65f) GUI.contentColor = new Color(0.9f, 0.8f, 0.2f);
            else GUI.contentColor = new Color(0.95f, 0.35f, 0.35f);

            GUILayout.Label($"{dummyDps:0.#} ({(ratio * 100f):0}%)", EditorStyles.boldLabel, GUILayout.Width(100));
            GUI.contentColor = prevColor;

            // Prefab Pocisku
            tower.bulletPrefab = (GameObject)EditorGUILayout.ObjectField(tower.bulletPrefab, typeof(GameObject), false, GUILayout.Width(110));

            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(tower);
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // =========================================================================
    // 4. DETAILED INSPECTOR (DOLNY PANEL WYBRANEJ WIEŻY)
    // =========================================================================
    private void DrawSelectedTowerDetails()
    {
        EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (selectedTower == null)
        {
            EditorGUILayout.LabelField("Wybierz wieżę z tabeli powyżej, aby zobaczyć szczegóły celowania, efektów i kosztów.", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
            return;
        }

        detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll);

        EditorGUI.BeginChangeCheck();
        Undo.RecordObject(selectedTower, "Edit Tower Details");

        // Nagłówek wybranej wieży
        EditorGUILayout.BeginHorizontal();
        if (selectedTower.icon != null && selectedTower.icon.texture != null)
        {
            GUILayout.Label(selectedTower.icon.texture, GUILayout.Width(36), GUILayout.Height(36));
        }

        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField($"Szczegóły: {(!string.IsNullOrEmpty(selectedTower.buildingName) ? selectedTower.buildingName : selectedTower.name)}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Asset: {AssetDatabase.GetAssetPath(selectedTower)}", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Zaznacz w Projekcie (Ping)", GUILayout.Width(170)))
        {
            EditorGUIUtility.PingObject(selectedTower);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();

        // Lewa kolumna szczegółów: Podstawy i Celowanie
        EditorGUILayout.BeginVertical("helpbox", GUILayout.Width(position.width * 0.48f));
        EditorGUILayout.LabelField("🎯 Celowanie i Pocisk", EditorStyles.boldLabel);

        selectedTower.buildingName = EditorGUILayout.TextField("Nazwa w grze:", selectedTower.buildingName);
        selectedTower.icon = (Sprite)EditorGUILayout.ObjectField("Ikonka:", selectedTower.icon, typeof(Sprite), false);
        selectedTower.bulletPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab Pocisku:", selectedTower.bulletPrefab, typeof(GameObject), false);
        selectedTower.canShootChunk = EditorGUILayout.Toggle("Może strzelać w Chunki:", selectedTower.canShootChunk);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Priorytety Celowania (domyślne):", EditorStyles.miniBoldLabel);
        if (selectedTower.defaultTargetingPriorities == null)
        {
            selectedTower.defaultTargetingPriorities = new List<TargetingMode>();
        }

        int removeTargetIndex = -1;
        for (int p = 0; p < selectedTower.defaultTargetingPriorities.Count; p++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Priorytet #{p + 1}:", GUILayout.Width(90));
            selectedTower.defaultTargetingPriorities[p] = (TargetingMode)EditorGUILayout.EnumPopup(selectedTower.defaultTargetingPriorities[p]);

            GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);
            if (GUILayout.Button("X", GUILayout.Width(22))) removeTargetIndex = p;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        if (removeTargetIndex >= 0)
        {
            selectedTower.defaultTargetingPriorities.RemoveAt(removeTargetIndex);
        }

        if (selectedTower.defaultTargetingPriorities.Count < 3)
        {
            if (GUILayout.Button("+ Dodaj priorytet celowania", EditorStyles.miniButton))
            {
                selectedTower.defaultTargetingPriorities.Add(TargetingMode.ClosestOnTrack);
            }
        }

        EditorGUILayout.EndVertical();

        // Prawa kolumna szczegółów: Efekty Specjalne & Ekonomia
        EditorGUILayout.BeginVertical("helpbox", GUILayout.ExpandWidth(true));
        EditorGUILayout.LabelField("✨ Efekty Specjalne & Ekonomia", EditorStyles.boldLabel);

        // Efekty specjalne
        if (selectedTower.effects == null) selectedTower.effects = new List<TowerEffectSO>();

        int removeEffectIndex = -1;
        EditorGUILayout.LabelField("Efekty trafienia (TowerEffectSO):", EditorStyles.miniBoldLabel);
        for (int e = 0; e < selectedTower.effects.Count; e++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Efekt #{e + 1}:", GUILayout.Width(70));
            selectedTower.effects[e] = (TowerEffectSO)EditorGUILayout.ObjectField(selectedTower.effects[e], typeof(TowerEffectSO), false);

            GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);
            if (GUILayout.Button("X", GUILayout.Width(22))) removeEffectIndex = e;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        if (removeEffectIndex >= 0)
        {
            selectedTower.effects.RemoveAt(removeEffectIndex);
        }

        if (GUILayout.Button("+ Dodaj slot na efekt specjalny", EditorStyles.miniButton))
        {
            selectedTower.effects.Add(null);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Ekonomia Budynku:", EditorStyles.miniBoldLabel);
        selectedTower.baseMaxWorkers = Mathf.Max(0, EditorGUILayout.IntField("Wymagani Pracownicy:", selectedTower.baseMaxWorkers));

        // Koszty budowy
        if (selectedTower.constructionCost == null) selectedTower.constructionCost = new List<ResourceCost>();

        int removeCostIndex = -1;
        EditorGUILayout.LabelField("Koszty budowy:", EditorStyles.miniBoldLabel);
        for (int c = 0; c < selectedTower.constructionCost.Count; c++)
        {
            var cost = selectedTower.constructionCost[c];
            EditorGUILayout.BeginHorizontal();
            cost.type = (ResourceType)EditorGUILayout.EnumPopup(cost.type, GUILayout.Width(130));
            cost.amount = Mathf.Max(0, EditorGUILayout.IntField(cost.amount, GUILayout.Width(70)));
            selectedTower.constructionCost[c] = cost;

            GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);
            if (GUILayout.Button("X", GUILayout.Width(22))) removeCostIndex = c;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        if (removeCostIndex >= 0)
        {
            selectedTower.constructionCost.RemoveAt(removeCostIndex);
        }

        if (GUILayout.Button("+ Dodaj surowiec kosztu budowy", EditorStyles.miniButton))
        {
            selectedTower.constructionCost.Add(new ResourceCost { type = ResourceType.Wood, amount = 10 });
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(selectedTower);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // =========================================================================
    // 5. OBLICZENIA BOJOWE I DPS
    // =========================================================================
    private static float CalculateBaseDps(TowerData tower)
    {
        if (tower == null) return 0f;
        return tower.baseDamage * tower.fireRate;
    }

    private static float CalculateCritDps(TowerData tower)
    {
        if (tower == null) return 0f;
        float baseDps = CalculateBaseDps(tower);
        float critChance = tower.criticalChancel > 1f ? tower.criticalChancel / 100f : tower.criticalChancel;
        float critMult = tower.criticalDamageMultiplier > 0f ? tower.criticalDamageMultiplier : 1f;

        float avgMultiplier = 1f + Mathf.Clamp01(critChance) * Mathf.Max(0f, critMult - 1f);
        return baseDps * avgMultiplier;
    }

    private static float CalculateDummyDps(TowerData tower, float dummyArmorPct, float dummyMrPct)
    {
        if (tower == null) return 0f;
        float critDps = CalculateCritDps(tower);

        if (tower.damageType == DamageType.True)
        {
            return critDps;
        }

        float ap = tower.armorPenetration > 1f ? tower.armorPenetration / 100f : tower.armorPenetration;
        float mp = tower.magicPenetration > 1f ? tower.magicPenetration / 100f : tower.magicPenetration;

        float targetArmor = dummyArmorPct / 100f;
        float targetMr = dummyMrPct / 100f;

        if (tower.damageType == DamageType.Physical)
        {
            float effectiveArmor = Mathf.Clamp01(targetArmor - ap);
            return critDps * (1f - effectiveArmor);
        }
        else // Magic
        {
            float effectiveMr = Mathf.Clamp01(targetMr - mp);
            return critDps * (1f - effectiveMr);
        }
    }

    // =========================================================================
    // 6. TWORZENIE, DUPLIKOWANIE I USUWANIE ASSETÓW
    // =========================================================================
    private void CreateNewTowerAsset()
    {
        if (!Directory.Exists(TowersFolderPath))
        {
            Directory.CreateDirectory(TowersFolderPath);
        }

        int nextIndex = allTowers.Count + 1;
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{TowersFolderPath}/{nextIndex}. New Tower.asset");

        TowerData newTower = CreateInstance<TowerData>();
        newTower.buildingName = $"Nowa Wieża {nextIndex}";
        newTower.type = BuildingType.Defense;
        newTower.baseRange = 6f;
        newTower.baseDamage = 25f;
        newTower.fireRate = 1.0f;
        newTower.criticalChancel = 10f;
        newTower.criticalDamageMultiplier = 1.5f;
        newTower.armorPenetration = 0f;
        newTower.magicPenetration = 0f;
        newTower.damageType = DamageType.Physical;
        newTower.baseMaxWorkers = 1;
        newTower.defaultTargetingPriorities = new List<TargetingMode> { TargetingMode.ClosestOnTrack };
        newTower.constructionCost = new List<ResourceCost>
        {
            new ResourceCost { type = ResourceType.Wood, amount = 20 },
            new ResourceCost { type = ResourceType.Stone, amount = 10 }
        };

        AssetDatabase.CreateAsset(newTower, assetPath);
        AssetDatabase.SaveAssets();
        RefreshTowers();
        selectedTower = newTower;
    }

    private void DuplicateSelectedTower()
    {
        if (selectedTower == null) return;

        string originalPath = AssetDatabase.GetAssetPath(selectedTower);
        string dir = Path.GetDirectoryName(originalPath);
        string fileName = Path.GetFileNameWithoutExtension(originalPath);
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{fileName}_Copy.asset");

        TowerData copy = Instantiate(selectedTower);
        copy.buildingName = $"{selectedTower.buildingName} (Kopia)";

        AssetDatabase.CreateAsset(copy, assetPath);
        AssetDatabase.SaveAssets();
        RefreshTowers();
        selectedTower = copy;
    }

    private void DeleteTowerAsset(TowerData tower)
    {
        if (tower == null) return;
        string path = AssetDatabase.GetAssetPath(tower);
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();
        selectedTower = null;
        RefreshTowers();
    }
}
#endif
