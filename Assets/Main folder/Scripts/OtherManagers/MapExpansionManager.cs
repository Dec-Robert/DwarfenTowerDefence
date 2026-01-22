using UnityEngine;
using System.Collections.Generic;

public class MapExpansionManager : MonoBehaviour
{
    [Header("Referencje")]
    public FogOfWarManager fogManager;
    // ZMIANA: Odwo³ujemy siê do nowego Managera UI
    public UIExpansionMenu uiExpansionMenu;
    public HexMapGenerator mapGenerator;

    [Header("Ekonomia")]
    public int baseRevealCost = 10;
    public int costIncrement = 5;

    // (Pola HashSet bez zmian: activeChunks, pendingChunks...)
    private HashSet<Vector2Int> activeChunks = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> pendingChunks = new HashSet<Vector2Int>();
    private int totalChunksBought = 0;
    private Dictionary<Vector2Int, Vector2Int> roadDependencies;

    // ... (Start, OnDestroy, InitializeStartingChunks - BEZ ZMIAN) ...
    // ... (Skopiuj ze starego pliku) ...

    private void Start() { if (GameManager.Instance != null) GameManager.Instance.OnStateChanged += HandleStateChanged; }
    private void OnDestroy() { if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleStateChanged; }
    public void InitializeStartingChunks(List<Vector2Int> startingChunks) { activeChunks.Clear(); pendingChunks.Clear(); totalChunksBought = 0; foreach (var chunk in startingChunks) activeChunks.Add(chunk); if (mapGenerator != null) roadDependencies = mapGenerator.GetRoadRevealDependencies(); if (fogManager != null) fogManager.UpdateAllFogVisuals(); }

    // --- LOGIKA DIAGNOSTYCZNA (Nowe metody dla UI) ---

    public bool IsActive(Vector2Int chunk) => activeChunks.Contains(chunk);
    public bool IsPending(Vector2Int chunk) => pendingChunks.Contains(chunk);

    public bool IsRoadBlocked(Vector2Int chunk)
    {
        // True jeœli to droga I jej rodzic nie jest aktywny
        if (roadDependencies == null || !roadDependencies.ContainsKey(chunk)) return false;
        Vector2Int parent = roadDependencies[chunk];
        return !activeChunks.Contains(parent);
    }

    public bool IsTooFar(Vector2Int chunk)
    {
        return !IsNeighborToActiveChunk(chunk);
    }

    public bool IsChunkBuyable(Vector2Int chunkCoord)
    {
        if (IsActive(chunkCoord) || IsPending(chunkCoord)) return false;
        if (IsTooFar(chunkCoord)) return false;
        if (IsRoadBlocked(chunkCoord)) return false;
        return true;
    }

    public int GetCurrentCost()
    {
        return baseRevealCost + (totalChunksBought * costIncrement);
    }

    // --- OBS£UGA KLIKNIÊCIA ---

    public void OnFogClicked(Vector2Int chunkCoord)
    {
        // Zamiast sprawdzaæ wszystko tutaj i robiæ return,
        // przekazujemy koordynaty do UI. To UI zdecyduje co wyœwietliæ
        // na podstawie metod diagnostycznych powy¿ej.

        if (uiExpansionMenu != null)
        {
            Vector3 worldPos = HexGridMath.GetChunkCenterWorld(chunkCoord, mapGenerator.chunkRadius, mapGenerator.hexSize, mapGenerator.padding);

            // Centrujemy kamerê
            CenterCameraOnChunk(chunkCoord);

            // Otwieramy nowe menu
            uiExpansionMenu.ShowMenu(chunkCoord, worldPos);
        }
    }

    // Metoda wywo³ywana przez PRZYCISK w nowym UI
    public void PurchaseChunk(Vector2Int coord)
    {
        // Ostateczne sprawdzenie (security check)
        if (!IsChunkBuyable(coord)) return;

        int cost = GetCurrentCost();
        var costDict = new Dictionary<ResourceType, int> { { ResourceType.Gold, cost } };

        if (ResourceManager.Instance.SpendResources(costDict))
        {
            fogManager.RevealChunk(coord);
            pendingChunks.Add(coord);
            totalChunksBought++;
            fogManager.UpdateAllFogVisuals();

            // Zamknij menu po zakupie
            if (uiExpansionMenu != null) uiExpansionMenu.Hide();
        }
        else
        {
            Debug.Log("Nie staæ Ciê!");
            // Tu mo¿na dodaæ feedback w UI (np. trzêsienie tekstem ceny)
        }
    }

    // ... (Reszta metod: HandleStateChanged, ActivatePendingChunks, Helpers - BEZ ZMIAN) ...
    // Skopiuj ze starego pliku lub u¿yj tych skrótów:
    private void HandleStateChanged(GameManager.gameStates newState) { if (newState == GameManager.gameStates.PreparePhase) ActivatePendingChunks(); }
    private void ActivatePendingChunks() { if (pendingChunks.Count > 0) { foreach (var chunk in pendingChunks) activeChunks.Add(chunk); pendingChunks.Clear(); fogManager.UpdateAllFogVisuals(); } }
    private bool IsNeighborToActiveChunk(Vector2Int target) { foreach (var neighbor in HexGridMath.GetNeighbors(target)) if (activeChunks.Contains(neighbor)) return true; return false; }
    private void CenterCameraOnChunk(Vector2Int coord) { Vector3 targetPos = HexGridMath.GetChunkCenterWorld(coord, mapGenerator.chunkRadius, mapGenerator.hexSize, mapGenerator.padding); CameraController cam = Camera.main.GetComponent<CameraController>(); if (cam != null) cam.FocusOnPoint(targetPos); }
}