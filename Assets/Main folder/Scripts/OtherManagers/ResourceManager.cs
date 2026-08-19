using UnityEngine;
using System.Collections.Generic;
using System;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }
    
    [Header("Baza Startowa")]
    public CityBaseConfigSO cityConfig;
    private Dictionary<ResourceType, int> resourceBank = new Dictionary<ResourceType, int>();
    
    public event Action<ResourceType, int> OnResourceChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;
        InitializeResources();
    }

    private void InitializeResources()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            resourceBank[type] = 0;
        }

        if (cityConfig != null)
        {
            resourceBank[ResourceType.Gold] = cityConfig.startGold;
            resourceBank[ResourceType.Wood] = cityConfig.startWood;
            resourceBank[ResourceType.Stone] = cityConfig.startStone;
            resourceBank[ResourceType.Iron] = cityConfig.startIron;
            resourceBank[ResourceType.Coal] = cityConfig.startCoal;
            resourceBank[ResourceType.Food] = cityConfig.startFood;
        }
        else
        {
            Debug.LogError("[ECONOMY][ResourceManager] BRAK CITY BASE CONFIGU!");
            resourceBank[ResourceType.Gold] = 999999;
            resourceBank[ResourceType.Wood] = 999999;
            resourceBank[ResourceType.Stone] = 999999;
            resourceBank[ResourceType.Iron] = 999999;
            resourceBank[ResourceType.Coal] = 999999;
            resourceBank[ResourceType.Food] = 999999;
        }
    }

    private void Start()
    {
        UpdateAllUI();
    }

    public int GetResourceAmount(ResourceType type)
    {
        return resourceBank.ContainsKey(type) ? resourceBank[type] : 0;
    }

    public void AddResource(ResourceType type, int amount)
    {
        if (amount < 0) return;

        resourceBank[type] += amount;
        OnResourceChanged?.Invoke(type, resourceBank[type]);
    }

    public void AddResources(List<ResourceCost> resourceToAdd)
    {
        if (resourceToAdd == null) return;
        
        foreach (var resource in resourceToAdd)
        {
            AddResource(resource.type,resource.amount);
        }
    }

    public bool SpendResource(ResourceType type, int amount)
    {
        if (resourceBank[type] >= amount )
        {
            resourceBank[type] -= amount;
            OnResourceChanged?.Invoke(type, resourceBank[type]);
            return true;
        }
        return false;
    }

    // Override List ResourceCost
    public bool SpendResources(List<ResourceCost> resourcesToSpend)
    {
        if (resourcesToSpend == null || resourcesToSpend.Count == 0) return true;

        foreach (var resource in resourcesToSpend)
        {
            if (!CanAfford(resource.type, resource.amount)) return false;
        }

        foreach (var resource in resourcesToSpend)
        {
            resourceBank[resource.type] -= resource.amount;
            OnResourceChanged?.Invoke(resource.type, resourceBank[resource.type]);
        }
        return true;
    }
    
    public bool CanAfford(ResourceType type, int amount)
    {
        return resourceBank.ContainsKey(type) && resourceBank[type] >= amount;
    }

    public void UpdateAllUI()
    {
        foreach (var kvp in resourceBank)
        {
            OnResourceChanged?.Invoke(kvp.Key, kvp.Value);
        }
    }
}