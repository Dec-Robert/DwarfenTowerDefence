using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[TestFixture]
public class DuskEconomyCycleTests
{
    private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();
    private TimePhaseManager timeManager;
    private ResourceManager resourceManager;
    private PopLifeCycleManager popManager;
    private WorkforceManager workforceManager;
    private BuildingRegistry registry;

    [SetUp]
    public void SetUp()
    {
        var timeGo = Track(new GameObject("TestTimePhaseManager"));
        timeManager = timeGo.AddComponent<TimePhaseManager>();
        timeManager.OnGameSpeedChange += _ => { };
        InvokePrivate(timeManager, "Start");

        var cityConfig = Track(ScriptableObject.CreateInstance<CityBaseConfigSO>());
        cityConfig.startEther = 0;
        cityConfig.startWood = 0;
        cityConfig.startStone = 0;
        cityConfig.startIron = 0;
        cityConfig.startCoal = 0;
        cityConfig.startFood = 0;
        cityConfig.overrideHousingData = false;

        var rmGo = Track(new GameObject("TestResourceManager"));
        rmGo.SetActive(false);
        resourceManager = rmGo.AddComponent<ResourceManager>();
        resourceManager.cityConfig = cityConfig;
        rmGo.SetActive(true);

        var popGo = Track(new GameObject("TestPopLifeCycleManager"));
        popManager = popGo.AddComponent<PopLifeCycleManager>();

        var wfGo = Track(new GameObject("TestWorkforceManager"));
        workforceManager = wfGo.AddComponent<WorkforceManager>();
        InvokePrivate(workforceManager, "Start");

        var regGo = Track(new GameObject("TestBuildingRegistry"));
        registry = regGo.AddComponent<BuildingRegistry>();
        InvokePrivate(registry, "Start");
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }
        }
        createdObjects.Clear();
        typeof(BeaconEntity).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
    }

    private T Track<T>(T obj) where T : UnityEngine.Object
    {
        createdObjects.Add(obj);
        return obj;
    }

    private static void InvokePrivate(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(target, null);
    }

    private BuildingData CreateEconomicData(
        string assetName,
        string displayName,
        List<ResourceCost> prod,
        List<ResourceCost> upkeep = null,
        EconomicPhaseStep step = EconomicPhaseStep.Auto)
    {
        var data = Track(ScriptableObject.CreateInstance<BuildingData>());
        data.name = assetName;
        data.buildingName = displayName;
        data.type = BuildingType.Economic;
        data.baseMaxWorkers = 2;
        data.efficiencyTable = new List<float> { 0f, 1f, 1.25f };
        data.productionPerCycle = prod ?? new List<ResourceCost>();
        data.upkeepPerCycle = upkeep ?? new List<ResourceCost>();
        data.duskExecutionStep = step;
        return data;
    }

    private BuildingEntity CreateAndStaffEconomic(BuildingData data, int workers = 1, Race race = Race.Humans, BuildingPriority priority = BuildingPriority.Normal)
    {
        var go = Track(new GameObject($"Entity_{data.name}"));
        var entity = go.AddComponent<BuildingEntity>();
        entity.Initialize(data);
        entity.upkeepPriority = priority;
        for (int i = 0; i < workers; i++)
        {
            popManager.BirthCitizen(race, null);
            Assert.IsTrue(entity.TryAddWorker(race));
        }
        return entity;
    }

    private HousingEntity CreateHousing(
        string name,
        int initialResidents,
        int maxResidents,
        int foodPerResident,
        int growthFoodCost,
        int[] growthCycles = null,
        BuildingPriority priority = BuildingPriority.Normal)
    {
        var data = Track(ScriptableObject.CreateInstance<HousingBuildingData>());
        data.name = name;
        data.buildingName = name;
        data.type = BuildingType.Housing;
        data.housingRace = Race.Humans;
        data.initialResidents = initialResidents;
        data.maxResidents = maxResidents;
        data.growthCycles = growthCycles ?? new[] { 2, 2, 2, 2, 2 };
        data.upkeepPerResident = new List<HousingBuildingData.PerCapitaCost>
        {
            new HousingBuildingData.PerCapitaCost { type = ResourceType.Food, amount = foodPerResident }
        };
        data.growthSurplusCost = new List<ResourceCost>
        {
            new ResourceCost { type = ResourceType.Food, amount = growthFoodCost }
        };

        var go = Track(new GameObject($"Housing_{name}"));
        var housing = go.AddComponent<HousingEntity>();
        housing.Initialize(data);
        housing.upkeepPriority = priority;
        return housing;
    }

    [Test]
    public void Test01_NightToMorning_ResetsWorkers_WithoutHousingConsumptionOrGrowth()
    {
        var sawmillData = CreateEconomicData("Sawmill", "Tartak",
            new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 10 } });
        var sawmill = CreateAndStaffEconomic(sawmillData, workers: 1);

        var housing = CreateHousing("Human house", initialResidents: 1, maxResidents: 3, foodPerResident: 3, growthFoodCost: 6);
        resourceManager.AddResource(ResourceType.Food, 20);

        // Przechodzimy Morning -> Dusk (rozliczenie Dusk + zablokowanie pracowników)
        timeManager.ChangeCurrentPhase();
        Assert.AreEqual(TimePhases.Dusk, timeManager.CurrentTimePhase);
        Assert.AreEqual(WorkState.Locked, sawmill.GetAssignedCitizens()[0].workState);
        Assert.AreEqual(11, resourceManager.GetResourceAmount(ResourceType.Food)); // 20 - 3 survival - 6 growth
        Assert.AreEqual(1, housing.currentGrowthProgress);

        // Przechodzimy Dusk -> Night -> Morning
        timeManager.ChangeCurrentPhase();
        Assert.AreEqual(TimePhases.Night, timeManager.CurrentTimePhase);
        timeManager.ChangeCurrentPhase();
        Assert.AreEqual(TimePhases.Morning, timeManager.CurrentTimePhase);

        // W Morning: pracownik odblokowany, ale jedzenie i wzrost nie zmieniły się od Dusk
        Assert.AreEqual(WorkState.Assigned, sawmill.GetAssignedCitizens()[0].workState);
        Assert.AreEqual(11, resourceManager.GetResourceAmount(ResourceType.Food));
        Assert.AreEqual(1, housing.currentGrowthProgress);
    }

    [Test]
    public void Test02_MorningToDusk_ExecutesStrictFiveStepOrder_FromZeroTreasury()
    {
        // Krok 1: Tartak produkuje 30 Wood (0 upkeep)
        var sawmillData = CreateEconomicData("Sawmill", "Tartak",
            new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 30 } });
        CreateAndStaffEconomic(sawmillData);

        // Krok 2: Farma (-4 Wood -> +10 Food), Kamieniołom (-2 Wood -> +6 Stone), Retorta (-6 Wood -> +8 Coal)
        // Łącznie Krok 2 pobiera 12 Wood (zostaje 18 Wood)

        var farmData = CreateEconomicData("Farm", "Farm",
            new List<ResourceCost> { new ResourceCost { type = ResourceType.Food, amount = 10 } },
            new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 4 } });
        CreateAndStaffEconomic(farmData);

        var quarryData = CreateEconomicData("Quarry", "Quarry",
            new List<ResourceCost> { new ResourceCost { type = ResourceType.Stone, amount = 6 } },
            new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 2 } });
        CreateAndStaffEconomic(quarryData);

        var retortData = CreateEconomicData("Retort", "Retorta",
            new List<ResourceCost> { new ResourceCost { type = ResourceType.Coal, amount = 8 } },
            new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 6 } });
        CreateAndStaffEconomic(retortData);

        // Krok 3: Kopalnia (-14 Wood z Kroku 1, -4 Stone z Kroku 2 -> +6 Coal, +3 Iron)
        var minesData = CreateEconomicData("Mines", "Mines",
            new List<ResourceCost>
            {
                new ResourceCost { type = ResourceType.Coal, amount = 6 },
                new ResourceCost { type = ResourceType.Iron, amount = 3 }
            },
            new List<ResourceCost>
            {
                new ResourceCost { type = ResourceType.Wood, amount = 14 },
                new ResourceCost { type = ResourceType.Stone, amount = 4 }
            });
        CreateAndStaffEconomic(minesData);

        // Krok 4: Inny budynek Economic (-2 Iron z Kroku 3, -4 Coal z Kroków 2-3 -> +5 Ether)
        var alchemyData = CreateEconomicData("Alchemist", "Alchemik",
            new List<ResourceCost> { new ResourceCost { type = ResourceType.Ether, amount = 5 } },
            new List<ResourceCost>
            {
                new ResourceCost { type = ResourceType.Iron, amount = 2 },
                new ResourceCost { type = ResourceType.Coal, amount = 4 }
            });
        CreateAndStaffEconomic(alchemyData);

        // Krok 5: Dom (1 mieszkaniec: -3 Food survival z Kroku 2, -6 Food growth z Kroku 2)
        var housing = CreateHousing("Human house", initialResidents: 1, maxResidents: 4, foodPerResident: 3, growthFoodCost: 6);

        // Start z 0 wszystkiego w skarbcu
        Assert.AreEqual(0, resourceManager.GetResourceAmount(ResourceType.Wood));
        Assert.AreEqual(0, resourceManager.GetResourceAmount(ResourceType.Stone));
        Assert.AreEqual(0, resourceManager.GetResourceAmount(ResourceType.Food));
        Assert.AreEqual(0, resourceManager.GetResourceAmount(ResourceType.Coal));
        Assert.AreEqual(0, resourceManager.GetResourceAmount(ResourceType.Iron));
        Assert.AreEqual(0, resourceManager.GetResourceAmount(ResourceType.Ether));

        // Przejście Morning -> Dusk
        timeManager.ChangeCurrentPhase();

        // Sprawdzenie dokładnych bilansów końcowych:
        // Wood: +30 - 4 - 2 - 6 - 14 = 4
        // Stone: +6 - 4 = 2
        // Coal: +8 + 6 - 4 = 10
        // Iron: +3 - 2 = 1
        // Ether: +5
        // Food: +10 - 3 - 6 = 1
        Assert.AreEqual(4, resourceManager.GetResourceAmount(ResourceType.Wood));
        Assert.AreEqual(2, resourceManager.GetResourceAmount(ResourceType.Stone));
        Assert.AreEqual(10, resourceManager.GetResourceAmount(ResourceType.Coal));
        Assert.AreEqual(1, resourceManager.GetResourceAmount(ResourceType.Iron));
        Assert.AreEqual(5, resourceManager.GetResourceAmount(ResourceType.Ether));
        Assert.AreEqual(1, resourceManager.GetResourceAmount(ResourceType.Food));
        Assert.AreEqual(1, housing.currentGrowthProgress);
    }

    [Test]
    public void Test03_SawmillWithWorkshopUpkeepUpgrade_PaysUpkeepInStep1BeforeProducing()
    {
        var sawmillData = CreateEconomicData("Sawmill", "Tartak",
            new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 10 } });
        var sawmill = CreateAndStaffEconomic(sawmillData);

        var upgrade = Track(ScriptableObject.CreateInstance<BuildingUpgradeSO>());
        upgrade.upkeepIncrease = new List<ResourceCost>
        {
            new ResourceCost { type = ResourceType.Coal, amount = 3 }
        };
        sawmill.ApplyUpgrade(upgrade);

        resourceManager.AddResource(ResourceType.Coal, 5);

        timeManager.ChangeCurrentPhase(); // Morning -> Dusk

        Assert.AreEqual(2, resourceManager.GetResourceAmount(ResourceType.Coal));
        Assert.AreEqual(10, resourceManager.GetResourceAmount(ResourceType.Wood));
    }

    [Test]
    public void Test04_UnpaidUpkeepInSteps2To4_AppliesPenaltyAndStillProduces()
    {
        // Brak drewna w skarbcu i brak Tartaku; Kamieniołom potrzebuje 2 Wood -> brak upkeepu -> kara -50% (1.0 -> 0.5)
        var quarryData = CreateEconomicData("Quarry", "Quarry",
            new List<ResourceCost> { new ResourceCost { type = ResourceType.Stone, amount = 10 } },
            new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 2 } });
        CreateAndStaffEconomic(quarryData);

        resourceManager.AddResource(ResourceType.Wood, 1); // Za mało na koszt 2 -> nie powinno pobrać częściowo

        timeManager.ChangeCurrentPhase(); // Morning -> Dusk

        Assert.AreEqual(1, resourceManager.GetResourceAmount(ResourceType.Wood));
        Assert.AreEqual(5, resourceManager.GetResourceAmount(ResourceType.Stone));
    }

    [Test]
    public void Test05_Step4_IgnoresUniqueBuildingsWithUpkeep_LocksToolWorkshop()
    {
        // Outpost (Unique) ma upkeep w danych, ale nie jest BuildingType.Economic -> Krok 4 go nie pobiera
        var outpostData = Track(ScriptableObject.CreateInstance<BuildingData>());
        outpostData.name = "Outpost";
        outpostData.buildingName = "Outpost";
        outpostData.type = BuildingType.Unique;
        outpostData.upkeepPerCycle = new List<ResourceCost>
        {
            new ResourceCost { type = ResourceType.Food, amount = 5 },
            new ResourceCost { type = ResourceType.Wood, amount = 5 }
        };
        var outpostGo = Track(new GameObject("Outpost"));
        var outpost = outpostGo.AddComponent<OutpostEntity>();
        outpost.Initialize(outpostData);

        // ToolWorkshop (Unique) nie pobiera surowców, ale blokuje 3 Ludzi przy Dusk
        var wsData = Track(ScriptableObject.CreateInstance<BuildingData>());
        wsData.name = "ToolWorkshop";
        wsData.buildingName = "Warsztat narzedziowy";
        wsData.type = BuildingType.Unique;
        wsData.baseMaxWorkers = 3;
        var wsGo = Track(new GameObject("ToolWorkshop"));
        var ws = wsGo.AddComponent<ToolWorkshopEntity>();
        ws.Initialize(wsData);

        for (int i = 0; i < 3; i++)
        {
            popManager.BirthCitizen(Race.Humans, null);
            Assert.IsTrue(ws.TryAddWorker(Race.Humans));
        }

        resourceManager.AddResource(ResourceType.Food, 10);
        resourceManager.AddResource(ResourceType.Wood, 10);

        timeManager.ChangeCurrentPhase(); // Morning -> Dusk

        Assert.AreEqual(10, resourceManager.GetResourceAmount(ResourceType.Food));
        Assert.AreEqual(10, resourceManager.GetResourceAmount(ResourceType.Wood));
        foreach (var c in ws.GetAssignedCitizens())
        {
            Assert.AreEqual(WorkState.Locked, c.workState);
        }
    }

    [Test]
    public void Test06_Step5_GlobalGrowthBlockade_WhenAnyHouseStarves()
    {
        // Dom A: 1 mieszkaniec (3 Food survival, 6 Food growth)
        var houseA = CreateHousing("HouseA", initialResidents: 1, maxResidents: 4, foodPerResident: 3, growthFoodCost: 6, priority: BuildingPriority.High);
        houseA.currentGrowthProgress = 1;

        // Dom B: 3 mieszkańców (9 Food survival, 6 Food growth)
        var houseB = CreateHousing("HouseB", initialResidents: 3, maxResidents: 4, foodPerResident: 3, growthFoodCost: 6, priority: BuildingPriority.Normal);
        houseB.currentGrowthProgress = 1;

        // Mamy 10 Food: Dom A opłaci 3 (zostanie 7), Dom B potrzebuje 9 -> Dom B głoduje!
        // Przez globalną blokadę żaden dom nie pobiera 6 Food na wzrost (zostaje 7 Food),
        // Dom A NIE rośnie (zostaje progress = 1), Dom B traci 1 cykl (progress = 0).
        resourceManager.AddResource(ResourceType.Food, 10);

        timeManager.ChangeCurrentPhase(); // Morning -> Dusk

        Assert.AreEqual(7, resourceManager.GetResourceAmount(ResourceType.Food));
        Assert.AreEqual(1, houseA.currentGrowthProgress, "Dom A nie powinien rosnąć, gdy Dom B głoduje");
        Assert.AreEqual(0, houseB.currentGrowthProgress, "Głodujący Dom B powinien stracić 1 cykl postępu");
    }

    [Test]
    public void Test07_Step5_PartialGrowthAfterAllSurvivalPaid()
    {
        var houseA = CreateHousing("HouseA", initialResidents: 1, maxResidents: 4, foodPerResident: 3, growthFoodCost: 6, priority: BuildingPriority.High);
        var houseB = CreateHousing("HouseB", initialResidents: 1, maxResidents: 4, foodPerResident: 3, growthFoodCost: 6, priority: BuildingPriority.Normal);

        // 13 Food: oba domy płacą survival (3 + 3 = 6, zostaje 7).
        // Starczają zasoby na wzrost jednego domu (7 - 6 = 1), drugi się nie mieści.
        resourceManager.AddResource(ResourceType.Food, 13);

        timeManager.ChangeCurrentPhase(); // Morning -> Dusk

        Assert.AreEqual(1, resourceManager.GetResourceAmount(ResourceType.Food));
        Assert.AreEqual(1, houseA.currentGrowthProgress);
        Assert.AreEqual(0, houseB.currentGrowthProgress);
    }

    [Test]
    public void Test08_PhaseForecast_MatchesMorningSteps1To5_AndDuskDefenseOnly()
    {
        var farmData = CreateEconomicData("Farm", "Farm",
            new List<ResourceCost> { new ResourceCost { type = ResourceType.Food, amount = 10 } },
            new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 4 } });
        CreateAndStaffEconomic(farmData);

        CreateHousing("Human house", initialResidents: 2, maxResidents: 4, foodPerResident: 3, growthFoodCost: 4);

        var towerData = Track(ScriptableObject.CreateInstance<TowerData>());
        towerData.name = "Ballista";
        towerData.buildingName = "Wieża Balist";
        towerData.type = BuildingType.Defense;
        towerData.baseMaxWorkers = 1;
        towerData.upkeepPerCycle = new List<ResourceCost>
        {
            new ResourceCost { type = ResourceType.Iron, amount = 3 },
            new ResourceCost { type = ResourceType.Food, amount = 1 }
        };
        var towerGo = Track(new GameObject("TestTower"));
        var tower = towerGo.AddComponent<TowerEntity>();
        tower.Initialize(towerData);
        popManager.BirthCitizen(Race.Dwarves, null);
        Assert.IsTrue(tower.TryAddWorker(Race.Dwarves));

        var morningForecast = PhaseResourceForecastManager.CalculateForecast(TimePhases.Morning);
        var duskForecast = PhaseResourceForecastManager.CalculateForecast(TimePhases.Dusk);
        var nightForecast = PhaseResourceForecastManager.CalculateForecast(TimePhases.Night);

        // Morning: Farm (+10 Food, -4 Wood) + Housing (-6 survival, -4 growth Food) = Food 0, Wood -4
        Assert.AreEqual(-4, morningForecast[ResourceType.Wood]);
        Assert.AreEqual(0, morningForecast[ResourceType.Food]);
        Assert.AreEqual(0, morningForecast[ResourceType.Iron]);

        // Dusk: wyłącznie wieża obronna (-3 Iron, -1 Food), bez domów!
        Assert.AreEqual(-3, duskForecast[ResourceType.Iron]);
        Assert.AreEqual(-1, duskForecast[ResourceType.Food]);
        Assert.AreEqual(0, duskForecast[ResourceType.Wood]);

        // Night: wszystko 0
        Assert.AreEqual(0, nightForecast[ResourceType.Iron]);
        Assert.AreEqual(0, nightForecast[ResourceType.Food]);
        Assert.AreEqual(0, nightForecast[ResourceType.Wood]);

        // Spójność: Morning + Dusk == bilans dobowy DailyResourceCalculator
        var dailyFood = DailyResourceCalculator.CalculateDailyReport(ResourceType.Food);
        var dailyWood = DailyResourceCalculator.CalculateDailyReport(ResourceType.Wood);
        var dailyIron = DailyResourceCalculator.CalculateDailyReport(ResourceType.Iron);

        Assert.AreEqual(dailyFood.netDailyBalance, morningForecast[ResourceType.Food] + duskForecast[ResourceType.Food]);
        Assert.AreEqual(dailyWood.netDailyBalance, morningForecast[ResourceType.Wood] + duskForecast[ResourceType.Wood]);
        Assert.AreEqual(dailyIron.netDailyBalance, morningForecast[ResourceType.Iron] + duskForecast[ResourceType.Iron]);
    }

    [Test]
    public void Test09_HousingBuiltInMorning_Keeps100PercentRefundUntilDusk()
    {
        var housing = CreateHousing("Human house", initialResidents: 1, maxResidents: 3, foodPerResident: 2, growthFoodCost: 4);
        Assert.AreEqual(100, housing.RefundPercentage);

        // Symulacja Morning reset (np. gdyby dom przeżył noc) nie powinna zmienić na 50% przed pierwszym Dusk,
        // a rozliczenie Dusk oznacza dom jako użyty -> 50%
        resourceManager.AddResource(ResourceType.Food, 10);
        timeManager.ChangeCurrentPhase(); // Morning -> Dusk

        Assert.AreEqual(50, housing.RefundPercentage);
    }

    [Test]
    public void Test10_MorningPopulationGrowthForecast_PredictsGrowthWithFarmFood_AndRespectsGlobalStarvation()
    {
        var farmData = CreateEconomicData("Farm", "Farm",
            new List<ResourceCost> { new ResourceCost { type = ResourceType.Food, amount = 10 } },
            new List<ResourceCost> { new ResourceCost { type = ResourceType.Wood, amount = 4 } });
        CreateAndStaffEconomic(farmData);
        resourceManager.AddResource(ResourceType.Wood, 4); // Pozwala Farmie wyprodukować pełne 10 Food w Kroku 2

        var humanHouse = CreateHousing("Human house", initialResidents: 1, maxResidents: 4, foodPerResident: 3, growthFoodCost: 6);
        humanHouse.currentGrowthProgress = 0; // Potrzebuje 2 cykli (0 + 1 < 2 -> brak narodzin o zmierzchu)

        var forecast0 = PhaseResourceForecastManager.PredictIncomingPopulationGrowth(TimePhases.Morning);
        Assert.IsFalse(forecast0[Race.Humans]);

        // Teraz 1/2 postępu -> najbliższe Dusk urodzi mieszkańca z jedzenia wyprodukowanego przez Farmę (mimo 0 Food w skarbcu)
        humanHouse.currentGrowthProgress = 1;
        var forecast1 = PhaseResourceForecastManager.PredictIncomingPopulationGrowth(TimePhases.Morning);
        Assert.IsTrue(forecast1[Race.Humans]);
        Assert.IsFalse(forecast1[Race.Elves]);

        // Poza fazą Morning prognoza zawsze zwraca false
        Assert.IsFalse(PhaseResourceForecastManager.PredictIncomingPopulationGrowth(TimePhases.Dusk)[Race.Humans]);
        Assert.IsFalse(PhaseResourceForecastManager.PredictIncomingPopulationGrowth(TimePhases.Night)[Race.Humans]);

        // Dodanie głodującego domu innej rasy uruchamia globalną blokadę wzrostu -> prognoza gaśnie
        var starvingElfHouse = CreateHousing("Elf house", initialResidents: 2, maxResidents: 4, foodPerResident: 5, growthFoodCost: 8);
        starvingElfHouse.housingData.housingRace = Race.Elves;
        var forecastBlocked = PhaseResourceForecastManager.PredictIncomingPopulationGrowth(TimePhases.Morning);
        Assert.IsFalse(forecastBlocked[Race.Humans]);
    }

    [Test]
    public void Test11_PopulationInfoPanelUI_HighlightsTotalGreenInMorningWhenGrowthIncoming()
    {
        var farmData = CreateEconomicData("Farm", "Farm",
            new List<ResourceCost> { new ResourceCost { type = ResourceType.Food, amount = 10 } });
        CreateAndStaffEconomic(farmData);

        var humanHouse = CreateHousing("Human house", initialResidents: 1, maxResidents: 4, foodPerResident: 3, growthFoodCost: 6);
        humanHouse.currentGrowthProgress = 1;

        var panelGo = Track(new GameObject("PopPanel_Humans"));
        panelGo.SetActive(false);
        var panel = panelGo.AddComponent<PopulationInfoPanelUI>();

        var txtFree = Track(new GameObject("Txt_Free")).AddComponent<TextMeshProUGUI>();
        var txtWorking = Track(new GameObject("Txt_Working")).AddComponent<TextMeshProUGUI>();
        var txtAll = Track(new GameObject("Txt_All")).AddComponent<TextMeshProUGUI>();

        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(PopulationInfoPanelUI).GetField("race", flags)?.SetValue(panel, Race.Humans);
        typeof(PopulationInfoPanelUI).GetField("txtFree", flags)?.SetValue(panel, txtFree);
        typeof(PopulationInfoPanelUI).GetField("txtWorking", flags)?.SetValue(panel, txtWorking);
        typeof(PopulationInfoPanelUI).GetField("txtAll", flags)?.SetValue(panel, txtAll);

        Color normalColor = Color.white;
        Color greenColor = new Color32(74, 222, 128, 255);
        typeof(PopulationInfoPanelUI).GetField("normalColor", flags)?.SetValue(panel, normalColor);
        typeof(PopulationInfoPanelUI).GetField("growthIncomingColor", flags)?.SetValue(panel, greenColor);

        panelGo.SetActive(true);
        InvokePrivate(panel, "Start");

        Assert.AreEqual(greenColor, txtAll.color, "W Morning cyfra ogólna powinna świecić się na zielono przy nadchodzącym przyroście");

        timeManager.ChangeCurrentPhase(); // Morning -> Dusk
        Assert.AreEqual(normalColor, txtAll.color, "Po przejściu do Dusk kolor powinien wrócić do normalnego");
    }

    [Test]
    public void Test12_SingleRaceWorkerControllerUI_AddsAndRemovesWorkers_ForBuildingAndRuneForge()
    {
        var rootGo = Track(new GameObject("Pnl_RaceControllersSingle Variant"));
        var universalGo = Track(new GameObject("Pnl_Race_Universal"));
        universalGo.transform.SetParent(rootGo.transform, false);

        var titleGo = Track(new GameObject("Txt_RaceTitle"));
        titleGo.transform.SetParent(universalGo.transform, false);
        var txtTitle = titleGo.AddComponent<TextMeshProUGUI>();

        var freeGo = Track(new GameObject("Txt_FreeWorkers"));
        freeGo.transform.SetParent(universalGo.transform, false);
        var txtFree = freeGo.AddComponent<TextMeshProUGUI>();

        var buttonsGo = Track(new GameObject("Pnl_Buttons"));
        buttonsGo.transform.SetParent(universalGo.transform, false);

        var minusGo = Track(new GameObject("Btn_Minus"));
        minusGo.transform.SetParent(buttonsGo.transform, false);
        var btnMinus = minusGo.AddComponent<Button>();

        var countGo = Track(new GameObject("Txt_Count"));
        countGo.transform.SetParent(buttonsGo.transform, false);
        var txtCount = countGo.AddComponent<TextMeshProUGUI>();

        var plusGo = Track(new GameObject("Btn_Plus"));
        plusGo.transform.SetParent(buttonsGo.transform, false);
        var btnPlus = plusGo.AddComponent<Button>();

        var controller = rootGo.AddComponent<SingleRaceWorkerControllerUI>();

        // 1. Standardowy budynek (np. ToolWorkshop wymagający Ludzi)
        var wsData = Track(ScriptableObject.CreateInstance<BuildingData>());
        wsData.name = "ToolWorkshop";
        wsData.buildingName = "Warsztat";
        wsData.type = BuildingType.Unique;
        wsData.baseMaxWorkers = 2;
        wsData.requiresSpecificCitizen = true;
        wsData.requiredCitizenRace = Race.Humans;

        var wsGo = Track(new GameObject("Workshop"));
        var workshop = wsGo.AddComponent<ToolWorkshopEntity>();
        workshop.Initialize(wsData);

        popManager.BirthCitizen(Race.Humans, null);
        popManager.BirthCitizen(Race.Humans, null);

        controller.Setup(workshop);
        Assert.AreEqual(Race.Humans, controller.Race);
        Assert.AreEqual("0", txtCount.text);
        Assert.IsTrue(btnPlus.interactable);
        Assert.IsFalse(btnMinus.interactable);

        btnPlus.onClick.Invoke();
        Assert.AreEqual("1", txtCount.text);
        Assert.AreEqual(1, workshop.GetWorkerCount(Race.Humans));
        Assert.IsTrue(btnMinus.interactable);

        btnMinus.onClick.Invoke();
        Assert.AreEqual("0", txtCount.text);
        Assert.AreEqual(0, workshop.GetWorkerCount(Race.Humans));

        // 2. Kuźnia Runiczna (RuneForgeEntity) z krasnoludami
        var forgeData = Track(ScriptableObject.CreateInstance<BuildingData>());
        forgeData.name = "RuneSmith";
        forgeData.buildingName = "Kuźnia";
        forgeData.type = BuildingType.Unique;
        forgeData.baseMaxWorkers = 1;

        var forgeGo = Track(new GameObject("RuneForge"));
        var forge = forgeGo.AddComponent<RuneForgeEntity>();
        forge.Initialize(forgeData);

        popManager.BirthCitizen(Race.Dwarves, null);
        controller.Setup(forge);

        Assert.AreEqual(Race.Dwarves, controller.Race);
        Assert.AreEqual("0", txtCount.text);
        Assert.IsTrue(btnPlus.interactable);

        btnPlus.onClick.Invoke();
        Assert.AreEqual("1", txtCount.text);
        Assert.AreEqual(1, forge.assignedDwarves.Count);
        Assert.IsFalse(btnPlus.interactable); // Limit 1/1 osiągnięty

        btnMinus.onClick.Invoke();
        Assert.AreEqual("0", txtCount.text);
        Assert.AreEqual(0, forge.assignedDwarves.Count);
    }
}
