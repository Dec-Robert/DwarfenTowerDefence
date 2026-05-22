#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;
using UnityEngine.SceneManagement;

public class AdvancedSceneDumper : EditorWindow
{
    [MenuItem("Tools/Debug/Zaawansowany Eksport Sceny (z Parametrami)")]
    public static void DumpScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        StringBuilder sb = new StringBuilder();
        
        sb.AppendLine("=========================================");
        sb.AppendLine($"SCENA: '{activeScene.name}'");
        sb.AppendLine("LEGENDA:");
        sb.AppendLine("[P] - Obiekt jest Prefabem");
        sb.AppendLine("(WYŁ) - Obiekt jest wyłączony (SetActive(false))");
        sb.AppendLine("=========================================\n");

        // Pobieramy wszystkie główne obiekty na scenie
        foreach (GameObject go in activeScene.GetRootGameObjects())
        {
            DumpGameObject(go, sb, "");
        }

        // Zapis do pliku
        string path = Path.Combine(Application.dataPath, "AdvancedSceneDump.txt");
        File.WriteAllText(path, sb.ToString());
        
        Debug.Log($"<color=green>Pomyślnie wyeksportowano strukturę sceny do: {path}</color>");
        
        // Magiczna funkcja Unity - otwiera folder z plikiem tekstowym
        EditorUtility.RevealInFinder(path); 
    }

    private static void DumpGameObject(GameObject go, StringBuilder sb, string indent)
    {
        string activeTag = go.activeSelf ? "" : " (WYŁ)";
        string prefabTag = PrefabUtility.IsPartOfAnyPrefab(go) ? "[P] " : "";
        
        // Wypisujemy nazwę obiektu
        sb.AppendLine($"{indent}├─ {prefabTag}{go.name}{activeTag}");

        // Pobieramy komponenty
        Component[] components = go.GetComponents<Component>();
        string compIndent = indent + "│    "; // Wcięcie dla komponentów
        
        foreach (var c in components)
        {
            if (c == null)
            {
                sb.AppendLine($"{compIndent}[MISSING_SCRIPT]");
                continue;
            }
            
            string typeName = c.GetType().Name;
            
            // Pomijamy nudne komponenty, żeby nie zaśmiecać pliku
            if (typeName == "CanvasRenderer") continue; 

            // Pobieramy parametry
            string parameters = GetParameters(c);
            sb.AppendLine($"{compIndent}[{typeName}] {parameters}");
        }

        // Rekurencja dla wszystkich dzieci tego obiektu
        string childIndent = indent + "│  ";
        for (int i = 0; i < go.transform.childCount; i++)
        {
            DumpGameObject(go.transform.GetChild(i).gameObject, sb, childIndent);
        }
    }

    // Funkcja odczytująca parametry dokładnie tak, jak widać je w Inspektorze
    private static string GetParameters(Component c)
    {
        StringBuilder paramBuilder = new StringBuilder();
        SerializedObject so = new SerializedObject(c);
        SerializedProperty prop = so.GetIterator();
        
        bool enterChildren = true;

        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false; // Pobieramy tylko właściwości najwyższego poziomu
            
            // Pomijamy referencję do samego skryptu
            if (prop.name == "m_Script") continue; 

            string val = GetPropertyValueAsString(prop);
            paramBuilder.Append($"{prop.name}: {val} | ");
        }

        string result = paramBuilder.ToString();
        // Usuwamy ostatnie " | "
        if (result.EndsWith(" | ")) result = result.Substring(0, result.Length - 3);
        
        return result;
    }

    // Zamienia wartości z Inspektora na czytelny tekst
    private static string GetPropertyValueAsString(SerializedProperty prop)
    {
        try
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue.ToString();
                case SerializedPropertyType.Boolean: return prop.boolValue.ToString();
                case SerializedPropertyType.Float: return prop.floatValue.ToString("F2");
                case SerializedPropertyType.String: return $"\"{prop.stringValue}\"";
                case SerializedPropertyType.Color: return $"R:{prop.colorValue.r:F1} G:{prop.colorValue.g:F1} B:{prop.colorValue.b:F1} A:{prop.colorValue.a:F1}";
                case SerializedPropertyType.ObjectReference: return prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "null";
                case SerializedPropertyType.Enum: return prop.enumNames.Length > prop.enumValueIndex && prop.enumValueIndex >= 0 ? prop.enumNames[prop.enumValueIndex] : prop.enumValueIndex.ToString();
                case SerializedPropertyType.Vector2: return prop.vector2Value.ToString();
                case SerializedPropertyType.Vector3: return prop.vector3Value.ToString();
                case SerializedPropertyType.Rect: return prop.rectValue.ToString();
                case SerializedPropertyType.ArraySize: return $"Size: {prop.intValue}";
                default: return $"[{prop.propertyType}]";
            }
        }
        catch
        {
            return "[Błąd Odczytu]";
        }
    }
}
#endif