#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class FogOfWarPrefabBuilder
{
    private const string PrefabPath = "Assets/Main folder/Prefabs/Terrain/Fog of war.prefab";
    private const string HexMeshGuid = "f083fcd0a9a41284fbf3a9a02a88fedd";
    private const string FogMatGuid = "ecf964584f2921a42a52d9d4bc81e15f";

    [MenuItem("Tools/Map/Rebuild Fog of War Prefab (Radius 3)")]
    public static void RebuildFogPrefabRadius3()
    {
        RebuildFogPrefab(3);
    }

    [MenuItem("Tools/Map/Rebuild Fog of War Prefab (Radius 4)")]
    public static void RebuildFogPrefabRadius4()
    {
        RebuildFogPrefab(4);
    }

    public static void RebuildFogPrefab(int radius, float hexSize = 1.0f, float padding = 0.0f)
    {
        string meshPath = AssetDatabase.GUIDToAssetPath(HexMeshGuid);
        Mesh hexMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (hexMesh == null)
        {
            // Spróbuj pobrać wszystkie obiekty pod tą ścieżką (dla plików .obj submesh)
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(meshPath);
            foreach (var a in allAssets)
            {
                if (a is Mesh m)
                {
                    hexMesh = m;
                    break;
                }
            }
        }

        string matPath = AssetDatabase.GUIDToAssetPath(FogMatGuid);
        Material fogMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

        if (hexMesh == null)
        {
            Debug.LogError($"[FogOfWarPrefabBuilder] Nie znaleziono siatki heksa pod guid {HexMeshGuid}!");
            return;
        }

        GameObject root = new GameObject("Fog of war");
        root.layer = 7;

        int count = 0;
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Mathf.Max(-radius, -q - radius);
            int r2 = Mathf.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++)
            {
                Vector3 localPos = HexGridMath.AxialToWorld(q, r, hexSize, padding);

                GameObject fogHex = new GameObject("Fog");
                fogHex.layer = 7;
                fogHex.transform.SetParent(root.transform, false);
                fogHex.transform.localPosition = localPos;
                fogHex.transform.localRotation = Quaternion.identity;
                fogHex.transform.localScale = Vector3.one;

                MeshFilter mf = fogHex.AddComponent<MeshFilter>();
                mf.sharedMesh = hexMesh;

                MeshRenderer mr = fogHex.AddComponent<MeshRenderer>();
                mr.sharedMaterial = fogMat;

                MeshCollider mc = fogHex.AddComponent<MeshCollider>();
                mc.sharedMesh = hexMesh;
                mc.convex = true;

                count++;
            }
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=green>[FogOfWarPrefabBuilder] Prefab mgły został pomyślnie przebudowany dla promienia {radius} ({count} heksów).</color>");
    }
}
#endif
