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

    // Zmienna do zapamiêtania prêdkoœci przed pauz¹ (domyœlnie 1x)
    private float storedSpeed = 1f;

    public event Action<int> OnHourTick;
    public event Action<int> OnDayChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Start()
    {
        // Gra startuje zapauzowana, ale storedSpeed ustawiamy na 1,
        // ¿eby po wciœniêciu spacji gra ruszy³a 1x, a nie zosta³a na 0.
        storedSpeed = 1f;
        Time.timeScale = 0f;
    }

    private void Update()
    {
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

    // --- ZMIENIONA METODA USTAWIANIA PRÊDKOŒCI ---
    public void SetTimeSpeed(float scale)
    {
        Time.timeScale = scale;

        // Jeœli ustawiamy prêdkoœæ wiêksz¹ od 0, zapamiêtujemy j¹ jako "ostatni¹ dobr¹"
        if (scale > 0)
        {
            storedSpeed = scale;
        }
    }

    // --- NOWA METODA: PRZE£¥CZANIE PAUZY (Dla Spacji) ---
    public void TogglePause()
    {
        if (Time.timeScale == 0f)
        {
            // Wznów (wróæ do zapamiêtanej)
            Time.timeScale = storedSpeed;
        }
        else
        {
            // Zapauzuj (storedSpeed zaktualizowa³o siê automatycznie w SetTimeSpeed lub jest aktualne)
            // Ale dla pewnoœci mo¿emy zapisaæ obecn¹ przed zerowaniem
            storedSpeed = Time.timeScale;
            Time.timeScale = 0f;
        }
    }

    private void UpdateUI()
    {
        if (timeDisplay != null)
        {
            float minutes = (currentTime - Mathf.Floor(currentTime)) * 60;
            timeDisplay.text = GetFormattedTime();
        }
    }

    public string GetFormattedTime()
    {
        float minutes = (currentTime - Mathf.Floor(currentTime)) * 60;
        return $"Dzieñ {dayCount} | {Mathf.FloorToInt(currentTime):00}:{Mathf.FloorToInt(minutes):00}";
    }
}