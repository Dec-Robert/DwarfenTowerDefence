using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kontroler modalu ekspedycji i zwiadu chunka (Pnl_ChunkExpeditionModal).
/// Odpowiada za:
/// - Przełączanie zakładek: EKSPEDYCJA (pełna) i ZWIAD TAKTYCZNY (1-dniowy),
/// - Prognozę terenu (mgła vs odkryte biomy po zwiadzie),
/// - Selektor liczby elfów (stepper [-/+]),
/// - Dynamiczne przeliczanie czasu trwania oraz kosztów (Kryształ Eteru i żywność),
/// - Walidację braku surowców/elfów/sprzętu z wyszarzeniem przycisku i czerwonym oznaczeniem braków,
/// - Wysłanie wyprawy i automatyczne zamknięcie modalu.
/// </summary>
public class ChunkExpeditionModalUI : MonoBehaviour
{
    // ── 1. Zakładki i Nagłówek ────────────────────────────────────────────────
    [Header("Zakładki")]
    [SerializeField] private Button btnTabSurvey;
    [SerializeField] private TextMeshProUGUI txtTabSurveyLabel;
    [SerializeField] private Image imgTabSurveyBg;
    [SerializeField] private Button btnTabRecon;
    [SerializeField] private TextMeshProUGUI txtTabReconLabel;
    [SerializeField] private Image imgTabReconBg;

    [Header("Nagłówek Modalu")]
    [SerializeField] private TextMeshProUGUI txtTitle;
    [SerializeField] private Button btnClose;

    // ── 2. Widok Ekspedycji (View_Survey) ────────────────────────────────────
    [Header("Widok Ekspedycji")]
    [SerializeField] private GameObject viewSurvey;

    // Prognoza mgły
    [SerializeField] private GameObject pnlUnscoutedState;
    [SerializeField] private GameObject pnlScoutedState;
    [SerializeField] private Transform pnlScoutedTilesContainer;
    [SerializeField] private GameObject featureTilePrefab;

    // Stan sprzętu
    [SerializeField] private Transform pnlSurveySlotsRow;
    [SerializeField] private TextMeshProUGUI txtSurveyUsageSummary;

    // Drużyna (Elfy)
    [SerializeField] private Button btnElfMinus;
    [SerializeField] private TextMeshProUGUI txtElfCount;
    [SerializeField] private Button btnElfPlus;
    [SerializeField] private TextMeshProUGUI txtFreeWorkers;

    // Podsumowanie parametrów
    [SerializeField] private TextMeshProUGUI txtSurveyNeededElves;
    [SerializeField] private TextMeshProUGUI txtSurveyDuration;

    // Koszty
    [SerializeField] private Transform pnlSurveyCostRow;
    [SerializeField] private Button btnSendSurvey;
    [SerializeField] private TextMeshProUGUI txtSendSurveyLabel;

    // ── 3. Widok Zwiadu (View_Recon) ─────────────────────────────────────────
    [Header("Widok Zwiadu")]
    [SerializeField] private GameObject viewRecon;
    [SerializeField] private TextMeshProUGUI txtReconDesc;
    [SerializeField] private TextMeshProUGUI txtReconNeededElves;
    [SerializeField] private TextMeshProUGUI txtReconDuration;
    [SerializeField] private Transform pnlReconCostRow;
    [SerializeField] private Button btnSendRecon;
    [SerializeField] private TextMeshProUGUI txtSendReconLabel;

    [Header("Zasoby i Prefabs")]
    [SerializeField] private ResourceVisualsSO resourceVisuals;
    [SerializeField] private GameObject slotBadgePrefab;

    // Paleta kolorów
    private static readonly Color ColorTabActive   = new Color(0.125f, 0.125f, 0.141f, 1.0f); // #202024
    private static readonly Color ColorTabInactive = new Color(0.086f, 0.086f, 0.098f, 0.8f); // #161619
    private static readonly Color ColorGreenActive = new Color(0.180f, 0.800f, 0.443f, 1.0f); // #2ECC71
    private static readonly Color ColorBlueActive  = new Color(0.000f, 0.600f, 1.000f, 1.0f); // #0099FF
    private static readonly Color ColorTextWhite   = new Color(0.960f, 0.960f, 0.960f, 1.0f);
    private static readonly Color ColorTextMuted   = new Color(0.557f, 0.557f, 0.576f, 1.0f);
    private static readonly Color ColorTextRed     = new Color(0.950f, 0.260f, 0.210f, 1.0f); // #F44336
    private static readonly Color ColorDisabledBg  = new Color(0.240f, 0.240f, 0.260f, 0.6f);

    private Vector2Int currentChunk;
    private int assignedElves = 2;
    private bool isSurveyTabActive = true;

    private void Awake()
    {
        AutoBindReferences();
    }

    private void OnEnable()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceChanged += HandleResourceChanged;

        if (WorkforceManager.Instance != null)
            WorkforceManager.Instance.OnWorkforceStatsChanged += HandleWorkforceChanged;
    }

    private void OnDisable()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceChanged -= HandleResourceChanged;

        if (WorkforceManager.Instance != null)
            WorkforceManager.Instance.OnWorkforceStatsChanged -= HandleWorkforceChanged;
    }

    private void HandleResourceChanged(ResourceType type, int amount)
    {
        RefreshView();
    }

    private void HandleWorkforceChanged(Race race, WorkforceManager.WorkforceStats stats)
    {
        if (race == Race.Elves)
        {
            RefreshView();
        }
    }

    // =========================================================================
    // AUTO-BINDING REFERENCJI
    // =========================================================================

    public void AutoBindReferences()
    {
        // 1. Pasek zakładek
        Transform tabsBar = transform.Find("Pnl_TabsBar");
        if (tabsBar != null)
        {
            if (btnTabSurvey == null) btnTabSurvey = tabsBar.Find("Btn_TabSurvey")?.GetComponent<Button>();
            if (btnTabSurvey != null)
            {
                if (txtTabSurveyLabel == null) txtTabSurveyLabel = btnTabSurvey.transform.Find("Txt_Label")?.GetComponent<TextMeshProUGUI>();
                if (imgTabSurveyBg == null) imgTabSurveyBg = btnTabSurvey.GetComponent<Image>();
            }

            if (btnTabRecon == null) btnTabRecon = tabsBar.Find("Btn_TabRecon")?.GetComponent<Button>();
            if (btnTabRecon != null)
            {
                if (txtTabReconLabel == null) txtTabReconLabel = btnTabRecon.transform.Find("Txt_Label")?.GetComponent<TextMeshProUGUI>();
                if (imgTabReconBg == null) imgTabReconBg = btnTabRecon.GetComponent<Image>();
            }
        }

        // 2. Karta główna (Pnl_WindowCard)
        Transform card = transform.Find("Pnl_WindowCard") ?? transform;

        // TopBar
        Transform topBar = card.Find("Pnl_TopBar");
        if (topBar != null)
        {
            if (txtTitle == null) txtTitle = topBar.Find("Txt_Title")?.GetComponent<TextMeshProUGUI>();
            if (btnClose == null) btnClose = topBar.Find("Btn_Close")?.GetComponent<Button>();
        }

        // Widok 1: View_Survey
        if (viewSurvey == null) viewSurvey = card.Find("View_Survey")?.gameObject;
        if (viewSurvey != null)
        {
            // Prognoza
            Transform secForecast = viewSurvey.transform.Find("Sec_Forecast");
            if (secForecast != null)
            {
                Transform fb = secForecast.Find("Pnl_ForecastBox");
                if (fb != null)
                {
                    if (pnlUnscoutedState == null) pnlUnscoutedState = fb.Find("Pnl_UnscoutedState")?.gameObject;
                    if (pnlScoutedState == null) pnlScoutedState = fb.Find("Pnl_ScoutedState")?.gameObject;
                    if (pnlScoutedTilesContainer == null && pnlScoutedState != null)
                        pnlScoutedTilesContainer = pnlScoutedState.transform;
                }
            }

            // Stan ekwipunków
            Transform secGear = viewSurvey.transform.Find("Sec_EquipmentStatus");
            if (secGear != null)
            {
                Transform gc = secGear.Find("Pnl_GearCard");
                if (gc != null)
                {
                    if (txtSurveyUsageSummary == null)
                        txtSurveyUsageSummary = gc.Find("Pnl_GearSubheader/Txt_UsageSummary")?.GetComponent<TextMeshProUGUI>();
                    if (pnlSurveySlotsRow == null)
                        pnlSurveySlotsRow = gc.Find("Pnl_SlotsRow");
                }
            }

            // Drużyna (Elfy)
            Transform secTeam = viewSurvey.transform.Find("Sec_Team");
            if (secTeam != null)
            {
                Transform elfBox = secTeam.Find("Pnl_ScoutController/Pnl_Race_ELVES")
                    ?? secTeam.Find("Pnl_StepperCard");
                if (elfBox != null)
                {
                    Transform btns = elfBox.Find("Pnl_Buttons") ?? elfBox.Find("Pnl_Stepper");
                    if (btns != null)
                    {
                        if (btnElfMinus == null) btnElfMinus = btns.Find("Btn_Minus")?.GetComponent<Button>();
                        if (txtElfCount == null) txtElfCount = btns.Find("Txt_Count")?.GetComponent<TextMeshProUGUI>()
                            ?? btns.Find("Txt_ElfCount")?.GetComponent<TextMeshProUGUI>();
                        if (btnElfPlus == null) btnElfPlus = btns.Find("Btn_Plus")?.GetComponent<Button>();
                    }
                    if (txtFreeWorkers == null)
                        txtFreeWorkers = elfBox.Find("Txt_FreeWorkers")?.GetComponent<TextMeshProUGUI>();
                }
            }

            // Podsumowanie
            Transform summaryRow = viewSurvey.transform.Find("Pnl_SummaryCardsRow");
            if (summaryRow != null)
            {
                if (txtSurveyNeededElves == null)
                    txtSurveyNeededElves = summaryRow.Find("Col_NeededElves/Txt_Value")?.GetComponent<TextMeshProUGUI>()
                        ?? summaryRow.Find("Badge_MIN. SKŁAD/Txt_Value")?.GetComponent<TextMeshProUGUI>();

                if (txtSurveyDuration == null)
                    txtSurveyDuration = summaryRow.Find("Col_Time/Txt_Value")?.GetComponent<TextMeshProUGUI>()
                        ?? summaryRow.Find("Badge_CZAS TRWANIA/Txt_Value")?.GetComponent<TextMeshProUGUI>();
            }

            // Koszty i Akcja
            Transform secCost = viewSurvey.transform.Find("Sec_ReconCost") ?? viewSurvey.transform.Find("Sec_Supplies");
            if (secCost != null)
            {
                if (pnlSurveyCostRow == null)
                    pnlSurveyCostRow = secCost.Find("Pnl_ReconCostRow") ?? secCost.Find("Pnl_CostRow");
            }

            if (btnSendSurvey == null) btnSendSurvey = viewSurvey.transform.Find("Btn_SendSurvey")?.GetComponent<Button>();
            if (btnSendSurvey != null && txtSendSurveyLabel == null)
                txtSendSurveyLabel = btnSendSurvey.transform.Find("Txt_Label")?.GetComponent<TextMeshProUGUI>();
        }

        // Widok 2: View_Recon
        if (viewRecon == null) viewRecon = card.Find("View_Recon")?.gameObject;
        if (viewRecon != null)
        {
            if (txtReconDesc == null)
                txtReconDesc = viewRecon.transform.Find("Pnl_ReconDescBox/Txt_Desc")?.GetComponent<TextMeshProUGUI>();

            Transform reconSummary = viewRecon.transform.Find("Pnl_ReconSummaryCards");
            if (reconSummary != null)
            {
                if (txtReconNeededElves == null)
                    txtReconNeededElves = reconSummary.Find("Col_NeededElves/Txt_Value")?.GetComponent<TextMeshProUGUI>()
                        ?? reconSummary.Find("Badge_PRZYDZIAŁ/Txt_Value")?.GetComponent<TextMeshProUGUI>();

                if (txtReconDuration == null)
                    txtReconDuration = reconSummary.Find("Col_Time/Txt_Value")?.GetComponent<TextMeshProUGUI>()
                        ?? reconSummary.Find("Badge_CZAS ZWIADU/Txt_Value")?.GetComponent<TextMeshProUGUI>();
            }

            Transform secReconCost = viewRecon.transform.Find("Sec_ReconCost");
            if (secReconCost != null && pnlReconCostRow == null)
            {
                pnlReconCostRow = secReconCost.Find("Pnl_ReconCostRow");
            }

            if (btnSendRecon == null) btnSendRecon = viewRecon.transform.Find("Btn_SendRecon")?.GetComponent<Button>();
            if (btnSendRecon != null && txtSendReconLabel == null)
                txtSendReconLabel = btnSendRecon.transform.Find("Txt_Label")?.GetComponent<TextMeshProUGUI>();
        }

        // Zasoby i prefabrykaty
#if UNITY_EDITOR
        if (resourceVisuals == null)
        {
            resourceVisuals = UnityEditor.AssetDatabase.LoadAssetAtPath<ResourceVisualsSO>("Assets/Main folder/ScriptableObjects/Configs/ResourceVisualDatabase.asset");
        }
        if (slotBadgePrefab == null)
        {
            slotBadgePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Main folder/Prefabs/UI/Pnl_ExpeditionEq_Slot.prefab");
            if (slotBadgePrefab == null)
                slotBadgePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Main folder/Prefabs/UI/Pnl_ExpeditionSlotBadge.prefab");
        }
        if (featureTilePrefab == null)
        {
            featureTilePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Main folder/Prefabs/UI/Pnl_ReconFeatureTile.prefab");
        }
#endif
    }

    // =========================================================================
    // INICJALIZACJA I OTWARCIE MODALU
    // =========================================================================

    public void Setup(Vector2Int chunkCoord)
    {
        AutoBindReferences();
        currentChunk = chunkCoord;

        // Ustawienia tytułu
        if (txtTitle != null)
        {
            txtTitle.text = $"EKSPEDYCJA: CHUNK [{chunkCoord.x}, {chunkCoord.y}]";
        }

        // Podpięcie przycisku zamknięcia
        if (btnClose != null)
        {
            btnClose.onClick.RemoveAllListeners();
            btnClose.onClick.AddListener(CloseWindow);
        }

        // Podpięcie zakładek
        if (btnTabSurvey != null)
        {
            btnTabSurvey.onClick.RemoveAllListeners();
            btnTabSurvey.onClick.AddListener(() => SwitchTab(true));
        }

        if (btnTabRecon != null)
        {
            btnTabRecon.onClick.RemoveAllListeners();
            btnTabRecon.onClick.AddListener(() => SwitchTab(false));
        }

        // Stepper elfów
        if (btnElfMinus != null)
        {
            btnElfMinus.onClick.RemoveAllListeners();
            btnElfMinus.onClick.AddListener(DecrementElves);
        }

        if (btnElfPlus != null)
        {
            btnElfPlus.onClick.RemoveAllListeners();
            btnElfPlus.onClick.AddListener(IncrementElves);
        }

        // Przyciski wysyłki
        if (btnSendSurvey != null)
        {
            btnSendSurvey.onClick.RemoveAllListeners();
            btnSendSurvey.onClick.AddListener(HandleSendSurvey);
        }

        if (btnSendRecon != null)
        {
            btnSendRecon.onClick.RemoveAllListeners();
            btnSendRecon.onClick.AddListener(HandleSendRecon);
        }

        // Domyślna liczba elfów
        assignedElves = MapExpansionManager.Instance != null && MapExpansionManager.Instance.costConfig != null
            ? MapExpansionManager.Instance.costConfig.minElvesForSurvey
            : 2;

        // Domyślnie zakładka ekspedycji
        SwitchTab(true);

        RefreshView();
    }

    public void CloseWindow()
    {
        gameObject.SetActive(false);
    }

    // =========================================================================
    // OBSŁUGA ZAKŁADEK (TABS)
    // =========================================================================

    private void SwitchTab(bool isSurvey)
    {
        isSurveyTabActive = isSurvey;

        if (viewSurvey != null) viewSurvey.SetActive(isSurvey);
        if (viewRecon != null) viewRecon.SetActive(!isSurvey);

        // Wizualizacja aktywnej zakładki
        if (imgTabSurveyBg != null)
            imgTabSurveyBg.color = isSurvey ? ColorTabActive : ColorTabInactive;

        if (txtTabSurveyLabel != null)
            txtTabSurveyLabel.color = isSurvey ? ColorGreenActive : ColorTextMuted;

        if (imgTabReconBg != null)
            imgTabReconBg.color = !isSurvey ? ColorTabActive : ColorTabInactive;

        if (txtTabReconLabel != null)
            txtTabReconLabel.color = !isSurvey ? ColorBlueActive : ColorTextMuted;

        RefreshView();
    }

    // =========================================================================
    // STEPPER ELFÓW
    // =========================================================================

    private void DecrementElves()
    {
        int minElves = MapExpansionManager.Instance != null && MapExpansionManager.Instance.costConfig != null
            ? MapExpansionManager.Instance.costConfig.minElvesForSurvey
            : 2;

        if (assignedElves > minElves)
        {
            assignedElves--;
            RefreshView();
        }
    }

    private void IncrementElves()
    {
        int maxAvailableGear = MapExpansionManager.Instance != null ? MapExpansionManager.Instance.AvailableEquipment : 0;
        int maxAvailableElves = WorkforceManager.Instance != null ? WorkforceManager.Instance.GetIdleCount(Race.Elves) : 0;
        int maxLimit = Mathf.Min(maxAvailableGear, maxAvailableElves);

        // Pozwalamy zwiększać do limitu (lub minimum jeśli gracz nie ma jeszcze sprzętu, by widział braki)
        if (assignedElves < Mathf.Max(2, maxLimit))
        {
            assignedElves++;
            RefreshView();
        }
    }

    // =========================================================================
    // ODŚWIEŻANIE WIDOKU I WALIDACJA
    // =========================================================================

    public void RefreshView()
    {
        if (MapExpansionManager.Instance == null) return;

        var config = MapExpansionManager.Instance.costConfig;
        if (config == null) return;

        bool hasRecon = false;
        ChunkReconIntel intel = null;
        if (MapExpansionManager.Instance.activeChunks.TryGetValue(currentChunk, out var chunkData))
        {
            hasRecon = chunkData.hasReconIntel;
            intel = chunkData.reconIntel;
        }

        // Obsługa zakładki zwiadu, jeśli został już ukończony
        if (hasRecon)
        {
            if (btnTabRecon != null) btnTabRecon.interactable = false;
            if (txtTabReconLabel != null)
            {
                txtTabReconLabel.text = "ZWIAD UKOŃCZONY";
                txtTabReconLabel.color = ColorTextMuted;
            }
            if (!isSurveyTabActive)
            {
                SwitchTab(true);
                return;
            }
        }
        else
        {
            if (btnTabRecon != null) btnTabRecon.interactable = true;
            if (txtTabReconLabel != null)
            {
                txtTabReconLabel.text = "ZWIAD TAKTYCZNY";
            }
        }

        // 1. Odświeżenie slotów ekwipunku w oknie
        int totalEq = MapExpansionManager.Instance.TotalEquipment;
        int usedEq = MapExpansionManager.Instance.UsedEquipment;
        if (txtSurveyUsageSummary != null)
        {
            txtSurveyUsageSummary.text = $"{usedEq} / {totalEq} W użyciu";
        }
        RebuildSlotsRow(totalEq, usedEq);

        // 2. Prognoza terenu (Forecast)
        if (pnlUnscoutedState != null) pnlUnscoutedState.SetActive(!hasRecon);
        if (pnlScoutedState != null) pnlScoutedState.SetActive(hasRecon);
        if (hasRecon && intel != null)
        {
            RebuildScoutedFeatureTiles(intel);
        }

        // 3. Widok Ekspedycji
        if (isSurveyTabActive)
        {
            RefreshSurveyView(config);
        }
        else
        {
            RefreshReconView(config);
        }
    }

    private void RefreshSurveyView(ExpansionCostConfig config)
    {
        // Wolne elfy
        int idleElves = WorkforceManager.Instance != null ? WorkforceManager.Instance.GetIdleCount(Race.Elves) : 0;
        int availableGear = MapExpansionManager.Instance.AvailableEquipment;

        if (txtElfCount != null) txtElfCount.text = assignedElves.ToString();
        if (txtFreeWorkers != null)
        {
            txtFreeWorkers.text = $"Dostępne elfy: {idleElves} wolnych";
            txtFreeWorkers.color = idleElves >= assignedElves ? ColorTextWhite : ColorTextRed;
        }

        // Czas trwania, min skład i koszty
        bool isRoad = MapExpansionManager.Instance.activeChunks.TryGetValue(currentChunk, out var cd) && cd.isRoadChunk;
        MissionCost surveyCost = ExpansionCostCalculator.CalculateSurveyCost(
            currentChunk,
            MapExpansionManager.Instance.activeChunks,
            isRoad,
            config,
            assignedElves
        );

        int duration = surveyCost.days;
        if (txtSurveyDuration != null)
        {
            string cycleWord = duration == 1 ? "Cykl" : "Cykle";
            txtSurveyDuration.text = $"{duration} {cycleWord}";
        }

        if (txtSurveyNeededElves != null)
        {
            txtSurveyNeededElves.text = $"{assignedElves} Zwiadowców";
        }

        int etherCost = surveyCost.etherCost;
        int foodCost = surveyCost.foodCost;
        UpdateCostRow(pnlSurveyCostRow, etherCost, foodCost);

        // Walidacja możliwości rozpoczęcia ekspedycji
        bool canStart = MapExpansionManager.Instance.CanStartSurvey(currentChunk, assignedElves, out string reason);
        int currentEther = ResourceManager.Instance != null ? ResourceManager.Instance.GetResourceAmount(ResourceType.Ether) : 0;
        int currentFood = ResourceManager.Instance != null ? ResourceManager.Instance.GetResourceAmount(ResourceType.Food) : 0;

        if (btnSendSurvey != null)
        {
            btnSendSurvey.interactable = canStart;

            if (txtSendSurveyLabel != null)
            {
                if (canStart)
                {
                    txtSendSurveyLabel.text = "WYŚLIJ EKSPEDYCJE";
                    txtSendSurveyLabel.color = ColorTextWhite;
                }
                else
                {
                    txtSendSurveyLabel.color = ColorTextRed;
                    if (currentEther < etherCost || currentFood < foodCost)
                        txtSendSurveyLabel.text = "BRAK ZASOBÓW";
                    else if (idleElves < assignedElves)
                        txtSendSurveyLabel.text = "BRAK WOLNYCH ELFÓW";
                    else if (availableGear < assignedElves)
                        txtSendSurveyLabel.text = "BRAK EKWIPUNKU";
                    else
                        txtSendSurveyLabel.text = reason.ToUpper();
                }
            }
        }
    }

    private void RefreshReconView(ExpansionCostConfig config)
    {
        int idleElves = WorkforceManager.Instance != null ? WorkforceManager.Instance.GetIdleCount(Race.Elves) : 0;
        int availableGear = MapExpansionManager.Instance.AvailableEquipment;

        if (txtReconNeededElves != null)
            txtReconNeededElves.text = $"{config.reconElfRequirement} Zwiadowca";

        if (txtReconDuration != null)
        {
            string cycleWord = config.reconDays == 1 ? "Cykl" : "Cykle";
            txtReconDuration.text = $"{config.reconDays} {cycleWord}";
        }

        // Koszty zwiadu
        MissionCost reconCost = ExpansionCostCalculator.CalculateReconCost(config);
        int etherCost = reconCost.etherCost;
        int foodCost = reconCost.foodCost;
        UpdateCostRow(pnlReconCostRow, etherCost, foodCost);

        // Walidacja zwiadu
        bool canStart = MapExpansionManager.Instance.CanStartRecon(currentChunk, out string reason);
        int currentEther = ResourceManager.Instance != null ? ResourceManager.Instance.GetResourceAmount(ResourceType.Ether) : 0;
        int currentFood = ResourceManager.Instance != null ? ResourceManager.Instance.GetResourceAmount(ResourceType.Food) : 0;

        if (btnSendRecon != null)
        {
            btnSendRecon.interactable = canStart;

            if (txtSendReconLabel != null)
            {
                if (canStart)
                {
                    txtSendReconLabel.text = "WYŚLIJ ZWIAD TAKTYCZNY";
                    txtSendReconLabel.color = ColorTextWhite;
                }
                else
                {
                    txtSendReconLabel.color = ColorTextRed;
                    if (currentEther < etherCost || currentFood < foodCost)
                        txtSendReconLabel.text = "BRAK ZASOBÓW";
                    else if (idleElves < config.reconElfRequirement)
                        txtSendReconLabel.text = "BRAK WOLNYCH ELFÓW";
                    else if (availableGear < config.reconElfRequirement)
                        txtSendReconLabel.text = "BRAK EKWIPUNKU";
                    else
                        txtSendReconLabel.text = reason.ToUpper();
                }
            }
        }
    }

    private void UpdateCostRow(Transform costRow, int etherCost, int foodCost)
    {
        if (costRow == null) return;

        int currentEther = ResourceManager.Instance != null ? ResourceManager.Instance.GetResourceAmount(ResourceType.Ether) : 0;
        int currentFood = ResourceManager.Instance != null ? ResourceManager.Instance.GetResourceAmount(ResourceType.Food) : 0;

        // Szukamy komponentów SingleResourceUI
        var resourceItems = costRow.GetComponentsInChildren<SingleResourceUI>(true);
        if (resourceItems != null && resourceItems.Length >= 2)
        {
            // Kryształ Eteru
            resourceItems[0].Setup(ResourceType.Ether, etherCost, resourceVisuals);
            var etherTxt = resourceItems[0].GetComponentInChildren<TextMeshProUGUI>();
            if (etherTxt != null)
            {
                etherTxt.color = currentEther >= etherCost ? ColorTextWhite : ColorTextRed;
            }

            // Żywność
            resourceItems[1].Setup(ResourceType.Food, foodCost, resourceVisuals);
            var foodTxt = resourceItems[1].GetComponentInChildren<TextMeshProUGUI>();
            if (foodTxt != null)
            {
                foodTxt.color = currentFood >= foodCost ? ColorTextWhite : ColorTextRed;
            }
        }
    }

    private void RebuildSlotsRow(int total, int used)
    {
        if (pnlSurveySlotsRow == null) return;

        for (int i = pnlSurveySlotsRow.childCount - 1; i >= 0; i--)
        {
            Destroy(pnlSurveySlotsRow.GetChild(i).gameObject);
        }

        if (slotBadgePrefab == null || total <= 0) return;

        for (int i = 1; i <= total; i++)
        {
            GameObject slotObj = Instantiate(slotBadgePrefab, pnlSurveySlotsRow);
            slotObj.name = $"Slot_{i}";

            TextMeshProUGUI txtIndex = slotObj.transform.Find("Txt_Index")?.GetComponent<TextMeshProUGUI>()
                ?? slotObj.GetComponentInChildren<TextMeshProUGUI>();
            if (txtIndex != null) txtIndex.text = i.ToString();

            Image bar = slotObj.transform.Find("Img_SlotBar")?.GetComponent<Image>();
            if (bar != null)
            {
                bar.color = (i <= used) ? new Color(0.240f, 0.240f, 0.260f, 1f) : ColorGreenActive;
            }
        }
    }

    private void RebuildScoutedFeatureTiles(ChunkReconIntel intel)
    {
        if (pnlScoutedTilesContainer == null) return;

        for (int i = pnlScoutedTilesContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(pnlScoutedTilesContainer.GetChild(i).gameObject);
        }

        if (featureTilePrefab == null || intel == null) return;

        CreateFeatureTile("Las :", intel.GetAbundance(HexFeatureType.Forest));
        CreateFeatureTile("Żyzna gleba :", intel.GetAbundance(HexFeatureType.FertileSoil));
        CreateFeatureTile("Góry :", intel.GetAbundance(HexFeatureType.Mountain));
    }

    private void CreateFeatureTile(string featureName, ReconAbundance abundance)
    {
        GameObject tile = Instantiate(featureTilePrefab, pnlScoutedTilesContainer);
        tile.name = $"Tile_{featureName.Trim(' ', ':')}";

        TextMeshProUGUI txtName = tile.transform.Find("Txt_FeatureName")?.GetComponent<TextMeshProUGUI>();
        if (txtName != null) txtName.text = featureName;

        TextMeshProUGUI txtAbundance = tile.transform.Find("Txt_Abundance")?.GetComponent<TextMeshProUGUI>();
        if (txtAbundance != null)
        {
            txtAbundance.text = abundance switch
            {
                ReconAbundance.Abundant => "obfite",
                ReconAbundance.Moderate => "umiarkowane",
                ReconAbundance.Scarce   => "znikome",
                _                       => "brak"
            };

            txtAbundance.color = abundance switch
            {
                ReconAbundance.Abundant => ColorGreenActive,
                ReconAbundance.Moderate => new Color(0.2f, 0.7f, 1.0f, 1f),
                ReconAbundance.Scarce   => new Color(0.95f, 0.75f, 0.2f, 1f),
                _                       => ColorTextMuted
            };
        }
    }

    // =========================================================================
    // WYWOŁANIA AKCJI WYPRAWY
    // =========================================================================

    private void HandleSendSurvey()
    {
        if (MapExpansionManager.Instance == null) return;

        bool success = MapExpansionManager.Instance.TryStartSurvey(currentChunk, assignedElves);
        if (success)
        {
            CloseWindow();
        }
        else
        {
            RefreshView();
        }
    }

    private void HandleSendRecon()
    {
        if (MapExpansionManager.Instance == null) return;

        bool success = MapExpansionManager.Instance.TryStartRecon(currentChunk);
        if (success)
        {
            CloseWindow();
        }
        else
        {
            RefreshView();
        }
    }
}
