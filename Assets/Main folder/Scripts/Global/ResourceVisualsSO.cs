using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ResourceVisualDatabase", menuName = "Tower Defense/Resource Visuals")]

public class ResourceVisualsSO : ScriptableObject
{

    //Struct for resources
    [System.Serializable]
    public struct ResourceVisual
    {
        public ResourceType type;
        public Sprite icon;
    }
    
    //Struct for tower data
    [System.Serializable]
    public struct StatsVisual
    {
        public BattleStats stat;
        public Sprite icon;
    }
    
    
    public List<ResourceVisual> resourceVisuals;
    public List<StatsVisual> statVisuals;
        
    public Sprite GetIcon(ResourceType type)
    {
            foreach (var res in resourceVisuals)
            {
                if (res.type == type) return res.icon;
            }
            return null;
    }

    public Sprite GetIcon(BattleStats stat)
    {
        foreach (var res in statVisuals)
        {
            if (res.stat == stat) return res.icon;
        }
        return null;
    }
    
}
