using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewMetaUpgrade", menuName = "Game/Meta Upgrade")]
public class MetaUpgradeSO : ScriptableObject
{
    public string id;
    public string upgradeName;
    [TextArea] public string description;
    public Sprite icon;
    public int cost;

    public bool isUnlocked = false;

    [Header("Wymagania")]
    public List<MetaUpgradeSO> prerequisites;
}