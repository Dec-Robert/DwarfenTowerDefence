using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewHouse", menuName = "City Builder/Housing Data")]
public class HousingBuildingData : BuildingData
{
    [Header("Ustawienia Populacji")]
    public Race housingRace;
    public int maxResidents = 5;
    public int initialResidents = 2;
    public int daysPerGrowth = 1; // Co ile dni pojawia siê nowy obywatel

    [Header("Koszty Utrzymania (Dziennie)")]
    // Koszt sta³y (np. 1 Food)
    public List<ResourceCost> baseDailyUpkeep;

    // Koszt zmienny (np. 0.2 Food za ka¿dego mieszkañca)
    // U¿ywamy float dla precyzji, zdefiniujemy to w nowej strukturze pomocniczej tutaj
    [System.Serializable]
    public struct PerCapitaCost
    {
        public ResourceType type;
        public float amount; // np. 0.2
    }
    public List<PerCapitaCost> upkeepPerResident;

    private void OnValidate()
    {
        type = BuildingType.Utility; // Wymuszamy typ Utility
    }


}