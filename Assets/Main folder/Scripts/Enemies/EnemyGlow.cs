using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Globalny stan fioletowej poświaty wrogów (emisja materiału).
///     Siłą (0..1) i kolorem steruje DayNightCycle (poświata tylko w fazie Night, z płynnym przejściem),
///     a EnemyStats odświeża z tego emisję przez MaterialPropertyBlock.
///
///     Emisja URP/Lit działa tylko gdy materiał ma włączony keyword _EMISSION, którego nie da się ustawić
///     w MaterialPropertyBlock. Dlatego każdy wspólny materiał wroga jest raz kopiowany do wariantu z włączoną
///     emisją (współdzielonego przez wszystkich wrogów - nie powstają instancje per wróg).
///     Materiały spoza URP/Lit (np. Shader Graph bez właściwości _EmissionColor) są pomijane.
/// </summary>
public static class EnemyGlow
{
    public const float EliteMultiplier = 1.6f;
    public const float BossMultiplier = 2.2f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly Dictionary<Material, Material> variantCache = new Dictionary<Material, Material>();

    public static float Strength { get; private set; }
    public static Color GlowColor { get; private set; } = new Color(0.34f, 0.08f, 0.68f, 1f);

    public static event Action OnStrengthChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Strength = 0f;
        GlowColor = new Color(0.34f, 0.08f, 0.68f, 1f);
        OnStrengthChanged = null;
        variantCache.Clear();
    }

    public static void SetGlow(float strength, Color color)
    {
        strength = Mathf.Max(0f, strength);
        bool changed = !Mathf.Approximately(strength, Strength) || GlowColor != color;
        Strength = strength;
        GlowColor = color;
        if (changed) OnStrengthChanged?.Invoke();
    }

    public static Color GetEmission(EnemyRank rank)
    {
        float mult = rank == EnemyRank.Boss ? BossMultiplier : rank == EnemyRank.Elite ? EliteMultiplier : 1f;
        Color c = GlowColor * (Strength * mult);
        c.a = 1f;
        return c;
    }

    /// <summary>Podmienia współdzielone materiały renderów na warianty z włączoną emisją.</summary>
    public static void PrepareRenderers(Renderer[] renderers)
    {
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int m = 0; m < mats.Length; m++)
            {
                Material variant = GetVariant(mats[m]);
                if (variant != null && variant != mats[m])
                {
                    mats[m] = variant;
                    changed = true;
                }
            }

            if (changed) r.sharedMaterials = mats;
        }
    }

    private static Material GetVariant(Material source)
    {
        if (source == null || source.shader == null) return source;
        if (variantCache.TryGetValue(source, out Material cached)) return cached;

        Material result = source;
        string shaderName = source.shader.name;
        if (shaderName.StartsWith("Universal Render Pipeline/") && source.HasProperty(EmissionColorId))
        {
            result = new Material(source) { name = source.name + " (EnemyGlow)" };
            result.EnableKeyword("_EMISSION");
            result.SetColor(EmissionColorId, Color.black);
        }
        else
        {
            Debug.Log($"[EnemyGlow] Materiał '{source.name}' (shader '{shaderName}') nie obsługuje emisji URP/Lit - wróg z tym materiałem nie będzie świecił.");
        }

        variantCache[source] = result;
        return result;
    }
}
