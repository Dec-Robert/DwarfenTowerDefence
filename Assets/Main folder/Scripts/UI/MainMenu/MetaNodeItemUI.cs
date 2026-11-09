using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Poziomy widoczności w Mgle Odkrywania (Fog of War) meta-drzewa.
/// </summary>
public enum MetaFogState
{
    Purchased,  // Poziom i: Wykupione
    Available,  // Poziom i+1: Dostępne do zakupu (spełnione wymagania)
    Mystery,    // Poziom i+2: Zamazane / Tajemnicze (wymagania niespełnione, ale bezpośredni poprzednik i lub i+1)
    Hidden      // Poziom i+3+: Głęboka mgła (całkowicie ukryte)
}

/// <summary>
/// Komponent pojedynczego węzła w grafie 2D meta-progresji.
/// Odpowiada za wyświetlanie węzła w jednym z 4 stanów Mgły Odkrywania (Fog of War),
/// pozycjonowanie na bazie upgrade.nodePosition oraz reakcję na kliknięcie.
/// Zgodne z wytycznymi GEMINI.md:
/// - 12-14 pt: Tytuł i etykiety węzła
/// - 10 pt: Pomocniczy tekst stanu
/// - 32 px: Standardowy rozmiar ikony
/// </summary>
public class MetaNodeItemUI : MonoBehaviour
{
    [Header("Wymiary węzła")]
    public const float NodeWidth = 180f;
    public const float NodeHeight = 64f;

    [Header("Typografia GEMINI.md")]
    public const float FontSizeTitle = 13f;
    public const float FontSizeCost = 11f;
    public const float FontSizeHelper = 10f;

    [Header("Wymiary ikon GEMINI.md")]
    public const float IconSize = 32f;

    [Header("Referencje UI")]
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Image bgImage;
    [SerializeField] private Image borderImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI txtTitle;
    [SerializeField] private TextMeshProUGUI txtCost;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button btnSelect;

    [Header("Kolorystyka stanów")]
    public static readonly Color ColorPurchasedBorder = new Color(0.95f, 0.78f, 0.20f, 1.0f); // Jasne złoto
    public static readonly Color ColorPurchasedBg = new Color(0.16f, 0.20f, 0.14f, 0.95f);     // Ciemna oliwka/złoto
    public static readonly Color ColorAvailableBorder = new Color(0.20f, 0.80f, 1.00f, 1.0f); // Jasny cyan / błękit
    public static readonly Color ColorAvailableBg = new Color(0.12f, 0.16f, 0.22f, 0.95f);     // Ciemny błękit
    public static readonly Color ColorMysteryBorder = new Color(0.40f, 0.44f, 0.52f, 0.60f);   // Zamglony grafit/fiolet
    public static readonly Color ColorMysteryBg = new Color(0.10f, 0.11f, 0.13f, 0.85f);       // Ciemny węgiel

    private MetaUpgradeSO upgrade;
    private MetaFogState currentFogState = MetaFogState.Hidden;
    private Action<MetaUpgradeSO> onSelectedCallback;

    public MetaUpgradeSO Upgrade => upgrade;
    public MetaFogState CurrentFogState => currentFogState;
    public RectTransform RectTransform => rectTransform != null ? rectTransform : (rectTransform = GetComponent<RectTransform>());
    public TextMeshProUGUI TxtTitle => txtTitle;
    public TextMeshProUGUI TxtCost => txtCost;
    public Image IconImage => iconImage;
    public Image BorderImage => borderImage;
    public Button BtnSelect => btnSelect;

    private void Awake()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (btnSelect == null) btnSelect = GetComponent<Button>();
    }

    /// <summary>
    /// Dynamicznie wylicza 4-poziomowy stan Mgły Odkrywania (Fog of War) dla danego węzła:
    /// - Level i (Purchased): upgrade.isUnlocked == true
    /// - Level i+1 (Available): !upgrade.isUnlocked && upgrade.ArePrerequisitesMet()
    /// - Level i+2 (Mystery): !upgrade.isUnlocked && !upgrade.ArePrerequisitesMet(), lecz co najmniej jeden rodzic ma Level i lub i+1
    /// - Level i+3+ (Hidden): wszystkie inne przypadki (całkowicie ukryte w głębokiej mgle)
    /// </summary>
    public static MetaFogState CalculateFogState(MetaUpgradeSO upgrade)
    {
        if (ReferenceEquals(upgrade, null)) return MetaFogState.Hidden;

        if (upgrade.isUnlocked)
        {
            return MetaFogState.Purchased;
        }

        if (upgrade.ArePrerequisitesMet())
        {
            return MetaFogState.Available;
        }

        // Sprawdź czy co najmniej jedno wymaganie jest na poziomie i (Purchased) lub i+1 (Available)
        if (upgrade.prerequisites != null && upgrade.prerequisites.Count > 0)
        {
            foreach (var prereq in upgrade.prerequisites)
            {
                if (ReferenceEquals(prereq, null)) continue;
                if (prereq.isUnlocked || prereq.ArePrerequisitesMet())
                {
                    return MetaFogState.Mystery;
                }
            }
        }

        return MetaFogState.Hidden;
    }

    /// <summary>
    /// Inicjalizuje węzeł danymi upgrade'u i podpina callback wyboru.
    /// </summary>
    public void Setup(MetaUpgradeSO node, Action<MetaUpgradeSO> onSelect)
    {
        upgrade = node;
        onSelectedCallback = onSelect;

        EnsureHierarchyBuilt();

        if (rectTransform != null && upgrade != null)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = upgrade.nodePosition;
            rectTransform.sizeDelta = new Vector2(NodeWidth, NodeHeight);
        }

        MetaFogState state = CalculateFogState(upgrade);
        UpdateVisuals(state);
    }

    /// <summary>
    /// Aktualizuje stan graficzny węzła w zależności od poziomu Mgły Odkrywania.
    /// </summary>
    public void UpdateVisuals(MetaFogState state)
    {
        currentFogState = state;
        EnsureHierarchyBuilt();

        if (state == MetaFogState.Hidden)
        {
            gameObject.SetActive(false);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            return;
        }

        gameObject.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        switch (state)
        {
            case MetaFogState.Purchased:
                if (canvasGroup != null) canvasGroup.alpha = 1.0f;
                if (borderImage != null) borderImage.color = ColorPurchasedBorder;
                if (bgImage != null) bgImage.color = ColorPurchasedBg;
                if (txtTitle != null)
                {
                    txtTitle.text = upgrade != null ? upgrade.DisplayName : LocalizationManager.Get("UI_META_START_NODE", "Start");
                    txtTitle.color = new Color(0.96f, 0.90f, 0.70f);
                }
                if (txtCost != null)
                {
                    txtCost.text = LocalizationManager.Get("META_UNLOCKED", "ODBLOKOWANE");
                    txtCost.color = new Color(0.55f, 0.95f, 0.55f);
                }
                if (iconImage != null)
                {
                    iconImage.color = Color.white;
                    if (upgrade != null && upgrade.icon != null) iconImage.sprite = upgrade.icon;
                }
                break;

            case MetaFogState.Available:
                if (canvasGroup != null) canvasGroup.alpha = 1.0f;
                if (borderImage != null) borderImage.color = ColorAvailableBorder;
                if (bgImage != null) bgImage.color = ColorAvailableBg;
                if (txtTitle != null)
                {
                    txtTitle.text = upgrade != null ? upgrade.DisplayName : "";
                    txtTitle.color = Color.white;
                }
                if (txtCost != null)
                {
                    txtCost.text = upgrade != null ? LocalizationManager.GetFormat("UI_META_COST_HOPE_FMT", upgrade.cost) : "";
                    txtCost.color = new Color(0.35f, 0.85f, 1.0f);
                }
                if (iconImage != null)
                {
                    iconImage.color = Color.white;
                    if (upgrade != null && upgrade.icon != null) iconImage.sprite = upgrade.icon;
                }
                break;

            case MetaFogState.Mystery:
                if (canvasGroup != null) canvasGroup.alpha = 0.65f;
                if (borderImage != null) borderImage.color = ColorMysteryBorder;
                if (bgImage != null) bgImage.color = ColorMysteryBg;
                if (txtTitle != null)
                {
                    txtTitle.text = "???";
                    txtTitle.color = new Color(0.70f, 0.72f, 0.75f);
                }
                if (txtCost != null)
                {
                    txtCost.text = "???";
                    txtCost.color = new Color(0.60f, 0.62f, 0.68f);
                }
                if (iconImage != null)
                {
                    iconImage.color = new Color(0.20f, 0.22f, 0.28f, 0.6f); // Sylwetka / cień
                }
                break;
        }
    }

    public void OnNodeClicked()
    {
        if (currentFogState == MetaFogState.Hidden) return;
        onSelectedCallback?.Invoke(upgrade);
    }

    /// <summary>
    /// Buduje procedurę UI jeśli węzeł został stworzony dynamicznie (np. bez prefabu).
    /// </summary>
    public void EnsureHierarchyBuilt()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null) rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (borderImage == null)
        {
            if (GetComponent<CanvasRenderer>() == null)
            {
                gameObject.AddComponent<CanvasRenderer>();
            }
            borderImage = GetComponent<Image>();
            if (borderImage == null) borderImage = gameObject.AddComponent<Image>();
            borderImage.color = ColorAvailableBorder;
        }

        if (btnSelect == null)
        {
            btnSelect = GetComponent<Button>();
            if (btnSelect == null) btnSelect = gameObject.AddComponent<Button>();
            btnSelect.targetGraphic = borderImage;
            btnSelect.onClick.RemoveAllListeners();
            btnSelect.onClick.AddListener(OnNodeClicked);
        }

        // Tło wewnętrzne (Background)
        Transform bgTrans = transform.Find("Bg");
        if (bgTrans == null)
        {
            GameObject bgObj = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgObj.transform.SetParent(transform, false);
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = new Vector2(2f, 2f);
            bgRect.offsetMax = new Vector2(-2f, -2f);
            bgImage = bgObj.GetComponent<Image>();
            bgImage.color = ColorAvailableBg;
        }
        else if (bgImage == null)
        {
            bgImage = bgTrans.GetComponent<Image>();
        }

        // Ikona (32 px)
        Transform iconTrans = transform.Find("Icon");
        if (iconTrans == null)
        {
            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObj.transform.SetParent(transform, false);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(24f, 0f);
            iconRect.sizeDelta = new Vector2(IconSize, IconSize);
            iconImage = iconObj.GetComponent<Image>();
            iconImage.preserveAspect = true;
        }
        else if (iconImage == null)
        {
            iconImage = iconTrans.GetComponent<Image>();
        }

        // Tytuł węzła (12-14 pt)
        Transform titleTrans = transform.Find("Txt_Title");
        if (titleTrans == null)
        {
            GameObject titleObj = new GameObject("Txt_Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleObj.transform.SetParent(transform, false);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.5f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(46f, 0f);
            titleRect.offsetMax = new Vector2(-6f, -4f);
            txtTitle = titleObj.GetComponent<TextMeshProUGUI>();
            txtTitle.fontSize = FontSizeTitle;
            txtTitle.fontStyle = FontStyles.Bold;
            txtTitle.alignment = TextAlignmentOptions.MidlineLeft;
            txtTitle.overflowMode = TextOverflowModes.Ellipsis;
        }
        else if (txtTitle == null)
        {
            txtTitle = titleTrans.GetComponent<TextMeshProUGUI>();
        }

        // Koszt / Stan węzła (10-12 pt)
        Transform costTrans = transform.Find("Txt_Cost");
        if (costTrans == null)
        {
            GameObject costObj = new GameObject("Txt_Cost", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            costObj.transform.SetParent(transform, false);
            RectTransform costRect = costObj.GetComponent<RectTransform>();
            costRect.anchorMin = new Vector2(0f, 0f);
            costRect.anchorMax = new Vector2(1f, 0.5f);
            costRect.offsetMin = new Vector2(46f, 4f);
            costRect.offsetMax = new Vector2(-6f, 0f);
            txtCost = costObj.GetComponent<TextMeshProUGUI>();
            txtCost.fontSize = FontSizeCost;
            txtCost.alignment = TextAlignmentOptions.MidlineLeft;
        }
        else if (txtCost == null)
        {
            txtCost = costTrans.GetComponent<TextMeshProUGUI>();
        }
    }
}
