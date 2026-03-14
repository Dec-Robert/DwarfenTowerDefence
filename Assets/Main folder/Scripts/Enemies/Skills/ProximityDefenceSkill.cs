using UnityEngine;

public class ProximityDefenceSkill : EnemySkill
{
    [Header("Konfiguracja Odbijania")]
    [Tooltip("Zasięg w jakim wieża zostanie całkowicie zignorowana (brak obrażeń bezpośrednich)")]
    public float safeRadius = 15f; 

    public override float OnBeforeDamageCalculation(float incomingDamage, DamageType type, TowerEntity sourceTower = null, bool isAoE = false)
    {
        // 1. Jeśli to atak obszarowy (AoE), przyjmujemy obrażenia normalnie!
        if (isAoE) return incomingDamage;

        // 2. Jeśli nie znamy wieży, przyjmujemy obrażenia
        if (sourceTower == null) return incomingDamage;

        // 3. Sprawdzamy odległość
        float distanceToTower = Vector3.Distance(transform.position, sourceTower.transform.position);

        if (distanceToTower <= safeRadius)
        {
            Debug.Log($"<color=yellow>{name} ODBIŁ strzał! Wieża {sourceTower.name} stała za blisko (Dystans: {distanceToTower:F1} / Zasięg ochronny: {safeRadius}).</color>");
            return 0f; // Obrażenia anulowane do zera!
        }

        return incomingDamage;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, safeRadius);
    }
}