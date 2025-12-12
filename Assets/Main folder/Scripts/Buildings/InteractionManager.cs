using UnityEngine;
using UnityEngine.EventSystems;

public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance { get; private set; }

    [Header("Referencje")]
    public HexMapGenerator mapGenerator;
    public LayerMask hexLayer; // Layer z heksami

    [Header("Stan Budowania")]
    private BuildingData selectedBuilding; // Mo¿e to byæ Tartak (BuildingData) albo Wie¿a (TowerData)

    private void Awake()
    {
        Instance = this;
    }

    // Tê metodê wo³aj¹ przyciski w UI (zarówno wie¿, jak i budynków)
    public void SelectBuildingToBuild(BuildingData data)
    {
        selectedBuilding = data;
        Debug.Log($"Wybrano do budowy: {data.buildingName}");

        // Opcjonalnie: Poka¿ "ducha" budynku pod kursorem
    }

    public void CancelBuilding()
    {
        selectedBuilding = null;
        Debug.Log("Anulowano budowanie.");
    }

    void Update()
    {
        // Prawy przycisk myszy - Anuluj
        if (Input.GetMouseButtonDown(1))
        {
            CancelBuilding();
            return;
        }

        // Lewy przycisk myszy - Akcja
        if (Input.GetMouseButtonDown(0))
        {
            // Blokada UI
            if (EventSystem.current.IsPointerOverGameObject()) return;

            HandleClick();
        }
    }

    void HandleClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f, hexLayer))
        {
            // Szukamy komponentu HexCell na trafionym obiekcie
            HexCell clickedCell = hit.collider.GetComponentInParent<HexCell>();

            if (clickedCell != null)
            {
                if (selectedBuilding != null)
                {
                    // TRYB BUDOWANIA
                    TryBuildOnHex(clickedCell);
                }
                else
                {
                    // TRYB SELEKCJI (Poka¿ info, menu kontekstowe)
                    SelectHex(clickedCell);
                }
            }
        }
    }

    void SelectHex(HexCell cell)
    {
        // SprawdŸ czy na heksie jest budynek (BuildingEntity)
        BuildingEntity building = cell.GetComponentInChildren<BuildingEntity>();

        if (building != null)
        {
            // Jeœli to budynek ekonomiczny -> otwórz menu kontekstowe
            if (building.data.type == BuildingType.Economic || building.data.type == BuildingType.Utility)
            {
                if (ContextMenuUI.Instance != null)
                {
                    ContextMenuUI.Instance.ShowMenu(building);
                }
            }
            else if (building.data.type == BuildingType.Defense)
            {
                // Tutaj logika dla wie¿ (inne menu)
                Debug.Log("Klikniêto wie¿ê - tu bêdzie inne menu.");
            }
        }
        else
        {
            // Pusty heks - mo¿e jakieœ info o terenie?
            var data = mapGenerator.worldData[cell.chunkCoord][cell.localCoord];
            Debug.Log($"Wybrano pusty heks: {data.feature}");
        }
    }

    void TryBuildOnHex(HexCell cell)
    {
        // 1. Pobierz dane logiczne heksa
        if (!mapGenerator.worldData.ContainsKey(cell.chunkCoord)) return;
        HexCellData data = mapGenerator.worldData[cell.chunkCoord][cell.localCoord];

        // 2. Walidacja Terenu (Czy biom pasuje?)
        if (!selectedBuilding.allowedTerrain.Contains(data.feature))
        {
            Debug.Log($"<color=red>Z³y teren!</color> {selectedBuilding.buildingName} wymaga: {string.Join(", ", selectedBuilding.allowedTerrain)}");
            return;
        }

        // 3. Walidacja Zajêtoœci
        // Musimy sprawdziæ, czy coœ ju¿ tu stoi.
        // Jeœli budujemy na lesie (Tartak), to musimy sprawdziæ, czy heks ma cechê Forest.
        // Ale musimy te¿ sprawdziæ, czy nie ma tu ju¿ Innego Budynku.
        // Na razie sprawdzamy dzieci transformu (uproszczenie), docelowo HexCellData powinno mieæ pole 'OccupyingBuilding'.

        bool isOccupiedByBuilding = cell.GetComponentInChildren<BuildingEntity>() != null;
        // ^ Zak³adam, ¿e dodamy skrypt BuildingEntity do prefabów budynków

        if (isOccupiedByBuilding)
        {
            Debug.Log("To pole jest ju¿ zabudowane!");
            return;
        }

        // 4. P³atnoœæ i Budowa
        var costDict = selectedBuilding.GetCostDictionary();
        if (ResourceManager.Instance.SpendResources(costDict))
        {
            Build(cell, data);
        }
        else
        {
            Debug.Log("Za ma³o surowców!");
        }
    }

    void Build(HexCell cell, HexCellData data)
    {
        // A. Jeœli budujemy na zasobie (np. Tartak na Lesie), nie niszczymy lasu logicznie, ale wizualnie usuwamy drzewka
        // B. Jeœli budujemy Wie¿ê na pustym polu, nic nie usuwamy (bo nic tam nie ma)

        // Czyœcimy wizualne "œmieci" (drzewka, kamienie) z heksa przed postawieniem budynku
        foreach (Transform child in cell.transform)
        {
            Destroy(child.gameObject);
        }

        // Instancjowanie
        GameObject newObj = Instantiate(selectedBuilding.prefab, cell.transform.position, Quaternion.identity);
        newObj.transform.parent = cell.transform;

        // Jeœli to wie¿a, konfigurujemy j¹ (rzutowanie sprawdza czy to TowerData)
        if (selectedBuilding is TowerData towerData)
        {
            var controller = newObj.GetComponent<TowerController>();
            if (controller != null) controller.towerData = towerData;
        }

        // Dodanie skryptu identyfikuj¹cego budynek (wa¿ne do logiki klikania póŸniej)
        // Zak³adam istnienie BuildingEntity (napiszemy go zaraz)
        var entity = newObj.GetComponent<BuildingEntity>();
        if (entity == null) entity = newObj.AddComponent<BuildingEntity>();
        entity.Initialize(selectedBuilding);

        Debug.Log($"Zbudowano {selectedBuilding.buildingName}");

        // Reset wyboru (chyba ¿e trzymasz Shift)
        if (!Input.GetKey(KeyCode.LeftShift))
        {
            selectedBuilding = null;
        }
    }
}