using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TimeUI : MonoBehaviour
{
    [Header("Morning")]
    [SerializeField] private Color morningBackground;
    [SerializeField] private Color morningText;
    
    [Header("Dusk")]
    [SerializeField] private Color duskBackground;
    [SerializeField] private Color duskText;
    
    [Header("Night")]
    [SerializeField] private Color nightBackground;
    [SerializeField] private Color nightText;

    [Header("Time Modes")] 
    [SerializeField] private GameObject pnlDayMode;
    [SerializeField] private GameObject pnlNightMode;

    [Header("Time Buttons")] 
    [SerializeField] private Button btnPause;
    [SerializeField] private Button btnPlay;
    [SerializeField] private Button btnFast;

    [Header("Phase Button")] 
    [SerializeField] private Button btnNextPhase;
    
    [Space(20)]
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI phaseText;
    [SerializeField] private TextMeshProUGUI dayText;
    
    void Start()
    {
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted += ChangeToMorning;
            TimePhaseManager.Instance.OnDuskStarted += ChangeToDusk;
            TimePhaseManager.Instance.OnNightStarted += ChangeToNight;
            
            btnPause.onClick.AddListener(TimePhaseManager.Instance.PauseWave);
            btnPlay.onClick.AddListener(TimePhaseManager.Instance.ContinueWave);
            btnFast.onClick.AddListener(TimePhaseManager.Instance.SpeedUpWave);
            btnNextPhase.onClick.AddListener(OnNextPhaseButtonClicked);
            
            TimePhaseManager.Instance.OnGameSpeedChange += SetNightText;
        }

        LocalizationManager.OnLanguageChanged -= RefreshCurrentVisuals;
        LocalizationManager.OnLanguageChanged += RefreshCurrentVisuals;

        RefreshCurrentVisuals();
    }

    private void OnNextPhaseButtonClicked()
    {
        if (TimePhaseManager.Instance == null) return;

        if (TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Dusk)
        {
            if (CenterInfoPanelControllerUI.Instance != null &&
                CenterInfoPanelControllerUI.Instance.OpenHearthWindow(isPreviewOnly: false))
            {
                return;
            }
        }

        TimePhaseManager.Instance.ChangeCurrentPhase();
    }
    
    private void OnDestroy()
    {
        LocalizationManager.OnLanguageChanged -= RefreshCurrentVisuals;

        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= ChangeToMorning;
            TimePhaseManager.Instance.OnDuskStarted -= ChangeToDusk;
            TimePhaseManager.Instance.OnNightStarted -= ChangeToNight;
            
            btnPause.onClick.RemoveAllListeners();
            btnPlay.onClick.RemoveAllListeners();
            btnFast.onClick.RemoveAllListeners();
            btnNextPhase.onClick.RemoveAllListeners();

            TimePhaseManager.Instance.OnGameSpeedChange -= SetNightText;
        }
    }

    private void RefreshCurrentVisuals()
    {
        if (TimePhaseManager.Instance == null)
        {
            ChangeToMorning();
            return;
        }

        switch (TimePhaseManager.Instance.CurrentTimePhase)
        {
            case TimePhases.Morning:
                ChangeToMorning();
                break;
            case TimePhases.Dusk:
                ChangeToDusk();
                break;
            case TimePhases.Night:
                ChangeToNight();
                break;
            default:
                ChangeToMorning();
                break;
        }
    }
    
    private void ChangeToMorning()
    {
        SetPhaseVisuals(morningBackground, morningText, LocalizationManager.Get("UI_PHASE_MORNING", "Świt"));
        ChangeHangingPhase(false);
    }
    
    private void ChangeToDusk()
    {
        SetPhaseVisuals(duskBackground, duskText, LocalizationManager.Get("UI_PHASE_DUSK", "Zmierzch"));
    }
    
    private void ChangeToNight()
    {
        float speed = TimePhaseManager.Instance != null ? Time.timeScale : 1f;
        SetPhaseVisuals(nightBackground, nightText, LocalizationManager.GetFormat("UI_PHASE_NIGHT_SPEED_FMT", speed));
        ChangeHangingPhase(true);
    }

    private void SetPhaseVisuals(Color bgColor, Color txtColor, string phaseName)
    {
        if (background != null) background.color = bgColor;
        
        if (phaseText != null)
        {
            phaseText.color = txtColor;
            phaseText.text = phaseName;
        }
        
        if (dayText != null)
        {
            dayText.color = txtColor;
            int day = TimePhaseManager.Instance != null ? Mathf.Max(1, TimePhaseManager.Instance.CurrentDay) : 1;
            dayText.text = LocalizationManager.GetFormat("UI_DAY_COUNTER_FMT", day);
        }
    }

    private void ChangeHangingPhase(bool isNight)
    {
        pnlDayMode.SetActive(!isNight);
        pnlNightMode.SetActive(isNight);
        //Miejsce na ewentualne animacje dotweenem
    }

    private void SetNightText(float gameState)
    {
        phaseText.text = LocalizationManager.GetFormat("UI_PHASE_NIGHT_SPEED_FMT", gameState);
    }
}
