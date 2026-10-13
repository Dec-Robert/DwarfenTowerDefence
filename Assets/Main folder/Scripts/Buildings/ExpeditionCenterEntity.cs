using UnityEngine;

/// <summary>
/// Budynek: Centrum Ekspedycyjne / Obóz Zwiadowców.
/// Pasywnie zapewnia określoną liczbę ekwipunku zwiadowczego (bazowo +1).
/// Ekwipunek zasila globalną pulę osady w MapExpansionManager.
/// Blokuje rozbiórkę, jeśli jego ekwipunek jest aktualnie w terenie na misjach.
/// </summary>
public class ExpeditionCenterEntity : BuildingEntity
{
    [Header("Ekwipunek Zwiadowczy")]
    [SerializeField] private int equipmentProvided = 1;

    public int EquipmentProvided => (isBuildingActive && gameObject.activeInHierarchy) ? equipmentProvided : 0;

    public bool CanSendMission => MapExpansionManager.Instance != null && MapExpansionManager.Instance.AvailableEquipment >= 2;

    public override void Initialize(BuildingData buildingData)
    {
        base.Initialize(buildingData);
        RegisterToExpansionManager();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        RegisterToExpansionManager();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        UnregisterFromExpansionManager();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        UnregisterFromExpansionManager();
    }

    private void RegisterToExpansionManager()
    {
        if (MapExpansionManager.Instance != null)
        {
            MapExpansionManager.Instance.RegisterExpeditionCenter(this);
        }
    }

    private void UnregisterFromExpansionManager()
    {
        if (MapExpansionManager.Instance != null)
        {
            MapExpansionManager.Instance.UnregisterExpeditionCenter(this);
        }
    }

    public override bool CanDemolish()
    {
        if (MapExpansionManager.Instance == null) return true;
        return MapExpansionManager.Instance.CanDemolishCenter(this);
    }

    /// <summary>
    /// Metoda pomocnicza dla wysyłania misji (np. z poziomu UI budynku).
    /// </summary>
    public bool StartMission(Vector2Int targetChunk, int elfCount = 2)
    {
        if (MapExpansionManager.Instance == null) return false;
        return MapExpansionManager.Instance.TryStartSurvey(targetChunk, elfCount);
    }
}