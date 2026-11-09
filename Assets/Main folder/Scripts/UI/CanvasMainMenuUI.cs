using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Kontroler menu pauzy / menu gry podpięty pod Canvas_MainMenu na Game scene.
/// Zgodny ze standardami UI z GEMINI.md oraz systemem lokalizacji.
/// </summary>
public class CanvasMainMenuUI : MonoBehaviour
{
    [Header("Referencje Paneli")]
    [SerializeField] private GameObject pnlBackground;

    [Header("Przyciski")]
    [SerializeField] private Button btnResume;
    [SerializeField] private Button btnBackToMenu;
    [SerializeField] private Button btnLanguage;
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

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += ApplyGeminiUIStandards;
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= ApplyGeminiUIStandards;
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
        Transform btnContainer = container.Find("Pnl_Button") ?? container;

        if (btnResume == null)
        {
            Transform t = btnContainer.Find("Btn_Start");
            if (t != null) btnResume = t.GetComponent<Button>();
        }

        if (btnBackToMenu == null)
        {
            Transform t = btnContainer.Find("Btn_Back_To_Menu");
            if (t != null) btnBackToMenu = t.GetComponent<Button>();
        }

        if (btnExit == null)
        {
            Transform t = btnContainer.Find("Btn_Exit");
            if (t != null) btnExit = t.GetComponent<Button>();
        }

        if (btnLanguage == null)
        {
            Transform t = btnContainer.Find("Btn_Language");
            if (t != null)
            {
                btnLanguage = t.GetComponent<Button>();
            }
            else if (btnExit != null)
            {
                // Dynamiczne utworzenie przycisku języka obok wyjścia
                GameObject langGo = Instantiate(btnExit.gameObject, btnExit.transform.parent);
                langGo.name = "Btn_Language";
                langGo.transform.SetSiblingIndex(btnExit.transform.GetSiblingIndex());
                btnLanguage = langGo.GetComponent<Button>();
            }
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

        if (btnLanguage != null)
        {
            btnLanguage.onClick.RemoveAllListeners();
            btnLanguage.onClick.AddListener(ToggleLanguage);
        }

        if (btnExit != null)
        {
            btnExit.onClick.RemoveAllListeners();
            btnExit.onClick.AddListener(QuitGame);
        }
    }

    private void ToggleLanguage()
    {
        string nextLang = LocalizationManager.CurrentLanguage == "pl" ? "en" : "pl";
        LocalizationManager.SetLanguage(nextLang);
    }

    private void ApplyGeminiUIStandards()
    {
        // GEMINI.md: 16 pt dla sekcji / przycisków funkcyjnych
        SetButtonTextAndSize(btnResume, LocalizationManager.Get("UI_BTN_RESUME", "Wznów grę"), 16f);
        SetButtonTextAndSize(btnBackToMenu, LocalizationManager.Get("UI_BTN_BACK_TO_MENU", "Wróć do menu"), 16f);
        SetButtonTextAndSize(btnExit, LocalizationManager.Get("UI_BTN_EXIT", "Wyjdź z gry"), 16f);

        if (btnLanguage != null)
        {
            string label = LocalizationManager.CurrentLanguage == "pl" 
                ? "Język: Polski (Switch to EN)" 
                : "Language: English (Zmień na PL)";
            SetButtonTextAndSize(btnLanguage, label, 14f);
        }
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
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (pnlBackground != null)
        {
            pnlBackground.SetActive(true);
        }

        ApplyGeminiUIStandards();
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = previousTimeScale > 0.001f ? previousTimeScale : 1f;

        if (pnlBackground != null)
        {
            pnlBackground.SetActive(false);
        }
    }

    public void BackToMainMenu()
    {
        Time.timeScale = 1f;

        string targetScene = Application.CanStreamedLevelBeLoaded(mainMenuSceneName)
            ? mainMenuSceneName
            : fallbackMainMenuSceneName;

        SceneManager.LoadScene(targetScene);
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
