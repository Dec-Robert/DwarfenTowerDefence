using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class MainMenuController : MonoBehaviour
{
    [Header("Referencje")]
    public UIDocument uiDocument;

    [Header("Konfiguracja")]
    public string gameSceneName = "GameScene";
    public string feedbackUrl = "https://forms.google.com/your-form-url";

    [Header("Meta Dane")] public float playerHope = 0;

    // Cache widok�w
    private VisualElement menuMain;
    private VisualElement menuPlay;
    private VisualElement menuProgression;
    private ScrollView metaList;
    private Label lblCurrency;
    private Label lblProgress;

    private void Update()
    {
        // DEBUG: F5 dodaje 10 000 Nadziei
        if (Input.GetKeyDown(KeyCode.F5))
        {
            GiveDebugHope();
        }

        // DEBUG: F1 resetuje CAŁY postęp i wyzerowuje konto
        if (Input.GetKeyDown(KeyCode.F1))
        {
            HardResetProgression();
        }
    }
    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        playerHope = SaveManager.Instance.currentSaveData.totalHope;
        // 1. Znajd� kontenery
        menuMain = root.Q<VisualElement>("Menu_Main");
        menuPlay = root.Q<VisualElement>("Menu_Play");
        menuProgression = root.Q<VisualElement>("Menu_Progression");

        metaList = root.Q<ScrollView>("MetaList");
        lblCurrency = root.Q<Label>("Lbl_Currency");
        lblProgress = root.Q<Label>("Lbl_Progress");

        // 2. Podepnij przyciski MENU G��WNEGO
        root.Q<Button>("Btn_OpenPlay").clicked += () => SwitchMenu(menuPlay);
        root.Q<Button>("Btn_Settings").clicked += () => Debug.Log("Ustawienia...");
        root.Q<Button>("Btn_Feedback").clicked += () => Application.OpenURL(feedbackUrl);
        root.Q<Button>("Btn_Quit").clicked += QuitGame;

        // 3. Podepnij przyciski MENU PLAY
        root.Q<Button>("Btn_StartGame").clicked += StartGame;
        root.Q<Button>("Btn_OpenProgression").clicked += OpenProgression;
        root.Q<Button>("Btn_BackToMain").clicked += () => SwitchMenu(menuMain);

        // 4. Podepnij przyciski PROGRESJI
        root.Q<Button>("Btn_BackToPlay").clicked += () => SwitchMenu(menuPlay);
        // Przycisk "Wie�e" jest tylko ozdob� (zablokowany w CSS)

        // Na start poka� g��wne
        SwitchMenu(menuMain);
        
    }

    void SwitchMenu(VisualElement targetMenu)
    {
        menuMain.style.display = DisplayStyle.None;
        menuPlay.style.display = DisplayStyle.None;
        menuProgression.style.display = DisplayStyle.None;

        targetMenu.style.display = DisplayStyle.Flex;
    }

    void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // --- LOGIKA PROGRESJI ---

    void OpenProgression()
    {
        SwitchMenu(menuProgression);
        RefreshProgressionUI();
    }

    void RefreshProgressionUI()
    {
        metaList.Clear();
        lblCurrency.text = $"Nadzieja: {playerHope}";

        List<MetaUpgradeSO> availableUpgrades = new List<MetaUpgradeSO>();
        List<MetaUpgradeSO> ownedUpgrades = new List<MetaUpgradeSO>();

        int unlockedCount = 0;

        // POBIERAMY LISTĘ Z MANAGERA
        var upgradesList = MetaUpgradeManager.Instance != null ? MetaUpgradeManager.Instance.allUpgrades : new List<MetaUpgradeSO>();

        // 1. ETAP SEGREGACJI
        foreach (var upgrade in upgradesList)
        {
            // ZABEZPIECZENIE: Jeśli z jakiegoś powodu w liście jest puste pole (null), ignorujemy je
            if (upgrade == null) continue;

            // Zliczamy odblokowane
            if (upgrade.isUnlocked)
            {
                unlockedCount++;
                ownedUpgrades.Add(upgrade); // Idzie na dół listy
                continue;
            }

            // Sprawdzamy wymagania dla nieodblokowanych
            bool prereqsMet = true;
            
            // ZABEZPIECZENIE: Sprawdzamy czy lista prerequisites nie jest null
            if (upgrade.prerequisites != null)
            {
                foreach (var req in upgrade.prerequisites)
                {
                    // ZABEZPIECZENIE: Sprawdzamy czy konkretny element listy wymagań nie jest null
                    if (req != null && !req.isUnlocked)
                    {
                        prereqsMet = false;
                        break;
                    }
                }
            }

            // Jeśli wymagania spełnione -> Idzie na górę listy
            if (prereqsMet)
            {
                availableUpgrades.Add(upgrade);
            }
        }

        lblProgress.text = $"Odblokowano: {unlockedCount}/{upgradesList.Count}";

        // 2. ETAP ŁĄCZENIA
        List<MetaUpgradeSO> finalDisplayList = new List<MetaUpgradeSO>();
        finalDisplayList.AddRange(availableUpgrades);
        finalDisplayList.AddRange(ownedUpgrades);

        // 3. ETAP GENEROWANIA UI
        foreach (var upgrade in finalDisplayList)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("upgrade-card");
            if (upgrade.isUnlocked) card.AddToClassList("upgrade-card-unlocked");

            // Ikona
            if (upgrade.icon != null)
            {
                VisualElement icon = new VisualElement();
                icon.style.backgroundImage = new StyleBackground(upgrade.icon);
                icon.style.width = 50; icon.style.height = 50;
                card.Add(icon);
            }

            // Info
            VisualElement info = new VisualElement();
            info.AddToClassList("card-info");

            Label title = new Label(upgrade.upgradeName);
            title.AddToClassList("card-title");
            
            // ZABEZPIECZENIE: Upewniamy się, że description nie zwróci błędu jeśli jest puste
            Label desc = new Label(upgrade.description ?? "");
            desc.AddToClassList("card-desc");

            info.Add(title);
            info.Add(desc);
            card.Add(info);

            // Przycisk / Status
            if (upgrade.isUnlocked)
            {
                Label ownedLbl = new Label("POSIADASZ");
                ownedLbl.style.color = new Color(0.5f, 1f, 0.5f);
                card.Add(ownedLbl);
            }
            else
            {
                Button buyBtn = new Button();
                buyBtn.AddToClassList("card-buy-btn");

                if (playerHope >= upgrade.cost)
                {
                    buyBtn.text = $"{upgrade.cost} Nadziei";
                    buyBtn.clicked += () => BuyUpgrade(upgrade);
                }
                else
                {
                    buyBtn.text = $"{upgrade.cost} Nadziei";
                    buyBtn.SetEnabled(false);
                    buyBtn.style.color = new Color(1f, 0.5f, 0.5f);
                    buyBtn.tooltip = "Nie stać Cię";
                }
                card.Add(buyBtn);
            }

            metaList.Add(card);
        }
    }

    void BuyUpgrade(MetaUpgradeSO upgrade)
    {
        if (playerHope >= upgrade.cost)
        {
            playerHope -= upgrade.cost;
            upgrade.isUnlocked = true;

            // --- AKTUALIZACJA ZAPISU ---
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.currentSaveData.totalHope = (int)playerHope;
                if (!SaveManager.Instance.currentSaveData.unlockedUpgradeIDs.Contains(upgrade.id))
                {
                    SaveManager.Instance.currentSaveData.unlockedUpgradeIDs.Add(upgrade.id);
                }
                SaveManager.Instance.SaveGame(); 
            }
            
            RefreshProgressionUI();
        }
    }
    
    // =========================================================================
    // DEBUG CHEATS (Tylko w Menu Głównym)
    // =========================================================================

    private void HardResetProgression()
    {
        // 1. Zresetuj lokalną walutę w Menu
        playerHope = 0;

        // 2. Zresetuj plik Save'a (Wyzeruj konto i wyczyść listę odblokowanych ID)
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.currentSaveData.totalHope = 0;
            SaveManager.Instance.currentSaveData.unlockedUpgradeIDs.Clear();
            SaveManager.Instance.SaveGame(); // Zapisz pusty stan natychmiast na dysk
        }

        // 3. Oznacz wszystkie obiekty ScriptableObject (MetaUpgradeSO) jako ZABLOKOWANE
        if (MetaUpgradeManager.Instance != null && MetaUpgradeManager.Instance.allUpgrades != null)
        {
            foreach (var upgrade in MetaUpgradeManager.Instance.allUpgrades)
            {
                if (upgrade != null)
                {
                    upgrade.isUnlocked = false;
                }
            }
        }

        // 4. Odśwież UI, jeśli gracz aktualnie przebywa w oknie progresji
        if (menuProgression != null && menuProgression.style.display == DisplayStyle.Flex)
        {
            RefreshProgressionUI();
        }

        // Ostrzeżenie wizualne w konsoli dla pewności
        Debug.LogWarning("<color=red>[DEBUG] HARD RESET! Cały postęp wyczyszczony. Waluta: 0.</color>");
    }
    
    private void GiveDebugHope()
    {
        playerHope += 10000;

        // Natychmiastowy zapis do SaveManagera
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.currentSaveData.totalHope = (int)playerHope;
            SaveManager.Instance.SaveGame();
        }

        // Jeśli jesteśmy na ekranie progresji, odśwież licznik w rogu
        if (menuProgression.style.display == DisplayStyle.Flex)
        {
            RefreshProgressionUI();
        }

        Debug.Log("<color=green>[DEBUG] Dodano 10 000 Nadziei!</color>");
    }
}