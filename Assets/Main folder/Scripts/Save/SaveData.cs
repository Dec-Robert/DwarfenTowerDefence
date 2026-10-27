using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public int totalHope;
    // Zapisujemy tylko listę ID odblokowanych ulepsze�
    public List<string> unlockedUpgradeIDs = new List<string>();
    // Lista ID odblokowanych wież obronnych (np. "1. Sentinel Arbalest", "6. Royal Bombard")
    public List<string> unlockedTowerIDs = new List<string>();
}