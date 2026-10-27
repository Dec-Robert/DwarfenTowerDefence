using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

public static class ChallengerMeta3_3Tests
{
    private static int _passed = 0;
    private static int _failed = 0;

    private static void Assert(bool condition, string testName, string details = "")
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"[CHALLENGER-M3-PASS] {testName}");
        }
        else
        {
            _failed++;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[CHALLENGER-M3-FAIL] {testName} - {details}");
            Console.ResetColor();
        }
    }

    private static T CreateInstanceWithoutNative<T>() where T : class
    {
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }

    private static TowerData CreateDummyTower(string assetName, string buildingName, float range, float damage, float fireRate, float armorPen, float magicPen)
    {
        TowerData t = CreateInstanceWithoutNative<TowerData>();
        try { t.name = assetName; } catch { }
        t.buildingName = buildingName;
        t.description = $"Lore for {buildingName}";
        t.baseRange = range;
        t.baseDamage = damage;
        t.fireRate = fireRate;
        t.armorPenetration = armorPen;
        t.magicPenetration = magicPen;
        t.isUnlocked = true;
        return t;
    }

    private static SaveManager CreateSaveManager(string savePath)
    {
        SaveManager mgr = CreateInstanceWithoutNative<SaveManager>();
        mgr.currentSaveData = new SaveData();
        mgr.allTowers = new List<TowerData>();
        mgr.SavePath = savePath;
        return mgr;
    }

    private static MetaUpgradeManager CreateMetaUpgradeManager()
    {
        MetaUpgradeManager mgr = CreateInstanceWithoutNative<MetaUpgradeManager>();
        mgr.allUpgrades = new List<MetaUpgradeSO>();
        mgr.allTowers = new List<TowerData>();
        return mgr;
    }

    private static MetaUpgradeSO CreateDummyUpgrade(string id, string name, MetaEffectType effect)
    {
        MetaUpgradeSO u = CreateInstanceWithoutNative<MetaUpgradeSO>();
        try { u.name = name; } catch { }
        u.id = id;
        u.upgradeName = name;
        u.effectType = effect;
        return u;
    }

    private class NullLogHandler : ILogHandler
    {
        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args) { }
        public void LogException(Exception exception, UnityEngine.Object context) { }
    }

    public static int RunAll()
    {
        try
        {
            Debug.unityLogger.logHandler = new NullLogHandler();
        }
        catch { }

        Console.WriteLine("\n=================================================");
        Console.WriteLine("CHALLENGER M3: R3 SAVE SYSTEM & TOWER DRAFT SELECTION");
        Console.WriteLine("=================================================\n");

        Test1_SaveData_Initialization();
        Test2_SaveManager_BaselineStarterTowers();
        Test3_SaveManager_SyncTowersWithSave();
        Test4_SaveManager_UnlockTowerAndSerialization();
        Test5_TowerDraft_SequentialFiltering();
        Test6_TowerDraftModalUI_GEMINICompliance();
        Test7_MetaUpgradeManager_DraftHookAndDetection();
        Test8_ExhaustiveAllPermutationsDraftReduction();
        Test9_JSONPersistenceRoundtripAndDiacritics();
        Test10_SaveSystemEdgeCasesAndCorruptionRecovery();

        Console.WriteLine("\n=================================================");
        Console.WriteLine($"CHALLENGER M3 SUMMARY: {_passed} PASSED, {_failed} FAILED");
        Console.WriteLine("=================================================\n");

        return _failed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1: SaveData New Instance
    // ─────────────────────────────────────────────────────────────────────────
    private static void Test1_SaveData_Initialization()
    {
        SaveData data = new SaveData();
        Assert(data.unlockedTowerIDs != null, "SaveData_UnlockedTowerIDsNotNull", "unlockedTowerIDs must be initialized");
        Assert(data.unlockedTowerIDs.Count == 0, "SaveData_UnlockedTowerIDsInitiallyEmpty", $"Count was {data.unlockedTowerIDs.Count}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2: SaveManager Baseline Starter Towers
    // ─────────────────────────────────────────────────────────────────────────
    private static void Test2_SaveManager_BaselineStarterTowers()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"save_test_m3_{Guid.NewGuid():N}.json");
        try
        {
            SaveManager mgr = CreateSaveManager(tempPath);

            if (File.Exists(tempPath)) File.Delete(tempPath);

            mgr.LoadGame();

            Assert(mgr.currentSaveData.unlockedTowerIDs != null, "SaveManager_LoadGame_HasUnlockedTowers", "unlockedTowerIDs must not be null");
            Assert(mgr.currentSaveData.unlockedTowerIDs.Count == 2, "SaveManager_LoadGame_Exactly2StarterTowers", $"Count was {mgr.currentSaveData.unlockedTowerIDs.Count}");
            Assert(mgr.currentSaveData.unlockedTowerIDs.Contains("1. Sentinel Arbalest"), "SaveManager_ContainsSentinelArbalest", "Starter 1 missing");
            Assert(mgr.currentSaveData.unlockedTowerIDs.Contains("6. Royal Bombard"), "SaveManager_ContainsRoyalBombard", "Starter 2 missing");

            Assert(File.Exists(tempPath), "SaveManager_FileWrittenImmediately", "Save file must be written to disk");
            string json = File.ReadAllText(tempPath);
            Assert(json.Contains("1. Sentinel Arbalest") && json.Contains("6. Royal Bombard"), "SaveManager_JSONContainsStarters", "JSON file content missing starters");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3: SaveManager SyncTowersWithSave
    // ─────────────────────────────────────────────────────────────────────────
    private static void Test3_SaveManager_SyncTowersWithSave()
    {
        List<TowerData> towers = new List<TowerData>
        {
            CreateDummyTower("1. Sentinel Arbalest", "Arbalet", 25f, 15f, 1.2f, 20f, 0f),
            CreateDummyTower("2. Thunderbuss", "Garłacz Gromowy", 12f, 40f, 0.8f, 35f, 0f),
            CreateDummyTower("3. Steam Repeater", "Repetier Parowy", 18f, 10f, 3.5f, 10f, 0f),
            CreateDummyTower("4. Crucible of Cinders", "Tygiel Pożogi", 14f, 22f, 1.5f, 15f, 25f),
            CreateDummyTower("5. Shadow Spire", "Iglica Cieni", 22f, 60f, 0.5f, 0f, 50f),
            CreateDummyTower("6. Royal Bombard", "Bombarda", 30f, 85f, 0.4f, 50f, 0f),
            CreateDummyTower("7. Soul Monolith", "Monolit dusz", 20f, 45f, 1.0f, 20f, 40f)
        };

        SaveManager mgr = CreateSaveManager(Path.Combine(Path.GetTempPath(), $"save_sync_{Guid.NewGuid():N}.json"));
        mgr.currentSaveData = new SaveData
        {
            unlockedTowerIDs = new List<string> { "1. Sentinel Arbalest", "Arbalet", "6. Royal Bombard", "Bombarda" }
        };

        foreach (var t in towers) t.isUnlocked = true;

        mgr.SyncTowersWithSave(towers);

        Assert(towers[0].isUnlocked == true, "Sync_Starter1_SentinelArbalest_Unlocked", "Sentinel Arbalest must be unlocked");
        Assert(towers[5].isUnlocked == true, "Sync_Starter2_RoyalBombard_Unlocked", "Royal Bombard must be unlocked");

        Assert(towers[1].isUnlocked == false, "Sync_Draft1_Thunderbuss_Locked", "Thunderbuss must be locked");
        Assert(towers[2].isUnlocked == false, "Sync_Draft2_SteamRepeater_Locked", "Steam Repeater must be locked");
        Assert(towers[3].isUnlocked == false, "Sync_Draft3_Crucible_Locked", "Crucible of Cinders must be locked");
        Assert(towers[4].isUnlocked == false, "Sync_Draft4_ShadowSpire_Locked", "Shadow Spire must be locked");
        Assert(towers[6].isUnlocked == false, "Sync_Draft5_SoulMonolith_Locked", "Soul Monolith must be locked");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 4: SaveManager UnlockTower and Serialization
    // ─────────────────────────────────────────────────────────────────────────
    private static void Test4_SaveManager_UnlockTowerAndSerialization()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"save_unlock_{Guid.NewGuid():N}.json");
        try
        {
            SaveManager mgr = CreateSaveManager(tempPath);
            mgr.LoadGame();

            TowerData thunderbuss = CreateDummyTower("2. Thunderbuss", "Garłacz Gromowy", 12f, 40f, 0.8f, 35f, 0f);
            thunderbuss.isUnlocked = false;
            mgr.allTowers = new List<TowerData> { thunderbuss };

            mgr.UnlockTower("Garłacz Gromowy");

            Assert(mgr.currentSaveData.unlockedTowerIDs.Count == 3, "UnlockTower_CountIncremented", $"Count was {mgr.currentSaveData.unlockedTowerIDs.Count}");
            Assert(mgr.currentSaveData.unlockedTowerIDs.Contains("Garłacz Gromowy"), "UnlockTower_ContainsID", "New ID must be present");
            Assert(thunderbuss.isUnlocked == true, "UnlockTower_TargetInstanceUnlocked", "TowerData instance must be set to unlocked");

            Assert(File.Exists(tempPath), "UnlockTower_FilePersisted", "Save file must exist");
            string json = File.ReadAllText(tempPath);
            Assert(json.Contains("Garłacz Gromowy"), "UnlockTower_JSONContainsID", "Persisted JSON missing new tower");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 5: Tower Draft Sequential Filtering
    // ─────────────────────────────────────────────────────────────────────────
    private static void Test5_TowerDraft_SequentialFiltering()
    {
        List<TowerData> all7Towers = new List<TowerData>
        {
            CreateDummyTower("1. Sentinel Arbalest", "Arbalet", 25f, 15f, 1.2f, 20f, 0f),
            CreateDummyTower("2. Thunderbuss", "Garłacz Gromowy", 12f, 40f, 0.8f, 35f, 0f),
            CreateDummyTower("3. Steam Repeater", "Repetier Parowy", 18f, 10f, 3.5f, 10f, 0f),
            CreateDummyTower("4. Crucible of Cinders", "Tygiel Pożogi", 14f, 22f, 1.5f, 15f, 25f),
            CreateDummyTower("5. Shadow Spire", "Iglica Cieni", 22f, 60f, 0.5f, 0f, 50f),
            CreateDummyTower("6. Royal Bombard", "Bombarda", 30f, 85f, 0.4f, 50f, 0f),
            CreateDummyTower("7. Soul Monolith", "Monolit dusz", 20f, 45f, 1.0f, 20f, 40f)
        };

        List<string> unlocked = new List<string> { "1. Sentinel Arbalest", "Arbalet", "6. Royal Bombard", "Bombarda" };

        var pool5 = TowerDraftModalUI.FilterCandidates(all7Towers, unlocked);
        Assert(pool5.Count == 5, "DraftFilter_Step0_Leaves5", $"Expected 5, got {pool5.Count}");

        unlocked.Add("Garłacz Gromowy");
        var pool4 = TowerDraftModalUI.FilterCandidates(all7Towers, unlocked);
        Assert(pool4.Count == 4, "DraftFilter_Step1_Leaves4", $"Expected 4, got {pool4.Count}");

        unlocked.Add("Repetier Parowy");
        var pool3 = TowerDraftModalUI.FilterCandidates(all7Towers, unlocked);
        Assert(pool3.Count == 3, "DraftFilter_Step2_Leaves3", $"Expected 3, got {pool3.Count}");

        unlocked.Add("Tygiel Pożogi");
        var pool2 = TowerDraftModalUI.FilterCandidates(all7Towers, unlocked);
        Assert(pool2.Count == 2, "DraftFilter_Step3_Leaves2", $"Expected 2, got {pool2.Count}");

        unlocked.Add("Iglica Cieni");
        var pool1 = TowerDraftModalUI.FilterCandidates(all7Towers, unlocked);
        Assert(pool1.Count == 1, "DraftFilter_Step4_Leaves1", $"Expected 1, got {pool1.Count}");
        Assert(pool1[0].buildingName == "Monolit dusz", "DraftFilter_Step4_LastIsSoulMonolith", "Last candidate mismatch");

        unlocked.Add("Monolit dusz");
        var pool0 = TowerDraftModalUI.FilterCandidates(all7Towers, unlocked);
        Assert(pool0.Count == 0, "DraftFilter_Step5_Leaves0", $"Expected 0, got {pool0.Count}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 6: TowerDraftModalUI GEMINI Compliance
    // ─────────────────────────────────────────────────────────────────────────
    private static void Test6_TowerDraftModalUI_GEMINICompliance()
    {
        Assert(TowerDraftModalUI.FontSizeHeader == 20f, "GEMINI_FontSizeHeader_20pt", "Header must be 20pt");
        Assert(TowerDraftModalUI.FontSizeButton == 16f, "GEMINI_FontSizeButton_16pt", "Button must be 16pt");
        Assert(TowerDraftModalUI.FontSizeContent >= 12f && TowerDraftModalUI.FontSizeContent <= 14f, "GEMINI_FontSizeContent_12_14pt", "Content must be 12-14pt");
        Assert(TowerDraftModalUI.FontSizeHelper == 10f, "GEMINI_FontSizeHelper_10pt", "Helper text must be 10pt");
        Assert(TowerDraftModalUI.IconSizeStandard == 32f, "GEMINI_IconSizeStandard_32px", "Standard icon must be 32px");
        Assert(TowerDraftModalUI.IconSizeSmall == 24f, "GEMINI_IconSizeSmall_24px", "Small icon must be 24px");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 7: MetaUpgradeManager Hook & Detection
    // ─────────────────────────────────────────────────────────────────────────
    private static void Test7_MetaUpgradeManager_DraftHookAndDetection()
    {
        MetaUpgradeManager mgr = CreateMetaUpgradeManager();

        MetaUpgradeSO draftSO = CreateDummyUpgrade("nowa_wieza_1", "Nowa wieża 1", MetaEffectType.TowerDraftUnlock);
        MetaUpgradeSO regularSO = CreateDummyUpgrade("krasnoludzka_sila_1", "Krasnoludzka siła I", MetaEffectType.BonusWorkersPerShift);

        Assert(mgr.IsTowerDraftUpgrade(draftSO), "MetaMgr_DetectsTowerDraftUpgrade_True", "draft node must be recognized as draft");
        Assert(!mgr.IsTowerDraftUpgrade(regularSO), "MetaMgr_DetectsRegularUpgrade_False", "regular node must not be recognized as draft");

        MetaUpgradeSO notifiedNode = null;
        mgr.OnUpgradePurchased += (node) => notifiedNode = node;

        mgr.OnNodePurchased(regularSO);

        Assert(notifiedNode == regularSO, "MetaMgr_OnUpgradePurchasedEvent_Fired", "Event must be invoked with purchased node");
        Assert(regularSO.isUnlocked == true, "MetaMgr_OnNodePurchased_MarksUnlocked", "Node must be set to isUnlocked = true");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 8: Exhaustive All 120 Permutations of Draft Reduction (5 -> 4 -> 3 -> 2 -> 1 -> 0)
    // ─────────────────────────────────────────────────────────────────────────
    private static void Test8_ExhaustiveAllPermutationsDraftReduction()
    {
        List<TowerData> all7Towers = new List<TowerData>
        {
            CreateDummyTower("1. Sentinel Arbalest", "Arbalet", 25f, 15f, 1.2f, 20f, 0f),
            CreateDummyTower("2. Thunderbuss", "Garłacz Gromowy", 12f, 40f, 0.8f, 35f, 0f),
            CreateDummyTower("3. Steam Repeater", "Repetier Parowy", 18f, 10f, 3.5f, 10f, 0f),
            CreateDummyTower("4. Crucible of Cinders", "Tygiel Pożogi", 14f, 22f, 1.5f, 15f, 25f),
            CreateDummyTower("5. Shadow Spire", "Iglica Cieni", 22f, 60f, 0.5f, 0f, 50f),
            CreateDummyTower("6. Royal Bombard", "Bombarda", 30f, 85f, 0.4f, 50f, 0f),
            CreateDummyTower("7. Soul Monolith", "Monolit dusz", 20f, 45f, 1.0f, 20f, 40f)
        };

        var draftableTowers = new List<TowerData>
        {
            all7Towers[1], // Thunderbuss
            all7Towers[2], // Steam Repeater
            all7Towers[3], // Crucible
            all7Towers[4], // Shadow Spire
            all7Towers[6]  // Soul Monolith
        };

        var indexPermutations = GetIndexPermutations(5).ToList();
        Assert(indexPermutations.Count == 120, "DraftPermutations_CountIs120", $"Expected 120 permutations, got {indexPermutations.Count}");

        bool allPermutationsValid = true;
        string failureMsg = "";

        foreach (var pIndices in indexPermutations)
        {
            var perm = pIndices.Select(idx => draftableTowers[idx]).ToList();
            List<string> unlocked = new List<string> { "1. Sentinel Arbalest", "Arbalet", "6. Royal Bombard", "Bombarda" };

            // Step 0: Initial state -> exactly 5
            var pool0 = TowerDraftModalUI.FilterCandidates(all7Towers, unlocked);
            if (pool0.Count != 5) { allPermutationsValid = false; failureMsg = $"Step 0 count was {pool0.Count}"; break; }

            // Pick 1st tower -> exactly 4
            unlocked.Add(perm[0].buildingName);
            var pool1 = TowerDraftModalUI.FilterCandidates(all7Towers, unlocked);
            if (pool1.Count != 4 || pool1.Any(t => t.buildingName == perm[0].buildingName)) { allPermutationsValid = false; failureMsg = $"Step 1 failed for {perm[0].buildingName}"; break; }

            // Pick 2nd tower -> exactly 3
            unlocked.Add(perm[1].buildingName);
            var pool2 = TowerDraftModalUI.FilterCandidates(all7Towers, unlocked);
            if (pool2.Count != 3 || pool2.Any(t => t.buildingName == perm[1].buildingName)) { allPermutationsValid = false; failureMsg = $"Step 2 failed for {perm[1].buildingName}"; break; }

            // Pick 3rd tower -> exactly 2
            unlocked.Add(perm[2].buildingName);
            var pool3 = TowerDraftModalUI.FilterCandidates(all7Towers, unlocked);
            if (pool3.Count != 2 || pool3.Any(t => t.buildingName == perm[2].buildingName)) { allPermutationsValid = false; failureMsg = $"Step 3 failed for {perm[2].buildingName}"; break; }

            // Pick 4th tower -> exactly 1
            unlocked.Add(perm[3].buildingName);
            var pool4 = TowerDraftModalUI.FilterCandidates(all7Towers, unlocked);
            if (pool4.Count != 1 || pool4[0].buildingName != perm[4].buildingName) { allPermutationsValid = false; failureMsg = $"Step 4 failed: expected last to be {perm[4].buildingName}"; break; }

            // Pick 5th tower -> exactly 0
            unlocked.Add(perm[4].buildingName);
            var pool5 = TowerDraftModalUI.FilterCandidates(all7Towers, unlocked);
            if (pool5.Count != 0) { allPermutationsValid = false; failureMsg = $"Step 5 count was {pool5.Count}"; break; }
        }

        Assert(allPermutationsValid, "DraftPermutations_All120StrictlyReduce5_4_3_2_1_0", failureMsg);
    }

    private static IEnumerable<List<int>> GetIndexPermutations(int n)
    {
        return Permute(Enumerable.Range(0, n).ToList());

        static IEnumerable<List<int>> Permute(List<int> list)
        {
            if (list.Count == 1)
            {
                yield return new List<int> { list[0] };
                yield break;
            }

            for (int i = 0; i < list.Count; i++)
            {
                int item = list[i];
                var remaining = new List<int>(list);
                remaining.RemoveAt(i);
                foreach (var p in Permute(remaining))
                {
                    p.Insert(0, item);
                    yield return p;
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 9: JSON Persistence Roundtrip and Polish Diacritics
    // ─────────────────────────────────────────────────────────────────────────
    private static void Test9_JSONPersistenceRoundtripAndDiacritics()
    {
        string tempPath = Path.Combine(Path.GetTempPath(), $"save_roundtrip_{Guid.NewGuid():N}.json");
        try
        {
            SaveManager writerMgr = CreateSaveManager(tempPath);
            writerMgr.currentSaveData = new SaveData
            {
                totalHope = 1337,
                unlockedUpgradeIDs = new List<string> { "nowa_wieza_1", "ludzka_wytrwalosc_1", "krasnoludzkie_palenisko_1" },
                unlockedTowerIDs = new List<string>
                {
                    "1. Sentinel Arbalest",
                    "6. Royal Bombard",
                    "Garłacz Gromowy",
                    "Tygiel Pożogi",
                    "Monolit dusz"
                }
            };

            writerMgr.SaveGame();

            Assert(File.Exists(tempPath), "JSONRoundtrip_FileCreatedOnDisk", "Save file must exist on disk");
            string rawContent = File.ReadAllText(tempPath);
            Assert(rawContent.Length > 20, "JSONRoundtrip_FileContentNotEmpty", "Save file content must not be empty");
            Assert(rawContent.Contains("1337"), "JSONRoundtrip_HopePreserved", "Total hope missing in raw JSON");
            Assert(rawContent.Contains("Garłacz Gromowy"), "JSONRoundtrip_PolishDiacriticsGarlacz", "Diacritic Garłacz missing in raw JSON");
            Assert(rawContent.Contains("Tygiel Pożogi"), "JSONRoundtrip_PolishDiacriticsPozogi", "Diacritic Pożogi missing in raw JSON");

            // Independent reader
            SaveManager readerMgr = CreateSaveManager(tempPath);
            readerMgr.LoadGame();

            Assert(readerMgr.currentSaveData.totalHope == 1337, "JSONRoundtrip_ReadTotalHopeMatch", $"Expected 1337, got {readerMgr.currentSaveData.totalHope}");
            Assert(readerMgr.currentSaveData.unlockedUpgradeIDs.Count == 3, "JSONRoundtrip_ReadUpgradesCountMatch", $"Expected 3, got {readerMgr.currentSaveData.unlockedUpgradeIDs.Count}");
            Assert(readerMgr.currentSaveData.unlockedTowerIDs.Count == 5, "JSONRoundtrip_ReadTowersCountMatch", $"Expected 5, got {readerMgr.currentSaveData.unlockedTowerIDs.Count}");
            Assert(readerMgr.currentSaveData.unlockedTowerIDs.Contains("Garłacz Gromowy"), "JSONRoundtrip_ReadDiacriticGarlaczPreserved");
            Assert(readerMgr.currentSaveData.unlockedTowerIDs.Contains("Tygiel Pożogi"), "JSONRoundtrip_ReadDiacriticPozogiPreserved");
            Assert(readerMgr.currentSaveData.unlockedTowerIDs.Contains("Monolit dusz"), "JSONRoundtrip_ReadMonolitPreserved");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 10: Save System Edge Cases and Corruption Recovery
    // ─────────────────────────────────────────────────────────────────────────
    private static void Test10_SaveSystemEdgeCasesAndCorruptionRecovery()
    {
        // 1. Corrupted / incomplete JSON recovery
        string corruptPath = Path.Combine(Path.GetTempPath(), $"save_corrupt_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(corruptPath, "{ \"totalHope\": 500, \"unlockedTowerIDs\": [");
            SaveManager mgr = CreateSaveManager(corruptPath);

            bool noCrashOnCorrupt = true;
            try
            {
                mgr.LoadGame();
            }
            catch (Exception ex)
            {
                noCrashOnCorrupt = false;
                Console.WriteLine($"Crash on corrupt JSON: {ex.Message}");
            }

            Assert(noCrashOnCorrupt, "CorruptionRecovery_NoUnhandledCrash", "LoadGame must handle malformed JSON gracefully");
            Assert(mgr.currentSaveData.unlockedTowerIDs != null && mgr.currentSaveData.unlockedTowerIDs.Count >= 2,
                "CorruptionRecovery_RestoresBaselineStarters", $"Expected >=2 starters, got {mgr.currentSaveData.unlockedTowerIDs?.Count}");
        }
        finally
        {
            if (File.Exists(corruptPath)) { try { File.Delete(corruptPath); } catch { } }
        }

        // 2. Legacy save file (missing unlockedTowerIDs field)
        string legacyPath = Path.Combine(Path.GetTempPath(), $"save_legacy_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(legacyPath, "{\n  \"totalHope\": 250,\n  \"unlockedUpgradeIDs\": [\"krasnoludzka_sila_1\"]\n}");
            SaveManager legacyMgr = CreateSaveManager(legacyPath);
            legacyMgr.LoadGame();

            Assert(legacyMgr.currentSaveData.totalHope == 250, "LegacyMigration_PreservesHope", "Hope must be preserved");
            Assert(legacyMgr.currentSaveData.unlockedUpgradeIDs.Contains("krasnoludzka_sila_1"), "LegacyMigration_PreservesUpgrades", "Upgrades preserved");
            Assert(legacyMgr.currentSaveData.unlockedTowerIDs.Count == 2, "LegacyMigration_SeedsDefaultStarters", $"Expected 2, got {legacyMgr.currentSaveData.unlockedTowerIDs.Count}");
            Assert(legacyMgr.currentSaveData.unlockedTowerIDs.Contains("1. Sentinel Arbalest"), "LegacyMigration_HasSentinelArbalest");
            Assert(legacyMgr.currentSaveData.unlockedTowerIDs.Contains("6. Royal Bombard"), "LegacyMigration_HasRoyalBombard");

            // Verify auto-saved to disk with new field
            string updatedJson = File.ReadAllText(legacyPath);
            Assert(updatedJson.Contains("unlockedTowerIDs") && updatedJson.Contains("Sentinel Arbalest"), "LegacyMigration_AutoSavesToDisk");
        }
        finally
        {
            if (File.Exists(legacyPath)) { try { File.Delete(legacyPath); } catch { } }
        }

        // 3. Null and empty collections in TowerDraftModalUI.FilterCandidates
        var nullCandidateResult = TowerDraftModalUI.FilterCandidates(null, new List<string> { "1. Sentinel Arbalest" });
        Assert(nullCandidateResult != null && nullCandidateResult.Count == 0, "FilterCandidates_NullListSafe", "Null towers list returns empty");

        var dummyTowers = new List<TowerData> { CreateDummyTower("T1", "B1", 10, 10, 1, 0, 0) };
        var nullUnlockedResult = TowerDraftModalUI.FilterCandidates(dummyTowers, null);
        Assert(nullUnlockedResult.Count == 1, "FilterCandidates_NullUnlockedSafe", "Null unlocked list treats all as locked");

        // 4. Idempotency of UnlockTower
        string idempPath = Path.Combine(Path.GetTempPath(), $"save_idemp_{Guid.NewGuid():N}.json");
        try
        {
            SaveManager idempMgr = CreateSaveManager(idempPath);
            idempMgr.LoadGame();
            int initialCount = idempMgr.currentSaveData.unlockedTowerIDs.Count;

            idempMgr.UnlockTower("2. Thunderbuss");
            idempMgr.UnlockTower("2. Thunderbuss");
            idempMgr.UnlockTower("2. Thunderbuss");

            Assert(idempMgr.currentSaveData.unlockedTowerIDs.Count == initialCount + 1,
                "UnlockTower_IdempotentNoDuplicates", $"Expected {initialCount + 1}, got {idempMgr.currentSaveData.unlockedTowerIDs.Count}");
        }
        finally
        {
            if (File.Exists(idempPath)) { try { File.Delete(idempPath); } catch { } }
        }
    }
}

