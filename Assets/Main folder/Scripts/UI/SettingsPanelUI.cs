using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kontroler panelu ustawień (Pnl_Settings) na scenie MainMenu Demo scene.
/// Zastępuje proceduralnie generowane menu ekranu predefiniowanym panelem w hierarchii sceny.
/// Zgodny ze standardami interfejsu GEMINI.md:
/// - 20 pt nagłówek
/// - 16 pt sekcje i przyciski akcji
/// - 12-14 pt elementy list rozwijanych
/// </summary>
public class SettingsPanelUI : MonoBehaviour
{
    [Header("Referencje UI - Listy Rozwijane")]
    [SerializeField] private TMP_Dropdown ddResolution;
    [SerializeField] private TMP_Dropdown ddWindowMode;
    [SerializeField] private TMP_Dropdown ddLanguage;

    [Header("Referencje UI - Przyciski")]
    [SerializeField] private Button btnAccept;
    [SerializeField] private Button btnGoBack;

    [Header("Referencje UI - Etykiety Tekstowe")]
    [SerializeField] private TextMeshProUGUI txtTitle;
    [SerializeField] private TextMeshProUGUI txtResolutionLabel;
    [SerializeField] private TextMeshProUGUI txtWindowModeLabel;
    [SerializeField] private TextMeshProUGUI txtLanguageLabel;
    [SerializeField] private TextMeshProUGUI txtAccept;
    [SerializeField] private TextMeshProUGUI txtGoBack;

    public const string PREF_RES_WIDTH = "Settings_Resolution_Width";
    public const string PREF_RES_HEIGHT = "Settings_Resolution_Height";
    public const string PREF_WINDOW_MODE = "Settings_Window_Mode";

    [Serializable]
    public struct ResolutionOption
    {
        public int width;
        public int height;
        public string label;

        public ResolutionOption(int w, int h, string l)
        {
            width = w;
            height = h;
            label = l;
        }
    }

    [Serializable]
    public struct WindowModeOption
    {
        public FullScreenMode mode;
        public string labelKey;
        public string defaultLabel;

        public WindowModeOption(FullScreenMode m, string key, string def)
        {
            mode = m;
            labelKey = key;
            defaultLabel = def;
        }
    }

    private readonly List<ResolutionOption> resolutionOptions = new List<ResolutionOption>()
    {
        new ResolutionOption(1280, 720, "1280 x 720 (16:9 HD)"),
        new ResolutionOption(1366, 768, "1366 x 768 (16:9)"),
        new ResolutionOption(1600, 900, "1600 x 900 (16:9 HD+)"),
        new ResolutionOption(1920, 1080, "1920 x 1080 (16:9 Full HD)"),
        new ResolutionOption(2560, 1440, "2560 x 1440 (16:9 2K QHD)"),
        new ResolutionOption(3840, 2160, "3840 x 2160 (16:9 4K UHD)")
    };

    private readonly List<WindowModeOption> windowModeOptions = new List<WindowModeOption>()
    {
        new WindowModeOption(FullScreenMode.ExclusiveFullScreen, "UI_MODE_FULLSCREEN", "Pełny ekran"),
        new WindowModeOption(FullScreenMode.FullScreenWindow, "UI_MODE_BORDERLESS", "Pełny ekran w oknie"),
        new WindowModeOption(FullScreenMode.Windowed, "UI_MODE_WINDOWED", "W oknie")
    };

    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        FindReferencesIfNull();
        InitializeResolutionList();
        LoadAndApplySavedSettings();
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
    }

    private void Start()
    {
        FindReferencesIfNull();
        SetupDropdowns();
        SetupButtons();
        ApplyGeminiUIStandards();
        SyncUIFromCurrentSettings();
    }

    public void FindReferencesIfNull()
    {
        if (txtTitle == null)
        {
            Transform t = transform.Find("Txt_Settings");
            if (t != null) txtTitle = t.GetComponent<TextMeshProUGUI>();
        }

        Transform btnContainer = transform.Find("Pnl_Button") ?? transform;

        if (ddResolution == null)
        {
            Transform t = btnContainer.Find("Pnl_Resolution/Dropdown") ?? transform.Find("Dropdown_Resolution");
            if (t != null) ddResolution = t.GetComponent<TMP_Dropdown>();
        }

        if (txtResolutionLabel == null)
        {
            Transform t = btnContainer.Find("Pnl_Resolution/Txt_Resolution");
            if (t != null) txtResolutionLabel = t.GetComponent<TextMeshProUGUI>();
        }

        if (ddWindowMode == null)
        {
            Transform t = btnContainer.Find("Pnl_WindowsMode/Dropdown") ?? transform.Find("Dropdown_WindowsMode");
            if (t != null) ddWindowMode = t.GetComponent<TMP_Dropdown>();
        }

        if (txtWindowModeLabel == null)
        {
            Transform t = btnContainer.Find("Pnl_WindowsMode/Txt_WindowsMode");
            if (t != null) txtWindowModeLabel = t.GetComponent<TextMeshProUGUI>();
        }

        if (ddLanguage == null)
        {
            Transform t = btnContainer.Find("Pnl_Language/Dropdown") ?? transform.Find("Dropdown_Language");
            if (t != null) ddLanguage = t.GetComponent<TMP_Dropdown>();
        }

        if (txtLanguageLabel == null)
        {
            Transform t = btnContainer.Find("Pnl_Language/Txt_Language");
            if (t != null) txtLanguageLabel = t.GetComponent<TextMeshProUGUI>();
        }

        Transform buttonsTr = btnContainer.Find("Pnl_Buttons") ?? btnContainer;

        if (btnAccept == null)
        {
            Transform t = buttonsTr.Find("Btn_Accept");
            if (t != null) btnAccept = t.GetComponent<Button>();
        }

        if (btnGoBack == null)
        {
            Transform t = buttonsTr.Find("Btn_GoBack");
            if (t != null) btnGoBack = t.GetComponent<Button>();
        }

        if (txtAccept == null && btnAccept != null)
        {
            txtAccept = btnAccept.GetComponentInChildren<TextMeshProUGUI>();
        }

        if (txtGoBack == null && btnGoBack != null)
        {
            txtGoBack = btnGoBack.GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    private void InitializeResolutionList()
    {
        int nativeW = Screen.currentResolution.width;
        int nativeH = Screen.currentResolution.height;

        bool exists = false;
        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            if (resolutionOptions[i].width == nativeW && resolutionOptions[i].height == nativeH)
            {
                exists = true;
                break;
            }
        }

        if (!exists && nativeW > 0 && nativeH > 0)
        {
            resolutionOptions.Add(new ResolutionOption(nativeW, nativeH, $"{nativeW} x {nativeH} (Natywna / Wykryta)"));
            resolutionOptions.Sort((a, b) => (a.width != b.width) ? a.width.CompareTo(b.width) : a.height.CompareTo(b.height));
        }
    }

    public static void LoadAndApplySavedSettings()
    {
        if (PlayerPrefs.HasKey(PREF_RES_WIDTH) && PlayerPrefs.HasKey(PREF_RES_HEIGHT))
        {
            int savedW = PlayerPrefs.GetInt(PREF_RES_WIDTH, 1920);
            int savedH = PlayerPrefs.GetInt(PREF_RES_HEIGHT, 1080);
            FullScreenMode savedMode = (FullScreenMode)PlayerPrefs.GetInt(PREF_WINDOW_MODE, (int)FullScreenMode.FullScreenWindow);

            Screen.SetResolution(savedW, savedH, savedMode);
            Debug.Log($"[SettingsPanelUI] Załadowano zapisane ustawienia ekranu: {savedW}x{savedH}, tryb: {savedMode}");
        }
    }

    private void SetupDropdowns()
    {
        if (ddResolution != null)
        {
            ddResolution.ClearOptions();
            List<string> options = new List<string>();
            foreach (var opt in resolutionOptions)
            {
                options.Add(opt.label);
            }
            ddResolution.AddOptions(options);
        }

        PopulateWindowModeDropdown();
        PopulateLanguageDropdown();
    }

    private void PopulateWindowModeDropdown()
    {
        if (ddWindowMode != null)
        {
            int curVal = ddWindowMode.value;
            ddWindowMode.ClearOptions();
            List<string> options = new List<string>();
            foreach (var opt in windowModeOptions)
            {
                string text = LocalizationManager.Get(opt.labelKey, opt.defaultLabel);
                options.Add(text);
            }
            ddWindowMode.AddOptions(options);
            if (curVal >= 0 && curVal < options.Count)
            {
                ddWindowMode.SetValueWithoutNotify(curVal);
            }
        }
    }

    private void PopulateLanguageDropdown()
    {
        if (ddLanguage != null)
        {
            ddLanguage.ClearOptions();
            List<string> options = new List<string> { "Polski", "English" };
            ddLanguage.AddOptions(options);
            int curIdx = (LocalizationManager.CurrentLanguage == "en") ? 1 : 0;
            ddLanguage.SetValueWithoutNotify(curIdx);
        }
    }

    private void SetupButtons()
    {
        if (btnAccept != null)
        {
            btnAccept.onClick.RemoveAllListeners();
            btnAccept.onClick.AddListener(ApplySettings);
        }

        if (btnGoBack != null)
        {
            btnGoBack.onClick.RemoveAllListeners();
            btnGoBack.onClick.AddListener(CloseSettings);
        }
    }

    private void HandleLanguageChanged()
    {
        ApplyGeminiUIStandards();
        PopulateWindowModeDropdown();
        PopulateLanguageDropdown();
        SyncUIFromCurrentSettings();
    }

    public void ApplyGeminiUIStandards()
    {
        // Aktualizacja etykiet lokalizacyjnych z zachowaniem czcionek i ich rozmiarów ze sceny
        if (txtTitle != null)
        {
            txtTitle.text = LocalizationManager.Get("UI_HEADER_SETTINGS", "USTAWIENIA");
        }

        if (txtResolutionLabel != null)
        {
            txtResolutionLabel.text = LocalizationManager.Get("UI_SETTINGS_RESOLUTION_LABEL", "Rozdzielczość ekranu:");
        }

        if (txtWindowModeLabel != null)
        {
            txtWindowModeLabel.text = LocalizationManager.Get("UI_SETTINGS_WINDOW_MODE_LABEL", "Tryb okna:");
        }

        if (txtLanguageLabel != null)
        {
            txtLanguageLabel.text = LocalizationManager.Get("UI_LABEL_LANGUAGE_SELECT", "Język gry:");
        }

        if (txtAccept != null)
        {
            txtAccept.text = LocalizationManager.Get("UI_SETTINGS_BTN_APPLY", "Zastosuj");
        }

        if (txtGoBack != null)
        {
            txtGoBack.text = LocalizationManager.Get("UI_BTN_BACK_TO_MENU", "Do menu");
        }
    }

    public void OpenSettings()
    {
        FindReferencesIfNull();
        gameObject.SetActive(true);
        SyncUIFromCurrentSettings();
    }

    public void CloseSettings()
    {
        gameObject.SetActive(false);
    }

    public void SyncUIFromCurrentSettings()
    {
        if (ddResolution != null && resolutionOptions.Count > 0)
        {
            int curW = Screen.width;
            int curH = Screen.height;

            if (PlayerPrefs.HasKey(PREF_RES_WIDTH) && PlayerPrefs.HasKey(PREF_RES_HEIGHT))
            {
                curW = PlayerPrefs.GetInt(PREF_RES_WIDTH, curW);
                curH = PlayerPrefs.GetInt(PREF_RES_HEIGHT, curH);
            }

            int bestIdx = 0;
            for (int i = 0; i < resolutionOptions.Count; i++)
            {
                if (resolutionOptions[i].width == curW && resolutionOptions[i].height == curH)
                {
                    bestIdx = i;
                    break;
                }
            }
            ddResolution.SetValueWithoutNotify(bestIdx);
        }

        if (ddWindowMode != null && windowModeOptions.Count > 0)
        {
            FullScreenMode curMode = Screen.fullScreenMode;
            if (PlayerPrefs.HasKey(PREF_WINDOW_MODE))
            {
                curMode = (FullScreenMode)PlayerPrefs.GetInt(PREF_WINDOW_MODE, (int)curMode);
            }

            int modeIdx = 0;
            for (int i = 0; i < windowModeOptions.Count; i++)
            {
                if (windowModeOptions[i].mode == curMode)
                {
                    modeIdx = i;
                    break;
                }
            }
            ddWindowMode.SetValueWithoutNotify(modeIdx);
        }

        if (ddLanguage != null)
        {
            int langIdx = (LocalizationManager.CurrentLanguage == "en") ? 1 : 0;
            ddLanguage.SetValueWithoutNotify(langIdx);
        }
    }

    public void ApplySettings()
    {
        if (ddResolution != null && ddResolution.value >= 0 && ddResolution.value < resolutionOptions.Count)
        {
            var res = resolutionOptions[ddResolution.value];
            FullScreenMode mode = FullScreenMode.FullScreenWindow;
            if (ddWindowMode != null && ddWindowMode.value >= 0 && ddWindowMode.value < windowModeOptions.Count)
            {
                mode = windowModeOptions[ddWindowMode.value].mode;
            }

            Screen.SetResolution(res.width, res.height, mode);
            PlayerPrefs.SetInt(PREF_RES_WIDTH, res.width);
            PlayerPrefs.SetInt(PREF_RES_HEIGHT, res.height);
            PlayerPrefs.SetInt(PREF_WINDOW_MODE, (int)mode);
            PlayerPrefs.Save();
            Debug.Log($"[SettingsPanelUI] Zastosowano i zapisano ustawienia: {res.width}x{res.height}, tryb: {mode}");
        }

        if (ddLanguage != null)
        {
            string selectedLang = (ddLanguage.value == 1) ? "en" : "pl";
            LocalizationManager.SetLanguage(selectedLang);
        }
    }
}
