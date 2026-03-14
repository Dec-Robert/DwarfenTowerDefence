using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


[CustomEditor(typeof(HousingBuildingData))]
public class HousingBuildingDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Aktualizacja obiektu
        serializedObject.Update();

        // 1. Rysujemy nagłówek i podstawowe pola z BuildingData (Te które chcemy)
        EditorGUILayout.LabelField("Base Building Info", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("buildingName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));

        // Typ jest ustawiany automatycznie w OnValidate, wi�c mo�na go wy�wietli� jako ReadOnly lub ukry�
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
        // Bo domy ich nie u�ywaj�!

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("--- HOUSING SPECIFICS ---", EditorStyles.boldLabel);

        // 2. Rysujemy wszystkie pola specyficzne dla HousingBuildingData
        // Używamy iteratora, żeby nie wpisywać każdego ręcznie, 
        // zaczynając od pierwszego pola unikalnego dla tej klasy.

        var property = serializedObject.GetIterator();

        // Przeskakujemy do pierwszego pola (zawsze 'm_Script')
        property.NextVisible(true);

        while (property.NextVisible(false))
            // Rysujemy tylko te właściwości, które należą do HousingBuildingData, a nie do klasy bazowej
            // Sprawdzamy to po nazwach, których NIE chcemy, lub rysujemy wszystko co zosta�o
            // Najpro�ciej: Rysujemy konkretne pola, kt�re zdefiniowa�e� w HousingBuildingData
            if (property.name == "housingRace" ||
                property.name == "maxResidents" ||
                property.name == "initialResidents" ||
                property.name == "growthDifficultyMultiplier" ||
                property.name == "baseGrowthTicks" ||
                property.name == "productionPerResident" ||
                property.name == "baseDailyUpkeep" ||
                property.name == "upkeepPerResident" ||
                property.name == "growthSurplusCost")
                EditorGUILayout.PropertyField(property, true);

        // Zapisz zmiany
        serializedObject.ApplyModifiedProperties();
    }
}