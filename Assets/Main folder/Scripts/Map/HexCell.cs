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
    public int Elevation
    {
        get
        {
            if (Data == null) return 0;
            if (Data.elevation != 0) return Data.elevation;
            if (Data.feature == HexFeatureType.Hill) return Mathf.Max(1, Data.featureLevel);
            if (Data.feature == HexFeatureType.Sinkhole) return Data.featureLevel < 0 ? Data.featureLevel : -1;
            if (Data.feature == HexFeatureType.Mountain) return 4;
            return 0;
        }
    }
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

    public GameObject FeatureProp => featurePropInstance;

    public void RegisterFeatureProp(GameObject prop)
    {
        if (featurePropInstance != null && featurePropInstance != prop)
        {
            Destroy(featurePropInstance);
        }
        featurePropInstance = prop;
        if (HasBuilding())
        {
            SetDecorationsVisible(false);
        }
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
            SetTopMaterial(highlightMat);
            targetHeight = baseHeight + liftOffset;
        }
        else
        {
            if (isHighlighted)
            {
                SetTopMaterial(originalMaterial);
                isHighlighted = false;
            }
            targetHeight = baseHeight;
        }
    }

    // Podmienia tylko slot 0 (górna powierzchnia). Slot 1 (boczne ściany, HexSideTerrain) pozostaje bez zmian.
    private void SetTopMaterial(Material mat)
    {
        Material[] mats = myRenderer.sharedMaterials;
        if (mats.Length <= 1)
        {
            myRenderer.sharedMaterial = mat;
            return;
        }
        mats[0] = mat;
        myRenderer.sharedMaterials = mats;
    }

    // Metoda dla wstecznej kompatybilności
    public bool CheckHasBuilding()
    {
        return HasBuilding();
    }
}
