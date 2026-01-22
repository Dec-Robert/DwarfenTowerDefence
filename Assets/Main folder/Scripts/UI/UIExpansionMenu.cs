using UnityEngine;
using UnityEngine.UIElements;

public class UIExpansionMenu : MonoBehaviour
{
    [Header("Referencje")]
    public UIDocument uiDocument;
    public MapExpansionManager expansionManager;

    private VisualElement root;
    private VisualElement menuPanel;
    private Label lblTitle, lblDesc, lblCostVal;
    private VisualElement costContainer;
    private Button btnBuy, btnClose;

    private Vector2Int currentTarget;
    private Vector3 targetWorldPos;
    private bool isVisible = false;

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;
        menuPanel = root.Q<VisualElement>("ExpansionMenu");

        lblTitle = root.Q<Label>("Exp_Title");
        lblDesc = root.Q<Label>("Exp_Description");
        lblCostVal = root.Q<Label>("Exp_CostVal");
        costContainer = root.Q<VisualElement>("Exp_CostContainer");

        btnBuy = root.Q<Button>("Btn_Exp_Buy");
        btnClose = root.Q<Button>("Btn_Exp_Close");

        if (btnBuy != null) btnBuy.clicked += OnBuyClicked;
        if (btnClose != null) btnClose.clicked += Hide;
    }

    private void Update()
    {
        // Aktualizacja pozycji (pod¹¿anie za punktem na mapie)
        if (isVisible && menuPanel != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(targetWorldPos);
            // Odwrócenie Y dla UI Toolkit
            float panelY = Screen.height - screenPos.y;

            // Centrowanie panelu nad punktem
            menuPanel.style.left = screenPos.x - (menuPanel.layout.width / 2);
            menuPanel.style.top = panelY - (menuPanel.layout.height / 2);
        }
    }

    public void ShowMenu(Vector2Int chunkCoord, Vector3 worldPos)
    {
        currentTarget = chunkCoord;
        targetWorldPos = worldPos;

        UpdateContent();

        menuPanel.style.display = DisplayStyle.Flex;
        isVisible = true;
    }

    public void Hide()
    {
        if (menuPanel != null) menuPanel.style.display = DisplayStyle.None;
        isVisible = false;
    }

    void UpdateContent()
    {
        bool isBuyable = false;
        int cost = expansionManager.GetCurrentCost();

        // 1. Sprawdzenie stanu logicznego
        if (expansionManager.IsPending(currentTarget))
        {
            lblTitle.text = "Teren Odkrywany";
            lblDesc.text = "Ekspedycja w toku.\nTeren stanie siê dostêpny po zakoñczeniu obecnej fali wrogów.";
            lblDesc.style.color = new Color(0.5f, 1f, 0.5f); // Zielonkawy
        }
        else if (expansionManager.IsRoadBlocked(currentTarget))
        {
            lblTitle.text = "Mroczna Œcie¿ka";
            lblDesc.text = "Cienie grasuj¹ce w tym terenie nie pozostawi¹ nikogo przy ¿yciu.\nNajpierw zabezpiecz i odkryj wczeœniejsze odcinki drogi.";
            lblDesc.style.color = new Color(1f, 0.4f, 0.4f); // Czerwonawy
        }
        else if (expansionManager.IsTooFar(currentTarget))
        {
            lblTitle.text = "Dalekie Ziemie";
            lblDesc.text = "Zbyt daleko od bazy.\nMusisz najpierw przej¹æ s¹siedni teren.";
            lblDesc.style.color = Color.white;
        }
        else
        {
            // Mo¿na kupiæ
            lblTitle.text = "Dzicze";
            lblDesc.text = "Teren gotowy do przejêcia.\nCzy chcesz wys³aæ ekspedycjê?";
            lblDesc.style.color = Color.white;
            isBuyable = true;
        }

        // 2. Aktualizacja przycisków i ceny
        if (isBuyable)
        {
            costContainer.style.display = DisplayStyle.Flex;
            lblCostVal.text = $"{cost} Z³ota";
            btnBuy.style.display = DisplayStyle.Flex;

            // Sprawdzenie czy staæ
            bool canAfford = ResourceManager.Instance.CanAfford(ResourceType.Gold, cost);
            btnBuy.SetEnabled(canAfford);
        }
        else
        {
            costContainer.style.display = DisplayStyle.None;
            btnBuy.style.display = DisplayStyle.None;
        }
    }

    void OnBuyClicked()
    {
        expansionManager.PurchaseChunk(currentTarget);
    }
}