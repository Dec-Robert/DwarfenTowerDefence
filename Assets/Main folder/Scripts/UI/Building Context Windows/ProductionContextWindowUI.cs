using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Główny komponent zarządzający oknem kontekstowym budynków produkcyjnych (Pnl_ProductionContext).
/// Obsługuje: zapotrzebowanie na surowce (Upkeep), przydział załogi 3 ras, dynamiczne progi wydajności,
/// bilans kalkulacji produkcji z kafelkami surowców, wstrzymywanie/wznawianie (Pnl_Stopped) oraz wyburzanie.
/// </summary>
public class ProductionContextWindowUI : MonoBehaviour
{
    [System.Serializable]
    public class RaceControlView
    {
        public Race race;
        public GameObject rootBox;
        public TextMeshProUGUI txtTitle;
        public TextMeshProUGUI txtCount;
        public TextMeshProUGUI txtFree;
        public Button btnMinus;
        public Button btnPlus;
    }

    [System.Serializable]
    public class ThresholdSegmentView
    {
        public GameObject rootObject;
        public LayoutElement layoutElement;
        public TextMeshProUGUI txtMarker;
        public Image imgBar;
        public TextMeshProUGUI txtLevel;
        public TextMeshProUGUI txtPercentage;
    }

    public class ResourceColumnView
    {
        public GameObject rootObject;
        public RectTransform rectTransform;
        public Image imgResourceIcon;

        public GameObject rowBase;
        public TextMeshProUGUI txtBaseValue;

        public GameObject rowOnTopBonus;
        public TextMeshProUGUI txtOnTopValue;

        public GameObject rowAdjacentBonus;
        public TextMeshProUGUI txtAdjacentValue;

        public GameObject rowExternalMultipliers;
        public TextMeshProUGUI txtExternalMultiplierValue;

        public GameObject rowCrewEfficiency;
        public TextMeshProUGUI txtCrewEfficiencyValue;
    }

    // ── Główne referencje okna ───────────────────────────────────────────────
    [Header("Nagłówek i Ogólne")]
    [SerializeField] private TextMeshProUGUI txtBuildingName;
    [SerializeField] private Button btnClose;
    [SerializeField] private ResourceVisualsSO resourceVisualsSO;

    // ── Sekcja Utrzymania (Upkeep) ─────────────────────────────────────────────
    [Header("Utrzymanie (Upkeep)")]
    [SerializeField] private GameObject secUpkeep;
    [SerializeField] private Transform pnlUpkeepResources;

    // ── Sekcja Załogi i Wydajności ────────────────────────────────────────────
    [Header("Załoga i Wydajność")]
    [SerializeField] private GameObject secWorkers;
    [SerializeField] private TextMeshProUGUI txtWorkerCount;
    [SerializeField] private TextMeshProUGUI txtEfficiencyPercentage;
    [SerializeField] private List<RaceControlView> raceControls = new List<RaceControlView>();

    [Header("Progi Wydajności")]
    [SerializeField] private GameObject secEfficiencyThresholds;
    [SerializeField] private TextMeshProUGUI txtThresholdLabel;
    [SerializeField] private TextMeshProUGUI txtCurrentThresholdStatus;
    [SerializeField] private Transform pnlThresholdBarsContainer;
    [SerializeField] private List<ThresholdSegmentView> thresholdSegments = new List<ThresholdSegmentView>();

    // ── Sekcja Bilansu Produkcji (Tabela Kolumn Surowców) ────────────────────
    [Header("Bilans Produkcji")]
    [SerializeField] private GameObject pnlProductionResourceCard;
    [SerializeField] private Transform pnlModifiersList;
    [SerializeField] private Transform pnlStaticText;
    [SerializeField] private GameObject resourceColumnPrefab;
    [SerializeField] private Transform rowNetSummary;
    [SerializeField] private TextMeshProUGUI txtNetSummaryLabel;

    // Etykiety wierszy w Pnl_StaticText
    [SerializeField] private GameObject lblRowBase;
    [SerializeField] private GameObject lblRowOnTopBonus;
    [SerializeField] private GameObject lblRowAdjacentBonus;
    [SerializeField] private GameObject lblRowExternalMult;
    [SerializeField] private GameObject lblRowCrewEfficiency;

    // ── Dolna Belka i Nakładka Wstrzymania ────────────────────────────────────
    [Header("Akcje i Wstrzymanie")]
    [SerializeField] private GameObject pnlStopped;
    [SerializeField] private TextMeshProUGUI txtStopped;
    [SerializeField] private Button btnPause;
    [SerializeField] private TextMeshProUGUI txtPauseLabel;
    [SerializeField] private Image imgPauseBackground;
    [SerializeField] private Button btnDemolish;
    [SerializeField] private TextMeshProUGUI txtDemolishLabel;
    [SerializeField] private Image imgDemolishBackground;

    // ── Stan bieżącego budynku ────────────────────────────────────────────────
    private BuildingEntity currentBuilding;
    private bool isDemolishConfirming;
    private readonly List<ResourceColumnView> cachedResourceColumns = new List<ResourceColumnView>();
    private readonly List<TextMeshProUGUI> cachedNetSummaryValues = new List<TextMeshProUGUI>();

    // Kolory wartości w kolumnach
    private readonly Color colBaseColor = new Color(1f, 0.85f, 0.3f, 1f);
    private readonly Color colOnTopColor = new Color(0.45f, 0.75f, 1f, 1f);
    private readonly Color colSurroundingColor = new Color(0.4f, 0.85f, 0.45f, 1f);
    private readonly Color colExtMultColor = new Color(0.4f, 0.85f, 0.45f, 1f);
    private readonly Color colEfficiencyColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    private readonly Color colNeutralDashColor = new Color(0.55f, 0.58f, 0.62f, 0.75f);
    private readonly Color netActiveColor = new Color(0.35f, 0.9f, 0.45f, 1f);
    private readonly Color netStoppedColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);

    // Kolory przycisków
    private readonly Color pauseActiveBg = new Color(0.28f, 0.22f, 0.12f, 0.95f);
    private readonly Color pauseActiveTxt = new Color(1f, 0.85f, 0.4f, 1f);
    private readonly Color pauseStoppedBg = new Color(0.12f, 0.28f, 0.16f, 0.95f);
    private readonly Color pauseStoppedTxt = new Color(0.4f, 0.95f, 0.5f, 1f);

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

    private Coroutine layoutCoroutine;

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
    /// Automatycznie wyszukuje i wiąże komponenty po nazwach w hierarchii Pnl_ProductionContext,
    /// jeśli którekolwiek z pól jest puste.
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

        // 3. Utrzymanie
        if (secUpkeep == null)
            secUpkeep = transform.Find("Pnl_ColumnsContainer/Pnl_LeftColumn/Sec_Upkeep")?.gameObject;

        if (pnlUpkeepResources == null)
            pnlUpkeepResources = transform.Find("Pnl_ColumnsContainer/Pnl_LeftColumn/Sec_Upkeep/Pnl_UpkeepResources");

        // 4. Załoga (Sec_Workers)
        if (secWorkers == null)
            secWorkers = transform.Find("Pnl_ColumnsContainer/Pnl_LeftColumn/Sec_Workers")?.gameObject;

        if (secWorkers != null)
        {
            if (txtWorkerCount == null)
                txtWorkerCount = secWorkers.transform.Find("Pnl_WorkersSummary/Txt_WorkerCount")?.GetComponent<TextMeshProUGUI>();

            if (txtEfficiencyPercentage == null)
                txtEfficiencyPercentage = secWorkers.transform.Find("Pnl_WorkersSummary/Txt_EfficiencyPercentage")?.GetComponent<TextMeshProUGUI>();

            AutoBindRaceControls();

            // Progi wydajności
            if (secEfficiencyThresholds == null)
                secEfficiencyThresholds = secWorkers.transform.Find("Sec_EfficiencyThresholds")?.gameObject;

            if (txtThresholdLabel == null)
                txtThresholdLabel = secWorkers.transform.Find("Sec_EfficiencyThresholds/Pnl_ThresholdHeader/Txt_ThresholdLabel")?.GetComponent<TextMeshProUGUI>();

            if (txtCurrentThresholdStatus == null)
                txtCurrentThresholdStatus = secWorkers.transform.Find("Sec_EfficiencyThresholds/Pnl_ThresholdHeader/Txt_CurrentThresholdStatus")?.GetComponent<TextMeshProUGUI>();

            if (pnlThresholdBarsContainer == null)
                pnlThresholdBarsContainer = secWorkers.transform.Find("Sec_EfficiencyThresholds/Pnl_ThresholdBarsContainer");

            AutoBindThresholdSegments();
        }

        // 5. Produkcja (Pnl_ProductionResourceCard + Pnl_ModifiersList + Row_NetSummary)
        if (pnlProductionResourceCard == null)
            pnlProductionResourceCard = transform.Find("Pnl_ColumnsContainer/Pnl_RightColumn/Pnl_ProductionResourceCard")?.gameObject;

        if (pnlProductionResourceCard != null)
        {
            if (pnlModifiersList == null)
                pnlModifiersList = pnlProductionResourceCard.transform.Find("Pnl_ModifiersList");

            if (pnlModifiersList != null && pnlStaticText == null)
                pnlStaticText = pnlModifiersList.Find("Pnl_StaticText");

            if (pnlStaticText != null)
            {
                if (lblRowBase == null)
                    lblRowBase = pnlStaticText.Find("Txt_Base")?.gameObject;
                if (lblRowOnTopBonus == null)
                    lblRowOnTopBonus = pnlStaticText.Find("Txt_OnTopBonus")?.gameObject;
                if (lblRowAdjacentBonus == null)
                    lblRowAdjacentBonus = pnlStaticText.Find("Txt_AdjacentBonus")?.gameObject;
                if (lblRowExternalMult == null)
                    lblRowExternalMult = pnlStaticText.Find("Txt_ExternalMult")?.gameObject;
                if (lblRowCrewEfficiency == null)
                    lblRowCrewEfficiency = (pnlStaticText.Find("Txt_CrewEfficency") ?? pnlStaticText.Find("Txt_CrewEfficiency"))?.gameObject;
            }

            if (rowNetSummary == null)
                rowNetSummary = pnlProductionResourceCard.transform.Find("Row_NetSummary");

            if (rowNetSummary != null && txtNetSummaryLabel == null)
                txtNetSummaryLabel = rowNetSummary.Find("Txt_Label")?.GetComponent<TextMeshProUGUI>();
        }

        if (resourceColumnPrefab == null)
        {
#if UNITY_EDITOR
            resourceColumnPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Main folder/Prefabs/UI/Pnl_ResourceColumn.prefab");
#endif
        }

        // 6. Nakładka Pnl_Stopped
        if (pnlStopped == null)
            pnlStopped = transform.Find("Pnl_Stopped")?.gameObject;

        if (pnlStopped != null && txtStopped == null)
            txtStopped = pnlStopped.transform.Find("Txt_Stopped")?.GetComponent<TextMeshProUGUI>();

        // 7. Dolna belka akcji
        if (btnPause == null)
            btnPause = transform.Find("Pnl_BottomBar/Btn_Pause")?.GetComponent<Button>();

        if (btnPause != null)
        {
            if (txtPauseLabel == null)
                txtPauseLabel = btnPause.transform.Find("Txt_Label")?.GetComponent<TextMeshProUGUI>();
            if (imgPauseBackground == null)
                imgPauseBackground = btnPause.GetComponent<Image>();
        }

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

    private void AutoBindRaceControls()
    {
        if (secWorkers == null) return;

        if (raceControls == null || raceControls.Count == 0)
        {
            raceControls = new List<RaceControlView>
            {
                new RaceControlView { race = Race.Dwarves },
                new RaceControlView { race = Race.Humans },
                new RaceControlView { race = Race.Elves }
            };
        }

        Transform raceParent = secWorkers.transform.Find("Pnl_RaceControllers");
        if (raceParent == null) return;

        foreach (var rc in raceControls)
        {
            string boxNameUpper = "Pnl_Race_" + rc.race.ToString().ToUpper();
            string boxNameDefault = "Pnl_Race_" + rc.race;
            Transform box = raceParent.Find(boxNameUpper) ?? raceParent.Find(boxNameDefault);

            if (box != null)
            {
                rc.rootBox = box.gameObject;
                if (rc.txtTitle == null) rc.txtTitle = box.Find("Txt_RaceTitle")?.GetComponent<TextMeshProUGUI>();
                if (rc.txtFree == null) rc.txtFree = box.Find("Txt_FreeWorkers")?.GetComponent<TextMeshProUGUI>();

                Transform btnRow = box.Find("Pnl_Buttons");
                if (btnRow != null)
                {
                    if (rc.btnMinus == null) rc.btnMinus = btnRow.Find("Btn_Minus")?.GetComponent<Button>();
                    if (rc.txtCount == null) rc.txtCount = btnRow.Find("Txt_Count")?.GetComponent<TextMeshProUGUI>();
                    if (rc.btnPlus == null) rc.btnPlus = btnRow.Find("Btn_Plus")?.GetComponent<Button>();
                }
            }
        }
    }

    private void AutoBindThresholdSegments()
    {
        if (pnlThresholdBarsContainer == null) return;

        if (thresholdSegments == null) thresholdSegments = new List<ThresholdSegmentView>();
        thresholdSegments.Clear();

        for (int i = 0; i < pnlThresholdBarsContainer.childCount; i++)
        {
            Transform segTr = pnlThresholdBarsContainer.GetChild(i);
            ThresholdSegmentView seg = new ThresholdSegmentView
            {
                rootObject = segTr.gameObject,
                layoutElement = segTr.GetComponent<LayoutElement>(),
                txtMarker = segTr.Find("Img_ActiveMarker")?.GetComponent<TextMeshProUGUI>(),
                imgBar = segTr.Find("Img_Bar")?.GetComponent<Image>(),
                txtLevel = segTr.Find("Txt_Level")?.GetComponent<TextMeshProUGUI>(),
                txtPercentage = segTr.Find("Txt_Percentage")?.GetComponent<TextMeshProUGUI>()
            };
            thresholdSegments.Add(seg);
        }
    }

    // =========================================================================
    // SETUP I CYKL ŻYCIA
    // =========================================================================

    /// <summary>
    /// Główna metoda otwierająca okno dla wybranego budynku produkcyjnego.
    /// </summary>
    public void Setup(BuildingEntity building)
    {
        AutoBindReferences();

        UnsubscribeEvents();
        currentBuilding = building;
        ResetDemolishState();

        if (currentBuilding == null) return;

        // Subskrypcja zdarzeń
        if (WorkforceManager.Instance != null)
        {
            WorkforceManager.Instance.OnWorkforceStatsChanged += OnWorkforceChanged;
        }

        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted += OnMorningReset;
        }

        // Podpięcie przycisku zamknięcia
        if (btnClose != null)
        {
            btnClose.onClick.RemoveAllListeners();
            btnClose.onClick.AddListener(CloseWindow);
        }

        // Podpięcie przycisku wstrzymania/wznowienia
        if (btnPause != null)
        {
            btnPause.onClick.RemoveAllListeners();
            btnPause.onClick.AddListener(TogglePause);
        }

        // Podpięcie przycisku wyburzenia
        if (btnDemolish != null)
        {
            btnDemolish.onClick.RemoveAllListeners();
            btnDemolish.onClick.AddListener(HandleDemolishClick);
        }

        // Podpięcie przycisków załogi
        SetupRaceButtonListeners();

        // Pełne odświeżenie widoku
        RefreshAll();
    }

    private void UnsubscribeEvents()
    {
        if (WorkforceManager.Instance != null)
        {
            WorkforceManager.Instance.OnWorkforceStatsChanged -= OnWorkforceChanged;
        }

        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= OnMorningReset;
        }
    }

    private void OnWorkforceChanged(Race race, WorkforceManager.WorkforceStats stats)
    {
        RefreshWorkersUI();
        RefreshProductionCards();
    }

    private void OnMorningReset()
    {
        RefreshAll();
    }

    private void SetupRaceButtonListeners()
    {
        foreach (var rc in raceControls)
        {
            Race capturedRace = rc.race;

            if (rc.btnPlus != null)
            {
                rc.btnPlus.onClick.RemoveAllListeners();
                rc.btnPlus.onClick.AddListener(() =>
                {
                    if (currentBuilding == null || !currentBuilding.isBuildingActive) return;
                    if (currentBuilding.TryAddWorker(capturedRace))
                    {
                        RefreshWorkersUI();
                        RefreshProductionCards();
                    }
                });
            }

            if (rc.btnMinus != null)
            {
                rc.btnMinus.onClick.RemoveAllListeners();
                rc.btnMinus.onClick.AddListener(() =>
                {
                    if (currentBuilding == null || !currentBuilding.isBuildingActive) return;
                    currentBuilding.RemoveWorker(capturedRace);
                    RefreshWorkersUI();
                    RefreshProductionCards();
                });
            }
        }
    }

    // =========================================================================
    // REFRESH METODY
    // =========================================================================

    /// <summary>
    /// Pełne odświeżenie wszystkich sekcji panelu.
    /// </summary>
    public void RefreshAll()
    {
        if (currentBuilding == null) return;

        // 1. Tytuł budynku
        if (txtBuildingName != null)
        {
            string bName = currentBuilding.data != null ? currentBuilding.data.buildingName : currentBuilding.name;
            txtBuildingName.text = string.IsNullOrEmpty(bName) ? "BUDYNEK PRODUKCYJNY" : bName.ToUpper();
        }

        // 2. Stan wstrzymania i przycisk wstrzymania
        RefreshStoppedAndPauseButton();

        // 3. Utrzymanie
        RefreshUpkeepUI();

        // 4. Załoga i progi
        RefreshWorkersUI();

        // 5. Bilans produkcji (kolumny surowców i podsumowanie netto)
        RefreshProductionCards();

        // Wymuś przebudowę układu
        ForceRebuildLayout();
    }

    private void RefreshStoppedAndPauseButton()
    {
        if (currentBuilding == null) return;

        bool isActive = currentBuilding.isBuildingActive;

        // Nakładka Pnl_Stopped
        if (pnlStopped != null)
        {
            pnlStopped.SetActive(!isActive);
        }

        // Przycisk Pauza / Wznów
        if (btnPause != null)
        {
            if (txtPauseLabel != null)
            {
                txtPauseLabel.text = isActive ? "WSTRZYMAJ" : "WZNÓW";
                txtPauseLabel.color = isActive ? pauseActiveTxt : pauseStoppedTxt;
            }

            if (imgPauseBackground != null)
            {
                imgPauseBackground.color = isActive ? pauseActiveBg : pauseStoppedBg;
            }
        }
    }

    // ── Utrzymanie (Upkeep) ───────────────────────────────────────────────────
    private void RefreshUpkeepUI()
    {
        if (secUpkeep == null || currentBuilding == null || currentBuilding.data == null) return;

        var upkeepList = currentBuilding.data.upkeepPerCycle;
        if (upkeepList == null || upkeepList.Count == 0)
        {
            secUpkeep.SetActive(false);
            return;
        }

        secUpkeep.SetActive(true);
        if (pnlUpkeepResources == null) return;

        int activeItems = 0;
        for (int i = 0; i < upkeepList.Count; i++)
        {
            var cost = upkeepList[i];
            Transform itemTr;
            if (i < pnlUpkeepResources.childCount)
            {
                itemTr = pnlUpkeepResources.GetChild(i);
                itemTr.gameObject.SetActive(true);
            }
            else
            {
                if (pnlUpkeepResources.childCount > 0)
                {
                    itemTr = Instantiate(pnlUpkeepResources.GetChild(0), pnlUpkeepResources, false);
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

            // Kwota
            TextMeshProUGUI amtTmp = itemTr.Find("Txt_Amount")?.GetComponent<TextMeshProUGUI>();
            if (amtTmp != null)
            {
                amtTmp.text = "-" + cost.amount;
            }

            activeItems++;
        }

        // Ukryj nadmiarowe
        for (int i = activeItems; i < pnlUpkeepResources.childCount; i++)
        {
            pnlUpkeepResources.GetChild(i).gameObject.SetActive(false);
        }
    }

    // ── Załoga i Progi Wydajności ─────────────────────────────────────────────
    private void RefreshWorkersUI()
    {
        if (currentBuilding == null) return;

        bool isBuildingActive = currentBuilding.isBuildingActive;
        int totalWorkers = currentBuilding.GetTotalWorkerCount();
        int maxWorkers = currentBuilding.data != null ? currentBuilding.data.baseMaxWorkers : 1;
        if (currentBuilding.Upgrades != null) maxWorkers += currentBuilding.Upgrades.LocalBonusWorkers;
        if (CityStatsManager.Instance != null) maxWorkers += CityStatsManager.Instance.globalBonusWorkersPerShift;

        float crewEff = currentBuilding.Production != null ? currentBuilding.Production.GetCrewEfficiency() : 0f;

        // Podsumowanie pracowników
        if (txtWorkerCount != null)
        {
            txtWorkerCount.text = $"Pracownicy: {totalWorkers} / {maxWorkers}";
        }

        // Procent wydajności w podsumowaniu
        if (txtEfficiencyPercentage != null)
        {
            txtEfficiencyPercentage.text = $"{Mathf.RoundToInt(crewEff * 100f)}%";
        }

        // Kafelki 3 ras
        foreach (var rc in raceControls)
        {
            int assignedCount = currentBuilding.GetWorkerCount(rc.race);
            int freeCount = WorkforceManager.Instance != null ? WorkforceManager.Instance.GetIdleCount(rc.race) : 0;

            if (rc.txtCount != null) rc.txtCount.text = assignedCount.ToString();
            if (rc.txtFree != null) rc.txtFree.text = $"Wolni: {freeCount}";

            // Gdy budynek jest wstrzymany, przyciski są zablokowane
            if (rc.btnMinus != null) rc.btnMinus.interactable = isBuildingActive && (assignedCount > 0);
            if (rc.btnPlus != null) rc.btnPlus.interactable = isBuildingActive && (totalWorkers < maxWorkers) && (freeCount > 0);
        }

        // Progi wydajności
        RefreshThresholdsUI(totalWorkers, maxWorkers);
    }

    private void RefreshThresholdsUI(int totalWorkers, int maxWorkers)
    {
        if (secEfficiencyThresholds == null || currentBuilding == null || currentBuilding.data == null) return;

        int requiredSegments = maxWorkers + 1;
        float[] effTable = currentBuilding.data.GetEffectiveEfficiencyTable(requiredSegments);

        int currentThresholdLevel = Mathf.Clamp(totalWorkers, 0, maxWorkers);
        float currentEffVal = currentThresholdLevel < effTable.Length ? effTable[currentThresholdLevel] : effTable[effTable.Length - 1];

        // Nagłówek statusu
        if (txtCurrentThresholdStatus != null)
        {
            txtCurrentThresholdStatus.text = $"Poziom {currentThresholdLevel}: {Mathf.RoundToInt(currentEffVal * 100f)}%";
        }

        // Rozbudowa segmentów w kontenerze, jeśli potrzeba
        while (thresholdSegments.Count < requiredSegments && thresholdSegments.Count > 0)
        {
            Instantiate(thresholdSegments[0].rootObject, pnlThresholdBarsContainer, false);
            AutoBindThresholdSegments();
        }

        Color[] greenPalette = new Color[]
        {
            new Color(0.85f, 0.25f, 0.2f, 0.9f),  // Poziom 0: czerwony/bazowy
            new Color(0.55f, 0.45f, 0.15f, 0.9f), // Poziom 1: oliwkowo-złoty
            new Color(0.3f, 0.55f, 0.2f, 0.9f),   // Poziom 2: zieleń stonowana
            new Color(0.15f, 0.65f, 0.25f, 0.9f), // Poziom 3: zieleń nasycona
            new Color(0.1f, 0.8f, 0.35f, 0.95f)   // Poziom 4+: żywa jasna zieleń
        };

        for (int i = 0; i < thresholdSegments.Count; i++)
        {
            var seg = thresholdSegments[i];
            if (i >= requiredSegments)
            {
                seg.rootObject.SetActive(false);
                continue;
            }

            seg.rootObject.SetActive(true);
            bool isActive = (i == currentThresholdLevel);
            float levelEff = effTable[i];

            // Szerokość segmentu: wg delty
            float stepDelta = i == 0 ? Mathf.Max(0.5f, levelEff) : (levelEff - effTable[i - 1]);
            if (seg.layoutElement != null)
            {
                seg.layoutElement.flexibleWidth = Mathf.Max(0.1f, stepDelta);
            }

            if (seg.txtLevel != null)
            {
                seg.txtLevel.text = i.ToString();
                seg.txtLevel.color = isActive ? new Color(1f, 0.85f, 0.2f) : new Color(0.65f, 0.65f, 0.65f);
            }

            if (seg.txtPercentage != null)
            {
                seg.txtPercentage.text = $"{Mathf.RoundToInt(levelEff * 100f)}%";
                seg.txtPercentage.color = isActive ? new Color(1f, 0.85f, 0.2f) : new Color(0.7f, 0.7f, 0.7f);
            }

            if (seg.txtMarker != null)
            {
                seg.txtMarker.text = isActive ? "▲" : "";
                seg.txtMarker.color = new Color(1f, 0.85f, 0.2f);
            }

            if (seg.imgBar != null)
            {
                Color baseColor = i < greenPalette.Length ? greenPalette[i] : greenPalette[greenPalette.Length - 1];
                if (isActive)
                {
                    seg.imgBar.color = new Color(Mathf.Min(1f, baseColor.r * 1.35f), Mathf.Min(1f, baseColor.g * 1.35f), Mathf.Min(1f, baseColor.b * 1.35f), 1f);
                }
                else
                {
                    seg.imgBar.color = new Color(baseColor.r * 0.7f, baseColor.g * 0.7f, baseColor.b * 0.7f, 0.75f);
                }
            }
        }
    }

    // ── Bilans Produkcji (Tabela Pnl_ResourceColumn + Row_NetSummary) ─────────
    private void RefreshProductionCards()
    {
        if (pnlProductionResourceCard == null || currentBuilding == null || currentBuilding.data == null) return;

        var prodList = currentBuilding.data.productionPerCycle;
        var terrainBonuses = currentBuilding.Terrain != null ? currentBuilding.Terrain.CalculateTerrainBonuses() : null;
        Dictionary<ResourceType, float> upgradeBonuses = currentBuilding.Upgrades != null
            ? currentBuilding.Upgrades.GetAllProductionBonuses()
            : null;

        bool hasAnyProduction = (prodList != null && prodList.Count > 0) ||
                                (upgradeBonuses != null && upgradeBonuses.Count > 0) ||
                                (terrainBonuses != null && terrainBonuses.Count > 0);

        if (!hasAnyProduction)
        {
            pnlProductionResourceCard.SetActive(false);
            return;
        }

        pnlProductionResourceCard.SetActive(true);
        if (pnlModifiersList == null) return;

        // Zbuduj listę produkowanych surowców (bazowe + ewentualne z ulepszeń + ewentualne z terenu)
        List<(ResourceType type, float baseAmount)> resourcesToDisplay = new List<(ResourceType, float)>();

        if (prodList != null)
        {
            for (int i = 0; i < prodList.Count; i++)
            {
                var cost = prodList[i];
                float flatBonus = 0f;
                if (upgradeBonuses != null && upgradeBonuses.TryGetValue(cost.type, out float val))
                {
                    flatBonus = val;
                }
                resourcesToDisplay.Add((cost.type, cost.amount + flatBonus));
            }
        }

        if (upgradeBonuses != null)
        {
            foreach (var kvp in upgradeBonuses)
            {
                if (kvp.Value <= 0f) continue;
                if (!resourcesToDisplay.Exists(r => r.type == kvp.Key))
                {
                    resourcesToDisplay.Add((kvp.Key, kvp.Value));
                }
            }
        }

        if (terrainBonuses != null)
        {
            foreach (var kvp in terrainBonuses)
            {
                if (kvp.Value <= 0f) continue;
                if (!resourcesToDisplay.Exists(r => r.type == kvp.Key))
                {
                    resourcesToDisplay.Add((kvp.Key, 0f));
                }
            }
        }

        int requiredColumns = resourcesToDisplay.Count;
        EnsureResourceColumnsCount(requiredColumns);
        EnsureNetSummaryValuesCount(requiredColumns);

        // Obliczenia wspólne i terenowe dla budynku
        float terrainMult = currentBuilding.Terrain != null ? currentBuilding.Terrain.GetTerrainMultiplier() : 1f;

        float externalMultiplier = currentBuilding.Production != null ? currentBuilding.Production.GetGlobalMultiplier() : 1f;
        float crewEff = currentBuilding.Production != null ? currentBuilding.Production.GetCrewEfficiency() : 0f;

        // Globalna widoczność wierszy tabeli (spójna dla Pnl_StaticText i każdej kolumny Pnl_ResourceColumn)
        bool showBase = true;
        bool showOnTop = false;
        bool showAdjacent = false;

        for (int i = 0; i < resourcesToDisplay.Count; i++)
        {
            ResourceType rt = resourcesToDisplay[i].type;
            float onTop = currentBuilding.Terrain != null ? currentBuilding.Terrain.GetOnTopBonus(rt) * terrainMult : 0f;
            float adj = currentBuilding.Terrain != null ? currentBuilding.Terrain.GetAdjacentBonus(rt) * terrainMult : 0f;
            if (onTop > 0.001f) showOnTop = true;
            if (adj > 0.001f) showAdjacent = true;
        }

        bool showExtMult = !Mathf.Approximately(externalMultiplier, 1.0f);
        bool showCrewEff = true;

        if (lblRowBase != null) lblRowBase.SetActive(showBase);
        if (lblRowOnTopBonus != null) lblRowOnTopBonus.SetActive(showOnTop);
        if (lblRowAdjacentBonus != null) lblRowAdjacentBonus.SetActive(showAdjacent);
        if (lblRowExternalMult != null) lblRowExternalMult.SetActive(showExtMult);
        if (lblRowCrewEfficiency != null) lblRowCrewEfficiency.SetActive(showCrewEff);

        // Parametry wymiarowe do idealnego wyrównania kolumn Pnl_ResourceColumn i pól Txt_Value w Row_NetSummary
        RectTransform staticRt = pnlStaticText as RectTransform;
        float staticWidth = staticRt != null && staticRt.sizeDelta.x > 0f ? staticRt.sizeDelta.x : 150f;
        float staticHeight = staticRt != null && staticRt.sizeDelta.y > 0f ? staticRt.sizeDelta.y : 200f;
        const float columnWidth = 50f;

        if (rowNetSummary != null)
        {
            rowNetSummary.gameObject.SetActive(true);
            var hlg = rowNetSummary.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null)
            {
                hlg.spacing = 0f;
            }
            if (txtNetSummaryLabel != null)
            {
                var lblRt = txtNetSummaryLabel.rectTransform;
                lblRt.sizeDelta = new Vector2(staticWidth, lblRt.sizeDelta.y);
            }
        }

        // Wypełnianie kolumn surowców oraz odpowiadających im wartości w Row_NetSummary
        for (int i = 0; i < cachedResourceColumns.Count; i++)
        {
            var colView = cachedResourceColumns[i];
            if (colView == null || colView.rootObject == null) continue;

            if (i >= requiredColumns)
            {
                colView.rootObject.SetActive(false);
                continue;
            }

            colView.rootObject.SetActive(true);

            // Pozycjonowanie kolumny na osi X (tuż za Pnl_StaticText, co 50 px)
            if (colView.rectTransform != null)
            {
                colView.rectTransform.anchorMin = new Vector2(0f, 1f);
                colView.rectTransform.anchorMax = new Vector2(0f, 1f);
                colView.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                colView.rectTransform.sizeDelta = new Vector2(columnWidth, staticHeight);
                colView.rectTransform.anchoredPosition = new Vector2(
                    staticWidth + i * columnWidth + columnWidth * 0.5f,
                    -staticHeight * 0.5f);
            }

            ResourceType resType = resourcesToDisplay[i].type;
            float baseAmountWithUpgrade = resourcesToDisplay[i].baseAmount;

            float colOnTop = currentBuilding.Terrain != null ? currentBuilding.Terrain.GetOnTopBonus(resType) * terrainMult : 0f;
            float colAdjacent = currentBuilding.Terrain != null ? currentBuilding.Terrain.GetAdjacentBonus(resType) * terrainMult : 0f;

            float baseWithFlats = baseAmountWithUpgrade + colOnTop + colAdjacent;
            float netTotal = currentBuilding.isBuildingActive ? (baseWithFlats * externalMultiplier * crewEff) : 0f;

            // Ikona surowca w nagłówku kolumny (Spacer)
            if (colView.imgResourceIcon != null && resourceVisualsSO != null)
            {
                colView.imgResourceIcon.enabled = true;
                colView.imgResourceIcon.sprite = resourceVisualsSO.GetIcon(resType);
                colView.imgResourceIcon.color = colView.imgResourceIcon.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            }

            // 1. Wartość bazowa
            if (colView.rowBase != null)
            {
                colView.rowBase.SetActive(showBase);
                if (colView.txtBaseValue != null)
                {
                    if (baseAmountWithUpgrade > 0.001f)
                    {
                        colView.txtBaseValue.text = $"+{baseAmountWithUpgrade:0.#}";
                        colView.txtBaseValue.color = colBaseColor;
                    }
                    else
                    {
                        colView.txtBaseValue.text = "-";
                        colView.txtBaseValue.color = colNeutralDashColor;
                    }
                }
            }

            // 2. Złoże pod budynkiem
            if (colView.rowOnTopBonus != null)
            {
                colView.rowOnTopBonus.SetActive(showOnTop);
                if (showOnTop && colView.txtOnTopValue != null)
                {
                    if (colOnTop > 0.001f)
                    {
                        colView.txtOnTopValue.text = $"+{colOnTop:0.#}";
                        colView.txtOnTopValue.color = colOnTopColor;
                    }
                    else
                    {
                        colView.txtOnTopValue.text = "-";
                        colView.txtOnTopValue.color = colNeutralDashColor;
                    }
                }
            }

            // 3. Złoża w pobliżu
            if (colView.rowAdjacentBonus != null)
            {
                colView.rowAdjacentBonus.SetActive(showAdjacent);
                if (showAdjacent && colView.txtAdjacentValue != null)
                {
                    if (colAdjacent > 0.001f)
                    {
                        colView.txtAdjacentValue.text = $"+{colAdjacent:0.#}";
                        colView.txtAdjacentValue.color = colSurroundingColor;
                    }
                    else
                    {
                        colView.txtAdjacentValue.text = "-";
                        colView.txtAdjacentValue.color = colNeutralDashColor;
                    }
                }
            }

            // 4. Mnożnik ulepszeń / badań
            if (colView.rowExternalMultipliers != null)
            {
                colView.rowExternalMultipliers.SetActive(showExtMult);
                if (showExtMult && colView.txtExternalMultiplierValue != null)
                {
                    colView.txtExternalMultiplierValue.text = $"x{externalMultiplier:0.##}";
                    colView.txtExternalMultiplierValue.color = colExtMultColor;
                }
            }

            // 5. Wydajność pracowników
            if (colView.rowCrewEfficiency != null)
            {
                colView.rowCrewEfficiency.SetActive(showCrewEff);
                if (colView.txtCrewEfficiencyValue != null)
                {
                    colView.txtCrewEfficiencyValue.text = $"{Mathf.RoundToInt(crewEff * 100f)}%";
                    colView.txtCrewEfficiencyValue.color = colEfficiencyColor;
                }
            }

            // 6. Odpowiadająca wartość w Row_NetSummary
            if (i < cachedNetSummaryValues.Count && cachedNetSummaryValues[i] != null)
            {
                TextMeshProUGUI netTmp = cachedNetSummaryValues[i];
                netTmp.gameObject.SetActive(true);
                netTmp.alignment = TextAlignmentOptions.Center;

                RectTransform netRt = netTmp.rectTransform;
                netRt.sizeDelta = new Vector2(columnWidth, netRt.sizeDelta.y);

                LayoutElement netLe = netTmp.GetComponent<LayoutElement>();
                if (netLe != null)
                {
                    netLe.preferredWidth = columnWidth;
                }

                if (currentBuilding.isBuildingActive)
                {
                    netTmp.text = $"+{netTotal:0.0}";
                    netTmp.color = netActiveColor;
                }
                else
                {
                    netTmp.text = "0.0";
                    netTmp.color = netStoppedColor;
                }
            }
        }

        // Ukryj nadmiarowe wartości w Row_NetSummary (gdy budynek ma mniej surowców niż wcześniej otwarty)
        for (int i = requiredColumns; i < cachedNetSummaryValues.Count; i++)
        {
            if (cachedNetSummaryValues[i] != null)
            {
                cachedNetSummaryValues[i].gameObject.SetActive(false);
            }
        }
    }

    private void EnsureResourceColumnsCount(int count)
    {
        if (pnlModifiersList == null) return;

        cachedResourceColumns.RemoveAll(c => c == null || c.rootObject == null);

        // Jeśli lista jest pusta, przeskanuj istniejące kolumny w Pnl_ModifiersList (pomijając Pnl_StaticText)
        if (cachedResourceColumns.Count == 0)
        {
            for (int i = 0; i < pnlModifiersList.childCount; i++)
            {
                Transform child = pnlModifiersList.GetChild(i);
                if (child == pnlStaticText || child.name == "Pnl_StaticText") continue;

                var view = BindColumnView(child.gameObject);
                if (view != null) cachedResourceColumns.Add(view);
            }
        }

        // Jeśli brakuje kolumn, utwórz nowe klonując prefab Pnl_ResourceColumn lub pierwszą kolumnę
        while (cachedResourceColumns.Count < count)
        {
            GameObject newObj = null;
            if (resourceColumnPrefab != null)
            {
                newObj = Instantiate(resourceColumnPrefab, pnlModifiersList, false);
            }
            else if (cachedResourceColumns.Count > 0)
            {
                newObj = Instantiate(cachedResourceColumns[0].rootObject, pnlModifiersList, false);
            }

            if (newObj == null) break;

            newObj.name = $"Pnl_ResourceColumn_{cachedResourceColumns.Count}";
            var newView = BindColumnView(newObj);
            cachedResourceColumns.Add(newView);
        }
    }

    private void EnsureNetSummaryValuesCount(int count)
    {
        if (rowNetSummary == null) return;

        cachedNetSummaryValues.RemoveAll(t => t == null);

        // Przeskanuj istniejące dzieci Row_NetSummary (pomijając Txt_Label)
        if (cachedNetSummaryValues.Count == 0)
        {
            for (int i = 0; i < rowNetSummary.childCount; i++)
            {
                Transform child = rowNetSummary.GetChild(i);
                if ((txtNetSummaryLabel != null && child == txtNetSummaryLabel.transform) || child.name == "Txt_Label")
                    continue;

                TextMeshProUGUI tmp = child.GetComponent<TextMeshProUGUI>();
                if (tmp != null) cachedNetSummaryValues.Add(tmp);
            }
        }

        // Jeśli brakuje pól Txt_Value dla kolejnych surowców, sklonuj pierwsze pole Txt_Value
        while (cachedNetSummaryValues.Count < count && cachedNetSummaryValues.Count > 0)
        {
            GameObject cloneObj = Instantiate(cachedNetSummaryValues[0].gameObject, rowNetSummary, false);
            cloneObj.name = $"Txt_Value_{cachedNetSummaryValues.Count}";
            TextMeshProUGUI cloneTmp = cloneObj.GetComponent<TextMeshProUGUI>();
            if (cloneTmp == null) break;
            cachedNetSummaryValues.Add(cloneTmp);
        }
    }

    private ResourceColumnView BindColumnView(GameObject colObj)
    {
        if (colObj == null) return null;

        Transform colTr = colObj.transform;
        Transform baseTr = colTr.Find("Txt_Value_Base");
        Transform onTopTr = colTr.Find("Txt_Value_OnTop");
        Transform surrTr = colTr.Find("Txt_Value_Surrouding") ?? colTr.Find("Txt_Value_Surrounding");
        Transform extMultTr = colTr.Find("Txt_Value_ExternalMult");
        Transform effTr = colTr.Find("Txt_Value_Efficency") ?? colTr.Find("Txt_Value_Efficiency");

        ResourceColumnView view = new ResourceColumnView
        {
            rootObject = colObj,
            rectTransform = colObj.GetComponent<RectTransform>(),
            imgResourceIcon = colTr.Find("Spacer")?.GetComponent<Image>(),

            rowBase = baseTr?.gameObject,
            txtBaseValue = baseTr?.GetComponent<TextMeshProUGUI>(),

            rowOnTopBonus = onTopTr?.gameObject,
            txtOnTopValue = onTopTr?.GetComponent<TextMeshProUGUI>(),

            rowAdjacentBonus = surrTr?.gameObject,
            txtAdjacentValue = surrTr?.GetComponent<TextMeshProUGUI>(),

            rowExternalMultipliers = extMultTr?.gameObject,
            txtExternalMultiplierValue = extMultTr?.GetComponent<TextMeshProUGUI>(),

            rowCrewEfficiency = effTr?.gameObject,
            txtCrewEfficiencyValue = effTr?.GetComponent<TextMeshProUGUI>()
        };

        return view;
    }

    // =========================================================================
    // AKCJE: WSTRZYMAJ / WZNÓW I WYBURZ
    // =========================================================================

    /// <summary>
    /// Przełącza stan wstrzymania produkcji budynku (isBuildingActive).
    /// Przy wstrzymaniu natychmiast uwalnia wszystkich pracowników, zachowując
    /// status zmęczenia Locked dla tych, którzy już przepracowali cykl.
    /// </summary>
    public void TogglePause()
    {
        if (currentBuilding == null) return;

        if (currentBuilding.isBuildingActive)
        {
            // Wstrzymaj produkcję
            currentBuilding.isBuildingActive = false;
            currentBuilding.Workers?.ReleaseAllWorkers(preserveLockedState: true);
        }
        else
        {
            // Wznów produkcję
            currentBuilding.isBuildingActive = true;
        }

        RefreshAll();
    }

    /// <summary>
    /// Obsługuje dwustopniowe potwierdzenie wyburzenia budynku.
    /// 1. kliknięcie: zmiana etykiety na "NA PEWNO?"
    /// 2. kliknięcie: wywołanie Demolish() i zamknięcie okna.
    /// </summary>
    public void HandleDemolishClick()
    {
        if (currentBuilding == null) return;

        if (!isDemolishConfirming)
        {
            // Krok 1: oczekiwanie na potwierdzenie
            isDemolishConfirming = true;
            if (txtDemolishLabel != null) txtDemolishLabel.text = "NA PEWNO?";
            if (imgDemolishBackground != null) imgDemolishBackground.color = demolishConfirmBg;
            if (txtDemolishLabel != null) txtDemolishLabel.color = demolishConfirmTxt;
        }
        else
        {
            // Krok 2: faktyczne wyburzenie
            isDemolishConfirming = false;
            var buildingToDemolish = currentBuilding;
            CloseWindow();
            buildingToDemolish.Demolish();
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

    /// <summary>
    /// Zamyka okno kontekstowe i usuwa zaznaczenie budynku.
    /// </summary>
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

    private System.Collections.IEnumerator RebuildLayoutRoutine()
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
}
