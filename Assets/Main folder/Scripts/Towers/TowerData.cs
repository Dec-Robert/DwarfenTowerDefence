using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewTower", menuName = "Game/Tower Data")]
public class TowerData : BuildingData
{
    [Header("Statystyki Bojowe")]
    public float baseRange;                 // Zasiêg ataku w jednostkach gry 2 zapewnia, ¿e wie¿a mo¿e atakowaæ tylko na tym samym heksie, ka¿de kolejne +3/4 jednostki zwiêksza zasiêg o 1 heks
    public float baseDamage;
    public float fireRate;                  // Strza³y na sekundê

    public float criticalChancel;           // Wartoœæ procentowa, np. 20 = 20% daje 20% szansy na trafienie krytyczne
    public float criticalDamageMultiplier;  // Wartoœæ procentowa, np. 2 = 200% obra¿eñ przy trafieniu krytycznym
    public float armorPenetration;          // Wartoœæ procentowa, np. 20 = 20% penetracji pancerza
    public float magicPenetration;          // Wartoœæ procentowa, np. 20 = 20% penetracji odpornoœci magicznej


    private BuildingType type;              // Bazowy typ wiez, zawsze ustawiany na Defense w OnValidate()
    public DamageType damageType;           // Typ obra¿eñ, np. Physical, Magic, True


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