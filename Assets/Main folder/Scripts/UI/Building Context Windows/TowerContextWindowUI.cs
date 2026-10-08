using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Główny komponent zarządzający oknem kontekstowym wieży (Pnl_TowerContext).
/// Obsługuje: zapotrzebowanie na surowce (Upkeep), obsadę 3 ras, dynamiczne progi wydajności,
/// 8 statystyk bojowych z kolorowaniem buff/nerf oraz wybór 3 priorytetów ostrzału.
/// </summary>
public class TowerContextWindowUI : MonoBehaviour
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
    public class CombatStatItemView
    {
        public BattleStats statType;
        public GameObject rootObject;
        public Image imgIcon;
        public TextMeshProUGUI txtName;
        public TextMeshProUGUI txtBaseValue;
        public TextMeshProUGUI txtCurrentValue;
    }

    [System.Serializable]
    public class PrioritySlotView
    {
        public GameObject slotObject;
        public Button slotButton;
        public Image imgBackground;
        public TextMeshProUGUI txtSlotNumber;
        public TextMeshProUGUI txtContent;
    }

    [System.Serializable]
    public class TargetingButtonView
    {
        public TargetingMode mode;
        public Button button;
        public Image imgBackground;
        public TextMeshProUGUI txtLabel;
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

    // ── Główne referencje okna ───────────────────────────────────────────────
    [Header("Nagłówek i Ogólne")]
    [SerializeField] private TextMeshProUGUI txtTowerName;
    [SerializeField] private Button btnClose;
    [SerializeField] private ResourceVisualsSO resourceVisualsSO;

    // ── Sekcja Utrzymania (Upkeep) ─────────────────────────────────────────────
    [Header("Utrzymanie (Upkeep)")]
    [SerializeField] private GameObject secUpkeep;
    [SerializeField] private Transform pnlUpkeepResources;

    // ── Sekcja Załogi i Wydajności ────────────────────────────────────────────
    [Header("Załoga i Wydajność")]
    [SerializeField] private TextMeshProUGUI txtWorkerCount;
    [SerializeField] private TextMeshProUGUI txtEfficiencyPercentage;
    [SerializeField] private List<RaceControlView> raceControls = new List<RaceControlView>();

    [Header("Progi Wydajności")]
    [SerializeField] private GameObject secEfficiencyThresholds;
    [SerializeField] private TextMeshProUGUI txtThresholdLabel;
    [SerializeField] private TextMeshProUGUI txtCurrentThresholdStatus;
    [SerializeField] private Transform pnlThresholdBarsContainer;
    [SerializeField] private List<ThresholdSegmentView> thresholdSegments = new List<ThresholdSegmentView>();

    // ── Sekcja Statystyk Bojowych ─────────────────────────────────────────────
    [Header("Statystyki Bojowe (8 pozycji)")]
    [SerializeField] private Transform pnlCombatStatsGrid;
    [SerializeField] private List<CombatStatItemView> combatStatViews = new List<CombatStatItemView>();

    // ── Sekcja Priorytetów Ostrzału ───────────────────────────────────────────
    [Header("Priorytety Ostrzału")]
    [SerializeField] private List<PrioritySlotView> prioritySlots = new List<PrioritySlotView>();
    [SerializeField] private List<TargetingButtonView> targetingButtons = new List<TargetingButtonView>();

    // ── Stan bieżącej wieży ───────────────────────────────────────────────────
    private TowerEntity currentTower;

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
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    // =========================================================================
    // INICJALIZACJA I AUTO-BINDING
    // =========================================================================

    /// <summary>
    /// Automatycznie wyszukuje i wiąże pola SerializeField po nazwach dzieci,
    /// jeśli pole nie zostało uprzednio przypisane w Inspektorze.
    /// </summary>
    public void AutoBindReferences()
    {
        // 1. Nagłówek
        if (txtTowerName == null)
            txtTowerName = transform.Find("Pnl_TopBar/Txt_TowerName")?.GetComponent<TextMeshProUGUI>();

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

        // 4. Załoga: Podsumowanie
        if (txtWorkerCount == null)
            txtWorkerCount = transform.Find("Pnl_ColumnsContainer/Pnl_LeftColumn/Sec_Workers/Pnl_WorkersSummary/Txt_WorkerCount")?.GetComponent<TextMeshProUGUI>();

        if (txtEfficiencyPercentage == null)
            txtEfficiencyPercentage = transform.Find("Pnl_ColumnsContainer/Pnl_LeftColumn/Sec_Workers/Pnl_WorkersSummary/Txt_EfficiencyPercentage")?.GetComponent<TextMeshProUGUI>();

        // 5. Załoga: Rasy (Krasnoludy, Ludzie, Elfy)
        AutoBindRaceControls();

        // 6. Progi wydajności
        if (secEfficiencyThresholds == null)
            secEfficiencyThresholds = transform.Find("Pnl_ColumnsContainer/Pnl_LeftColumn/Sec_Workers/Sec_EfficiencyThresholds")?.gameObject;

        if (txtThresholdLabel == null)
            txtThresholdLabel = transform.Find("Pnl_ColumnsContainer/Pnl_LeftColumn/Sec_Workers/Sec_EfficiencyThresholds/Pnl_ThresholdHeader/Txt_ThresholdLabel")?.GetComponent<TextMeshProUGUI>();

        if (txtCurrentThresholdStatus == null)
            txtCurrentThresholdStatus = transform.Find("Pnl_ColumnsContainer/Pnl_LeftColumn/Sec_Workers/Sec_EfficiencyThresholds/Pnl_ThresholdHeader/Txt_CurrentThresholdStatus")?.GetComponent<TextMeshProUGUI>();

        if (pnlThresholdBarsContainer == null)
            pnlThresholdBarsContainer = transform.Find("Pnl_ColumnsContainer/Pnl_LeftColumn/Sec_Workers/Sec_EfficiencyThresholds/Pnl_ThresholdBarsContainer");

        AutoBindThresholdSegments();

        // 7. Statystyki bojowe
        if (pnlCombatStatsGrid == null)
            pnlCombatStatsGrid = transform.Find("Pnl_ColumnsContainer/Pnl_LeftColumn/Sec_CombatStats/Pnl_CombatStatsGrid");

        AutoBindCombatStats();

        // 8. Priorytety ostrzału
        AutoBindPrioritySlots();
        AutoBindTargetingButtons();
    }

    private void AutoBindRaceControls()
    {
        if (raceControls == null || raceControls.Count == 0)
        {
            raceControls = new List<RaceControlView>
            {
                new RaceControlView { race = Race.Dwarves },
                new RaceControlView { race = Race.Humans },
                new RaceControlView { race = Race.Elves }
            };
        }

        Transform raceParent = transform.Find("Pnl_ColumnsContainer/Pnl_LeftColumn/Sec_Workers/Pnl_RaceControllers");
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

    private void AutoBindCombatStats()
    {
        if (pnlCombatStatsGrid == null) return;

        var statMapping = new (string childName, BattleStats type)[]
        {
            ("Pnl_CombatStatItem_Damage", BattleStats.Damage),
            ("Pnl_CombatStatItem_AttackSpeed", BattleStats.FireRate),
            ("Pnl_CombatStatItem_Range", BattleStats.Range),
            ("Pnl_CombatStatItem_DmgType", BattleStats.DamageType),
            ("Pnl_CombatStatItem_CritChance", BattleStats.CriticalChancel),
            ("Pnl_CombatStatItem_CritDmg", BattleStats.CriticalMultiplier),
            ("Pnl_CombatStatItem_ArmorPen", BattleStats.ArmorPenetration),
            ("Pnl_CombatStatItem_MagicPen", BattleStats.MagicPenetration)
        };

        if (combatStatViews == null) combatStatViews = new List<CombatStatItemView>();
        combatStatViews.Clear();

        foreach (var map in statMapping)
        {
            Transform itemTr = pnlCombatStatsGrid.Find(map.childName);
            if (itemTr == null)
            {
                // Fallback: wyszukanie po fragmencie nazwy
                for (int i = 0; i < pnlCombatStatsGrid.childCount; i++)
                {
                    var child = pnlCombatStatsGrid.GetChild(i);
                    if (child.name.IndexOf(map.type.ToString(), StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        itemTr = child;
                        break;
                    }
                }
            }

            if (itemTr != null)
            {
                Transform valuesTr = itemTr.Find("Pnl_Values");
                CombatStatItemView statView = new CombatStatItemView
                {
                    statType = map.type,
                    rootObject = itemTr.gameObject,
                    imgIcon = itemTr.Find("Img_StatIcon")?.GetComponent<Image>(),
                    txtName = itemTr.Find("Txt_StatName")?.GetComponent<TextMeshProUGUI>(),
                    txtBaseValue = valuesTr?.Find("Txt_BaseValue")?.GetComponent<TextMeshProUGUI>(),
                    txtCurrentValue = valuesTr?.Find("Txt_CurrentValue")?.GetComponent<TextMeshProUGUI>()
                };
                combatStatViews.Add(statView);
            }
        }
    }

    private void AutoBindPrioritySlots()
    {
        Transform slotsParent = transform.Find("Pnl_ColumnsContainer/Pnl_RightColumn/Pnl_PrioritySlots");
        if (slotsParent == null) return;

        if (prioritySlots == null) prioritySlots = new List<PrioritySlotView>();
        prioritySlots.Clear();

        for (int i = 1; i <= 3; i++)
        {
            Transform slotTr = slotsParent.Find("Slot_" + i);
            if (slotTr != null)
            {
                Button btn = slotTr.GetComponent<Button>();
                if (btn == null) btn = slotTr.gameObject.AddComponent<Button>();

                PrioritySlotView slotView = new PrioritySlotView
                {
                    slotObject = slotTr.gameObject,
                    slotButton = btn,
                    imgBackground = slotTr.GetComponent<Image>(),
                    txtSlotNumber = slotTr.Find("Txt_SlotNumber")?.GetComponent<TextMeshProUGUI>(),
                    txtContent = slotTr.Find("Txt_Content")?.GetComponent<TextMeshProUGUI>()
                };
                prioritySlots.Add(slotView);
            }
        }
    }

    private void AutoBindTargetingButtons()
    {
        Transform gridTr = transform.Find("Pnl_ColumnsContainer/Pnl_RightColumn/Pnl_TargetingOptionsGrid");
        if (gridTr == null) return;

        if (targetingButtons == null) targetingButtons = new List<TargetingButtonView>();
        targetingButtons.Clear();

        foreach (TargetingMode mode in (TargetingMode[])Enum.GetValues(typeof(TargetingMode)))
        {
            Transform btnTr = gridTr.Find("Btn_Target_" + mode);
            if (btnTr != null)
            {
                TargetingButtonView tb = new TargetingButtonView
                {
                    mode = mode,
                    button = btnTr.GetComponent<Button>(),
                    imgBackground = btnTr.GetComponent<Image>(),
                    txtLabel = btnTr.Find("Txt_Label")?.GetComponent<TextMeshProUGUI>()
                };
                targetingButtons.Add(tb);
            }
        }
    }

    // =========================================================================
    // SETUP I OBSŁUGA ZDARZEŃ
    // =========================================================================

    /// <summary>
    /// Otwiera i konfiguruje okno dla konkretnej wieży.
    /// </summary>
    public void Setup(TowerEntity tower)
    {
        if (tower == null) return;

        AutoBindReferences();
        UnsubscribeEvents();

        currentTower = tower;

        if (currentTower.controller == null)
        {
            currentTower.controller = currentTower.GetComponent<TowerController>();
        }

        // Subskrypcja zdarzeń
        if (currentTower.controller != null)
        {
            currentTower.controller.OnStatsChanged += OnTowerStatsChanged;
        }

        if (WorkforceManager.Instance != null)
        {
            WorkforceManager.Instance.OnWorkforceStatsChanged += OnWorkforceChanged;
        }

        // Podpięcie przycisku zamknięcia
        if (btnClose != null)
        {
            btnClose.onClick.RemoveAllListeners();
            btnClose.onClick.AddListener(CloseWindow);
        }

        // Podpięcie przycisków ras
        SetupRaceButtonListeners();

        // Podpięcie slotów priorytetów
        SetupPrioritySlotListeners();

        // Podpięcie przycisków trybów celowania
        SetupTargetingButtonListeners();

        // Pełne odświeżenie widoku
        RefreshAll();
    }

    private void UnsubscribeEvents()
    {
        if (currentTower != null && currentTower.controller != null)
        {
            currentTower.controller.OnStatsChanged -= OnTowerStatsChanged;
        }

        if (WorkforceManager.Instance != null)
        {
            WorkforceManager.Instance.OnWorkforceStatsChanged -= OnWorkforceChanged;
        }
    }

    private void OnTowerStatsChanged()
    {
        RefreshCombatStatsAndEfficiency();
        RefreshPrioritiesUI();
    }

    private void OnWorkforceChanged(Race race, WorkforceManager.WorkforceStats stats)
    {
        RefreshWorkersUI();
    }

    // =========================================================================
    // REFRESH METODY
    // =========================================================================

    /// <summary>
    /// Pełne odświeżenie wszystkich sekcji panelu.
    /// </summary>
    public void RefreshAll()
    {
        if (currentTower == null) return;

        // 1. Tytuł wieży
        if (txtTowerName != null)
        {
            string towerName = currentTower.data != null ? currentTower.data.buildingName : currentTower.name;
            txtTowerName.text = string.IsNullOrEmpty(towerName) ? "WIEŻA" : towerName.ToUpper();
        }

        // 2. Sekcje
        RefreshUpkeepUI();
        RefreshWorkersUI();
        RefreshCombatStatsAndEfficiency();
        RefreshPrioritiesUI();

        // Wymuś przebudowę układu
        ForceRebuildLayout();
    }

    // ── Utrzymanie (Upkeep) ───────────────────────────────────────────────────
    private void RefreshUpkeepUI()
    {
        if (secUpkeep == null || currentTower == null || currentTower.data == null) return;

        var upkeepList = currentTower.data.upkeepPerCycle;
        if (upkeepList == null || upkeepList.Count == 0)
        {
            // Brak kosztów utrzymania -> ukryj sekcję
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
                // Utwórz kolejny kafelek klonując pierwszy
                itemTr = Instantiate(pnlUpkeepResources.GetChild(0), pnlUpkeepResources, false);
                itemTr.gameObject.SetActive(true);
            }

            // Ustaw ikonę
            Image iconImg = itemTr.Find("Img_ResourceIcon")?.GetComponent<Image>();
            if (iconImg != null && resourceVisualsSO != null)
            {
                iconImg.sprite = resourceVisualsSO.GetIcon(cost.type);
                iconImg.color = iconImg.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            }

            // Ustaw ilość
            TextMeshProUGUI amtTmp = itemTr.Find("Txt_Amount")?.GetComponent<TextMeshProUGUI>();
            if (amtTmp != null)
            {
                amtTmp.text = "-" + cost.amount;
            }

            activeItems++;
        }

        // Wyłącz nadmiarowe elementy
        for (int i = activeItems; i < pnlUpkeepResources.childCount; i++)
        {
            pnlUpkeepResources.GetChild(i).gameObject.SetActive(false);
        }
    }

    // ── Załoga i Progi Wydajności ─────────────────────────────────────────────
    private void RefreshWorkersUI()
    {
        if (currentTower == null) return;

        int totalWorkers = currentTower.GetTotalWorkerCount();
        int maxWorkers = currentTower.data != null ? currentTower.data.baseMaxWorkers : 1;
        if (currentTower.Upgrades != null) maxWorkers += currentTower.Upgrades.LocalBonusWorkers;
        if (CityStatsManager.Instance != null) maxWorkers += CityStatsManager.Instance.globalBonusWorkersPerShift;

        // Podsumowanie
        if (txtWorkerCount != null)
        {
            txtWorkerCount.text = $"Pracownicy: {totalWorkers} / {maxWorkers}";
        }

        // Kafelki 3 ras
        foreach (var rc in raceControls)
        {
            int assignedCount = currentTower.GetWorkerCount(rc.race);
            int freeCount = WorkforceManager.Instance != null ? WorkforceManager.Instance.GetIdleCount(rc.race) : 0;

            if (rc.txtCount != null) rc.txtCount.text = assignedCount.ToString();
            if (rc.txtFree != null) rc.txtFree.text = $"Wolni: {freeCount}";

            if (rc.btnMinus != null) rc.btnMinus.interactable = assignedCount > 0;
            if (rc.btnPlus != null) rc.btnPlus.interactable = (totalWorkers < maxWorkers) && (freeCount > 0);
        }

        // Dynamiczne progi wydajności
        RefreshThresholdsUI(totalWorkers, maxWorkers);
    }

    private void RefreshThresholdsUI(int totalWorkers, int maxWorkers)
    {
        if (secEfficiencyThresholds == null) return;

        float[] effTable = currentTower.GetEfficencyTable();
        if (effTable == null || effTable.Length == 0) return;

        int currentThresholdLevel = Mathf.Clamp(totalWorkers, 0, maxWorkers);
        float currentEffVal = totalWorkers < effTable.Length ? effTable[totalWorkers] : effTable[effTable.Length - 1];
        if (!currentTower.hasSupplies) currentEffVal = Mathf.Max(0.1f, currentEffVal - 0.5f);

        // Nagłówek statusu
        if (txtCurrentThresholdStatus != null)
        {
            txtCurrentThresholdStatus.text = $"Poziom {currentThresholdLevel}: {Mathf.RoundToInt(currentEffVal * 100f)}%";
        }

        // Upewnij się, że mamy odpowiednią liczbę segmentów w kontenerze (od poziomu 0 do maxWorkers)
        int requiredSegments = maxWorkers + 1;
        while (thresholdSegments.Count < requiredSegments && thresholdSegments.Count > 0)
        {
            GameObject newSegObj = Instantiate(thresholdSegments[0].rootObject, pnlThresholdBarsContainer, false);
            AutoBindThresholdSegments();
        }

        // Paleta zieleni: odcień zieleni rośnie z kolejnymi poziomami
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

            float levelEff = i < effTable.Length ? effTable[i] : effTable[effTable.Length - 1];

            // Szerokość segmentu: wg procentowej wartości (np. 50% = flexibleWidth 0.5, 10% = 0.1)
            float stepDelta = i == 0 ? levelEff : (levelEff - (i - 1 < effTable.Length ? effTable[i - 1] : levelEff));
            if (seg.layoutElement != null)
            {
                seg.layoutElement.flexibleWidth = Mathf.Max(0.1f, stepDelta);
            }

            // Teksty
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

            // Wskaźnik ▲
            if (seg.txtMarker != null)
            {
                seg.txtMarker.text = isActive ? "▲" : "";
                seg.txtMarker.color = new Color(1f, 0.85f, 0.2f);
            }

            // Pasek i podświetlenie
            if (seg.imgBar != null)
            {
                Color baseColor = i < greenPalette.Length ? greenPalette[i] : greenPalette[greenPalette.Length - 1];
                if (isActive)
                {
                    // Rozjaśnij aktywny segment
                    seg.imgBar.color = new Color(Mathf.Min(1f, baseColor.r * 1.35f), Mathf.Min(1f, baseColor.g * 1.35f), Mathf.Min(1f, baseColor.b * 1.35f), 1f);
                }
                else
                {
                    seg.imgBar.color = new Color(baseColor.r * 0.7f, baseColor.g * 0.7f, baseColor.b * 0.7f, 0.75f);
                }
            }
        }
    }

    // ── Statystyki Bojowe na żywo ─────────────────────────────────────────────
    private void RefreshCombatStatsAndEfficiency()
    {
        if (currentTower == null || currentTower.controller == null) return;

        TowerStats currentStats = currentTower.controller.GetAllStats();
        TowerData tData = currentTower.controller.towerData;

        // Aktualizacja procentu wydajności w nagłówku
        int workerCount = currentTower.GetTotalWorkerCount();
        float[] effTable = currentTower.GetEfficencyTable();
        float curEff = workerCount < effTable.Length ? effTable[workerCount] : effTable[effTable.Length - 1];
        if (!currentTower.hasSupplies) curEff = Mathf.Max(0.1f, curEff - 0.5f);

        if (txtEfficiencyPercentage != null)
        {
            txtEfficiencyPercentage.text = $"Wydajność: {Mathf.RoundToInt(curEff * 100f)}%";
        }

        // Odświeżenie 8 statystyk
        foreach (var statView in combatStatViews)
        {
            if (statView.rootObject == null) continue;

            // Przypisanie ikony z bazy
            if (statView.imgIcon != null && resourceVisualsSO != null)
            {
                Sprite icon = resourceVisualsSO.GetIcon(statView.statType);
                if (icon != null)
                {
                    statView.imgIcon.sprite = icon;
                    statView.imgIcon.color = Color.white;
                }
            }

            float baseVal = 0f;
            float curVal = 0f;
            string unit = "";
            bool isFloat = true;
            bool isPercent = false;

            switch (statView.statType)
            {
                case BattleStats.Damage:
                    baseVal = tData != null ? tData.baseDamage : currentStats.damage;
                    curVal = currentStats.damage;
                    unit = " PKT";
                    isFloat = false;
                    break;

                case BattleStats.FireRate:
                    baseVal = tData != null ? tData.fireRate : currentStats.fireRate;
                    curVal = currentStats.fireRate;
                    unit = " /S";
                    isFloat = true;
                    break;

                case BattleStats.Range:
                    baseVal = tData != null ? tData.baseRange : currentStats.range;
                    curVal = currentStats.range;
                    unit = " M";
                    isFloat = false;
                    break;

                case BattleStats.CriticalChancel:
                    baseVal = tData != null ? tData.criticalChancel * 100f : currentStats.criticalChancel * 100f;
                    curVal = currentStats.criticalChancel * 100f;
                    unit = "%";
                    isFloat = false;
                    isPercent = true;
                    break;

                case BattleStats.CriticalMultiplier:
                    baseVal = tData != null ? tData.criticalDamageMultiplier * 100f : currentStats.criticalDamageMultiplier * 100f;
                    curVal = currentStats.criticalDamageMultiplier * 100f;
                    unit = "%";
                    isFloat = false;
                    isPercent = true;
                    break;

                case BattleStats.ArmorPenetration:
                    baseVal = tData != null ? tData.armorPenetration * 100f : currentStats.armorPenetration * 100f;
                    curVal = currentStats.armorPenetration * 100f;
                    unit = "%";
                    isFloat = false;
                    isPercent = true;
                    break;

                case BattleStats.MagicPenetration:
                    baseVal = tData != null ? tData.magicPenetration * 100f : currentStats.magicPenetration * 100f;
                    curVal = currentStats.magicPenetration * 100f;
                    unit = "%";
                    isFloat = false;
                    isPercent = true;
                    break;

                case BattleStats.DamageType:
                    FormatDamageTypeStat(statView, currentStats.damageType);
                    continue;
            }

            FormatNumericalStat(statView, baseVal, curVal, unit, isFloat, isPercent);
        }
    }

    private void FormatNumericalStat(CombatStatItemView view, float baseVal, float curVal, string unit, bool isFloat, bool isPercent)
    {
        string curStr = isFloat ? curVal.ToString("F2") + unit : Mathf.RoundToInt(curVal) + unit;
        string baseStr = isFloat ? baseVal.ToString("F2") + unit : Mathf.RoundToInt(baseVal) + unit;

        bool isModified = Mathf.Abs(curVal - baseVal) > 0.01f;

        if (view.txtBaseValue != null)
        {
            if (isModified)
            {
                view.txtBaseValue.gameObject.SetActive(true);
                view.txtBaseValue.text = $"<s>{baseStr}</s>";
            }
            else
            {
                view.txtBaseValue.gameObject.SetActive(false);
            }
        }

        if (view.txtCurrentValue != null)
        {
            view.txtCurrentValue.text = curStr;

            if (isModified)
            {
                // Zielony jeśli bonus, czerwony jeśli kara
                view.txtCurrentValue.color = curVal > baseVal
                    ? new Color(0.4f, 0.95f, 0.4f, 1f)
                    : new Color(0.95f, 0.35f, 0.35f, 1f);
            }
            else
            {
                view.txtCurrentValue.color = Color.white;
            }
        }
    }

    private void FormatDamageTypeStat(CombatStatItemView view, DamageType dmgType)
    {
        if (view.txtBaseValue != null) view.txtBaseValue.gameObject.SetActive(false);

        if (view.txtCurrentValue != null)
        {
            switch (dmgType)
            {
                case DamageType.Physical:
                    view.txtCurrentValue.text = "FIZYCZNE";
                    view.txtCurrentValue.color = new Color(0.95f, 0.45f, 0.25f, 1f);
                    break;
                case DamageType.Magic:
                    view.txtCurrentValue.text = "MAGICZNE";
                    view.txtCurrentValue.color = new Color(0.35f, 0.65f, 1f, 1f);
                    break;
                case DamageType.True:
                    view.txtCurrentValue.text = "CZYSTE";
                    view.txtCurrentValue.color = new Color(1f, 0.9f, 0.5f, 1f);
                    break;
            }
        }
    }

    // ── Priorytety Ostrzału ───────────────────────────────────────────────────
    private void RefreshPrioritiesUI()
    {
        if (currentTower == null || currentTower.controller == null) return;

        List<TargetingMode> activeRules = currentTower.controller.activeTargetingRules;
        if (activeRules == null) activeRules = new List<TargetingMode>();

        // 1. Trzy sloty poziomo
        for (int i = 0; i < prioritySlots.Count; i++)
        {
            var slot = prioritySlots[i];
            if (slot.slotObject == null) continue;

            if (i < activeRules.Count)
            {
                TargetingMode mode = activeRules[i];
                if (slot.txtContent != null)
                {
                    slot.txtContent.text = GetTargetingModeShortName(mode);
                    slot.txtContent.color = Color.white;
                }
                if (slot.imgBackground != null)
                {
                    slot.imgBackground.color = new Color(0.2f, 0.25f, 0.32f, 0.95f);
                }
            }
            else if (i == 0 && activeRules.Count == 0)
            {
                // Slot 1 pusty -> domyślny wyszarzony Closest
                if (slot.txtContent != null)
                {
                    slot.txtContent.text = "Najbliżej\n(Domyślny)";
                    slot.txtContent.color = new Color(0.55f, 0.6f, 0.65f, 0.7f);
                }
                if (slot.imgBackground != null)
                {
                    slot.imgBackground.color = new Color(0.13f, 0.14f, 0.17f, 0.9f);
                }
            }
            else
            {
                if (slot.txtContent != null)
                {
                    slot.txtContent.text = "[Pusty]";
                    slot.txtContent.color = new Color(0.35f, 0.35f, 0.35f, 0.6f);
                }
                if (slot.imgBackground != null)
                {
                    slot.imgBackground.color = new Color(0.11f, 0.12f, 0.15f, 0.9f);
                }
            }
        }

        // 2. Siatka 9 przycisków celowania
        foreach (var tb in targetingButtons)
        {
            if (tb.button == null) continue;

            bool isSelected = activeRules.Contains(tb.mode);
            if (tb.imgBackground != null)
            {
                tb.imgBackground.color = isSelected
                    ? new Color(0.2f, 0.45f, 0.75f, 0.95f) // Podświetlony aktywny cel
                    : new Color(0.16f, 0.18f, 0.22f, 0.95f);
            }

            if (tb.txtLabel != null)
            {
                tb.txtLabel.color = isSelected ? Color.white : new Color(0.85f, 0.85f, 0.85f);
            }
        }
    }

    private string GetTargetingModeShortName(TargetingMode mode)
    {
        switch (mode)
        {
            case TargetingMode.Closest: return "BLISKO";
            case TargetingMode.Furthest: return "DALEKO";
            case TargetingMode.FurthestOnTrack: return "START\nTRASY";
            case TargetingMode.ClosestOnTrack: return "KONIEC\nTRASY";
            case TargetingMode.MostHP: return "MAX HP";
            case TargetingMode.LeastHP: return "MIN HP";
            case TargetingMode.HighestThreat: return "ELITA\nBOSS";
            case TargetingMode.Random: return "LOSOWY";
            case TargetingMode.Chunk: return "PUNKT";
            default: return mode.ToString();
        }
    }

    // =========================================================================
    // OBSŁUGA INTERAKCJI UŻYTKOWNIKA
    // =========================================================================

    private void SetupRaceButtonListeners()
    {
        foreach (var rc in raceControls)
        {
            Race race = rc.race;
            if (rc.btnMinus != null)
            {
                rc.btnMinus.onClick.RemoveAllListeners();
                rc.btnMinus.onClick.AddListener(() => OnRemoveWorker(race));
            }
            if (rc.btnPlus != null)
            {
                rc.btnPlus.onClick.RemoveAllListeners();
                rc.btnPlus.onClick.AddListener(() => OnAddWorker(race));
            }
        }
    }

    private void SetupPrioritySlotListeners()
    {
        for (int i = 0; i < prioritySlots.Count; i++)
        {
            int slotIndex = i;
            var slot = prioritySlots[i];
            if (slot.slotButton != null)
            {
                slot.slotButton.onClick.RemoveAllListeners();
                slot.slotButton.onClick.AddListener(() => OnPrioritySlotClicked(slotIndex));
            }
        }
    }

    private void SetupTargetingButtonListeners()
    {
        foreach (var tb in targetingButtons)
        {
            TargetingMode mode = tb.mode;
            if (tb.button != null)
            {
                tb.button.onClick.RemoveAllListeners();
                tb.button.onClick.AddListener(() => OnTargetingOptionClicked(mode));
            }
        }
    }

    private void OnAddWorker(Race race)
    {
        if (currentTower == null) return;
        currentTower.TryAddWorker(race);
        RefreshWorkersUI();
    }

    private void OnRemoveWorker(Race race)
    {
        if (currentTower == null) return;
        currentTower.RemoveWorker(race);
        RefreshWorkersUI();
    }

    private void OnPrioritySlotClicked(int slotIndex)
    {
        if (currentTower == null || currentTower.controller == null) return;

        var rules = currentTower.controller.activeTargetingRules;
        if (rules != null && slotIndex < rules.Count)
        {
            rules.RemoveAt(slotIndex);
            RefreshPrioritiesUI();
        }
    }

    private void OnTargetingOptionClicked(TargetingMode mode)
    {
        if (currentTower == null || currentTower.controller == null) return;

        var rules = currentTower.controller.activeTargetingRules;
        if (rules == null)
        {
            rules = new List<TargetingMode>();
            currentTower.controller.activeTargetingRules = rules;
        }

        if (rules.Contains(mode))
        {
            // Kliknięcie już aktywnego celu -> usuń
            rules.Remove(mode);
        }
        else if (rules.Count < 3)
        {
            // Przydziel do kolejnego wolnego slotu
            rules.Add(mode);
        }

        RefreshPrioritiesUI();
    }

    /// <summary>
    /// Zamyka okno i czyści zaznaczenie wieży.
    /// </summary>
    public void CloseWindow()
    {
        UnsubscribeEvents();

        if (CenterInfoPanelControllerUI.Instance != null)
        {
            CenterInfoPanelControllerUI.Instance.HideAll();
        }
        else
        {
            gameObject.SetActive(false);
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
