#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BuildingData))]
public class BuildingDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        BuildingData bData = (BuildingData)target;
        if (bData != null && bData.type != BuildingType.Housing)
        {
            bData.EnsureDefaultEfficiencyTable();
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

                if (prop.name == "efficiencyTable")
                {
                    DrawEfficiencySummaryBox(bData);
                    EditorGUILayout.PropertyField(prop, new GUIContent("Tabela Wydajności (Surowe wartości)"), true);
                    continue;
                }

                EditorGUILayout.PropertyField(prop, true);
            }
            while (prop.NextVisible(false));
        }

        serializedObject.ApplyModifiedProperties();
    }

    public static void DrawEfficiencySummaryBox(BuildingData bData)
    {
        if (bData == null || bData.type == BuildingType.Housing) return;

        bData.EnsureDefaultEfficiencyTable();

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("📊 Tabela Wydajności Pracowników (Efficiency Table)", EditorStyles.boldLabel);

        if (bData.efficiencyTable != null && bData.efficiencyTable.Count > 0)
        {
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < bData.efficiencyTable.Count; i++)
            {
                float pct = Mathf.Round(bData.efficiencyTable[i] * 100f);
                bool isBase = i > 0 && i <= bData.baseMaxWorkers;
                Color prevBg = GUI.backgroundColor;
                GUI.backgroundColor = i == 0
                    ? new Color(0.95f, 0.55f, 0.45f)
                    : (isBase ? new Color(0.5f, 0.95f, 0.6f) : new Color(0.55f, 0.8f, 1f));

                GUILayout.Box($"{i}p:\n{pct:F0}%", GUILayout.MinWidth(42), GUILayout.Height(32));
                GUI.backgroundColor = prevBg;
            }
            EditorGUILayout.EndHorizontal();
        }

        Color prevBtn = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.3f, 0.65f, 1f);
        if (GUILayout.Button("🛠️ Otwórz w Efficiency Table Editor (Wykres + Symulator)", GUILayout.Height(26)))
        {
            BuildingEfficiencyEditorWindow.OpenForBuilding(bData);
        }
        GUI.backgroundColor = prevBtn;

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
    }
}
#endif
