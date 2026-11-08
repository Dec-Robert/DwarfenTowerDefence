using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Precyzyjny kalkulator szacowanego bilansu surowców w skali pełnej doby (24h).
/// Zbiera i agreguje:
/// - Produkcję budynków ekonomicznych (o Zmierzchu)
/// - Produkcję mieszkańców domostw (o Poranku)
/// - Koszty utrzymania (upkeep) budynków ekonomicznych (o Zmierzchu)
/// - Koszty utrzymania (upkeep) obsadzonych wież obronnych (w Nocy)
/// - Koszty przetrwania populacji (o Poranku)
/// - Koszty rozwoju nowej populacji (o Poranku)
/// </summary>
public static class DailyResourceCalculator
{
    public class ResourceSourceItem
    {
        public string label;
        public int count;
        public int totalAmount;

        public ResourceSourceItem(string label, int count, int totalAmount)
        {
            this.label = label;
            this.count = count;
            this.totalAmount = totalAmount;
        }

        public string GetFormattedProduction()
        {
            string countSuffix = count > 1 ? $" (x{count})" : "";
            return $"• {label}{countSuffix}: +{totalAmount}";
        }

        public string GetFormattedConsumption()
        {
            string countSuffix = count > 1 ? $" (x{count})" : "";
            return $"• {label}{countSuffix}: -{totalAmount}";
        }
    }

    public class DailyResourceReport
    {
        public ResourceType resourceType;
        public int totalDailyProduction;
        public int totalDailyConsumption;
        public int netDailyBalance => totalDailyProduction - totalDailyConsumption;
        public List<ResourceSourceItem> productionSources = new();
        public List<ResourceSourceItem> consumptionSources = new();
    }

    public static DailyResourceReport CalculateDailyReport(ResourceType targetResource)
    {
        var report = new DailyResourceReport
        {
            resourceType = targetResource
        };

        if (BuildingRegistry.Instance == null) return report;

        // Słowniki pomocnicze do agregacji po nazwie budynku
        var prodDict = new Dictionary<string, (int count, float amount)>();
        var upkeepDict = new Dictionary<string, (int count, int amount)>();
        var towerUpkeepDict = new Dictionary<string, (int count, int amount)>();

        int housingProdAmount = 0;
        int housingProdResidents = 0;
        int totalSurvivalCost = 0;
        int totalGrowthCost = 0;

        var allBuildings = BuildingRegistry.Instance.AllBuildings;
        if (allBuildings == null) return report;

        foreach (var b in allBuildings)
        {
            if (b == null || !b.isBuildingActive || b.data == null) continue;

            // 1. BUDYNKI EKONOMICZNE (Produkcja i Koszty stałe)
            if (b.data.type == BuildingType.Economic && !(b is HousingEntity))
            {
                float crewEfficiency = b.Production != null ? b.Production.GetCrewEfficiency() : 1f;
                if (crewEfficiency <= 0f) continue;

                string buildingName = GetBuildingDisplayName(b.data);

                // Produkcja
                var currentProd = b.GetCurrentProduction();
                if (currentProd != null && currentProd.TryGetValue(targetResource, out float prodAmount) && prodAmount > 0f)
                {
                    if (prodDict.TryGetValue(buildingName, out var entry))
                    {
                        prodDict[buildingName] = (entry.count + 1, entry.amount + prodAmount);
                    }
                    else
                    {
                        prodDict[buildingName] = (1, prodAmount);
                    }
                }

                // Upkeep budynku produkcyjnego
                if (b.data.upkeepPerCycle != null)
                {
                    foreach (var cost in b.data.upkeepPerCycle)
                    {
                        if (cost.type == targetResource && cost.amount > 0)
                        {
                            if (upkeepDict.TryGetValue(buildingName, out var entry))
                            {
                                upkeepDict[buildingName] = (entry.count + 1, entry.amount + cost.amount);
                            }
                            else
                            {
                                upkeepDict[buildingName] = (1, cost.amount);
                            }
                        }
                    }
                }
            }
            // 2. WIEŻE OBRONNE (Koszty nocne)
            else if (b.data.type == BuildingType.Defense)
            {
                if (b is TowerEntity tower)
                {
                    // Wieża ponosi koszty tylko jeśli posiada załogę
                    if (tower.GetTotalWorkerCount() <= 0) continue;
                }

                if (b.data.upkeepPerCycle != null)
                {
                    string towerName = GetBuildingDisplayName(b.data);
                    foreach (var cost in b.data.upkeepPerCycle)
                    {
                        if (cost.type == targetResource && cost.amount > 0)
                        {
                            if (towerUpkeepDict.TryGetValue(towerName, out var entry))
                            {
                                towerUpkeepDict[towerName] = (entry.count + 1, entry.amount + cost.amount);
                            }
                            else
                            {
                                towerUpkeepDict[towerName] = (1, cost.amount);
                            }
                        }
                    }
                }
            }
            // 3. DOMOSTWA I MIESZKAŃCY
            else if (b is HousingEntity housing)
            {
                if (housing.housingData == null) continue;

                int residentCount = housing.residents != null ? housing.residents.Count : 0;

                // Produkcja mieszkańców
                if (residentCount > 0 && housing.housingData.productionPerResident != null)
                {
                    foreach (var prod in housing.housingData.productionPerResident)
                    {
                        if (prod.type == targetResource)
                        {
                            int totalAmount = Mathf.FloorToInt(prod.amount * residentCount);
                            if (totalAmount > 0)
                            {
                                housingProdAmount += totalAmount;
                                housingProdResidents += residentCount;
                            }
                        }
                    }
                }

                // Przetrwanie mieszkańców (survival cost: baza + na mieszkańca)
                var survivalCosts = housing.CalculateSurvivalCost();
                if (survivalCosts != null)
                {
                    foreach (var cost in survivalCosts)
                    {
                        if (cost.type == targetResource && cost.amount > 0)
                        {
                            totalSurvivalCost += cost.amount;
                        }
                    }
                }

                // Koszt rozwoju populacji (growth cost)
                if (!housing.stopGrowth && residentCount < housing.GetEffectiveMaxResidents())
                {
                    var growthCosts = housing.CalculateGrowthCost();
                    if (growthCosts != null)
                    {
                        foreach (var cost in growthCosts)
                        {
                            if (cost.type == targetResource && cost.amount > 0)
                            {
                                totalGrowthCost += cost.amount;
                            }
                        }
                    }
                }
            }
        }

        // --- Składanie produkcji ---
        foreach (var kvp in prodDict)
        {
            int roundedAmount = Mathf.RoundToInt(kvp.Value.amount);
            if (roundedAmount > 0)
            {
                report.productionSources.Add(new ResourceSourceItem(kvp.Key, kvp.Value.count, roundedAmount));
                report.totalDailyProduction += roundedAmount;
            }
        }

        if (housingProdAmount > 0)
        {
            report.productionSources.Add(new ResourceSourceItem("Mieszkańcy", housingProdResidents, housingProdAmount));
            report.totalDailyProduction += housingProdAmount;
        }

        // --- Składanie zużycia ---
        if (totalSurvivalCost > 0)
        {
            report.consumptionSources.Add(new ResourceSourceItem("Utrzymanie populacji", 0, totalSurvivalCost));
            report.totalDailyConsumption += totalSurvivalCost;
        }

        if (totalGrowthCost > 0)
        {
            report.consumptionSources.Add(new ResourceSourceItem("Rozwój populacji", 0, totalGrowthCost));
            report.totalDailyConsumption += totalGrowthCost;
        }

        foreach (var kvp in upkeepDict)
        {
            report.consumptionSources.Add(new ResourceSourceItem(kvp.Key, kvp.Value.count, kvp.Value.amount));
            report.totalDailyConsumption += kvp.Value.amount;
        }

        foreach (var kvp in towerUpkeepDict)
        {
            report.consumptionSources.Add(new ResourceSourceItem(kvp.Key, kvp.Value.count, kvp.Value.amount));
            report.totalDailyConsumption += kvp.Value.amount;
        }

        return report;
    }

    public static string GetBuildingDisplayName(BuildingData data)
    {
        if (data == null) return "Budynek";
        if (!string.IsNullOrEmpty(data.buildingName)) return data.buildingName;
        return data.name;
    }

    public static string GetResourceDisplayName(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Ether: return "Eter";
            case ResourceType.Wood: return "Drewno";
            case ResourceType.Stone: return "Kamień";
            case ResourceType.Coal: return "Węgiel";
            case ResourceType.Food: return "Żywność";
            case ResourceType.Iron: return "Żelazo";
            case ResourceType.Hope: return "Nadzieja";
            default: return type.ToString();
        }
    }
}
