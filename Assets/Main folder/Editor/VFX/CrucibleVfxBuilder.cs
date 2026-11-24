#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class CrucibleVfxBuilder
{
    public const string BurnZoneShaderName = "Custom/CrucibleBurnZone";
    public const string FireParticlesShaderName = "Custom/BombardMuzzleVFX";
    public const string TrailShaderName = "Custom/ProjectileTrail";

    public const string FireballCoreMaterialPath = "Assets/Main folder/Materials/VFX/CrucibleFireballCore.mat";
    public const string FireballTrailMaterialPath = "Assets/Main folder/Materials/VFX/CrucibleFireballTrail.mat";
    public const string FireParticlesMaterialPath = "Assets/Main folder/Materials/VFX/CrucibleFireParticles.mat";
    public const string BurnZoneDiscMaterialPath = "Assets/Main folder/Materials/VFX/CrucibleBurnZoneDisc.mat";

    public const string GroundProjectilePrefabPath = "Assets/Main folder/Prefabs/Bullets/GroundProjectile.prefab";
    public const string GroundDamageZonePrefabPath = "Assets/Main folder/Prefabs/Bullets/GroundDamageZone.prefab";

    static CrucibleVfxBuilder()
    {
        EditorApplication.delayCall += EnsureBuiltIfMissing;
    }

    private static void EnsureBuiltIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        if (!IsCrucibleVfxWired())
        {
            BuildAll(saveAssets: true);
        }
    }

    [MenuItem("Tools/VFX/Rebuild Crucible VFX", priority = 201)]
    public static void RebuildAllMenu()
    {
        BuildAll(saveAssets: true);
        Debug.Log("<color=green>[CrucibleVfxBuilder] Zbudowano i podpięto efekty VFX Tygla Pożogi (pocisk + podpalony teren).</color>");
    }

    public static void BuildAll(bool saveAssets = true)
    {
        EnsureFolder("Assets/Main folder/Materials/VFX");

        Shader burnZoneShader = Shader.Find(BurnZoneShaderName);
        Shader fireParticlesShader = Shader.Find(FireParticlesShaderName);
        Shader trailShader = Shader.Find(TrailShaderName);

        if (burnZoneShader == null || fireParticlesShader == null || trailShader == null)
        {
            Debug.LogError($"[CrucibleVfxBuilder] Brakuje shaderów VFX (BurnZone={burnZoneShader != null}, Particles={fireParticlesShader != null}, Trail={trailShader != null}).");
            return;
        }

        Material coreMat = EnsureFireballCoreMaterial(burnZoneShader);
        Material trailMat = EnsureFireballTrailMaterial(trailShader);
        Material particleMat = EnsureFireParticlesMaterial(fireParticlesShader);
        Material discMat = EnsureBurnZoneDiscMaterial(burnZoneShader);

        UpdateGroundProjectilePrefab(coreMat, trailMat, particleMat);
        UpdateGroundDamageZonePrefab(discMat, particleMat);

        if (saveAssets)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static bool IsCrucibleVfxWired()
    {
        GameObject projPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GroundProjectilePrefabPath);
        GameObject zonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GroundDamageZonePrefabPath);
        if (projPrefab == null || zonePrefab == null) return true;

        GroundProjectile proj = projPrefab.GetComponent<GroundProjectile>();
        GroundDamageZone zone = zonePrefab.GetComponent<GroundDamageZone>();
        if (proj == null || zone == null) return true;

        return proj.TrailRenderer != null &&
               proj.FlightParticles != null &&
               zone.GroundDiscRenderer != null &&
               zone.ImpactBurstVfx != null &&
               zone.ZoneFlamesVfx != null;
    }

    private static Material EnsureFireballCoreMaterial(Shader shader)
    {
        Material mat = LoadOrCreateMaterial(FireballCoreMaterialPath, shader, "CrucibleFireballCore");
        mat.SetColor("_CoreColor", new Color(2.8f, 1.9f, 0.55f, 1f));
        mat.SetColor("_EmberColor", new Color(2.2f, 0.65f, 0.08f, 1f));
        mat.SetColor("_CharColor", new Color(0.42f, 0.08f, 0.02f, 1f));
        mat.SetFloat("_CrackScale", 3.5f);
        mat.SetFloat("_CrackSharpness", 1.8f);
        mat.SetFloat("_CoreHeat", 1.2f);
        mat.SetFloat("_PulseSpeed", 5.0f);
        mat.SetFloat("_EdgeNoiseScale", 2.0f);
        mat.SetFloat("_EdgeNoiseAmp", 0f);
        mat.SetFloat("_EdgeSoftness", 0.1f);
        mat.SetFloat("_PhaseFade", 1f);
        mat.SetFloat("_IsFireballCore", 1f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.One);
        mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
        mat.SetFloat("_ZWrite", 1f);
        mat.renderQueue = (int)RenderQueue.Geometry;
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material EnsureFireballTrailMaterial(Shader shader)
    {
        Material mat = LoadOrCreateMaterial(FireballTrailMaterialPath, shader, "CrucibleFireballTrail");
        mat.SetColor("_Color", new Color(1.0f, 0.62f, 0.18f, 1.0f));
        mat.SetFloat("_Intensity", 2.1f);
        mat.SetFloat("_FadePower", 1.5f);
        mat.SetFloat("_EdgeSoftness", 0.25f);
        mat.SetFloat("_CoreBoost", 0.32f);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material EnsureFireParticlesMaterial(Shader shader)
    {
        Material mat = LoadOrCreateMaterial(FireParticlesMaterialPath, shader, "CrucibleFireParticles");
        mat.SetColor("_Color", new Color(1.0f, 0.58f, 0.16f, 1.0f));
        mat.SetColor("_CoreColor", new Color(1.0f, 0.94f, 0.68f, 1.0f));
        mat.SetFloat("_Intensity", 2.1f);
        mat.SetFloat("_EdgeSoftness", 0.52f);
        mat.SetFloat("_FalloffPower", 1.3f);
        mat.SetFloat("_CoreBoost", 1.4f);
        mat.SetFloat("_CorePower", 2.3f);
        mat.SetFloat("_LobeCount", 5.0f);
        mat.SetFloat("_LobeAmplitude", 0.15f);
        mat.SetFloat("_TurbulenceSpeed", 8.0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.One);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material EnsureBurnZoneDiscMaterial(Shader shader)
    {
        Material mat = LoadOrCreateMaterial(BurnZoneDiscMaterialPath, shader, "CrucibleBurnZoneDisc");
        mat.SetColor("_CoreColor", new Color(2.4f, 1.35f, 0.28f, 1f));
        mat.SetColor("_EmberColor", new Color(1.9f, 0.48f, 0.06f, 1f));
        mat.SetColor("_CharColor", new Color(0.10f, 0.04f, 0.03f, 0.92f));
        mat.SetFloat("_CrackScale", 5.5f);
        mat.SetFloat("_CrackSharpness", 3.1f);
        mat.SetFloat("_CoreHeat", 0.85f);
        mat.SetFloat("_PulseSpeed", 3.2f);
        mat.SetFloat("_EdgeNoiseScale", 3.8f);
        mat.SetFloat("_EdgeNoiseAmp", 0.22f);
        mat.SetFloat("_EdgeSoftness", 0.24f);
        mat.SetFloat("_PhaseFade", 1f);
        mat.SetFloat("_IsFireballCore", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.renderQueue = -1;
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material LoadOrCreateMaterial(string path, Shader shader, string matName)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = matName };
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }
        return mat;
    }

    private static void UpdateGroundProjectilePrefab(Material coreMat, Material trailMat, Material particleMat)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(GroundProjectilePrefabPath) == null)
        {
            Debug.LogWarning($"[CrucibleVfxBuilder] Nie znaleziono {GroundProjectilePrefabPath}.");
            return;
        }

        Mesh sphereMesh = GetBuiltinMesh(PrimitiveType.Sphere);
        GameObject root = PrefabUtility.LoadPrefabContents(GroundProjectilePrefabPath);
        try
        {
            root.transform.localScale = new Vector3(0.26f, 0.26f, 0.26f);

            MeshFilter mf = root.GetComponent<MeshFilter>();
            if (mf != null && sphereMesh != null)
            {
                mf.sharedMesh = sphereMesh;
            }

            MeshRenderer mr = root.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = coreMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            RemoveChildrenByName(root.transform, "Trail");
            RemoveChildrenByName(root.transform, "FlightFlames");

            TrailRenderer trail = CreateFireballTrail(root.transform, trailMat);
            ParticleSystem flightFlames = CreateFlightFlames(root.transform, particleMat);

            GroundProjectile gp = root.GetComponent<GroundProjectile>();
            if (gp != null)
            {
                SerializedObject so = new SerializedObject(gp);
                so.FindProperty("trailRenderer").objectReferenceValue = trail;
                so.FindProperty("flightParticles").objectReferenceValue = flightFlames;
                so.FindProperty("detachVfxOnImpact").boolValue = true;
                so.FindProperty("orientToFlightTangent").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(gp);
            }

            PrefabUtility.SaveAsPrefabAsset(root, GroundProjectilePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static TrailRenderer CreateFireballTrail(Transform parent, Material trailMat)
    {
        GameObject trailGo = new GameObject("Trail");
        trailGo.transform.SetParent(parent, false);
        trailGo.transform.localPosition = Vector3.zero;
        trailGo.transform.localRotation = Quaternion.identity;
        trailGo.transform.localScale = Vector3.one;

        TrailRenderer trail = trailGo.AddComponent<TrailRenderer>();
        trail.time = 0.28f;
        trail.minVertexDistance = 0.08f;
        trail.widthMultiplier = 0.24f;
        trail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 1.0f),
            new Keyframe(0.55f, 0.45f),
            new Keyframe(1f, 0.0f)
        );

        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1.0f, 0.94f, 0.65f), 0.0f),
                new GradientColorKey(new Color(1.0f, 0.48f, 0.10f), 0.45f),
                new GradientColorKey(new Color(0.62f, 0.12f, 0.02f), 1.0f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0.0f),
                new GradientAlphaKey(0.65f, 0.45f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        trail.colorGradient = grad;
        trail.sharedMaterial = trailMat;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.autodestruct = false;
        return trail;
    }

    private static ParticleSystem CreateFlightFlames(Transform parent, Material particleMat)
    {
        GameObject psGo = new GameObject("FlightFlames");
        psGo.transform.SetParent(parent, false);
        psGo.transform.localPosition = Vector3.zero;
        psGo.transform.localRotation = Quaternion.identity;
        psGo.transform.localScale = Vector3.one;

        ParticleSystem ps = psGo.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = psGo.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.40f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.30f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = 64;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 38f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1.0f, 0.92f, 0.60f), 0.0f),
                new GradientColorKey(new Color(1.0f, 0.46f, 0.08f), 0.45f),
                new GradientColorKey(new Color(0.55f, 0.10f, 0.02f), 1.0f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0.0f),
                new GradientAlphaKey(0.70f, 0.4f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.85f),
            new Keyframe(0.3f, 1.0f),
            new Keyframe(1f, 0.1f)
        ));

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sharedMaterial = particleMat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    private static void UpdateGroundDamageZonePrefab(Material discMat, Material particleMat)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(GroundDamageZonePrefabPath) == null)
        {
            Debug.LogWarning($"[CrucibleVfxBuilder] Nie znaleziono {GroundDamageZonePrefabPath}.");
            return;
        }

        Mesh quadMesh = GetBuiltinMesh(PrimitiveType.Quad);
        GameObject root = PrefabUtility.LoadPrefabContents(GroundDamageZonePrefabPath);
        try
        {
            MeshRenderer rootRenderer = root.GetComponent<MeshRenderer>();
            if (rootRenderer != null)
            {
                rootRenderer.enabled = false;
                rootRenderer.shadowCastingMode = ShadowCastingMode.Off;
                rootRenderer.receiveShadows = false;
            }

            RemoveChildrenByName(root.transform, "ScorchedGroundDisc");
            RemoveChildrenByName(root.transform, "ImpactBurstVfx");
            RemoveChildrenByName(root.transform, "ZoneFlamesVfx");

            Renderer discRenderer = CreateScorchedDisc(root.transform, quadMesh, discMat);
            ParticleSystem impactBurst = CreateImpactBurstVfx(root.transform, particleMat);
            ParticleSystem zoneFlames = CreateZoneFlamesVfx(root.transform, particleMat);

            GroundDamageZone zone = root.GetComponent<GroundDamageZone>();
            if (zone != null)
            {
                SerializedObject so = new SerializedObject(zone);
                so.FindProperty("groundDiscRenderer").objectReferenceValue = discRenderer;
                so.FindProperty("impactBurstVfx").objectReferenceValue = impactBurst;
                so.FindProperty("zoneFlamesVfx").objectReferenceValue = zoneFlames;
                so.FindProperty("igniteDuration").floatValue = 0.2f;
                so.FindProperty("fadeOutDuration").floatValue = 0.7f;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(zone);
            }

            PrefabUtility.SaveAsPrefabAsset(root, GroundDamageZonePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Renderer CreateScorchedDisc(Transform parent, Mesh quadMesh, Material discMat)
    {
        GameObject discGo = new GameObject("ScorchedGroundDisc");
        discGo.transform.SetParent(parent, false);
        discGo.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        discGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        discGo.transform.localScale = new Vector3(1.18f, 1.18f, 1f);

        MeshFilter mf = discGo.AddComponent<MeshFilter>();
        mf.sharedMesh = quadMesh;

        MeshRenderer mr = discGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = discMat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return mr;
    }

    private static ParticleSystem CreateImpactBurstVfx(Transform parent, Material particleMat)
    {
        GameObject go = new GameObject("ImpactBurstVfx");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        // Kompensacja niejednorodnej skali rodzica GroundDamageZone (1.6, 0.4, 1.6)
        go.transform.localScale = new Vector3(0.625f, 0.625f, 2.5f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.duration = 0.45f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.4f, 4.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.24f, 0.46f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0.35f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 32;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.25f;

        var limitVel = ps.limitVelocityOverLifetime;
        limitVel.enabled = true;
        limitVel.limit = 0.4f;
        limitVel.dampen = 0.4f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1.0f, 0.95f, 0.72f), 0.0f),
                new GradientColorKey(new Color(1.0f, 0.50f, 0.10f), 0.45f),
                new GradientColorKey(new Color(0.68f, 0.14f, 0.02f), 1.0f)
            },
            new[]
            {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.8f, 0.4f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.25f, 1.05f),
            new Keyframe(1f, 0.15f)
        ));

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sharedMaterial = particleMat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingFudge = 0.2f;
        return ps;
    }

    private static ParticleSystem CreateZoneFlamesVfx(Transform parent, Material particleMat)
    {
        GameObject go = new GameObject("ZoneFlamesVfx");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        // Kompensacja niejednorodnej skali rodzica GroundDamageZone (1.6, 0.4, 1.6)
        go.transform.localScale = new Vector3(0.625f, 0.625f, 2.5f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.78f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.18f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 48;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 24f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.66f;
        shape.radiusThickness = 0.85f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1.0f, 0.88f, 0.45f), 0.0f),
                new GradientColorKey(new Color(1.0f, 0.45f, 0.08f), 0.50f),
                new GradientColorKey(new Color(0.55f, 0.10f, 0.02f), 1.0f)
            },
            new[]
            {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(0.85f, 0.18f),
                new GradientAlphaKey(0.65f, 0.55f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.6f),
            new Keyframe(0.35f, 1.05f),
            new Keyframe(1f, 0.25f)
        ));

        var rotationOverLifetime = ps.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sharedMaterial = particleMat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingFudge = 0.1f;
        return ps;
    }

    private static Mesh GetBuiltinMesh(PrimitiveType primitiveType)
    {
        GameObject temp = GameObject.CreatePrimitive(primitiveType);
        try
        {
            return temp.GetComponent<MeshFilter>().sharedMesh;
        }
        finally
        {
            Object.DestroyImmediate(temp);
        }
    }

    private static void RemoveChildrenByName(Transform parent, string childName)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void EnsureFolder(string assetFolderPath)
    {
        if (AssetDatabase.IsValidFolder(assetFolderPath)) return;

        string parent = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(assetFolderPath);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
#endif
