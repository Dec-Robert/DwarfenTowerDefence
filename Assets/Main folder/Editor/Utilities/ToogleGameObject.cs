using UnityEditor;
using UnityEngine;

public class ToggleGameObject
{
    [MenuItem("Edit/Toggle Active State %q", false, 0)]
    static void ToggleActiveState()
        {
        foreach (GameObject obj in Selection.gameObjects)
        {
            Undo.RecordObject(obj, "Toggle Active State");
            obj.SetActive(!obj.activeSelf);
        }
    }
}