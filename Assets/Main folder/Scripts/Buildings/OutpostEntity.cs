using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Posterunek – specjalny budynek który:
///   1. Natychmiast odblokowuje pełne budownictwo na chunku (FullyUnlocked)
///   2. Po 10 dniach oferuje bezpłatną transformację w 1 z 3 budynków mieszkalnych
///   3. Ulepszenia modyfikują opcje transformacji
///
/// Cykl życia:
///   Postawiony → odlicza dni → po 10 dniach: UI pokazuje wybór → Gracz wybiera → Destroy + Spawn
/// </summary>
public class OutpostEntity : MonoBehaviour
{
    [Header("── Konfiguracja ────────────────────────")]
    [Tooltip("Po ilu dniach pojawia się opcja transformacji")]
    public int daysUntilTransform = 10;

    [Header("── Opcje Transformacji (bazowe) ─────────")]
    [Tooltip("Prefaby budynków do wyboru przy transformacji")]
    public List<TransformOption> baseTransformOptions = new List<TransformOption>();

    [Header("── Stan (Podgląd) ───────────────────────")]
    [SerializeField] private int daysRemaining;
    [SerializeField] private bool transformAvailable = false;
    [SerializeField] private Vector2Int chunkCoord;

    // Aktywne opcje (bazowe + modyfikowane przez ulepszenia)
    private List<TransformOption> activeOptions = new List<TransformOption>();

    private MapExpansionManager expansionManager;

    // =========================================================================
    // INICJALIZACJA
    // =========================================================================

    public void Initialize(Vector2Int chunk, MapExpansionManager manager)
    {
        chunkCoord       = chunk;
        expansionManager = manager;
        daysRemaining    = daysUntilTransform;
        activeOptions    = new List<TransformOption>(baseTransformOptions);

        // Natychmiastowe odblokowanie budownictwa
        expansionManager.OnOutpostBuilt(chunkCoord);

        Debug.Log($"[Outpost] Posterunek na {chunkCoord}. " +
                  $"Transformacja za {daysRemaining} dni.");
    }

    // =========================================================================
    // TICK DZIENNY
    // =========================================================================

    /// <summary>Wywołuj raz na dzień przez MapExpansionManager lub GameManager.</summary>
    public void OnDayPassed()
    {
        if (transformAvailable) return;

        daysRemaining--;
        Debug.Log($"[Outpost] {chunkCoord} – {daysRemaining} dni do transformacji.");

        if (daysRemaining <= 0)
        {
            transformAvailable = true;
            expansionManager.OnOutpostReadyToTransform(this, activeOptions);
        }
    }

    // =========================================================================
    // TRANSFORMACJA
    // =========================================================================

    /// <summary>
    /// Gracz wybrał opcję transformacji.
    /// Niszczy posterunek i spawnuje wybrany budynek w tym samym miejscu.
    /// </summary>
    public void Transform(TransformOption chosen)
    {
        if (!transformAvailable)
        {
            Debug.LogWarning("[Outpost] Transformacja niedostępna.");
            return;
        }

        Debug.Log($"[Outpost] Transformacja → {chosen.displayName}");

        // Spawn wybranego budynku na pozycji posterunku
        if (chosen.buildingPrefab != null)
            Instantiate(chosen.buildingPrefab, transform.position, transform.rotation);

        expansionManager.OnOutpostTransformed(chunkCoord);
        Destroy(gameObject);
    }

    // =========================================================================
    // ULEPSZENIA (modyfikują listę opcji transformacji)
    // =========================================================================

    /// <summary>
    /// Wywołaj gdy gracz kupi ulepszenie posterunku.
    /// Ulepszenie może podmienić lub dodać opcję transformacji.
    /// </summary>
    public void ApplyUpgrade(OutpostUpgrade upgrade)
    {
        foreach (var replacement in upgrade.replacedOptions)
        {
            // Szukamy opcji bazowej którą to ulepszenie zastępuje
            int idx = activeOptions.FindIndex(o => o.optionId == replacement.replacesOptionId);
            if (idx >= 0)
                activeOptions[idx] = replacement.newOption;
            else
                activeOptions.Add(replacement.newOption); // Nowa opcja
        }

        Debug.Log($"[Outpost] Ulepszenie '{upgrade.upgradeName}' zastosowane. " +
                  $"Opcji transformacji: {activeOptions.Count}");
    }

    // ─── Gettery ──────────────────────────────────────────────────────────────
    public bool            TransformAvailable => transformAvailable;
    public int             DaysRemaining      => daysRemaining;
    public Vector2Int      ChunkCoord         => chunkCoord;
    public List<TransformOption> ActiveOptions => activeOptions;
}

// ============================================================================
// Klasy pomocnicze (mogą żyć w osobnych plikach jeśli urosną)
// ============================================================================

[System.Serializable]
public class TransformOption
{
    [Tooltip("Unikalny ID opcji (do zastępowania przez ulepszenia)")]
    public string optionId;
    public string displayName;
    [TextArea] public string description;
    public GameObject buildingPrefab;

    // Przykładowe informacje dla UI
    public Race  primaryRace;
    public int       populationSlots;
}

[System.Serializable]
public class OutpostUpgrade
{
    public string upgradeName;
    public List<OptionReplacement> replacedOptions = new List<OptionReplacement>();
}

[System.Serializable]
public class OptionReplacement
{
    public string        replacesOptionId; // ID bazowej opcji do zastąpienia
    public TransformOption newOption;      // Nowa opcja po ulepszeniu
}