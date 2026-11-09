using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewRuneDef", menuName = "Game/Runes/Rune Definition")]
public class RuneDefinitionSO : ScriptableObject
{
    [Header("Główne Informacje")]
    public string runeName;
    public Sprite icon;
    public RuneRarity rarity;

    public string DisplayName => LocalizationManager.Get($"RUNE_{GetLocalizationId()}_NAME", runeName);

    public string GetLocalizationId()
    {
        string raw = !string.IsNullOrEmpty(runeName) ? runeName : name;
        var sb = new System.Text.StringBuilder();
        foreach (char c in raw)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
            else if (c == ' ' || c == '_' || c == '-') sb.Append('_');
        }
        string clean = sb.ToString();
        while (clean.Contains("__")) clean = clean.Replace("__", "_");
        return clean.Trim('_');
    }

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
