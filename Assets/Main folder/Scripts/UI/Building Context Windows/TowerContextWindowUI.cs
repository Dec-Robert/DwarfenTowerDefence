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
        public CombatStatItemUI itemUI;
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
        public Image imgIcon;
    }

    [System.Serializable]
    public class TargetingButtonView
    {
        public TargetingMode mode;
        public Button button;
        public Image imgBackground;
        public TextMeshProUGUI txtLabel;
        public Image imgIcon;
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
    [SerializeField] private GameObject secCombatStats;
    [SerializeField] private TextMeshProUGUI txtCombatStatsTitle;
    [SerializeField] private Transform pnlCombatStatsGrid;
    [SerializeField] private List<CombatStatItemView> combatStatViews = new List<CombatStatItemView>();

    public GameObject SecCombatStats => secCombatStats;
    public TextMeshProUGUI TxtCombatStatsTitle => txtCombatStatsTitle;
    public Transform PnlCombatStatsGrid => pnlCombatStatsGrid;
    public List<CombatStatItemView> CombatStatViews => combatStatViews;

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
        if (secCombatStats == null)
            secCombatStats = transform.Find("Pnl_ColumnsContainer/Pnl_LeftColumn/Sec_CombatStats")?.gameObject;

        if (txtCombatStatsTitle == null)
            txtCombatStatsTitle = transform.Find("Pnl_ColumnsContainer/Pnl_LeftColumn/Sec_CombatStats/Txt_CombatStatsTitle")?.GetComponent<TextMeshProUGUI>();

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

        Transform raceParent = transform.Find("Pnl_ColumnsContainer/Pnl_LeftColumn/Sec_Workers/Pnl_RaceControllers")
            ?? transform.Find("Pnl_ColumnsContainer/Pnl_LeftColumn/Sec_Workers/Pnl_RaceControllers (1)");

        if (raceParent == null)
        {
            Transform secWorkers = transform.Find("Pnl_ColumnsContainer/Pnl_LeftColumn/Sec_Workers");
            if (secWorkers != null)
            {
                for (int i = 0; i < secWorkers.childCount; i++)
                {
                    var child = secWorkers.GetChild(i);
                    if (child.name.IndexOf("RaceController", StringComparison.OrdinalIgnoreCase) >= 0
                        || child.name.IndexOf("Race", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        raceParent = child;
                        break;
                    }
                }
            }
        }

        if (raceParent == null) return;

        foreach (var rc in raceControls)
        {
            string boxNameUpper = "Pnl_Race_" + rc.race.ToString().ToUpper();
            string boxNameDefault = "Pnl_Race_" + rc.race;
            Transform box = raceParent.Find(boxNameUpper) ?? raceParent.Find(boxNameDefault);

            if (box == null)
            {
                for (int i = 0; i < raceParent.childCount; i++)
                {
                    var child = raceParent.GetChild(i);
                    if (child.name.IndexOf(rc.race.ToString(), StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        box = child;
                        break;
                    }
                }
            }

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

    /// <summary>
    /// Zwraca zlokalizowaną nazwę danej statystyki bojowej.
    /// </summary>
    public static string GetLocalizedStatName(BattleStats stat)
    {
        return stat switch
        {
            BattleStats.Damage => LocalizationManager.Get("stat_damage", "Obrażenia"),
            BattleStats.FireRate => LocalizationManager.Get("stat_firerate", "Szybkostrzelność"),
            BattleStats.Range => LocalizationManager.Get("stat_range", "Zasięg"),
            BattleStats.DamageType => LocalizationManager.Get("stat_damagetype", "Typ obrażeń"),
            BattleStats.CriticalChancel => LocalizationManager.Get("stat_critchance", "Szansa na krytyk"),
            BattleStats.CriticalMultiplier => LocalizationManager.Get("stat_critmultiplier", "Mnożnik krytyczny"),
            BattleStats.ArmorPenetration => LocalizationManager.Get("stat_armorpen", "Przebicie pancerza"),
            BattleStats.MagicPenetration => LocalizationManager.Get("stat_magicpen", "Przebicie magii"),
            _ => stat.ToString()
        };
    }

    private void AutoBindCombatStats()
    {
        if (pnlCombatStatsGrid == null) return;

        var statMapping = new (string childName, BattleStats type, string[] aliases)[]
        {
            ("Pnl_CombatStatItem_Damage", BattleStats.Damage, new[] { "Damage", "Obrazenia", "Obrażenia", "Dmg" }),
            ("Pnl_CombatStatItem_AttackSpeed", BattleStats.FireRate, new[] { "AttackSpeed", "FireRate", "Szybkostrzelnosc", "Szybkostrzelność" }),
            ("Pnl_CombatStatItem_Range", BattleStats.Range, new[] { "Range", "Zasieg", "Zasięg" }),
            ("Pnl_CombatStatItem_DmgType", BattleStats.DamageType, new[] { "DmgType", "DamageType", "Typ" }),
            ("Pnl_CombatStatItem_CritChance", BattleStats.CriticalChancel, new[] { "CritChance", "CriticalChance", "Kryt" }),
            ("Pnl_CombatStatItem_CritDmg", BattleStats.CriticalMultiplier, new[] { "CritDmg", "CriticalMultiplier", "CritMult", "Mnożnik", "Mnoznik" }),
            ("Pnl_CombatStatItem_ArmorPen", BattleStats.ArmorPenetration, new[] { "ArmorPen", "ArmorPenetration", "Pancerz" }),
            ("Pnl_CombatStatItem_MagicPen", BattleStats.MagicPenetration, new[] { "MagicPen", "MagicPenetration", "Magia" })
        };

        if (combatStatViews == null) combatStatViews = new List<CombatStatItemView>();
        combatStatViews.Clear();

        for (int mIndex = 0; mIndex < statMapping.Length; mIndex++)
        {
            var map = statMapping[mIndex];
            Transform itemTr = pnlCombatStatsGrid.Find(map.childName);
            if (itemTr == null)
            {
                // Fallback 1: wyszukanie po tekście Txt_StatName lub aliasach w nazwie GameObjectu
                for (int i = 0; i < pnlCombatStatsGrid.childCount; i++)
                {
                    var child = pnlCombatStatsGrid.GetChild(i);
                    bool matched = false;

                    var nameTxt = child.Find("Txt_StatName")?.GetComponent<TextMeshProUGUI>();
                    if (nameTxt != null && !string.IsNullOrEmpty(nameTxt.text))
                    {
                        foreach (var alias in map.aliases)
                        {
                            if (nameTxt.text.IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                itemTr = child;
                                matched = true;
                                break;
                            }
                        }
                    }

                    if (!matched)
                    {
                        foreach (var alias in map.aliases)
                        {
                            if (child.name.IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                itemTr = child;
                                matched = true;
                                break;
                            }
                        }
                    }

                    if (matched) break;
                }
            }

            // Fallback 2: wyszukanie po indeksie w siatce
            if (itemTr == null && mIndex < pnlCombatStatsGrid.childCount)
            {
                itemTr = pnlCombatStatsGrid.GetChild(mIndex);
            }

            if (itemTr != null)
            {
                CombatStatItemUI itemUI = itemTr.GetComponent<CombatStatItemUI>();
                if (itemUI != null) itemUI.AutoFindReferences();

                Transform valuesTr = itemTr.Find("Pnl_Values");
                CombatStatItemView statView = new CombatStatItemView
                {
                    statType = map.type,
                    rootObject = itemTr.gameObject,
                    itemUI = itemUI,
                    imgIcon = itemUI != null && itemUI.ImgIcon != null ? itemUI.ImgIcon : itemTr.Find("Img_StatIcon")?.GetComponent<Image>(),
                    txtName = itemUI != null && itemUI.TxtName != null ? itemUI.TxtName : itemTr.Find("Txt_StatName")?.GetComponent<TextMeshProUGUI>(),
                    txtBaseValue = itemUI != null && itemUI.TxtBaseValue != null
                        ? itemUI.TxtBaseValue
                        : (valuesTr != null ? valuesTr.Find("Txt_BaseValue")?.GetComponent<TextMeshProUGUI>() : itemTr.Find("Txt_BaseValue")?.GetComponent<TextMeshProUGUI>()),
                    txtCurrentValue = itemUI != null && itemUI.TxtCurrentValue != null
                        ? itemUI.TxtCurrentValue
                        : (valuesTr != null ? valuesTr.Find("Txt_CurrentValue")?.GetComponent<TextMeshProUGUI>() : itemTr.Find("Txt_CurrentValue")?.GetComponent<TextMeshProUGUI>())
                };

                if (statView.txtName != null)
                {
                    statView.txtName.text = GetLocalizedStatName(statView.statType);
                }

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

                Transform iconTr = slotTr.Find("Img_Icon");
                Image iconImg = iconTr != null ? iconTr.GetComponent<Image>() : null;
                if (iconImg == null)
                {
                    GameObject iconGo = new GameObject("Img_Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    iconGo.transform.SetParent(slotTr, false);
                    RectTransform rt = iconGo.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(28f, 28f);
                    rt.anchoredPosition = new Vector2(10f, 0f);
                    iconImg = iconGo.GetComponent<Image>();
                    iconImg.raycastTarget = false;
                    iconImg.preserveAspect = true;
                }

                PrioritySlotView slotView = new PrioritySlotView
                {
                    slotObject = slotTr.gameObject,
                    slotButton = btn,
                    imgBackground = slotTr.GetComponent<Image>(),
                    txtSlotNumber = slotTr.Find("Txt_SlotNumber")?.GetComponent<TextMeshProUGUI>(),
                    txtContent = slotTr.Find("Txt_Content")?.GetComponent<TextMeshProUGUI>(),
                    imgIcon = iconImg
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
                Transform iconTr = btnTr.Find("Img_Icon");
                Image iconImg = iconTr != null ? iconTr.GetComponent<Image>() : null;
                if (iconImg == null)
                {
                    GameObject iconGo = new GameObject("Img_Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    iconGo.transform.SetParent(btnTr, false);
                    RectTransform rt = iconGo.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(32f, 32f);
                    rt.anchoredPosition = Vector2.zero;
                    iconImg = iconGo.GetComponent<Image>();
                    iconImg.raycastTarget = false;
                    iconImg.preserveAspect = true;
                }

                TextMeshProUGUI txt = btnTr.Find("Txt_Label")?.GetComponent<TextMeshProUGUI>();
                if (txt != null)
                {
                    txt.gameObject.SetActive(false);
                }

                TargetingButtonView tb = new TargetingButtonView
                {
                    mode = mode,
                    button = btnTr.GetComponent<Button>(),
                    imgBackground = btnTr.GetComponent<Image>(),
                    txtLabel = txt,
                    imgIcon = iconImg
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
        LocalizationManager.OnLanguageChanged += RefreshAll;
        RefreshAll();
    }

    private void UnsubscribeEvents()
    {
        LocalizationManager.OnLanguageChanged -= RefreshAll;
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
            string towerName = currentTower.data != null ? currentTower.data.DisplayName : currentTower.name;
            txtTowerName.text = string.IsNullOrEmpty(towerName) ? LocalizationManager.Get("UI_TOWER_DEFAULT_TITLE", "WIEŻA") : towerName.ToUpper();
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
        if (CityStatsManager.Instance != null) maxWorkers += CityStatsManager.Instance.globalBonusTowerCrewSlots;

        // Podsumowanie
        if (txtWorkerCount != null)
        {
            txtWorkerCount.text = LocalizationManager.GetFormat("UI_PROD_WORKERS_FMT", totalWorkers, maxWorkers);
        }

        // Kafelki 3 ras
        foreach (var rc in raceControls)
        {
            int assignedCount = currentTower.GetWorkerCount(rc.race);
            int freeCount = WorkforceManager.Instance != null ? WorkforceManager.Instance.GetIdleCount(rc.race) : 0;

            if (rc.txtCount != null) rc.txtCount.text = assignedCount.ToString();
            if (rc.txtFree != null) rc.txtFree.text = LocalizationManager.GetFormat("UI_PROD_FREE_WORKERS_FMT", freeCount);

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
        float currentEffVal = currentTower.GetCurrentEfficiency();

        // Nagłówek statusu
        if (txtCurrentThresholdStatus != null)
        {
            txtCurrentThresholdStatus.text = LocalizationManager.GetFormat("UI_PROD_LEVEL_FMT", currentThresholdLevel, Mathf.RoundToInt(currentEffVal * 100f));
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
        float curEff = currentTower.GetCurrentEfficiency();

        if (txtEfficiencyPercentage != null)
        {
            txtEfficiencyPercentage.text = LocalizationManager.GetFormat("UI_TOWER_EFFICIENCY_FMT", Mathf.RoundToInt(curEff * 100f));
        }

        // Aktualizacja tytułu sekcji statystyk bojowych
        if (txtCombatStatsTitle != null)
        {
            txtCombatStatsTitle.text = LocalizationManager.Get("UI_TOWER_COMBAT_STATS", "STATYSTYKI BOJOWE");
        }

        // Odświeżenie 8 statystyk
        foreach (var statView in combatStatViews)
        {
            if (statView.rootObject == null) continue;

            // Aktualizacja zlokalizowanej nazwy statystyki
            if (statView.txtName != null)
            {
                statView.txtName.text = GetLocalizedStatName(statView.statType);
            }

            // Przypisanie ikony z bazy jeśli widok zawiera Image
            if (statView.imgIcon != null)
            {
                if (resourceVisualsSO != null)
                {
                    Sprite icon = resourceVisualsSO.GetIcon(statView.statType);
                    if (icon != null)
                    {
                        statView.imgIcon.gameObject.SetActive(true);
                        statView.imgIcon.sprite = icon;
                        statView.imgIcon.color = Color.white;
                    }
                    else
                    {
                        statView.imgIcon.gameObject.SetActive(false);
                    }
                }
            }

            float baseVal = 0f;
            float curVal = 0f;
            string unit = "";
            bool isFloat = true;
            bool isPercent = false;
            int decimals = 2;

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
                    unit = "";
                    isFloat = (curVal % 1f != 0 || baseVal % 1f != 0);
                    decimals = 1;
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

            FormatNumericalStat(statView, baseVal, curVal, unit, isFloat, isPercent, decimals);
        }
    }

    private void FormatNumericalStat(CombatStatItemView view, float baseVal, float curVal, string unit, bool isFloat, bool isPercent, int decimals = 2)
    {
        string curStr = isFloat
            ? ((decimals == 1 && curVal % 1f == 0) ? Mathf.RoundToInt(curVal).ToString() : curVal.ToString($"F{decimals}")) + unit
            : Mathf.RoundToInt(curVal) + unit;
        string baseStr = isFloat
            ? ((decimals == 1 && baseVal % 1f == 0) ? Mathf.RoundToInt(baseVal).ToString() : baseVal.ToString($"F{decimals}")) + unit
            : Mathf.RoundToInt(baseVal) + unit;

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
                    view.txtCurrentValue.text = LocalizationManager.Get("UI_TOWER_DMG_PHYSICAL", "FIZYCZNE");
                    view.txtCurrentValue.color = new Color32(255, 140, 52, 255);
                    break;
                case DamageType.Magic:
                    view.txtCurrentValue.text = LocalizationManager.Get("UI_TOWER_DMG_MAGIC", "MAGICZNE");
                    view.txtCurrentValue.color = new Color32(0, 176, 240, 255);
                    break;
                case DamageType.True:
                    view.txtCurrentValue.text = LocalizationManager.Get("UI_TOWER_DMG_TRUE", "CZYSTE");
                    view.txtCurrentValue.color = Color.white;
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
                if (slot.imgIcon != null)
                {
                    slot.imgIcon.gameObject.SetActive(true);
                    if (resourceVisualsSO != null)
                    {
                        slot.imgIcon.sprite = resourceVisualsSO.GetIcon(mode);
                        slot.imgIcon.color = Color.white;
                    }
                }
                if (slot.txtContent != null)
                {
                    slot.txtContent.gameObject.SetActive(false);
                }
                if (slot.imgBackground != null)
                {
                    slot.imgBackground.color = new Color(0.2f, 0.25f, 0.32f, 0.95f);
                }
            }
            else if (i == 0 && activeRules.Count == 0)
            {
                // Slot 1 pusty -> domyślny wyszarzony Closest
                if (slot.imgIcon != null)
                {
                    slot.imgIcon.gameObject.SetActive(true);
                    if (resourceVisualsSO != null)
                    {
                        slot.imgIcon.sprite = resourceVisualsSO.GetIcon(TargetingMode.Closest);
                        slot.imgIcon.color = new Color(0.55f, 0.6f, 0.65f, 0.45f);
                    }
                }
                if (slot.txtContent != null)
                {
                    slot.txtContent.gameObject.SetActive(false);
                }
                if (slot.imgBackground != null)
                {
                    slot.imgBackground.color = new Color(0.13f, 0.14f, 0.17f, 0.9f);
                }
            }
            else
            {
                if (slot.imgIcon != null)
                {
                    slot.imgIcon.gameObject.SetActive(false);
                }
                if (slot.txtContent != null)
                {
                    slot.txtContent.gameObject.SetActive(true);
                    slot.txtContent.text = "-";
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

            if (tb.mode == TargetingMode.Chunk)
            {
                bool canTargetHex = currentTower.controller != null &&
                                    currentTower.controller.towerData != null &&
                                    currentTower.controller.towerData.canShootChunk;
                tb.button.gameObject.SetActive(canTargetHex);
                if (!canTargetHex) continue;
            }

            bool isSelectingThisHex = tb.mode == TargetingMode.Chunk &&
                                      InteractionManager.Instance != null &&
                                      InteractionManager.Instance.IsSelectingHexTarget;
            bool isSelected = (tb.mode == TargetingMode.Chunk && currentTower.controller != null && currentTower.controller.HasGroundTarget) || activeRules.Contains(tb.mode);

            if (tb.imgBackground != null)
            {
                if (isSelectingThisHex)
                {
                    // Lekkie jasne podświetlenie sygnalizujące tryb wyboru heksa na mapie
                    tb.imgBackground.color = new Color(0.35f, 0.65f, 0.95f, 1f);
                }
                else if (isSelected)
                {
                    tb.imgBackground.color = new Color(0.2f, 0.45f, 0.75f, 0.95f); // Podświetlony aktywny cel
                }
                else
                {
                    tb.imgBackground.color = new Color(0.16f, 0.18f, 0.22f, 0.95f);
                }
            }

            if (tb.imgIcon != null)
            {
                if (resourceVisualsSO != null)
                {
                    tb.imgIcon.sprite = resourceVisualsSO.GetIcon(tb.mode);
                }
                tb.imgIcon.color = (isSelected || isSelectingThisHex) ? Color.white : new Color(0.7f, 0.72f, 0.78f, 0.85f);
            }

            if (tb.txtLabel != null)
            {
                tb.txtLabel.gameObject.SetActive(false);
            }
        }
    }

    private string GetTargetingModeShortName(TargetingMode mode)
    {
        switch (mode)
        {
            case TargetingMode.Closest: return LocalizationManager.Get("UI_TARGETING_CLOSEST", "BLISKO");
            case TargetingMode.Furthest: return LocalizationManager.Get("UI_TARGETING_FURTHEST", "DALEKO");
            case TargetingMode.FurthestOnTrack: return LocalizationManager.Get("UI_TARGETING_FURTHEST_TRACK", "START\nTRASY");
            case TargetingMode.ClosestOnTrack: return LocalizationManager.Get("UI_TARGETING_CLOSEST_TRACK", "KONIEC\nTRASY");
            case TargetingMode.MostHP: return LocalizationManager.Get("UI_TARGETING_MOST_HP", "MAX HP");
            case TargetingMode.LeastHP: return LocalizationManager.Get("UI_TARGETING_LEAST_HP", "MIN HP");
            case TargetingMode.HighestThreat: return LocalizationManager.Get("UI_TARGETING_HIGHEST_THREAT", "ELITA\nBOSS");
            case TargetingMode.Random: return LocalizationManager.Get("UI_TARGETING_RANDOM", "LOSOWY");
            case TargetingMode.Chunk: return LocalizationManager.Get("UI_TARGETING_CHUNK", "PUNKT");
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
            if (rules[slotIndex] == TargetingMode.Chunk)
            {
                currentTower.controller.ClearGroundTarget();
            }
            else
            {
                rules.RemoveAt(slotIndex);
            }
            RefreshPrioritiesUI();
        }
    }

    private void OnTargetingOptionClicked(TargetingMode mode)
    {
        if (currentTower == null || currentTower.controller == null) return;

        if (mode == TargetingMode.Chunk)
        {
            if (InteractionManager.Instance != null && InteractionManager.Instance.IsSelectingHexTarget)
            {
                // Ponowne kliknięcie w trakcie wyboru -> anuluj tryb wyboru
                InteractionManager.Instance.CancelHexTargetSelection();
                RefreshPrioritiesUI();
                return;
            }

            if (currentTower.controller.HasGroundTarget)
            {
                currentTower.controller.ClearGroundTarget();
                RefreshPrioritiesUI();
            }
            else
            {
                InteractionManager.Instance?.StartHexTargetSelection(currentTower.controller, () =>
                {
                    RefreshPrioritiesUI();
                });
                RefreshPrioritiesUI();
            }
            return;
        }

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
