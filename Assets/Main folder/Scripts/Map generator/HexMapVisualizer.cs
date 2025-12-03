using UnityEngine;
using System.Collections.Generic;

public class HexMapVisualizer : MonoBehaviour
{
    [Header("Prefaby Terenu")]
    public GameObject hexPrefab;
    public GameObject prefabForest;
    public GameObject prefabMountain;
    public GameObject prefabFertile;

    [Header("Materia³y Pod³o¿a")]
    public Material matGrass;
    public Material matPath;
    public Material matForest;
    public Material matMountain;
    public Material matHill;
    public Material matSinkhole;
    public Material matFertile;
    public Material matBase;
    public Material matBeacon;

    private Transform mapHolder;
    private MaterialPropertyBlock propBlock;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
    }

    // G³ówna metoda rysuj¹ca - wywo³ywana przez Generator
    public void VisualizeWorld(
        Dictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>> worldData,
        HashSet<Vector2Int> allValidChunks,
        int chunkRadius,
        float hexSize,
        float padding)
    {
        // Czyszczenie starej mapy
        if (mapHolder != null) DestroyImmediate(mapHolder.gameObject);
        mapHolder = new GameObject("World Map").transform;
        mapHolder.parent = transform.parent; // Podpinamy pod ten sam obiekt co generator

        foreach (var chunkCoord in allValidChunks)
        {
            Vector3 centerWorld = HexGridMath.GetChunkCenterWorld(chunkCoord, chunkRadius, hexSize, padding);
            GameObject chunkObj = new GameObject($"Chunk_{chunkCoord.x}_{chunkCoord.y}");
            chunkObj.transform.parent = mapHolder;
            chunkObj.transform.position = centerWorld;

            if (worldData.ContainsKey(chunkCoord))
            {
                foreach (var kvp in worldData[chunkCoord])
                {
                    Vector2Int local = kvp.Key;
                    HexCellData cellData = kvp.Value;

                    Vector3 basePos = centerWorld + HexGridMath.AxialToWorld(local.x, local.y, hexSize, padding);

                    // Modyfikacja wysokoœci
                    float heightOffset = 0f;
                    if (cellData.feature == HexFeatureType.Hill)
                        heightOffset = cellData.featureLevel * 0.1f;
                    else if (cellData.feature == HexFeatureType.Sinkhole)
                        heightOffset = cellData.featureLevel * 0.1f; // level ujemny

                    Vector3 finalPos = basePos + Vector3.up * heightOffset;

                    GameObject hex = Instantiate(hexPrefab, finalPos, Quaternion.identity, chunkObj.transform);
                    hex.name = $"Hex_{local.x}_{local.y}";

                    ApplyVisualsToHex(hex, cellData);
                }
            }
        }
    }

    void ApplyVisualsToHex(GameObject hexObj, HexCellData data)
    {
        Renderer r = hexObj.GetComponentInChildren<Renderer>();
        if (r == null) return;

        Material matToUse = matGrass;
        string nameSuffix = "";
        hexObj.transform.localScale = Vector3.one;

        if (data.isPath)
        {
            matToUse = matPath;
            nameSuffix = " (Path)";
        }
        else
        {
            switch (data.feature)
            {
                case HexFeatureType.Forest:
                    matToUse = matForest;
                    nameSuffix = " (Forest)";
                    if (prefabForest != null) SpawnProp(hexObj, prefabForest);
                    break;
                case HexFeatureType.Mountain:
                    matToUse = matMountain;
                    nameSuffix = " (Mountain)";
                    if (prefabMountain != null) SpawnProp(hexObj, prefabMountain);
                    break;
                case HexFeatureType.Hill:
                    matToUse = matHill;
                    nameSuffix = $" (Hill +{data.featureLevel})";
                    break;
                case HexFeatureType.Sinkhole:
                    matToUse = matSinkhole;
                    nameSuffix = $" (Sinkhole {data.featureLevel})";
                    break;
                case HexFeatureType.FertileSoil:
                    matToUse = matFertile;
                    nameSuffix = " (Fertile)";
                    if (prefabFertile != null) SpawnProp(hexObj, prefabFertile);
                    break;
                case HexFeatureType.Base:
                    matToUse = matBase;
                    hexObj.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                    nameSuffix = " [CAPITOL]";
                    break;
                case HexFeatureType.Beacon:
                    matToUse = matBeacon;
                    hexObj.transform.localScale = new Vector3(0.8f, 3f, 0.8f);
                    nameSuffix = " [BEACON]";
                    break;
            }
        }

        r.sharedMaterial = matToUse;
        hexObj.name += nameSuffix;

        // Opcjonalnie: Ustawienie koloru w PropertyBlock (jeœli potrzebujesz)
        // r.SetPropertyBlock(propBlock);
    }

    void SpawnProp(GameObject parentHex, GameObject prefab)
    {
        GameObject prop = Instantiate(prefab, parentHex.transform);
        prop.transform.localPosition = Vector3.zero;
        prop.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
    }

    public void ClearMap()
    {
        if (mapHolder != null) DestroyImmediate(mapHolder.gameObject);
        // Czyœci dzieci jeœli jakieœ zosta³y
        while (transform.childCount > 0) DestroyImmediate(transform.GetChild(0).gameObject);
    }
}