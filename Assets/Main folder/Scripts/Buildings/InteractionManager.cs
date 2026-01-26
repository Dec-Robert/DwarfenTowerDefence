using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance { get; private set; }

    [Header("Referencje")]
    public HexMapGenerator mapGenerator;
    public UIDocument uiDocument;
    public LayerMask hexLayer;

    [Header("Materia³y Widma")]
    public Material ghostValidMat;   // Zielony przezroczysty
    public Material ghostInvalidMat; // Czerwony przezroczysty

    [Header("Zasiêg (Range Visualizer)")]
    public GameObject rangeVisualizerPrefab; // Prosty p³aski okr¹g/cylinder

    [Header("Stan Budowania")]
    public BuildingData selectedBuilding;

    private GameObject currentGhost;
    private GameObject currentRangePreview;
    private HexCell lastHoveredCell;


    private TowerController lastSelectedTower;

    private void Awake()
    {
        Instance = this;
    }

    public void SelectBuildingToBuild(BuildingData data)
    {
        // Jeœli ju¿ coœ wybieraliœmy, usuwamy stare widmo
        ClearGhost();

        selectedBuilding = data;

        // Tworzymy nowe widmo
        CreateGhost(data);
        Debug.Log($"[Interaction] Tryb budowania: {data.buildingName}");
    }

    public void CancelBuilding()
    {
        selectedBuilding = null;
        ClearGhost();
        Debug.Log("[Interaction] Anulowano budowanie.");
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            CancelBuilding();
            return;
        }

        // Aktualizacja pozycji widma za myszk¹
        if (selectedBuilding != null && currentGhost != null)
        {
            UpdateGhostPosition();
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUI()) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            HandleClick();
        }
    }

    // --- LOGIKA WIDMA (GHOST) ---

    void CreateGhost(BuildingData data)
    {
        if (data.prefab == null) return;

        // 1. Tworzymy kopiê prefabu
        currentGhost = Instantiate(data.prefab);
        currentGhost.name = "Placement_Ghost";

        // 2. Wy³¹czamy wszystkie skrypty logiczne, ¿eby wie¿a nie strzela³a we mgle
        MonoBehaviour[] scripts = currentGhost.GetComponentsInChildren<MonoBehaviour>();
        foreach (var s in scripts) s.enabled = false;

        // 3. Wy³¹czamy collidery, ¿eby widmo nie blokowa³o myszki
        Collider[] colliders = currentGhost.GetComponentsInChildren<Collider>();
        foreach (var c in colliders) c.enabled = false;

        // 4. Jeœli to wie¿a, stwórz podgl¹d zasiêgu
        if (data is TowerData towerData)
        {
            if (rangeVisualizerPrefab != null)
            {
                currentRangePreview = Instantiate(rangeVisualizerPrefab, currentGhost.transform);
                currentRangePreview.transform.localPosition = new Vector3(0, 0.1f, 0); // Lekko nad ziemi¹
                // Skalujemy okr¹g do zasiêgu (zasiêg to promieñ, wiêc skala to œrednica)
                float scale = towerData.baseRange * 2f;
                currentRangePreview.transform.localScale = new Vector3(scale, 1, scale);
            }
        }
    }

    void UpdateGhostPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 1000f, hexLayer))
        {
            HexCell cell = hit.collider.GetComponentInParent<HexCell>();
            if (cell != null)
            {
                // Przesuñ widmo do œrodka heksa
                currentGhost.transform.position = cell.transform.position;

                // SprawdŸ czy miejsce jest poprawne i zmieñ materia³
                bool isValid = CheckIfPlacementIsValid(cell);
                ApplyGhostMaterial(isValid);

                currentGhost.SetActive(true);
            }
        }
        else
        {
            currentGhost.SetActive(false); // Ukryj jeœli myszka poza map¹
        }
    }

    void ApplyGhostMaterial(bool isValid)
    {
        Material targetMat = isValid ? ghostValidMat : ghostInvalidMat;
        Renderer[] renderers = currentGhost.GetComponentsInChildren<Renderer>();

        foreach (var r in renderers)
        {
            // Nie zmieniamy materia³u Range Preview, jeœli go mamy
            if (currentRangePreview != null && r.transform.IsChildOf(currentRangePreview.transform)) continue;

            r.sharedMaterial = targetMat;
        }
    }

    bool CheckIfPlacementIsValid(HexCell cell)
    {
        if (!mapGenerator.worldData.ContainsKey(cell.chunkCoord)) return false;
        HexCellData data = mapGenerator.worldData[cell.chunkCoord][cell.localCoord];

        // 1. Czy teren pasuje
        if (!selectedBuilding.allowedTerrain.Contains(data.feature)) return false;

        // 2. Czy zajête przez budynek
        if (cell.GetComponentInChildren<BuildingEntity>() != null) return false;

        return true;
    }

    void ClearGhost()
    {
        if (currentGhost != null) Destroy(currentGhost);
        if (currentRangePreview != null) Destroy(currentRangePreview);
    }

    // --- RESZTA LOGIKI (KLIKNIÊCIE I BUDOWA) ---

    void HandleClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 1000f, hexLayer))
        {
            HexCell clickedCell = hit.collider.GetComponentInParent<HexCell>();

            if (clickedCell != null)
            {
                if (selectedBuilding != null)
                {
                    // Tryb budowania: schowaj stary zasiêg, jeœli by³, i buduj
                    DeselectAll();
                    TryBuildOnHex(clickedCell);
                }
                else
                {
                    // Tryb inspekcji
                    BuildingEntity building = clickedCell.GetComponentInChildren<BuildingEntity>();

                    if (building != null)
                    {
                        // 1. Schowaj zasiêg poprzedniej wie¿y przed pokazaniem nowej
                        DeselectAll();

                        // 2. Poka¿ UI inspektora
                        if (UIBuildingInspector.Instance != null)
                        {
                            UIBuildingInspector.Instance.ShowInspector(building);
                        }

                        // 3. SprawdŸ czy to wie¿a i w³¹cz zasiêg
                        if (building is TowerEntity towerEntity)
                        {
                            lastSelectedTower = towerEntity.controller;
                            if (lastSelectedTower != null)
                            {
                                lastSelectedTower.ShowRangeIndicator(true);
                            }
                        }
                    }
                    else
                    {
                        // Klikniêto pusty heks - odznacz wszystko
                        DeselectAll();
                        if (UIBuildingInspector.Instance != null) UIBuildingInspector.Instance.Hide();
                    }
                }
            }
        }
    }

    void TryBuildOnHex(HexCell cell)
    {
        if (!CheckIfPlacementIsValid(cell)) return;

        var costs = selectedBuilding.GetCostDictionary();
        if (ResourceManager.Instance.SpendResources(costs))
        {
            PerformBuild(cell, mapGenerator.worldData[cell.chunkCoord][cell.localCoord]);
        }
        else
        {
            Debug.Log("<color=orange>Za ma³o surowców!</color>");
        }
    }

    void PerformBuild(HexCell cell, HexCellData data)
    {
        // Przed budow¹ usuwamy "widmo", ¿eby nie przeszkadza³o
        // (Chyba ¿e trzymamy Shift, wtedy zaraz stworzymy nowe)
        bool keepBuilding = Input.GetKey(KeyCode.LeftShift);

        // Usuñ dekoracje z heksa
        foreach (Transform child in cell.transform) Destroy(child.gameObject);

        // Postaw prawdziwy budynek
        GameObject newObj = Instantiate(selectedBuilding.prefab, cell.transform.position, Quaternion.identity);
        newObj.transform.parent = cell.transform;

        var entity = newObj.GetComponent<BuildingEntity>();
        if (entity == null) entity = newObj.AddComponent<BuildingEntity>();
        entity.Initialize(selectedBuilding);

        if (selectedBuilding is TowerData towerData)
        {
            var controller = newObj.GetComponent<TowerController>();
            if (controller != null) controller.towerData = towerData;
        }

        if (!keepBuilding)
        {
            CancelBuilding();
        }
        else
        {
            // Jeœli budujemy dalej, odœwie¿amy widmo (¿eby sprawdzi³o nowe warunki zajêtoœci)
            UpdateGhostPosition();
        }
    }

    private bool IsPointerOverUI()
    {
        if (uiDocument == null) return false;
        Vector2 mousePos = Input.mousePosition;
        Vector2 panelPos = new Vector2(mousePos.x, Screen.height - mousePos.y);
        VisualElement picked = uiDocument.rootVisualElement.panel.Pick(panelPos);
        return picked != null && picked != uiDocument.rootVisualElement;
    }

    public void DeselectAll()
    {
        if (lastSelectedTower != null)
        {
            lastSelectedTower.ShowRangeIndicator(false);
            lastSelectedTower = null;
        }
    }
}