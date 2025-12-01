using UnityEngine;
using System.Collections.Generic;

// Struktura danych dostêpna dla wszystkich skryptów
[System.Serializable]
public struct ChunkPathData
{
    public Vector2Int entryHex;
    public Vector2Int exitHex;
    public List<Vector2Int> internalPath;
}

public static class HexGridMath
{
    // Zwraca listê 6 s¹siadów dla danego hexa
    public static List<Vector2Int> GetNeighbors(Vector2Int hex)
    {
        return new List<Vector2Int>
        {
            new Vector2Int(hex.x + 1, hex.y),
            new Vector2Int(hex.x + 1, hex.y - 1),
            new Vector2Int(hex.x, hex.y - 1),
            new Vector2Int(hex.x - 1, hex.y),
            new Vector2Int(hex.x - 1, hex.y + 1),
            new Vector2Int(hex.x, hex.y + 1)
        };
    }

    // Oblicza dystans w siatce heksagonalnej
    public static int GetDistance(Vector2Int a, Vector2Int b)
    {
        return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.x + a.y - b.x - b.y) + Mathf.Abs(a.y - b.y)) / 2;
    }

    // Konwersja koordynatów Axial (q, r) na pozycjê w œwiecie 3D
    public static Vector3 AxialToWorld(int q, int r, float size, float padding)
    {
        float totalSize = size + padding;
        float x = totalSize * Mathf.Sqrt(3) * (q + r / 2f);
        float z = totalSize * 3f / 2f * r;
        return new Vector3(x, 0, z);
    }

    // Oblicza œrodek chunku w œwiecie
    public static Vector3 GetChunkCenterWorld(Vector2Int chunkCoord, int chunkRadius, float size, float padding)
    {
        int cq = chunkCoord.x;
        int cr = chunkCoord.y;
        int centerQ = cq * (2 * chunkRadius + 1) + cr * chunkRadius;
        int centerR = cq * -chunkRadius + cr * (chunkRadius + 1);
        return AxialToWorld(centerQ, centerR, size, padding);
    }
}