using UnityEngine;
using System.Collections.Generic;

public class HexMapVisualizer : MonoBehaviour
{
    public static HexMapVisualizer Instance { get; private set; }

    [Header("Prefaby Terenu")]
    public GameObject hexPrefab;
    public GameObject prefabForest;
    public GameObject prefabMountain;
    public GameObject prefabFertile;
    public GameObject prefabMeadow;

    // --- NOWO��: Prefaby Specjalne ---
    [Header("Prefaby Budynk�w Specjalnych")]
    public GameObject prefabBase;   // Kapitol
    public GameObject prefabBeacon; // Beacon of Hope

    [Header("Materia�y Specjalne")]
    public Material matPath;
    public Material matForest;
    public Material matMountain;
    public Material matHill;
    public Material matSinkhole;
    public Material matFertile;
    public Material matMeadow;
    public Material matBase;
    public Material matBeacon;

    [Header("Materiał Domyślny")]
    public Material matDefaultGrass;

    private Transform mapHolder;

    // S�ownik przechowuj�cy fizyczne obiekty chunk�w
    private Dictionary<Vector2Int, GameObject> chunkGameObjects = new Dictionary<Vector2Int, GameObject>();

    private Dictionary<Vector2Int, Dictionary<Vector2Int, HexCell>> visualHexGrid = new Dictionary<Vector2Int, Dictionary<Vector2Int, HexCell>>();


    private void Awake()
    {
        Instance = this;
    }
    public GameObject GetChunkGameObject(Vector2Int coord)
    {
        if (chunkGameObjects.ContainsKey(coord)) return chunkGameObjects[coord];
        return null;
    }

    public void VisualizeWorld(
            Dictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>> worldData,
            HashSet<Vector2Int> allValidChunks,
            int chunkRadius,
            float hexSize,
            float padding)
    {
        ClearMap(); // Czy�ci stare obiekty i s�owniki (visualHexGrid i chunkGameObjects)

        if (mapHolder == null)
        {
            mapHolder = new GameObject("World Map").transform;
            mapHolder.parent = transform.parent;
        }

        foreach (var chunkCoord in allValidChunks)
        {
            Vector3 centerWorld = HexGridMath.GetChunkCenterWorld(chunkCoord, chunkRadius, hexSize, padding);

            Material chunkBaseMat = matDefaultGrass;

            // Ustalanie biomu i materia�u dla chunku
            GameObject chunkObj = new GameObject($"Chunk_{chunkCoord.x}_{chunkCoord.y}");
            chunkObj.transform.parent = mapHolder;
            chunkObj.transform.position = centerWorld;

            // Rejestracja w s�owniku GameObject�w (dla Fog of War)
            chunkGameObjects.Add(chunkCoord, chunkObj);

            // --- NOWO��: Inicjalizacja s�ownika heks�w dla tego chunku ---
            if (!visualHexGrid.ContainsKey(chunkCoord))
            {
                visualHexGrid[chunkCoord] = new Dictionary<Vector2Int, HexCell>();
            }
            // -------------------------------------------------------------

            if (worldData.ContainsKey(chunkCoord))
            {
                foreach (var kvp in worldData[chunkCoord])
                {
                    Vector2Int local = kvp.Key;
                    HexCellData cellData = kvp.Value;

                    Vector3 basePos = centerWorld + HexGridMath.AxialToWorld(local.x, local.y, hexSize, padding);

                    // Modyfikacja wysoko�ci dla specjalnych teren�w
                    // Modyfikacja wysokości dla specjalnych terenów
                    float heightOffset = 0f;
                    if (cellData.elevation != 0) heightOffset = cellData.elevation * 0.1f;
                    else if (cellData.feature == HexFeatureType.Hill) heightOffset = cellData.featureLevel * 0.1f;
                    else if (cellData.feature == HexFeatureType.Sinkhole) heightOffset = cellData.featureLevel * 0.1f;

                    Vector3 finalPos = basePos + Vector3.up * heightOffset;

                    // Instancjowanie bazy heksa
                    GameObject hex = Instantiate(hexPrefab, finalPos, Quaternion.identity, chunkObj.transform);
                    hex.name = $"Hex_{local.x}_{local.y}";

                    // Dodanie komponentu logicznego
                    HexCell cellComponent = hex.AddComponent<HexCell>();
                    cellComponent.Initialize(cellData);

                    // --- NOWO��: Rejestracja heksa w szybkim s�owniku ---
                    // Dzi�ki temu InteractionManager mo�e go znale�� w czasie O(1)
                    visualHexGrid[chunkCoord].Add(local, cellComponent);
                    // ----------------------------------------------------

                    // Nak�adanie wizuali�w terenu (Trawa, Las, G�ry)
                    ApplyVisualsToHex(hex, cellData, chunkBaseMat);

                    // Budowanie predefiniowanych budynk�w
                    if (cellData.startingBuilding != null)
                    {
                        SpawnPredefinedBuilding(hex, cellData.startingBuilding);
                    }
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
                                case HexFeatureType.Meadow:
                    matToUse = matMeadow != null ? matMeadow : (matFertile != null ? matFertile : groundMat);
                    nameSuffix = " (Meadow)";
                    if (prefabMeadow != null) SpawnProp(hexObj, prefabMeadow);
                    else if (prefabFertile != null) SpawnProp(hexObj, prefabFertile);
                    break;

                

                case HexFeatureType.Base:
                    matToUse = matBase != null ? matBase : groundMat;
                    nameSuffix = " [CAPITOL]";
                    if (prefabBase != null)
                    {
                        GameObject bObj = Instantiate(prefabBase, hexObj.transform);
                        bObj.transform.localPosition = Vector3.zero;
                        BuildingEntity bEntity = bObj.GetComponent<BuildingEntity>() ?? bObj.AddComponent<BuildingEntity>();
                        HexCell cell = hexObj.GetComponent<HexCell>();
                        if (cell != null) cell.SetBuilding(bEntity);
                    }
                    else
                    {
                        hexObj.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                    }
                    break;

                case HexFeatureType.Beacon:
                    matToUse = matBeacon != null ? matBeacon : groundMat;
                    nameSuffix = " [BEACON]";
                    if (prefabBeacon != null)
                    {
                        GameObject bObj = Instantiate(prefabBeacon, hexObj.transform);
                        bObj.transform.localPosition = Vector3.zero;
                        BuildingEntity bEntity = bObj.GetComponent<BeaconEntity>() ?? bObj.GetComponent<BuildingEntity>() ?? bObj.AddComponent<BeaconEntity>();
                        HexCell cell = hexObj.GetComponent<HexCell>();
                        if (cell != null) cell.SetBuilding(bEntity);
                    }
                    else
                    {
                        hexObj.transform.localScale = new Vector3(0.8f, 3f, 0.8f);
                    }
                    break;
            }
        }

        r.sharedMaterial = matToUse;
        hexObj.name += nameSuffix;
    }

    GameObject SpawnProp(GameObject parentHex, GameObject prefab)
    {
        GameObject prop = Instantiate(prefab, parentHex.transform);
        prop.transform.localPosition = Vector3.zero;
        prop.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);

        HexCell cell = parentHex.GetComponent<HexCell>();
        if (cell != null)
        {
            cell.RegisterFeatureProp(prop);
        }

        return prop;
    }

    public void ClearMap()
    {
        visualHexGrid.Clear(); // <--- WA�NE: Czy�cimy referencje
        chunkGameObjects.Clear();
        if (mapHolder != null) DestroyImmediate(mapHolder.gameObject);
        while (transform.childCount > 0) DestroyImmediate(transform.GetChild(0).gameObject);
    }

    void SpawnPredefinedBuilding(GameObject hexObj, BuildingData buildingData)
    {
        if (buildingData.prefab == null) return;

        // 1. CZYSZCZENIE TERENU
        // ApplyVisualsToHex mog�o doda� drzewka lub ska�y jako dzieci heksa.
        // Musimy je usun��, �eby budynek nie przenika� si� z lasem.
        // Robimy list� tymczasow�, bo nie mo�na usuwa� obiekt�w podczas iteracji po transform.
        List<GameObject> childrenToDestroy = new List<GameObject>();
        foreach (Transform child in hexObj.transform)
        {
            childrenToDestroy.Add(child.gameObject);
        }

        // Niszczymy dekoracje (u�ywamy DestroyImmediate, bo to dzieje si� w trakcie generowania)
        foreach (var child in childrenToDestroy)
        {
            DestroyImmediate(child);
        }

        // 2. INSTANCJOWANIE BUDYNKU
        GameObject buildingObj = Instantiate(buildingData.prefab, hexObj.transform.position, Quaternion.identity);
        buildingObj.transform.parent = hexObj.transform;

        // 3. INICJALIZACJA LOGIKI

        // A. Je�li to wie�a - przeka� dane do kontrolera
        if (buildingData is TowerData towerData)
        {
            var controller = buildingObj.GetComponent<TowerController>();
            if (controller != null)
            {
                controller.towerData = towerData;
            }
        }

        // B. Inicjalizacja BuildingEntity (Logika ekonomii/pracownik�w)
        var entity = buildingObj.GetComponent<BuildingEntity>();

        // Je�li prefab nie ma skryptu (np. prosty model), dodajemy go
        if (entity == null)
        {
            // Sprawdzamy typ, �eby doda� odpowiedni skrypt (np. TowerEntity dla wie�)
            if (buildingData is TowerData) entity = buildingObj.AddComponent<TowerEntity>();
            else if (buildingData is HousingBuildingData) entity = buildingObj.AddComponent<HousingEntity>();
            else entity = buildingObj.AddComponent<BuildingEntity>();
        }

        // Wymuszamy startow� inicjalizacj�
        entity.Initialize(buildingData);
        HexCell cellComp = hexObj.GetComponent<HexCell>();
        if (cellComp != null) cellComp.SetBuilding(entity);
    }

    public HexCell GetHexCell(Vector2Int chunkCoord, Vector2Int localCoord)
    {
        if (visualHexGrid.ContainsKey(chunkCoord) && visualHexGrid[chunkCoord].ContainsKey(localCoord))
        {
            return visualHexGrid[chunkCoord][localCoord];
        }
        return null;
    }
    
    public void RedrawSingleHex(Vector2Int chunkCoord, Vector2Int localCoord, HexCellData cellData)
    {
        HexCell cell = GetHexCell(chunkCoord, localCoord);
        if (cell == null) return;

        // 1. Zniszcz stare dekoracje (żeby nie nałożyły się np. drzewa na góry)
        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform child in cell.transform)
        {
            // Ignoruj budynki gracza
            if (child.GetComponent<BuildingEntity>() == null)
            {
                toDestroy.Add(child.gameObject);
            }
        }
        foreach (var obj in toDestroy) Destroy(obj);

        // 2. Ustal materiał
        Material chunkBaseMat = matDefaultGrass;

        // 3. Zaaplikuj nowe wizualia
        ApplyVisualsToHex(cell.gameObject, cellData, chunkBaseMat);
    }


}