using System; // Potrzebne do Action
using UnityEngine;
using static GameManager;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Ustawienia Startowe")]
    [SerializeField] private int startingHp = 20;
    [SerializeField] private int startingGold = 100;

    public int gold { get; private set; }
    public int mainGateHP { get; private set; }
    public int waveNumber { get; private set; }

    // ZDARZENIA 
    public event Action<int> OnGoldChanged;
    public event Action<int> OnHealthChanged;
    public event Action OnGameOver;                 // Sygna³ koñca gry
    public event Action<gameStates> OnStateChanged; // Sygan³ zmiany fazy gry preperation->wave->preparation->...


    public gameStates currentGameState { get; private set; }

    //STANY GRY
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
        gold = startingGold;

        currentGameState = gameStates.PreparePhase;
        waveNumber = 1;

    }

    private void Start()
    {
        UpdateUI();
    }

    // Metoda do zmiany z³ota (dodawanie i odejmowanie)
    public void ModifyGold(int amount)
    {
        gold += amount;

        // Zabezpieczenie przed ujemnym z³otem (opcjonalne, ale logiczne)
        if (gold < 0) gold = 0;

        Debug.Log($"Z³oto zmienione o: {amount}. Aktualne: {gold}");

        // Wyœlij sygna³ do UI! "Hej, z³oto siê zmieni³o, nowa wartoœæ to X"
        OnGoldChanged?.Invoke(gold);
    }

    // Metoda do zmiany HP
    public void ModifyBaseHealth(int amount)
    {
        mainGateHP += amount;

        Debug.Log($"¯ycie bazy zmienione o: {amount}. Aktualne: {mainGateHP}");

        // Wyœlij sygna³ do UI
        OnHealthChanged?.Invoke(mainGateHP);

        // Logika przegranej - IDEALNE miejsce na hermetyzacjê
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
        Time.timeScale = 1f; // Zatrzymanie czasu
    }

    private void UpdateUI()
    {
        OnGoldChanged?.Invoke(gold);
        OnHealthChanged?.Invoke(mainGateHP);
    }


    //ZARZ¥DZANIE STANAMI


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