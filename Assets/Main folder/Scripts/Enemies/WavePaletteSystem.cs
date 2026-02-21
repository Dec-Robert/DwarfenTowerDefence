using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Zarządza "Paletą Operacyjną" wrogów.
/// 
/// Działanie:
///   1. Era Dostępności – na podstawie numeru fali zwęża dostępną pulę wrogów.
///   2. Bloki – co blockSize fal losowana jest nowa paleta (Main + Support).
///   3. Paleta jest używana przez WaveThreatCalculator do doboru wrogów.
/// </summary>
public class WavePaletteSystem
{
    private readonly WaveScalingConfig cfg;
    private readonly List<EnemyData> allEnemies;

    // Aktualnie aktywna paleta
    public List<EnemyData> MainTypes    { get; private set; } = new List<EnemyData>();
    public List<EnemyData> SupportTypes { get; private set; } = new List<EnemyData>();

    // Numer ostatniego bloku dla którego wygenerowano paletę
    private int lastGeneratedBlock = -1;

    public WavePaletteSystem(WaveScalingConfig config, List<EnemyData> enemies)
    {
        cfg        = config;
        allEnemies = enemies;
    }

    // =========================================================================
    // API Publiczne
    // =========================================================================

    /// <summary>
    /// Upewnij się, że paleta jest aktualna dla danej fali.
    /// Wywołaj na początku każdej fali.
    /// </summary>
    public void EnsurePaletteForWave(int waveNumber)
    {
        int currentBlock = GetBlockIndex(waveNumber);
        if (currentBlock != lastGeneratedBlock)
        {
            RollNewPalette(waveNumber);
            lastGeneratedBlock = currentBlock;
        }
    }

    /// <summary>
    /// Zwraca pulę dostępnych wrogów z aktywnej ery (bez palette filtra).
    /// Używane przez AdaptiveFeedback do szukania zagrożonego typu.
    /// </summary>
    public List<EnemyData> GetEraPool(int waveNumber)
    {
        int maxIndex = GetEraEnemyCount(waveNumber);
        return allEnemies.Take(maxIndex).ToList();
    }

    /// <summary>
    /// Losuje typ wroga z aktualnej palety.
    /// supportChance: szansa na wybranie z puli Support zamiast Main.
    /// </summary>
    public EnemyData DrawEnemy(float supportChance = 0f)
    {
        bool useSupport = SupportTypes.Count > 0 && Random.value < supportChance;
        var pool = useSupport ? SupportTypes : MainTypes;
        if (pool.Count == 0) pool = MainTypes; // fallback
        return pool[Random.Range(0, pool.Count)];
    }

    /// <summary>
    /// Sprawdza czy dany typ wroga jest w aktywnej palecie.
    /// Używane przez Echo do wymuszenia dodania zagrożonego typu.
    /// </summary>
    public bool IsInPalette(EnemyData enemy)
        => MainTypes.Contains(enemy) || SupportTypes.Contains(enemy);

    /// <summary>
    /// Wymusza dodanie konkretnego wroga do puli Main (Echo response).
    /// Usuwa ostatni element jeśli paleta jest pełna.
    /// </summary>
    public void ForceAddToMain(EnemyData enemy)
    {
        if (MainTypes.Contains(enemy)) return;
        if (MainTypes.Count >= cfg.paletteMainCount)
            MainTypes.RemoveAt(MainTypes.Count - 1);
        MainTypes.Insert(0, enemy); // Na początku – wyższy priorytet przy losowaniu
    }

    public string GetPaletteDebugString()
    {
        string main    = string.Join(", ", MainTypes.Select(e => e.enemyName));
        string support = string.Join(", ", SupportTypes.Select(e => e.enemyName));
        return $"Main: [{main}]  Support: [{support}]";
    }

    // =========================================================================
    // Prywatne
    // =========================================================================

    private void RollNewPalette(int waveNumber)
    {
        MainTypes.Clear();
        SupportTypes.Clear();

        var era = GetEraPool(waveNumber);
        if (era.Count == 0) return;

        // Losujemy bez powtórzeń
        var shuffled = era.OrderBy(_ => Random.value).ToList();

        int mainCount    = Mathf.Min(cfg.paletteMainCount,    shuffled.Count);
        int supportStart = mainCount;
        int supportCount = Mathf.Min(cfg.paletteSupportCount, shuffled.Count - supportStart);

        MainTypes    = shuffled.Take(mainCount).ToList();
        SupportTypes = shuffled.Skip(supportStart).Take(supportCount).ToList();

        Debug.Log($"<color=cyan>[Palette] Nowa paleta (fala {waveNumber}, blok {lastGeneratedBlock + 1}):\n{GetPaletteDebugString()}</color>");
    }

    private int GetBlockIndex(int waveNumber)
        => (waveNumber - 1) / cfg.blockSize;

    private int GetEraEnemyCount(int waveNumber)
    {
        if (waveNumber <= cfg.era1MaxWave) return Mathf.Min(cfg.era1EnemyCount, allEnemies.Count);
        if (waveNumber <= cfg.era2MaxWave) return Mathf.Min(cfg.era2EnemyCount, allEnemies.Count);
        if (waveNumber <= cfg.era3MaxWave) return Mathf.Min(cfg.era3EnemyCount, allEnemies.Count);
        if (waveNumber <= cfg.era4MaxWave) return Mathf.Min(cfg.era4EnemyCount, allEnemies.Count);
        return allEnemies.Count; // Era 5 – wszystko
    }
}