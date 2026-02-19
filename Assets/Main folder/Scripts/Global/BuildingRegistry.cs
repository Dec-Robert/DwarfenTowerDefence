using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralny rejestr wszystkich aktywnych budynków na scenie.
///
/// Zastępuje statyczne BuildingEntity.AllBuildings.
/// Budynki same się rejestrują/wyrejestrowują przez OnEnable/OnDisable.
///
/// SETUP: Dodaj jako komponent na pustym GameObject "Registry" w scenie.
/// </summary>
public class BuildingRegistry : MonoBehaviour
{
    public static BuildingRegistry Instance { get; private set; }

    private readonly List<BuildingEntity> buildings = new List<BuildingEntity>();

    // Zdarzenia – inne systemy mogą reagować na pojawienie/zniknięcie budynku
    public event System.Action<BuildingEntity> OnBuildingRegistered;
    public event System.Action<BuildingEntity> OnBuildingUnregistered;

    // =========================================================================
    // Cykl życia
    // =========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        // Lista czyści się automatycznie razem z obiektem przy zmianie sceny
        buildings.Clear();
        Instance = null;
    }

    // =========================================================================
    // Rejestracja (wywoływana przez BuildingEntity)
    // =========================================================================

    public void Register(BuildingEntity building)
    {
        if (building == null || buildings.Contains(building)) return;
        buildings.Add(building);
        OnBuildingRegistered?.Invoke(building);
    }

    public void Unregister(BuildingEntity building)
    {
        if (building == null) return;
        buildings.Remove(building);
        OnBuildingUnregistered?.Invoke(building);
    }

    // =========================================================================
    // Odpytywanie
    // =========================================================================

    /// <summary>Wszystkie aktywne budynki (tylko do odczytu).</summary>
    public IReadOnlyList<BuildingEntity> AllBuildings => buildings;

    /// <summary>Zwraca wszystkie budynki danego typu.</summary>
    public List<T> GetAllOfType<T>() where T : BuildingEntity
    {
        var result = new List<T>();
        foreach (var b in buildings)
            if (b is T match) result.Add(match);
        return result;
    }

    /// <summary>Zwraca wszystkie budynki produkujące dany zasób.</summary>
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