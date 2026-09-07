using System;
using System.Collections.Generic;
using UnityEngine;

public class MapExpansionManager : MonoBehaviour
{
    public static MapExpansionManager Instance { get; private set; }

    public Dictionary<Vector2Int, ChunkStateData> activeChunks = new Dictionary<Vector2Int, ChunkStateData>();
    public Dictionary<Vector2Int, Vector2Int> roadDependencies = new Dictionary<Vector2Int, Vector2Int>();

    public event Action<List<Vector2Int>> OnMapInitialized;
    public event Action<Vector2Int, ChunkState> OnChunkStateChanged;

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
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted += HandleMorningTick;
        }
    }

    private void OnDestroy()
    {
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= HandleMorningTick;
        }
    }

    public void Initialize(IReadOnlyDictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>> worldData, List<Vector2Int> initialSettledChunks = null)
    {
        activeChunks.Clear();
        roadDependencies.Clear();

        if (HexMapGenerator.Instance != null)
        {
            roadDependencies = HexMapGenerator.Instance.GetRoadRevealDependencies();
        }

        HashSet<Vector2Int> startSettled = new HashSet<Vector2Int>();
        if (initialSettledChunks != null)
        {
            foreach (var c in initialSettledChunks)
            {
                startSettled.Add(c);
            }
        }
        else
        {
            startSettled.Add(Vector2Int.zero);
        }

        foreach (var chunkCoord in worldData.Keys)
        {
            ChunkStateData data = new ChunkStateData
            {
                chunkCoord = chunkCoord,
                baseState = startSettled.Contains(chunkCoord) ? ChunkState.Settled : ChunkState.Wilderness
            };

            if (roadDependencies.ContainsKey(chunkCoord))
            {
                data.isRoadChunk = true;
                data.roadPredecessor = roadDependencies[chunkCoord];
                data.hasRoadPredecessor = data.roadPredecessor.x != -999;
            }

            activeChunks.Add(chunkCoord, data);
        }

        ChunkState initialRoadState = ChunkState.Settled;
        List<Vector2Int> initialRoadChunks = new List<Vector2Int>();

        foreach (var kvp in activeChunks)
        {
            ChunkStateData data = kvp.Value;
            if (!data.isRoadChunk || !data.hasRoadPredecessor) continue;

            if (startSettled.Contains(data.roadPredecessor) && !startSettled.Contains(kvp.Key))
            {
                initialRoadChunks.Add(kvp.Key);
            }
        }

        foreach (var roadCoord in initialRoadChunks)
        {
            activeChunks[roadCoord].baseState = initialRoadState;
            if (initialRoadState == ChunkState.Settled)
            {
                startSettled.Add(roadCoord);
            }
        }

        PropagateBorderlands();

        List<Vector2Int> settledList = new List<Vector2Int>(startSettled);
        OnMapInitialized?.Invoke(settledList);
    }
    private void HandleMorningTick()
    {
        TickSurveys();
        TickOutposts();
        PropagateBorderlands();
    }

    private void TickSurveys()
    {
        List<Vector2Int> completedSurveys = new List<Vector2Int>();

        foreach (var kvp in activeChunks)
        {
            ChunkStateData data = kvp.Value;
            if (data.baseState != ChunkState.Surveying) continue;

            data.surveyDaysRemaining--;
            if (data.surveyDaysRemaining <= 0)
            {
                completedSurveys.Add(kvp.Key);
            }
        }

        foreach (var coord in completedSurveys)
        {
            SetChunkBaseState(coord, ChunkState.Outskirts);
        }
    }

    private void TickOutposts()
    {
        List<Vector2Int> fullySettledCoords = new List<Vector2Int>();

        foreach (var kvp in activeChunks)
        {
            ChunkStateData data = kvp.Value;
            if (data.baseState != ChunkState.Outskirts || data.activeOutpost == null) continue;

            data.outpostSettlementDaysRemaining--;
            if (data.outpostSettlementDaysRemaining <= 0)
            {
                fullySettledCoords.Add(kvp.Key);
            }
        }

        foreach (var coord in fullySettledCoords)
        {
            ChunkStateData data = activeChunks[coord];
            OutpostEntity outpost = data.activeOutpost;

            SetChunkBaseState(coord, ChunkState.Settled);

            if (outpost != null)
            {
                outpost.CompleteSettlement();
            }
        }
    }

    public void PropagateBorderlands()
    {
        List<Vector2Int> toBorderlands = new List<Vector2Int>();

        foreach (var kvp in activeChunks)
        {
            Vector2Int coord = kvp.Key;
            ChunkStateData data = kvp.Value;

            if (data.baseState != ChunkState.Wilderness) continue;

            bool hasValidNeighbor = false;
            foreach (var neighborCoord in HexGridMath.GetNeighbors(coord))
            {
                if (activeChunks.TryGetValue(neighborCoord, out var neighborData))
                {
                    if (neighborData.baseState == ChunkState.Outskirts || neighborData.baseState == ChunkState.Settled)
                    {
                        hasValidNeighbor = true;
                        break;
                    }
                }
            }

            if (!hasValidNeighbor) continue;

            if (data.isRoadChunk && data.hasRoadPredecessor)
            {
                if (activeChunks.TryGetValue(data.roadPredecessor, out var predData))
                {
                    if (predData.baseState != ChunkState.Outskirts && predData.baseState != ChunkState.Settled)
                    {
                        continue;
                    }
                }
                else
                {
                    continue;
                }
            }

            toBorderlands.Add(coord);
        }

        foreach (var coord in toBorderlands)
        {
            SetChunkBaseState(coord, ChunkState.Borderlands);
        }
    }

    public bool TryStartSurvey(Vector2Int coord, int daysRequired)
    {
        if (!activeChunks.TryGetValue(coord, out var data)) return false;
        if (data.baseState != ChunkState.Borderlands) return false;

        data.surveyDaysRemaining = daysRequired;
        SetChunkBaseState(coord, ChunkState.Surveying);
        return true;
    }

    public void RegisterOutpost(OutpostEntity outpost, Vector2Int coord, int daysToSettle)
    {
        if (!activeChunks.TryGetValue(coord, out var data)) return;

        data.activeOutpost = outpost;
        data.outpostSettlementDaysRemaining = daysToSettle;

        OnChunkStateChanged?.Invoke(coord, data.EffectiveState);
    }

    public void UnregisterOutpost(OutpostEntity outpost, Vector2Int coord)
    {
        if (!activeChunks.TryGetValue(coord, out var data)) return;
        if (data.activeOutpost != outpost) return;

        data.activeOutpost = null;

        if (data.baseState == ChunkState.Outskirts)
        {
            DemolishInvalidBuildingsOnChunk(coord);
            OnChunkStateChanged?.Invoke(coord, data.EffectiveState);
        }
    }

    private void DemolishInvalidBuildingsOnChunk(Vector2Int chunkCoord)
    {
        if (BuildingRegistry.Instance == null) return;

        var allBuildings = new List<BuildingEntity>(BuildingRegistry.Instance.AllBuildings);
        foreach (var building in allBuildings)
        {
            if (building == null) continue;

            HexCell cell = building.GetComponentInParent<HexCell>();
            if (cell != null && cell.chunkCoord == chunkCoord)
            {
                if (building.data.type != BuildingType.Defense && !building.data.isOutpost)
                {
                    building.Demolish();
                }
            }
        }
    }

    public void SetChunkBaseState(Vector2Int coord, ChunkState newState)
    {
        if (!activeChunks.TryGetValue(coord, out var data)) return;

        data.baseState = newState;
        OnChunkStateChanged?.Invoke(coord, data.EffectiveState);
    }

    public ChunkState GetEffectiveState(Vector2Int coord)
    {
        if (activeChunks.TryGetValue(coord, out var data))
        {
            return data.EffectiveState;
        }
        return ChunkState.Wilderness;
    }

    public ChunkStateData GetChunkData(Vector2Int coord)
    {
        activeChunks.TryGetValue(coord, out var data);
        return data;
    }
}