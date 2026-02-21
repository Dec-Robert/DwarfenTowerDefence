using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public float totalArtifacts;
    // Zapisujemy tylko listę ID odblokowanych ulepsze�
    public List<string> unlockedUpgradeIDs = new List<string>();
}