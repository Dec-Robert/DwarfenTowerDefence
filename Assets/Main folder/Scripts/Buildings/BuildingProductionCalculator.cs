using System.Linq;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Precyzyjny kalkulator szacowanej produkcji surowców dla budynku w danej lokalizacji.
/// Uwzględnia:
/// - Produkcję bazową (BuildingData.productionPerCycle),
/// - Bonusy terenowe stojącego heksa (on-top: cechy terenu oraz wzniesienie),
/// - Bonusy z sąsiednich heksów (adjacent: cechy terenu oraz wzniesienia),
/// - Kary za eksploatację surowca przez inne istniejące budynki wokół (penaltyPerUser / penaltyPerExtraBuilding),
/// - Brak możliwości czerpania z heksów już zabudowanych przez inne obiekty,
/// - Mnożniki meta-ulepszeń (BuildingTerrainBonusMultiplier, SpecificBuildingProductionBonus) oraz Ogniska (Beacon),
/// - Pełną wydajność załogi 100% (mnożnik 1.0f).
/// </summary>
public static class BuildingProductionCalculator
{
    public struct NeighborBuildingImpact
    {
        public BuildingEntity building;
        public ResourceType resourceType;
        public float currentProduction;
        public float projectedProduction;
        public float difference => projectedProduction - currentProduction;
    }

    public struct ResourceProductionEstimate
    {
        public ResourceType resourceType;
        public float baseProduction;
        public float onTopBonus;
        public float adjacentNetBonus;
        public float adjacentGrossBonus;
        public float neighborPenalty;
        public float terrainMultiplier;
        public float globalMultiplier;
        public float totalProduction; // Przy 100% wydajności
    }

    /// <summary>
    /// Sprawdza, czy budynek posiada jakąkolwiek potencjalną produkcję surowców lub reguły terenowe.
    /// </summary>
    public static bool HasProduction(BuildingData data)
    {
        if (ReferenceEquals(data, null)) return false;
        if (data.type == BuildingType.Defense || data is TowerData) return false;
        if (data.type == BuildingType.Housing || data is HousingBuildingData) return false;

        bool hasBaseProd = data.productionPerCycle != null && data.productionPerCycle.Count > 0;
        bool hasBonusRules = data.bonusRules != null && data.bonusRules.Count > 0;
        bool hasElevation = data.elevationBonus.enabled;

        return hasBaseProd || hasBonusRules || hasElevation || data.type == BuildingType.Economic;
    }

    /// <summary>
    /// Oblicza szacowaną produkcję surowców (dla każdego produkowanego surowca) przy postawieniu budynku na wskazanym heksie.
    /// </summary>
    public static List<ResourceProductionEstimate> CalculateProjectedProduction(
        HexCell cell,
        BuildingData data,
        HexMapGenerator mapGen = null,
        HexMapVisualizer visualizer = null)
    {
        var results = new List<ResourceProductionEstimate>();
        if (ReferenceEquals(cell, null) || ReferenceEquals(data, null)) return results;
        if (!HasProduction(data)) return results;

        if (ReferenceEquals(mapGen, null))
        {
            mapGen = HexMapGenerator.Instance;
        }

        if (ReferenceEquals(visualizer, null))
        {
            visualizer = HexMapVisualizer.Instance;
        }

        Dictionary<Vector2Int, HexCellData> chunkData = null;
        if (!ReferenceEquals(mapGen, null) && mapGen.worldData != null && mapGen.worldData.ContainsKey(cell.chunkCoord))
        {
            chunkData = mapGen.worldData[cell.chunkCoord];
        }

        var onTopBonuses = new Dictionary<ResourceType, float>();
        var adjacentNetBonuses = new Dictionary<ResourceType, float>();
        var adjacentGrossBonuses = new Dictionary<ResourceType, float>();
        var neighborPenalties = new Dictionary<ResourceType, float>();

        var rules = data.GetEffectiveBonusRules();

        // 1. Bonusy On-Top (cecha terenu na własnym heksie)
        if (rules != null && rules.Count > 0 && chunkData != null && chunkData.TryGetValue(cell.localCoord, out var myHexData))
        {
            foreach (var rule in rules)
            {
                if (rule.requiredFeature == myHexData.feature && rule.resourceYields != null)
                {
                    foreach (var yield in rule.resourceYields)
                    {
                        if (yield.onTopProductionBonus > 0f)
                        {
                            AddAmount(onTopBonuses, yield.resourceType, yield.onTopProductionBonus);
                        }
                    }
                }
            }
        }

        // 2. Bonus On-Top z wysokości (Kamieniołom)
        int ownElevation = cell.Elevation;
        if (ownElevation <= 0 && chunkData != null && chunkData.TryGetValue(cell.localCoord, out var ownHexData))
        {
            ownElevation = ownHexData.elevation;
        }

        if (data.elevationBonus.enabled && ownElevation > 0)
        {
            int existingUsers = BuildingProductionRegistry.GetUsageCount(cell);
            float penalty = data.elevationBonus.penaltyPerExtraBuilding > 0f ? data.elevationBonus.penaltyPerExtraBuilding : 3f;
            float effectiveLevel = Mathf.Max(0f, ownElevation - penalty * existingUsers);
            float onTopYield = effectiveLevel * data.elevationBonus.onTopBonusPerLevel;

            if (onTopYield > 0f)
            {
                AddAmount(onTopBonuses, data.elevationBonus.resourceType, onTopYield);
            }
        }

        // 3. Bonusy z sąsiednich heksów (cechy terenu, np. Las dla Tartaku, Góra dla Kopalni)
        int chunkRadius = mapGen != null ? mapGen.chunkRadius : 3;
        if (rules != null && rules.Count > 0 && mapGen != null && mapGen.worldData != null)
        {
            foreach (var rule in rules)
            {
                if (rule.requiredFeature == HexFeatureType.None || rule.resourceYields == null) continue;

                int radius = rule.searchRadius <= 0 ? 1 : rule.searchRadius;
                var hexesToScan = HexGridMath.GetHexesInRadiusCrossChunk(cell.chunkCoord, cell.localCoord, radius, chunkRadius);

                foreach (var nInfo in hexesToScan)
                {
                    if (nInfo.chunkCoord == cell.chunkCoord && nInfo.localCoord == cell.localCoord) continue;
                    if (nInfo.chunkCoord != cell.chunkCoord && !HexGridMath.IsChunkRevealed(nInfo.chunkCoord)) continue;

                    if (!mapGen.worldData.TryGetValue(nInfo.chunkCoord, out var targetChunkData)) continue;
                    if (!targetChunkData.TryGetValue(nInfo.localCoord, out var neighborData)) continue;
                    if (neighborData.feature != rule.requiredFeature) continue;

                    var neighborCell = !ReferenceEquals(visualizer, null)
                        ? visualizer.GetHexCell(nInfo.chunkCoord, nInfo.localCoord)
                        : null;

                    int existingUsers = 0;
                    if (!ReferenceEquals(neighborCell, null))
                    {
                        bool isCellOccupied = false;
                        if (!ReferenceEquals(neighborCell.OccupyingBuilding, null))
                        {
                            isCellOccupied = true;
                        }
                        else if (neighborCell.Data != null && !ReferenceEquals(neighborCell.Data.occupyingBuilding, null))
                        {
                            isCellOccupied = true;
                        }
                        else
                        {
                            try { isCellOccupied = neighborCell.HasBuilding(); } catch { }
                        }

                        if (isCellOccupied) continue;
                        existingUsers = BuildingProductionRegistry.GetUsageCount(neighborCell);
                    }
                    else if (neighborData.occupyingBuilding != null)
                    {
                        continue;
                    }

                    foreach (var yield in rule.resourceYields)
                    {
                        float grossYield = yield.baseBonusPerHex;
                        float penalty = yield.penaltyPerUser * existingUsers;
                        float netYield = Mathf.Max(yield.minBonus, grossYield - penalty);

                        if (netYield > 0f)
                        {
                            AddAmount(adjacentNetBonuses, yield.resourceType, netYield);
                            AddAmount(adjacentGrossBonuses, yield.resourceType, grossYield);
                            float penaltyAmt = Mathf.Max(0f, grossYield - netYield);
                            if (penaltyAmt > 0f)
                            {
                                AddAmount(neighborPenalties, yield.resourceType, penaltyAmt);
                            }
                        }
                    }
                }
            }
        }

        // 4. Bonusy z sąsiednich wzgórz (np. Kamieniołom)
        if (data.elevationBonus.enabled && mapGen != null && mapGen.worldData != null)
        {
            int radius = data.elevationBonus.searchRadius <= 0 ? 1 : data.elevationBonus.searchRadius;
            var hexesToScan = HexGridMath.GetHexesInRadiusCrossChunk(cell.chunkCoord, cell.localCoord, radius, chunkRadius);

            foreach (var nInfo in hexesToScan)
            {
                if (nInfo.chunkCoord == cell.chunkCoord && nInfo.localCoord == cell.localCoord) continue;
                if (nInfo.chunkCoord != cell.chunkCoord && !HexGridMath.IsChunkRevealed(nInfo.chunkCoord)) continue;

                if (!mapGen.worldData.TryGetValue(nInfo.chunkCoord, out var targetChunkData)) continue;
                if (!targetChunkData.TryGetValue(nInfo.localCoord, out var neighborData)) continue;

                int elevation = neighborData.elevation;
                var neighborCell = !ReferenceEquals(visualizer, null)
                    ? visualizer.GetHexCell(nInfo.chunkCoord, nInfo.localCoord)
                    : null;

                if (!ReferenceEquals(neighborCell, null))
                {
                    elevation = neighborCell.Elevation;
                }

                if (elevation <= 0) continue;

                int existingUsers = !ReferenceEquals(neighborCell, null) ? BuildingProductionRegistry.GetUsageCount(neighborCell) : 0;
                float penaltyPerBuilding = data.elevationBonus.penaltyPerExtraBuilding > 0f ? data.elevationBonus.penaltyPerExtraBuilding : 3f;
                float effectiveLevel = Mathf.Max(0f, elevation - penaltyPerBuilding * existingUsers);
                float netYield = effectiveLevel * data.elevationBonus.adjacentBonusPerLevel;

                float grossYield = elevation * data.elevationBonus.adjacentBonusPerLevel;

                if (netYield > 0f)
                {
                    var rType = data.elevationBonus.resourceType;
                    AddAmount(adjacentNetBonuses, rType, netYield);
                    AddAmount(adjacentGrossBonuses, rType, grossYield);
                    float penaltyAmt = Mathf.Max(0f, grossYield - netYield);
                    if (penaltyAmt > 0f)
                    {
                        AddAmount(neighborPenalties, rType, penaltyAmt);
                    }
                }
            }
        }

        // 5. Mnożniki Meta i Globalne
        float metaTerrainBonus = 0f;
        float metaProdBonus = 0f;
        if (MetaUpgradeManager.Instance != null)
        {
            metaTerrainBonus = MetaUpgradeManager.Instance.GetBuildingValue(MetaEffectType.BuildingTerrainBonusMultiplier, data);
            metaProdBonus = MetaUpgradeManager.Instance.GetBuildingValue(MetaEffectType.SpecificBuildingProductionBonus, data);
        }

        float terrainMultiplier = Mathf.Max(0.1f, 1f + metaTerrainBonus);
        float hearthBonus = BeaconEntity.GetActiveDayProductionBonus();
        float globalMultiplier = Mathf.Max(0.1f, 1f + metaProdBonus + hearthBonus);

        // 6. Zestawienie wszystkich produkowanych surowców
        var allResources = new HashSet<ResourceType>();
        if (data.productionPerCycle != null)
        {
            foreach (var cost in data.productionPerCycle)
            {
                allResources.Add(cost.type);
            }
        }
        foreach (var r in onTopBonuses.Keys) allResources.Add(r);
        foreach (var r in adjacentNetBonuses.Keys) allResources.Add(r);

        foreach (var rType in allResources)
        {
            float baseAmt = 0f;
            if (data.productionPerCycle != null)
            {
                foreach (var cost in data.productionPerCycle)
                {
                    if (cost.type == rType) baseAmt += cost.amount;
                }
            }

            float onTop = onTopBonuses.TryGetValue(rType, out float ot) ? ot : 0f;
            float adjNet = adjacentNetBonuses.TryGetValue(rType, out float an) ? an : 0f;
            float adjGross = adjacentGrossBonuses.TryGetValue(rType, out float ag) ? ag : 0f;
            float pen = neighborPenalties.TryGetValue(rType, out float p) ? p : 0f;

            float totalTerrain = (onTop + adjNet) * terrainMultiplier;
            // Maksymalnie na 100% wydajności załogi (1.0f)
            float total = (baseAmt + totalTerrain) * globalMultiplier * 1.0f;

            results.Add(new ResourceProductionEstimate
            {
                resourceType = rType,
                baseProduction = baseAmt,
                onTopBonus = onTop,
                adjacentNetBonus = adjNet,
                adjacentGrossBonus = adjGross,
                neighborPenalty = pen,
                terrainMultiplier = terrainMultiplier,
                globalMultiplier = globalMultiplier,
                totalProduction = total
            });
        }

        return results;
    }

    private static List<Vector2Int> GetHexesInRadius(Vector2Int center, int radius)
    {
        var results = new List<Vector2Int>();
        for (int q = -radius; q <= radius; q++)
        {
            int r1 = Mathf.Max(-radius, -q - radius);
            int r2 = Mathf.Min(radius, -q + radius);
            for (int r = r1; r <= r2; r++)
            {
                results.Add(new Vector2Int(center.x + q, center.y + r));
            }
        }
        return results;
    }

    /// <summary>
    /// Pobiera heksy surowcow, z ktorych czerpalby budynek candidateData postawiony na candidateCell.
    /// </summary>
    public static HashSet<HexCell> GetClaimedResourceHexes(
        HexCell cell,
        BuildingData data,
        HexMapGenerator mapGen = null,
        HexMapVisualizer visualizer = null)
    {
        var claimedHexes = new HashSet<HexCell>();
        if (cell == null || data == null) return claimedHexes;

        if (mapGen == null) mapGen = HexMapGenerator.Instance;
        if (visualizer == null) visualizer = HexMapVisualizer.Instance;
        if (mapGen == null || mapGen.worldData == null) return claimedHexes;

        int chunkRadius = mapGen != null ? mapGen.chunkRadius : 3;
        var rules = data.GetEffectiveBonusRules();

        if (rules != null && rules.Count > 0)
        {
            foreach (var rule in rules)
            {
                if (rule.requiredFeature == HexFeatureType.None || rule.resourceYields == null) continue;

                int radius = rule.searchRadius <= 0 ? 1 : rule.searchRadius;
                var hexesToScan = HexGridMath.GetHexesInRadiusCrossChunk(cell.chunkCoord, cell.localCoord, radius, chunkRadius);

                foreach (var nInfo in hexesToScan)
                {
                    if (nInfo.chunkCoord == cell.chunkCoord && nInfo.localCoord == cell.localCoord) continue;
                    if (nInfo.chunkCoord != cell.chunkCoord && !HexGridMath.IsChunkRevealed(nInfo.chunkCoord)) continue;

                    if (!mapGen.worldData.TryGetValue(nInfo.chunkCoord, out var targetChunkData)) continue;
                    if (!targetChunkData.TryGetValue(nInfo.localCoord, out var neighborData)) continue;
                    if (neighborData.feature != rule.requiredFeature) continue;

                    var neighborCell = visualizer != null ? visualizer.GetHexCell(nInfo.chunkCoord, nInfo.localCoord) : null;
                    if (neighborCell == null) continue;

                    if (!neighborCell.HasBuilding())
                    {
                        claimedHexes.Add(neighborCell);
                    }
                }
            }
        }

        if (data.elevationBonus.enabled)
        {
            int radius = data.elevationBonus.searchRadius <= 0 ? 1 : data.elevationBonus.searchRadius;
            var hexesToScan = HexGridMath.GetHexesInRadiusCrossChunk(cell.chunkCoord, cell.localCoord, radius, chunkRadius);

            foreach (var nInfo in hexesToScan)
            {
                if (nInfo.chunkCoord == cell.chunkCoord && nInfo.localCoord == cell.localCoord) continue;
                if (nInfo.chunkCoord != cell.chunkCoord && !HexGridMath.IsChunkRevealed(nInfo.chunkCoord)) continue;

                if (!mapGen.worldData.TryGetValue(nInfo.chunkCoord, out var targetChunkData)) continue;
                if (!targetChunkData.TryGetValue(nInfo.localCoord, out var neighborData)) continue;

                var neighborCell = visualizer != null ? visualizer.GetHexCell(nInfo.chunkCoord, nInfo.localCoord) : null;
                if (neighborCell == null) continue;

                if (neighborCell.Elevation > 0)
                {
                    claimedHexes.Add(neighborCell);
                }
            }
        }

        return claimedHexes;
    }

    public static Dictionary<BuildingEntity, List<NeighborBuildingImpact>> CalculateImpactsOnExistingBuildings(
        HexCell candidateCell,
        BuildingData candidateData,
        HexMapGenerator mapGen = null,
        HexMapVisualizer visualizer = null)
    {
        var results = new Dictionary<BuildingEntity, List<NeighborBuildingImpact>>();
        if (candidateCell == null || candidateData == null || !HasProduction(candidateData))
        {
            return results;
        }

        var candidateClaimedHexes = GetClaimedResourceHexes(candidateCell, candidateData, mapGen, visualizer);

        IReadOnlyList<BuildingEntity> allBuildings = null;
        if (BuildingRegistry.Instance != null)
        {
            allBuildings = BuildingRegistry.Instance.AllBuildings;
        }
        else
        {
            allBuildings = Object.FindObjectsByType<BuildingEntity>(FindObjectsSortMode.None);
        }

        if (allBuildings == null || allBuildings.Count == 0) return results;

        Vector3 candidatePos = candidateCell.transform.position;

        foreach (var building in allBuildings)
        {
            if (building == null || !building.isBuildingActive || building.data == null ||
                building.Terrain == null || building.Production == null)
            {
                continue;
            }

            // Szybki filtr odleglosci - budynek dalej niz 20f nie moze wejsc w interakcje
            if (Vector3.Distance(building.transform.position, candidatePos) > 20f)
            {
                continue;
            }

            bool isInteracting = false;
            var adjRes = building.Terrain.AdjacentResourceHexes;
            var adjElev = building.Terrain.AdjacentElevationHexes;
            var ownCell = building.Terrain.RegisteredOwnCell;

            // 1. Czy candidateCell zabiera heks istniejacemu budynkowi?
            if (adjRes != null && adjRes.ContainsKey(candidateCell))
            {
                isInteracting = true;
            }
            else if (adjElev != null && adjElev.Contains(candidateCell))
            {
                isInteracting = true;
            }

            // 2. Czy candidateClaimedHexes nakladaja sie na heksy uzywane przez ten budynek?
            if (!isInteracting && candidateClaimedHexes.Count > 0)
            {
                if (ownCell != null && candidateClaimedHexes.Contains(ownCell))
                {
                    isInteracting = true;
                }
                else if (adjRes != null)
                {
                    foreach (var h in adjRes.Keys)
                    {
                        if (h != null && candidateClaimedHexes.Contains(h))
                        {
                            isInteracting = true;
                            break;
                        }
                    }
                }

                if (!isInteracting && adjElev != null)
                {
                    foreach (var h in adjElev)
                    {
                        if (h != null && candidateClaimedHexes.Contains(h))
                        {
                            isInteracting = true;
                            break;
                        }
                    }
                }
            }

            if (!isInteracting) continue;

            var curProd = building.GetEffectiveCurrentProduction();
            var projProd = building.SimulateProjectedProduction(candidateCell, candidateClaimedHexes);

            var impacts = new List<NeighborBuildingImpact>();
            var allTypes = new HashSet<ResourceType>(curProd.Keys);
            foreach (var t in projProd.Keys) allTypes.Add(t);

            foreach (var rType in allTypes)
            {
                float curVal = curProd.TryGetValue(rType, out float cv) ? cv : 0f;
                float projVal = projProd.TryGetValue(rType, out float pv) ? pv : 0f;

                if (Mathf.Abs(projVal - curVal) > 0.05f)
                {
                    impacts.Add(new NeighborBuildingImpact
                    {
                        building = building,
                        resourceType = rType,
                        currentProduction = curVal,
                        projectedProduction = projVal
                    });
                }
            }

            if (impacts.Count > 0)
            {
                results[building] = impacts;
            }
        }

        return results;
    }

    private static void AddAmount(Dictionary<ResourceType, float> dict, ResourceType type, float amount)
    {
        if (dict.ContainsKey(type)) dict[type] += amount;
        else dict.Add(type, amount);
    }
}
