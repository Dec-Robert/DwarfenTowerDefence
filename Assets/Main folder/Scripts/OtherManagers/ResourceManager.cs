using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    [SerializeField] private bool DEBUG_MODE=false;

    //Inicjalizacja wartoœci pocz¹tkowych
    [System.Serializable]
    public struct ResourceStartAmount
    {
        public ResourceType type;
        public int amount;
    }
    public List<ResourceStartAmount> startingResources;

    private Dictionary<ResourceType, int> resourceBank = new Dictionary<ResourceType, int>();

    public event Action<ResourceType, int> OnResourceChanged;


    private void Update()
    {
        //DEBUG
        if (Input.GetKeyDown(KeyCode.M) && DEBUG_MODE) // M jak Money
        {
            AddResource(ResourceType.Gold, 100);
            AddResource(ResourceType.Wood, 50);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;

        InitializeResources();
    }

    private void Start()
    {
        UpdateAllUI();
    }


    private void InitializeResources()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            resourceBank[type] = 0;
        }

        foreach (var entry in startingResources)
        {
            resourceBank[entry.type] = entry.amount;
        }
    }

    public int GetResourceAmount(ResourceType type)
    {
        if (resourceBank.ContainsKey(type))
            return resourceBank[type];
        return 0;
    }

    public void AddResource(ResourceType type, int amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning("U¿yj SpendResource do odejmowania!");
            return;
        }

        resourceBank[type] += amount;

        // Powiadom UI
        OnResourceChanged?.Invoke(type, resourceBank[type]);
        Debug.Log($"[Resource] Dodano {amount} {type}. Razem: {resourceBank[type]}");
    }

    public bool SpendResources(Dictionary<ResourceType,int> resourcesToSpend)
    {
        foreach (var resource in resourcesToSpend) 
        {
            if (!CanAfford(resource.Key, resource.Value)) return false;
        }

        foreach (var resource in resourcesToSpend)
        {
            resourceBank[resource.Key] = resourceBank[resource.Key] - resource.Value;
            OnResourceChanged?.Invoke(resource.Key, resourceBank[resource.Key]);
        }
        return true;
    }

    public bool CanAfford(ResourceType type, int amount)
    {
        return resourceBank.ContainsKey(type) && resourceBank[type] >= amount;
    }

    // Pomocnicza do odœwie¿enia ca³ego UI na raz
    public void UpdateAllUI()
    {
        foreach (var kvp in resourceBank)
        {
            OnResourceChanged?.Invoke(kvp.Key, kvp.Value);
        }
    }

}
