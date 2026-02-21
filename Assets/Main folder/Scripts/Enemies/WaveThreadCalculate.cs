using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Oblicza Wave Threat Budget (WTB) i dobiera wrogów z palety.
///
/// Typy fal:
///   EarlyWarning  – fale 1-10  (łagodne, ostrzegawcze)
///   Ramp          – fale 11-20 (narastanie trudności)
///   Standard      – normalna fala
///   Spike         – co 15 fal (duży skok trudności)
///   Cooldown      – bezpośrednio po Spike (odetchnięcie)
/// </summary>
public class WaveThreatCalculator
{
    public enum WaveDifficultyType
    {
        EarlyWarning,
        Ramp,
        Standard,
        Spike,
        Cooldown
    }

    private readonly WaveScalingConfig cfg;

    public WaveThreatCalculator(WaveScalingConfig config)
    {
        cfg = config;
    }

    // =========================================================================
    // API Publiczne
    // =========================================================================

    /// <summary>Klasyfikuje falę i oblicza jej WTB.</summary>
    public float CalculateBudget(int waveNumber, out WaveDifficultyType diffType)
    {
        diffType = ClassifyWave(waveNumber);
        float diffMultiplier = GetDifficultyMultiplier(diffType, waveNumber);
        float budget = cfg.baseWaveValue * waveNumber * diffMultiplier;

        Debug.Log($"<color=orange>[WTB] Fala {waveNumber} | Typ: <b>{diffType}</b> | " +
                  $"Mnożnik: {diffMultiplier:F2} | Budżet: <b>{budget:F1} PKT</b></color>");

        return budget;
    }

    /// <summary>
    /// Kupuje wrogów z palety za podany budżet.
    /// echoPriorityEnemy (może być null) – wymuszony typ z Echo, dostaje echoBudgetShare budżetu.
    /// </summary>
    public Queue<EnemyData> SpendBudget(
        float totalBudget,
        WavePaletteSystem palette,
        EnemyData echoPriorityEnemy,
        float echoBudgetShare)
    {
        var result = new List<EnemyData>();
        float remainingBudget = totalBudget;

        // --- Faza 1: Priorytet Echo ---
        if (echoPriorityEnemy != null && echoBudgetShare > 0f)
        {
            float echoBudget = totalBudget * echoBudgetShare;
            remainingBudget -= echoBudget;

            BuyEnemiesFor(echoBudget, echoPriorityEnemy, ref result);
            Debug.Log($"<color=magenta>[Echo] Priorytet: {echoPriorityEnemy.enemyName} " +
                      $"za {echoBudget:F1} PKT ({echoBudgetShare * 100:F0}% budżetu)</color>");
        }

        // --- Faza 2: Normalne losowanie z palety ---
        int safety = 2000;
        while (remainingBudget > 0.01f && safety-- > 0)
        {
            // Dobór wrogów których stać
            EnemyData candidate = palette.DrawEnemy(cfg.supportSpawnChance);

            if (candidate == null || candidate.threatCost > remainingBudget)
            {
                // Szukamy tańszego z Main
                var affordable = palette.MainTypes.FindAll(e => e.threatCost <= remainingBudget);
                if (affordable.Count == 0) break;
                candidate = affordable[Random.Range(0, affordable.Count)];
            }

            result.Add(candidate);
            remainingBudget -= candidate.threatCost;
        }

        // --- Tasowanie i log ---
        Shuffle(result);
        LogWaveComposition(result, totalBudget - remainingBudget);

        // Zamiana na Queue
        var queue = new Queue<EnemyData>();
        foreach (var e in result) queue.Enqueue(e);
        return queue;
    }

    public WaveDifficultyType ClassifyWave(int waveNumber)
    {
        if (waveNumber <= 10)  return WaveDifficultyType.EarlyWarning;
        if (waveNumber <= cfg.normalPhaseStartWave - 1) return WaveDifficultyType.Ramp;
        if (IsSpike(waveNumber))    return WaveDifficultyType.Spike;
        if (IsSpike(waveNumber - 1)) return WaveDifficultyType.Cooldown;
        return WaveDifficultyType.Standard;
    }

    // =========================================================================
    // Prywatne
    // =========================================================================

    private bool IsSpike(int waveNumber)
        => waveNumber > 0 && waveNumber % cfg.spikeInterval == 0;

    private float GetDifficultyMultiplier(WaveDifficultyType type, int waveNumber)
    {
        switch (type)
        {
            case WaveDifficultyType.EarlyWarning: return cfg.earlyPhaseMultiplier;
            case WaveDifficultyType.Ramp:         return cfg.rampPhaseMultiplier;
            case WaveDifficultyType.Spike:        return cfg.diffSpike;
            case WaveDifficultyType.Cooldown:     return cfg.diffCooldown;
            default:                              return cfg.diffStandard;
        }
    }

    private void BuyEnemiesFor(float budget, EnemyData enemy, ref List<EnemyData> list)
    {
        if (enemy.threatCost <= 0) return;
        int count = Mathf.FloorToInt(budget / enemy.threatCost);
        for (int i = 0; i < count; i++) list.Add(enemy);
    }

    private void LogWaveComposition(List<EnemyData> list, float spent)
    {
        var counts = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var e in list)
        {
            if (!counts.ContainsKey(e.enemyName)) counts[e.enemyName] = 0;
            counts[e.enemyName]++;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>Skład fali ({list.Count} wrogów, {spent:F1} PKT):</b>");
        foreach (var kvp in counts)
            sb.AppendLine($"  · {kvp.Key}: {kvp.Value} szt.");
        Debug.Log(sb.ToString());
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T tmp = list[i];
            int j  = Random.Range(i, list.Count);
            list[i] = list[j];
            list[j] = tmp;
        }
    }
}