using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class HexPathfinder
{
    private int chunkRadius;

    public HexPathfinder(int chunkRadius)
    {
        this.chunkRadius = chunkRadius;
    }

    // --- POPRAWIONY A* DLA CHUNKÓW (Global) ---
    // Teraz przyjmuje list� dost�pnych chunk�w (validChunks) i map� koszt�w (costs)
    public List<Vector2Int> FindChunkPath(
        Vector2Int startChunk,
        Vector2Int goalChunk,
        HashSet<Vector2Int> validChunks,
        Dictionary<Vector2Int, int> chunkCosts)
    {
        var openSet = new List<Vector2Int> { startChunk };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, int> { [startChunk] = 0 };

        while (openSet.Count > 0)
        {
            // Wybieramy w�ze� o najni�szym koszcie fScore (g + h)
            Vector2Int current = openSet.OrderBy(x =>
                (gScore.ContainsKey(x) ? gScore[x] : int.MaxValue) + HexGridMath.GetDistance(x, goalChunk)
            ).First();

            if (current == goalChunk) return ReconstructPath(cameFrom, current);

            openSet.Remove(current);

            foreach (var neighbor in HexGridMath.GetNeighbors(current))
            {
                // 1. POPRAWKA: Sprawdzamy, czy chunk faktycznie istnieje na mapie
                if (!validChunks.Contains(neighbor)) continue;

                // Blokada bazy (chyba �e to cel)
                if (neighbor == Vector2Int.zero && goalChunk != Vector2Int.zero) continue;

                // 2. POPRAWKA: Dodajemy koszt chunku (�eby wymusi� zakr�canie)
                int moveCost = (chunkCosts != null && chunkCosts.ContainsKey(neighbor)) ? chunkCosts[neighbor] : 1;

                int tentativeG = gScore[current] + moveCost;

                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    if (!openSet.Contains(neighbor)) openSet.Add(neighbor);
                }
            }
        }
        return new List<Vector2Int>(); // Brak �cie�ki
    }

    // --- A* DLA HEKS�W (Local) ---
    public List<Vector2Int> FindLocalPath(Vector2Int start, Vector2Int goal, Dictionary<Vector2Int, int> costMap)
    {
        var openSet = new List<Vector2Int> { start };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, int> { [start] = 0 };

        while (openSet.Count > 0)
        {
            Vector2Int current = openSet.OrderBy(x => gScore[x] + HexGridMath.GetDistance(x, goal)).First();

            if (current == goal) return ReconstructPath(cameFrom, current);

            openSet.Remove(current);

            foreach (var neighbor in HexGridMath.GetNeighbors(current))
            {
                int dist = HexGridMath.GetDistance(Vector2Int.zero, neighbor);
                if (dist > chunkRadius) continue;
                if (dist == chunkRadius && neighbor != start && neighbor != goal) continue;

                int moveCost = (costMap != null && costMap.ContainsKey(neighbor)) ? costMap[neighbor] : 1;
                int tentativeG = gScore[current] + moveCost;

                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    if (!openSet.Contains(neighbor)) openSet.Add(neighbor);
                }
            }
        }
        return new List<Vector2Int>();
    }

    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var path = new List<Vector2Int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }
}