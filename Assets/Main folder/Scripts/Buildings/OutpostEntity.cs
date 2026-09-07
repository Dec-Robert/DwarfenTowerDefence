using System;
using System.Collections.Generic;
using UnityEngine;

public class OutpostEntity : BuildingEntity
{
    [Header("Konfiguracja transformacji")]
    public int daysUntilTransform = 2;
    public List<TransformOption> baseTransformOptions = new();

    [SerializeField] private bool transformAvailable;
    [SerializeField] private Vector2Int chunkCoord;

    public bool TransformAvailable => transformAvailable;
    public Vector2Int ChunkCoord => chunkCoord;
    public List<TransformOption> ActiveOptions { get; private set; } = new();

    public int DaysRemaining
    {
        get
        {
            if (MapExpansionManager.Instance == null) return 0;
            var data = MapExpansionManager.Instance.GetChunkData(chunkCoord);
            return data != null ? data.outpostSettlementDaysRemaining : 0;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (MapExpansionManager.Instance != null)
        {
            MapExpansionManager.Instance.UnregisterOutpost(this, chunkCoord);
        }
    }

    public override void Initialize(BuildingData buildingData)
    {
        base.Initialize(buildingData);

        var myCell = GetComponentInParent<HexCell>();
        if (myCell != null)
        {
            chunkCoord = myCell.chunkCoord;
        }

        ActiveOptions = new List<TransformOption>(baseTransformOptions);

        if (MapExpansionManager.Instance != null)
        {
            MapExpansionManager.Instance.RegisterOutpost(this, chunkCoord, daysUntilTransform);
        }
    }

    protected override void HandleProduction()
    {
    }

    public void CompleteSettlement()
    {
        transformAvailable = true;
    }

    public void Transform(TransformOption chosen)
    {
        if (!transformAvailable || chosen == null || chosen.buildingPrefab == null) return;

        HexCell cell = GetComponentInParent<HexCell>();
        Transform parentTransform = cell != null ? cell.transform : transform.parent;
        Vector3 spawnPos = transform.position;
        Quaternion spawnRot = transform.rotation;

        Demolish();

        GameObject newBuildingObj = Instantiate(chosen.buildingPrefab, spawnPos, spawnRot, parentTransform);
        BuildingEntity newEntity = newBuildingObj.GetComponent<BuildingEntity>();

        if (newEntity != null)
        {
            BuildingData targetData = chosen.buildingData != null ? chosen.buildingData : newEntity.data;
            if (targetData != null)
            {
                newEntity.Initialize(targetData);
            }
        }
    }
}

[Serializable]
public class TransformOption
{
    public string optionId;
    public string displayName;
    [TextArea] public string description;
    public GameObject buildingPrefab;
    public BuildingData buildingData;
}