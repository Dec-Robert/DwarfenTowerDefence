using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewUpgrade", menuName = "City Builder/Building Upgrade")]
public class BuildingUpgradeSO : ScriptableObject
{
    [Header("Wizualia")]
    public string upgradeName;
    public Sprite icon;
    [TextArea] public string description; // Co to daje (opis dla gracza)

    [Header("Koszt Ulepszenia")]
    public List<BuildingData.ResourceCost> cost;

    [Header("Zmiany Statystyk (Opcjonalne)")]
    // Te listy bêd¹ modyfikowaæ bazowe statystyki budynku
    public List<BuildingData.ResourceCost> productionModifier; // np. Wood +5
    public List<BuildingData.ResourceCost> upkeepModifier;     // np. Gold +2

    [Header("Logika Specjalna (Opcjonalne)")]
    // Jeœli potrzebujesz skomplikowanej logiki (np. sadzenie lasu), 
    // mo¿emy tu dodaæ system efektów podobny do wie¿, ale na razie u¿yjemy ID lub nazwy
    public string specialEffectID;
}