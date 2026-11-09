using UnityEngine;

[System.Serializable]
public class RuneItem
{
    public string runeID;
    public RuneDefinitionSO definition; // Link do Scriptable Objectu z nazwą i ikoną

    public float primaryValue;

    public bool hasPenalty;
    public RuneStatType penaltyStat;
    public RuneValueType penaltyValueType;
    public float penaltyValue; // Wartość zawsze ujemna

    public RuneItem(RuneDefinitionSO definition, float primaryValue)
    {
        this.runeID = System.Guid.NewGuid().ToString();
        this.definition = definition;
        this.primaryValue = primaryValue;
        this.hasPenalty = false;
    }

    public void SetPenalty(RuneStatType pStat, RuneValueType pType, float pValue)
    {
        this.hasPenalty = true;
        this.penaltyStat = pStat;
        this.penaltyValueType = pType;
        this.penaltyValue = pValue; // Powinno być na minusie
    }

    public static bool IsPercentageStat(RuneStatType stat, RuneValueType valType)
    {
        if (valType == RuneValueType.Percent) return true;
        return stat == RuneStatType.CriticalChance
            || stat == RuneStatType.CriticalDamage
            || stat == RuneStatType.ArmorPenetration
            || stat == RuneStatType.MagicPenetration;
    }

    public static string GetStatDisplayName(RuneStatType stat)
    {
        string key = stat switch
        {
            RuneStatType.Range => "stat_rune_range",
            RuneStatType.Damage => "stat_rune_damage",
            RuneStatType.FireRate => "stat_rune_firerate",
            RuneStatType.ArmorPenetration => "stat_rune_armorpen",
            RuneStatType.MagicPenetration => "stat_rune_magicpen",
            RuneStatType.CriticalChance => "stat_rune_critchance",
            RuneStatType.CriticalDamage => "stat_rune_critdamage",
            _ => null
        };

        if (key != null && LocalizationManager.Instance != null)
        {
            return LocalizationManager.Get(key, stat.ToString());
        }

        return stat.ToString();
    }

    public string GetPrimaryText()
    {
        string sign = primaryValue > 0 ? "+" : "";
        bool isPct = IsPercentageStat(definition.primaryStat, definition.valueType);
        string symbol = isPct ? "%" : "";

        string valString = (isPct || primaryValue % 1f == 0f)
            ? primaryValue.ToString("0")
            : primaryValue.ToString("0.0");

        return $"{sign}{valString}{symbol} {GetStatDisplayName(definition.primaryStat)}";
    }

    public string GetPenaltyText()
    {
        if (!hasPenalty) return "";
        bool isPct = IsPercentageStat(penaltyStat, penaltyValueType);
        string symbol = isPct ? "%" : "";

        string valString = (isPct || penaltyValue % 1f == 0f)
            ? penaltyValue.ToString("0")
            : penaltyValue.ToString("0.0");

        return $"<color=red>{valString}{symbol} {GetStatDisplayName(penaltyStat)}</color>";
    }
}