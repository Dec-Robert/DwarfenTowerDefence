using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CityBaseConfig", menuName = "Tower Defense/City Base Config")]
public class CityBaseConfigSO : ScriptableObject
{
    [Header("── Surowce Startowe Bazy ──")]
    public float startGold = 100f;
    public float startWood = 50f;
    public float startStone = 0f;
    public float startIron = 0f;
    public float startCoal = 0f;
    public float startFood = 50f;

    [Header("── Parametry Osady ──")]
    [Tooltip("Początkowe HP Głównego Budynku (Kapitolu/Głównej Bramy)")]
    public int baseCityHP = 20;

    [Header("── Parametry Domów (Nadpisywane) ──")]
    [Tooltip("Jeśli zaznaczone, poniższe wartości nadpiszą te ustawione indywidualnie w HousingBuildingData!")]
    public bool overrideHousingData = true;
    
    public int humanStartPop = 2;
    public int humanMaxPop = 5;

    public int elfStartPop = 1;
    public int elfMaxPop = 4;

    public int dwarfStartPop = 1;
    public int dwarfMaxPop = 4;
}