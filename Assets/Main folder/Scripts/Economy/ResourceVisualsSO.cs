using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ResourceVisualDatabase", menuName = "Tower Defense/Resource Visuals")]

public class ResourceVisualsSO : ScriptableObject
{

    //Struct for resources icons
    [System.Serializable]
    public struct ResourceVisual
    {
        public ResourceType type;
        public Sprite icon;
    }
    
    //Struct for tower stats icons
    [System.Serializable]
    public struct StatsVisual
    {
        public BattleStats stat;
        public Sprite icon;
    }
    
    //Struct for races icons
    [System.Serializable]
    public struct RacesVisual
    {
        public Race race;
        public Sprite icon;
    }
    
    //Struct for races icons
    [System.Serializable]
    public struct FocusVisual
    {
        public TargetingMode focus;
        public Sprite icon;
    }

    
    
    public List<ResourceVisual> resourceVisuals;
    public List<StatsVisual> statVisuals;
    public List<RacesVisual> raceVisuals;
    public List<FocusVisual> focusVisuals;
    
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

    public Sprite GetIcon(RuneStatType stat)
    {
        return stat switch
        {
            RuneStatType.Damage => GetIcon(BattleStats.Damage),
            RuneStatType.Range => GetIcon(BattleStats.Range),
            RuneStatType.FireRate => GetIcon(BattleStats.FireRate),
            RuneStatType.CriticalChance => GetIcon(BattleStats.CriticalChancel),
            RuneStatType.CriticalDamage => GetIcon(BattleStats.CriticalMultiplier),
            RuneStatType.ArmorPenetration => GetIcon(BattleStats.ArmorPenetration),
            RuneStatType.MagicPenetration => GetIcon(BattleStats.MagicPenetration),
            _ => null
        };
    }

    public Sprite GetIcon(Race race)
    {
        foreach (var res in raceVisuals)
        {
            if (res.race == race) return res.icon;
        }
        return null;
    }
    public Sprite GetIcon(TargetingMode focus)
    {
        foreach (var res in focusVisuals)
        {
            if (res.focus == focus) return res.icon;
        }
        return null;
    }
}
