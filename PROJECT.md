# Project: DwarfenTowerDefence Combat Loop Performance Optimization

## Architecture
- **Targeting & Registry Module (`EnemyRegistry`)**: Static registry tracking active enemies with zero allocation via Unity's `OnEnable`/`OnDisable` lifecycle. Replaces whole-scene `FindObjectsByType` traversal in `TowerController`.
- **Tower Combat Module (`TowerController`)**: Pre-allocated ping-pong candidate buffers (`candidateBufferA`, `candidateBufferB`) and imperative iterative filtering loops. Preserves 100% numerical and behavioral fidelity for all 8 targeting modes, tie-breakers, taunts, and chunk modes with 0 bytes GC heap allocation.
- **Enemy Skills Module (`MaddeningGazeSkill`, `ShadeMinionSkill`, `PointDefenseSkill`)**: Interval throttling (0.2s for Gaze, 0.1s for Shade, 4.0s cooldown for PointDefense), LayerMask scoping / non-alloc querying, gate coordinate caching, and robust `OnDisable` lifecycle cleanup for Fear and Taunt debuffs.
- **Visuals & Material Batching Module (`EnemyStats`)**: Universal `MaterialPropertyBlock` setting both `_BaseColor` and `_Color` properties via static block, caching all child renderers, eliminating all `renderer.material` access (zero material leaks), and restoring full SRP Batcher efficiency via `SetPropertyBlock(null)` upon reset.
- **Testing & Verification Module**: 45-test automated NUnit test suite in `Assets/Main folder/Editor/Tests/CombatOptimizationTests.cs` validating targeting priority fidelity, skill debuffs/blasts, material instance immutability, and 0 GC allocations.

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | `EnemyRegistry` Implementation | Static registry tracking `ActiveEnemies` via `OnEnable`/`OnDisable`, covering `EnemySpawner`, `BlobSplitSkill`, and pooling | M1 | Survey R1 |
| 2 | `EnemyStats` Walker Caching | Cache `EnemyWalker` property on `EnemyStats` at `Awake` to eliminate `GetComponent` calls during track progress queries | M1 | Survey R1 |
| 3 | `TowerController` FindObjects Elimination | Replace `FindObjectsByType<EnemyStats>` with iteration over `EnemyRegistry.ActiveEnemies` and squared distance checks | M1 | Survey R1 |
| 4 | `TowerController` Zero-Alloc Buffers | Pre-allocate ping-pong candidate buffers and replace LINQ/delegates/FindAll with imperative indexed loops | M1 | Survey R1 |
| 5 | Targeting Priority Rules Fidelity | Maintain 100% fidelity for Closest, Furthest, ClosestOnTrack, FurthestOnTrack, MostHP, LeastHP, HighestThreat, Random, Chunk, and Taunt | M1 | Survey R1 |
| 6 | `MaddeningGazeSkill` Physics & Timer Optimization | 0.2s interval throttling, LayerMask / NonAlloc query, persistent collections, and `OnDisable` Fear cleanup | M1 | Survey R2 |
| 7 | `ShadeMinionSkill` Physics & Timer Optimization | 0.1s interval throttling, cached base coordinate, LayerMask / NonAlloc query, and `OnDisable` Taunt cleanup | M1 | Survey R2 |
| 8 | `PointDefenseSkill` Physics Optimization | NonAlloc projectile query / LayerMask scoping, retain 4.0s cooldown, zero heap allocation during blast | M1 | Survey R2 |
| 9 | `EnemyStats` MaterialPropertyBlock Integration | Static cached `MaterialPropertyBlock`, setting `_BaseColor` and `_Color`, eliminating 5 `renderer.material` cloning points | M1 | Survey R3 |
| 10 | Multi-Renderer Support & SRP Batching | Cache `Renderer[]` on `EnemyStats`, restore shared material batching via `SetPropertyBlock(null)` for normal enemies | M1 | Survey R3 |
| 11 | Comprehensive E2E / Unit Test Suite | Automated NUnit test suite in `Assets/Main folder/Editor/Tests/CombatOptimizationTests.cs` (45 tests) | M2 | Requirements |
| 12 | Performance Profiling & Adversarial Audit | 0 GC allocations verified, 0 compiler errors, passing test suite, and CLEAN forensic audit | M3 | Acceptance Criteria |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Combat Loop Core Optimization | Features 1–10: `EnemyRegistry.cs`, `EnemyStats.cs` lifecycle/Walker/MPB, `TowerController.cs` zero-alloc targeting, `MaddeningGazeSkill.cs`, `ShadeMinionSkill.cs`, `PointDefenseSkill.cs` | none | DONE |
| 2 | Automated Test Suite Creation | Feature 11: 45 automated tests in `Assets/Main folder/Editor/Tests/CombatOptimizationTests.cs` covering targeting, skills, and material immutability | M1 | DONE |
| 3 | Final Verification & Hardening | Feature 12: Solution compilation, empirical stress tests, forensic audit | M2 | DONE |

## Interface Contracts
### `EnemyRegistry` ↔ `TowerController`
- `public static List<EnemyStats> ActiveEnemies { get; }`: Contiguous read-only view of currently active enemies.
- `public static void Register(EnemyStats enemy)`: Idempotent addition.
- `public static void Unregister(EnemyStats enemy)`: Fast removal upon death, breach, or disable.

### `EnemyStats` ↔ `TowerController`
- `public EnemyWalker Walker { get; }`: Cached reference to walker component, avoiding repeated `GetComponent<EnemyWalker>()`.
- `public float GetCurrentHealth()`: Preserved for HP filtering.
- `public EnemyRank rank`: Preserved for threat level calculations.

### `EnemyStats` ↔ Rendering Subsystem
- `private static MaterialPropertyBlock sharedPropertyBlock`: Reusable block for all enemies.
- Property IDs: `_BaseColor` (URP Lit & Shader Graph) and `_Color` (Built-in / Legacy fallback).
- Reset: `r.SetPropertyBlock(null)` on normal rank enemies to restore SRP batching; `ApplyVisualColor(Color.red)` on elite enemies.

## Code Layout
- `Assets/Main folder/Scripts/Enemies/EnemyRegistry.cs`: Static active enemy registry.
- `Assets/Main folder/Scripts/Enemies/EnemyStats.cs`: Registry hooks, walker caching, and MaterialPropertyBlock.
- `Assets/Main folder/Scripts/Towers/TowerController.cs`: Zero-alloc candidate buffers and imperative filtering.
- `Assets/Main folder/Scripts/Enemies/Skills/MaddeningGazeSkill.cs`: Throttled interval, NonAlloc querying, and `OnDisable` cleanup.
- `Assets/Main folder/Scripts/Enemies/Skills/ShadeMinionSkill.cs`: Throttled interval, cached base coordinate, and `OnDisable` cleanup.
- `Assets/Main folder/Scripts/Enemies/Skills/PointDefenseSkill.cs`: NonAlloc blast querying and LayerMask scoping.
- `Assets/Main folder/Editor/Tests/CombatOptimizationTests.cs`: 45-test automated NUnit test suite.
