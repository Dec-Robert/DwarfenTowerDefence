using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EconomyUpgradeCardUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI titleText;
    public Image iconImage;
    public Button upgradeButton;
    public TextMeshProUGUI costText;

    private BuildingUpgradeSO myUpgrade;
    private BuildingEntity targetBuilding;
    private ContextMenuUI parentMenu; // ¯eby odœwie¿yæ menu po klikniêciu

    public void Setup(BuildingUpgradeSO upgrade, BuildingEntity building, ContextMenuUI menu)
    {
        myUpgrade = upgrade;
        targetBuilding = building;
        parentMenu = menu;

        titleText.text = upgrade.upgradeName;
        iconImage.sprite = upgrade.icon;

        // Koszt
        StringBuilder sb = new StringBuilder();
        foreach (var c in upgrade.cost) sb.AppendLine($"{c.amount} {c.type}");
        costText.text = sb.ToString();

        upgradeButton.onClick.RemoveAllListeners();
        upgradeButton.onClick.AddListener(TryBuyUpgrade);
    }

    // Obs³uga Hovera dla prawego panelu z opisem (Benefity)
    // W Unity u¿yj EventTrigger component w edytorze lub interfejsów IPointerEnterHandler
    public void OnPointerEnter()
    {
        parentMenu.ShowUpgradeDetails(myUpgrade);
    }

    public void OnPointerExit()
    {
        parentMenu.ClearUpgradeDetails();
    }

    void TryBuyUpgrade()
    {
        // Konwersja kosztów
        Dictionary<ResourceType, int> costs = new Dictionary<ResourceType, int>();
        foreach (var c in myUpgrade.cost) costs.Add(c.type, c.amount);

        if (ResourceManager.Instance.SpendResources(costs))
        {
            targetBuilding.ApplyUpgrade(myUpgrade);
            parentMenu.RefreshMenu(); // Odœwie¿ widok (przejœcie do nast. tieru)
        }
        else
        {
            Debug.Log("Nie staæ ciê na ulepszenie!");
        }
    }

    void Update()
    {
        // Sprawdzanie czy staæ (interaktywnoœæ przycisku)
        // ... (analogicznie do BuildUIButton)
    }
}