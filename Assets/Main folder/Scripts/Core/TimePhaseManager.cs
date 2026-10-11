using UnityEngine;
using System;

public class TimePhaseManager : MonoBehaviour
{
    public static TimePhaseManager Instance { get; private set; }

    public TimePhases CurrentTimePhase { get; private set; } = TimePhases.Morning;
    public int CurrentDay { get; private set; } = 1;

    public event Action OnMorningStarted;
    public event Action OnDuskStarted;
    public event Action OnNightStarted;

    public event Action<float> OnGameSpeedChange;
    

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else
        {
            Instance = this;
            if (CurrentDay < 1) CurrentDay = 1;
        }
    }

    private void Start()
    {
        CurrentDay = 1;
        CurrentTimePhase = TimePhases.Morning; // Default phase
        OnMorningStarted?.Invoke();
    }

    
    // Pause during waves
    public void TogglePause()
    {
        if (Time.timeScale == 0f) Time.timeScale = 1.0f;
        else Time.timeScale = 0f;
        
    }

    public void PauseWave()
    {
        Time.timeScale = 0f;
        OnGameSpeedChange.Invoke(Time.timeScale);
    }

    public void ContinueWave()
    {
        Time.timeScale = 1f;
        OnGameSpeedChange.Invoke(Time.timeScale);
    }

    public void SpeedUpWave()
    {
        if (Time.timeScale < 3f) Time.timeScale += 1;
        else if (Time.timeScale == 3f) Time.timeScale = 5f;
        else Time.timeScale = 1f;
        
        OnGameSpeedChange.Invoke(Time.timeScale);
        
    }
    
    

    public void ChangeCurrentPhase()
    {
        int timePhaseIndex = (int)CurrentTimePhase + 1;
        if (timePhaseIndex >= Enum.GetValues(typeof(TimePhases)).Length) timePhaseIndex = 0;
        
        CurrentTimePhase = (TimePhases)timePhaseIndex;

        switch (CurrentTimePhase)
        {
            case TimePhases.Morning:
                ChangeDay();
                OnMorningStarted?.Invoke();
                break;
            case TimePhases.Dusk:
                OnDuskStarted?.Invoke();
                break;
            case TimePhases.Night:
                OnNightStarted?.Invoke();
                OnGameSpeedChange.Invoke(Time.timeScale);
                break;
        }
        
        Debug.Log($"[Time Manager] Cycle changed to {CurrentTimePhase}.");
    }

    private void ChangeDay()
    {
        CurrentDay += 1;
    }
    
}