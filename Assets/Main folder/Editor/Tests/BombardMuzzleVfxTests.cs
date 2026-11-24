using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[TestFixture]
public class BombardMuzzleVfxTests
{
    private const string RoyalBombardDataPath = "Assets/Main folder/ScriptableObjects/Towers/6. Royal Bombard.asset";

    private readonly List<Object> createdObjects = new List<Object>();

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BombardMuzzleVfxBuilder.FirePrefabPath) == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(BombardMuzzleVfxBuilder.SmokePrefabPath) == null)
        {
            BombardMuzzleVfxBuilder.BuildAll(saveAssets: true);
        }
    }

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
    public void BombardVfxAssets_ExistAndUseProceduralShader()
    {
        Shader expectedShader = Shader.Find(BombardMuzzleVfxBuilder.ShaderName);
        Assert.IsNotNull(expectedShader, $"Nie znaleziono shadera {BombardMuzzleVfxBuilder.ShaderName}.");

        Material fireMat = AssetDatabase.LoadAssetAtPath<Material>(BombardMuzzleVfxBuilder.FireMaterialPath);
        Material smokeMat = AssetDatabase.LoadAssetAtPath<Material>(BombardMuzzleVfxBuilder.SmokeMaterialPath);
        Assert.IsNotNull(fireMat, "Nie znaleziono materiału BombardMuzzleFire.mat.");
        Assert.IsNotNull(smokeMat, "Nie znaleziono materiału BombardMuzzleSmoke.mat.");
        Assert.AreEqual(expectedShader, fireMat.shader, "BombardMuzzleFire.mat powinien używać proceduralnego shadera.");
        Assert.AreEqual(expectedShader, smokeMat.shader, "BombardMuzzleSmoke.mat powinien używać proceduralnego shadera.");

        GameObject firePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BombardMuzzleVfxBuilder.FirePrefabPath);
        GameObject smokePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BombardMuzzleVfxBuilder.SmokePrefabPath);
        Assert.IsNotNull(firePrefab, "Nie znaleziono prefaba BombardMuzzleFire.");
        Assert.IsNotNull(smokePrefab, "Nie znaleziono prefaba BombardMuzzleSmoke.");

        AssertValidVfxParticleSystem(firePrefab.GetComponent<ParticleSystem>(), fireMat, "BombardMuzzleFire");
        AssertValidVfxParticleSystem(smokePrefab.GetComponent<ParticleSystem>(), smokeMat, "BombardMuzzleSmoke");
    }

    [Test]
    public void CannonTowerPrefab_HasMuzzleFireAndSmokeWired()
    {
        GameObject cannonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BombardMuzzleVfxBuilder.CannonTowerPrefabPath);
        Assert.IsNotNull(cannonPrefab, "Nie znaleziono prefaba Cannon tower.");

        TowerData bombardData = AssetDatabase.LoadAssetAtPath<TowerData>(RoyalBombardDataPath);
        Assert.IsNotNull(bombardData, "Nie znaleziono assetu 6. Royal Bombard.");
        Assert.AreEqual(cannonPrefab, bombardData.prefab, "Royal Bombard powinien wskazywać na Cannon tower.prefab.");

        TowerController controller = cannonPrefab.GetComponent<TowerController>();
        Assert.IsNotNull(controller, "Cannon tower powinien posiadać TowerController.");
        Assert.IsNotNull(controller.firePoint, "Cannon tower powinien mieć ustawiony firePoint.");

        Assert.IsNotNull(controller.MuzzleFireVfx, "Pole muzzleFireVfx powinno być podpięte w Cannon tower.prefab.");
        Assert.IsNotNull(controller.MuzzleSmokeVfx, "Pole muzzleSmokeVfx powinno być podpięte w Cannon tower.prefab.");

        Assert.AreEqual(controller.firePoint, controller.MuzzleFireVfx.transform.parent, "Ogień powinien być dzieckiem Shooting Point.");
        Assert.AreEqual(controller.firePoint, controller.MuzzleSmokeVfx.transform.parent, "Dym powinien być dzieckiem Shooting Point.");

        Assert.IsFalse(controller.MuzzleFireVfx.main.playOnAwake);
        Assert.IsFalse(controller.MuzzleFireVfx.main.loop);
        Assert.IsFalse(controller.MuzzleSmokeVfx.main.playOnAwake);
        Assert.IsFalse(controller.MuzzleSmokeVfx.main.loop);
    }

    [TestCase(0f)]
    [TestCase(90f)]
    [TestCase(-60f)]
    [TestCase(180f)]
    public void Shoot_TriggersBothMuzzleEffects_InForwardBarrelDirection(float yawAngle)
    {
        GameObject cannonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BombardMuzzleVfxBuilder.CannonTowerPrefabPath);
        Assert.IsNotNull(cannonPrefab);

        GameObject dummyBullet = new GameObject("BombardTestDummyBullet");
        createdObjects.Add(dummyBullet);

        TowerData testData = ScriptableObject.CreateInstance<TowerData>();
        testData.bulletPrefab = dummyBullet;
        createdObjects.Add(testData);

        GameObject towerGo = Object.Instantiate(cannonPrefab, Vector3.zero, Quaternion.identity);
        createdObjects.Add(towerGo);

        TowerController controller = towerGo.GetComponent<TowerController>();
        controller.towerData = testData;
        if (controller.partToRotate != null)
        {
            controller.partToRotate.rotation = Quaternion.Euler(0f, yawAngle, 0f);
        }

        MethodInfo shootMethod = typeof(TowerController).GetMethod("Shoot", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(shootMethod, "Nie znaleziono metody TowerController.Shoot.");
        shootMethod.Invoke(controller, new object[] { false });

        GameObject spawnedBullet = GameObject.Find("BombardTestDummyBullet(Clone)");
        if (spawnedBullet != null)
        {
            createdObjects.Add(spawnedBullet);
        }

        Vector3 muzzlePos = controller.firePoint.position;
        Vector3 expectedForward = controller.firePoint.forward;

        AssertEmitsForward(controller.MuzzleFireVfx, muzzlePos, expectedForward, "Ogień wystrzału");
        AssertEmitsForward(controller.MuzzleSmokeVfx, muzzlePos, expectedForward, "Dym wystrzału");
    }

    [Test]
    public void TowerController_WithoutMuzzleVfx_DoesNotThrowOnShoot()
    {
        GameObject dummyBullet = new GameObject("PlainTowerDummyBullet");
        createdObjects.Add(dummyBullet);

        TowerData testData = ScriptableObject.CreateInstance<TowerData>();
        testData.bulletPrefab = dummyBullet;
        createdObjects.Add(testData);

        GameObject towerGo = new GameObject("PlainTower");
        createdObjects.Add(towerGo);
        towerGo.AddComponent<TowerEntity>();
        TowerController controller = towerGo.AddComponent<TowerController>();
        controller.towerData = testData;

        MethodInfo shootMethod = typeof(TowerController).GetMethod("Shoot", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(shootMethod);

        Assert.DoesNotThrow(() => shootMethod.Invoke(controller, new object[] { false }));

        GameObject spawnedBullet = GameObject.Find("PlainTowerDummyBullet(Clone)");
        if (spawnedBullet != null)
        {
            createdObjects.Add(spawnedBullet);
        }
    }

    private static void AssertValidVfxParticleSystem(ParticleSystem ps, Material expectedMat, string label)
    {
        Assert.IsNotNull(ps, $"{label} musi mieć komponent ParticleSystem.");
        Assert.IsFalse(ps.main.playOnAwake, $"{label} nie powinien odtwarzać się przy starcie (playOnAwake = false).");
        Assert.IsFalse(ps.main.loop, $"{label} nie powinien być zapętlony.");
        Assert.AreEqual(ParticleSystemSimulationSpace.World, ps.main.simulationSpace, $"{label} powinien symulować w World Space.");

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        Assert.IsNotNull(renderer, $"{label} musi mieć ParticleSystemRenderer.");
        Assert.AreEqual(expectedMat, renderer.sharedMaterial, $"{label} powinien używać oczekiwanego materiału.");
        Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode, $"{label} nie powinien rzucać cieni.");
        Assert.IsFalse(renderer.receiveShadows, $"{label} nie powinien przyjmować cieni.");
    }

    private static void AssertEmitsForward(ParticleSystem ps, Vector3 muzzlePos, Vector3 expectedForward, string label)
    {
        Assert.IsNotNull(ps, $"{label} nie może być null.");

        ps.Simulate(0.05f, true, true, false);
        ParticleSystem.Particle[] particles = new ParticleSystem.Particle[64];
        int count = ps.GetParticles(particles);
        Assert.Greater(count, 0, $"{label} powinien wyemitować cząsteczki po wystrzale.");

        Vector3 avgDisplacement = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            avgDisplacement += (particles[i].position - muzzlePos);
        }
        avgDisplacement /= count;

        Assert.Greater(avgDisplacement.magnitude, 0.01f, $"{label} powinien przemieścić cząsteczki od wylotu lufy.");
        float forwardDot = Vector3.Dot(avgDisplacement.normalized, expectedForward.normalized);
        Assert.Greater(forwardDot, 0.5f, $"{label} powinien lecieć do przodu wzdłuż osi lufy (dot={forwardDot:F3}).");
    }
}
