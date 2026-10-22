using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

class Program
{
    private static int _passedTests = 0;
    private static int _failedTests = 0;
    private static readonly List<string> _failures = new List<string>();

    static void Assert(bool condition, string testName, string details = "")
    {
        if (condition)
        {
            _passedTests++;
            Console.WriteLine($"[PASS] {testName}");
        }
        else
        {
            _failedTests++;
            string msg = $"[FAIL] {testName} - {details}";
            _failures.Add(msg);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            Console.ResetColor();
        }
    }

    static int Main()
    {
        Console.WriteLine("===================================================================");
        Console.WriteLine("CHALLENGER M1-2: EMPIRICAL DATA MODELS & BACKWARD COMPATIBILITY");
        Console.WriteLine("===================================================================\n");

        // 1. ChunkPathData tests
        TestChunkPathDataFieldsAndInitialization();
        TestChunkPathDataNextChunksTowardsBase();
        TestChunkPathDataNextChunkTowardsBaseGetterSetter();
        TestChunkPathDataAddNextChunkTowardsBase();
        TestChunkPathDataIsCrossroadsLogic();
        TestChunkPathDataGetAllEntriesAndExits();
        TestChunkPathDataGetAllEndpoints();
        TestChunkPathDataAddExtraEntryAndExit();
        TestChunkPathDataNullSafetyAndStress();

        // 2. ChunkStateData tests
        TestChunkStateDataFieldsAndInitialization();
        TestChunkStateDataRoadPredecessorGetterSetter();
        TestChunkStateDataHasRoadPredecessor();
        TestChunkStateDataAddRoadPredecessor();
        TestChunkReconIntelIsCrossroads();
        TestChunkStateDataNullSafetyAndStress();

        // 3. MapGenerationContext & LoopDefinition tests
        TestLoopDefinitionModel();
        TestMapGenerationContextLoopsAndCrossroadsCollections();
        TestMapGenerationContextResetPreservation();

        // 4. Backward Compatibility & Cross-System Simulation
        TestBackwardCompatibilityCallPatterns();

        Console.WriteLine("\n===================================================================");
        Console.WriteLine($"TOTAL TESTS: {_passedTests + _failedTests} | PASSED: {_passedTests} | FAILED: {_failedTests}");
        Console.WriteLine("===================================================================");

        if (_failures.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\nSUMMARY OF FAILURES:");
            foreach (var f in _failures)
            {
                Console.WriteLine("  * " + f);
            }
            Console.ResetColor();
            return 1;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\nALL VERIFICATIONS PASSED SUCCESSFULLY!");
        Console.ResetColor();
        return 0;
    }

    // =========================================================================
    // 1. CHUNKPATHDATA TESTS
    // =========================================================================

    static void TestChunkPathDataFieldsAndInitialization()
    {
        var data = new ChunkPathData();
        Assert(data.extraEntries != null, "ChunkPathData: extraEntries is initialized non-null");
        Assert(data.extraEntries.Count == 0, "ChunkPathData: extraEntries starts empty");
        Assert(data.extraExits != null, "ChunkPathData: extraExits is initialized non-null");
        Assert(data.extraExits.Count == 0, "ChunkPathData: extraExits starts empty");
        Assert(data.nextChunksTowardsBase != null, "ChunkPathData: nextChunksTowardsBase is initialized non-null");
        Assert(data.nextChunksTowardsBase.Count == 0, "ChunkPathData: nextChunksTowardsBase starts empty");
        Assert(data.entryHex == default, "ChunkPathData: default entryHex is (0, 0)");
        Assert(data.exitHex == default, "ChunkPathData: default exitHex is (0, 0)");
    }

    static void TestChunkPathDataNextChunksTowardsBase()
    {
        var data = new ChunkPathData();
        data.nextChunksTowardsBase.Add(new Vector2Int(1, 2));
        data.nextChunksTowardsBase.Add(new Vector2Int(3, 4));

        Assert(data.nextChunksTowardsBase.Count == 2, "ChunkPathData: nextChunksTowardsBase can store multiple predecessors");
        Assert(data.nextChunksTowardsBase[0] == new Vector2Int(1, 2) && data.nextChunksTowardsBase[1] == new Vector2Int(3, 4),
            "ChunkPathData: nextChunksTowardsBase maintains insertion order");
    }

    static void TestChunkPathDataNextChunkTowardsBaseGetterSetter()
    {
        var data = new ChunkPathData();

        // 1. Getter on empty list -> sentinel (-999, -999)
        Assert(data.nextChunkTowardsBase == new Vector2Int(-999, -999),
            "ChunkPathData: nextChunkTowardsBase getter on empty list returns sentinel (-999, -999)");

        // 2. Setter with normal value -> list has count 1 with that value
        data.nextChunkTowardsBase = new Vector2Int(5, 6);
        Assert(data.nextChunksTowardsBase.Count == 1,
            "ChunkPathData: nextChunkTowardsBase setter populates list with exactly 1 element");
        Assert(data.nextChunksTowardsBase[0] == new Vector2Int(5, 6),
            "ChunkPathData: nextChunksTowardsBase[0] matches assigned value");
        Assert(data.nextChunkTowardsBase == new Vector2Int(5, 6),
            "ChunkPathData: nextChunkTowardsBase getter returns newly assigned value");

        // 3. Setter overwriting existing multiple values
        data.nextChunksTowardsBase.Add(new Vector2Int(7, 8));
        Assert(data.nextChunksTowardsBase.Count == 2, "ChunkPathData: manual addition of second predecessor");
        Assert(data.nextChunkTowardsBase == new Vector2Int(5, 6),
            "ChunkPathData: nextChunkTowardsBase getter returns first element even when multiple exist");

        data.nextChunkTowardsBase = new Vector2Int(10, 20);
        Assert(data.nextChunksTowardsBase.Count == 1 && data.nextChunksTowardsBase[0] == new Vector2Int(10, 20),
            "ChunkPathData: nextChunkTowardsBase setter clears previous list and sets single element");

        // 4. Setter with sentinel (-999, -999) -> clears list without adding sentinel
        data.nextChunkTowardsBase = new Vector2Int(-999, -999);
        Assert(data.nextChunksTowardsBase.Count == 0,
            "ChunkPathData: Setting sentinel (-999, -999) clears nextChunksTowardsBase without storing sentinel");
        Assert(data.nextChunkTowardsBase == new Vector2Int(-999, -999),
            "ChunkPathData: Getter after setting sentinel returns sentinel (-999, -999)");
    }

    static void TestChunkPathDataAddNextChunkTowardsBase()
    {
        var data = new ChunkPathData();

        // 1. Add normal chunk
        data.AddNextChunkTowardsBase(new Vector2Int(1, 1));
        Assert(data.nextChunksTowardsBase.Count == 1 && data.nextChunksTowardsBase[0] == new Vector2Int(1, 1),
            "ChunkPathData.AddNextChunkTowardsBase: adds chunk correctly");

        // 2. Duplicate prevention
        data.AddNextChunkTowardsBase(new Vector2Int(1, 1));
        Assert(data.nextChunksTowardsBase.Count == 1,
            "ChunkPathData.AddNextChunkTowardsBase: prevents duplicate coordinates");

        // 3. Sentinel ignored
        data.AddNextChunkTowardsBase(new Vector2Int(-999, -999));
        Assert(data.nextChunksTowardsBase.Count == 1,
            "ChunkPathData.AddNextChunkTowardsBase: ignores sentinel (-999, -999)");

        // 4. Add second distinct chunk
        data.AddNextChunkTowardsBase(new Vector2Int(2, 2));
        Assert(data.nextChunksTowardsBase.Count == 2 && data.nextChunksTowardsBase[1] == new Vector2Int(2, 2),
            "ChunkPathData.AddNextChunkTowardsBase: successfully adds distinct second predecessor");
    }

    static void TestChunkPathDataIsCrossroadsLogic()
    {
        var data = new ChunkPathData();

        // Initially no extra entries and no extra exits -> false
        Assert(data.isCrossroads == false,
            "ChunkPathData.isCrossroads: false when extraEntries and extraExits are empty");

        // Add 1 extra exit -> true
        data.extraExits.Add(new Vector2Int(1, 0));
        Assert(data.isCrossroads == true,
            "ChunkPathData.isCrossroads: true when extraExits > 0 and extraEntries == 0");

        // Clear extra exits, add 1 extra entry -> true
        data.extraExits.Clear();
        data.extraEntries.Add(new Vector2Int(0, 1));
        Assert(data.isCrossroads == true,
            "ChunkPathData.isCrossroads: true when extraEntries > 0 and extraExits == 0");

        // Both > 0 -> true
        data.extraExits.Add(new Vector2Int(2, 0));
        Assert(data.isCrossroads == true,
            "ChunkPathData.isCrossroads: true when both extraEntries > 0 and extraExits > 0");

        // Clear both -> false
        data.extraEntries.Clear();
        data.extraExits.Clear();
        Assert(data.isCrossroads == false,
            "ChunkPathData.isCrossroads: false after clearing both lists");
    }

    static void TestChunkPathDataGetAllEntriesAndExits()
    {
        var data = new ChunkPathData();
        data.entryHex = new Vector2Int(1, 2);
        data.exitHex = new Vector2Int(8, 9);

        // 1. Without extra entries/exits
        var entriesOnlyPrimary = data.GetAllEntries();
        var exitsOnlyPrimary = data.GetAllExits();

        Assert(entriesOnlyPrimary.Count == 1 && entriesOnlyPrimary[0] == new Vector2Int(1, 2),
            "ChunkPathData.GetAllEntries: returns single primary entryHex when extraEntries is empty");
        Assert(exitsOnlyPrimary.Count == 1 && exitsOnlyPrimary[0] == new Vector2Int(8, 9),
            "ChunkPathData.GetAllExits: returns single primary exitHex when extraExits is empty");

        // 2. Add extra entries
        data.extraEntries.Add(new Vector2Int(3, 4));
        data.extraEntries.Add(new Vector2Int(5, 6));

        var allEntries = data.GetAllEntries();
        Assert(allEntries.Count == 3, "ChunkPathData.GetAllEntries: count is 3 (1 primary + 2 extra)");
        Assert(allEntries[0] == new Vector2Int(1, 2) && allEntries[1] == new Vector2Int(3, 4) && allEntries[2] == new Vector2Int(5, 6),
            "ChunkPathData.GetAllEntries: correctly combines primary entryHex followed by extraEntries");

        // 3. Add extra exits
        data.extraExits.Add(new Vector2Int(10, 11));

        var allExits = data.GetAllExits();
        Assert(allExits.Count == 2, "ChunkPathData.GetAllExits: count is 2 (1 primary + 1 extra)");
        Assert(allExits[0] == new Vector2Int(8, 9) && allExits[1] == new Vector2Int(10, 11),
            "ChunkPathData.GetAllExits: correctly combines primary exitHex followed by extraExits");

        // 4. Immutability of source: modifying returned list should not affect internal state
        allEntries.Add(new Vector2Int(99, 99));
        Assert(data.extraEntries.Count == 2,
            "ChunkPathData.GetAllEntries: returns a new list instance, preventing external modification");

        allExits.Add(new Vector2Int(99, 99));
        Assert(data.extraExits.Count == 1,
            "ChunkPathData.GetAllExits: returns a new list instance, preventing external modification");
    }

    static void TestChunkPathDataGetAllEndpoints()
    {
        var data = new ChunkPathData();
        data.entryHex = new Vector2Int(1, 0);
        data.exitHex = new Vector2Int(5, 0);
        data.AddExtraEntry(new Vector2Int(0, 1));
        data.AddExtraExit(new Vector2Int(4, 1));

        var endpoints = data.GetAllEndpoints();
        Assert(endpoints.Count == 4,
            "ChunkPathData.GetAllEndpoints: returns all 4 endpoints (2 entries + 2 exits)");
        Assert(endpoints.Contains(new Vector2Int(1, 0)) &&
               endpoints.Contains(new Vector2Int(0, 1)) &&
               endpoints.Contains(new Vector2Int(5, 0)) &&
               endpoints.Contains(new Vector2Int(4, 1)),
            "ChunkPathData.GetAllEndpoints: contains every entry and exit point");
    }

    static void TestChunkPathDataAddExtraEntryAndExit()
    {
        var data = new ChunkPathData();

        // Test AddExtraEntry
        data.AddExtraEntry(new Vector2Int(2, 3));
        Assert(data.extraEntries.Count == 1 && data.extraEntries[0] == new Vector2Int(2, 3),
            "ChunkPathData.AddExtraEntry: adds new coordinate");
        data.AddExtraEntry(new Vector2Int(2, 3));
        Assert(data.extraEntries.Count == 1,
            "ChunkPathData.AddExtraEntry: prevents duplicate coordinate");

        // Test AddExtraExit
        data.AddExtraExit(new Vector2Int(7, 8));
        Assert(data.extraExits.Count == 1 && data.extraExits[0] == new Vector2Int(7, 8),
            "ChunkPathData.AddExtraExit: adds new coordinate");
        data.AddExtraExit(new Vector2Int(7, 8));
        Assert(data.extraExits.Count == 1,
            "ChunkPathData.AddExtraExit: prevents duplicate coordinate");
    }

    static void TestChunkPathDataNullSafetyAndStress()
    {
        var data = new ChunkPathData();

        // Simulate edge case: serialized fields set to null
        data.extraEntries = null!;
        data.extraExits = null!;
        data.nextChunksTowardsBase = null!;

        // 1. isCrossroads null-safety
        bool crossSafe = false;
        try
        {
            crossSafe = (data.isCrossroads == false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception on null isCrossroads: {ex}");
        }
        Assert(crossSafe, "ChunkPathData.isCrossroads: null-safe when lists are null");

        // 2. GetAllEntries null-safety
        List<Vector2Int>? entries = null;
        try
        {
            entries = data.GetAllEntries();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception on null GetAllEntries: {ex}");
        }
        Assert(entries != null && entries.Count == 1 && entries[0] == data.entryHex,
            "ChunkPathData.GetAllEntries: null-safe when extraEntries is null (returns primary entryHex)");

        // 3. GetAllExits null-safety
        List<Vector2Int>? exits = null;
        try
        {
            exits = data.GetAllExits();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception on null GetAllExits: {ex}");
        }
        Assert(exits != null && exits.Count == 1 && exits[0] == data.exitHex,
            "ChunkPathData.GetAllExits: null-safe when extraExits is null (returns primary exitHex)");

        // 4. nextChunkTowardsBase getter null-safety
        Vector2Int gotVal = default;
        try
        {
            gotVal = data.nextChunkTowardsBase;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception on null nextChunkTowardsBase getter: {ex}");
        }
        Assert(gotVal == new Vector2Int(-999, -999),
            "ChunkPathData.nextChunkTowardsBase: null-safe getter returns sentinel (-999, -999) when list is null");

        // 5. nextChunkTowardsBase setter null-recovery
        try
        {
            data.nextChunkTowardsBase = new Vector2Int(12, 34);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception on null nextChunkTowardsBase setter: {ex}");
        }
        Assert(data.nextChunksTowardsBase != null && data.nextChunksTowardsBase.Count == 1 && data.nextChunksTowardsBase[0] == new Vector2Int(12, 34),
            "ChunkPathData.nextChunkTowardsBase: setter automatically allocates list if null and stores value");

        // 6. Add methods null-recovery
        data.extraEntries = null!;
        data.AddExtraEntry(new Vector2Int(1, 2));
        Assert(data.extraEntries != null && data.extraEntries.Count == 1 && data.extraEntries[0] == new Vector2Int(1, 2),
            "ChunkPathData.AddExtraEntry: automatically allocates list if null");

        data.extraExits = null!;
        data.AddExtraExit(new Vector2Int(3, 4));
        Assert(data.extraExits != null && data.extraExits.Count == 1 && data.extraExits[0] == new Vector2Int(3, 4),
            "ChunkPathData.AddExtraExit: automatically allocates list if null");

        data.nextChunksTowardsBase = null!;
        data.AddNextChunkTowardsBase(new Vector2Int(5, 6));
        Assert(data.nextChunksTowardsBase != null && data.nextChunksTowardsBase.Count == 1 && data.nextChunksTowardsBase[0] == new Vector2Int(5, 6),
            "ChunkPathData.AddNextChunkTowardsBase: automatically allocates list if null");
    }

    // =========================================================================
    // 2. CHUNKSTATEDATA TESTS
    // =========================================================================

    static void TestChunkStateDataFieldsAndInitialization()
    {
        var state = new ChunkStateData();
        Assert(state.roadPredecessors != null, "ChunkStateData: roadPredecessors is initialized non-null");
        Assert(state.roadPredecessors.Count == 0, "ChunkStateData: roadPredecessors starts empty");
        Assert(state.reconIntel != null, "ChunkStateData: reconIntel is initialized non-null");
        Assert(state.hasRoadPredecessor == false, "ChunkStateData: hasRoadPredecessor is false initially");
        Assert(state.roadPredecessor == new Vector2Int(-999, -999), "ChunkStateData: roadPredecessor returns sentinel initially");
    }

    static void TestChunkStateDataRoadPredecessorGetterSetter()
    {
        var state = new ChunkStateData();

        // 1. Getter returns sentinel
        Assert(state.roadPredecessor == new Vector2Int(-999, -999),
            "ChunkStateData: default roadPredecessor returns sentinel (-999, -999)");

        // 2. Setter assigns value
        state.roadPredecessor = new Vector2Int(3, 3);
        Assert(state.roadPredecessors.Count == 1 && state.roadPredecessors[0] == new Vector2Int(3, 3),
            "ChunkStateData: setting roadPredecessor populates roadPredecessors[0]");
        Assert(state.roadPredecessor == new Vector2Int(3, 3),
            "ChunkStateData: getting roadPredecessor returns assigned value");

        // 3. Multi-predecessor scenario (M2 loop merge)
        state.roadPredecessors.Add(new Vector2Int(4, 4));
        Assert(state.roadPredecessors.Count == 2, "ChunkStateData: roadPredecessors stores multiple predecessors");
        Assert(state.roadPredecessor == new Vector2Int(3, 3),
            "ChunkStateData: getter returns primary predecessor (first element) when multiple exist");

        // 4. Overwrite via setter clears previous entries
        state.roadPredecessor = new Vector2Int(7, 7);
        Assert(state.roadPredecessors.Count == 1 && state.roadPredecessors[0] == new Vector2Int(7, 7),
            "ChunkStateData: setting roadPredecessor clears previous list and sets single element");

        // 5. Setting sentinel clears list
        state.roadPredecessor = new Vector2Int(-999, -999);
        Assert(state.roadPredecessors.Count == 0,
            "ChunkStateData: setting sentinel (-999, -999) clears roadPredecessors list without adding sentinel");
        Assert(state.roadPredecessor == new Vector2Int(-999, -999),
            "ChunkStateData: getter returns sentinel after clearing");
    }

    static void TestChunkStateDataHasRoadPredecessor()
    {
        var state = new ChunkStateData();

        Assert(state.hasRoadPredecessor == false,
            "ChunkStateData.hasRoadPredecessor: false when list is empty");

        state.roadPredecessor = new Vector2Int(1, 2);
        Assert(state.hasRoadPredecessor == true,
            "ChunkStateData.hasRoadPredecessor: true when predecessor assigned");

        // Test backward compatibility setter: data.hasRoadPredecessor = true / false should be a no-op
        state.hasRoadPredecessor = false; // should not mutate underlying list or break count
        Assert(state.hasRoadPredecessor == true,
            "ChunkStateData.hasRoadPredecessor: setter is safe no-op, dynamic getter reflects roadPredecessors.Count > 0");

        state.roadPredecessors.Clear();
        Assert(state.hasRoadPredecessor == false,
            "ChunkStateData.hasRoadPredecessor: false when cleared");

        state.hasRoadPredecessor = true;
        Assert(state.hasRoadPredecessor == false,
            "ChunkStateData.hasRoadPredecessor: setting true when list is empty remains false without crash");
    }

    static void TestChunkStateDataAddRoadPredecessor()
    {
        var state = new ChunkStateData();

        // 1. Add predecessor
        state.AddRoadPredecessor(new Vector2Int(2, 2));
        Assert(state.roadPredecessors.Count == 1 && state.roadPredecessors[0] == new Vector2Int(2, 2),
            "ChunkStateData.AddRoadPredecessor: adds predecessor");

        // 2. Prevent duplicate
        state.AddRoadPredecessor(new Vector2Int(2, 2));
        Assert(state.roadPredecessors.Count == 1,
            "ChunkStateData.AddRoadPredecessor: prevents duplicate coordinates");

        // 3. Sentinel ignored
        state.AddRoadPredecessor(new Vector2Int(-999, -999));
        Assert(state.roadPredecessors.Count == 1,
            "ChunkStateData.AddRoadPredecessor: ignores sentinel (-999, -999)");

        // 4. Add second predecessor
        state.AddRoadPredecessor(new Vector2Int(3, 3));
        Assert(state.roadPredecessors.Count == 2 && state.roadPredecessors[1] == new Vector2Int(3, 3),
            "ChunkStateData.AddRoadPredecessor: adds distinct second predecessor");
    }

    static void TestChunkReconIntelIsCrossroads()
    {
        // 1. Verify existence of ChunkReconIntel.isCrossroads field via Reflection
        FieldInfo? field = typeof(ChunkReconIntel).GetField("isCrossroads", BindingFlags.Public | BindingFlags.Instance);
        Assert(field != null, "ChunkReconIntel: 'isCrossroads' field exists on class");
        Assert(field != null && field.FieldType == typeof(bool), "ChunkReconIntel: 'isCrossroads' field is of type bool");

        // 2. Test instantiation and default
        var intel = new ChunkReconIntel();
        Assert(intel.isCrossroads == false, "ChunkReconIntel: isCrossroads defaults to false");

        // 3. Test assignment
        intel.isCrossroads = true;
        Assert(intel.isCrossroads == true, "ChunkReconIntel: isCrossroads can be set to true");

        // 4. Test via ChunkStateData
        var state = new ChunkStateData();
        Assert(state.reconIntel.isCrossroads == false, "ChunkStateData: reconIntel.isCrossroads defaults to false");
        state.reconIntel.isCrossroads = true;
        Assert(state.reconIntel.isCrossroads == true, "ChunkStateData: reconIntel.isCrossroads can be mutated");
    }

    static void TestChunkStateDataNullSafetyAndStress()
    {
        var state = new ChunkStateData();

        // Simulate edge case: serialized fields set to null
        state.roadPredecessors = null!;

        // 1. hasRoadPredecessor null-safety
        bool hasPred = true;
        try
        {
            hasPred = state.hasRoadPredecessor;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception on null hasRoadPredecessor: {ex}");
        }
        Assert(hasPred == false, "ChunkStateData.hasRoadPredecessor: null-safe returns false when list is null");

        // 2. roadPredecessor getter null-safety
        Vector2Int pred = default;
        try
        {
            pred = state.roadPredecessor;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception on null roadPredecessor getter: {ex}");
        }
        Assert(pred == new Vector2Int(-999, -999),
            "ChunkStateData.roadPredecessor: null-safe getter returns sentinel (-999, -999) when list is null");

        // 3. roadPredecessor setter null-recovery
        try
        {
            state.roadPredecessor = new Vector2Int(8, 8);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception on null roadPredecessor setter: {ex}");
        }
        Assert(state.roadPredecessors != null && state.roadPredecessors.Count == 1 && state.roadPredecessors[0] == new Vector2Int(8, 8),
            "ChunkStateData.roadPredecessor: setter automatically allocates list if null and assigns coordinate");

        // 4. AddRoadPredecessor null-recovery
        state.roadPredecessors = null!;
        state.AddRoadPredecessor(new Vector2Int(9, 9));
        Assert(state.roadPredecessors != null && state.roadPredecessors.Count == 1 && state.roadPredecessors[0] == new Vector2Int(9, 9),
            "ChunkStateData.AddRoadPredecessor: automatically allocates list if null and adds coordinate");
    }

    // =========================================================================
    // 3. MAPGENERATIONCONTEXT & LOOPDEFINITION TESTS
    // =========================================================================

    static void TestLoopDefinitionModel()
    {
        // 1. Structure reflection check
        Type loopType = typeof(LoopDefinition);
        Assert(loopType.GetField("splitCrossroads") != null, "LoopDefinition: has splitCrossroads field");
        Assert(loopType.GetField("mergeCrossroads") != null, "LoopDefinition: has mergeCrossroads field");
        Assert(loopType.GetField("mainSegmentChunks") != null, "LoopDefinition: has mainSegmentChunks field");
        Assert(loopType.GetField("loopChunks") != null, "LoopDefinition: has loopChunks field");
        Assert(loopType.GetField("loopFlowRatio") != null, "LoopDefinition: has loopFlowRatio field");

        // 2. Default constructor
        var def1 = new LoopDefinition();
        Assert(def1.mainSegmentChunks != null && def1.mainSegmentChunks.Count == 0,
            "LoopDefinition(): mainSegmentChunks initialized non-null and empty");
        Assert(def1.loopChunks != null && def1.loopChunks.Count == 0,
            "LoopDefinition(): loopChunks initialized non-null and empty");
        Assert(Math.Abs(def1.loopFlowRatio - 0.3f) < 0.001f,
            "LoopDefinition(): loopFlowRatio defaults to 0.30f (30% flow)");

        // 3. Parameterized constructor
        var split = new Vector2Int(4, 5);
        var merge = new Vector2Int(2, 2);
        var def2 = new LoopDefinition(split, merge, 0.40f);

        Assert(def2.splitCrossroads == split, "LoopDefinition(split, merge, ratio): splitCrossroads set correctly");
        Assert(def2.mergeCrossroads == merge, "LoopDefinition(split, merge, ratio): mergeCrossroads set correctly");
        Assert(Math.Abs(def2.loopFlowRatio - 0.40f) < 0.001f, "LoopDefinition(split, merge, ratio): loopFlowRatio set correctly");
        Assert(def2.mainSegmentChunks != null && def2.loopChunks != null,
            "LoopDefinition(split, merge, ratio): segment lists initialized non-null");

        // 4. Segment chunk population
        def2.mainSegmentChunks.Add(new Vector2Int(3, 3));
        def2.loopChunks.Add(new Vector2Int(4, 3));
        def2.loopChunks.Add(new Vector2Int(3, 2));

        Assert(def2.mainSegmentChunks.Count == 1 && def2.loopChunks.Count == 2,
            "LoopDefinition: correctly holds bypassed and bypass loop chunk coordinates");
    }

    static void TestMapGenerationContextLoopsAndCrossroadsCollections()
    {
        var ctx = new MapGenerationContext(12345);

        // 1. Check loops collection
        Assert(ctx.loops != null, "MapGenerationContext: loops collection is initialized non-null");
        Assert(ctx.loops.Count == 0, "MapGenerationContext: loops starts empty");

        // 2. Check crossroadsChunks collection
        Assert(ctx.crossroadsChunks != null, "MapGenerationContext: crossroadsChunks collection is initialized non-null");
        Assert(ctx.crossroadsChunks.Count == 0, "MapGenerationContext: crossroadsChunks starts empty");

        // 3. Add LoopDefinition to ctx.loops
        var loop = new LoopDefinition(new Vector2Int(5, 5), new Vector2Int(2, 2), 0.3f);
        ctx.loops.Add(loop);
        Assert(ctx.loops.Count == 1 && ctx.loops[0] == loop,
            "MapGenerationContext: successfully stores LoopDefinition items");

        // 4. Add coordinates to ctx.crossroadsChunks
        ctx.crossroadsChunks.Add(new Vector2Int(5, 5));
        ctx.crossroadsChunks.Add(new Vector2Int(2, 2));
        Assert(ctx.crossroadsChunks.Count == 2,
            "MapGenerationContext: crossroadsChunks stores crossroads coordinates");
        Assert(ctx.crossroadsChunks.Contains(new Vector2Int(5, 5)),
            "MapGenerationContext: crossroadsChunks supports fast O(1) membership lookup");
        Assert(ctx.crossroadsChunks.Contains(new Vector2Int(99, 99)) == false,
            "MapGenerationContext: crossroadsChunks returns false for non-crossroads coordinate");
    }

    static void TestMapGenerationContextResetPreservation()
    {
        var ctx = new MapGenerationContext(42);

        // Populate collections
        ctx.loops.Add(new LoopDefinition(new Vector2Int(3, 3), new Vector2Int(1, 1)));
        ctx.crossroadsChunks.Add(new Vector2Int(3, 3));
        ctx.crossroadsChunks.Add(new Vector2Int(1, 1));
        ctx.chunkPaths[new Vector2Int(1, 1)] = new ChunkPathData();

        Assert(ctx.loops.Count == 1, "Context setup: loops populated");
        Assert(ctx.crossroadsChunks.Count == 2, "Context setup: crossroadsChunks populated");

        // Perform Reset
        ctx.Reset(777);

        Assert(ctx.loops != null && ctx.loops.Count == 0,
            "MapGenerationContext.Reset: loops collection cleared cleanly");
        Assert(ctx.crossroadsChunks != null && ctx.crossroadsChunks.Count == 0,
            "MapGenerationContext.Reset: crossroadsChunks collection cleared cleanly");
        Assert(ctx.chunkPaths.Count == 0,
            "MapGenerationContext.Reset: chunkPaths collection cleared cleanly");
        Assert(ctx.seed == 777 && ctx.Rng != null && ctx.Rng.Seed == 777,
            "MapGenerationContext.Reset: seed and Rng re-initialized with new seed");
    }

    // =========================================================================
    // 4. BACKWARD COMPATIBILITY & CROSS-SYSTEM SIMULATION
    // =========================================================================

    static void TestBackwardCompatibilityCallPatterns()
    {
        // Simulate exact call patterns found in PathGeneration.cs and MapExpansionManager.cs
        // 1. PathGeneration.cs line 89:
        //    data.nextChunkTowardsBase = ...;
        // 2. MapExpansionManager.cs line 169:
        //    data.roadPredecessor = ...;
        // 3. MapExpansionManager.cs line 170:
        //    data.hasRoadPredecessor = true;
        // 4. Recon scan:
        //    intel.isCrossroads = pathData.isCrossroads;

        var pathData = new ChunkPathData();
        var stateData = new ChunkStateData();

        // Step A: Legacy path generation assigns nextChunkTowardsBase
        Vector2Int baseCoord = new Vector2Int(0, 0);
        pathData.nextChunkTowardsBase = baseCoord;
        Assert(pathData.nextChunkTowardsBase == baseCoord, "Legacy assignment: pathData.nextChunkTowardsBase");
        Assert(pathData.nextChunksTowardsBase.Count == 1 && pathData.nextChunksTowardsBase[0] == baseCoord,
            "Underlying multi-predecessor reflects single predecessor assignment");

        // Step B: Legacy expansion assigns roadPredecessor and hasRoadPredecessor
        stateData.roadPredecessor = baseCoord;
        stateData.hasRoadPredecessor = true;
        Assert(stateData.roadPredecessor == baseCoord, "Legacy assignment: stateData.roadPredecessor");
        Assert(stateData.hasRoadPredecessor == true, "Legacy check: stateData.hasRoadPredecessor");
        Assert(stateData.roadPredecessors.Count == 1 && stateData.roadPredecessors[0] == baseCoord,
            "Underlying multi-predecessors list matches legacy assignment");

        // Step C: M2 Loop expansion branch adds second predecessor
        Vector2Int loopAltCoord = new Vector2Int(1, -1);
        pathData.AddNextChunkTowardsBase(loopAltCoord);
        stateData.AddRoadPredecessor(loopAltCoord);

        Assert(pathData.nextChunksTowardsBase.Count == 2, "PathData has 2 predecessors for loop");
        Assert(stateData.roadPredecessors.Count == 2, "StateData has 2 road predecessors for loop");

        // Legacy code reading property still receives primary predecessor without throwing or breaking
        Assert(pathData.nextChunkTowardsBase == baseCoord, "Legacy code reading nextChunkTowardsBase receives primary");
        Assert(stateData.roadPredecessor == baseCoord, "Legacy code reading roadPredecessor receives primary");

        // Step D: Recon intel receives crossroads flag
        pathData.AddExtraExit(new Vector2Int(2, 2));
        Assert(pathData.isCrossroads == true, "PathData marked as crossroads");

        stateData.reconIntel.isCrossroads = pathData.isCrossroads;
        Assert(stateData.reconIntel.isCrossroads == true, "ReconIntel correctly captures isCrossroads flag");
    }
}
