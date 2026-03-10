using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

/// <summary>
/// Cienki koordynator interakcji gracza z mapą.
/// Odpowiada wyłącznie za:
///   - obsługę inputu (kliknięcia, PPM, Shift),
///   - przełączanie między trybem budowania i inspekcji,
///   - zarządzanie selekcją wieży (wskaźnik zasięgu).
///
/// Cała logika wizualna i budowlana żyje w:
///   BuildingGhostController, BuildingPlacer, HexHighlighter.
/// </summary>
public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance { get; private set; }

    [Header("Referencje")]
    public HexMapGenerator       mapGenerator;
    public MapExpansionManager   expansionManager;
    public UIDocument            uiDocument;
    public LayerMask             hexLayer;

    [Header("Widmo")]
    public Material    ghostValidMat;
    public Material    ghostInvalidMat;
    public GameObject  rangeVisualizerPrefab;

    [Header("Podświetlenie zasobów")]
    public Material resourceHighlightMat;

    // -------------------------------------------------------------------------
    // Stan
    // -------------------------------------------------------------------------

    public BuildingData SelectedBuilding { get; private set; }

    private TowerController lastSelectedTower;

    // -------------------------------------------------------------------------
    // Komponenty logiczne
    // -------------------------------------------------------------------------

    private BuildingGhostController ghost;
    private BuildingPlacer          placer;
    private HexHighlighter          highlighter;

    // =========================================================================
    // Cykl życia
    // =========================================================================

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ghost       = new BuildingGhostController(ghostValidMat, ghostInvalidMat, rangeVisualizerPrefab);
        placer      = new BuildingPlacer(mapGenerator);
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
            if (IsPointerOverUI()) return;
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
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, hexLayer))
        {
            ghost.Hide();
            highlighter.Clear();
            return;
        }

        HexCell cell = hit.collider.GetComponentInParent<HexCell>();
        if (cell == null) return;

        bool isValid = placer.IsPlacementValid(cell, SelectedBuilding);
        ghost.MoveTo(cell.transform.position, isValid);
        highlighter.HighlightFor(SelectedBuilding, cell.chunkCoord, cell.localCoord);
    }

    // =========================================================================
    // Obsługa kliknięcia
    // =========================================================================

    private void HandleClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, hexLayer)) return;

        HexCell clickedCell = hit.collider.GetComponentInParent<HexCell>();
        if (clickedCell == null) return;

        if (SelectedBuilding != null)
            HandleBuildClick(clickedCell);
        else
            HandleInspectClick(clickedCell);
    }

    private void HandleBuildClick(HexCell cell)
    {
        DeselectAll();

        bool success = placer.TryBuild(cell, SelectedBuilding);

        if (success && !Input.GetKey(KeyCode.LeftShift))
            CancelBuilding();
        else if (success)
            UpdateGhostPosition(); // Shift – kontynuuj budowanie
    }

    private void HandleInspectClick(HexCell cell)
    {
        var building = cell.GetComponentInChildren<BuildingEntity>();

        if (building == null)
        {
            DeselectAll();
            UIBuildingInspector.Instance?.Hide();
            return;
        }

        DeselectAll();

        // Otwórz inspektor
        UIBuildingInspector.Instance?.ShowInspector(building);

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
        if (lastSelectedTower != null)
        {
            lastSelectedTower.ShowRangeIndicator(false);
            lastSelectedTower = null;
        }

        highlighter.Clear();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private bool IsPointerOverUI()
    {
        if (uiDocument == null) return false;
        Vector2 mousePos  = Input.mousePosition;
        Vector2 panelPos  = new Vector2(mousePos.x, Screen.height - mousePos.y);
        VisualElement hit = uiDocument.rootVisualElement.panel.Pick(panelPos);
        return hit != null && hit != uiDocument.rootVisualElement;
    }
}