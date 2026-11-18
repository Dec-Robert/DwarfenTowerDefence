using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BuildingRegistry : MonoBehaviour
{
    public static BuildingRegistry Instance { get; private set; }

    private readonly List<BuildingEntity> buildings = new List<BuildingEntity>();

    public event System.Action<BuildingEntity> OnBuildingRegistered;
    public event System.Action<BuildingEntity> OnBuildingUnregistered;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnDuskStarted += ExecuteDuskEconomy;
            TimePhaseManager.Instance.OnNightStarted += ExecuteNightDefenseUpkeep;
        }
        if (MapExpansionManager.Instance != null)
        {
            MapExpansionManager.Instance.OnChunkStateChanged += HandleChunkStateChanged;
        }
        if (FogOfWarManager.Instance != null)
        {
            FogOfWarManager.Instance.OnChunkRevealed += HandleChunkRevealed;
        }
    }

    private void OnDestroy()
    {
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnDuskStarted -= ExecuteDuskEconomy;
            TimePhaseManager.Instance.OnNightStarted -= ExecuteNightDefenseUpkeep;
        }
        if (MapExpansionManager.Instance != null)
        {
            MapExpansionManager.Instance.OnChunkStateChanged -= HandleChunkStateChanged;
        }
        if (FogOfWarManager.Instance != null)
        {
            FogOfWarManager.Instance.OnChunkRevealed -= HandleChunkRevealed;
        }
        buildings.Clear();
        Instance = null;
    }

    private void HandleChunkStateChanged(Vector2Int coord, ChunkState newState)
    {
        if (newState == ChunkState.Outskirts || newState == ChunkState.Settled)
        {
            RescanAllBuildings();
        }
    }

    private void HandleChunkRevealed(Vector2Int coord)
    {
        RescanAllBuildings();
    }

    public void RescanAllBuildings()
    {
        for (int i = 0; i < buildings.Count; i++)
        {
            if (buildings[i] != null && buildings[i].isBuildingActive)
            {
                buildings[i].ForceRescan();
            }
        }
        BuildingEntity.NotifyEconomyChanged();
    }

    public void Register(BuildingEntity building)
    {
        if (building == null || buildings.Contains(building)) return;
        buildings.Add(building);
        OnBuildingRegistered?.Invoke(building);
        BuildingEntity.NotifyEconomyChanged();
    }

    public void Unregister(BuildingEntity building)
    {
        if (building == null) return;
        buildings.Remove(building);
        OnBuildingUnregistered?.Invoke(building);
        BuildingEntity.NotifyEconomyChanged();
    }

    private void ExecuteDuskEconomy()
    {
        var activeBuildings = buildings.Where(b => b != null && b.isBuildingActive).ToList();

        // Warsztaty narzędziowe (Unique, bez kosztu surowcowego) blokują swoją załogę po dniu pracy
        foreach (var workshop in activeBuildings.OfType<ToolWorkshopEntity>())
        {
            workshop.ProcessUpkeep();
        }

        // Kroki 1-4: Budynki ekonomiczne w ścisłej kolejności zależności surowcowych
        var economicBuildings = activeBuildings
            .Where(b => b.data != null && b.data.type == BuildingType.Economic && !(b is HousingEntity))
            .OrderBy(b => (int)b.data.GetEffectiveDuskStep())
            .ThenBy(b => b.upkeepPriority)
            .ToList();

        for (int step = (int)EconomicPhaseStep.Step1_PrimaryWood; step <= (int)EconomicPhaseStep.Step4_OtherEconomic; step++)
        {
            var stepBuildings = economicBuildings
                .Where(b => (int)b.data.GetEffectiveDuskStep() == step)
                .ToList();
            if (stepBuildings.Count == 0) continue;

            foreach (var building in stepBuildings)
            {
                building.ProcessUpkeep();
            }

            foreach (var building in stepBuildings)
            {
                building.ProcessProduction();
            }
        }

        // Krok 5: Domy mieszkalne (produkcja -> utrzymanie -> wzrost tylko jeśli wszystkie opłaciły utrzymanie)
        ExecuteDuskHousingEconomy(activeBuildings);

        BuildingEntity.NotifyEconomyChanged();
        PhaseResourceForecastManager.NotifyForecastDirty();
    }

    private void ExecuteDuskHousingEconomy(List<BuildingEntity> activeBuildings)
    {
        var housingBuildings = activeBuildings
            .OfType<HousingEntity>()
            .Where(h => h.housingData != null)
            .OrderBy(h => h.upkeepPriority)
            .ThenBy(h => h.GetDaysRemaining())
            .ToList();

        if (housingBuildings.Count == 0) return;

        foreach (var housing in housingBuildings)
        {
            housing.ProcessDuskProduction();
        }

        bool allHousesPaidSurvival = true;
        foreach (var housing in housingBuildings)
        {
            if (!housing.ProcessDuskSurvival())
            {
                allHousesPaidSurvival = false;
            }
        }

        foreach (var housing in housingBuildings)
        {
            housing.ProcessDuskGrowth(canGrowCityWide: allHousesPaidSurvival);
        }
    }

    private void ExecuteNightDefenseUpkeep()
    {
        var defenseBuildings = buildings
            .Where(b => b != null && b.isBuildingActive && b.data != null && b.data.type == BuildingType.Defense)
            .OrderBy(b => b.upkeepPriority)
            .ToList();

        foreach (var defense in defenseBuildings)
            defense.ProcessUpkeep();
    }

    public IReadOnlyList<BuildingEntity> AllBuildings => buildings;

    public List<T> GetAllOfType<T>() where T : BuildingEntity
    {
        var result = new List<T>();
        foreach (var b in buildings)
            if (b is T match) result.Add(match);
        return result;
    }

    public List<BuildingEntity> GetProducersOf(ResourceType resource)
    {
        var result = new List<BuildingEntity>();
        foreach (var b in buildings)
        {
            var prod = b.GetCurrentProduction();
            if (prod.ContainsKey(resource)) result.Add(b);
        }
        return result;
    }
}