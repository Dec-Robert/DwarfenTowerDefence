using UnityEngine;

public class ExpeditionCenterEntity : BuildingEntity
{
    [SerializeField] private bool elfAssigned;
    [SerializeField] private bool missionActive;
    [SerializeField] private Vector2Int currentMissionTarget = new Vector2Int(-999, -999);

    private Citizen reservedElf;

    public bool HasElf => elfAssigned;
    public bool IsBusy => missionActive;
    public bool CanSendMission => elfAssigned && !missionActive;
    public Vector2Int ActiveTarget => currentMissionTarget;

    public override void Initialize(BuildingData buildingData)
    {
        base.Initialize(buildingData);

        TryReserveElf();

        if (MapExpansionManager.Instance != null)
        {
            MapExpansionManager.Instance.OnChunkStateChanged += HandleChunkStateChanged;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (MapExpansionManager.Instance != null)
        {
            MapExpansionManager.Instance.OnChunkStateChanged -= HandleChunkStateChanged;

            if (missionActive && currentMissionTarget.x != -999)
            {
                MapExpansionManager.Instance.SetChunkBaseState(currentMissionTarget, ChunkState.Borderlands);
            }
        }

        ReleaseElf();
    }

    private void TryReserveElf()
    {
        if (CitizenManager.Instance == null) return;

        reservedElf = CitizenManager.Instance.citizens.Find(c => c.race == Race.Elves && c.workState == WorkState.Idle);

        if (reservedElf != null)
        {
            reservedElf.workState = WorkState.Assigned;
            elfAssigned = true;
        }
        else
        {
            elfAssigned = false;
        }
    }

    private void ReleaseElf()
    {
        if (reservedElf == null || !elfAssigned) return;

        reservedElf.workState = WorkState.Idle;
        reservedElf = null;
        elfAssigned = false;
    }

    public bool StartMission(Vector2Int targetChunk, int daysRequired)
    {
        if (!CanSendMission) return false;
        if (MapExpansionManager.Instance == null) return false;

        bool started = MapExpansionManager.Instance.TryStartSurvey(targetChunk, daysRequired);
        if (!started) return false;

        currentMissionTarget = targetChunk;
        missionActive = true;
        return true;
    }

    private void HandleChunkStateChanged(Vector2Int coord, ChunkState newState)
    {
        if (!missionActive || coord != currentMissionTarget) return;

        if (newState == ChunkState.Outskirts)
        {
            missionActive = false;
            currentMissionTarget = new Vector2Int(-999, -999);
        }
    }
}