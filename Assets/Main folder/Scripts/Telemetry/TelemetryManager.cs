using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Analytics;
using System.Threading.Tasks;
using System.Collections.Generic;
using Unity.VisualScripting;
using Event = UnityEngine.Event;

public class TelemetryManager : MonoBehaviour
{
    public static TelemetryManager Instance { get; private set; }

    // Zmienne do mierzenia czasu trwania "Runu"
    private float runStartTime;
    private bool isRunActive = false;

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        await InitializeUnityServices();
    }

    private async Task InitializeUnityServices()
    {
        try
        {
            InitializationOptions options = new InitializationOptions();
            options.SetEnvironmentName("testing"); // Ustawienie na 'testing' zgodnie z Twoimi poprawkami

            await UnityServices.InitializeAsync(options);

            // Zbieranie podstawowych danych (w tym DAU/MAU, długość włączenia gry, kraj gracza)
            AnalyticsService.Instance.StartDataCollection();

            Debug.Log("<color=green>[TelemetryManager] Unity Analytics zainicjowane (Środowisko: testing)!</color>");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"<color=orange>[TelemetryManager] Błąd inicjalizacji: {e.Message}</color>");
        }
    }

    // =========================================================================
    // ETAP 2: ZDARZENIA GRY (CORE LOOP)
    // =========================================================================

    /// <summary>
    /// Wywoływane w momencie startu "Runu" (kiedy scena gry się załaduje).
    /// </summary>
    public void RecordRunStarted()
    {
        runStartTime = Time.unscaledTime; // Używamy unscaled, by pauza w grze nie psuła pomiaru, jeśli tego chcesz
        isRunActive = true;
        
        // Zabezpieczenie przed wysłaniem eventu przed inicjalizacją (bardzo szybki dysk gracza)
        if (UnityServices.State == ServicesInitializationState.Initialized)
        {
            RunStarted runStarted = new RunStarted();
            AnalyticsService.Instance.RecordEvent(runStarted);
            AnalyticsService.Instance.Flush(); // Wymusza wysłanie paczki
            Debug.Log("[Telemetry] Wysłano zdarzenie: run_started");
        }
    }

    /// <summary>
    /// Wywoływane, gdy gracz zginie (Game Over).
    /// Wysyłamy, ile trwał run, ile dni przeżył i z jakim stanem zasobów skończył.
    /// </summary>
    public void RecordRunEnded(int daysSurvived, int finalHopeGathered)
    {
        if (!isRunActive) return;

        float runDurationSeconds = Time.unscaledTime - runStartTime;
        float runDurationMinutes = runDurationSeconds / 60f;
        isRunActive = false;

        RunEnded runEnded = new RunEnded(daysSurvived,runDurationMinutes,finalHopeGathered);

        if (UnityServices.State == ServicesInitializationState.Initialized)
        {
            AnalyticsService.Instance.RecordEvent(runEnded);
            AnalyticsService.Instance.Flush();
            Debug.Log($"[Telemetry] Wysłano zdarzenie: run_ended | Dni: {daysSurvived} | Czas: {runDurationMinutes:F1}m");
        }
    }
}

public class RunEnded : Unity.Services.Analytics.Event
{
    public RunEnded(float _daysSurvived, float _durationMinutes, int _hopeGathered) :
        base(name: "run_ended")
    {
        daysSurvived = _daysSurvived;
        durationMinutes = _durationMinutes;
        hopeGathered = _hopeGathered;
    }
    
    public float daysSurvived { set{SetParameter("DaysSurvived", value);} }
    public float durationMinutes { set {SetParameter("DurationMinutes", value); } }
    public int hopeGathered { set{SetParameter("HopeGathered", value);} }

}

public class RunStarted : Unity.Services.Analytics.Event
{
    public RunStarted()
        : base("run_started")
    {
    }
    
}

