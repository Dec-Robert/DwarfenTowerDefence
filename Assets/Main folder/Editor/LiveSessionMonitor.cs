#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class LiveSessionMonitorWindow : EditorWindow
{
    private Vector2 scrollPos;
    private bool autoRefresh = true;

    [MenuItem("Tools/Debug/Live Session Monitor")]
    public static void ShowWindow()
    {
        var window = GetWindow<LiveSessionMonitorWindow>("Live Session Monitor");
        window.minSize = new Vector2(450, 600);
        window.Show();
    }

    private void OnInspectorUpdate()
    {
        // Odświeżaj okno co klatkę edytora, jeśli gra jest uruchomiona i mamy włączony auto-refresh
        if (Application.isPlaying && autoRefresh)
        {
            Repaint();
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("To narzędzie zbiera dane na żywo z uruchomionych menedżerów. Uruchom grę (Play Mode), aby zobaczyć statystyki.", MessageType.Info);
            return;
        }

        DrawToolbar();

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawResourceStats();
        EditorGUILayout.Space(10);
        
        DrawPopulationStats();
        EditorGUILayout.Space(10);
        
        DrawCityStats();
        EditorGUILayout.Space(10);

        DrawGlobalModifiers();

        EditorGUILayout.EndScrollView();
    }

    // =========================================================================
    // UI - PASEK NARZĘDZIOWY
    // =========================================================================
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        autoRefresh = GUILayout.Toggle(autoRefresh, "Auto-Odświeżanie", EditorStyles.toolbarButton, GUILayout.Width(120));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Wymuś Odświeżenie", EditorStyles.toolbarButton, GUILayout.Width(130)))
        {
            Repaint();
        }
        EditorGUILayout.EndHorizontal();
    }

    // =========================================================================
    // SEKCJA 1: SUROWCE (Baza vs Meta vs Aktualne)
    // =========================================================================
    private void DrawResourceStats()
    {
        EditorGUILayout.LabelField("💰 ZASOBY STARTOWE (Baza vs Meta)", EditorStyles.boldLabel);
        
        if (ResourceManager.Instance == null || MetaUpgradeManager.Instance == null)
        {
            EditorGUILayout.HelpBox("Brak ResourceManager lub MetaUpgradeManager na scenie.", MessageType.Warning);
            return;
        }

        DrawTableHeader("Surowiec", "Baza (Config)", "Bonus z Meta", "Aktualny Stan Konta");

        DrawResourceRow(ResourceType.Gold, MetaEffectType.StartingGold);
        DrawResourceRow(ResourceType.Wood, MetaEffectType.StartingWood);
        DrawResourceRow(ResourceType.Stone, MetaEffectType.StartingStone);
        DrawResourceRow(ResourceType.Iron, MetaEffectType.StartingIron);
        DrawResourceRow(ResourceType.Coal, MetaEffectType.StartingCoal);
        DrawResourceRow(ResourceType.Food, MetaEffectType.StartingFood);
        
        EditorGUILayout.Space(5);
        DrawTableRow("Nadzieja (Hope)", "-", "-", SaveManager.Instance?.currentSaveData.totalHope.ToString() ?? "Brak");
    }

    private void DrawResourceRow(ResourceType type, MetaEffectType metaType)
    {
        // Baza z CityBaseConfig (podpiętego do ResourceManager)
        float baseAmount = 0;
        if (ResourceManager.Instance != null && ResourceManager.Instance.cityConfig != null)
        {
            var cfg = ResourceManager.Instance.cityConfig;
            switch (type)
            {
                case ResourceType.Gold: baseAmount = cfg.startGold; break;
                case ResourceType.Wood: baseAmount = cfg.startWood; break;
                case ResourceType.Stone: baseAmount = cfg.startStone; break;
                case ResourceType.Iron: baseAmount = cfg.startIron; break;
                case ResourceType.Coal: baseAmount = cfg.startCoal; break;
                case ResourceType.Food: baseAmount = cfg.startFood; break;
            }
        }

        // Meta bonus (Pobrany prosto z managera po poprawce sumowania)
        float metaBonus = MetaUpgradeManager.Instance != null ? MetaUpgradeManager.Instance.GetValue(metaType) : 0;

        // Aktualny (Live)
        float currentAmount = ResourceManager.Instance != null ? ResourceManager.Instance.GetResourceAmount(type) : 0;

        string bonusText = metaBonus > 0 ? $"+{metaBonus}" : "-";
        DrawTableRow(type.ToString(), baseAmount.ToString(), bonusText, currentAmount.ToString());
    }

    // =========================================================================
    // SEKCJA 2: POPULACJA (Ludzie, Elfy, Krasnoludy)
    // =========================================================================
    private void DrawPopulationStats()
    {
        EditorGUILayout.LabelField("🏠 STATYSTYKI MIESZKANIOWE (Domy)", EditorStyles.boldLabel);

        if (BuildingRegistry.Instance == null)
        {
            EditorGUILayout.HelpBox("Brak BuildingRegistry na scenie.", MessageType.Warning);
            return;
        }

        // Pobierz i posortuj domy po rasie (aby nie sprawdzać wszystkich budynków za każdym razem)
        var allHouses = BuildingRegistry.Instance.GetAllOfType<HousingEntity>();
        var humanHouses = allHouses.Where(h => h.housingData.housingRace == Race.Humans).ToList();
        var elfHouses = allHouses.Where(h => h.housingData.housingRace == Race.Elves).ToList();
        var dwarfHouses = allHouses.Where(h => h.housingData.housingRace == Race.Dwarves).ToList();

        DrawRaceHouseStats("Ludzie (Humans)", Race.Humans, humanHouses, MetaEffectType.HousingMaxResidents_Humans);
        DrawRaceHouseStats("Elfy (Elves)", Race.Elves, elfHouses, MetaEffectType.HousingMaxResidents_Elves);
        DrawRaceHouseStats("Krasnoludy (Dwarves)", Race.Dwarves, dwarfHouses, MetaEffectType.HousingMaxResidents_Dwarves);
    }

    private void DrawRaceHouseStats(string label, Race race, List<HousingEntity> houses, MetaEffectType specificMaxPopEffect)
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        if (houses.Count == 0)
        {
            EditorGUILayout.LabelField("Brak wybudowanych domów tej rasy.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            return;
        }

        // Bierzemy dane z pierwszego znalezionego domu
        HousingBuildingData template = houses[0].housingData;
        CityBaseConfigSO globalCfg = ResourceManager.Instance?.cityConfig;

        // --- Startowa Populacja (Start) ---
        int baseStartPop = template.initialResidents;
        if (globalCfg != null && globalCfg.overrideHousingData)
        {
            if (race == Race.Humans) baseStartPop = globalCfg.humanStartPop;
            else if (race == Race.Elves) baseStartPop = globalCfg.elfStartPop;
            else if (race == Race.Dwarves) baseStartPop = globalCfg.dwarfStartPop;
        }

        int metaStartPopBonus = MetaUpgradeManager.Instance != null ? Mathf.RoundToInt(MetaUpgradeManager.Instance.GetValue(MetaEffectType.HousingStartPopulation)) : 0;
        int totalStartPop = baseStartPop + metaStartPopBonus;

        // --- Maksymalna Populacja (Max) ---
        int baseMaxPop = template.maxResidents;
        if (globalCfg != null && globalCfg.overrideHousingData)
        {
            if (race == Race.Humans) baseMaxPop = globalCfg.humanMaxPop;
            else if (race == Race.Elves) baseMaxPop = globalCfg.elfMaxPop;
            else if (race == Race.Dwarves) baseMaxPop = globalCfg.dwarfMaxPop;
        }

        int metaMaxPopGlobal = MetaUpgradeManager.Instance != null ? Mathf.RoundToInt(MetaUpgradeManager.Instance.GetValue(MetaEffectType.HousingMaxResidents)) : 0;
        int metaMaxPopSpecific = MetaUpgradeManager.Instance != null ? Mathf.RoundToInt(MetaUpgradeManager.Instance.GetValue(specificMaxPopEffect)) : 0;
        int totalMaxPopBonus = metaMaxPopGlobal + metaMaxPopSpecific;
        int totalMaxPop = baseMaxPop + totalMaxPopBonus;

        // --- Żywa Populacja ---
        int currentLivePop = houses.Sum(h => h.residents.Count);
        int currentLiveMaxCap = houses.Count * totalMaxPop; 
        
        string startBonusTxt = metaStartPopBonus > 0 ? $"+{metaStartPopBonus}" : "-";
        DrawTableRow("Startowi Mieszkańcy", baseStartPop.ToString(), startBonusTxt, totalStartPop.ToString());
        
        string maxMetaStr = (totalMaxPopBonus > 0) ? $"+{totalMaxPopBonus} (Glo:{metaMaxPopGlobal} | Spe:{metaMaxPopSpecific})" : "-";
        DrawTableRow("Limit Mieszkańców", baseMaxPop.ToString(), maxMetaStr, totalMaxPop.ToString());

        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField($"Całkowita obecna populacja: {currentLivePop} / {currentLiveMaxCap} (Wybudowano {houses.Count} domów)", EditorStyles.miniBoldLabel);

        EditorGUILayout.EndVertical();
    }

    // =========================================================================
    // SEKCJA 3: BAZA, ZMIANY, PRACOWNICY
    // =========================================================================
    private void DrawCityStats()
    {
        EditorGUILayout.LabelField("⚙️ ZARZĄDZANIE OSADĄ (City Stats)", EditorStyles.boldLabel);

        if (CityStatsManager.Instance == null)
        {
            EditorGUILayout.HelpBox("Brak CityStatsManager na scenie.", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginVertical("box");
        DrawTableHeader("Parametr", "Baza", "Bonus z Meta", "Łącznie");

        // HP Bazy
        int baseHp = 20; 
        if (GameManager.Instance != null && GameManager.Instance.cityConfig != null)
            baseHp = GameManager.Instance.cityConfig.baseCityHP;

        int metaHp = MetaUpgradeManager.Instance != null ? Mathf.RoundToInt(MetaUpgradeManager.Instance.GetValue(MetaEffectType.CityBaseHealth)) : 0;
        int currentHp = GameManager.Instance != null ? GameManager.Instance.mainGateHP : 0;
        
        string hpBonusTxt = metaHp > 0 ? $"+{metaHp}" : "-";
        DrawTableRow("HP Miasta (Kapitol)", baseHp.ToString(), hpBonusTxt, $"{currentHp} (Zostało)");

        // Zmiany (Shifts)
        int metaShifts = CityStatsManager.Instance.globalBonusShifts;
        string shiftsBonusTxt = metaShifts > 0 ? $"+{metaShifts}" : "-";
        DrawTableRow("Dodatkowe Zmiany", "0", shiftsBonusTxt, metaShifts.ToString());

        // Slot Pracownika na zmianę
        int metaWorkers = CityStatsManager.Instance.globalBonusWorkersPerShift;
        string workersBonusTxt = metaWorkers > 0 ? $"+{metaWorkers}" : "-";
        DrawTableRow("Zwiększenie obsady zmiany", "0", workersBonusTxt, metaWorkers.ToString());

        EditorGUILayout.EndVertical();
    }

    // =========================================================================
    // SEKCJA 4: GLOBALNE MODYFIKATORY
    // =========================================================================
    private void DrawGlobalModifiers()
    {
        EditorGUILayout.LabelField("🌐 REJESTR GLOBALNYCH MODYFIKATORÓW", EditorStyles.boldLabel);

        if (GlobalModifierRegistry.Instance == null)
        {
            EditorGUILayout.HelpBox("Brak GlobalModifierRegistry.", MessageType.Warning);
            return;
        }

        // Tu sięgamy "pod maskę" do prywatnej listy przez refleksję, by ją po prostu odczytać dla wyświetlenia
        System.Reflection.FieldInfo fieldInfo = typeof(GlobalModifierRegistry).GetField("modifiers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (fieldInfo != null)
        {
            List<StatModifier> modifiers = (List<StatModifier>)fieldInfo.GetValue(GlobalModifierRegistry.Instance);

            if (modifiers == null || modifiers.Count == 0)
            {
                EditorGUILayout.LabelField("Brak aktywnych modyfikatorów w rejestrze.", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.BeginVertical("box");
                DrawTableHeader("Źródło (Source)", "Statystyka", "Mnożnik (Multi)", "Płaski Bonus (Flat)");
                
                foreach (var mod in modifiers)
                {
                    // Kolorujemy modyfikatory pochodzące z run (Forge_) by były lepiej widoczne
                    GUI.contentColor = mod.sourceId.StartsWith("Forge_") ? new Color(1f, 0.7f, 0.2f) : Color.white;
                    DrawTableRow(mod.sourceId, mod.stat.ToString(), $"x {mod.multiplier:F2}", $"+ {mod.flat:F2}");
                    GUI.contentColor = Color.white;
                }
                EditorGUILayout.EndVertical();
            }
        }
    }

    // =========================================================================
    // POMOCNICZE WIDOKI (TABELE)
    // =========================================================================

    private void DrawTableHeader(string c1, string c2, string c3, string c4)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"<b>{c1}</b>", new GUIStyle(EditorStyles.label) { richText = true, alignment = TextAnchor.MiddleLeft }, GUILayout.Width(130));
        GUILayout.Label($"<b>{c2}</b>", new GUIStyle(EditorStyles.label) { richText = true, alignment = TextAnchor.MiddleCenter }, GUILayout.Width(100));
        GUILayout.Label($"<b>{c3}</b>", new GUIStyle(EditorStyles.label) { richText = true, alignment = TextAnchor.MiddleCenter }, GUILayout.Width(100));
        GUILayout.Label($"<b>{c4}</b>", new GUIStyle(EditorStyles.label) { richText = true, alignment = TextAnchor.MiddleRight }, GUILayout.ExpandWidth(true));
        EditorGUILayout.EndHorizontal();
        
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f));
        EditorGUILayout.Space(2);
    }

    private void DrawTableRow(string c1, string c2, string c3, string c4)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(c1, GUILayout.Width(130));
        GUILayout.Label(c2, new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(100));
        
        // Koloruj na zielono, jeśli bonus z mety istnieje
        GUIStyle bonusStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
        if (c3 != "0" && c3 != "+0" && c3 != "-") bonusStyle.normal.textColor = new Color(0.2f, 0.9f, 0.2f);
        
        GUILayout.Label(c3, bonusStyle, GUILayout.Width(100));
        GUILayout.Label(c4, new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleRight }, GUILayout.ExpandWidth(true));
        EditorGUILayout.EndHorizontal();
    }
}
#endif