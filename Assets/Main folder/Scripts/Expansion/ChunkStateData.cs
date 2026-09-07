using UnityEngine;

[System.Serializable]
public class ChunkStateData
{
    public ChunkState state = ChunkState.Wilderness;
    public bool hasOutpost = false;
    public bool isRoadChunk = false;
    public int discoveredOnDay = -1;
    
    
}