using UnityEngine;
using System;
using TMPro;

public class TimeCycleManager : MonoBehaviour
{
    public static TimeCycleManager Instance { get; private set; }

    [Header("Konfiguracja Czasu")]
    public float realSecondsPerHour = 2.0f;

    [Header("Cykl Dnia i Nocy")]
    public int dayStartHour = 6;   // 06:00 - Koniec fali / Pocz¹tek dnia
    public int nightStartHour = 20; // 20:00 - Pocz¹tek fali

    [Header("Podgl¹d")]
    [Range(0, 24)] public float currentTime = 6.0f;
    public int currentHour = 6;
    public int dayCount = 1;

    [Header("UI")]
    public TextMeshProUGUI timeDisplay;

    // Eventy
    public event Action<int> OnHourTick;
    public event Action<int> OnDayChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Start()
    {
        // Ustawiamy domyœln¹ prêdkoœæ na 1x na starcie
        SetTimeSpeed(1f);
    }

    private void Update()
    {
        // Czas p³ynie tylko jeœli gra nie jest zapauzowana (timeScale > 0)
        // U¿ywamy unscaledDeltaTime, ¿eby zmiana prêdkoœci gry nie psu³a obliczeñ UI,
        // ALE sama symulacja (Movement, Physics) zale¿y od Time.timeScale, wiêc tu mno¿ymy przez timeScale sami,
        // albo po prostu korzystamy ze standardowego Time.deltaTime który ju¿ uwzglêdnia skalê.

        currentTime += Time.deltaTime / realSecondsPerHour;

        UpdateUI();

        if (currentTime >= currentHour + 1)
        {
            currentHour++;

            // --- LOGIKA ZMIANY FAZY ---
            CheckPhaseChange(currentHour);

            // Nowy Dzieñ
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
        // Pocz¹tek Nocy (Fala)
        if (hour == nightStartHour)
        {
            if (GameManager.Instance.currentGameState != GameManager.gameStates.InWave)
            {
                Debug.Log($"<color=red>Godzina {hour}:00! Nadchodzi noc...</color>");
                GameManager.Instance.StartWave();
            }
        }
        // Pocz¹tek Dnia (Koniec Fali - jeœli fala zosta³a pokonana wczeœniej, to GM sam zmieni³ stan,
        // ale jeœli nie, to tutaj mo¿emy wymusiæ koniec lub po prostu zmieniæ oœwietlenie)
        else if (hour == dayStartHour)
        {
            // Opcjonalnie: Wymuszenie koñca fali o œwicie, jeœli wrogowie nie zostali zabici?
            // Albo po prostu uznanie tego za pocz¹tek dnia roboczego.
            // Na razie GameManager zarz¹dza koñcem fali po zabiciu wrogów, 
            // wiêc tutaj tylko logujemy œwit.
            Debug.Log($"<color=yellow>Godzina {hour}:00! Œwit.</color>");
        }
    }

    // --- KONTROLA PRÊDKOŒCI ---
    public void SetTimeSpeed(float scale)
    {
        Time.timeScale = scale;
        Debug.Log($"Prêdkoœæ gry ustawiona na: {scale}x");
    }

    private void UpdateUI()
    {
        if (timeDisplay != null)
        {
            float minutes = (currentTime - Mathf.Floor(currentTime)) * 60;
            timeDisplay.text = $"Dzieñ {dayCount} | {Mathf.FloorToInt(currentTime):00}:{Mathf.FloorToInt(minutes):00}";
        }
    }
}   