using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewTower", menuName = "Game/Tower Data")]
public class TowerData : BuildingData
{
    [Header("Statystyki Bojowe")] 
    public float baseRange; // Zasig ataku w jednostkach gry.
    public float baseDamage;
    public float fireRate; // Strza�y na sekund�

    public float criticalChancel; // Wartość procentowa, np. 20 = 20% daje 20% szansy na trafienie krytyczne
    public float criticalDamageMultiplier; // Wartość procentowa, np. 2 = 200% obrażeń przy trafieniu krytycznym
    public float armorPenetration; // Wartość procentowa, np. 20 = 20% penetracji pancerza
    public float magicPenetration; // Wartość procentowa, np. 20 = 20% penetracji odporności magicznej
    public DamageType damageType; // Typ obrażeń, np. Physical, Magic, True

    [Header("Celowanie")] public bool canShootChunk;

    [Tooltip("Domyślna lista priorytetów (do 3). Puste = domyślnie ClosestOnTrack")]
    public List<TargetingMode> defaultTargetingPriorities;


    // Lista efektów, np. [SlowEffect, PoisonEffect]
    [Header("Efekty Specjalne")] public List<TowerEffectSO> effects;

    public GameObject bulletPrefab;

    private BuildingType type; // Bazowy typ wiez, zawsze ustawiany na Defense w OnValidate()

    // To wymusza typ Defense automatycznie w edytorze
    private void OnValidate()
    {
        type = BuildingType.Defense;
    }
}