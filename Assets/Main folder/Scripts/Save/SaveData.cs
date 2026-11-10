using System;
using System.Collections.Generic;

[Serializable]
public struct UpgradeInvestmentData
{
    public string upgradeId;
    public int investedHope;

    public UpgradeInvestmentData(string id, int hope)
    {
        upgradeId = id;
        investedHope = hope;
    }
}

[Serializable]
public class SaveData
{
    public int totalHope;
    // Zapisujemy tylko listę ID odblokowanych ulepszeń
    public List<string> unlockedUpgradeIDs = new List<string>();
    // Częściowe wpłaty Nadziei na ulepszenia (mechanika hold-to-invest)
    public List<UpgradeInvestmentData> upgradeInvestments = new List<UpgradeInvestmentData>();
    // Lista ID odblokowanych wież obronnych (np. "1. Sentinel Arbalest", "6. Royal Bombard")
    public List<string> unlockedTowerIDs = new List<string>();
    // Flaga ukończenia pierwszej rozgrywki / gry tutorialowej
    public bool isTutorialCompleted = false;

    public int GetInvestedHope(string id)
    {
        if (string.IsNullOrEmpty(id) || upgradeInvestments == null) return 0;
        for (int i = 0; i < upgradeInvestments.Count; i++)
        {
            if (upgradeInvestments[i].upgradeId == id)
                return upgradeInvestments[i].investedHope;
        }
        return 0;
    }

    public void SetInvestedHope(string id, int amount)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (upgradeInvestments == null) upgradeInvestments = new List<UpgradeInvestmentData>();
        for (int i = 0; i < upgradeInvestments.Count; i++)
        {
            if (upgradeInvestments[i].upgradeId == id)
            {
                upgradeInvestments[i] = new UpgradeInvestmentData(id, amount);
                return;
            }
        }
        upgradeInvestments.Add(new UpgradeInvestmentData(id, amount));
    }
}
