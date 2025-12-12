using UnityEngine;

public class HexCell : MonoBehaviour
{
    public Vector2Int chunkCoord;
    public Vector2Int localCoord;

    public HexCellData GetData()
    {
        return FindObjectOfType<HexMapGenerator>().worldData[chunkCoord][localCoord];
    }
}
