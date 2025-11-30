using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TowerBuilder : MonoBehaviour
{
    public static TowerBuilder Instance { get; private set; }
    [SerializeField] private TowerData selectedTower;
    public LayerMask hexLayer; // Warstwa, na której s¹ heksy (opcjonalne, ale zalecane)'

    private void Awake()
    {
        Instance = this;
    }

    // Tê metodê bêd¹ wywo³ywaæ przyciski w UI
    public void SelectTower(TowerData tower)
    {
        selectedTower = tower;
        Debug.Log($"Wybrano wie¿ê: {tower.towerName} (Koszt: {tower.cost})");
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            AttemptBuild();
        }

        if (Input.GetMouseButtonDown(1))
        {
            selectedTower = null;
            Debug.Log("Anulowano budowanie");
        }
    }

    void AttemptBuild()
    {
        if (selectedTower == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f, hexLayer))
        {
            GameObject clickedObject = hit.collider.gameObject;

            if (clickedObject.tag == "Road") 
            {
                return;
            }

            if (clickedObject.transform.childCount > 0)
            {
                Debug.Log("Pole zajête!");
                return;
            }

            if (GameManager.Instance.gold >= selectedTower.cost)
            {
                BuildTower(clickedObject.transform);
            }
            else
            {
                Debug.Log("Za ma³o z³ota!");
            }
        }
    }

    void BuildTower(Transform hexTransform)
    {
        // 1. Zabieramy z³oto
        GameManager.Instance.ModifyGold(-selectedTower.cost);

        // 2. Pobieramy prefab z DANYCH
        GameObject prefabToBuild = selectedTower.prefab;

        // 3. Stawiamy
        GameObject newTower = Instantiate(prefabToBuild, hexTransform.position, Quaternion.identity);
        newTower.transform.parent = hexTransform;
        newTower.transform.localPosition += Vector3.up * 0.5f;

        // 4. Inicjalizujemy dane wie¿y (¿eby wiedzia³a jak strzelaæ)
        TowerController controller = newTower.GetComponent<TowerController>();
        if (controller != null)
        {
            controller.towerData = selectedTower;
        }

        Debug.Log($"Postawiono {selectedTower.towerName}!");
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
        {
            selectedTower = null;
            Debug.Log("Shift nie wciœniêy. Budowa tego samego typu wie¿ przerwana");
        }

    }
}