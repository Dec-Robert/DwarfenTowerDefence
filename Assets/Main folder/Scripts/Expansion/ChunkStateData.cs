using System;
using UnityEngine;

[Serializable]
public class ChunkStateData
{
    public Vector2Int chunkCoord;
    public ChunkState baseState = ChunkState.Wilderness;
    public OutpostEntity activeOutpost;
    public bool isRoadChunk;
    public Vector2Int roadPredecessor = new Vector2Int(-999, -999);
    public bool hasRoadPredecessor;
    public int surveyDaysRemaining;
    public int outpostSettlementDaysRemaining;

    public ChunkState EffectiveState
    {
        get
        {
            if (baseState == ChunkState.Outskirts && activeOutpost != null)
            {
                return ChunkState.Settled;
            }
            return baseState;
        }
    }

    public bool CanBuildDefense => EffectiveState == ChunkState.Outskirts || EffectiveState == ChunkState.Settled;

    public bool CanBuildEconomic => EffectiveState == ChunkState.Settled;
}