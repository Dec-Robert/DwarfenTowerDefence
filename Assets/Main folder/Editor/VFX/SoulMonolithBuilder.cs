#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class SoulMonolithBuilder
{
    public const string BeamShaderName = "Custom/SoulMonolithBeam";
    public const string ParticleShaderName = "Custom/BombardMuzzleVFX";

    public const string BeamMaterialPath = "Assets/Main folder/Materials/VFX/SoulMonolithBeam.mat";
    public const string ParticlesMaterialPath = "Assets/Main folder/Materials/VFX/SoulMonolithVfxParticles.mat";
    public const string OriginVfxPrefabPath = "Assets/Main folder/Prefabs/VFX/SoulMonolithOriginVfx.prefab";
    public const string ImpactVfxPrefabPath = "Assets/Main folder/Prefabs/VFX/SoulMonolithImpactVfx.prefab";
    public const string SoulMonolithPrefabPath = "Assets/Main folder/Prefabs/Towers/Soul Monolith Pref.prefab";
    public const string SoulMonolithDataPath = "Assets/Main folder/ScriptableObjects/Towers/7. Soul Monolith.asset";

    static SoulMonolithBuilder()
    {
        EditorApplication.delayCall += EnsureBuiltIfMissing;
    }

    private static void EnsureBuiltIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        if (!IsSoulMonolithWired())
        {
            BuildAll(saveAssets: true);
        }
    }

    [MenuItem("Tools/VFX/Rebuild Soul Monolith Beam VFX", priority = 203)]
    public static void RebuildAllMenu()
    {
        BuildAll(saveAssets: true);
        Debug.Log("<color=cyan>[SoulMonolithBuilder] Skonfigurowano promień Monolitu Dusz (Soul Monolith Pref + TowerBeamWeapon + VFX).</color>");
    }

    public static void BuildAll(bool saveAssets = true)
    {
        EnsureFolder("Assets/Main folder/Materials/VFX");
        EnsureFolder("Assets/Main folder/Prefabs/VFX");

        Shader beamShader = Shader.Find(BeamShaderName);
        Shader particleShader = Shader.Find(ParticleShaderName);

        if (beamShader == null || particleShader == null)
        {
            Debug.LogError($"[SoulMonolithBuilder] Brak shaderów (Beam={beamShader != null}, Particles={particleShader != null}).");
            return;
        }

        Material beamMat = EnsureBeamMaterial(beamShader);
        Material particleMat = EnsureParticlesMaterial(particleShader);

        GameObject originVfxPrefab = BuildOriginVfxPrefab(particleMat);
        GameObject impactVfxPrefab = BuildImpactVfxPrefab(particleMat);

        WireSoulMonolithPrefab(beamMat, originVfxPrefab, impactVfxPrefab);
        EnsureTowerDataConfigured();

        if (saveAssets)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static bool IsSoulMonolithWired()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(OriginVfxPrefabPath) == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(ImpactVfxPrefabPath) == null)
        {
            return false;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SoulMonolithPrefabPath);
        if (prefab == null) return true;

        TowerController controller = prefab.GetComponent<TowerController>();
        TowerBeamWeapon beam = prefab.GetComponent<TowerBeamWeapon>();
        if (controller == null || beam == null) return false;

        Material particleMat = AssetDatabase.LoadAssetAtPath<Material>(ParticlesMaterialPath);
        if (particleMat == null || particleMat.GetFloat("_LobeAmplitude") > 0.001f)
        {
            return false;
        }

        return controller.BeamWeapon == beam &&
               controller.firePoint != null &&
               controller.TillDeath &&
               beam.BeamLine != null &&
               beam.OriginVfx != null &&
               beam.OriginVfx.main.startSize.constantMax <= 0.17f &&
               beam.ImpactVfx != null &&
               beam.ImpactVfx.main.startSize.constantMax <= 0.17f;
    }

    private static Material EnsureBeamMaterial(Shader shader)
    {
        Material mat = LoadOrCreateMaterial(BeamMaterialPath, shader, "SoulMonolithBeam");
        mat.SetColor("_LowChargeColor", new Color(0.22f, 0.92f, 1.0f, 1.0f));
        mat.SetColor("_MidChargeColor", new Color(0.42f, 0.55f, 1.45f, 1.0f));
        mat.SetColor("_HighChargeColor", new Color(1.45f, 0.32f, 1.85f, 1.0f));
        mat.SetColor("_CoreColor", new Color(1.35f, 1.55f, 1.8f, 1.0f));
        mat.SetFloat("_Charge01", 0f);
        mat.SetFloat("_ScrollSpeed", 4.5f);
        mat.SetFloat("_PulseSpeed", 7.5f);
        mat.SetFloat("_EdgeSoftness", 0.28f);
        mat.SetFloat("_Intensity", 1.65f);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material EnsureParticlesMaterial(Shader shader)
    {
        Material mat = LoadOrCreateMaterial(ParticlesMaterialPath, shader, "SoulMonolithVfxParticles");
        mat.SetColor("_Color", new Color(0.35f, 0.85f, 1.0f, 1.0f));
        mat.SetColor("_CoreColor", new Color(0.92f, 0.98f, 1.0f, 1.0f));
        mat.SetFloat("_Intensity", 2.0f);
        mat.SetFloat("_EdgeSoftness", 0.72f);
        mat.SetFloat("_FalloffPower", 1.55f);
        mat.SetFloat("_CoreBoost", 1.3f);
        mat.SetFloat("_CorePower", 2.1f);
        mat.SetFloat("_LobeCount", 1.0f);
        mat.SetFloat("_LobeAmplitude", 0.0f);
        mat.SetFloat("_TurbulenceSpeed", 0.0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.One);
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

    private static GameObject BuildOriginVfxPrefab(Material particleMat)
    {
        GameObject go = new GameObject("SoulMonolithOriginVfx");
        try
        {
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.duration = 1.0f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 32;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 18f;

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
                    new GradientColorKey(new Color(0.55f, 0.96f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.62f, 0.35f, 1.0f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0.0f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.85f),
                new Keyframe(0.35f, 1.0f),
                new Keyframe(1f, 0.0f)
            ));

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = particleMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return PrefabUtility.SaveAsPrefabAsset(go, OriginVfxPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static GameObject BuildImpactVfxPrefab(Material particleMat)
    {
        GameObject go = new GameObject("SoulMonolithImpactVfx");
        try
        {
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.duration = 1.0f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.20f, 0.38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 1.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 48;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 24f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.75f, 0.98f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.85f, 0.30f, 1.0f), 0.6f),
                    new GradientColorKey(new Color(0.45f, 0.10f, 0.80f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0.0f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.85f),
                new Keyframe(0.30f, 1.0f),
                new Keyframe(1f, 0.0f)
            ));

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = particleMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return PrefabUtility.SaveAsPrefabAsset(go, ImpactVfxPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void WireSoulMonolithPrefab(Material beamMat, GameObject originVfxPrefab, GameObject impactVfxPrefab)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SoulMonolithPrefabPath) == null)
        {
            Debug.LogWarning($"[SoulMonolithBuilder] Nie znaleziono {SoulMonolithPrefabPath}.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(SoulMonolithPrefabPath);
        try
        {
            TowerController controller = root.GetComponent<TowerController>();
            if (controller == null)
            {
                Debug.LogError("[SoulMonolithBuilder] Soul Monolith Pref nie posiada TowerController.");
                return;
            }

            Transform firePoint = controller.firePoint;
            if (firePoint == null)
            {
                Transform existing = root.transform.Find("FirePoint");
                if (existing != null)
                {
                    firePoint = existing;
                }
                else
                {
                    GameObject fpGo = new GameObject("FirePoint");
                    fpGo.transform.SetParent(root.transform, false);
                    fpGo.transform.position = ComputeMonolithApexPosition(root.transform);
                    fpGo.transform.localRotation = Quaternion.identity;
                    fpGo.transform.localScale = Vector3.one;
                    firePoint = fpGo.transform;
                }
            }

            RemoveChildrenByName(root.transform, "SoulBeamLine");
            RemoveChildrenByName(root.transform, "SoulMonolithOriginVfx");
            RemoveChildrenByName(root.transform, "SoulMonolithImpactVfx");

            GameObject lineGo = new GameObject("SoulBeamLine");
            lineGo.transform.SetParent(root.transform, false);
            LineRenderer line = lineGo.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.enabled = false;
            line.positionCount = 2;
            line.numCapVertices = 4;
            line.widthMultiplier = 0.10f;
            line.sharedMaterial = beamMat;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;

            Gradient lineGrad = new Gradient();
            lineGrad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(1.0f, 0.5f), new GradientAlphaKey(0.95f, 1f) }
            );
            line.colorGradient = lineGrad;

            GameObject originInstance = (GameObject)PrefabUtility.InstantiatePrefab(originVfxPrefab, root.transform);
            originInstance.transform.position = firePoint.position;
            ParticleSystem originPs = originInstance.GetComponent<ParticleSystem>();

            GameObject impactInstance = (GameObject)PrefabUtility.InstantiatePrefab(impactVfxPrefab, root.transform);
            impactInstance.transform.position = firePoint.position;
            ParticleSystem impactPs = impactInstance.GetComponent<ParticleSystem>();

            TowerBeamWeapon beam = root.GetComponent<TowerBeamWeapon>();
            if (beam == null)
            {
                beam = root.AddComponent<TowerBeamWeapon>();
            }

            SerializedObject beamSo = new SerializedObject(beam);
            beamSo.FindProperty("beamLine").objectReferenceValue = line;
            beamSo.FindProperty("originVfx").objectReferenceValue = originPs;
            beamSo.FindProperty("impactVfx").objectReferenceValue = impactPs;
            beamSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("firePoint").objectReferenceValue = firePoint;
            controllerSo.FindProperty("beamWeapon").objectReferenceValue = beam;
            controllerSo.FindProperty("tillDeath").boolValue = true;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(beam);
            EditorUtility.SetDirty(controller);
            PrefabUtility.SaveAsPrefabAsset(root, SoulMonolithPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureTowerDataConfigured()
    {
        TowerData data = AssetDatabase.LoadAssetAtPath<TowerData>(SoulMonolithDataPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SoulMonolithPrefabPath);
        if (data == null) return;

        bool dirty = false;
        if (prefab != null && data.prefab != prefab)
        {
            data.prefab = prefab;
            dirty = true;
        }
        if (data.bulletPrefab != null)
        {
            data.bulletPrefab = null;
            dirty = true;
        }
        if (!data.tillDeath)
        {
            data.tillDeath = true;
            dirty = true;
        }
        if (Mathf.Abs(data.baseRange - 3f) > 0.01f)
        {
            data.baseRange = 3f;
            dirty = true;
        }
        if (Mathf.Abs(data.baseDamage - 50f) > 0.01f)
        {
            data.baseDamage = 50f;
            dirty = true;
        }
        if (Mathf.Abs(data.fireRate - 10f) > 0.01f)
        {
            data.fireRate = 10f;
            dirty = true;
        }
        if (data.damageType != DamageType.Magic)
        {
            data.damageType = DamageType.Magic;
            dirty = true;
        }

        if (dirty)
        {
            EditorUtility.SetDirty(data);
        }
    }

    private static Vector3 ComputeMonolithApexPosition(Transform root)
    {
        MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
        Bounds combined = default;
        bool hasBounds = false;

        foreach (MeshFilter mf in filters)
        {
            if (mf.sharedMesh == null) continue;
            Bounds lb = mf.sharedMesh.bounds;
            Matrix4x4 l2w = mf.transform.localToWorldMatrix;

            for (int ix = 0; ix < 2; ix++)
            for (int iy = 0; iy < 2; iy++)
            for (int iz = 0; iz < 2; iz++)
            {
                Vector3 corner = new Vector3(
                    ix == 0 ? lb.min.x : lb.max.x,
                    iy == 0 ? lb.min.y : lb.max.y,
                    iz == 0 ? lb.min.z : lb.max.z);
                Vector3 wc = l2w.MultiplyPoint3x4(corner);
                if (!hasBounds)
                {
                    combined = new Bounds(wc, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(wc);
                }
            }
        }

        if (!hasBounds)
        {
            return new Vector3(0f, 1.35f, 0f);
        }

        return new Vector3(combined.center.x, Mathf.Lerp(combined.min.y, combined.max.y, 0.78f), combined.center.z);
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
