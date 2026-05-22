using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

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
    

    private BuildingGhostController ghost;
    private HexHighlighter highlighter;

    private TowerController lastSelectedTower;
    private BuildingPlacer placer;
    public static InteractionManager Instance { get; private set; }
    

    public BuildingData SelectedBuilding { get; private set; }
    

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ghost = new BuildingGhostController(ghostValidMat, ghostInvalidMat, rangeVisualizerPrefab);
        placer = new BuildingPlacer(mapGenerator);
        highlighter = new HexHighlighter(mapGenerator, resourceHighlightMat);
    }

    private void Update()
    {
        // PPM – anuluj budowanie
        if (Input.GetMouseButtonDown(1))
        {
            CancelBuilding();
            return;
        }

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
        ghost.MoveTo(cell.transform.position, isValid);
        highlighter.HighlightFor(SelectedBuilding, cell.chunkCoord, cell.localCoord);
    }

    // =========================================================================
    // Obsługa kliknięcia
    // =========================================================================

    private void HandleClick()
    {
        Debug.Log("[Interaction] Attempt to interact");
        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 1000f))
        {
            EnemyInfoUI.Instance.CloseWindow();
            DeselectAll();
            return;
        }

        
        if (hit.collider.TryGetComponent<EnemyStats>(out var stats)) HandleEnemyClick(stats);
        else if (hit.collider.TryGetComponent<HexCell>(out var cell)) HandleBuildClick(cell); 
        else HandleInspectClick(cell);
    }

    private void HandleBuildClick(HexCell cell)
    {
        DeselectAll();

        var success = placer.TryBuild(cell, SelectedBuilding);

        if (success) TelemetryManager.Instance.addBuilding(SelectedBuilding.buildingName);
        
        if (success && !Input.GetKey(KeyCode.LeftShift))
            CancelBuilding();
        else if (success)
            UpdateGhostPosition(); 
    }

    private void HandleInspectClick(HexCell cell)
    {
        var building = cell.GetComponentInChildren<BuildingEntity>();

        if (building == null)
        {
            DeselectAll();
            return;
        }

        DeselectAll();
        
        // Pokaż zasięg wieży
        if (building is TowerEntity towerEntity)
        {
            lastSelectedTower = towerEntity.controller;
            lastSelectedTower?.ShowRangeIndicator(true);
        }

        // Podświetl sąsiednie zasoby
        highlighter.HighlightFor(building.data, cell.chunkCoord, cell.localCoord, building);
    }

    // =========================================================================
    // Selekcja
    // =========================================================================

    public void DeselectAll()
    {
        Debug.Log("[Interaction] DeselectAll");
        if (lastSelectedTower != null)
        {
            lastSelectedTower.ShowRangeIndicator(false);
            lastSelectedTower = null;
        }

        highlighter.Clear();
    }



    private void HandleEnemyClick(EnemyStats stats)
    {
        DeselectAll();
        EnemyInfoUI.Instance.OpenWindow(stats);
        
    }
}