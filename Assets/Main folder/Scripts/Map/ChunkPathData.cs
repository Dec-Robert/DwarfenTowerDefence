using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Dane ścieżki przez pojedynczy chunk.
///
/// UWAGA: Ta klasa zastępuje uproszczony struct ChunkPathData z HexGridMath.cs.
/// Usuń struct ChunkPathData z HexGridMath.cs i zostaw tylko tę klasę.
/// </summary>
[System.Serializable]
public class ChunkPathData
{
    /// <summary>Heks wejściowy (skąd wchodzi ścieżka).</summary>
    public Vector2Int entryHex;

    /// <summary>Dodatkowe wejścia (dla węzłów z wieloma gałęziami lub powrotu pętli).</summary>
    public List<Vector2Int> extraEntries = new List<Vector2Int>();

    /// <summary>Heks wyjściowy (dokąd wychodzi ścieżka w stronę bazy).</summary>
    public Vector2Int exitHex;

    /// <summary>Dodatkowe wyjścia (dla rozdroży rozdzielających ruch, np. odgałęzienie pętli).</summary>
    public List<Vector2Int> extraExits = new List<Vector2Int>();

    /// <summary>Lokalne heksy tworzące ścieżkę wewnątrz chunku.</summary>
    public List<Vector2Int> internalPath;

    /// <summary>
    /// Lista chunków będących bezpośrednimi następcami w stronę bazy.
    /// Zwykły chunk ma dokładnie 1 następcę.
    /// Rozdroże rozgałęziające (np. Rozdroże 2 pętli) ma 2 następców (główna ścieżka A oraz pętla).
    /// </summary>
    public List<Vector2Int> nextChunksTowardsBase = new List<Vector2Int>();

    /// <summary>
    /// Właściwość wstecznej kompatybilności dla kodu oczekującego pojedynczego chunku w stronę bazy.
    /// Getter zwraca pierwszy element z nextChunksTowardsBase lub wartość sentinel (-999, -999) gdy lista jest pusta.
    /// Setter czyści listę i przypisuje podaną wartość (ignorując sentinel (-999, -999)).
    /// </summary>
    public Vector2Int nextChunkTowardsBase
    {
        get => nextChunksTowardsBase != null && nextChunksTowardsBase.Count > 0
            ? nextChunksTowardsBase[0]
            : new Vector2Int(-999, -999);
        set
        {
            if (nextChunksTowardsBase == null)
                nextChunksTowardsBase = new List<Vector2Int>();
            else
                nextChunksTowardsBase.Clear();

            if (value.x != -999 || value.y != -999)
            {
                nextChunksTowardsBase.Add(value);
            }
        }
    }

    /// <summary>
    /// Flaga określająca, czy chunk jest rozdrożem (posiada dodatkowe wejścia lub dodatkowe wyjścia).
    /// </summary>
    public bool isCrossroads => (extraEntries != null && extraEntries.Count > 0) || (extraExits != null && extraExits.Count > 0);

    /// <summary>Zwraca wszystkie wejścia (główne + dodatkowe).</summary>
    public List<Vector2Int> GetAllEntries()
    {
        var list = new List<Vector2Int> { entryHex };
        if (extraEntries != null && extraEntries.Count > 0)
        {
            list.AddRange(extraEntries);
        }
        return list;
    }

    /// <summary>Zwraca wszystkie wyjścia (główne + dodatkowe).</summary>
    public List<Vector2Int> GetAllExits()
    {
        var list = new List<Vector2Int> { exitHex };
        if (extraExits != null && extraExits.Count > 0)
        {
            list.AddRange(extraExits);
        }
        return list;
    }

    /// <summary>
    /// Zwraca wszystkie punkty brzegowe chunku (wszystkie wejścia oraz wszystkie wyjścia).
    /// Wykorzystywane przy wyznaczaniu punktu węzłowego (hubHex) rozdroża Y/T.
    /// </summary>
    public List<Vector2Int> GetAllEndpoints()
    {
        var list = GetAllEntries();
        list.AddRange(GetAllExits());
        return list;
    }

    /// <summary>Bezpiecznie dodaje dodatkowe wejście, o ile jeszcze nie istnieje.</summary>
    public void AddExtraEntry(Vector2Int hex)
    {
        if (extraEntries == null) extraEntries = new List<Vector2Int>();
        if (!extraEntries.Contains(hex)) extraEntries.Add(hex);
    }

    /// <summary>Bezpiecznie dodaje dodatkowe wyjście, o ile jeszcze nie istnieje.</summary>
    public void AddExtraExit(Vector2Int hex)
    {
        if (extraExits == null) extraExits = new List<Vector2Int>();
        if (!extraExits.Contains(hex)) extraExits.Add(hex);
    }

    /// <summary>Bezpiecznie dodaje następcę w stronę bazy, o ile nie jest wartoscią sentinel.</summary>
    public void AddNextChunkTowardsBase(Vector2Int chunk)
    {
        if (chunk.x == -999 && chunk.y == -999) return;
        if (nextChunksTowardsBase == null) nextChunksTowardsBase = new List<Vector2Int>();
        if (!nextChunksTowardsBase.Contains(chunk)) nextChunksTowardsBase.Add(chunk);
    }
}