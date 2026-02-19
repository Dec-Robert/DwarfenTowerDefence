using UnityEngine;

/// <summary>
/// Zarządza wizualnym "widmem" budynku podczas trybu stawiania.
/// Odpowiada za tworzenie, przesuwanie, zmianę materiału i usuwanie ghost-prefabu.
/// </summary>
public class BuildingGhostController
{
    private readonly Material ghostValidMat;
    private readonly Material ghostInvalidMat;
    private readonly GameObject rangeVisualizerPrefab;

    private GameObject currentGhost;
    private GameObject currentRangePreview;

    public BuildingGhostController(
        Material ghostValidMat,
        Material ghostInvalidMat,
        GameObject rangeVisualizerPrefab)
    {
        this.ghostValidMat         = ghostValidMat;
        this.ghostInvalidMat       = ghostInvalidMat;
        this.rangeVisualizerPrefab = rangeVisualizerPrefab;
    }

    // =========================================================================
    // API Publiczne
    // =========================================================================

    public void CreateGhost(BuildingData data)
    {
        if (data.prefab == null) return;

        currentGhost      = Object.Instantiate(data.prefab);
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

            float scale = towerData.baseRange * 2f;
            currentRangePreview.transform.localScale = new Vector3(scale, 1, scale);
        }
    }

    public void MoveTo(Vector3 position, bool isValid)
    {
        if (currentGhost == null) return;
        currentGhost.transform.position = position;
        currentGhost.SetActive(true);
        ApplyMaterial(isValid);
    }

    public void Hide()
    {
        if (currentGhost != null) currentGhost.SetActive(false);
    }

    public void Clear()
    {
        if (currentGhost       != null) Object.Destroy(currentGhost);
        if (currentRangePreview != null) Object.Destroy(currentRangePreview);
        currentGhost        = null;
        currentRangePreview = null;
    }

    public bool IsActive => currentGhost != null;

    // =========================================================================
    // Prywatne
    // =========================================================================

    private void ApplyMaterial(bool isValid)
    {
        Material mat = isValid ? ghostValidMat : ghostInvalidMat;

        foreach (var r in currentGhost.GetComponentsInChildren<Renderer>())
        {
            if (currentRangePreview != null && r.transform.IsChildOf(currentRangePreview.transform))
                continue;

            r.sharedMaterial = mat;
        }
    }
}