using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Moduł 4: System "Echo Błędów" (Adaptive Feedback)
///
/// Śledzi ilu wrogów każdego typu dotarło do bazy w danej fali.
/// Jeśli odsetek jest > echoBreachThreshold, typ jest flagowany jako "Zagrożenie".
/// W fali N+echoResponseDelay system wymusza jego obecność w palecie i rezerwuje
/// na niego echoPriorityBudgetShare % budżetu.
/// </summary>
public class AdaptiveFeedbackSystem
{
    private readonly WaveScalingConfig cfg;

    // ─── Tracking bieżącej fali ──────────────────────────────────────────────
    // Klucz: EnemyData, wartość: (spawned, breached)
    private Dictionary<EnemyData, WaveTracker> currentWaveTracking
        = new Dictionary<EnemyData, WaveTracker>();

    // ─── Aktywne zagrożenia ──────────────────────────────────────────────────
    // Klucz: EnemyData, wartość: fala wygaśnięcia zagrożenia
    private Dictionary<EnemyData, int> activeThreats
        = new Dictionary<EnemyData, int>();

    // ─── Zagrożenia oczekujące na aktywację (N+delay) ───────────────────────
    // Klucz: fala aktywacji, wartość: lista typów
    private Dictionary<int, List<EnemyData>> pendingThreats
        = new Dictionary<int, List<EnemyData>>();

    public AdaptiveFeedbackSystem(WaveScalingConfig config)
    {
        cfg = config;
    }

    // =========================================================================
    // API SPAWNER – Rejestruj zdarzenia
    // =========================================================================

    /// <summary>Wywołaj gdy wróg zostaje zspawnowany w bieżącej fali.</summary>
    public void RegisterSpawn(EnemyData enemy)
    {
        EnsureTracker(enemy);
        currentWaveTracking[enemy].spawned++;
    }

    /// <summary>
    /// Wywołaj gdy wróg DOTARŁ DO BAZY (ReachDestination w EnemyWalker).
    /// Wymaga przekazania danych wroga – dodaj pole w EnemyWalker lub EnemyStats.
    /// </summary>
    public void RegisterBreach(EnemyData enemy)
    {
        EnsureTracker(enemy);
        currentWaveTracking[enemy].breached++;
        Debug.Log($"<color=red>[Echo] Przełom: {enemy.enemyName} dotarł do bazy!</color>");
    }

    // =========================================================================
    // API SPAWNER – Koniec/Początek fali
    // =========================================================================

    /// <summary>Wywołaj na koniec każdej fali. Analizuje wyniki i planuje odpowiedź.</summary>
    public void OnWaveEnded(int waveNumber)
    {
        foreach (var kvp in currentWaveTracking)
        {
            var enemy   = kvp.Key;
            var tracker = kvp.Value;
            if (tracker.spawned == 0) continue;

            float breachRate = (float)tracker.breached / tracker.spawned;

            if (breachRate >= cfg.echoBreachThreshold)
            {
                int activationWave = waveNumber + cfg.echoResponseDelay;
                int expiryWave     = activationWave + cfg.echoThreatDuration;

                if (!pendingThreats.ContainsKey(activationWave))
                    pendingThreats[activationWave] = new List<EnemyData>();

                if (!pendingThreats[activationWave].Contains(enemy))
                    pendingThreats[activationWave].Add(enemy);

                Debug.Log(
                    $"<color=magenta>[Echo] Zagrożenie: <b>{enemy.enemyName}</b> " +
                    $"({tracker.breached}/{tracker.spawned} = {breachRate * 100:F0}% przełomów). " +
                    $"Aktywacja w fali {activationWave}, wygasa w fali {expiryWave}.</color>");
            }
        }

        currentWaveTracking.Clear();
    }

    /// <summary>
    /// Wywołaj na początku każdej fali.
    /// Aktywuje zaplanowane zagrożenia i usuwa wygasłe.
    /// </summary>
    public void OnWaveStarted(int waveNumber)
    {
        // Aktywacja nowych zagrożeń
        if (pendingThreats.TryGetValue(waveNumber, out var toActivate))
        {
            foreach (var enemy in toActivate)
            {
                int expiry = waveNumber + cfg.echoThreatDuration;
                activeThreats[enemy] = expiry;
                Debug.Log(
                    $"<color=magenta>[Echo] AKTYWACJA zagrożenia: <b>{enemy.enemyName}</b> " +
                    $"(wygasa fala {expiry})</color>");
            }
            pendingThreats.Remove(waveNumber);
        }

        // Usunięcie wygasłych
        var expired = activeThreats.Where(kvp => kvp.Value <= waveNumber).Select(kvp => kvp.Key).ToList();
        foreach (var e in expired)
        {
            activeThreats.Remove(e);
            Debug.Log($"<color=grey>[Echo] Zagrożenie wygasło: {e.enemyName}</color>");
        }
    }

    // =========================================================================
    // API SPAWNER – Pytania o stan
    // =========================================================================

    /// <summary>Czy istnieje aktywne zagrożenie Echo dla tej fali?</summary>
    public bool HasActiveThreat(int waveNumber) => activeThreats.Count > 0;

    /// <summary>
    /// Zwraca najważniejsze aktywne zagrożenie (najmłodsze = najnowszy wpis).
    /// Może zwrócić null jeśli brak zagrożeń.
    /// </summary>
    public EnemyData GetPriorityThreat()
    {
        if (activeThreats.Count == 0) return null;
        // Zwracamy wroga z najbliższą datą wygaśnięcia (najnowsze zagrożenie)
        return activeThreats.OrderByDescending(kvp => kvp.Value).First().Key;
    }

    /// <summary>Lista wszystkich aktywnych zagrożeń.</summary>
    public List<EnemyData> GetAllThreats() => activeThreats.Keys.ToList();

    // =========================================================================
    // Prywatne
    // =========================================================================

    private void EnsureTracker(EnemyData enemy)
    {
        if (!currentWaveTracking.ContainsKey(enemy))
            currentWaveTracking[enemy] = new WaveTracker();
    }

    private class WaveTracker
    {
        public int spawned  = 0;
        public int breached = 0;
    }
}