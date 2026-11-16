using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pojedyncza karta poziomu ulepszenia zaklęcia w Pnl_MainSpellModal (prefab Card_Spell_Tier).
/// </summary>
public class SpellTierCardUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI txtDescription;
    [SerializeField] private TextMeshProUGUI txtCostLabel;
    [SerializeField] private Transform pnlCostContainer;
    [SerializeField] private Button btnUpgrade;
    [SerializeField] private TextMeshProUGUI txtUpgradeLabel;
    [SerializeField] private GameObject singleResourcePrefab;
    [SerializeField] private ResourceVisualsSO resourceVisuals;

    private readonly List<SingleResourceUI> costItems = new List<SingleResourceUI>();

    public void AutoBindReferences()
    {
        txtDescription ??= transform.Find("Pnl_Info/Txt_Desc")?.GetComponent<TextMeshProUGUI>();
        txtCostLabel ??= transform.Find("Pnl_Info/Txt_Cost")?.GetComponent<TextMeshProUGUI>();
        if (pnlCostContainer == null)
        {
            var costTr = transform.Find("Pnl_Info/Pnl_Cost");
            if (costTr != null) pnlCostContainer = costTr;
        }
        btnUpgrade ??= transform.Find("Btn_Upgrade")?.GetComponent<Button>();
        txtUpgradeLabel ??= transform.Find("Btn_Upgrade/Txt_Label")?.GetComponent<TextMeshProUGUI>();
    }

    public void Setup(
        SpellUpgradeNodeData node,
        bool isCurrentAvailableTier,
        bool canAfford,
        GameObject fallbackSingleResourcePrefab,
        ResourceVisualsSO fallbackVisuals,
        Action onUpgrade)
    {
        AutoBindReferences();
        if (node == null) return;

        if (txtDescription != null)
            txtDescription.text = node.description;

        var prefabToUse = singleResourcePrefab != null ? singleResourcePrefab : fallbackSingleResourcePrefab;
        var visualsToUse = resourceVisuals != null ? resourceVisuals : fallbackVisuals;

        bool hasCosts = DruidTowerWindowUI.PopulateResourceCostContainer(
            pnlCostContainer,
            costItems,
            node.costs,
            prefabToUse,
            visualsToUse);

        if (txtCostLabel != null)
            txtCostLabel.gameObject.SetActive(hasCosts);

        if (pnlCostContainer != null)
            pnlCostContainer.gameObject.SetActive(hasCosts);

        if (btnUpgrade != null)
        {
            btnUpgrade.gameObject.SetActive(isCurrentAvailableTier);
            if (isCurrentAvailableTier)
            {
                btnUpgrade.interactable = canAfford;
                btnUpgrade.onClick.RemoveAllListeners();
                if (onUpgrade != null)
                {
                    btnUpgrade.onClick.AddListener(() => onUpgrade());
                }
            }
        }

        if (txtUpgradeLabel != null && isCurrentAvailableTier)
            txtUpgradeLabel.text = LocalizationManager.Get("DRUID_CAVE_UPGRADE_BTN", "Ulepsz");
    }
}
