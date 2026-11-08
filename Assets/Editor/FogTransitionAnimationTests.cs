using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class FogTransitionAnimationTests
{
    private GameObject _holder;

    [SetUp]
    public void SetUp()
    {
        _holder = new GameObject("TransitionTestHolder");
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
    public void TransitionParameters_HaveSensibleDefaultValues()
    {
        var fowGo = new GameObject("FogOfWarManager", typeof(FogOfWarManager));
        fowGo.transform.SetParent(_holder.transform);
        var fow = fowGo.GetComponent<FogOfWarManager>();

        Assert.Greater(fow.surveyTransitionDuration, 0f, "surveyTransitionDuration should be > 0");
        Assert.Greater(fow.surveyPulseMultiplier, 1f, "surveyPulseMultiplier should be > 1 for visible burst");
        Assert.Greater(fow.outskirtsTransitionDuration, 0f, "outskirtsTransitionDuration should be > 0");
        Assert.Greater(fow.outskirtsSinkDistance, 0f, "outskirtsSinkDistance should be > 0");
        Assert.IsNotNull(fow.transitionEaseCurve, "transitionEaseCurve should be initialized");
    }

    [Test]
    public void MistHLSLShader_ContainsRequiredTransitionProperties()
    {
        var shader = Shader.Find("Custom/MistHLSL");
        Assert.IsNotNull(shader, "Custom/MistHLSL shader must exist.");
        Assert.AreNotEqual(-1, shader.FindPropertyIndex("_MistAlpha"), "Custom/MistHLSL must declare _MistAlpha");
        Assert.AreNotEqual(-1, shader.FindPropertyIndex("_DiscoverableEmission"), "Custom/MistHLSL must declare _DiscoverableEmission");
    }

    [Test]
    public void OutskirtsTransition_CollidersAreRemovedImmediately()
    {
        var fowGo = new GameObject("FogOfWarManager", typeof(FogOfWarManager));
        fowGo.transform.SetParent(_holder.transform);
        var fow = fowGo.GetComponent<FogOfWarManager>();

        var dummyPrefab = new GameObject("DummyFogPrefabWithCollider");
        dummyPrefab.transform.SetParent(_holder.transform);
        var child = new GameObject("HexMesh");
        child.transform.SetParent(dummyPrefab.transform);
        child.AddComponent<MeshCollider>();
        fow.fogPrefab = dummyPrefab;

        Vector2Int coord = new Vector2Int(1, 2);

        // Ustawiamy najpierw stan Borderlands, a nastepnie Surveying
        fow.UpdateChunkFogState(coord, ChunkState.Borderlands, animate: false);
        fow.UpdateChunkFogState(coord, ChunkState.Surveying, animate: false);

        // Przejscie do Outskirts
        fow.UpdateChunkFogState(coord, ChunkState.Outskirts, animate: false);

        var activeFogField = typeof(FogOfWarManager).GetField("activeFogChunks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var activeFog = (Dictionary<Vector2Int, GameObject>)activeFogField.GetValue(fow);

        Assert.IsTrue(activeFog.ContainsKey(coord), "Chunk should have active fog object for Outskirts.");
        var fogObj = activeFog[coord];
        var colliders = fogObj.GetComponentsInChildren<Collider>();
        Assert.AreEqual(0, colliders.Length, "Colliders must be completely removed so terrain is interactive immediately.");
    }

    [Test]
    public void ClearFog_StopsCoroutinesAndClearsTransitionalObjects()
    {
        var fowGo = new GameObject("FogOfWarManager", typeof(FogOfWarManager));
        fowGo.transform.SetParent(_holder.transform);
        var fow = fowGo.GetComponent<FogOfWarManager>();

        var dummyPrefab = new GameObject("DummyFogPrefab");
        dummyPrefab.transform.SetParent(_holder.transform);
        fow.fogPrefab = dummyPrefab;

        Vector2Int coord = new Vector2Int(3, 4);
        fow.UpdateChunkFogState(coord, ChunkState.Borderlands, animate: false);

        // Metoda prywatna ClearFog przez Reflection
        var clearFogMethod = typeof(FogOfWarManager).GetMethod("ClearFog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        clearFogMethod.Invoke(fow, null);

        var activeFogField = typeof(FogOfWarManager).GetField("activeFogChunks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var activeFog = (Dictionary<Vector2Int, GameObject>)activeFogField.GetValue(fow);
        Assert.AreEqual(0, activeFog.Count, "activeFogChunks should be empty after ClearFog.");

        var transFogField = typeof(FogOfWarManager).GetField("transitionalFogChunks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var transFog = (Dictionary<Vector2Int, GameObject>)transFogField.GetValue(fow);
        Assert.AreEqual(0, transFog.Count, "transitionalFogChunks should be empty after ClearFog.");
    }

    [Test]
    public void DuplicateStateCall_DoesNotInterruptOrCorruptState()
    {
        var fowGo = new GameObject("FogOfWarManager", typeof(FogOfWarManager));
        fowGo.transform.SetParent(_holder.transform);
        var fow = fowGo.GetComponent<FogOfWarManager>();

        var dummyPrefab = new GameObject("DummyFogPrefab");
        dummyPrefab.transform.SetParent(_holder.transform);
        fow.fogPrefab = dummyPrefab;

        Vector2Int coord = new Vector2Int(5, 5);
        fow.UpdateChunkFogState(coord, ChunkState.Borderlands, animate: false);
        fow.UpdateChunkFogState(coord, ChunkState.Surveying, animate: false);

        // Wywolanie ponowne z tym samym stanem (symulacja OnMissionStarted)
        Assert.DoesNotThrow(() => fow.UpdateChunkFogState(coord, ChunkState.Surveying, animate: true));

        var chunkStatesField = typeof(FogOfWarManager).GetField("chunkFogStates", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var chunkStates = (Dictionary<Vector2Int, ChunkState>)chunkStatesField.GetValue(fow);
        Assert.AreEqual(ChunkState.Surveying, chunkStates[coord]);
    }
}
