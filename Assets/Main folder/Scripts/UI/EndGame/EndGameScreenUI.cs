using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Główny kontroler ekranu końcowego gry (Zwycięstwo / Porażka) inspirowanego The Last Spell.
/// Subskrybuje HopeSessionManager.OnSessionEnded, pauzuje grę i prezentuje statystyki runu,
/// poziomy Scroll View z dynamicznie generowanymi kartami Nadziei i sekwencyjną animacją,
/// tooltipami, płynnie naliczanym stanem konta oraz przyciskami MENU i POWTÓRZ OBRONĘ.
/// </summary>
public class EndGameScreenUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Panel i Animacja Wejścia")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform animatedWindow;
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float cardRevealDelay = 0.10f;
    [SerializeField] private float hopeCountDuration = 0.85f;
    [SerializeField] private GameObject[] objectsToHideOnShow;

    [Header("Nagłówek Wyniku")]
    [SerializeField] private TextMeshProUGUI txtHeaderTitle;
    [SerializeField] private TextMeshProUGUI txtNightsSurvived;
    [SerializeField] private Image imgHeaderBanner;
    [SerializeField] private Color victoryHeaderColor = new Color(0.95f, 0.78f, 0.25f, 1f);
    [SerializeField] private Color defeatHeaderColor = new Color(0.85f, 0.22f, 0.22f, 1f);

    [Header("Górna Sekcja - Statystyki Runu")]
    [SerializeField] private TextMeshProUGUI txtEnemiesKilled;
    [SerializeField] private TextMeshProUGUI txtBossesKilled;
    [SerializeField] private TextMeshProUGUI txtTowersBuilt;
    [SerializeField] private TextMeshProUGUI txtProductionBuilt;
    [SerializeField] private TextMeshProUGUI txtExpeditionsCompleted;
    [SerializeField] private TextMeshProUGUI txtRunesCrafted;
    [SerializeField] private TextMeshProUGUI txtFinalPopulation;

    [Header("Środkowa Sekcja - Scroll View Kart Nadziei")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform cardsContent;
    [SerializeField] private EndGameHopeCardUI cardTemplate;
    [SerializeField] private Button btnPrevPage;
    [SerializeField] private Button btnNextPage;
    [SerializeField] private TextMeshProUGUI txtPageIndicator;
    [SerializeField] private GameObject objEmptyCardsInfo;
    [SerializeField] private float scrollViewportFraction = 0.75f;
    [SerializeField] private float scrollSmoothSpeed = 10f;

    [Header("Tooltip Karty Nadziei")]
    [SerializeField] private RectTransform pnlCardTooltip;
    [SerializeField] private TextMeshProUGUI txtTooltipTitle;
    [SerializeField] private TextMeshProUGUI txtTooltipRarity;
    [SerializeField] private TextMeshProUGUI txtTooltipBreakdown;

    [Header("Rzadkość Kart (4 poziomy)")]
    [SerializeField] private Sprite[] rarityTierSprites = new Sprite[4];
    [SerializeField] private Color[] rarityFallbackColors = new Color[]
    {
        new Color(0.85f, 0.85f, 0.85f, 1f), // Tier 0 - Common (szary)
        new Color(0.35f, 0.85f, 0.45f, 1f), // Tier 1 - Uncommon (zielony)
        new Color(0.30f, 0.65f, 0.95f, 1f), // Tier 2 - Rare (niebieski)
        new Color(0.96f, 0.78f, 0.28f, 1f)  // Tier 3 - Legendary (złoty)
    };

    [Header("Środkowa Sekcja (Prawa) - Podsumowanie Nadziei")]
    [SerializeField] private TextMeshProUGUI txtRunHopeEarned;
    [SerializeField] private TextMeshProUGUI txtMultiplierInfo;
    [SerializeField] private TextMeshProUGUI txtTotalAccountHope;

    [Header("Dolna Sekcja - Czas, Rekord i Nawigacja")]
    [SerializeField] private TextMeshProUGUI txtRunDuration;
    [SerializeField] private TextMeshProUGUI txtBestRecord;
    [SerializeField] private GameObject objNewRecordBadge;
    [SerializeField] private Button btnMenu;
    [SerializeField] private GameObject objMetaUpgradeBadge;
    [SerializeField] private Button btnRetry;
    [SerializeField] private string mainMenuSceneName = "MainMenu Demo scene";

    private TextMeshProUGUI txtStatsSectionTitle;
    private TextMeshProUGUI txtHopeSectionTitle;
    private TextMeshProUGUI txtSummarySectionTitle;
    private TextMeshProUGUI txtLblEnemiesKilled;
    private TextMeshProUGUI txtLblBossesKilled;
    private TextMeshProUGUI txtLblTowersBuilt;
    private TextMeshProUGUI txtLblProductionBuilt;
    private TextMeshProUGUI txtLblExpeditionsCompleted;
    private TextMeshProUGUI txtLblRunesCrafted;
    private TextMeshProUGUI txtLblFinalPopulation;
    private TextMeshProUGUI txtEmptyCardsInfo;
    private TextMeshProUGUI txtLblRunHope;
    private TextMeshProUGUI txtLblAccountHope;
    private TextMeshProUGUI txtNewRecordBadge;
    private TextMeshProUGUI txtMenuBtn;
    private TextMeshProUGUI txtMetaUpgradeBadge;
    private TextMeshProUGUI txtRetryBtn;

    private static readonly string[] RarityLabels = { "Ranga: Zwykła", "Ranga: Rzadka", "Ranga: Epicka", "Ranga: Legendarna" };

    private readonly List<HopeSessionManager.GroupedHopeCardData> groupedCards = new List<HopeSessionManager.GroupedHopeCardData>();
    private readonly List<EndGameHopeCardUI> spawnedCards = new List<EndGameHopeCardUI>();
    private Coroutine presentationCoroutine;
    private bool isSubscribed = false;
    private bool isPresenting = false;
    private bool isSmoothScrolling = false;
    private bool isApplyingSmoothScroll = false;
    private bool isScrollListenerBound = false;
    private float targetNormalizedX = 0f;
    private HopeSessionManager.HopeSessionSummary currentSummary;

    private void Awake()
    {
        AutoBindScrollReferences();
        AutoBindStaticLabels();
        RefreshStaticLocalization();

        if (btnPrevPage != null) btnPrevPage.onClick.AddListener(PrevPage);
        if (btnNextPage != null) btnNextPage.onClick.AddListener(NextPage);
        if (btnMenu != null) btnMenu.onClick.AddListener(OnMenuClicked);
        if (btnRetry != null) btnRetry.onClick.AddListener(OnRetryClicked);

        if (txtPageIndicator != null)
        {
            txtPageIndicator.gameObject.SetActive(false);
        }

        if (pnlCardTooltip != null)
        {
            pnlCardTooltip.gameObject.SetActive(false);
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void Start()
    {
        TrySubscribe();

        if (HopeSessionManager.Instance != null &&
            HopeSessionManager.Instance.isSessionEnded &&
            HopeSessionManager.Instance.lastSummary != null)
        {
            Show(HopeSessionManager.Instance.lastSummary);
        }
    }

    private void Update()
    {
        if (isPresenting && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
        {
            SkipPresentation();
        }

        if (isSmoothScrolling && scrollRect != null)
        {
            float current = scrollRect.horizontalNormalizedPosition;
            float next = Mathf.MoveTowards(current, targetNormalizedX, scrollSmoothSpeed * Time.unscaledDeltaTime);
            isApplyingSmoothScroll = true;
            scrollRect.horizontalNormalizedPosition = next;
            isApplyingSmoothScroll = false;

            if (Mathf.Abs(next - targetNormalizedX) <= 0.001f)
            {
                isApplyingSmoothScroll = true;
                scrollRect.horizontalNormalizedPosition = targetNormalizedX;
                isApplyingSmoothScroll = false;
                isSmoothScrolling = false;
            }
            UpdateScrollButtonsInteractable();
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (isScrollListenerBound && scrollRect != null)
        {
            scrollRect.onValueChanged.RemoveListener(OnScrollRectValueChanged);
            isScrollListenerBound = false;
        }
    }

    private void TrySubscribe()
    {
        if (isSubscribed || HopeSessionManager.Instance == null) return;
        HopeSessionManager.Instance.OnSessionEnded += Show;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || HopeSessionManager.Instance == null) return;
        HopeSessionManager.Instance.OnSessionEnded -= Show;
        isSubscribed = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isPresenting)
        {
            SkipPresentation();
        }
    }

    /// <summary>
    /// Wyświetla ekran końcowy z danymi podsumowania sesji i pauzuje czas gry.
    /// </summary>
    public void Show(HopeSessionManager.HopeSessionSummary summary)
    {
        if (summary == null) return;

        currentSummary = summary;
        Time.timeScale = 0f;

        CenterInfoPanelControllerUI.Instance?.HideAll();
        EnemyInfoUI.Instance?.CloseWindow();
        transform.SetAsLastSibling();

        if (objectsToHideOnShow != null)
        {
            for (int i = 0; i < objectsToHideOnShow.Length; i++)
            {
                if (objectsToHideOnShow[i] != null)
                {
                    objectsToHideOnShow[i].SetActive(false);
                }
            }
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        AutoBindScrollReferences();
        AutoBindStaticLabels();
        RefreshStaticLocalization();
        HideCardTooltip();
        PopulateHeader(summary);
        PopulateStats(summary);
        PopulateFooter(summary);
        UpdateMetaUpgradeHint(summary.totalAccountHope);

        groupedCards.Clear();
        groupedCards.AddRange(HopeSessionManager.BuildGroupedHopeCards(summary.records));

        if (presentationCoroutine != null)
        {
            StopCoroutine(presentationCoroutine);
            presentationCoroutine = null;
        }

        if (Application.isPlaying && gameObject.activeInHierarchy)
        {
            PopulateCards(prepareForReveal: true);
            SetHopeCounters(0, Mathf.Max(0, summary.totalAccountHope - summary.finalTotalHope), summary.multiplier);
            presentationCoroutine = StartCoroutine(PlayPresentationRoutine(summary));
        }
        else
        {
            PopulateCards(prepareForReveal: false);
            SetHopeCounters(summary.finalTotalHope, summary.totalAccountHope, summary.multiplier);
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            if (animatedWindow != null) animatedWindow.localScale = Vector3.one;
        }
    }

    public void SkipPresentation()
    {
        if (!isPresenting) return;
        isPresenting = false;

        if (presentationCoroutine != null)
        {
            StopCoroutine(presentationCoroutine);
            presentationCoroutine = null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (animatedWindow != null) animatedWindow.localScale = Vector3.one;

        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] != null && spawnedCards[i].gameObject.activeSelf)
            {
                spawnedCards[i].SnapToRevealed();
            }
        }

        if (currentSummary != null)
        {
            SetHopeCounters(currentSummary.finalTotalHope, currentSummary.totalAccountHope, currentSummary.multiplier);
        }
    }

    private IEnumerator PlayPresentationRoutine(HopeSessionManager.HopeSessionSummary summary)
    {
        isPresenting = true;

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (animatedWindow != null) animatedWindow.localScale = Vector3.one * 0.94f;

        // Odczekanie jednej klatki pozwala Canvasowi przeliczyć szerokość Viewportu i Contentu w ScrollRect
        yield return null;
        RebuildCardsLayout();
        UpdateScrollButtonsState();

        float fadeDur = Mathf.Max(0.01f, fadeDuration);
        float elapsed = 0f;
        while (elapsed < fadeDur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDur);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            if (canvasGroup != null) canvasGroup.alpha = eased;
            if (animatedWindow != null) animatedWindow.localScale = Vector3.Lerp(Vector3.one * 0.94f, Vector3.one, eased);
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 1f;
        if (animatedWindow != null) animatedWindow.localScale = Vector3.one;

        // Sekwencyjne odkrywanie wszystkich wygenerowanych kart w Scroll View
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] != null && spawnedCards[i].gameObject.activeSelf)
            {
                spawnedCards[i].PlayReveal(0.22f);
                yield return new WaitForSecondsRealtime(cardRevealDelay);
            }
        }

        // Płynne nabijanie licznika Nadziei
        int startAccount = Mathf.Max(0, summary.totalAccountHope - summary.finalTotalHope);
        float countDur = Mathf.Max(0.05f, hopeCountDuration);
        float countElapsed = 0f;

        while (countElapsed < countDur)
        {
            countElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(countElapsed / countDur);
            float eased = 1f - Mathf.Pow(1f - t, 2f);

            int runVal = Mathf.RoundToInt(Mathf.Lerp(0, summary.finalTotalHope, eased));
            int accountVal = Mathf.RoundToInt(Mathf.Lerp(startAccount, summary.totalAccountHope, eased));
            SetHopeCounters(runVal, accountVal, summary.multiplier);
            yield return null;
        }

        SetHopeCounters(summary.finalTotalHope, summary.totalAccountHope, summary.multiplier);
        isPresenting = false;
        presentationCoroutine = null;
    }

    private void PopulateHeader(HopeSessionManager.HopeSessionSummary summary)
    {
        Color headerColor = summary.isVictory ? victoryHeaderColor : defeatHeaderColor;

        if (txtHeaderTitle != null)
        {
            txtHeaderTitle.text = summary.isVictory
                ? LocalizationManager.Get("UI_ENDGAME_VICTORY", "ZWYCIĘSTWO!")
                : LocalizationManager.Get("UI_ENDGAME_DEFEAT", "TWIERDZA UPADŁA");
            txtHeaderTitle.color = Color.black;
        }

        if (imgHeaderBanner != null)
        {
            imgHeaderBanner.color = new Color(headerColor.r, headerColor.g, headerColor.b, imgHeaderBanner.color.a);
        }

        if (txtNightsSurvived != null)
        {
            txtNightsSurvived.text = LocalizationManager.GetFormat("UI_ENDGAME_NIGHTS_SURVIVED_FMT", summary.nightsSurvived);
        }
    }

    private void PopulateStats(HopeSessionManager.HopeSessionSummary summary)
    {
        SetStatText(txtEnemiesKilled, summary.enemiesKilled.ToString());
        SetStatText(txtBossesKilled, summary.bossesKilled.ToString());
        SetStatText(txtTowersBuilt, summary.towersBuilt.ToString());
        SetStatText(txtProductionBuilt, summary.productionBuildingsBuilt.ToString());
        SetStatText(txtExpeditionsCompleted, summary.expeditionsCompleted.ToString());
        SetStatText(txtRunesCrafted, summary.runesCrafted.ToString());
        SetStatText(txtFinalPopulation, summary.finalPopulation.ToString());
    }

    private void SetHopeCounters(int runHope, int totalAccountHope, float multiplier)
    {
        if (txtRunHopeEarned != null)
        {
            txtRunHopeEarned.text = $"+{runHope}";
        }

        if (txtMultiplierInfo != null)
        {
            bool showMult = Mathf.Abs(multiplier - 1f) > 0.01f;
            txtMultiplierInfo.gameObject.SetActive(showMult);
            if (showMult)
            {
                txtMultiplierInfo.text = LocalizationManager.GetFormat("UI_ENDGAME_MULTIPLIER_FMT", $"{multiplier:0.##}");
            }
        }

        if (txtTotalAccountHope != null)
        {
            txtTotalAccountHope.text = totalAccountHope.ToString();
        }
    }

    private void PopulateFooter(HopeSessionManager.HopeSessionSummary summary)
    {
        if (txtRunDuration != null)
        {
            int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(summary.runDurationSeconds));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            txtRunDuration.text = LocalizationManager.GetFormat("UI_ENDGAME_DURATION_FMT", $"{minutes:00}:{seconds:00}");
        }

        if (txtBestRecord != null)
        {
            txtBestRecord.text = LocalizationManager.GetFormat("UI_ENDGAME_RECORD_FMT", summary.bestNightsSurvived);
        }

        if (objNewRecordBadge != null)
        {
            objNewRecordBadge.SetActive(summary.isNewRecord);
        }
    }

    private void UpdateMetaUpgradeHint(int totalAccountHope)
    {
        if (objMetaUpgradeBadge == null) return;
        if (DemoBuildConfig.IsDemoV2Active)
        {
            objMetaUpgradeBadge.SetActive(false);
            return;
        }
        bool canAfford = HasAffordableMetaUpgrade(totalAccountHope);
        objMetaUpgradeBadge.SetActive(canAfford);
    }

    private bool HasAffordableMetaUpgrade(int totalAccountHope)
    {
        if (MetaUpgradeManager.Instance == null || MetaUpgradeManager.Instance.allUpgrades == null)
        {
            return false;
        }

        var save = SaveManager.Instance != null ? SaveManager.Instance.currentSaveData : null;
        var upgrades = MetaUpgradeManager.Instance.allUpgrades;

        for (int i = 0; i < upgrades.Count; i++)
        {
            var node = upgrades[i];
            if (node == null || node.isUnlocked) continue;
            if (save != null && save.unlockedUpgradeIDs != null && save.unlockedUpgradeIDs.Contains(node.id)) continue;

            bool prereqsMet = true;
            if (node.prerequisites != null)
            {
                for (int p = 0; p < node.prerequisites.Count; p++)
                {
                    var pre = node.prerequisites[p];
                    if (pre != null && !pre.isUnlocked && (save == null || save.unlockedUpgradeIDs == null || !save.unlockedUpgradeIDs.Contains(pre.id)))
                    {
                        prereqsMet = false;
                        break;
                    }
                }
            }

            if (!prereqsMet) continue;

            int invested = save != null ? save.GetInvestedHope(node.id) : 0;
            int remainingCost = Mathf.Max(0, node.cost - invested);
            if (totalAccountHope >= remainingCost)
            {
                return true;
            }
        }

        return false;
    }

    public void PrevPage()
    {
        ScrollBy(-1);
    }

    public void NextPage()
    {
        ScrollBy(1);
    }

    private void ScrollBy(int direction)
    {
        if (scrollRect == null || cardsContent == null || !HasHorizontalOverflow()) return;

        HideCardTooltip();

        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
        float viewportWidth = viewport != null ? viewport.rect.width : 0f;
        float scrollableWidth = Mathf.Max(1f, cardsContent.rect.width - viewportWidth);
        float stepNormalized = viewportWidth > 0f
            ? Mathf.Clamp01((viewportWidth * Mathf.Clamp01(scrollViewportFraction)) / scrollableWidth)
            : 0.25f;

        float basePos = isSmoothScrolling ? targetNormalizedX : scrollRect.horizontalNormalizedPosition;
        targetNormalizedX = Mathf.Clamp01(basePos + direction * stepNormalized);
        isSmoothScrolling = true;
    }

    private void AutoBindScrollReferences()
    {
        Transform searchRoot = panelRoot != null ? panelRoot.transform : transform;

        if (scrollRect == null && searchRoot != null)
        {
            scrollRect = searchRoot.GetComponentInChildren<ScrollRect>(true);
        }

        if (cardsContent == null && scrollRect != null)
        {
            cardsContent = scrollRect.content;
        }

        if (cardTemplate == null && cardsContent != null)
        {
            cardTemplate = cardsContent.GetComponentInChildren<EndGameHopeCardUI>(true);
        }

        if (cardsContent != null)
        {
            cardsContent.anchorMin = new Vector2(0f, 0f);
            cardsContent.anchorMax = new Vector2(0f, 1f);
            cardsContent.pivot = new Vector2(0f, 0.5f);

            var fitter = cardsContent.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        if (!isScrollListenerBound && scrollRect != null)
        {
            scrollRect.onValueChanged.AddListener(OnScrollRectValueChanged);
            isScrollListenerBound = true;
        }
    }

    private void AutoBindStaticLabels()
    {
        Transform searchRoot = panelRoot != null ? panelRoot.transform : transform;
        if (searchRoot == null) return;

        if (txtStatsSectionTitle == null) txtStatsSectionTitle = FindLabel(searchRoot, "Pnl_Window/Pnl_RunStatsSection/Txt_SectionTitle");
        if (txtLblEnemiesKilled == null) txtLblEnemiesKilled = FindLabel(searchRoot, "Pnl_Window/Pnl_RunStatsSection/Pnl_StatsGrid/Stat_Enemies/Lbl");
        if (txtLblBossesKilled == null) txtLblBossesKilled = FindLabel(searchRoot, "Pnl_Window/Pnl_RunStatsSection/Pnl_StatsGrid/Stat_Bosses/Lbl");
        if (txtLblTowersBuilt == null) txtLblTowersBuilt = FindLabel(searchRoot, "Pnl_Window/Pnl_RunStatsSection/Pnl_StatsGrid/Stat_Towers/Lbl");
        if (txtLblProductionBuilt == null) txtLblProductionBuilt = FindLabel(searchRoot, "Pnl_Window/Pnl_RunStatsSection/Pnl_StatsGrid/Stat_Production/Lbl");
        if (txtLblExpeditionsCompleted == null) txtLblExpeditionsCompleted = FindLabel(searchRoot, "Pnl_Window/Pnl_RunStatsSection/Pnl_StatsGrid/Stat_Expeditions/Lbl");
        if (txtLblRunesCrafted == null) txtLblRunesCrafted = FindLabel(searchRoot, "Pnl_Window/Pnl_RunStatsSection/Pnl_StatsGrid/Stat_Runes/Lbl");
        if (txtLblFinalPopulation == null) txtLblFinalPopulation = FindLabel(searchRoot, "Pnl_Window/Pnl_RunStatsSection/Pnl_StatsGrid/Stat_Population/Lbl");

        if (txtHopeSectionTitle == null) txtHopeSectionTitle = FindLabel(searchRoot, "Pnl_Window/Pnl_Middle/Pnl_HopeCarouselSection/Txt_SectionTitle");
        if (txtSummarySectionTitle == null) txtSummarySectionTitle = FindLabel(searchRoot, "Pnl_Window/Pnl_Middle/Pnl_HopeSummarySection/Txt_SectionTitle");
        if (txtLblRunHope == null) txtLblRunHope = FindLabel(searchRoot, "Pnl_Window/Pnl_Middle/Pnl_HopeSummarySection/Pnl_SummaryContent/Lbl_RunHope");
        if (txtLblAccountHope == null) txtLblAccountHope = FindLabel(searchRoot, "Pnl_Window/Pnl_Middle/Pnl_HopeSummarySection/Pnl_SummaryContent/Pnl_TotalAccountHope/Lbl_AccountHope");

        if (txtEmptyCardsInfo == null && objEmptyCardsInfo != null)
            txtEmptyCardsInfo = objEmptyCardsInfo.GetComponent<TextMeshProUGUI>() ?? objEmptyCardsInfo.GetComponentInChildren<TextMeshProUGUI>(true);
        if (txtNewRecordBadge == null && objNewRecordBadge != null)
            txtNewRecordBadge = objNewRecordBadge.GetComponentInChildren<TextMeshProUGUI>(true);
        if (txtMenuBtn == null && btnMenu != null)
            txtMenuBtn = btnMenu.transform.Find("Txt")?.GetComponent<TextMeshProUGUI>() ?? btnMenu.GetComponentInChildren<TextMeshProUGUI>(true);
        if (txtMetaUpgradeBadge == null && objMetaUpgradeBadge != null)
            txtMetaUpgradeBadge = objMetaUpgradeBadge.GetComponentInChildren<TextMeshProUGUI>(true);
        if (txtRetryBtn == null && btnRetry != null)
            txtRetryBtn = btnRetry.transform.Find("Txt")?.GetComponent<TextMeshProUGUI>() ?? btnRetry.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void RefreshStaticLocalization()
    {
        SetLocalizedLabel(txtStatsSectionTitle, "UI_ENDGAME_SECTION_STATS", "STATYSTYKI OBRONY");
        SetLocalizedLabel(txtLblEnemiesKilled, "UI_ENDGAME_STAT_ENEMIES", "Zabici wrogowie");
        SetLocalizedLabel(txtLblBossesKilled, "UI_ENDGAME_STAT_BOSSES", "Zabici bossowie");
        SetLocalizedLabel(txtLblTowersBuilt, "UI_ENDGAME_STAT_TOWERS", "Zbudowane wieże");
        SetLocalizedLabel(txtLblProductionBuilt, "UI_ENDGAME_STAT_PRODUCTION", "Budynki prod.");
        SetLocalizedLabel(txtLblExpeditionsCompleted, "UI_ENDGAME_STAT_EXPEDITIONS", "Ekspedycje");
        SetLocalizedLabel(txtLblRunesCrafted, "UI_ENDGAME_STAT_RUNES", "Wytworzone runy");
        SetLocalizedLabel(txtLblFinalPopulation, "UI_ENDGAME_STAT_POPULATION", "Populacja");

        SetLocalizedLabel(txtHopeSectionTitle, "UI_ENDGAME_SECTION_HOPE", "ZDOBYTA NADZIEJA");
        SetLocalizedLabel(txtEmptyCardsInfo, "UI_ENDGAME_EMPTY_CARDS", "Brak zdobytych osiągnięć w tym runie");
        SetLocalizedLabel(txtSummarySectionTitle, "UI_ENDGAME_SECTION_SUMMARY", "PODSUMOWANIE");
        SetLocalizedLabel(txtLblRunHope, "UI_ENDGAME_SUMMARY_RUN", "Z tego runu:");
        SetLocalizedLabel(txtLblAccountHope, "UI_ENDGAME_SUMMARY_TOTAL", "Łączna Nadzieja");

        SetLocalizedLabel(txtNewRecordBadge, "UI_ENDGAME_NEW_RECORD", "NOWY REKORD!");
        SetLocalizedLabel(txtMenuBtn, "UI_ENDGAME_MENU_BTN", "MENU");
        SetLocalizedLabel(txtMetaUpgradeBadge, "UI_ENDGAME_META_BADGE", "ULEPSZENIA!");
        SetLocalizedLabel(txtRetryBtn, "UI_ENDGAME_RETRY_BTN", "POWTÓRZ OBRONĘ");
    }

    private static TextMeshProUGUI FindLabel(Transform root, string relativePath)
    {
        Transform t = root.Find(relativePath);
        return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
    }

    private static void SetLocalizedLabel(TextMeshProUGUI target, string key, string fallback)
    {
        if (target != null)
        {
            target.text = LocalizationManager.Get(key, fallback);
        }
    }

    private void OnScrollRectValueChanged(Vector2 _)
    {
        if (!isApplyingSmoothScroll)
        {
            isSmoothScrolling = false;
        }
        UpdateScrollButtonsInteractable();
    }

    private void PopulateCards(bool prepareForReveal)
    {
        ClearSpawnedCards();

        if (txtPageIndicator != null)
        {
            txtPageIndicator.gameObject.SetActive(false);
        }

        if (cardTemplate != null && cardsContent != null)
        {
            for (int i = 0; i < groupedCards.Count; i++)
            {
                var cardData = groupedCards[i];
                EndGameHopeCardUI card = Instantiate(cardTemplate, cardsContent);
                card.name = $"Card_Hope_{i}";
                Sprite raritySprite = GetRaritySprite(cardData.rarityTier);
                Color fallbackColor = GetRarityFallbackColor(cardData.rarityTier);
                card.Setup(cardData, raritySprite, fallbackColor, ShowCardTooltip, HideCardTooltip);
                if (prepareForReveal)
                {
                    card.PrepareForReveal();
                }
                spawnedCards.Add(card);
            }
        }

        if (objEmptyCardsInfo != null)
        {
            objEmptyCardsInfo.SetActive(groupedCards.Count == 0);
        }

        RebuildCardsLayout();

        if (scrollRect != null)
        {
            isSmoothScrolling = false;
            targetNormalizedX = 0f;
            isApplyingSmoothScroll = true;
            scrollRect.horizontalNormalizedPosition = 0f;
            isApplyingSmoothScroll = false;
        }

        UpdateScrollButtonsState();
    }

    private void ClearSpawnedCards()
    {
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(spawnedCards[i].gameObject);
                }
                else
                {
                    DestroyImmediate(spawnedCards[i].gameObject);
                }
            }
        }
        spawnedCards.Clear();

        if (cardTemplate != null && cardTemplate.gameObject.scene.IsValid())
        {
            cardTemplate.gameObject.SetActive(false);
        }

        // Ukrywa przykładowe karty pozostawione w Content na scenie (np. Card_Hope (1))
        if (cardsContent != null)
        {
            for (int i = 0; i < cardsContent.childCount; i++)
            {
                cardsContent.GetChild(i).gameObject.SetActive(false);
            }
        }
    }

    private void RebuildCardsLayout()
    {
        Canvas.ForceUpdateCanvases();
        if (cardsContent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(cardsContent);
        }
        if (scrollRect != null)
        {
            RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
            if (viewport != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
            }
        }
    }

    private bool HasHorizontalOverflow()
    {
        if (scrollRect == null || cardsContent == null || groupedCards.Count == 0) return false;
        RectTransform viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
        if (viewport == null) return false;
        return cardsContent.rect.width > viewport.rect.width + 1f;
    }

    private void UpdateScrollButtonsState()
    {
        bool hasOverflow = HasHorizontalOverflow();

        if (btnPrevPage != null)
        {
            btnPrevPage.gameObject.SetActive(hasOverflow);
        }

        if (btnNextPage != null)
        {
            btnNextPage.gameObject.SetActive(hasOverflow);
        }

        UpdateScrollButtonsInteractable();
    }

    private void UpdateScrollButtonsInteractable()
    {
        if (scrollRect == null) return;

        float pos = scrollRect.horizontalNormalizedPosition;
        if (btnPrevPage != null && btnPrevPage.gameObject.activeSelf)
        {
            btnPrevPage.interactable = pos > 0.005f;
        }
        if (btnNextPage != null && btnNextPage.gameObject.activeSelf)
        {
            btnNextPage.interactable = pos < 0.995f;
        }
    }

    private void ShowCardTooltip(HopeSessionManager.GroupedHopeCardData data, RectTransform cardRt)
    {
        if (pnlCardTooltip == null) return;

        pnlCardTooltip.gameObject.SetActive(true);

        if (txtTooltipTitle != null)
        {
            txtTooltipTitle.text = $"{data.title} (+{data.totalAmount})";
        }

        if (txtTooltipRarity != null)
        {
            int tier = Mathf.Clamp(data.rarityTier, 0, RarityLabels.Length - 1);
            txtTooltipRarity.text = LocalizationManager.Get($"UI_ENDGAME_RARITY_{tier}", RarityLabels[tier]);
            txtTooltipRarity.color = GetRarityFallbackColor(tier);
        }

        if (txtTooltipBreakdown != null)
        {
            if (data.breakdownLines != null && data.breakdownLines.Count > 0)
            {
                var sb = new StringBuilder();
                int maxLines = Mathf.Min(data.breakdownLines.Count, 8);
                for (int i = 0; i < maxLines; i++)
                {
                    if (i > 0) sb.Append('\n');
                    sb.Append("• ").Append(data.breakdownLines[i]);
                }
                if (data.breakdownLines.Count > maxLines)
                {
                    sb.Append(LocalizationManager.GetFormat("UI_ENDGAME_TOOLTIP_MORE_FMT", data.breakdownLines.Count - maxLines));
                }
                txtTooltipBreakdown.text = sb.ToString();
            }
            else
            {
                txtTooltipBreakdown.text = $"• {data.title} (+{data.totalAmount})";
            }
        }

        if (cardRt != null)
        {
            Vector3[] corners = new Vector3[4];
            cardRt.GetWorldCorners(corners);
            // Pozycja nad środkiem karty
            Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;
            pnlCardTooltip.position = topCenter + new Vector3(0f, 12f, 0f);
        }
    }

    private void HideCardTooltip()
    {
        if (pnlCardTooltip != null)
        {
            pnlCardTooltip.gameObject.SetActive(false);
        }
    }

    private Sprite GetRaritySprite(int tier)
    {
        if (rarityTierSprites == null || rarityTierSprites.Length == 0) return null;
        int clamped = Mathf.Clamp(tier, 0, rarityTierSprites.Length - 1);
        return rarityTierSprites[clamped];
    }

    private Color GetRarityFallbackColor(int tier)
    {
        if (rarityFallbackColors == null || rarityFallbackColors.Length == 0) return Color.white;
        int clamped = Mathf.Clamp(tier, 0, rarityFallbackColors.Length - 1);
        return rarityFallbackColors[clamped];
    }

    private void SetStatText(TextMeshProUGUI target, string value)
    {
        if (target != null) target.text = value;
    }

    private void OnMenuClicked()
    {
        Time.timeScale = 1f;
        if (LoadingScreenManager.Instance != null)
        {
            LoadingScreenManager.LoadScene(mainMenuSceneName);
        }
        else
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    private void OnRetryClicked()
    {
        Time.timeScale = 1f;
        string currentScene = SceneManager.GetActiveScene().name;
        LoadingScreenManager.LoadScene(currentScene);
    }

#if UNITY_EDITOR
    [ContextMenu("Test Preview - Victory")]
    private void TestPreviewVictory()
    {
        Show(CreateMockSummary(true));
    }

    [ContextMenu("Test Preview - Defeat")]
    private void TestPreviewDefeat()
    {
        Show(CreateMockSummary(false));
    }

    private HopeSessionManager.HopeSessionSummary CreateMockSummary(bool victory)
    {
        var mockRecords = new List<HopeSessionManager.HopeRecord>
        {
            new HopeSessionManager.HopeRecord { reason = "Przetrwano noc 5", amount = 100, milestoneType = MilestoneType.NightsSurvived, groupKey = "NightsSurvived", groupTitle = "Survivor" },
            new HopeSessionManager.HopeRecord { reason = "Przetrwano noc 10", amount = 200, milestoneType = MilestoneType.NightsSurvived, groupKey = "NightsSurvived", groupTitle = "Survivor" },
            new HopeSessionManager.HopeRecord { reason = "Zabij 100 wrogów", amount = 50, milestoneType = MilestoneType.EnemiesKilled, groupKey = "EnemiesKilled", groupTitle = "Łowca" },
            new HopeSessionManager.HopeRecord { reason = "Zabij 500 wrogów", amount = 100, milestoneType = MilestoneType.EnemiesKilled, groupKey = "EnemiesKilled", groupTitle = "Łowca" },
            new HopeSessionManager.HopeRecord { reason = "Zabij 1000 wrogów", amount = 150, milestoneType = MilestoneType.EnemiesKilled, groupKey = "EnemiesKilled", groupTitle = "Łowca" },
            new HopeSessionManager.HopeRecord { reason = "Ukończ 5 ekspedycji", amount = 80, milestoneType = MilestoneType.ExpeditionsCompleted, groupKey = "ExpeditionsCompleted", groupTitle = "Odkrywca" },
            new HopeSessionManager.HopeRecord { reason = "Wykuj 1. runę", amount = 40, milestoneType = MilestoneType.RuneCraftingDone, groupKey = "RuneCraftingDone", groupTitle = "Kowal Run" },
            new HopeSessionManager.HopeRecord { reason = "Wykuj 3. runę", amount = 60, milestoneType = MilestoneType.RuneCraftingDone, groupKey = "RuneCraftingDone", groupTitle = "Kowal Run" },
            new HopeSessionManager.HopeRecord { reason = "Wykuj 5. runę", amount = 90, milestoneType = MilestoneType.RuneCraftingDone, groupKey = "RuneCraftingDone", groupTitle = "Kowal Run" },
            new HopeSessionManager.HopeRecord { reason = "Wykuj 7. runę", amount = 120, milestoneType = MilestoneType.RuneCraftingDone, groupKey = "RuneCraftingDone", groupTitle = "Kowal Run" },
            new HopeSessionManager.HopeRecord { reason = "Zbuduj 15 wież", amount = 75, milestoneType = MilestoneType.TotalTowers, groupKey = "TotalTowers", groupTitle = "Architekt" }
        };

        return new HopeSessionManager.HopeSessionSummary
        {
            isVictory = victory,
            nightsSurvived = victory ? 50 : 18,
            currentNight = victory ? 50 : 19,
            wavePoints = 300,
            milestonePoints = 450,
            multiplier = 1.25f,
            rawTotalHope = 750,
            finalTotalHope = 938,
            totalAccountHope = 5145,
            runDurationSeconds = 1122f,
            bestNightsSurvived = victory ? 50 : 24,
            isNewRecord = victory,
            enemiesKilled = 1420,
            bossesKilled = 3,
            towersBuilt = 16,
            productionBuildingsBuilt = 9,
            expeditionsCompleted = 5,
            runesCrafted = 7,
            finalPopulation = 34,
            records = mockRecords
        };
    }
#endif
}
