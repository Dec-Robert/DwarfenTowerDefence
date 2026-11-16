using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class SurveyingSpawnAndSpeedTests
{
    private GameObject holder;

    [SetUp]
    public void SetUp()
    {
        holder = new GameObject("__SurveyingSpawnAndSpeedTestHolder");
    }

    [TearDown]
    public void TearDown()
    {
        if (holder != null)
        {
            Object.DestroyImmediate(holder);
        }
    }

    private HexMapGenerator CreateTestMapGenerator()
    {
        var go = new GameObject("HexMapGenerator");
        go.transform.SetParent(holder.transform);
        var gen = go.AddComponent<HexMapGenerator>();
        gen.mapWidth = 6;
        gen.mapMinY = -4;
        gen.mapMaxY = 4;
        gen.chunkRadius = 3;
        gen.hexSize = 1f;
        gen.padding = 0.02f;
        gen.minChunkDistance = 7;
        gen.maxGenerationAttempts = 50;
        gen.allowExtraSpawners = false;
        gen.chanceForMoreBranching = 0;
        gen.globalObstacleChance = 20;
        gen.baseRoadEndLocal = new Vector2Int(3, -2);
        gen.topologySettings = new HexMapGenerator.RoadTopologySettings();
        gen.internalSettings = new HexMapGenerator.ChunkStyleSettings();
        gen.terrainConfig = TerrainGenerationConfig.CreateDefault();
        gen.specialChunkTypes = new List<SpecialChunkDefinition>();
        return gen;
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, $"Method '{methodName}' not found on {target.GetType().Name}.");
        method.Invoke(target, null);
    }

    [Test]
    public void IsChunkOpenForSpawn_AllowsSurveyingOutskirtsSettled_AndBlocksWildernessBorderlands()
    {
        var expansionGo = new GameObject("MapExpansionManager");
        expansionGo.transform.SetParent(holder.transform);
        var expansion = expansionGo.AddComponent<MapExpansionManager>();

        var fowGo = new GameObject("FogOfWarManager");
        fowGo.transform.SetParent(holder.transform);
        var fow = fowGo.AddComponent<FogOfWarManager>();
        fow.expansionManager = expansion;

        var worldData = new Dictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>>
        {
            { new Vector2Int(0, 0), new Dictionary<Vector2Int, HexCellData>() },
            { new Vector2Int(1, 0), new Dictionary<Vector2Int, HexCellData>() },
            { new Vector2Int(2, 0), new Dictionary<Vector2Int, HexCellData>() },
            { new Vector2Int(3, 0), new Dictionary<Vector2Int, HexCellData>() },
            { new Vector2Int(4, 0), new Dictionary<Vector2Int, HexCellData>() },
        };
        expansion.Initialize(worldData);

        expansion.SetChunkBaseState(new Vector2Int(0, 0), ChunkState.Settled);
        expansion.SetChunkBaseState(new Vector2Int(1, 0), ChunkState.Outskirts);
        expansion.SetChunkBaseState(new Vector2Int(2, 0), ChunkState.Surveying);
        expansion.SetChunkBaseState(new Vector2Int(3, 0), ChunkState.Borderlands);
        expansion.SetChunkBaseState(new Vector2Int(4, 0), ChunkState.Wilderness);

        Assert.IsTrue(fow.IsChunkOpenForSpawn(new Vector2Int(0, 0)));
        Assert.IsTrue(fow.IsChunkOpenForSpawn(new Vector2Int(1, 0)));
        Assert.IsTrue(fow.IsChunkOpenForSpawn(new Vector2Int(2, 0)));
        Assert.IsFalse(fow.IsChunkOpenForSpawn(new Vector2Int(3, 0)));
        Assert.IsFalse(fow.IsChunkOpenForSpawn(new Vector2Int(4, 0)));

        // Krytyczne: IsChunkRevealed musi pozostać false dla Surveying (dla bonusów budynków / podświetleń)
        Assert.IsFalse(fow.IsChunkRevealed(new Vector2Int(2, 0)));
        Assert.IsTrue(fow.IsChunkRevealed(new Vector2Int(1, 0)));
    }

    [Test]
    public void IsWorldPositionInSurveyingFog_TrueOnlyInsideSurveyingChunk()
    {
        var gen = CreateTestMapGenerator();

        var expansionGo = new GameObject("MapExpansionManager");
        expansionGo.transform.SetParent(holder.transform);
        var expansion = expansionGo.AddComponent<MapExpansionManager>();

        var fowGo = new GameObject("FogOfWarManager");
        fowGo.transform.SetParent(holder.transform);
        var fow = fowGo.AddComponent<FogOfWarManager>();
        fow.expansionManager = expansion;
        fow.mapGenerator = gen;

        var cSettled = new Vector2Int(0, 0);
        var cOutskirts = new Vector2Int(1, 0);
        var cSurveying = new Vector2Int(2, 0);
        var cBorderlands = new Vector2Int(3, 0);

        var worldData = new Dictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>>
        {
            { cSettled, new Dictionary<Vector2Int, HexCellData>() },
            { cOutskirts, new Dictionary<Vector2Int, HexCellData>() },
            { cSurveying, new Dictionary<Vector2Int, HexCellData>() },
            { cBorderlands, new Dictionary<Vector2Int, HexCellData>() },
        };
        expansion.Initialize(worldData);
        expansion.SetChunkBaseState(cOutskirts, ChunkState.Outskirts);
        expansion.SetChunkBaseState(cSurveying, ChunkState.Surveying);
        expansion.SetChunkBaseState(cBorderlands, ChunkState.Borderlands);
        fow.UpdateAllFogVisuals();

        Vector3 posSettled = HexGridMath.GetChunkCenterWorld(cSettled, gen.chunkRadius, gen.hexSize, gen.padding);
        Vector3 posOutskirts = HexGridMath.GetChunkCenterWorld(cOutskirts, gen.chunkRadius, gen.hexSize, gen.padding);
        Vector3 posSurveying = HexGridMath.GetChunkCenterWorld(cSurveying, gen.chunkRadius, gen.hexSize, gen.padding);
        Vector3 posBorderlands = HexGridMath.GetChunkCenterWorld(cBorderlands, gen.chunkRadius, gen.hexSize, gen.padding);

        Assert.IsFalse(fow.IsWorldPositionInSurveyingFog(posSettled));
        Assert.IsFalse(fow.IsWorldPositionInSurveyingFog(posOutskirts));
        Assert.IsTrue(fow.IsWorldPositionInSurveyingFog(posSurveying));
        Assert.IsFalse(fow.IsWorldPositionInSurveyingFog(posBorderlands));

        // Po zakończeniu eksploracji (Surveying -> Outskirts) pozycja przestaje być we mgle Surveying
        fow.UpdateChunkFogState(cSurveying, ChunkState.Outskirts, animate: false);
        Assert.IsFalse(fow.IsWorldPositionInSurveyingFog(posSurveying));
    }

    [Test]
    public void EnemySpawner_MovesSpawnImmediatelyOnSurveying_AndNotOnRecon()
    {
        var gen = CreateTestMapGenerator();
        var ctx = gen.BuildMapData(12345);

        var expansionGo = new GameObject("MapExpansionManager");
        expansionGo.transform.SetParent(holder.transform);
        var expansion = expansionGo.AddComponent<MapExpansionManager>();

        var fowGo = new GameObject("FogOfWarManager");
        fowGo.transform.SetParent(holder.transform);
        var fow = fowGo.AddComponent<FogOfWarManager>();
        fow.expansionManager = expansion;
        fow.mapGenerator = gen;

        var spawnerGo = new GameObject("EnemySpawner");
        spawnerGo.transform.SetParent(holder.transform);
        var spawner = spawnerGo.AddComponent<EnemySpawner>();
        spawner.mapGenerator = gen;
        spawner.fogManager = fow;

        // Zasymuluj połączenia ze Start()
        InvokePrivateMethod(fow, "Start");
        InvokePrivateMethod(spawner, "Start");

        expansion.Initialize(ctx.worldData);
        fow.UpdateAllFogVisuals();
        spawner.InitializeRoutes();

        Assert.IsNotEmpty(spawner.activeRoutes, "Expected at least one active route on initialized map.");

        Vector3 initialSpawnPoint = spawner.activeRoutes[0].currentSpawnPoint;
        int initialActivePathLen = spawner.activeRoutes[0].currentActivePath.Count;

        // Znajdź chunk drogi w stanie Borderlands (kolejny nieodkryty na ścieżce)
        Vector2Int nextBorderlandsRoad = expansion.activeChunks
            .Where(kvp => kvp.Value.isRoadChunk && kvp.Value.baseState == ChunkState.Borderlands)
            .Select(kvp => kvp.Key)
            .First();

        // 1. Zwiad taktyczny (Recon) nie zmienia ChunkState -> spawn NIE przenosi się
        var chunkData = expansion.GetChunkData(nextBorderlandsRoad);
        chunkData.isReconActive = true;
        chunkData.reconIntel = new ChunkReconIntel { hasIntel = true, hasEnemyPath = true };
        Assert.AreEqual(ChunkState.Borderlands, expansion.GetEffectiveState(nextBorderlandsRoad));
        Assert.AreEqual(initialSpawnPoint, spawner.activeRoutes[0].currentSpawnPoint);

        // 2. Faktyczna eksploracja (Survey -> ChunkState.Surveying) natychmiast przenosi spawn na ten chunk
        expansion.SetChunkBaseState(nextBorderlandsRoad, ChunkState.Surveying);

        Vector3 newSpawnPoint = spawner.activeRoutes[0].currentSpawnPoint;
        Vector2Int newSpawnChunk = gen.GetChunkCoordFromWorldPosition(newSpawnPoint);

        Assert.AreEqual(nextBorderlandsRoad, newSpawnChunk, "Spawn point should move onto the Surveying road chunk.");
        Assert.Greater(spawner.activeRoutes[0].currentActivePath.Count, initialActivePathLen);
    }

    [Test]
    public void EnemyWalker_AppliesSpeedMultiplierOnlyInSurveyingFog()
    {
        var gen = CreateTestMapGenerator();

        var expansionGo = new GameObject("MapExpansionManager");
        expansionGo.transform.SetParent(holder.transform);
        var expansion = expansionGo.AddComponent<MapExpansionManager>();

        var fowGo = new GameObject("FogOfWarManager");
        fowGo.transform.SetParent(holder.transform);
        var fow = fowGo.AddComponent<FogOfWarManager>();
        fow.expansionManager = expansion;
        fow.mapGenerator = gen;
        fow.surveyingEnemySpeedMultiplier = 1.5f;

        var cOutskirts = new Vector2Int(0, 0);
        var cSurveying = new Vector2Int(1, 0);

        var worldData = new Dictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>>
        {
            { cOutskirts, new Dictionary<Vector2Int, HexCellData>() },
            { cSurveying, new Dictionary<Vector2Int, HexCellData>() },
        };
        expansion.Initialize(worldData);
        expansion.SetChunkBaseState(cOutskirts, ChunkState.Outskirts);
        expansion.SetChunkBaseState(cSurveying, ChunkState.Surveying);
        fow.UpdateAllFogVisuals();

        var enemyGo = new GameObject("TestEnemy");
        enemyGo.transform.SetParent(holder.transform);
        var stats = enemyGo.AddComponent<EnemyStats>();
        var walker = enemyGo.AddComponent<EnemyWalker>();
        walker.speed = 4f;

        Vector3 posSurveying = HexGridMath.GetChunkCenterWorld(cSurveying, gen.chunkRadius, gen.hexSize, gen.padding);
        Vector3 posOutskirts = HexGridMath.GetChunkCenterWorld(cOutskirts, gen.chunkRadius, gen.hexSize, gen.padding);

        walker.Initialize(new List<Vector3> { posSurveying, posOutskirts }, stats);

        Assert.IsTrue(walker.IsInSurveyingFog);
        Assert.AreEqual(6f, walker.EffectiveSpeed, 0.001f);
        Assert.AreEqual(6f, stats.CurrentMoveSpeed, 0.001f);

        // Przejście na teren Outskirts -> brak bonusu prędkości
        enemyGo.transform.position = posOutskirts;
        walker.RefreshSurveyingFogState();

        Assert.IsFalse(walker.IsInSurveyingFog);
        Assert.AreEqual(4f, walker.EffectiveSpeed, 0.001f);
        Assert.AreEqual(4f, stats.CurrentMoveSpeed, 0.001f);
    }

    [Test]
    public void FogRenderQueues_AllowEnemyHealthBarsThroughSurveyingFog_WhileBlockedByOpaqueFog()
    {
        var fowGo = new GameObject("FogOfWarManager");
        fowGo.transform.SetParent(holder.transform);
        var fow = fowGo.AddComponent<FogOfWarManager>();

        Shader mistShader = Shader.Find("Custom/Mist");
        Shader depthShader = Shader.Find("Custom/MistDepthPrepass");
        Shader hpShader = Shader.Find("Custom/EnemyHealthBar");
        if (mistShader == null || depthShader == null || hpShader == null)
        {
            Assert.Inconclusive("Required shaders not loaded in this test context.");
            return;
        }

        var defaultMat = new Material(mistShader);
        var borderlandsMat = new Material(mistShader);
        var surveyMat = new Material(mistShader);
        var surveyDepthMat = new Material(depthShader);
        var hpMat = new Material(hpShader);

        try
        {
            fow.defaultFogMaterial = defaultMat;
            fow.borderlandsFogMaterial = borderlandsMat;
            fow.surveyingFogMaterial = surveyMat;
            fow.surveyingDepthMaterial = surveyDepthMat;

            fow.UpdateAllFogVisuals();

            Assert.Less(defaultMat.renderQueue, hpMat.renderQueue, "Opaque Wilderness fog must render before EnemyHealthBar.");
            Assert.Less(borderlandsMat.renderQueue, hpMat.renderQueue, "Opaque Borderlands fog must render before EnemyHealthBar.");
            Assert.Greater(surveyDepthMat.renderQueue, hpMat.renderQueue, "Surveying depth prepass must render AFTER EnemyHealthBar so it does not Z-cull bars.");
            Assert.Greater(surveyMat.renderQueue, surveyDepthMat.renderQueue, "Surveying mist must render after Surveying depth prepass.");
        }
        finally
        {
            Object.DestroyImmediate(defaultMat);
            Object.DestroyImmediate(borderlandsMat);
            Object.DestroyImmediate(surveyMat);
            Object.DestroyImmediate(surveyDepthMat);
            Object.DestroyImmediate(hpMat);
        }
    }
}
