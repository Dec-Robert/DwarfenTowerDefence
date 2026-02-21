using UnityEngine;

/// <summary>
/// Moduł 3: Ewolucja Statystyk (Runtime Mutation)
/// 
/// Oblicza finalne statystyki wroga w momencie spawnu na podstawie numeru fali.
/// Logika elit jest nakładana na już przeskalowane wartości.
/// 
/// Klasa jest stateless – to czysty kalkulator, nie MonoBehaviour.
/// </summary>
public static class EnemyStatScaler
{
    /// <summary>
    /// Zwraca zestaw gotowych do użycia statystyk dla danego wroga i fali.
    /// Elitarne mnożniki są nakładane po skalowaniu.
    /// </summary>
    public static ScaledEnemyStats Calculate(
        EnemyData data,
        int waveNumber,
        WaveScalingConfig cfg,
        float beaconHpMod    = 1f,
        float beaconSpeedMod = 1f,
        float beaconEliteMod = 1f)
    {
        var result = new ScaledEnemyStats();

        // ─── HP (wykładniczy) ─────────────────────────────────────────────────
        float hpExponent  = Mathf.Pow(cfg.hpExponentialBase, waveNumber);
        float scaledHp    = data.baseHp * Mathf.Min(hpExponent, cfg.hpMaxMultiplier);
        result.finalHp    = scaledHp * beaconHpMod;

        // ─── Prędkość (liniowa) ───────────────────────────────────────────────
        float speedMultiplier = 1f + cfg.speedGrowthPerWave * waveNumber;
        speedMultiplier       = Mathf.Min(speedMultiplier, cfg.speedMaxMultiplier);
        result.finalSpeed     = data.moveSpeed * speedMultiplier * beaconSpeedMod;

        // ─── Pancerz (logarytmiczny + hard cap) ───────────────────────────────
        // Rośnie szybko na początku, zwalnia w późnej grze
        float armorGrowth  = cfg.armorGrowthRate * Mathf.Log(waveNumber + 1);
        result.finalArmor  = Mathf.Clamp(data.armor + armorGrowth, 0f, cfg.armorHardCap);

        // ─── Odporność Magiczna (logarytmiczna + hard cap) ───────────────────
        float mrGrowth          = cfg.magicResistGrowthRate * Mathf.Log(waveNumber + 1);
        result.finalMagicResist = Mathf.Clamp(data.magicResist + mrGrowth, 0f, cfg.magicResistHardCap);

        // ─── Unik (liniowy + hard cap) ────────────────────────────────────────
        float dodgeGrowth  = cfg.dodgeGrowthRate * Mathf.Log(waveNumber + 1);
        result.finalDodge  = Mathf.Clamp(data.dodgeChance + dodgeGrowth, 0f, cfg.dodgeHardCap);

        // ─── Losowanie Elity ──────────────────────────────────────────────────
        result.isElite = false;
        if (data.canBeElite)
        {
            float eliteChance = data.eliteSpawnChance * beaconEliteMod;
            result.isElite = Random.Range(0f, 100f) < eliteChance;
        }

        // Nakładamy elitarne mnożniki NA przeskalowane wartości
        if (result.isElite)
        {
            result.finalHp    *= data.eliteHpMultiplier;
            result.finalSpeed *= data.eliteSpeedMultiplier;
        }

        LogScaling(data.enemyName, waveNumber, result);
        return result;
    }

    private static void LogScaling(string name, int wave, ScaledEnemyStats s)
    {
        string eliteTag = s.isElite ? " <color=red>[ELITE]</color>" : "";
        Debug.Log(
            $"<color=grey>[Scaler] {name}{eliteTag} (fala {wave}): " +
            $"HP={s.finalHp:F0} | SPD={s.finalSpeed:F2} | " +
            $"ARM={s.finalArmor:F1}% | MR={s.finalMagicResist:F1}% | " +
            $"DODGE={s.finalDodge:F1}%</color>");
    }
}

/// <summary>
/// Dane wyjściowe EnemyStatScaler – przekazywane do EnemyStats.Initialize().
/// </summary>
public class ScaledEnemyStats
{
    public float finalHp;
    public float finalSpeed;
    public float finalArmor;
    public float finalMagicResist;
    public float finalDodge;
    public bool  isElite;
}