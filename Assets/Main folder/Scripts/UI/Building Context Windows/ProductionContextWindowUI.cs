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

    public class ProductionCardItemView
    {
        public GameObject rootObject;
        public Image imgResourceIcon;
        public TextMeshProUGUI txtResourceName;
        public TextMeshProUGUI txtTotalProduction;

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

        public GameObject rowNetSummary;
        public TextMeshProUGUI txtNetSummaryValue;
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

    // ── Sekcja Bilansu Produkcji ──────────────────────────────────────────────
    [Header("Bilans Produkcji")]
    [SerializeField] private GameObject secProduction;
    [SerializeField] private Transform pnlProductionCardsContainer;
    [SerializeField] private GameObject cardPrefab;

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
    private readonly List<ProductionCardItemView> cachedCardViews = new List<ProductionCardItemView>();

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

        // 5. Produkcja (Sec_Production)
        if (secProduction == null)
            secProduction = transform.Find("Pnl_ColumnsContainer/Pnl_RightColumn/Sec_Production")?.gameObject;

        if (pnlProductionCardsContainer == null)
            pnlProductionCardsContainer = transform.Find("Pnl_ColumnsContainer/Pnl_RightColumn/Sec_Production/Pnl_ProductionCardsContainer");

        if (cardPrefab == null)
        {
#if UNITY_EDITOR
            cardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Main folder/Prefabs/UI/Pnl_ProductionResourceCard.prefab");
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

        // 5. Bilans produkcji (kafelki surowców)
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

        float scalingFactor = currentBuilding.data.workerScalingFactor;
        float passiveEff = 0f;
        if (MetaUpgradeManager.Instance != null)
        {
            passiveEff = MetaUpgradeManager.Instance.GetValue(MetaEffectType.BuildingPassiveEfficiency);
        }

        int requiredSegments = maxWorkers + 1;
        float[] effTable = new float[requiredSegments];
        effTable[0] = passiveEff;
        if (requiredSegments > 1) effTable[1] = 1.0f;
        for (int i = 2; i < requiredSegments; i++)
        {
            effTable[i] = 1f + (i - 1) * scalingFactor;
        }

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

    // ── Bilans Produkcji (Kafelki Surowców) ────────────────────────────────────
    private void RefreshProductionCards()
    {
        if (secProduction == null || currentBuilding == null || currentBuilding.data == null) return;

        var prodList = currentBuilding.data.productionPerCycle;
        if (prodList == null || prodList.Count == 0)
        {
            secProduction.SetActive(false);
            return;
        }

        secProduction.SetActive(true);
        if (pnlProductionCardsContainer == null) return;

        // Upewnij się, że mamy kafelki w kontenerze
        int requiredCards = prodList.Count;
        EnsureCardViewsCount(requiredCards);

        float externalMultiplier = currentBuilding.Production != null ? currentBuilding.Production.GetGlobalMultiplier() : 1f;
        float crewEff = currentBuilding.Production != null ? currentBuilding.Production.GetCrewEfficiency() : 0f;

        for (int i = 0; i < cachedCardViews.Count; i++)
        {
            var cardView = cachedCardViews[i];
            if (i >= requiredCards)
            {
                cardView.rootObject.SetActive(false);
                continue;
            }

            cardView.rootObject.SetActive(true);
            var prodCost = prodList[i];
            ResourceType resType = prodCost.type;
            float baseAmount = prodCost.amount;

            // Premie terenowe dotyczą pierwszego/głównego produkowanego surowca
            bool isPrimary = (i == 0);
            float onTopBonus = isPrimary && currentBuilding.Terrain != null ? currentBuilding.Terrain.GetOnTopBonus() : 0f;
            float adjacentBonus = isPrimary && currentBuilding.Terrain != null ? currentBuilding.Terrain.GetAdjacentBonus() : 0f;

            // Premie z ulepszeń budynku
            float upgradeFlat = 0f;
            if (currentBuilding.Upgrades != null)
            {
                var bonuses = currentBuilding.Upgrades.GetAllProductionBonuses();
                if (bonuses != null && bonuses.TryGetValue(resType, out float val))
                {
                    upgradeFlat = val;
                }
            }

            float baseWithFlats = baseAmount + onTopBonus + adjacentBonus + upgradeFlat;
            float netTotal = currentBuilding.isBuildingActive ? (baseWithFlats * externalMultiplier * crewEff) : 0f;

            // Nagłówek kafelka
            if (cardView.imgResourceIcon != null && resourceVisualsSO != null)
            {
                cardView.imgResourceIcon.sprite = resourceVisualsSO.GetIcon(resType);
                cardView.imgResourceIcon.color = cardView.imgResourceIcon.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            }

            if (cardView.txtResourceName != null)
            {
                cardView.txtResourceName.text = resType.ToString().ToUpper();
            }

            if (cardView.txtTotalProduction != null)
            {
                if (currentBuilding.isBuildingActive)
                {
                    cardView.txtTotalProduction.text = $"+{netTotal:0.0} / cykl";
                    cardView.txtTotalProduction.color = new Color(0.35f, 0.85f, 0.4f, 1f);
                }
                else
                {
                    cardView.txtTotalProduction.text = "0.0 / cykl";
                    cardView.txtTotalProduction.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
                }
            }

            // 1. Wartość bazowa
            if (cardView.rowBase != null)
            {
                cardView.rowBase.SetActive(baseAmount > 0f);
                if (cardView.txtBaseValue != null)
                    cardView.txtBaseValue.text = $"+{baseAmount:0.#}";
            }

            // 2. Bonus za złoże pod budynkiem (tylko gdy > 0)
            if (cardView.rowOnTopBonus != null)
            {
                bool showOnTop = onTopBonus > 0.001f;
                cardView.rowOnTopBonus.SetActive(showOnTop);
                if (showOnTop && cardView.txtOnTopValue != null)
                    cardView.txtOnTopValue.text = $"+{onTopBonus:0.#}";
            }

            // 3. Bonus za złoża w pobliżu (tylko gdy > 0)
            if (cardView.rowAdjacentBonus != null)
            {
                bool showAdj = adjacentBonus > 0.001f;
                cardView.rowAdjacentBonus.SetActive(showAdj);
                if (showAdj && cardView.txtAdjacentValue != null)
                    cardView.txtAdjacentValue.text = $"+{adjacentBonus:0.#}";
            }

            // 4. Mnożnik ulepszeń / badań (tylko gdy != 1.0)
            if (cardView.rowExternalMultipliers != null)
            {
                bool showExt = !Mathf.Approximately(externalMultiplier, 1.0f);
                cardView.rowExternalMultipliers.SetActive(showExt);
                if (showExt && cardView.txtExternalMultiplierValue != null)
                    cardView.txtExternalMultiplierValue.text = $"x{externalMultiplier:0.##}";
            }

            // 5. Wydajność załogi (zawsze)
            if (cardView.rowCrewEfficiency != null)
            {
                cardView.rowCrewEfficiency.SetActive(true);
                if (cardView.txtCrewEfficiencyValue != null)
                    cardView.txtCrewEfficiencyValue.text = $"{Mathf.RoundToInt(crewEff * 100f)}%";
            }

            // 6. Podsumowanie netto
            if (cardView.rowNetSummary != null)
            {
                cardView.rowNetSummary.SetActive(true);
                if (cardView.txtNetSummaryValue != null)
                {
                    if (currentBuilding.isBuildingActive)
                    {
                        cardView.txtNetSummaryValue.text = $"+{netTotal:0.0} / cykl";
                        cardView.txtNetSummaryValue.color = new Color(0.35f, 0.9f, 0.45f, 1f);
                    }
                    else
                    {
                        cardView.txtNetSummaryValue.text = "0.0 / cykl (Wstrzymano)";
                        cardView.txtNetSummaryValue.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
                    }
                }
            }
        }
    }

    private void EnsureCardViewsCount(int count)
    {
        if (pnlProductionCardsContainer == null) return;

        // Jeśli lista jest pusta, przeskanuj dzieci kontenera
        if (cachedCardViews.Count == 0)
        {
            for (int i = 0; i < pnlProductionCardsContainer.childCount; i++)
            {
                var view = BindCardView(pnlProductionCardsContainer.GetChild(i).gameObject);
                if (view != null) cachedCardViews.Add(view);
            }
        }

        // Jeśli brakuje, utwórz nowe kafelki klonując prefab lub pierwszy kafelek
        while (cachedCardViews.Count < count)
        {
            GameObject newObj = null;
            if (cardPrefab != null)
            {
                newObj = Instantiate(cardPrefab, pnlProductionCardsContainer, false);
            }
            else if (cachedCardViews.Count > 0)
            {
                newObj = Instantiate(cachedCardViews[0].rootObject, pnlProductionCardsContainer, false);
            }

            if (newObj == null) break;

            var newView = BindCardView(newObj);
            cachedCardViews.Add(newView);
        }
    }

    private ProductionCardItemView BindCardView(GameObject cardObj)
    {
        Transform cardTr = cardObj.transform;
        ProductionCardItemView view = new ProductionCardItemView
        {
            rootObject = cardObj,
            imgResourceIcon = cardTr.Find("Pnl_CardHeader/Img_ResourceIcon")?.GetComponent<Image>(),
            txtResourceName = cardTr.Find("Pnl_CardHeader/Txt_ResourceName")?.GetComponent<TextMeshProUGUI>(),
            txtTotalProduction = cardTr.Find("Pnl_CardHeader/Txt_TotalProduction")?.GetComponent<TextMeshProUGUI>(),

            rowBase = cardTr.Find("Pnl_ModifiersList/Row_Base")?.gameObject,
            txtBaseValue = cardTr.Find("Pnl_ModifiersList/Row_Base/Txt_Value")?.GetComponent<TextMeshProUGUI>(),

            rowOnTopBonus = cardTr.Find("Pnl_ModifiersList/Row_OnTopBonus")?.gameObject,
            txtOnTopValue = cardTr.Find("Pnl_ModifiersList/Row_OnTopBonus/Txt_Value")?.GetComponent<TextMeshProUGUI>(),

            rowAdjacentBonus = cardTr.Find("Pnl_ModifiersList/Row_AdjacentBonus")?.gameObject,
            txtAdjacentValue = cardTr.Find("Pnl_ModifiersList/Row_AdjacentBonus/Txt_Value")?.GetComponent<TextMeshProUGUI>(),

            rowExternalMultipliers = cardTr.Find("Pnl_ModifiersList/Row_ExternalMultipliers")?.gameObject,
            txtExternalMultiplierValue = cardTr.Find("Pnl_ModifiersList/Row_ExternalMultipliers/Txt_Value")?.GetComponent<TextMeshProUGUI>(),

            rowCrewEfficiency = cardTr.Find("Pnl_ModifiersList/Row_CrewEfficiency")?.gameObject,
            txtCrewEfficiencyValue = cardTr.Find("Pnl_ModifiersList/Row_CrewEfficiency/Txt_Value")?.GetComponent<TextMeshProUGUI>(),

            rowNetSummary = cardTr.Find("Row_NetSummary")?.gameObject,
            txtNetSummaryValue = cardTr.Find("Row_NetSummary/Txt_Value")?.GetComponent<TextMeshProUGUI>()
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
