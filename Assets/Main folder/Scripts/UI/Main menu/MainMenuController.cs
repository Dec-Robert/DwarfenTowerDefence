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

    [Header("Meta Dane")]
    public List<MetaUpgradeSO> allMetaUpgrades;
    public int playerArtifacts = 10; // Tymczasowa waluta gracza (mock)

    // Cache widoków
    private VisualElement menuMain;
    private VisualElement menuPlay;
    private VisualElement menuProgression;
    private ScrollView metaList;
    private Label lblCurrency;
    private Label lblProgress;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        // 1. ZnajdŸ kontenery
        menuMain = root.Q<VisualElement>("Menu_Main");
        menuPlay = root.Q<VisualElement>("Menu_Play");
        menuProgression = root.Q<VisualElement>("Menu_Progression");

        metaList = root.Q<ScrollView>("MetaList");
        lblCurrency = root.Q<Label>("Lbl_Currency");
        lblProgress = root.Q<Label>("Lbl_Progress");

        // 2. Podepnij przyciski MENU G£ÓWNEGO
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
        // Przycisk "Wie¿e" jest tylko ozdob¹ (zablokowany w CSS)

        // Na start poka¿ g³ówne
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
        lblCurrency.text = $"Artefakty: {playerArtifacts}";

        // Listy pomocnicze do sortowania
        List<MetaUpgradeSO> availableUpgrades = new List<MetaUpgradeSO>();
        List<MetaUpgradeSO> ownedUpgrades = new List<MetaUpgradeSO>();

        int unlockedCount = 0;

        // 1. ETAP SEGREGACJI
        foreach (var upgrade in allMetaUpgrades)
        {
            // Zliczamy odblokowane
            if (upgrade.isUnlocked)
            {
                unlockedCount++;
                ownedUpgrades.Add(upgrade); // Idzie na dó³ listy
                continue;
            }

            // Sprawdzamy wymagania dla nieodblokowanych
            bool prereqsMet = true;
            if (upgrade.prerequisites != null)
            {
                foreach (var req in upgrade.prerequisites)
                {
                    if (!req.isUnlocked)
                    {
                        prereqsMet = false;
                        break;
                    }
                }
            }

            // Jeœli wymagania spe³nione -> Idzie na górê listy
            if (prereqsMet)
            {
                availableUpgrades.Add(upgrade);
            }
            // Jeœli wymagania niespe³nione -> W ogóle nie dodajemy (ukryte)
        }

        lblProgress.text = $"Odblokowano: {unlockedCount}/{allMetaUpgrades.Count}";

        // 2. ETAP £¥CZENIA (Najpierw Dostêpne, potem Posiadane)
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
            Label desc = new Label(upgrade.description);
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

                if (playerArtifacts >= upgrade.cost)
                {
                    buyBtn.text = $"{upgrade.cost} Art";
                    buyBtn.clicked += () => BuyUpgrade(upgrade);
                }
                else
                {
                    buyBtn.text = $"{upgrade.cost} Art";
                    buyBtn.SetEnabled(false);
                    buyBtn.style.color = new Color(1f, 0.5f, 0.5f);
                    buyBtn.tooltip = "Nie staæ Ciê";
                }
                card.Add(buyBtn);
            }

            metaList.Add(card);
        }
    }

    void BuyUpgrade(MetaUpgradeSO upgrade)
    {
        if (playerArtifacts >= upgrade.cost)
        {
            playerArtifacts -= upgrade.cost;
            upgrade.isUnlocked = true; // Zapiszemy to tylko w sesji, w prawdziwej grze tutaj SaveSystem.Save()
            RefreshProgressionUI(); // Odœwie¿ widok
        }
    }
}