using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[TestFixture]
public class RepeaterVfxTests
{
    private const string SteamRepeaterDataPath = "Assets/Main folder/ScriptableObjects/Towers/3. Steam Repeater.asset";

    private readonly List<Object> createdObjects = new List<Object>();

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        GameObject repeaterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RepeaterVfxBuilder.RepeaterPrefabPath);
        TowerController controller = repeaterPrefab != null ? repeaterPrefab.GetComponent<TowerController>() : null;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(RepeaterVfxBuilder.FlashPrefabPath) == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(RepeaterVfxBuilder.SteamPrefabPath) == null ||
            controller == null ||
            controller.MuzzleFireVfx == null ||
            controller.MuzzleSmokeVfx == null ||
            controller.AlignSmokeToFirePoint)
        {
            RepeaterVfxBuilder.BuildAll(saveAssets: true);
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
    public void RepeaterVfxAssets_ExistAndUseValidMaterials()
    {
        Shader expectedShader = Shader.Find(RepeaterVfxBuilder.ParticleShaderName);
        Assert.IsNotNull(expectedShader, $"Nie znaleziono shadera {RepeaterVfxBuilder.ParticleShaderName}.");

        Material flashMat = AssetDatabase.LoadAssetAtPath<Material>(RepeaterVfxBuilder.FlashMaterialPath);
        Material steamMat = AssetDatabase.LoadAssetAtPath<Material>(RepeaterVfxBuilder.SteamMaterialPath);
        Assert.IsNotNull(flashMat, "Brak materiału RepeaterMuzzleFlash.mat.");
        Assert.IsNotNull(steamMat, "Brak materiału RepeaterSteamVent.mat.");
        Assert.AreEqual(expectedShader, flashMat.shader);
        Assert.AreEqual(expectedShader, steamMat.shader);

        GameObject flashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RepeaterVfxBuilder.FlashPrefabPath);
        GameObject steamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RepeaterVfxBuilder.SteamPrefabPath);
        Assert.IsNotNull(flashPrefab, "Brak prefaba RepeaterMuzzleFlash.");
        Assert.IsNotNull(steamPrefab, "Brak prefaba RepeaterSteamVent.");

        AssertValidParticlePrefab(flashPrefab.GetComponent<ParticleSystem>(), flashMat, "RepeaterMuzzleFlash");
        AssertValidParticlePrefab(steamPrefab.GetComponent<ParticleSystem>(), steamMat, "RepeaterSteamVent");
    }

    [Test]
    public void RepeaterPrefab_HasForwardMuzzlePointAndWiredSteamVfx()
    {
        TowerData repeaterData = AssetDatabase.LoadAssetAtPath<TowerData>(SteamRepeaterDataPath);
        GameObject repeaterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RepeaterVfxBuilder.RepeaterPrefabPath);
        Assert.IsNotNull(repeaterData);
        Assert.IsNotNull(repeaterPrefab);
        Assert.AreEqual(repeaterPrefab, repeaterData.prefab, "Steam Repeater powinien wskazywać na Reaper tmp.prefab.");

        TowerController controller = repeaterPrefab.GetComponent<TowerController>();
        Assert.IsNotNull(controller);
        Assert.IsNotNull(controller.partToRotate);
        Assert.IsNotNull(controller.firePoint);
        Assert.AreEqual("MuzzlePoint", controller.firePoint.name, "firePoint powinien wskazywać na dedykowany MuzzlePoint przy wylocie luf.");

        float forwardDot = Vector3.Dot(controller.firePoint.forward, controller.partToRotate.forward);
        Assert.Greater(forwardDot, 0.99f, "MuzzlePoint powinien być skierowany do przodu zgodnie z osią obrotu wieży.");

        Assert.IsNotNull(controller.MuzzleFireVfx, "Pole muzzleFireVfx powinno być podpięte.");
        Assert.IsNotNull(controller.MuzzleSmokeVfx, "Pole muzzleSmokeVfx (para z zaworu) powinno być podpięte.");
        Assert.IsFalse(controller.AlignSmokeToFirePoint, "Zawór pary powinien mieć alignSmokeToFirePoint = false, by pozostać na korpusie.");
        Assert.AreNotEqual(controller.firePoint, controller.MuzzleSmokeVfx.transform.parent, "Zawór pary nie powinien siedzieć na MuzzlePoint.");
    }

    [TestCase(0f)]
    [TestCase(90f)]
    [TestCase(-45f)]
    public void Shoot_TriggersMuzzleFlashForward_AndSteamVentUpwardAtValve(float yawAngle)
    {
        GameObject repeaterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RepeaterVfxBuilder.RepeaterPrefabPath);
        Assert.IsNotNull(repeaterPrefab);

        GameObject dummyBullet = new GameObject("RepeaterTestDummyBullet");
        createdObjects.Add(dummyBullet);

        TowerData testData = ScriptableObject.CreateInstance<TowerData>();
        testData.bulletPrefab = dummyBullet;
        createdObjects.Add(testData);

        GameObject towerGo = Object.Instantiate(repeaterPrefab, Vector3.zero, Quaternion.identity);
        createdObjects.Add(towerGo);

        TowerController controller = towerGo.GetComponent<TowerController>();
        controller.towerData = testData;
        if (controller.partToRotate != null)
        {
            controller.partToRotate.rotation = Quaternion.Euler(0f, yawAngle, 0f);
        }

        Vector3 valvePosBefore = controller.MuzzleSmokeVfx.transform.position;

        MethodInfo shootMethod = typeof(TowerController).GetMethod("Shoot", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(shootMethod);
        shootMethod.Invoke(controller, new object[] { false });

        GameObject spawnedBullet = GameObject.Find("RepeaterTestDummyBullet(Clone)");
        if (spawnedBullet != null)
        {
            createdObjects.Add(spawnedBullet);
        }

        // Zawór pary nie został przeniesiony do MuzzlePoint
        Assert.Less(Vector3.Distance(valvePosBefore, controller.MuzzleSmokeVfx.transform.position), 0.001f,
            "Zawór pary powinien pozostać na swojej pozycji na korpusie wieży.");

        // 1. Błysk wylotowy leci do przodu wzdłuż osi lufy
        ParticleSystem flash = controller.MuzzleFireVfx;
        flash.Simulate(0.03f, true, true, false);
        ParticleSystem.Particle[] particles = new ParticleSystem.Particle[32];
        int flashCount = flash.GetParticles(particles);
        Assert.Greater(flashCount, 0, "Błysk wystrzału powinien wyemitować cząsteczki.");

        Vector3 avgFlashOffset = AverageDisplacement(particles, flashCount, controller.firePoint.position);
        Assert.Greater(Vector3.Dot(avgFlashOffset.normalized, controller.firePoint.forward), 0.5f,
            "Błysk wystrzału powinien przemieszczać się do przodu z lufy.");

        // 2. Para z zaworu uchodzi w górę z korpusu
        ParticleSystem steam = controller.MuzzleSmokeVfx;
        steam.Simulate(0.08f, true, true, false);
        int steamCount = steam.GetParticles(particles);
        Assert.Greater(steamCount, 0, "Zawór pary powinien wyemitować cząsteczki przy strzale.");

        Vector3 avgSteamOffset = AverageDisplacement(particles, steamCount, valvePosBefore);
        Assert.Greater(avgSteamOffset.y, 0.01f, "Para z zaworu powinna unosić się ku górze.");
    }

    private static Vector3 AverageDisplacement(ParticleSystem.Particle[] particles, int count, Vector3 origin)
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            sum += (particles[i].position - origin);
        }
        return sum / count;
    }

    private static void AssertValidParticlePrefab(ParticleSystem ps, Material expectedMat, string label)
    {
        Assert.IsNotNull(ps, $"{label} musi mieć ParticleSystem.");
        Assert.IsFalse(ps.main.playOnAwake);
        Assert.IsFalse(ps.main.loop);
        Assert.AreEqual(ParticleSystemSimulationSpace.World, ps.main.simulationSpace);

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        Assert.IsNotNull(renderer);
        Assert.AreEqual(expectedMat, renderer.sharedMaterial);
        Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode);
        Assert.IsFalse(renderer.receiveShadows);
    }
}
