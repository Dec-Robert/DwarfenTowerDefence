using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Analytics;
using System.Threading.Tasks;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.VisualScripting;
using Event = UnityEngine.Event;

public class TelemetryManager : MonoBehaviour
{
    public static TelemetryManager Instance { get; private set; }

    // Zmienne do mierzenia czasu trwania "Runu"
    private float runStartTime;
    private bool isRunActive = false;

    public List<string> buildedBuildings = new List<string>();
    

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
        double runDurationMinutesFormatted = (double) runDurationMinutes;
        isRunActive = false;

        RunEnded runEnded = new RunEnded(daysSurvived,runDurationMinutes,finalHopeGathered);

        if (UnityServices.State == ServicesInitializationState.Initialized)
        {
            AnalyticsService.Instance.RecordEvent(runEnded);
            Debug.Log($"[Telemetry] Zapamiętano zdarzenie: run_ended | Dni: {daysSurvived} | Czas: {runDurationMinutes:F1}m");

            Dictionary<string, int> buildingCount = CountBuildings(buildedBuildings);

            if (buildingCount.Count > 0)
            {

                foreach (var i in buildingCount)
                {
                    BuildingAmmount buildingAmmountTelemetry =  new BuildingAmmount(i.Key, i.Value);
                    AnalyticsService.Instance.RecordEvent(buildingAmmountTelemetry);
                    Debug.Log($"[Telemetry] Postawiony budynek: {i.Key} | Ilosc: {i.Value}");
                }
            }

            
            AnalyticsService.Instance.Flush();
        }
    }
    
    // =========================================================================
    // Metody powiązane z budowaniem budynkow
    // =========================================================================

    public void addBuilding(string _buildingName)
    {
        buildedBuildings.Add(_buildingName);
    }
    private Dictionary<string, int> CountBuildings(List<string> buildings)
    {
        Dictionary<string, int> buildingCount = new Dictionary<string, int>();

        foreach (var i in buildings)
        {
            if (buildingCount.ContainsKey(i)) buildingCount[i]++;
            else buildingCount.Add(i, 1);
        }
        
        return buildingCount;
    }
}



/// <summary>
/// Zakończone rozgrywki
/// </summary>
public class RunEnded : Unity.Services.Analytics.Event
{
    public RunEnded(float _daysSurvived, double _durationMinutes, int _hopeGathered) :
        base(name: "run_ended")
    {
        daysSurvived = _daysSurvived;
        durationMinutes = _durationMinutes;
        hopeGathered = _hopeGathered;
    }
    
    public float daysSurvived { set{SetParameter("DaysSurvived", value);} }
    public double durationMinutes { set {SetParameter("DurationMinutes", value); } }
    public int hopeGathered { set{SetParameter("HopeGathered", value);} }

}


/// <summary>
/// Rozpoczęte rozgrywki
/// </summary>
public class RunStarted : Unity.Services.Analytics.Event
{
    public RunStarted()
        : base("run_started")
    {
    }
    
}

/// <summary>
/// Ile danych budynków jest budowane w czasie jednego runa
/// </summary>
public class BuildingAmmount : Unity.Services.Analytics.Event
{
    public BuildingAmmount(string _buildingName,int _buildingAmount)
        : base("building_ammount")
    {
        buildingName = _buildingName;
        buidlingAmount = _buildingAmount;
    }
    public string buildingName {set{SetParameter("BuildingName", value);} }
    public int buidlingAmount {set{SetParameter("BuidlingAmmout", value);} }
}

/// <summary>
/// Ile danych budynków jest ulepszane do konkretnego tieru
/// </summary>
public class TierUpgrade : Unity.Services.Analytics.Event
{
    public TierUpgrade(string _buildingName,int _buildingTier)
        : base("building_upgrade")
    {
        buildingName = _buildingName;
        buidlingTier = _buildingTier;
    }
    public string buildingName {set{SetParameter("BuildingName", value);} }
    public int buidlingTier {set{SetParameter("BuildingTier", value);} }
}