using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance { get; private set; }

    [Header("Referencje")]
    public HexMapGenerator mapGenerator;
    public LayerMask hexLayer; // Upewnij siê, ¿e Heksy s¹ na tej warstwie!

    [Header("Stan Budowania")]
    // Jeœli to pole nie jest null, oznacza to, ¿e gracz ma "m³otek w rêku"
    [SerializeField] private BuildingData selectedBuilding;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;
    }

    // --- API DLA UI (Przyciski w menu budowania) ---
    public void SelectBuildingToBuild(BuildingData data)
    {
        selectedBuilding = data;
        Debug.Log($"Wybrano do budowy: {data.buildingName}");

        // Tu mo¿na dodaæ logikê "Ducha" (Ghost Building) pod¹¿aj¹cego za kursorem
    }

    public void CancelBuilding()
    {
        selectedBuilding = null;
        Debug.Log("Anulowano tryb budowania.");
    }

    // --- G£ÓWNA PÊTLA ---
    void Update()
    {
        // 1. Anulowanie (Prawy Przycisk Myszy)
        if (Input.GetMouseButtonDown(1))
        {
            CancelBuilding();
            // Jeœli by³o otwarte menu kontekstowe, te¿ mo¿na je zamkn¹æ
            if (BuildingContextMenu.Instance != null) BuildingContextMenu.Instance.CloseMenu();
            return;
        }

        // 2. Klikniêcie (Lewy Przycisk Myszy)
        if (Input.GetMouseButtonDown(0))
        {
            // Blokada klikania "przez" UI (np. ¿eby nie zbudowaæ wie¿y klikaj¹c w przycisk pauzy)
            if (EventSystem.current.IsPointerOverGameObject()) return;

            HandleClick();
        }
    }

    void HandleClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Rysujemy liniê debugow¹ w Scene View, ¿eby widzieæ gdzie celujemy
        Debug.DrawRay(ray.origin, ray.direction * 1000, Color.yellow, 0.5f);

        // Strzelamy promieniem w warstwê heksów
        if (Physics.Raycast(ray, out hit, 1000f, hexLayer))
        {
            // Próbujemy pobraæ komponent HexCell z trafionego obiektu lub jego rodzica
            HexCell clickedCell = hit.collider.GetComponentInParent<HexCell>();

            if (clickedCell != null)
            {
                // SCENARIUSZ A: Mamy wybrany budynek do zbudowania (np. Tartak na kursorze)
                if (selectedBuilding != null)
                {
                    TryBuildOnHex(clickedCell);
                }
                // SCENARIUSZ B: Tryb selekcji (Klikamy, ¿eby sprawdziæ co to jest)
                else
                {
                    // Sprawdzamy, czy na tym heksie stoi ju¿ jakiœ budynek
                    // (BuildingEntity powinien byæ dzieckiem HexCell w hierarchii)
                    BuildingEntity building = clickedCell.GetComponentInChildren<BuildingEntity>();

                    if (building != null)
                    {
                        Debug.Log($"Klikniêto budynek: {building.data.buildingName}");

                        // Otwieramy nowe okno Inspektora (UI Toolkit)
                        if (UIBuildingInspector.Instance != null)
                        {
                            UIBuildingInspector.Instance.ShowInspector(building);
                        }
                        else
                        {
                            Debug.LogWarning("Brak UIBuildingInspector na scenie!");
                        }
                    }
                    else
                    {
                        Debug.Log("Klikniêto pusty heks.");
                    }
                }
            }
        }
    }

    void ProcessHexInteraction(HexCell cell)
    {
        // A. SprawdŸ, czy na heksie stoi ju¿ jakiœ budynek
        BuildingEntity existingBuilding = cell.GetComponentInChildren<BuildingEntity>();

        if (existingBuilding != null)
        {
            // Jeœli jest budynek -> Otwórz Menu Kontekstowe
            Debug.Log($"Klikniêto budynek: {existingBuilding.data.buildingName}");

            if (BuildingContextMenu.Instance != null)
            {
                BuildingContextMenu.Instance.OpenMenu(existingBuilding);
            }

            // Jeœli byliœmy w trybie budowania, klikniêcie w inny budynek mo¿e anulowaæ budowanie
            // lub po prostu je zignorowaæ. Tutaj resetujemy, ¿eby otworzyæ menu.
            CancelBuilding();
            return;
        }

        // B. Jeœli pole jest puste (lub ma tylko drzewa/ska³y) i mamy wybrany budynek -> BUDUJEMY
        if (selectedBuilding != null)
        {
            TryBuildOnHex(cell);
            return;
        }

        // C. Jeœli nic nie budujemy i nic tam nie ma -> Poka¿ info o terenie (Debug)
        SelectHexInfo(cell);
    }

    void TryBuildOnHex(HexCell cell)
    {
        // 1. Pobierz logiczne dane heksa (biom, typ terenu)
        if (!mapGenerator.worldData.ContainsKey(cell.chunkCoord)) return;
        HexCellData data = mapGenerator.worldData[cell.chunkCoord][cell.localCoord];

        // 2. Walidacja Terenu (Czy budynek pozwala na ten typ terenu?)
        if (!selectedBuilding.allowedTerrain.Contains(data.feature))
        {
            Debug.Log($"<color=red>Z³y teren!</color> {selectedBuilding.buildingName} wymaga: {string.Join(", ", selectedBuilding.allowedTerrain)} (Tu jest: {data.feature})");
            return;
        }

        // 3. Sprawdzenie kosztów i p³atnoœæ
        // Zamieniamy listê kosztów na S³ownik dla ResourceManagera
        Dictionary<ResourceType, int> costDict = selectedBuilding.GetCostDictionary();

        if (ResourceManager.Instance.SpendResources(costDict))
        {
            // P³atnoœæ przesz³a pomyœlnie -> Budujemy
            PerformBuild(cell, data);
        }
        else
        {
            Debug.Log("Za ma³o surowców!");
            // Tu mo¿na dodaæ dŸwiêk b³êdu
        }
    }

    void PerformBuild(HexCell cell, HexCellData data)
    {
        // 1. Czyœcimy heks z dekoracji (drzewa, kamienie)
        // Jeœli budujemy Tartak na Lesie, usuwamy model lasu, ¿eby Tartak nie przenika³ przez drzewa.
        // Logicznie w 'HexCellData' teren to nadal 'Forest', co jest OK.
        foreach (Transform child in cell.transform)
        {
            Destroy(child.gameObject);
        }

        // 2. Instancjowanie budynku
        GameObject newObj = Instantiate(selectedBuilding.prefab, cell.transform.position, Quaternion.identity);
        newObj.transform.parent = cell.transform;

        // Drobna korekta pozycji Y (opcjonalna, zale¿na od pivotów modeli)
        // newObj.transform.localPosition = Vector3.zero; 

        // 3. Inicjalizacja komponentu logicznego (BuildingEntity)
        var entity = newObj.GetComponent<BuildingEntity>();
        if (entity == null) entity = newObj.AddComponent<BuildingEntity>();
        entity.Initialize(selectedBuilding);

        // 4. Konfiguracja specyficzna dla Wie¿
        // (Wie¿e u¿ywaj¹ TowerData, które dziedziczy po BuildingData)
        if (selectedBuilding is TowerData towerData)
        {
            var controller = newObj.GetComponent<TowerController>();
            if (controller != null)
            {
                controller.towerData = towerData;
            }
        }

        Debug.Log($"<color=green>Zbudowano {selectedBuilding.buildingName}!</color>");

        // 5. Reset trybu budowania (chyba ¿e trzymamy Shift dla seryjnego budowania)
        if (!Input.GetKey(KeyCode.LeftShift))
        {
            selectedBuilding = null;
        }
    }

    void SelectHexInfo(HexCell cell)
    {
        var data = mapGenerator.worldData[cell.chunkCoord][cell.localCoord];
        Debug.Log($"Info o terenie: {data.feature} (Poziom: {data.featureLevel})");

        // Jeœli menu kontekstowe by³o otwarte dla innego budynku, zamykamy je
        if (BuildingContextMenu.Instance != null) BuildingContextMenu.Instance.CloseMenu();
    }
}