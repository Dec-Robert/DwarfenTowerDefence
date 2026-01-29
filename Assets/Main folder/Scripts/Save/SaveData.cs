using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public int totalArtifacts;
    // Zapisujemy tylko listê ID odblokowanych ulepszeñ
    public List<string> unlockedUpgradeIDs = new List<string>();
}