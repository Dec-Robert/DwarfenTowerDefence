using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewTower", menuName = "Game/Towers/Tower Data")]
public class TowerData : BuildingData
{
    public const float HexToWorldRangeMultiplier = 2.0f;

    [Header("Statystyki Bojowe")] 
    [Tooltip("Zasięg ataku w liczbie heksów (np. 3 oznacza zasięg do 3 heksów od wieży).")]
    public float baseRange;
    public float BaseRangeInWorldUnits => baseRange * HexToWorldRangeMultiplier;
    public float baseDamage;
    public float fireRate; // Strza�y na sekund�

    public float criticalChancel; // Wartość procentowa, np. 20 = 20% daje 20% szansy na trafienie krytyczne
    public float criticalDamageMultiplier; // Wartość procentowa, np. 2 = 200% obrażeń przy trafieniu krytycznym
    public float armorPenetration; // Wartość procentowa, np. 20 = 20% penetracji pancerza
    public float magicPenetration; // Wartość procentowa, np. 20 = 20% penetracji odporności magicznej
    public DamageType damageType; // Typ obrażeń, np. Physical, Magic, True

        [Header("Celowanie")]
    [Tooltip("Czy wieża może strzelać w wyznaczony punkt/heks na mapie (np. Bombarda, Crucible).")]
    public bool canShootChunk;
    public bool CanTargetGroundHex => canShootChunk;

    [Tooltip("Domyślna lista priorytetów (do 3). Puste = domyślnie ClosestOnTrack")]
    public List<TargetingMode> defaultTargetingPriorities;


    // Lista efektów, np. [SlowEffect, PoisonEffect]
    [Header("Efekty Specjalne")] public List<TowerEffectSO> effects;

    public GameObject bulletPrefab;

    // To wymusza typ Defense automatycznie w edytorze
    private void OnValidate()
    {
        type = BuildingType.Defense;
    }
}