using UnityEngine;

public class HexCell : MonoBehaviour
{
    // Adres
    public Vector2Int chunkCoord;
    public Vector2Int localCoord;

    // Model Danych i Budynek O(1)
    public HexCellData Data { get; private set; }
    public BuildingEntity OccupyingBuilding { get; private set; }
    private GameObject featurePropInstance;

    public bool HasBuilding()
    {
        return OccupyingBuilding != null || GetComponentInChildren<BuildingEntity>() != null;
    }
    public int Elevation => Data != null ? Data.elevation : 0;
    public bool IsHill => Elevation > 0;
    public bool IsCavity => Elevation < 0;
    public bool IsFlat => Elevation == 0;
    public HexFeatureType Feature => Data != null ? Data.feature : HexFeatureType.None;
    public bool IsRoad => Data != null && (Data.isRoad || Data.isPath);

    // Highlight - Kolory
    private Renderer myRenderer;
    private Material originalMaterial;
    private bool isHighlighted = false;

    // Highlight - Animacja Wysokości
    [Header("Elevation Animation")]
    public float liftOffset = 0.2f; // O ile podnieść heks
    public float moveSpeed = 10f;   // Prędkość animacji

    private float baseHeight; // Początkowa wysokość
    private float targetHeight; // Gdzie chcemy być teraz

    public void Initialize(HexCellData data)
    {
        Data = data;
        chunkCoord = data.chunkCoord;
        localCoord = data.localCoord;
        if (data.occupyingBuilding != null)
        {
            OccupyingBuilding = data.occupyingBuilding;
        }
    }

    public void RegisterFeatureProp(GameObject prop)
    {
        featurePropInstance = prop;
    }

    public void SetBuilding(BuildingEntity building)
    {
        OccupyingBuilding = building;
        if (Data != null)
        {
            Data.occupyingBuilding = building;
        }
        SetDecorationsVisible(false);
    }

    public void ClearBuilding()
    {
        OccupyingBuilding = null;
        if (Data != null)
        {
            Data.occupyingBuilding = null;
        }
        SetDecorationsVisible(true);
    }

    public void SetDecorationsVisible(bool visible)
    {
        if (featurePropInstance != null)
        {
            featurePropInstance.SetActive(visible);
        }
    }

    private void Start()
    {
        myRenderer = GetComponentInChildren<Renderer>();

        // Zapamiętujemy wysokość nadaną przez generator mapy
        baseHeight = transform.localPosition.y;
        targetHeight = baseHeight;
    }

    private void Update()
    {
        // Płynna animacja do pozycji docelowej
        if (Mathf.Abs(transform.localPosition.y - targetHeight) > 0.005f)
        {
            float newY = Mathf.Lerp(transform.localPosition.y, targetHeight, Time.deltaTime * moveSpeed);
            transform.localPosition = new Vector3(transform.localPosition.x, newY, transform.localPosition.z);
        }
        else if (transform.localPosition.y != targetHeight)
        {
            transform.localPosition = new Vector3(transform.localPosition.x, targetHeight, transform.localPosition.z);
        }
    }

    public void ToggleHighlight(bool state, Material highlightMat = null)
    {
        if (myRenderer == null) return;

        if (state)
        {
            if (!isHighlighted)
            {
                originalMaterial = myRenderer.sharedMaterial;
                isHighlighted = true;
            }
            myRenderer.sharedMaterial = highlightMat;
            targetHeight = baseHeight + liftOffset;
        }
        else
        {
            if (isHighlighted)
            {
                myRenderer.sharedMaterial = originalMaterial;
                isHighlighted = false;
            }
            targetHeight = baseHeight;
        }
    }

    // Metoda dla wstecznej kompatybilności
    public bool CheckHasBuilding()
    {
        return HasBuilding();
    }
}
