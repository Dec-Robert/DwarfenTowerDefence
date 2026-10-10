using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Zunifikowana konsola deweloperska i manager cheatów.
/// Uruchamiana klawiszem tyldy (`~`), działa w IMGUI (OnGUI),
/// posiada historię komend, autouzupełnianie (Tab) oraz DontDestroyOnLoad.
/// </summary>
public class DebugCheatManager : MonoBehaviour
{
    public static DebugCheatManager Instance { get; private set; }

    [Header("Ustawienia Konsoli")]
    public KeyCode toggleKey = KeyCode.BackQuote;
    public KeyCode altToggleKey = KeyCode.Tilde;
    public int maxLogEntries = 100;

    private bool isOpen = false;
    private string currentInput = "";
    private bool shouldFocusInput = false;
    private Vector2 scrollPosition = Vector2.zero;

    private readonly List<LogEntry> logEntries = new List<LogEntry>();
    private readonly List<string> commandHistory = new List<string>();
    private int historyIndex = -1;

    private static readonly string[] KnownCommands = new string[]
    {
        "help",
        "clear",
        "res",
        "killall",
        "god",
        "hp",
        "wave finish",
        "spawn",
        "map reveal",
        "map settleall",
        "map scoutall",
        "rune_random",
        "rune_stat",
        "rune clear",
        "hope",
        "meta_all",
        "meta_new"
    };

    private struct LogEntry
    {
        public string text;
        public Color color;
    }

    // =========================================================================
    // INICJALIZACJA I CYKL ŻYCIA
    // =========================================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null)
        {
            var existing = FindAnyObjectByType<DebugCheatManager>();
            if (existing == null)
            {
                var go = new GameObject("[DebugCheatManager]");
                existing = go.AddComponent<DebugCheatManager>();
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LogInfo("Konsola deweloperska aktywna. Naciśnij [~] (tylda), aby otworzyć/zamknąć. Wpisz 'help' po listę komend.");
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey) || Input.GetKeyDown(altToggleKey))
        {
            ToggleConsole();
        }
    }

    public void ToggleConsole()
    {
        isOpen = !isOpen;

        var cam = Camera.main != null ? Camera.main.GetComponent<CameraController>() : FindAnyObjectByType<CameraController>();
        if (cam != null)
        {
            cam.isInputLocked = isOpen;
        }

        if (isOpen)
        {
            shouldFocusInput = true;
            historyIndex = -1;
        }
    }

    // =========================================================================
    // RENDEROWANIE IMGUI (OnGUI)
    // =========================================================================

    private void OnGUI()
    {
        if (!isOpen) return;

        // Przechwytywanie klawiszy funkcyjnych w konsoli
        Event e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == toggleKey || e.keyCode == altToggleKey || e.keyCode == KeyCode.Escape)
            {
                e.Use();
                ToggleConsole();
                return;
            }
            else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                e.Use();
                if (!string.IsNullOrWhiteSpace(currentInput))
                {
                    ExecuteCommand(currentInput);
                    currentInput = "";
                    historyIndex = -1;
                }
                return;
            }
            else if (e.keyCode == KeyCode.UpArrow)
            {
                e.Use();
                NavigateHistory(-1);
                return;
            }
            else if (e.keyCode == KeyCode.DownArrow)
            {
                e.Use();
                NavigateHistory(1);
                return;
            }
            else if (e.keyCode == KeyCode.Tab)
            {
                e.Use();
                AutoCompleteCommand();
                return;
            }
        }

        DrawConsoleWindow();
    }

    private void DrawConsoleWindow()
    {
        float windowWidth = Screen.width - 20f;
        float windowHeight = Mathf.Clamp(Screen.height * 0.45f, 260f, 650f);
        Rect windowRect = new Rect(10f, 10f, windowWidth, windowHeight);

        GUI.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.94f);
        GUI.Box(windowRect, GUIContent.none);

        GUILayout.BeginArea(new Rect(15f, 15f, windowWidth - 10f, windowHeight - 10f));

        // Pasek górny / Nagłówek
        GUILayout.BeginHorizontal();
        GUI.color = new Color(0.4f, 0.8f, 1f);
        GUILayout.Label("<b>[KONSOLA DEWELOPERSKA]</b>  (Klawisz [~] zamyka | Tab: Dopełnienie | ↑/↓: Historia)", GUILayout.ExpandWidth(true));
        GUI.color = Color.white;

        if (GUILayout.Button("Wyczyść", GUILayout.Width(70f)))
        {
            logEntries.Clear();
        }

        if (GUILayout.Button("X", GUILayout.Width(35f)))
        {
            ToggleConsole();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5f);

        // Lista logów (przewijana)
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(windowHeight - 75f));
        GUIStyle logStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            wordWrap = true,
            richText = true
        };

        foreach (var entry in logEntries)
        {
            GUI.color = entry.color;
            GUILayout.Label(entry.text, logStyle);
        }
        GUI.color = Color.white;
        GUILayout.EndScrollView();

        GUILayout.Space(5f);

        // Pasek wprowadzania komendy
        GUILayout.BeginHorizontal();
        GUI.color = Color.cyan;
        GUILayout.Label(">", GUILayout.Width(15f));
        GUI.color = Color.white;

        GUI.SetNextControlName("ConsoleInput");
        currentInput = GUILayout.TextField(currentInput, GUILayout.ExpandWidth(true));

        if (GUILayout.Button("Wykonaj", GUILayout.Width(80f)))
        {
            if (!string.IsNullOrWhiteSpace(currentInput))
            {
                ExecuteCommand(currentInput);
                currentInput = "";
                historyIndex = -1;
            }
        }
        GUILayout.EndHorizontal();

        if (shouldFocusInput)
        {
            GUI.FocusControl("ConsoleInput");
            shouldFocusInput = false;
        }

        GUILayout.EndArea();
    }

    // =========================================================================
    // HISTORIA I AUTO-COMPLETE
    // =========================================================================

    private void NavigateHistory(int direction)
    {
        if (commandHistory.Count == 0) return;

        if (historyIndex == -1)
        {
            historyIndex = commandHistory.Count;
        }

        historyIndex += direction;
        historyIndex = Mathf.Clamp(historyIndex, 0, commandHistory.Count);

        if (historyIndex < commandHistory.Count)
        {
            currentInput = commandHistory[historyIndex];
        }
        else
        {
            currentInput = "";
        }
    }

    private void AutoCompleteCommand()
    {
        if (string.IsNullOrWhiteSpace(currentInput)) return;

        string trimmed = currentInput.TrimStart();
        string match = KnownCommands.FirstOrDefault(c => c.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(match))
        {
            currentInput = match + " ";
        }
    }

    // =========================================================================
    // SILNIK PARSOWANIA I WYKONYWANIA KOMEND
    // =========================================================================

    public void ExecuteCommand(string input)
    {
        string trimmed = input.Trim();
        if (string.IsNullOrEmpty(trimmed)) return;

        // Dodaj do historii
        if (commandHistory.Count == 0 || commandHistory[commandHistory.Count - 1] != trimmed)
        {
            commandHistory.Add(trimmed);
        }

        LogCommand($"> {trimmed}");

        string[] parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        string command = parts[0].ToLowerInvariant();

        try
        {
            switch (command)
            {
                case "help":
                    HandleHelp(parts);
                    break;

                case "clear":
                    logEntries.Clear();
                    break;

                case "res":
                    HandleResources(parts);
                    break;

                case "killall":
                case "kill":
                    HandleKillAll();
                    break;

                case "god":
                    HandleGodMode();
                    break;

                case "hp":
                    HandleBaseHealth(parts);
                    break;

                case "wave":
                    HandleWave(parts);
                    break;

                case "spawn":
                    HandleSpawn(parts);
                    break;

                case "map":
                    HandleMap(parts);
                    break;

                case "rune_random":
                    HandleRuneRandom(parts);
                    break;

                case "rune_stat":
                    HandleRuneStat(parts);
                    break;

                case "rune":
                    HandleRuneGeneric(parts);
                    break;

                case "hope":
                    HandleHope(parts);
                    break;

                case "meta_all":
                    HandleMetaAll();
                    break;

                case "meta_new":
                    HandleMetaNew();
                    break;

                default:
                    LogError($"Nieznana komenda: '{command}'. Wpisz 'help', aby zobaczyć listę dostępnych poleceń.");
                    break;
            }
        }
        catch (Exception ex)
        {
            LogError($"Błąd wykonania komendy '{trimmed}': {ex.Message}");
        }

        // Automatyczne przewinięcie na sam dół logów
        scrollPosition.y = float.MaxValue;
    }

    // =========================================================================
    // OBSŁUGA KOMEND
    // =========================================================================

    private void HandleHelp(string[] parts)
    {
        if (parts.Length > 1)
        {
            string sub = parts[1].ToLowerInvariant();
            switch (sub)
            {
                case "res":
                    LogInfo("<b>res &lt;typ&gt; &lt;ilość&gt;</b> - Dodaje określoną ilość surowca (np. res gold 500, res wood 100).");
                    LogInfo("<b>res all &lt;ilość&gt;</b> - Dodaje podaną ilość do każdego surowca.");
                    LogInfo("<b>res clear</b> - Zeruje stan wszystkich surowców do zera.");
                    break;
                case "killall":
                    LogInfo("<b>killall</b> - Natychmiast eliminuje wszystkich żywych wrogów na mapie.");
                    break;
                case "god":
                    LogInfo("<b>god</b> - Przełącza nieśmiertelność bazy (główna brama nie otrzymuje obrażeń).");
                    break;
                case "hp":
                    LogInfo("<b>hp &lt;ilość&gt;</b> - Ustawia punkty życia bramy bazy na wskazaną wartość.");
                    break;
                case "wave":
                    LogInfo("<b>wave finish</b> - Kończy obecną noc: eliminuje wrogów, zatrzymuje spawner i przechodzi do świtu.");
                    break;
                case "spawn":
                    LogInfo("<b>spawn &lt;nazwa_wroga&gt; [ilość]</b> - Spawnuje wroga o podanej nazwie na trasie marszu.");
                    break;
                case "map":
                    LogInfo("<b>map reveal</b> - Odsłania całą mapę (zdejmuje mgłę wojny).");
                    LogInfo("<b>map settleall</b> - Ustawia wszystkie chunki jako Settled (pełna kontrola).");
                    LogInfo("<b>map scoutall</b> - Ustawia chunki jako Outskirts (odkryte zwiadem).");
                    break;
                case "rune_random":
                    LogInfo("<b>rune_random &lt;ilość&gt;</b> - Generuje określoną liczbę w pełni losowych run.");
                    LogInfo("<b>rune_random &lt;rarity&gt; &lt;ilość&gt;</b> - Generuje runy o zadanej rzadkości (Common, Uncommon, Rare, Legendary, Cursed).");
                    break;
                case "rune_stat":
                    LogInfo("<b>rune_stat &lt;rarity&gt; &lt;stat&gt;</b> - Generuje runę o rzadkości i statystyce (np. rune_stat Legendary Damage).");
                    break;
                case "meta_all":
                    LogInfo("<b>meta_all</b> - Odblokowuje wszystkie meta-upgrady, aplikuje ich efekty i zapisuje w save.");
                    break;
                case "meta_new":
                    LogInfo("<b>meta_new</b> - Blokuje wszystkie meta-upgrady, resetuje listę w save i odświeża grę.");
                    break;
                default:
                    LogInfo($"Brak szczegółowego opisu dla '{sub}'.");
                    break;
            }
            return;
        }

        LogInfo("<b>=== LISTA DOSTĘPNYCH KOMEND DEWELOPERSKICH ===</b>");
        LogInfo("• <b>res &lt;typ&gt; &lt;ilość&gt;</b> | <b>res all &lt;ilość&gt;</b> | <b>res clear</b> - Zarządzanie zasobami");
        LogInfo("• <b>killall</b> - Uśmierca wszystkich wrogów");
        LogInfo("• <b>god</b> - Toggle nieśmiertelności bazy");
        LogInfo("• <b>hp &lt;ilość&gt;</b> - Ustawia HP bramy bazy");
        LogInfo("• <b>wave finish</b> - Natychmiast kończy noc i przechodzi do świtu");
        LogInfo("• <b>spawn &lt;nazwa&gt; [ilość]</b> - Spawnuje wroga na trasie");
        LogInfo("• <b>map reveal</b> | <b>map settleall</b> | <b>map scoutall</b> - Zarządzanie mapą i mgłą");
        LogInfo("• <b>rune_random &lt;ilość&gt;</b> | <b>rune_random &lt;rarity&gt; &lt;ilość&gt;</b> - Generowanie run");
        LogInfo("• <b>rune_stat &lt;rarity&gt; &lt;stat&gt;</b> | <b>rune clear</b> - Konkretne runy / czyszczenie");
        LogInfo("• <b>hope &lt;ilość&gt;</b> - Dodaje punkty nadziei do sesji");
        LogInfo("• <b>meta_all</b> | <b>meta_new</b> - Odblokowanie / reset meta-progresji");
        LogInfo("• <b>clear</b> - Czyszczenie logów konsoli | <b>help &lt;komenda&gt;</b> - Szczegóły komendy");
    }

    private void HandleResources(string[] parts)
    {
        if (ResourceManager.Instance == null)
        {
            LogError("ResourceManager.Instance nie istnieje na scenie.");
            return;
        }

        if (parts.Length < 2)
        {
            LogError("Użycie: res <typ> <ilość> | res all <ilość> | res clear");
            return;
        }

        string sub = parts[1].ToLowerInvariant();

        if (sub == "clear")
        {
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                int current = ResourceManager.Instance.GetResourceAmount(type);
                if (current > 0)
                {
                    ResourceManager.Instance.SpendResource(type, current);
                }
            }
            LogSuccess("Wszystkie surowce zostały wyzerowane.");
            return;
        }

        if (sub == "all")
        {
            if (parts.Length < 3 || !int.TryParse(parts[2], out int allAmount))
            {
                LogError("Użycie: res all <ilość>");
                return;
            }

            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                ResourceManager.Instance.AddResource(type, allAmount);
            }
            LogSuccess($"Dodano +{allAmount} do każdego surowca.");
            return;
        }

        // Pojedynczy surowiec
        if (!Enum.TryParse(sub, true, out ResourceType resType))
        {
            string available = string.Join(", ", Enum.GetNames(typeof(ResourceType)));
            LogError($"Nieznany surowiec '{sub}'. Dostępne: {available}");
            return;
        }

        if (parts.Length < 3 || !int.TryParse(parts[2], out int amount))
        {
            LogError($"Użycie: res {resType} <ilość>");
            return;
        }

        ResourceManager.Instance.AddResource(resType, amount);
        LogSuccess($"Dodano +{amount} {resType}. Aktualnie: {ResourceManager.Instance.GetResourceAmount(resType)}");
    }

    private void HandleKillAll()
    {
        EnemyStats[] allEnemies = FindObjectsByType<EnemyStats>(FindObjectsSortMode.None);
        int count = 0;

        foreach (var enemy in allEnemies)
        {
            if (enemy != null && enemy.GetCurrentHealth() > 0)
            {
                enemy.TakeDamage(999999f, DamageType.Physical, 100, 100, true, 1000);
                count++;
            }
        }

        // Dodatkowo upewnij się co do EnemyRegistry
        if (EnemyRegistry.ActiveEnemies != null)
        {
            var registrySnapshot = new List<EnemyStats>(EnemyRegistry.ActiveEnemies);
            foreach (var enemy in registrySnapshot)
            {
                if (enemy != null && enemy.GetCurrentHealth() > 0)
                {
                    enemy.TakeDamage(999999f, DamageType.Physical, 100, 100, true, 1000);
                    count++;
                }
            }
        }

        LogSuccess($"Zabito wszystkich wrogów ({count} wyeliminowanych).");
    }

    private void HandleGodMode()
    {
        if (GameManager.Instance == null)
        {
            LogError("GameManager.Instance nie istnieje na scenie.");
            return;
        }

        GameManager.Instance.isInvincible = !GameManager.Instance.isInvincible;

        if (GameManager.Instance.isInvincible)
        {
            LogSuccess("<b>God Mode: WŁĄCZONY</b> (Baza ignoruje wszystkie obrażenia).");
        }
        else
        {
            LogWarning("<b>God Mode: WYŁĄCZONY</b> (Baza ponownie otrzymuje obrażenia).");
        }
    }

    private void HandleBaseHealth(string[] parts)
    {
        if (GameManager.Instance == null)
        {
            LogError("GameManager.Instance nie istnieje na scenie.");
            return;
        }

        if (parts.Length < 2 || !int.TryParse(parts[1], out int value))
        {
            LogError("Użycie: hp <ilość>");
            return;
        }

        GameManager.Instance.SetBaseHealth(value);
        LogSuccess($"Punkty życia bramy bazy ustawione na {value}.");
    }

    private void HandleWave(string[] parts)
    {
        if (parts.Length < 2 || parts[1].ToLowerInvariant() != "finish")
        {
            LogError("Użycie: wave finish");
            return;
        }

        var spawner = FindAnyObjectByType<EnemySpawner>();
        if (spawner != null)
        {
            spawner.ForceCompleteWave();
            LogSuccess("Fala nocy została natychmiastowo zakończona.");
        }
        else
        {
            // Fallback jeśli spawner nie istnieje
            HandleKillAll();
            TimePhaseManager.Instance?.ChangeCurrentPhase();
            LogSuccess("Przełączono fazę nocy (spawner nie był aktywny).");
        }
    }

    private void HandleSpawn(string[] parts)
    {
        if (parts.Length < 2)
        {
            LogError("Użycie: spawn <nazwa_wroga> [ilość]");
            return;
        }

        string enemyName = parts[1];
        int count = 1;
        if (parts.Length >= 3)
        {
            int.TryParse(parts[2], out count);
        }
        count = Mathf.Max(1, count);

        var spawner = FindAnyObjectByType<EnemySpawner>();
        if (spawner == null)
        {
            LogError("Nie znaleziono EnemySpawner na scenie.");
            return;
        }

        bool success = spawner.SpawnEnemyByName(enemyName, count);
        if (success)
        {
            LogSuccess($"Zespawnowano {count}x '{enemyName}' na trasach wrogów.");
        }
        else
        {
            string available = spawner.availableEnemies != null
                ? string.Join(", ", spawner.availableEnemies.Where(e => e != null).Select(e => !string.IsNullOrEmpty(e.enemyName) ? e.enemyName : e.name))
                : "brak";
            LogError($"Nie znaleziono wroga '{enemyName}'. Dostępni wrogowie: {available}");
        }
    }

    private void HandleMap(string[] parts)
    {
        var expansion = MapExpansionManager.Instance;
        if (expansion == null)
        {
            LogError("MapExpansionManager.Instance nie istnieje na scenie.");
            return;
        }

        if (parts.Length < 2)
        {
            LogError("Użycie: map reveal | map settleall | map scoutall");
            return;
        }

        string sub = parts[1].ToLowerInvariant();
        var coords = new List<Vector2Int>(expansion.activeChunks.Keys);

        switch (sub)
        {
            case "reveal":
            case "settleall":
                foreach (var coord in coords)
                {
                    expansion.SetChunkBaseState(coord, ChunkState.Settled);
                }
                LogSuccess("Wszystkie chunki mapy zostały ustawione na status Settled (pełne odkrycie).");
                break;

            case "scoutall":
                int changed = 0;
                foreach (var coord in coords)
                {
                    var data = expansion.activeChunks[coord];
                    if (data.baseState == ChunkState.Surveying || data.baseState == ChunkState.Borderlands || data.baseState == ChunkState.Wilderness)
                    {
                        expansion.SetChunkBaseState(coord, ChunkState.Outskirts);
                        changed++;
                    }
                }
                LogSuccess($"Odkryto zwiadem (Outskirts) {changed} chunków.");
                break;

            default:
                LogError($"Nieznana podkomenda mapy '{sub}'. Użyj: map reveal | map settleall | map scoutall");
                break;
        }
    }

    private void HandleRuneRandom(string[] parts)
    {
        if (RuneManager.Instance == null)
        {
            LogError("RuneManager.Instance nie istnieje na scenie.");
            return;
        }

        if (parts.Length == 2)
        {
            // rune_random <ilość>
            if (int.TryParse(parts[1], out int count))
            {
                count = Mathf.Max(1, count);
                for (int i = 0; i < count; i++)
                {
                    var rune = RuneManager.Instance.GenerateRandomRune();
                    if (rune != null) RuneManager.Instance.playerRunes.Add(rune);
                }
                RuneManager.Instance.NotifyInventoryChanged();
                LogSuccess($"Wygenerowano {count} w pełni losowych run.");
                return;
            }
        }
        else if (parts.Length >= 3)
        {
            // rune_random <rarity> <ilość>
            if (!Enum.TryParse(parts[1], true, out RuneRarity rarity))
            {
                string rarities = string.Join(", ", Enum.GetNames(typeof(RuneRarity)));
                LogError($"Nieznana rzadkość '{parts[1]}'. Dostępne: {rarities}");
                return;
            }

            if (!int.TryParse(parts[2], out int count))
            {
                LogError("Użycie: rune_random <rarity> <ilość>");
                return;
            }

            count = Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                var rune = RuneManager.Instance.GenerateRandomRune(rarity);
                if (rune != null) RuneManager.Instance.playerRunes.Add(rune);
            }
            RuneManager.Instance.NotifyInventoryChanged();
            LogSuccess($"Wygenerowano {count} run o rzadkości {rarity}.");
            return;
        }

        LogError("Użycie: rune_random <ilość> LUB rune_random <rarity> <ilość>");
    }

    private void HandleRuneStat(string[] parts)
    {
        if (RuneManager.Instance == null)
        {
            LogError("RuneManager.Instance nie istnieje na scenie.");
            return;
        }

        if (parts.Length < 3)
        {
            LogError("Użycie: rune_stat <rarity> <stat> (np. rune_stat Legendary Damage)");
            return;
        }

        if (!Enum.TryParse(parts[1], true, out RuneRarity rarity))
        {
            string rarities = string.Join(", ", Enum.GetNames(typeof(RuneRarity)));
            LogError($"Nieznana rzadkość '{parts[1]}'. Dostępne: {rarities}");
            return;
        }

        if (!Enum.TryParse(parts[2], true, out RuneStatType stat))
        {
            string stats = string.Join(", ", Enum.GetNames(typeof(RuneStatType)));
            LogError($"Nieznana statystyka '{parts[2]}'. Dostępne: {stats}");
            return;
        }

        var rune = RuneManager.Instance.GenerateRuneWithStat(rarity, stat);
        if (rune != null)
        {
            LogSuccess($"Wygenerowano runę: {rune.definition.runeName} [{rarity}] ze statystyką {stat} ({rune.GetPrimaryText()}).");
        }
        else
        {
            LogError($"Nie znaleziono definicji runy dla rzadkości {rarity} i statystyki {stat}.");
        }
    }

    private void HandleRuneGeneric(string[] parts)
    {
        if (RuneManager.Instance == null)
        {
            LogError("RuneManager.Instance nie istnieje na scenie.");
            return;
        }

        if (parts.Length >= 2 && parts[1].ToLowerInvariant() == "clear")
        {
            RuneManager.Instance.ClearInventory();
            LogSuccess("Ekwipunek run gracza został wyczyszczony.");
            return;
        }

        LogError("Użycie: rune clear | rune_random <ilość> | rune_stat <rarity> <stat>");
    }

    private void HandleHope(string[] parts)
    {
        if (HopeSessionManager.Instance == null)
        {
            LogError("HopeSessionManager.Instance nie istnieje na scenie.");
            return;
        }

        if (parts.Length < 2 || !int.TryParse(parts[1], out int amount))
        {
            LogError("Użycie: hope <ilość>");
            return;
        }

        HopeSessionManager.Instance.AddRawHope(amount);
        LogSuccess($"Dodano +{amount} punktów nadziei do sesji.");
    }

    private void HandleMetaAll()
    {
        if (MetaUpgradeManager.Instance == null)
        {
            LogError("MetaUpgradeManager.Instance nie istnieje na scenie.");
            return;
        }

        int count = 0;
        foreach (var upgrade in MetaUpgradeManager.Instance.allUpgrades)
        {
            if (upgrade != null)
            {
                upgrade.isUnlocked = true;
                if (SaveManager.Instance != null && !SaveManager.Instance.currentSaveData.unlockedUpgradeIDs.Contains(upgrade.id))
                {
                    SaveManager.Instance.currentSaveData.unlockedUpgradeIDs.Add(upgrade.id);
                }
                count++;
            }
        }

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveGame();
        }

        MetaUpgradeManager.Instance.ReapplyAllUpgrades();
        LogSuccess($"Odblokowano wszystkie meta-ulepszenia ({count}) i zapisano stan gry.");
    }

    private void HandleMetaNew()
    {
        if (MetaUpgradeManager.Instance == null)
        {
            LogError("MetaUpgradeManager.Instance nie istnieje na scenie.");
            return;
        }

        foreach (var upgrade in MetaUpgradeManager.Instance.allUpgrades)
        {
            if (upgrade != null)
            {
                upgrade.isUnlocked = false;
            }
        }

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.currentSaveData.unlockedUpgradeIDs.Clear();
            SaveManager.Instance.SaveGame();
        }

        MetaUpgradeManager.Instance.ReapplyAllUpgrades();
        LogSuccess("Zablokowano wszystkie meta-ulepszenia i zresetowano save'a.");
    }

    // =========================================================================
    // HELPERY LOGOWANIA W KONSOLI
    // =========================================================================

    private void LogCommand(string text)
    {
        AddLog(text, Color.cyan);
    }

    private void LogSuccess(string text)
    {
        AddLog($"[OK] {text}", new Color(0.3f, 1f, 0.4f));
    }

    private void LogInfo(string text)
    {
        AddLog($"[INFO] {text}", Color.white);
    }

    private void LogWarning(string text)
    {
        AddLog($"[WARN] {text}", Color.yellow);
    }

    private void LogError(string text)
    {
        AddLog($"[ERR] {text}", new Color(1f, 0.35f, 0.35f));
    }

    private void AddLog(string text, Color color)
    {
        logEntries.Add(new LogEntry { text = text, color = color });
        if (logEntries.Count > maxLogEntries)
        {
            logEntries.RemoveAt(0);
        }
        scrollPosition.y = float.MaxValue;
    }
}