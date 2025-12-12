using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

public class ContextMenuUI : MonoBehaviour
{
    public static ContextMenuUI Instance { get; private set; }

    [Header("G³ówny Panel")]
    public GameObject contentPanel;

    [Header("Sekcja Info")]
    public Image buildingIcon;
    public TextMeshProUGUI buildingNameText;
    public TextMeshProUGUI upkeepText;
    public TextMeshProUGUI productionText;

    [Header("Sekcja Ulepszeñ")]
    public Transform upgradesContainer;
    public GameObject upgradeCardPrefab; // Pamiêtaj: Prefab musi mieæ EconomyUpgradeCardUI!
    public TextMeshProUGUI tierNameText;
    public GameObject maxLevelInfo;

    [Header("Sekcja Opisu")]
    public TextMeshProUGUI benefitsDescriptionText;

    [Header("Sekcja Akcji")]
    public Button demolishButton;

    private BuildingEntity currentSelection;

    private void Awake()
    {
        Instance = this;
        contentPanel.SetActive(false);
    }

    private void Start()
    {
        demolishButton.onClick.AddListener(DemolishBuilding);
    }

    public void ShowMenu(BuildingEntity building)
    {
        if (building.data.type != BuildingType.Economic && building.data.type != BuildingType.Utility)
        {
            return;
        }

        currentSelection = building;
        RefreshMenu();
        contentPanel.SetActive(true);
    }

    public void HideMenu()
    {
        contentPanel.SetActive(false);
        currentSelection = null;
    }

    public void RefreshMenu()
    {
        if (currentSelection == null) return;

        buildingNameText.text = currentSelection.data.buildingName;
        buildingIcon.sprite = currentSelection.data.icon;

        UpdateStatsText();

        foreach (Transform child in upgradesContainer) Destroy(child.gameObject);

        if (currentSelection.isMaxLevel)
        {
            tierNameText.text = "Maksymalny Poziom";
            maxLevelInfo.SetActive(true);
        }
        else
        {
            maxLevelInfo.SetActive(false);

            int tierIdx = currentSelection.currentTierIndex;
            // Sprawdzamy czy lista tierów istnieje i czy indeks jest poprawny
            if (currentSelection.data.upgradeTiers != null && tierIdx < currentSelection.data.upgradeTiers.Count)
            {
                var currentTier = currentSelection.data.upgradeTiers[tierIdx];
                tierNameText.text = currentTier.tierName;

                foreach (var upgrade in currentTier.availableUpgrades)
                {
                    GameObject card = Instantiate(upgradeCardPrefab, upgradesContainer);
                    // --- ZMIANA NAZWY KLASY ---
                    card.GetComponent<EconomyUpgradeCardUI>().Setup(upgrade, currentSelection, this);
                }
            }
            else
            {
                // Zabezpieczenie gdyby zabrak³o definicji Tierów w Data
                maxLevelInfo.SetActive(true);
            }
        }

        ClearUpgradeDetails();
    }

    void UpdateStatsText()
    {
        var prod = currentSelection.GetCurrentProduction();
        StringBuilder sbProd = new StringBuilder("Produkcja:\n");
        foreach (var p in prod) sbProd.AppendLine($"+{p.Value} {p.Key}");
        productionText.text = sbProd.ToString();

        var upk = currentSelection.GetCurrentUpkeep();
        StringBuilder sbUpk = new StringBuilder("Utrzymanie:\n");
        foreach (var u in upk) sbUpk.AppendLine($"-{u.Value} {u.Key}");
        upkeepText.text = sbUpk.ToString();
    }

    public void ShowUpgradeDetails(BuildingUpgradeSO upgrade)
    {
        benefitsDescriptionText.text = $"<b>{upgrade.upgradeName}</b>\n\n{upgrade.description}";

        if (upgrade.productionModifier != null && upgrade.productionModifier.Count > 0)
        {
            benefitsDescriptionText.text += "\n\nZmiany Produkcji:";
            foreach (var mod in upgrade.productionModifier)
                benefitsDescriptionText.text += $"\n+{mod.amount} {mod.type}";
        }
    }

    public void ClearUpgradeDetails()
    {
        benefitsDescriptionText.text = "Wybierz ulepszenie, aby zobaczyæ szczegó³y.";
    }

    void DemolishBuilding()
    {
        if (currentSelection != null)
        {
            Destroy(currentSelection.gameObject);
            HideMenu();
            Debug.Log("Budynek wyburzony.");
        }
    }
}