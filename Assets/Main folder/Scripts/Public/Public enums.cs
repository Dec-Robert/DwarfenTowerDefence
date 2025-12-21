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