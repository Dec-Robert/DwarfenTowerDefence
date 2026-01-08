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

    // Zbiory koordynatów chunków
    private HashSet<Vector2Int> activeChunks = new HashSet<Vector2Int>();  // W pe³ni odkryte (mo¿na od nich iœæ dalej)
    private HashSet<Vector2Int> pendingChunks = new HashSet<Vector2Int>(); // Kupione w tej turze (czekaj¹ na koniec fali)
    
    private int totalChunksBought = 0;
    
    // Mapa zale¿noœci: Klucz = Chunk, Wartoœæ = Chunk wymagany (rodzic)
    private Dictionary<Vector2Int, Vector2Int> roadDependencies;

    private void Start()
    {
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

    // Wywo³ywane przez HexMapGenerator po wygenerowaniu mapy
    public void InitializeStartingChunks(List<Vector2Int> startingChunks)
    {
        activeChunks.Clear();
        pendingChunks.Clear();
        totalChunksBought = 0;

        // Dodajemy chunki startowe (Baza + S¹siad) jako aktywne
        foreach (var chunk in startingChunks)
        {
            activeChunks.Add(chunk);
        }

        // Pobieramy zale¿noœci drogowe z generatora
        if (mapGenerator != null)
        {
            roadDependencies = mapGenerator.GetRoadRevealDependencies();
        }
        
        // Odœwie¿amy wygl¹d mg³y na starcie (¿eby pokazaæ, co mo¿na kupiæ)
        if (fogManager != null)
        {
            fogManager.UpdateAllFogVisuals();
        }
    }

    // --- API DLA WIZUALIZACJI (FogOfWarManager pyta: "Czy ten chunk ma byæ zielony?") ---
    public bool IsChunkBuyable(Vector2Int chunkCoord)
    {
        // 1. Czy ju¿ jest nasz?
        if (activeChunks.Contains(chunkCoord) || pendingChunks.Contains(chunkCoord)) 
            return false;

        // 2. Czy s¹siaduje z aktywnym terenem?
        if (!IsNeighborToActiveChunk(chunkCoord)) 
            return false;

        // 3. Czy spe³nia wymogi drogi (nie mo¿na przeskakiwaæ)?
        if (!CanRevealRoadChunk(chunkCoord)) 
            return false;

        return true;
    }

    // --- INTERAKCJA GRACZA ---
    public void OnFogClicked(Vector2Int chunkCoord)
    {
        // U¿ywamy tej samej logiki co przy kolorowaniu
        if (!IsChunkBuyable(chunkCoord))
        {
            // Opcjonalnie: Tu mo¿na dodaæ komunikat na ekranie "Teren niedostêpny"
            Debug.Log("Nie mo¿na odkryæ tego terenu (za daleko, zablokowane lub ju¿ odkryte).");
            return;
        }

        // Obliczenia i UI
        int cost = CalculateCost();
        CenterCameraOnChunk(chunkCoord);
        
        Vector3 worldPos = HexGridMath.GetChunkCenterWorld(chunkCoord, mapGenerator.chunkRadius, mapGenerator.hexSize, mapGenerator.padding);
        expansionUI.ShowPanel(chunkCoord, cost, worldPos, TryBuyChunk);
    }

    private void TryBuyChunk(Vector2Int coord)
    {
        int cost = CalculateCost();
        
        // Tworzymy s³ownik kosztów dla ResourceManagera
        var costDict = new Dictionary<ResourceType, int> { { ResourceType.Gold, cost } };

        if (ResourceManager.Instance.SpendResources(costDict))
        {
            // Sukces - usuwamy mg³ê wizualnie
            fogManager.RevealChunk(coord);
            
            // Dodajemy do "Poczekalni" - stanie siê aktywny dopiero po fali
            pendingChunks.Add(coord);
            totalChunksBought++;
            
            // Odœwie¿amy kolory (ten chunk przestanie œwieciæ na zielono, bo jest ju¿ kupiony)
            fogManager.UpdateAllFogVisuals();
        }
        else
        {
            Debug.Log("Nie staæ Ciê!");
        }
    }

    // --- LOGIKA STANU GRY ---
    private void HandleStateChanged(GameManager.gameStates newState)
    {
        // Gdy koñczy siê fala i wchodzimy w fazê przygotowañ -> Aktywujemy kupione tereny
        if (newState == GameManager.gameStates.PreparePhase)
        {
            ActivatePendingChunks();
        }
    }

    private void ActivatePendingChunks()
    {
        if (pendingChunks.Count > 0)
        {
            foreach (var chunk in pendingChunks)
            {
                activeChunks.Add(chunk);
            }
            pendingChunks.Clear();
            
            // Teren siê powiêkszy³ -> nowe chunki mog¹ staæ siê dostêpne -> Odœwie¿amy kolory
            fogManager.UpdateAllFogVisuals();
            Debug.Log("Nowe tereny aktywowane.");
        }
    }

    // --- HELPERS ---

    private bool IsNeighborToActiveChunk(Vector2Int target)
    {
        foreach (var neighbor in HexGridMath.GetNeighbors(target))
        {
            // Tylko ACTIVE chunks daj¹ s¹siedztwo (PENDING nie daj¹!)
            if (activeChunks.Contains(neighbor)) return true;
        }
        return false;
    }

    private bool CanRevealRoadChunk(Vector2Int chunk)
    {
        // Jeœli nie ma go w liœcie zale¿noœci, to jest zwyk³ym terenem -> OK
        if (roadDependencies == null || !roadDependencies.ContainsKey(chunk)) 
            return true;

        // Jeœli jest drog¹, pobieramy jego "rodzica" (chunk bli¿ej bazy)
        Vector2Int requiredParent = roadDependencies[chunk];

        // Rodzic musi byæ w pe³ni AKTYWNY
        return activeChunks.Contains(requiredParent);
    }

    private int CalculateCost()
    {
        return baseRevealCost + (totalChunksBought * costIncrement);
    }

    private void CenterCameraOnChunk(Vector2Int coord)
    {
        Vector3 targetPos = HexGridMath.GetChunkCenterWorld(coord, mapGenerator.chunkRadius, mapGenerator.hexSize, mapGenerator.padding);
        
        CameraController cam = Camera.main.GetComponent<CameraController>();
        if (cam != null)
        {
            cam.FocusOnPoint(targetPos);
        }
    }
}