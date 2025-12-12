using UnityEngine;
using System.Collections.Generic;

public class MapExpansionManager : MonoBehaviour
{
    [Header("Referencje")]
    public FogOfWarManager fogManager;
    public ExpansionUI expansionUI;
    public HexMapGenerator mapGenerator;

    [Header("Ekonomia")]
    public int baseRevealCost = 10;
    public int costIncrement = 5;

    // Chunki w pe³ni aktywne (mo¿na od nich ekspandowaæ dalej)
    private HashSet<Vector2Int> activeChunks = new HashSet<Vector2Int>();

    // Chunki kupione w obecnej turze (czekaj¹ na koniec fali, by staæ siê aktywne)
    private HashSet<Vector2Int> pendingChunks = new HashSet<Vector2Int>();

    // Licznik kupionych chunków do skalowania ceny
    private int totalChunksBought = 0;

    private void Start()
    {
        // Subskrypcja zmiany stanów gry
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged += HandleStateChanged;
        }

    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
        }

    }

    // Inicjalizacja startowa (wywo³ywana przez HexMapGenerator)
    public void InitializeStartingChunks(List<Vector2Int> startingChunks)
    {
        activeChunks.Clear();
        pendingChunks.Clear();
        totalChunksBought = 0;

        foreach (var chunk in startingChunks)
        {
            activeChunks.Add(chunk);
        }
        Debug.Log($"[Expansion] Zainicjalizowano {activeChunks.Count} startowych chunków.");
    }

    // G³ówna metoda wywo³ywana po klikniêciu w FogOfWar
    public void OnFogClicked(Vector2Int chunkCoord)
    {
        // 1. SprawdŸ czy chunk nie jest ju¿ dostêpny
        if (activeChunks.Contains(chunkCoord) || pendingChunks.Contains(chunkCoord))
        {
            return;
        }

        // 2. SprawdŸ s¹siedztwo (Tylko s¹siedzi w pe³ni AKTYWNYCH chunków)
        if (!IsNeighborToActiveChunk(chunkCoord))
        {
            Debug.LogWarning("Ten teren jest za daleko! Musisz najpierw zakoñczyæ falê, aby rozszerzyæ zasiêg.");
            return;
        }

        // 3. Oblicz pozycjê i koszt
        int currentCost = CalculateCost();
        Vector3 worldPos = HexGridMath.GetChunkCenterWorld(chunkCoord, mapGenerator.chunkRadius, mapGenerator.hexSize, mapGenerator.padding);

        // 4. Centruj kamerê
        CenterCameraOnChunk(worldPos);


        // 5. Poka¿ UI
        expansionUI.ShowPanel(chunkCoord, currentCost, worldPos, TryBuyChunk);
    }

    // Callback wywo³ywany przez przycisk "Kup" w UI
    private void TryBuyChunk(Vector2Int coord)
    {
        int cost = CalculateCost();

        if (ResourceManager.Instance.GetResourceAmount(ResourceType.Gold) >= cost)
        {
            Dictionary<ResourceType, int> tmp = new Dictionary<ResourceType, int>();
            tmp.Add(ResourceType.Gold, cost);
            // P³atnoœæ
            ResourceManager.Instance.SpendResources(tmp);

            // Logika Mapy - wizualne odkrycie od razu
            fogManager.RevealChunk(coord);

            // Dodajemy do oczekuj¹cych (nie daj¹ zasiêgu w tej turze)
            pendingChunks.Add(coord);

            // Zwiêkszamy licznik (cena nastêpnego roœnie)
            totalChunksBought++;

            Debug.Log($"Kupiono chunk {coord}.");
        }
        else
        {
            Debug.Log("Brak z³ota!");
        }
    }


    // Obs³uga zmiany stanu gry (Koniec fali -> PreparePhase)
    private void HandleStateChanged(GameManager.gameStates newState)
    {
        if (newState == GameManager.gameStates.PreparePhase)
        {
            ActivatePendingChunks();
        }
    }

    // Przenosi chunki z "Oczekuj¹cych" do "Aktywnych" (zwiêksza zasiêg)
    private void ActivatePendingChunks()
    {
        if (pendingChunks.Count > 0)
        {
            Debug.Log($"[Expansion] Fala zakoñczona. Aktywowanie {pendingChunks.Count} nowych chunków.");

            foreach (var chunk in pendingChunks)
            {
                activeChunks.Add(chunk);
            }
            pendingChunks.Clear();
        }
    }

    // Sprawdza czy cel s¹siaduje z jakimkolwiek aktywnym chunkiem
    private bool IsNeighborToActiveChunk(Vector2Int target)
    {
        List<Vector2Int> neighbors = HexGridMath.GetNeighbors(target);
        foreach (var neighbor in neighbors)
        {
            if (activeChunks.Contains(neighbor))
            {
                return true;
            }
        }
        return false;
    }

    private int CalculateCost()
    {
        return baseRevealCost + (totalChunksBought * costIncrement);
    }

    private void CenterCameraOnChunk(Vector3 targetPos)
    {
        // Znajdujemy kamerê i u¿ywamy jej metody FocusOnPoint (zak³adamy, ¿e CameraController jest na MainCamera)
        CameraController cam = Camera.main.GetComponent<CameraController>();
        if (cam != null)
        {
            cam.FocusOnPoint(targetPos);
        }
    }
}