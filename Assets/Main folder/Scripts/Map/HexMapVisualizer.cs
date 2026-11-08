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

    [Header("Mountain Configuration")]
    [Tooltip("Prefab modelu 3D dla szczytu gory (np. MountainTop).")]
    public GameObject mountainTopPrefab;
    [Tooltip("Pionowe przesuniecie prefabu szczytu gory.")]
    public float mountainTopYOffset = 0f;
    [Tooltip("Mnoznik skali prefabu szczytu gory.")]
    public float mountainTopScaleMultiplier = 1f;
    [Tooltip("Deterministyczny losowy obrot szczytu gory wokol osi Y.")]
    public bool randomizeMountainRotation = true;
    [Tooltip("Dedykowany material scian bocznych gory z przejsciem geologicznym (HexSide_Mountain).")]
    public Material matSideMountain;

    // --- Prefaby Specjalne ---
    [Header("Prefaby Budynków Specjalnych")]
    public GameObject prefabBase;   // Kapitol
    public GameObject prefabBeacon; // Beacon of Hope

    [Header("Materiały Specjalne")]
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

    [Header("Ustawienia Wysokosci Terenu (Elevation)")]
    [Tooltip("Wysokosc pionowa (Y) przypadajaca na kazdy poziom wzniesienia (gory, wzgorza) i zapadliny.")]
    public float elevationHeightStep = 0.25f;

    [Header("Wspólna Wysokość Terenu i Mgły")]
    [SerializeField]
    private float maxTerrainSurfaceY = 0f;

    /// <summary>
    /// Maksymalna wysokość powierzchni terenu w przestrzeni świata (Y).
    /// Wyliczana podczas generacji mapy jako najwyższy punkt powierzchni heksów.
    /// Jeśli mapa nie została jeszcze wygenerowana, zwraca wysokość dla maksymalnego poziomu wzniesienia (5).
    /// </summary>
    public float MaxTerrainSurfaceY
    {
        get
        {
            if (maxTerrainSurfaceY > 0f) return maxTerrainSurfaceY;
            return 5f * elevationHeightStep;
        }
        private set => maxTerrainSurfaceY = value;
    }

    [Header("Boczne Sciany Heksow (Geometria Proceduralna)")]
    [Tooltip("Generuje proceduralny mesh heksa: submesh 0 = gora, submesh 1 = boczne sciany dopasowane do sasiadow. Wylaczenie przywraca oryginalny mesh prefabu.")]
    public bool generateSideWalls = true;
    [Tooltip("Material bocznych scian (submesh 1). Docelowo shader HexSideTerrain. Gdy pusty - uzywany jest material gornej powierzchni.")]
    public Material matSide;
    [Tooltip("Poziom Y (lokalny w chunku), do ktorego schodza sciany na zewnetrznej krawedzi mapy. Powinien lezec ponizej najglebszego zapadliska.")]
    public float mapEdgeBottomY = -1.4f;
    [Tooltip("Ukryty pas sciany pod powierzchnia sasiada. Powinien byc >= HexCell.liftOffset, aby podniesiony (podswietlony) heks nie odslanial szczeliny. 0 = wylaczone.")]
    public float liftSkirtDepth = 0.25f;

    // Cache meshy terenu - heksy o identycznym ukladzie krawedzi wspoldziela jeden mesh.
    private readonly Dictionary<string, Mesh> terrainMeshCache = new Dictionary<string, Mesh>();
    private readonly HexTerrainMeshBuilder.EdgeSpec[] edgeBuffer = new HexTerrainMeshBuilder.EdgeSpec[6];

    private HashSet<Vector2Int> globalRoadMap;
    private Dictionary<Vector2Int, float> globalHeightsMap;
    private int cachedChunkRadius = 3;
    private float cachedHexRadius = 1.02f;

    private Transform mapHolder;

    // Słownik przechowujący fizyczne obiekty chunków
    private Dictionary<Vector2Int, GameObject> chunkGameObjects = new Dictionary<Vector2Int, GameObject>();

    private Dictionary<Vector2Int, Dictionary<Vector2Int, HexCell>> visualHexGrid = new Dictionary<Vector2Int, Dictionary<Vector2Int, HexCell>>();

    public void RegisterHexCell(Vector2Int chunkCoord, Vector2Int localCoord, HexCell cell)
    {
        if (!visualHexGrid.ContainsKey(chunkCoord))
        {
            visualHexGrid[chunkCoord] = new Dictionary<Vector2Int, HexCell>();
        }
        visualHexGrid[chunkCoord][localCoord] = cell;
    }

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
        ClearMap(); // Czyści stare obiekty i słowniki (visualHexGrid i chunkGameObjects)

        cachedChunkRadius = chunkRadius;
        cachedHexRadius = hexSize + padding;

        if (mapHolder == null)
        {
            mapHolder = new GameObject("World Map").transform;
            mapHolder.parent = transform.parent;
        }

        // Globalne mapy (axial -> Y oraz axial -> isRoad) - potrzebne na granicach chunkow.
        MaxTerrainSurfaceY = ComputeMaxTerrainHeight(worldData);
        globalHeightsMap = generateSideWalls ? BuildGlobalHeightMap(worldData, allValidChunks, chunkRadius) : null;
        globalRoadMap = BuildGlobalRoadMap(worldData, allValidChunks, chunkRadius);

        // Synchronizacja parametrów bocznych ścian z generatorem mapy
        if (matSide != null)
        {
            matSide.SetFloat("_HeightStep", elevationHeightStep);
            matSide.SetFloat("_BaseHeightY", 0f);
        }
        if (matSideMountain != null)
        {
            matSideMountain.SetFloat("_HeightStep", elevationHeightStep);
            matSideMountain.SetFloat("_BaseHeightY", 0f);
        }

        foreach (var chunkCoord in allValidChunks)
        {
            Vector3 centerWorld = HexGridMath.GetChunkCenterWorld(chunkCoord, chunkRadius, hexSize, padding);
            Vector2Int chunkCenterAxial = HexGridMath.GetChunkCenterAxial(chunkCoord, chunkRadius);

            Material chunkBaseMat = matDefaultGrass;

            // Ustalanie biomu i materiału dla chunku
            GameObject chunkObj = new GameObject($"Chunk_{chunkCoord.x}_{chunkCoord.y}");
            chunkObj.transform.parent = mapHolder;
            chunkObj.transform.position = centerWorld;

            // Rejestracja w słowniku GameObjectów (dla Fog of War)
            chunkGameObjects.Add(chunkCoord, chunkObj);

            // Inicjalizacja słownika heksów dla tego chunku
            if (!visualHexGrid.ContainsKey(chunkCoord))
            {
                visualHexGrid[chunkCoord] = new Dictionary<Vector2Int, HexCell>();
            }

            if (worldData.ContainsKey(chunkCoord))
            {
                foreach (var kvp in worldData[chunkCoord])
                {
                    Vector2Int local = kvp.Key;
                    HexCellData cellData = kvp.Value;

                    Vector3 basePos = centerWorld + HexGridMath.AxialToWorld(local.x, local.y, hexSize, padding);

                    // Modyfikacja wysokości dla specjalnych terenów
                    float heightOffset = GetTerrainHeight(cellData);
        if (heightOffset > maxTerrainSurfaceY)
        {
            maxTerrainSurfaceY = heightOffset;
            if (FogOfWarManager.Instance != null)
            {
                FogOfWarManager.Instance.RefreshMistHeight();
            }
        }

                    Vector3 finalPos = basePos + Vector3.up * heightOffset;

                    // Instancjowanie bazy heksa
                    GameObject hex = Instantiate(hexPrefab, finalPos, Quaternion.identity, chunkObj.transform);
                    hex.name = $"Hex_{local.x}_{local.y}";

                    Vector2Int globalCoord = chunkCenterAxial + local;
                    bool isRoadCell = cellData.isRoad || cellData.isPath;
                    float hillHeight01 = GetHillHeight01(cellData);

                    // Proceduralny mesh: gora + boczne sciany dopasowane do sasiadow
                    if (globalHeightsMap != null || isRoadCell || hillHeight01 > 0f)
                    {
                        ApplyTerrainMesh(hex, globalCoord, heightOffset, globalHeightsMap, hexSize + padding, isRoadCell, hillHeight01);
                    }

                    // Dodanie komponentu logicznego
                    HexCell cellComponent = hex.AddComponent<HexCell>();
                    cellComponent.Initialize(cellData);

                    // Rejestracja heksa w szybkim słowniku (O(1) dla InteractionManager)
                    visualHexGrid[chunkCoord].Add(local, cellComponent);

                    // Nakładanie wizualiów terenu (Trawa, Las, Góry)
                    ApplyVisualsToHex(hex, cellData, chunkBaseMat, globalCoord);

                    // Budowanie predefiniowanych budynków
                    if (cellData.startingBuilding != null)
                    {
                        SpawnPredefinedBuilding(hex, cellData.startingBuilding);
                    }
                }
            }
        }
    }

    void ApplyVisualsToHex(GameObject hexObj, HexCellData data, Material groundMat, Vector2Int globalCoord = default)
    {
        Renderer r = hexObj.GetComponentInChildren<Renderer>();
        if (r == null) return;

        Material matToUse = groundMat;
        string nameSuffix = "";
        hexObj.transform.localScale = Vector3.one;
        bool isMountain = false;

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
                    isMountain = true;
                    SpawnMountainProp(hexObj, globalCoord);
                    break;
                case HexFeatureType.Hill:
                    matToUse = groundMat; // W nowej architekturze wzgorze zachowuje bazowy material terenu (np. trawy)
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

        ApplyTerrainMaterials(r, matToUse, isMountain);
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

    GameObject SpawnMountainProp(GameObject parentHex, Vector2Int globalCoord)
    {
        GameObject prefabToUse = mountainTopPrefab != null ? mountainTopPrefab : prefabMountain;
        if (prefabToUse == null)
        {
            return null;
        }

        HexCell cell = parentHex.GetComponent<HexCell>();
        if (cell != null && cell.FeatureProp != null)
        {
            Destroy(cell.FeatureProp);
        }

        GameObject prop = Instantiate(prefabToUse, parentHex.transform);
        prop.name = "MountainTop_Prop";
        prop.transform.localPosition = new Vector3(0f, mountainTopYOffset, 0f);

        if (randomizeMountainRotation)
        {
            int hash = (globalCoord.x * 73856093) ^ (globalCoord.y * 19349663);
            float rotY = ((float)((hash & 0x7fffffff) % 36000)) / 100f; // [0..360)
            prop.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
        }
        else
        {
            prop.transform.localRotation = Quaternion.identity;
        }

        prop.transform.localScale = prefabToUse.transform.localScale * mountainTopScaleMultiplier;

        // Wylaczenie colliderow dekoracji, aby nie blokowaly raycastow wyboru terenu ani budowy
        Collider[] colliders = prop.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        if (cell != null)
        {
            cell.RegisterFeatureProp(prop);
        }

        return prop;
    }

    public void ClearMap()
    {
        maxTerrainSurfaceY = 0f;
        visualHexGrid.Clear();
        chunkGameObjects.Clear();
        globalRoadMap?.Clear();
        globalHeightsMap?.Clear();
        if (mapHolder != null) DestroyImmediate(mapHolder.gameObject);
        while (transform.childCount > 0) DestroyImmediate(transform.GetChild(0).gameObject);
        ReleaseTerrainMeshes();
    }

    void SpawnPredefinedBuilding(GameObject hexObj, BuildingData buildingData)
    {
        if (buildingData.prefab == null) return;

        // 1. CZYSZCZENIE TERENU
        List<GameObject> childrenToDestroy = new List<GameObject>();
        foreach (Transform child in hexObj.transform)
        {
            childrenToDestroy.Add(child.gameObject);
        }
        foreach (var child in childrenToDestroy)
        {
            DestroyImmediate(child);
        }

        // 2. INSTANCJOWANIE BUDYNKU
        GameObject buildingObj = Instantiate(buildingData.prefab, hexObj.transform.position, Quaternion.identity);
        buildingObj.transform.parent = hexObj.transform;

        // 3. INICJALIZACJA LOGIKI
        if (buildingData is TowerData towerData)
        {
            var controller = buildingObj.GetComponent<TowerController>();
            if (controller != null)
            {
                controller.towerData = towerData;
            }
        }

        var entity = buildingObj.GetComponent<BuildingEntity>();
        if (entity == null)
        {
            if (buildingData is TowerData) entity = buildingObj.AddComponent<TowerEntity>();
            else if (buildingData is HousingBuildingData) entity = buildingObj.AddComponent<HousingEntity>();
            else entity = buildingObj.AddComponent<BuildingEntity>();
        }

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

    /// <summary>
    /// Zwraca rzeczywista wysokosc powierzchni Y w przestrzeni swiata dla heksa w danym chunku.
    /// Uzywane przez GroundMist do dopasowania wysokosci mgly do kazdego heksa z osobna.
    /// </summary>
    public float GetHexTerrainSurfaceY(Vector2Int chunkCoord, Vector2Int localCoord)
    {
        HexCell cell = GetHexCell(chunkCoord, localCoord);
        if (cell != null)
        {
            return cell.transform.position.y;
        }
        return 0f;
    }

    /// <summary>
    /// Zwraca rzeczywista wysokosc powierzchni Y dla heksa odpowiadajacego podanej pozycji lokalnej w chunku.
    /// </summary>
    public float GetHexTerrainSurfaceYByLocalPos(Vector2Int chunkCoord, Vector3 localPos)
    {
        float hexStep = cachedHexRadius > 0f ? cachedHexRadius : 1f;
        Vector2Int axial = HexGridMath.WorldToAxial(localPos.x, localPos.z, hexStep, 0f);
        HexCell cell = GetHexCell(chunkCoord, axial);
        if (cell != null)
        {
            return cell.transform.position.y;
        }

        if (visualHexGrid.TryGetValue(chunkCoord, out var cells))
        {
            float bestDistSq = float.MaxValue;
            float bestY = 0f;
            foreach (var kvp in cells)
            {
                if (kvp.Value == null) continue;
                Vector3 p = kvp.Value.transform.localPosition;
                float dx = p.x - localPos.x;
                float dz = p.z - localPos.z;
                float distSq = dx * dx + dz * dz;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestY = kvp.Value.transform.position.y;
                }
            }
            if (bestDistSq < 4.0f)
            {
                return bestY;
            }
        }
        return 0f;
    }
    
    public void RedrawSingleHex(Vector2Int chunkCoord, Vector2Int localCoord, HexCellData cellData)
    {
        HexCell cell = GetHexCell(chunkCoord, localCoord);
        if (cell == null) return;

        // 1. Zniszcz stare dekoracje (żeby nie nałożyły się np. drzewa na góry)
        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform child in cell.transform)
        {
            if (child.GetComponent<BuildingEntity>() == null)
            {
                toDestroy.Add(child.gameObject);
            }
        }
        foreach (var obj in toDestroy) Destroy(obj);

        // 2. Aktualizacja mapy dróg i mesha
        Vector2Int centerAxial = HexGridMath.GetChunkCenterAxial(chunkCoord, cachedChunkRadius > 0 ? cachedChunkRadius : 3);
        Vector2Int globalCoord = centerAxial + localCoord;
        bool isRoad = cellData.isRoad || cellData.isPath;
        if (globalRoadMap != null)
        {
            if (isRoad) globalRoadMap.Add(globalCoord);
            else globalRoadMap.Remove(globalCoord);
        }

        float heightOffset = GetTerrainHeight(cellData);
        float radius = cachedHexRadius > 0f ? cachedHexRadius : 1f;
        float hillHeight01 = GetHillHeight01(cellData);
        ApplyTerrainMesh(cell.gameObject, globalCoord, heightOffset, globalHeightsMap, radius, isRoad, hillHeight01);

        // Aktualizuj sąsiednie drogi, jeśli ich maska uległa zmianie
        for (int k = 0; k < 6; k++)
        {
            Vector2Int neighborGlobal = globalCoord + HexTerrainMeshBuilder.Directions[k];
            if (globalRoadMap != null && globalRoadMap.Contains(neighborGlobal))
            {
                foreach (var chunkKvp in visualHexGrid)
                {
                    Vector2Int cCenter = HexGridMath.GetChunkCenterAxial(chunkKvp.Key, cachedChunkRadius > 0 ? cachedChunkRadius : 3);
                    Vector2Int nLocal = neighborGlobal - cCenter;
                    if (chunkKvp.Value.TryGetValue(nLocal, out HexCell nCell) && nCell != null)
                    {
                        float nHeight = GetTerrainHeight(nCell.Data);
                        ApplyTerrainMesh(nCell.gameObject, neighborGlobal, nHeight, globalHeightsMap, radius, true);
                        break;
                    }
                }
            }
        }

        // 3. Ustal materiał
        Material chunkBaseMat = matDefaultGrass;

        // 4. Zaaplikuj nowe wizualia
        ApplyVisualsToHex(cell.gameObject, cellData, chunkBaseMat, globalCoord);
    }

    // =========================================================================
    // Geometria terenu: wysokosci, boczne sciany, materialy
    // =========================================================================

    /// <summary>
    /// Wysokosc gornej powierzchni heksa (lokalne Y w chunku). Jedyne zrodlo prawdy
    /// zarowno dla pozycji heksa, jak i dla scian sasiadow.
    /// </summary>
    public float GetTerrainHeight(HexCellData cellData)
    {
        if (cellData.isRoad) return 0f;
        if (cellData.elevation != 0) return cellData.elevation * elevationHeightStep;
        if (cellData.feature == HexFeatureType.Hill) return Mathf.Max(1, cellData.featureLevel) * elevationHeightStep;
        if (cellData.feature == HexFeatureType.Sinkhole) return (cellData.featureLevel < 0 ? cellData.featureLevel : -1) * elevationHeightStep;
        if (cellData.feature == HexFeatureType.Mountain) return 4f * elevationHeightStep;
        return 0f;
    }

    /// <summary>
    /// Oblicza maksymalną wysokość powierzchni terenu na podstawie danych komórek.
    /// Jeśli dane są puste lub brak komórek o wysokości > 0, zwraca 5 * elevationHeightStep (poziom 5).
    /// </summary>
    public float ComputeMaxTerrainHeight(IReadOnlyDictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>> data)
    {
        float maxH = 0f;
        if (data != null)
        {
            foreach (var chunk in data.Values)
            {
                if (chunk == null) continue;
                foreach (var cell in chunk.Values)
                {
                    if (cell != null)
                    {
                        float h = GetTerrainHeight(cell);
                        if (h > maxH) maxH = h;
                    }
                }
            }
        }
        return maxH > 0f ? maxH : (5f * elevationHeightStep);
    }


    private Dictionary<Vector2Int, float> BuildGlobalHeightMap(
        Dictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>> worldData,
        HashSet<Vector2Int> allValidChunks,
        int chunkRadius)
    {
        var heights = new Dictionary<Vector2Int, float>();
        foreach (var chunkCoord in allValidChunks)
        {
            if (!worldData.TryGetValue(chunkCoord, out var cells)) continue;
            Vector2Int centerAxial = HexGridMath.GetChunkCenterAxial(chunkCoord, chunkRadius);
            foreach (var kvp in cells)
            {
                heights[centerAxial + kvp.Key] = GetTerrainHeight(kvp.Value);
            }
        }
        return heights;
    }

    private HashSet<Vector2Int> BuildGlobalRoadMap(
        Dictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>> worldData,
        HashSet<Vector2Int> allValidChunks,
        int chunkRadius)
    {
        var roads = new HashSet<Vector2Int>();
        foreach (var chunkCoord in allValidChunks)
        {
            if (!worldData.TryGetValue(chunkCoord, out var cells)) continue;
            Vector2Int centerAxial = HexGridMath.GetChunkCenterAxial(chunkCoord, chunkRadius);
            foreach (var kvp in cells)
            {
                if (kvp.Value != null && (kvp.Value.isRoad || kvp.Value.isPath))
                {
                    roads.Add(centerAxial + kvp.Key);
                }
            }
        }
        return roads;
    }

    /// <summary>
    /// Oblicza 6-bitową maskę krawędzi drogi dla danego globalnego heksa.
    /// Bit k = 1: krawędź k graniczy z innym terenem lub krawędzią mapy (wymaga pobocza).
    /// Bit k = 0: krawędź k łączy się z innym heksem Road (brak pobocza).
    /// </summary>
    public int ComputeRoadEdgeMask(Vector2Int globalCoord)
    {
        int mask = 0;
        for (int k = 0; k < 6; k++)
        {
            Vector2Int neighborCoord = globalCoord + HexTerrainMeshBuilder.Directions[k];
            bool neighborIsRoad = globalRoadMap != null && globalRoadMap.Contains(neighborCoord);
            if (!neighborIsRoad)
            {
                mask |= (1 << k);
            }
        }
        return mask;
    }

    private float GetHillHeight01(HexCellData cellData)
    {
        // Hill shader na gornej powierzchni zostal wycofany na rzecz bazowego materialu terenu
        return 0f;
    }

    private void ApplyTerrainMesh(GameObject hex, Vector2Int globalCoord, float ownHeight, Dictionary<Vector2Int, float> heights, float radius, bool isRoad = false, float hillHeight01 = 0f)
    {
        MeshFilter mf = hex.GetComponent<MeshFilter>();
        if (mf == null) return;

        for (int k = 0; k < 6; k++)
        {
            float neighborHeight = 0f;
            bool hasNeighbor = heights != null && heights.TryGetValue(globalCoord + HexTerrainMeshBuilder.Directions[k], out neighborHeight);
            edgeBuffer[k] = HexTerrainMeshBuilder.ComputeEdge(ownHeight, hasNeighbor, neighborHeight, mapEdgeBottomY, liftSkirtDepth);
        }

        int roadMask = isRoad ? ComputeRoadEdgeMask(globalCoord) : 0;

        string key = HexTerrainMeshBuilder.BuildCacheKey(radius, ownHeight, elevationHeightStep, edgeBuffer, roadMask, hillHeight01);
        if (!terrainMeshCache.TryGetValue(key, out Mesh mesh) || mesh == null)
        {
            mesh = HexTerrainMeshBuilder.Build(radius, ownHeight, elevationHeightStep, edgeBuffer, roadMask, hillHeight01);
            mesh.hideFlags = HideFlags.DontSave;
            terrainMeshCache[key] = mesh;
        }
        mf.sharedMesh = mesh;
    }

    /// <summary>Slot 0 = gora (material terenu), slot 1 = boczne sciany (matSide / matSideMountain). Dla starego meshu - jeden slot jak dawniej.</summary>
    private void ApplyTerrainMaterials(Renderer r, Material topMat, bool isMountain = false)
    {
        MeshFilter mf = r.GetComponent<MeshFilter>();
        bool hasSideSubmesh = mf != null && mf.sharedMesh != null && mf.sharedMesh.subMeshCount > HexTerrainMeshBuilder.SideSubmesh;
        if (!hasSideSubmesh)
        {
            r.sharedMaterial = topMat;
            return;
        }
        Material sideMatToUse = (isMountain && matSideMountain != null) ? matSideMountain : (matSide != null ? matSide : topMat);
        r.sharedMaterials = new Material[] { topMat, sideMatToUse };
    }

    private void ReleaseTerrainMeshes()
    {
        foreach (var mesh in terrainMeshCache.Values)
        {
            if (mesh != null) DestroyImmediate(mesh);
        }
        terrainMeshCache.Clear();
    }

    private void OnDestroy()
    {
        ReleaseTerrainMeshes();
    }
}
