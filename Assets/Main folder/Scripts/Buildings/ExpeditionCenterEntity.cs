using UnityEngine;

/// <summary>
///     Centrum Ekspedycyjne – specjalny budynek który:
///     - Rozszerza BuildingEntity (pełna integracja z inspektorem)
///     - Blokuje na stałe 1 elfa z populacji (WorkState.Assigned)
///     - Wysyła misje zwiadowcze (jedną na raz)
///     - Rejestruje się w MapExpansionManager
/// </summary>
public class ExpeditionCenterEntity : BuildingEntity
{
    [Header("── Konfiguracja Ekspedycji ────────────────")]
    public int reservedElves = 1;

    [Header("── Stan (Podgląd) ───────────────────────")] [SerializeField]
    private bool elfIsAssigned;

    [SerializeField] private bool missionActive;
    [SerializeField] private ScoutingMission currentMission;
    private MapExpansionManager expansionManager;

    private Citizen reservedElf;

    // =========================================================================
    // MISJE
    // =========================================================================

    public bool CanSendMission => elfIsAssigned && !missionActive;

    // ─── Gettery publiczne ────────────────────────────────────────────────────
    public bool HasElf => elfIsAssigned;
    public bool IsBusy => missionActive;
    public ScoutingMission ActiveMission => currentMission;

    protected override void OnDestroy()
    {
        base.OnDestroy();
        ReleaseElf();
        expansionManager?.UnregisterExpeditionCenter(this);
    }

    // =========================================================================
    // INICJALIZACJA
    // =========================================================================

    /// <summary>
    ///     Wywoływana przez BuildingPlacer po postawieniu budynku.
    ///     Najpierw inicjalizuje bazowy BuildingEntity, następnie logikę ekspedycji.
    /// </summary>
    public override void Initialize(BuildingData buildingData)
    {
        base.Initialize(buildingData);

        expansionManager = FindObjectOfType<MapExpansionManager>();

        if (expansionManager == null)
        {
            Debug.LogError("[ExpCenter] Brak MapExpansionManager na scenie!");
            return;
        }

        TryReserveElf();
        expansionManager.RegisterExpeditionCenter(this);
    }

    // =========================================================================
    // ZARZĄDZANIE ELFEM
    // =========================================================================

    private void TryReserveElf()
    {
        if (CitizenManager.Instance == null) return;

        reservedElf = CitizenManager.Instance.citizens.Find(c => c.race == Race.Elves && c.workState == WorkState.Idle);

        if (reservedElf != null)
        {
            reservedElf.workState = WorkState.Assigned;
            elfIsAssigned = true;
            Debug.Log("[ExpCenter] Elf zarezerwowany dla Centrum Ekspedycyjnego.");
        }
        else
        {
            elfIsAssigned = false;
            Debug.LogWarning("[ExpCenter] Brak wolnych elfów! Centrum nie może działać.");
        }
    }

    private void ReleaseElf()
    {
        if (reservedElf == null || !elfIsAssigned) return;
        reservedElf.workState = WorkState.Idle;
        reservedElf = null;
        elfIsAssigned = false;
    }

    /// <summary>Wywołuj tylko przez MapExpansionManager.</summary>
    public bool StartMission(ScoutingMission mission)
    {
        if (!CanSendMission)
        {
            Debug.LogWarning("[ExpCenter] Centrum zajęte lub brak elfa.");
            return false;
        }

        currentMission = mission;
        missionActive = true;
        Debug.Log($"[ExpCenter] Wysłano: {mission}");
        return true;
    }

    public void OnMissionComplete(ScoutingMission mission)
    {
        currentMission = null;
        missionActive = false;
        Debug.Log($"[ExpCenter] Misja zakończona: chunk {mission.targetChunk} odkryty.");
    }
}