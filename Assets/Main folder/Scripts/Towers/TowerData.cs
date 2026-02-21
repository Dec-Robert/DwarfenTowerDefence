using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewTower", menuName = "Game/Tower Data")]
public class TowerData : BuildingData
{
    [Header("Statystyki Bojowe")]
    public float baseRange;                 // Zasi�g ataku w jednostkach gry 2 zapewnia, �e wie�a mo�e atakowa� tylko na tym samym heksie, ka�de kolejne +3/4 jednostki zwi�ksza zasi�g o 1 heks
    public float baseDamage;
    public float fireRate;                  // Strza�y na sekund�

    public float criticalChancel;           // Warto�� procentowa, np. 20 = 20% daje 20% szansy na trafienie krytyczne
    public float criticalDamageMultiplier;  // Warto�� procentowa, np. 2 = 200% obra�e� przy trafieniu krytycznym
    public float armorPenetration;          // Warto�� procentowa, np. 20 = 20% penetracji pancerza
    public float magicPenetration;          // Warto�� procentowa, np. 20 = 20% penetracji odporno�ci magicznej


    private BuildingType type;              // Bazowy typ wiez, zawsze ustawiany na Defense w OnValidate()
    public DamageType damageType;           // Typ obra�e�, np. Physical, Magic, True

    //
    [Header("Efekty Specjalne")]
    // Lista efekt�w, np. [SlowEffect, PoisonEffect]
    public List<TowerEffectSO> effects;

    public GameObject bulletPrefab;

    // To wymusza typ Defense automatycznie w edytorze
    private void OnValidate()
    {
        type = BuildingType.Defense;
    }
}