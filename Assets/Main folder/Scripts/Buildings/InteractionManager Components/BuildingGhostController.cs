using UnityEngine;

/// <summary>
///     Zarządza wizualnym "widmem" budynku podczas trybu stawiania.
///     Odpowiada za tworzenie, przesuwanie, zmianę materiału i usuwanie ghost-prefabu,
///     a także podgląd zasięgu wież i podgląd szacowanej produkcji budynków ekonomicznych.
/// </summary>
public class BuildingGhostController
{
    private readonly Material ghostInvalidMat;
    private readonly Material ghostValidMat;
    private readonly GameObject rangeVisualizerPrefab;
    private readonly ResourceVisualsSO resourceVisualsSO;

    private GameObject currentGhost;
    private GameObject currentRangePreview;
    private GameObject currentProductionPreview;
    private BuildingProductionPreviewUI previewUI;

    public BuildingGhostController(
        Material ghostValidMat,
        Material ghostInvalidMat,
        GameObject rangeVisualizerPrefab,
        ResourceVisualsSO resourceVisualsSO = null)
    {
        this.ghostValidMat = ghostValidMat;
        this.ghostInvalidMat = ghostInvalidMat;
        this.rangeVisualizerPrefab = rangeVisualizerPrefab;
        this.resourceVisualsSO = resourceVisualsSO;
    }

    public bool IsActive => currentGhost != null;

    // =========================================================================
    // API Publiczne
    // =========================================================================

    public void CreateGhost(BuildingData data)
    {
        if (data == null || data.prefab == null) return;

        currentGhost = Object.Instantiate(data.prefab);
        currentGhost.name = "Placement_Ghost";

        // Wyłączamy logikę i kolizje żeby widmo było pasywne
        foreach (var s in currentGhost.GetComponentsInChildren<MonoBehaviour>())
            s.enabled = false;
        foreach (var c in currentGhost.GetComponentsInChildren<Collider>())
            c.enabled = false;

        // Podgląd zasięgu dla wież
        if (data is TowerData towerData && rangeVisualizerPrefab != null)
        {
            currentRangePreview = Object.Instantiate(rangeVisualizerPrefab, currentGhost.transform);
            currentRangePreview.transform.localPosition = new Vector3(0, 0.1f, 0);

            var scale = towerData.BaseRangeInWorldUnits * 2f;
            currentRangePreview.transform.localScale = new Vector3(scale, 1, scale);
        }

        // Podgląd produkcji dla budynków produkcyjnych
        if (BuildingProductionCalculator.HasProduction(data))
        {
            currentProductionPreview = BuildingProductionPreviewUI.CreatePreview(currentGhost.transform, resourceVisualsSO);
            if (currentProductionPreview != null)
            {
                previewUI = currentProductionPreview.GetComponent<BuildingProductionPreviewUI>();
            }
        }
    }

    public void MoveTo(Vector3 position, bool isValid, HexCell cell = null, BuildingData data = null)
    {
        if (currentGhost == null) return;
        currentGhost.transform.position = position;
        currentGhost.SetActive(true);
        ApplyMaterial(isValid);

        if (previewUI != null)
        {
            previewUI.UpdatePreview(cell, data, isValid);
        }
    }

    public void Hide()
    {
        if (currentGhost != null) currentGhost.SetActive(false);
        if (currentProductionPreview != null) currentProductionPreview.SetActive(false);
    }

    public void Clear()
    {
        if (currentGhost != null) Object.Destroy(currentGhost);
        if (currentRangePreview != null) Object.Destroy(currentRangePreview);
        if (currentProductionPreview != null) Object.Destroy(currentProductionPreview);
        currentGhost = null;
        currentRangePreview = null;
        currentProductionPreview = null;
        previewUI = null;
    }

    // =========================================================================
    // Prywatne
    // =========================================================================

    private void ApplyMaterial(bool isValid)
    {
        var mat = isValid ? ghostValidMat : ghostInvalidMat;

        foreach (var r in currentGhost.GetComponentsInChildren<Renderer>())
        {
            if (currentRangePreview != null && r.transform.IsChildOf(currentRangePreview.transform))
                continue;

            if (currentProductionPreview != null && r.transform.IsChildOf(currentProductionPreview.transform))
                continue;

            r.sharedMaterial = mat;
        }
    }
}
