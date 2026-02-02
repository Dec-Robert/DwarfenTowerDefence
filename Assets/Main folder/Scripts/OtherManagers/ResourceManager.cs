using UnityEngine;
using System.Collections.Generic;
using System;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    [System.Serializable]
    public struct ResourceStartAmount
    {
        public ResourceType type;
        public float amount; // ZMIANA NA FLOAT
    }
    public List<ResourceStartAmount> startingResources;

    // S³ownik teraz przechowuje float
    private Dictionary<ResourceType, float> resourceBank = new Dictionary<ResourceType, float>();

    // Event przesy³a teraz float
    public event Action<ResourceType, float> OnResourceChanged;

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
            resourceBank[type] = 0f;
        }
        foreach (var entry in startingResources)
        {
            resourceBank[entry.type] = entry.amount;
        }
    }

    private void Start()
    {
        UpdateAllUI();
    }

    public float GetResourceAmount(ResourceType type)
    {
        return resourceBank.ContainsKey(type) ? resourceBank[type] : 0f;
    }

    public void AddResource(ResourceType type, float amount)
    {
        if (amount < 0) return;

        resourceBank[type] += amount;

        // POPRAWKA: Zaokr¹glenie do 4 miejsc po przecinku, aby usun¹æ "œmieci" (0.00001)
        resourceBank[type] = (float)Math.Round(resourceBank[type], 4);

        OnResourceChanged?.Invoke(type, resourceBank[type]);
    }

    public bool SpendResource(ResourceType type, float amount)
    {
        // Sprawdzamy z ma³ym marginesem b³êdu
        if (resourceBank[type] >= amount - 0.0001f)
        {
            resourceBank[type] -= amount;

            // POPRAWKA: Zaokr¹glenie wyniku
            resourceBank[type] = (float)Math.Round(resourceBank[type], 4);

            // Zabezpieczenie, ¿eby nie spad³o poni¿ej absolutnego zera przez b³¹d float
            if (resourceBank[type] < 0) resourceBank[type] = 0;

            OnResourceChanged?.Invoke(type, resourceBank[type]);
            return true;
        }
        return false;
    }

    // Wersja dla s³ownika (transakcja atomowa)
    public bool SpendResources(Dictionary<ResourceType, float> resourcesToSpend)
    {
        if (resourcesToSpend == null || resourcesToSpend.Count == 0) return true;

        foreach (var resource in resourcesToSpend)
        {
            if (!CanAfford(resource.Key, resource.Value)) return false;
        }

        foreach (var resource in resourcesToSpend)
        {
            resourceBank[resource.Key] -= resource.Value;

            // POPRAWKA:
            resourceBank[resource.Key] = (float)Math.Round(resourceBank[resource.Key], 4);
            if (resourceBank[resource.Key] < 0) resourceBank[resource.Key] = 0;

            OnResourceChanged?.Invoke(resource.Key, resourceBank[resource.Key]);
        }
        return true;
    }

    // Przeci¹¿enie dla int (kompatybilnoœæ wsteczna z kodem który u¿ywa int)
    public bool SpendResources(Dictionary<ResourceType, int> resourcesToSpend)
    {
        // Konwersja w locie
        Dictionary<ResourceType, float> floatDict = new Dictionary<ResourceType, float>();
        foreach (var kvp in resourcesToSpend) floatDict.Add(kvp.Key, (float)kvp.Value);
        return SpendResources(floatDict);
    }

    public bool CanAfford(ResourceType type, float amount)
    {
        return resourceBank.ContainsKey(type) && resourceBank[type] >= amount - 0.001f; // Ma³y margines b³êdu float
    }

    public void UpdateAllUI()
    {
        foreach (var kvp in resourceBank)
        {
            OnResourceChanged?.Invoke(kvp.Key, kvp.Value);
        }
    }
}