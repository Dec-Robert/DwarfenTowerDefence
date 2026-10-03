using System;
using UnityEngine;

/// <summary>
/// Core game manager handling base health, game over state, and telemetry initialization.
/// Completely decoupled from the time phase logic, relying on TimePhaseManager instead.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Ustawienia Startowe")]
    public CityBaseConfigSO cityConfig;

    public int mainGateHP { get; private set; }
    
    public int waveNumber => TimePhaseManager.Instance != null ? TimePhaseManager.Instance.CurrentDay : 1;

    public event Action<int> OnHealthChanged;
    public event Action OnGameOver;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;

        mainGateHP = cityConfig != null ? cityConfig.baseCityHP : 20;
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(mainGateHP);

        if (TelemetryManager.Instance != null)
        {
            TelemetryManager.Instance.RecordRunStarted();
        }
    }
    
    public void ModifyBaseHealth(int amount)
    {
        mainGateHP += amount;

        Debug.Log($"Zycie bazy zmienione o: {amount}. Aktualne: {mainGateHP}");

        OnHealthChanged?.Invoke(mainGateHP);

        if (mainGateHP <= 0)
        {
            mainGateHP = 0;
            EndGame();
        }
    }

    public void InstantDestruction()
    {
        mainGateHP = 0;
        OnHealthChanged?.Invoke(mainGateHP);
        EndGame();
    }
    
    private void EndGame()
    {
        Debug.Log("KONIEC GRY!");
        OnGameOver?.Invoke(); 
    }
}