#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ChunkLayoutMigrationTool
{
    private static int HexDistance(Vector2Int a, Vector2Int b)
    {
        return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.x + a.y - b.x - b.y) + Mathf.Abs(a.y - b.y)) / 2;
    }

    [MenuItem("Tools/Map/Migrate All ChunkLayoutSO to Radius 3")]
    public static void MigrateAllChunkLayoutsToRadius3()
    {
        string[] guids = AssetDatabase.FindAssets("t:ChunkLayoutSO");
        int totalPruned = 0;
        int filesModified = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ChunkLayoutSO layout = AssetDatabase.LoadAssetAtPath<ChunkLayoutSO>(path);
            if (layout == null) continue;

            bool isDirty = false;

            if (layout.hexes != null)
            {
                int before = layout.hexes.Count;
                layout.hexes.RemoveAll(h => HexDistance(Vector2Int.zero, h.localCoord) > 3);
                int removed = before - layout.hexes.Count;
                if (removed > 0)
                {
                    totalPruned += removed;
                    isDirty = true;
                }
            }

            if (HexDistance(Vector2Int.zero, layout.roadConnectionEdge) > 3)
            {
                layout.roadConnectionEdge = new Vector2Int(3, -2);
                isDirty = true;
            }

            if (isDirty)
            {
                EditorUtility.SetDirty(layout);
                filesModified++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=green>[ChunkLayoutMigrationTool] Zakończono migrację: zmodyfikowano {filesModified} plików, usunięto {totalPruned} heksów poza promieniem 3.</color>");
    }
}
#endif
