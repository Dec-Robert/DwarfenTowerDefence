using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Ustawienia Startowe")]
    [SerializeField] private int startingHp = 20;

    public int mainGateHP { get; private set; }
    public int waveNumber { get; private set; }

    // ZDARZENIA 
    // USUNIęTO: OnGoldChanged (teraz nas�uchujemy ResourceManager.OnResourceChanged w UI)
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
        // USUNI�TO: gold = startingGold;

        currentGameState = gameStates.PreparePhase;
        waveNumber = 1;
    }

    private void Start()
    {
        // Od�wie�amy UI tylko dla zdrowia (surowce od�wie�a ResourceManager w swoim Start)
        OnHealthChanged?.Invoke(mainGateHP);
    }

    // USUNI�TO: Metod� ModifyGold(int amount)

    // Metoda do zmiany HP
    public void ModifyBaseHealth(int amount)
    {
        mainGateHP += amount;

        Debug.Log($"�ycie bazy zmienione o: {amount}. Aktualne: {mainGateHP}");

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
        OnGameOver?.Invoke(); // To uruchomi ekran

        // Opcjonalnie tutaj, ale GameOverController robi to lepiej (bo obs�uguje UI)
        // Time.timeScale = 0f; 
    }

    // ZARZ�DZANIE STANAMI

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