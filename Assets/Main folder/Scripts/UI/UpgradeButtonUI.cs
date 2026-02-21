using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class UpgradeButtonUI : MonoBehaviour
{
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public Button btn;
    public GameObject selectionHighlight; // Ramka zaznaczenia

    private BuildingUpgradeSO myUpgrade;
    private Action<BuildingUpgradeSO> onSelectCallback;

    public void Setup(BuildingUpgradeSO upgrade, Action<BuildingUpgradeSO> callback)
    {
        myUpgrade = upgrade;
        onSelectCallback = callback;

        if (nameText) nameText.text = upgrade.upgradeName;
        if (iconImage && upgrade.icon) iconImage.sprite = upgrade.icon;

        if (selectionHighlight) selectionHighlight.SetActive(false);

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => {
            onSelectCallback?.Invoke(myUpgrade);
            if (selectionHighlight) selectionHighlight.SetActive(true);
        });
    }

    public void Deselect()
    {
        if (selectionHighlight) selectionHighlight.SetActive(false);
    }
}//