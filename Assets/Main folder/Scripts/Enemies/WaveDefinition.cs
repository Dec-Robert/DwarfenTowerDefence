using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Wave_Day_X", menuName = "Tower Defense/Predefined Wave")]
public class WaveDefinition : ScriptableObject
{
    public int dayNumber;       // Dzie�, w kt�rym ta fala wyst�pi
    public string waveMessage;  // Komunikat dla gracza (np. "Boss nadchodzi!")

    [System.Serializable]
    public struct WaveEntry
    {
        public EnemyData enemy; // Jaki wr�g
        public int count;       // Ile sztuk
    }

    [Header("Sk�ad Fali")]
    // Lista grup, np.:
    // 1. 20 Szkieletów
    // 2. 1 Blob
    // 3. 10 Arcanist�w
    public List<WaveEntry> enemies;
}