using UnityEngine;

public class HexCell : MonoBehaviour
{
    // Adres
    public Vector2Int chunkCoord;
    public Vector2Int localCoord;

    // Highlight - Kolory
    private Renderer myRenderer;
    private Material originalMaterial;
    private bool isHighlighted = false;

    // Highlight - Animacja Wysoko�ci
    [Header("Elevation Animation")]
    public float liftOffset = 0.2f; // O ile podnie�� heks
    public float moveSpeed = 10f;   // Pr�dko�� animacji

    private float baseHeight; // Pocz�tkowa wysokość (np. dla Wzg�rz to b�dzie > 0)
    private float targetHeight; // Gdzie chcemy by� teraz

    private void Start()
    {
        myRenderer = GetComponentInChildren<Renderer>();

        // Zapami�tujemy wysoko�� nadan� przez generator mapy (Hill/Sinkhole)
        // U�ywamy localPosition, bo heks jest dzieckiem Chunku
        baseHeight = transform.localPosition.y;
        targetHeight = baseHeight;
    }

    private void Update()
    {
        // P�ynna animacja do pozycji docelowej
        // Sprawdzamy dystans, �eby nie liczy� lerpa w niesko�czono��
        if (Mathf.Abs(transform.localPosition.y - targetHeight) > 0.005f)
        {
            float newY = Mathf.Lerp(transform.localPosition.y, targetHeight, Time.deltaTime * moveSpeed);
            transform.localPosition = new Vector3(transform.localPosition.x, newY, transform.localPosition.z);
        }
        else if (transform.localPosition.y != targetHeight)
        {
            // Doci�gni�cie do idealnej pozycji
            transform.localPosition = new Vector3(transform.localPosition.x, targetHeight, transform.localPosition.z);
        }
    }

    public void ToggleHighlight(bool state, Material highlightMat = null)
    {
        if (myRenderer == null) return;

        if (state)
        {
            // W��cz pod�wietlenie
            if (!isHighlighted)
            {
                originalMaterial = myRenderer.sharedMaterial;
                isHighlighted = true;
            }
            myRenderer.sharedMaterial = highlightMat;

            // Ustaw cel: w g�r�
            targetHeight = baseHeight + liftOffset;
        }
        else
        {
            // Wy��cz pod�wietlenie
            if (isHighlighted)
            {
                myRenderer.sharedMaterial = originalMaterial;
                isHighlighted = false;
            }

            // Ustaw cel: powr�t do bazy
            targetHeight = baseHeight;
        }
    }

    public bool HasBuilding()
    {
        // Sprawdzamy czy w dzieciach jest BuildingEntity (lub TowerEntity)
        return GetComponentInChildren<BuildingEntity>() != null;
    }
}