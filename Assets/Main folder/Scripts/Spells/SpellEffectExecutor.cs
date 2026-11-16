using System.Collections.Generic;
using UnityEngine;

public static class SpellEffectExecutor
{
    public static void Execute(DruidTowerEntity tower, Vector3 worldTarget)
    {
        if (tower == null || tower.ActiveSpell == null) return;

        SpellDataSO spell = tower.ActiveSpell;
        float damage = tower.GetEffectiveDamage();
        float radius = tower.GetEffectiveRadius();
        SpellSpecializationData spec = tower.GetActiveSpecialization();

        if (spell.vfxPrefab != null)
        {
            var vfx = Object.Instantiate(spell.vfxPrefab, worldTarget, Quaternion.identity);
            Object.Destroy(vfx, 3f);
        }
        else
        {
            SpawnFallbackBurstVfx(worldTarget, radius, spell.indicatorColor);
        }

        var enemies = EnemyRegistry.ActiveEnemies;
        if (enemies != null && enemies.Count > 0)
        {
            var snapshot = new List<EnemyStats>(enemies);
            float radiusSq = radius * radius;

            float slowPercent = spell.baseSlowPercent;
            float slowDuration = spell.baseSlowDuration;

            if (spec != null)
            {
                if (spec.appliesFreeze)
                {
                    slowPercent = 1.0f;
                    slowDuration = Mathf.Max(slowDuration, spec.bonusSlowDuration);
                }
                else if (spec.bonusSlowPercent > 0f)
                {
                    slowPercent = Mathf.Clamp01(slowPercent + spec.bonusSlowPercent);
                    slowDuration = Mathf.Max(slowDuration, spec.bonusSlowDuration);
                }
            }

            foreach (var enemy in snapshot)
            {
                if (enemy == null || enemy.IsDead) continue;

                Vector3 diff = enemy.transform.position - worldTarget;
                diff.y = 0f;
                if (diff.sqrMagnitude > radiusSq) continue;

                if (damage > 0f)
                {
                    enemy.TakeDamage(damage, spell.damageType, 0f, 0f, false, 1f, null, true, DeathSource.Combat);
                }

                if (slowPercent > 0f && slowDuration > 0f && enemy != null && !enemy.IsDead)
                {
                    var walker = enemy.GetComponent<EnemyWalker>();
                    walker?.ApplySpellSlow(slowPercent, slowDuration);
                }
            }
        }

        if (spec != null && spec.spawnsGroundZone)
        {
            GameObject zoneObj = spell.groundZonePrefab != null
                ? Object.Instantiate(spell.groundZonePrefab, worldTarget, Quaternion.identity)
                : new GameObject($"SpellZone_{spell.spellId}");

            zoneObj.transform.position = worldTarget;
            var zone = zoneObj.GetComponent<SpellGroundZone>();
            if (zone == null) zone = zoneObj.AddComponent<SpellGroundZone>();

            zone.Initialize(
                radius,
                spec.groundZoneDps,
                spell.damageType,
                spec.groundZoneSlowPercent,
                spec.groundZoneDuration,
                spell.indicatorColor
            );
        }
    }

    private static void SpawnFallbackBurstVfx(Vector3 worldTarget, float radius, Color indicatorColor)
    {
        var burst = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        burst.name = "SpellFallbackBurstVfx";
        var col = burst.GetComponent<Collider>();
        if (col != null) Object.Destroy(col);

        burst.transform.position = new Vector3(worldTarget.x, worldTarget.y + 0.08f, worldTarget.z);
        var renderer = burst.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SpellCastManager.ApplyTranslucentColor(renderer, new Color(indicatorColor.r, indicatorColor.g, indicatorColor.b, 0.65f));
        }

        var vfx = burst.AddComponent<SpellFallbackBurstVfx>();
        vfx.Initialize(radius, indicatorColor);
    }
}

public class SpellFallbackBurstVfx : MonoBehaviour
{
    private float targetRadius;
    private Color baseColor;
    private float elapsed;
    private const float Duration = 0.45f;
    private Material cachedMaterial;

    public void Initialize(float radius, Color color)
    {
        targetRadius = Mathf.Max(0.5f, radius);
        baseColor = color;
        var renderer = GetComponent<Renderer>();
        if (renderer != null) cachedMaterial = renderer.material;
        transform.localScale = new Vector3(0.4f, 0.06f, 0.4f);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Duration);

        float currentDiameter = Mathf.Lerp(0.4f, targetRadius * 2f, 1f - (1f - t) * (1f - t));
        transform.localScale = new Vector3(currentDiameter, 0.06f, currentDiameter);

        if (cachedMaterial != null)
        {
            Color c = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(0.65f, 0f, t));
            if (cachedMaterial.HasProperty("_BaseColor")) cachedMaterial.SetColor("_BaseColor", c);
            cachedMaterial.color = c;
        }

        if (elapsed >= Duration)
        {
            Destroy(gameObject);
        }
    }
}
