using UnityEngine;
using System;

public class TimePhaseManager : MonoBehaviour
{
    public static TimePhaseManager Instance { get; private set; }

    public TimePhases CurrentTimePhase { get; private set; }
    public int CurrentDay { get; private set; } 

    public event Action OnMorningStarted;
    public event Action OnDuskStarted;
    public event Action OnNightStarted;
    

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
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
                break;
        }
        
        Debug.Log($"[Time Manager] Cycle changed to {CurrentTimePhase}.");
    }

    private void ChangeDay()
    {
        CurrentDay += 1;
    }
    
}