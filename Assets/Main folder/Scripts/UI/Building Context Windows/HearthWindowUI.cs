using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HearthWindowUI : MonoBehaviour
{
    [Serializable]
    public class HearthLevelCardRefs
    {
        public GameObject rootObject;
        public Button button;
        public Image background;
        public Outline outline;
        public GameObject selectedTopBar;
        public Image selectedTopBarImage;
        public TextMeshProUGUI txtLevelIndex;
        public GameObject coalCostObject;
        public SingleResourceUI coalCostSingleUI;
        public TextMeshProUGUI txtCoalCostAmount;
        public GameObject badgeFreeObject;
        public Image imgIconBg;
        public Image imgLevelIcon;
        public TextMeshProUGUI txtLevelTitle;
        public Image badgeTagImage;
        public TextMeshProUGUI txtTag;
    }

    [Serializable]
    public class HearthEffectItemRefs
    {
        public GameObject rootObject;
        public Image imgEffectIcon;
        public TextMeshProUGUI txtEffectTitle;
        public TextMeshProUGUI txtEffectValue;
        public TextMeshProUGUI txtEffectDesc;
    }

    [Header("Główne Przyciski Okna")]
    public Button backgroundBlockButton;
    public Button btnClose;
    public Button btnCancel;
    public Button btnIgniteAndStart;
    public Image imgIgniteButtonBg;
    public TextMeshProUGUI txtIgniteLabel;

    [Header("Pasek Informacji i Zasobów (Top Info Bar)")]
    public Image imgFreeWorkersBadge;
    public TextMeshProUGUI txtFreeWorkersAmount;
    public SingleResourceUI coalStockSingleResourceUI;
    public TextMeshProUGUI txtCoalStockFallback;

    [Header("Podgląd Paleniska (Preview Box)")]
    public Image imgHearthPreview;
    public TextMeshProUGUI txtPreviewLevelName;
    public TextMeshProUGUI txtPreviewLevelCost;

    [Header("Karty 5 Poziomów Paleniska")]
    public HearthLevelCardRefs[] levelCards = new HearthLevelCardRefs[5];

    [Header("Kolumny Efektów (Noc / Następny Dzień)")]
    public HearthEffectItemRefs[] nightEffectItems = new HearthEffectItemRefs[5];
    public HearthEffectItemRefs[] dayEffectItems = new HearthEffectItemRefs[2];

    [Header("Stopka Podsumowania (Footer)")]
    public TextMeshProUGUI txtSummaryHeader;
    public TextMeshProUGUI txtSummaryDetails;

    private int currentSelectedLevel = 3;
    private bool isPreviewOnly;
    private bool listenersBound;

    private GameObject idleWorkersModalRoot;
    private TextMeshProUGUI txtIdleModalTitle;
    private TextMeshProUGUI txtIdleModalMessage;
    private TextMeshProUGUI txtIdleModalCancel;
    private TextMeshProUGUI txtIdleModalConfirm;

    private static readonly Color ColorCardNormalBg = new Color(0.16f, 0.14f, 0.12f, 0.98f);
    private static readonly Color ColorCardSelectedBg = new Color(0.24f, 0.19f, 0.13f, 1.00f);
    private static readonly Color ColorBorderSubtle = new Color(0.32f, 0.27f, 0.22f, 0.70f);
    private static readonly Color ColorGoldHighlight = new Color(0.95f, 0.72f, 0.22f, 1.00f);

    private static readonly Color ColorPositive = new Color(0.35f, 0.85f, 0.45f, 1.00f);
    private static readonly Color ColorNegative = new Color(0.92f, 0.32f, 0.30f, 1.00f);
    private static readonly Color ColorNeutral = new Color(0.75f, 0.78f, 0.82f, 1.00f);
    private static readonly Color ColorAmberGold = new Color(0.95f, 0.65f, 0.18f, 1.00f);

    private static readonly Color ColorIgniteActive = new Color(0.82f, 0.36f, 0.12f, 1.00f);
    private static readonly Color ColorIgniteDisabled = new Color(0.32f, 0.26f, 0.22f, 0.90f);

    private void Awake()
    {
        AutoBindReferences();
        BindButtons();
    }

    private void OnEnable()
    {
        BeaconEntity.EnsureTimeSubscription();

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged -= HandleResourceChanged;
            ResourceManager.Instance.OnResourceChanged += HandleResourceChanged;
        }

        if (WorkforceManager.Instance != null)
        {
            WorkforceManager.Instance.OnWorkforceStatsChanged -= HandleWorkforceStatsChanged;
            WorkforceManager.Instance.OnWorkforceStatsChanged += HandleWorkforceStatsChanged;
        }

        BeaconEntity.OnHearthStateChanged -= RefreshAllUI;
        BeaconEntity.OnHearthStateChanged += RefreshAllUI;
        LocalizationManager.OnLanguageChanged -= RefreshAllUI;
        LocalizationManager.OnLanguageChanged += RefreshAllUI;

        ApplyBackdropAndOrder();
        HideEmptyPlaceholderIcons();
        RefreshAllUI();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= RefreshAllUI;
        BeaconEntity.OnHearthStateChanged -= RefreshAllUI;

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged -= HandleResourceChanged;
        }

        if (WorkforceManager.Instance != null)
        {
            WorkforceManager.Instance.OnWorkforceStatsChanged -= HandleWorkforceStatsChanged;
        }
    }

    public void Setup(bool previewOnly = false)
    {
        AutoBindReferences();
        BindButtons();

        isPreviewOnly = previewOnly;
        currentSelectedLevel = Mathf.Clamp(BeaconEntity.SelectedDraftLevel, 1, 5);
        if (!BeaconEntity.IsLevelUnlocked(currentSelectedLevel))
        {
            currentSelectedLevel = 3;
            BeaconEntity.SelectedDraftLevel = 3;
        }

        if (idleWorkersModalRoot != null) idleWorkersModalRoot.SetActive(false);
        ApplyBackdropAndOrder();
        HideEmptyPlaceholderIcons();
        RefreshAllUI();
        ForceRebuildImmediate();
    }

    private void ApplyBackdropAndOrder()
    {
        transform.SetAsLastSibling();
        if (backgroundBlockButton != null)
        {
            var bgImg = backgroundBlockButton.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.color = new Color(0.03f, 0.04f, 0.06f, 0.94f);
            }
        }
    }

    private void HideEmptyPlaceholderIcons()
    {
        var pnlHearth = transform.Find("Pnl_Hearth");
        if (pnlHearth == null) return;

        HideEmptyChildIcons(pnlHearth.Find("Pnl_EffectsColumnsRow/Sec_NightEffects/Pnl_Header"));
        HideEmptyChildIcons(pnlHearth.Find("Pnl_EffectsColumnsRow/Sec_DayEffects/Pnl_Header"));
        if (btnIgniteAndStart != null)
        {
            HideEmptyChildIcons(btnIgniteAndStart.transform);
        }
    }

    private static void HideEmptyChildIcons(Transform parent)
    {
        if (parent == null) return;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.GetComponent<Button>() != null) continue;
            var img = child.GetComponent<Image>();
            if (img != null && img.sprite == null && img != parent.GetComponent<Image>())
            {
                img.gameObject.SetActive(false);
            }
        }
    }

    public void ForceRebuildImmediate()
    {
        var rect = transform as RectTransform;
        if (rect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        var pnlHearth = transform.Find("Pnl_Hearth") as RectTransform;
        if (pnlHearth != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(pnlHearth);
        }
    }

    private void BindButtons()
    {
        if (listenersBound) return;

        if (backgroundBlockButton != null)
        {
            backgroundBlockButton.onClick.RemoveAllListeners();
            backgroundBlockButton.onClick.AddListener(CloseWindow);
        }

        if (btnClose != null)
        {
            btnClose.onClick.RemoveAllListeners();
            btnClose.onClick.AddListener(CloseWindow);
        }

        if (btnCancel != null)
        {
            btnCancel.onClick.RemoveAllListeners();
            btnCancel.onClick.AddListener(CloseWindow);
        }

        if (btnIgniteAndStart != null)
        {
            btnIgniteAndStart.onClick.RemoveAllListeners();
            btnIgniteAndStart.onClick.AddListener(OnIgniteClicked);
        }

        if (levelCards != null)
        {
            for (int i = 0; i < levelCards.Length; i++)
            {
                int capturedLevel = i + 1;
                if (levelCards[i] != null && levelCards[i].button != null)
                {
                    levelCards[i].button.onClick.RemoveAllListeners();
                    levelCards[i].button.onClick.AddListener(() => SelectLevel(capturedLevel));
                }
            }
        }

        listenersBound = true;
    }

    public void SelectLevel(int levelIndex)
    {
        int clamped = Mathf.Clamp(levelIndex, 1, 5);
        if (!BeaconEntity.IsLevelUnlocked(clamped)) return;

        currentSelectedLevel = clamped;
        BeaconEntity.SelectedDraftLevel = currentSelectedLevel;
        RefreshAllUI();
    }

    private void OnIgniteClicked()
    {
        if (isPreviewOnly) return;

        if (TimePhaseManager.Instance != null && TimePhaseManager.Instance.CurrentTimePhase != TimePhases.Dusk)
        {
            return;
        }

        int idleWorkers = GetTotalIdleWorkers();
        if (idleWorkers > 0)
        {
            ShowIdleWorkersModal(idleWorkers);
            return;
        }

        ExecuteIgniteAndStartNight();
    }

    private void ExecuteIgniteAndStartNight()
    {
        if (idleWorkersModalRoot != null) idleWorkersModalRoot.SetActive(false);

        if (BeaconEntity.TryIgniteAndStartNight(currentSelectedLevel))
        {
            CloseWindow();
        }
        else
        {
            RefreshAllUI();
        }
    }

    private int GetTotalIdleWorkers()
    {
        int totalIdleWorkers = 0;
        if (WorkforceManager.Instance != null)
        {
            foreach (Race race in Enum.GetValues(typeof(Race)))
            {
                totalIdleWorkers += WorkforceManager.Instance.GetIdleCount(race);
            }
        }
        return totalIdleWorkers;
    }

    public void CloseWindow()
    {
        if (CenterInfoPanelControllerUI.Instance != null)
        {
            CenterInfoPanelControllerUI.Instance.HideAll();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void HandleResourceChanged(ResourceType type, int newAmount)
    {
        if (type == ResourceType.Coal)
        {
            RefreshAllUI();
        }
    }

    private void HandleWorkforceStatsChanged(Race race, WorkforceManager.WorkforceStats stats)
    {
        RefreshFreeWorkersUI();
    }

    public void RefreshAllUI()
    {
        RefreshStaticLabels();
        HearthConfigSO config = BeaconEntity.GetConfig();
        if (config == null) return;

        int currentCoal = GetCurrentCoalAmount();
        HearthLevelData selectedData = config.GetLevelData(currentSelectedLevel);

        RefreshFreeWorkersUI();
        RefreshCoalStockUI(currentCoal);
        RefreshLevelCardsUI(config, currentCoal);
        RefreshPreviewBoxUI(selectedData);
        RefreshEffectsUI(selectedData);
        RefreshFooterUI(selectedData, currentCoal);
    }

    private void RefreshStaticLabels()
    {
        var pnlHearth = transform.Find("Pnl_Hearth");
        if (pnlHearth == null) return;

        SetHearthTMP(pnlHearth, "Pnl_TopBar/Txt_WindowTitle", "UI_HEARTH_WINDOW_TITLE", "PŁOMIEŃ NADZIEI");
        SetHearthTMP(pnlHearth, "Pnl_InfoAndCoalBar/Txt_IntroDesc", "UI_HEARTH_INTRO_DESC", "Dzień dobiega końca.");
        SetHearthTMP(pnlHearth, "Pnl_InfoAndCoalBar/Pnl_FreeWorkers/Txt_FreeWorkersLabel", "UI_HEARTH_FREE_WORKERS_LABEL", "WOLNI PRACOWNICY:");
        SetHearthTMP(pnlHearth, "Pnl_InfoAndCoalBar/Pnl_CoalStock/Txt_CoalLabel", "UI_HEARTH_COAL_STOCK_LABEL", "ZAPAS WĘGLA:");
        SetHearthTMP(pnlHearth, "Pnl_EffectsColumnsRow/Sec_DayEffects/Pnl_Header/Txt_SectionTitle", "UI_HEARTH_DAY_EFFECTS_TITLE", "EFEKTY NASTĘPNEGO DNIA (PRODUKCJA)");
        SetHearthTMP(pnlHearth, "Pnl_EffectsColumnsRow/Sec_NightEffects/Pnl_Header/Txt_SectionTitle", "UI_HEARTH_NIGHT_EFFECTS_TITLE", "EFEKTY TEJ NOCY (OBRONA & ŁUPY)");
        SetHearthTMP(pnlHearth, "Pnl_FooterBar/Btn_Cancel/Txt_CancelLabel", "UI_BTN_CANCEL", "ANULUJ");
    }

    private static void SetHearthTMP(Transform root, string path, string key, string fallback)
    {
        var tmp = root.Find(path)?.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = LocalizationManager.Get(key, fallback);
    }

    private int GetCurrentCoalAmount()
    {
        if (ResourceManager.Instance != null)
        {
            return ResourceManager.Instance.GetResourceAmount(ResourceType.Coal);
        }
        return 0;
    }

    private void RefreshFreeWorkersUI()
    {
        int totalIdleWorkers = 0;
        if (WorkforceManager.Instance != null)
        {
            foreach (Race race in Enum.GetValues(typeof(Race)))
            {
                totalIdleWorkers += WorkforceManager.Instance.GetIdleCount(race);
            }
        }

        if (txtFreeWorkersAmount != null)
        {
            txtFreeWorkersAmount.text = totalIdleWorkers.ToString();
            txtFreeWorkersAmount.color = totalIdleWorkers > 0 ? ColorAmberGold : ColorPositive;
        }

        if (imgFreeWorkersBadge != null)
        {
            imgFreeWorkersBadge.color = totalIdleWorkers > 0
                ? new Color(0.28f, 0.20f, 0.10f, 1f)
                : new Color(0.14f, 0.24f, 0.16f, 1f);
        }
    }

    private void RefreshCoalStockUI(int currentCoal)
    {
        if (coalStockSingleResourceUI != null)
        {
            coalStockSingleResourceUI.Setup(ResourceType.Coal, currentCoal);
        }
        else if (txtCoalStockFallback != null)
        {
            txtCoalStockFallback.text = currentCoal.ToString();
        }
    }

    private void RefreshLevelCardsUI(HearthConfigSO config, int currentCoal)
    {
        if (levelCards == null) return;

        for (int i = 0; i < levelCards.Length; i++)
        {
            var card = levelCards[i];
            if (card == null || card.rootObject == null) continue;

            HearthLevelData data = config.GetLevelData(i + 1);
            bool isLevelUnlocked = BeaconEntity.IsLevelUnlocked(data.levelIndex);
            int coalCost = BeaconEntity.GetCoalCost(data);
            bool isSelected = (data.levelIndex == currentSelectedLevel) && isLevelUnlocked;
            bool canAfford = currentCoal >= coalCost;

            if (card.button != null)
            {
                card.button.interactable = isLevelUnlocked;
            }

            if (card.background != null)
            {
                card.background.color = !isLevelUnlocked
                    ? new Color(0.10f, 0.09f, 0.09f, 0.65f)
                    : (isSelected ? ColorCardSelectedBg : ColorCardNormalBg);
            }

            if (card.outline != null)
            {
                Color levelOutlineColor = isLevelUnlocked ? GetLevelOutlineColor(data.levelIndex) : ColorNeutral;
                levelOutlineColor.a = !isLevelUnlocked ? 0.25f : (isSelected ? 1f : 0.55f);
                card.outline.effectColor = levelOutlineColor;
                card.outline.effectDistance = isSelected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            }

            if (card.selectedTopBar != null)
            {
                card.selectedTopBar.SetActive(isSelected);
            }

            if (card.selectedTopBarImage != null)
            {
                card.selectedTopBarImage.color = data.accentColor;
            }

            if (card.txtLevelIndex != null)
            {
                card.txtLevelIndex.text = LocalizationManager.GetFormat("UI_HEARTH_LEVEL_NUM", data.levelIndex);
                card.txtLevelIndex.color = isSelected ? ColorGoldHighlight : new Color(0.68f, 0.65f, 0.60f, 1f);
            }

            if (card.coalCostObject != null) card.coalCostObject.SetActive(true);
            if (card.badgeFreeObject != null) card.badgeFreeObject.SetActive(false);

            if (card.coalCostSingleUI != null)
            {
                card.coalCostSingleUI.Setup(ResourceType.Coal, coalCost);
            }

            if (card.txtCoalCostAmount != null)
            {
                card.txtCoalCostAmount.text = coalCost.ToString();
                card.txtCoalCostAmount.color = canAfford ? Color.white : ColorNegative;
            }

            bool hasIcon = data.levelIcon != null || (card.imgLevelIcon != null && card.imgLevelIcon.sprite != null);
            if (card.imgIconBg != null)
            {
                card.imgIconBg.gameObject.SetActive(hasIcon);
                if (hasIcon)
                {
                    card.imgIconBg.color = new Color(data.accentColor.r * 0.35f, data.accentColor.g * 0.35f, data.accentColor.b * 0.35f, 0.95f);
                }
            }

            if (card.imgLevelIcon != null)
            {
                if (data.levelIcon != null) card.imgLevelIcon.sprite = data.levelIcon;
                card.imgLevelIcon.gameObject.SetActive(hasIcon);
                card.imgLevelIcon.color = data.accentColor;
            }

            if (card.txtLevelTitle != null)
            {
                card.txtLevelTitle.text = data.DisplayTitle;
            }

            if (card.badgeTagImage != null)
            {
                card.badgeTagImage.gameObject.SetActive(false);
            }

            if (card.txtTag != null)
            {
                card.txtTag.gameObject.SetActive(false);
            }
        }
    }

    private static Color GetLevelOutlineColor(int levelIndex)
    {
        return levelIndex switch
        {
            1 => new Color(0.141f, 0.063f, 0.200f, 1f), // #241033
            2 => new Color(0.431f, 0.118f, 0.349f, 1f), // #6E1E59
            3 => new Color(0.710f, 0.169f, 0.263f, 1f), // #B52B43
            4 => new Color(0.890f, 0.278f, 0.141f, 1f), // #E34724
            5 => new Color(1.000f, 0.478f, 0.000f, 1f), // #FF7A00
            _ => new Color(0.710f, 0.169f, 0.263f, 1f)
        };
    }

    private void RefreshPreviewBoxUI(HearthLevelData selectedData)
    {
        if (imgHearthPreview != null)
        {
            if (selectedData.levelIcon != null)
            {
                imgHearthPreview.sprite = selectedData.levelIcon;
            }
            imgHearthPreview.gameObject.SetActive(imgHearthPreview.sprite != null);
            imgHearthPreview.color = selectedData.accentColor;
        }

        if (txtPreviewLevelName != null)
        {
            txtPreviewLevelName.text = LocalizationManager.GetFormat("UI_HEARTH_LEVEL_NAME_FMT", selectedData.levelIndex, selectedData.DisplayTitle.ToUpperInvariant());
            txtPreviewLevelName.color = selectedData.accentColor;
        }

        if (txtPreviewLevelCost != null)
        {
            txtPreviewLevelCost.text = string.Empty;
            txtPreviewLevelCost.gameObject.SetActive(false);
        }
    }

    private void RefreshEffectsUI(HearthLevelData selectedData)
    {
        // 1. Szybkostrzelność wież obronnych (FireRate) - ukryj jeśli 0%
        bool hasFireRate = !Mathf.Approximately(selectedData.towerFireRateBonus, 0f);
        UpdateEffectItem(
            GetNightEffect(0),
            LocalizationManager.Get("UI_HEARTH_EFF_FIRE_RATE", "Szybkostrzelność wież"),
            FormatPercentDelta(selectedData.towerFireRateBonus),
            GetPlayerBeneficialColor(selectedData.towerFireRateBonus),
            GetTowerFireRateDescription(selectedData.towerFireRateBonus),
            hasFireRate
        );

        // 2. Zasięg wież obronnych (Range) - ukryj jeśli 0%
        bool hasRange = !Mathf.Approximately(selectedData.towerRangeBonus, 0f);
        UpdateEffectItem(
            GetNightEffect(1),
            LocalizationManager.Get("UI_HEARTH_EFF_RANGE", "Zasięg wież"),
            FormatPercentDelta(selectedData.towerRangeBonus),
            GetPlayerBeneficialColor(selectedData.towerRangeBonus),
            GetTowerRangeDescription(selectedData.towerRangeBonus),
            hasRange
        );

        // 3. Siła i prędkość wrogów (Enemy HP & Speed) - ukryj jeśli 0%
        bool hasEnemyStats = !Mathf.Approximately(selectedData.enemyStatsBonus, 0f);
        UpdateEffectItem(
            GetNightEffect(2),
            LocalizationManager.Get("UI_HEARTH_EFF_ENEMY_STATS", "Zdrowie i prędkość wrogów w nocy"),
            FormatPercentDelta(selectedData.enemyStatsBonus),
            GetPlayerBeneficialColor(-selectedData.enemyStatsBonus),
            GetEnemyStatsDescription(selectedData.enemyStatsBonus),
            hasEnemyStats
        );

        // 4. Szansa na drop i rzadkość run (RuneManager) - ukryj jeśli 0%
        bool hasRuneDrop = !Mathf.Approximately(selectedData.runeDropAndRarityBonus, 0f);
        UpdateEffectItem(
            GetNightEffect(3),
            LocalizationManager.Get("UI_HEARTH_EFF_RUNE_DROP", "Szansa na drop i rzadkość Run"),
            FormatPercentDelta(selectedData.runeDropAndRarityBonus),
            GetPlayerBeneficialColor(selectedData.runeDropAndRarityBonus),
            GetRuneDropDescription(selectedData.runeDropAndRarityBonus),
            hasRuneDrop
        );

        // 5. Szanse na drop Etheru (opisowa etykieta bez wartości %) - zawsze widoczne
        Color etherColor = GetEtherTierColor(selectedData.guaranteedEtherPercent);
        string etherLabel = string.IsNullOrEmpty(selectedData.etherChanceTierLabel) ? LocalizationManager.Get("hearth_ether_tier_średnie", "ŚREDNIE") : selectedData.DisplayEtherTierLabel.ToUpperInvariant();
        UpdateEffectItem(
            GetNightEffect(4),
            LocalizationManager.Get("UI_HEARTH_EFF_ETHER_DROP", "Szanse na kryształy etheru"),
            etherLabel,
            etherColor,
            string.Empty,
            true
        );

        // Dzień 1: Produkcja wszystkich budynków następnego dnia
        float effectiveDayProduction = BeaconEntity.GetEffectiveNextDayProductionBonus(selectedData);
        bool hasDayProduction = !Mathf.Approximately(effectiveDayProduction, 0f);
        if (hasDayProduction)
        {
            UpdateEffectItem(
                GetDayEffect(0),
                LocalizationManager.Get("UI_HEARTH_EFF_PRODUCTION", "Produkcja wszystkich budynków"),
                FormatPercentDelta(effectiveDayProduction),
                GetPlayerBeneficialColor(effectiveDayProduction),
                GetDayProductionDescription(effectiveDayProduction),
                true
            );
        }
        else
        {
            UpdateEffectItem(
                GetDayEffect(0),
                LocalizationManager.Get("UI_HEARTH_NO_EFFECTS", "Brak dodatkowych efektów"),
                string.Empty,
                ColorNeutral,
                string.Empty,
                true
            );
        }

        // Dzień 2: Zużycie węgla tej nocy - usunięte z UI na życzenie użytkownika (pokazane przy poziomie)
        var dayCoalItem = GetDayEffect(1);
        if (dayCoalItem != null && dayCoalItem.rootObject != null)
        {
            dayCoalItem.rootObject.SetActive(false);
        }

        RebuildEffectsLayout();
    }

    private void RebuildEffectsLayout()
    {
        var pnlHearth = transform.Find("Pnl_Hearth");
        if (pnlHearth == null) return;

        var nightList = pnlHearth.Find("Pnl_EffectsColumnsRow/Sec_NightEffects/Pnl_EffectsList") as RectTransform;
        if (nightList != null) LayoutRebuilder.ForceRebuildLayoutImmediate(nightList);

        var dayList = pnlHearth.Find("Pnl_EffectsColumnsRow/Sec_DayEffects/Pnl_EffectsList") as RectTransform;
        if (dayList != null) LayoutRebuilder.ForceRebuildLayoutImmediate(dayList);

        var effectsRow = pnlHearth.Find("Pnl_EffectsColumnsRow") as RectTransform;
        if (effectsRow != null) LayoutRebuilder.ForceRebuildLayoutImmediate(effectsRow);
    }

    private void RefreshFooterUI(HearthLevelData selectedData, int currentCoal)
    {
        bool isDusk = TimePhaseManager.Instance == null || TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Dusk;
        bool canIgniteNow = !isPreviewOnly && isDusk;
        int coalCost = BeaconEntity.GetCoalCost(selectedData);
        bool hasEnoughCoal = currentCoal >= coalCost;

        if (txtSummaryHeader != null)
        {
            string costStr = coalCost <= 0 ? LocalizationManager.Get("UI_HEARTH_COST_ZERO", "0 Węgla") : LocalizationManager.GetFormat("UI_HEARTH_COST_FMT", coalCost);
            txtSummaryHeader.text = LocalizationManager.GetFormat("UI_HEARTH_SUMMARY_HEADER_FMT", selectedData.levelIndex, selectedData.DisplayTitle.ToUpperInvariant(), costStr.ToUpperInvariant());
            txtSummaryHeader.color = selectedData.accentColor;
        }

        if (txtSummaryDetails != null)
        {
            if (!canIgniteNow)
            {
                txtSummaryDetails.text = LocalizationManager.Get("UI_HEARTH_PREVIEW_MORNING", "Tryb podglądu (Morning) – rozpalenie Paleniska i rozpoczęcie Nocy będzie dostępne o Zmierzchu (Dusk).");
                txtSummaryDetails.color = ColorNeutral;
            }
            else if (!hasEnoughCoal)
            {
                int missing = coalCost - currentCoal;
                txtSummaryDetails.text = LocalizationManager.GetFormat("UI_HEARTH_NOT_ENOUGH_COAL_DETAILS", missing, currentCoal);
                txtSummaryDetails.color = ColorNegative;
            }
            else
            {
                int remaining = currentCoal - coalCost;
                string etherTier = string.IsNullOrEmpty(selectedData.etherChanceTierLabel) ? LocalizationManager.Get("hearth_ether_tier_średnie", "Średnie") : selectedData.DisplayEtherTierLabel;
                txtSummaryDetails.text = LocalizationManager.GetFormat("UI_HEARTH_AFTER_IGNITION_DETAILS", remaining, etherTier);
                txtSummaryDetails.color = new Color(0.68f, 0.65f, 0.60f, 1f);
            }
        }

        if (btnIgniteAndStart != null)
        {
            bool interactable = canIgniteNow && hasEnoughCoal;
            btnIgniteAndStart.interactable = interactable;

            if (imgIgniteButtonBg != null)
            {
                imgIgniteButtonBg.color = interactable ? ColorIgniteActive : ColorIgniteDisabled;
            }

            if (txtIgniteLabel != null)
            {
                if (!canIgniteNow)
                {
                    txtIgniteLabel.text = LocalizationManager.Get("UI_HEARTH_AVAILABLE_AT_DUSK", "DOSTĘPNE O ZMIERZCHU (DUSK)");
                }
                else if (!hasEnoughCoal)
                {
                    txtIgniteLabel.text = LocalizationManager.GetFormat("UI_HEARTH_NO_COAL_FMT", currentCoal, coalCost);
                }
                else
                {
                    txtIgniteLabel.text = LocalizationManager.Get("UI_HEARTH_IGNITE_BTN", "Rozpocznij noc");
                }
            }
        }
    }

    private void ShowIdleWorkersModal(int idleCount)
    {
        EnsureIdleWorkersModal();
        if (idleWorkersModalRoot == null)
        {
            ExecuteIgniteAndStartNight();
            return;
        }

        if (txtIdleModalTitle != null)
            txtIdleModalTitle.text = LocalizationManager.Get("UI_HEARTH_IDLE_WORKERS_MODAL_TITLE", "Wolni mieszkańcy");
        if (txtIdleModalMessage != null)
            txtIdleModalMessage.text = LocalizationManager.GetFormat("UI_HEARTH_IDLE_WORKERS_MODAL_MSG", idleCount);
        if (txtIdleModalCancel != null)
            txtIdleModalCancel.text = LocalizationManager.Get("UI_HEARTH_IDLE_WORKERS_CANCEL", "Wróć i przydziel");
        if (txtIdleModalConfirm != null)
            txtIdleModalConfirm.text = LocalizationManager.Get("UI_HEARTH_IDLE_WORKERS_CONFIRM", "Rozpocznij noc");

        idleWorkersModalRoot.SetActive(true);
        idleWorkersModalRoot.transform.SetAsLastSibling();
    }

    private void EnsureIdleWorkersModal()
    {
        if (idleWorkersModalRoot != null) return;

        var overlayGo = new GameObject("Pnl_IdleWorkersModal", typeof(RectTransform), typeof(Image), typeof(Button));
        overlayGo.transform.SetParent(transform, false);
        var overlayRt = overlayGo.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;

        var overlayImg = overlayGo.GetComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.72f);
        var overlayBtn = overlayGo.GetComponent<Button>();
        overlayBtn.transition = Selectable.Transition.None;
        overlayBtn.onClick.AddListener(() => overlayGo.SetActive(false));

        var boxGo = new GameObject("Pnl_ModalBox", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        boxGo.transform.SetParent(overlayGo.transform, false);
        var boxRt = boxGo.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(460f, 0f);

        boxGo.GetComponent<Image>().color = new Color(0.11f, 0.12f, 0.16f, 0.98f);
        var outline = boxGo.GetComponent<Outline>();
        outline.effectColor = ColorAmberGold;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var vlg = boxGo.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(24, 24, 20, 20);
        vlg.spacing = 14f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = boxGo.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        txtIdleModalTitle = CreateModalText(boxGo.transform, "Txt_Title", 20f, FontStyles.Bold, ColorAmberGold, TextAlignmentOptions.Center);
        txtIdleModalMessage = CreateModalText(boxGo.transform, "Txt_Message", 14f, FontStyles.Normal, Color.white, TextAlignmentOptions.Center);

        var btnRow = new GameObject("Pnl_Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnRow.transform.SetParent(boxGo.transform, false);
        var hlg = btnRow.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = false;

        txtIdleModalCancel = CreateModalButton(btnRow.transform, "Btn_Cancel", new Color(0.22f, 0.24f, 0.30f, 1f), () =>
        {
            overlayGo.SetActive(false);
            CloseWindow();
        });

        txtIdleModalConfirm = CreateModalButton(btnRow.transform, "Btn_Confirm", ColorIgniteActive, ExecuteIgniteAndStartNight);

        idleWorkersModalRoot = overlayGo;
        idleWorkersModalRoot.SetActive(false);
    }

    private static TextMeshProUGUI CreateModalText(Transform parent, string name, float fontSize, FontStyles style, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    private static TextMeshProUGUI CreateModalButton(Transform parent, string name, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bgColor;
        var le = go.GetComponent<LayoutElement>();
        le.preferredHeight = 40f;

        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(onClick);

        var lblGo = new GameObject("Txt_Label", typeof(RectTransform));
        lblGo.transform.SetParent(go.transform, false);
        var lblRt = lblGo.GetComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero;
        lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = new Vector2(8f, 4f);
        lblRt.offsetMax = new Vector2(-8f, -4f);

        var tmp = lblGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 14f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    private HearthEffectItemRefs GetNightEffect(int index)
    {
        if (nightEffectItems != null && index >= 0 && index < nightEffectItems.Length)
            return nightEffectItems[index];
        return null;
    }

    private HearthEffectItemRefs GetDayEffect(int index)
    {
        if (dayEffectItems != null && index >= 0 && index < dayEffectItems.Length)
            return dayEffectItems[index];
        return null;
    }

    private void UpdateEffectItem(HearthEffectItemRefs item, string title, string valueText, Color valueColor, string desc, bool isVisible = true)
    {
        if (item == null) return;
        if (item.rootObject != null)
        {
            item.rootObject.SetActive(isVisible);
        }
        if (!isVisible) return;

        if (item.imgEffectIcon != null)
        {
            item.imgEffectIcon.gameObject.SetActive(item.imgEffectIcon.sprite != null);
        }
        if (item.txtEffectTitle != null) item.txtEffectTitle.text = title;
        if (item.txtEffectValue != null)
        {
            bool hasValue = !string.IsNullOrEmpty(valueText);
            item.txtEffectValue.gameObject.SetActive(hasValue);
            item.txtEffectValue.text = valueText;
            item.txtEffectValue.color = valueColor;
        }
        if (item.txtEffectDesc != null)
        {
            item.txtEffectDesc.text = string.Empty;
            item.txtEffectDesc.gameObject.SetActive(false);
        }
    }

    private static string FormatPercentDelta(float bonus)
    {
        int pct = Mathf.RoundToInt(bonus * 100f);
        if (pct == 0) return LocalizationManager.Get("UI_HEARTH_EFF_BASE", "0% (100% bazy)");
        return pct > 0 ? $"+{pct}%" : $"{pct}%";
    }

    private static Color GetPlayerBeneficialColor(float playerBeneficialValue)
    {
        if (Mathf.Approximately(playerBeneficialValue, 0f)) return ColorNeutral;
        return playerBeneficialValue > 0f ? ColorPositive : ColorNegative;
    }

    private static string GetTowerFireRateDescription(float bonus)
    {
        int pct = Mathf.RoundToInt(bonus * 100f);
        if (pct < 0) return LocalizationManager.GetFormat("hearth_desc_firerate_neg", Mathf.Abs(pct));
        if (pct > 0) return LocalizationManager.GetFormat("hearth_desc_firerate_pos", pct);
        return LocalizationManager.Get("hearth_desc_firerate_zero", "Wieże obronne strzelają ze standardową szybkostrzelnością (100% normy).");
    }

    private static string GetTowerRangeDescription(float bonus)
    {
        int pct = Mathf.RoundToInt(bonus * 100f);
        if (pct < 0) return LocalizationManager.GetFormat("hearth_desc_range_neg", Mathf.Abs(pct));
        if (pct > 0) return LocalizationManager.GetFormat("hearth_desc_range_pos", pct);
        return LocalizationManager.Get("hearth_desc_range_zero", "Standardowy zasięg widzenia i ostrzału wszystkich wież (100% normy).");
    }

    private static string GetEnemyStatsDescription(float bonus)
    {
        int pct = Mathf.RoundToInt(bonus * 100f);
        if (pct > 0) return LocalizationManager.GetFormat("hearth_desc_enemy_pos", pct);
        if (pct < 0) return LocalizationManager.GetFormat("hearth_desc_enemy_neg", Mathf.Abs(pct));
        return LocalizationManager.Get("hearth_desc_enemy_zero", "Wrogowie nacierają ze standardową siłą, wytrzymałością i prędkością ruchu.");
    }

    private static string GetRuneDropDescription(float bonus)
    {
        int pct = Mathf.RoundToInt(bonus * 100f);
        if (pct > 0) return LocalizationManager.GetFormat("hearth_desc_runes_pos", pct);
        if (pct < 0) return LocalizationManager.GetFormat("hearth_desc_runes_neg", Mathf.Abs(pct));
        return LocalizationManager.Get("hearth_desc_runes_zero", "Bazowa szansa na zdobycie run z pokonanych wrogów bez dodatkowych modyfikatorów.");
    }

    private static string GetDayProductionDescription(float bonus)
    {
        int pct = Mathf.RoundToInt(bonus * 100f);
        if (pct < 0) return LocalizationManager.GetFormat("hearth_desc_prod_neg", Mathf.Abs(pct));
        if (pct > 0) return LocalizationManager.GetFormat("hearth_desc_prod_pos", pct);
        return LocalizationManager.Get("hearth_desc_prod_zero", "Wszystkie kopalnie, tartaki i warsztaty pracują następnego dnia ze 100% bazowej wydajności.");
    }

    private static string GetCoalCostDescription(HearthLevelData data, int coalCost = -1)
    {
        if (coalCost < 0) coalCost = BeaconEntity.GetCoalCost(data);
        if (coalCost <= 0) return LocalizationManager.Get("hearth_desc_coal_zero", "Wygaszone palenisko nie zużywa węgla, lecz wystawia twierdzę i robotników na dotkliwy chłód.");
        if (data.levelIndex <= 2) return LocalizationManager.GetFormat("hearth_desc_coal_low", coalCost);
        if (data.levelIndex == 3) return LocalizationManager.GetFormat("hearth_desc_coal_mid", coalCost);
        return LocalizationManager.GetFormat("hearth_desc_coal_high", coalCost);
    }

    private static Color GetEtherTierColor(float guaranteedPercent)
    {
        if (guaranteedPercent >= 0.80f) return new Color(0.35f, 0.85f, 1.00f); // Ogromne (Cyan)
        if (guaranteedPercent >= 0.60f) return ColorPositive;                 // Duże (Zielony)
        if (guaranteedPercent >= 0.40f) return ColorAmberGold;                // Średnie (Bursztyn/Złoto)
        if (guaranteedPercent >= 0.20f) return ColorNeutral;                  // Niskie (Szary/Neutralny)
        return ColorNegative;                                                 // Zerowe (Czerwony)
    }

    private static string GetEtherDropDescription(HearthLevelData data)
    {
        int pct = Mathf.RoundToInt(data.guaranteedEtherPercent * 100f);
        if (pct > 0)
        {
            return LocalizationManager.GetFormat("hearth_desc_ether_guaranteed", pct);
        }
        return LocalizationManager.Get("hearth_desc_ether_none", "Brak gwarantowanego Etheru z fali – możliwe jedynie zdobycie rzadkiego naturalnego dropu (5% szansy) lub z Elit i Bossów.");
    }

    // =========================================================================
    // AutoBindReferences – automatyczne podpinanie hierarchii UI
    // =========================================================================
    public void AutoBindReferences()
    {
        var bgBlock = transform.Find("Img_BackgroundBlock");
        if (bgBlock != null && backgroundBlockButton == null)
        {
            backgroundBlockButton = bgBlock.GetComponent<Button>();
            if (backgroundBlockButton == null)
            {
                backgroundBlockButton = bgBlock.gameObject.AddComponent<Button>();
                backgroundBlockButton.transition = Selectable.Transition.None;
            }
        }

        var pnlHearth = transform.Find("Pnl_Hearth");
        if (pnlHearth == null) return;

        // 1. TopBar
        var btnCloseTr = pnlHearth.Find("Pnl_TopBar/Btn_Close");
        if (btnCloseTr != null) btnClose = btnCloseTr.GetComponent<Button>();

        var badgeMenuTr = pnlHearth.Find("Pnl_TopBar/Pnl_TitleColumn/Badge_Menu") ?? pnlHearth.Find("Pnl_TopBar/Badge_Menu");
        if (badgeMenuTr != null)
        {
            badgeMenuTr.gameObject.SetActive(false);
        }

        // 2. InfoAndCoalBar
        var badgeFreeWorkersTr = pnlHearth.Find("Pnl_InfoAndCoalBar/Pnl_FreeWorkers/Badge_FreeWorkers");
        if (badgeFreeWorkersTr != null)
        {
            imgFreeWorkersBadge = badgeFreeWorkersTr.GetComponent<Image>();
            var txtAmt = badgeFreeWorkersTr.Find("Txt_Amount");
            if (txtAmt != null) txtFreeWorkersAmount = txtAmt.GetComponent<TextMeshProUGUI>();
        }

        var coalStockValTr = pnlHearth.Find("Pnl_InfoAndCoalBar/Pnl_CoalStock/Pnl_CoalStockValue");
        if (coalStockValTr != null)
        {
            coalStockSingleResourceUI = coalStockValTr.GetComponent<SingleResourceUI>();
            txtCoalStockFallback = coalStockValTr.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        // 3. PreviewBox
        var previewBoxTr = pnlHearth.Find("Pnl_HearthPreviewBox");
        if (previewBoxTr != null)
        {
            var imgPrev = previewBoxTr.Find("Img_HearthPreview");
            if (imgPrev != null) imgHearthPreview = imgPrev.GetComponent<Image>();

            var summaryBadge = previewBoxTr.Find("Badge_CurrentLevelSummary");
            if (summaryBadge != null)
            {
                for (int c = 0; c < summaryBadge.childCount; c++)
                {
                    var child = summaryBadge.GetChild(c);
                    if (child != null && child.name != "Txt_PreviewLevelName")
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }

            var txtPrevName = previewBoxTr.Find("Badge_CurrentLevelSummary/Txt_PreviewLevelName");
            if (txtPrevName != null) txtPrevName = txtPrevName.GetComponent<TextMeshProUGUI>() != null ? txtPrevName : null;
            if (txtPrevName != null) txtPreviewLevelName = txtPrevName.GetComponent<TextMeshProUGUI>();

            var txtPrevCost = previewBoxTr.Find("Badge_CurrentLevelSummary/Txt_PreviewLevelCost");
            if (txtPrevCost == null) txtPrevCost = previewBoxTr.Find("Txt_PreviewLevelCost");
            if (txtPrevCost != null)
            {
                txtPreviewLevelCost = txtPrevCost.GetComponent<TextMeshProUGUI>();
                if (txtPreviewLevelCost != null)
                {
                    txtPreviewLevelCost.gameObject.SetActive(false);
                }
            }
        }

        // 4. LevelsRow (5 kart)
        var levelsRowTr = pnlHearth.Find("Pnl_LevelsRow");
        if (levelsRowTr != null)
        {
            if (levelCards == null || levelCards.Length != 5)
                levelCards = new HearthLevelCardRefs[5];

            for (int i = 0; i < 5; i++)
            {
                var cardTr = levelsRowTr.Find($"Card_HearthLevel_{i + 1}");
                if (cardTr == null && i < levelsRowTr.childCount)
                    cardTr = levelsRowTr.GetChild(i);

                if (cardTr != null)
                {
                    levelCards[i] = BindCardRefs(cardTr);
                }
            }
        }

        // 5. EffectsColumnsRow
        var nightListTr = pnlHearth.Find("Pnl_EffectsColumnsRow/Sec_NightEffects/Pnl_EffectsList");
        if (nightListTr != null)
        {
            if (nightEffectItems == null || nightEffectItems.Length != 5)
                nightEffectItems = new HearthEffectItemRefs[5];

            for (int i = 0; i < 4; i++)
            {
                var itemTr = nightListTr.Find($"EffectItem_{i + 1}");
                if (itemTr == null && i < nightListTr.childCount)
                    itemTr = nightListTr.GetChild(i);

                if (itemTr != null)
                {
                    nightEffectItems[i] = BindEffectItemRefs(itemTr);
                }
            }

            var item5Tr = nightListTr.Find("EffectItem_5");
            if (item5Tr == null && nightEffectItems[3] != null && nightEffectItems[3].rootObject != null)
            {
                var cloned = Instantiate(nightEffectItems[3].rootObject, nightListTr);
                cloned.name = "EffectItem_5";
                item5Tr = cloned.transform;
            }

            if (item5Tr != null)
            {
                nightEffectItems[4] = BindEffectItemRefs(item5Tr);
                if (nightEffectItems[4].imgEffectIcon != null)
                {
                    nightEffectItems[4].imgEffectIcon.color = new Color(0.35f, 0.85f, 1.00f);
                }
            }
        }

        var dayListTr = pnlHearth.Find("Pnl_EffectsColumnsRow/Sec_DayEffects/Pnl_EffectsList");
        if (dayListTr != null)
        {
            if (dayEffectItems == null || dayEffectItems.Length != 2)
                dayEffectItems = new HearthEffectItemRefs[2];

            for (int i = 0; i < 2; i++)
            {
                var itemTr = dayListTr.Find($"EffectItem_{i + 1}");
                if (itemTr == null && i < dayListTr.childCount)
                    itemTr = dayListTr.GetChild(i);

                if (itemTr != null)
                {
                    dayEffectItems[i] = BindEffectItemRefs(itemTr);
                }
            }
        }

        // 6. FooterBar
        var footerTr = pnlHearth.Find("Pnl_FooterBar");
        if (footerTr != null)
        {
            var sumHeader = footerTr.Find("Pnl_FuelSummary/Txt_SummaryHeader");
            if (sumHeader != null) txtSummaryHeader = sumHeader.GetComponent<TextMeshProUGUI>();

            var sumDetails = footerTr.Find("Pnl_FuelSummary/Txt_SummaryDetails");
            if (sumDetails != null) txtSummaryDetails = sumDetails.GetComponent<TextMeshProUGUI>();

            var cancelTr = footerTr.Find("Btn_Cancel");
            if (cancelTr != null) btnCancel = cancelTr.GetComponent<Button>();

            var igniteTr = footerTr.Find("Btn_IgniteAndStart");
            if (igniteTr != null)
            {
                btnIgniteAndStart = igniteTr.GetComponent<Button>();
                imgIgniteButtonBg = igniteTr.GetComponent<Image>();
                var igniteLbl = igniteTr.Find("Txt_IgniteLabel");
                if (igniteLbl != null) txtIgniteLabel = igniteLbl.GetComponent<TextMeshProUGUI>();
            }
        }
    }

    private static HearthLevelCardRefs BindCardRefs(Transform cardTr)
    {
        var refs = new HearthLevelCardRefs
        {
            rootObject = cardTr.gameObject,
            button = cardTr.GetComponent<Button>(),
            background = cardTr.GetComponent<Image>(),
            outline = cardTr.GetComponent<Outline>()
        };

        var topBarTr = cardTr.Find("Img_SelectedTopBar");
        if (topBarTr != null)
        {
            refs.selectedTopBar = topBarTr.gameObject;
            refs.selectedTopBarImage = topBarTr.GetComponent<Image>();
        }

        var lvlIdxTr = cardTr.Find("Pnl_CardTopRow/Txt_LevelIndex");
        if (lvlIdxTr != null) refs.txtLevelIndex = lvlIdxTr.GetComponent<TextMeshProUGUI>();

        var coalCostTr = cardTr.Find("Pnl_CardTopRow/Pnl_CostContainer/Pnl_CoalCost");
        if (coalCostTr != null)
        {
            refs.coalCostObject = coalCostTr.gameObject;
            refs.coalCostSingleUI = coalCostTr.GetComponent<SingleResourceUI>();
            var amtTr = coalCostTr.Find("Txt_Amount");
            if (amtTr != null) refs.txtCoalCostAmount = amtTr.GetComponent<TextMeshProUGUI>();
            else refs.txtCoalCostAmount = coalCostTr.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        var badgeFreeTr = cardTr.Find("Pnl_CardTopRow/Pnl_CostContainer/Badge_Free");
        if (badgeFreeTr != null) refs.badgeFreeObject = badgeFreeTr.gameObject;

        var iconBgTr = cardTr.Find("Pnl_IconContainer/Img_IconBg");
        if (iconBgTr != null) refs.imgIconBg = iconBgTr.GetComponent<Image>();

        var iconTr = cardTr.Find("Pnl_IconContainer/Img_LevelIcon");
        if (iconTr == null) iconTr = cardTr.Find("Pnl_IconContainer/Img_IconBg/Img_LevelIcon");
        if (iconTr != null) refs.imgLevelIcon = iconTr.GetComponent<Image>();

        var titleTr = cardTr.Find("Txt_LevelTitle");
        if (titleTr != null) refs.txtLevelTitle = titleTr.GetComponent<TextMeshProUGUI>();

        var badgeTagTr = cardTr.Find("Pnl_TagRow/Badge_LevelTag");
        if (badgeTagTr != null)
        {
            refs.badgeTagImage = badgeTagTr.GetComponent<Image>();
            var txtTagTr = badgeTagTr.Find("Txt_Tag");
            if (txtTagTr != null) refs.txtTag = txtTagTr.GetComponent<TextMeshProUGUI>();
        }

        return refs;
    }

    private static HearthEffectItemRefs BindEffectItemRefs(Transform itemTr)
    {
        var refs = new HearthEffectItemRefs
        {
            rootObject = itemTr.gameObject
        };

        var iconTr = itemTr.Find("Pnl_TopRow/Img_EffectIcon");
        if (iconTr != null) refs.imgEffectIcon = iconTr.GetComponent<Image>();

        var titleTr = itemTr.Find("Pnl_TopRow/Txt_EffectTitle");
        if (titleTr != null) refs.txtEffectTitle = titleTr.GetComponent<TextMeshProUGUI>();

        var valTr = itemTr.Find("Pnl_TopRow/Txt_EffectValue");
        if (valTr != null) refs.txtEffectValue = valTr.GetComponent<TextMeshProUGUI>();

        var descTr = itemTr.Find("Txt_EffectDesc");
        if (descTr != null)
        {
            refs.txtEffectDesc = descTr.GetComponent<TextMeshProUGUI>();
            if (refs.txtEffectDesc != null)
            {
                refs.txtEffectDesc.text = string.Empty;
                refs.txtEffectDesc.gameObject.SetActive(false);
            }
        }

        return refs;
    }
}
