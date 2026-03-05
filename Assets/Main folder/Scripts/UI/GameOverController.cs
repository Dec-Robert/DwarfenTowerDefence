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
    }//

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
        Time.timeScale = 0f;

        int days = TimeCycleManager.Instance != null ? TimeCycleManager.Instance.dayCount : 0;

        int finalHope = 0;
        if (HopeSessionManager.Instance != null)
        {
            finalHope = HopeSessionManager.Instance.CalculateFinalHope();
        }

        if (statsLabel != null)
        {
            statsLabel.text = $"Twoja osada przetrwała {days} dni.\nZgromadzono Nadzieję: {finalHope}\n\nLepsze jutro nigdy nie nadeszło.";
        }

        root.style.display = DisplayStyle.Flex;

        // --- NOWE: Wysyłanie podsumowania do Unity Analytics ---
        if (TelemetryManager.Instance != null)
        {
            TelemetryManager.Instance.RecordRunEnded(days, finalHope);
        }
        // ------------------------------------------------------

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.currentSaveData.totalHope += finalHope;
            SaveManager.Instance.SaveGame(); 
        }
    }

    void RestartGame()
    {
        Time.timeScale = 1f; // Wa�ne: Odblokuj czas przed reloadem!
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void GoToMenu()
    {
        Time.timeScale = 1f; // Wa�ne: Odblokuj czas!
        SceneManager.LoadScene(mainMenuSceneName);
    }
}