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
                // Produkcja (np. Z�oto od mieszka�c�w)
                if (house.housingData.productionPerResident != null)
                {
                    foreach (var prod in house.housingData.productionPerResident)
                    {
                        if (prod.type == type)
                            data.dailyProduction += prod.amount * house.residents.Count;
                    }
                }

                // Konsumpcja (Jedzenie)
                // (Sumujemy baz� i per capita)
                if (house.housingData.baseDailyUpkeep != null)
                    foreach (var cost in house.housingData.baseDailyUpkeep)
                        if (cost.type == type) data.dailyConsumption += cost.amount;

                if (house.housingData.upkeepPerResident != null)
                    foreach (var cost in house.housingData.upkeepPerResident)
                        if (cost.type == type) data.dailyConsumption += cost.amount * house.residents.Count;

                continue; // Domy obs�u�one, idziemy do nast�pnego
            }

            // 2. Beacon - Specyficzna logika
            if (building is BeaconEntity beacon)
            {
                if (type == ResourceType.Coal)
                {
                    // Beacon zu�ywa tyle, ile gracz ustawi� suwakiem (lub ile potrzeba do spalania)
                    // Dla prognozy bierzemy ustawienie suwaka (planowane wydatki)
                    data.dailyConsumption += beacon.dailyCoalInput;
                }
                continue;
            }

            // 3. Budynki Ekonomiczne i Wie�e (Standard)

            // Obliczamy efektywno�� (Ludzie)
            int workers = building.GetAssignedCitizens().FindAll(c => c.workState != WorkState.Exhausted).Count;
            float efficiency = 0f;
            if (workers > 0)
            {
                efficiency = 1.0f + ((workers - 1) * 0.25f); // 25% za kolejnego
            }

            // Uwzgl�dniamy Beacon Bonus dla produkcji
            float beaconBonus = 1f;

            // Ile zmian aktywnych? (Uproszczenie: zak�adamy, �e obsadzone zmiany pracuj�)
            // Lepsze przybli�enie: ile godzin w dobie budynek pracuje?
            // currentShiftLength * ilo��_obsadzonych_zmian? 
            // Dla uproszczenia prognozy: bierzemy produkcj� na cykl * ilo�� aktywnych zmian.
            // Zak�adamy, �e je�li s� pracownicy, to pracuj�.

            // Produkcja
            var prodDict = building.GetCurrentProduction(); // To ju� uwzgl�dnia teren!
            if (prodDict.ContainsKey(type))
            {
                // Produkcja na zmian� * Ilo�� zmian * Efektywno�� * Beacon
                // (Tutaj zak�adamy 1 zmian� na dzie� dla uproszczenia wy�wietlania, 
                // lub mno�ymy przez maxShifts je�li s� obsadzone)
                // Przyjmijmy bezpiecznie: to co wylicza GetCurrentProduction to "Baza na zmian�".

                // Je�li budynek ma pracownik�w -> generuje.
                if (workers > 0)
                {
                    // Uproszczona prognoza: (ProdukcjaNaZmian�) * (LiczbaZmianObsadzonych)
                    // Zak�adamy �e jak s� ludzie to pracuj� na wszystkich dost�pnych zmianach?
                    // W Twoim kodzie pracownicy s� wsp�lni dla zmian.
                    // Wi�c: ProdukcjaDienna = ProdNaZmiane * Ilo��Zmian * Efektywno��

                    float totalProd = (prodDict[type] * efficiency * beaconBonus) * building.getMaxShifts();
                    data.dailyProduction += totalProd;
                }
            }

            // Konsumpcja (Upkeep)
            var costDict = building.GetCurrentUpkeep();
            if (costDict.ContainsKey(type))
            {
                // Dla wie�: Amunicja jest pobierana raz na noc (upkeepPerCycle).
                if (building is TowerEntity)
                {
                    // Wie�a pobiera raz na dob�
                    data.dailyConsumption += costDict[type];
                }
                else
                {
                    // Budynki eko pobieraj� paliwo co godzin� pracy.
                    // Suma na dzie� = KosztNaZmiane * Ilo��Zmian * Efektywno��
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