using UnityEngine;
using System.IO;
using System.Collections.Generic;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private string savePath;
    public SaveData currentSaveData = new SaveData();

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // SaveManager żyje mi�dzy scenami
        }

        savePath = Path.Combine(Application.persistentDataPath, "player_progress.json");
        LoadGame();
    }

    [ContextMenu("Save Game")]
    public void SaveGame()
    {
        // 1. Przygotuj dane do zapisu (np. zaktualizuj list� ID z SO)
        // (Wi�kszo�� danych aktualizujemy w locie w currentSaveData)

        // 2. Konwersja na JSON
        string json = JsonUtility.ToJson(currentSaveData, true);

        // 3. Zapis do pliku
        File.WriteAllText(savePath, json);

        Debug.Log($"<color=green>[SaveManager] Gra zapisana w: {savePath}</color>");
    }

    public void LoadGame()
    {
        if (File.Exists(savePath))
        {
            // 1. Odczyt pliku
            string json = File.ReadAllText(savePath);

            // 2. Konwersja z JSON na obiekt
            currentSaveData = JsonUtility.FromJson<SaveData>(json);

            Debug.Log("[SaveManager] Wczytano post�p gracza.");
        }
        else
        {
            Debug.Log("[SaveManager] Brak pliku zapisu. Tworzenie nowej gry.");
            currentSaveData = new SaveData();
            SaveGame();
        }
    }

    // Pomocnicza metoda do synchronizacji SO z zapisanymi danymi
    public void SyncUpgradesWithSave(List<MetaUpgradeSO> allUpgrades)
    {
        foreach (var upgrade in allUpgrades)
        {
            // Je�li ID ulepszenia znajduje si� w li�cie zapisanych ID -> odblokuj je w SO
            upgrade.isUnlocked = currentSaveData.unlockedUpgradeIDs.Contains(upgrade.id);
        }
    }
}