using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    public static readonly string[] DefaultStarterTowerIDs = new string[]
    {
        "1. Sentinel Arbalest",
        "6. Royal Bombard"
    };

    private string savePath;
    public SaveData currentSaveData = new SaveData();

    [Header("Katalog Wiez")]
    public List<TowerData> allTowers = new List<TowerData>();

    [Header("Debug")]
    [Tooltip("Gdy włączone, wszystkie wieże są domyślnie odblokowane w celach testowych/debugowania.")]
    public bool unlockAllTowersForDebug = true;

    public string SavePath
    {
        get => savePath;
        set => savePath = value;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null)
        {
            var existing = FindAnyObjectByType<SaveManager>();
            if (existing == null)
            {
                var go = new GameObject("[SaveManager]");
                existing = go.AddComponent<SaveManager>();
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // SaveManager żyje mi�dzy scenami
        }

        if (string.IsNullOrEmpty(savePath))
        {
            savePath = Path.Combine(Application.persistentDataPath, "player_progress.json");
        }
        EnsureTowersLoaded();
        LoadGame();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureTowersLoaded();
        if (allTowers != null && allTowers.Count > 0)
        {
            SyncTowersWithSave(allTowers);
        }
    }

    [ContextMenu("Save Game")]
    public void SaveGame()
    {
        // 1. Przygotuj dane do zapisu (np. zaktualizuj list� ID z SO)
        // (Wi�kszo�� danych aktualizujemy w locie w currentSaveData)

        if (string.IsNullOrEmpty(savePath))
        {
            savePath = Path.Combine(Application.persistentDataPath, "player_progress.json");
        }

        // 2. Konwersja na JSON
        string json;
        try
        {
            json = JsonUtility.ToJson(currentSaveData, true);
        }
        catch (System.Security.SecurityException)
        {
            json = SerializeSaveDataManual(currentSaveData);
        }

        // 3. Zapis do pliku
        string directory = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(savePath, json);

        try { Debug.Log($"<color=green>[SaveManager] Gra zapisana w: {savePath}</color>"); } catch { }
    }

    public void LoadGame()
    {
        if (string.IsNullOrEmpty(savePath))
        {
            savePath = Path.Combine(Application.persistentDataPath, "player_progress.json");
        }

        if (File.Exists(savePath))
        {
            // 1. Odczyt pliku
            string json = File.ReadAllText(savePath);

            // 2. Konwersja z JSON na obiekt
            try
            {
                currentSaveData = JsonUtility.FromJson<SaveData>(json);
            }
            catch (System.Security.SecurityException)
            {
                currentSaveData = DeserializeSaveDataManual(json);
            }

            Debug.Log("[SaveManager] Wczytano post�p gracza.");
        }
        else
        {
            Debug.Log("[SaveManager] Brak pliku zapisu. Tworzenie nowej gry.");
            currentSaveData = new SaveData();
        }

        if (currentSaveData == null)
        {
            currentSaveData = new SaveData();
        }

        bool needsSave = false;
        if (currentSaveData.unlockedTowerIDs == null || currentSaveData.unlockedTowerIDs.Count == 0)
        {
            currentSaveData.unlockedTowerIDs = new List<string>(DefaultStarterTowerIDs);
            needsSave = true;
        }

        if (needsSave || !File.Exists(savePath))
        {
            SaveGame();
        }

        EnsureTowersLoaded();
        if (allTowers != null && allTowers.Count > 0)
        {
            SyncTowersWithSave(allTowers);
        }
    }

    // Pomocnicza metoda do synchronizacji SO z zapisanymi danymi
    public void SyncUpgradesWithSave(List<MetaUpgradeSO> allUpgrades)
    {
        if (currentSaveData == null || allUpgrades == null) return;
        if (currentSaveData.unlockedUpgradeIDs == null)
        {
            currentSaveData.unlockedUpgradeIDs = new System.Collections.Generic.List<string>();
        }

        foreach (var upgrade in allUpgrades)
        {
            if (upgrade == null) continue;
            upgrade.isUnlocked = currentSaveData.unlockedUpgradeIDs.Contains(upgrade.id);

            // Jesli wezel nadrzedny z wariantami jest odblokowany, ale zaden wariant nie jest w save, domyslnie dodaj wariant A
            if (upgrade.hasVariants && upgrade.isUnlocked)
            {
                bool hasA = upgrade.variantA != null && currentSaveData.unlockedUpgradeIDs.Contains(upgrade.variantA.id);
                bool hasB = upgrade.variantB != null && currentSaveData.unlockedUpgradeIDs.Contains(upgrade.variantB.id);

                if (!hasA && !hasB && upgrade.variantA != null)
                {
                    currentSaveData.unlockedUpgradeIDs.Add(upgrade.variantA.id);
                    upgrade.variantA.isUnlocked = true;
                    SaveGame();
                }
            }
        }
    }

    /// <summary>
    /// Bezplatnie przelacza aktywny wariant A/B ulepszenia nadrzednego i trwale zapisuje gre.
    /// </summary>
    public void SwitchUpgradeVariant(MetaUpgradeSO baseUpgrade, string targetVariant)
    {
        if (baseUpgrade == null || !baseUpgrade.hasVariants || currentSaveData == null) return;
        if (currentSaveData.unlockedUpgradeIDs == null)
        {
            currentSaveData.unlockedUpgradeIDs = new System.Collections.Generic.List<string>();
        }

        if (targetVariant == "B" && baseUpgrade.variantB != null)
        {
            if (baseUpgrade.variantA != null)
            {
                currentSaveData.unlockedUpgradeIDs.Remove(baseUpgrade.variantA.id);
                baseUpgrade.variantA.isUnlocked = false;
            }

            if (!currentSaveData.unlockedUpgradeIDs.Contains(baseUpgrade.variantB.id))
            {
                currentSaveData.unlockedUpgradeIDs.Add(baseUpgrade.variantB.id);
            }
            baseUpgrade.variantB.isUnlocked = true;
        }
        else if (baseUpgrade.variantA != null)
        {
            if (baseUpgrade.variantB != null)
            {
                currentSaveData.unlockedUpgradeIDs.Remove(baseUpgrade.variantB.id);
                baseUpgrade.variantB.isUnlocked = false;
            }

            if (!currentSaveData.unlockedUpgradeIDs.Contains(baseUpgrade.variantA.id))
            {
                currentSaveData.unlockedUpgradeIDs.Add(baseUpgrade.variantA.id);
            }
            baseUpgrade.variantA.isUnlocked = true;
        }

        SaveGame();
    }

    // Synchronizacja stanu odblokowania wiez z SaveData
    public void SyncTowersWithSave(List<TowerData> towers)
    {
        if (towers != null && towers.Count > 0)
        {
            allTowers = towers;
        }

        var listToSync = (towers != null && towers.Count > 0) ? towers : allTowers;
        if (listToSync == null || currentSaveData == null) return;

        if (currentSaveData.unlockedTowerIDs == null)
        {
            currentSaveData.unlockedTowerIDs = new List<string>(DefaultStarterTowerIDs);
        }

        foreach (var tower in listToSync)
        {
            if (ReferenceEquals(tower, null)) continue;
            if (unlockAllTowersForDebug)
            {
                tower.isUnlocked = true;
                continue;
            }
            string tName = null;
            try { tName = tower.name; } catch { }
            tower.isUnlocked = (!string.IsNullOrEmpty(tName) && currentSaveData.unlockedTowerIDs.Contains(tName)) ||
                               (!string.IsNullOrEmpty(tower.buildingName) && currentSaveData.unlockedTowerIDs.Contains(tower.buildingName));
        }
    }

    // Odblokowanie konkretnej wiezy i zapisanie stanu gry
    public void UnlockTower(string towerID)
    {
        if (string.IsNullOrEmpty(towerID)) return;

        if (currentSaveData.unlockedTowerIDs == null)
        {
            currentSaveData.unlockedTowerIDs = new List<string>(DefaultStarterTowerIDs);
        }

        if (!currentSaveData.unlockedTowerIDs.Contains(towerID))
        {
            currentSaveData.unlockedTowerIDs.Add(towerID);
        }

        EnsureTowersLoaded();
        if (allTowers != null)
        {
            foreach (var tower in allTowers)
            {
                if (ReferenceEquals(tower, null)) continue;
                string tName = null;
                try { tName = tower.name; } catch { }
                if ((!string.IsNullOrEmpty(tName) && tName == towerID) || tower.buildingName == towerID)
                {
                    tower.isUnlocked = true;
                }
            }
        }

        SaveGame();
    }

    public void EnsureTowersLoaded()
    {
        if (allTowers != null && allTowers.Count > 0) return;

        var assetDatabaseType = System.Type.GetType("UnityEditor.AssetDatabase, UnityEditor.CoreModule") ??
                                System.Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
        if (assetDatabaseType != null)
        {
            try
            {
                var findAssetsMethod = assetDatabaseType.GetMethod("FindAssets", new System.Type[] { typeof(string) });
                var guidToPathMethod = assetDatabaseType.GetMethod("GUIDToAssetPath", new System.Type[] { typeof(string) });
                var loadAssetMethod = assetDatabaseType.GetMethod("LoadAssetAtPath", new System.Type[] { typeof(string), typeof(System.Type) });

                if (findAssetsMethod != null && guidToPathMethod != null && loadAssetMethod != null)
                {
                    string[] guids = (string[])findAssetsMethod.Invoke(null, new object[] { "t:TowerData" });
                    if (guids != null && guids.Length > 0)
                    {
                        allTowers = new List<TowerData>();
                        foreach (string guid in guids)
                        {
                            string path = (string)guidToPathMethod.Invoke(null, new object[] { guid });
                            TowerData tower = (TowerData)loadAssetMethod.Invoke(null, new object[] { path, typeof(TowerData) });
                            if (tower != null) allTowers.Add(tower);
                        }
                    }
                }
            }
            catch { }
        }
    }

    private static string SerializeSaveDataManual(SaveData data)
    {
        if (data == null) return "{}";
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"    \"totalHope\": {data.totalHope},");
        sb.Append("    \"unlockedUpgradeIDs\": [");
        if (data.unlockedUpgradeIDs != null && data.unlockedUpgradeIDs.Count > 0)
        {
            sb.AppendLine();
            for (int i = 0; i < data.unlockedUpgradeIDs.Count; i++)
            {
                sb.Append($"        \"{data.unlockedUpgradeIDs[i]}\"");
                if (i < data.unlockedUpgradeIDs.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("    ],");
        }
        else
        {
            sb.AppendLine("],");
        }
        sb.Append("    \"unlockedTowerIDs\": [");
        if (data.unlockedTowerIDs != null && data.unlockedTowerIDs.Count > 0)
        {
            sb.AppendLine();
            for (int i = 0; i < data.unlockedTowerIDs.Count; i++)
            {
                sb.Append($"        \"{data.unlockedTowerIDs[i]}\"");
                if (i < data.unlockedTowerIDs.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("    ],");
        }
        else
        {
            sb.AppendLine("],");
        }
        sb.AppendLine($"    \"isTutorialCompleted\": {(data.isTutorialCompleted ? "true" : "false")}");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static SaveData DeserializeSaveDataManual(string json)
    {
        SaveData data = new SaveData();
        if (string.IsNullOrEmpty(json)) return data;

        var matchHope = System.Text.RegularExpressions.Regex.Match(json, @"""totalHope""\s*:\s*(\d+)");
        if (matchHope.Success && int.TryParse(matchHope.Groups[1].Value, out int hope))
        {
            data.totalHope = hope;
        }

        var matchUpgrades = System.Text.RegularExpressions.Regex.Match(json, @"""unlockedUpgradeIDs""\s*:\s*\[([^\]]*)\]");
        if (matchUpgrades.Success)
        {
            var itemMatches = System.Text.RegularExpressions.Regex.Matches(matchUpgrades.Groups[1].Value, @"""([^""]+)""");
            foreach (System.Text.RegularExpressions.Match m in itemMatches)
            {
                data.unlockedUpgradeIDs.Add(m.Groups[1].Value);
            }
        }

        var matchTowers = System.Text.RegularExpressions.Regex.Match(json, @"""unlockedTowerIDs""\s*:\s*\[([^\]]*)\]");
        if (matchTowers.Success)
        {
            var itemMatches = System.Text.RegularExpressions.Regex.Matches(matchTowers.Groups[1].Value, @"""([^""]+)""");
            foreach (System.Text.RegularExpressions.Match m in itemMatches)
            {
                data.unlockedTowerIDs.Add(m.Groups[1].Value);
            }
        }

        var matchTutorial = System.Text.RegularExpressions.Regex.Match(json, "\"isTutorialCompleted\"\\s*:\\s*(true|false)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (matchTutorial.Success && bool.TryParse(matchTutorial.Groups[1].Value, out bool tut))
        {
            data.isTutorialCompleted = tut;
        }

        return data;
    }

    public bool HasCompletedTutorial()
    {
        return currentSaveData != null && currentSaveData.isTutorialCompleted;
    }

    public void SetTutorialCompleted(bool completed = true)
    {
        if (currentSaveData == null) currentSaveData = new SaveData();
        currentSaveData.isTutorialCompleted = completed;
        SaveGame();
    }
}
