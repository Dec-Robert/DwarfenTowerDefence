using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Główny komponent zarządzający oknem kontekstowym budynków mieszkalnych (Pnl_HousingContext).
/// Obsługuje:
/// - Podsumowanie populacji rasy (ikona, nazwa, licznik oraz pasek zapełnienia pojemności),
/// - Koszty utrzymania (stawki per mieszkaniec, sumaryczne zapotrzebowanie dzienne oraz koszty nadwyżki wzrostu),
/// - Przełącznik sterowania wzrostem (AKTYWNY / WSTRZYMANY / PEŁNY),
/// - Pasek postępu cykli demograficznych i szacowany czas do narodzin,
/// - Dwuetapowe wyburzanie z potwierdzeniem.
/// </summary>
public class HousingContextWindowUI : MonoBehaviour
{
    // ── 1. Nagłówek i Ogólne ────────────────────────────────────────────────
    [Header("Nagłówek i Ogólne")]
    [SerializeField] private TextMeshProUGUI txtBuildingName;
    [SerializeField] private Button btnClose;
    [SerializeField] private ResourceVisualsSO resourceVisualsSO;

    // ── 2. Sekcja Populacji ─────────────────────────────────────────────────
    [Header("Sekcja Populacji")]
    [SerializeField] private GameObject secPopulation;
    [SerializeField] private Image imgRaceIcon;
    [SerializeField] private TextMeshProUGUI txtRaceName;
    [SerializeField] private TextMeshProUGUI txtPopulationCount;
    [SerializeField] private RectTransform imgCapacityFill;

    // ── 3. Sekcja Utrzymania ────────────────────────────────────────────────
    [Header("Sekcja Utrzymania")]
    [SerializeField] private GameObject secUpkeep;
    [SerializeField] private Transform pnlResidentUpkeepItems;
    [SerializeField] private TextMeshProUGUI txtResidentUpkeepTotal;

    [Header("Koszty Wzrostu")]
    [SerializeField] private Button btnToggleGrowth;
    [SerializeField] private TextMeshProUGUI txtGrowthStatusLabel;
    [SerializeField] private Image imgGrowthStatusBackground;
    [SerializeField] private Transform pnlGrowthCostItems;

    // ── 4. Sekcja Progresji Wzrostu ─────────────────────────────────────────
    [Header("Progresja Wzrostu")]
    [SerializeField] private GameObject secGrowthProgress;
    [SerializeField] private TextMeshProUGUI txtCyclesRequired;
    [SerializeField] private RectTransform imgGrowthProgressFill;
    [SerializeField] private TextMeshProUGUI txtProgressStatus;
    [SerializeField] private TextMeshProUGUI txtProgressRate;

    // ── 5. Dolna Belka (Wyburzanie) ─────────────────────────────────────────
    [Header("Akcje i Wyburzanie")]
    [SerializeField] private Button btnDemolish;
    [SerializeField] private TextMeshProUGUI txtDemolishLabel;
    [SerializeField] private Image imgDemolishBackground;

    // ── Stan i Kolory ───────────────────────────────────────────────────────
    private HousingEntity currentHousing;
    private bool isDemolishConfirming;
    private Coroutine layoutCoroutine;

    // Kolory przycisku ToggleGrowth
    private readonly Color growthActiveBg = new Color(0.14f, 0.28f, 0.18f, 0.95f);
    private readonly Color growthActiveTxt = new Color(0.45f, 0.95f, 0.55f, 1f);

    private readonly Color growthPausedBg = new Color(0.28f, 0.22f, 0.12f, 0.95f);
    private readonly Color growthPausedTxt = new Color(1f, 0.85f, 0.4f, 1f);

    private readonly Color growthFullBg = new Color(0.2f, 0.2f, 0.25f, 0.85f);
    private readonly Color growthFullTxt = new Color(0.7f, 0.72f, 0.75f, 1f);

    // Kolory przycisku Wyburzania (zgodne z ProductionContextWindowUI)
    private readonly Color demolishNormalBg = new Color(0.32f, 0.12f, 0.12f, 0.95f);
    private readonly Color demolishNormalTxt = new Color(1f, 0.45f, 0.45f, 1f);
    private readonly Color demolishConfirmBg = new Color(0.6f, 0.15f, 0.15f, 1f);
    private readonly Color demolishConfirmTxt = new Color(1f, 0.9f, 0.2f, 1f);

    private void Awake()
    {
        AutoBindReferences();
    }

    private void OnValidate()
    {
        AutoBindReferences();
    }

    private void OnDisable()
    {
        if (layoutCoroutine != null)
        {
            StopCoroutine(layoutCoroutine);
            layoutCoroutine = null;
        }
        UnsubscribeEvents();
        ResetDemolishState();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    // =========================================================================
    // INICJALIZACJA I AUTO-BINDING
    // =========================================================================

    /// <summary>
    /// Automatycznie wyszukuje i wiąże komponenty po nazwach w hierarchii Pnl_HousingContext.
    /// </summary>
    public void AutoBindReferences()
    {
        // 1. Nagłówek
        if (txtBuildingName == null)
            txtBuildingName = transform.Find("Pnl_TopBar/Txt_BuildingName")?.GetComponent<TextMeshProUGUI>();

        if (btnClose == null)
            btnClose = transform.Find("Pnl_TopBar/Btn_Close")?.GetComponent<Button>();

        // 2. ResourceVisualsSO fallback
        if (resourceVisualsSO == null)
        {
#if UNITY_EDITOR
            resourceVisualsSO = UnityEditor.AssetDatabase.LoadAssetAtPath<ResourceVisualsSO>(
                "Assets/Main folder/ScriptableObjects/Configs/ResourceVisualDatabase.asset");
#endif
        }

        // 3. Sekcja Populacji
        if (secPopulation == null)
            secPopulation = transform.Find("Sec_Population")?.gameObject;

        if (secPopulation != null)
        {
            if (imgRaceIcon == null)
                imgRaceIcon = secPopulation.transform.Find("Pnl_PopHeaderRow/Img_RaceIcon")?.GetComponent<Image>();

            if (txtRaceName == null)
                txtRaceName = secPopulation.transform.Find("Pnl_PopHeaderRow/Txt_RaceName")?.GetComponent<TextMeshProUGUI>();

            if (txtPopulationCount == null)
                txtPopulationCount = secPopulation.transform.Find("Pnl_PopHeaderRow/Txt_PopulationCount")?.GetComponent<TextMeshProUGUI>();

            if (imgCapacityFill == null)
                imgCapacityFill = secPopulation.transform.Find("Pnl_CapacityBar/Img_CapacityFill")?.GetComponent<RectTransform>();
        }

        // 4. Sekcja Utrzymania
        if (secUpkeep == null)
            secUpkeep = transform.Find("Sec_Upkeep")?.gameObject;

        if (secUpkeep != null)
        {
            if (pnlResidentUpkeepItems == null)
                pnlResidentUpkeepItems = secUpkeep.transform.Find("Pnl_ResidentUpkeep/Pnl_ResidentUpkeepItems");

            if (txtResidentUpkeepTotal == null)
                txtResidentUpkeepTotal = secUpkeep.transform.Find("Pnl_ResidentUpkeep/Txt_ResidentUpkeepTotal")?.GetComponent<TextMeshProUGUI>();

            if (btnToggleGrowth == null)
                btnToggleGrowth = secUpkeep.transform.Find("Pnl_GrowthUpkeep/Pnl_GrowthHeaderRow/Btn_ToggleGrowth")?.GetComponent<Button>();

            if (btnToggleGrowth != null)
            {
                if (txtGrowthStatusLabel == null)
                    txtGrowthStatusLabel = btnToggleGrowth.transform.Find("Txt_GrowthStatusLabel")?.GetComponent<TextMeshProUGUI>();

                if (imgGrowthStatusBackground == null)
                    imgGrowthStatusBackground = btnToggleGrowth.GetComponent<Image>();
            }

            if (pnlGrowthCostItems == null)
                pnlGrowthCostItems = secUpkeep.transform.Find("Pnl_GrowthUpkeep/Pnl_GrowthCostItems");
        }

        // 5. Sekcja Progresji Wzrostu
        if (secGrowthProgress == null)
            secGrowthProgress = transform.Find("Sec_GrowthProgress")?.gameObject;

        if (secGrowthProgress != null)
        {
            if (txtCyclesRequired == null)
                txtCyclesRequired = secGrowthProgress.transform.Find("Pnl_ProgHeaderRow/Txt_CyclesRequired")?.GetComponent<TextMeshProUGUI>();

            if (imgGrowthProgressFill == null)
                imgGrowthProgressFill = secGrowthProgress.transform.Find("Pnl_GrowthProgressBar/Img_GrowthProgressFill")?.GetComponent<RectTransform>();

            if (txtProgressStatus == null)
                txtProgressStatus = secGrowthProgress.transform.Find("Pnl_ProgressLabels/Txt_ProgressStatus")?.GetComponent<TextMeshProUGUI>();

            if (txtProgressRate == null)
                txtProgressRate = secGrowthProgress.transform.Find("Pnl_ProgressLabels/Txt_ProgressRate")?.GetComponent<TextMeshProUGUI>();
        }

        // 6. Dolna belka akcji
        if (btnDemolish == null)
            btnDemolish = transform.Find("Pnl_BottomBar/Btn_Demolish")?.GetComponent<Button>();

        if (btnDemolish != null)
        {
            if (txtDemolishLabel == null)
                txtDemolishLabel = btnDemolish.transform.Find("Txt_Label")?.GetComponent<TextMeshProUGUI>();

            if (imgDemolishBackground == null)
                imgDemolishBackground = btnDemolish.GetComponent<Image>();
        }
    }

    // =========================================================================
    // KONFIGURACJA I CYKL ŻYCIA
    // =========================================================================

    public void Setup(HousingEntity housingEntity)
    {
        AutoBindReferences();

        UnsubscribeEvents();
        currentHousing = housingEntity;
        ResetDemolishState();

        if (currentHousing == null) return;

        SubscribeEvents();

        // Podpięcie przycisku zamknięcia
        if (btnClose != null)
        {
            btnClose.onClick.RemoveAllListeners();
            btnClose.onClick.AddListener(CloseWindow);
        }

        // Podpięcie przycisku przełączania wzrostu
        if (btnToggleGrowth != null)
        {
            btnToggleGrowth.onClick.RemoveAllListeners();
            btnToggleGrowth.onClick.AddListener(HandleToggleGrowth);
        }

        // Podpięcie przycisku wyburzenia
        if (btnDemolish != null)
        {
            btnDemolish.onClick.RemoveAllListeners();
            btnDemolish.onClick.AddListener(HandleDemolishClick);
        }

        RefreshAll();
        ForceRebuildLayout();
    }

    private void SubscribeEvents()
    {
        if (PopLifeCycleManager.Instance != null)
        {
            PopLifeCycleManager.Instance.OnCitizenBorn += HandleCitizenChanged;
            PopLifeCycleManager.Instance.OnCitizenDied += HandleCitizenDied;
            PopLifeCycleManager.Instance.OnTotalPopulationChanged += HandleTotalPopulationChanged;
        }

        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted += HandleMorningStarted;
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged += HandleResourceChanged;
        }

        if (currentHousing != null)
        {
            currentHousing.OnHousingStateChanged += HandleMorningStarted;
        }
    }

    private void UnsubscribeEvents()
    {
        if (PopLifeCycleManager.Instance != null)
        {
            PopLifeCycleManager.Instance.OnCitizenBorn -= HandleCitizenChanged;
            PopLifeCycleManager.Instance.OnCitizenDied -= HandleCitizenDied;
            PopLifeCycleManager.Instance.OnTotalPopulationChanged -= HandleTotalPopulationChanged;
        }

        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= HandleMorningStarted;
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged -= HandleResourceChanged;
        }

        if (currentHousing != null)
        {
            currentHousing.OnHousingStateChanged -= HandleMorningStarted;
        }
    }

    private void HandleCitizenChanged(Citizen citizen)
    {
        if (currentHousing == null) return;
        RefreshPopulationUI();
        RefreshUpkeepUI();
        RefreshGrowthProgressUI();
    }

    private void HandleCitizenDied(Citizen citizen, DeathReason reason)
    {
        if (currentHousing == null) return;
        RefreshPopulationUI();
        RefreshUpkeepUI();
        RefreshGrowthProgressUI();
    }

    private void HandleTotalPopulationChanged(Race race, int count)
    {
        if (currentHousing == null) return;
        RefreshPopulationUI();
    }

    private void HandleMorningStarted()
    {
        if (currentHousing == null) return;
        RefreshAll();
    }

    private void HandleResourceChanged(ResourceType type, int newAmount)
    {
        if (currentHousing == null) return;
        RefreshGrowthToggleState();
        RefreshGrowthProgressUI();
    }

    // =========================================================================
    // ODŚWIEŻANIE WIDOKU (REFRESH)
    // =========================================================================

    public void RefreshAll()
    {
        if (currentHousing == null) return;

        RefreshHeaderUI();
        RefreshPopulationUI();
        RefreshUpkeepUI();
        RefreshGrowthProgressUI();
    }

    private void RefreshHeaderUI()
    {
        if (txtBuildingName != null)
        {
            string name = currentHousing.data != null ? currentHousing.data.buildingName : "CHATA";
            txtBuildingName.text = name.ToUpper();
        }
    }

    private void RefreshPopulationUI()
    {
        var data = currentHousing.housingData;
        if (data == null) return;

        int currentPop = currentHousing.residents != null ? currentHousing.residents.Count : 0;
        int maxPop = currentHousing.GetEffectiveMaxResidents();

        // Ikona rasy
        if (imgRaceIcon != null && resourceVisualsSO != null)
        {
            imgRaceIcon.sprite = resourceVisualsSO.GetIcon(data.housingRace);
            imgRaceIcon.color = imgRaceIcon.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        }

        // Nazwa rasy
        if (txtRaceName != null)
        {
            txtRaceName.text = GetRaceDisplayName(data.housingRace);
        }

        // Licznik mieszkańców
        if (txtPopulationCount != null)
        {
            txtPopulationCount.text = $"Mieszkańcy: {currentPop} / {maxPop}";
        }

        // Pasek zapełnienia miejsc
        if (imgCapacityFill != null)
        {
            float fillRatio = maxPop > 0 ? Mathf.Clamp01((float)currentPop / maxPop) : 0f;
            imgCapacityFill.anchorMin = Vector2.zero;
            imgCapacityFill.anchorMax = new Vector2(fillRatio, 1f);
            imgCapacityFill.sizeDelta = Vector2.zero;
            imgCapacityFill.anchoredPosition = Vector2.zero;
        }
    }

    private void RefreshUpkeepUI()
    {
        var data = currentHousing.housingData;
        if (data == null) return;

        int currentPop = currentHousing.residents != null ? currentHousing.residents.Count : 0;

        // ── 1. Kafelki kosztów na osobę (Pnl_ResidentUpkeepItems) ───────────
        if (pnlResidentUpkeepItems != null)
        {
            var perCapitaList = data.upkeepPerResident ?? new List<HousingBuildingData.PerCapitaCost>();
            int activeItems = 0;

            for (int i = 0; i < perCapitaList.Count; i++)
            {
                var cost = perCapitaList[i];
                Transform itemTr;

                if (i < pnlResidentUpkeepItems.childCount)
                {
                    itemTr = pnlResidentUpkeepItems.GetChild(i);
                    itemTr.gameObject.SetActive(true);
                }
                else
                {
                    if (pnlResidentUpkeepItems.childCount > 0)
                    {
                        itemTr = Instantiate(pnlResidentUpkeepItems.GetChild(0), pnlResidentUpkeepItems, false);
                        itemTr.gameObject.SetActive(true);
                    }
                    else break;
                }

                // Ikona
                Image iconImg = itemTr.Find("Img_ResourceIcon")?.GetComponent<Image>();
                if (iconImg != null && resourceVisualsSO != null)
                {
                    iconImg.sprite = resourceVisualsSO.GetIcon(cost.type);
                    iconImg.color = iconImg.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                }

                // Wartość (stawka per osoba)
                TextMeshProUGUI amtTmp = itemTr.Find("Txt_Amount")?.GetComponent<TextMeshProUGUI>();
                if (amtTmp != null)
                {
                    amtTmp.text = $"-{cost.amount:0.#} / os.";
                }

                activeItems++;
            }

            for (int i = activeItems; i < pnlResidentUpkeepItems.childCount; i++)
            {
                pnlResidentUpkeepItems.GetChild(i).gameObject.SetActive(false);
            }
        }

        // ── 2. Etykieta sumaryczna łącznego kosztu domu ─────────────────────
        if (txtResidentUpkeepTotal != null)
        {
            var summaryList = new List<string>();
            var survivalCosts = currentHousing.CalculateSurvivalCost();

            foreach (var cost in survivalCosts)
            {
                if (cost.amount > 0)
                {
                    summaryList.Add($"-{cost.amount} {GetResourcePolishName(cost.type)}");
                }
            }

            if (summaryList.Count > 0)
            {
                txtResidentUpkeepTotal.text = $"Łączny koszt: {string.Join(", ", summaryList)} / świt";
            }
            else
            {
                txtResidentUpkeepTotal.text = "Łączny koszt: brak kosztów utrzymania";
            }
        }

        // ── 3. Przycisk przełącznika wzrostu ────────────────────────────────
        RefreshGrowthToggleState();

        // ── 4. Kafelki kosztów wzrostu (Pnl_GrowthCostItems) ─────────────────
        if (pnlGrowthCostItems != null)
        {
            var growthCosts = data.growthSurplusCost ?? new List<ResourceCost>();
            int activeGrowthItems = 0;

            for (int i = 0; i < growthCosts.Count; i++)
            {
                var cost = growthCosts[i];
                Transform itemTr;

                if (i < pnlGrowthCostItems.childCount)
                {
                    itemTr = pnlGrowthCostItems.GetChild(i);
                    itemTr.gameObject.SetActive(true);
                }
                else
                {
                    if (pnlGrowthCostItems.childCount > 0)
                    {
                        itemTr = Instantiate(pnlGrowthCostItems.GetChild(0), pnlGrowthCostItems, false);
                        itemTr.gameObject.SetActive(true);
                    }
                    else break;
                }

                Image iconImg = itemTr.Find("Img_ResourceIcon")?.GetComponent<Image>();
                if (iconImg != null && resourceVisualsSO != null)
                {
                    iconImg.sprite = resourceVisualsSO.GetIcon(cost.type);
                    iconImg.color = iconImg.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                }

                TextMeshProUGUI amtTmp = itemTr.Find("Txt_Amount")?.GetComponent<TextMeshProUGUI>();
                if (amtTmp != null)
                {
                    amtTmp.text = $"-{cost.amount}";
                }

                activeGrowthItems++;
            }

            for (int i = activeGrowthItems; i < pnlGrowthCostItems.childCount; i++)
            {
                pnlGrowthCostItems.GetChild(i).gameObject.SetActive(false);
            }
        }
    }

    private void RefreshGrowthToggleState()
    {
        if (btnToggleGrowth == null) return;

        int currentPop = currentHousing.residents != null ? currentHousing.residents.Count : 0;
        int maxPop = currentHousing.GetEffectiveMaxResidents();

        if (currentPop >= maxPop)
        {
            // Dom pełny - przycisk nieaktywny
            btnToggleGrowth.interactable = false;
            if (txtGrowthStatusLabel != null)
            {
                txtGrowthStatusLabel.text = "WZROST: PEŁNY";
                txtGrowthStatusLabel.color = growthFullTxt;
            }
            if (imgGrowthStatusBackground != null)
            {
                imgGrowthStatusBackground.color = growthFullBg;
            }
        }
        else if (currentHousing.stopGrowth)
        {
            // Gracz wstrzymał wzrost
            btnToggleGrowth.interactable = true;
            if (txtGrowthStatusLabel != null)
            {
                txtGrowthStatusLabel.text = "WZROST: WSTRZYMANY";
                txtGrowthStatusLabel.color = growthPausedTxt;
            }
            if (imgGrowthStatusBackground != null)
            {
                imgGrowthStatusBackground.color = growthPausedBg;
            }
        }
        else
        {
            // Wzrost aktywny
            btnToggleGrowth.interactable = true;
            if (txtGrowthStatusLabel != null)
            {
                txtGrowthStatusLabel.text = "WZROST: AKTYWNY";
                txtGrowthStatusLabel.color = growthActiveTxt;
            }
            if (imgGrowthStatusBackground != null)
            {
                imgGrowthStatusBackground.color = growthActiveBg;
            }
        }
    }

    private void RefreshGrowthProgressUI()
    {
        if (currentHousing == null) return;

        int currentPop = currentHousing.residents != null ? currentHousing.residents.Count : 0;
        int maxPop = currentHousing.GetEffectiveMaxResidents();
        bool isFull = currentPop >= maxPop;

        int requiredTicks = currentHousing.CalculateRequiredGrowthTicks();
        int currentTicks = isFull ? requiredTicks : currentHousing.currentGrowthProgress;
        float progressRatio = isFull ? 1f : currentHousing.GetGrowthProgress();

        // Liczba wymaganych cykli
        if (txtCyclesRequired != null)
        {
            txtCyclesRequired.text = isFull ? "WYMAGANE: PEŁNY" : $"WYMAGANE: {requiredTicks} CYKLE";
        }

        // Pasek postępu
        if (imgGrowthProgressFill != null)
        {
            imgGrowthProgressFill.anchorMin = Vector2.zero;
            imgGrowthProgressFill.anchorMax = new Vector2(Mathf.Clamp01(progressRatio), 1f);
            imgGrowthProgressFill.sizeDelta = Vector2.zero;
            imgGrowthProgressFill.anchoredPosition = Vector2.zero;
        }

        // Etykieta statusu postępu
        if (txtProgressStatus != null)
        {
            if (isFull)
            {
                txtProgressStatus.text = "Maksymalna populacja osiągnięta";
                txtProgressStatus.color = new Color(0.45f, 0.95f, 0.55f, 1f);
            }
            else
            {
                int percentage = Mathf.RoundToInt(progressRatio * 100f);
                string growthStatus = currentHousing.GetGrowthStatus();

                if (growthStatus == "GŁÓD (-1 CYKL)" || growthStatus == "BRAK ZASOBÓW")
                {
                    txtProgressStatus.text = $"Postęp: {currentTicks} z {requiredTicks} cykli ({percentage}%) - <color=#FF6666>{growthStatus}</color>";
                }
                else if (growthStatus == "BRAK NADWYŻKI")
                {
                    txtProgressStatus.text = $"Postęp: {currentTicks} z {requiredTicks} cykli ({percentage}%) - <color=#FF9944>BRAK NADWYŻKI</color>";
                }
                else if (growthStatus == "WSTRZYMANY")
                {
                    txtProgressStatus.text = $"Postęp: {currentTicks} z {requiredTicks} cykli ({percentage}%) - <color=#FFCC66>WSTRZYMANY</color>";
                }
                else
                {
                    txtProgressStatus.text = $"Postęp: {currentTicks} z {requiredTicks} cykli ({percentage}%)";
                }
                txtProgressStatus.color = Color.white;
            }
        }

        // Etykieta tempa
        if (txtProgressRate != null)
        {
            txtProgressRate.text = "1 cykl / świt (gdy zapewnione zasoby)";
        }
    }

    // =========================================================================
    // AKCJE GRACZA
    // =========================================================================

    public void HandleToggleGrowth()
    {
        if (currentHousing == null) return;

        int currentPop = currentHousing.residents != null ? currentHousing.residents.Count : 0;
        int maxPop = currentHousing.GetEffectiveMaxResidents();
        if (currentPop >= maxPop) return;

        currentHousing.stopGrowth = !currentHousing.stopGrowth;
        RefreshGrowthToggleState();
        RefreshGrowthProgressUI();
    }

    public void HandleDemolishClick()
    {
        if (currentHousing == null) return;

        if (!isDemolishConfirming)
        {
            // Krok 1: Oczekiwanie na potwierdzenie
            isDemolishConfirming = true;
            if (txtDemolishLabel != null) txtDemolishLabel.text = "NA PEWNO?";
            if (imgDemolishBackground != null) imgDemolishBackground.color = demolishConfirmBg;
            if (txtDemolishLabel != null) txtDemolishLabel.color = demolishConfirmTxt;
        }
        else
        {
            // Krok 2: Faktyczne wyburzenie
            isDemolishConfirming = false;
            var housingToDemolish = currentHousing;
            CloseWindow();
            housingToDemolish.Demolish();
        }
    }

    private void ResetDemolishState()
    {
        isDemolishConfirming = false;
        if (txtDemolishLabel != null)
        {
            txtDemolishLabel.text = "WYBURZ";
            txtDemolishLabel.color = demolishNormalTxt;
        }
        if (imgDemolishBackground != null)
        {
            imgDemolishBackground.color = demolishNormalBg;
        }
    }

    public void CloseWindow()
    {
        UnsubscribeEvents();
        ResetDemolishState();

        if (CenterInfoPanelControllerUI.Instance != null)
        {
            CenterInfoPanelControllerUI.Instance.HideAll();
        }

        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.DeselectAll();
        }
    }

    // =========================================================================
    // PRZEBUDOWA UKŁADU (LAYOUT REBUILD)
    // =========================================================================

    public void ForceRebuildLayout()
    {
        if (gameObject.activeInHierarchy)
        {
            if (layoutCoroutine != null) StopCoroutine(layoutCoroutine);
            layoutCoroutine = StartCoroutine(RebuildLayoutRoutine());
        }
        else
        {
            ForceRebuildImmediate();
        }
    }

    private IEnumerator RebuildLayoutRoutine()
    {
        ForceRebuildImmediate();
        yield return null;
        ForceRebuildImmediate();
        layoutCoroutine = null;
    }

    public void ForceRebuildImmediate()
    {
        var rt = transform as RectTransform;
        if (rt == null) return;

        Canvas.ForceUpdateCanvases();

        var layoutGroups = rt.GetComponentsInChildren<LayoutGroup>(true);
        for (int i = layoutGroups.Length - 1; i >= 0; i--)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutGroups[i].GetComponent<RectTransform>());
        }

        var fitters = rt.GetComponentsInChildren<ContentSizeFitter>(true);
        for (int i = fitters.Length - 1; i >= 0; i--)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(fitters[i].GetComponent<RectTransform>());
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        Canvas.ForceUpdateCanvases();
    }

    // =========================================================================
    // POMOCNICZE
    // =========================================================================

    private string GetRaceDisplayName(Race race)
    {
        return race switch
        {
            Race.Humans => "LUDZIE",
            Race.Elves => "ELFY",
            Race.Dwarves => "KRASNOLUDY",
            _ => race.ToString().ToUpper()
        };
    }

    private string GetResourcePolishName(ResourceType type)
    {
        return type switch
        {
            ResourceType.Gold => "Złoto",
            ResourceType.Wood => "Drewno",
            ResourceType.Stone => "Kamień",
            ResourceType.Iron => "Żelazo",
            ResourceType.Coal => "Węgiel",
            ResourceType.Food => "Żywność",
            _ => type.ToString()
        };
    }

    private void AddToDict(Dictionary<ResourceType, int> d, ResourceType t, int a)
    {
        if (d.ContainsKey(t)) d[t] += a;
        else d.Add(t, a);
    }
}
