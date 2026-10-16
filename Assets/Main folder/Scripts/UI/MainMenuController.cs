using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Kontroler dla MainMenu scene (UI Toolkit).
/// Zapewnia obsługę UI Toolkit i przejścia do Game scene lub MainMenu Demo scene.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private string gameSceneName = "Game scene";
    [SerializeField] private string feedbackUrl = "https://forms.gle/XPK9PmrQN8EDr5fb9";
    [SerializeField] private int playerHope = 0;

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }
    }

    private void Start()
    {
        // Jeśli UI Toolkit nie posiada zdefiniowanego drzewa (brak uxml w projekcie po refaktoryzacji),
        // automatycznie załaduj scenę MainMenu Demo scene
        if (uiDocument == null || uiDocument.visualTreeAsset == null)
        {
            if (Application.CanStreamedLevelBeLoaded("MainMenu Demo scene"))
            {
                SceneManager.LoadScene("MainMenu Demo scene");
            }
        }
    }

    private void OnEnable()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        var root = uiDocument.rootVisualElement;

        var startButton = root.Q<Button>("start-button") ?? root.Q<Button>("Btn_Start") ?? root.Q<Button>("start");
        if (startButton != null)
        {
            startButton.clicked += OnStartGameClicked;
        }

        var exitButton = root.Q<Button>("exit-button") ?? root.Q<Button>("Btn_Exit") ?? root.Q<Button>("exit");
        if (exitButton != null)
        {
            exitButton.clicked += OnExitClicked;
        }

        var feedbackButton = root.Q<Button>("feedback-button") ?? root.Q<Button>("feedback");
        if (feedbackButton != null)
        {
            feedbackButton.clicked += OnFeedbackClicked;
        }
    }

    private void OnDisable()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;
        var root = uiDocument.rootVisualElement;

        var startButton = root.Q<Button>("start-button") ?? root.Q<Button>("Btn_Start") ?? root.Q<Button>("start");
        if (startButton != null) startButton.clicked -= OnStartGameClicked;

        var exitButton = root.Q<Button>("exit-button") ?? root.Q<Button>("Btn_Exit") ?? root.Q<Button>("exit");
        if (exitButton != null) exitButton.clicked -= OnExitClicked;

        var feedbackButton = root.Q<Button>("feedback-button") ?? root.Q<Button>("feedback");
        if (feedbackButton != null) feedbackButton.clicked -= OnFeedbackClicked;
    }

    public void OnStartGameClicked()
    {
        Time.timeScale = 1f;
        if (Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
        else
        {
            Debug.LogError($"[MainMenuController] Nie można załadować sceny: {gameSceneName}!");
        }
    }

    public void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OnFeedbackClicked()
    {
        if (!string.IsNullOrEmpty(feedbackUrl))
        {
            Application.OpenURL(feedbackUrl);
        }
    }
}
