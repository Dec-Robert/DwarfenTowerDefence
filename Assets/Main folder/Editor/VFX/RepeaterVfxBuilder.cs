#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class RepeaterVfxBuilder
{
    public const string ParticleShaderName = "Custom/BombardMuzzleVFX";
    public const string FlashMaterialPath = "Assets/Main folder/Materials/VFX/RepeaterMuzzleFlash.mat";
    public const string SteamMaterialPath = "Assets/Main folder/Materials/VFX/RepeaterSteamVent.mat";
    public const string FlashPrefabPath = "Assets/Main folder/Prefabs/VFX/RepeaterMuzzleFlash.prefab";
    public const string SteamPrefabPath = "Assets/Main folder/Prefabs/VFX/RepeaterSteamVent.prefab";
    public const string RepeaterPrefabPath = "Assets/Main folder/Prefabs/Towers/Reapeter tmp.prefab";

    static RepeaterVfxBuilder()
    {
        EditorApplication.delayCall += EnsureBuiltIfMissing;
    }

    private static void EnsureBuiltIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        bool needsBuild =
            AssetDatabase.LoadAssetAtPath<GameObject>(FlashPrefabPath) == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(SteamPrefabPath) == null ||
            !IsRepeaterWired();

        if (needsBuild)
        {
            BuildAll(saveAssets: true);
        }
    }

    [MenuItem("Tools/VFX/Rebuild Repeater VFX", priority = 202)]
    public static void RebuildAllMenu()
    {
        BuildAll(saveAssets: true);
        Debug.Log("<color=green>[RepeaterVfxBuilder] Zbudowano i podpięto efekty VFX Repetiera Parowego (Muzzle Flash + Steam Vent).</color>");
    }

    public static void BuildAll(bool saveAssets = true)
    {
        EnsureFolder("Assets/Main folder/Materials/VFX");
        EnsureFolder("Assets/Main folder/Prefabs/VFX");

        Shader vfxShader = Shader.Find(ParticleShaderName);
        if (vfxShader == null)
        {
            Debug.LogError($"[RepeaterVfxBuilder] Nie znaleziono shadera '{ParticleShaderName}'.");
            return;
        }

        Material flashMat = EnsureFlashMaterial(vfxShader);
        Material steamMat = EnsureSteamMaterial(vfxShader);

        GameObject flashPrefab = BuildFlashPrefab(flashMat);
        GameObject steamPrefab = BuildSteamPrefab(steamMat);

        WireRepeaterPrefab(flashPrefab, steamPrefab);

        if (saveAssets)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static bool IsRepeaterWired()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RepeaterPrefabPath);
        if (prefab == null) return true;

        TowerController controller = prefab.GetComponent<TowerController>();
        if (controller == null) return true;

        return controller.MuzzleFireVfx != null &&
               controller.MuzzleSmokeVfx != null &&
               !controller.AlignSmokeToFirePoint &&
               controller.firePoint != null &&
               controller.firePoint.name == "MuzzlePoint";
    }

    private static Material EnsureFlashMaterial(Shader shader)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(FlashMaterialPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = "RepeaterMuzzleFlash" };
            AssetDatabase.CreateAsset(mat, FlashMaterialPath);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }

        mat.SetColor("_Color", new Color(1.0f, 0.72f, 0.24f, 1.0f));
        mat.SetColor("_CoreColor", new Color(1.0f, 0.98f, 0.82f, 1.0f));
        mat.SetFloat("_Intensity", 2.6f);
        mat.SetFloat("_EdgeSoftness", 0.45f);
        mat.SetFloat("_FalloffPower", 1.4f);
        mat.SetFloat("_CoreBoost", 1.9f);
        mat.SetFloat("_CorePower", 2.6f);
        mat.SetFloat("_LobeCount", 6.0f);
        mat.SetFloat("_LobeAmplitude", 0.16f);
        mat.SetFloat("_TurbulenceSpeed", 15.0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.One);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material EnsureSteamMaterial(Shader shader)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(SteamMaterialPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = "RepeaterSteamVent" };
            AssetDatabase.CreateAsset(mat, SteamMaterialPath);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }

        mat.SetColor("_Color", new Color(0.92f, 0.96f, 1.0f, 0.62f));
        mat.SetColor("_CoreColor", new Color(1.0f, 1.0f, 1.0f, 1.0f));
        mat.SetFloat("_Intensity", 1.05f);
        mat.SetFloat("_EdgeSoftness", 0.62f);
        mat.SetFloat("_FalloffPower", 1.1f);
        mat.SetFloat("_CoreBoost", 0.12f);
        mat.SetFloat("_CorePower", 2.0f);
        mat.SetFloat("_LobeCount", 4.0f);
        mat.SetFloat("_LobeAmplitude", 0.14f);
        mat.SetFloat("_TurbulenceSpeed", 2.8f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static GameObject BuildFlashPrefab(Material flashMat)
    {
        GameObject go = new GameObject("RepeaterMuzzleFlash");
        try
        {
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.duration = 0.09f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.8f, 4.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 32;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 16f;
            shape.radius = 0.04f;
            shape.radiusThickness = 1f;

            var limitVel = ps.limitVelocityOverLifetime;
            limitVel.enabled = true;
            limitVel.separateAxes = false;
            limitVel.limit = 0.3f;
            limitVel.dampen = 0.5f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1.0f, 0.96f, 0.76f), 0.0f),
                    new GradientColorKey(new Color(1.0f, 0.60f, 0.14f), 0.45f),
                    new GradientColorKey(new Color(0.85f, 0.25f, 0.05f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.85f, 0.35f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.65f),
                new Keyframe(0.3f, 1.05f),
                new Keyframe(1f, 0.15f)
            ));

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = flashMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = 0.2f;

            return PrefabUtility.SaveAsPrefabAsset(go, FlashPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static GameObject BuildSteamPrefab(Material steamMat)
    {
        GameObject go = new GameObject("RepeaterSteamVent");
        try
        {
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.duration = 0.40f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.gravityModifier = -0.18f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 48;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 4) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.03f;
            shape.radiusThickness = 1f;

            var limitVel = ps.limitVelocityOverLifetime;
            limitVel.enabled = true;
            limitVel.separateAxes = false;
            limitVel.limit = 0.35f;
            limitVel.dampen = 0.32f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.95f, 0.97f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.84f, 0.88f, 0.92f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.55f, 0.0f),
                    new GradientAlphaKey(0.38f, 0.4f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.50f),
                new Keyframe(0.45f, 1.15f),
                new Keyframe(1f, 1.45f)
            ));

            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = steamMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return PrefabUtility.SaveAsPrefabAsset(go, SteamPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void WireRepeaterPrefab(GameObject flashPrefab, GameObject steamPrefab)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RepeaterPrefabPath) == null)
        {
            Debug.LogWarning($"[RepeaterVfxBuilder] Nie znaleziono {RepeaterPrefabPath}.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(RepeaterPrefabPath);
        try
        {
            TowerController controller = root.GetComponent<TowerController>();
            if (controller == null)
            {
                Debug.LogError("[RepeaterVfxBuilder] Reaper tmp.prefab nie posiada TowerController.");
                return;
            }

            Transform turretRot = controller.partToRotate != null
                ? controller.partToRotate
                : root.transform.Find("Tmp/Obracany");

            if (turretRot == null)
            {
                Debug.LogError("[RepeaterVfxBuilder] Nie znaleziono Obracany w Reaper tmp.prefab.");
                return;
            }

            RemoveChildrenByName(turretRot, "MuzzlePoint");
            RemoveChildrenByName(turretRot, "SteamValvePoint");

            Transform drum = turretRot.Find("Cylinder.001");
            Vector3 muzzleWorldPos = ComputeMuzzleWorldPos(turretRot, drum);
            Vector3 valveWorldPos = ComputeSteamValveWorldPos(turretRot, drum);

            GameObject muzzlePointGo = new GameObject("MuzzlePoint");
            muzzlePointGo.transform.SetParent(turretRot, false);
            muzzlePointGo.transform.position = muzzleWorldPos;
            muzzlePointGo.transform.rotation = turretRot.rotation;
            muzzlePointGo.transform.localScale = ComputeUnitCompensatedLocalScale(turretRot);

            GameObject flashInstance = (GameObject)PrefabUtility.InstantiatePrefab(flashPrefab, muzzlePointGo.transform);
            flashInstance.transform.localPosition = Vector3.zero;
            flashInstance.transform.localRotation = Quaternion.identity;
            flashInstance.transform.localScale = Vector3.one;
            ParticleSystem flashPs = flashInstance.GetComponent<ParticleSystem>();

            GameObject valvePointGo = new GameObject("SteamValvePoint");
            valvePointGo.transform.SetParent(turretRot, false);
            valvePointGo.transform.position = valveWorldPos;
            valvePointGo.transform.rotation = turretRot.rotation * Quaternion.Euler(-115f, 0f, 0f);
            valvePointGo.transform.localScale = ComputeUnitCompensatedLocalScale(turretRot);

            GameObject steamInstance = (GameObject)PrefabUtility.InstantiatePrefab(steamPrefab, valvePointGo.transform);
            steamInstance.transform.localPosition = Vector3.zero;
            steamInstance.transform.localRotation = Quaternion.identity;
            steamInstance.transform.localScale = Vector3.one;
            ParticleSystem steamPs = steamInstance.GetComponent<ParticleSystem>();

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("firePoint").objectReferenceValue = muzzlePointGo.transform;
            so.FindProperty("muzzleFireVfx").objectReferenceValue = flashPs;
            so.FindProperty("muzzleSmokeVfx").objectReferenceValue = steamPs;
            so.FindProperty("alignSmokeToFirePoint").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(controller);
            PrefabUtility.SaveAsPrefabAsset(root, RepeaterPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Vector3 ComputeMuzzleWorldPos(Transform turretRot, Transform drum)
    {
        if (drum != null && TryComputeWorldBounds(drum, includeSelf: false, out Bounds barrelBounds))
        {
            return new Vector3(barrelBounds.center.x, barrelBounds.center.y, barrelBounds.max.z + 0.02f);
        }

        if (TryComputeWorldBounds(turretRot, includeSelf: false, out Bounds turretBounds))
        {
            return new Vector3(turretBounds.center.x, turretBounds.center.y, turretBounds.max.z + 0.02f);
        }

        return new Vector3(0f, 0.63f, 0.72f);
    }

    private static Vector3 ComputeSteamValveWorldPos(Transform turretRot, Transform drum)
    {
        if (drum != null && TryComputeWorldBounds(drum, includeSelf: true, out Bounds drumBounds))
        {
            float zPos = Mathf.Lerp(drumBounds.min.z, drumBounds.max.z, 0.28f);
            return new Vector3(drumBounds.center.x, drumBounds.max.y + 0.02f, zPos);
        }

        if (TryComputeWorldBounds(turretRot, includeSelf: false, out Bounds turretBounds))
        {
            float zPos = Mathf.Lerp(turretBounds.min.z, turretBounds.max.z, 0.25f);
            return new Vector3(turretBounds.center.x, turretBounds.max.y + 0.02f, zPos);
        }

        return new Vector3(0f, 0.85f, 0.05f);
    }

    private static bool TryComputeWorldBounds(Transform targetRoot, bool includeSelf, out Bounds combinedBounds)
    {
        combinedBounds = default;
        bool hasAny = false;

        MeshFilter[] filters = targetRoot.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter mf in filters)
        {
            if (!includeSelf && mf.transform == targetRoot) continue;
            if (mf.sharedMesh == null) continue;

            Bounds localBounds = mf.sharedMesh.bounds;
            Matrix4x4 l2w = mf.transform.localToWorldMatrix;

            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;

            for (int ix = 0; ix < 2; ix++)
            for (int iy = 0; iy < 2; iy++)
            for (int iz = 0; iz < 2; iz++)
            {
                Vector3 corner = new Vector3(ix == 0 ? min.x : max.x, iy == 0 ? min.y : max.y, iz == 0 ? min.z : max.z);
                Vector3 worldCorner = l2w.MultiplyPoint3x4(corner);
                if (!hasAny)
                {
                    combinedBounds = new Bounds(worldCorner, Vector3.zero);
                    hasAny = true;
                }
                else
                {
                    combinedBounds.Encapsulate(worldCorner);
                }
            }
        }

        return hasAny;
    }

    private static Vector3 ComputeUnitCompensatedLocalScale(Transform parent)
    {
        Vector3 pScale = parent.lossyScale;
        return new Vector3(
            Mathf.Abs(pScale.x) > 0.0001f ? 1f / pScale.x : 1f,
            Mathf.Abs(pScale.y) > 0.0001f ? 1f / pScale.y : 1f,
            Mathf.Abs(pScale.z) > 0.0001f ? 1f / pScale.z : 1f
        );
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
