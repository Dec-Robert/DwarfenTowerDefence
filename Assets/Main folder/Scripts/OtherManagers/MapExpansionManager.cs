using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Main coordinator for the map expansion system.
/// Manages the state of map chunks, active scouting missions, outposts, and expedition centers.
/// Integrates with the time phase system to process daily updates for missions and settlement timers.
/// </summary>
public class MapExpansionManager : MonoBehaviour
{
    public static MapExpansionManager Instance { get; private set; }

    [Header("Referencje")]
    public FogOfWarManager fogManager;
    public HexMapGenerator mapGenerator;
    public ExpansionCostConfig costConfig;

    public Dictionary<Vector2Int, ChunkStateData> activeChunks = new Dictionary<Vector2Int, ChunkStateData>();
    public List<ScoutingMission> activeMissions = new List<ScoutingMission>();
    private List<OutpostEntity> activeOutposts = new List<OutpostEntity>();
    public List<ExpeditionCenterEntity> playerExpeditionCenter = new List<ExpeditionCenterEntity>();
    public Dictionary<Vector2Int, Vector2Int> roadDependencies = new Dictionary<Vector2Int, Vector2Int>();

    public event System.Action<Vector2Int> OnChunkBecameFullyUnlocked;
    public event System.Action<List<Vector2Int>> OnMapInitialized;

    private void Awake()
    {
        Instance = this;
        activeChunks = new Dictionary<Vector2Int, ChunkStateData>();
        activeMissions = new List<ScoutingMission>();
        activeOutposts = new List<OutpostEntity>();
        playerExpeditionCenter = new List<ExpeditionCenterEntity>();
        roadDependencies = new Dictionary<Vector2Int, Vector2Int>();
    }

    private void Start()
    {
    }

    private void OnDestroy()
    {
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || activeChunks == null || mapGenerator == null) return;

        foreach (var kvp in activeChunks)
        {
            Vector3 worldPos = HexGridMath.GetChunkCenterWorld(
                kvp.Key,
                mapGenerator.chunkRadius,
                mapGenerator.hexSize,
                mapGenerator.padding);

            worldPos.y = 0.5f;

            Gizmos.color = kvp.Value.state switch
            {
                ChunkState.FullyUnlocked => new Color(0f, 1f, 0f, 0.25f),
                ChunkState.MilitaryOnly  => new Color(1f, 0.5f, 0f, 0.25f),
                ChunkState.Scouting      => new Color(0f, 0.5f, 1f, 0.25f),
                ChunkState.Unlocked      => new Color(1f, 1f, 0f, 0.2f),
                _                        => new Color(1f, 0f, 0f, 0.12f),
            };

            float chunkRadius = mapGenerator.chunkRadius * mapGenerator.hexSize * 1.5f;
            Gizmos.DrawSphere(worldPos, chunkRadius);

            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, Gizmos.color.a * 2f);
            Gizmos.DrawWireSphere(worldPos, chunkRadius);
        }
    }
#endif
}