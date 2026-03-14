using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
///     Odpowiada za wykrywanie sąsiednich hexów z zasobami,
///     obliczanie bonusów produkcji oraz (NOWOŚĆ) dynamiczną modyfikację terenu.
/// </summary>
public class BuildingTerrainComponent
{
    private readonly List<HexCell> adjacentResourceHexes = new();
    private readonly Transform buildingTransform;
    private readonly BuildingData data;
    private readonly BuildingUpgradeComponent upgrades; // <--- NOWE

    private readonly HashSet<Vector2Int> artificiallyCreatedFeatures = new();
    private float cachedOnTopBonus;
    private int daysSinceLastDestroy;

    // =========================================================================
    // DYNAMICZNA MODYFIKACJA TERENU (Sianie / Niszczenie)
    // =========================================================================

    // Liczniki dni do mechanik czasowych (żeby nie siali codziennie)
    private int daysSinceLastPlant;

    // Zmienna dla specjalnej mechaniki Twierdzy Krasnoludów
    private int mountainChainSize;

    public BuildingTerrainComponent(BuildingData data, Transform buildingTransform, BuildingUpgradeComponent upgrades)
    {
        this.data = data;
        this.buildingTransform = buildingTransform;
        this.upgrades = upgrades; // Zapisujemy referencję do ulepszeń
    }

    // =========================================================================
    // SKANOWANIE TERENU
    // =========================================================================

    public void FindAdjacentResources()
    {
        UnregisterAll();
        mountainChainSize = 0;

        if (data.bonusRule.requiredFeature == HexFeatureType.None) return;

        var myCell = buildingTransform.GetComponentInParent<HexCell>();
        if (myCell == null) return;

        var mapGen = Object.FindObjectOfType<HexMapGenerator>();
        if (mapGen == null || !mapGen.worldData.ContainsKey(myCell.chunkCoord)) return;

        var chunkData = mapGen.worldData[myCell.chunkCoord];

        // 1. Sprawdzenie „on top" (Budynek stoi na zasobie)
        cachedOnTopBonus = 0f;
        if (chunkData.TryGetValue(myCell.localCoord, out var myHexData))
            if (myHexData.feature == data.bonusRule.requiredFeature)
                cachedOnTopBonus = data.bonusRule.onTopProductionBonus;

        // --- MECHANIKA SPECJALNA: ŁAŃCUCH GÓRSKI (Kopalnia T3) ---
        if (upgrades.HasSpecialEffect("MINE_MOUNTAIN_CHAIN"))
        {
            CalculateMountainChain(myCell, mapGen);
            return; // Kończymy skanowanie, łańcuch zastępuje normalny bonus
        }
        // ---------------------------------------------------------

        // 2. Ustalenie dynamicznego promienia poszukiwań
        var radius = data.bonusRule.searchRadius <= 0 ? 1 : data.bonusRule.searchRadius;

        if (upgrades.HasSpecialEffect("INCREASE_RANGE_1")) radius += 1;
        if (upgrades.HasSpecialEffect("INCREASE_RANGE_2")) radius += 1; // NOWE (Użyj tego na T3!)

        // 3. Poszukiwanie w promieniu
        var hexesToScan = GetHexesInRadius(myCell.localCoord, radius);

        foreach (var coord in hexesToScan)
        {
            if (coord == myCell.localCoord) continue; // Pomijamy samych siebie
            if (!chunkData.TryGetValue(coord, out var neighborData)) continue;
            if (neighborData.feature != data.bonusRule.requiredFeature) continue;

            var neighborCell = HexMapVisualizer.Instance.GetHexCell(myCell.chunkCoord, coord);
            if (neighborCell == null) continue;

            // Ulepszenie Farmy T3 pozwala ignorować fakt, że inny budynek zajął już ten hex!
            var ignoreOccupied = upgrades.HasSpecialEffect("IGNORE_OCCUPIED_PENALTY");

            if (neighborCell.HasBuilding() && !ignoreOccupied) continue;

            RegisterHex(neighborCell);
        }
    }

    // =========================================================================
    // OBLICZANIE BONUSU
    // =========================================================================

    public float CalculateTerrainBonus()
    {
        if (data.bonusRule.requiredFeature == HexFeatureType.None) return 0f;

        // Jeśli to Łańcuch Górski, liczymy bonus zupełnie inaczej
        if (upgrades.HasSpecialEffect("MINE_MOUNTAIN_CHAIN"))
            return mountainChainSize * (data.bonusRule.baseBonusPerHex * 0.8f);

        var totalBonus = cachedOnTopBonus;
        var rule = data.bonusRule;

        // 1. Pobranie zsumowanych modyfikatorów terenu ze SWOICH ulepszeń (BuildingUpgradeSO)
        var myPercentMultiplier = upgrades.GetTotalTerrainMultiplier();
        var myFlatBonus = upgrades.GetTotalTerrainFlatBonus();

        foreach (var cell in adjacentResourceHexes)
        {
            if (cell == null) continue;

            var usersCount = BuildingProductionRegistry.GetUsageCount(cell);

            // Jeśli mamy ulepszenie ignorujące innych użytkowników, udajemy że jesteśmy sami
            if (upgrades.HasSpecialEffect("IGNORE_OCCUPIED_PENALTY")) usersCount = 1;

            // BAZOWA WYLICZANKA (z uwzględnieniem kary za to, że 3 tartaki rąbią ten sam las)
            var baseHexYield = rule.baseBonusPerHex - rule.penaltyPerUser * (usersCount - 1);
            if (baseHexYield < rule.minBonus) baseHexYield = rule.minBonus;

            // 2. Aplikowanie PŁASKIEGO bonusu z naszego ulepszenia (np. Nowe Piły dają +1 z KAŻDEGO lasu)
            baseHexYield += myFlatBonus;

            // Zabezpieczenie, by zła matematyka nie wygenerowała nam ujemnych zasobów z lasu
            if (baseHexYield < 0) baseHexYield = 0;

            // 3. Aplikowanie PROCENTOWEGO bonusu z ulepszenia ORAZ bonusu z Meta Progresji globalnej (Tartak)
            var metaTerrainMultiplier = MetaUpgradeManager.Instance != null
                ? MetaUpgradeManager.Instance.GetBuildingValue(MetaEffectType.BuildingTerrainBonusMultiplier, data)
                : 0f;

            var finalMultiplier = myPercentMultiplier + metaTerrainMultiplier;

            totalBonus += baseHexYield * finalMultiplier;
        }

        return totalBonus;
    }

    // =========================================================================
    // ZARZĄDZANIE HEKSAMI I ZWALNIANIE
    // =========================================================================

    public void ReleaseAll()
    {
        UnregisterAll();
    }

    private void RegisterHex(HexCell cell)
    {
        BuildingProductionRegistry.RegisterUsage(cell);
        adjacentResourceHexes.Add(cell);
    }

    private void UnregisterAll()
    {
        foreach (var cell in adjacentResourceHexes)
            if (cell != null)
                BuildingProductionRegistry.UnregisterUsage(cell);
        adjacentResourceHexes.Clear();
        cachedOnTopBonus = 0f;
    }

    // =========================================================================
    // MATEMATYKA / ALGORYTMY (Promień i BFS dla Gór)
    // =========================================================================

    private List<Vector2Int> GetHexesInRadius(Vector2Int center, int radius)
    {
        var results = new List<Vector2Int>();
        for (var q = -radius; q <= radius; q++)
        {
            var r1 = Mathf.Max(-radius, -q - radius);
            var r2 = Mathf.Min(radius, -q + radius);
            for (var r = r1; r <= r2; r++) results.Add(new Vector2Int(center.x + q, center.y + r));
        }

        return results;
    }

    /// <summary>
    ///     BFS (Breadth-First Search) szukający połączonych gór.
    ///     Aktualnie działa w obrębie jednego chunku dla stabilności i wydajności.
    /// </summary>
    private void CalculateMountainChain(HexCell startCell, HexMapGenerator mapGen)
    {
        var chunkData = mapGen.worldData[startCell.chunkCoord];
        var visited = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();

        queue.Enqueue(startCell.localCoord);
        visited.Add(startCell.localCoord);

        mountainChainSize = 0;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            mountainChainSize++;

            foreach (var neighbor in HexGridMath.GetNeighbors(current))
            {
                if (visited.Contains(neighbor)) continue;

                if (chunkData.TryGetValue(neighbor, out var neighborData))
                    // Jeśli sąsiad to też góra, dodaj do łańcucha
                    if (neighborData.feature == HexFeatureType.Mountain)
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
            }
        }

        Debug.Log(
            $"[Mountain Chain] Kopalnia na {startCell.localCoord} wykryła łańcuch o wielkości {mountainChainSize} gór!");
    }

    /// <summary>
    ///     Wywoływane co rano przez BuildingEntity. Realizuje efekty ulepszeń czasowych.
    /// </summary>
    public void ProcessDailyTerrainModifiers()
    {
        // 1. TARTAK: Sadzenie lasu (Co 2 dni)
        if (upgrades.HasSpecialEffect("SAWMILL_PLANT_FOREST"))
        {
            daysSinceLastPlant++;
            if (daysSinceLastPlant >= 2)
            {
                TryConvertRandomHex(HexFeatureType.Forest);
                daysSinceLastPlant = 0;
            }
        }

        // 2. TARTAK: Wyniszczająca Wycinka (Co 3 dni traci jeden heks bezpowrotnie)
        if (upgrades.HasSpecialEffect("SAWMILL_DESTROY_FOREST"))
        {
            daysSinceLastDestroy++;
            if (daysSinceLastDestroy >= 3)
            {
                TryDestroyResourceHex();
                daysSinceLastDestroy = 0;
            }
        }

        // 3. FARMA: Użyźnianie Gleby (Co 4 dni)
        if (upgrades.HasSpecialEffect("FARM_CREATE_SOIL"))
        {
            daysSinceLastPlant++;
            if (daysSinceLastPlant >= 4)
            {
                TryConvertRandomHex(HexFeatureType.FertileSoil);
                daysSinceLastPlant = 0;
            }
        }
    }

    /// <summary>
    ///     Szuka pustego heksa w zasięgu (promień) i podmienia jego Feature.
    /// </summary>
    private void TryConvertRandomHex(HexFeatureType newFeature)
    {
        var myCell = buildingTransform.GetComponentInParent<HexCell>();
        if (myCell == null) return;

        var mapGen = Object.FindObjectOfType<HexMapGenerator>();
        if (mapGen == null || !mapGen.worldData.ContainsKey(myCell.chunkCoord)) return;

        var radius = data.bonusRule.searchRadius <= 0 ? 1 : data.bonusRule.searchRadius;
        if (upgrades.HasSpecialEffect("INCREASE_RANGE_1")) radius += 1;
        if (upgrades.HasSpecialEffect("INCREASE_RANGE_2")) radius += 1;

        var hexesToScan = GetHexesInRadius(myCell.localCoord, radius);
        var emptyCandidates = new List<Vector2Int>();

        var chunkData = mapGen.worldData[myCell.chunkCoord];

        // Zbieranie kandydatów (tylko PUSTE pola, bez budynków i bez dróg)
        foreach (var coord in hexesToScan)
        {
            if (coord == myCell.localCoord) continue;
            if (!chunkData.TryGetValue(coord, out var cellData)) continue;

            var neighborCell = HexMapVisualizer.Instance.GetHexCell(myCell.chunkCoord, coord);
            if (neighborCell == null || neighborCell.HasBuilding()) continue;

            if (cellData.feature == HexFeatureType.None && !cellData.isPath) emptyCandidates.Add(coord);
        }

        // Jeśli mamy gdzie, "siejemy"
        if (emptyCandidates.Count > 0)
        {
            var chosenHex = emptyCandidates[Random.Range(0, emptyCandidates.Count)];
            chunkData[chosenHex].feature = newFeature;
            artificiallyCreatedFeatures.Add(chosenHex);

            Debug.Log($"<color=green>Natura się odradza! Zbudowano {newFeature} na {chosenHex}.</color>");

            // Wymuś odświeżenie grafiki i ponowne przeliczenie statystyk
            RefreshVisualsAndRescan(myCell.chunkCoord, chosenHex);
        }
    }

    /// <summary>
    ///     Losowo niszczy jeden heks zasobu, z którego korzysta ten budynek.
    /// </summary>
    private void TryDestroyResourceHex()
    {
        if (adjacentResourceHexes.Count > 0)
        {
            // Znajdujemy żywą ofiarę z naszej własnej puli pobierania
            var targetToDestroy = adjacentResourceHexes[Random.Range(0, adjacentResourceHexes.Count)];

            var mapGen = Object.FindObjectOfType<HexMapGenerator>();
            if (mapGen != null && mapGen.worldData.ContainsKey(targetToDestroy.chunkCoord))
            {
                var cellData = mapGen.worldData[targetToDestroy.chunkCoord][targetToDestroy.localCoord];

                // Zamiana w pustynię/nicość
                cellData.feature = HexFeatureType.None;

                Debug.LogWarning(
                    $"<color=red>Brutalna wycinka! Zniszczono surowiec na {targetToDestroy.localCoord}.</color>");
                RefreshVisualsAndRescan(targetToDestroy.chunkCoord, targetToDestroy.localCoord);
            }
        }
    }

    /// <summary>
    ///     Nakazuje Visualizerowi zaktualizowanie modelu 3D na mapie i każe sąsiadom przeliczyć bonusy.
    /// </summary>
    private void RefreshVisualsAndRescan(Vector2Int chunkCoord, Vector2Int localCoord)
    {
        var changedCell = HexMapVisualizer.Instance.GetHexCell(chunkCoord, localCoord);
        var mapGen = Object.FindObjectOfType<HexMapGenerator>();

        if (changedCell != null && mapGen != null)
        {
            // 1. Pobieramy aktualne dane o heksie i biomie
            var cellData = mapGen.worldData[chunkCoord][localCoord];
            var currentBiome = BiomeType.Plains; // Domyślnie

            // Hack z refleksją lub szukaniem w słowniku, by pobrać biom (bo mapGen trzyma to "wewnątrz")
            // Skoro HexMapGenerator budował mapę z MapGenerationContext, najprościej użyć pętli 
            // po ustawieniach wizualizatora. Stwórzmy słownik w locie.
            var biomeDict = new Dictionary<BiomeType, Material>();
            foreach (var bs in mapGen.biomeSettings)
                if (bs.biomeGroundMaterial != null && !biomeDict.ContainsKey(bs.type))
                    biomeDict.Add(bs.type, bs.biomeGroundMaterial);

            // Ustawienie biomu (z uproszczeniem, domyślnie bierzemy Plains, 
            // ale jeśli chcesz by było perfekcyjnie, musiałbyś dorzucić chunkBiomes do publicznego dostępu.
            // Poniżej założenie, że pobieramy byle jaki materiał trawy:
            if (biomeDict.Count > 0) currentBiome = biomeDict.Keys.First();

            // 2. Fizycznie przebudowujemy Heks (Usunie stare drzewa, postawi nowe)
            HexMapVisualizer.Instance.RedrawSingleHex(chunkCoord, localCoord, cellData, biomeDict, currentBiome);
        }

        // 3. Informujemy budynki, żeby sprawdziły, czy zyskały/straciły dostęp do tego nowego surowca
        var entity = buildingTransform.GetComponent<BuildingEntity>();
        if (entity != null)
        {
            entity.ForceRescan(); // Zaktualizuje nasze własne dane

            // Poinformuj sąsiadów
            foreach (var nCoord in HexGridMath.GetNeighbors(localCoord))
            {
                var neighborHex = HexMapVisualizer.Instance.GetHexCell(chunkCoord, nCoord);
                if (neighborHex != null) neighborHex.GetComponentInChildren<BuildingEntity>()?.ForceRescan();
            }
        }
    }

    public void DestroyArtificialFeatures()
    {
        var mapGen = Object.FindObjectOfType<HexMapGenerator>();
        var myCell = buildingTransform.GetComponentInParent<HexCell>();

        if (mapGen == null || myCell == null) return;

        var chunkData = mapGen.worldData[myCell.chunkCoord];

        foreach (var hexCoord in artificiallyCreatedFeatures)
            if (chunkData.TryGetValue(hexCoord, out var cellData))
                // Zamieniamy w nicość tylko jeśli inny budynek nie zmienił tego na np. Górę w międzyczasie
                if (cellData.feature == HexFeatureType.Forest || cellData.feature == HexFeatureType.FertileSoil)
                {
                    cellData.feature = HexFeatureType.None;
                    RefreshVisualsAndRescan(myCell.chunkCoord, hexCoord);
                }

        artificiallyCreatedFeatures.Clear();
    }
}