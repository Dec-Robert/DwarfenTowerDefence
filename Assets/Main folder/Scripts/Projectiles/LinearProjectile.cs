using UnityEngine;
using System.Collections.Generic;

public class LinearProjectile : ProjectileBase
{
    [Header("Ustawienia Ruchu")]
    public float speed = 15f;
    public float lifeTime = 5f;
    public int pierceCount = 3;

    [Header("Korekta Wysokoœci")]
    [Tooltip("Wysokoœæ Y, na której pocisk powinien kosiæ wrogów (np. 0.8)")]
    public float targetCombatY = 0.8f;
    public float heightCorrectionSpeed = 5f;

    private int currentHits = 0;
    private HashSet<EnemyStats> hitEnemies = new HashSet<EnemyStats>();
    private Vector3 moveDirection;

    public override void Initialize(float _damage, DamageType _type, List<TowerEffectSO> _effects,bool _isCritical, float _criticalMultiplier,float _=0f)
    {
        base.Initialize(_damage, _type, _effects, _isCritical, _criticalMultiplier);

        // Zapamiêtujemy kierunek patrzenia lufy (Forward) w momencie strza³u
        // Sp³aszczamy go do p³aszczyzny XZ, ¿eby wysokoœæ kontrolowa³ skrypt
        moveDirection = transform.forward;
        moveDirection.y = 0;
        moveDirection.Normalize();
    }

    void Start()
    {
        // Pocisk zniknie po czasie, nawet jeœli w nic nie trafi
        Destroy(gameObject, lifeTime);
    }

    void Update()
    {
        // 1. Ruch w poziomie (XZ)
        transform.position += moveDirection * speed * Time.deltaTime;

        // 2. P³ynna korekta wysokoœci (Y)
        // Lerpujemy z obecnej wysokoœci (np. lufa na wzgórzu) do wysokoœci bojowej
        float currentY = transform.position.y;
        float newY = Mathf.Lerp(currentY, targetCombatY, Time.deltaTime * heightCorrectionSpeed);

        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    // Wykorzystujemy SphereCollider (Is Trigger)
    private void OnTriggerEnter(Collider other)
    {
        // Sprawdzamy czy trafiliœmy wroga
        EnemyStats enemy = other.GetComponent<EnemyStats>();

        // Jeœli to wróg i jeszcze go nie trafiliœmy tym konkretnym pociskiem
        if (enemy != null && !hitEnemies.Contains(enemy))
        {
            hitEnemies.Add(enemy);

            // Metoda z ProjectileBase: zadaje dmg i nak³ada efekty (np. spowolnienie)
            ApplyDamageAndEffects(enemy);

            Debug.Log($"[Cannon] Trafiono: {enemy.name}. Przebicie: {currentHits + 1}/{pierceCount}");

            currentHits++;

            // Jeœli pocisk przebi³ ju¿ maksymaln¹ liczbê wrogów, znika
            if (currentHits >= pierceCount)
            {
                Destroy(gameObject);
            }
        }
    }
}