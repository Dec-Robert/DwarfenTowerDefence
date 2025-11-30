using UnityEngine;

[CreateAssetMenu(fileName = "NewTower", menuName = "Tower Defense/Tower Data")]

public class TowerData : ScriptableObject
{
    public string towerName;
    public string towerDescription;
    public Sprite icon;

    public float baseRange;
    public float baseDamage;
    public float fireRate;
    public int cost;

    public GameObject prefab;
    public GameObject bulletPrefab;

}
