using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[TestFixture]
public class SimpleProjectileTrailTests
{
    private const string BulletSmallPrefabPath = "Assets/Main folder/Prefabs/Bullets/Bullet small.prefab";
    private const string TrailMaterialPath = "Assets/Main folder/Materials/VFX/SmallBulletTrail.mat";
    private const string TrailShaderName = "Custom/ProjectileTrail";

    private readonly List<Object> createdObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }
        createdObjects.Clear();
    }

    [Test]
    public void BulletSmallPrefab_HasValidTrailSetup()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletSmallPrefabPath);
        Assert.IsNotNull(prefab, "Nie znaleziono prefaba Bullet small.");

        SimpleProjectile projectile = prefab.GetComponent<SimpleProjectile>();
        Assert.IsNotNull(projectile, "Bullet small powinien mieć komponent SimpleProjectile.");

        Transform trailChild = prefab.transform.Find("Trail");
        Assert.IsNotNull(trailChild, "Bullet small powinien mieć obiekt potomny Trail.");

        TrailRenderer trail = trailChild.GetComponent<TrailRenderer>();
        Assert.IsNotNull(trail, "Obiekt Trail powinien mieć komponent TrailRenderer.");

        Material expectedMat = AssetDatabase.LoadAssetAtPath<Material>(TrailMaterialPath);
        Assert.IsNotNull(expectedMat, "Nie znaleziono materiału SmallBulletTrail.mat.");
        Assert.AreEqual(expectedMat, trail.sharedMaterial, "TrailRenderer powinien używać SmallBulletTrail.mat.");
        Assert.IsNotNull(expectedMat.shader, "Materiał SmallBulletTrail powinien mieć przypisany shader.");
        Assert.AreEqual(TrailShaderName, expectedMat.shader.name);

        Assert.AreEqual(ShadowCastingMode.Off, trail.shadowCastingMode);
        Assert.IsFalse(trail.receiveShadows);
        Assert.AreEqual(0.14f, trail.time, 0.001f);
        Assert.AreEqual(0.07f, trail.widthMultiplier, 0.001f);

        FieldInfo trailField = typeof(SimpleProjectile).GetField("trailRenderer", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(trailField);
        Assert.AreEqual(trail, trailField.GetValue(projectile), "SimpleProjectile.trailRenderer powinien być podpięty w prefabie.");
    }

    [Test]
    public void Launch_OrientsProjectileTowardsTarget()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletSmallPrefabPath);
        GameObject bulletGo = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        createdObjects.Add(bulletGo);

        GameObject targetGo = new GameObject("Target");
        targetGo.transform.position = new Vector3(10f, 0f, 0f);
        createdObjects.Add(targetGo);

        SimpleProjectile projectile = bulletGo.GetComponent<SimpleProjectile>();
        InvokePrivate(projectile, "Awake");
        projectile.Initialize(25f, DamageType.Physical, null, false, 1.5f);
        projectile.Launch(targetGo.transform, targetGo.transform.position);

        float dot = Vector3.Dot(bulletGo.transform.forward, Vector3.right);
        Assert.Greater(dot, 0.999f, "Pocisk powinien obrócić się w stronę celu podczas Launch.");
    }

    [Test]
    public void Launch_NormalVsCritical_AppliesGradientAndWidthWithoutMaterialLeak()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletSmallPrefabPath);
        Material expectedSharedMat = AssetDatabase.LoadAssetAtPath<Material>(TrailMaterialPath);

        GameObject targetGo = new GameObject("Target");
        targetGo.transform.position = new Vector3(0f, 0f, 8f);
        createdObjects.Add(targetGo);

        // Normalny strzał
        GameObject normalGo = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        createdObjects.Add(normalGo);
        SimpleProjectile normalProj = normalGo.GetComponent<SimpleProjectile>();
        TrailRenderer normalTrail = normalGo.GetComponentInChildren<TrailRenderer>();

        InvokePrivate(normalProj, "Awake");
        normalProj.Initialize(20f, DamageType.Physical, null, false, 2f);
        normalProj.Launch(targetGo.transform, targetGo.transform.position);

        // Strzał krytyczny
        GameObject critGo = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        createdObjects.Add(critGo);
        SimpleProjectile critProj = critGo.GetComponent<SimpleProjectile>();
        TrailRenderer critTrail = critGo.GetComponentInChildren<TrailRenderer>();

        InvokePrivate(critProj, "Awake");
        critProj.Initialize(20f, DamageType.Physical, null, true, 2f);
        critProj.Launch(targetGo.transform, targetGo.transform.position);

        Assert.AreSame(expectedSharedMat, normalTrail.sharedMaterial, "Normalny strzał nie może tworzyć instancji materiału.");
        Assert.AreSame(expectedSharedMat, critTrail.sharedMaterial, "Strzał krytyczny nie może tworzyć instancji materiału.");

        Assert.AreEqual(0.07f, normalTrail.widthMultiplier, 0.001f);
        Assert.AreEqual(0.07f * 1.25f, critTrail.widthMultiplier, 0.001f);

        Color normalHead = normalTrail.colorGradient.colorKeys[0].color;
        Color critHead = critTrail.colorGradient.colorKeys[0].color;
        Assert.AreNotEqual(normalHead, critHead, "Strzał krytyczny powinien wyróżniać się kolorem gradientu.");
        Assert.Greater(critHead.r, critHead.b, "Krytyczny gradient powinien mieć ciepły złocisty odcień.");
    }

    [Test]
    public void HitTarget_DetachesTrailAndDestroysProjectile()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletSmallPrefabPath);
        GameObject bulletGo = Object.Instantiate(prefab, new Vector3(3f, 1f, 2f), Quaternion.identity);
        createdObjects.Add(bulletGo);

        TrailRenderer trail = bulletGo.GetComponentInChildren<TrailRenderer>();
        createdObjects.Add(trail.gameObject);

        GameObject targetGo = new GameObject("Target");
        targetGo.transform.position = bulletGo.transform.position;
        createdObjects.Add(targetGo);

        SimpleProjectile projectile = bulletGo.GetComponent<SimpleProjectile>();
        InvokePrivate(projectile, "Awake");
        projectile.Initialize(10f, DamageType.Physical, null, false, 1.5f);
        projectile.Launch(targetGo.transform, targetGo.transform.position);

        InvokePrivate(projectile, "Update");

        Assert.IsTrue(bulletGo == null, "Pocisk powinien zostać zniszczony po trafieniu.");
        Assert.IsTrue(trail != null, "Obiekt Trail powinien przetrwać zniszczenie pocisku, aby płynnie dogasnąć.");
        Assert.IsNull(trail.transform.parent, "Trail powinien zostać odpięty od rodzica przed zniszczeniem pocisku.");
        Assert.IsFalse(trail.emitting, "Odpięty Trail powinien wyłączyć emisję nowych punktów.");
        Assert.IsTrue(trail.autodestruct, "Odpięty Trail powinien mieć włączone autodestruct.");
    }

    [Test]
    public void SimpleProjectile_WithoutTrailRenderer_DoesNotThrow()
    {
        GameObject bulletGo = new GameObject("PlainBullet");
        bulletGo.transform.position = Vector3.zero;
        createdObjects.Add(bulletGo);

        SimpleProjectile projectile = bulletGo.AddComponent<SimpleProjectile>();

        GameObject targetGo = new GameObject("Target");
        targetGo.transform.position = Vector3.zero;
        createdObjects.Add(targetGo);

        Assert.DoesNotThrow(() =>
        {
            InvokePrivate(projectile, "Awake");
            projectile.Initialize(10f, DamageType.Physical, null, true, 2f);
            projectile.Launch(targetGo.transform, targetGo.transform.position);
            InvokePrivate(projectile, "Update");
        });

        Assert.IsTrue(bulletGo == null, "Pocisk bez TrailRenderera nadal powinien trafić i się zniszczyć.");
    }

    private static void InvokePrivate(object instance, string methodName)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, $"Nie znaleziono metody {methodName}.");
        method.Invoke(instance, null);
    }
}
