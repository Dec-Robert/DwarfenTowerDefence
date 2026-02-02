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

    [Header("Wizualizacja Bonusów")]
    public Material resourceHighlightMat; // Przypisz jasny zielony materia³ (Transparent/Unlit)

    private List<HexCell> highlightedHexes = new List<HexCell>();

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

        // NOWOŒÆ: Opuszczenie heksów (zasobów)
        ClearHighlights();

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
                // 1. Przesuñ widmo do œrodka heksa
                currentGhost.transform.position = cell.transform.position;

                // 2. SprawdŸ czy miejsce jest poprawne i zmieñ kolor widma
                bool isValid = CheckIfPlacementIsValid(cell);
                ApplyGhostMaterial(isValid);

                // 3. NOWOŒÆ: Podœwietl i unieœ okoliczne zasoby
                // Dziêki temu gracz widzi, które lasy zostan¹ "u¿yte" przez tartak zanim go postawi
                HighlightResourcesFor(selectedBuilding, cell.chunkCoord, cell.localCoord);

                currentGhost.SetActive(true);
            }
        }
        else
        {
            currentGhost.SetActive(false);
            ClearHighlights(); // Jeœli myszka zjedzie z mapy, opuœæ heksy
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

        if (data.isPath)
        {
            return false;
        }

        if (!selectedBuilding.allowedTerrain.Contains(data.feature)) return false;

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
                // SCENARIUSZ A: Tryb Budowania
                if (selectedBuilding != null)
                {
                    // Stary zasiêg/podœwietlenia znikaj¹
                    DeselectAll();

                    // Próba budowy (jeœli siê uda, PerformBuild zresetuje selectedBuilding, chyba ¿e Shift)
                    TryBuildOnHex(clickedCell);
                }
                // SCENARIUSZ B: Tryb Inspekcji
                else
                {
                    BuildingEntity building = clickedCell.GetComponentInChildren<BuildingEntity>();

                    if (building != null)
                    {
                        // 1. Czyœcimy poprzednie zaznaczenia
                        DeselectAll();

                        // 2. Otwieramy UI Inspektora
                        if (UIBuildingInspector.Instance != null)
                        {
                            UIBuildingInspector.Instance.ShowInspector(building);
                        }

                        // 3. Jeœli to wie¿a -> Poka¿ zasiêg
                        if (building is TowerEntity towerEntity)
                        {
                            lastSelectedTower = towerEntity.controller;
                            if (lastSelectedTower != null)
                            {
                                lastSelectedTower.ShowRangeIndicator(true);
                            }
                        }

                        // 4. NOWOŒÆ: Jeœli budynek korzysta z zasobów -> Podnieœ je
                        // (Np. klikasz Tartak -> okoliczne lasy siê unosz¹)
                        HighlightResourcesFor(building.data, clickedCell.chunkCoord, clickedCell.localCoord);
                    }
                    else
                    {
                        // Klikniêto pusty teren -> Odznacz wszystko i zamknij UI
                        DeselectAll();
                        if (UIBuildingInspector.Instance != null) UIBuildingInspector.Instance.Hide();

                        // Debug.Log("Klikniêto pusty heks.");
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

        NotifyNeighborsAboutChange(cell);

    }

    void NotifyNeighborsAboutChange(HexCell centerCell)
    {
        // Pobieramy s¹siadów
        List<Vector2Int> neighbors = HexGridMath.GetNeighbors(centerCell.localCoord);

        if (HexMapVisualizer.Instance != null)
        {
            foreach (var nCoord in neighbors)
            {
                // Znajdujemy heksa-s¹siada
                HexCell neighborHex = HexMapVisualizer.Instance.GetHexCell(centerCell.chunkCoord, nCoord);

                if (neighborHex != null)
                {
                    // Jeœli na s¹siedzie stoi budynek -> ka¿ mu przeliczyæ zasoby
                    BuildingEntity neighborBuilding = neighborHex.GetComponentInChildren<BuildingEntity>();
                    if (neighborBuilding != null)
                    {
                        neighborBuilding.ForceRescan();
                    }
                }
            }
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
        // Ukryj zasiêg poprzedniej wie¿y
        if (lastSelectedTower != null)
        {
            lastSelectedTower.ShowRangeIndicator(false);
            lastSelectedTower = null;
        }

        // NOWOŒÆ: Opuszczenie heksów (jeœli by³y podœwietlone w trybie inspekcji)
        ClearHighlights();
    }



    // Dodaj tê metodê
    public void HighlightResourcesFor(BuildingData data, Vector2Int centerChunk, Vector2Int centerLocal)
    {
        ClearHighlights();

        if (data.bonusRule.requiredFeature == HexFeatureType.None) return;

        var neighbors = HexGridMath.GetNeighbors(centerLocal);
        neighbors.Add(centerLocal);

        foreach (var nCoord in neighbors)
        {
            // SprawdŸ w danych czy to surowiec (Logic check)
            if (mapGenerator.worldData.ContainsKey(centerChunk) &&
                mapGenerator.worldData[centerChunk].ContainsKey(nCoord))
            {
                var cellData = mapGenerator.worldData[centerChunk][nCoord];

                if (cellData.feature == data.bonusRule.requiredFeature)
                {
                    // --- NAPRAWA: Pobieramy HexCell bezpoœrednio z Wizualizera ---
                    // Nie u¿ywamy ju¿ nazw stringowych ani transform.Find
                    HexCell cell = mapGenerator.visualizer.GetHexCell(centerChunk, nCoord);

                    if (cell != null)
                    {
                        cell.ToggleHighlight(true, resourceHighlightMat);
                        highlightedHexes.Add(cell);
                    }
                    // else { Debug.LogWarning($"Nie znaleziono wizualnego heksa dla {nCoord}"); }
                }
            }
        }
    }

    public void ClearHighlights()
    {
        foreach (var cell in highlightedHexes)
        {
            if (cell != null) cell.ToggleHighlight(false);
        }
        highlightedHexes.Clear();
    }
}