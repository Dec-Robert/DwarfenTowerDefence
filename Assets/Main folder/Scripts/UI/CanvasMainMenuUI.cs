using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Kontroler menu pauzy / menu gry podpięty pod Canvas_MainMenu na Game scene.
/// Zgodny ze standardami UI z GEMINI.md.
/// </summary>
public class CanvasMainMenuUI : MonoBehaviour
{
    [Header("Referencje Paneli")]
    [SerializeField] private GameObject pnlBackground;

    [Header("Przyciski")]
    [SerializeField] private Button btnResume;
    [SerializeField] private Button btnBackToMenu;
    [SerializeField] private Button btnExit;

    [Header("Konfiguracja Scen")]
    [SerializeField] private string mainMenuSceneName = "MainMenu Demo scene";
    [SerializeField] private string fallbackMainMenuSceneName = "MainMenu scene";

    private bool isPaused = false;
    private float previousTimeScale = 1f;

    public bool IsPaused => isPaused;

    private void Awake()
    {
        FindReferencesIfNull();
        if (pnlBackground != null)
        {
            pnlBackground.SetActive(false);
        }
    }

    private void Start()
    {
        FindReferencesIfNull();
        SetupButtonListeners();
        ApplyGeminiUIStandards();

        // Menu pauzy domyślnie ukryte na starcie
        if (pnlBackground != null)
        {
            pnlBackground.SetActive(false);
        }
        isPaused = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Jeśli otwarte jest okno budynku, ESC powinno je najpierw zamknąć, a nie otwierać menu pauzy
            if (!isPaused && ExtendedBuildingUI.Instance != null && ExtendedBuildingUI.Instance.IsOpen)
            {
                return;
            }

            TogglePauseMenu();
        }
    }

    private void FindReferencesIfNull()
    {
        if (pnlBackground == null)
        {
            Transform bg = transform.Find("Pnl_Background");
            if (bg != null) pnlBackground = bg.gameObject;
        }

        Transform container = pnlBackground != null ? pnlBackground.transform : transform;

        if (btnResume == null)
        {
            Transform t = container.Find("Pnl_Button/Btn_Start") ?? container.Find("Btn_Start");
            if (t != null) btnResume = t.GetComponent<Button>();
        }

        if (btnBackToMenu == null)
        {
            Transform t = container.Find("Pnl_Button/Btn_Back_To_Menu") ?? container.Find("Btn_Back_To_Menu");
            if (t != null) btnBackToMenu = t.GetComponent<Button>();
        }

        if (btnExit == null)
        {
            Transform t = container.Find("Pnl_Button/Btn_Exit") ?? container.Find("Btn_Exit");
            if (t != null) btnExit = t.GetComponent<Button>();
        }
    }

    private void SetupButtonListeners()
    {
        if (btnResume != null)
        {
            btnResume.onClick.RemoveAllListeners();
            btnResume.onClick.AddListener(ResumeGame);
        }

        if (btnBackToMenu != null)
        {
            btnBackToMenu.onClick.RemoveAllListeners();
            btnBackToMenu.onClick.AddListener(BackToMainMenu);
        }

        if (btnExit != null)
        {
            btnExit.onClick.RemoveAllListeners();
            btnExit.onClick.AddListener(QuitGame);
        }
    }

    private void ApplyGeminiUIStandards()
    {
        // GEMINI.md: 16 pt dla sekcji / przycisków funkcyjnych
        SetButtonTextAndSize(btnResume, "Wznów grę", 16f);
        SetButtonTextAndSize(btnBackToMenu, "Wróć do menu", 16f);
        SetButtonTextAndSize(btnExit, "Wyjdź z gry", 16f);
    }

    private void SetButtonTextAndSize(Button btn, string newText, float fontSize)
    {
        if (btn == null) return;
        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            if (!string.IsNullOrEmpty(newText)) tmp.text = newText;
            tmp.fontSize = fontSize;
        }
    }

    public void TogglePauseMenu()
    {
        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;

        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.PauseWave();
        }

        if (pnlBackground != null)
        {
            pnlBackground.SetActive(true);
        }
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;

        if (TimePhaseManager.Instance != null && previousTimeScale > 0f)
        {
            TimePhaseManager.Instance.ContinueWave();
            Time.timeScale = previousTimeScale;
        }

        if (pnlBackground != null)
        {
            pnlBackground.SetActive(false);
        }
    }

    public void BackToMainMenu()
    {
        Time.timeScale = 1f;

        if (Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else if (Application.CanStreamedLevelBeLoaded(fallbackMainMenuSceneName))
        {
            SceneManager.LoadScene(fallbackMainMenuSceneName);
        }
        else
        {
            Debug.LogError($"[CanvasMainMenuUI] Nie można załadować sceny menu głównego ({mainMenuSceneName} / {fallbackMainMenuSceneName})!");
        }
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
