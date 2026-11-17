using System.Collections.Generic;
using UnityEngine;

public class DruidTowerSpawner : MonoBehaviour
{
    public static DruidTowerSpawner Instance { get; private set; }

    [Header("Konfiguracja Spawnu")]
    public GameObject druidTowerPrefab;
    public BuildingData druidTowerBuildingData;
    [Tooltip("Tylko do testów w edytorze bez odblokowanego node'a meta-progresji.")]
    public bool ignoreMetaUnlockForTesting = false;

    public bool HasSpawnedThisSession { get; private set; }

    private bool isSubscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (isSubscribed && MapExpansionManager.Instance != null)
        {
            MapExpansionManager.Instance.OnChunkStateChanged -= HandleChunkStateChanged;
            isSubscribed = false;
        }
    }

    private void TrySubscribe()
    {
        if (!isSubscribed && MapExpansionManager.Instance != null)
        {
            MapExpansionManager.Instance.OnChunkStateChanged += HandleChunkStateChanged;
            isSubscribed = true;
        }
    }

    public static int CalculateSpawnChance(int currentDay)
    {
        if (currentDay <= 10) return 0;
        return Mathf.Clamp((currentDay - 10) * 10, 0, 100);
    }

    public static bool IsMetaUnlocked()
    {
        if (DemoBuildConfig.IsDemoV2Active) return false;
        if (MetaUpgradeManager.Instance == null) return false;
        return MetaUpgradeManager.Instance.IsUnlocked(MetaEffectType.UnlockDruidTower);
    }

    private void HandleChunkStateChanged(Vector2Int chunkCoord, ChunkState newState)
    {
        if (newState != ChunkState.Outskirts) return;
        if (HasSpawnedThisSession) return;
        if (!ignoreMetaUnlockForTesting && !IsMetaUnlocked()) return;

        int currentDay = TimePhaseManager.Instance != null ? TimePhaseManager.Instance.CurrentDay : 1;
        int chance = CalculateSpawnChance(currentDay);
        if (chance <= 0) return;

        int roll = UnityEngine.Random.Range(0, 100);
        if (roll >= chance) return;

        TrySpawnInChunk(chunkCoord);
    }

    public bool TrySpawnInChunk(Vector2Int chunkCoord, System.Random rng = null)
    {
        if (HasSpawnedThisSession) return false;
        if (HexMapGenerator.Instance == null || HexMapGenerator.Instance.worldData == null) return false;
        if (!HexMapGenerator.Instance.worldData.TryGetValue(chunkCoord, out var chunkCells) || chunkCells == null)
            return false;

        HexCellData chosenCellData = SelectTargetCellInChunk(chunkCells, rng);
        if (chosenCellData == null) return false;

        if (HexMapVisualizer.Instance != null)
        {
            HexMapVisualizer.Instance.FlattenHexToGrassland(chunkCoord, chosenCellData.localCoord, chosenCellData);
            HexCell cellComp = HexMapVisualizer.Instance.GetHexCell(chunkCoord, chosenCellData.localCoord);
            if (cellComp != null)
            {
                SpawnDruidTowerOnCell(cellComp);
                HasSpawnedThisSession = true;
                Debug.Log($"[DruidTowerSpawner] Zrujnowana Wieża Druida pojawiła się na chunku {chunkCoord}, hex {chosenCellData.localCoord}.");
                return true;
            }
        }

        return false;
    }

    public static HexCellData SelectTargetCellInChunk(Dictionary<Vector2Int, HexCellData> chunkCells, System.Random rng = null)
    {
        if (chunkCells == null || chunkCells.Count == 0) return null;

        var emptyGrasslandCandidates = new List<HexCellData>();
        var nonRoadFallbackCandidates = new List<HexCellData>();

        foreach (var cell in chunkCells.Values)
        {
            if (cell == null) continue;
            if (cell.isRoad || cell.isPath) continue;
            if (cell.startingBuilding != null || cell.occupyingBuilding != null) continue;

            if (cell.feature == HexFeatureType.None && cell.elevation == 0)
            {
                emptyGrasslandCandidates.Add(cell);
            }
            nonRoadFallbackCandidates.Add(cell);
        }

        List<HexCellData> pool = emptyGrasslandCandidates.Count > 0 ? emptyGrasslandCandidates : nonRoadFallbackCandidates;
        if (pool.Count == 0) return null;

        int idx = rng != null ? rng.Next(0, pool.Count) : UnityEngine.Random.Range(0, pool.Count);
        return pool[idx];
    }

    private void SpawnDruidTowerOnCell(HexCell cell)
    {
        GameObject towerObj;
        if (druidTowerPrefab != null)
        {
            towerObj = Instantiate(druidTowerPrefab, cell.transform.position, Quaternion.identity, cell.transform);
        }
        else
        {
            towerObj = CreateFallbackVisualObject(cell.transform);
        }

        var entity = towerObj.GetComponent<DruidTowerEntity>();
        if (entity == null) entity = towerObj.AddComponent<DruidTowerEntity>();

        BuildingData dataToUse = druidTowerBuildingData != null ? druidTowerBuildingData : CreateFallbackBuildingData();
        entity.Initialize(dataToUse);
        cell.SetBuilding(entity);
    }

    private static GameObject CreateFallbackVisualObject(Transform parent)
    {
        var root = new GameObject("DruidCave_Ruins");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;

        var ruined = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ruined.name = "RuinedVisual";
        ruined.transform.SetParent(root.transform, false);
        ruined.transform.localScale = new Vector3(0.75f, 0.35f, 0.75f);
        ruined.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        var ruinedRenderer = ruined.GetComponent<Renderer>();
        if (ruinedRenderer != null) ruinedRenderer.material.color = new Color(0.45f, 0.48f, 0.42f);

        var active = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        active.name = "ActiveVisual";
        active.transform.SetParent(root.transform, false);
        active.transform.localScale = new Vector3(0.75f, 0.9f, 0.75f);
        active.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        var activeRenderer = active.GetComponent<Renderer>();
        if (activeRenderer != null) activeRenderer.material.color = new Color(0.25f, 0.75f, 0.55f);
        active.SetActive(false);

        var entity = root.AddComponent<DruidTowerEntity>();
        entity.ruinedVisual = ruined;
        entity.activeVisual = active;
        return root;
    }

    private static BuildingData CreateFallbackBuildingData()
    {
        var bd = ScriptableObject.CreateInstance<BuildingData>();
        bd.name = "DruidCaveData";
        bd.buildingName = LocalizationManager.Get("BUILDING_DRUID_CAVE_NAME", "Jama Druida");
        bd.description = LocalizationManager.Get("BUILDING_DRUID_CAVE_DESC", "Starożytna jama szalonego druida pozwalająca rzucać zaklęcia podczas nocy.");
        bd.type = BuildingType.Unique;
        return bd;
    }
}
