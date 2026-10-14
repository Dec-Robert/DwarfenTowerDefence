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

    public int maxGateHP { get; private set; }
    public int mainGateHP { get; private set; }
    
    public int waveNumber => TimePhaseManager.Instance != null ? TimePhaseManager.Instance.CurrentDay : 1;

    public event Action<int> OnHealthChanged;
    public event Action OnGameOver;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;

        maxGateHP = cityConfig != null ? cityConfig.baseCityHP : 20;
        mainGateHP = maxGateHP;
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(mainGateHP);

        if (TelemetryManager.Instance != null)
        {
            TelemetryManager.Instance.RecordRunStarted();
        }
    }
    
    public bool isInvincible { get; set; } = false;

    public void ModifyBaseHealth(int amount)
    {
        if (isInvincible && amount < 0) return;

        mainGateHP += amount;
        if (mainGateHP > maxGateHP)
        {
            maxGateHP = mainGateHP;
        }

        Debug.Log($"Zycie bazy zmienione o: {amount}. Aktualne: {mainGateHP}");

        OnHealthChanged?.Invoke(mainGateHP);

        if (mainGateHP <= 0)
        {
            mainGateHP = 0;
            EndGame();
        }
    }

    public void SetBaseHealth(int value)
    {
        mainGateHP = Mathf.Max(0, value);
        if (mainGateHP > maxGateHP)
        {
            maxGateHP = mainGateHP;
        }
        Debug.Log($"Zycie bazy ustawione na: {mainGateHP}");
        OnHealthChanged?.Invoke(mainGateHP);

        if (mainGateHP <= 0)
        {
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