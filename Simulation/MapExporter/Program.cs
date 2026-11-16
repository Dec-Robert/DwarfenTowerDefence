using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityEngine;

namespace MapExporter
{
    internal class Vec2Dto
    {
        public int x { get; set; }
        public int y { get; set; }
        public Vec2Dto() { }
        public Vec2Dto(int x, int y) { this.x = x; this.y = y; }
        public Vector2Int ToV2() => new Vector2Int(x, y);
        public int[] ToArr() => new[] { x, y };
    }

    internal class HexOverrideDto
    {
        public int[] localCoord { get; set; } = new int[2];
        public int feature { get; set; }
        public int featureLevel { get; set; }
        public int elevation { get; set; }
        public string? startingBuilding { get; set; }
    }

    internal class ChunkLayoutDto
    {
        public int type { get; set; }
        public List<HexOverrideDto> hexes { get; set; } = new();
        public int[] roadConnectionEdge { get; set; } = new[] { 3, -2 };
    }

    internal class ExpansionDto
    {
        public int[] chunkCoordinate { get; set; } = new int[2];
        public ChunkLayoutDto? baseLayout { get; set; }
    }

    internal class ModifierDto
    {
        public string name { get; set; } = "";
        public int[] targetChunk { get; set; } = new int[2];
        public ChunkLayoutDto? modifierLayout { get; set; }
    }

    internal class MetaMapConfigDto
    {
        public ChunkLayoutDto? startingChunkBase { get; set; }
        public List<ExpansionDto> expansions { get; set; } = new();
        public List<ModifierDto> globalModifiers { get; set; } = new();
    }

    internal class RingRuleDto
    {
        public string ruleName { get; set; } = "";
        public int ringMin { get; set; }
        public int ringMax { get; set; }
        public int forestWeight { get; set; }
        public int fertileSoilWeight { get; set; }
        public int hillWeight { get; set; }
        public int mountainWeight { get; set; }
        public int sinkholeWeight { get; set; }
        public int emptyWeight { get; set; }
        public bool clusterForests { get; set; }
        public int forestClusterChance { get; set; }
        public int minForestClusters { get; set; }
        public int maxForestClusters { get; set; }
        public bool clusterMountains { get; set; }
        public int mountainClusterChance { get; set; }
        public int minMountainClusters { get; set; }
        public int maxMountainClusters { get; set; }
    }

    internal class GuaranteeDto
    {
        public string guaranteeName { get; set; } = "";
        public int ringMin { get; set; }
        public int ringMax { get; set; }
        public int feature { get; set; }
        public int minCount { get; set; }
        public int minDistinctChunks { get; set; }
        public int maxAllowed { get; set; } = -1;
    }

    internal class TerrainConfigDto
    {
        public List<RingRuleDto> ringRules { get; set; } = new();
        public List<GuaranteeDto> guarantees { get; set; } = new();
    }

    internal class TopologySettingsDto
    {
        public int safeStartChunks { get; set; } = 2;
        public int minStartRoadLength { get; set; } = 6;
        public int minChunksBetweenCrossroads { get; set; } = 1;
        public int guaranteedLoops { get; set; } = 1;
        public int extraLoopChance { get; set; } = 30;
        public int[] loopSpanRange { get; set; } = new[] { 2, 4 };
        public float loopFlowRatio { get; set; } = 0.3f;
        public int[] sideBranchChances { get; set; } = new[] { 100, 85 };
        public float branchFlowRatio { get; set; } = 0.3f;
    }

    internal class InternalSettingsDto
    {
        public int highwayWeight { get; set; } = 20;
        public int windingWeight { get; set; } = 30;
        public int mazeWeight { get; set; } = 50;
        public int wallCost { get; set; } = 50;
        public int windingWallCount { get; set; } = 2;
        public int windingWallLength { get; set; } = 3;
        public int mazeWallCount { get; set; } = 5;
        public int mazeWallLength { get; set; } = 3;
    }

    internal class ExporterConfig
    {
        public int mapWidth { get; set; } = 8;
        public int mapMinY { get; set; } = -3;
        public int mapMaxY { get; set; } = 3;
        public int chunkRadius { get; set; } = 3;
        public float hexSize { get; set; } = 1.0f;
        public float padding { get; set; } = 0.0f;
        public int minChunkDistance { get; set; } = 11;
        public int maxGenerationAttempts { get; set; } = 1000;
        public bool allowExtraSpawners { get; set; } = true;
        public int[] extraSpawnerChances { get; set; } = new[] { 80, 60, 40, 20, 5, 5 };
        public int[] extraSpawnDistanceRange { get; set; } = new[] { 2, 6 };
        public int chanceForMoreBranching { get; set; } = 30;
        public int globalObstacleChance { get; set; } = 70;
        public int[] baseRoadEndLocal { get; set; } = new[] { 3, -2 };
        public TopologySettingsDto topologySettings { get; set; } = new();
        public InternalSettingsDto internalSettings { get; set; } = new();
        public TerrainConfigDto? terrainConfig { get; set; }
        public MetaMapConfigDto? metaConfig { get; set; }
    }

    internal static class Program
    {
        private static readonly FieldInfo? CachedPtrField =
            typeof(UnityEngine.Object).GetField("m_CachedPtr", BindingFlags.NonPublic | BindingFlags.Instance);

        private static T CreateUnityObject<T>(string name = "") where T : class
        {
            var obj = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
            // Initialize m_CachedPtr so Unity's custom == / != / bool operators treat it as non-null!
            if (obj is UnityEngine.Object && CachedPtrField != null)
            {
                CachedPtrField.SetValue(obj, new IntPtr(1));
            }
            return obj;
        }

        private static BuildingData GetOrCreateBuildingStub(
            Dictionary<string, BuildingData> cache, string? name)
        {
            string key = string.IsNullOrWhiteSpace(name) ? "" : name.Trim();
            if (cache.TryGetValue(key, out var existing))
                return existing;

            var b = CreateUnityObject<BuildingData>(key);
            b.buildingName = key;
            cache[key] = b;
            return b;
        }

        private static ChunkLayoutSO? BuildChunkLayout(
            ChunkLayoutDto? dto, Dictionary<string, BuildingData> buildingCache)
        {
            if (dto == null) return null;
            var so = CreateUnityObject<ChunkLayoutSO>();
            so.type = (PredefinedChunkType)dto.type;
            so.roadConnectionEdge = dto.roadConnectionEdge != null && dto.roadConnectionEdge.Length >= 2
                ? new Vector2Int(dto.roadConnectionEdge[0], dto.roadConnectionEdge[1])
                : Vector2Int.zero;
            so.hexes = new List<HexOverride>();
            if (dto.hexes != null)
            {
                foreach (var h in dto.hexes)
                {
                    so.hexes.Add(new HexOverride
                    {
                        localCoord = new Vector2Int(h.localCoord[0], h.localCoord[1]),
                        feature = (HexFeatureType)h.feature,
                        featureLevel = h.featureLevel,
                        elevation = h.elevation,
                        building = string.IsNullOrWhiteSpace(h.startingBuilding)
                            ? null
                            : GetOrCreateBuildingStub(buildingCache, h.startingBuilding)
                    });
                }
            }
            return so;
        }

        private static TerrainGenerationConfig BuildDefaultTerrainConfig()
        {
            var tc = CreateUnityObject<TerrainGenerationConfig>();
            tc.ringRules = new List<RingFeatureRule>
            {
                new RingFeatureRule
                {
                    ruleName = "Rings 1-2 (Early Survival)",
                    ringMin = 1, ringMax = 2,
                    forestWeight = 24, fertileSoilWeight = 22, hillWeight = 15,
                    mountainWeight = 0, sinkholeWeight = 7, emptyWeight = 32,
                    clusterForests = true, forestClusterChance = 30, minForestClusters = 1, maxForestClusters = 3,
                    clusterMountains = false, mountainClusterChance = 0, minMountainClusters = 0, maxMountainClusters = 0
                },
                new RingFeatureRule
                {
                    ruleName = "Rings 3-4 (Expansion & Mining)",
                    ringMin = 3, ringMax = 4,
                    forestWeight = 20, fertileSoilWeight = 15, hillWeight = 15,
                    mountainWeight = 25, sinkholeWeight = 5, emptyWeight = 20,
                    clusterForests = true, forestClusterChance = 25, minForestClusters = 1, maxForestClusters = 2,
                    clusterMountains = true, mountainClusterChance = 30, minMountainClusters = 1, maxMountainClusters = 2
                },
                new RingFeatureRule
                {
                    ruleName = "Rings 5+ (Wilderness Frontier)",
                    ringMin = 5, ringMax = 99,
                    forestWeight = 15, fertileSoilWeight = 10, hillWeight = 20,
                    mountainWeight = 30, sinkholeWeight = 10, emptyWeight = 15,
                    clusterForests = false, forestClusterChance = 15, minForestClusters = 0, maxForestClusters = 1,
                    clusterMountains = true, mountainClusterChance = 30, minMountainClusters = 1, maxMountainClusters = 2
                }
            };
            tc.guarantees = new List<ResourceGuarantee>
            {
                new ResourceGuarantee { guaranteeName = "R1-2 Forest >= 10", ringMin = 1, ringMax = 2, feature = HexFeatureType.Forest, minCount = 10, minDistinctChunks = 1, maxAllowed = -1 },
                new ResourceGuarantee { guaranteeName = "R1-2 FertileSoil >= 6", ringMin = 1, ringMax = 2, feature = HexFeatureType.Meadow, minCount = 6, minDistinctChunks = 1, maxAllowed = -1 },
                new ResourceGuarantee { guaranteeName = "R1-2 Hill >= 6", ringMin = 1, ringMax = 2, feature = HexFeatureType.Hill, minCount = 6, minDistinctChunks = 1, maxAllowed = -1 },
                new ResourceGuarantee { guaranteeName = "R1-2 Mountain == 0", ringMin = 1, ringMax = 2, feature = HexFeatureType.Mountain, minCount = 0, minDistinctChunks = 0, maxAllowed = 0 },
                new ResourceGuarantee { guaranteeName = "R3-4 Mountain >= 10", ringMin = 3, ringMax = 4, feature = HexFeatureType.Mountain, minCount = 10, minDistinctChunks = 2, maxAllowed = -1 },
            };
            return tc;
        }

        private static TerrainGenerationConfig BuildTerrainConfig(TerrainConfigDto? dto)
        {
            if (dto == null || (dto.ringRules.Count == 0 && dto.guarantees.Count == 0))
                return BuildDefaultTerrainConfig();

            var tc = CreateUnityObject<TerrainGenerationConfig>();
            tc.ringRules = dto.ringRules.Select(r => new RingFeatureRule
            {
                ruleName = r.ruleName,
                ringMin = r.ringMin,
                ringMax = r.ringMax,
                forestWeight = r.forestWeight,
                fertileSoilWeight = r.fertileSoilWeight,
                hillWeight = r.hillWeight,
                mountainWeight = r.mountainWeight,
                sinkholeWeight = r.sinkholeWeight,
                emptyWeight = r.emptyWeight,
                clusterForests = r.clusterForests,
                forestClusterChance = r.forestClusterChance,
                minForestClusters = r.minForestClusters,
                maxForestClusters = r.maxForestClusters,
                clusterMountains = r.clusterMountains,
                mountainClusterChance = r.mountainClusterChance,
                minMountainClusters = r.minMountainClusters,
                maxMountainClusters = r.maxMountainClusters
            }).ToList();

            tc.guarantees = dto.guarantees.Select(g => new ResourceGuarantee
            {
                guaranteeName = g.guaranteeName,
                ringMin = g.ringMin,
                ringMax = g.ringMax,
                feature = (HexFeatureType)g.feature,
                minCount = g.minCount,
                minDistinctChunks = g.minDistinctChunks,
                maxAllowed = g.maxAllowed
            }).ToList();
            return tc;
        }

        public static int Main(string[] args)
        {
            if (args.Contains("--dump-enums"))
            {
                Type[] types;
                try { types = typeof(HexMapGenerator).Assembly.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }

                var enumMap = new Dictionary<string, Dictionary<string, int>>();
                foreach (var t in types.Where(t => t.IsEnum).OrderBy(t => t.FullName))
                {
                    var d = new Dictionary<string, int>();
                    foreach (var name in Enum.GetNames(t))
                        d[name] = Convert.ToInt32(Enum.Parse(t, name));
                    enumMap[t.FullName ?? t.Name] = d;
                }
                Console.WriteLine(JsonSerializer.Serialize(enumMap, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }

            if (args.Contains("--dump-survey-oracle"))
            {
                var ec = CreateUnityObject<ExpansionCostConfig>();
                ec.minDays = 1;
                ec.maxDays = 7;
                ec.maxDistanceForTime = 6;
                ec.baseEtherCost = 20;
                ec.baseFoodCost = 10;
                ec.etherPerHex = 5;
                ec.foodPerHex = 5;
                ec.roadCostMultiplier = 0.6f;
                ec.roadTimeMultiplier = 0.7f;
                ec.minElvesForSurvey = 1;
                ec.chunksPerExtraSurveyDay = 4;
                ec.reconDays = 1;
                ec.reconElfRequirement = 1;
                ec.reconEtherCost = 0;
                ec.reconFoodCost = 50;
                ec.reconWoodCost = 20;

                var chunks = new Dictionary<Vector2Int, ChunkStateData>
                {
                    { Vector2Int.zero, new ChunkStateData { baseState = ChunkState.Settled } }
                };
                var rows = new List<object>();
                for (int ring = 1; ring <= 6; ring++)
                {
                    var coord = new Vector2Int(ring, 0);
                    foreach (bool isRoad in new[] { false, true })
                    {
                        foreach (int elves in new[] { 1, 2, 3 })
                        {
                            foreach (int disc in new[] { 0, 1, 3, 4, 5, 8 })
                            {
                                var mc = ExpansionCostCalculator.CalculateSurveyCost(coord, chunks, isRoad, ec, elves, disc);
                                rows.Add(new { ring, isRoad, elves, disc, days = mc.days, ether = mc.etherCost, food = mc.foodCost, wood = mc.woodCost });
                            }
                        }
                    }
                }
                Console.WriteLine(JsonSerializer.Serialize(rows));
                return 0;
            }

            int seed = 12345;
            string? configPath = null;
            string? outPath = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--seed" && i + 1 < args.Length)
                    seed = int.Parse(args[++i]);
                else if (args[i] == "--config" && i + 1 < args.Length)
                    configPath = args[++i];
                else if (args[i] == "--out" && i + 1 < args.Length)
                    outPath = args[++i];
            }

            ExporterConfig cfg;
            if (!string.IsNullOrEmpty(configPath))
            {
                string raw = File.ReadAllText(configPath);
                cfg = JsonSerializer.Deserialize<ExporterConfig>(raw) ?? new ExporterConfig();
            }
            else
            {
                cfg = new ExporterConfig();
            }

            var buildingCache = new Dictionary<string, BuildingData>();
            var gen = CreateUnityObject<HexMapGenerator>();
            gen.seed = seed;
            gen.mapWidth = cfg.mapWidth;
            gen.mapMinY = cfg.mapMinY;
            gen.mapMaxY = cfg.mapMaxY;
            gen.chunkRadius = cfg.chunkRadius;
            gen.hexSize = cfg.hexSize;
            gen.padding = cfg.padding;
            gen.minChunkDistance = cfg.minChunkDistance;
            gen.maxGenerationAttempts = cfg.maxGenerationAttempts;
            gen.allowExtraSpawners = cfg.allowExtraSpawners;
            gen.extraSpawnerChances = cfg.extraSpawnerChances;
            gen.extraSpawnDistanceRange = new Vector2Int(cfg.extraSpawnDistanceRange[0], cfg.extraSpawnDistanceRange[1]);
            gen.chanceForMoreBranching = cfg.chanceForMoreBranching;
            gen.globalObstacleChance = cfg.globalObstacleChance;
            gen.baseRoadEndLocal = new Vector2Int(cfg.baseRoadEndLocal[0], cfg.baseRoadEndLocal[1]);
            gen.specialChunkTypes = new List<SpecialChunkDefinition>();
            gen.forceUsePredefinedMap = false;

            gen.topologySettings = new HexMapGenerator.RoadTopologySettings
            {
                safeStartChunks = cfg.topologySettings.safeStartChunks,
                minStartRoadLength = cfg.topologySettings.minStartRoadLength,
                minChunksBetweenCrossroads = cfg.topologySettings.minChunksBetweenCrossroads,
                guaranteedLoops = cfg.topologySettings.guaranteedLoops,
                extraLoopChance = cfg.topologySettings.extraLoopChance,
                loopSpanRange = new Vector2Int(cfg.topologySettings.loopSpanRange[0], cfg.topologySettings.loopSpanRange[1]),
                loopFlowRatio = cfg.topologySettings.loopFlowRatio,
                sideBranchChances = cfg.topologySettings.sideBranchChances,
                branchFlowRatio = cfg.topologySettings.branchFlowRatio
            };

            gen.internalSettings = new HexMapGenerator.ChunkStyleSettings
            {
                highwayWeight = cfg.internalSettings.highwayWeight,
                windingWeight = cfg.internalSettings.windingWeight,
                mazeWeight = cfg.internalSettings.mazeWeight,
                wallCost = cfg.internalSettings.wallCost,
                windingWallCount = cfg.internalSettings.windingWallCount,
                windingWallLength = cfg.internalSettings.windingWallLength,
                mazeWallCount = cfg.internalSettings.mazeWallCount,
                mazeWallLength = cfg.internalSettings.mazeWallLength
            };

            gen.terrainConfig = BuildTerrainConfig(cfg.terrainConfig);

            if (cfg.metaConfig != null)
            {
                var unlockedMarker = CreateUnityObject<MetaUpgradeSO>("__SimUnlocked");
                unlockedMarker.isUnlocked = true;

                var mc = CreateUnityObject<MetaMapConfig>();
                mc.startingChunkBase = BuildChunkLayout(cfg.metaConfig.startingChunkBase, buildingCache);
                mc.expansions = cfg.metaConfig.expansions.Select(e => new MetaMapConfig.ExpansionChunkLink
                {
                    chunkCoordinate = new Vector2Int(e.chunkCoordinate[0], e.chunkCoordinate[1]),
                    requiredUpgrade = unlockedMarker,
                    baseLayout = BuildChunkLayout(e.baseLayout, buildingCache)
                }).ToList();
                mc.globalModifiers = cfg.metaConfig.globalModifiers.Select(m => new MetaMapConfig.ChunkModifierLink
                {
                    name = m.name,
                    targetChunk = new Vector2Int(m.targetChunk[0], m.targetChunk[1]),
                    requiredUpgrade = unlockedMarker,
                    modifierLayout = BuildChunkLayout(m.modifierLayout, buildingCache)
                }).ToList();
                gen.metaConfig = mc;
            }

            MapGenerationContext ctx = gen.BuildMapData(seed);
            List<SpawnPathVariant> routes = gen.GetAllSpawnRoutes();
            Dictionary<Vector2Int, List<Vector2Int>> roadDeps = gen.GetRoadRevealDependenciesMulti();

            var chunkList = new List<object>();
            var orderedCoords = ctx.allValidChunks
                .OrderBy(c => HexGridMath.GetDistance(Vector2Int.zero, c))
                .ThenBy(c => c.x)
                .ThenBy(c => c.y)
                .ToList();

            foreach (var c in orderedCoords)
            {
                ctx.chunkTypes.TryGetValue(c, out var ctype);
                ctx.chunkPaths.TryGetValue(c, out var pathData);

                var hexesOut = new List<object>();
                if (ctx.worldData.TryGetValue(c, out var hexDict))
                {
                    foreach (var kv in hexDict.OrderBy(k => k.Key.x).ThenBy(k => k.Key.y))
                    {
                        var h = kv.Value;
                        Vector2Int globalCoord = HexGridMath.LocalToGlobal(c, h.localCoord, cfg.chunkRadius);
                        Vector3 worldPos = HexGridMath.AxialToWorld(globalCoord.x, globalCoord.y, cfg.hexSize, cfg.padding);
                        string? startBld = h.startingBuilding != null ? h.startingBuilding.buildingName : null;
                        hexesOut.Add(new
                        {
                            local = new[] { h.localCoord.x, h.localCoord.y },
                            global = new[] { globalCoord.x, globalCoord.y },
                            world = new[] { Math.Round(worldPos.x, 5), Math.Round(worldPos.z, 5) },
                            feature = (int)h.feature,
                            featureName = h.feature.ToString(),
                            featureLevel = h.featureLevel,
                            elevation = h.elevation,
                            isRoad = h.isRoad,
                            startingBuilding = startBld
                        });
                    }
                }

                object? pathOut = null;
                if (pathData != null)
                {
                    pathOut = new
                    {
                        entryHex = new[] { pathData.entryHex.x, pathData.entryHex.y },
                        extraEntries = (pathData.extraEntries ?? new List<Vector2Int>()).Select(v => new[] { v.x, v.y }).ToList(),
                        exitHex = new[] { pathData.exitHex.x, pathData.exitHex.y },
                        extraExits = (pathData.extraExits ?? new List<Vector2Int>()).Select(v => new[] { v.x, v.y }).ToList(),
                        internalPath = (pathData.internalPath ?? new List<Vector2Int>()).Select(v => new[] { v.x, v.y }).ToList(),
                        nextChunksTowardsBase = (pathData.nextChunksTowardsBase ?? new List<Vector2Int>()).Select(v => new[] { v.x, v.y }).ToList(),
                        isCrossroads = pathData.isCrossroads
                    };
                }

                chunkList.Add(new
                {
                    coord = new[] { c.x, c.y },
                    ring = HexGridMath.GetDistance(Vector2Int.zero, c),
                    chunkType = ctype.ToString(),
                    path = pathOut,
                    hexes = hexesOut
                });
            }

            var routesOut = routes.Select(r => new
            {
                name = r.name,
                flowWeight = r.flowWeight,
                spawnerChunk = new[] { r.spawnerChunk.x, r.spawnerChunk.y },
                isLoopRoute = r.isLoopRoute,
                loopSplitCrossroads = new[] { r.loopSplitCrossroads.x, r.loopSplitCrossroads.y },
                loopMergeCrossroads = new[] { r.loopMergeCrossroads.x, r.loopMergeCrossroads.y },
                fullPath = (r.fullPath ?? new List<Vector3>())
                    .Select(p => new[] { Math.Round(p.x, 5), Math.Round(p.y, 5), Math.Round(p.z, 5) })
                    .ToList()
            }).ToList();

            var roadDepsOut = roadDeps
                .OrderBy(k => k.Key.x).ThenBy(k => k.Key.y)
                .Select(k => new
                {
                    chunk = new[] { k.Key.x, k.Key.y },
                    dependsOn = k.Value.Select(v => new[] { v.x, v.y }).ToList()
                }).ToList();

            var output = new
            {
                seed = ctx.seed,
                chunkRadius = cfg.chunkRadius,
                hexSize = cfg.hexSize,
                padding = cfg.padding,
                mainSpawnerChunk = new[] { ctx.mainSpawnerChunk.x, ctx.mainSpawnerChunk.y },
                extraSpawnerChunks = ctx.extraSpawnerChunks.Select(v => new[] { v.x, v.y }).ToList(),
                crossroadsChunks = ctx.crossroadsChunks.OrderBy(v => v.x).ThenBy(v => v.y).Select(v => new[] { v.x, v.y }).ToList(),
                allValidChunks = orderedCoords.Select(v => new[] { v.x, v.y }).ToList(),
                generatedChunkSequence = ctx.generatedChunkSequence.Select(v => new[] { v.x, v.y }).ToList(),
                chunks = chunkList,
                spawnRoutes = routesOut,
                roadDependencies = roadDepsOut
            };

            string json = JsonSerializer.Serialize(output, new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            });

            if (!string.IsNullOrEmpty(outPath))
                File.WriteAllText(outPath, json);
            else
                Console.WriteLine(json);

            return 0;
        }
    }
}
