using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel szczegółów wybranego węzła meta-ulepszenia w Menu Głównym.
/// Umożliwia inspekcję węzła, weryfikację wymagań oraz dokonanie trwałego zakupu za Nadzieję.
/// Standardy interfejsu zgodne ze specyfikacją GEMINI.md:
/// - 20 pt: Główny nagłówek / nazwa węzła (Txt_NodeName)
/// - 16 pt: Tytuły sekcji ("KOSZT ULEPSZENIA", "EFEKT", "WYMAGANIA") oraz przycisk akcji ("KUP ULEPSZENIE")
/// - 12-14 pt: Opisy i treść fabularna (Txt_Description)
/// - 10 pt: Teksty pomocnicze i wskazówki (Txt_Helper)
/// - 32 px: Standardowy rozmiar ikony węzła
/// - 24 px: Mała ikona zasobu Nadziei
/// </summary>
public class MetaNodeDetailsPanelUI : MonoBehaviour
{
    [Header("Typografia GEMINI.md")]
    public const float FontSizeHeader = 20f;
    public const float FontSizeSection = 16f;
    public const float FontSizeButton = 16f;
    public const float FontSizeDescription = 13f; // 12-14 pt
    public const float FontSizeHelper = 10f;

    [Header("Wymiary Ikon GEMINI.md")]
    public const float IconSizeStandard = 32f;
    public const float IconSizeSmall = 24f;

    [Header("Referencje UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI txtNodeName;
    [SerializeField] private Image imgNodeIcon;
    [SerializeField] private RectTransform imgNodeIconRect;
    [SerializeField] private TextMeshProUGUI txtCostSection;
    [SerializeField] private TextMeshProUGUI txtCostValue;
    [SerializeField] private Image imgHopeIcon;
    [SerializeField] private RectTransform imgHopeIconRect;
    [SerializeField] private TextMeshProUGUI txtEffectSection;
    [SerializeField] private TextMeshProUGUI txtDescription;
    [SerializeField] private TextMeshProUGUI txtPrereqSection;
    [SerializeField] private TextMeshProUGUI txtPrereqList;
    [SerializeField] private TextMeshProUGUI txtHelper;
    [SerializeField] private Button btnPurchase;
    [SerializeField] private TextMeshProUGUI txtPurchaseButton;
    [SerializeField] private Button btnClose;

    private MetaUpgradeSO currentUpgrade;
    private MetaFogState currentFogState = MetaFogState.Hidden;
    private Action<MetaUpgradeSO> onUpgradePurchasedCallback;

    [Header("Warianty A/B")]
    private GameObject pnlVariants;
    private Button btnVariantA;
    private Image imgVariantABorder;
    private TextMeshProUGUI txtVariantATitle;
    private TextMeshProUGUI txtVariantADesc;
    private Button btnVariantB;
    private Image imgVariantBBorder;
    private TextMeshProUGUI txtVariantBTitle;
    private TextMeshProUGUI txtVariantBDesc;
    private string selectedVariant = "A";

    // Publiczne gettery do inspekcji i testów jednostkowych
    public MetaUpgradeSO CurrentUpgrade => currentUpgrade;
    public MetaFogState CurrentFogState => currentFogState;
    public TextMeshProUGUI TxtNodeName => txtNodeName;
    public TextMeshProUGUI TxtCostSection => txtCostSection;
    public TextMeshProUGUI TxtCostValue => txtCostValue;
    public TextMeshProUGUI TxtEffectSection => txtEffectSection;
    public TextMeshProUGUI TxtDescription => txtDescription;
    public TextMeshProUGUI TxtPrereqSection => txtPrereqSection;
    public TextMeshProUGUI TxtPrereqList => txtPrereqList;
    public TextMeshProUGUI TxtHelper => txtHelper;
    public Button BtnPurchase => btnPurchase;
    public TextMeshProUGUI TxtPurchaseButton => txtPurchaseButton;
    public RectTransform ImgNodeIconRect => imgNodeIconRect;
    public RectTransform ImgHopeIconRect => imgHopeIconRect;

    private void Awake()
    {
        EnsureHierarchyBuilt();
        if (btnPurchase != null)
        {
            btnPurchase.onClick.RemoveAllListeners();
            btnPurchase.onClick.AddListener(OnPurchaseClicked);
        }
        if (btnClose != null)
        {
            btnClose.onClick.RemoveAllListeners();
            btnClose.onClick.AddListener(ClosePanel);
        }
    }

    /// <summary>
    /// Rejestruje callback powiadamiający kontroler grafu o zakupie węzła.
    /// </summary>
    public void SetOnPurchasedCallback(Action<MetaUpgradeSO> callback)
    {
        onUpgradePurchasedCallback = callback;
    }

    /// <summary>
    /// Wyświetla szczegóły węzła w zadanym stanie Mgły Odkrywania.
    /// </summary>
    public void DisplayNode(MetaUpgradeSO upgrade, MetaFogState fogState)
    {
        currentUpgrade = upgrade;
        currentFogState = fogState;

        EnsureHierarchyBuilt();

        if (upgrade == null || fogState == MetaFogState.Hidden)
        {
            ClosePanel();
            return;
        }

        if (panelRoot != null) panelRoot.SetActive(true);
        gameObject.SetActive(true);

        int currentHope = (SaveManager.Instance != null && SaveManager.Instance.currentSaveData != null)
            ? SaveManager.Instance.currentSaveData.totalHope
            : 0;

        switch (fogState)
        {
            case MetaFogState.Purchased:
                txtNodeName.text = upgrade.DisplayName;
                txtDescription.text = !string.IsNullOrEmpty(upgrade.description) ? upgrade.DisplayDescription : LocalizationManager.Get("META_UPGRADE_ACTIVE", "Ulepszenie aktywne.");
                txtCostValue.text = LocalizationManager.Get("META_PURCHASED", "WYKUPIONE");
                txtCostValue.color = new Color(0.55f, 0.95f, 0.55f);
                if (imgNodeIcon != null)
                {
                    imgNodeIcon.color = Color.white;
                    if (upgrade.icon != null) imgNodeIcon.sprite = upgrade.icon;
                }
                FormatPrerequisites(upgrade);
                txtHelper.text = LocalizationManager.Get("META_PERMANENT_INFO", "Ulepszenie jest trwale aktywne w Twoim Bastionie.");
                txtHelper.color = new Color(0.70f, 0.75f, 0.80f);
                btnPurchase.interactable = false;
                txtPurchaseButton.text = LocalizationManager.Get("META_UNLOCKED", "ODBLOKOWANE");
                break;

            case MetaFogState.Available:
                txtNodeName.text = upgrade.DisplayName;
                txtDescription.text = !string.IsNullOrEmpty(upgrade.description) ? upgrade.DisplayDescription : LocalizationManager.Get("META_NO_DESC", "Brak opisu.");
                txtCostValue.text = LocalizationManager.GetFormat("UI_META_COST_HOPE_FMT", upgrade.cost);
                txtCostValue.color = new Color(0.35f, 0.85f, 1.0f);
                if (imgNodeIcon != null)
                {
                    imgNodeIcon.color = Color.white;
                    if (upgrade.icon != null) imgNodeIcon.sprite = upgrade.icon;
                }
                FormatPrerequisites(upgrade);

                bool canAfford = currentHope >= upgrade.cost;
                btnPurchase.interactable = canAfford;
                txtPurchaseButton.text = LocalizationManager.Get("UI_META_PURCHASE_BTN", "KUP ULEPSZENIE");

                if (canAfford)
                {
                    txtHelper.text = LocalizationManager.Get("UI_META_ENOUGH_HOPE", "Posiadasz wystarczającą ilość Nadziei, aby odblokować to ulepszenie.");
                    txtHelper.color = new Color(0.40f, 0.90f, 0.50f);
                }
                else
                {
                    int missing = upgrade.cost - currentHope;
                    txtHelper.text = LocalizationManager.GetFormat("UI_META_NEED_HOPE_FMT", missing);
                    txtHelper.color = new Color(0.95f, 0.40f, 0.40f);
                }
                break;

            case MetaFogState.Mystery:
                txtNodeName.text = "???";
                txtDescription.text = LocalizationManager.Get("UI_META_MYSTERY_DESC", "Zbadaj wcześniejsze ulepszenia");
                txtCostValue.text = "???";
                txtCostValue.color = new Color(0.60f, 0.62f, 0.68f);
                if (imgNodeIcon != null)
                {
                    imgNodeIcon.color = new Color(0.25f, 0.28f, 0.35f, 0.7f);
                }
                txtPrereqList.text = LocalizationManager.Get("UI_META_PREREQ_UNKNOWN", "Wymagania: Nieznane (ukryte w mgle)");
                txtPrereqList.color = new Color(0.60f, 0.62f, 0.68f);
                txtHelper.text = LocalizationManager.Get("UI_META_PREREQ_REQUIRED", "Wymaga odblokowania wcześniejszych węzłów.");
                txtHelper.color = new Color(0.70f, 0.72f, 0.75f);
                btnPurchase.interactable = false;
                txtPurchaseButton.text = LocalizationManager.Get("UI_META_LOCKED", "ZABLOKOWANE");
                break;
        }

        UpdateVariantsUI();
    }

    /// <summary>
    /// Formatuje listę wymagań z kolorowaniem (zielony = spełnione, czerwony = brak).
    /// </summary>
    private void FormatPrerequisites(MetaUpgradeSO upgrade)
    {
        if (upgrade == null || upgrade.prerequisites == null || upgrade.prerequisites.Count == 0)
        {
            txtPrereqList.text = LocalizationManager.Get("UI_META_NO_PREREQ", "Brak wymagań wstępnych (Węzeł początkowy)");
            txtPrereqList.color = new Color(0.55f, 0.95f, 0.55f);
            return;
        }

        List<string> reqStrings = new List<string>();
        foreach (var req in upgrade.prerequisites)
        {
            if (req == null) continue;
            string colorHex = req.isUnlocked ? "#8DE68D" : "#E68D8D";
            string status = req.isUnlocked ? "(Odblokowane)" : "(Zablokowane)";
            reqStrings.Add($"• <color={colorHex}>{req.upgradeName} {status}</color>");
        }

        txtPrereqList.text = string.Join("\n", reqStrings);
    }

    /// <summary>
    /// Obsługa kliknięcia przycisku zakupu ulepszenia.
    /// </summary>
    public void OnPurchaseClicked()
    {
        if (currentUpgrade == null) return;

        // Jeśli to węzeł z wariantami i jest JUŻ wykupiony -> przełącz wariant za 0 Nadziei!
        if (currentUpgrade.hasVariants && currentFogState == MetaFogState.Purchased)
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.SwitchUpgradeVariant(currentUpgrade, selectedVariant);
            }
            Debug.Log($"[MetaNodeDetailsPanelUI] Przełączono wariant {currentUpgrade.upgradeName} na {selectedVariant}");
            DisplayNode(currentUpgrade, MetaFogState.Purchased);
            onUpgradePurchasedCallback?.Invoke(currentUpgrade);
            return;
        }

        if (currentFogState != MetaFogState.Available) return;

        int playerHope = (SaveManager.Instance != null && SaveManager.Instance.currentSaveData != null)
            ? SaveManager.Instance.currentSaveData.totalHope
            : 0;

        if (playerHope < currentUpgrade.cost)
        {
            Debug.LogWarning($"[MetaNodeDetailsPanelUI] Brak wystarczającej ilości Nadziei ({playerHope} / {currentUpgrade.cost})!");
            return;
        }

        // 1. Pobranie waluty Nadziei
        if (SaveManager.Instance != null && SaveManager.Instance.currentSaveData != null)
        {
            SaveManager.Instance.currentSaveData.totalHope -= currentUpgrade.cost;
        }

        // 2. Oznaczenie węzła jako odblokowanego w SO i SaveData
        currentUpgrade.isUnlocked = true;

        if (SaveManager.Instance != null && SaveManager.Instance.currentSaveData != null)
        {
            if (SaveManager.Instance.currentSaveData.unlockedUpgradeIDs == null)
            {
                SaveManager.Instance.currentSaveData.unlockedUpgradeIDs = new List<string>();
            }

            if (!SaveManager.Instance.currentSaveData.unlockedUpgradeIDs.Contains(currentUpgrade.id))
            {
                SaveManager.Instance.currentSaveData.unlockedUpgradeIDs.Add(currentUpgrade.id);
            }

            // Jeśli posiada warianty, odblokuj wybrany wariant A/B
            if (currentUpgrade.hasVariants)
            {
                MetaUpgradeSO targetVar = (selectedVariant == "B") ? currentUpgrade.variantB : currentUpgrade.variantA;
                if (targetVar != null)
                {
                    targetVar.isUnlocked = true;
                    if (!SaveManager.Instance.currentSaveData.unlockedUpgradeIDs.Contains(targetVar.id))
                    {
                        SaveManager.Instance.currentSaveData.unlockedUpgradeIDs.Add(targetVar.id);
                    }
                }
            }

            // 3. Trwały zapis gry do JSON
            SaveManager.Instance.SaveGame();
        }

        Debug.Log($"<color=green>[MetaNodeDetailsPanelUI] Zakupiono ulepszenie: {currentUpgrade.upgradeName} ({currentUpgrade.id}) za {currentUpgrade.cost} Nadziei.</color>");

        // 4. Powiadomienie MetaUpgradeManager
        if (MetaUpgradeManager.Instance != null)
        {
            MetaUpgradeManager.Instance.OnNodePurchased(currentUpgrade);
        }

        // 5. Odświeżenie widoku panelu i grafu
        DisplayNode(currentUpgrade, MetaFogState.Purchased);
        onUpgradePurchasedCallback?.Invoke(currentUpgrade);

        // 6. Jeśli to węzeł draftu nowej wieży, otwórz modal wyboru
        bool isDraft = (MetaUpgradeManager.Instance != null && MetaUpgradeManager.Instance.IsTowerDraftUpgrade(currentUpgrade)) ||
                       (!string.IsNullOrEmpty(currentUpgrade.id) && currentUpgrade.id.StartsWith("nowa_wie", StringComparison.OrdinalIgnoreCase));

        if (isDraft)
        {
            if (MetaUpgradeManager.Instance != null)
            {
                MetaUpgradeManager.Instance.TriggerTowerDraft();
            }
            else
            {
                TowerDraftModalUI draftModal = TowerDraftModalUI.Instance ?? TowerDraftModalUI.GetOrCreateInstance();
                if (draftModal != null)
                {
                    draftModal.ShowDraft(SaveManager.Instance?.allTowers);
                }
            }
        }
    }

    private void UpdateVariantsUI()
    {
        if (currentUpgrade == null || !currentUpgrade.hasVariants || currentFogState == MetaFogState.Hidden || currentFogState == MetaFogState.Mystery)
        {
            if (pnlVariants != null) pnlVariants.SetActive(false);
            if (txtDescription != null) txtDescription.gameObject.SetActive(true);
            return;
        }

        if (pnlVariants != null) pnlVariants.SetActive(true);
        if (txtDescription != null) txtDescription.gameObject.SetActive(false);

        List<string> unlockedIDs = (SaveManager.Instance != null && SaveManager.Instance.currentSaveData != null)
            ? SaveManager.Instance.currentSaveData.unlockedUpgradeIDs
            : new List<string>();

        string activeVariant = currentUpgrade.GetActiveVariant(unlockedIDs) ?? "A";
        if (currentFogState == MetaFogState.Purchased)
        {
            selectedVariant = activeVariant;
        }

        // Karta A
        if (txtVariantATitle != null && currentUpgrade.variantA != null)
        {
            txtVariantATitle.text = !string.IsNullOrEmpty(currentUpgrade.variantA.upgradeName) ? currentUpgrade.variantA.DisplayName : LocalizationManager.Get("UI_META_VARIANT_A", "Wariant A");
        }
        if (txtVariantADesc != null && currentUpgrade.variantA != null)
        {
            txtVariantADesc.text = currentUpgrade.variantA.DisplayDescription;
        }

        // Karta B
        if (txtVariantBTitle != null && currentUpgrade.variantB != null)
        {
            txtVariantBTitle.text = !string.IsNullOrEmpty(currentUpgrade.variantB.upgradeName) ? currentUpgrade.variantB.DisplayName : LocalizationManager.Get("UI_META_VARIANT_B", "Wariant B");
        }
        if (txtVariantBDesc != null && currentUpgrade.variantB != null)
        {
            txtVariantBDesc.text = currentUpgrade.variantB.DisplayDescription;
        }

        RefreshVariantSelectionVisuals(activeVariant);
    }

    private void RefreshVariantSelectionVisuals(string activeVariant)
    {
        if (imgVariantABorder != null)
        {
            bool isSelected = selectedVariant == "A";
            bool isActive = currentFogState == MetaFogState.Purchased && activeVariant == "A";
            imgVariantABorder.color = isActive ? new Color(0.20f, 0.40f, 0.25f, 0.98f)
                                    : (isSelected ? new Color(0.35f, 0.32f, 0.18f, 0.98f)
                                    : new Color(0.14f, 0.15f, 0.18f, 0.90f));
        }

        if (imgVariantBBorder != null)
        {
            bool isSelected = selectedVariant == "B";
            bool isActive = currentFogState == MetaFogState.Purchased && activeVariant == "B";
            imgVariantBBorder.color = isActive ? new Color(0.20f, 0.40f, 0.25f, 0.98f)
                                    : (isSelected ? new Color(0.35f, 0.32f, 0.18f, 0.98f)
                                    : new Color(0.14f, 0.15f, 0.18f, 0.90f));
        }

        if (currentFogState == MetaFogState.Purchased)
        {
            if (selectedVariant == activeVariant)
            {
                txtPurchaseButton.text = LocalizationManager.GetFormat("UI_META_VARIANT_ACTIVE_FMT", selectedVariant);
                btnPurchase.interactable = false;
                txtHelper.text = LocalizationManager.Get("UI_META_VARIANT_ACTIVE_HINT", "Ten wariant jest obecnie aktywny. Wybierz drugi wariant, aby go bezpłatnie przełączyć.");
                txtHelper.color = new Color(0.70f, 0.75f, 0.80f);
            }
            else
            {
                txtPurchaseButton.text = LocalizationManager.GetFormat("UI_META_ACTIVATE_VARIANT_FMT", selectedVariant);
                btnPurchase.interactable = true;
                txtHelper.text = LocalizationManager.GetFormat("UI_META_SWITCH_VARIANT_HINT", selectedVariant);
                txtHelper.color = new Color(0.40f, 0.90f, 0.50f);
            }
        }
        else if (currentFogState == MetaFogState.Available)
        {
            int currentHope = (SaveManager.Instance != null && SaveManager.Instance.currentSaveData != null)
                ? SaveManager.Instance.currentSaveData.totalHope
                : 0;

            bool canAfford = currentHope >= currentUpgrade.cost;
            btnPurchase.interactable = canAfford;
            txtPurchaseButton.text = LocalizationManager.GetFormat("UI_META_BUY_VARIANT_FMT", selectedVariant, currentUpgrade.cost);
            txtHelper.text = LocalizationManager.GetFormat("UI_META_BUY_VARIANT_HINT", selectedVariant);
        }
    }

    public void SelectVariant(string variant)
    {
        if (currentUpgrade == null || !currentUpgrade.hasVariants) return;
        selectedVariant = variant;

        List<string> unlockedIDs = (SaveManager.Instance != null && SaveManager.Instance.currentSaveData != null)
            ? SaveManager.Instance.currentSaveData.unlockedUpgradeIDs
            : new List<string>();

        string activeVariant = currentUpgrade.GetActiveVariant(unlockedIDs) ?? "A";
        RefreshVariantSelectionVisuals(activeVariant);
    }

    public void ClosePanel()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        gameObject.SetActive(false);
        currentUpgrade = null;
        currentFogState = MetaFogState.Hidden;
    }

    /// <summary>
    /// Buduje procedurę hierarchii panelu szczegółów zgodnie z GEMINI.md, jeśli brak prefabu.
    /// </summary>
    public void EnsureHierarchyBuilt()
    {
        if (panelRoot == null) panelRoot = gameObject;

        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect == null) rootRect = gameObject.AddComponent<RectTransform>();

        Image rootImg = GetComponent<Image>();
        if (rootImg == null)
        {
            rootImg = gameObject.AddComponent<Image>();
            rootImg.color = new Color(0.09f, 0.10f, 0.13f, 0.96f); // Ciemny grafit
        }

        // Nagłówek / Tytuł węzła (20 pt)
        Transform nameTrans = transform.Find("Txt_NodeName");
        if (nameTrans == null)
        {
            GameObject nameObj = new GameObject("Txt_NodeName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            nameObj.transform.SetParent(transform, false);
            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -20f);
            nameRect.sizeDelta = new Vector2(-40f, 40f);

            txtNodeName = nameObj.GetComponent<TextMeshProUGUI>();
            txtNodeName.fontSize = FontSizeHeader; // 20 pt
            txtNodeName.fontStyle = FontStyles.Bold;
            txtNodeName.color = new Color(0.95f, 0.85f, 0.45f); // Złoty
            txtNodeName.alignment = TextAlignmentOptions.MidlineLeft;
        }
        else if (txtNodeName == null)
        {
            txtNodeName = nameTrans.GetComponent<TextMeshProUGUI>();
        }

        // Ikona węzła (32 px)
        Transform iconTrans = transform.Find("Img_NodeIcon");
        if (iconTrans == null)
        {
            GameObject iconObj = new GameObject("Img_NodeIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObj.transform.SetParent(transform, false);
            imgNodeIconRect = iconObj.GetComponent<RectTransform>();
            imgNodeIconRect.anchorMin = new Vector2(1f, 1f);
            imgNodeIconRect.anchorMax = new Vector2(1f, 1f);
            imgNodeIconRect.pivot = new Vector2(1f, 1f);
            imgNodeIconRect.anchoredPosition = new Vector2(-20f, -20f);
            imgNodeIconRect.sizeDelta = new Vector2(IconSizeStandard, IconSizeStandard); // 32 px
            imgNodeIcon = iconObj.GetComponent<Image>();
            imgNodeIcon.preserveAspect = true;
        }
        else
        {
            if (imgNodeIcon == null) imgNodeIcon = iconTrans.GetComponent<Image>();
            if (imgNodeIconRect == null) imgNodeIconRect = iconTrans.GetComponent<RectTransform>();
        }

        // Sekcja Kosztu: Tytuł (16 pt)
        Transform costSecTrans = transform.Find("Txt_CostSection");
        if (costSecTrans == null)
        {
            GameObject costSecObj = new GameObject("Txt_CostSection", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            costSecObj.transform.SetParent(transform, false);
            RectTransform csRect = costSecObj.GetComponent<RectTransform>();
            csRect.anchorMin = new Vector2(0f, 1f);
            csRect.anchorMax = new Vector2(1f, 1f);
            csRect.pivot = new Vector2(0.5f, 1f);
            csRect.anchoredPosition = new Vector2(0f, -70f);
            csRect.sizeDelta = new Vector2(-40f, 26f);

            txtCostSection = costSecObj.GetComponent<TextMeshProUGUI>();
            txtCostSection.fontSize = FontSizeSection; // 16 pt
            txtCostSection.fontStyle = FontStyles.Bold;
            txtCostSection.color = new Color(0.85f, 0.88f, 0.95f);
            txtCostSection.text = LocalizationManager.Get("UI_META_COST_SECTION", "KOSZT ULEPSZENIA");
        }
        else if (txtCostSection == null)
        {
            txtCostSection = costSecTrans.GetComponent<TextMeshProUGUI>();
        }

        // Wartość kosztu (14 pt) oraz ikona Nadziei (24 px)
        Transform costValTrans = transform.Find("Txt_CostValue");
        if (costValTrans == null)
        {
            GameObject costValObj = new GameObject("Txt_CostValue", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            costValObj.transform.SetParent(transform, false);
            RectTransform cvRect = costValObj.GetComponent<RectTransform>();
            cvRect.anchorMin = new Vector2(0f, 1f);
            cvRect.anchorMax = new Vector2(1f, 1f);
            cvRect.pivot = new Vector2(0.5f, 1f);
            cvRect.anchoredPosition = new Vector2(0f, -98f);
            cvRect.sizeDelta = new Vector2(-40f, 24f);

            txtCostValue = costValObj.GetComponent<TextMeshProUGUI>();
            txtCostValue.fontSize = FontSizeDescription; // 13 pt
            txtCostValue.color = new Color(0.35f, 0.85f, 1.0f);
            txtCostValue.text = LocalizationManager.GetFormat("UI_META_COST_HOPE_FMT", 100);
        }
        else if (txtCostValue == null)
        {
            txtCostValue = costValTrans.GetComponent<TextMeshProUGUI>();
        }

        Transform hopeIconTrans = transform.Find("Img_HopeIcon");
        if (hopeIconTrans == null)
        {
            GameObject hopeIconObj = new GameObject("Img_HopeIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hopeIconObj.transform.SetParent(transform, false);
            imgHopeIconRect = hopeIconObj.GetComponent<RectTransform>();
            imgHopeIconRect.anchorMin = new Vector2(0f, 1f);
            imgHopeIconRect.anchorMax = new Vector2(0f, 1f);
            imgHopeIconRect.pivot = new Vector2(0f, 1f);
            imgHopeIconRect.anchoredPosition = new Vector2(20f, -98f);
            imgHopeIconRect.sizeDelta = new Vector2(IconSizeSmall, IconSizeSmall); // 24 px
            imgHopeIcon = hopeIconObj.GetComponent<Image>();
        }
        else
        {
            if (imgHopeIcon == null) imgHopeIcon = hopeIconTrans.GetComponent<Image>();
            if (imgHopeIconRect == null) imgHopeIconRect = hopeIconTrans.GetComponent<RectTransform>();
        }

        // Sekcja Efektu: Tytuł (16 pt)
        Transform effSecTrans = transform.Find("Txt_EffectSection");
        if (effSecTrans == null)
        {
            GameObject effSecObj = new GameObject("Txt_EffectSection", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            effSecObj.transform.SetParent(transform, false);
            RectTransform esRect = effSecObj.GetComponent<RectTransform>();
            esRect.anchorMin = new Vector2(0f, 1f);
            esRect.anchorMax = new Vector2(1f, 1f);
            esRect.pivot = new Vector2(0.5f, 1f);
            esRect.anchoredPosition = new Vector2(0f, -135f);
            esRect.sizeDelta = new Vector2(-40f, 26f);

            txtEffectSection = effSecObj.GetComponent<TextMeshProUGUI>();
            txtEffectSection.fontSize = FontSizeSection; // 16 pt
            txtEffectSection.fontStyle = FontStyles.Bold;
            txtEffectSection.color = new Color(0.85f, 0.88f, 0.95f);
            txtEffectSection.text = LocalizationManager.Get("UI_META_EFFECT_SECTION", "EFEKT I OPIS");
        }
        else if (txtEffectSection == null)
        {
            txtEffectSection = effSecTrans.GetComponent<TextMeshProUGUI>();
        }

        // Opis ulepszenia (12-14 pt)
        Transform descTrans = transform.Find("Txt_Description");
        if (descTrans == null)
        {
            GameObject descObj = new GameObject("Txt_Description", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            descObj.transform.SetParent(transform, false);
            RectTransform descRect = descObj.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0f, 1f);
            descRect.anchorMax = new Vector2(1f, 1f);
            descRect.pivot = new Vector2(0.5f, 1f);
            descRect.anchoredPosition = new Vector2(0f, -165f);
            descRect.sizeDelta = new Vector2(-40f, 85f);

            txtDescription = descObj.GetComponent<TextMeshProUGUI>();
            txtDescription.fontSize = FontSizeDescription; // 13 pt
            txtDescription.color = new Color(0.82f, 0.84f, 0.88f);
        }
        else if (txtDescription == null)
        {
            txtDescription = descTrans.GetComponent<TextMeshProUGUI>();
        }

        // Kontener Wariantów A/B
        Transform varTrans = transform.Find("Pnl_Variants");
        if (varTrans == null)
        {
            pnlVariants = new GameObject("Pnl_Variants", typeof(RectTransform));
            pnlVariants.transform.SetParent(transform, false);
            RectTransform varRect = pnlVariants.GetComponent<RectTransform>();
            varRect.anchorMin = new Vector2(0f, 1f);
            varRect.anchorMax = new Vector2(1f, 1f);
            varRect.pivot = new Vector2(0.5f, 1f);
            varRect.anchoredPosition = new Vector2(0f, -140f);
            varRect.sizeDelta = new Vector2(-40f, 110f);

            // Karta Wariantu A
            GameObject cardAObj = new GameObject("Card_VariantA", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            cardAObj.transform.SetParent(pnlVariants.transform, false);
            RectTransform cardARect = cardAObj.GetComponent<RectTransform>();
            cardARect.anchorMin = new Vector2(0f, 0f);
            cardARect.anchorMax = new Vector2(0.485f, 1f);
            cardARect.offsetMin = Vector2.zero;
            cardARect.offsetMax = Vector2.zero;
            imgVariantABorder = cardAObj.GetComponent<Image>();
            imgVariantABorder.color = new Color(0.18f, 0.20f, 0.25f, 0.95f);
            btnVariantA = cardAObj.GetComponent<Button>();
            btnVariantA.onClick.AddListener(() => SelectVariant("A"));

            GameObject titleAObj = new GameObject("Txt_TitleA", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleAObj.transform.SetParent(cardAObj.transform, false);
            RectTransform tARect = titleAObj.GetComponent<RectTransform>();
            tARect.anchorMin = new Vector2(0f, 0.55f);
            tARect.anchorMax = new Vector2(1f, 1f);
            tARect.offsetMin = new Vector2(8f, 0f);
            tARect.offsetMax = new Vector2(-8f, -4f);
            txtVariantATitle = titleAObj.GetComponent<TextMeshProUGUI>();
            txtVariantATitle.fontSize = 13f;
            txtVariantATitle.fontStyle = FontStyles.Bold;
            txtVariantATitle.color = new Color(0.95f, 0.85f, 0.45f);

            GameObject descAObj = new GameObject("Txt_DescA", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            descAObj.transform.SetParent(cardAObj.transform, false);
            RectTransform dARect = descAObj.GetComponent<RectTransform>();
            dARect.anchorMin = new Vector2(0f, 0f);
            dARect.anchorMax = new Vector2(1f, 0.55f);
            dARect.offsetMin = new Vector2(8f, 4f);
            dARect.offsetMax = new Vector2(-8f, 0f);
            txtVariantADesc = descAObj.GetComponent<TextMeshProUGUI>();
            txtVariantADesc.fontSize = 11f;
            txtVariantADesc.color = new Color(0.80f, 0.82f, 0.86f);

            // Karta Wariantu B
            GameObject cardBObj = new GameObject("Card_VariantB", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            cardBObj.transform.SetParent(pnlVariants.transform, false);
            RectTransform cardBRect = cardBObj.GetComponent<RectTransform>();
            cardBRect.anchorMin = new Vector2(0.515f, 0f);
            cardBRect.anchorMax = new Vector2(1f, 1f);
            cardBRect.offsetMin = Vector2.zero;
            cardBRect.offsetMax = Vector2.zero;
            imgVariantBBorder = cardBObj.GetComponent<Image>();
            imgVariantBBorder.color = new Color(0.18f, 0.20f, 0.25f, 0.95f);
            btnVariantB = cardBObj.GetComponent<Button>();
            btnVariantB.onClick.AddListener(() => SelectVariant("B"));

            GameObject titleBObj = new GameObject("Txt_TitleB", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleBObj.transform.SetParent(cardBObj.transform, false);
            RectTransform tBRect = titleBObj.GetComponent<RectTransform>();
            tBRect.anchorMin = new Vector2(0f, 0.55f);
            tBRect.anchorMax = new Vector2(1f, 1f);
            tBRect.offsetMin = new Vector2(8f, 0f);
            tBRect.offsetMax = new Vector2(-8f, -4f);
            txtVariantBTitle = titleBObj.GetComponent<TextMeshProUGUI>();
            txtVariantBTitle.fontSize = 13f;
            txtVariantBTitle.fontStyle = FontStyles.Bold;
            txtVariantBTitle.color = new Color(0.95f, 0.85f, 0.45f);

            GameObject descBObj = new GameObject("Txt_DescB", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            descBObj.transform.SetParent(cardBObj.transform, false);
            RectTransform dBRect = descBObj.GetComponent<RectTransform>();
            dBRect.anchorMin = new Vector2(0f, 0f);
            dBRect.anchorMax = new Vector2(1f, 0.55f);
            dBRect.offsetMin = new Vector2(8f, 4f);
            dBRect.offsetMax = new Vector2(-8f, 0f);
            txtVariantBDesc = descBObj.GetComponent<TextMeshProUGUI>();
            txtVariantBDesc.fontSize = 11f;
            txtVariantBDesc.color = new Color(0.80f, 0.82f, 0.86f);

            pnlVariants.SetActive(false);
        }

        // Sekcja Wymagań: Tytuł (16 pt)
        Transform prereqSecTrans = transform.Find("Txt_PrereqSection");
        if (prereqSecTrans == null)
        {
            GameObject prereqSecObj = new GameObject("Txt_PrereqSection", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            prereqSecObj.transform.SetParent(transform, false);
            RectTransform psRect = prereqSecObj.GetComponent<RectTransform>();
            psRect.anchorMin = new Vector2(0f, 1f);
            psRect.anchorMax = new Vector2(1f, 1f);
            psRect.pivot = new Vector2(0.5f, 1f);
            psRect.anchoredPosition = new Vector2(0f, -260f);
            psRect.sizeDelta = new Vector2(-40f, 26f);

            txtPrereqSection = prereqSecObj.GetComponent<TextMeshProUGUI>();
            txtPrereqSection.fontSize = FontSizeSection; // 16 pt
            txtPrereqSection.fontStyle = FontStyles.Bold;
            txtPrereqSection.color = new Color(0.85f, 0.88f, 0.95f);
            txtPrereqSection.text = LocalizationManager.Get("UI_META_PREREQ_HEADER", "WYMAGANIA WSTĘPNE");
        }
        else if (txtPrereqSection == null)
        {
            txtPrereqSection = prereqSecTrans.GetComponent<TextMeshProUGUI>();
        }

        // Lista wymagań (12-14 pt)
        Transform prereqListTrans = transform.Find("Txt_PrereqList");
        if (prereqListTrans == null)
        {
            GameObject prereqListObj = new GameObject("Txt_PrereqList", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            prereqListObj.transform.SetParent(transform, false);
            RectTransform plRect = prereqListObj.GetComponent<RectTransform>();
            plRect.anchorMin = new Vector2(0f, 1f);
            plRect.anchorMax = new Vector2(1f, 1f);
            plRect.pivot = new Vector2(0.5f, 1f);
            plRect.anchoredPosition = new Vector2(0f, -290f);
            plRect.sizeDelta = new Vector2(-40f, 75f);

            txtPrereqList = prereqListObj.GetComponent<TextMeshProUGUI>();
            txtPrereqList.fontSize = FontSizeDescription; // 13 pt
        }
        else if (txtPrereqList == null)
        {
            txtPrereqList = prereqListTrans.GetComponent<TextMeshProUGUI>();
        }

        // Tekst pomocniczy / Hint (10 pt)
        Transform helperTrans = transform.Find("Txt_Helper");
        if (helperTrans == null)
        {
            GameObject helperObj = new GameObject("Txt_Helper", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            helperObj.transform.SetParent(transform, false);
            RectTransform helperRect = helperObj.GetComponent<RectTransform>();
            helperRect.anchorMin = new Vector2(0f, 0f);
            helperRect.anchorMax = new Vector2(1f, 0f);
            helperRect.pivot = new Vector2(0.5f, 0f);
            helperRect.anchoredPosition = new Vector2(0f, 75f);
            helperRect.sizeDelta = new Vector2(-40f, 35f);

            txtHelper = helperObj.GetComponent<TextMeshProUGUI>();
            txtHelper.fontSize = FontSizeHelper; // 10 pt
            txtHelper.color = new Color(0.70f, 0.72f, 0.76f);
            txtHelper.alignment = TextAlignmentOptions.Center;
        }
        else if (txtHelper == null)
        {
            txtHelper = helperTrans.GetComponent<TextMeshProUGUI>();
        }

        // Przycisk Zakupu ("KUP ULEPSZENIE", 16 pt)
        Transform btnTrans = transform.Find("Btn_Purchase");
        if (btnTrans == null)
        {
            GameObject btnObj = new GameObject("Btn_Purchase", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(transform, false);
            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0f, 0f);
            btnRect.anchorMax = new Vector2(1f, 0f);
            btnRect.pivot = new Vector2(0.5f, 0f);
            btnRect.anchoredPosition = new Vector2(0f, 20f);
            btnRect.sizeDelta = new Vector2(-40f, 48f);

            Image btnImg = btnObj.GetComponent<Image>();
            btnImg.color = new Color(0.18f, 0.55f, 0.28f, 1f); // Zielony akcent

            btnPurchase = btnObj.GetComponent<Button>();

            GameObject btnTextObj = new GameObject("Txt_Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            btnTextObj.transform.SetParent(btnObj.transform, false);
            RectTransform btRect = btnTextObj.GetComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.offsetMin = Vector2.zero;
            btRect.offsetMax = Vector2.zero;

            txtPurchaseButton = btnTextObj.GetComponent<TextMeshProUGUI>();
            txtPurchaseButton.fontSize = FontSizeButton; // 16 pt
            txtPurchaseButton.fontStyle = FontStyles.Bold;
            txtPurchaseButton.alignment = TextAlignmentOptions.Center;
            txtPurchaseButton.color = Color.white;
            txtPurchaseButton.text = LocalizationManager.Get("UI_META_PURCHASE_BTN", "KUP ULEPSZENIE");
        }
        else
        {
            if (btnPurchase == null) btnPurchase = btnTrans.GetComponent<Button>();
            if (txtPurchaseButton == null) txtPurchaseButton = btnTrans.GetComponentInChildren<TextMeshProUGUI>();
        }
    }
}
