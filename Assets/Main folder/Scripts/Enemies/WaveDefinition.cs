using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Wave_Day_X", menuName = "Tower Defense/Predefined Wave")]
public class WaveDefinition : ScriptableObject
{
    [Header("Konfiguracja Dnia i Komunikatu")]
    public int dayNumber = 1;
    public string waveMessage;

    [Header("Parametry Spawnu i Skalowania Fali")]
    [Tooltip("Jeśli <= 0, używany jest domyślny nightSpawnInterval w EnemySpawner")]
    public float customSpawnInterval = 0f;
    [Tooltip("Mnożnik HP wszystkich wrogów w tej fali (1.0 = bazowe HP)")]
    public float hpMultiplier = 1f;
    [Tooltip("Mnożnik prędkości ruchu wszystkich wrogów w tej fali (1.0 = bazowa prędkość)")]
    public float speedMultiplier = 1f;
    [Tooltip("Czy wymieszać kolejność spawnowania wrogów ze wszystkich grup")]
    public bool shuffleEnemies = false;
    [Tooltip("Czy pozwalać na losowe pojawianie się Elit zgodnie z szansą w EnemyData")]
    public bool allowRandomElites = false;

    [System.Serializable]
    public struct WaveEntry
    {
        public EnemyData enemy;
        public int count;
        [Tooltip("Wymusza rangę Elite dla całej tej grupy")]
        public bool forceElite;
    }

    [Header("Skład Fali")]
    public List<WaveEntry> enemies = new List<WaveEntry>();
}