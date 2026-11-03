using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class DailyResourceBalanceTests
{
    private List<GameObject> createdObjects;
    private BuildingRegistry registry;

    [SetUp]
    public void SetUp()
    {
        createdObjects = new List<GameObject>();

        var regGO = new GameObject("TestBuildingRegistry");
        createdObjects.Add(regGO);
        registry = regGO.AddComponent<BuildingRegistry>();
    }

    [TearDown]
    public void TearDown()
    {
        if (createdObjects != null)
        {
            for (int i = 0; i < createdObjects.Count; i++)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }
            createdObjects.Clear();
        }
    }

    private BuildingData CreateEconomicData(string name, ResourceType prodType, int prodAmount, ResourceType upkeepType = ResourceType.Ether, int upkeepAmount = 0)
    {
        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.buildingName = name;
        data.type = BuildingType.Economic;
        data.productionPerCycle = new List<ResourceCost>
        {
            new ResourceCost { type = prodType, amount = prodAmount }
        };

        if (upkeepAmount > 0)
        {
            data.upkeepPerCycle = new List<ResourceCost>
            {
                new ResourceCost { type = upkeepType, amount = upkeepAmount }
            };
        }

        return data;
    }

    private BuildingEntity CreateBuildingEntity(BuildingData data)
    {
        var go = new GameObject($"Entity_{data.buildingName}");
        createdObjects.Add(go);

        var entity = go.AddComponent<BuildingEntity>();
        entity.Initialize(data);
        registry.Register(entity);
        return entity;
    }

    [Test]
    public void Test01_EconomicBuildings_AggregatedByDisplayName()
    {
        var sawData = CreateEconomicData("Tartak", ResourceType.Wood, 15);

        // 2 Tartaki
        CreateBuildingEntity(sawData);
        CreateBuildingEntity(sawData);

        var report = DailyResourceCalculator.CalculateDailyReport(ResourceType.Wood);

        Assert.AreEqual(30, report.totalDailyProduction);
        Assert.AreEqual(0, report.totalDailyConsumption);
        Assert.AreEqual(30, report.netDailyBalance);

        Assert.AreEqual(1, report.productionSources.Count);
        Assert.AreEqual("Tartak", report.productionSources[0].label);
        Assert.AreEqual(2, report.productionSources[0].count);
        Assert.AreEqual(30, report.productionSources[0].totalAmount);
        Assert.AreEqual("• Tartak (x2): +30", report.productionSources[0].GetFormattedProduction());
    }

    [Test]
    public void Test02_EconomicUpkeep_AggregatedByDisplayName()
    {
        var smelterData = CreateEconomicData("Huta", ResourceType.Iron, 5, ResourceType.Coal, 3);

        CreateBuildingEntity(smelterData);
        CreateBuildingEntity(smelterData);

        var coalReport = DailyResourceCalculator.CalculateDailyReport(ResourceType.Coal);

        Assert.AreEqual(0, coalReport.totalDailyProduction);
        Assert.AreEqual(6, coalReport.totalDailyConsumption);
        Assert.AreEqual(-6, coalReport.netDailyBalance);

        Assert.AreEqual(1, coalReport.consumptionSources.Count);
        Assert.AreEqual("Huta", coalReport.consumptionSources[0].label);
        Assert.AreEqual(2, coalReport.consumptionSources[0].count);
        Assert.AreEqual(6, coalReport.consumptionSources[0].totalAmount);
        Assert.AreEqual("• Huta (x2): -6", coalReport.consumptionSources[0].GetFormattedConsumption());
    }

    [Test]
    public void Test03_HousingSurvivalAndGrowth_SeparatedInConsumption()
    {
        var housingData = ScriptableObject.CreateInstance<HousingBuildingData>();
        housingData.buildingName = "Dom Ludzi";
        housingData.type = BuildingType.Housing;
        housingData.housingRace = Race.Humans;
        housingData.maxResidents = 5;
        housingData.upkeepPerResident = new List<HousingBuildingData.PerCapitaCost>
        {
            new HousingBuildingData.PerCapitaCost { type = ResourceType.Food, amount = 2f }
        };
        housingData.growthSurplusCost = new List<ResourceCost>
        {
            new ResourceCost { type = ResourceType.Food, amount = 4 }
        };

        var go = new GameObject("HousingEntity");
        createdObjects.Add(go);
        var housing = go.AddComponent<HousingEntity>();
        housing.data = housingData;

        // Dodajemy 4 mieszkańców
        for (int i = 0; i < 4; i++)
        {
            var citizen = new Citizen();
            citizen.race = Race.Humans;
            housing.residents.Add(citizen);
        }

        registry.Register(housing);

        var foodReport = DailyResourceCalculator.CalculateDailyReport(ResourceType.Food);

        Assert.AreEqual(12, foodReport.totalDailyConsumption); // 8 przetrwanie + 4 rozwój
        Assert.AreEqual(-12, foodReport.netDailyBalance);

        var survivalItem = foodReport.consumptionSources.Find(x => x.label == "Utrzymanie populacji");
        var growthItem = foodReport.consumptionSources.Find(x => x.label == "Rozwój populacji");

        Assert.IsNotNull(survivalItem, "Utrzymanie populacji powinno być na liście");
        Assert.AreEqual(8, survivalItem.totalAmount);

        Assert.IsNotNull(growthItem, "Rozwój populacji powinien być na liście");
        Assert.AreEqual(4, growthItem.totalAmount);
    }

    [Test]
    public void Test04_TowerDefenseUpkeep_OnlyWhenHasWorkers()
    {
        var towerData = ScriptableObject.CreateInstance<TowerData>();
        towerData.buildingName = "Wieża Balist";
        towerData.type = BuildingType.Defense;
        towerData.upkeepPerCycle = new List<ResourceCost>
        {
            new ResourceCost { type = ResourceType.Iron, amount = 3 }
        };

        // Wieża 1: brak załogi -> koszt 0
        var goEmpty = new GameObject("Tower_Empty");
        createdObjects.Add(goEmpty);
        var towerEmpty = goEmpty.AddComponent<TowerEntity>();
        towerEmpty.Initialize(towerData);
        registry.Register(towerEmpty);

        var ironReportEmpty = DailyResourceCalculator.CalculateDailyReport(ResourceType.Iron);
        Assert.AreEqual(0, ironReportEmpty.totalDailyConsumption);

        // Wieża 2: z załogą -> koszt 3
        var goManned = new GameObject("Tower_Manned");
        createdObjects.Add(goManned);
        var towerManned = goManned.AddComponent<TowerEntity>();
        towerManned.Initialize(towerData);
        towerManned.Workers.GetAssignedCitizens().Add(new Citizen());
        registry.Register(towerManned);

        var ironReportManned = DailyResourceCalculator.CalculateDailyReport(ResourceType.Iron);
        Assert.AreEqual(3, ironReportManned.totalDailyConsumption);
        Assert.AreEqual(1, ironReportManned.consumptionSources.Count);
        Assert.AreEqual("Wieża Balist", ironReportManned.consumptionSources[0].label);
    }

    [Test]
    public void Test05_NetDailyBalance_CalculationCorrectness()
    {
        var farmData = CreateEconomicData("Farma", ResourceType.Food, 25);
        CreateBuildingEntity(farmData);

        var housingData = ScriptableObject.CreateInstance<HousingBuildingData>();
        housingData.buildingName = "Namiot";
        housingData.type = BuildingType.Housing;
        housingData.upkeepPerResident = new List<HousingBuildingData.PerCapitaCost>
        {
            new HousingBuildingData.PerCapitaCost { type = ResourceType.Food, amount = 2f }
        };

        var go = new GameObject("HousingEntity2");
        createdObjects.Add(go);
        var housing = go.AddComponent<HousingEntity>();
        housing.data = housingData;
        housing.stopGrowth = true; // Bez rozwoju

        for (int i = 0; i < 5; i++)
        {
            var citizen = new Citizen();
            citizen.race = Race.Humans;
            housing.residents.Add(citizen);
        }
        registry.Register(housing);

        var foodReport = DailyResourceCalculator.CalculateDailyReport(ResourceType.Food);

        Assert.AreEqual(25, foodReport.totalDailyProduction);
        Assert.AreEqual(10, foodReport.totalDailyConsumption);
        Assert.AreEqual(15, foodReport.netDailyBalance); // +15 netto
    }
}
