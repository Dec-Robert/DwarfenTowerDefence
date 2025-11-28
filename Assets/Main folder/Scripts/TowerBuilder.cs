using UnityEngine;

public class TowerBuilder : MonoBehaviour
{
    public GameObject towerPrefab;
    public LayerMask hexLayer; // Warstwa, na której s¹ heksy (opcjonalne, ale zalecane)

    void Update()
    {
        // Klikniêcie Lewym Przyciskiem Myszy (0)
        if (Input.GetMouseButtonDown(0))
        {
            BuildTower();
        }
    }

    void BuildTower()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Strzelamy promieniem w œwiat
        if (Physics.Raycast(ray, out hit, 100f, hexLayer))
        {
            GameObject clickedObject = hit.collider.gameObject;

            // 1. Sprawdzamy czy to Hex (czy ma tag w nazwie lub odpowiedni komponent)
            if (clickedObject.name.Contains("Hex"))
            {
                // 2. Sprawdzamy czy to nie jest droga (Generator oznaczy³ je tagiem "Road" lub "Road start"/"Road end")
                if (clickedObject.CompareTag("Road") || clickedObject.CompareTag("Road start") || clickedObject.CompareTag("Road end"))
                {
                    Debug.Log("Nie mo¿na budowaæ na drodze!");
                    return;
                }

                // 3. Sprawdzamy czy coœ ju¿ tu stoi (bardzo prosta metoda: sprawdzamy dzieci obiektu)
                // Zak³adamy, ¿e wie¿a staje siê dzieckiem Hexa
                if (clickedObject.transform.childCount > 0)
                {
                    // Tutaj mo¿na dodaæ m¹drzejsze sprawdzanie, czy to faktycznie wie¿a, czy np. dekoracja
                    // Ale na razie zak³adamy, ¿e Hex jest pusty.
                    foreach (Transform child in clickedObject.transform)
                    {
                        if (child.GetComponent<SimpleTower>() != null)
                        {
                            Debug.Log("Tu ju¿ stoi wie¿a!");
                            return;
                        }
                    }
                }

                // 4. Stawiamy wie¿ê
                PlaceTower(clickedObject.transform);
            }
        }
    }

    void PlaceTower(Transform hexTransform)
    {
        // Tworzymy wie¿ê dok³adnie w pozycji hexa
        GameObject newTower = Instantiate(towerPrefab, hexTransform.position, Quaternion.identity);

        // Ustawiamy hexa jako rodzica (dziêki temu heks "trzyma" wie¿ê i ³atwo sprawdziæ zajêtoœæ)
        newTower.transform.parent = hexTransform;

        // Ewentualna korekta wysokoœci (jeœli wie¿a wchodzi w ziemiê)
        newTower.transform.localPosition += Vector3.up * 0.5f;

        Debug.Log("Wie¿a postawiona!");
    }
}