using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public int totalHope;
    // Zapisujemy tylko listę ID odblokowanych ulepsze�
    public List<string> unlockedUpgradeIDs = new List<string>();
}