#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class BombardMuzzleVfxBuilder
{
    public const string ShaderName = "Custom/BombardMuzzleVFX";
    public const string FireMaterialPath = "Assets/Main folder/Materials/VFX/BombardMuzzleFire.mat";
    public const string SmokeMaterialPath = "Assets/Main folder/Materials/VFX/BombardMuzzleSmoke.mat";
    public const string FirePrefabPath = "Assets/Main folder/Prefabs/VFX/BombardMuzzleFire.prefab";
    public const string SmokePrefabPath = "Assets/Main folder/Prefabs/VFX/BombardMuzzleSmoke.prefab";
    public const string CannonTowerPrefabPath = "Assets/Main folder/Prefabs/Towers/Bombard Pref.prefab";

    static BombardMuzzleVfxBuilder()
    {
        EditorApplication.delayCall += EnsureBuiltIfMissing;
    }

    private static void EnsureBuiltIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        bool needsBuild =
            AssetDatabase.LoadAssetAtPath<GameObject>(FirePrefabPath) == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(SmokePrefabPath) == null ||
            !IsCannonTowerWired();

        if (needsBuild)
        {
            BuildAll(saveAssets: true);
        }
    }

    [MenuItem("Tools/VFX/Rebuild Bombard Muzzle VFX", priority = 200)]
    public static void RebuildAllMenu()
    {
        BuildAll(saveAssets: true);
        Debug.Log("<color=green>[BombardMuzzleVfxBuilder] Zbudowano i podpięto efekty VFX wystrzału Bombardy (Ogień + Dym).</color>");
    }

    public static void BuildAll(bool saveAssets = true)
    {
        EnsureFolder("Assets/Main folder/Materials/VFX");
        EnsureFolder("Assets/Main folder/Prefabs/VFX");

        Shader vfxShader = Shader.Find(ShaderName);
        if (vfxShader == null)
        {
            Debug.LogError($"[BombardMuzzleVfxBuilder] Nie znaleziono shadera '{ShaderName}'.");
            return;
        }

        Material fireMat = EnsureFireMaterial(vfxShader);
        Material smokeMat = EnsureSmokeMaterial(vfxShader);

        GameObject firePrefab = BuildFirePrefab(fireMat);
        GameObject smokePrefab = BuildSmokePrefab(smokeMat);

        WireCannonTowerPrefab(firePrefab, smokePrefab);

        if (saveAssets)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static bool IsCannonTowerWired()
    {
        GameObject towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CannonTowerPrefabPath);
        if (towerPrefab == null) return true;

        TowerController controller = towerPrefab.GetComponent<TowerController>();
        if (controller == null) return true;

        return controller.MuzzleFireVfx != null && controller.MuzzleSmokeVfx != null;
    }

    private static Material EnsureFireMaterial(Shader shader)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(FireMaterialPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = "BombardMuzzleFire" };
            AssetDatabase.CreateAsset(mat, FireMaterialPath);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }

        mat.SetColor("_Color", new Color(1.0f, 0.68f, 0.22f, 1.0f));
        mat.SetColor("_CoreColor", new Color(1.0f, 0.96f, 0.78f, 1.0f));
        mat.SetFloat("_Intensity", 2.4f);
        mat.SetFloat("_EdgeSoftness", 0.50f);
        mat.SetFloat("_FalloffPower", 1.35f);
        mat.SetFloat("_CoreBoost", 1.65f);
        mat.SetFloat("_CorePower", 2.5f);
        mat.SetFloat("_LobeCount", 5.0f);
        mat.SetFloat("_LobeAmplitude", 0.14f);
        mat.SetFloat("_TurbulenceSpeed", 11.0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.One);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material EnsureSmokeMaterial(Shader shader)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterialPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = "BombardMuzzleSmoke" };
            AssetDatabase.CreateAsset(mat, SmokeMaterialPath);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }

        mat.SetColor("_Color", new Color(0.78f, 0.76f, 0.73f, 0.88f));
        mat.SetColor("_CoreColor", new Color(1.0f, 0.86f, 0.68f, 1.0f));
        mat.SetFloat("_Intensity", 1.0f);
        mat.SetFloat("_EdgeSoftness", 0.56f);
        mat.SetFloat("_FalloffPower", 1.15f);
        mat.SetFloat("_CoreBoost", 0.14f);
        mat.SetFloat("_CorePower", 2.1f);
        mat.SetFloat("_LobeCount", 4.0f);
        mat.SetFloat("_LobeAmplitude", 0.12f);
        mat.SetFloat("_TurbulenceSpeed", 1.8f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static GameObject BuildFirePrefab(Material fireMat)
    {
        GameObject go = new GameObject("BombardMuzzleFire");
        try
        {
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.duration = 0.20f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.10f, 0.17f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.4f, 5.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.48f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 32;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.05f;
            shape.radiusThickness = 1f;

            var limitVel = ps.limitVelocityOverLifetime;
            limitVel.enabled = true;
            limitVel.separateAxes = false;
            limitVel.limit = 0.4f;
            limitVel.dampen = 0.45f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fireGradient = new Gradient();
            fireGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1.00f, 0.96f, 0.72f), 0.0f),
                    new GradientColorKey(new Color(1.00f, 0.56f, 0.12f), 0.45f),
                    new GradientColorKey(new Color(0.75f, 0.18f, 0.04f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.85f, 0.35f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fireGradient);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0.0f, 0.45f),
                new Keyframe(0.25f, 1.05f),
                new Keyframe(1.0f, 0.20f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = fireMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = 0.2f;

            return PrefabUtility.SaveAsPrefabAsset(go, FirePrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static GameObject BuildSmokePrefab(Material smokeMat)
    {
        GameObject go = new GameObject("BombardMuzzleSmoke");
        try
        {
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.duration = 1.25f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.85f, 1.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.36f, 0.64f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = Color.white;
            main.gravityModifier = -0.06f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 64;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.08f;
            shape.radiusThickness = 1f;

            var limitVel = ps.limitVelocityOverLifetime;
            limitVel.enabled = true;
            limitVel.separateAxes = false;
            limitVel.limit = 0.22f;
            limitVel.dampen = 0.36f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient smokeGradient = new Gradient();
            smokeGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.76f, 0.72f, 0.66f), 0.0f),
                    new GradientColorKey(new Color(0.46f, 0.46f, 0.48f), 0.38f),
                    new GradientColorKey(new Color(0.24f, 0.24f, 0.26f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.78f, 0.0f),
                    new GradientAlphaKey(0.62f, 0.35f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(smokeGradient);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0.0f, 0.55f),
                new Keyframe(0.40f, 1.15f),
                new Keyframe(1.0f, 1.55f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = smokeMat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return PrefabUtility.SaveAsPrefabAsset(go, SmokePrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void WireCannonTowerPrefab(GameObject firePrefab, GameObject smokePrefab)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CannonTowerPrefabPath) == null)
        {
            Debug.LogWarning($"[BombardMuzzleVfxBuilder] Nie znaleziono {CannonTowerPrefabPath}.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(CannonTowerPrefabPath);
        try
        {
            TowerController controller = root.GetComponent<TowerController>();
            if (controller == null)
            {
                Debug.LogError("[BombardMuzzleVfxBuilder] Cannon tower.prefab nie posiada TowerController.");
                return;
            }

            Transform firePoint = controller.firePoint != null
                ? controller.firePoint
                : root.transform.Find("Pivot/Shooting Point");

            if (firePoint == null)
            {
                Debug.LogError("[BombardMuzzleVfxBuilder] Nie znaleziono Shooting Point w Cannon tower.prefab.");
                return;
            }

            RemoveExistingChild(firePoint, "BombardMuzzleFire");
            RemoveExistingChild(firePoint, "BombardMuzzleSmoke");

            ParticleSystem firePs = AttachVfxChild(firePrefab, firePoint);
            ParticleSystem smokePs = AttachVfxChild(smokePrefab, firePoint);

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("muzzleFireVfx").objectReferenceValue = firePs;
            so.FindProperty("muzzleSmokeVfx").objectReferenceValue = smokePs;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(controller);
            PrefabUtility.SaveAsPrefabAsset(root, CannonTowerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RemoveExistingChild(Transform parent, string childName)
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

    private static ParticleSystem AttachVfxChild(GameObject prefab, Transform parent)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localScale = Vector3.one;
        // Kompensacja odwróconej osi Z na głównym obiekcie Cannon tower (scale.z = -1) dla podglądu przy domyślnym kącie
        instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        return instance.GetComponent<ParticleSystem>();
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
