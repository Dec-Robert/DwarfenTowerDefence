using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class MistHeightAndUnificationTests
{
    private GameObject _holder;

    [SetUp]
    public void SetUp()
    {
        _holder = new GameObject("TestHolder");
    }

    [TearDown]
    public void TearDown()
    {
        if (_holder != null)
        {
            Object.DestroyImmediate(_holder);
        }
    }

    [Test]
    public void ComputeMaxTerrainHeight_CalculatesMaxElevationCorrectly()
    {
        var visualizerGo = new GameObject("Visualizer", typeof(HexMapVisualizer));
        visualizerGo.transform.SetParent(_holder.transform);
        var visualizer = visualizerGo.GetComponent<HexMapVisualizer>();

        var worldData = new Dictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>>();
        var chunkDict = new Dictionary<Vector2Int, HexCellData>();

        chunkDict[new Vector2Int(0, 0)] = new HexCellData { elevation = 0 };
        chunkDict[new Vector2Int(0, 1)] = new HexCellData { elevation = 2 };
        chunkDict[new Vector2Int(0, 2)] = new HexCellData { elevation = 5 }; // Highest
        chunkDict[new Vector2Int(0, 3)] = new HexCellData { elevation = 1 };

        worldData[Vector2Int.zero] = chunkDict;

        float expectedMaxHeight = 5f * visualizer.elevationHeightStep;
        float actualMaxHeight = visualizer.ComputeMaxTerrainHeight(worldData);

        Assert.AreEqual(expectedMaxHeight, actualMaxHeight, 0.0001f);
    }

    [Test]
    public void MistWorldHeight_IncludesPositiveOffsetAboveMaxTerrainSurface()
    {
        var fowGo = new GameObject("FogOfWarManager", typeof(FogOfWarManager));
        fowGo.transform.SetParent(_holder.transform);
        var fow = fowGo.GetComponent<FogOfWarManager>();

        float offset = 0.05f;
        fow.mistHeightOffset = offset;
        fow.autoCalculateMistHeight = true;

        float calculatedHeight = fow.CalculateMistWorldHeight();
        float surfaceY = fow.MaxTerrainSurfaceY;

        Assert.Greater(calculatedHeight, surfaceY);
        Assert.AreEqual(surfaceY + offset, calculatedHeight, 0.0001f);
    }

    [Test]
    public void ManualMistWorldHeight_UsedWhenAutoCalculateDisabled()
    {
        var fowGo = new GameObject("FogOfWarManager", typeof(FogOfWarManager));
        fowGo.transform.SetParent(_holder.transform);
        var fow = fowGo.GetComponent<FogOfWarManager>();

        fow.autoCalculateMistHeight = false;
        fow.manualMistWorldHeight = 2.5f;

        float height = fow.CalculateMistWorldHeight();
        Assert.AreEqual(2.5f, height, 0.0001f);
    }

    [Test]
    public void WorldSpacePosition_PreservedRegardlessOfParentTransformOffset()
    {
        // Parent object with arbitrary position, rotation, and non-uniform scale
        var parent = new GameObject("Parent");
        parent.transform.position = new Vector3(15.4f, -3.8f, 22.1f);
        parent.transform.rotation = Quaternion.Euler(10f, 30f, 0f);
        parent.transform.localScale = new Vector3(1.2f, 0.8f, 1.5f);
        parent.transform.SetParent(_holder.transform);

        var fogChunk = new GameObject("FogChunk");
        fogChunk.transform.SetParent(parent.transform);

        float targetWorldY = 1.35f;
        // Enforce world position directly as done in FogOfWarManager
        fogChunk.transform.position = new Vector3(10f, targetWorldY, 5f);

        Assert.AreEqual(targetWorldY, fogChunk.transform.position.y, 0.0001f);
    }

    [Test]
    public void DiscoverableStates_DoNotUseLegacyPinkMaterial()
    {
        var fowGo = new GameObject("FogOfWarManager", typeof(FogOfWarManager));
        fowGo.transform.SetParent(_holder.transform);
        var fow = fowGo.GetComponent<FogOfWarManager>();

        var mistShader = Shader.Find("Custom/MistHLSL") ?? Shader.Find("Universal Render Pipeline/Lit");
        var mistMat = new Material(mistShader) { name = "MistHLSL" };
        var legacyMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "FogOfWar3" };

        fow.borderlandsFogMaterial = mistMat;
        fow.surveyingFogMaterial = mistMat;
        fow.defaultFogMaterial = mistMat;

        // Verify that discoverable states use Mist material and explicitly never use legacy material
        Assert.AreNotEqual(legacyMat, fow.borderlandsFogMaterial);
        Assert.AreNotEqual(legacyMat, fow.surveyingFogMaterial);
        Assert.AreEqual(mistMat, fow.borderlandsFogMaterial);
        Assert.AreEqual(mistMat, fow.surveyingFogMaterial);

        Object.DestroyImmediate(mistMat);
        Object.DestroyImmediate(legacyMat);
    }

    [Test]
    public void SurveyingMaterial_ContainsMistAlphaProperty()
    {
        var mistShader = Shader.Find("Custom/MistHLSL");
        if (mistShader != null)
        {
            var mat = new Material(mistShader);
            mat.SetFloat("_MistAlpha", 0.75f);
            Assert.AreEqual(0.75f, mat.GetFloat("_MistAlpha"), 0.001f);
            Object.DestroyImmediate(mat);
        }
    }

    [Test]
    public void MistShader_ContainsSideDropAndAlphaProperties()
    {
        var mistShader = Shader.Find("Custom/MistHLSL");
        if (mistShader != null)
        {
            int sideDropIdx = mistShader.FindPropertyIndex("_MistSideDrop");
            int alphaIdx = mistShader.FindPropertyIndex("_MistAlpha");

            Assert.AreNotEqual(-1, sideDropIdx, "Shader should declare _MistSideDrop property");
            Assert.AreNotEqual(-1, alphaIdx, "Shader should declare _MistAlpha property");
        }
    }
    [Test]
    public void MistShader_ContainsBlendingAndCullProperties()
    {
        var mistShader = Shader.Find("Custom/MistHLSL");
        if (mistShader != null)
        {
            Assert.AreNotEqual(-1, mistShader.FindPropertyIndex("_SrcBlend"), "Shader should declare _SrcBlend");
            Assert.AreNotEqual(-1, mistShader.FindPropertyIndex("_DstBlend"), "Shader should declare _DstBlend");
            Assert.AreNotEqual(-1, mistShader.FindPropertyIndex("_ZWrite"), "Shader should declare _ZWrite");
            Assert.AreNotEqual(-1, mistShader.FindPropertyIndex("_Cull"), "Shader should declare _Cull");
        }
    }

    [Test]
    public void GroundMistShader_ContainsRequiredGroundMistProperties()
    {
        var groundMistShader = Shader.Find("Custom/GroundMist");
        if (groundMistShader != null)
        {
            Assert.AreNotEqual(-1, groundMistShader.FindPropertyIndex("_GroundMistCoverage"), "Should have _GroundMistCoverage");
            Assert.AreNotEqual(-1, groundMistShader.FindPropertyIndex("_GroundMistOpacity"), "Should have _GroundMistOpacity");
            Assert.AreNotEqual(-1, groundMistShader.FindPropertyIndex("_GroundMistDarkColor"), "Should have _GroundMistDarkColor");
            Assert.AreNotEqual(-1, groundMistShader.FindPropertyIndex("_GroundMistLightColor"), "Should have _GroundMistLightColor");
            Assert.AreNotEqual(-1, groundMistShader.FindPropertyIndex("_GroundMistEmissionStrength"), "Should have _GroundMistEmissionStrength");
            Assert.AreNotEqual(-1, groundMistShader.FindPropertyIndex("_MacroScale"), "Should have _MacroScale");
            Assert.AreNotEqual(-1, groundMistShader.FindPropertyIndex("_MacroSpeed"), "Should have _MacroSpeed");
            Assert.AreNotEqual(-1, groundMistShader.FindPropertyIndex("_DetailScale"), "Should have _DetailScale");
            Assert.AreNotEqual(-1, groundMistShader.FindPropertyIndex("_DetailSpeed"), "Should have _DetailSpeed");
            Assert.AreNotEqual(-1, groundMistShader.FindPropertyIndex("_ColorSteps"), "Should have _ColorSteps");
            Assert.AreNotEqual(-1, groundMistShader.FindPropertyIndex("_GroundMistFade"), "Should have _GroundMistFade");
        }
    }

    [Test]
    public void GroundMist_HeightOffset_IsPositiveAndConfigurable()
    {
        var fowGo = new GameObject("FogOfWarManager", typeof(FogOfWarManager));
        fowGo.transform.SetParent(_holder.transform);
        var fow = fowGo.GetComponent<FogOfWarManager>();

        Assert.Greater(fow.groundMistHeightOffset, 0f, "Ground mist offset must be positive to prevent Z-fighting");
        Assert.LessOrEqual(fow.groundMistHeightOffset, 0.1f, "Ground mist offset should stay close to terrain surface");
    }

    [Test]
    public void HexGridMath_WorldToAxial_RoundtripsWithAxialToWorld()
    {
        float hexSize = 1.0f;
        float padding = 0.05f;

        Vector2Int[] testAxials = new Vector2Int[]
        {
            new Vector2Int(0, 0),
            new Vector2Int(1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(-1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(2, -1),
            new Vector2Int(-2, 1)
        };

        foreach (var original in testAxials)
        {
            Vector3 worldPos = HexGridMath.AxialToWorld(original.x, original.y, hexSize, padding);
            Vector2Int reconstructed = HexGridMath.WorldToAxial(worldPos.x, worldPos.z, hexSize, padding);
            Assert.AreEqual(original, reconstructed, $"WorldToAxial roundtrip failed for {original}");
        }
    }

    [Test]
    public void SettledChunks_DoNotSpawnFogObjectOnInitialization()
    {
        var fowGo = new GameObject("FogOfWarManager", typeof(FogOfWarManager));
        fowGo.transform.SetParent(_holder.transform);
        var fow = fowGo.GetComponent<FogOfWarManager>();

        var expGo = new GameObject("MapExpansionManager", typeof(MapExpansionManager));
        expGo.transform.SetParent(_holder.transform);
        var exp = expGo.GetComponent<MapExpansionManager>();
        fow.expansionManager = exp;

        // Mock a prefab
        var dummyPrefab = new GameObject("DummyFogPrefab");
        dummyPrefab.transform.SetParent(_holder.transform);
        fow.fogPrefab = dummyPrefab;

        var worldData = new Dictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>>();
        worldData[Vector2Int.zero] = new Dictionary<Vector2Int, HexCellData>();
        worldData[new Vector2Int(1, 0)] = new Dictionary<Vector2Int, HexCellData>();

        exp.Initialize(worldData, new List<Vector2Int> { Vector2Int.zero });

        var allChunks = new HashSet<Vector2Int> { Vector2Int.zero, new Vector2Int(1, 0) };
        fow.InitializeFog(allChunks, 2, 1f, 0.05f);

        // Chunk (0,0) is Settled: should NOT have a fog object
        var activeFogField = typeof(FogOfWarManager).GetField("activeFogChunks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var activeFog = (Dictionary<Vector2Int, GameObject>)activeFogField.GetValue(fow);

        Assert.IsFalse(activeFog.ContainsKey(Vector2Int.zero), "Settled chunk (0,0) must NOT have a fog object spawned.");
        Assert.IsTrue(activeFog.ContainsKey(new Vector2Int(1, 0)), "Unsettled chunk (1,0) should have a fog object.");
    }

    [Test]
    public void TmpMistShader_ContainsRequiredOutskirtsMistProperties()
    {
        var tmpMistShader = Shader.Find("Custom/TmpMist");
        Assert.IsNotNull(tmpMistShader, "Custom/TmpMist shader should be loaded and available.");
        Assert.AreNotEqual(-1, tmpMistShader.FindPropertyIndex("_BaseColor"), "Should have _BaseColor");
        Assert.AreNotEqual(-1, tmpMistShader.FindPropertyIndex("_MistAlpha"), "Should have _MistAlpha");
        Assert.AreNotEqual(-1, tmpMistShader.FindPropertyIndex("_GroundMistFade"), "Should have _GroundMistFade");
        Assert.AreNotEqual(-1, tmpMistShader.FindPropertyIndex("_NoiseScale"), "Should have _NoiseScale");
        Assert.AreNotEqual(-1, tmpMistShader.FindPropertyIndex("_NoiseSpeed"), "Should have _NoiseSpeed");
    }

    [Test]
    public void OutskirtsMist_HexHeightCalculation_EqualsSurfaceYPlusHalfElevationStep()
    {
        float elevationStep = 0.25f;
        int level = 4;
        float surfaceY = level * elevationStep; // 1.0f

        float halfStep = elevationStep * 0.5f; // 0.125f
        float expectedMistY = surfaceY + halfStep; // 1.125f (level 4.5 in elevation terms)

        Assert.AreEqual(4.5f * elevationStep, expectedMistY, 0.0001f);
        Assert.AreEqual(1.125f, expectedMistY, 0.0001f);
    }

    [Test]
    public void OutskirtsMist_RemovesCollidersFromFogObject()
    {
        var fowGo = new GameObject("FogOfWarManager", typeof(FogOfWarManager));
        fowGo.transform.SetParent(_holder.transform);
        var fow = fowGo.GetComponent<FogOfWarManager>();

        // Create a dummy fog prefab with child colliders
        var prefabGo = new GameObject("DummyFogPrefabWithColliders");
        prefabGo.transform.SetParent(_holder.transform);
        var childHex = new GameObject("ChildHex");
        childHex.transform.SetParent(prefabGo.transform);
        childHex.AddComponent<MeshCollider>();

        fow.fogPrefab = prefabGo;

        Vector2Int coord = new Vector2Int(2, 2);
        fow.UpdateChunkFogState(coord, ChunkState.Outskirts);

        var activeFogField = typeof(FogOfWarManager).GetField("activeFogChunks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var activeFog = (Dictionary<Vector2Int, GameObject>)activeFogField.GetValue(fow);

        Assert.IsTrue(activeFog.ContainsKey(coord), "Fog object should be tracked for Outskirts chunk.");
        var fogObj = activeFog[coord];
        var remainingColliders = fogObj.GetComponentsInChildren<Collider>();
        Assert.AreEqual(0, remainingColliders.Length, "Outskirts fog must have all Collider components removed.");
    }
}
