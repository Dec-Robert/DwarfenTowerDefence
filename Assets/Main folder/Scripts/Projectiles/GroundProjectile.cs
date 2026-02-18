using UnityEngine;
using System.Collections.Generic;

public class GroundProjectile : ProjectileBase
{
    [Header("Statystyki Strefy")]
    public float damagePerTick = 10f;
    public float tickInterval = 1f;
    public float duration = 5f;
    public GameObject groundEffectPrefab; // Musi mieæ skrypt GroundDamageZone!
    public float zoneCritChance = 0f; // Szansa na krytyczne trafienie w strefie 

    [Header("Parametry Lotu")]
    public float speed = 10f;
    public float arcHeight = 2.0f; // Jak wysoko pocisk ma siê wznieœæ w locie

    private Vector3 startPos;
    private Vector3 targetPos;
    private float progress = 0f;


    public override void Initialize(float _damage, DamageType _type, List<TowerEffectSO> _effects, bool _isCritical, float _criticalMultiplier, float _critChance)
    {
        damage = _damage;
        damageType = _type;
        effects = _effects;
        criticalMultiplier = _criticalMultiplier;
        zoneCritChance = _critChance;
    }

    // Metoda startowa (nazywana "Launch" jak w innych specjalnych pociskach)
    public void Launch(Vector3 _targetPos)
    {
        startPos = transform.position;
        targetPos = _targetPos;

        // Upewniamy siê, ¿e celujemy w ziemiê (Y=0 lub wysokoœæ terenu)
        // Mo¿esz tu u¿yæ Raycasta w dó³, ¿eby znaleŸæ ziemiê, jeœli mapa jest pofalowana
        // targetPos.y = 0.1f; 
    }

    void Update()
    {
        // Obliczamy dystans ca³kowity
        float totalDist = Vector3.Distance(startPos, targetPos);
        if (totalDist <= 0.1f) { SpawnZone(); return; }

        // Przesuwamy siê w czasie (liniowo 0 -> 1)
        float step = speed * Time.deltaTime;
        progress += step / totalDist;

        if (progress >= 1.0f)
        {
            SpawnZone();
            return;
        }

        // Interpolacja Liniowa (ruch po prostej)
        Vector3 currentPos = Vector3.Lerp(startPos, targetPos, progress);

        // Dodanie ³uku (Parabola)
        // Wzór: sin(pi * progress) daje górkê od 0 do 1 i z powrotem do 0
        float height = Mathf.Sin(Mathf.PI * progress) * arcHeight;
        currentPos.y += height;

        transform.position = currentPos;

        // Opcjonalnie: Rotacja w kierunku lotu (wymaga obliczenia pochodnej lub LookAt na next frame)
    }

    void SpawnZone()
    {
        if (groundEffectPrefab != null)
        {
            // Tworzymy strefê na ziemi
            GameObject zoneObj = Instantiate(groundEffectPrefab, targetPos, Quaternion.identity);

            // Konfigurujemy j¹ danymi z wie¿y
            GroundDamageZone zone = zoneObj.GetComponent<GroundDamageZone>();
            if (zone != null)
            {
                // Przekazujemy obra¿enia z ProjectileBase (to te wyliczone przez wie¿ê)
                // Uwaga: Mo¿esz chcieæ u¿yæ damagePerTick z tego skryptu ALBO damage z wie¿y.
                // Zazwyczaj wie¿a definiuje DPS, wiêc u¿yjmy 'damage' z Initialize.
                zone.Setup(damage, damageType, effects, tickInterval, duration,armourPiercing,magicPiercing,criticalMultiplier,zoneCritChance);
            }
        }
        else
        {
            Debug.LogError("Brak prefabu GroundEffect w GroundProjectile!");
        }

        Destroy(gameObject); // Niszczymy pocisk (kroplê)
    }
}