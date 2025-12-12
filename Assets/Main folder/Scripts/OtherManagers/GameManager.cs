using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Ustawienia Startowe")]
    [SerializeField] private int startingHp = 20;
    // USUNIÊTO: startingGold (teraz ustawiasz to w inspektorze ResourceManagera)

    // USUNIÊTO: public int gold
    public int mainGateHP { get; private set; }
    public int waveNumber { get; private set; }

    // ZDARZENIA 
    // USUNIÊTO: OnGoldChanged (teraz nas³uchujemy ResourceManager.OnResourceChanged w UI)
    public event Action<int> OnHealthChanged;
    public event Action OnGameOver;
    public event Action<gameStates> OnStateChanged;

    public gameStates currentGameState { get; private set; }

    // STANY GRY
    public enum gameStates
    {
        InWave,
        PreparePhase,
        GameOver
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }

        // Inicjalizacja
        mainGateHP = startingHp;
        // USUNIÊTO: gold = startingGold;

        currentGameState = gameStates.PreparePhase;
        waveNumber = 1;
    }

    private void Start()
    {
        // Odœwie¿amy UI tylko dla zdrowia (surowce odœwie¿a ResourceManager w swoim Start)
        OnHealthChanged?.Invoke(mainGateHP);
    }

    // USUNIÊTO: Metodê ModifyGold(int amount)

    // Metoda do zmiany HP
    public void ModifyBaseHealth(int amount)
    {
        mainGateHP += amount;

        Debug.Log($"¯ycie bazy zmienione o: {amount}. Aktualne: {mainGateHP}");

        OnHealthChanged?.Invoke(mainGateHP);

        if (mainGateHP <= 0)
        {
            mainGateHP = 0;
            EndGame();
        }
    }

    private void EndGame()
    {
        Debug.Log("KONIEC GRY!");
        OnGameOver?.Invoke();
        Time.timeScale = 1f; // Zatrzymanie czasu (opcjonalne)
    }

    // ZARZ¥DZANIE STANAMI

    public void StartWave()
    {
        if (currentGameState == gameStates.PreparePhase)
        {
            ChangeState(gameStates.InWave);
        }
    }

    public void EndWave()
    {
        if (currentGameState == gameStates.InWave)
        {
            ChangeState(gameStates.PreparePhase);
            waveNumber++;
        }
    }

    private void ChangeState(gameStates newState)
    {
        currentGameState = newState;
        Debug.Log($"Zmiana stanu gry na: {newState}");
        OnStateChanged?.Invoke(newState);
    }
}