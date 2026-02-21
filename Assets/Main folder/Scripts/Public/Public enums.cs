using UnityEngine;

public enum ResourceType
{
    /*
    Wszystkie dost�pne wykorzystania syrowc�w:

    Budowa wie�/budynk�w, 
    Ulepszenie budynk�w, 
    Odkrywanie terenu, 
    Utrzymanie budynk�w,
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
    Assigned,   //Przydzielony do pracy, czeka na rozpocz�cie pracy, mozna nim porusza� mi�dzy budynkami
    Working,    //Pracuje zablokowany do konca zmiany
    Exhausted   //Zm�czony po zmianie
}

public enum DayPhase
{
    Day,
    Night
}

public enum DamageType
{
    Physical, // Zwyk�e wie�e (�ucznik, Armata)
    Magic,    // Wie�e magiczne (L�d, Ogie�)
    True      // Typ obra�e� osi�galny tylko dzi�ki niekt�rym run�S
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

    [Tooltip("Zasi�g poszukiwania (1 = tylko s�siedzi)")]
    public int range;

    [Header("Matematyka")]
    public float baseBonusPerHex;   // np. 0.5
    public float penaltyPerUser;    // np. 0.2
    public float minBonus;          // np. 0.1

    // --- NOWE POLE ---
    [Tooltip("Jednorazowy bonus do produkcji, je�li budynek stoi BEZPO�REDNIO na tym terenie.")]
    public float onTopProductionBonus;
}