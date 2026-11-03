using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public int totalHope;
    // Zapisujemy tylko listę ID odblokowanych ulepszeń
    public List<string> unlockedUpgradeIDs = new List<string>();
    // Lista ID odblokowanych wież obronnych (np. "1. Sentinel Arbalest", "6. Royal Bombard")
    public List<string> unlockedTowerIDs = new List<string>();
    // Flaga ukończenia pierwszej rozgrywki / gry tutorialowej
    public bool isTutorialCompleted = false;
}
