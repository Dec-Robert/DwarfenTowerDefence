using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class ExpeditionSendPerformanceTests
{
    private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

    [SetUp]
    public void SetUp()
    {
        ClearSingletons();
    }

    [TearDown]
    public void TearDown()
    {
        ClearSingletons();
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }
        }
        createdObjects.Clear();
    }

    private static void ClearSingletons()
    {
        SetStaticProperty(typeof(MapExpansionManager), "Instance", null);
        SetStaticProperty(typeof(FogOfWarManager), "Instance", null);
        SetStaticProperty(typeof(HexMapGenerator), "Instance", null);
        SetStaticProperty(typeof(ResourceManager), "Instance", null);
        SetStaticProperty(typeof(WorkforceManager), "Instance", null);
    }

    private static void SetStaticProperty(Type type, string propName, object value)
    {
        var prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(null, value);
            return;
        }
        var field = type.GetField($"<{propName}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
        field?.SetValue(null, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
        return (T)field.GetValue(target);
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, $"Method '{methodName}' not found on {target.GetType().Name}.");
        method.Invoke(target, args);
    }

    private (MapExpansionManager exp, FogOfWarManager fow) SetupMapAndFog()
    {
        var genGo = new GameObject("TestHexMapGenerator");
        createdObjects.Add(genGo);
        var gen = genGo.AddComponent<HexMapGenerator>();
        gen.chunkRadius = 3;
        gen.hexSize = 1f;
        gen.padding = 0.02f;
        SetStaticProperty(typeof(HexMapGenerator), "Instance", gen);

        var expGo = new GameObject("TestMapExpansionManager");
        createdObjects.Add(expGo);
        var exp = expGo.AddComponent<MapExpansionManager>();
        SetStaticProperty(typeof(MapExpansionManager), "Instance", exp);

        var fowGo = new GameObject("TestFogOfWarManager");
        createdObjects.Add(fowGo);
        var fow = fowGo.AddComponent<FogOfWarManager>();
        fow.expansionManager = exp;
        fow.mapGenerator = gen;
        SetStaticProperty(typeof(FogOfWarManager), "Instance", fow);

        var worldData = new Dictionary<Vector2Int, Dictionary<Vector2Int, HexCellData>>
        {
            { new Vector2Int(0, 0), new Dictionary<Vector2Int, HexCellData>() },
            { new Vector2Int(1, 0), new Dictionary<Vector2Int, HexCellData>() },
            { new Vector2Int(2, 0), new Dictionary<Vector2Int, HexCellData>() }
        };
        exp.Initialize(worldData);
        fow.UpdateAllFogVisuals();
        return (exp, fow);
    }

    [Test]
    public void BorderlandsToSurveying_AndRecon_DoNotMarkGlobalFogMaskDirty()
    {
        var (exp, fow) = SetupMapAndFog();
        var borderCoord = new Vector2Int(1, 0);
        var wildCoord = new Vector2Int(2, 0);

        Assert.AreEqual(ChunkState.Borderlands, exp.GetEffectiveState(borderCoord));
        Assert.AreEqual(ChunkState.Wilderness, exp.GetEffectiveState(wildCoord));
        Assert.IsFalse(GetPrivateField<bool>(fow, "isGlobalFogMaskDirty"));

        // Przejście Borderlands -> Surveying nie zmienia _FogMask
        fow.UpdateChunkFogState(borderCoord, ChunkState.Surveying, animate: false);
        Assert.IsFalse(GetPrivateField<bool>(fow, "isGlobalFogMaskDirty"),
            "Borderlands -> Surveying must not trigger global fog mask rebuild.");

        // Ponowne wywołanie dla Recon (stan bez zmian) także nie brudzi maski
        fow.UpdateChunkFogState(wildCoord, ChunkState.Wilderness, animate: false);
        Assert.IsFalse(GetPrivateField<bool>(fow, "isGlobalFogMaskDirty"),
            "Unchanged state (e.g. Recon) must not mark global fog mask dirty.");

        // Natomiast Wilderness -> Borderlands lub Surveying -> Outskirts MUSI oznaczyć maskę jako dirty
        fow.UpdateChunkFogState(wildCoord, ChunkState.Borderlands, animate: false);
        Assert.IsTrue(GetPrivateField<bool>(fow, "isGlobalFogMaskDirty"),
            "Wilderness -> Borderlands must mark global fog mask dirty.");

        InvokePrivateMethod(fow, "LateUpdate");
        Assert.IsFalse(GetPrivateField<bool>(fow, "isGlobalFogMaskDirty"),
            "LateUpdate must flush dirty global fog mask.");

        fow.UpdateChunkFogState(borderCoord, ChunkState.Outskirts, animate: false);
        Assert.IsTrue(GetPrivateField<bool>(fow, "isGlobalFogMaskDirty"),
            "Surveying -> Outskirts must mark global fog mask dirty.");
    }

    [Test]
    public void BorderlandsToSurveying_ProducesBitIdenticalGlobalFogMaskTexture()
    {
        var (exp, fow) = SetupMapAndFog();
        var borderCoord = new Vector2Int(1, 0);
        var chunks = new HashSet<Vector2Int>(exp.activeChunks.Keys);

        InvokePrivateMethod(fow, "CalculateWorldBounds", chunks);
        InvokePrivateMethod(fow, "InitMaskTexture");
        fow.UpdateAllFogVisuals();

        Texture2D tex = fow.FogMaskTexture;
        Assert.IsNotNull(tex);
        Color32[] beforePixels = tex.GetPixels32();

        // Symulacja wysyłki ekspedycji na chunk (1,0)
        exp.SetChunkBaseState(borderCoord, ChunkState.Surveying);
        fow.UpdateChunkFogState(borderCoord, ChunkState.Surveying, animate: false);
        fow.UpdateGlobalFogMask();

        Color32[] afterPixels = tex.GetPixels32();
        Assert.AreEqual(beforePixels.Length, afterPixels.Length);

        for (int i = 0; i < beforePixels.Length; i++)
        {
            Assert.AreEqual(beforePixels[i].r, afterPixels[i].r);
            Assert.AreEqual(beforePixels[i].g, afterPixels[i].g);
            Assert.AreEqual(beforePixels[i].b, afterPixels[i].b);
            Assert.AreEqual(beforePixels[i].a, afterPixels[i].a);
        }
    }

    [Test]
    public void ChunkExpeditionModalUI_SuppressesResourceAndWorkforceCallbacksDuringSubmit()
    {
        var go = new GameObject("TestChunkModal");
        createdObjects.Add(go);
        var modal = go.AddComponent<ChunkExpeditionModalUI>();

        var isSubmittingField = typeof(ChunkExpeditionModalUI).GetField("isSubmittingMission", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(isSubmittingField);

        // Gdy isSubmittingMission == true, HandleResourceChanged / HandleWorkforceChanged wracają natychmiast bez NullRef z nieistniejących pól UI
        // (gdy MapExpansionManager.Instance == null oba i tak wyszłyby wcześnie, więc stawiamy MapExpansionManager z costConfig != null,
        // gdzie brak isSubmittingMission rzuciłby w RefreshView lub wykonał pełne odświeżenie).
        var expGo = new GameObject("TestExp");
        createdObjects.Add(expGo);
        var exp = expGo.AddComponent<MapExpansionManager>();
        var cfg = ScriptableObject.CreateInstance<ExpansionCostConfig>();
        createdObjects.Add(cfg);
        exp.costConfig = cfg;
        exp.activeChunks[Vector2Int.zero] = new ChunkStateData { chunkCoord = Vector2Int.zero, baseState = ChunkState.Borderlands };
        SetStaticProperty(typeof(MapExpansionManager), "Instance", exp);

        isSubmittingField.SetValue(modal, true);
        Assert.DoesNotThrow(() =>
        {
            InvokePrivateMethod(modal, "HandleResourceChanged", ResourceType.Ether, 10);
            InvokePrivateMethod(modal, "HandleWorkforceChanged", Race.Elves, new WorkforceManager.WorkforceStats());
        });
        isSubmittingField.SetValue(modal, false);
    }

    [Test]
    public void PixelChunkLookupCache_ProducesBitIdenticalGlobalFogMask_AcrossDawnTransition()
    {
        var (exp, fow) = SetupMapAndFog();
        var borderCoord = new Vector2Int(1, 0);
        var wildCoord = new Vector2Int(2, 0);
        var chunks = new HashSet<Vector2Int>(exp.activeChunks.Keys);

        InvokePrivateMethod(fow, "CalculateWorldBounds", chunks);
        InvokePrivateMethod(fow, "InitMaskTexture");

        exp.SetChunkBaseState(borderCoord, ChunkState.Outskirts);
        exp.SetChunkBaseState(wildCoord, ChunkState.Borderlands);
        fow.UpdateAllFogVisuals();

        Texture2D tex = fow.FogMaskTexture;
        Assert.IsNotNull(tex);
        Color32[] cachedPixels = tex.GetPixels32();

        // Force invalidation and recompute to confirm deterministic parity
        InvokePrivateMethod(fow, "InvalidatePixelChunkLookup");
        fow.UpdateGlobalFogMask();
        Color32[] rebuiltPixels = tex.GetPixels32();

        Assert.AreEqual(cachedPixels.Length, rebuiltPixels.Length);
        for (int i = 0; i < cachedPixels.Length; i++)
        {
            Assert.AreEqual(cachedPixels[i].r, rebuiltPixels[i].r);
            Assert.AreEqual(cachedPixels[i].g, rebuiltPixels[i].g);
            Assert.AreEqual(cachedPixels[i].b, rebuiltPixels[i].b);
            Assert.AreEqual(cachedPixels[i].a, rebuiltPixels[i].a);
        }
    }

    [Test]
    public void DawnRevealManagers_CoalesceMultipleChunkEvents_UntilLateUpdate()
    {
        var regGo = new GameObject("TestBuildingRegistry");
        createdObjects.Add(regGo);
        var registry = regGo.AddComponent<BuildingRegistry>();

        InvokePrivateMethod(registry, "HandleChunkStateChanged", new Vector2Int(1, 0), ChunkState.Outskirts);
        InvokePrivateMethod(registry, "HandleChunkRevealed", new Vector2Int(1, 0));
        InvokePrivateMethod(registry, "HandleChunkStateChanged", new Vector2Int(2, 0), ChunkState.Settled);
        Assert.IsTrue(GetPrivateField<bool>(registry, "isRescanDirty"));

        InvokePrivateMethod(registry, "LateUpdate");
        Assert.IsFalse(GetPrivateField<bool>(registry, "isRescanDirty"));

        var spawnerGo = new GameObject("TestEnemySpawner");
        createdObjects.Add(spawnerGo);
        var spawner = spawnerGo.AddComponent<EnemySpawner>();
        InvokePrivateMethod(spawner, "OnChunkRevealedHandler", new Vector2Int(1, 0));
        InvokePrivateMethod(spawner, "OnChunkRevealedHandler", new Vector2Int(2, 0));
        Assert.IsTrue(GetPrivateField<bool>(spawner, "isRoutesDirty"));

        InvokePrivateMethod(spawner, "LateUpdate");
        Assert.IsFalse(GetPrivateField<bool>(spawner, "isRoutesDirty"));

        var wallGo = new GameObject("TestChunkWallManager");
        createdObjects.Add(wallGo);
        var walls = wallGo.AddComponent<ChunkWallManager>();
        walls.SetMode(WallSystemMode.Solution2_FrontLine);
        InvokePrivateMethod(walls, "HandleChunkStateChanged", new Vector2Int(1, 0), ChunkState.Outskirts);
        InvokePrivateMethod(walls, "HandleChunkStateChanged", new Vector2Int(2, 0), ChunkState.Settled);
        Assert.IsTrue(GetPrivateField<bool>(walls, "isRebuildDirty"));

        InvokePrivateMethod(walls, "LateUpdate");
        Assert.IsFalse(GetPrivateField<bool>(walls, "isRebuildDirty"));
    }
}
