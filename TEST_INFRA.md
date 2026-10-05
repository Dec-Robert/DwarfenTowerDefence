# E2E Test Infra: DwarfenTowerDefence Combat Optimization

## Test Philosophy
- Opaque-box and requirement-driven testing derived from `ORIGINAL_REQUEST.md`.
- Category-Partition + Boundary Value Analysis + Pairwise Combinatorial Testing.
- Validates functional preservation, zero heap allocations, zero material leaks, and robust error handling.

## Feature Inventory
| # | Feature | Source | Tier 1 | Tier 2 | Tier 3 |
|---|---------|--------|:------:|:------:|:------:|
| 1 | `EnemyRegistry` tracking | ORIGINAL_REQUEST §R1 | 5 | 5 | ✓ |
| 2 | Tower Targeting Modes | ORIGINAL_REQUEST §R1 | 8 | 8 | ✓ |
| 3 | Enemy Skills Physics & Intervals | ORIGINAL_REQUEST §R2 | 5 | 5 | ✓ |
| 4 | MaterialPropertyBlock & Visuals | ORIGINAL_REQUEST §R3 | 5 | 5 | ✓ |

## Test Architecture
- Test Suite Path: `Assets/Main folder/Editor/Tests/CombatOptimizationTests.cs`
- Test Runner: NUnit / Unity Test Framework (EditMode & PlayMode), verified via `dotnet build Assembly-CSharp-Editor.csproj` and `dotnet build DwarfenTowerDefence.sln`.
- Pass/Fail Semantics: All test methods assert expected states without exceptions; exit code 0 on build.

## Real-World Application Scenarios (Tier 4)
| # | Scenario | Features Exercised | Complexity |
|---|----------|--------------------|------------|
| 1 | Continuous Wave Targeting | F1, F2: Multiple enemy waves spawning, splitting, dying while 10+ towers target simultaneously | High |
| 2 | Boss Encounter with Maddening Gaze | F1, F2, F3: Boss with Fear aura approaches towers, affects tower stats, dies and cleans debuffs | High |
| 3 | Heavy Projectile Barrage with Area Defence | F3: Multiple linear/simple projectiles intercepted and destroyed by Point Defense skill | High |
| 4 | High-Frequency Hit Flash & Elite Spawns | F4: Multiple enemies hit rapidly, flashing white/yellow, reverting to shared materials without leaks | High |
