using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ToolWorkshopResearchTests
{
    private readonly List<Object> createdObjects = new List<Object>();
    private ToolWorkshopResearchManager researchManager;
    private WorkshopTechTreeCatalogSO catalog;

    [SetUp]
    public void SetUp()
    {
        var mgrGo = new GameObject("Test_ToolWorkshopResearchManager");
        createdObjects.Add(mgrGo);
        researchManager = mgrGo.AddComponent<ToolWorkshopResearchManager>();
        SetAutoPropertyOrField(typeof(ToolWorkshopResearchManager), null, "<Instance>k__BackingField", researchManager);

        catalog = ScriptableObject.CreateInstance<WorkshopTechTreeCatalogSO>();
        createdObjects.Add(catalog);
        researchManager.SetCatalogForTesting(catalog);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }
        createdObjects.Clear();
        SetAutoPropertyOrField(typeof(ToolWorkshopResearchManager), null, "<Instance>k__BackingField", null);
    }

    [Test]
    public void Formula_MatchesUserSpecification_ForActiveWorkshops0To4()
    {
        Assert.AreEqual(1, ToolWorkshopResearchManager.CalculateBaseDuration(1, 0));
        Assert.AreEqual(5, ToolWorkshopResearchManager.CalculateBaseDuration(2, 3));
        Assert.AreEqual(4 + 7, ToolWorkshopResearchManager.CalculateBaseDuration(4, 7));

        Assert.AreEqual(0f, ToolWorkshopResearchManager.CalculateDailyResearchSpeed(0), 0.0001f);
        Assert.AreEqual(1.00f, ToolWorkshopResearchManager.CalculateDailyResearchSpeed(1), 0.0001f);
        Assert.AreEqual(1.25f, ToolWorkshopResearchManager.CalculateDailyResearchSpeed(2), 0.0001f);
        Assert.AreEqual(1f + 0.25f * Mathf.Sqrt(2f), ToolWorkshopResearchManager.CalculateDailyResearchSpeed(3), 0.0001f);
        Assert.AreEqual(1f + 0.25f * Mathf.Sqrt(3f), ToolWorkshopResearchManager.CalculateDailyResearchSpeed(4), 0.0001f);

        float est1 = ToolWorkshopResearchManager.CalculateEstimatedDuration(2, 2, 1); // T_baza = 4, speed = 1 -> 4 dni
        Assert.AreEqual(4f, est1, 0.0001f);

        float est2 = ToolWorkshopResearchManager.CalculateEstimatedDuration(2, 2, 2); // T_baza = 4, speed = 1.25 -> 3.2 dnia
        Assert.AreEqual(3.2f, est2, 0.0001f);
    }

    [Test]
    public void WorkshopStaffing_Requires3Humans_RejectsOtherRaces_AndPausesWhenUnderstaffed()
    {
        var workshopData = CreateBuildingData("ToolWorkshop", BuildingType.Utility, maxWorkers: 3);
        var workshop = CreateWorkshop(workshopData);

        Assert.IsFalse(workshop.TryAddWorker(Race.Elves));
        Assert.IsFalse(workshop.TryAddWorker(Race.Dwarves));

        AssignHumanWorkersDirectly(workshop, 2);
        Assert.IsFalse(workshop.IsContributingToResearch);
        Assert.AreEqual(0, researchManager.ActiveFullyStaffedWorkshopsCount);

        AssignHumanWorkersDirectly(workshop, 3);
        Assert.IsTrue(workshop.IsContributingToResearch);
        Assert.AreEqual(1, researchManager.ActiveFullyStaffedWorkshopsCount);

        var farm = CreateBuildingData("Farm", BuildingType.Economic);
        var t1a = CreateUpgrade("Farm_T1_A", u => u.firstWorkerBonusMultiplier = 0.30f);
        var t1b = CreateUpgrade("Farm_T1_B");
        var t2a = CreateUpgrade("Farm_T2_A", u => u.extraSearchRadius = 1);
        var t2b = CreateUpgrade("Farm_T2_B");
        catalog.buildingTrees.Add(MakeTwoTierTree(farm, t1a, t1b, t2a, t2b));

        // T1 trwa 1 dzien -> po 1 ticku odblokowuje sie
        Assert.IsTrue(researchManager.TryStartResearch(farm, 1, true));
        researchManager.ProcessMorningTick();
        Assert.AreEqual(1, researchManager.DiscoveredUpgradesCount);
        Assert.IsTrue(researchManager.IsOptionResearched(farm, 1, true));

        // T2 z 1 odkrytym ulepszeniem: T_baza = 2 + 1 = 3 dni
        Assert.IsTrue(researchManager.TryStartResearch(farm, 2, true));
        researchManager.ProcessMorningTick(); // 1 / 3
        Assert.AreEqual(1f, researchManager.ActiveResearch.currentProgress, 0.001f);

        // Zejscie do 2/3 pracownikow pauzuje bez utraty postepu
        AssignHumanWorkersDirectly(workshop, 2);
        Assert.AreEqual(0, researchManager.ActiveFullyStaffedWorkshopsCount);
        researchManager.ProcessMorningTick();
        Assert.AreEqual(1f, researchManager.ActiveResearch.currentProgress, 0.001f);

        // Powrot do 3/3 i drugi warsztat -> speed = 1.25 -> po 2 tickach konczy (1 + 1.25 + 1.25 = 3.5 >= 3)
        AssignHumanWorkersDirectly(workshop, 3);
        var workshop2 = CreateWorkshop(workshopData);
        AssignHumanWorkersDirectly(workshop2, 3);
        Assert.AreEqual(2, researchManager.ActiveFullyStaffedWorkshopsCount);

        researchManager.ProcessMorningTick();
        Assert.IsTrue(researchManager.ActiveResearch.IsActive);
        researchManager.ProcessMorningTick();
        Assert.IsFalse(researchManager.ActiveResearch.IsActive);
        Assert.AreEqual(2, researchManager.DiscoveredUpgradesCount);
    }

    [Test]
    public void Research_EnforcesSequentialTiers_MutualExclusion_AndResetsOnSwitch()
    {
        var farm = CreateBuildingData("Farm", BuildingType.Economic);
        var mines = CreateBuildingData("Mines", BuildingType.Economic);

        var f1a = CreateUpgrade("Farm_T1_A");
        var f1b = CreateUpgrade("Farm_T1_B");
        var f2a = CreateUpgrade("Farm_T2_A");
        var f2b = CreateUpgrade("Farm_T2_B");
        var m1a = CreateUpgrade("Mines_T1_A");
        var m1b = CreateUpgrade("Mines_T1_B");

        catalog.buildingTrees.Add(MakeTwoTierTree(farm, f1a, f1b, f2a, f2b));
        catalog.buildingTrees.Add(MakeTwoTierTree(mines, m1a, m1b, null, null));

        var workshop = CreateWorkshop(CreateBuildingData("ToolWorkshop", BuildingType.Utility, 3));
        AssignHumanWorkersDirectly(workshop, 3);

        // Nie mozna zaczac T2 przed T1
        Assert.IsFalse(researchManager.TryStartResearch(farm, 2, true));

        // Zaczynamy T1 A dla Farmy
        Assert.IsTrue(researchManager.TryStartResearch(farm, 1, true));
        researchManager.ProcessMorningTick();
        Assert.IsTrue(researchManager.IsOptionResearched(farm, 1, true));

        // Opcja B z T1 jest teraz zablokowana na stale
        Assert.IsTrue(researchManager.IsTierLockedByAlternative(farm, 1, false));
        Assert.IsFalse(researchManager.TryStartResearch(farm, 1, false));

        // T2 jest teraz dostepne; zmiana badania na Kopalnie zeruje postep czastkowy
        Assert.IsTrue(researchManager.TryStartResearch(farm, 2, true)); // T_baza = 3
        researchManager.ProcessMorningTick();
        Assert.AreEqual(1f, researchManager.ActiveResearch.currentProgress, 0.001f);

        Assert.IsTrue(researchManager.TryStartResearch(mines, 1, false));
        Assert.AreEqual(0f, researchManager.ActiveResearch.currentProgress, 0.001f);
        Assert.AreEqual("Mines", researchManager.ActiveResearch.buildingKey);
    }

    [Test]
    public void UnlockedUpgrades_ApplyToExistingAndFutureBuildings_AndT4CrossSystems()
    {
        var retort = CreateBuildingData("Retort", BuildingType.Economic, upkeepWood: 4);
        var r1a = CreateUpgrade("Retort_T1_A", u => u.upkeepReduction.Add(new ResourceCost { type = ResourceType.Wood, amount = 3 }));
        var r1b = CreateUpgrade("Retort_T1_B");
        var r2a = CreateUpgrade("Retort_T2_A", u => u.extraWorkersPerShift = 1);
        var r2b = CreateUpgrade("Retort_T2_B");
        var r3a = CreateUpgrade("Retort_T3_A", u => u.upkeepReduction.Add(new ResourceCost { type = ResourceType.Wood, amount = 3 }));
        var r3b = CreateUpgrade("Retort_T3_B");
        var r4a = CreateUpgrade("Retort_T4_A", u => u.specialEffectIDs.Add("RETORT_REDUCE_TOWER_UPKEEP"));
        var r4b = CreateUpgrade("Retort_T4_B");

        catalog.buildingTrees.Add(new WorkshopBuildingTree
        {
            targetBuilding = retort,
            tiers = new List<WorkshopTierEntry>
            {
                new WorkshopTierEntry { tier = 1, optionA = r1a, optionB = r1b },
                new WorkshopTierEntry { tier = 2, optionA = r2a, optionB = r2b },
                new WorkshopTierEntry { tier = 3, optionA = r3a, optionB = r3b },
                new WorkshopTierEntry { tier = 4, optionA = r4a, optionB = r4b },
            }
        });

        // Istniejacy budynek przed badaniem
        var existingRetort = CreateBuildingEntity(retort);
        Assert.AreEqual(4f, existingRetort.GetEffectiveUpkeep()[0].amount, 0.001f);

        var workshop = CreateWorkshop(CreateBuildingData("ToolWorkshop", BuildingType.Utility, 3));
        AssignHumanWorkersDirectly(workshop, 3);

        // Odblokuj T1 A (-3 Drewna -> z 4 na 1)
        researchManager.TryStartResearch(retort, 1, true);
        researchManager.ProcessMorningTick();
        Assert.AreEqual(1f, existingRetort.GetEffectiveUpkeep()[0].amount, 0.001f);

        // Nowo postawiony budynek po badaniu dziedziczy ulepszenie od razu
        var futureRetort = CreateBuildingEntity(retort);
        Assert.AreEqual(1f, futureRetort.GetEffectiveUpkeep()[0].amount, 0.001f);

        // T2 A (+1 slot), T3 A (kolejne -3 Drewna -> floor na 0, brak kosztu ujemnego!)
        researchManager.TryStartResearch(retort, 2, true);
        for (int i = 0; i < 3; i++) researchManager.ProcessMorningTick();
        researchManager.TryStartResearch(retort, 3, true);
        for (int i = 0; i < 5; i++) researchManager.ProcessMorningTick();

        Assert.AreEqual(0, futureRetort.GetEffectiveUpkeep().Count);

        // T4 A (Retorta: -25% kosztow wiez przy 1 aktywnym budynku, -40% przy >= 2)
        researchManager.TryStartResearch(retort, 4, true);
        for (int i = 0; i < 7; i++) researchManager.ProcessMorningTick();

        // 2 aktywne retorty -> 0.60f mnoznik kosztow wiezy
        Assert.AreEqual(0.60f, researchManager.GetTowerNightUpkeepMultiplier(), 0.001f);

        var towerData = CreateBuildingData("Rune tower", BuildingType.Defense, upkeepWood: 10);
        var effectiveTowerUpkeep = TowerEntity.GetEffectiveTowerUpkeep(towerData);
        Assert.AreEqual(6, effectiveTowerUpkeep[0].amount);
    }

    [Test]
    public void GateEntity_WorkshopReinforcement_CapsBonusMaxHp()
    {
        var gateGo = new GameObject("Test_Gate");
        createdObjects.Add(gateGo);
        var gate = gateGo.AddComponent<GateEntity>();

        float baseMax = gate.MaxHP;
        gate.ApplyWorkshopReinforcement(15f, 100f);
        Assert.AreEqual(baseMax + 15f, gate.MaxHP, 0.001f);

        for (int i = 0; i < 10; i++)
        {
            gate.ApplyWorkshopReinforcement(15f, 100f);
        }
        Assert.AreEqual(baseMax + 100f, gate.MaxHP, 0.001f);
    }

    private BuildingData CreateBuildingData(string name, BuildingType type, int maxWorkers = 2, int upkeepWood = 0)
    {
        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.name = name;
        data.buildingName = name;
        data.type = type;
        data.baseMaxWorkers = maxWorkers;
        data.constructionCost = new List<ResourceCost>();
        data.productionPerCycle = new List<ResourceCost>();
        data.upkeepPerCycle = new List<ResourceCost>();
        if (upkeepWood > 0f)
        {
            data.upkeepPerCycle.Add(new ResourceCost { type = ResourceType.Wood, amount = upkeepWood });
        }
        createdObjects.Add(data);
        return data;
    }

    private BuildingUpgradeSO CreateUpgrade(string name, System.Action<BuildingUpgradeSO> configure = null)
    {
        var so = ScriptableObject.CreateInstance<BuildingUpgradeSO>();
        so.name = name;
        so.upgradeName = name;
        so.productionBonus = new List<ResourceCost>();
        so.upkeepIncrease = new List<ResourceCost>();
        so.upkeepReduction = new List<ResourceCost>();
        so.terrainFlatBonus = 0f;
        so.specialEffectIDs = new List<string>();
        so.nextTierOptions = new List<BuildingUpgradeSO>();
        configure?.Invoke(so);
        createdObjects.Add(so);
        return so;
    }

    private WorkshopBuildingTree MakeTwoTierTree(
        BuildingData target,
        BuildingUpgradeSO t1a, BuildingUpgradeSO t1b,
        BuildingUpgradeSO t2a, BuildingUpgradeSO t2b)
    {
        var tree = new WorkshopBuildingTree { targetBuilding = target };
        tree.tiers.Add(new WorkshopTierEntry { tier = 1, optionA = t1a, optionB = t1b });
        if (t2a != null || t2b != null)
        {
            tree.tiers.Add(new WorkshopTierEntry { tier = 2, optionA = t2a, optionB = t2b });
        }
        return tree;
    }

    private ToolWorkshopEntity CreateWorkshop(BuildingData data)
    {
        var go = new GameObject("Test_Workshop");
        createdObjects.Add(go);
        var workshop = go.AddComponent<ToolWorkshopEntity>();
        workshop.Initialize(data);
        return workshop;
    }

    private BuildingEntity CreateBuildingEntity(BuildingData data)
    {
        var go = new GameObject("Test_" + data.name);
        createdObjects.Add(go);
        var entity = go.AddComponent<BuildingEntity>();
        entity.Initialize(data);
        return entity;
    }

    private void AssignHumanWorkersDirectly(ToolWorkshopEntity workshop, int count)
    {
        var workersField = typeof(BuildingWorkerComponent).GetField("assignedWorkers", BindingFlags.Instance | BindingFlags.NonPublic);
        var list = (List<Citizen>)workersField.GetValue(workshop.Workers);
        list.Clear();
        for (int i = 0; i < count; i++)
        {
            list.Add(new Citizen { race = Race.Humans });
        }
        researchManager.NotifyWorkshopsChanged();
    }

    private static void SetAutoPropertyOrField(System.Type type, object instance, string fieldName, object value)
    {
        var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(instance, value);
        }
    }
}
