using UnityEngine;

public enum ResourceType
{
    /*
    Wszystkie dostêpne wykorzystania syrowców:

    Budowa wie¿/budynków, 
    Ulepszenie budynków, 
    Odkrywanie terenu, 
    Utrzymanie budynków,
    Meta-game upgrades
    */

    Gold,       
    Wood,       
    Stone,      
    Coal,       
    Food,       
    Population, 
    Artifacts,  
    Iron        
}

public enum Race
{
    Humans,
    Elves,
    Dwarves
}

public enum WorkState
{
    Idle,       //Nie pracuje   
    Assigned,   //Przydzielony do pracy, czeka na rozpoczêcie pracy, mozna nim poruszaæ miêdzy budynkami
    Working,    //Pracuje zablokowany do konca zmiany
    Exhausted   //Zmêczony po zmianie
}

public enum DayPhase
{
    Day,
    Night
}

public enum DamageType
{
    Physical, // Zwyk³e wie¿e (£ucznik, Armata)
    Magic,    // Wie¿e magiczne (Lód, Ogieñ)
    True      // Typ obra¿eñ osi¹galny tylko dziêki niektórym run¹S
}

public enum EnemyRank
{
    Normal,
    Elite,
    Boss
}

[System.Serializable]
public struct TerrainBonusRule
{
    [Tooltip("Jaki teren daje bonus? (np. Forest dla Tartaku)")]
    public HexFeatureType requiredFeature;

    [Tooltip("Zasiêg poszukiwania (1 = tylko s¹siedzi)")]
    public int range;

    [Header("Matematyka")]
    public float baseBonusPerHex;   // np. 0.5
    public float penaltyPerUser;    // np. 0.2
    public float minBonus;          // np. 0.1

    // --- NOWE POLE ---
    [Tooltip("Jednorazowy bonus do produkcji, jeœli budynek stoi BEZPOŒREDNIO na tym terenie.")]
    public float onTopProductionBonus;
}

public enum ChunkState
{
    Locked,       // Zablokowany, nie mo¿na nic robiæ, maj¹ to drogi i niodkryte jeszcze chunki
    Unlocked,     // Odblokowany, mo¿na wys³aæ zwiadowcê, ale nie mo¿na budowaæ
    Scouting,     // W trakcie odkrywania 
    MilitaryOnly, // Mo¿na wie¿e, nie mo¿na ekonomii (chyba ¿e minie czas)
    FullyUnlocked // Mo¿na wszystko
}

[System.Serializable]
public class ScoutingMission
{
    public Vector2Int targetChunk;
    public ExpeditionCenterEntity assignedCenter; // Sk¹d wyszed³ zwiadowca
    public int daysRemaining;
    public int totalDuration;
}

[System.Serializable]
public class ChunkStateData
{
    public ChunkState state;
    public int economyUnlockTimer; // Ile dni do samoistnego odblokowania (jeœli Posterunek nie jest wymagany, ale pisa³eœ o odczekaniu 1-7 dni)
}