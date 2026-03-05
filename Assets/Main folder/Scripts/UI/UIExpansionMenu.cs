using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class UIExpansionMenu : MonoBehaviour
{
    [Header("Referencje")]
    public UIDocument          uiDocument;
    public MapExpansionManager expansionManager;

    // ─── Elementy UI (Expansion Menu) ────────────────────────────────────────
    private VisualElement root;
    private VisualElement menuPanel;
    private Label         lblTitle, lblDesc, lblCostVal;
    private VisualElement costContainer;
    private Button        btnBuy, btnClose;

    // ─── Stan ────────────────────────────────────────────────────────────────
    private Vector2Int currentTarget;
    private Vector3    targetWorldPos;
    private bool       isVisible = false;

    // Tryb panelu (do obsługi kliknięcia Kup)
    private enum PanelMode { Scout, MissionInProgress, Blocked, TransformOptions }
    private PanelMode currentMode;

    // Dla trybu transformacji
    private OutpostEntity          pendingOutpost;
    private List<TransformOption>  pendingOptions;

    // =========================================================================
    // UNITY
    // =========================================================================

private void OnEnable()
    {
        root      = uiDocument.rootVisualElement;
        
        // Szukamy kontenera
        menuPanel = root.Q<VisualElement>("ExpansionMenu");
        if (menuPanel == null) Debug.LogError("[UIExpansionMenu] BŁĄD: Nie znaleziono elementu o nazwie 'ExpansionMenu' w UXML!");

        lblTitle      = root.Q<Label>("Exp_Title");
        if (lblTitle == null) Debug.LogError("[UIExpansionMenu] BŁĄD: Nie znaleziono elementu o nazwie 'Exp_Title' w UXML!");

        lblDesc       = root.Q<Label>("Exp_Description");
        if (lblDesc == null) Debug.LogError("[UIExpansionMenu] BŁĄD: Nie znaleziono elementu o nazwie 'Exp_Description' w UXML!");

        lblCostVal    = root.Q<Label>("Exp_CostVal");
        if (lblCostVal == null) Debug.LogError("[UIExpansionMenu] BŁĄD: Nie znaleziono elementu o nazwie 'Exp_CostVal' w UXML!");

        costContainer = root.Q<VisualElement>("Exp_CostContainer");
        if (costContainer == null) Debug.LogError("[UIExpansionMenu] BŁĄD: Nie znaleziono elementu o nazwie 'Exp_CostContainer' w UXML!");

        btnBuy   = root.Q<Button>("Btn_Exp_Buy");
        if (btnBuy == null) Debug.LogError("[UIExpansionMenu] BŁĄD: Nie znaleziono elementu o nazwie 'Btn_Exp_Buy' w UXML!");

        btnClose = root.Q<Button>("Btn_Exp_Close");
        if (btnClose == null) Debug.LogError("[UIExpansionMenu] BŁĄD: Nie znaleziono elementu o nazwie 'Btn_Exp_Close' w UXML!");

        // Podpinanie eventów tylko gdy guziki istnieją
        if (btnBuy != null) btnBuy.clicked += OnBuyClicked;
        if (btnClose != null) btnClose.clicked += Hide;
    }

    

    private void Update()
    {
        // Panel podąża za punktem na mapie
        if (!isVisible || menuPanel == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(targetWorldPos);
        float   panelY    = Screen.height - screenPos.y;

        menuPanel.style.left = screenPos.x - (menuPanel.layout.width  / 2f);
        menuPanel.style.top  = panelY      - (menuPanel.layout.height / 2f);
    }

    // =========================================================================
    // API PUBLICZNE – wywoływane przez MapExpansionManager
    // =========================================================================

    /// <summary>
    /// Główny punkt wejścia – pokaż menu dla danego chunka.
    /// UIExpansionMenu samo wykrywa stan i buduje treść.
    /// </summary>
    public void ShowMenu(Vector2Int chunkCoord, Vector3 worldPos)
    {
        currentTarget  = chunkCoord;
        targetWorldPos = worldPos;

        UpdateContent();

        menuPanel.style.display = DisplayStyle.Flex;
        isVisible = true;
    }

    /// <summary>Ukryj menu.</summary>
    public void Hide()
    {
        if (menuPanel != null) menuPanel.style.display = DisplayStyle.None;
        isVisible = false;
    }

    /// <summary>Chunk właśnie odkryty przez zwiadowcę – odśwież jeśli jesteśmy na tym chunku.</summary>
    public void OnChunkDiscovered(Vector2Int coord)
    {
        if (isVisible && currentTarget == coord)
            UpdateContent();
    }

    /// <summary>Chunk w pełni zasiedlony – odśwież jeśli otwarty.</summary>
    public void OnChunkFullyUnlocked(Vector2Int coord)
    {
        if (isVisible && currentTarget == coord)
            UpdateContent();
    }

    /// <summary>Posterunek gotowy do transformacji – pokaż opcje wyboru.</summary>
    public void ShowTransformOptions(OutpostEntity outpost, List<TransformOption> options)
    {
        pendingOutpost = outpost;
        pendingOptions = options;
        currentMode    = PanelMode.TransformOptions;

        // Ustawiamy pozycję na środku ekranu (posterunek nie ma worldPos w zasięgu kamery)
        targetWorldPos = outpost.transform.position;

        BuildTransformUI(options);

        menuPanel.style.display = DisplayStyle.Flex;
        isVisible = true;
    }

    // =========================================================================
    // BUDOWANIE TREŚCI
    // =========================================================================

    private void UpdateContent()
    {
        if (expansionManager == null) return;

        // Określ stan logiczny chunka
        if (expansionManager.IsInProcessOfScouting(currentTarget))
        {
            BuildScoutingInProgressUI();
        }
        else if (expansionManager.IsBlockedByRoad(currentTarget))
        {
            BuildBlockedUI();
        }
        else if (expansionManager.IsTooFar(currentTarget))
        {
            BuildTooFarUI();
        }
        else if (expansionManager.IsChunkScoutable(currentTarget))
        {
            BuildScoutUI();
        }
        else
        {
            Hide();
        }
    }

    // ─── Tryb: Zwiadowca w drodze ─────────────────────────────────────────────
    private void BuildScoutingInProgressUI()
    {
        currentMode = PanelMode.MissionInProgress;

        float progress = expansionManager.GetScoutingProgress(currentTarget);
        int   pct      = Mathf.RoundToInt(progress * 100f);

        lblTitle.text = "Teren Odkrywany";
        lblDesc.text  = $"Ekspedycja w toku... {pct}%\n" +
                        "Misja zakończy się po następnej fali.";
        lblDesc.style.color = new Color(0.5f, 1f, 0.5f);

        SetCostVisible(false);
        SetBuyVisible(false);
    }

    // ─── Tryb: Zablokowane przez drogę ───────────────────────────────────────
    private void BuildBlockedUI()
    {
        currentMode = PanelMode.Blocked;

        lblTitle.text = "Mroczna Ścieżka";
        lblDesc.text  = "Najpierw zabezpiecz wcześniejszy odcinek drogi.";
        lblDesc.style.color = new Color(1f, 0.4f, 0.4f);

        SetCostVisible(false);
        SetBuyVisible(false);
    }

    // ─── Tryb: Za daleko ──────────────────────────────────────────────────────
    private void BuildTooFarUI()
    {
        currentMode = PanelMode.Blocked;

        lblTitle.text = "Dalekie Ziemie";
        lblDesc.text  = "Zbyt daleko od bazy. Odkryj sąsiedni teren najpierw.";
        lblDesc.style.color = Color.white;

        SetCostVisible(false);
        SetBuyVisible(false);
    }

    // ─── Tryb: Tylko budownictwo wojskowe ─────────────────────────────────────
    private void BuildMilitaryOnlyUI()
    {
        currentMode = PanelMode.Blocked;

        lblTitle.text = "Teren Odkryty";
        lblDesc.text  = "Możesz tutaj budować wieże obronne.\n" +
                        "Postaw Posterunek aby odblokować pełne budownictwo.";
        lblDesc.style.color = new Color(1f, 0.8f, 0.3f);

        SetCostVisible(false);
        SetBuyVisible(false);
    }

    // ─── Tryb: Wyślij zwiadowcę ───────────────────────────────────────────────
    private void BuildScoutUI()
    {
        currentMode = PanelMode.Scout;

        MissionCost cost = expansionManager.GetMissionCost(currentTarget);

        // ZABEZPIECZENIE przed nullem
        if (lblTitle == null || lblDesc == null || lblCostVal == null || btnBuy == null)
        {
            Debug.LogError("[UIExpansionMenu] Błąd w BuildScoutUI: Któryś z elementów UI jest NULL! Sprawdź logi z OnEnable.");
            return;
        }

        lblTitle.text = "Ziemia Rozrzedzonej Mgły";
        lblDesc.text  = $"Czas podróży: {cost.days} dni\n" +
                        (cost.isRoad ? "<i>[Droga – tańsza ekspedycja]</i>" : "");
        lblDesc.style.color = Color.white;

        // Koszt
        SetCostVisible(true);
        lblCostVal.text = $"{cost.goldCost} Złota  +  {cost.foodCost} Jedzenia";

        // Przycisk – aktywny jeśli stać i jest wolne centrum
        bool canAffordGold = ResourceManager.Instance.CanAfford(ResourceType.Gold, cost.goldCost);
        bool canAffordFood = ResourceManager.Instance.CanAfford(ResourceType.Food, cost.foodCost);
        bool hasCenter     = expansionManager.HasFreeExpeditionCenter();

        SetBuyVisible(true);
        btnBuy.SetEnabled(canAffordGold && canAffordFood && hasCenter);

        if (!hasCenter)
            btnBuy.text = "Brak Centrum";
        else if (!canAffordGold || !canAffordFood)
            btnBuy.text = "Za Mało Zasobów";
        else
            btnBuy.text = "WYŚLIJ";
    }

    // ─── Tryb: Transformacja Posterunku ──────────────────────────────────────
    private void BuildTransformUI(List<TransformOption> options)
    {
        lblTitle.text = "Posterunek Gotowy";
        lblDesc.text  = "Posterunek może teraz przekształcić się.\nWybierz jeden z dostępnych budynków:";
        lblDesc.style.color = new Color(1f, 0.9f, 0.3f);

        SetCostVisible(false);

        // Nadpisujemy przycisk Kup pierwszą opcją (prosta implementacja)
        // Docelowo możesz tu zbudować dynamiczne przyciski per opcja
        if (options != null && options.Count > 0)
        {
            SetBuyVisible(true);
            btnBuy.text = $"→ {options[0].displayName}";
            btnBuy.SetEnabled(true);
        }
        else
        {
            SetBuyVisible(false);
        }
    }

    // =========================================================================
    // KLIKNIĘCIE KUP
    // =========================================================================

    private void OnBuyClicked()
    {
        switch (currentMode)
        {
            case PanelMode.Scout:
                expansionManager.TrySendScout(currentTarget);
                // Manager woła Hide() po sukcesie
                break;

            case PanelMode.TransformOptions:
                if (pendingOutpost != null && pendingOptions != null && pendingOptions.Count > 0)
                {
                    pendingOutpost.Transform(pendingOptions[0]);
                    Hide();
                }
                break;
        }
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private void SetCostVisible(bool visible)
    {
        if (costContainer != null)
            costContainer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void SetBuyVisible(bool visible)
    {
        if (btnBuy != null)
            btnBuy.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}