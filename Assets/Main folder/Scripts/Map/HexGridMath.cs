using UnityEngine;
using System.Collections.Generic;

public static class HexGridMath
{
    // Zwraca liste 6 sasiadow dla danego hexa
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

    // Konwersja koordynatow Axial (q, r) na pozycje w swiecie 3D
    public static Vector3 AxialToWorld(int q, int r, float size, float padding)
    {
        float totalSize = size + padding;
        float x = totalSize * Mathf.Sqrt(3) * (q + r / 2f);
        float z = totalSize * 3f / 2f * r;
        return new Vector3(x, 0, z);
    }

    // Oblicza srodek chunku w swiecie
    public static Vector3 GetChunkCenterWorld(Vector2Int chunkCoord, int chunkRadius, float size, float padding)
    {
        int cq = chunkCoord.x;
        int cr = chunkCoord.y;
        int centerQ = cq * (2 * chunkRadius + 1) + cr * chunkRadius;
        int centerR = cq * -chunkRadius + cr * (chunkRadius + 1);
        return AxialToWorld(centerQ, centerR, size, padding);
    }

    // Zwraca globalne koordynaty axial srodka chunku (ta sama formula co GetChunkCenterWorld).
    // Globalny heks = GetChunkCenterAxial(chunk) + localCoord.
    public static Vector2Int GetChunkCenterAxial(Vector2Int chunkCoord, int chunkRadius)
    {
        int cq = chunkCoord.x;
        int cr = chunkCoord.y;
        return new Vector2Int(
            cq * (2 * chunkRadius + 1) + cr * chunkRadius,
            cq * -chunkRadius + cr * (chunkRadius + 1));
    }

    public static Vector2Int LocalToGlobal(Vector2Int chunkCoord, Vector2Int localCoord, int chunkRadius)
    {
        int r = chunkRadius > 0 ? chunkRadius : 3;
        return GetChunkCenterAxial(chunkCoord, r) + localCoord;
    }

    /// <summary>
    /// Konwertuje globalny heks (axial) na koordynaty chunku i koordynaty lokalne w chunku.
    /// Zwraca dokladna przynaleznosc heksa do chunku dla dowolnego promienia chunku.
    /// </summary>
    public static void GlobalToChunkAndLocal(Vector2Int globalCoord, int chunkRadius, out Vector2Int chunkCoord, out Vector2Int localCoord)
    {
        int R = chunkRadius > 0 ? chunkRadius : 3;
        int det = 3 * R * R + 3 * R + 1;

        float cqFrac = ((R + 1f) * globalCoord.x - R * globalCoord.y) / det;
        float crFrac = (R * globalCoord.x + (2f * R + 1f) * globalCoord.y) / det;

        float x = cqFrac;
        float z = crFrac;
        float y = -x - z;

        int rx = Mathf.RoundToInt(x);
        int ry = Mathf.RoundToInt(y);
        int rz = Mathf.RoundToInt(z);

        float xDiff = Mathf.Abs(rx - x);
        float yDiff = Mathf.Abs(ry - y);
        float zDiff = Mathf.Abs(rz - z);

        if (xDiff > yDiff && xDiff > zDiff)
        {
            rx = -ry - rz;
        }
        else if (yDiff > zDiff)
        {
            ry = -rx - rz;
        }
        else
        {
            rz = -rx - ry;
        }

        chunkCoord = new Vector2Int(rx, rz);
        Vector2Int centerAxial = GetChunkCenterAxial(chunkCoord, R);
        localCoord = globalCoord - centerAxial;
    }

    /// <summary>
    /// Zwraca wszystkie sasiadujace heksy w zadanym promieniu, poprawnie mapujac przekroczenia granic chunkow.
    /// </summary>
    public static List<NeighborHexInfo> GetHexesInRadiusCrossChunk(Vector2Int centerChunk, Vector2Int centerLocal, int radius, int chunkRadius)
    {
        var results = new List<NeighborHexInfo>();
        int r = chunkRadius > 0 ? chunkRadius : 3;
        Vector2Int centerGlobal = LocalToGlobal(centerChunk, centerLocal, r);

        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Mathf.Max(-radius, -q - radius);
            int r2 = Mathf.Min(radius, -q + radius);
            for (int rCoord = r1; rCoord <= r2; rCoord++)
            {
                Vector2Int g = new Vector2Int(centerGlobal.x + q, centerGlobal.y + rCoord);
                GlobalToChunkAndLocal(g, r, out var cCoord, out var lCoord);
                results.Add(new NeighborHexInfo
                {
                    chunkCoord = cCoord,
                    localCoord = lCoord,
                    globalCoord = g
                });
            }
        }
        return results;
    }

    /// <summary>
    /// Zwraca 6 bezposrednich sasiadow heksa, poprawnie mapujac przekroczenia granic chunkow.
    /// </summary>
    public static List<NeighborHexInfo> GetNeighborsCrossChunk(Vector2Int centerChunk, Vector2Int centerLocal, int chunkRadius)
    {
        var results = new List<NeighborHexInfo>();
        int r = chunkRadius > 0 ? chunkRadius : 3;
        Vector2Int centerGlobal = LocalToGlobal(centerChunk, centerLocal, r);

        foreach (var nGlobal in GetNeighbors(centerGlobal))
        {
            GlobalToChunkAndLocal(nGlobal, r, out var cCoord, out var lCoord);
            results.Add(new NeighborHexInfo
            {
                chunkCoord = cCoord,
                localCoord = lCoord,
                globalCoord = nGlobal
            });
        }
        return results;
    }

    /// <summary>
    /// Sprawdza, czy dany chunk jest odkryty (Outskirts lub Settled).
    /// Gdy brak managerow ekspansji/mgly (np. testy jednostkowe), zwraca true.
    /// </summary>
    public static bool IsChunkRevealed(Vector2Int chunkCoord)
    {
        if (MapExpansionManager.Instance != null)
        {
            var state = MapExpansionManager.Instance.GetEffectiveState(chunkCoord);
            return state == ChunkState.Outskirts || state == ChunkState.Settled;
        }
        if (FogOfWarManager.Instance != null)
        {
            return FogOfWarManager.Instance.IsChunkRevealed(chunkCoord);
        }
        return true;
    }
}

public struct NeighborHexInfo
{
    public Vector2Int chunkCoord;
    public Vector2Int localCoord;
    public Vector2Int globalCoord;
}
