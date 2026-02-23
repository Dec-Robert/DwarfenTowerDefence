using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewRuneDef", menuName = "Tower Defense/Rune Definition")]
public class RuneDefinitionSO : ScriptableObject
{
    [Header("Główne Informacje")]
    public string runeName;
    public Sprite icon;
    public RuneRarity rarity;

    [Header("Główna Statystyka (Buff)")]
    public RuneStatType primaryStat;
    public RuneValueType valueType;
    [Tooltip("Zakres losowania wartości. X = Min, Y = Max.")]
    public Vector2 primaryValueRange;

    [Header("Kary (Tylko dla Przeklętych Run)")]
    [Tooltip("Jeśli rzadkość to Cursed, system wylosuje jedną z poniższych kar.")]
    public List<CursedPenalty> possiblePenalties = new List<CursedPenalty>();

    [System.Serializable]
    public struct CursedPenalty
    {
        public RuneStatType stat;
        public RuneValueType valueType;
        [Tooltip("Wpisuj jako wartości DODATNIE (np. 10 - 30). System sam zamieni je na ujemne.")]
        public Vector2 penaltyRange;
    }
}