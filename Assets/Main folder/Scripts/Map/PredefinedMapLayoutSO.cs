using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Referencja do konkretnego heksa na predefiniowanej planszy (współrzędne chunku + współrzędne lokalne wewnątrz chunku).
/// </summary>
[Serializable]
public struct PredefinedHexRef : IEquatable<PredefinedHexRef>
{
    public Vector2Int chunkCoord;
    public Vector2Int localCoord;

    public PredefinedHexRef(Vector2Int chunk, Vector2Int local)
    {
        chunkCoord = chunk;
        localCoord = local;
    }

    public bool Equals(PredefinedHexRef other)
        => chunkCoord == other.chunkCoord && localCoord == other.localCoord;

    public override bool Equals(object obj)
        => obj is PredefinedHexRef other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(chunkCoord, localCoord);

    public override string ToString()
        => $"Chunk ({chunkCoord.x}, {chunkCoord.y}), Local ({localCoord.x}, {localCoord.y})";
}

/// <summary>
/// Dane pojedynczego heksa na predefiniowanej mapie.
/// </summary>
[Serializable]
public class PredefinedHexData
{
    public Vector2Int localCoord;
    public HexFeatureType feature = HexFeatureType.None;
    public int featureLevel = 0;
    public int elevation = 0;
    public bool isRoad = false;
    public BuildingData startingBuilding;

    public PredefinedHexData Clone()
    {
        return new PredefinedHexData
        {
            localCoord = this.localCoord,
            feature = this.feature,
            featureLevel = this.featureLevel,
            elevation = this.elevation,
            isRoad = this.isRoad,
            startingBuilding = this.startingBuilding
        };
    }
}

/// <summary>
/// Dane pojedynczego chunku wchodzącego w skład predefiniowanej mapy.
/// </summary>
[Serializable]
public class PredefinedChunkData
{
    public Vector2Int chunkCoord;
    [Tooltip("Czy ten chunk ma być odkryty i rozliczony jako początkowa strefa gracza (brak mgły wojny)?")]
    public bool isUnlockedAtStart = false;
    public List<PredefinedHexData> hexes = new List<PredefinedHexData>();

    public PredefinedHexData GetHex(Vector2Int localCoord)
    {
        if (hexes == null) return null;
        return hexes.Find(h => h.localCoord == localCoord);
    }

    public PredefinedHexData GetOrCreateHex(Vector2Int localCoord)
    {
        if (hexes == null) hexes = new List<PredefinedHexData>();
        var found = hexes.Find(h => h.localCoord == localCoord);
        if (found == null)
        {
            found = new PredefinedHexData { localCoord = localCoord };
            hexes.Add(found);
        }
        return found;
    }

    public void RemoveHex(Vector2Int localCoord)
    {
        if (hexes == null) return;
        hexes.RemoveAll(h => h.localCoord == localCoord);
    }
}

/// <summary>
/// ScriptableObject przechowujący w pełni predefiniowany layout planszy (np. dla pierwszej gry / misji tutorialowej).
/// Zawiera wszystkie zdefiniowane chunki, cechy terenu, kafelki dróg, punkty baz/spawnerów oraz stan mgły wojny.
/// </summary>
[CreateAssetMenu(fileName = "NewPredefinedMapLayout", menuName = "Map/Predefined Map Layout")]
public class PredefinedMapLayoutSO : ScriptableObject
{
    [Header("Informacje o Mapie")]
    public string mapName = "Tutorial Map";
    [TextArea(2, 4)]
    public string description = "Predefiniowana plansza dla pierwszej rozgrywki gracza.";
    public int chunkRadius = 3;

    [Header("Punkty Strategiczne")]
    [Tooltip("Lokalizacja Głównej Bazy gracza (Ratusza/Beacon of Hope).")]
    public PredefinedHexRef playerBase = new PredefinedHexRef(Vector2Int.zero, Vector2Int.zero);
    public BuildingData startingBaseBuilding;

    [Tooltip("Punkty wejścia / spawnu wrogów na krawędziach lub wewnątrz chunków spawnerów.")]
    public List<PredefinedHexRef> enemySpawners = new List<PredefinedHexRef>();

    [Header("Chunki")]
    public List<PredefinedChunkData> chunks = new List<PredefinedChunkData>();

    // =========================================================================
    // WYSZUKIWANIE I MANIPULACJA CHUNKAMI
    // =========================================================================

    public PredefinedChunkData GetChunk(Vector2Int chunkCoord)
    {
        if (chunks == null) return null;
        return chunks.Find(c => c.chunkCoord == chunkCoord);
    }

    public PredefinedChunkData GetOrCreateChunk(Vector2Int chunkCoord, bool unlockedAtStart = false)
    {
        if (chunks == null) chunks = new List<PredefinedChunkData>();
        var chunk = chunks.Find(c => c.chunkCoord == chunkCoord);
        if (chunk == null)
        {
            chunk = new PredefinedChunkData
            {
                chunkCoord = chunkCoord,
                isUnlockedAtStart = unlockedAtStart,
                hexes = new List<PredefinedHexData>()
            };
            chunks.Add(chunk);
        }
        return chunk;
    }

    public bool RemoveChunk(Vector2Int chunkCoord)
    {
        if (chunks == null) return false;
        return chunks.RemoveAll(c => c.chunkCoord == chunkCoord) > 0;
    }

    public PredefinedHexData GetHex(Vector2Int chunkCoord, Vector2Int localCoord)
    {
        var chunk = GetChunk(chunkCoord);
        return chunk?.GetHex(localCoord);
    }

    // =========================================================================
    // WALIDACJA UKŁADU MAPY
    // =========================================================================

    /// <summary>
    /// Przeprowadza pełną walidację mapy:
    /// - Sprawdza obecność bazy i spawnerów.
    /// - Weryfikuje ciągłość dróg ze spawnerów do bazy za pomocą BFS.
    /// </summary>
    public bool ValidateMap(out List<string> errors, out List<string> warnings)
    {
        errors = new List<string>();
        warnings = new List<string>();

        if (chunks == null || chunks.Count == 0)
        {
            errors.Add("Mapa nie zawiera żadnych chunków!");
            return false;
        }

        // 1. Sprawdzenie Bazy
        var baseChunk = GetChunk(playerBase.chunkCoord);
        if (baseChunk == null)
        {
            errors.Add($"Baza gracza odwołuje się do nieistniejącego chunku {playerBase.chunkCoord}.");
        }
        else
        {
            var baseHex = baseChunk.GetHex(playerBase.localCoord);
            if (baseHex == null)
            {
                warnings.Add($"Heks bazy gracza {playerBase} nie ma jeszcze zdefiniowanych danych w chunku.");
            }
        }

        // 2. Sprawdzenie Spawnerów
        if (enemySpawners == null || enemySpawners.Count == 0)
        {
            errors.Add("Mapa nie ma zdefiniowanego ani jednego spawnera wrogów (enemySpawners jest puste).");
        }
        else
        {
            for (int i = 0; i < enemySpawners.Count; i++)
            {
                var s = enemySpawners[i];
                var sChunk = GetChunk(s.chunkCoord);
                if (sChunk == null)
                {
                    errors.Add($"Spawner #{i + 1} ({s}) znajduje się w nieistniejącym chunku {s.chunkCoord}.");
                }
                else
                {
                    var sHex = sChunk.GetHex(s.localCoord);
                    if (sHex != null && !sHex.isRoad)
                    {
                        warnings.Add($"Spawner #{i + 1} ({s}) nie leży bezpośrednio na heksie oznaczonym jako Droga (isRoad).");
                    }
                }
            }
        }

        // 3. Sprawdzenie ciągłości drogi ze spawnerów do bazy
        if (baseChunk != null && enemySpawners != null && enemySpawners.Count > 0)
        {
            // Budujemy mapę sąsiedztwa heksów drogowych
            var roadGraph = BuildRoadAdjacency();
            Vector2Int baseWorldHex = PredefinedToGlobalHex(playerBase.chunkCoord, playerBase.localCoord, chunkRadius);

            for (int i = 0; i < enemySpawners.Count; i++)
            {
                var s = enemySpawners[i];
                Vector2Int spawnerWorldHex = PredefinedToGlobalHex(s.chunkCoord, s.localCoord, chunkRadius);

                if (!HasPathBFS(roadGraph, spawnerWorldHex, baseWorldHex))
                {
                    warnings.Add($"Brak nieprzerwanej ścieżki z heksów drogi (isRoad) od Spawnera #{i + 1} ({s}) do Bazy ({playerBase})!");
                }
            }
        }

        return errors.Count == 0;
    }

    // =========================================================================
    // GEOMETRIA I NAWIGACJA
    // =========================================================================

    public static Vector2Int PredefinedToGlobalHex(Vector2Int chunkCoord, Vector2Int localCoord, int radius)
    {
        int cq = chunkCoord.x;
        int cr = chunkCoord.y;
        int centerQ = cq * (2 * radius + 1) + cr * radius;
        int centerR = cq * -radius + cr * (radius + 1);
        return new Vector2Int(centerQ + localCoord.x, centerR + localCoord.y);
    }

    public static Vector3 GetHexWorldPosition(Vector2Int chunkCoord, Vector2Int localCoord, int radius, float hexSize, float padding)
    {
        Vector3 chunkCenter = HexGridMath.GetChunkCenterWorld(chunkCoord, radius, hexSize, padding);
        Vector3 localPos = HexGridMath.AxialToWorld(localCoord.x, localCoord.y, hexSize, padding);
        return chunkCenter + localPos;
    }

    private Dictionary<Vector2Int, List<Vector2Int>> BuildRoadAdjacency()
    {
        var roadHexes = new HashSet<Vector2Int>();
        var globalToRef = new Dictionary<Vector2Int, PredefinedHexRef>();

        if (chunks != null)
        {
            foreach (var c in chunks)
            {
                if (c.hexes == null) continue;
                foreach (var h in c.hexes)
                {
                    if (h.isRoad)
                    {
                        Vector2Int g = PredefinedToGlobalHex(c.chunkCoord, h.localCoord, chunkRadius);
                        roadHexes.Add(g);
                        globalToRef[g] = new PredefinedHexRef(c.chunkCoord, h.localCoord);
                    }
                }
            }
        }

        // Dodajemy bazę i spawny do grafu drogowego, jeśli jeszcze ich tam nie ma
        Vector2Int baseG = PredefinedToGlobalHex(playerBase.chunkCoord, playerBase.localCoord, chunkRadius);
        roadHexes.Add(baseG);

        if (enemySpawners != null)
        {
            foreach (var s in enemySpawners)
            {
                Vector2Int sG = PredefinedToGlobalHex(s.chunkCoord, s.localCoord, chunkRadius);
                roadHexes.Add(sG);
            }
        }

        var graph = new Dictionary<Vector2Int, List<Vector2Int>>();
        foreach (var hex in roadHexes)
        {
            graph[hex] = new List<Vector2Int>();
            foreach (var neighbor in HexGridMath.GetNeighbors(hex))
            {
                if (roadHexes.Contains(neighbor))
                {
                    graph[hex].Add(neighbor);
                }
            }
        }

        return graph;
    }

    private bool HasPathBFS(Dictionary<Vector2Int, List<Vector2Int>> graph, Vector2Int start, Vector2Int goal)
    {
        if (!graph.ContainsKey(start) || !graph.ContainsKey(goal)) return false;
        if (start == goal) return true;

        var visited = new HashSet<Vector2Int> { start };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == goal) return true;

            if (graph.TryGetValue(cur, out var neighbors))
            {
                foreach (var n in neighbors)
                {
                    if (visited.Add(n))
                    {
                        queue.Enqueue(n);
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Wyznacza trasy w przestrzeni świata 3D z każdego spawnera do bazy gracza,
    /// wykorzystując heksy dróg. Gotowe do przekazania do EnemySpawner.
    /// </summary>
    public List<List<Vector3>> CalculateSpawnPathsWorld(float hexSize, float padding)
    {
        var result = new List<List<Vector3>>();
        if (enemySpawners == null || enemySpawners.Count == 0) return result;

        var graph = BuildRoadAdjacency();
        Vector2Int baseG = PredefinedToGlobalHex(playerBase.chunkCoord, playerBase.localCoord, chunkRadius);

        // Tworzymy mapę z powrotem z Global -> Ref
        var globalToRef = new Dictionary<Vector2Int, PredefinedHexRef>();
        if (chunks != null)
        {
            foreach (var c in chunks)
            {
                if (c.hexes == null) continue;
                foreach (var h in c.hexes)
                {
                    Vector2Int g = PredefinedToGlobalHex(c.chunkCoord, h.localCoord, chunkRadius);
                    globalToRef[g] = new PredefinedHexRef(c.chunkCoord, h.localCoord);
                }
            }
        }
        globalToRef[baseG] = playerBase;

        foreach (var spawner in enemySpawners)
        {
            Vector2Int startG = PredefinedToGlobalHex(spawner.chunkCoord, spawner.localCoord, chunkRadius);
            globalToRef[startG] = spawner;

            var pathG = FindShortestPath(graph, startG, baseG);
            if (pathG != null && pathG.Count > 0)
            {
                var worldPoints = new List<Vector3>();
                foreach (var gHex in pathG)
                {
                    if (globalToRef.TryGetValue(gHex, out var r))
                    {
                        worldPoints.Add(GetHexWorldPosition(r.chunkCoord, r.localCoord, chunkRadius, hexSize, padding));
                    }
                }
                result.Add(worldPoints);
            }
        }

        return result;
    }

    private List<Vector2Int> FindShortestPath(Dictionary<Vector2Int, List<Vector2Int>> graph, Vector2Int start, Vector2Int goal)
    {
        if (!graph.ContainsKey(start) || !graph.ContainsKey(goal)) return null;

        var visited = new HashSet<Vector2Int> { start };
        var parent = new Dictionary<Vector2Int, Vector2Int>();
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        bool found = false;
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == goal)
            {
                found = true;
                break;
            }

            if (graph.TryGetValue(cur, out var neighbors))
            {
                foreach (var n in neighbors)
                {
                    if (visited.Add(n))
                    {
                        parent[n] = cur;
                        queue.Enqueue(n);
                    }
                }
            }
        }

        if (!found) return null;

        var path = new List<Vector2Int>();
        var curr = goal;
        while (curr != start)
        {
            path.Add(curr);
            curr = parent[curr];
        }
        path.Add(start);
        path.Reverse();
        return path;
    }
}
