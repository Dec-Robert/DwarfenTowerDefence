using UnityEngine;
using System.Collections.Generic;

public static class EconomyForecastSystem
{
    public struct ForecastData
    {
        public float dailyProduction;
        public float dailyConsumption;
        public float netBalance => dailyProduction - dailyConsumption;
    }

    public static ForecastData GetForecastFor(ResourceType type)
    {
        ForecastData data = new ForecastData();

        foreach (var building in BuildingEntity.AllBuildings)
        {
            if (building == null) continue;

            // 1. Domy (Housing) - Specyficzna logika
            if (building is HousingEntity house)
            {
                // Produkcja (np. Z³oto od mieszkañców)
                if (house.housingData.productionPerResident != null)
                {
                    foreach (var prod in house.housingData.productionPerResident)
                    {
                        if (prod.type == type)
                            data.dailyProduction += prod.amount * house.residents.Count;
                    }
                }

                // Konsumpcja (Jedzenie)
                // (Sumujemy bazê i per capita)
                if (house.housingData.baseDailyUpkeep != null)
                    foreach (var cost in house.housingData.baseDailyUpkeep)
                        if (cost.type == type) data.dailyConsumption += cost.amount;

                if (house.housingData.upkeepPerResident != null)
                    foreach (var cost in house.housingData.upkeepPerResident)
                        if (cost.type == type) data.dailyConsumption += cost.amount * house.residents.Count;

                continue; // Domy obs³u¿one, idziemy do nastêpnego
            }

            // 2. Beacon - Specyficzna logika
            if (building is BeaconEntity beacon)
            {
                if (type == ResourceType.Coal)
                {
                    // Beacon zu¿ywa tyle, ile gracz ustawi³ suwakiem (lub ile potrzeba do spalania)
                    // Dla prognozy bierzemy ustawienie suwaka (planowane wydatki)
                    data.dailyConsumption += beacon.dailyCoalInput;
                }
                continue;
            }

            // 3. Budynki Ekonomiczne i Wie¿e (Standard)

            // Obliczamy efektywnoœæ (Ludzie)
            int workers = building.GetAssignedCitizens().FindAll(c => c.workState != WorkState.Exhausted).Count;
            float efficiency = 0f;
            if (workers > 0)
            {
                efficiency = 1.0f + ((workers - 1) * 0.25f); // 25% za kolejnego
            }

            // Uwzglêdniamy Beacon Bonus dla produkcji
            float beaconBonus = 1f;
            if (BeaconEntity.Instance != null) beaconBonus = BeaconEntity.Instance.GetGlobalProductionMultiplier();

            // Ile zmian aktywnych? (Uproszczenie: zak³adamy, ¿e obsadzone zmiany pracuj¹)
            // Lepsze przybli¿enie: ile godzin w dobie budynek pracuje?
            // currentShiftLength * iloœæ_obsadzonych_zmian? 
            // Dla uproszczenia prognozy: bierzemy produkcjê na cykl * iloœæ aktywnych zmian.
            // Zak³adamy, ¿e jeœli s¹ pracownicy, to pracuj¹.

            // Produkcja
            var prodDict = building.GetCurrentProduction(); // To ju¿ uwzglêdnia teren!
            if (prodDict.ContainsKey(type))
            {
                // Produkcja na zmianê * Iloœæ zmian * Efektywnoœæ * Beacon
                // (Tutaj zak³adamy 1 zmianê na dzieñ dla uproszczenia wyœwietlania, 
                // lub mno¿ymy przez maxShifts jeœli s¹ obsadzone)
                // Przyjmijmy bezpiecznie: to co wylicza GetCurrentProduction to "Baza na zmianê".

                // Jeœli budynek ma pracowników -> generuje.
                if (workers > 0)
                {
                    // Uproszczona prognoza: (ProdukcjaNaZmianê) * (LiczbaZmianObsadzonych)
                    // Zak³adamy ¿e jak s¹ ludzie to pracuj¹ na wszystkich dostêpnych zmianach?
                    // W Twoim kodzie pracownicy s¹ wspólni dla zmian.
                    // Wiêc: ProdukcjaDienna = ProdNaZmiane * IloœæZmian * Efektywnoœæ

                    float totalProd = (prodDict[type] * efficiency * beaconBonus) * building.getMaxShifts();
                    data.dailyProduction += totalProd;
                }
            }

            // Konsumpcja (Upkeep)
            var costDict = building.GetCurrentUpkeep();
            if (costDict.ContainsKey(type))
            {
                // Dla wie¿: Amunicja jest pobierana raz na noc (upkeepPerCycle).
                if (building is TowerEntity)
                {
                    // Wie¿a pobiera raz na dobê
                    data.dailyConsumption += costDict[type];
                }
                else
                {
                    // Budynki eko pobieraj¹ paliwo co godzinê pracy.
                    // Suma na dzieñ = KosztNaZmiane * IloœæZmian * Efektywnoœæ
                    if (workers > 0)
                    {
                        float totalCost = (costDict[type] * efficiency) * building.getMaxShifts();
                        data.dailyConsumption += totalCost;
                    }
                }
            }
        }

        return data;
    }
}