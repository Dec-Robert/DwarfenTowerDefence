using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Wave_Day_X", menuName = "Game/Enemies & Waves/Predefined Wave")]
public class WaveDefinition : ScriptableObject
{
    [Header("Konfiguracja Dnia i Komunikatu")]
    public int dayNumber = 1;
    public string waveMessage;

    [Header("Ekonomia Fali (Ether)")]
    [Tooltip("Maksymalna pula eteru do zdobycia ze zwykłych wrogów w tej fali (np. Fala 1 = 3, Fala 2 = 4, Fala 3 = 6)")]
    public int etherPool = 3;

    [Header("Parametry Spawnu i Skalowania Fali")]
    [Tooltip("Jeśli <= 0, używany jest domyślny nightSpawnInterval w EnemySpawner")]
    public float customSpawnInterval = 0f;

    [Tooltip("Legacy: w zdefiniowanych falach statystyki wynikają z wybranego Tieru. Fale po ostatniej zdefiniowanej używają skalowania 1.1^a")]
    public float hpMultiplier = 1f;

    [Tooltip("Legacy: w zdefiniowanych falach statystyki wynikają z wybranego Tieru")]
    public float speedMultiplier = 1f;

    [Tooltip("Czy wymieszać kolejność spawnowania wrogów ze wszystkich grup")]
    public bool shuffleEnemies = false;

    [Tooltip("Czy pozwalać na losowe pojawianie się Elit zgodnie z szansą w EnemyData")]
    public bool allowRandomElites = false;

    [System.Serializable]
    public struct WaveEntry
    {
        public EnemyData enemy;

        [Range(1, 4)]
        [Tooltip("Tier wroga (1 - 4) określający jego statystyki, prefab i ikonkę")]
        public int tier;

        public int count;

        [Tooltip("Wymusza rangę Elite dla całej tej grupy")]
        public bool forceElite;

        /// <summary>
        /// Zwraca poprawny numer tieru (1 - 4), domyślnie 1 gdy pole nie zostało zainicjalizowane.
        /// </summary>
        public int Tier => tier > 0 ? Mathf.Clamp(tier, 1, 4) : 1;
    }

    [Header("Skład Fali")]
    public List<WaveEntry> enemies = new List<WaveEntry>();
}
