using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public class DayNightAndBuildingLightTests
{
    private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();
    private TimePhaseManager timeManager;
    private DayNightCycle dayNightCycle;
    private Light sunLight;

    [SetUp]
    public void SetUp()
    {
        var timeGo = Track(new GameObject("Test_TimePhaseManager"));
        timeManager = timeGo.AddComponent<TimePhaseManager>();
        timeManager.OnGameSpeedChange += _ => { };
        InvokeMethod(timeManager, "Start");

        var sunGo = Track(new GameObject("Test_SunLight"));
        sunLight = sunGo.AddComponent<Light>();
        sunLight.type = LightType.Directional;

        var dncGo = Track(new GameObject("Test_DayNightCycle"));
        dayNightCycle = dncGo.AddComponent<DayNightCycle>();
        dayNightCycle.sunLight = sunLight;
        dayNightCycle.transitionDuration = 1.0f;
        InvokeMethod(dayNightCycle, "Start");
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }
        }
        createdObjects.Clear();
    }

    [Test]
    public void PhaseConfigs_HaveDistinctDawnDuskNightAtmospheresAndFogProminence()
    {
        var dawn = DayNightCycle.RecommendedMorning();
        var dusk = DayNightCycle.RecommendedDusk();
        var night = DayNightCycle.RecommendedNight();

        Assert.Greater(dawn.sunIntensity, dusk.sunIntensity);
        Assert.Greater(dusk.sunIntensity, night.sunIntensity);

        // Ścisła gradacja jasności ambientu: Świt > Zmierzch > Noc (Noc nie może być jaśniejsza niż Zmierzch)
        float dawnLuma = dawn.ambientSkyColor.grayscale;
        float duskLuma = dusk.ambientSkyColor.grayscale;
        float nightLuma = night.ambientSkyColor.grayscale;
        Assert.Greater(dawnLuma, duskLuma, "Świt musi mieć wyższą luminancję ambientu niż Zmierzch.");
        Assert.Greater(duskLuma, nightLuma, "Zmierzch musi być jaśniejszy niż Noc.");

        // Tło kamery zamiast jasnego domyślnego skyboxa musi pozostać ciemne we wszystkich fazach
        Assert.Less(dawn.cameraBackgroundColor.grayscale, 0.20f);
        Assert.Less(dusk.cameraBackgroundColor.grayscale, 0.16f);
        Assert.Less(night.cameraBackgroundColor.grayscale, 0.10f);

        Assert.AreNotEqual(dawn.ambientSkyColor, dusk.ambientSkyColor);
        Assert.AreNotEqual(dusk.ambientSkyColor, night.ambientSkyColor);

        // Purple fog mniej wyraźny za dnia i o zmierzchu, silniejszy w nocy
        Assert.Less(dawn.mistEmissionMultiplier, night.mistEmissionMultiplier);
        Assert.Less(dusk.mistEmissionMultiplier, night.mistEmissionMultiplier);
        Assert.Less(dawn.mistBrightnessMultiplier, night.mistBrightnessMultiplier);
        Assert.Less(dusk.mistBrightnessMultiplier, night.mistBrightnessMultiplier);
        Assert.Less(dawn.mistSparkMultiplier, night.mistSparkMultiplier);
        Assert.Less(dusk.mistSparkMultiplier, night.mistSparkMultiplier);
        Assert.Less(dawn.fogDensity, night.fogDensity);
    }

    [Test]
    public void TerrainShaders_AllIncludeForwardPlusClusterLightLoop()
    {
        string[] shaderPaths =
        {
            "Assets/Main folder/Materials/Terrain/Grassland.shader",
            "Assets/Main folder/Materials/Terrain/Road.shader",
            "Assets/Main folder/Materials/Terrain/HexSideTerrain.shader",
            "Assets/Main folder/Materials/Terrain/Forest.shader",
            "Assets/Main folder/Materials/Terrain/Meadow.shader",
            "Assets/Main folder/Materials/Terrain/Mountain.shader",
            "Assets/Main folder/Materials/Terrain/Hill.shader",
            "Assets/Main folder/Materials/Terrain/Sinkhole.shader",
            "Assets/Main folder/Materials/Terrain/Fertile.shader",
            "Assets/Main folder/Materials/Terrain/CastleStone.shader"
        };

        foreach (string path in shaderPaths)
        {
            Assert.IsTrue(File.Exists(path), $"Brak shadera: {path}");
            string src = File.ReadAllText(path);
            Assert.IsTrue(src.Contains("_CLUSTER_LIGHT_LOOP"),
                $"Shader {path} musi zawierać #pragma multi_compile _ _CLUSTER_LIGHT_LOOP dla Forward+!");
            Assert.IsFalse(src.Contains("max(sampleGI, trilightGI)"),
                $"Shader {path} nie powinien zawyżać nocnego ambientu przez max(sampleGI, trilightGI).");
        }

        string commonHlsl = File.ReadAllText("Assets/Main folder/Materials/Terrain/TerrainGroundCommon.hlsl");
        Assert.IsTrue(commonHlsl.Contains("unity_AmbientSky"), "TerrainGroundCommon.hlsl musi używać real-time Trilight ambient.");
        Assert.IsFalse(commonHlsl.Contains("max(sampleGI, trilightGI)"), "TerrainGroundCommon.hlsl nie powinien zawyżać nocnego ambientu przez SampleSH.");
    }

    [Test]
    public void AllBuildingTowerAndGatePrefabs_HaveShadowlessNightLightConfigured()
    {
        string[] folders =
        {
            "Assets/Main folder/Prefabs/Buildings",
            "Assets/Main folder/Prefabs/Towers"
        };

        int checkedPrefabs = 0;
        foreach (string folder in folders)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, $"Nie udało się wczytać prefabu: {path}");

                var sl = prefab.GetComponentInChildren<SettlementLight>(true);
                Assert.IsNotNull(sl, $"Prefab {path} musi mieć skonfigurowany komponent SettlementLight.");
                Assert.IsFalse(sl.CastShadows, $"Prefab {path} powinien mieć domyślnie wyłączone cienie (LightShadows.None).");
                Assert.IsTrue(sl.autoControlByPhase, $"Prefab {path} powinien automatycznie reagować na fazy.");

                var pt = sl.GetComponent<Light>();
                Assert.IsNotNull(pt, $"Prefab {path} musi mieć komponent Light.");
                Assert.AreEqual(LightType.Point, pt.type, $"Prefab {path} powinien używać Point Light.");
                Assert.AreEqual(LightShadows.None, pt.shadows, $"Prefab {path} powinien mieć LightShadows.None.");
                Assert.IsFalse(pt.enabled, $"Prefab {path} powinien zaczynać z wyłączonym Light (zapobiega błyskom przy spawnie za dnia).");

                if (path.EndsWith("Beacon of hope.prefab", StringComparison.OrdinalIgnoreCase))
                    Assert.IsTrue(sl.isBeacon, "Beacon of hope.prefab musi mieć flagę isBeacon = true.");
                else
                    Assert.IsFalse(sl.isBeacon, $"Standardowy prefab {path} nie powinien mieć flagi isBeacon.");

                checkedPrefabs++;
            }
        }

        GameObject gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Main folder/Prefabs/Walls/Gate.prefab");
        Assert.IsNotNull(gatePrefab);
        var gateLight = gatePrefab.GetComponentInChildren<SettlementLight>(true);
        Assert.IsNotNull(gateLight, "Gate.prefab musi posiadać SettlementLight.");
        Assert.IsFalse(gateLight.CastShadows);
        checkedPrefabs++;

        Assert.AreEqual(22, checkedPrefabs, "Oczekiwano 22 aktywnych prefabów (15 budynków + 6 wież + 1 brama).");
    }

    [Test]
    public void BuildingLights_AreOffDuringDawnAndDusk_AndActivateDuringNight()
    {
        GameObject humanHousePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Main folder/Prefabs/Buildings/Human house prefab.prefab");
        GameObject houseObj = Track(UnityEngine.Object.Instantiate(humanHousePrefab));
        var sl = houseObj.GetComponentInChildren<SettlementLight>(true);
        Light pt = sl.TargetLight;

        // Dawn: OFF
        Assert.AreEqual(TimePhases.Morning, timeManager.CurrentTimePhase);
        SettlementLight.TickAllLights(0.5f);
        Assert.IsFalse(pt.enabled);
        Assert.AreEqual(0f, pt.intensity, 0.001f);

        // Dusk: still OFF
        timeManager.ChangeCurrentPhase();
        Assert.AreEqual(TimePhases.Dusk, timeManager.CurrentTimePhase);
        for (int i = 0; i < 10; i++) SettlementLight.TickAllLights(0.25f);
        Assert.IsFalse(pt.enabled, "Światło budynku powinno być wyłączone w fazie Dusk (Zmierzch).");
        Assert.AreEqual(0f, pt.intensity, 0.001f);

        // Night: fades ON smoothly without shadows
        timeManager.ChangeCurrentPhase();
        Assert.AreEqual(TimePhases.Night, timeManager.CurrentTimePhase);
        for (int i = 0; i < 12; i++) SettlementLight.TickAllLights(0.25f);
        Assert.IsTrue(pt.enabled, "Światło budynku powinno być włączone w fazie Night (Noc).");
        Assert.Greater(pt.intensity, 1.0f);
        Assert.AreEqual(LightShadows.None, pt.shadows);

        // Return to Dawn: fades OFF cleanly
        timeManager.ChangeCurrentPhase();
        Assert.AreEqual(TimePhases.Morning, timeManager.CurrentTimePhase);
        for (int i = 0; i < 10; i++) SettlementLight.TickAllLights(0.25f);
        Assert.IsFalse(pt.enabled, "Światło budynku powinno zgasnąć po powrocie do Świtu.");
        Assert.AreEqual(0f, pt.intensity, 0.001f);
    }

    [Test]
    public void BeaconLight_ScalesStrengthRangeAndColorByPlayerSelectedLevel()
    {
        timeManager.ChangeCurrentPhase(); // Dusk
        timeManager.ChangeCurrentPhase(); // Night

        GameObject beaconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Main folder/Prefabs/Buildings/Beacon of hope.prefab");
        GameObject beaconObj = Track(UnityEngine.Object.Instantiate(beaconPrefab));
        var sl = beaconObj.GetComponentInChildren<SettlementLight>(true);
        Light pt = sl.TargetLight;

        sl.ApplyBeaconLevelSettings(1, immediate: true);
        float lvl1Intensity = sl.baseIntensity;
        float lvl1Range = sl.baseRange;
        Color lvl1Color = pt.color;

        sl.ApplyBeaconLevelSettings(5, immediate: true);
        float lvl5Intensity = sl.baseIntensity;
        float lvl5Range = sl.baseRange;
        Color lvl5Color = pt.color;

        Assert.Greater(lvl5Intensity, lvl1Intensity);
        Assert.Greater(lvl5Range, lvl1Range);
        Assert.AreNotEqual(lvl1Color, lvl5Color);
    }

    [Test]
    public void RuntimeCreatedBuildingDuringNight_IlluminatesImmediatelyAndNoDuplicateLightsAdded()
    {
        timeManager.ChangeCurrentPhase(); // Dusk
        timeManager.ChangeCurrentPhase(); // Night

        GameObject sawmillPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Main folder/Prefabs/Buildings/Sawmill.prefab");
        GameObject obj = Track(UnityEngine.Object.Instantiate(sawmillPrefab));
        var entity = obj.GetComponent<BuildingEntity>() ?? obj.AddComponent<BuildingEntity>();
        InvokeMethod(entity, "SetupAtmosphericLighting");

        SettlementLight[] lights = obj.GetComponentsInChildren<SettlementLight>(true);
        Assert.AreEqual(1, lights.Length, "Nie powinny powstać duplikaty SettlementLight.");

        SettlementLight.TickAllLights(0.1f);
        Assert.IsTrue(lights[0].TargetLight.enabled, "Budynek postawiony w nocy powinien od razu świecić.");
        Assert.Greater(lights[0].TargetLight.intensity, 1.0f);

        int countBeforeDestroy = SettlementLight.RegisteredCount;
        UnityEngine.Object.DestroyImmediate(obj);
        Assert.AreEqual(countBeforeDestroy - 1, SettlementLight.RegisteredCount, "Zniszczony budynek musi wyrejestrować światło.");
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        createdObjects.Add(obj);
        return obj;
    }

    private static void InvokeMethod(object target, string methodName)
    {
        MethodInfo m = target.GetType().GetMethod(methodName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        m?.Invoke(target, null);
    }
}
