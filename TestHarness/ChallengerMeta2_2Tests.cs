using System;
using System.Collections.Generic;

public static class ChallengerMeta2_2Tests
{
    private static int _passed = 0;
    private static int _failed = 0;

    private static void Assert(bool condition, string testName, string details = "")
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"[CHALLENGER2-PASS] {testName}");
        }
        else
        {
            _failed++;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[CHALLENGER2-FAIL] {testName} - {details}");
            Console.ResetColor();
        }
    }

    public static int RunAll()
    {
        Console.WriteLine("\n=================================================");
        Console.WriteLine("CHALLENGER 2: R2 EMPIRICAL STRESS TEST SUITE");
        Console.WriteLine("Testing: Rune Gating, Forge Tabs, Racial Synergies");
        Console.WriteLine("=================================================\n");

        Test1_RuneGating_ZeroUnlockedWeightsStrictlyZero();
        Test1_RuneGating_UncommonUnlockedWeightsPositiveAndHigherZero();
        Test1_RuneGating_ProgressiveTierWeightsAndMonteCarlo();
        Test1_RuneGating_AdversarialBoundaryConditions();

        Test2_ForgeTabs_LockedInteractability();
        Test2_ForgeTabs_UnlockedInteractability();
        Test2_ForgeTabs_TabSwitchingGate();

        Test3_RacialSynergies_TowerEntity_DwarfBonus();
        Test3_RacialSynergies_TowerEntity_ElfBonus();
        Test3_RacialSynergies_TowerEntity_HumanBonus();
        Test3_RacialSynergies_TowerEntity_MixedCrewConcurrence();
        Test3_RacialSynergies_TowerEntity_LockedUpgradesYieldZero();

        Test3_RacialSynergies_BuildingProduction_HumanBonus();
        Test3_RacialSynergies_BuildingProduction_ElfBonusSawmillFarm();
        Test3_RacialSynergies_BuildingProduction_ElfBonusNonSawmillZero();
        Test3_RacialSynergies_BuildingProduction_LockedUpgradesYieldZero();

        Console.WriteLine("\n=================================================");
        Console.WriteLine($"CHALLENGER 2 SUMMARY: {_passed} PASSED, {_failed} FAILED");
        Console.WriteLine("=================================================\n");

        return _failed;
    }

    // =========================================================================
    // 1. RUNE GATING TESTS
    // =========================================================================

    public class MockRuneConfig
    {
        public float weightCommon = 60f;
        public float weightUncommon = 20f;
        public float weightRare = 10f;
        public float weightLegendary = 6f;
        public float weightCursed = 4f;
    }

    public class MockMetaState
    {
        public HashSet<MetaEffectType> unlocked = new HashSet<MetaEffectType>();
        public Dictionary<MetaEffectType, float> bonuses = new Dictionary<MetaEffectType, float>();

        public bool IsUnlocked(MetaEffectType t) => unlocked.Contains(t);
        public float GetBonus(MetaEffectType t) => bonuses.TryGetValue(t, out var val) ? val : 0f;
    }

    // Exact replica of RuneManager.RollRarity logic
    public static (RuneRarity rarity, float wCommon, float wUnc, float wRare, float wLeg, float wCursed)
        SimulateRollRarity(MockRuneConfig config, MockMetaState meta, float hearthRuneMult = 1.0f, float? forcedRoll = null)
    {
        float wCommon = config.weightCommon;

        bool uncUnlocked = meta == null || meta.IsUnlocked(MetaEffectType.UnlockRuneRarity_Uncommon);
        bool rareUnlocked = meta == null || meta.IsUnlocked(MetaEffectType.UnlockRuneRarity_Rare);
        bool legUnlocked = meta == null || meta.IsUnlocked(MetaEffectType.UnlockRuneRarity_Legendary);
        bool cursedUnlocked = meta == null || meta.IsUnlocked(MetaEffectType.UnlockRuneRarity_Cursed);

        float wUnc = uncUnlocked ? Math.Max(0f, (config.weightUncommon + (meta != null ? meta.GetBonus(MetaEffectType.RuneDropChance_Uncommon) : 0f)) * hearthRuneMult) : 0f;
        float wRare = rareUnlocked ? Math.Max(0f, (config.weightRare + (meta != null ? meta.GetBonus(MetaEffectType.RuneDropChance_Rare) : 0f)) * hearthRuneMult) : 0f;
        float wLeg = legUnlocked ? Math.Max(0f, (config.weightLegendary + (meta != null ? meta.GetBonus(MetaEffectType.RuneDropChance_Legendary) : 0f)) * hearthRuneMult) : 0f;
        float wCursed = cursedUnlocked ? Math.Max(0f, (config.weightCursed + (meta != null ? meta.GetBonus(MetaEffectType.RuneDropChance_Cursed) : 0f)) * hearthRuneMult) : 0f;

        float totalBonus = 0f;
        if (uncUnlocked && wUnc > config.weightUncommon) totalBonus += (wUnc - config.weightUncommon);
        if (rareUnlocked && wRare > config.weightRare) totalBonus += (wRare - config.weightRare);
        if (legUnlocked && wLeg > config.weightLegendary) totalBonus += (wLeg - config.weightLegendary);
        if (cursedUnlocked && wCursed > config.weightCursed) totalBonus += (wCursed - config.weightCursed);
        wCommon = Math.Max(0f, wCommon - totalBonus);

        float totalWeight = wCommon + wUnc + wRare + wLeg + wCursed;
        if (totalWeight <= 0f) return (RuneRarity.Common, wCommon, wUnc, wRare, wLeg, wCursed);

        float roll = forcedRoll.HasValue ? forcedRoll.Value : (float)(new Random().NextDouble() * totalWeight);

        if (roll <= wCommon) return (RuneRarity.Common, wCommon, wUnc, wRare, wLeg, wCursed);
        roll -= wCommon;

        if (roll <= wUnc) return (RuneRarity.Uncommon, wCommon, wUnc, wRare, wLeg, wCursed);
        roll -= wUnc;

        if (roll <= wRare) return (RuneRarity.Rare, wCommon, wUnc, wRare, wLeg, wCursed);
        roll -= wRare;

        if (roll <= wLeg) return (RuneRarity.Legendary, wCommon, wUnc, wRare, wLeg, wCursed);

        return (cursedUnlocked ? RuneRarity.Cursed : RuneRarity.Common, wCommon, wUnc, wRare, wLeg, wCursed);
    }

    private static void Test1_RuneGating_ZeroUnlockedWeightsStrictlyZero()
    {
        var config = new MockRuneConfig();
        var meta = new MockMetaState(); // Nothing unlocked

        var result = SimulateRollRarity(config, meta);

        Assert(result.wUnc == 0f, "Rune Gating: Uncommon weight is strictly 0 when locked", $"actual: {result.wUnc}");
        Assert(result.wRare == 0f, "Rune Gating: Rare weight is strictly 0 when locked", $"actual: {result.wRare}");
        Assert(result.wLeg == 0f, "Rune Gating: Legendary weight is strictly 0 when locked", $"actual: {result.wLeg}");
        Assert(result.wCursed == 0f, "Rune Gating: Cursed weight is strictly 0 when locked", $"actual: {result.wCursed}");
        Assert(result.wCommon == config.weightCommon, "Rune Gating: Common weight equals config weight when no higher tiers are unlocked", $"actual: {result.wCommon}");
    }

    private static void Test1_RuneGating_UncommonUnlockedWeightsPositiveAndHigherZero()
    {
        var config = new MockRuneConfig();
        var meta = new MockMetaState();
        meta.unlocked.Add(MetaEffectType.UnlockRuneRarity_Uncommon);

        var result = SimulateRollRarity(config, meta);

        Assert(result.wUnc > 0f, "Rune Gating: Uncommon weight > 0 when UnlockRuneRarity_Uncommon unlocked", $"actual: {result.wUnc}");
        Assert(result.wUnc == 20f, "Rune Gating: Uncommon weight matches config (20f)", $"actual: {result.wUnc}");
        Assert(result.wRare == 0f, "Rune Gating: Rare weight remains strictly 0 when only Uncommon is unlocked", $"actual: {result.wRare}");
        Assert(result.wLeg == 0f, "Rune Gating: Legendary weight remains strictly 0 when only Uncommon is unlocked", $"actual: {result.wLeg}");
        Assert(result.wCursed == 0f, "Rune Gating: Cursed weight remains strictly 0 when only Uncommon is unlocked", $"actual: {result.wCursed}");
    }

    private static void Test1_RuneGating_ProgressiveTierWeightsAndMonteCarlo()
    {
        var config = new MockRuneConfig();
        var meta = new MockMetaState();
        int rolls = 100000;
        var rng = new Random(42);

        // Scenario A: 0 Unlocked -> 100,000 rolls must be 100% Common
        int commonCount = 0, uncCount = 0, rareCount = 0, legCount = 0, cursedCount = 0;
        for (int i = 0; i < rolls; i++)
        {
            float roll = (float)(rng.NextDouble() * 60f);
            var res = SimulateRollRarity(config, meta, 1.0f, roll);
            if (res.rarity == RuneRarity.Common) commonCount++;
            else if (res.rarity == RuneRarity.Uncommon) uncCount++;
            else if (res.rarity == RuneRarity.Rare) rareCount++;
            else if (res.rarity == RuneRarity.Legendary) legCount++;
            else if (res.rarity == RuneRarity.Cursed) cursedCount++;
        }

        Assert(commonCount == rolls, "Monte Carlo (100k rolls, 0 unlocked): 100% Common drops", $"Common={commonCount}/{rolls}");
        Assert(uncCount == 0 && rareCount == 0 && legCount == 0 && cursedCount == 0,
            "Monte Carlo (100k rolls, 0 unlocked): Zero drops of Uncommon/Rare/Legendary/Cursed",
            $"Unc={uncCount}, Rare={rareCount}, Leg={legCount}, Cursed={cursedCount}");

        // Scenario B: Unlock Uncommon only -> 100,000 rolls must yield Common & Uncommon, ZERO Rare/Leg/Cursed
        meta.unlocked.Add(MetaEffectType.UnlockRuneRarity_Uncommon);
        commonCount = uncCount = rareCount = legCount = cursedCount = 0;
        float totalWeight = config.weightCommon + config.weightUncommon; // 60 + 20 = 80

        for (int i = 0; i < rolls; i++)
        {
            float roll = (float)(rng.NextDouble() * totalWeight);
            var res = SimulateRollRarity(config, meta, 1.0f, roll);
            if (res.rarity == RuneRarity.Common) commonCount++;
            else if (res.rarity == RuneRarity.Uncommon) uncCount++;
            else if (res.rarity == RuneRarity.Rare) rareCount++;
            else if (res.rarity == RuneRarity.Legendary) legCount++;
            else if (res.rarity == RuneRarity.Cursed) cursedCount++;
        }

        Assert(commonCount > 0, "Monte Carlo (100k rolls, Uncommon unlocked): Common dropped", $"Common={commonCount}");
        Assert(uncCount > 0, "Monte Carlo (100k rolls, Uncommon unlocked): Uncommon dropped", $"Uncommon={uncCount}");
        Assert(rareCount == 0, "Monte Carlo (100k rolls, Uncommon unlocked): Rare strictly 0 drops", $"Rare={rareCount}");
        Assert(legCount == 0, "Monte Carlo (100k rolls, Uncommon unlocked): Legendary strictly 0 drops", $"Leg={legCount}");
        Assert(cursedCount == 0, "Monte Carlo (100k rolls, Uncommon unlocked): Cursed strictly 0 drops", $"Cursed={cursedCount}");

        // Ratio verification: expected 60/80 = 75% Common, 20/80 = 25% Uncommon (+/- 1%)
        double uncRatio = (double)uncCount / rolls;
        Assert(Math.Abs(uncRatio - 0.25) < 0.01, "Monte Carlo: Uncommon drop ratio is 25% (+/- 1%)", $"Actual: {uncRatio:P2}");
    }

    private static void Test1_RuneGating_AdversarialBoundaryConditions()
    {
        var config = new MockRuneConfig();
        var meta = new MockMetaState();

        // 1. Total weight zero edge case -> returns Common
        config.weightCommon = 0f;
        var resZero = SimulateRollRarity(config, meta);
        Assert(resZero.rarity == RuneRarity.Common, "Rune Gating Edge Case: Total weight <= 0 returns Common", $"got {resZero.rarity}");

        // 2. Roll precision overflow (roll > totalWeight due to float inaccuracy) when Cursed locked
        config.weightCommon = 60f;
        meta.unlocked.Add(MetaEffectType.UnlockRuneRarity_Uncommon);
        // Force roll beyond totalWeight (e.g. 80.001f when total is 80f)
        var resOverflow = SimulateRollRarity(config, meta, 1.0f, 80.5f);
        Assert(resOverflow.rarity == RuneRarity.Common, "Rune Gating Overflow: Exceeding total weight when Cursed locked safely returns Common", $"got {resOverflow.rarity}");

        // 3. Roll precision overflow when Cursed IS unlocked returns Cursed
        meta.unlocked.Add(MetaEffectType.UnlockRuneRarity_Cursed);
        var resOverflowCursed = SimulateRollRarity(config, meta, 1.0f, 100.5f);
        Assert(resOverflowCursed.rarity == RuneRarity.Cursed, "Rune Gating Overflow: Exceeding total weight when Cursed unlocked returns Cursed", $"got {resOverflowCursed.rarity}");
    }

    // =========================================================================
    // 2. FORGE TAB INTERACTABILITY TESTS
    // =========================================================================

    public class MockForgeUIState
    {
        public bool btnTabFusion_interactable = true;
        public bool btnTabTransmutation_interactable = true;
        public string txtFusionLabel = "";
        public string txtTransmuteLabel = "";
        public RuneForgeWindowUI.RuneForgeTab currentTab = RuneForgeWindowUI.RuneForgeTab.Socketing;

        public void RefreshLeftNav(MockMetaState meta)
        {
            bool fusionUnlocked = meta == null || meta.IsUnlocked(MetaEffectType.UnlockRuneFusion);
            bool transmuteUnlocked = meta == null || meta.IsUnlocked(MetaEffectType.UnlockRuneTransmutation);

            btnTabFusion_interactable = fusionUnlocked;
            txtFusionLabel = fusionUnlocked ? "Fuzja Run" : "Fuzja (Zablokowana)";

            btnTabTransmutation_interactable = transmuteUnlocked;
            txtTransmuteLabel = transmuteUnlocked ? "Transmutacja Run" : "Transmutacja (Zablokowana)";
        }

        public bool SwitchTab(RuneForgeWindowUI.RuneForgeTab tab, MockMetaState meta)
        {
            if (tab == RuneForgeWindowUI.RuneForgeTab.Fusion && meta != null && !meta.IsUnlocked(MetaEffectType.UnlockRuneFusion))
                return false;
            if (tab == RuneForgeWindowUI.RuneForgeTab.Transmutation && meta != null && !meta.IsUnlocked(MetaEffectType.UnlockRuneTransmutation))
                return false;

            currentTab = tab;
            return true;
        }
    }

    private static void Test2_ForgeTabs_LockedInteractability()
    {
        var meta = new MockMetaState(); // Neither fusion nor transmutation unlocked
        var ui = new MockForgeUIState();

        ui.RefreshLeftNav(meta);

        Assert(!ui.btnTabFusion_interactable, "Forge Tabs: btnTabFusion.interactable == false when UnlockRuneFusion locked");
        Assert(ui.txtFusionLabel == "Fuzja (Zablokowana)", "Forge Tabs: Fusion tab label reflects locked status", $"got '{ui.txtFusionLabel}'");

        Assert(!ui.btnTabTransmutation_interactable, "Forge Tabs: btnTabTransmutation.interactable == false when UnlockRuneTransmutation locked");
        Assert(ui.txtTransmuteLabel == "Transmutacja (Zablokowana)", "Forge Tabs: Transmutation tab label reflects locked status", $"got '{ui.txtTransmuteLabel}'");
    }

    private static void Test2_ForgeTabs_UnlockedInteractability()
    {
        var meta = new MockMetaState();
        meta.unlocked.Add(MetaEffectType.UnlockRuneFusion);
        meta.unlocked.Add(MetaEffectType.UnlockRuneTransmutation);

        var ui = new MockForgeUIState();
        ui.RefreshLeftNav(meta);

        Assert(ui.btnTabFusion_interactable, "Forge Tabs: btnTabFusion.interactable == true when UnlockRuneFusion unlocked");
        Assert(ui.txtFusionLabel == "Fuzja Run", "Forge Tabs: Fusion tab label is 'Fuzja Run'");

        Assert(ui.btnTabTransmutation_interactable, "Forge Tabs: btnTabTransmutation.interactable == true when UnlockRuneTransmutation unlocked");
        Assert(ui.txtTransmuteLabel == "Transmutacja Run", "Forge Tabs: Transmutation tab label is 'Transmutacja Run'");
    }

    private static void Test2_ForgeTabs_TabSwitchingGate()
    {
        var meta = new MockMetaState(); // Locked
        var ui = new MockForgeUIState();

        bool switchedFusion = ui.SwitchTab(RuneForgeWindowUI.RuneForgeTab.Fusion, meta);
        Assert(!switchedFusion, "Forge Tabs: SwitchTab(Fusion) rejected when locked");
        Assert(ui.currentTab == RuneForgeWindowUI.RuneForgeTab.Socketing, "Forge Tabs: Current tab remains Socketing after rejected Fusion switch");

        bool switchedTransmute = ui.SwitchTab(RuneForgeWindowUI.RuneForgeTab.Transmutation, meta);
        Assert(!switchedTransmute, "Forge Tabs: SwitchTab(Transmutation) rejected when locked");
        Assert(ui.currentTab == RuneForgeWindowUI.RuneForgeTab.Socketing, "Forge Tabs: Current tab remains Socketing after rejected Transmute switch");

        // Unlock fusion only
        meta.unlocked.Add(MetaEffectType.UnlockRuneFusion);
        bool switchedFusionNow = ui.SwitchTab(RuneForgeWindowUI.RuneForgeTab.Fusion, meta);
        Assert(switchedFusionNow, "Forge Tabs: SwitchTab(Fusion) succeeds when UnlockRuneFusion unlocked");
        Assert(ui.currentTab == RuneForgeWindowUI.RuneForgeTab.Fusion, "Forge Tabs: Current tab is now Fusion");
    }

    // =========================================================================
    // 3. RACIAL SYNERGIES IN TOWER ENTITY & BUILDING PRODUCTION TESTS
    // =========================================================================

    public static (float damageMulti, float rangeMulti, float metaCritChance, float metaArmorPen, float metaEfficiencyBonus)
        SimulateTowerRecalculateStats(int dwarfCount, int elfCount, int humanCount, MockMetaState meta)
    {
        float metaDamageMulti = 0f;
        float metaRangeMulti = 0f;
        float metaCritChance = 0f;
        float metaArmorPen = 0f;
        float metaEfficiencyBonus = 0f;

        if (meta != null)
        {
            // Krasnoludy: +8% obrażeń, +5% przebicia pancerza na krasnoluda
            if (dwarfCount > 0 && (meta.IsUnlocked(MetaEffectType.TowerCrewSynergy_Dwarf)))
            {
                metaDamageMulti += 0.08f * dwarfCount;
                metaArmorPen += 0.05f * dwarfCount;
            }

            // Elfy: +10% zasięgu, +5% szansy na trafienie krytyczne na elfa
            if (elfCount > 0 && (meta.IsUnlocked(MetaEffectType.TowerCrewSynergy_Elf)))
            {
                metaRangeMulti += 0.10f * elfCount;
                metaCritChance += 0.05f * elfCount;
            }

            // Ludzie (Ludzka wytrwałość II): conajmniej 4 ludzi daje +20% wydajności
            if (humanCount >= 4 && (meta.IsUnlocked(MetaEffectType.TowerCrewSynergy_Human)))
            {
                metaEfficiencyBonus += 0.20f;
            }
        }

        return (metaDamageMulti, metaRangeMulti, metaCritChance, metaArmorPen, metaEfficiencyBonus);
    }

    private static void Test3_RacialSynergies_TowerEntity_DwarfBonus()
    {
        var meta = new MockMetaState();
        meta.unlocked.Add(MetaEffectType.TowerCrewSynergy_Dwarf);

        // 1 Dwarf: +8% dmg, +5% arm pen
        var res1 = SimulateTowerRecalculateStats(1, 0, 0, meta);
        Assert(Math.Abs(res1.damageMulti - 0.08f) < 0.0001f, "Tower Synergy (1 Dwarf): Damage multi is exactly +8% (0.08)", $"got {res1.damageMulti}");
        Assert(Math.Abs(res1.metaArmorPen - 0.05f) < 0.0001f, "Tower Synergy (1 Dwarf): Armor pen is exactly +5% (0.05)", $"got {res1.metaArmorPen}");

        // 2 Dwarfs: +16% dmg, +10% arm pen
        var res2 = SimulateTowerRecalculateStats(2, 0, 0, meta);
        Assert(Math.Abs(res2.damageMulti - 0.16f) < 0.0001f, "Tower Synergy (2 Dwarfs): Damage multi is exactly +16% (0.16)", $"got {res2.damageMulti}");
        Assert(Math.Abs(res2.metaArmorPen - 0.10f) < 0.0001f, "Tower Synergy (2 Dwarfs): Armor pen is exactly +10% (0.10)", $"got {res2.metaArmorPen}");

        // 3 Dwarfs: +24% dmg, +15% arm pen
        var res3 = SimulateTowerRecalculateStats(3, 0, 0, meta);
        Assert(Math.Abs(res3.damageMulti - 0.24f) < 0.0001f, "Tower Synergy (3 Dwarfs): Damage multi is exactly +24% (0.24)", $"got {res3.damageMulti}");
        Assert(Math.Abs(res3.metaArmorPen - 0.15f) < 0.0001f, "Tower Synergy (3 Dwarfs): Armor pen is exactly +15% (0.15)", $"got {res3.metaArmorPen}");

        // 4 Dwarfs: +32% dmg, +20% arm pen
        var res4 = SimulateTowerRecalculateStats(4, 0, 0, meta);
        Assert(Math.Abs(res4.damageMulti - 0.32f) < 0.0001f, "Tower Synergy (4 Dwarfs): Damage multi is exactly +32% (0.32)", $"got {res4.damageMulti}");
        Assert(Math.Abs(res4.metaArmorPen - 0.20f) < 0.0001f, "Tower Synergy (4 Dwarfs): Armor pen is exactly +20% (0.20)", $"got {res4.metaArmorPen}");
    }

    private static void Test3_RacialSynergies_TowerEntity_ElfBonus()
    {
        var meta = new MockMetaState();
        meta.unlocked.Add(MetaEffectType.TowerCrewSynergy_Elf);

        // 1 Elf: +10% range, +5% crit
        var res1 = SimulateTowerRecalculateStats(0, 1, 0, meta);
        Assert(Math.Abs(res1.rangeMulti - 0.10f) < 0.0001f, "Tower Synergy (1 Elf): Range multi is exactly +10% (0.10)", $"got {res1.rangeMulti}");
        Assert(Math.Abs(res1.metaCritChance - 0.05f) < 0.0001f, "Tower Synergy (1 Elf): Crit chance is exactly +5% (0.05)", $"got {res1.metaCritChance}");

        // 2 Elves: +20% range, +10% crit
        var res2 = SimulateTowerRecalculateStats(0, 2, 0, meta);
        Assert(Math.Abs(res2.rangeMulti - 0.20f) < 0.0001f, "Tower Synergy (2 Elves): Range multi is exactly +20% (0.20)", $"got {res2.rangeMulti}");
        Assert(Math.Abs(res2.metaCritChance - 0.10f) < 0.0001f, "Tower Synergy (2 Elves): Crit chance is exactly +10% (0.10)", $"got {res2.metaCritChance}");

        // 3 Elves: +30% range, +15% crit
        var res3 = SimulateTowerRecalculateStats(0, 3, 0, meta);
        Assert(Math.Abs(res3.rangeMulti - 0.30f) < 0.0001f, "Tower Synergy (3 Elves): Range multi is exactly +30% (0.30)", $"got {res3.rangeMulti}");
        Assert(Math.Abs(res3.metaCritChance - 0.15f) < 0.0001f, "Tower Synergy (3 Elves): Crit chance is exactly +15% (0.15)", $"got {res3.metaCritChance}");

        // 4 Elves: +40% range, +20% crit
        var res4 = SimulateTowerRecalculateStats(0, 4, 0, meta);
        Assert(Math.Abs(res4.rangeMulti - 0.40f) < 0.0001f, "Tower Synergy (4 Elves): Range multi is exactly +40% (0.40)", $"got {res4.rangeMulti}");
        Assert(Math.Abs(res4.metaCritChance - 0.20f) < 0.0001f, "Tower Synergy (4 Elves): Crit chance is exactly +20% (0.20)", $"got {res4.metaCritChance}");
    }

    private static void Test3_RacialSynergies_TowerEntity_HumanBonus()
    {
        var meta = new MockMetaState();
        meta.unlocked.Add(MetaEffectType.TowerCrewSynergy_Human);

        // Sub-threshold tests: 1, 2, 3 humans must give 0% bonus
        for (int h = 1; h <= 3; h++)
        {
            var res = SimulateTowerRecalculateStats(0, 0, h, meta);
            Assert(res.metaEfficiencyBonus == 0f, $"Tower Synergy ({h} Humans): Efficiency bonus is strictly 0 (below threshold of 4)", $"got {res.metaEfficiencyBonus}");
        }

        // Threshold test: 4 humans gives exactly +20% efficiency
        var res4 = SimulateTowerRecalculateStats(0, 0, 4, meta);
        Assert(Math.Abs(res4.metaEfficiencyBonus - 0.20f) < 0.0001f, "Tower Synergy (4 Humans): Efficiency bonus is exactly +20% (0.20)", $"got {res4.metaEfficiencyBonus}");

        // Above threshold: 5 humans also gives +20% efficiency
        var res5 = SimulateTowerRecalculateStats(0, 0, 5, meta);
        Assert(Math.Abs(res5.metaEfficiencyBonus - 0.20f) < 0.0001f, "Tower Synergy (5 Humans): Efficiency bonus is +20% (flat threshold)", $"got {res5.metaEfficiencyBonus}");
    }

    private static void Test3_RacialSynergies_TowerEntity_MixedCrewConcurrence()
    {
        var meta = new MockMetaState();
        meta.unlocked.Add(MetaEffectType.TowerCrewSynergy_Dwarf);
        meta.unlocked.Add(MetaEffectType.TowerCrewSynergy_Elf);
        meta.unlocked.Add(MetaEffectType.TowerCrewSynergy_Human);

        // Mixed crew: 2 Dwarfs, 1 Elf, 4 Humans
        var res = SimulateTowerRecalculateStats(2, 1, 4, meta);

        Assert(Math.Abs(res.damageMulti - 0.16f) < 0.0001f, "Tower Synergy (Mixed Crew): Dwarf damage multi is +16%", $"got {res.damageMulti}");
        Assert(Math.Abs(res.metaArmorPen - 0.10f) < 0.0001f, "Tower Synergy (Mixed Crew): Dwarf armor pen is +10%", $"got {res.metaArmorPen}");
        Assert(Math.Abs(res.rangeMulti - 0.10f) < 0.0001f, "Tower Synergy (Mixed Crew): Elf range multi is +10%", $"got {res.rangeMulti}");
        Assert(Math.Abs(res.metaCritChance - 0.05f) < 0.0001f, "Tower Synergy (Mixed Crew): Elf crit chance is +5%", $"got {res.metaCritChance}");
        Assert(Math.Abs(res.metaEfficiencyBonus - 0.20f) < 0.0001f, "Tower Synergy (Mixed Crew): Human efficiency bonus is +20%", $"got {res.metaEfficiencyBonus}");
    }

    private static void Test3_RacialSynergies_TowerEntity_LockedUpgradesYieldZero()
    {
        var meta = new MockMetaState(); // Upgrades NOT unlocked
        var res = SimulateTowerRecalculateStats(4, 4, 4, meta);

        Assert(res.damageMulti == 0f, "Tower Synergy (Locked Upgrades): Dwarf damage multi is 0");
        Assert(res.metaArmorPen == 0f, "Tower Synergy (Locked Upgrades): Dwarf armor pen is 0");
        Assert(res.rangeMulti == 0f, "Tower Synergy (Locked Upgrades): Elf range multi is 0");
        Assert(res.metaCritChance == 0f, "Tower Synergy (Locked Upgrades): Elf crit chance is 0");
        Assert(res.metaEfficiencyBonus == 0f, "Tower Synergy (Locked Upgrades): Human efficiency bonus is 0");
    }

    // Exact replica of BuildingProductionComponent.GetCrewEfficiency synergy logic
    public static float SimulateBuildingProductionCrewEfficiency(
        float baseTableEff, int totalWorkers, int humanCount, int elfCount, string buildingName, MockMetaState meta)
    {
        float baseEff = baseTableEff;

        if (totalWorkers > 0 && meta != null)
        {
            // Human production synergy (Ludzka wytrwałość I): if humanCount >= 4, +20% efficiency
            if (humanCount >= 4 && meta.IsUnlocked(MetaEffectType.ProductionSynergy_Human))
            {
                baseEff += 0.20f;
            }

            // Elf production synergy (Elfy 3): if sawmill/farm, +5% efficiency per elf worker
            if (elfCount > 0 && meta.IsUnlocked(MetaEffectType.ProductionSynergy_Elf))
            {
                if (IsSawmillOrFarm(buildingName))
                {
                    baseEff += 0.05f * elfCount;
                }
            }
        }

        return baseEff;
    }

    private static bool IsSawmillOrFarm(string bName)
    {
        if (string.IsNullOrEmpty(bName)) return false;
        return bName.IndexOf("Sawmill", StringComparison.OrdinalIgnoreCase) >= 0
            || bName.IndexOf("Tartak", StringComparison.OrdinalIgnoreCase) >= 0
            || bName.IndexOf("Farm", StringComparison.OrdinalIgnoreCase) >= 0
            || bName.IndexOf("Farma", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void Test3_RacialSynergies_BuildingProduction_HumanBonus()
    {
        var meta = new MockMetaState();
        meta.unlocked.Add(MetaEffectType.ProductionSynergy_Human);

        // Sub-threshold: 3 humans
        float eff3 = SimulateBuildingProductionCrewEfficiency(1.0f, 3, 3, 0, "Quarry", meta);
        Assert(eff3 == 1.0f, "Building Production (3 Humans): No synergy bonus below 4 humans", $"got {eff3}");

        // Threshold: 4 humans -> +0.20f
        float eff4 = SimulateBuildingProductionCrewEfficiency(1.0f, 4, 4, 0, "Quarry", meta);
        Assert(Math.Abs(eff4 - 1.20f) < 0.0001f, "Building Production (4 Humans): Exactly +20% efficiency (1.0 -> 1.2)", $"got {eff4}");

        // 5 humans -> +0.20f
        float eff5 = SimulateBuildingProductionCrewEfficiency(1.2f, 5, 5, 0, "Quarry", meta);
        Assert(Math.Abs(eff5 - 1.40f) < 0.0001f, "Building Production (5 Humans): Exactly +20% efficiency on base 1.2 (1.2 -> 1.4)", $"got {eff5}");
    }

    private static void Test3_RacialSynergies_BuildingProduction_ElfBonusSawmillFarm()
    {
        var meta = new MockMetaState();
        meta.unlocked.Add(MetaEffectType.ProductionSynergy_Elf);

        string[] eligibleBuildings = { "Sawmill", "Tartak", "Farm", "Farma", "Sawmill Level 2", "Tartak Drewna" };

        foreach (var bName in eligibleBuildings)
        {
            // 1 Elf: +5% eff
            float eff1 = SimulateBuildingProductionCrewEfficiency(1.0f, 1, 0, 1, bName, meta);
            Assert(Math.Abs(eff1 - 1.05f) < 0.0001f, $"Building Production ({bName}, 1 Elf): +5% efficiency (1.05)", $"got {eff1}");

            // 2 Elves: +10% eff
            float eff2 = SimulateBuildingProductionCrewEfficiency(1.0f, 2, 0, 2, bName, meta);
            Assert(Math.Abs(eff2 - 1.10f) < 0.0001f, $"Building Production ({bName}, 2 Elves): +10% efficiency (1.10)", $"got {eff2}");

            // 3 Elves: +15% eff
            float eff3 = SimulateBuildingProductionCrewEfficiency(1.0f, 3, 0, 3, bName, meta);
            Assert(Math.Abs(eff3 - 1.15f) < 0.0001f, $"Building Production ({bName}, 3 Elves): +15% efficiency (1.15)", $"got {eff3}");
        }
    }

    private static void Test3_RacialSynergies_BuildingProduction_ElfBonusNonSawmillZero()
    {
        var meta = new MockMetaState();
        meta.unlocked.Add(MetaEffectType.ProductionSynergy_Elf);

        string[] nonEligibleBuildings = { "IronMine", "Kopalnia Żelaza", "StoneQuarry", "Smelter", "Hearth" };

        foreach (var bName in nonEligibleBuildings)
        {
            float eff = SimulateBuildingProductionCrewEfficiency(1.0f, 3, 0, 3, bName, meta);
            Assert(eff == 1.0f, $"Building Production ({bName}, 3 Elves): Non-sawmill/farm building receives strictly 0 bonus", $"got {eff}");
        }
    }

    private static void Test3_RacialSynergies_BuildingProduction_LockedUpgradesYieldZero()
    {
        var meta = new MockMetaState(); // Locked

        // 4 humans without ProductionSynergy_Human
        float effH = SimulateBuildingProductionCrewEfficiency(1.0f, 4, 4, 0, "Quarry", meta);
        Assert(effH == 1.0f, "Building Production (Locked Human Synergy): 4 humans receive 0 bonus");

        // 3 elves on Sawmill without ProductionSynergy_Elf
        float effE = SimulateBuildingProductionCrewEfficiency(1.0f, 3, 0, 3, "Sawmill", meta);
        Assert(effE == 1.0f, "Building Production (Locked Elf Synergy): 3 elves receive 0 bonus");
    }
}
