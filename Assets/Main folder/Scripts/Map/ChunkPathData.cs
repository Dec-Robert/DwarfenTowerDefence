using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Dane ścieżki przez pojedynczy chunk.
///
/// UWAGA: Ta klasa zastępuje uproszczony struct ChunkPathData z HexGridMath.cs.
/// Usuń struct ChunkPathData z HexGridMath.cs i zostaw tylko tę klasę.
/// </summary>
public class ChunkPathData
{
    /// <summary>Heks wejściowy (skąd wchodzi ścieżka).</summary>
    public Vector2Int entryHex;

    /// <summary>Dodatkowe wejścia (dla węzłów z wieloma gałęziami).</summary>
    public List<Vector2Int> extraEntries = new List<Vector2Int>();

    /// <summary>Heks wyjściowy (dokąd wychodzi ścieżka).</summary>
    public Vector2Int exitHex;

    /// <summary>Lokalne heksy tworzące ścieżkę wewnątrz chunku.</summary>
    public List<Vector2Int> internalPath;

    /// <summary>
    /// Który chunk jest następny w stronę bazy.
    /// Wartość sentinel (-999, -999) oznacza brak połączenia.
    /// </summary>
    public Vector2Int nextChunkTowardsBase = new Vector2Int(-999, -999);

    /// <summary>Zwraca wszystkie wejścia (główne + dodatkowe).</summary>
    public List<Vector2Int> GetAllEntries()
    {
        var list = new List<Vector2Int> { entryHex };
        list.AddRange(extraEntries);
        return list;
    }
}