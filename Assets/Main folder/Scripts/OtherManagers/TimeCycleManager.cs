using UnityEngine;
using System;
using TMPro;

public class TimeCycleManager : MonoBehaviour
{
    public static TimeCycleManager Instance { get; private set; }

    [Header("Konfiguracja Czasu")]
    public float realSecondsPerHour = 2.0f;
    [Tooltip("Ile razy wolniej płynie czas w NOCY (np. 3 = noc jest 3x dłuższa).")]
    public float nightSlowdownFactor = 3.0f;

    // --- NOWE ZMIENNE GRACE PERIOD ---
    [Header("Grace Period (Dni Pokoju)")]
    [Tooltip("Ile pierwszych dni jest wolnych od ataków (np. 9 = 10. nocy przyjdą wrogowie)")]
    public int gracePeriodDays = 9; 
    
    [Tooltip("Mnożnik czasu nocy podczas Grace Periodu (np. 0.5 = 2x szybciej niż w dzień)")]
    public float gracePeriodNightFactor = 0.5f; 
    // ---------------------------------

    [Header("Cykl Dnia i Nocy")]
    public int dayStartHour = 6;
    public int nightStartHour = 20;

    [Header("Podgląd")]
    [Range(0, 24)] public float currentTime = 5.0f;
    public int currentHour = 5;
    public int dayCount = 1;
    
    // Zmienna do zapami�tania pr�dko�ci przed pauz� (domy�lnie 1x)
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
        // �eby po wci�ni�ciu spacji gra ruszy�a 1x, a nie zosta�a na 0.
        storedSpeed = 1f;
        Time.timeScale = 0f;
    }

    private void Update()
    {
        // 1. Sprawdzamy czy jest noc (20:00 - 06:00)
        bool isNight = (currentHour >= nightStartHour) || (currentHour < dayStartHour);

        // --- ZMIANA: Kalkulacja czasu z uwzględnieniem Grace Period ---
        float currentSecondsPerHour = realSecondsPerHour;

        if (isNight)
        {
            if (dayCount <= gracePeriodDays)
            {
                // Noc podczas Grace Periodu (np. przyspieszona 2x -> 0.5f)
                currentSecondsPerHour = realSecondsPerHour * gracePeriodNightFactor;
            }
            else
            {
                // Normalna noc z atakami (spowolniona)
                currentSecondsPerHour = realSecondsPerHour * nightSlowdownFactor;
            }
        }
        // --------------------------------------------------------------

        // 3. Dodajemy czas
        currentTime += Time.deltaTime / currentSecondsPerHour;

        // 4. Wybijanie pełnych godzin
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
            Debug.Log($"<color=yellow>Godzina {hour}:00! �wit.</color>");
        }
    }

    // --- ZMIENIONA METODA USTAWIANIA PR�DKO�CI ---
    public void SetTimeSpeed(float scale)
    {
        Time.timeScale = scale;

        // Je�li ustawiamy pr�dko�� wi�ksz� od 0, zapami�tujemy j� jako "ostatni� dobr�"
        if (scale > 0)
        {
            storedSpeed = scale;
        }
    }

    // --- NOWA METODA: PRZE��CZANIE PAUZY (Dla Spacji) ---
    public void TogglePause()
    {
        if (Time.timeScale == 0f)
        {
            // Wzn�w (wr�� do zapami�tanej)
            Time.timeScale = storedSpeed;
        }
        else
        {
            // Zapauzuj (storedSpeed zaktualizowa�o si� automatycznie w SetTimeSpeed lub jest aktualne)
            // Ale dla pewno�ci mo�emy zapisa� obecn� przed zerowaniem
            storedSpeed = Time.timeScale;
            Time.timeScale = 0f;
        }
    }
    
    public string GetFormattedTime()
    {
        float minutes = (currentTime - Mathf.Floor(currentTime)) * 60;
        return $"Dzie� {dayCount} | {Mathf.FloorToInt(currentTime):00}:{Mathf.FloorToInt(minutes):00}";
    }

    public string GetFormattedHour()
    {
        float minutes = (currentTime - Mathf.Floor(currentTime)) * 60;
        return $"{Mathf.FloorToInt(currentTime):00}:{Mathf.FloorToInt(minutes):00}";

    }
}