using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
///     Menedżer dropu Etheru z wrogów podczas nocnych fal obronnych.
///     - Pula fali (WaveDefinition.etherPool) jest twardym limitem dla dropu ze zwykłych wrogów.
///     - Stopień rozpalenia Wielkiego Paleniska (HearthLevelData) wyznacza procent gwarantowany (80% -> 0%).
///     - System Kill-Pool gwarantuje, że gracz otrzyma obiecany procent eteru z pokonywanych w walce wrogów.
///     - Zwykli wrogowie mają dodatkowo stałą szansę (~5%) na naturalny drop pozostałej części puli fali.
///     - Elity i Bossowie mają osobny, gwarantowany drop konfigurowany w EnemyData, NIEZALEŻNY od puli fali.
/// </summary>
public class EnemyEtherDropManager : MonoBehaviour
{
    public static EnemyEtherDropManager Instance { get; private set; }

    [Header("Stan Bieżącej Fali")]
    [SerializeField] private int currentWaveNumber = 1;
    [SerializeField] private int currentWaveEtherPool = 3;
    [SerializeField] private int guaranteedEtherCount = 1;
    [SerializeField] private int guaranteedDroppedCount = 0;
    [SerializeField] private int naturalDroppedCount = 0;
    [SerializeField] private int eliteBossEtherDropped = 0;

    [Header("Śledzenie Zabójstw (Kill-Pool)")]
    [SerializeField] private int totalNormalKillsThisWave = 0;
    [SerializeField] private int estimatedNormalEnemiesInWave = 10;
    [SerializeField] private float activeGuaranteedPercent = 0.40f;
    [SerializeField] private float activeNaturalDropChance = 0.05f;
    [SerializeField] private string activeEtherChanceLabel = "Średnie";

    /// <summary>
    ///     Zdarzenie wywoływane przy każdym dropie Etheru:
    ///     (ilość, ranga wroga, czy gwarantowany, pozycja w świecie)
    /// </summary>
    public event Action<int, EnemyRank, bool, Vector3> OnEtherDropped;

    public int CurrentWaveEtherPool => currentWaveEtherPool;
    public int GuaranteedEtherCount => guaranteedEtherCount;
    public int GuaranteedDroppedCount => guaranteedDroppedCount;
    public int NaturalDroppedCount => naturalDroppedCount;
    public int TotalWaveEtherDropped => guaranteedDroppedCount + naturalDroppedCount;
    public int EliteBossEtherDropped => eliteBossEtherDropped;
    public float ActiveGuaranteedPercent => activeGuaranteedPercent;
    public float ActiveNaturalDropChance => activeNaturalDropChance;
    public string ActiveEtherChanceLabel => activeEtherChanceLabel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    /// <summary>
    ///     Inicjalizuje stan dropu Etheru dla rozpoczynającej się nocy / fali.
    /// </summary>
    public void InitializeWave(WaveDefinition waveDef, int waveNumber, int totalEnemiesCount)
    {
        currentWaveNumber = waveNumber;
        guaranteedDroppedCount = 0;
        naturalDroppedCount = 0;
        eliteBossEtherDropped = 0;
        totalNormalKillsThisWave = 0;

        estimatedNormalEnemiesInWave = Mathf.Max(1, totalEnemiesCount);

        if (waveDef != null)
        {
            currentWaveEtherPool = Mathf.Max(0, waveDef.etherPool);
        }
        else
        {
            // Fallback kalkulacji puli, jeśli brak assetu: np. Dzień 1 = 3, Dzień 2 = 4, Dzień 3 = 6
            currentWaveEtherPool = Mathf.Max(1, 2 + waveNumber);
        }

        activeGuaranteedPercent = BeaconEntity.GetGuaranteedEtherPercent();
        activeEtherChanceLabel = BeaconEntity.GetEtherChanceLabel();
        activeNaturalDropChance = BeaconEntity.GetNaturalEtherDropChance();

        // Oblicz dokładną liczbę gwarantowanego eteru z puli
        guaranteedEtherCount = Mathf.Clamp(
            Mathf.RoundToInt(currentWaveEtherPool * activeGuaranteedPercent),
            0,
            currentWaveEtherPool
        );

        Debug.Log($"<color=cyan>[EnemyEtherDropManager] Zainicjalizowano Falę {currentWaveNumber}: " +
                  $"Pula={currentWaveEtherPool} Etheru, Szansa={activeEtherChanceLabel} ({activeGuaranteedPercent:P0}), " +
                  $"Gwarant={guaranteedEtherCount} szt., NaturalDropSzansa={activeNaturalDropChance:P0}, " +
                  $"Szacowani wrogowie={estimatedNormalEnemiesInWave}.</color>");
    }

    /// <summary>
    ///     Rozpatruje zgon przeciwnika pod kątem przyznania Etheru.
    /// </summary>
    public void HandleEnemyDeath(EnemyStats stats, DeathSource source)
    {
        // Tylko zabójstwa w walce (wieże, gracz) generują drop
        if (source != DeathSource.Combat || stats == null) return;

        Vector3 dropPos = stats.transform.position;

        // 1. ELITY i BOSSOWIE: drop w 100% gwarantowany, NIEZALEŻNY od puli fali
        if (stats.rank == EnemyRank.Elite || stats.rank == EnemyRank.Boss)
        {
            int eliteBossDrop = 0;
            if (stats.data != null)
            {
                eliteBossDrop = stats.rank == EnemyRank.Elite
                    ? stats.data.etherDropElite
                    : stats.data.etherDropBoss;
            }
            else
            {
                eliteBossDrop = stats.rank == EnemyRank.Elite ? 1 : 5;
            }

            if (eliteBossDrop > 0)
            {
                AwardEther(eliteBossDrop, stats.rank, true, dropPos);
                eliteBossEtherDropped += eliteBossDrop;
                Debug.Log($"<color=gold>[EnemyEtherDropManager] Pokonano {stats.rank}! Drop: +{eliteBossDrop} Etheru (Niezależny od puli fali).</color>");
            }
            return;
        }

        // 2. ZWYKŁY WRÓG (Normal): rozliczany w ramach puli fali i algorytmu Kill-Pool
        totalNormalKillsThisWave++;
        int neededGuaranteed = guaranteedEtherCount - guaranteedDroppedCount;
        int remainingEstimatedKills = Mathf.Max(1, estimatedNormalEnemiesInWave - totalNormalKillsThisWave + 1);

        bool dropAwarded = false;

        // A) Warunek Gwarancji (Kill-Pool):
        // Jeśli zbliża się koniec fali lub proporcja zabójstw wskazuje na drop gwarantowany
        if (neededGuaranteed > 0)
        {
            bool triggerGuarantee = false;

            if (remainingEstimatedKills <= neededGuaranteed)
            {
                // Zabezpieczenie: zostało tyle mobów ile brakującego eteru -> 100% gwarant
                triggerGuarantee = true;
            }
            else
            {
                // Dynamiczna szansa oparta na brakującym eterze do pozostałych wrogów
                float dynamicPityChance = (float)neededGuaranteed / remainingEstimatedKills;
                if (Random.value <= dynamicPityChance)
                {
                    triggerGuarantee = true;
                }
            }

            if (triggerGuarantee)
            {
                guaranteedDroppedCount++;
                AwardEther(1, EnemyRank.Normal, true, dropPos);
                dropAwarded = true;
                Debug.Log($"<color=cyan>[EnemyEtherDropManager] Drop GWARANTOWANY: +1 Ether ({guaranteedDroppedCount}/{guaranteedEtherCount} zebranych z gwarantu).</color>");
            }
        }

        // B) Naturalny drop (jeśli ten kill nie dropnął gwarantu i limit puli fali nie został jeszcze osiągnięty)
        if (!dropAwarded && TotalWaveEtherDropped < currentWaveEtherPool)
        {
            if (Random.value <= activeNaturalDropChance)
            {
                naturalDroppedCount++;
                AwardEther(1, EnemyRank.Normal, false, dropPos);
                Debug.Log($"<color=green>[EnemyEtherDropManager] Drop NATURALNY (szansa {activeNaturalDropChance:P0}): +1 Ether (Łącznie z fali: {TotalWaveEtherDropped}/{currentWaveEtherPool}).</color>");
            }
        }
    }

    private void AwardEther(int amount, EnemyRank rank, bool isGuaranteed, Vector3 position)
    {
        if (amount <= 0) return;

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.AddResource(ResourceType.Ether, amount);
        }

        if (FloatingTextManager.Instance != null)
        {
            FloatingTextManager.Instance.ShowGain(position, "Ether", amount);
        }

        OnEtherDropped?.Invoke(amount, rank, isGuaranteed, position);
    }
}
