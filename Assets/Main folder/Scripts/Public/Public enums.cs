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
