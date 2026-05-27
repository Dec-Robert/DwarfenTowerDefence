#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;
using UnityEngine.UI;
using TMPro;

public class CanvasUIDumper : EditorWindow
{
    [MenuItem("Tools/Debug/Eksportuj UI (Tylko Canvas_MAIN_HUD)")]
    public static void DumpCanvas()
    {
        // Szukamy konkretnego obiektu na scenie
        GameObject rootCanvas = GameObject.Find("Canvas_MAIN_HUD");
        
        if (rootCanvas == null)
        {
            Debug.LogError("Nie znaleziono obiektu 'Canvas_MAIN_HUD' na scenie! Sprawdź nazwę.");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=========================================");
        sb.AppendLine($"EKSPORT STRUKTURY UI: {rootCanvas.name}");
        sb.AppendLine("=========================================\n");

        // Odpalamy rekurencję tylko dla Canvasa
        DumpGameObject(rootCanvas, sb, "");

        string path = Path.Combine(Application.dataPath, "CanvasUI_Dump.txt");
        File.WriteAllText(path, sb.ToString());
        
        Debug.Log($"<color=green>Pomyślnie wyeksportowano UI do: {path}</color>");
        EditorUtility.RevealInFinder(path);
    }

    private static void DumpGameObject(GameObject go, StringBuilder sb, string indent)
    {
        string activeTag = go.activeSelf ? "" : " (WYŁ)";
        sb.AppendLine($"{indent}├─ 🔲 {go.name}{activeTag}");

        Component[] components = go.GetComponents<Component>();
        string compIndent = indent + "│    ";

        foreach (var c in components)
        {
            if (c == null) continue;
            string typeName = c.GetType().Name;
            
            // Pomijamy transformy i renderery, zostawiamy tylko to co ważne w UI
            if (typeName == "CanvasRenderer" || typeName == "Transform") continue;

            string details = GetUIParameters(c);
            sb.AppendLine($"{compIndent}└─ [{typeName}] {details}");
        }

        string childIndent = indent + "│  ";
        for (int i = 0; i < go.transform.childCount; i++)
        {
            DumpGameObject(go.transform.GetChild(i).gameObject, sb, childIndent);
        }
    }

    private static string GetUIParameters(Component c)
    {
        // 1. ZAAWANSOWANE CZYTANIE KONKRETNYCH KOMPONENTÓW UI
        if (c is RectTransform rt)
        {
            return $"Anchors(Min:{rt.anchorMin}, Max:{rt.anchorMax}) | Pivot:{rt.pivot} | Size:{rt.sizeDelta} | Pos:{rt.anchoredPosition}";
        }
        else if (c is HorizontalOrVerticalLayoutGroup layout)
        {
            return $"Spacing: {layout.spacing} | ChildControlSize(W:{layout.childControlWidth}, H:{layout.childControlHeight}) | ForceExpand(W:{layout.childForceExpandWidth}, H:{layout.childForceExpandHeight}) | Align: {layout.childAlignment}";
        }
        else if (c is ContentSizeFitter csf)
        {
            return $"H_Fit: {csf.horizontalFit} | V_Fit: {csf.verticalFit}";
        }
        else if (c is Image img)
        {
            return $"Color: {img.color} | RaycastTarget: {img.raycastTarget} | Sprite: {(img.sprite ? img.sprite.name : "None")}";
        }
        else if (c is TextMeshProUGUI tmp)
        {
            string txt = tmp.text.Length > 20 ? tmp.text.Substring(0, 20) + "..." : tmp.text;
            txt = txt.Replace("\n", " ");
            return $"Text: \"{txt}\" | Size: {tmp.fontSize} | Align: {tmp.alignment} | RaycastTarget: {tmp.raycastTarget}";
        }
        else if (c is Button btn)
        {
            return $"Interactable: {btn.interactable}";
        }

        // 2. FALLBACK: Czytanie innych skryptów (np. Twoich własnych)
        StringBuilder pb = new StringBuilder();
        SerializedObject so = new SerializedObject(c);
        SerializedProperty prop = so.GetIterator();
        bool enterChildren = true;
        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (prop.name == "m_Script" || prop.name == "m_ObjectHideFlags") continue;
            
            pb.Append($"{prop.name}: {GetPropValue(prop)} | ");
        }

        string res = pb.ToString();
        return res.EndsWith(" | ") ? res.Substring(0, res.Length - 3) : res;
    }

    private static string GetPropValue(SerializedProperty prop)
    {
        try
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue.ToString();
                case SerializedPropertyType.Boolean: return prop.boolValue.ToString();
                case SerializedPropertyType.Float: return prop.floatValue.ToString("F1");
                case SerializedPropertyType.String: return $"\"{prop.stringValue}\"";
                case SerializedPropertyType.Color: return "Color";
                case SerializedPropertyType.ObjectReference: return prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "null";
                case SerializedPropertyType.Enum: return prop.enumNames.Length > prop.enumValueIndex && prop.enumValueIndex >= 0 ? prop.enumNames[prop.enumValueIndex] : prop.enumValueIndex.ToString();
                case SerializedPropertyType.Vector2: return prop.vector2Value.ToString();
                default: return "?";
            }
        }
        catch { return "Err"; }
    }
}
#endif