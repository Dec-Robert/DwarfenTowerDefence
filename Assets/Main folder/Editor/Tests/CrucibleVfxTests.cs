using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[TestFixture]
public class CrucibleVfxTests
{
    private const string CrucibleTowerDataPath = "Assets/Main folder/ScriptableObjects/Towers/4. Crucible of Conflagrations.asset";

    private readonly List<Object> createdObjects = new List<Object>();

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        GameObject projPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrucibleVfxBuilder.GroundProjectilePrefabPath);
        GameObject zonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrucibleVfxBuilder.GroundDamageZonePrefabPath);
        GroundProjectile proj = projPrefab != null ? projPrefab.GetComponent<GroundProjectile>() : null;
        GroundDamageZone zone = zonePrefab != null ? zonePrefab.GetComponent<GroundDamageZone>() : null;

        if (proj == null || proj.TrailRenderer == null || proj.FlightParticles == null ||
            zone == null || zone.GroundDiscRenderer == null || zone.ImpactBurstVfx == null || zone.ZoneFlamesVfx == null)
        {
            CrucibleVfxBuilder.BuildAll(saveAssets: true);
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
    public void CrucibleVfxAssets_ExistAndHaveValidMaterialsAndShaders()
    {
        Shader burnZoneShader = Shader.Find(CrucibleVfxBuilder.BurnZoneShaderName);
        Shader fireParticlesShader = Shader.Find(CrucibleVfxBuilder.FireParticlesShaderName);
        Shader trailShader = Shader.Find(CrucibleVfxBuilder.TrailShaderName);

        Assert.IsNotNull(burnZoneShader, $"Nie znaleziono shadera {CrucibleVfxBuilder.BurnZoneShaderName}.");
        Assert.IsNotNull(fireParticlesShader, $"Nie znaleziono shadera {CrucibleVfxBuilder.FireParticlesShaderName}.");
        Assert.IsNotNull(trailShader, $"Nie znaleziono shadera {CrucibleVfxBuilder.TrailShaderName}.");

        Material coreMat = AssetDatabase.LoadAssetAtPath<Material>(CrucibleVfxBuilder.FireballCoreMaterialPath);
        Material trailMat = AssetDatabase.LoadAssetAtPath<Material>(CrucibleVfxBuilder.FireballTrailMaterialPath);
        Material particleMat = AssetDatabase.LoadAssetAtPath<Material>(CrucibleVfxBuilder.FireParticlesMaterialPath);
        Material discMat = AssetDatabase.LoadAssetAtPath<Material>(CrucibleVfxBuilder.BurnZoneDiscMaterialPath);

        Assert.IsNotNull(coreMat, "Brak materiału CrucibleFireballCore.mat.");
        Assert.IsNotNull(trailMat, "Brak materiału CrucibleFireballTrail.mat.");
        Assert.IsNotNull(particleMat, "Brak materiału CrucibleFireParticles.mat.");
        Assert.IsNotNull(discMat, "Brak materiału CrucibleBurnZoneDisc.mat.");

        Assert.AreEqual(burnZoneShader, coreMat.shader);
        Assert.AreEqual(trailShader, trailMat.shader);
        Assert.AreEqual(fireParticlesShader, particleMat.shader);
        Assert.AreEqual(burnZoneShader, discMat.shader);
    }

    [Test]
    public void GroundProjectilePrefab_HasFireballCoreTrailAndFlightParticles()
    {
        TowerData crucibleData = AssetDatabase.LoadAssetAtPath<TowerData>(CrucibleTowerDataPath);
        GameObject projPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrucibleVfxBuilder.GroundProjectilePrefabPath);
        Assert.IsNotNull(crucibleData);
        Assert.IsNotNull(projPrefab);
        Assert.AreEqual(projPrefab, crucibleData.bulletPrefab, "Tygiel Pożogi powinien używać GroundProjectile.prefab.");

        GroundProjectile gp = projPrefab.GetComponent<GroundProjectile>();
        Assert.IsNotNull(gp);

        MeshRenderer coreRenderer = projPrefab.GetComponent<MeshRenderer>();
        Material expectedCoreMat = AssetDatabase.LoadAssetAtPath<Material>(CrucibleVfxBuilder.FireballCoreMaterialPath);
        Assert.IsNotNull(coreRenderer);
        Assert.AreEqual(expectedCoreMat, coreRenderer.sharedMaterial, "Rdzeń pocisku powinien używać CrucibleFireballCore.mat.");
        Assert.AreEqual(ShadowCastingMode.Off, coreRenderer.shadowCastingMode);
        Assert.IsFalse(coreRenderer.receiveShadows);

        Assert.IsNotNull(gp.TrailRenderer, "GroundProjectile powinien mieć podpięty TrailRenderer.");
        Assert.IsNotNull(gp.FlightParticles, "GroundProjectile powinien mieć podpięty ParticleSystem lotu.");
        Assert.AreEqual(ParticleSystemSimulationSpace.World, gp.FlightParticles.main.simulationSpace);
        Assert.IsFalse(gp.FlightParticles.main.playOnAwake);
        Assert.IsTrue(gp.FlightParticles.main.loop);
    }

    [Test]
    public void GroundProjectile_Impact_SpawnsZoneAndDetachesFlightVfx()
    {
        GameObject projPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrucibleVfxBuilder.GroundProjectilePrefabPath);
        GameObject projGo = Object.Instantiate(projPrefab, Vector3.zero, Quaternion.identity);
        createdObjects.Add(projGo);

        GroundProjectile gp = projGo.GetComponent<GroundProjectile>();
        TrailRenderer trail = gp.TrailRenderer;
        ParticleSystem flames = gp.FlightParticles;
        createdObjects.Add(trail.gameObject);
        createdObjects.Add(flames.gameObject);

        Vector3 targetPos = new Vector3(0.05f, 0f, 0f);
        gp.Initialize(10f, DamageType.Magic, null, false, 1.5f);
        gp.Launch(null, targetPos);

        Assert.IsTrue(trail.emitting);
        Assert.IsTrue(flames.isPlaying);

        var zonesBefore = new HashSet<GroundDamageZone>(Object.FindObjectsByType<GroundDamageZone>(FindObjectsSortMode.None));
        InvokePrivate(gp, "Update");

        GroundDamageZone spawnedZone = null;
        foreach (var zone in Object.FindObjectsByType<GroundDamageZone>(FindObjectsSortMode.None))
        {
            if (!zonesBefore.Contains(zone))
            {
                spawnedZone = zone;
                createdObjects.Add(zone.gameObject);
                break;
            }
        }

        Assert.IsTrue(projGo == null, "Pocisk powinien zostać zniszczony po uderzeniu w ziemię.");
        Assert.IsNotNull(spawnedZone, "Uderzenie pocisku powinno utworzyć strefę GroundDamageZone.");
        Assert.IsTrue(trail != null && trail.transform.parent == null, "Smuga powinna zostać odpięta od pocisku.");
        Assert.IsFalse(trail.emitting, "Odpięta smuga powinna wyłączyć emisję.");
        Assert.IsTrue(flames != null && flames.transform.parent == null, "Cząsteczki płomieni powinny zostać odpięte od pocisku.");
        Assert.IsFalse(flames.isEmitting, "Odpięte płomienie powinny zatrzymać emisję nowych cząsteczek.");
    }

    [Test]
    public void GroundDamageZonePrefab_HasScorchedDiscImpactBurstAndLoopingFlames()
    {
        GameObject zonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrucibleVfxBuilder.GroundDamageZonePrefabPath);
        Assert.IsNotNull(zonePrefab);

        GroundDamageZone zone = zonePrefab.GetComponent<GroundDamageZone>();
        Assert.IsNotNull(zone);

        MeshRenderer rootRenderer = zonePrefab.GetComponent<MeshRenderer>();
        Assert.IsTrue(rootRenderer == null || !rootRenderer.enabled, "Domyślny cylinder na korzeniu powinien być wyłączony.");

        BoxCollider collider = zonePrefab.GetComponent<BoxCollider>();
        Assert.IsNotNull(collider);
        Assert.IsTrue(collider.isTrigger);

        Material expectedDiscMat = AssetDatabase.LoadAssetAtPath<Material>(CrucibleVfxBuilder.BurnZoneDiscMaterialPath);
        Assert.IsNotNull(zone.GroundDiscRenderer, "GroundDamageZone powinien mieć podpięty dysk spalonej ziemi.");
        Assert.AreEqual(expectedDiscMat, zone.GroundDiscRenderer.sharedMaterial);
        Assert.AreEqual(ShadowCastingMode.Off, zone.GroundDiscRenderer.shadowCastingMode);

        Assert.IsNotNull(zone.ImpactBurstVfx, "GroundDamageZone powinien mieć podpięty ImpactBurstVfx.");
        Assert.IsFalse(zone.ImpactBurstVfx.main.loop);
        Assert.IsFalse(zone.ImpactBurstVfx.main.playOnAwake);

        Assert.IsNotNull(zone.ZoneFlamesVfx, "GroundDamageZone powinien mieć podpięty ZoneFlamesVfx.");
        Assert.IsTrue(zone.ZoneFlamesVfx.main.loop);
        Assert.IsFalse(zone.ZoneFlamesVfx.main.playOnAwake);
    }

    [Test]
    public void GroundDamageZone_Lifecycle_StopsFlamesAndFadesOutViaPropertyBlock_WithoutMaterialLeak()
    {
        GameObject zonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrucibleVfxBuilder.GroundDamageZonePrefabPath);
        Material expectedDiscMat = AssetDatabase.LoadAssetAtPath<Material>(CrucibleVfxBuilder.BurnZoneDiscMaterialPath);

        GameObject zoneGo = Object.Instantiate(zonePrefab, Vector3.zero, Quaternion.identity);
        createdObjects.Add(zoneGo);

        GroundDamageZone zone = zoneGo.GetComponent<GroundDamageZone>();
        zone.Setup(10f, DamageType.Magic, null, 0.5f, 5f, 0f, 0f, 1.5f, 0f);

        Assert.AreSame(expectedDiscMat, zone.GroundDiscRenderer.sharedMaterial, "MaterialPropertyBlock nie może tworzyć instancji materiału.");
        Assert.IsTrue(zone.ZoneFlamesVfx.isEmitting, "Płomienie strefy powinny być aktywne po Setup.");
        Assert.Less(zone.CurrentPhaseFade, 0.5f, "Na starcie strefa powinna zaczynać od fazy rozpalenia.");

        // Faza aktywnego płonięcia (po 0.2s igniteDuration)
        zone.AdvanceVisualLifecycle(0.5f);
        Assert.AreEqual(1f, zone.CurrentPhaseFade, 0.001f);
        Assert.IsTrue(zone.ZoneFlamesVfx.isEmitting);

        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        zone.GroundDiscRenderer.GetPropertyBlock(mpb);
        Assert.AreEqual(1f, mpb.GetFloat("_PhaseFade"), 0.001f);

        // Wejście w fazę wygasania (ostatnie 0.7s z 5s)
        zone.AdvanceVisualLifecycle(4.1f); // łączny czas 4.6s
        Assert.IsFalse(zone.ZoneFlamesVfx.isEmitting, "Emisja płomieni powinna zostać zatrzymana przed końcem życia strefy.");
        Assert.Less(zone.CurrentPhaseFade, 0.7f);

        zone.GroundDiscRenderer.GetPropertyBlock(mpb);
        Assert.AreEqual(zone.CurrentPhaseFade, mpb.GetFloat("_PhaseFade"), 0.001f);

        // Koniec czasu trwania
        zone.AdvanceVisualLifecycle(0.4f); // łączny czas 5.0s
        Assert.AreEqual(0f, zone.CurrentPhaseFade, 0.001f);
        Assert.AreSame(expectedDiscMat, zone.GroundDiscRenderer.sharedMaterial);
    }

    private static void InvokePrivate(object instance, string methodName)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, $"Nie znaleziono metody {methodName}.");
        method.Invoke(instance, null);
    }
}
