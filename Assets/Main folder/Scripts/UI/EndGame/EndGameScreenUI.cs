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
/// karuzelę zgrupowanych kart Nadziei z sekwencyjną animacją i tooltipami,
/// płynnie naliczany stan konta oraz przyciski MENU i POWTÓRZ OBRONĘ.
/// </summary>
public class EndGameScreenUI : MonoBehaviour, IPointerClickHandler
{
    [Serializable]
    public struct MilestoneIconMapping
    {
        public MilestoneType type;
        public Sprite icon;
    }

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
    [SerializeField] private TextMeshProUGUI txtHeaderSubtitle;
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

    [Header("Środkowa Sekcja - Karuzela Kart Nadziei")]
    [SerializeField] private EndGameHopeCardUI[] cardSlots;
    [SerializeField] private Button btnPrevPage;
    [SerializeField] private Button btnNextPage;
    [SerializeField] private TextMeshProUGUI txtPageIndicator;
    [SerializeField] private GameObject objEmptyCardsInfo;

    [Header("Tooltip Karty Nadziei")]
    [SerializeField] private RectTransform pnlCardTooltip;
    [SerializeField] private TextMeshProUGUI txtTooltipTitle;
    [SerializeField] private TextMeshProUGUI txtTooltipRarity;
    [SerializeField] private TextMeshProUGUI txtTooltipBreakdown;

    [Header("Ikony Kategorii i Rzadkości (4 poziomy)")]
    [SerializeField] private Sprite defaultCategoryIcon;
    [SerializeField] private List<MilestoneIconMapping> milestoneIcons = new List<MilestoneIconMapping>();
    [SerializeField] private Sprite[] rarityTierSprites = new Sprite[4];
    [SerializeField] private Color[] rarityFallbackColors = new Color[]
    {
        new Color(0.85f, 0.85f, 0.85f, 1f), // Tier 0 - Zwykła
        new Color(0.30f, 0.85f, 0.40f, 1f), // Tier 1 - Rzadka
        new Color(0.90f, 0.25f, 0.25f, 1f), // Tier 2 - Epicka
        new Color(0.70f, 0.30f, 0.95f, 1f)  // Tier 3 - Legendarna
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

    private static readonly string[] RarityLabels = { "Ranga: Zwykła", "Ranga: Rzadka", "Ranga: Epicka", "Ranga: Legendarna" };

    private readonly List<HopeSessionManager.GroupedHopeCardData> groupedCards = new List<HopeSessionManager.GroupedHopeCardData>();
    private int currentPage = 0;
    private Coroutine presentationCoroutine;
    private bool isSubscribed = false;
    private bool isPresenting = false;
    private HopeSessionManager.HopeSessionSummary currentSummary;

    private void Awake()
    {
        if (btnPrevPage != null) btnPrevPage.onClick.AddListener(PrevPage);
        if (btnNextPage != null) btnNextPage.onClick.AddListener(NextPage);
        if (btnMenu != null) btnMenu.onClick.AddListener(OnMenuClicked);
        if (btnRetry != null) btnRetry.onClick.AddListener(OnRetryClicked);

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
    }

    private void OnDestroy()
    {
        Unsubscribe();
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

        HideCardTooltip();
        PopulateHeader(summary);
        PopulateStats(summary);
        PopulateFooter(summary);
        UpdateMetaUpgradeHint(summary.totalAccountHope);

        groupedCards.Clear();
        groupedCards.AddRange(HopeSessionManager.BuildGroupedHopeCards(summary.records));
        currentPage = 0;

        if (presentationCoroutine != null)
        {
            StopCoroutine(presentationCoroutine);
            presentationCoroutine = null;
        }

        if (Application.isPlaying && gameObject.activeInHierarchy)
        {
            RefreshCarousel(prepareForReveal: true);
            SetHopeCounters(0, Mathf.Max(0, summary.totalAccountHope - summary.finalTotalHope), summary.multiplier);
            presentationCoroutine = StartCoroutine(PlayPresentationRoutine(summary));
        }
        else
        {
            RefreshCarousel(prepareForReveal: false);
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

        if (cardSlots != null)
        {
            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (cardSlots[i] != null && cardSlots[i].gameObject.activeSelf)
                {
                    cardSlots[i].SnapToRevealed();
                }
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

        // Sekwencyjne odkrywanie kart na bieżącej stronie
        if (cardSlots != null)
        {
            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (cardSlots[i] != null && cardSlots[i].gameObject.activeSelf)
                {
                    cardSlots[i].PlayReveal(0.22f);
                    yield return new WaitForSecondsRealtime(cardRevealDelay);
                }
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
            txtHeaderTitle.text = summary.isVictory ? "ZWYCIĘSTWO!" : "TWIERDZA UPADŁA";
            txtHeaderTitle.color = headerColor;
        }

        if (txtHeaderSubtitle != null)
        {
            int nightContext = summary.currentNight > 0 ? summary.currentNight : Mathf.Max(1, summary.nightsSurvived + 1);
            txtHeaderSubtitle.text = summary.isVictory
                ? "Twierdza przetrwała wszystkie fale!"
                : $"Brama została zniszczona podczas {nightContext}. nocy";
        }

        if (imgHeaderBanner != null)
        {
            imgHeaderBanner.color = new Color(headerColor.r, headerColor.g, headerColor.b, imgHeaderBanner.color.a);
        }

        if (txtNightsSurvived != null)
        {
            txtNightsSurvived.text = $"Przetrwano nocy: {summary.nightsSurvived}";
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
                txtMultiplierInfo.text = $"Mnożnik: x{multiplier:0.##}";
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
            txtRunDuration.text = $"Czas gry: {minutes:00}:{seconds:00}";
        }

        if (txtBestRecord != null)
        {
            txtBestRecord.text = $"Rekord: {summary.bestNightsSurvived} nocy";
        }

        if (objNewRecordBadge != null)
        {
            objNewRecordBadge.SetActive(summary.isNewRecord);
        }
    }

    private void UpdateMetaUpgradeHint(int totalAccountHope)
    {
        if (objMetaUpgradeBadge == null) return;
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
        if (currentPage <= 0) return;
        currentPage--;
        HideCardTooltip();
        RefreshCarousel(prepareForReveal: false);
    }

    public void NextPage()
    {
        int totalPages = GetTotalPages();
        if (currentPage >= totalPages - 1) return;
        currentPage++;
        HideCardTooltip();
        RefreshCarousel(prepareForReveal: false);
    }

    private int GetCardsPerPage()
    {
        return cardSlots != null && cardSlots.Length > 0 ? cardSlots.Length : 4;
    }

    private int GetTotalPages()
    {
        if (groupedCards.Count == 0) return 1;
        return Mathf.CeilToInt((float)groupedCards.Count / GetCardsPerPage());
    }

    private void RefreshCarousel(bool prepareForReveal)
    {
        int perPage = GetCardsPerPage();
        int totalPages = GetTotalPages();
        currentPage = Mathf.Clamp(currentPage, 0, totalPages - 1);

        int startIndex = currentPage * perPage;

        if (cardSlots != null)
        {
            for (int i = 0; i < cardSlots.Length; i++)
            {
                if (cardSlots[i] == null) continue;

                int dataIndex = startIndex + i;
                if (dataIndex < groupedCards.Count)
                {
                    var cardData = groupedCards[dataIndex];
                    Sprite icon = GetCategoryIcon(cardData.milestoneType);
                    Sprite raritySprite = GetRaritySprite(cardData.rarityTier);
                    Color fallbackColor = GetRarityFallbackColor(cardData.rarityTier);
                    cardSlots[i].Setup(cardData, icon, raritySprite, fallbackColor, ShowCardTooltip, HideCardTooltip);
                    if (prepareForReveal)
                    {
                        cardSlots[i].PrepareForReveal();
                    }
                }
                else
                {
                    cardSlots[i].Hide();
                }
            }
        }

        if (objEmptyCardsInfo != null)
        {
            objEmptyCardsInfo.SetActive(groupedCards.Count == 0);
        }

        if (btnPrevPage != null)
        {
            btnPrevPage.interactable = currentPage > 0;
        }

        if (btnNextPage != null)
        {
            btnNextPage.interactable = currentPage < totalPages - 1;
        }

        if (txtPageIndicator != null)
        {
            txtPageIndicator.text = groupedCards.Count > 0 ? $"{currentPage + 1} / {totalPages}" : "0 / 0";
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
            txtTooltipRarity.text = RarityLabels[tier];
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
                    sb.Append($"\n... (+{data.breakdownLines.Count - maxLines} więcej)");
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

    private Sprite GetCategoryIcon(MilestoneType? type)
    {
        if (type.HasValue && milestoneIcons != null)
        {
            for (int i = 0; i < milestoneIcons.Count; i++)
            {
                if (milestoneIcons[i].type == type.Value && milestoneIcons[i].icon != null)
                {
                    return milestoneIcons[i].icon;
                }
            }
        }
        return defaultCategoryIcon;
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
