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

    public string GetPrimaryText()
    {
        string sign = primaryValue > 0 ? "+" : "";
        string symbol = definition.valueType == RuneValueType.Percent ? "%" : "";
        return $"{sign}{primaryValue}{symbol} {definition.primaryStat}";
    }

    public string GetPenaltyText()
    {
        if (!hasPenalty) return "";
        string symbol = penaltyValueType == RuneValueType.Percent ? "%" : "";
        return $"<color=red>{penaltyValue}{symbol} {penaltyStat}</color>";
    }
}