using UnityEngine;
using System;
using TMPro; // WA¯NE: Dodana przestrzeñ nazw dla TextMeshPro

public class TimeCycleManager : MonoBehaviour
{
    public static TimeCycleManager Instance { get; private set; }

    [Header("Konfiguracja Czasu")]
    [Tooltip("Ile realnych sekund trwa jedna godzina w grze")]
    public float realSecondsPerHour = 2.0f;

    [Header("Podgl¹d")]
    [Range(0, 24)] public float currentTime = 6.0f; // Startujemy o 6:00 rano
    public int currentHour = 6;
    public int dayCount = 1;

    [Header("UI")]
    public TextMeshProUGUI timeDisplay; // <--- Tutaj przypisz tekst w Inspektorze

    // Eventy
    public event Action<int> OnHourTick; // Przekazuje która godzina wybi³a (0-23)
    public event Action<int> OnDayChanged; // Przekazuje numer dnia

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Update()
    {
        // Up³yw czasu
        currentTime += Time.deltaTime / realSecondsPerHour;

        // Aktualizacja wygl¹du zegara (co klatkê, ¿eby minuty p³ynê³y)
        UpdateUI();

        // Sprawdzenie pe³nej godziny (Logika gry)
        if (currentTime >= currentHour + 1)
        {
            currentHour++;

            // Obs³uga pó³nocy (Nowy Dzieñ)
            if (currentHour >= 24)
            {
                currentHour = 0;
                currentTime = 0;
                dayCount++;
                OnDayChanged?.Invoke(dayCount);
                Debug.Log($"<color=yellow>Dzieñ {dayCount} rozpoczêty!</color>");
            }

            // Wywo³anie produkcji (Budynki reaguj¹ tutaj)
            OnHourTick?.Invoke(currentHour);
        }
    }

    private void UpdateUI()
    {
        if (timeDisplay != null)
        {
            // Obliczamy minuty z u³amka godziny (dla efektu wizualnego)
            // (currentTime - floor) daje nam np. 0.5, co razy 60 daje 30 minut
            float minutes = (currentTime - Mathf.Floor(currentTime)) * 60;

            // Formatowanie: Dzieñ 1 | 06:30
            // :00 oznacza formatowanie do dwóch cyfr (np. 05 zamiast 5)
            timeDisplay.text = $"Dzieñ {dayCount} | {Mathf.FloorToInt(currentTime):00}:{Mathf.FloorToInt(minutes):00}";
        }
    }

    // Helper dla innych skryptów, jeœli potrzebuj¹ stringa z czasem
    public string GetFormattedTime()
    {
        float minutes = (currentTime - Mathf.Floor(currentTime)) * 60;
        return $"{Mathf.FloorToInt(currentTime):00}:{Mathf.FloorToInt(minutes):00}";
    }
}