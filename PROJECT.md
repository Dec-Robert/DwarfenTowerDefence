# Project: DwarfenTowerDefence Map Generation Overhaul & Rebalance

## Architecture
- **Coordinates & Grid**: Hexagonal grid with axial coordinates $(q, r)$, chunk radius $R=3$ (19 hexes per chunk).
- **Core Pipeline (`BuildMapData`)**:
  1. `RegisterAllChunks`
  2. `ApplyMetaChunks` (Base at `(0,0)`)
  3. `CalculateMainPath` (Path A $\ge 11$ chunks, safe start 2 chunks with $\ge 6$ road hexes each)
  4. `GenerateLoops` (1 guaranteed + 30% second, span 2..4, length $\ge$ bypassed)
  5. `GenerateSideBranches` (2 branches, outward direction)
  6. `RebuildAllCrossroadsInternalPaths` (Y/T routing via central `hubHex`)
  7. `PlaceSpecialChunks` (rings 3–8, strictly outside all paths)
  8. `GenerateTerrain` (ring-based spawn rules, no biomes)
  9. `EnsureResourceGuarantees` (Ring 1–2 survival resources, Ring 3–4 mountain clusters)
  10. `ApplySpecialChunkResources` (`layoutOverride`)
- **Data Flow & Dependencies**:
  - `MapRng` initialized in `MapGenerationContext` drives all procedural RNG.
  - `ChunkPathData` and `MapGenerationContext` store path segments, crossroads, and loops.
  - `EnemySpawner` scans routes from Base upward, merges identical active paths before crossroads, and distributes waves via Flow weights.
  - `MapExpansionManager` and `ChunkWallManager` support multi-predecessors (`roadPredecessors`) to enable flexible loop exploration and frontier gate placement.

## Code Layout
| Module / File | Responsibility |
|---|---|
| `Assets/Main folder/Scripts/Map/MapGenerationContext.cs` | Holds generation state, `MapRng`, `LoopDefinition`, `crossroadsChunks`. |
| `Assets/Main folder/Scripts/Map/ChunkPathData.cs` | Path connections, `extraExits`, `nextChunksTowardsBase`, `isCrossroads`. |
| `Assets/Main folder/Scripts/Map/PathGeneration.cs` | Main Path A, loops, side branches, Y/T hub crossroads internal routing. |
| `Assets/Main folder/Scripts/Map/TerrainGenerationConfig.cs` | ScriptableObject defining ring rules, guarantees, and `CreateDefault()`. |
| `Assets/Main folder/Scripts/Map/TerrainGeneration.cs` | Ring-based terrain feature generation and `EnsureGuarantees()`. |
| `Assets/Main folder/Scripts/Map/SpecialChunkPlacer.cs` | Places unique chunks in rings 3-8 outside all paths using `MapRng`. |
| `Assets/Main folder/Scripts/Map/SpecialChunkDefinition.cs` | Special chunk definition with `layoutOverride` (no `allowedBiomes`). |
| `Assets/Main folder/Scripts/Map/HexMapGenerator.cs` | Orchestrates pipeline, seed configuration, road settings. |
| `Assets/Main folder/Scripts/Map/HexMapVisualizer.cs` | World visualizer using `matDefaultGrass` (no biome materials). |
| `Assets/Main folder/Scripts/Buildings/BuildingEntity Components/BuildingTerrainComponent.cs` | Redraws hexes without biome parameters. |
| `Assets/Main folder/Scripts/Enemies/EnemySpawner.cs` | Flow model, base-upward fog scan, route merging before crossroads. |
| `Assets/Main folder/Scripts/Expansion/MapExpansionManager.cs` | Multi-predecessor Borderlands propagation, recon intel crossroads flag. |
| `Assets/Main folder/Scripts/Expansion/ChunkStateData.cs` | `roadPredecessors` list, `isCrossroads` intel. |
| `Assets/Main folder/Scripts/Map/ChunkWallManager.cs` | Gate placement on boundary supporting multi-predecessors. |
| `Assets/Main folder/Editor/MapGenerationSimulatorWindow.cs` | Editor window testing 500 seeds with metrics and anomaly reports. |
| `Assets/Main folder/Editor/Tests/MapGenerationTests.cs` | 14 EditMode unit tests verifying invariants. |
| `BALANCE_DESIGN_DOCUMENT.md` | Balance design document sections 4 and 6.5. |
| `Assembly-CSharp.csproj` | Project compilation definitions (stale BiomeGeneration entry removed). |

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | Biome Deletion & Assembly Cleanup | Remove `BiomeType`, `BiomeSettings`, `BiomeGenerator`, stale `.csproj` reference | M1 | Survey 1, 2, 3 |
| 2 | Deterministic RNG (`MapRng`) | Implement `MapRng` wrapping `System.Random` in `MapGenerationContext` | M1 | Survey 1, 3 |
| 3 | Extended `ChunkPathData` | Support `extraExits`, `nextChunksTowardsBase`, `isCrossroads` | M1 | Survey 1, 2, 3 |
| 4 | Extended `ChunkStateData` | Support `roadPredecessors` list in `ChunkStateData` | M1 | Survey 2, 3 |
| 5 | `MapGenerationContext` Topology Models | Context contains `LoopDefinition` list, `crossroadsChunks`, `Rng` | M1 | Survey 1, 3 |
| 6 | Visualizer & Buildings Biome Cleanup | Strip `chunkBiomes`/`biomeMaterials` from `HexMapVisualizer` & `BuildingTerrainComponent` | M1 | Survey 1, 2 |
| 7 | Main Path A Generation | Path A $\ge 11$ chunks from furthest spawner to gate chunk | M2 | Survey 1, 3 |
| 8 | Protected Start Zone | First 2 chunks from base have zero crossroads or branch attachments | M2 | Survey 1, 3 |
| 9 | Protected Start Road Meander | Road length on each of first 2 chunks is $\ge 6$ hexes (`internalPath.Count >= 6`) | M2 | Survey 1, 3 |
| 10 | Loop Generation | 1 guaranteed loop + 30% second, span 2..4, length $\ge$ bypassed | M2 | Survey 1, 2, 3 |
| 11 | Outward Side Branches | 2 side branches with chances {100, 85}, spawners do not backtrack towards base | M2 | Survey 1, 3 |
| 12 | Crossroads Y/T Hub Routing | Clean Y/T road geometry connecting at central `hubHex` ($\text{dist} \le 1$) | M2 | Survey 1, 3 |
| 13 | EnemySpawner Flow Model | 70% main / 30% branch or loop flow weight distribution | M2 | Survey 2, 3 |
| 14 | Base-Upward Fog Route Scanning | Route scanning from Base upward to first unrevealed chunk | M2 | Survey 2, 3 |
| 15 | Fog Boundary Spawner on Loop | Loop in fog spawns at Rozdroże 1 fog boundary with 30% flow | M2 | Survey 2, 3 |
| 16 | Pre-Crossroads Route Merging | Routes sharing active revealed path merged into 1 spawner (100% Night 1) | M2 | Survey 2, 3 |
| 17 | Multi-Predecessor Borderlands | `MapExpansionManager` allows Borderlands if ANY predecessor is settled/outskirts | M2 | Survey 2, 3 |
| 18 | Recon Crossroads Detection | `ChunkReconIntel.isCrossroads` set during reconnaissance | M2 | Survey 2, 3 |
| 19 | ChunkWallManager Multi-Predecessor Support | Frontier gate placement supports multi-predecessor road dependencies | M2 | Survey 2 |
| 20 | `TerrainGenerationConfig` Asset | ScriptableObject defining ring spawn rules, guarantees, and `CreateDefault()` | M3 | Survey 1, 3 |
| 21 | Ring 1–2 Resource Guarantees | Forest $\ge 10$, FertileSoil $\ge 6$, Hill $\ge 6$, Mountain $= 0$ | M3 | Survey 1, 3 |
| 22 | Ring 3–4 Mountain Guarantees | Mountain $\ge 10$ across $\ge 2$ distinct chunks | M3 | Survey 1, 3 |
| 23 | `EnsureGuarantees()` Post-Pass | Post-generation check that tops up any resource deficits on free hexes | M3 | Survey 1, 3 |
| 24 | Special Chunk Path Exclusion | Predefined chunks placed in rings 3–8 strictly outside all paths via `MapRng` | M3 | Survey 1, 3 |
| 25 | Editor Map Simulator Window | `Tools > Map > Generation Simulator` analyzing 500 seeds with metrics & anomalies | M4 | Survey 1, 3 |
| 26 | EditMode Unit Test Suite | 14 EditMode tests in `MapGenerationTests.cs` validating all invariants | M4 | Survey 1, 3 |
| 27 | Balance Document Overhaul | Update sections 4 and 6.5 of `BALANCE_DESIGN_DOCUMENT.md` | M4 | Survey 3 |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Fundamenty, Modele Danych i Wycięcie Biomów | Features 1–6: Purge Biomes, fix CS2001, add `MapRng`, extend `ChunkPathData`, `ChunkStateData`, `MapGenerationContext`, cleanup visualizer. | none | DONE |
| M2 | Nowy Algorytm Ścieżek, Rozdroży, Pętli i Flow | Features 7–19: Path A $\ge 11$ chunks, safe start 2 chunks ($\ge 6$ hexes), loops, side branches, Y/T hub crossroads, Flow model, base-upward scan, route merging, `MapExpansionManager`, `ChunkWallManager`. | M1 | DONE |
| M3 | Nowa Generacja Zasobów i Chunki Unikatowe | Features 20–24: `TerrainGenerationConfig`, ring rules, guarantees (R1-2 & R3-4), `EnsureGuarantees()`, special chunk placement outside paths. | M1, M2 | IN_PROGRESS |
| M4 | Testy EditMode, Symulator i Dokumentacja | Features 25–27: `MapGenerationSimulatorWindow` (500 seeds), `MapGenerationTests` (14 EditMode tests), `BALANCE_DESIGN_DOCUMENT.md` update. | M1, M2, M3 | PLANNED |

## Interface Contracts
### MapGenerationContext ↔ Subsystems
- `ctx.Rng`: `MapRng` with `Range(int min, int maxExclusive)`, `Range(float min, float max)`, `Value`, `Shuffle<T>(IList<T> list)`.
- `ctx.loops`: `List<LoopDefinition>` where `LoopDefinition` has `splitCrossroads`, `mergeCrossroads`, `mainSegmentChunks`, `loopChunks`, `loopFlowRatio`.
- `ctx.crossroadsChunks`: `HashSet<Vector2Int>`.

### ChunkPathData ↔ PathGeneration & Expansion
- `extraExits`: `List<Vector2Int>` (outgoing directions from crossroads).
- `nextChunksTowardsBase`: `List<Vector2Int>` (all predecessor chunks towards Base).
- `isCrossroads`: `extraEntries.Count > 0 || extraExits.Count > 0`.

### PathGeneration ↔ EnemySpawner
- `GetAllSpawnRoutes(Vector2Int baseRoadEndLocal)` returns `List<SpawnPathVariant>`.
- `SpawnPathVariant`: `List<Vector3> fullPath`, `float flowWeight` (normalized tree probability).

### TerrainGenerationConfig ↔ TerrainGeneration
- `CreateDefault()` returns `TerrainGenerationConfig` instance with default rules and guarantees.
- `guarantees`: list of `ResourceGuarantee` with `ringRange`, `feature`, `minCount`, `minDistinctChunks`.
