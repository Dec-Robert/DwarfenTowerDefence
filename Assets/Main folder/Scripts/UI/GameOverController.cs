using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class GameOverController : MonoBehaviour
{
    [Header("Referencje")]
    public UIDocument uiDocument;

    [Header("Konfiguracja Scen")]
    [SerializeField] public string mainMenuSceneName = "MainMenuScene";

    private VisualElement root;
    private Label statsLabel;
    private Button btnRestart;
    private Button btnMenu;

    private void OnEnable()
    {
        var uiRoot = uiDocument.rootVisualElement;
        root = uiRoot.Q<VisualElement>("Root");
        statsLabel = uiRoot.Q<Label>("Lbl_Stats");
        btnRestart = uiRoot.Q<Button>("Btn_Restart");
        btnMenu = uiRoot.Q<Button>("Btn_Menu");

        if (btnRestart != null) btnRestart.clicked += RestartGame;
        if (btnMenu != null) btnMenu.clicked += GoToMenu;

        // Na start ukrywamy
        root.style.display = DisplayStyle.None;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver += ShowGameOver;
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= ShowGameOver;
        }
    }

    void ShowGameOver()
    {
        // 1. Zatrzymujemy czas
        Time.timeScale = 0f;

        // 2. Pobieramy dane (Dzieñ)
        int days = 0;
        if (TimeCycleManager.Instance != null)
        {
            days = TimeCycleManager.Instance.dayCount;
        }

        // 3. Ustawiamy tekst
        if (statsLabel != null)
        {
            statsLabel.text = $"Twoja osada przetrwa³a {days} dni.\n\nLepsze jutro nigdy nie nadesz³o.";
        }

        // 4. Pokazujemy ekran (Pamiêtaj o Sort Order w Unity!)
        root.style.display = DisplayStyle.Flex;

        int earned = ResourceManager.Instance.GetResourceAmount(ResourceType.Artifacts);

        // 2. Dodaj do globalnego banku w SaveManager
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.currentSaveData.totalArtifacts += earned;
            SaveManager.Instance.SaveGame(); // Zapisujemy stan na dysku
        }

        root.style.display = DisplayStyle.Flex;
    }

    void RestartGame()
    {
        Time.timeScale = 1f; // Wa¿ne: Odblokuj czas przed reloadem!
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void GoToMenu()
    {
        Time.timeScale = 1f; // Wa¿ne: Odblokuj czas!
        SceneManager.LoadScene(mainMenuSceneName);
    }
}