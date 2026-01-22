using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Wave_Day_X", menuName = "Tower Defense/Predefined Wave")]
public class WaveDefinition : ScriptableObject
{
    public int dayNumber;       // Dzieñ, w którym ta fala wyst¹pi
    public string waveMessage;  // Komunikat dla gracza (np. "Boss nadchodzi!")

    [System.Serializable]
    public struct WaveEntry
    {
        public EnemyData enemy; // Jaki wróg
        public int count;       // Ile sztuk
    }

    [Header("Sk³ad Fali")]
    // Lista grup, np.:
    // 1. 20 Szkieletów
    // 2. 1 Blob
    // 3. 10 Arcanistów
    public List<WaveEntry> enemies;
}