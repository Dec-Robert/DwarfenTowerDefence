using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Losuje i konfiguruje specjalne chunki na mapie.
/// Działa PO wygenerowaniu wszystkich ścieżek (Ścieżka A, pętle, boczne ścieżki i rozdroża).
///
/// Zasady umieszczania (Feature 24):
/// 1. Twardy pierścień odległości od bazy (0,0): dist >= 3 && dist <= 8.
/// 2. Całkowite wykluczenie ścieżek wrogów: chunk nie może być w ctx.chunkPaths ani ctx.generatedChunkSequence.
/// 3. Całkowite wykluczenie pętli: chunk nie może być w ctx.loops (loopChunks, mainSegmentChunks, rozdroża).
/// 4. Całkowite wykluczenie spawnerów i ich bezpośrednich sąsiadów: chunk nie może być w odległości <= 1
///    od żadnego spawnera (ctx.mainSpawnerChunk, ctx.extraSpawnerChunks, ChunkType.Spawner).
/// 5. Pełny determinizm: losowanie wyłącznie za pomocą ctx.Rng (zero UnityEngine.Random),
///    ze stabilnym sortowaniem i tasowaniem ctx.Rng.Shuffle().
/// 6. Poszanowanie unikalności (uniquePerMap) oraz layoutOverride przy aplikacji zasobów.
/// </summary>
public class SpecialChunkPlacer
{
    private readonly MapGenerationContext ctx;
    private readonly ChunkRegistry registry;
    private readonly List<SpecialChunkDefinition> definitions;

    public const int MinSpecialRing = 3;
    public const int MaxSpecialRing = 8;

    public SpecialChunkPlacer(
        MapGenerationContext ctx,
        ChunkRegistry registry,
        List<SpecialChunkDefinition> definitions)
    {
        this.ctx         = ctx;
        this.registry    = registry;
        this.definitions = definitions;
    }

    // =========================================================================
    // API Publiczne
    // =========================================================================

    /// <summary>
    /// Przechodzi przez wszystkie definicje i deterministycznie umieszcza specjalne chunki.
    /// Używa kontekstu przekazanego w konstruktorze.
    /// </summary>
    public void PlaceSpecialChunks()
    {
        PlaceSpecialChunks(this.ctx);
    }

    /// <summary>
    /// Przechodzi przez wszystkie definicje i deterministycznie umieszcza specjalne chunki
    /// w podanym kontekście mapy (wspiera testy headless i wywołania bezpośrednie).
    /// </summary>
    public void PlaceSpecialChunks(MapGenerationContext targetCtx)
    {
        var activeCtx = targetCtx ?? this.ctx;
        if (activeCtx == null)
        {
            SafeLogError("[SpecialChunkPlacer] MapGenerationContext jest null!");
            return;
        }

        if (definitions == null || definitions.Count == 0) return;

        // Upewnij się, że generator pseudolosowy jest zainicjalizowany
        if (activeCtx.Rng == null)
        {
            activeCtx.Initialize(activeCtx.seed > 0 ? activeCtx.seed : 12345);
        }

        var placedUnique = new HashSet<string>();
        var allSpawners = GetAllSpawnerChunks(activeCtx);

        foreach (var definition in definitions)
        {
            if ((object)definition == null) continue;

            string idKey = !string.IsNullOrEmpty(definition.chunkId) ? definition.chunkId : "SpecialChunk";

            // Unique – pomijamy jeśli dany chunkId już został postawiony na tej mapie
            if (definition.uniquePerMap && placedUnique.Contains(idKey))
                continue;

            // Deterministyczny rzut na pojawienie się chunku (0-99)
            int spawnRoll = activeCtx.Rng.Range(0, 100);
            if (spawnRoll >= definition.spawnChance)
                continue;

            // Wyznacz twardy zakres pierścieni [3, 8] z uwzględnieniem konfiguracji definicji
            int minRing = MinSpecialRing;
            int maxRing = MaxSpecialRing;
            if (definition.distanceRange.x > 0 || definition.distanceRange.y > 0)
            {
                minRing = Mathf.Clamp(Mathf.Min(definition.distanceRange.x, definition.distanceRange.y), MinSpecialRing, MaxSpecialRing);
                maxRing = Mathf.Clamp(Mathf.Max(definition.distanceRange.x, definition.distanceRange.y), MinSpecialRing, MaxSpecialRing);
            }

            // Znajdź kwalifikujących się kandydatów spełniających wszystkie twarde reguły
            var candidates = GetEligibleCandidates(activeCtx, minRing, maxRing, allSpawners);

            if (candidates.Count == 0)
            {
                SafeLogWarning($"[SpecialChunkPlacer] Brak prawidłowych kandydatów dla '{idKey}' w pierścieniach [{minRing}, {maxRing}].");
                continue;
            }

            // Stabilne sortowanie współrzędnych (q, r) przed tasowaniem dla gwarancji identycznego seeda na każdej platformie
            candidates.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            // Deterministyczne tasowanie Fishera-Yatesa za pomocą MapRng
            activeCtx.Rng.Shuffle(candidates);

            Vector2Int chosen = candidates[0];
            activeCtx.chunkTypes[chosen] = ChunkType.Special;
            activeCtx.specialChunkData[chosen] = definition;

            if (definition.uniquePerMap)
            {
                placedUnique.Add(idKey);
            }

            SafeLog($"[SpecialChunkPlacer] Chunk specjalny '{idKey}' umieszczony na {chosen} (dystans od bazy: {HexGridMath.GetDistance(Vector2Int.zero, chosen)})");
        }
    }

    /// <summary>
    /// Zwraca definicję specjalnego chunku dla podanych współrzędnych lub null.
    /// </summary>
    public SpecialChunkDefinition GetDefinition(Vector2Int chunkCoord)
    {
        var activeCtx = this.ctx;
        if (activeCtx != null && activeCtx.specialChunkData.TryGetValue(chunkCoord, out var def))
            return def;
        return null;
    }

    // =========================================================================
    // Filtrowanie i Walidacja Kandydatów
    // =========================================================================

    /// <summary>
    /// Zbiera wszystkie chunki spełniające twarde kryteria selekcji:
    /// - Odległość od bazy: dist >= minRing && dist <= maxRing (w przedziale [3, 8])
    /// - Brak kolizji ze ścieżkami (Path A, odnogi boczne, sekwencja chunków)
    /// - Brak kolizji z pętlami (ani w loopChunks, ani w mainSegmentChunks, ani crossroads)
    /// - Brak sąsiedztwa ze spawnerami (odległość > 1 od każdego chunku spawnera)
    /// - Brak kolizji z chunkami bazy, meta oraz wcześniej postawionymi chunkami specjalnymi
    /// </summary>
    public List<Vector2Int> GetEligibleCandidates(
        MapGenerationContext activeCtx,
        int minRing,
        int maxRing,
        HashSet<Vector2Int> allSpawners)
    {
        var eligible = new List<Vector2Int>();

        foreach (var chunk in activeCtx.allValidChunks)
        {
            if (IsChunkEligible(chunk, activeCtx, minRing, maxRing, allSpawners))
            {
                eligible.Add(chunk);
            }
        }

        return eligible;
    }

    private bool IsChunkEligible(
        Vector2Int chunk,
        MapGenerationContext activeCtx,
        int minRing,
        int maxRing,
        HashSet<Vector2Int> allSpawners)
    {
        // 1. Wykluczenie bazy i chunków meta
        if (chunk == Vector2Int.zero) return false;
        if (registry != null && registry.IsMetaChunk(chunk)) return false;

        // 2. Wykluczenie chunków już zajętych (przez inny ChunkType lub inny chunk specjalny)
        if (activeCtx.chunkTypes.ContainsKey(chunk)) return false;
        if (activeCtx.specialChunkData.ContainsKey(chunk)) return false;

        // 3. Twarde ograniczenie pierścienia odległości [3, 8]
        int dist = HexGridMath.GetDistance(Vector2Int.zero, chunk);
        if (dist < minRing || dist > maxRing) return false;

        // 4. Ścisłe wykluczenie ścieżek wrogów
        if (activeCtx.chunkPaths.ContainsKey(chunk)) return false;
        if (activeCtx.generatedChunkSequence != null && activeCtx.generatedChunkSequence.Contains(chunk)) return false;
        if (activeCtx.crossroadsChunks != null && activeCtx.crossroadsChunks.Contains(chunk)) return false;

        // 5. Ścisłe wykluczenie pętli (obejścia pętli oraz omijane odcinki głównej trasy)
        if (activeCtx.loops != null)
        {
            foreach (var loop in activeCtx.loops)
            {
                if (loop == null) continue;
                if (loop.splitCrossroads == chunk || loop.mergeCrossroads == chunk) return false;
                if (loop.loopChunks != null && loop.loopChunks.Contains(chunk)) return false;
                if (loop.mainSegmentChunks != null && loop.mainSegmentChunks.Contains(chunk)) return false;
            }
        }

        // 6. Ścisłe wykluczenie spawnerów oraz chunków bezpośrednio z nimi sąsiadujących (dist <= 1)
        if (allSpawners != null)
        {
            foreach (var spawner in allSpawners)
            {
                if (HexGridMath.GetDistance(chunk, spawner) <= 1)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Zbiera koordynaty wszystkich spawnerów na mapie (spawner główny, odnogi boczne oraz ChunkType.Spawner).
    /// </summary>
    private HashSet<Vector2Int> GetAllSpawnerChunks(MapGenerationContext activeCtx)
    {
        var spawners = new HashSet<Vector2Int>();

        if (activeCtx.mainSpawnerChunk != Vector2Int.zero)
        {
            spawners.Add(activeCtx.mainSpawnerChunk);
        }

        if (activeCtx.extraSpawnerChunks != null)
        {
            foreach (var extra in activeCtx.extraSpawnerChunks)
            {
                if (extra != Vector2Int.zero)
                    spawners.Add(extra);
            }
        }

        if (activeCtx.chunkTypes != null)
        {
            foreach (var kvp in activeCtx.chunkTypes)
            {
                if (kvp.Value == ChunkType.Spawner && kvp.Key != Vector2Int.zero)
                {
                    spawners.Add(kvp.Key);
                }
            }
        }

        return spawners;
    }

    private static void SafeLog(string msg)
    {
        try { Debug.Log(msg); } catch { }
    }

    private static void SafeLogWarning(string msg)
    {
        try { Debug.LogWarning(msg); } catch { }
    }

    private static void SafeLogError(string msg)
    {
        try { Debug.LogError(msg); } catch { }
    }
}