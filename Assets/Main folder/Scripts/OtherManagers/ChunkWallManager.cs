using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ChunkWallManager : MonoBehaviour
{
    public static ChunkWallManager Instance { get; private set; }

    [Header("Referencje")]
    public MapExpansionManager expansionManager;
    public HexMapGenerator mapGenerator;

    [Header("Prefaby")]
    public GameObject wallPrefab;
    public GameObject corridorWallPrefab;
    public GameObject gatePrefab;

    [Header("Ustawienia")]
    public float wallHeightOffset = 0.1f;
    public bool debug = false;

    private WallSystemMode currentMode = WallSystemMode.Disabled;
    private readonly List<GameObject> activeWalls = new List<GameObject>();
    private readonly Dictionary<(Vector2Int, Vector2Int), GateEntity> activeGates = new Dictionary<(Vector2Int, Vector2Int), GateEntity>();
    private Dictionary<Vector2Int, Vector2Int> globalHexToChunk = new Dictionary<Vector2Int, Vector2Int>();

    private int chunkRadius;
    private float hexSize;
    private float padding;
    private HashSet<Vector2Int> baseChunks = new HashSet<Vector2Int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (expansionManager != null)
        {
            expansionManager.OnMapInitialized += HandleMapInitialized;
            expansionManager.OnChunkStateChanged += HandleChunkStateChanged;
        }
    }

    private void OnDestroy()
    {
        if (expansionManager != null)
        {
            expansionManager.OnMapInitialized -= HandleMapInitialized;
            expansionManager.OnChunkStateChanged -= HandleChunkStateChanged;
        }
    }

    public void SetMode(WallSystemMode mode)
    {
        if (currentMode == mode) return;
        currentMode = mode;
        RebuildAll();
    }

    public WallSystemMode CurrentMode => currentMode;

    private void HandleMapInitialized(List<Vector2Int> startChunks)
    {
        chunkRadius = mapGenerator.chunkRadius;
        hexSize = mapGenerator.hexSize;
        padding = mapGenerator.padding;

        baseChunks = new HashSet<Vector2Int>(startChunks);
        BuildGlobalHexLookup();

        if (debug)
        {
            SetMode(WallSystemMode.Solution2_FrontLine);
        }
        else
        {
            RebuildAll();
        }
    }

    private void HandleChunkStateChanged(Vector2Int coord, ChunkState newState)
    {
        if (currentMode == WallSystemMode.Disabled) return;
        if (newState == ChunkState.Settled || newState == ChunkState.Outskirts)
        {
            RebuildAll();
        }
    }

    private void RebuildAll()
    {
        ClearAll();

        if (currentMode == WallSystemMode.Disabled) return;
        if (mapGenerator == null || expansionManager == null) return;
        if (mapGenerator.worldData == null) return;

        switch (currentMode)
        {
            case WallSystemMode.Solution1_BaseOnly:
                BuildSolution1();
                break;

            case WallSystemMode.Solution2_FrontLine:
                BuildSolution2();
                break;
        }
    }

    private void BuildSolution1()
    {
        foreach (var chunk in baseChunks)
        {
            PlaceOuterWallsForChunk(chunk, excludeNeighborChunks: baseChunks);
        }

        foreach (var kvp in expansionManager.roadDependencies)
        {
            Vector2Int successor = kvp.Key;
            Vector2Int predecessor = kvp.Value;

            if (baseChunks.Contains(predecessor) && !baseChunks.Contains(successor))
            {
                PlaceGateOnBoundary(predecessor, successor);
            }
        }
    }

    private void BuildSolution2()
    {
        var settledChunks = GetSettledChunks();
        if (settledChunks.Count == 0) return;

        var roadChunks = GetRoadChunks();
        var frontierChunks = FindFrontierChunks(settledChunks, roadChunks);

        var walledCityChunks = new HashSet<Vector2Int>(settledChunks);
        foreach (var f in frontierChunks)
        {
            walledCityChunks.Remove(f);
        }

        foreach (var chunk in walledCityChunks)
        {
            PlaceOuterWallsForChunk(chunk, excludeNeighborChunks: walledCityChunks);
        }

        foreach (var frontier in frontierChunks)
        {
            if (!expansionManager.roadDependencies.TryGetValue(frontier, out var predecessor))
                continue;

            if (!walledCityChunks.Contains(predecessor)) continue;

            PlaceGateOnBoundary(predecessor, frontier);
        }
    }

    private void PlaceOuterWallsForChunk(Vector2Int chunkCoord, HashSet<Vector2Int> excludeNeighborChunks)
    {
        if (!mapGenerator.worldData.ContainsKey(chunkCoord)) return;

        var chunkData = mapGenerator.worldData[chunkCoord];

        foreach (var kvp in chunkData)
        {
            Vector2Int localCoord = kvp.Key;
            Vector2Int globalCoord = LocalToGlobal(chunkCoord, localCoord);
            Vector3 hexWorldPos = GetHexWorldPos(chunkCoord, localCoord);
            bool isPath = kvp.Value.isPath;

            foreach (var neighborGlobal in HexGridMath.GetNeighbors(globalCoord))
            {
                Vector2Int neighborChunk;
                bool neighborInMap = globalHexToChunk.TryGetValue(neighborGlobal, out neighborChunk);

                if (neighborInMap && neighborChunk == chunkCoord) continue;
                if (neighborInMap && excludeNeighborChunks.Contains(neighborChunk)) continue;

                if (isPath && neighborInMap)
                {
                    Vector2Int neighborLocal = GlobalToLocal(neighborChunk, neighborGlobal);
                    if (mapGenerator.worldData.TryGetValue(neighborChunk, out var neighborData))
                    {
                        if (neighborData.TryGetValue(neighborLocal, out var neighborCell) && neighborCell.isPath)
                        {
                            continue;
                        }
                    }
                }

                Vector3 neighborWorldPos = neighborInMap
                    ? GetHexWorldPos(neighborChunk, GlobalToLocal(neighborChunk, neighborGlobal))
                    : EstimateWorldPosOutsideMap(neighborGlobal);

                Vector3 wallPos = (hexWorldPos + neighborWorldPos) * 0.5f;
                wallPos.y = wallHeightOffset;

                Quaternion wallRot = GetEdgeRotation(hexWorldPos, neighborWorldPos);
                SpawnWall(wallPrefab, wallPos, wallRot);
            }
        }
    }

    private void PlaceGateOnBoundary(Vector2Int insideChunk, Vector2Int outsideChunk)
    {
        var key = insideChunk.x < outsideChunk.x || (insideChunk.x == outsideChunk.x && insideChunk.y < outsideChunk.y)
            ? (insideChunk, outsideChunk) : (outsideChunk, insideChunk);

        if (activeGates.ContainsKey(key)) return;
        if (gatePrefab == null) return;
        if (!mapGenerator.worldData.ContainsKey(insideChunk)) return;

        var insideData = mapGenerator.worldData[insideChunk];

        foreach (var kvp in insideData)
        {
            if (!kvp.Value.isPath) continue;

            Vector2Int localCoord = kvp.Key;
            Vector2Int globalCoord = LocalToGlobal(insideChunk, localCoord);

            foreach (var neighborGlobal in HexGridMath.GetNeighbors(globalCoord))
            {
                if (!globalHexToChunk.TryGetValue(neighborGlobal, out var neighborChunk)) continue;
                if (neighborChunk != outsideChunk) continue;

                Vector2Int neighborLocal = GlobalToLocal(outsideChunk, neighborGlobal);

                if (mapGenerator.worldData.TryGetValue(outsideChunk, out var outsideData))
                {
                    if (outsideData.TryGetValue(neighborLocal, out var neighborCell) && neighborCell.isPath)
                    {
                        Vector3 insideHexPos = GetHexWorldPos(insideChunk, localCoord);
                        Vector3 outsideHexPos = GetHexWorldPos(outsideChunk, neighborLocal);

                        Vector3 gatePos = (insideHexPos + outsideHexPos) * 0.5f;
                        gatePos.y = wallHeightOffset;

                        Quaternion gateRot = GetEdgeRotation(insideHexPos, outsideHexPos);

                        var gateObj = Instantiate(gatePrefab, gatePos, gateRot, transform);
                        var gateEntity = gateObj.GetComponent<GateEntity>();

                        if (gateEntity != null)
                        {
                            gateEntity.chunkA = insideChunk;
                            gateEntity.chunkB = outsideChunk;
                            activeGates[key] = gateEntity;
                        }
                        else
                        {
                            activeWalls.Add(gateObj);
                        }

                        return;
                    }
                }
            }
        }
    }

    private HashSet<Vector2Int> GetSettledChunks()
    {
        return new HashSet<Vector2Int>(
            expansionManager.activeChunks
                .Where(kvp => kvp.Value.EffectiveState == ChunkState.Settled)
                .Select(kvp => kvp.Key));
    }

    private HashSet<Vector2Int> GetRoadChunks()
    {
        return new HashSet<Vector2Int>(expansionManager.roadDependencies.Keys);
    }

    private HashSet<Vector2Int> FindFrontierChunks(
        HashSet<Vector2Int> settled,
        HashSet<Vector2Int> roadChunks)
    {
        var successors = new Dictionary<Vector2Int, List<Vector2Int>>();
        foreach (var kvp in expansionManager.roadDependencies)
        {
            var chunk = kvp.Key;
            var pred = kvp.Value;
            if (!successors.ContainsKey(pred))
                successors[pred] = new List<Vector2Int>();
            successors[pred].Add(chunk);
        }

        var frontier = new HashSet<Vector2Int>();

        foreach (var chunk in roadChunks)
        {
            if (!settled.Contains(chunk)) continue;

            bool hasSettledSuccessor = successors.TryGetValue(chunk, out var succs)
                && succs.Any(s => settled.Contains(s));

            if (!hasSettledSuccessor)
                frontier.Add(chunk);
        }

        return frontier;
    }

    private Vector2Int LocalToGlobal(Vector2Int chunkCoord, Vector2Int localCoord)
    {
        int cq = chunkCoord.x, cr = chunkCoord.y, R = chunkRadius;
        int centerQ = cq * (2 * R + 1) + cr * R;
        int centerR = cq * (-R) + cr * (R + 1);
        return new Vector2Int(centerQ + localCoord.x, centerR + localCoord.y);
    }

    private Vector2Int GlobalToLocal(Vector2Int chunkCoord, Vector2Int globalCoord)
    {
        int cq = chunkCoord.x, cr = chunkCoord.y, R = chunkRadius;
        int centerQ = cq * (2 * R + 1) + cr * R;
        int centerR = cq * (-R) + cr * (R + 1);
        return new Vector2Int(globalCoord.x - centerQ, globalCoord.y - centerR);
    }

    private Vector3 GetHexWorldPos(Vector2Int chunkCoord, Vector2Int localCoord)
    {
        Vector3 chunkCenter = HexGridMath.GetChunkCenterWorld(chunkCoord, chunkRadius, hexSize, padding);
        return chunkCenter + HexGridMath.AxialToWorld(localCoord.x, localCoord.y, hexSize, padding);
    }

    private Vector3 EstimateWorldPosOutsideMap(Vector2Int globalAxial)
    {
        return HexGridMath.AxialToWorld(globalAxial.x, globalAxial.y, hexSize, padding);
    }

    private Quaternion GetEdgeRotation(Vector3 from, Vector3 to)
    {
        Vector3 dir = (to - from).normalized;
        if (dir == Vector3.zero) return Quaternion.identity;
        return Quaternion.LookRotation(dir) * Quaternion.Euler(0, 90, 0);
    }

    private void BuildGlobalHexLookup()
    {
        globalHexToChunk.Clear();
        foreach (var chunkKvp in mapGenerator.worldData)
        {
            var chunkCoord = chunkKvp.Key;
            foreach (var localCoord in chunkKvp.Value.Keys)
            {
                var globalCoord = LocalToGlobal(chunkCoord, localCoord);
                globalHexToChunk[globalCoord] = chunkCoord;
            }
        }
    }

    private void SpawnWall(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return;
        var obj = Instantiate(prefab, pos, rot, transform);
        activeWalls.Add(obj);
    }

    private void ClearAll()
    {
        foreach (var wall in activeWalls)
            if (wall != null) Destroy(wall);
        activeWalls.Clear();

        foreach (var kvp in activeGates)
            if (kvp.Value != null) Destroy(kvp.Value.gameObject);
        activeGates.Clear();
    }
}