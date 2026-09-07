using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// OutpostEntity
/// Specjalny budynek militarny, który natychmiastowo przekształca chunk w stan FullyUnlocked.
/// Po określonej liczbie poranków daje graczowi darmową możliwość transformacji w budynek mieszkalny.
/// </summary>
public class OutpostEntity : BuildingEntity
{
    [Header("Konfiguracja transformacji")]
    public int daysUntilTransform = 5;
    public List<TransformOption> baseTransformOptions = new();

    [SerializeField] private int daysRemaining;
    [SerializeField] private bool transformAvailable;
    [SerializeField] private Vector2Int chunkCoord;

    private MapExpansionManager expansionManager;

    public bool TransformAvailable => transformAvailable;
    public int DaysRemaining => daysRemaining;
    public Vector2Int ChunkCoord => chunkCoord;
    public List<TransformOption> ActiveOptions { get; private set; } = new();

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (expansionManager != null)
        {
            expansionManager.UnregisterOutpost(this);
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

        expansionManager = MapExpansionManager.Instance;
        daysRemaining = daysUntilTransform;
        ActiveOptions = new List<TransformOption>(baseTransformOptions);

        if (expansionManager != null)
        {
            expansionManager.RegisterOutpost(this);
            expansionManager.OnOutpostBuilt(chunkCoord);
        }
    }

    protected override void HandleProduction()
    {
    }

    protected override void HandleDayReset()
    {
        base.HandleDayReset();

        if (transformAvailable) return;

        daysRemaining--;

        if (daysRemaining <= 0)
        {
            transformAvailable = true;
        }
    }

    public void Transform(TransformOption chosen)
    {
        if (!transformAvailable) return;

        if (chosen.buildingPrefab != null)
        {
            Instantiate(chosen.buildingPrefab, transform.position, transform.rotation, transform.parent);
        }

        Demolish();
    }
}

[Serializable]
public class TransformOption
{
    public string optionId;
    public string displayName;
    [TextArea] public string description;
    public GameObject buildingPrefab;
}