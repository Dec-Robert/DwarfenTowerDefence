using UnityEngine;
using UnityEditor;
using System.Linq;

// Ten atrybut ³¹czy edytor ze skryptem TowerData
[CustomEditor(typeof(TowerData))]
public class TowerDataEditor : Editor
{
    // Lista nazw zmiennych z BuildingData, które chcesz ukryæ
    // Upewnij siê, ¿e wpisujesz tu dok³adne nazwy zmiennych (nie etykiet z Header!)
    private readonly string[] _fieldsToHide = new string[]
    {
        "productionPerCycle",     // Przyk³ad: zmienna odpowiadaj¹ca za "ProductionPerCycle"
        "terrainProductionRules", // Przyk³ad: zmienna od "Zasady produkcji terenowej"
        "resourceType",           // Inne pola z BuildingData do ukrycia
        "anotherUnwantedField"
    };

    public override void OnInspectorGUI()
    {
        // Pobiera aktualny obiekt
        serializedObject.Update();

        // Pobiera iterator po w³aœciwoœciach
        SerializedProperty prop = serializedObject.GetIterator();

        // Wchodzi w pierwszy element (zazwyczaj m_Script)
        if (prop.NextVisible(true))
        {
            do
            {
                // Zawsze rysuj pole "Script" (choæ zazwyczaj jest wyszarzone)
                if (prop.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(prop);
                    }
                    continue;
                }

                // SprawdŸ, czy nazwa pola znajduje siê na liœcie do ukrycia
                if (_fieldsToHide.Contains(prop.name))
                {
                    continue; // Pomiñ rysowanie tego pola
                }

                // Rysuj wszystkie pozosta³e pola normalnie
                EditorGUILayout.PropertyField(prop, true);

            } while (prop.NextVisible(false)); // Przechodzi do kolejnych pól na tym samym poziomie
        }

        // Zatwierdza zmiany
        serializedObject.ApplyModifiedProperties();
    }
}