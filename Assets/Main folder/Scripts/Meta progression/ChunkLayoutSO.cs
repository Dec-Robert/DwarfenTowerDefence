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

    [Header("Układ Heks�w")]
    public List<HexOverride> hexes;

    [Header("Punkt BRAMY (Tylko dla StartingCenter)")]
    [Tooltip("Koordynaty heksa na kraw�dzi chunku, do kt�rego ma doprowadzi� droga ze �wiata.")]
    public Vector2Int roadConnectionEdge;
}