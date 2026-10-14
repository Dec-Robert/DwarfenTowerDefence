using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MapExpansionManager : MonoBehaviour
{
    public static MapExpansionManager Instance { get; private set; }

    [Header("Konfiguracja Kosztów")]
    public ExpansionCostConfig costConfig;

    public Dictionary<Vector2Int, ChunkStateData> activeChunks = new Dictionary<Vector2Int, ChunkStateData>();
    public Dictionary<Vector2Int, Vector2Int> roadDependencies = new Dictionary<Vector2Int, Vector2Int>();

    // ─── Rejestr Centrów Ekspedycyjnych i Misji ──────────────────────────────
    private readonly List<ExpeditionCenterEntity> expeditionCenters = new List<ExpeditionCenterEntity>();
    public readonly List<ScoutingMission> activeMissions = new List<ScoutingMission>();

    // ─── Zdarzenia ────────────────────────────────────────────────────────────
    public event Action<List<Vector2Int>> OnMapInitialized;
    public event Action<Vector2Int, ChunkState> OnChunkStateChanged;
    public event Action<ScoutingMission> OnMissionStarted;
    public event Action<ScoutingMission> OnMissionCompleted;
    public event Action<int, int> OnEquipmentChanged; // (available, total)
    public event Action<Vector2Int, ChunkReconIntel> OnReconIntelDiscovered;
    public event Action<Vector2Int> OnBorderlandsChunkClicked;

    private FogOfWarManager fogManager;

    // ─── Właściwości Ekwipunku ────────────────────────────────────────────────
    public int TotalEquipment => expeditionCenters.Where(c => c != null && c.isBuildingActive && c.gameObject.activeInHierarchy).Sum(c => c.EquipmentProvided);
    public int UsedEquipment => activeMissions.Sum(m => m.equipmentUsed);
    public int AvailableEquipment => Mathf.Max(0, TotalEquipment - UsedEquipment);
    public IReadOnlyList<ScoutingMission> ActiveMissions => activeMissions;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (costConfig == null)
        {
            costConfig = Resources.Load<ExpansionCostConfig>("ExpansionCostConfig");
            if (costConfig == null)
            {
                costConfig = ScriptableObject.CreateInstance<ExpansionCostConfig>();
            }
        }
    }

    private void Start()
    {
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted += HandleMorningTick;
        }

        fogManager = FindFirstObjectByType<FogOfWarManager>();
        if (fogManager != null)
        {
            fogManager.OnBorderlandsClicked += HandleBorderlandsClicked;
        }
    }

    private void OnDestroy()
    {
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= HandleMorningTick;
        }

        if (fogManager != null)
        {
            fogManager.OnBorderlandsClicked -= HandleBorderlandsClicked;
        }
    }

    private void HandleBorderlandsClicked(Vector2Int coord)
    {
        OnBorderlandsChunkClicked?.Invoke(coord);

        // Jeśli na chunku trwa już ekspedycja lub zwiad: otwórz Centrum z podświetleniem tej wyprawy
        if (activeChunks.TryGetValue(coord, out var chunkData) && chunkData.hasActiveMission)
        {
            CenterInfoPanelControllerUI.Instance?.OpenExpeditionCenter(null, coord);
            return;
        }

        // Płynnie centrujemy kamerę na chunku (FocusOnPoint przesuwa X/Z, nie modyfikując rotacji ani wysokości Y)
        Vector3 chunkPos = ExpeditionCenterWindowUI.GetChunkWorldPosition(coord);
        if (CameraController.Instance != null)
        {
            CameraController.Instance.FocusOnPoint(chunkPos);
        }

        // Otwieramy modal zwiadu/ekspedycji chunka
        CenterInfoPanelControllerUI.Instance?.OpenChunkExpeditionModal(coord);
    }

    // =========================================================================
    // Zarządzanie Budynkami (Expedition Centers)
    // =========================================================================

    public void RegisterExpeditionCenter(ExpeditionCenterEntity center)
    {
        if (center == null || expeditionCenters.Contains(center)) return;
        expeditionCenters.Add(center);
        OnEquipmentChanged?.Invoke(AvailableEquipment, TotalEquipment);
    }

    public void UnregisterExpeditionCenter(ExpeditionCenterEntity center)
    {
        if (center == null) return;
        expeditionCenters.Remove(center);
        OnEquipmentChanged?.Invoke(AvailableEquipment, TotalEquipment);
    }

    public bool CanDemolishCenter(ExpeditionCenterEntity center)
    {
        if (center == null) return true;
        int remainingTotal = TotalEquipment - center.EquipmentProvided;
        return remainingTotal >= UsedEquipment;
    }

    // =========================================================================
    // Inicjalizacja Mapy
    // =========================================================================

    public void Initialize(IReadOnlyDictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>> worldData, List<Vector2Int> initialSettledChunks = null)
    {
        activeChunks.Clear();
        roadDependencies.Clear();
        activeMissions.Clear();

        if (HexMapGenerator.Instance != null)
        {
            roadDependencies = HexMapGenerator.Instance.GetRoadRevealDependencies();
        }

        HashSet<Vector2Int> startSettled = new HashSet<Vector2Int>();
        if (initialSettledChunks != null)
        {
            foreach (var c in initialSettledChunks)
            {
                startSettled.Add(c);
            }
        }
        else
        {
            startSettled.Add(Vector2Int.zero);
        }

        foreach (var chunkCoord in worldData.Keys)
        {
            ChunkStateData data = new ChunkStateData
            {
                chunkCoord = chunkCoord,
                baseState = startSettled.Contains(chunkCoord) ? ChunkState.Settled : ChunkState.Wilderness
            };

            if (roadDependencies.ContainsKey(chunkCoord))
            {
                data.isRoadChunk = true;
                data.roadPredecessor = roadDependencies[chunkCoord];
                data.hasRoadPredecessor = data.roadPredecessor.x != -999;
            }

            activeChunks.Add(chunkCoord, data);
        }

        ChunkState initialRoadState = ChunkState.Settled;
        List<Vector2Int> initialRoadChunks = new List<Vector2Int>();

        foreach (var kvp in activeChunks)
        {
            ChunkStateData data = kvp.Value;
            if (!data.isRoadChunk || !data.hasRoadPredecessor) continue;

            if (startSettled.Contains(data.roadPredecessor) && !startSettled.Contains(kvp.Key))
            {
                initialRoadChunks.Add(kvp.Key);
            }
        }

        foreach (var roadCoord in initialRoadChunks)
        {
            activeChunks[roadCoord].baseState = initialRoadState;
            if (initialRoadState == ChunkState.Settled)
            {
                startSettled.Add(roadCoord);
            }
        }

        PropagateBorderlands();

        List<Vector2Int> settledList = new List<Vector2Int>(startSettled);
        OnMapInitialized?.Invoke(settledList);
    }

    // =========================================================================
    // Cykl Dnia (Poranny Tick)
    // =========================================================================

    private void HandleMorningTick()
    {
        TickActiveMissions();
        TickOutposts();
        PropagateBorderlands();
    }

    private void TickActiveMissions()
    {
        List<ScoutingMission> finishedMissions = new List<ScoutingMission>();

        foreach (var mission in activeMissions)
        {
            if (mission.Tick())
            {
                finishedMissions.Add(mission);
            }
        }

        foreach (var mission in finishedMissions)
        {
            CompleteMission(mission);
        }

        if (finishedMissions.Count > 0)
        {
            OnEquipmentChanged?.Invoke(AvailableEquipment, TotalEquipment);
        }
    }

    private void CompleteMission(ScoutingMission mission)
    {
        activeMissions.Remove(mission);

        // Zwrot elfów do puli Idle
        if (mission.assignedElves != null && mission.assignedElves.Count > 0)
        {
            WorkforceManager.Instance?.ReleaseExpeditionWorkers(mission.assignedElves);
        }

        if (!activeChunks.TryGetValue(mission.targetChunk, out var chunkData))
        {
            OnMissionCompleted?.Invoke(mission);
            return;
        }

        if (mission.missionType == ScoutingMissionType.Survey)
        {
            chunkData.surveyDaysRemaining = 0;
            SetChunkBaseState(mission.targetChunk, ChunkState.Outskirts);
        }
        else if (mission.missionType == ScoutingMissionType.Recon)
        {
            chunkData.isReconActive = false;
            chunkData.reconIntel = GenerateReconIntel(mission.targetChunk);
            OnReconIntelDiscovered?.Invoke(mission.targetChunk, chunkData.reconIntel);
        }

        OnMissionCompleted?.Invoke(mission);
    }

    private ChunkReconIntel GenerateReconIntel(Vector2Int chunkCoord)
    {
        var intel = new ChunkReconIntel { hasIntel = true };

        if (HexMapGenerator.Instance == null || HexMapGenerator.Instance.worldData == null)
        {
            return intel;
        }

        if (!HexMapGenerator.Instance.worldData.TryGetValue(chunkCoord, out var chunkHexes))
        {
            return intel;
        }

        var featureCounts = new Dictionary<HexFeatureType, int>();
        bool hasRoad = false;

        foreach (var cellData in chunkHexes.Values)
        {
            if (cellData == null) continue;

            if (cellData.isPath) hasRoad = true;

            if (cellData.feature != HexFeatureType.None &&
                cellData.feature != HexFeatureType.Base &&
                cellData.feature != HexFeatureType.Beacon &&
                cellData.feature != HexFeatureType.Wall)
            {
                if (!featureCounts.ContainsKey(cellData.feature))
                    featureCounts[cellData.feature] = 0;
                featureCounts[cellData.feature]++;
            }
        }

        intel.hasEnemyPath = hasRoad;

        foreach (var kvp in featureCounts)
        {
            var abundance = ExpansionCostCalculator.EvaluateAbundance(kvp.Value, costConfig);
            if (abundance != ReconAbundance.None)
            {
                intel.features[kvp.Key] = abundance;
            }
        }

        return intel;
    }

    private void TickOutposts()
    {
        List<Vector2Int> fullySettledCoords = new List<Vector2Int>();

        foreach (var kvp in activeChunks)
        {
            ChunkStateData data = kvp.Value;
            if (data.baseState != ChunkState.Outskirts || data.activeOutpost == null) continue;

            data.outpostSettlementDaysRemaining--;
            if (data.outpostSettlementDaysRemaining <= 0)
            {
                fullySettledCoords.Add(kvp.Key);
            }
        }

        foreach (var coord in fullySettledCoords)
        {
            ChunkStateData data = activeChunks[coord];
            OutpostEntity outpost = data.activeOutpost;

            SetChunkBaseState(coord, ChunkState.Settled);

            if (outpost != null)
            {
                outpost.CompleteSettlement();
            }
        }
    }

    public void PropagateBorderlands()
    {
        List<Vector2Int> toBorderlands = new List<Vector2Int>();

        foreach (var kvp in activeChunks)
        {
            Vector2Int coord = kvp.Key;
            ChunkStateData data = kvp.Value;

            if (data.baseState != ChunkState.Wilderness) continue;

            bool hasValidNeighbor = false;
            foreach (var neighborCoord in HexGridMath.GetNeighbors(coord))
            {
                if (activeChunks.TryGetValue(neighborCoord, out var neighborData))
                {
                    if (neighborData.baseState == ChunkState.Outskirts || neighborData.baseState == ChunkState.Settled)
                    {
                        hasValidNeighbor = true;
                        break;
                    }
                }
            }

            if (!hasValidNeighbor) continue;

            if (data.isRoadChunk && data.hasRoadPredecessor)
            {
                if (activeChunks.TryGetValue(data.roadPredecessor, out var predData))
                {
                    if (predData.baseState != ChunkState.Outskirts && predData.baseState != ChunkState.Settled)
                    {
                        continue;
                    }
                }
                else
                {
                    continue;
                }
            }

            toBorderlands.Add(coord);
        }

        foreach (var coord in toBorderlands)
        {
            SetChunkBaseState(coord, ChunkState.Borderlands);
        }
    }

    // =========================================================================
    // API Ekspedycji (Survey & Recon)
    // =========================================================================

    public bool CanStartSurvey(Vector2Int coord, int elfCount, out string reason)
    {
        if (!activeChunks.TryGetValue(coord, out var data))
        {
            reason = "Nieprawidłowy obszar.";
            return false;
        }

        if (data.baseState != ChunkState.Borderlands)
        {
            reason = "Obszar nie znajduje się na granicy (Borderlands).";
            return false;
        }

        if (data.hasActiveMission)
        {
            reason = "Na tym obszarze trwa już aktywna misja.";
            return false;
        }

        int minElves = costConfig != null ? costConfig.minElvesForSurvey : 2;
        if (elfCount < minElves)
        {
            reason = $"Wymagane co najmniej {minElves} elfów.";
            return false;
        }

        if (AvailableEquipment < elfCount)
        {
            reason = $"Brak wystarczającej liczby wolnego ekwipunku zwiadowczego ({AvailableEquipment}/{elfCount}).";
            return false;
        }

        int idleElves = WorkforceManager.Instance != null ? WorkforceManager.Instance.GetIdleCount(Race.Elves) : 0;
        if (idleElves < elfCount)
        {
            reason = $"Brak wystarczającej liczby wolnych elfów ({idleElves}/{elfCount}).";
            return false;
        }

        var cost = CalculateSurveyCost(coord, elfCount);
        if (ResourceManager.Instance != null && !cost.CanAfford(
                ResourceManager.Instance.GetResourceAmount(ResourceType.Gold),
                ResourceManager.Instance.GetResourceAmount(ResourceType.Food)))
        {
            reason = $"Niewystarczające zasoby (Wymagane: {cost.goldCost} Złota, {cost.foodCost} Jedzenia).";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool TryStartSurvey(Vector2Int coord, int elfCount)
    {
        if (!CanStartSurvey(coord, elfCount, out _)) return false;

        var cost = CalculateSurveyCost(coord, elfCount);

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.SpendResource(ResourceType.Gold, cost.goldCost);
            ResourceManager.Instance.SpendResource(ResourceType.Food, cost.foodCost);
        }

        List<Citizen> assignedElves = null;
        if (WorkforceManager.Instance != null)
        {
            WorkforceManager.Instance.TryAssignExpeditionWorkers(elfCount, out assignedElves);
        }

        var mission = ScoutingMission.CreateSurvey(coord, cost.days, elfCount, cost.goldCost, cost.foodCost, assignedElves);
        activeMissions.Add(mission);

        var data = activeChunks[coord];
        data.surveyDaysRemaining = cost.days;
        SetChunkBaseState(coord, ChunkState.Surveying);

        OnMissionStarted?.Invoke(mission);
        OnEquipmentChanged?.Invoke(AvailableEquipment, TotalEquipment);
        return true;
    }

    public bool CanStartRecon(Vector2Int coord, out string reason)
    {
        if (!activeChunks.TryGetValue(coord, out var data))
        {
            reason = "Nieprawidłowy obszar.";
            return false;
        }

        if (data.baseState != ChunkState.Borderlands)
        {
            reason = "Obszar nie znajduje się na granicy (Borderlands).";
            return false;
        }

        if (data.hasActiveMission)
        {
            reason = "Na tym obszarze trwa już aktywna misja.";
            return false;
        }

        if (data.hasReconIntel)
        {
            reason = "Ten obszar został już zbadany przez zwiad wstępny.";
            return false;
        }

        int reqElves = costConfig != null ? costConfig.reconElfRequirement : 3;
        if (AvailableEquipment < reqElves)
        {
            reason = $"Brak ekwipunku zwiadowczego ({AvailableEquipment}/{reqElves}).";
            return false;
        }

        int idleElves = WorkforceManager.Instance != null ? WorkforceManager.Instance.GetIdleCount(Race.Elves) : 0;
        if (idleElves < reqElves)
        {
            reason = $"Brak wolnych elfów ({idleElves}/{reqElves}).";
            return false;
        }

        var cost = CalculateReconCost();
        if (ResourceManager.Instance != null && !cost.CanAfford(
                ResourceManager.Instance.GetResourceAmount(ResourceType.Gold),
                ResourceManager.Instance.GetResourceAmount(ResourceType.Food)))
        {
            reason = $"Niewystarczające zasoby (Wymagane: {cost.goldCost} Złota, {cost.foodCost} Jedzenia).";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool TryStartRecon(Vector2Int coord)
    {
        if (!CanStartRecon(coord, out _)) return false;

        var cost = CalculateReconCost();
        int reqElves = costConfig != null ? costConfig.reconElfRequirement : 3;

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.SpendResource(ResourceType.Gold, cost.goldCost);
            ResourceManager.Instance.SpendResource(ResourceType.Food, cost.foodCost);
        }

        List<Citizen> assignedElves = null;
        if (WorkforceManager.Instance != null)
        {
            WorkforceManager.Instance.TryAssignExpeditionWorkers(reqElves, out assignedElves);
        }

        var mission = ScoutingMission.CreateRecon(coord, cost.days, reqElves, cost.goldCost, cost.foodCost, assignedElves);
        activeMissions.Add(mission);

        var data = activeChunks[coord];
        data.isReconActive = true;

        OnMissionStarted?.Invoke(mission);
        OnEquipmentChanged?.Invoke(AvailableEquipment, TotalEquipment);
        return true;
    }

    public MissionCost CalculateSurveyCost(Vector2Int targetChunk, int elfCount)
    {
        bool isRoad = activeChunks.TryGetValue(targetChunk, out var d) && d.isRoadChunk;
        return ExpansionCostCalculator.CalculateSurveyCost(targetChunk, activeChunks, isRoad, costConfig, elfCount);
    }

    public MissionCost CalculateReconCost()
    {
        return ExpansionCostCalculator.CalculateReconCost(costConfig);
    }

    public ScoutingMission GetActiveMissionForChunk(Vector2Int targetChunk)
    {
        return activeMissions.Find(m => m.targetChunk == targetChunk);
    }

    public ChunkReconIntel GetChunkReconIntel(Vector2Int targetChunk)
    {
        return activeChunks.TryGetValue(targetChunk, out var d) ? d.reconIntel : null;
    }

    // =========================================================================
    // Posterunki (Outposts)
    // =========================================================================

    public void RegisterOutpost(OutpostEntity outpost, Vector2Int coord, int daysToSettle)
    {
        if (!activeChunks.TryGetValue(coord, out var data)) return;

        data.activeOutpost = outpost;
        data.outpostSettlementDaysRemaining = daysToSettle;

        OnChunkStateChanged?.Invoke(coord, data.EffectiveState);
    }

    public void UnregisterOutpost(OutpostEntity outpost, Vector2Int coord)
    {
        if (!activeChunks.TryGetValue(coord, out var data)) return;
        if (data.activeOutpost != outpost) return;

        data.activeOutpost = null;

        if (data.baseState == ChunkState.Outskirts)
        {
            DemolishInvalidBuildingsOnChunk(coord);
            OnChunkStateChanged?.Invoke(coord, data.EffectiveState);
        }
    }

    private void DemolishInvalidBuildingsOnChunk(Vector2Int chunkCoord)
    {
        if (BuildingRegistry.Instance == null) return;

        var allBuildings = new List<BuildingEntity>(BuildingRegistry.Instance.AllBuildings);
        foreach (var building in allBuildings)
        {
            if (building == null) continue;

            HexCell cell = building.GetComponentInParent<HexCell>();
            if (cell != null && cell.chunkCoord == chunkCoord)
            {
                if (building.data.type != BuildingType.Defense && !building.data.isOutpost)
                {
                    building.Demolish();
                }
            }
        }
    }

    public void SetChunkBaseState(Vector2Int coord, ChunkState newState)
    {
        if (!activeChunks.TryGetValue(coord, out var data)) return;

        data.baseState = newState;
        OnChunkStateChanged?.Invoke(coord, data.EffectiveState);
    }

    public ChunkState GetEffectiveState(Vector2Int coord)
    {
        if (activeChunks.TryGetValue(coord, out var data))
        {
            return data.EffectiveState;
        }
        return ChunkState.Wilderness;
    }

    public ChunkStateData GetChunkData(Vector2Int coord)
    {
        activeChunks.TryGetValue(coord, out var data);
        return data;
    }
}