using UnityEngine;
using System;
using TMPro;

public class TimeCycleManager : MonoBehaviour
{
    public static TimeCycleManager Instance { get; private set; }

    [Header("Konfiguracja Czasu")]
    public float realSecondsPerHour = 2.0f;

    [Header("Cykl Dnia i Nocy")]
    public int dayStartHour = 6;
    public int nightStartHour = 20;

    [Header("Podgl¹d")]
    [Range(0, 24)] public float currentTime = 6.0f;
    public int currentHour = 6;
    public int dayCount = 1;

    [Header("UI")]
    public TextMeshProUGUI timeDisplay;

    public event Action<int> OnHourTick;
    public event Action<int> OnDayChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Start()
    {
        // ZMIANA: Gra startuje ZAPAUZOWANA (0f)
        SetTimeSpeed(0f);
    }

    private void Update()
    {
        // Czas gry korzysta ze zwyk³ego deltaTime (wiêc zatrzyma siê, gdy timeScale = 0)
        currentTime += Time.deltaTime / realSecondsPerHour;

        UpdateUI();

        if (currentTime >= currentHour + 1)
        {
            currentHour++;

            CheckPhaseChange(currentHour);

            if (currentHour >= 24)
            {
                currentHour = 0;
                currentTime = 0;
                dayCount++;
                OnDayChanged?.Invoke(dayCount);
            }

            OnHourTick?.Invoke(currentHour);
        }
    }

    private void CheckPhaseChange(int hour)
    {
        if (hour == nightStartHour)
        {
            if (GameManager.Instance.currentGameState != GameManager.gameStates.InWave)
            {
                Debug.Log($"<color=red>Godzina {hour}:00! Nadchodzi noc...</color>");
                GameManager.Instance.StartWave();
            }
        }
        else if (hour == dayStartHour)
        {
            Debug.Log($"<color=yellow>Godzina {hour}:00! Œwit.</color>");
        }
    }

    public void SetTimeSpeed(float scale)
    {
        Time.timeScale = scale;
        // Debug.Log($"Prêdkoœæ gry: {scale}x"); // Opcjonalnie wy³¹cz log, ¿eby nie spamowa³
    }

    private void UpdateUI()
    {
        if (timeDisplay != null)
        {
            float minutes = (currentTime - Mathf.Floor(currentTime)) * 60;
            timeDisplay.text = $"Dzieñ {dayCount} | {Mathf.FloorToInt(currentTime):00}:{Mathf.FloorToInt(minutes):00}";
        }
    }

    // --- POPRAWKA: Dodanie "Dzieñ X" do zwracanego stringa ---
    public string GetFormattedTime()
    {
        float minutes = (currentTime - Mathf.Floor(currentTime)) * 60;
        return $"Dzieñ {dayCount} | {Mathf.FloorToInt(currentTime):00}:{Mathf.FloorToInt(minutes):00}";
    }
}