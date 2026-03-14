using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Zarządza systemem murów i bram na granicy odkrytego terytorium.
///
/// ─── TRYBY (WallSystemMode) ──────────────────────────────────────────────────
///
///   Solution1_BaseOnly
///     Mury tylko wokół chunków startowych (chunk 0,0 + meta-chunki).
///     Mury są statyczne — pojawiają się raz na starcie gry.
///
///   Solution2_FrontLine
///     "Żywa linia frontu":
///     • Mury zewnętrzne otaczają całe odkryte miasto (FullyUnlocked chunki).
///     • Wewnątrz miasta wzdłuż dróg stoją mniejsze mury korytarzowe
///       (na hexach sąsiadujących z hexami drogi) — TYLKO dla niegraniczych chunków.
///     • Na granicy ostatniego odkrytego chunku drogi i terytorium za nim
///       stoi Brama z HP.
///     • Gdy gracz odkrywa kolejny chunk: stara brama → mur, nowa brama głębiej.
///
/// ─── SETUP SCENY ─────────────────────────────────────────────────────────────
///   1. Przeciągnij ten komponent na dowolny pusty GameObject na scenie.
///   2. Przypisz referencje w Inspectorze.
///   3. Ustaw prefaby: wallPrefab, corridorWallPrefab, gatePrefab.
///   4. Aktywny tryb ustawiany jest przez MetaUpgrade przez SetMode().
///
/// </summary>
public class ChunkWallManager : MonoBehaviour
{
    public static ChunkWallManager Instance { get; private set; }

    // =========================================================================
    // REFERENCJE
    // =========================================================================

    [Header("── Referencje ────────────────────────────────")]
    public MapExpansionManager expansionManager;
    public HexMapGenerator     mapGenerator;

    [Header("── Prefaby ──────────────────────────────────")]
    [Tooltip("Mur zewnętrzny — grubszy, wyższy")]
    public GameObject wallPrefab;

    [Tooltip("Mur korytarzowy wzdłuż drogi — mniejszy dla czytelności")]
    public GameObject corridorWallPrefab;

    [Tooltip("Brama — byt z HP, stoi na granicy chunków")]
    public GameObject gatePrefab;

    [Header("── Ustawienia ───────────────────────────────")]
    [Tooltip("Wysokość (Y) na jakiej spawniemy mury i bramy")]
    public float wallHeightOffset = 0.1f;
    
    public bool debug = false;

    // =========================================================================
    // STAN WEWNĘTRZNY
    // =========================================================================

    private WallSystemMode currentMode = WallSystemMode.Disabled;

    /// <summary>Wszystkie aktywne obiekty murów (spawned, nie-brama).</summary>
    private readonly List<GameObject> activeWalls = new List<GameObject>();

    /// <summary>Aktywne bramy — klucz to (chunkBliżejBazy, chunkFrontier) posortowane.</summary>
    private readonly Dictionary<(Vector2Int, Vector2Int), GateEntity> activeGates
        = new Dictionary<(Vector2Int, Vector2Int), GateEntity>();

    /// <summary>Lookup: globalny axial hex → chunkCoord. Budowany raz po inicjalizacji mapy.</summary>
    private Dictionary<Vector2Int, Vector2Int> globalHexToChunk
        = new Dictionary<Vector2Int, Vector2Int>();

    // Cache parametrów mapy
    private int   chunkRadius;
    private float hexSize;
    private float padding;

    // Chunki startowe (base + meta) — używane w Solution1
    private HashSet<Vector2Int> baseChunks = new HashSet<Vector2Int>();

    // =========================================================================
    // UNITY
    // =========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {

        if (expansionManager != null)
        {
            expansionManager.OnMapInitialized          += HandleMapInitialized;
            expansionManager.OnChunkBecameFullyUnlocked += HandleChunkFullyUnlocked;
        }
    }

    private void OnDestroy()
    {
        if (expansionManager != null)
        {
            expansionManager.OnMapInitialized          -= HandleMapInitialized;
            expansionManager.OnChunkBecameFullyUnlocked -= HandleChunkFullyUnlocked;
        }
    }

    // =========================================================================
    // PUBLICZNE API
    // =========================================================================

    /// <summary>
    /// Ustawia tryb systemu murów. Wywołaj z MetaUpgrade po zakupieniu Solution 2.
    /// Automatycznie przebudowuje wszystkie mury.
    /// </summary>
    public void SetMode(WallSystemMode mode)
    {
        if (currentMode == mode) return;
        currentMode = mode;
        Debug.Log($"[WallSystem] Tryb zmieniony na: {mode}");
        RebuildAll();
    }

    public WallSystemMode CurrentMode => currentMode;

    // =========================================================================
    // HANDLERY EVENTÓW
    // =========================================================================

    private void HandleMapInitialized(List<Vector2Int> startChunks)
    {
        // Cache parametrów mapy
        chunkRadius = mapGenerator.chunkRadius;
        hexSize     = mapGenerator.hexSize;
        padding     = mapGenerator.padding;

        // Zapamiętaj chunki startowe (Solution 1 ich używa)
        baseChunks = new HashSet<Vector2Int>(startChunks);

        // Zbuduj reverse lookup: globalAxialHex → chunkCoord
        BuildGlobalHexLookup();

        if(debug) SetMode(WallSystemMode.Solution2_FrontLine);
        
        // Postaw mury wg aktualnego trybu
        RebuildAll();
    }

    private void HandleChunkFullyUnlocked(Vector2Int coord)
    {
        if (currentMode == WallSystemMode.Disabled) return;

        // W obu trybach wystarczy przebudować wszystko:
        // mapa nie jest duża, a logika jest zależna od globalnego stanu
        RebuildAll();
    }

    // =========================================================================
    // GŁÓWNA LOGIKA PRZEBUDOWY
    // =========================================================================

    /// <summary>Usuwa wszystkie mury/bramy i buduje od nowa wg aktualnego trybu.</summary>
    private void RebuildAll()
    {
        ClearAll();

        if (currentMode == WallSystemMode.Disabled) return;
        if (mapGenerator == null || expansionManager == null) return;
        if (mapGenerator.worldData == null) return;

        switch (currentMode)
        {
            case WallSystemMode.Solution1_BaseOnly:
                BuildSolution1();
                break;

            case WallSystemMode.Solution2_FrontLine:
                BuildSolution2();
                break;
        }
    }

    // =========================================================================
    // SOLUTION 1 — TYLKO BAZA STARTOWA
    // =========================================================================

    private void BuildSolution1()
    {
        // Otaczamy murem tylko startowe chunki (0,0 i ekspansje meta)
        foreach (var chunk in baseChunks)
        {
            PlaceOuterWallsForChunk(chunk, excludeNeighborChunks: baseChunks);
        }

        // Bramy na wyjściu z bazy startowej
        foreach (var kvp in expansionManager.roadDependencies)
        {
            Vector2Int successor = kvp.Key;   
            Vector2Int predecessor = kvp.Value; 

            // Jeśli droga wychodzi z bazy startowej na zewnątrz
            if (baseChunks.Contains(predecessor) && !baseChunks.Contains(successor))
            {
                PlaceGateOnBoundary(predecessor, successor);
            }
        }
    }

    // =========================================================================
    // SOLUTION 2 — DYNAMICZNA LINIA FRONTU
    // =========================================================================

    private void BuildSolution2()
    {
        var fullyUnlocked = GetFullyUnlockedChunks();
        if (fullyUnlocked.Count == 0) return;

        var roadChunks     = GetRoadChunks();
        var frontierChunks = FindFrontierChunks(fullyUnlocked, roadChunks);

        // Zbiór chunków, które BĘDĄ otoczone murem (czyli FullyUnlocked BEZ Frontierów)
        var walledCityChunks = new HashSet<Vector2Int>(fullyUnlocked);
        foreach (var f in frontierChunks)
        {
            walledCityChunks.Remove(f);
        }

        // 1. Mury zewnętrzne wokół całego miasta (ignorujemy Frontiery)
        foreach (var chunk in walledCityChunks)
        {
            PlaceOuterWallsForChunk(chunk, excludeNeighborChunks: walledCityChunks);
        }

        // 2. Bramy na granicach walledCityChunks -> frontierChunks
        foreach (var frontier in frontierChunks)
        {
            if (!expansionManager.roadDependencies.TryGetValue(frontier, out var predecessor))
                continue;

            // Sprawdź czy poprzednik należy do otoczonego miasta
            if (!walledCityChunks.Contains(predecessor)) continue;

            PlaceGateOnBoundary(predecessor, frontier);
        }
    }
    // =========================================================================
    // MURY ZEWNĘTRZNE
    // =========================================================================

    /// <summary>
    /// Dla każdego hexa w danym chunku, jeśli przynajmniej jeden jego sąsiad
    /// jest w chunku SPOZA excludeSet (lub poza mapą) → postaw mur zewnętrzny
    /// na midpoincie krawędzi między tym hexem a sąsiadem.
    /// </summary>
    /// <summary>
    /// Dla każdego hexa w danym chunku, jeśli przynajmniej jeden jego sąsiad
    /// jest w chunku SPOZA excludeSet (lub poza mapą) → postaw mur zewnętrzny
    /// na midpoincie krawędzi między tym hexem a sąsiadem.
    /// Z wyjątkiem ścieżek prowadzących do Frontiera (tam stanie brama).
    /// </summary>
    private void PlaceOuterWallsForChunk(Vector2Int chunkCoord, HashSet<Vector2Int> excludeNeighborChunks)
    {
        if (!mapGenerator.worldData.ContainsKey(chunkCoord)) return;

        var chunkData = mapGenerator.worldData[chunkCoord];

        foreach (var kvp in chunkData)
        {
            Vector2Int localCoord = kvp.Key;
            Vector2Int globalCoord = LocalToGlobal(chunkCoord, localCoord);
            Vector3    hexWorldPos = GetHexWorldPos(chunkCoord, localCoord);
            bool       isPath     = kvp.Value.isPath; // Sprawdzamy czy TEN heks to droga

            // Sprawdź 6 sąsiadów w globalnej siatce
            foreach (var neighborGlobal in HexGridMath.GetNeighbors(globalCoord))
            {
                // Do jakiego chunku należy sąsiad?
                Vector2Int neighborChunk;
                bool neighborInMap = globalHexToChunk.TryGetValue(neighborGlobal, out neighborChunk);

                // Jeśli sąsiad jest w tym samym chunku — skip
                if (neighborInMap && neighborChunk == chunkCoord) continue;

                // Jeśli sąsiad jest w chunku należącym do otoczonego miasta — skip (wspólna bezpieczna granica)
                if (neighborInMap && excludeNeighborChunks.Contains(neighborChunk)) continue;

                // --- NOWE: ZROBIENIE DZIURY NA BRAMĘ ---
                if (isPath && neighborInMap)
                {
                    Vector2Int neighborLocal = GlobalToLocal(neighborChunk, neighborGlobal);
                    if (mapGenerator.worldData.TryGetValue(neighborChunk, out var neighborData))
                    {
                        if (neighborData.TryGetValue(neighborLocal, out var neighborCell) && neighborCell.isPath)
                        {
                            continue; // Zostaw dziurę!
                        }
                    }
                }
                // ----------------------------------------

                // Miejsce muru = midpoint między tym hexem a sąsiadem
                Vector3 neighborWorldPos = neighborInMap
                    ? GetHexWorldPos(neighborChunk, GlobalToLocal(neighborChunk, neighborGlobal))
                    : EstimateWorldPosOutsideMap(neighborGlobal);

                Vector3 wallPos = (hexWorldPos + neighborWorldPos) * 0.5f;
                wallPos.y = wallHeightOffset;

                Quaternion wallRot = GetEdgeRotation(hexWorldPos, neighborWorldPos);
                SpawnWall(wallPrefab, wallPos, wallRot);
            }
        }
    }

    // =========================================================================
    // MURY KORYTARZOWE
    // =========================================================================

    /// <summary>
    /// Dla każdego hexa drogi w chunku, stawia mały mur na każdym
    /// sąsiednim hexie który NIE jest hexem drogi (i jest w tym samym chunku).
    /// Nie stawia muru tam gdzie już stoi mur zewnętrzny (granica chunku).
    /// </summary>
    private void PlaceCorridorWallsForChunk(Vector2Int chunkCoord)
    {
        if (!mapGenerator.worldData.ContainsKey(chunkCoord)) return;

        var chunkData = mapGenerator.worldData[chunkCoord];

        foreach (var kvp in chunkData)
        {
            if (!kvp.Value.isPath) continue;   // tylko hexy drogi

            Vector2Int pathLocal  = kvp.Key;
            Vector2Int pathGlobal = LocalToGlobal(chunkCoord, pathLocal);

            foreach (var neighborGlobal in HexGridMath.GetNeighbors(pathGlobal))
            {
                // Sąsiad musi być w tym samym chunku
                if (!globalHexToChunk.TryGetValue(neighborGlobal, out var neighborChunk)) continue;
                if (neighborChunk != chunkCoord) continue;

                Vector2Int neighborLocal = GlobalToLocal(chunkCoord, neighborGlobal);
                if (!chunkData.TryGetValue(neighborLocal, out var neighborCell)) continue;

                // Nie stawiamy na hexach drogi (wrogowie muszą przejść!)
                if (neighborCell.isPath) continue;

                Vector3 wallPos = GetHexWorldPos(chunkCoord, neighborLocal);
                wallPos.y = wallHeightOffset;

                SpawnWall(corridorWallPrefab, wallPos, Quaternion.identity);
            }
        }
    }
// =========================================================================
    // BRAMA (NA KRAWĘDZI HEKSÓW)
    // =========================================================================

    /// <summary>
    /// Szuka heksów drogi na granicy dwóch chunków i stawia bramę 
    /// dokładnie na krawędzi pomiędzy nimi (jak mur zewnętrzny).
    /// </summary>
    private void PlaceGateOnBoundary(Vector2Int insideChunk, Vector2Int outsideChunk)
    {
        var key = insideChunk.x < outsideChunk.x || (insideChunk.x == outsideChunk.x && insideChunk.y < outsideChunk.y)
            ? (insideChunk, outsideChunk) : (outsideChunk, insideChunk);

        if (activeGates.ContainsKey(key)) return;
        if (gatePrefab == null) return;

        if (!mapGenerator.worldData.ContainsKey(insideChunk)) return;

        var insideData = mapGenerator.worldData[insideChunk];

        foreach (var kvp in insideData)
        {
            // Szukamy tylko heksów, które są ścieżką
            if (!kvp.Value.isPath) continue;

            Vector2Int localCoord = kvp.Key;
            Vector2Int globalCoord = LocalToGlobal(insideChunk, localCoord);

            // Sprawdzamy sąsiadów tego heksa w poszukiwaniu heksa z outsideChunk, który też jest ścieżką
            foreach (var neighborGlobal in HexGridMath.GetNeighbors(globalCoord))
            {
                if (!globalHexToChunk.TryGetValue(neighborGlobal, out var neighborChunk)) continue;
                if (neighborChunk != outsideChunk) continue; // To nie ten chunk

                Vector2Int neighborLocal = GlobalToLocal(outsideChunk, neighborGlobal);
                
                if (mapGenerator.worldData.TryGetValue(outsideChunk, out var outsideData))
                {
                    if (outsideData.TryGetValue(neighborLocal, out var neighborCell) && neighborCell.isPath)
                    {
                        // ZNALEZIONO GRANICĘ DROGI!
                        Vector3 insideHexPos = GetHexWorldPos(insideChunk, localCoord);
                        Vector3 outsideHexPos = GetHexWorldPos(outsideChunk, neighborLocal);

                        // Pozycja = idealny środek między dwoma heksami (krawędź)
                        Vector3 gatePos = (insideHexPos + outsideHexPos) * 0.5f;
                        gatePos.y = wallHeightOffset;

                        // Rotacja = prostopadle do wektora łączącego heksy
                        Quaternion gateRot = GetEdgeRotation(insideHexPos, outsideHexPos);

                        var gateObj = Instantiate(gatePrefab, gatePos, gateRot, transform);
                        var gateEntity = gateObj.GetComponent<GateEntity>();

                        if (gateEntity != null)
                        {
                            gateEntity.chunkA = insideChunk;
                            gateEntity.chunkB = outsideChunk;
                            activeGates[key] = gateEntity;
                        }
                        else
                        {
                            activeWalls.Add(gateObj); // Fallback
                        }

                        Debug.Log($"[WallSystem] Brama postawiona na krawędzi drogi między {insideChunk} a {outsideChunk}");
                        return; // Brama postawiona, kończymy szukanie dla tej pary chunków
                    }
                }
            }
        }
    }
    // =========================================================================
    // BRAMA
    // =========================================================================

    /// <summary>
    /// Stawia bramę na midpoincie między centrami dwóch chunków.
    /// Orientacja: prostopadle do linii A→B (czyli "w poprzek drogi").
    /// Wymaganie: chunk B (frontier) musi być FullyUnlocked.
    /// </summary>
    private void PlaceGateBetween(Vector2Int chunkA, Vector2Int chunkB)
    {
        // Klucz — kolejność nie ma znaczenia (para nieuporządkowana)
        var key = chunkA.x < chunkB.x || (chunkA.x == chunkB.x && chunkA.y < chunkB.y)
            ? (chunkA, chunkB) : (chunkB, chunkA);

        if (activeGates.ContainsKey(key))
        {
            Debug.LogWarning($"[WallSystem] Brama {chunkA}↔{chunkB} już istnieje.");
            return;
        }

        if (gatePrefab == null)
        {
            Debug.LogWarning("[WallSystem] Brak prefabu bramy!");
            return;
        }

        // Pozycja: midpoint między hexami drogi na granicy, fallback = midpoint centrów
        Vector3 posA = FindBoundaryPathMidpoint(chunkA, chunkB, out Vector3 posB);
        Vector3 gatePos = (posA + posB) * 0.5f;
        gatePos.y = wallHeightOffset;

        // Rotacja: brama "w poprzek" linii A→B
        Quaternion gateRot = GetEdgeRotation(posA, posB);

        var gateObj    = Instantiate(gatePrefab, gatePos, gateRot, transform);
        var gateEntity = gateObj.GetComponent<GateEntity>();

        if (gateEntity != null)
        {
            gateEntity.chunkA = chunkA;
            gateEntity.chunkB = chunkB;
            activeGates[key]  = gateEntity;
        }
        else
        {
            Debug.LogWarning("[WallSystem] Prefab bramy nie ma komponentu GateEntity!");
            activeWalls.Add(gateObj);   // Traktuj jak zwykły mur
        }

        Debug.Log($"[WallSystem] Brama postawiona między {chunkA} a {chunkB}");
    }

    /// <summary>
    /// Szuka pary hexów drogi na granicy między chunkA i chunkB.
    /// Zwraca world pos hexa po stronie chunkA; posB to world pos po stronie chunkB.
    /// Fallback: centra chunków.
    /// </summary>
    private Vector3 FindBoundaryPathMidpoint(Vector2Int chunkA, Vector2Int chunkB, out Vector3 posB)
    {
        if (mapGenerator.worldData.ContainsKey(chunkA) &&
            mapGenerator.worldData.ContainsKey(chunkB))
        {
            var dataA = mapGenerator.worldData[chunkA];
            var dataB = mapGenerator.worldData[chunkB];

            foreach (var kvpA in dataA)
            {
                if (!kvpA.Value.isPath) continue;
                Vector2Int globalA = LocalToGlobal(chunkA, kvpA.Key);

                foreach (var neighborGlobal in HexGridMath.GetNeighbors(globalA))
                {
                    if (!globalHexToChunk.TryGetValue(neighborGlobal, out var nc)) continue;
                    if (nc != chunkB) continue;

                    Vector2Int localB = GlobalToLocal(chunkB, neighborGlobal);
                    if (!dataB.TryGetValue(localB, out var cellB)) continue;
                    if (!cellB.isPath) continue;

                    // Znaleziono parę granicznych hexów drogi!
                    posB = GetHexWorldPos(chunkB, localB);
                    return GetHexWorldPos(chunkA, kvpA.Key);
                }
            }
        }

        // Fallback: centra chunków
        posB = HexGridMath.GetChunkCenterWorld(chunkB, chunkRadius, hexSize, padding);
        return HexGridMath.GetChunkCenterWorld(chunkA, chunkRadius, hexSize, padding);
    }

    // =========================================================================
    // POMOCNICZE — ANALIZA STANU MAPY
    // =========================================================================

    private HashSet<Vector2Int> GetFullyUnlockedChunks()
    {
        return new HashSet<Vector2Int>(
            expansionManager.activeChunks
                .Where(kvp => kvp.Value.state == ChunkState.FullyUnlocked)
                .Select(kvp => kvp.Key));
    }

    private HashSet<Vector2Int> GetRoadChunks()
    {
        return new HashSet<Vector2Int>(expansionManager.roadDependencies.Keys);
    }

    /// <summary>
    /// Chunki "frontier" = FullyUnlocked road chunki bez następnika w FU zbiorze.
    /// Czyli: ostatni odkryty chunk na każdej gałęzi drogi.
    /// </summary>
    private HashSet<Vector2Int> FindFrontierChunks(
        HashSet<Vector2Int> fullyUnlocked,
        HashSet<Vector2Int> roadChunks)
    {
        // Zbuduj mapę: predecessor → lista następników
        var successors = new Dictionary<Vector2Int, List<Vector2Int>>();
        foreach (var kvp in expansionManager.roadDependencies)
        {
            var chunk = kvp.Key;   // następnik
            var pred  = kvp.Value; // poprzednik
            if (!successors.ContainsKey(pred))
                successors[pred] = new List<Vector2Int>();
            successors[pred].Add(chunk);
        }

        var frontier = new HashSet<Vector2Int>();

        foreach (var chunk in roadChunks)
        {
            if (!fullyUnlocked.Contains(chunk)) continue;

            // Frontier = brak następnika który jest FullyUnlocked
            bool hasFullyUnlockedSuccessor = successors.TryGetValue(chunk, out var succs)
                && succs.Any(s => fullyUnlocked.Contains(s));

            if (!hasFullyUnlockedSuccessor)
                frontier.Add(chunk);
        }

        return frontier;
    }

    // =========================================================================
    // POMOCNICZE — MATEMATYKA HEX
    // =========================================================================

    /// <summary>Konwertuje (chunkCoord, localCoord) na globalny axial.</summary>
    private Vector2Int LocalToGlobal(Vector2Int chunkCoord, Vector2Int localCoord)
    {
        int cq = chunkCoord.x, cr = chunkCoord.y, R = chunkRadius;
        int centerQ = cq * (2 * R + 1) + cr * R;
        int centerR = cq * (-R)         + cr * (R + 1);
        return new Vector2Int(centerQ + localCoord.x, centerR + localCoord.y);
    }

    /// <summary>Konwertuje globalny axial → localCoord w danym chunku.</summary>
    private Vector2Int GlobalToLocal(Vector2Int chunkCoord, Vector2Int globalCoord)
    {
        int cq = chunkCoord.x, cr = chunkCoord.y, R = chunkRadius;
        int centerQ = cq * (2 * R + 1) + cr * R;
        int centerR = cq * (-R)         + cr * (R + 1);
        return new Vector2Int(globalCoord.x - centerQ, globalCoord.y - centerR);
    }

    /// <summary>World position hexa z (chunkCoord, localCoord).</summary>
    private Vector3 GetHexWorldPos(Vector2Int chunkCoord, Vector2Int localCoord)
    {
        Vector3 chunkCenter = HexGridMath.GetChunkCenterWorld(chunkCoord, chunkRadius, hexSize, padding);
        return chunkCenter + HexGridMath.AxialToWorld(localCoord.x, localCoord.y, hexSize, padding);
    }

    /// <summary>Szacuje world pos hexa który jest poza mapą (używane dla murów zewnętrznych).</summary>
    private Vector3 EstimateWorldPosOutsideMap(Vector2Int globalAxial)
    {
        // Nie znamy chunku — użyj globalnego AxialToWorld bezpośrednio
        // (niedokładne dla mapy z offsetami chunków, ale wystarczające do midpointu)
        return HexGridMath.AxialToWorld(globalAxial.x, globalAxial.y, hexSize, padding);
    }

    /// <summary>
    /// Rotacja obiektu tak, żeby był prostopadły do linii from→to
    /// (czyli "stał w poprzek ścieżki").
    /// </summary>
    private Quaternion GetEdgeRotation(Vector3 from, Vector3 to)
    {
        Vector3 dir = (to - from).normalized;
        if (dir == Vector3.zero) return Quaternion.identity;
        return Quaternion.LookRotation(dir) * Quaternion.Euler(0, 90, 0);
    }

    // =========================================================================
    // POMOCNICZE — BUDOWANIE LOOKUP
    // =========================================================================

    private void BuildGlobalHexLookup()
    {
        globalHexToChunk.Clear();
        foreach (var chunkKvp in mapGenerator.worldData)
        {
            var chunkCoord = chunkKvp.Key;
            foreach (var localCoord in chunkKvp.Value.Keys)
            {
                var globalCoord = LocalToGlobal(chunkCoord, localCoord);
                globalHexToChunk[globalCoord] = chunkCoord;
            }
        }
        Debug.Log($"[WallSystem] Global hex lookup zbudowany: {globalHexToChunk.Count} hexów.");
    }

    // =========================================================================
    // SPAWN / DESTROY
    // =========================================================================

    private void SpawnWall(GameObject prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return;
        var obj = Instantiate(prefab, pos, rot, transform);
        activeWalls.Add(obj);
    }

    private void ClearAll()
    {
        foreach (var wall in activeWalls)
            if (wall != null) Destroy(wall);
        activeWalls.Clear();

        foreach (var kvp in activeGates)
            if (kvp.Value != null) Destroy(kvp.Value.gameObject);
        activeGates.Clear();
    }

    // =========================================================================
    // DEBUG
    // =========================================================================

#if UNITY_EDITOR
    [ContextMenu("Debug: Rebuild Walls")]
    private void DebugRebuild() => RebuildAll();

    [ContextMenu("Debug: Switch to Solution2")]
    private void DebugSolution2() => SetMode(WallSystemMode.Solution2_FrontLine);

    [ContextMenu("Debug: Disable Walls")]
    private void DebugDisable() => SetMode(WallSystemMode.Disabled);
#endif
}