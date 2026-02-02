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

    // Highlight - Animacja Wysokoœci
    [Header("Elevation Animation")]
    public float liftOffset = 0.2f; // O ile podnieœæ heks
    public float moveSpeed = 10f;   // Prêdkoœæ animacji

    private float baseHeight; // Pocz¹tkowa wysokoœæ (np. dla Wzgórz to bêdzie > 0)
    private float targetHeight; // Gdzie chcemy byæ teraz

    private void Start()
    {
        myRenderer = GetComponentInChildren<Renderer>();

        // Zapamiêtujemy wysokoœæ nadan¹ przez generator mapy (Hill/Sinkhole)
        // U¿ywamy localPosition, bo heks jest dzieckiem Chunku
        baseHeight = transform.localPosition.y;
        targetHeight = baseHeight;
    }

    private void Update()
    {
        // P³ynna animacja do pozycji docelowej
        // Sprawdzamy dystans, ¿eby nie liczyæ lerpa w nieskoñczonoœæ
        if (Mathf.Abs(transform.localPosition.y - targetHeight) > 0.005f)
        {
            float newY = Mathf.Lerp(transform.localPosition.y, targetHeight, Time.deltaTime * moveSpeed);
            transform.localPosition = new Vector3(transform.localPosition.x, newY, transform.localPosition.z);
        }
        else if (transform.localPosition.y != targetHeight)
        {
            // Doci¹gniêcie do idealnej pozycji
            transform.localPosition = new Vector3(transform.localPosition.x, targetHeight, transform.localPosition.z);
        }
    }

    public void ToggleHighlight(bool state, Material highlightMat = null)
    {
        if (myRenderer == null) return;

        if (state)
        {
            // W³¹cz podœwietlenie
            if (!isHighlighted)
            {
                originalMaterial = myRenderer.sharedMaterial;
                isHighlighted = true;
            }
            myRenderer.sharedMaterial = highlightMat;

            // Ustaw cel: w górê
            targetHeight = baseHeight + liftOffset;
        }
        else
        {
            // Wy³¹cz podœwietlenie
            if (isHighlighted)
            {
                myRenderer.sharedMaterial = originalMaterial;
                isHighlighted = false;
            }

            // Ustaw cel: powrót do bazy
            targetHeight = baseHeight;
        }
    }

    public bool HasBuilding()
    {
        // Sprawdzamy czy w dzieciach jest BuildingEntity (lub TowerEntity)
        return GetComponentInChildren<BuildingEntity>() != null;
    }
}