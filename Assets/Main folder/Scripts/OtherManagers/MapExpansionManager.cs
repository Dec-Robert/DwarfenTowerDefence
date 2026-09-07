using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

/// <summary>
/// Main coordinator for the map expansion system. Subscribes to morning ticks from TimePhaseManager
/// to manage scouting mission durations and natural settlement in chunks. Provides API for Outposts
/// to instantly unlock territories and initializes chunk states at the start of the game.
/// </summary>
public class MapExpansionManager : MonoBehaviour
{
    public static MapExpansionManager Instance { get; private set; }
    //List of all chunks in world
    private List<ChunkRoadState> worldData;
    
    //List of modifications going on
    private List<ChunkTransformationState> ChunkTransformationStates = new List<ChunkTransformationState>();
    private Dictionary<int, List<Vector2Int>> roads;
    public event Action<Vector2Int, ChunkState> OnChunkStateChange;
    

    public void Initialize(Dictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>> chunkData,List<Vector2Int> initialChunks)
    {
        Dictionary<Vector2Int, Vector2Int> roadChunks = HexMapGenerator.Instance.GetRoadRevealDependencies();
        foreach (var c in chunkData)
        {
            Vector2Int chunk = c.Key;
            ChunkRoadState _chunkRoadState = new ChunkRoadState();
            _chunkRoadState.chunkCoords = chunk;
            if (initialChunks.Contains(chunk))
            {
                _chunkRoadState.state = ChunkState.Settled;
            }
            else
            {
                _chunkRoadState.state = ChunkState.Wilderness;
            }

            
        }
    }

    private void InitializeRoadsMaps()
    {
        List<List<Vector2Int>> finalRoads = new List<List<Vector2Int>>();
        
        Dictionary<Vector2Int,Vector2Int> roadDependencies = HexMapGenerator.Instance.GetRoadRevealDependencies();
        List<Vector2Int> roadStarts = new List<Vector2Int>();

        foreach (var transition in roadDependencies)
        {
            if (!roadDependencies.ContainsValue(transition.Key) && transition.Value != Vector2Int.zero)
            {
                roadStarts.Add(transition.Value);
            }
        }

        foreach (var start in roadStarts)
        {
            Vector2Int tmp = start;
            List<Vector2Int> road = new List<Vector2Int>();
            road.Add(tmp);
            
            while (tmp != Vector2Int.zero)
            {
                tmp = roadDependencies[tmp];
                road.Add(tmp);
            }
            finalRoads.Add(road);
            
        }

        Dictionary<Vector2Int,int> coordWithIndexes = new Dictionary<Vector2Int,int>();
        int roadStrenght = 0;
        foreach (var road in finalRoads)
        {
            int roadIndexer = 0;
            Vector2Int prevRoad = Vector2Int.zero;
            foreach (var coords in road)
            {

                coordWithIndexes[coords] = roadIndexer* 10 * (int)Math.Pow(10,roadStrenght);
                roadIndexer++;
                
            }

            roadStrenght++;
        }
        
    }



    private void ChangeChunkState(Vector2Int chunkCoords, ChunkState newState)
    {
        /*
        worldData[chunkCoords] = newState;
        OnChunkStateChange?.Invoke(chunkCoords, newState);
        */
    }

    private void UpdateAllWildernessChunks()
    {
        
    }
    private void UpdateWildernessChunk(Vector2Int chunkCoords)
    {
        List<Vector2Int> avaibleToMod = new List<Vector2Int>();
 
    }

    private void updateRoadState()
    {
        
    }
}