using UnityEngine;
using System.Collections.Generic;

public class MapExpansionManager : MonoBehaviour
{
    [Header("Referencje")]
    public FogOfWarManager fogManager;
    public UIExpansionMenu uiExpansionMenu;
    public HexMapGenerator mapGenerator;

    public Dictionary<Vector2Int, ChunkStateData> activeChunks = new Dictionary<Vector2Int, ChunkStateData>();
    public List<ScoutingMission> activeMissions = new List<ScoutingMission>();
    public List<ExpeditionCenterEntity> playerExpeditionCenter = new List<ExpeditionCenterEntity>();
    public Dictionary<Vector2Int, Vector2Int> roadDependencies = new Dictionary<Vector2Int, Vector2Int>();

    private void Awake()
    {
        // Upewniamy siê, ¿e kolekcje istniej¹ przed u¿yciem
        if (activeChunks == null) activeChunks = new Dictionary<Vector2Int, ChunkStateData>();
        if (activeMissions == null) activeMissions = new List<ScoutingMission>();
        if (playerExpeditionCenter == null) playerExpeditionCenter = new List<ExpeditionCenterEntity>();
        if (roadDependencies == null) roadDependencies = new Dictionary<Vector2Int, Vector2Int>();
    }


    public void Initialize(List<Vector2Int> coords)
    {
        activeChunks.Clear();
        activeMissions.Clear();
        playerExpeditionCenter.Clear();
        roadDependencies.Clear();
        roadDependencies = mapGenerator.GetRoadRevealDependencies();


        foreach (var coord in coords)
        {
            activeChunks.Add(coord, new ChunkStateData { state = ChunkState.FullyUnlocked });
            UnlockNewChunks(coord);

        }
    }

    //Funkcja wywo³ywana po klikniêciu na chunk, sprawdza stan chunka i wyœwietla odpowiedni komunikat
    public void OnFogClicked(Vector2Int coord)
    {
        if (activeChunks.ContainsKey(coord))
        {
            ChunkState chunkState = activeChunks[coord].state;
            switch (chunkState)
            {
                case ChunkState.Locked:
                    Debug.Log("Musisz odkryæ wczeœniejsz¹ drogê");
                    break;
                case ChunkState.Unlocked:
                    Debug.Log("Chunk odkryty. Wymaga zwiadowców do zbadanmia");
                    break;
                case ChunkState.Scouting:
                    Debug.Log("Chunk jest badany przez zwiadowców. Czekaj na zakoñczenie misji");
                    break;
            }

        }
        else Debug.Log("Chunk is too far for now");
    }

    //Funkcja sprawdzaj¹ca stan chunka, zwraca czy chunk jest drog¹
    private ChunkState ValidateChunk(Vector2Int coord)
    {
        if (!roadDependencies.ContainsKey(coord)) return ChunkState.Unlocked; //  Chunk nie zawieraj¹cy drogi
        if (activeChunks.ContainsKey(roadDependencies[coord])) return ChunkState.Unlocked; //Chunk jest drog¹ i wczeœniejsza droga jest odblokowan
        return ChunkState.Locked;   //Chunk jest drog¹ i wczeœniejsza droga jest zablokowana
    }

    //Funkcja wywo³ywana po zakoñczeniu misji, odblokowuje nowe chunki i aktualizuje UI
    private void UnlockNewChunks(Vector2Int coord)
    {
        foreach (var neighbour in HexGridMath.GetNeighbors(coord))
        {
            if (!activeChunks.ContainsKey(neighbour) && mapGenerator.IsChunkInMap(neighbour))
            {
                activeChunks.Add(neighbour, new ChunkStateData { state = ValidateChunk(neighbour) });
                

            }
        }
        fogManager.RevealChunk(coord);
    }

    public bool IsInProcessOfScouting(Vector2Int coord)
    {
        return activeMissions.Exists(m => m.targetChunk == coord);
    }
    public bool IsBlockedByRoad(Vector2Int coord)
    {
        if (!roadDependencies.ContainsKey(coord)) return false;
        Vector2Int previousRoad = roadDependencies[coord];
        ChunkState previousRoadState = activeChunks[previousRoad].state;
        return !(activeChunks.ContainsKey(roadDependencies[coord]) && (previousRoadState == ChunkState.MilitaryOnly || previousRoadState == ChunkState.FullyUnlocked));
    }
    public bool IsTooFar(Vector2Int coord)
    {
        return !activeChunks.ContainsKey(coord);
    }

    public bool IsChunkBuyable(Vector2Int coord)
    {
        if (!activeChunks.ContainsKey(coord)) return false; // Chunk poza map¹
        ChunkState state = activeChunks[coord].state;
        return state == ChunkState.Unlocked;
    }



}