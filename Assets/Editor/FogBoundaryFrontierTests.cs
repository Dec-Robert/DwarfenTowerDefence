using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class FogBoundaryFrontierTests
{
    private GameObject _holder;
    private FogOfWarManager _fow;
    private MapExpansionManager _exp;
    private const int ChunkRadius = 3;
    private const float HexSize = 1f;
    private const float Padding = 0f;

    [SetUp]
    public void SetUp()
    {
        _holder = new GameObject("FogBoundaryTestsHolder");

        var expGo = new GameObject("MapExpansionManager", typeof(MapExpansionManager));
        expGo.transform.SetParent(_holder.transform);
        _exp = expGo.GetComponent<MapExpansionManager>();

        var fowGo = new GameObject("FogOfWarManager", typeof(FogOfWarManager));
        fowGo.transform.SetParent(_holder.transform);
        _fow = fowGo.GetComponent<FogOfWarManager>();
        _fow.expansionManager = _exp;
        _fow.maskResolution = 512;
        _fow.boundaryTransitionWidth = 0.16f;

        var prefabGo = new GameObject("DummyFogPrefab");
        prefabGo.transform.SetParent(_holder.transform);
        var hexChild = new GameObject("HexMesh");
        hexChild.transform.SetParent(prefabGo.transform);
        hexChild.AddComponent<MeshFilter>();
        var mr = hexChild.AddComponent<MeshRenderer>();
        var mistShader = Shader.Find("Custom/MistHLSL");
        if (mistShader != null)
        {
            mr.sharedMaterial = new Material(mistShader);
        }
        _fow.fogPrefab = prefabGo;
    }

    [TearDown]
    public void TearDown()
    {
        if (_holder != null)
        {
            Object.DestroyImmediate(_holder);
        }
    }

    private void SetupChunks(Dictionary<Vector2Int, ChunkState> states, HashSet<Vector2Int> blockedRoadChunks = null)
    {
        _exp.activeChunks.Clear();
        var allCoords = new HashSet<Vector2Int>();

        foreach (var kvp in states)
        {
            var data = new ChunkStateData
            {
                chunkCoord = kvp.Key,
                baseState = kvp.Value
            };
            if (blockedRoadChunks != null && blockedRoadChunks.Contains(kvp.Key))
            {
                data.isRoadChunk = true;
                data.hasRoadPredecessor = true;
                data.roadPredecessors = new List<Vector2Int> { new Vector2Int(99, 99) };
            }
            _exp.activeChunks[kvp.Key] = data;
            allCoords.Add(kvp.Key);
        }

        _fow.InitializeFog(allCoords, ChunkRadius, HexSize, Padding);
    }

    private Color32 SampleMaskAtWorld(Vector2 worldXZ)
    {
        Vector4 bounds = Shader.GetGlobalVector("_FogWorldBounds");
        Texture2D tex = _fow.FogMaskTexture;
        Assert.IsNotNull(tex, "Global fog mask texture must be initialized.");

        float u = Mathf.Clamp01((worldXZ.x - bounds.x) / bounds.z);
        float v = Mathf.Clamp01((worldXZ.y - bounds.y) / bounds.w);
        int px = Mathf.Clamp(Mathf.FloorToInt(u * tex.width), 0, tex.width - 1);
        int py = Mathf.Clamp(Mathf.FloorToInt(v * tex.height), 0, tex.height - 1);
        return tex.GetPixel(px, py);
    }

    private float GetChunkLogicalWilderness(Vector2Int coord)
    {
        var activeFogField = typeof(FogOfWarManager).GetField("activeFogChunks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var activeFog = (Dictionary<Vector2Int, GameObject>)activeFogField.GetValue(_fow);
        Assert.IsTrue(activeFog.ContainsKey(coord), $"Chunk {coord} should have a fog object.");
        var mr = activeFog[coord].GetComponentInChildren<MeshRenderer>();
        var mpb = new MaterialPropertyBlock();
        mr.GetPropertyBlock(mpb);
        return mpb.GetFloat("_LogicalWilderness");
    }

    [Test]
    public void MistShader_DeclaresRequiredFrontierAndDebugProperties()
    {
        var shader = Shader.Find("Custom/MistHLSL");
        Assert.IsNotNull(shader);
        Assert.AreNotEqual(-1, shader.FindPropertyIndex("_LogicalWilderness"));
        Assert.AreNotEqual(-1, shader.FindPropertyIndex("_BoundaryWarpAmplitude"));
        Assert.AreNotEqual(-1, shader.FindPropertyIndex("_BoundaryDetailStrength"));
        Assert.AreNotEqual(-1, shader.FindPropertyIndex("_BoundaryAnimationSpeed"));
        Assert.AreNotEqual(-1, shader.FindPropertyIndex("_DebugBoundaryMode"));
    }

    [Test]
    public void SingleBorderlandsSurroundedByWilderness_HasCleanInteriorAndStrictWildernessIsolation()
    {
        var states = new Dictionary<Vector2Int, ChunkState>
        {
            { new Vector2Int(0, 0), ChunkState.Borderlands }
        };
        foreach (var n in HexGridMath.GetNeighbors(Vector2Int.zero))
        {
            states[n] = ChunkState.Wilderness;
        }
        SetupChunks(states);

        // Srodek Borderlands: R = 0 (czysty Discoverable), G = 0 (Borderlands)
        Vector3 center3 = HexGridMath.GetChunkCenterWorld(Vector2Int.zero, ChunkRadius, HexSize, Padding);
        Color32 centerSample = SampleMaskAtWorld(new Vector2(center3.x, center3.z));
        Assert.AreEqual(0, centerSample.g, "Borderlands center must have logical mask G == 0.");
        Assert.AreEqual(0, centerSample.r, "Borderlands center must have distance mask R == 0.");
        Assert.AreEqual(0f, GetChunkLogicalWilderness(Vector2Int.zero), 0.001f);

        // Kazdy Wilderness sasiad: G = 255, R = 255, _LogicalWilderness = 1
        foreach (var n in HexGridMath.GetNeighbors(Vector2Int.zero))
        {
            Vector3 nCenter = HexGridMath.GetChunkCenterWorld(n, ChunkRadius, HexSize, Padding);
            Color32 nSample = SampleMaskAtWorld(new Vector2(nCenter.x, nCenter.z));
            Assert.AreEqual(255, nSample.g, $"Wilderness chunk {n} must have G == 255.");
            Assert.AreEqual(255, nSample.r, $"Wilderness chunk {n} must have R == 255.");
            Assert.AreEqual(1f, GetChunkLogicalWilderness(n), 0.001f);
        }

        // Punkt na zewnetrznym heksie Borderlands przy samej granicy z Wilderness powinien miec Frontier (B==255) i R > 0
        Vector2Int edgeHex = HexGridMath.LocalToGlobal(Vector2Int.zero, new Vector2Int(ChunkRadius, 0), ChunkRadius);
        Vector2Int wildHex = edgeHex + new Vector2Int(1, 0);
        Vector3 cA = HexGridMath.AxialToWorld(edgeHex.x, edgeHex.y, HexSize, Padding);
        Vector3 cB = HexGridMath.AxialToWorld(wildHex.x, wildHex.y, HexSize, Padding);
        Vector2 justInsideBorderlands = Vector2.Lerp(new Vector2(cA.x, cA.z), new Vector2(cB.x, cB.z), 0.44f);
        Color32 edgeSample = SampleMaskAtWorld(justInsideBorderlands);
        Assert.AreEqual(0, edgeSample.g, "Point just inside Borderlands must remain logical G == 0.");
        Assert.Greater(edgeSample.r, 80, "Point near Frontier inside Borderlands must have positive transition mask R.");
        Assert.AreEqual(255, edgeSample.b, "Point at Frontier edge must mark debug channel B == 255.");
    }

    [Test]
    public void AdjacentBorderlandsAndSettledOutskirts_DoNotGenerateFrontierBetweenThem()
    {
        var states = new Dictionary<Vector2Int, ChunkState>
        {
            { new Vector2Int(0, 0), ChunkState.Settled },
            { new Vector2Int(1, 0), ChunkState.Borderlands },
            { new Vector2Int(0, 1), ChunkState.Borderlands },
            { new Vector2Int(-1, 1), ChunkState.Outskirts },
            { new Vector2Int(-1, 0), ChunkState.Wilderness },
            { new Vector2Int(0, -1), ChunkState.Wilderness },
            { new Vector2Int(1, -1), ChunkState.Wilderness }
        };
        SetupChunks(states);

        // Sprawdzamy granice Borderlands(1,0) <-> Borderlands(0,1): brak Frontier i R == 0
        Vector3 b1 = HexGridMath.GetChunkCenterWorld(new Vector2Int(1, 0), ChunkRadius, HexSize, Padding);
        Vector3 b2 = HexGridMath.GetChunkCenterWorld(new Vector2Int(0, 1), ChunkRadius, HexSize, Padding);
        Vector2 midBorderlands = 0.5f * (new Vector2(b1.x, b1.z) + new Vector2(b2.x, b2.z));
        Color32 borderlandsSeam = SampleMaskAtWorld(midBorderlands);
        Assert.AreEqual(0, borderlandsSeam.b, "No Frontier between adjacent Borderlands chunks.");
        Assert.AreEqual(0, borderlandsSeam.r, "No darkening transition between adjacent Borderlands chunks.");

        // Sprawdzamy granice Settled(0,0) <-> Borderlands(1,0): brak Frontier i R == 0
        Vector3 s0 = HexGridMath.GetChunkCenterWorld(Vector2Int.zero, ChunkRadius, HexSize, Padding);
        Vector2 midSettled = 0.5f * (new Vector2(s0.x, s0.z) + new Vector2(b1.x, b1.z));
        Color32 settledSeam = SampleMaskAtWorld(midSettled);
        Assert.AreEqual(0, settledSeam.b, "No Frontier between Settled and Borderlands.");
        Assert.AreEqual(0, settledSeam.r, "No transition band between Settled and Borderlands.");
    }

    [Test]
    public void BlockedRoadChunkAdjacentToSettled_RemainsStrictlyWilderness()
    {
        var states = new Dictionary<Vector2Int, ChunkState>
        {
            { new Vector2Int(0, 0), ChunkState.Settled },
            { new Vector2Int(1, 0), ChunkState.Wilderness }, // zablokowany road chunk bez odkrytego poprzednika
            { new Vector2Int(0, 1), ChunkState.Borderlands }
        };
        var blockedRoad = new HashSet<Vector2Int> { new Vector2Int(1, 0) };
        SetupChunks(states, blockedRoad);

        Assert.AreEqual(1f, GetChunkLogicalWilderness(new Vector2Int(1, 0)), 0.001f);
        Assert.AreEqual(0f, GetChunkLogicalWilderness(new Vector2Int(0, 1)), 0.001f);

        Vector3 roadCenter = HexGridMath.GetChunkCenterWorld(new Vector2Int(1, 0), ChunkRadius, HexSize, Padding);
        Color32 roadSample = SampleMaskAtWorld(new Vector2(roadCenter.x, roadCenter.z));
        Assert.AreEqual(255, roadSample.g);
        Assert.AreEqual(255, roadSample.r);
    }

    [Test]
    public void BorderlandsToSurveyingAndExpansion_UpdatesLogicalAndMaskStatesCleanly()
    {
        var states = new Dictionary<Vector2Int, ChunkState>
        {
            { new Vector2Int(0, 0), ChunkState.Settled },
            { new Vector2Int(1, 0), ChunkState.Borderlands },
            { new Vector2Int(2, 0), ChunkState.Wilderness }
        };
        SetupChunks(states);

        Assert.AreEqual(0f, GetChunkLogicalWilderness(new Vector2Int(1, 0)), 0.001f);
        Assert.AreEqual(1f, GetChunkLogicalWilderness(new Vector2Int(2, 0)), 0.001f);

        // Przejscie Borderlands -> Surveying
        _exp.activeChunks[new Vector2Int(1, 0)].baseState = ChunkState.Surveying;
        _fow.UpdateChunkFogState(new Vector2Int(1, 0), ChunkState.Surveying, animate: false);
        _fow.UpdateGlobalFogMask();
        Assert.AreEqual(0f, GetChunkLogicalWilderness(new Vector2Int(1, 0)), 0.001f);

        // Rozszerzenie odkrytego obszaru: (1,0) -> Outskirts, (2,0) -> Borderlands
        _exp.activeChunks[new Vector2Int(1, 0)].baseState = ChunkState.Outskirts;
        _exp.activeChunks[new Vector2Int(2, 0)].baseState = ChunkState.Borderlands;
        _fow.UpdateChunkFogState(new Vector2Int(1, 0), ChunkState.Outskirts, animate: false);
        _fow.UpdateChunkFogState(new Vector2Int(2, 0), ChunkState.Borderlands, animate: false);
        _fow.UpdateGlobalFogMask();

        Assert.AreEqual(0f, GetChunkLogicalWilderness(new Vector2Int(2, 0)), 0.001f);
        Vector3 c2 = HexGridMath.GetChunkCenterWorld(new Vector2Int(2, 0), ChunkRadius, HexSize, Padding);
        Color32 s2 = SampleMaskAtWorld(new Vector2(c2.x, c2.z));
        Assert.AreEqual(0, s2.g);
        Assert.AreEqual(0, s2.r);
    }
}
