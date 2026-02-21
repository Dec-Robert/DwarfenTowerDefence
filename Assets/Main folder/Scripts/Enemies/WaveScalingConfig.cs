using UnityEngine;

/// <summary>
/// Centralny ScriptableObject konfiguracji systemu zagrożeń.
/// Wszystkie "magiczne liczby" są tutaj – zero hardkodowania w logice.
/// Twórz przez: Tower Defense → Wave Scaling Config
/// </summary>
[CreateAssetMenu(fileName = "WaveScalingConfig", menuName = "Tower Defense/Wave Scaling Config")]
public class WaveScalingConfig : ScriptableObject
{
    // =========================================================================
    // ERA DOSTĘPNOŚCI (Dynamic Palette System)
    // =========================================================================
    [Header("── Era Dostępności ──────────────────────")]
    [Tooltip("Fale 1-X używają tylko pierwszych N wrogów z listy")]
    public int era1MaxWave   = 20;  public int era1EnemyCount = 5;
    public int era2MaxWave   = 40;  public int era2EnemyCount = 10;
    public int era3MaxWave   = 55;  public int era3EnemyCount = 15;
    public int era4MaxWave   = 70;  public int era4EnemyCount = 20;
    // Era 5 (71+): Wszystkie dostępne typy

    // =========================================================================
    // PALETA OPERACYJNA
    // =========================================================================
    [Header("── Paleta Operacyjna ───────────────────")]
    [Tooltip("Co ile fal generowana jest nowa paleta")]
    public int blockSize           = 5;
    [Tooltip("Ilu głównych typów wrogów w palecie")]
    public int paletteMainCount    = 5;
    [Tooltip("Ilu pomocniczych typów wrogów w palecie")]
    public int paletteSupportCount = 3;
    [Tooltip("Szansa (0-1) że w fali pojawi się typ pomocniczy")]
    [Range(0f, 1f)] public float supportSpawnChance = 0.25f;

    // =========================================================================
    // BUDŻET ZAGROŻENIA (Seismic Calculator)
    // =========================================================================
    [Header("── Budżet Zagrożenia ───────────────────")]
    [Tooltip("WTB = (BaseWaveValue * WaveNumber) * DifficultyMultiplier")]
    public float baseWaveValue     = 3f;

    [Header("Multiplier trudności")]
    public float diffStandard      = 1.0f;
    public float diffSpike         = 2.5f;   // Co 15 fal
    public float diffCooldown      = 0.8f;   // Fala bezpośrednio po spike

    [Header("Fazy wstępne (fale 1-20 = łagodne)")]
    [Tooltip("Mnożnik budżetu dla fal 1-10 (ostrzegawcze)")]
    public float earlyPhaseMultiplier = 0.35f;
    [Tooltip("Mnożnik budżetu dla fal 11-20 (narastanie)")]
    public float rampPhaseMultiplier  = 0.65f;
    [Tooltip("Powyżej której fali zaczyna się 'normalna' trudność")]
    public int normalPhaseStartWave   = 21;

    [Tooltip("Co ile fal następuje Spike")]
    public int spikeInterval          = 15;

    // =========================================================================
    // SKALOWANIE STATYSTYK (Runtime Mutation)
    // =========================================================================
    [Header("── Skalowanie HP ────────────────────────")]
    [Tooltip("FinalHP = baseHp * (hpExponentialBase ^ WaveNumber)")]
    public float hpExponentialBase    = 1.04f;  // +4% per fala
    [Tooltip("Maksymalny mnożnik HP (żeby nie wyjść poza int)")]
    public float hpMaxMultiplier      = 500f;

    [Header("── Skalowanie Pancerza ──────────────────")]
    [Tooltip("Armor rośnie logarytmicznie: baseArmor + armorGrowthRate * log(wave)")]
    public float armorGrowthRate      = 12f;
    [Tooltip("Twardy limit pancerza (%)")]
    [Range(0, 100)] public float armorHardCap = 85f;

    [Header("── Skalowanie Odporności Magicznej ──────")]
    public float magicResistGrowthRate = 10f;
    [Range(0, 100)] public float magicResistHardCap = 85f;

    [Header("── Skalowanie Uniku ──────────────────────")]
    public float dodgeGrowthRate      = 5f;
    [Tooltip("Twardy limit uniku (%) – by uniknąć 'nietykalnych' wrogów")]
    [Range(0, 100)] public float dodgeHardCap = 65f;

    [Header("── Skalowanie Prędkości ───────────────────")]
    [Tooltip("Prędkość rośnie liniowo co X fal")]
    public float speedGrowthPerWave   = 0.02f;  // +2% per fala
    [Tooltip("Maksymalny mnożnik prędkości")]
    public float speedMaxMultiplier   = 3.0f;

    // =========================================================================
    // ECHO BŁĘDÓW (Adaptive Feedback)
    // =========================================================================
    [Header("── System Echo ──────────────────────────")]
    [Tooltip("Jeśli >X% wrogów danego typu dotarło do bazy, flaguj jako zagrożenie")]
    [Range(0f, 1f)] public float echoBreachThreshold = 0.25f;   // 25%
    [Tooltip("Po ilu falach system odpowiada na zagrożenie")]
    public int echoResponseDelay      = 4;
    [Tooltip("Jaki % budżetu fali N+4 jest zarezerwowany dla zagrożonego typu")]
    [Range(0f, 1f)] public float echoPriorityBudgetShare = 0.35f; // 35%
    [Tooltip("Jak długo typ pozostaje 'zagrożeniem' (w falach)")]
    public int echoThreatDuration     = 8;
}