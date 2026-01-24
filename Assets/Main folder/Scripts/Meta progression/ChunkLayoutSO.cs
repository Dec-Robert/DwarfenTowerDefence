using UnityEngine;
using System.Collections.Generic;

public enum PredefinedChunkType
{
    StartingCenter, // (0,0)
    Expansion       // (-1,0) itp.
}

[System.Serializable]
public struct HexOverride
{
    public Vector2Int localCoord;
    public HexFeatureType feature;
    public int featureLevel;
    [Header("Opcjonalny Budynek Startowy")]
    public BuildingData building; // <--- NOWE POLE
}

[CreateAssetMenu(fileName = "NewChunkLayout", menuName = "Map/Predefined Chunk Layout")]
public class ChunkLayoutSO : ScriptableObject
{
    public PredefinedChunkType type;

    [Header("Uk³ad Heksów")]
    public List<HexOverride> hexes;

    [Header("Punkt BRAMY (Tylko dla StartingCenter)")]
    [Tooltip("Koordynaty heksa na krawêdzi chunku, do którego ma doprowadziæ droga ze œwiata.")]
    public Vector2Int roadConnectionEdge;
}