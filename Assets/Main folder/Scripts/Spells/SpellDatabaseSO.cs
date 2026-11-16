using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct ResourcePointValue
{
    public ResourceType resourceType;
    [Min(1)] public int pointsPerUnit;
}

[CreateAssetMenu(fileName = "SpellDatabase", menuName = "Game/Spells/Spell Database")]
public class SpellDatabaseSO : ScriptableObject
{
    [Header("Pula Dostępnych Zaklęć")]
    public List<SpellDataSO> availableSpells = new List<SpellDataSO>();

    [Header("Wartości Punktowe Surowców (Money Sink)")]
    public List<ResourcePointValue> resourcePointValues = new List<ResourcePointValue>
    {
        new ResourcePointValue { resourceType = ResourceType.Wood, pointsPerUnit = 1 },
        new ResourcePointValue { resourceType = ResourceType.Food, pointsPerUnit = 1 },
        new ResourcePointValue { resourceType = ResourceType.Stone, pointsPerUnit = 2 },
        new ResourcePointValue { resourceType = ResourceType.Coal, pointsPerUnit = 2 },
        new ResourcePointValue { resourceType = ResourceType.Iron, pointsPerUnit = 3 },
        new ResourcePointValue { resourceType = ResourceType.Ether, pointsPerUnit = 5 }
    };

    public int GetResourcePointValue(ResourceType type)
    {
        if (resourcePointValues != null)
        {
            for (int i = 0; i < resourcePointValues.Count; i++)
            {
                if (resourcePointValues[i].resourceType == type)
                    return Mathf.Max(1, resourcePointValues[i].pointsPerUnit);
            }
        }
        return 1;
    }

    public List<SpellDataSO> DrawRandomSpells(int count, System.Random rng = null)
    {
        var pool = new List<SpellDataSO>();
        if (availableSpells != null)
        {
            foreach (var s in availableSpells)
            {
                if (s != null) pool.Add(s);
            }
        }

        var result = new List<SpellDataSO>();
        int toDraw = Mathf.Min(count, pool.Count);
        for (int i = 0; i < toDraw; i++)
        {
            int idx = rng != null ? rng.Next(0, pool.Count) : UnityEngine.Random.Range(0, pool.Count);
            result.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return result;
    }

    public static SpellDatabaseSO CreateRuntimeDefault()
    {
        var db = CreateInstance<SpellDatabaseSO>();
        db.name = "RuntimeDefaultSpellDatabase";
        db.availableSpells.Add(SpellDataSO.CreateDefaultFireball());
        db.availableSpells.Add(SpellDataSO.CreateDefaultFrostNova());
        return db;
    }
}
