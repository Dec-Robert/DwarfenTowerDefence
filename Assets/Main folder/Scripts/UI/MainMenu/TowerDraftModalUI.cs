using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal wyboru nowej wieży obronnej (Tower Draft) w Menu Głównym i meta-progresji.
/// Wyświetla zablokowane wieże obronne, umożliwiając graczowi trwały wybór jednej z nich.
/// Standardy interfejsu zgodne ze specyfikacją GEMINI.md:
/// - 20 pt: Główny nagłówek okna oraz nazwy wież
/// - 16 pt: Tytuły sekcji, przyciski funkcyjne ("WYBIERZ WIEŻĘ", "ZAMKNIJ")
/// - 12-14 pt: Opisy fabularne, statystyki bojowe wież
/// - 10 pt: Teksty pomocnicze i instrukcje
/// - 32 px: Standardowe ikony
/// - 24 px: Małe ikony statystyk
/// </summary>
public class TowerDraftModalUI : MonoBehaviour
{
    public static TowerDraftModalUI Instance { get; private set; }

    [Header("Referencje UI (Opcjonalne - budowane proceduralnie jeśli brak)")]
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private Transform cardsContainer;
    [SerializeField] private TextMeshProUGUI txtTitle;
    [SerializeField] private TextMeshProUGUI txtSubtitle;
    [SerializeField] private Button btnClose;

    [Header("Typografia GEMINI.md")]
    public const float FontSizeHeader = 20f;
    public const float FontSizeButton = 16f;
    public const float FontSizeContent = 13f;
    public const float FontSizeHelper = 10f;

    [Header("Wymiary Ikon GEMINI.md")]
    public const float IconSizeStandard = 32f;
    public const float IconSizeSmall = 24f;

    private readonly List<TowerData> currentCandidates = new List<TowerData>();
    private Action onDraftComplete;
    private bool isOpen = false;

    public bool IsOpen => isOpen;
    public IReadOnlyList<TowerData> CurrentCandidates => currentCandidates;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Pobiera istniejącą instancję lub tworzy nową na bieżącym Canvasie.
    /// </summary>
    public static TowerDraftModalUI GetOrCreateInstance()
    {
        if (Instance != null) return Instance;

        Instance = FindFirstObjectByType<TowerDraftModalUI>();
        if (Instance != null) return Instance;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas_Dynamic", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        GameObject modalObj = new GameObject("Pnl_TowerDraftModal", typeof(RectTransform));
        modalObj.transform.SetParent(canvas.transform, false);
        Instance = modalObj.AddComponent<TowerDraftModalUI>();
        return Instance;
    }

    /// <summary>
    /// Filtruje pulę wież i pozostawia wyłącznie te, które nie zostały jeszcze odblokowane.
    /// </summary>
    public static List<TowerData> FilterCandidates(List<TowerData> allTowers, List<string> unlockedIDs)
    {
        List<TowerData> candidates = new List<TowerData>();
        if (allTowers == null) return candidates;

        HashSet<string> unlockedSet = (unlockedIDs != null) ? new HashSet<string>(unlockedIDs) : new HashSet<string>();

        foreach (var tower in allTowers)
        {
            if (ReferenceEquals(tower, null)) continue;

            string tName = null;
            try { tName = tower.name; } catch { }
            bool isUnlocked = (!string.IsNullOrEmpty(tName) && unlockedSet.Contains(tName)) ||
                              (!string.IsNullOrEmpty(tower.buildingName) && unlockedSet.Contains(tower.buildingName));

            if (!isUnlocked)
            {
                candidates.Add(tower);
            }
        }

        return candidates;
    }

    /// <summary>
    /// Otwiera modal draftu wież, prezentując zablokowane wieże do wyboru.
    /// </summary>
    public void ShowDraft(List<TowerData> allTowers, Action onComplete = null)
    {
        onDraftComplete = onComplete;

        List<string> unlockedIDs = (SaveManager.Instance != null && SaveManager.Instance.currentSaveData != null)
            ? SaveManager.Instance.currentSaveData.unlockedTowerIDs
            : new List<string>();

        List<TowerData> candidates = FilterCandidates(allTowers, unlockedIDs);
        currentCandidates.Clear();
        currentCandidates.AddRange(candidates);

        BuildOrRefreshModal();
        isOpen = true;
        if (modalRoot != null)
        {
            modalRoot.SetActive(true);
        }
    }

    /// <summary>
    /// Zamyka modal draftu.
    /// </summary>
    public void CloseModal()
    {
        isOpen = false;
        if (modalRoot != null)
        {
            modalRoot.SetActive(false);
        }
    }

    /// <summary>
    /// Wybiera daną wieżę, odblokowuje ją w SaveManagerze, trwale zapisuje grę i powiadamia odbiorców.
    /// </summary>
    public void SelectTower(TowerData tower)
    {
        if (ReferenceEquals(tower, null)) return;

        Debug.Log($"[TowerDraftModalUI] Wybrano wieżę: {tower.name} ({tower.buildingName})");

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.UnlockTower(tower.name);
            if (!string.IsNullOrEmpty(tower.buildingName) && tower.buildingName != tower.name)
            {
                // Dodaj także alias jeśli różny
                if (!SaveManager.Instance.currentSaveData.unlockedTowerIDs.Contains(tower.buildingName))
                {
                    SaveManager.Instance.currentSaveData.unlockedTowerIDs.Add(tower.buildingName);
                }
            }
            tower.isUnlocked = true;
            SaveManager.Instance.SaveGame();
        }
        else
        {
            tower.isUnlocked = true;
        }

        Action callback = onDraftComplete;
        onDraftComplete = null;
        CloseModal();
        callback?.Invoke();
    }

    /// <summary>
    /// Upewnia się, że proceduralna hierarchia UI modalu została utworzona (dostępne publicznie m.in. dla testów).
    /// </summary>
    public void EnsureHierarchyBuilt()
    {
        if (modalRoot == null)
        {
            BuildProceduralModalHierarchy();
        }
    }

    private void BuildOrRefreshModal()
    {
        EnsureHierarchyBuilt();
        PopulateDraftCards();
    }

    private void BuildProceduralModalHierarchy()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        TMP_FontAsset fontAsset = FindFontAsset();

        // 1. Root / Ciemny Overlay
        modalRoot = new GameObject("ModalOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        modalRoot.transform.SetParent(transform, false);

        RectTransform rootRect = modalRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image rootImg = modalRoot.GetComponent<Image>();
        rootImg.color = new Color(0.04f, 0.05f, 0.07f, 0.92f); // GEMINI.md tło overlay

        // 2. Okno Główne Modalu (Okno Draftu)
        GameObject windowObj = new GameObject("Window_Draft", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        windowObj.transform.SetParent(modalRoot.transform, false);

        RectTransform windowRect = windowObj.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(1080f, 660f);

        Image windowImg = windowObj.GetComponent<Image>();
        windowImg.color = new Color(0.11f, 0.12f, 0.14f, 0.98f); // Elegancki ciemny grafit

        // 3. Pasek Nagłówka (Header)
        GameObject headerObj = new GameObject("Header", typeof(RectTransform));
        headerObj.transform.SetParent(windowObj.transform, false);
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -15f);
        headerRect.sizeDelta = new Vector2(-40f, 60f);

        // Tytuł okna (20 pt)
        GameObject titleObj = new GameObject("Txt_Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(headerObj.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = new Vector2(-50f, 0f);

        txtTitle = titleObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) txtTitle.font = fontAsset;
        txtTitle.text = "WYBÓR WIEŻY OBRONNEJ • DRAFT ROZWOJU";
        txtTitle.fontSize = FontSizeHeader; // 20 pt
        txtTitle.fontStyle = FontStyles.Bold;
        txtTitle.color = new Color(0.95f, 0.85f, 0.45f); // Złoty nagłówek
        txtTitle.alignment = TextAlignmentOptions.MidlineLeft;

        // Podtytuł / Tekst pomocniczy (10 pt)
        GameObject subObj = new GameObject("Txt_Subtitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        subObj.transform.SetParent(headerObj.transform, false);
        RectTransform subRect = subObj.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 0f);
        subRect.anchorMax = new Vector2(1f, 0.5f);
        subRect.offsetMin = Vector2.zero;
        subRect.offsetMax = new Vector2(-50f, 0f);

        txtSubtitle = subObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) txtSubtitle.font = fontAsset;
        txtSubtitle.text = "Wybierz nową wieżę do odblokowania. Wybór jest trwały i udostępni wieżę do budowy w Bastionie.";
        txtSubtitle.fontSize = FontSizeHelper; // 10 pt
        txtSubtitle.color = new Color(0.72f, 0.74f, 0.78f);
        txtSubtitle.alignment = TextAlignmentOptions.MidlineLeft;

        // Przycisk Zamknięcia ("✕")
        GameObject btnCloseObj = CreateButton(headerObj.transform, "Btn_Close", "✕", fontAsset, FontSizeButton, new Vector2(40f, 40f));
        RectTransform closeRect = btnCloseObj.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0.7f);
        closeRect.anchorMax = new Vector2(1f, 0.7f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = Vector2.zero;
        btnClose = btnCloseObj.GetComponent<Button>();
        btnClose.onClick.AddListener(CloseModal);

        // 4. Kontener na Karty Wież (Cards Container)
        GameObject containerObj = new GameObject("CardsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        containerObj.transform.SetParent(windowObj.transform, false);
        cardsContainer = containerObj.transform;

        RectTransform containerRect = containerObj.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0f, 0f);
        containerRect.anchorMax = new Vector2(1f, 1f);
        containerRect.offsetMin = new Vector2(25f, 25f);
        containerRect.offsetMax = new Vector2(-25f, -85f);

        HorizontalLayoutGroup hlg = containerObj.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 15f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
    }

    private void PopulateDraftCards()
    {
        if (cardsContainer == null) return;

        // Usuń stare karty
        for (int i = cardsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(cardsContainer.GetChild(i).gameObject);
        }

        TMP_FontAsset fontAsset = FindFontAsset();

        if (currentCandidates.Count == 0)
        {
            // Wszystkie wieże są już odblokowane
            GameObject emptyObj = new GameObject("Txt_AllUnlocked", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            emptyObj.transform.SetParent(cardsContainer, false);
            TextMeshProUGUI emptyTxt = emptyObj.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) emptyTxt.font = fontAsset;
            emptyTxt.text = "Wszystkie wieże obronne zostały już odblokowane w Bastionie!";
            emptyTxt.fontSize = FontSizeButton; // 16 pt
            emptyTxt.alignment = TextAlignmentOptions.Center;
            emptyTxt.color = new Color(0.85f, 0.85f, 0.85f);
            return;
        }

        // Generuj karty dla każdej zablokowanej wieży
        foreach (var tower in currentCandidates)
        {
            CreateDraftCard(tower, fontAsset);
        }
    }

    private void CreateDraftCard(TowerData tower, TMP_FontAsset fontAsset)
    {
        // Karta wieży
        GameObject cardObj = new GameObject($"Card_{tower.name}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cardObj.transform.SetParent(cardsContainer, false);

        Image cardBg = cardObj.GetComponent<Image>();
        cardBg.color = new Color(0.16f, 0.17f, 0.20f, 0.95f); // Kolor tła karty

        // Układ wertykalny karty
        VerticalLayoutGroup vlg = cardObj.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(16, 16, 18, 18);
        vlg.spacing = 10f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // 1. Ikona Wieży (32 px standardowa ikona wg GEMINI.md)
        GameObject iconContainer = new GameObject("IconContainer", typeof(RectTransform), typeof(LayoutElement));
        iconContainer.transform.SetParent(cardObj.transform, false);
        LayoutElement iconLe = iconContainer.GetComponent<LayoutElement>();
        iconLe.preferredHeight = IconSizeStandard; // 32 px
        iconLe.minHeight = IconSizeStandard;

        GameObject iconObj = new GameObject("Img_TowerIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObj.transform.SetParent(iconContainer.transform, false);
        RectTransform iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(IconSizeStandard, IconSizeStandard); // 32x32 px
        Image iconImg = iconObj.GetComponent<Image>();
        if (tower.icon != null)
        {
            iconImg.sprite = tower.icon;
            iconImg.preserveAspect = true;
        }
        else
        {
            iconImg.color = new Color(0.4f, 0.45f, 0.5f, 0.5f);
        }

        // 2. Nazwa Wieży (20 pt nagłówek wg GEMINI.md)
        GameObject nameObj = new GameObject("Txt_TowerName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        nameObj.transform.SetParent(cardObj.transform, false);
        LayoutElement nameLe = nameObj.GetComponent<LayoutElement>();
        nameLe.preferredHeight = 44f;

        TextMeshProUGUI nameTxt = nameObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) nameTxt.font = fontAsset;
        nameTxt.text = !string.IsNullOrEmpty(tower.buildingName) ? tower.buildingName : tower.name;
        nameTxt.fontSize = FontSizeHeader; // 20 pt
        nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.color = new Color(0.95f, 0.85f, 0.45f);
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.textWrappingMode = TextWrappingModes.Normal;

        // 3. Opis Fabularny / Lore (12-14 pt wg GEMINI.md -> 13 pt)
        GameObject descObj = new GameObject("Txt_Description", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        descObj.transform.SetParent(cardObj.transform, false);
        LayoutElement descLe = descObj.GetComponent<LayoutElement>();
        descLe.preferredHeight = 110f;

        TextMeshProUGUI descTxt = descObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) descTxt.font = fontAsset;
        descTxt.text = !string.IsNullOrEmpty(tower.description) ? tower.description : "Machina obronna gotowa wesprzeć mury Bastionu.";
        descTxt.fontSize = FontSizeContent; // 13 pt
        descTxt.color = new Color(0.82f, 0.84f, 0.88f);
        descTxt.alignment = TextAlignmentOptions.TopLeft;
        descTxt.textWrappingMode = TextWrappingModes.Normal;
        descTxt.overflowMode = TextOverflowModes.Ellipsis;

        // 4. Panel Statystyk Bojowych (12-14 pt opis + 24 px małe ikony wg GEMINI.md)
        GameObject statsObj = new GameObject("Pnl_CombatStats", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        statsObj.transform.SetParent(cardObj.transform, false);
        LayoutElement statsLe = statsObj.GetComponent<LayoutElement>();
        statsLe.preferredHeight = 150f;

        VerticalLayoutGroup statsVlg = statsObj.GetComponent<VerticalLayoutGroup>();
        statsVlg.spacing = 4f;
        statsVlg.childControlWidth = true;
        statsVlg.childControlHeight = false;

        CreateStatRow(statsObj.transform, "Obrażenia:", $"{tower.baseDamage}", fontAsset);
        CreateStatRow(statsObj.transform, "Zasięg:", $"{tower.baseRange}", fontAsset);
        CreateStatRow(statsObj.transform, "Szybkostrzelność:", $"{tower.fireRate}/s", fontAsset);
        CreateStatRow(statsObj.transform, "Penetracja Pancerza:", $"{tower.armorPenetration}%", fontAsset);
        CreateStatRow(statsObj.transform, "Penetracja Magiczna:", $"{tower.magicPenetration}%", fontAsset);

        // 5. Przycisk Wyboru Wieży (16 pt wg GEMINI.md)
        GameObject btnSelectObj = CreateButton(cardObj.transform, "Btn_SelectTower", "WYBIERZ WIEŻĘ", fontAsset, FontSizeButton, new Vector2(0f, 44f));
        LayoutElement btnLe = btnSelectObj.AddComponent<LayoutElement>();
        btnLe.preferredHeight = 44f;
        btnLe.minHeight = 44f;

        Button btnSelect = btnSelectObj.GetComponent<Button>();
        btnSelect.onClick.AddListener(() => SelectTower(tower));
    }

    private void CreateStatRow(Transform parent, string label, string value, TMP_FontAsset fontAsset)
    {
        GameObject rowObj = new GameObject("StatRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        rowObj.transform.SetParent(parent, false);
        LayoutElement rowLe = rowObj.GetComponent<LayoutElement>();
        rowLe.preferredHeight = 24f; // 24 px wysokość wiersza ze statystyką

        HorizontalLayoutGroup hlg = rowObj.GetComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;

        // Mała ikona statystyki (24 px wg GEMINI.md)
        GameObject iconObj = new GameObject("Icon_Stat", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconObj.transform.SetParent(rowObj.transform, false);
        LayoutElement iconLe = iconObj.GetComponent<LayoutElement>();
        iconLe.preferredWidth = IconSizeSmall; // 24 px
        iconLe.preferredHeight = IconSizeSmall; // 24 px
        Image iconImg = iconObj.GetComponent<Image>();
        iconImg.color = new Color(0.85f, 0.75f, 0.35f, 0.8f);

        // Etykieta statystyki (13 pt)
        GameObject labelObj = new GameObject("Txt_Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(rowObj.transform, false);
        TextMeshProUGUI labelTxt = labelObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) labelTxt.font = fontAsset;
        labelTxt.text = label;
        labelTxt.fontSize = FontSizeContent; // 13 pt
        labelTxt.color = new Color(0.7f, 0.72f, 0.75f);
        labelTxt.alignment = TextAlignmentOptions.MidlineLeft;

        // Wartość statystyki (13 pt)
        GameObject valObj = new GameObject("Txt_Value", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        valObj.transform.SetParent(rowObj.transform, false);
        TextMeshProUGUI valTxt = valObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) valTxt.font = fontAsset;
        valTxt.text = value;
        valTxt.fontSize = FontSizeContent; // 13 pt
        valTxt.fontStyle = FontStyles.Bold;
        valTxt.color = Color.white;
        valTxt.alignment = TextAlignmentOptions.MidlineRight;
    }

    private GameObject CreateButton(Transform parent, string name, string label, TMP_FontAsset fontAsset, float fontSize, Vector2 size)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.sizeDelta = size;

        Image img = btnObj.GetComponent<Image>();
        img.color = new Color(0.75f, 0.55f, 0.18f); // Złocisto-brązowy przycisk

        Button btn = btnObj.GetComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.75f, 0.55f, 0.18f);
        colors.highlightedColor = new Color(0.88f, 0.68f, 0.25f);
        colors.pressedColor = new Color(0.6f, 0.42f, 0.12f);
        btn.colors = colors;

        GameObject textObj = new GameObject("Txt_BtnLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TextMeshProUGUI txt = textObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) txt.font = fontAsset;
        txt.text = label;
        txt.fontSize = fontSize; // GEMINI.md: 16 pt przyciski
        txt.fontStyle = FontStyles.Bold;
        txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.Center;

        return btnObj;
    }

    private TMP_FontAsset FindFontAsset()
    {
        TextMeshProUGUI existing = FindFirstObjectByType<TextMeshProUGUI>();
        if (existing != null && existing.font != null)
        {
            return existing.font;
        }
        return TMP_Settings.defaultFontAsset;
    }
}
