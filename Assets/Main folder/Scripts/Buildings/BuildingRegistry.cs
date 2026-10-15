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
            TimePhaseManager.Instance.OnMorningStarted += ExecuteMorningHousingEconomy;
            TimePhaseManager.Instance.OnDuskStarted += ExecuteDuskEconomy;
            TimePhaseManager.Instance.OnNightStarted += ExecuteNightDefenseUpkeep;
        }
    }

    private void OnDestroy()
    {
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= ExecuteMorningHousingEconomy;
            TimePhaseManager.Instance.OnDuskStarted -= ExecuteDuskEconomy;
            TimePhaseManager.Instance.OnNightStarted -= ExecuteNightDefenseUpkeep;
        }
        buildings.Clear();
        Instance = null;
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

    private void ExecuteMorningHousingEconomy()
    {
        var housingBuildings = buildings
            .OfType<HousingEntity>()
            .Where(h => h != null && h.isBuildingActive && h.housingData != null)
            .OrderBy(h => h.upkeepPriority)
            .ThenBy(h => h.GetDaysRemaining())
            .ToList();

        foreach (var housing in housingBuildings)
        {
            housing.ProcessMorningProduction();
        }

        foreach (var housing in housingBuildings)
        {
            housing.ProcessMorningSurvival();
        }

        foreach (var housing in housingBuildings)
        {
            housing.ProcessMorningGrowth();
        }
    }

    private void ExecuteDuskEconomy()
    {
        var activeBuildings = buildings.Where(b => b != null && b.isBuildingActive).ToList();

        var economicBuildings = activeBuildings
            .Where(b => b.data != null && b.data.type != BuildingType.Defense && b.data.type != BuildingType.Housing && !(b is HousingEntity))
            .OrderBy(b => b.upkeepPriority)
            .ToList();

        foreach (var building in economicBuildings)
        {
            building.ProcessUpkeep();
        }

        foreach (var building in economicBuildings)
        {
            building.ProcessProduction();
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