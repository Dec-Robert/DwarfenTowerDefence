using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Posterunek – specjalny budynek który:
///     1. Dziedziczy po BuildingEntity (automatyczna inicjalizacja przez BuildingPlacer)
///     2. Natychmiast odblokowuje pełne budownictwo na chunku (FullyUnlocked)
///     3. Po 10 dniach oferuje bezpłatną transformację w 1 z 3 budynków mieszkalnych
/// </summary>
public class OutpostEntity : BuildingEntity
{

    /*
    public int daysUntilTransform = 5;
    
    public List<TransformOption> baseTransformOptions = new();

    [SerializeField] private int daysRemaining;

    [SerializeField] private bool transformAvailable;
    [SerializeField] private Vector2Int chunkCoord;

    private MapExpansionManager expansionManager;

    // Gettery dla interfejsu
    public bool TransformAvailable => transformAvailable;
    public int DaysRemaining => daysRemaining;
    public Vector2Int ChunkCoord => chunkCoord;
    public List<TransformOption> ActiveOptions { get; private set; } = new();

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (expansionManager != null)
            expansionManager.UnregisterOutpost(this);
    }

    // =========================================================================
    // INICJALIZACJA (Nadpisanie z BuildingEntity)
    // =========================================================================

    public override void Initialize(BuildingData buildingData)
    {
        // 1. Inicjalizacja bazowa (tworzy niezbędne minimum)
        base.Initialize(buildingData);

        // 2. Szukamy HexCell pod nami, by wiedzieć gdzie stoimy
        var myCell = GetComponentInParent<HexCell>();
        if (myCell != null) chunkCoord = myCell.chunkCoord;

        // 3. Konfiguracja Posterunku
        expansionManager = FindObjectOfType<MapExpansionManager>();
        daysRemaining = daysUntilTransform;
        ActiveOptions = new List<TransformOption>(baseTransformOptions);

        if (expansionManager != null)
        {
            expansionManager.RegisterOutpost(this);
            // BARDZO WAŻNE: Odblokowanie chunka!
            expansionManager.OnOutpostBuilt(chunkCoord);
        }

        Debug.Log($"[Outpost] Posterunek zbudowany na {chunkCoord}. Transformacja za {daysRemaining} dni.");
    }

    // Does not generate resources
    protected override void HandleProduction()
    {
    }

    // =========================================================================
    // TICK DZIENNY
    // =========================================================================

    public void OnDayPassed()
    {
        if (transformAvailable) return;

        daysRemaining--;

        if (daysRemaining <= 0)
        {
            transformAvailable = true;
            Debug.Log($"[Outpost] {chunkCoord} gotowy do transformacji!");

            // Opcjonalnie: automatyczne otwarcie popupu, albo poczekanie aż gracz kliknie
            // expansionManager.OnOutpostReadyToTransform(this, activeOptions);
        }
        
    }

    // =========================================================================
    // TRANSFORMACJA
    // =========================================================================

    public void Transform(TransformOption chosen)
    {
        if (!transformAvailable) return;

        if (chosen.buildingPrefab != null)
            Instantiate(chosen.buildingPrefab, transform.position, transform.rotation, transform.parent);

        expansionManager?.OnOutpostTransformed(chunkCoord);
        Demolish(); // Niszczy ten budynek
    }
    */
}

[Serializable]
public class TransformOption
{
    public string optionId;
    public string displayName;
    [TextArea] public string description;
    public GameObject buildingPrefab;
}