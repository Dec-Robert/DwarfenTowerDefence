using UnityEngine;
using System.Collections.Generic;

public class HexMapVisualizer : MonoBehaviour
{
    [Header("Prefaby Terenu")]
    public GameObject hexPrefab;
    public GameObject prefabForest;
    public GameObject prefabMountain;
    public GameObject prefabFertile;

    [Header("Materia³y Specjalne")]
    public Material matPath;
    public Material matForest;
    public Material matMountain;
    public Material matHill;
    public Material matSinkhole;
    public Material matFertile;
    public Material matBase;
    public Material matBeacon;

    [Header("Materia³ Domyœlny")]
    public Material matDefaultGrass;

    private Transform mapHolder;

    // NOWOŒÆ: S³ownik przechowuj¹cy fizyczne obiekty chunków
    private Dictionary<Vector2Int, GameObject> chunkGameObjects = new Dictionary<Vector2Int, GameObject>();

    // API dla Fog Managera
    public GameObject GetChunkGameObject(Vector2Int coord)
    {
        if (chunkGameObjects.ContainsKey(coord)) return chunkGameObjects[coord];
        return null;
    }

    public void VisualizeWorld(
        Dictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>> worldData,
        HashSet<Vector2Int> allValidChunks,
        Dictionary<Vector2Int, BiomeType> chunkBiomes,
        Dictionary<BiomeType, Material> biomeMaterials,
        int chunkRadius,
        float hexSize,
        float padding)
    {
        ClearMap(); // Czyœci te¿ s³ownik

        if (mapHolder == null)
        {
            mapHolder = new GameObject("World Map").transform;
            mapHolder.parent = transform.parent;
        }

        foreach (var chunkCoord in allValidChunks)
        {
            Vector3 centerWorld = HexGridMath.GetChunkCenterWorld(chunkCoord, chunkRadius, hexSize, padding);

            string biomeName = "Unknown";
            Material chunkBaseMat = matDefaultGrass;

            if (chunkBiomes != null && chunkBiomes.ContainsKey(chunkCoord))
            {
                BiomeType biome = chunkBiomes[chunkCoord];
                biomeName = biome.ToString();
                if (biomeMaterials != null && biomeMaterials.ContainsKey(biome))
                {
                    chunkBaseMat = biomeMaterials[biome];
                }
            }

            GameObject chunkObj = new GameObject($"Chunk_{chunkCoord.x}_{chunkCoord.y}_{biomeName}");
            chunkObj.transform.parent = mapHolder;
            chunkObj.transform.position = centerWorld;

            // NOWOŒÆ: Rejestrujemy chunk w s³owniku
            chunkGameObjects.Add(chunkCoord, chunkObj);

            if (worldData.ContainsKey(chunkCoord))
            {
                foreach (var kvp in worldData[chunkCoord])
                {
                    Vector2Int local = kvp.Key;
                    HexCellData cellData = kvp.Value;

                    Vector3 basePos = centerWorld + HexGridMath.AxialToWorld(local.x, local.y, hexSize, padding);

                    float heightOffset = 0f;
                    if (cellData.feature == HexFeatureType.Hill) heightOffset = cellData.featureLevel * 0.1f;
                    else if (cellData.feature == HexFeatureType.Sinkhole) heightOffset = cellData.featureLevel * 0.1f;

                    Vector3 finalPos = basePos + Vector3.up * heightOffset;

                    GameObject hex = Instantiate(hexPrefab, finalPos, Quaternion.identity, chunkObj.transform);
                    hex.name = $"Hex_{local.x}_{local.y}";

                    // Dodajemy komponent logiczny
                    HexCell cellComponent = hex.AddComponent<HexCell>();
                    cellComponent.chunkCoord = chunkCoord;
                    cellComponent.localCoord = local;

                    ApplyVisualsToHex(hex, cellData, chunkBaseMat);
                }
            }
        }
    }

    void ApplyVisualsToHex(GameObject hexObj, HexCellData data, Material groundMat)
    {
        Renderer r = hexObj.GetComponentInChildren<Renderer>();
        if (r == null) return;

        Material matToUse = groundMat;
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
    }

    void SpawnProp(GameObject parentHex, GameObject prefab)
    {
        GameObject prop = Instantiate(prefab, parentHex.transform);
        prop.transform.localPosition = Vector3.zero;
        prop.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
    }

    public void ClearMap()
    {
        chunkGameObjects.Clear(); // Czyœcimy s³ownik
        if (mapHolder != null) DestroyImmediate(mapHolder.gameObject);
        while (transform.childCount > 0) DestroyImmediate(transform.GetChild(0).gameObject);
    }
}