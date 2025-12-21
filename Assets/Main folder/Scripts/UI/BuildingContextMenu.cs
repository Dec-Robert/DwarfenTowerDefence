using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text;

public class BuildingContextMenu : MonoBehaviour
{
    public static BuildingContextMenu Instance;

    [Header("G³ówne Elementy")]
    public GameObject panelRoot;
    public TextMeshProUGUI buildingNameText;
    public TextMeshProUGUI productionText;
    public TextMeshProUGUI consumptionText;
    public Image buildingIcon;

    [Header("Sekcja Ulepszeñ")]
    public TextMeshProUGUI tierText;
    public Transform upgradesContainer;
    public GameObject upgradeButtonPrefab;
    public GameObject finishedTextObject;

    [Header("Panel Boczny (Info o Ulepszeniu)")]
    public GameObject sideInfoPanel;
    public TextMeshProUGUI upgradeInfoText;
    public Button buyUpgradeButton;

    [Header("Akcje")]
    public Button destroyButton;
    public Button closeButton;

    private BuildingEntity currentTarget;
    private BuildingUpgradeSO selectedUpgrade;
    private List<UpgradeButtonUI> spawnedButtons = new List<UpgradeButtonUI>();

    private void Awake()
    {
        Instance = this;
        panelRoot.SetActive(false);
        sideInfoPanel.SetActive(false);

        buyUpgradeButton.onClick.AddListener(OnBuyUpgradeClicked);
        destroyButton.onClick.AddListener(OnDestroyClicked);
        if (closeButton) closeButton.onClick.AddListener(CloseMenu);
    }

    public void OpenMenu(BuildingEntity entity)
    {
        if (entity.data.type != BuildingType.Economic) return;

        currentTarget = entity;
        panelRoot.SetActive(true);
        selectedUpgrade = null;
        sideInfoPanel.SetActive(false);
        BuildingCitizenManager.Instance.Setup(entity);
        BuildingCitizenUI.Instance.Setup(entity.getMaxShifts(), entity.getMaxWorkersPerShift());
        BuildingCitizenUI.Instance.Refresh(entity);
        RefreshContent();
    }

    public void CloseMenu()
    {
        panelRoot.SetActive(false);
        currentTarget = null;
    }

    void RefreshContent()
    {
        if (currentTarget == null) return;

        // Nazwa i Tier (+1 dla gracza, ¿eby nie zaczynaæ od 0)
        buildingNameText.text = $"{currentTarget.data.buildingName} (Poziom {currentTarget.currentTier + 1})";
        if (currentTarget.data.icon) buildingIcon.sprite = currentTarget.data.icon;

        // Statystyki
        productionText.text = FormatResources(currentTarget.GetCurrentProduction());
        consumptionText.text = FormatResources(currentTarget.GetCurrentUpkeep());

        // Czyszczenie starych przycisków
        foreach (Transform child in upgradesContainer) Destroy(child.gameObject);
        spawnedButtons.Clear();

        List<BuildingUpgradeSO> options = currentTarget.GetAvailableUpgrades();

        if (options.Count > 0)
        {
            if (tierText) tierText.text = $"ULEPSZENIA (POZIOM {currentTarget.currentTier + 2})";
            if (finishedTextObject) finishedTextObject.SetActive(false);

            foreach (var upgrade in options)
            {
                GameObject btnObj = Instantiate(upgradeButtonPrefab, upgradesContainer);
                UpgradeButtonUI uiScript = btnObj.GetComponent<UpgradeButtonUI>();
                uiScript.Setup(upgrade, OnUpgradeSelected);
                spawnedButtons.Add(uiScript);
            }
        }
        else
        {
            if (tierText) tierText.text = "MAX POZIOM";
            if (finishedTextObject) finishedTextObject.SetActive(true);
        }
    }

    // --- TUTAJ BY£Y G£ÓWNE POPRAWKI ---
    void OnUpgradeSelected(BuildingUpgradeSO upgrade)
    {
        selectedUpgrade = upgrade;

        foreach (var btn in spawnedButtons) btn.Deselect();
        sideInfoPanel.SetActive(true);

        StringBuilder sb = new StringBuilder();

        // Tytu³ i opis
        sb.AppendLine($"<size=120%><b>{upgrade.upgradeName}</b></size>");
        sb.AppendLine($"<i>{upgrade.description}</i>");
        sb.AppendLine(""); // Pusta linia

        // KOSZT
        sb.AppendLine("<b>Koszt:</b>");
        if (upgrade.cost.Count == 0) sb.AppendLine("Darmowe");

        foreach (var cost in upgrade.cost)
        {
            // Jeœli staæ -> Bia³y (lub zielony), jeœli nie -> Czerwony
            bool canAfford = ResourceManager.Instance.CanAfford(cost.type, cost.amount);
            string color = canAfford ? "blue" : "red";
            sb.AppendLine($"<color={color}>- {cost.amount} {cost.type}</color>");
        }
        sb.AppendLine("");

        // ZMIANA PRODUKCJI
        if (upgrade.productionBonus != null && upgrade.productionBonus.Count > 0)
        {
            sb.AppendLine("<b>Produkcja:</b>");
            foreach (var production in upgrade.productionBonus)
            {
                // Wiêcej produkcji = Dobrze (Green), Mniej = le (Red)
                string color = production.amount >= 0 ? "#44FF44" : "red"; // Jasny zielony hex
                string sign = production.amount > 0 ? "+" : "";
                // POPRAWKA: U¿ycie zmiennej color w tagu
                sb.AppendLine($"<color={color}>{sign}{production.amount} {production.type}</color>");
            }
        }

        // ZMIANA UTRZYMANIA
        if (upgrade.upkeepIncrease != null && upgrade.upkeepIncrease.Count > 0)
        {
            sb.AppendLine("<b>Utrzymanie:</b>");
            foreach (var upkeep in upgrade.upkeepIncrease)
            {
                // Wiêkszy koszt = le (Red), Mniejszy koszt = Dobrze (Green)
                string color = upkeep.amount > 0 ? "red" : "#44FF44";
                string sign = upkeep.amount > 0 ? "+" : "";
                // POPRAWKA: U¿ycie zmiennej color w tagu
                sb.AppendLine($"<color={color}>{sign}{upkeep.amount} {upkeep.type}</color>");
            }
        }

        upgradeInfoText.text = sb.ToString();
    }

    void OnBuyUpgradeClicked()
    {
        if (selectedUpgrade == null || currentTarget == null) return;

        Dictionary<ResourceType, int> costs = new Dictionary<ResourceType, int>();
        foreach (var c in selectedUpgrade.cost) costs.Add(c.type, c.amount);

        if (ResourceManager.Instance.SpendResources(costs))
        {
            currentTarget.ApplyUpgrade(selectedUpgrade);
            RefreshContent();
            sideInfoPanel.SetActive(false);
        }
        else
        {
            Debug.Log("Nie staæ Ciê!");
        }
    }

    void OnDestroyClicked()
    {
        if (currentTarget != null)
        {
            currentTarget.Demolish();
            CloseMenu();
        }
    }

    string FormatResources(Dictionary<ResourceType, int> resources)
    {
        if (resources.Count == 0) return "-";
        StringBuilder sb = new StringBuilder();
        foreach (var kvp in resources)
        {
            sb.AppendLine($"{kvp.Value} {kvp.Key}");
        }
        return sb.ToString();
    }
}