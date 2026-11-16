using System.Collections.Generic;
using UnityEngine;

public class SpellGroundZone : MonoBehaviour
{
    private float radius;
    private float dps;
    private DamageType damageType;
    private float slowPercent;
    private float remainingDuration;
    private float tickTimer;
    private const float TickInterval = 0.5f;

    public void Initialize(float zoneRadius, float zoneDps, DamageType dmgType, float zoneSlowPercent, float duration, Color visualColor)
    {
        radius = zoneRadius;
        dps = zoneDps;
        damageType = dmgType;
        slowPercent = zoneSlowPercent;
        remainingDuration = duration;
        tickTimer = 0f;

        CreateFallbackVisualIfEmpty(visualColor);
    }

    private void CreateFallbackVisualIfEmpty(Color visualColor)
    {
        if (transform.childCount > 0 || GetComponent<Renderer>() != null) return;

        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "ZoneVisual";
        var col = disc.GetComponent<Collider>();
        if (col != null) Destroy(col);

        disc.transform.SetParent(transform, false);
        disc.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);
        disc.transform.localPosition = new Vector3(0f, 0.05f, 0f);

        var renderer = disc.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(visualColor.r, visualColor.g, visualColor.b, 0.45f);
        }
    }

    private void Update()
    {
        if (TimePhaseManager.Instance != null && TimePhaseManager.Instance.CurrentTimePhase != TimePhases.Night)
        {
            Destroy(gameObject);
            return;
        }

        remainingDuration -= Time.deltaTime;
        if (remainingDuration <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        tickTimer -= Time.deltaTime;
        if (tickTimer <= 0f)
        {
            tickTimer = TickInterval;
            ApplyTickEffects();
        }
    }

    private void ApplyTickEffects()
    {
        var enemies = EnemyRegistry.ActiveEnemies;
        if (enemies == null || enemies.Count == 0) return;

        var snapshot = new List<EnemyStats>(enemies);
        Vector3 center = transform.position;
        float radiusSq = radius * radius;

        foreach (var enemy in snapshot)
        {
            if (enemy == null || enemy.IsDead) continue;

            Vector3 diff = enemy.transform.position - center;
            diff.y = 0f;
            if (diff.sqrMagnitude > radiusSq) continue;

            if (dps > 0f)
            {
                enemy.TakeDamage(dps * TickInterval, damageType, 0f, 0f, false, 1f, null, true, DeathSource.Combat);
            }

            if (slowPercent > 0f)
            {
                var walker = enemy.GetComponent<EnemyWalker>();
                walker?.ApplySpellSlow(slowPercent, TickInterval + 0.15f);
            }
        }
    }
}
