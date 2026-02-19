using UnityEngine;
using System.Collections.Generic;

public class LinearProjectile : ProjectileBase
{
    [Header("Ustawienia Ruchu")]
    public float speed = 15f;
    public float lifeTime = 5f;
    public int pierceCount = 3;

    [Header("Korekta Wysokości")]
    public float targetCombatY = 0.8f;
    public float heightCorrectionSpeed = 5f;

    private int currentHits = 0;
    private HashSet<EnemyStats> hitEnemies = new HashSet<EnemyStats>();
    private Vector3 moveDirection;
    private bool launched = false;

    
    public override void Launch(Transform target, Vector3 targetPos, Transform firingPoint = null)
    {
        moveDirection = transform.forward;
        moveDirection.y = 0;
        moveDirection.Normalize();
        launched = true;
    }

    void Update()
    {
        if (!launched) return; // Czekamy na Launch - ustawienie pocisku

        // 1. Ruch w poziomie (XZ)
        transform.position += moveDirection * (speed * Time.deltaTime);

        // 2. Płynna korekta wysokości (Y)
        float currentY = transform.position.y;
        float newY = Mathf.Lerp(currentY, targetCombatY, Time.deltaTime * heightCorrectionSpeed);
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    // Wykorzystujemy SphereCollider (Is Trigger)
    private void OnTriggerEnter(Collider other)
    {
        EnemyStats enemy = other.GetComponent<EnemyStats>();
        if (enemy != null && !hitEnemies.Contains(enemy))
        {
            hitEnemies.Add(enemy);
            ApplyDamageAndEffects(enemy);
            currentHits++;
            if (currentHits >= pierceCount) Destroy(gameObject);
        }
    }
}