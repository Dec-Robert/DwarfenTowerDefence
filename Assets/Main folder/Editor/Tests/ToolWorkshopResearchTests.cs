using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        SetAutoPropertyOrField(typeof(ResourceManager), null, "<Instance>k__BackingField", null);
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

    [Test]
    public void ResearchCost_ChecksAffordability_SpendsOnStart_AndRefundsProportionallyOnCancel()
    {
        var rm = CreateResourceManager();
        rm.AddResource(ResourceType.Wood, 15);
        rm.AddResource(ResourceType.Iron, 5);

        var farm = CreateBuildingData("Farm", BuildingType.Economic);
        var t1a = CreateUpgrade("Farm_T1_A", u =>
        {
            u.cost = new List<ResourceCost>
            {
                new ResourceCost { type = ResourceType.Wood, amount = 10 },
                new ResourceCost { type = ResourceType.Iron, amount = 5 }
            };
        });
        var t1b = CreateUpgrade("Farm_T1_B");
        var t2a = CreateUpgrade("Farm_T2_A", u =>
        {
            u.cost = new List<ResourceCost>
            {
                new ResourceCost { type = ResourceType.Wood, amount = 10 },
                new ResourceCost { type = ResourceType.Iron, amount = 3 }
            };
        });
        var t2b = CreateUpgrade("Farm_T2_B");
        catalog.buildingTrees.Add(MakeTwoTierTree(farm, t1a, t1b, t2a, t2b));

        var workshop = CreateWorkshop(CreateBuildingData("ToolWorkshop", BuildingType.Utility, 3));
        AssignHumanWorkersDirectly(workshop, 3);

        // T1A kosztuje 10 Wood, 5 Iron -> przechodzi, zostaje 5 Wood, 0 Iron
        Assert.IsTrue(researchManager.TryStartResearch(farm, 1, true));
        Assert.AreEqual(5, rm.GetResourceAmount(ResourceType.Wood));
        Assert.AreEqual(0, rm.GetResourceAmount(ResourceType.Iron));

        // Konczymy T1A (1 dzien) -> odblokowane = 1, wiec T2 ma bazowy czas 2 + 1 = 3 dni
        researchManager.ProcessMorningTick();
        Assert.IsTrue(researchManager.IsOptionResearched(farm, 1, true));

        // Nie stac nas na T2A (potrzeba 10 Wood, 3 Iron)
        Assert.IsFalse(researchManager.CanAffordUpgrade(t2a));
        Assert.IsFalse(researchManager.TryStartResearch(farm, 2, true));

        // Dodajemy surowce i uruchamiamy T2A (wymaga 3 dni, koszt 10 Wood, 3 Iron)
        rm.AddResource(ResourceType.Wood, 5);
        rm.AddResource(ResourceType.Iron, 3);
        Assert.IsTrue(researchManager.TryStartResearch(farm, 2, true));
        Assert.AreEqual(0, rm.GetResourceAmount(ResourceType.Wood));
        Assert.AreEqual(0, rm.GetResourceAmount(ResourceType.Iron));

        // Po 1 dniu postep = 1/3 -> zwrot: 100 - (100/3 * 1) = 66.66% -> Floor(10 * 2/3) = 6 Wood, Floor(3 * 2/3) = 2 Iron
        researchManager.ProcessMorningTick();
        Assert.AreEqual(2f / 3f, researchManager.GetCurrentCancelRefundRatio(), 0.001f);
        researchManager.CancelCurrentResearch();

        Assert.IsNull(researchManager.ActiveResearch);
        Assert.AreEqual(6, rm.GetResourceAmount(ResourceType.Wood));
        Assert.AreEqual(2, rm.GetResourceAmount(ResourceType.Iron));
    }

    [Test]
    public void ToolWorkshopWindowUI_HidesFifthRow_DisplaysFlavourAndEffects_AndDisablesBtnWhenMissingCost()
    {
        var rm = CreateResourceManager();
        rm.AddResource(ResourceType.Wood, 5); // za malo na T1A (koszt 20)

        var farm = CreateBuildingData("Farm", BuildingType.Economic);
        var mines = CreateBuildingData("Mines", BuildingType.Economic);

        var f1a = CreateUpgrade("Farm_T1_A", u =>
        {
            u.flavourDescription = "Stare zaprzegi przyspieszaja orke.";
            u.firstWorkerBonusMultiplier = 0.30f;
            u.extraSearchRadius = 1;
            u.cost = new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 20 } };
        });
        var f1b = CreateUpgrade("Farm_T1_B", u =>
        {
            u.terrainFlatBonus = 2f;
        });
        var m1a = CreateUpgrade("Mines_T1_A", u => u.specialEffectIDs.Add("MINE_MOUNTAIN_CHAIN"));
        var m1b = CreateUpgrade("Mines_T1_B");

        catalog.buildingTrees.Add(MakeTwoTierTree(farm, f1a, f1b, null, null));
        catalog.buildingTrees.Add(MakeTwoTierTree(mines, m1a, m1b, null, null));

        var workshop = CreateWorkshop(CreateBuildingData("ToolWorkshop", BuildingType.Utility, 3));
        AssignHumanWorkersDirectly(workshop, 2);

        var ui = BuildTestWorkshopWindowUI(out var tabButtons, out var rowRoots, out var rowOptA, out var rowOptB,
            out var txtSelectedBuilding, out var txtProgress, out var txtRequiredWorkers,
            out var pnlDescription, out var txtDescription, out var txtEffects,
            out var txtCostsHeader, out var pnlCostsInner, out var txtTime, out var btnResearch, out var txtBtnLabel);

        ui.Setup(workshop);

        // Drzewo ma 2 tiery -> wiersze 0..1 aktywne, wiersze 2..4 ukryte
        Assert.IsTrue(rowRoots[0].activeSelf);
        Assert.IsTrue(rowRoots[1].activeSelf);
        Assert.IsFalse(rowRoots[2].activeSelf);
        Assert.IsFalse(rowRoots[3].activeSelf);
        Assert.IsFalse(rowRoots[4].activeSelf);

        Assert.AreEqual("Farm", txtSelectedBuilding.text);
        Assert.AreEqual("Ukończone 0/2", txtProgress.text);
        StringAssert.Contains("Nieaktywny (2/3)", txtRequiredWorkers.text);

        // F1A ma flavourDescription, punkty efektow i koszt
        Assert.IsTrue(pnlDescription.activeSelf);
        Assert.AreEqual("Stare zaprzegi przyspieszaja orke.", txtDescription.text);
        StringAssert.Contains("+30% wydajności 1. pracownika", txtEffects.text);
        StringAssert.Contains("+1 do zasięgu skanowania terenu", txtEffects.text);
        Assert.IsTrue(txtCostsHeader.gameObject.activeSelf);
        Assert.IsTrue(pnlCostsInner.gameObject.activeSelf);

        // Brak surowcow -> przycisk wyszarzony i nieaktywny
        Assert.IsFalse(btnResearch.interactable);
        Assert.AreEqual("Brak surowców", txtBtnLabel.text);
        Assert.Less(btnResearch.GetComponent<Image>().color.r, 0.7f);

        // Klikniecie F1B (bez flavour i bez kosztu) -> ukrywa opis i koszty, odblokowuje przycisk
        rowOptB[0].onClick.Invoke();
        Assert.IsFalse(pnlDescription.activeSelf);
        Assert.IsFalse(txtCostsHeader.gameObject.activeSelf);
        Assert.IsFalse(pnlCostsInner.gameObject.activeSelf);
        StringAssert.Contains("+2 produkcji za każde pasujące pole w zasięgu", txtEffects.text);
        Assert.IsTrue(btnResearch.interactable);
        Assert.AreEqual("Opracuj technologię", txtBtnLabel.text);

        // Przelaczenie na zakladke Mines
        tabButtons[1].onClick.Invoke();
        Assert.AreEqual("Mines", txtSelectedBuilding.text);
        StringAssert.Contains("pasma górskiego", txtEffects.text);
    }

    [Test]
    public void ToolWorkshopWindowUI_ActiveResearchKeepsCancelButtonAcrossPreviews_AndCancelsWithRefund()
    {
        var rm = CreateResourceManager();
        rm.AddResource(ResourceType.Wood, 10);

        var farm = CreateBuildingData("Farm", BuildingType.Economic);
        var mines = CreateBuildingData("Mines", BuildingType.Economic);

        var f1a = CreateUpgrade("Farm_T1_A", u =>
        {
            u.cost = new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 10 } };
        });
        var f1b = CreateUpgrade("Farm_T1_B");
        var f2a = CreateUpgrade("Farm_T2_A", u =>
        {
            u.cost = new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 10 } };
        });
        var f2b = CreateUpgrade("Farm_T2_B");
        var m1a = CreateUpgrade("Mines_T1_A");
        var m1b = CreateUpgrade("Mines_T1_B");

        catalog.buildingTrees.Add(MakeTwoTierTree(farm, f1a, f1b, f2a, f2b));
        catalog.buildingTrees.Add(MakeTwoTierTree(mines, m1a, m1b, null, null));

        var workshop = CreateWorkshop(CreateBuildingData("ToolWorkshop", BuildingType.Utility, 3));
        AssignHumanWorkersDirectly(workshop, 3);

        var ui = BuildTestWorkshopWindowUI(out var tabButtons, out _, out var rowOptA, out _,
            out _, out _, out _, out _, out _, out _, out _, out _, out _, out var btnResearch, out var txtBtnLabel);

        ui.Setup(workshop);

        // Start F1A (10 Wood, 1 dzien) i ukoncz -> odblokowane 1
        btnResearch.onClick.Invoke();
        Assert.AreEqual(0, rm.GetResourceAmount(ResourceType.Wood));
        researchManager.ProcessMorningTick();
        Assert.IsTrue(researchManager.IsOptionResearched(farm, 1, true));

        // Dodaj 10 Wood i zacznij F2A (wymaga 2 + 1 = 3 dni)
        rm.AddResource(ResourceType.Wood, 10);
        rowOptA[1].onClick.Invoke();
        btnResearch.onClick.Invoke();
        Assert.IsNotNull(researchManager.ActiveResearch);
        Assert.AreEqual(0, rm.GetResourceAmount(ResourceType.Wood));

        // Przelacz podglad na Mines T1A -> przycisk nadal pozwala anulowac aktywne badanie
        tabButtons[1].onClick.Invoke();
        Assert.IsTrue(btnResearch.interactable);
        StringAssert.Contains("Anuluj badanie", txtBtnLabel.text);

        // Anuluj przy 0 postepie -> 100% zwrotu (10 Wood) i przycisk wraca do trybu odkrywania wybranego Mines T1A
        btnResearch.onClick.Invoke();
        Assert.IsNull(researchManager.ActiveResearch);
        Assert.AreEqual(10, rm.GetResourceAmount(ResourceType.Wood));
        Assert.AreEqual("Opracuj technologię", txtBtnLabel.text);
    }

    private ResourceManager CreateResourceManager()
    {
        var cfg = ScriptableObject.CreateInstance<CityBaseConfigSO>();
        createdObjects.Add(cfg);

        var rmGo = new GameObject("Test_ResourceManager");
        createdObjects.Add(rmGo);
        rmGo.SetActive(false);
        var rm = rmGo.AddComponent<ResourceManager>();
        rm.cityConfig = cfg;
        rmGo.SetActive(true);
        return rm;
    }

    private ToolWorkshopWindowUI BuildTestWorkshopWindowUI(
        out List<Button> tabButtons,
        out List<GameObject> rowRoots,
        out List<Button> rowOptA,
        out List<Button> rowOptB,
        out TMP_Text txtSelectedBuilding,
        out TMP_Text txtProgress,
        out TMP_Text txtRequiredWorkers,
        out GameObject pnlDescription,
        out TMP_Text txtDescription,
        out TMP_Text txtEffects,
        out TMP_Text txtCostsHeader,
        out Transform pnlCostsInner,
        out TMP_Text txtTime,
        out Button btnResearch,
        out TMP_Text txtBtnLabel)
    {
        var root = new GameObject("Pnl_ToolWorkshopWindow");
        createdObjects.Add(root);

        var secHeader = CreateChild(root.transform, "Sec_Header");
        CreateTextChild(secHeader, "Txt_Title");
        CreateButtonChild(secHeader, "Btn_Close");

        var secTabs = CreateChild(root.transform, "Sec_BuildingsTab");
        tabButtons = new List<Button>();
        for (int i = 0; i < 5; i++)
        {
            var tab = CreateChild(secTabs, "Pnl_BuildingTab");
            tab.gameObject.AddComponent<Image>();
            tabButtons.Add(tab.gameObject.AddComponent<Button>());
            CreateChild(tab, "Img_Icon").gameObject.AddComponent<Image>();
            CreateTextChild(tab, "Txt_BuildingName");
        }

        var secFooter = CreateChild(root.transform, "Sec_Footer");
        var pnlResearch = CreateChild(secFooter, "Pnl_Research");
        var pnlResearchInfo = CreateChild(pnlResearch, "Pnl_Research_info");
        CreateTextChild(pnlResearchInfo, "Txt_Flavour");
        txtSelectedBuilding = CreateTextChild(pnlResearchInfo, "Txt_SelectedBuilding");
        txtProgress = CreateTextChild(pnlResearchInfo, "Txt_Progress");

        var pnlUpgradeTabs = CreateChild(pnlResearch, "Pnl_UpgradeTabs");
        rowRoots = new List<GameObject>();
        rowOptA = new List<Button>();
        rowOptB = new List<Button>();
        for (int i = 0; i < 5; i++)
        {
            var row = CreateChild(pnlUpgradeTabs, $"Pnl_SingleUpgradeRow_{i}");
            rowRoots.Add(row.gameObject);
            CreateTextChild(row, "Txt_TierNumber");
            rowOptA.Add(CreateUpgradeSlotChild(row, "Pnl_UpgradeA"));
            rowOptB.Add(CreateUpgradeSlotChild(row, "Pnl_UpgradeB"));
        }

        var pnlInfo = CreateChild(secFooter, "Pnl_Info");
        var workerGo = CreateChild(pnlInfo, "Pnl_RaceControllersSingle Variant");
        txtRequiredWorkers = CreateTextChild(workerGo, "Txt_RequiredWorkers");

        var pnlUpgradeInfo = CreateChild(pnlInfo, "Pnl_UpgradeInfo");
        var pnlHeader = CreateChild(pnlUpgradeInfo, "Pnl_Header");
        CreateChild(CreateChild(pnlHeader, "Img_Boarder"), "Img_Icon").gameObject.AddComponent<Image>();
        CreateTextChild(pnlHeader, "Txt_Name");

        pnlDescription = CreateChild(pnlUpgradeInfo, "Pnl_Description").gameObject;
        txtDescription = CreateTextChild(pnlDescription.transform, "Txt_Description");

        var pnlEffect = CreateChild(pnlUpgradeInfo, "Pnl_Effect");
        txtEffects = CreateTextChild(pnlEffect.transform, "Txt_EffectDesciptiorn");

        var pnlCostsOuter = CreateChild(pnlUpgradeInfo, "Pnl_Costs");
        txtCostsHeader = CreateTextChild(pnlCostsOuter, "Txt_Costs");
        pnlCostsInner = CreateChild(pnlCostsOuter, "Pnl_Costs");
        for (int i = 0; i < 3; i++)
        {
            var resGo = CreateChild(pnlCostsInner, $"Pnl_SingleResource_Small_{i}");
            var amountTmp = CreateTextChild(resGo, "Txt_Amount");
            var iconImg = CreateChild(resGo, "Img_Icon").gameObject.AddComponent<Image>();
            var singleRes = resGo.gameObject.AddComponent<SingleResourceUI>();
            SetAutoPropertyOrField(typeof(SingleResourceUI), singleRes, "ammount", amountTmp);
            SetAutoPropertyOrField(typeof(SingleResourceUI), singleRes, "icon", iconImg);
        }

        CreateChild(pnlCostsOuter, "Divider");
        txtTime = CreateTextChild(pnlCostsOuter, "Txt_Time");
        btnResearch = CreateButtonChild(pnlCostsOuter, "Btn_Research");
        btnResearch.gameObject.AddComponent<Image>();
        txtBtnLabel = CreateTextChild(btnResearch.transform, "Text (TMP)");

        return root.AddComponent<ToolWorkshopWindowUI>();
    }

    private static Transform CreateChild(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static TMP_Text CreateTextChild(Transform parent, string name)
    {
        var tr = CreateChild(parent, name);
        return tr.gameObject.AddComponent<TextMeshProUGUI>();
    }

    private static Button CreateButtonChild(Transform parent, string name)
    {
        var tr = CreateChild(parent, name);
        return tr.gameObject.AddComponent<Button>();
    }

    private static Button CreateUpgradeSlotChild(Transform parent, string name)
    {
        var tr = CreateChild(parent, name);
        tr.gameObject.AddComponent<Image>();
        var btn = tr.gameObject.AddComponent<Button>();
        CreateChild(tr, "Img_Icon").gameObject.AddComponent<Image>();
        CreateTextChild(tr, "Txt_UpgradeName");
        return btn;
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
