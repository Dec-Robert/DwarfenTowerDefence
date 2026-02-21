using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Główny koordynator systemu ekspansji.
///
/// Hookuje się w GameManager.OnStateChanged:
///   InWave → PreparePhase = "nowy dzień" (tick misji i timerów osadnictwa)
/// </summary>
public class MapExpansionManager : MonoBehaviour
{
    public static MapExpansionManager Instance { get; private set; }

    // =========================================================================
    // REFERENCJE
    // =========================================================================

    [Header("── Referencje ────────────────────────────")]
    public FogOfWarManager     fogManager;
    public HexMapGenerator     mapGenerator;
    public UIExpansionMenu     uiExpansionMenu;
    public ExpansionCostConfig costConfig;

    // =========================================================================
    // STAN
    // =========================================================================

    public Dictionary<Vector2Int, ChunkStateData>  activeChunks
           = new Dictionary<Vector2Int, ChunkStateData>();

    public List<ScoutingMission>                   activeMissions
           = new List<ScoutingMission>();

    private List<OutpostEntity>                    activeOutposts
           = new List<OutpostEntity>();

    public List<ExpeditionCenterEntity>            playerExpeditionCenter
           = new List<ExpeditionCenterEntity>();

    public Dictionary<Vector2Int, Vector2Int>      roadDependencies
           = new Dictionary<Vector2Int, Vector2Int>();

    // =========================================================================
    // INICJALIZACJA
    // =========================================================================

    private void Awake()
    {
        Instance               = this;
        activeChunks           = new Dictionary<Vector2Int, ChunkStateData>();
        activeMissions         = new List<ScoutingMission>();
        activeOutposts         = new List<OutpostEntity>();
        playerExpeditionCenter = new List<ExpeditionCenterEntity>();
        roadDependencies       = new Dictionary<Vector2Int, Vector2Int>();
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged += HandleStateChanged;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    /// <summary>
    /// Wywoływana przez ChunkRevealHandler po wygenerowaniu mapy.
    /// startCoords = startowe odkryte chunki (baza gracza).
    /// </summary>
    public void Initialize(List<Vector2Int> startCoords)
    {
        activeChunks.Clear();
        activeMissions.Clear();
        activeOutposts.Clear();
        playerExpeditionCenter.Clear();

        roadDependencies = mapGenerator.GetRoadRevealDependencies();
        HashSet<Vector2Int> roadChunks = new HashSet<Vector2Int>(roadDependencies.Keys);

        // PRZEBIEG 1: Ustaw wszystkie startowe chunki jako FullyUnlocked i odkryj ich mgłę.
        // Musi sie odbyc PRZED skanowaniem sasiadow, zeby ValidateChunk() widzial poprawny
        // stan wszystkich startowych chunkow – bez tego gateChunk bylby Unlocked w chwili
        // sprawdzania, co powoduje ze drugi chunk drogi dostaje Locked zamiast Unlocked.
        foreach (var coord in startCoords)
        {
            activeChunks[coord] = new ChunkStateData { state = ChunkState.FullyUnlocked };
            // Jawnie odkryj – ChunkRevealHandler mogl wywolac RevealChunk() wczesniej,
            // ale nowy FogOfWarManager wymaga UpdateChunkFogState zeby upewnic sie
            // ze teren jest widoczny (m.in. meta chunki poza 0,0).
            fogManager?.UpdateChunkFogState(coord, ChunkState.FullyUnlocked);
        }

        // PRZEBIEG 2: Teraz bezpiecznie odblokuj sasiadow.
        foreach (var coord in startCoords)
            UnlockNeighbors(coord, roadChunks);
    }

    // =========================================================================
    // TICK – hookujemy w zmianę stanu (każdy koniec fali = nowy "dzień")
    // =========================================================================

    private void HandleStateChanged(GameManager.gameStates newState)
    {
        // Noc → dzień: fala się skończyła, tick misji i timerów
        if (newState == GameManager.gameStates.PreparePhase)
        {
            // WAŻNA KOLEJNOŚĆ: settlement tick PRZED missions tick.
            // Gdyby było odwrotnie: CompleteMission() ustawia MilitaryOnly z days=1,
            // a TickSettlementTimers() w tej samej klatce dekrementuje 1→0 → natychmiast FullyUnlocked.
            // Poprawna kolejność: najpierw tick timerów (dla chunków z poprzednich dni),
            // potem tick misji (nowo odkryte chunki zaczną odliczać od następnej fali).
            TickSettlementTimers();
            TickMissions();
            TickOutposts();
        }
    }

    private void TickMissions()
    {
        var completed = new List<ScoutingMission>();
        foreach (var m in activeMissions)
            if (m.Tick()) completed.Add(m);

        foreach (var m in completed)
        {
            activeMissions.Remove(m);
            CompleteMission(m);
        }
    }

    private void TickSettlementTimers()
    {
        foreach (var kvp in activeChunks)
        {
            var data = kvp.Value;
            if (data.state != ChunkState.MilitaryOnly || data.hasOutpost) continue;

            data.settlementDaysRemaining--;
            if (data.settlementDaysRemaining <= 0)
            {
                data.state = ChunkState.FullyUnlocked;
                fogManager?.UpdateChunkFogState(kvp.Key, ChunkState.FullyUnlocked);
                Debug.Log($"[Expansion] {kvp.Key} w pełni zasiedlony.");
                uiExpansionMenu?.OnChunkFullyUnlocked(kvp.Key);
            }
        }
    }

    private void TickOutposts()
    {
        foreach (var o in activeOutposts)
            o.OnDayPassed();
    }

    // =========================================================================
    // KLIKNIĘCIE NA TEREN
    // =========================================================================

    /// <summary>Wywoływana gdy gracz kliknie na chunk pokryty mgłą.</summary>
    public void OnFogClicked(Vector2Int coord, Vector3 worldPos)
    {
        if (!activeChunks.TryGetValue(coord, out var data))
        {
            Debug.Log("[Expansion] Teren poza zasięgiem ekspansji.");
            return;
        }

        // W każdym przypadku pokazujemy menu – UIExpansionMenu.UpdateContent()
        // czyta stan z managera i samo buduje treść
        uiExpansionMenu?.ShowMenu(coord, worldPos);
    }

    // =========================================================================
    // WYSYŁANIE ZWIADOWCY
    // =========================================================================

    /// <summary>
    /// Wywołaj z UIExpansionMenu.OnBuyClicked().
    /// Zwraca false jeśli nie można wysłać (zajęte centrum, brak zasobów, zły stan).
    /// </summary>
    public bool TrySendScout(Vector2Int targetChunk)
    {
        if (!activeChunks.TryGetValue(targetChunk, out var data) || !data.CanScout)
        {
            Debug.LogWarning("[Expansion] Nieprawidłowy stan chunka.");
            return false;
        }

        // Wolne centrum ekspedycyjne
        var center = playerExpeditionCenter.Find(c => c.CanSendMission);
        if (center == null)
        {
            Debug.LogWarning("[Expansion] Brak wolnego Centrum Ekspedycyjnego!");
            return false;
        }

        // Oblicz koszt
        var cost = ExpansionCostCalculator.Calculate(
            targetChunk, activeChunks, data.isRoadChunk, costConfig);

        // Sprawdź i pobierz zasoby (atomowo przez SpendResources)
        var toSpend = new Dictionary<ResourceType, float>
        {
            { ResourceType.Gold, cost.goldCost },
            { ResourceType.Food, cost.foodCost }
        };

        if (!ResourceManager.Instance.SpendResources(toSpend))
        {
            Debug.LogWarning($"[Expansion] Za mało zasobów: {cost.goldCost}G + {cost.foodCost}F");
            return false;
        }

        // Stwórz i uruchom misję
        var mission = ScoutingMission.Create(
            targetChunk, cost.days, cost.goldCost, cost.foodCost, center);

        center.StartMission(mission);
        activeMissions.Add(mission);
        data.state = ChunkState.Scouting;
        fogManager?.UpdateChunkFogState(targetChunk, ChunkState.Scouting);

        Debug.Log($"[Expansion] Zwiadowca wysłany na {targetChunk}. {cost}");
        uiExpansionMenu?.Hide();
        return true;
    }

    // =========================================================================
    // ZAKOŃCZENIE MISJI
    // =========================================================================

    private void CompleteMission(ScoutingMission mission)
    {
        var coord = mission.targetChunk;
        Debug.Log($"[Expansion] Misja zakończona: {coord}");

        if (activeChunks.TryGetValue(coord, out var data))
        {
            // WAŻNE: Czas osadnictwa musi być obliczony PRZED zmianą stanu na MilitaryOnly.
            // Gdyby BFS działał po zmianie, znajdowałby sam coord jako "odkryty" (dystans 0)
            // i zwracał days=1, przez co chunk przechodziłby do FullyUnlocked po jednej fali.
            int settlementDays = CalculateSettlementTime(coord);
            data.state = ChunkState.MilitaryOnly;
            data.settlementDaysRemaining = settlementDays;
            data.discoveredOnDay = GameManager.Instance?.waveNumber ?? 0;
        }

        fogManager?.UpdateChunkFogState(coord, ChunkState.MilitaryOnly);
        UnlockNeighbors(coord, new HashSet<Vector2Int>(roadDependencies.Keys));

        mission.sourceCenter?.OnMissionComplete(mission);
        uiExpansionMenu?.OnChunkDiscovered(coord);
    }

    // =========================================================================
    // POSTERUNEK
    // =========================================================================

    public void OnOutpostBuilt(Vector2Int coord)
    {
        if (!activeChunks.TryGetValue(coord, out var data)) return;

        data.hasOutpost = true;
        data.settlementDaysRemaining = 0;

        if (data.state == ChunkState.MilitaryOnly)
        {
            data.state = ChunkState.FullyUnlocked;
            fogManager?.UpdateChunkFogState(coord, ChunkState.FullyUnlocked);
            Debug.Log($"[Expansion] Posterunek odblokował {coord} natychmiastowo.");
            uiExpansionMenu?.OnChunkFullyUnlocked(coord);
        }
    }

    public void OnOutpostReadyToTransform(OutpostEntity outpost, List<TransformOption> options)
    {
        Debug.Log($"[Expansion] Posterunek {outpost.ChunkCoord} gotowy do transformacji.");
        uiExpansionMenu?.ShowTransformOptions(outpost, options);
    }

    public void OnOutpostTransformed(Vector2Int coord)
    {
        var outpost = activeOutposts.Find(o => o.ChunkCoord == coord);
        if (outpost != null) activeOutposts.Remove(outpost);
        if (activeChunks.TryGetValue(coord, out var data)) data.hasOutpost = false;
    }

    public void RegisterOutpost(OutpostEntity outpost)
    {
        if (!activeOutposts.Contains(outpost)) activeOutposts.Add(outpost);
    }

    public void UnregisterOutpost(OutpostEntity outpost)
        => activeOutposts.Remove(outpost);

    // =========================================================================
    // CENTRA EKSPEDYCYJNE
    // =========================================================================

    public void RegisterExpeditionCenter(ExpeditionCenterEntity center)
    {
        if (!playerExpeditionCenter.Contains(center))
            playerExpeditionCenter.Add(center);
    }

    public void UnregisterExpeditionCenter(ExpeditionCenterEntity center)
        => playerExpeditionCenter.Remove(center);

    // =========================================================================
    // SĄSIEDZI I ODBLOKOWANIE
    // =========================================================================

    private void UnlockNeighbors(Vector2Int origin, HashSet<Vector2Int> roadChunks)
    {
        foreach (var neighbor in HexGridMath.GetNeighbors(origin))
        {
            if (activeChunks.ContainsKey(neighbor)) continue;
            if (!mapGenerator.IsChunkInMap(neighbor)) continue;

            var newState = ValidateChunk(neighbor);
            activeChunks[neighbor] = new ChunkStateData
            {
                state       = newState,
                isRoadChunk = roadChunks.Contains(neighbor)
            };
            fogManager?.UpdateChunkFogState(neighbor, newState);
        }
    }

    private ChunkState ValidateChunk(Vector2Int coord)
    {
        if (!roadDependencies.ContainsKey(coord)) return ChunkState.Unlocked;

        Vector2Int prev = roadDependencies[coord];
        if (!activeChunks.TryGetValue(prev, out var prevData)) return ChunkState.Locked;

        return prevData.IsMilitaryAllowed ? ChunkState.Unlocked : ChunkState.Locked;
    }

    private int CalculateSettlementTime(Vector2Int coord)
    {
        var cost = ExpansionCostCalculator.Calculate(coord, activeChunks, false, costConfig);
        return cost.days;
    }

    // =========================================================================
    // QUERY API – używane przez UIExpansionMenu.UpdateContent()
    // =========================================================================

    public bool IsInProcessOfScouting(Vector2Int coord)
        => activeMissions.Exists(m => m.targetChunk == coord);

    public bool IsFullyUnlocked(Vector2Int coord)
        => activeChunks.TryGetValue(coord, out var d) && d.state == ChunkState.FullyUnlocked;

    public bool IsMilitaryAllowed(Vector2Int coord)
        => activeChunks.TryGetValue(coord, out var d) && d.IsMilitaryAllowed;

    public bool IsTooFar(Vector2Int coord)
        => !activeChunks.ContainsKey(coord);

    public bool IsChunkScoutable(Vector2Int coord)
        => activeChunks.TryGetValue(coord, out var d) && d.CanScout;

    public bool IsBlockedByRoad(Vector2Int coord)
    {
        if (!roadDependencies.ContainsKey(coord)) return false;
        Vector2Int prev = roadDependencies[coord];
        return !activeChunks.TryGetValue(prev, out var d) || !d.IsMilitaryAllowed;
    }

    /// <summary>Koszt misji dla UI – wywołaj przed wyświetleniem panelu.</summary>
    public MissionCost GetMissionCost(Vector2Int coord)
    {
        if (!activeChunks.TryGetValue(coord, out var data))
            return default;
        return ExpansionCostCalculator.Calculate(coord, activeChunks, data.isRoadChunk, costConfig);
    }

    /// <summary>Czy jest dostępne centrum ekspedycyjne?</summary>
    public bool HasFreeExpeditionCenter()
        => playerExpeditionCenter.Exists(c => c.CanSendMission);

    /// <summary>Postęp aktywnej misji na chunku (0-1), lub -1 jeśli brak misji.</summary>
    public float GetScoutingProgress(Vector2Int coord)
    {
        var m = activeMissions.Find(x => x.targetChunk == coord);
        if (m == null) return -1f;
        return 1f - (float)m.daysRemaining / m.totalDays;
    }

    // =========================================================================
    // GIZMOS
    // =========================================================================

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || activeChunks == null || mapGenerator == null) return;

        foreach (var kvp in activeChunks)
        {
            Vector3 worldPos = HexGridMath.GetChunkCenterWorld(
                kvp.Key,
                mapGenerator.chunkRadius,
                mapGenerator.hexSize,
                mapGenerator.padding);

            worldPos.y = 0.5f;

            Gizmos.color = kvp.Value.state switch
            {
                ChunkState.FullyUnlocked => new Color(0f, 1f, 0f, 0.25f),
                ChunkState.MilitaryOnly  => new Color(1f, 0.5f, 0f, 0.25f),
                ChunkState.Scouting      => new Color(0f, 0.5f, 1f, 0.25f),
                ChunkState.Unlocked      => new Color(1f, 1f, 0f, 0.2f),
                _                        => new Color(1f, 0f, 0f, 0.12f),
            };

            float chunkRadius = mapGenerator.chunkRadius * mapGenerator.hexSize * 1.5f;
            Gizmos.DrawSphere(worldPos, chunkRadius);

            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, Gizmos.color.a * 2f);
            Gizmos.DrawWireSphere(worldPos, chunkRadius);
        }
    }
#endif
}