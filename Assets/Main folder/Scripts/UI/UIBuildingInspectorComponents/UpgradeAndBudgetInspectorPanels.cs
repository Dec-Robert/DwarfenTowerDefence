using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Panel inspektora obsługujący ulepszenia i zakup ich przez gracza.
/// Używany przez budynki ekonomiczne i domy.
/// </summary>
public class UpgradeInspectorPanel : BuildingInspectorPanel
{
    private readonly VisualElement upgradesList;
    private readonly VisualElement upgradeDetailsPanel;
    private readonly Label detTitle, detDesc, detEffects, detCost;
    private readonly Button btnConfirmBuy, btnCancelBuy;

    private BuildingEntity currentTarget;
    private BuildingUpgradeSO selectedUpgrade;

    private readonly System.Action onUpgradeBought;

    public UpgradeInspectorPanel(
        VisualElement root,
        VisualElement upgradesList,
        VisualElement upgradeDetailsPanel,
        Label detTitle, Label detDesc, Label detEffects, Label detCost,
        Button btnConfirmBuy, Button btnCancelBuy,
        System.Action onUpgradeBought)
        : base(root)
    {
        this.upgradesList        = upgradesList;
        this.upgradeDetailsPanel = upgradeDetailsPanel;
        this.detTitle            = detTitle;
        this.detDesc             = detDesc;
        this.detEffects          = detEffects;
        this.detCost             = detCost;
        this.btnConfirmBuy       = btnConfirmBuy;
        this.btnCancelBuy        = btnCancelBuy;
        this.onUpgradeBought     = onUpgradeBought;

        btnConfirmBuy?.RegisterCallback<ClickEvent>(_ => OnBuyConfirm());
        btnCancelBuy?.RegisterCallback<ClickEvent>(_ => CloseDetails());
    }

    public override void Refresh(BuildingEntity target)
    {
        currentTarget = target;
        GenerateUpgradesList();
    }

    public void CloseDetails()
    {
        selectedUpgrade = null;
        if (upgradeDetailsPanel != null)
            upgradeDetailsPanel.style.display = DisplayStyle.None;
    }

    // =========================================================================
    // Prywatne
    // =========================================================================

    private void GenerateUpgradesList()
    {
        upgradesList.Clear();
        var upgrades = currentTarget.GetAvailableUpgrades();

        if (upgrades.Count == 0)
        {
            upgradesList.Add(new Label("Maksymalny poziom."));
            return;
        }

        foreach (var up in upgrades)
        {
            var btn = new Button();
            btn.text = up.upgradeName;
            btn.AddToClassList("upgrade-btn");
            btn.RegisterCallback<ClickEvent>(_ => ShowUpgradeDetails(up));
            upgradesList.Add(btn);
        }
    }

    private void ShowUpgradeDetails(BuildingUpgradeSO upgrade)
    {
        selectedUpgrade = upgrade;
        if (upgradeDetailsPanel != null)
            upgradeDetailsPanel.style.display = DisplayStyle.Flex;

        if (detTitle != null) detTitle.text = upgrade.upgradeName;
        if (detDesc  != null) detDesc.text  = upgrade.description;

        if (detCost != null)
        {
            var sb = new StringBuilder("Koszt: ");
            foreach (var c in upgrade.cost) sb.Append($"{c.amount} {c.type}, ");
            detCost.text = sb.ToString().TrimEnd(',', ' ');
        }

        if (detEffects != null)
        {
            var sb = new StringBuilder();
            if (upgrade.productionBonus != null)
                foreach (var p in upgrade.productionBonus) sb.Append($"+{p.amount} {p.type} Prod, ");
            if (upgrade.upkeepIncrease != null)
                foreach (var u in upgrade.upkeepIncrease)  sb.Append($"+{u.amount} {u.type} Utrzymania, ");
            detEffects.text = sb.Length > 0 ? sb.ToString().TrimEnd(',', ' ') : "Brak zmiany statystyk";
        }
    }

    private void OnBuyConfirm()
    {
        if (selectedUpgrade == null || currentTarget == null) return;

        var costs = new Dictionary<ResourceType, float>();
        foreach (var c in selectedUpgrade.cost) costs.Add(c.type, c.amount);

        if (ResourceManager.Instance.SpendResources(costs))
        {
            currentTarget.ApplyUpgrade(selectedUpgrade);
            CloseDetails();
            onUpgradeBought?.Invoke();
        }
        else Debug.Log("[UpgradePanel] Nie stać Cię!");
    }
}

// =============================================================================

/// <summary>
/// Panel inspektora wyświetlający paski budżetu operacyjnego.
/// Używany tylko przez budynki ekonomiczne.
/// </summary>
public class BudgetInspectorPanel : BuildingInspectorPanel
{
    private readonly VisualElement budgetContainer;

    public BudgetInspectorPanel(VisualElement root, VisualElement budgetContainer)
        : base(root)
    {
        this.budgetContainer = budgetContainer;
    }

    public override void Refresh(BuildingEntity target)
    {
        if (budgetContainer == null) return;
        budgetContainer.Clear();

        var currentBudget = target.GetCurrentBudget();
        var maxBudget     = target.CalculateMaxDailyConsumption();

        if (maxBudget.Count == 0)
        {
            budgetContainer.Add(new Label("Brak kosztów operacyjnych"));
            return;
        }

        foreach (var kvp in maxBudget)
        {
            float maxVal     = kvp.Value;
            float currentVal = currentBudget.TryGetValue(kvp.Key, out float val) ? val : 0f;
            float percent    = Mathf.Clamp01(currentVal / maxVal) * 100f;

            var barBg = new VisualElement();
            barBg.AddToClassList("budget-bar-bg");

            var barFill = new VisualElement();
            barFill.AddToClassList("budget-bar-fill");
            barFill.style.width = Length.Percent(percent);
            barFill.style.backgroundColor = percent > 50f
                ? new UnityEngine.Color(0.2f, 0.8f, 0.2f)
                : percent > 20f
                    ? new UnityEngine.Color(0.9f, 0.8f, 0.1f)
                    : new UnityEngine.Color(0.9f, 0.2f, 0.2f);

            var barText = new Label($"{currentVal:F0} / {maxVal:F0} {kvp.Key}");
            barText.AddToClassList("budget-bar-text");

            barBg.Add(barFill);
            barBg.Add(barText);
            budgetContainer.Add(barBg);
        }
    }
}
