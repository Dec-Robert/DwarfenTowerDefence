using UnityEngine;

public class CityStatsManager : MonoBehaviour
{
    public static CityStatsManager Instance { get; private set; }

    [Header("Globalne Bonusy (Meta/Eventy)")]
    public int globalBonusShifts = 0;
    public int globalBonusWorkersPerShift = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    // Metody do modyfikacji przez Meta Progresjê
    public void AddGlobalShift(int amount) => globalBonusShifts += amount;
    public void AddGlobalWorkerSlot(int amount) => globalBonusWorkersPerShift += amount;
}