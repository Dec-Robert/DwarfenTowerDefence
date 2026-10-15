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

        RefreshAllUI();
    }

    private void OnDisable()
    {
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

        RefreshAllUI();
        ForceRebuildImmediate();
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
        currentSelectedLevel = Mathf.Clamp(levelIndex, 1, 5);
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

        if (BeaconEntity.TryIgniteAndStartNight(currentSelectedLevel))
        {
            CloseWindow();
        }
        else
        {
            RefreshAllUI();
        }
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
            bool isSelected = (data.levelIndex == currentSelectedLevel);
            bool canAfford = currentCoal >= data.coalCost;

            if (card.background != null)
            {
                card.background.color = isSelected ? ColorCardSelectedBg : ColorCardNormalBg;
            }

            if (card.outline != null)
            {
                card.outline.effectColor = isSelected ? ColorGoldHighlight : ColorBorderSubtle;
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
                card.txtLevelIndex.text = $"POZIOM {data.levelIndex}";
                card.txtLevelIndex.color = isSelected ? ColorGoldHighlight : new Color(0.68f, 0.65f, 0.60f, 1f);
            }

            bool isFree = data.coalCost <= 0;
            if (card.coalCostObject != null) card.coalCostObject.SetActive(!isFree);
            if (card.badgeFreeObject != null) card.badgeFreeObject.SetActive(isFree);

            if (!isFree)
            {
                if (card.coalCostSingleUI != null)
                {
                    card.coalCostSingleUI.Setup(ResourceType.Coal, data.coalCost);
                }

                if (card.txtCoalCostAmount != null)
                {
                    card.txtCoalCostAmount.text = data.coalCost.ToString();
                    card.txtCoalCostAmount.color = canAfford ? Color.white : ColorNegative;
                }
            }

            if (card.imgIconBg != null)
            {
                card.imgIconBg.color = new Color(data.accentColor.r * 0.35f, data.accentColor.g * 0.35f, data.accentColor.b * 0.35f, 0.95f);
            }

            if (card.imgLevelIcon != null)
            {
                if (data.levelIcon != null) card.imgLevelIcon.sprite = data.levelIcon;
                card.imgLevelIcon.color = data.accentColor;
            }

            if (card.txtLevelTitle != null)
            {
                card.txtLevelTitle.text = data.title;
            }

            if (card.badgeTagImage != null)
            {
                card.badgeTagImage.color = new Color(data.accentColor.r * 0.28f, data.accentColor.g * 0.28f, data.accentColor.b * 0.28f, 0.95f);
            }

            if (card.txtTag != null)
            {
                card.txtTag.text = data.tagText;
                card.txtTag.color = data.accentColor;
            }
        }
    }

    private void RefreshPreviewBoxUI(HearthLevelData selectedData)
    {
        if (imgHearthPreview != null)
        {
            if (selectedData.levelIcon != null)
            {
                imgHearthPreview.sprite = selectedData.levelIcon;
            }
            imgHearthPreview.color = selectedData.accentColor;
        }

        if (txtPreviewLevelName != null)
        {
            txtPreviewLevelName.text = $"POZIOM {selectedData.levelIndex}: {selectedData.title.ToUpperInvariant()}";
            txtPreviewLevelName.color = selectedData.accentColor;
        }

        if (txtPreviewLevelCost != null)
        {
            string costText = selectedData.coalCost <= 0 ? "0 WĘGLA (DARMOWY)" : $"{selectedData.coalCost} WĘGLA";
            string etherTier = string.IsNullOrEmpty(selectedData.etherChanceTierLabel) ? "ŚREDNIE" : selectedData.etherChanceTierLabel.ToUpperInvariant();
            txtPreviewLevelCost.text = $"KOSZT: {costText}  •  {selectedData.tagText}  •  ETER: {etherTier} ({Mathf.RoundToInt(selectedData.guaranteedEtherPercent * 100f)}%)";
        }
    }

    private void RefreshEffectsUI(HearthLevelData selectedData)
    {
        // 1. Szybkostrzelność wież obronnych (FireRate)
        UpdateEffectItem(
            GetNightEffect(0),
            "Szybkostrzelność wież",
            FormatPercentDelta(selectedData.towerFireRateBonus),
            GetPlayerBeneficialColor(selectedData.towerFireRateBonus),
            GetTowerFireRateDescription(selectedData.towerFireRateBonus)
        );

        // 2. Zasięg wież obronnych (Range)
        UpdateEffectItem(
            GetNightEffect(1),
            "Zasięg wież",
            FormatPercentDelta(selectedData.towerRangeBonus),
            GetPlayerBeneficialColor(selectedData.towerRangeBonus),
            GetTowerRangeDescription(selectedData.towerRangeBonus)
        );

        // 3. Siła i prędkość wrogów (Enemy HP & Speed - ujemny bonus jest korzystny dla gracza!)
        UpdateEffectItem(
            GetNightEffect(2),
            "Siła i prędkość wrogów w nocy",
            FormatPercentDelta(selectedData.enemyStatsBonus),
            GetPlayerBeneficialColor(-selectedData.enemyStatsBonus),
            GetEnemyStatsDescription(selectedData.enemyStatsBonus)
        );

        // 4. Szansa na drop i rzadkość run (RuneManager)
        UpdateEffectItem(
            GetNightEffect(3),
            "Szansa na drop i rzadkość Run",
            FormatPercentDelta(selectedData.runeDropAndRarityBonus),
            GetPlayerBeneficialColor(selectedData.runeDropAndRarityBonus),
            GetRuneDropDescription(selectedData.runeDropAndRarityBonus)
        );

        // 5. Szanse na drop Etheru (Ether Wave Pool & Guarantee)
        Color etherColor = GetEtherTierColor(selectedData.guaranteedEtherPercent);
        string etherLabel = string.IsNullOrEmpty(selectedData.etherChanceTierLabel) ? "Średnie" : selectedData.etherChanceTierLabel;
        UpdateEffectItem(
            GetNightEffect(4),
            "Szanse na drop Etheru",
            $"{etherLabel.ToUpperInvariant()} ({Mathf.RoundToInt(selectedData.guaranteedEtherPercent * 100f)}% gwarant)",
            etherColor,
            GetEtherDropDescription(selectedData)
        );

        // Dzień 1: Produkcja wszystkich budynków następnego dnia
        UpdateEffectItem(
            GetDayEffect(0),
            "Produkcja wszystkich budynków",
            FormatPercentDelta(selectedData.nextDayProductionBonus),
            GetPlayerBeneficialColor(selectedData.nextDayProductionBonus),
            GetDayProductionDescription(selectedData.nextDayProductionBonus)
        );

        // Dzień 2: Zużycie węgla tej nocy
        string coalValueText = selectedData.coalCost <= 0 ? "0 Węgla" : $"-{selectedData.coalCost} Węgla";
        Color coalColor = selectedData.coalCost <= 0
            ? ColorPositive
            : (selectedData.coalCost >= 65 ? ColorAmberGold : ColorNeutral);

        UpdateEffectItem(
            GetDayEffect(1),
            "Zużycie węgla tej nocy",
            coalValueText,
            coalColor,
            GetCoalCostDescription(selectedData)
        );
    }

    private void RefreshFooterUI(HearthLevelData selectedData, int currentCoal)
    {
        bool isDusk = TimePhaseManager.Instance == null || TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Dusk;
        bool canIgniteNow = !isPreviewOnly && isDusk;
        bool hasEnoughCoal = currentCoal >= selectedData.coalCost;

        if (txtSummaryHeader != null)
        {
            string costStr = selectedData.coalCost <= 0 ? "0 Węgla" : $"{selectedData.coalCost} Węgla";
            txtSummaryHeader.text = $"WYBRANO: POZIOM {selectedData.levelIndex} – {selectedData.title.ToUpperInvariant()} (KOSZT: {costStr.ToUpperInvariant()})";
            txtSummaryHeader.color = selectedData.accentColor;
        }

        if (txtSummaryDetails != null)
        {
            if (!canIgniteNow)
            {
                txtSummaryDetails.text = "Tryb podglądu (Morning) – rozpalenie Paleniska i rozpoczęcie Nocy będzie dostępne o Zmierzchu (Dusk).";
                txtSummaryDetails.color = ColorNeutral;
            }
            else if (!hasEnoughCoal)
            {
                int missing = selectedData.coalCost - currentCoal;
                txtSummaryDetails.text = $"Brak wystarczającej ilości węgla w skarbcu! Brakuje jeszcze {missing} szt. Węgla (posiadasz: {currentCoal}).";
                txtSummaryDetails.color = ColorNegative;
            }
            else
            {
                int remaining = currentCoal - selectedData.coalCost;
                int etherPct = Mathf.RoundToInt(selectedData.guaranteedEtherPercent * 100f);
                string etherTier = string.IsNullOrEmpty(selectedData.etherChanceTierLabel) ? "Średnie" : selectedData.etherChanceTierLabel;
                txtSummaryDetails.text = $"Po rozpaleniu pozostanie {remaining} Węgla. Szanse na Ether: {etherTier} ({etherPct}% gwarantowane z puli fali).";
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
                    txtIgniteLabel.text = "DOSTĘPNE O ZMIERZCHU (DUSK)";
                }
                else if (!hasEnoughCoal)
                {
                    txtIgniteLabel.text = $"BRAK WĘGLA ({currentCoal}/{selectedData.coalCost})";
                }
                else
                {
                    txtIgniteLabel.text = "ROZPAL PALENISKO I ROZPOCZNIJ NOC";
                }
            }
        }
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

    private void UpdateEffectItem(HearthEffectItemRefs item, string title, string valueText, Color valueColor, string desc)
    {
        if (item == null) return;
        if (item.txtEffectTitle != null) item.txtEffectTitle.text = title;
        if (item.txtEffectValue != null)
        {
            item.txtEffectValue.text = valueText;
            item.txtEffectValue.color = valueColor;
        }
        if (item.txtEffectDesc != null) item.txtEffectDesc.text = desc;
    }

    private static string FormatPercentDelta(float bonus)
    {
        int pct = Mathf.RoundToInt(bonus * 100f);
        if (pct == 0) return "0% (100% bazy)";
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
        if (pct < 0) return $"Chłód i mrok spowalniają załogi – szybkostrzelność wież spada o {Mathf.Abs(pct)}%.";
        if (pct > 0) return $"Żar Paleniska i wysokie morale przyspieszają tempo ostrzału wież o {pct}%.";
        return "Wieże obronne strzelają ze standardową szybkostrzelnością (100% normy).";
    }

    private static string GetTowerRangeDescription(float bonus)
    {
        int pct = Mathf.RoundToInt(bonus * 100f);
        if (pct < 0) return $"Gęsty mrok ogranicza widoczność przedpola – zasięg wież spada o {Mathf.Abs(pct)}%.";
        if (pct > 0) return $"Blask ognia rozświetla przedpole – zasięg ostrzału wież rośnie o {pct}%.";
        return "Standardowy zasięg widzenia i ostrzału wszystkich wież (100% normy).";
    }

    private static string GetEnemyStatsDescription(float bonus)
    {
        int pct = Mathf.RoundToInt(bonus * 100f);
        if (pct > 0) return $"Mrok rozzuchwala hordę – wrogowie posiadają o {pct}% więcej PŻ oraz poruszają się szybciej.";
        if (pct < 0) return $"Oślepiający blask Paleniska osłabia napastników – wrogowie mają o {Mathf.Abs(pct)}% mniej PŻ i są wolniejsi.";
        return "Wrogowie nacierają ze standardową siłą, wytrzymałością i prędkością ruchu.";
    }

    private static string GetRuneDropDescription(float bonus)
    {
        int pct = Mathf.RoundToInt(bonus * 100f);
        if (pct > 0) return $"Zagęszczona magia mroku zwiększa szansę na drop run oraz wylosowanie rzadkich run o {pct}%.";
        if (pct < 0) return $"Czysty płomień wypala mroczną energię – szansa na drop rzadkich run jest niższa o {Mathf.Abs(pct)}%.";
        return "Bazowa szansa na zdobycie run z pokonanych wrogów bez dodatkowych modyfikatorów.";
    }

    private static string GetDayProductionDescription(float bonus)
    {
        int pct = Mathf.RoundToInt(bonus * 100f);
        if (pct < 0) return $"Zmarznięci robotnicy pracują wolniej – produkcja wszystkich budynków następnego dnia spadnie o {Mathf.Abs(pct)}%.";
        if (pct > 0) return $"Wysokie morale i ciepło kuźni zwiększą wydobycie oraz produkcję wszystkich budynków o {pct}%.";
        return "Wszystkie kopalnie, tartaki i warsztaty pracują następnego dnia ze 100% bazowej wydajności.";
    }

    private static string GetCoalCostDescription(HearthLevelData data)
    {
        if (data.coalCost <= 0) return "Wygaszone palenisko nie zużywa węgla, lecz wystawia twierdzę i robotników na dotkliwy chłód.";
        if (data.levelIndex <= 2) return $"Oszczędne spalanie ({data.coalCost} szt. węgla) pozwala zachować zapasy kosztem lekkiego osłabienia.";
        if (data.levelIndex == 3) return $"Standardowy przydział paliwa ({data.coalCost} szt. węgla) utrzymuje pełną równowagę w twierdzy.";
        return $"Intensywne spalanie ({data.coalCost} szt. węgla) zapewnia potężne wsparcie obronne i gospodarcze.";
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
            return $"Gwarantowane pozyskanie co najmniej {pct}% Etheru z puli fali. Pozostały Ether może wydropić naturalnie (5% szansy).";
        }
        return "Brak gwarantowanego Etheru z fali – możliwe jedynie zdobycie rzadkiego naturalnego dropu (5% szansy) lub z Elit i Bossów.";
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

            var txtPrevName = previewBoxTr.Find("Badge_CurrentLevelSummary/Txt_PreviewLevelName");
            if (txtPrevName != null) txtPrevName = txtPrevName.GetComponent<TextMeshProUGUI>() != null ? txtPrevName : null;
            if (txtPrevName != null) txtPreviewLevelName = txtPrevName.GetComponent<TextMeshProUGUI>();

            var txtPrevCost = previewBoxTr.Find("Badge_CurrentLevelSummary/Txt_PreviewLevelCost");
            if (txtPrevCost == null) txtPrevCost = previewBoxTr.Find("Txt_PreviewLevelCost");
            if (txtPrevCost != null) txtPreviewLevelCost = txtPrevCost.GetComponent<TextMeshProUGUI>();
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
        if (descTr != null) refs.txtEffectDesc = descTr.GetComponent<TextMeshProUGUI>();

        return refs;
    }
}
