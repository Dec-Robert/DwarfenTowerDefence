using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewTower", menuName = "Game/Tower Data")]
public class TowerData : BuildingData
{
    [Header("Statystyki Bojowe")]
    public float baseRange;
    public float baseDamage;
    public float fireRate;

    private BuildingType type;

    [Header("Efekty Specjalne")]
    // Lista efektów, np. [SlowEffect, PoisonEffect]
    public List<TowerEffectSO> effects;

    public GameObject bulletPrefab;

    // To wymusza typ Defense automatycznie w edytorze
    private void OnValidate()
    {
        type = BuildingType.Defense;
    }
}