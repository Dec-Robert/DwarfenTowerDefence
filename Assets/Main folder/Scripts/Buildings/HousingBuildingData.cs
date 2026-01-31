using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "NewHouse", menuName = "City Builder/Housing Data")]
public class HousingBuildingData : BuildingData
{
    [Header("Konfiguracja Rasy")]
    public Race housingRace;
    public int maxResidents = 5;
    public int initialResidents = 2;

    [Header("Formu³a Wzrostu")]
    public float growthDifficultyMultiplier = 2.0f;
    public int baseGrowthTicks = 1;

    [Header("Produkcja Pasywna (Na mieszkañca)")]
    public List<ResourceCost> productionPerResident;

    [Header("Koszty Utrzymania (Baza + Per Pop)")]
    public List<ResourceCost> baseDailyUpkeep;
    public List<PerCapitaCost> upkeepPerResident;

    [Header("Koszty Wzrostu (Nadmiarowe)")]
    public List<ResourceCost> growthSurplusCost;

    [System.Serializable]
    public struct PerCapitaCost
    {
        public ResourceType type;
        public float amount;
    }

    private void OnValidate()
    {
        type = BuildingType.Utility;
    }
}

// =========================================================
//            TUTAJ ZACZYNA SIÊ KOD EDYTORA
// =========================================================

#if UNITY_EDITOR

[CustomEditor(typeof(HousingBuildingData))]
public class HousingBuildingDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Aktualizacja obiektu
        serializedObject.Update();

        // 1. Rysujemy nag³ówek i podstawowe pola z BuildingData (Te które chcemy)
        EditorGUILayout.LabelField("Base Building Info", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("buildingName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));

        // Typ jest ustawiany automatycznie w OnValidate, wiêc mo¿na go wyœwietliæ jako ReadOnly lub ukryæ
        GUI.enabled = false;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("type"));
        GUI.enabled = true;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("allowedTerrain"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("constructionCost"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("prefab"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("tier1Upgrades"));

        // TU POMIJAMY POLA: 
        // - productionPerCycle
        // - upkeepPerCycle
        // - workerScalingFactor
        // - baseShifts
        // - baseWorkersPerShift
        // Bo domy ich nie u¿ywaj¹!

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("--- HOUSING SPECIFICS ---", EditorStyles.boldLabel);

        // 2. Rysujemy wszystkie pola specyficzne dla HousingBuildingData
        // U¿ywamy iteratora, ¿eby nie wpisywaæ ka¿dego rêcznie, 
        // zaczynaj¹c od pierwszego pola unikalnego dla tej klasy.

        var property = serializedObject.GetIterator();

        // Przeskakujemy do pierwszego pola (zawsze 'm_Script')
        property.NextVisible(true);

        while (property.NextVisible(false))
        {
            // Rysujemy tylko te w³aœciwoœci, które nale¿¹ do HousingBuildingData, a nie do klasy bazowej
            // Sprawdzamy to po nazwach, których NIE chcemy, lub rysujemy wszystko co zosta³o
            // Najproœciej: Rysujemy konkretne pola, które zdefiniowa³eœ w HousingBuildingData

            if (property.name == "housingRace" ||
                property.name == "maxResidents" ||
                property.name == "initialResidents" ||
                property.name == "growthDifficultyMultiplier" ||
                property.name == "baseGrowthTicks" ||
                property.name == "productionPerResident" ||
                property.name == "baseDailyUpkeep" ||
                property.name == "upkeepPerResident" ||
                property.name == "growthSurplusCost")
            {
                EditorGUILayout.PropertyField(property, true);
            }
        }

        // Zapisz zmiany
        serializedObject.ApplyModifiedProperties();
    }
}
#endif