using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
///     Cienki koordynator interakcji gracza z mapą.
///     Odpowiada wyłącznie za:
///     - obsługę inputu (kliknięcia, PPM, Shift),
///     - przełączanie między trybem budowania i inspekcji,
///     - zarządzanie selekcją wieży (wskaźnik zasięgu).
///     Cała logika wizualna i budowlana żyje w:
///     BuildingGhostController, BuildingPlacer, HexHighlighter.
/// </summary>
public class InteractionManager : MonoBehaviour
{
    [Header("Referencje")] public HexMapGenerator mapGenerator;

    public MapExpansionManager expansionManager;
    public LayerMask hexLayer;

    [Header("Widmo")] public Material ghostValidMat;

    public Material ghostInvalidMat;
    public GameObject rangeVisualizerPrefab;

    [Header("Podświetlenie zasobów")] public Material resourceHighlightMat;

    [Header("Wizualia Zasobów")]
    public ResourceVisualsSO resourceVisualsSO;

    private BuildingGhostController ghost;
    private HexHighlighter highlighter;

    private TowerController lastSelectedTower;
    private BuildingPlacer placer;
    public static InteractionManager Instance { get; private set; }

    [Header("Tryb wyboru heksa docelowego")]
    public bool IsSelectingHexTarget => isSelectingHexTarget;
    private bool isSelectingHexTarget = false;
    private TowerController hexTargetingTower;
    private System.Action onHexTargetSelectionCallback;
    private HexCell hoveredTargetHex;

    public BuildingData SelectedBuilding { get; private set; }
    

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (resourceVisualsSO == null)
        {
#if UNITY_EDITOR
            resourceVisualsSO = UnityEditor.AssetDatabase.LoadAssetAtPath<ResourceVisualsSO>(
                "Assets/Main folder/ScriptableObjects/Configs/ResourceVisualDatabase.asset");
#endif
            if (resourceVisualsSO == null)
            {
                var allVisuals = Resources.FindObjectsOfTypeAll<ResourceVisualsSO>();
                if (allVisuals != null && allVisuals.Length > 0) resourceVisualsSO = allVisuals[0];
            }
        }

        ghost = new BuildingGhostController(ghostValidMat, ghostInvalidMat, rangeVisualizerPrefab, resourceVisualsSO);
        placer = new BuildingPlacer(mapGenerator);
        highlighter = new HexHighlighter(mapGenerator, resourceHighlightMat);
    }

    private void Update()
    {
        // PPM – anuluj budowanie lub wybór celu heksa
        if (Input.GetMouseButtonDown(1))
        {
            if (isSelectingHexTarget)
            {
                CancelHexTargetSelection();
                return;
            }

            CancelBuilding();
            return;
        }

        // Aktualizacja podglądu heksa w trybie wyboru celu punktowego
        if (isSelectingHexTarget)
            UpdateHexTargetSelectionHover();

        // Aktualizacja widma jeśli jesteśmy w trybie budowania
        if (SelectedBuilding != null && ghost.IsActive)
            UpdateGhostPosition();

        // LPM – obsługa kliknięcia
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            HandleClick();
        }
    }
    
    // =========================================================================
    // Tryb budowania
    // =========================================================================

    public void SelectBuildingToBuild(BuildingData data)
    {
        ghost.Clear();
        SelectedBuilding = data;
        ghost.CreateGhost(data);
        Debug.Log($"[Interaction] Tryb budowania: {data.buildingName}");
    }

    public void CancelBuilding()
    {
        SelectedBuilding = null;
        ghost.Clear();
        highlighter.Clear();
        Debug.Log("[Interaction] Anulowano budowanie.");
    }

    private void UpdateGhostPosition()
    {
        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out var hit, 1000f, hexLayer))
        {
            ghost.Hide();
            highlighter.Clear();
            return;
        }

        var cell = hit.collider.GetComponentInParent<HexCell>();
        if (cell == null) return;

        var isValid = placer.IsPlacementValid(cell, SelectedBuilding);
        ghost.MoveTo(cell.transform.position, isValid, cell, SelectedBuilding);
        highlighter.HighlightFor(SelectedBuilding, cell.chunkCoord, cell.localCoord);
    }

    // =========================================================================
    // Obsługa kliknięcia
    // =========================================================================

    public void StartHexTargetSelection(TowerController tower, System.Action onComplete)
    {
        if (tower == null) return;

        if (SelectedBuilding != null)
            CancelBuilding();

        isSelectingHexTarget = true;
        hexTargetingTower = tower;
        onHexTargetSelectionCallback = onComplete;
        tower.ShowRangeIndicator(true);
        Debug.Log($"[Interaction] Rozpoczęto wybór heksa docelowego dla: {tower.name}");
    }

    public void CancelHexTargetSelection()
    {
        if (hoveredTargetHex != null)
        {
            hoveredTargetHex.ToggleHighlight(false);
            hoveredTargetHex = null;
        }

        isSelectingHexTarget = false;
        var cb = onHexTargetSelectionCallback;
        onHexTargetSelectionCallback = null;
        cb?.Invoke();
        Debug.Log("[Interaction] Anulowano wybór heksa docelowego.");
    }

    private void FinishHexTargetSelection()
    {
        if (hoveredTargetHex != null)
        {
            hoveredTargetHex.ToggleHighlight(false);
            hoveredTargetHex = null;
        }

        isSelectingHexTarget = false;
        var cb = onHexTargetSelectionCallback;
        onHexTargetSelectionCallback = null;
        cb?.Invoke();
        Debug.Log("[Interaction] Zatwierdzono wybór heksa docelowego.");
    }

    private void UpdateHexTargetSelectionHover()
    {
        if (hexTargetingTower == null)
        {
            CancelHexTargetSelection();
            return;
        }

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 1000f, hexLayer))
        {
            if (hoveredTargetHex != null)
            {
                hoveredTargetHex.ToggleHighlight(false);
                hoveredTargetHex = null;
            }
            return;
        }

        var cell = hit.collider.GetComponentInParent<HexCell>();
        if (cell == null)
        {
            if (hoveredTargetHex != null)
            {
                hoveredTargetHex.ToggleHighlight(false);
                hoveredTargetHex = null;
            }
            return;
        }

        if (cell != hoveredTargetHex)
        {
            if (hoveredTargetHex != null)
                hoveredTargetHex.ToggleHighlight(false);

            hoveredTargetHex = cell;
            Vector3 diff = cell.transform.position - hexTargetingTower.transform.position;
            diff.y = 0;
            float dist = diff.magnitude;
            bool inRange = dist <= hexTargetingTower.CurrentWorldRange;
            hoveredTargetHex.ToggleHighlight(true, inRange ? ghostValidMat : ghostInvalidMat);
        }
    }

    private void HandleHexTargetSelectionClick()
    {
        if (hexTargetingTower == null)
        {
            CancelHexTargetSelection();
            return;
        }

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 1000f, hexLayer))
        {
            return;
        }

        var cell = hit.collider.GetComponentInParent<HexCell>();
        if (cell == null) return;

        Vector3 diff = cell.transform.position - hexTargetingTower.transform.position;
        diff.y = 0;
        float dist = diff.magnitude;
        if (dist <= hexTargetingTower.CurrentWorldRange)
        {
            Vector3 targetPos = cell.transform.position;
            hexTargetingTower.SetGroundTarget(targetPos, cell);
            FinishHexTargetSelection();
        }
        else
        {
            Debug.Log("[Interaction] Heks poza zasięgiem wieży!");
        }
    }

    private void HandleClick()
    {
        if (isSelectingHexTarget)
        {
            HandleHexTargetSelectionClick();
            return;
        }

        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 1000f))
        {
            Debug.Log("[Interaction] Attempt to interact");
            EnemyInfoUI.Instance?.CloseWindow();
            DeselectAll();
            return;
        }

        if (hit.collider.TryGetComponent<EnemyStats>(out var stats))
        {
            HandleEnemyClick(stats);
            return;
        }
        
        var cell = hit.collider.GetComponentInParent<HexCell>();
        if (cell != null)
        {
            EnemyInfoUI.Instance?.CloseWindow();
            if (SelectedBuilding != null) HandleBuildClick(cell);
            else HandleInspectClick(cell);
        } 
    }

    private void HandleBuildClick(HexCell cell)
    {
        DeselectAll();

        var success = placer.TryBuild(cell, SelectedBuilding);

        if (success && TelemetryManager.Instance != null) TelemetryManager.Instance.addBuilding(SelectedBuilding.buildingName);
        
        if (success)
        {
            if (!Input.GetKey(KeyCode.LeftShift))
            {
                CancelBuilding();
                HandleInspectClick(cell);
            }
            else
            {
                UpdateGhostPosition();
            }
        }
    }

    private void HandleInspectClick(HexCell cell)
    {
        BuildingEntity building;
        try
        {
            Debug.Log("[Interaction] Inspecting"+cell.chunkCoord);
            building = cell.OccupyingBuilding ?? cell.GetComponentInChildren<BuildingEntity>();
        }
        catch (Exception)
        {
            building = null;
        }

        if (building == null)
        {
            DeselectAll();
            Debug.Log("[Interaction] Interaction with terrain"+ cell.chunkCoord);
            return;
        }

        DeselectAll();
            
        Debug.Log("[Interaction]" + building.GetType());
        
        //Interaction for towers
        if (building is TowerEntity towerEntity)
        {
            CenterInfoPanelControllerUI.Instance?.Setup(building);
            lastSelectedTower = towerEntity.controller;
            lastSelectedTower?.ShowRangeIndicator(true);
        }
        //Interaction for houses
        else if (building is HousingEntity housingEntity)
        {
            CenterInfoPanelControllerUI.Instance?.Setup(building);
            lastSelectedTower?.ShowRangeIndicator(false);
            lastSelectedTower = null;
        }
        else if (building is RuneForgeEntity runeForgeEntity)
        {
            CenterInfoPanelControllerUI.Instance?.Setup(building);
            lastSelectedTower?.ShowRangeIndicator(false);
            lastSelectedTower = null;
        }
        else if (building is BeaconEntity)
        {
            CenterInfoPanelControllerUI.Instance?.Setup(building);
            lastSelectedTower?.ShowRangeIndicator(false);
            lastSelectedTower = null;
        }
        else if (building is ExpeditionCenterEntity)
        {
            CenterInfoPanelControllerUI.Instance?.Setup(building);
            lastSelectedTower?.ShowRangeIndicator(false);
            lastSelectedTower = null;
        }
        else if (building.data != null && building.data.type == BuildingType.Economic)
        {
            CenterInfoPanelControllerUI.Instance?.Setup(building);
            lastSelectedTower?.ShowRangeIndicator(false);
            lastSelectedTower = null;
        }

        // Podświetl sąsiednie zasoby
        //highlighter.HighlightFor(building.data, cell.chunkCoord, cell.localCoord, building);
        Debug.Log("[Interaction] Interaction with " + building);
    }

    // =========================================================================
    // Selekcja
    // =========================================================================

    public void DeselectAll()
    {
        Debug.Log("[Interaction] DeselectAll");
        if (isSelectingHexTarget)
        {
            CancelHexTargetSelection();
        }

        if (lastSelectedTower != null)
        {
            lastSelectedTower.ShowRangeIndicator(false);
            lastSelectedTower = null;
        }

        CenterInfoPanelControllerUI.Instance?.HideAll();
        highlighter.Clear();
    }

    private void HandleEnemyClick(EnemyStats stats)
    {
        Debug.Log("[Interaction] Interaction with " + stats.name);
        DeselectAll();
        EnemyInfoUI.Instance?.OpenWindow(stats);
    }
}