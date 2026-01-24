using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    [Header("Referencje")]
    public UIDocument uiDocument;

    [Header("Konfiguracja")]
    public string mainMenuSceneName = "MainMenuScene"; // Nazwa Twojej sceny menu

    // Cache elementów UI
    private VisualElement rootOverlay;
    private VisualElement mainPanel;
    private VisualElement confirmPanel;

    private Button btnResume;
    private Button btnMainMenu;
    private Button btnQuit;

    private Button btnConfirmYes;
    private Button btnConfirmNo;

    // Stan
    private bool isPaused = false;
    private float previousTimeScale = 1f;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        rootOverlay = root.Q<VisualElement>("Overlay");
        mainPanel = root.Q<VisualElement>("MainPanel");
        confirmPanel = root.Q<VisualElement>("ConfirmPanel");

        btnResume = root.Q<Button>("Btn_Resume");
        btnMainMenu = root.Q<Button>("Btn_MainMenu");
        btnQuit = root.Q<Button>("Btn_Quit");

        btnConfirmYes = root.Q<Button>("Btn_ConfirmYes");
        btnConfirmNo = root.Q<Button>("Btn_ConfirmNo");

        // Eventy
        if (btnResume != null) btnResume.clicked += TogglePause;
        if (btnMainMenu != null) btnMainMenu.clicked += GoToMainMenu;
        if (btnQuit != null) btnQuit.clicked += ShowExitConfirmation;

        if (btnConfirmYes != null) btnConfirmYes.clicked += QuitGame;
        if (btnConfirmNo != null) btnConfirmNo.clicked += HideExitConfirmation;

        // Na start ukrywamy wszystko
        rootOverlay.style.display = DisplayStyle.None;
    }

    private void Update()
    {
        // Obs³uga klawisza ESC
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Jeœli otwarte jest okienko potwierdzenia -> cofnij do menu pauzy
            if (isPaused && confirmPanel.style.display == DisplayStyle.Flex)
            {
                HideExitConfirmation();
            }
            // W przeciwnym razie prze³¹cz pauzê
            else
            {
                TogglePause();
            }
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            // --- PAUZA ---
            previousTimeScale = Time.timeScale; // Zapamiêtaj prêdkoœæ
            Time.timeScale = 0f; // Zatrzymaj czas

            rootOverlay.style.display = DisplayStyle.Flex; // Poka¿ menu
            mainPanel.style.display = DisplayStyle.Flex;   // Poka¿ przyciski
            confirmPanel.style.display = DisplayStyle.None; // Ukryj potwierdzenie
        }
        else
        {
            // --- WZNOWIENIE ---
            Time.timeScale = previousTimeScale; // Przywróæ prêdkoœæ (lub ustaw 1f)

            // Jeœli poprzednio by³a pauza (0), to przywróæ 1
            if (previousTimeScale == 0) Time.timeScale = 1f;

            rootOverlay.style.display = DisplayStyle.None;
        }
    }

    void GoToMainMenu()
    {
        // Wa¿ne: Przywróæ czas przed zmian¹ sceny!
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    void ShowExitConfirmation()
    {
        mainPanel.style.display = DisplayStyle.None;
        confirmPanel.style.display = DisplayStyle.Flex;
    }

    void HideExitConfirmation()
    {
        confirmPanel.style.display = DisplayStyle.None;
        mainPanel.style.display = DisplayStyle.Flex;
    }

    void QuitGame()
    {
        Debug.Log("Zamykanie gry...");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}