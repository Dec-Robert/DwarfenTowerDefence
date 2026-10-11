#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TowerData))]
public class TowerDataEditor : Editor
{
    private readonly string[] _fieldsToHide =
    {
        "productionPerCycle",
        "terrainProductionRules",
        "resourceType",
        "bonusRule"
    };

    public override void OnInspectorGUI()
    {
        TowerData tData = (TowerData)target;
        if (tData != null)
        {
            tData.EnsureDefaultEfficiencyTable();
        }

        serializedObject.Update();

        SerializedProperty prop = serializedObject.GetIterator();

        if (prop.NextVisible(true))
        {
            do
            {
                if (prop.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(prop);
                    }
                    continue;
                }

                if (_fieldsToHide.Contains(prop.name))
                {
                    continue;
                }

                if (prop.name == "efficiencyTable")
                {
                    BuildingDataEditor.DrawEfficiencySummaryBox(tData);
                    EditorGUILayout.PropertyField(prop, new GUIContent("Tabela Wydajności (Surowe wartości)"), true);
                    continue;
                }

                EditorGUILayout.PropertyField(prop, true);
            }
            while (prop.NextVisible(false));
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif