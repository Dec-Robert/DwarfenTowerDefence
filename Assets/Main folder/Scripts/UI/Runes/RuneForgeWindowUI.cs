using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Główny kontroler pełnoekranowego panelu Kuźni Runicznej (Pnl_RuneForgeFullscreen).
/// Obsługuje:
/// 1. Zakładkę Osadzania Run (View_Socketing) z modularnymi slotami Card_RunesmithSlot,
///    obsługującymi zarówno Drag & Drop jak i szybkie kliknięcia,
/// 2. Zakładkę Fuzji Run (View_Fusion) – łączenie 2 run tego samego poziomu w 1 wyższą,
/// 3. Zakładkę Transmutacji Run (View_Transmutation) – wymianę 3 run tego samego poziomu na losową wyższą,
/// 4. Prawy panel ekwipunku gracza z filtrami TMP_Dropdown (rzadkość oraz statystyka),
/// 5. Dolny panel podsumowania 8 statystyk bojowych (Pnl_CombatStatItem) oraz przydział kowali w Pnl_RaceControllers.
/// </summary>
public class RuneForgeWindowUI : MonoBehaviour
{
    public static RuneForgeWindowUI Instance { get; private set; }

    public enum RuneForgeTab
    {
        Socketing,
        Fusion,
        Transmutation
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

    // ── 1. Górna Belka ──────────────────────────────────────────────────────
    [Header("Górna Belka")]
    [SerializeField] private TextMeshProUGUI txtForgeTitle;
    [SerializeField] private Button btnClose;

    // ── 2. Lewy Panel Nawigacji ─────────────────────────────────────────────
    [Header("Lewy Panel Nawigacji")]
    [SerializeField] private Button btnTabSocketing;
    [SerializeField] private Button btnTabFusion;
    [SerializeField] private Button btnTabTransmutation;
    [SerializeField] private TextMeshProUGUI txtRuneTowersDesc;

    // ── 3. Środkowe Widoki ──────────────────────────────────────────────────
    [Header("Środkowe Widoki")]
    [SerializeField] private GameObject viewSocketing;
    [SerializeField] private GameObject viewFusion;
    [SerializeField] private GameObject viewTransmutation;

    // ── Widok 1: Osadzanie ──────────────────────────────────────────────────
    [Header("Widok 1: Osadzanie")]
    [SerializeField] private Image imgTowerHeaderIcon;
    [SerializeField] private TextMeshProUGUI txtTowerHeaderName;
    [SerializeField] private Transform pnlSlotsRow;

    // ── Widok 2: Fuzja ──────────────────────────────────────────────────────
    [Header("Widok 2: Fuzja")]
    [SerializeField] private Button btnFusionBackToTower;
    [SerializeField] private Transform slotFusion1;
    [SerializeField] private Transform slotFusion2;
    [SerializeField] private Transform slotFusionResult;
    [SerializeField] private TextMeshProUGUI txtFusionReqHeader;
    [SerializeField] private Button btnCraftFusion;

    // ── Widok 3: Transmutacja ───────────────────────────────────────────────
    [Header("Widok 3: Transmutacja")]
    [SerializeField] private Button btnTransmuteBackToTower;
    [SerializeField] private Transform slotTransmute1;
    [SerializeField] private Transform slotTransmute2;
    [SerializeField] private Transform slotTransmute3;
    [SerializeField] private Transform slotTransmuteResult;
    [SerializeField] private TextMeshProUGUI txtTransmuteReqHeader;
    [SerializeField] private Button btnCraftTransmute;

    // ── 4. Prawy Panel Ekwipunku ────────────────────────────────────────────
    [Header("Prawy Panel Ekwipunku")]
    [SerializeField] private TextMeshProUGUI txtInvCount;
    [SerializeField] private TMP_Dropdown ddRarity;
    [SerializeField] private TMP_Dropdown ddStat;
    [SerializeField] private Transform inventoryContent;
    [SerializeField] private ResourceVisualsSO resourceVisualsDB;

    public ResourceVisualsSO ResourceVisualsDB => resourceVisualsDB;

    // ── 5. Dolny Panel: Statystyki i Kowale ─────────────────────────────────
    [Header("Dolny Panel")]
    [SerializeField] private Transform pnlStatsGrid;
    [SerializeField] private List<CombatStatItemView> combatStatViews = new List<CombatStatItemView>();

    [Header("Sekcja Kowali (Dwarf Controller)")]
    [SerializeField] private GameObject pnlRaceDwarves;
    [SerializeField] private TextMeshProUGUI txtDwarfTitle;
    [SerializeField] private TextMeshProUGUI txtDwarfFree;
    [SerializeField] private TextMeshProUGUI txtDwarfCount;
    [SerializeField] private Button btnDwarfMinus;
    [SerializeField] private Button btnDwarfPlus;

    // ── Stan Bieżącej Kuźni ─────────────────────────────────────────────────
    private RuneForgeEntity currentForge;
    private RuneForgeTab currentTab = RuneForgeTab.Socketing;

    // Stan rzemiosła (Fuzja i Transmutacja)
    private RuneItem fusionRune1;
    private RuneItem fusionRune2;
    private RuneItem transmuteRune1;
    private RuneItem transmuteRune2;
    private RuneItem transmuteRune3;

    private readonly Color tabActiveBg = new Color(0.24f, 0.2f, 0.12f, 0.95f);
    private readonly Color tabActiveTxt = new Color(0.96f, 0.82f, 0.38f, 1f);
    private readonly Color tabInactiveBg = new Color(0.14f, 0.16f, 0.2f, 0.95f);
    private readonly Color tabInactiveTxt = new Color(0.72f, 0.75f, 0.8f, 1f);

    private void Awake()
    {
        Instance = this;
        AutoBindReferences();
    }

    private void OnValidate()
    {
        AutoBindReferences();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnsubscribeEvents();
    }

    // =========================================================================
    // INICJALIZACJA I AUTO-BINDING
    // =========================================================================

    public void AutoBindReferences()
    {
        Transform forgeRoot = transform.Find("Pnl_RuneForge") ?? transform;

        // 1. Górna Belka
        if (txtForgeTitle == null)
            txtForgeTitle = forgeRoot.Find("Pnl_TopBar/Txt_ForgeTitle")?.GetComponent<TextMeshProUGUI>();

        if (btnClose == null)
            btnClose = forgeRoot.Find("Pnl_TopBar/Btn_Close")?.GetComponent<Button>();

        // 2. Lewy Panel
        if (btnTabSocketing == null)
            btnTabSocketing = forgeRoot.Find("Pnl_BodyRow/Pnl_LeftNav/Btn_TabSocketing")?.GetComponent<Button>();

        if (btnTabFusion == null)
            btnTabFusion = forgeRoot.Find("Pnl_BodyRow/Pnl_LeftNav/Btn_TabFusion")?.GetComponent<Button>();

        if (btnTabTransmutation == null)
            btnTabTransmutation = forgeRoot.Find("Pnl_BodyRow/Pnl_LeftNav/Btn_TabTransmutation")?.GetComponent<Button>();

        if (txtRuneTowersDesc == null)
            txtRuneTowersDesc = forgeRoot.Find("Pnl_BodyRow/Pnl_LeftNav/Pnl_RuneTowersStatus/Txt_InfoDesc")?.GetComponent<TextMeshProUGUI>();

        // 3. Widoki Środkowe
        if (viewSocketing == null)
            viewSocketing = forgeRoot.Find("Pnl_BodyRow/Pnl_CenterAction/View_Socketing")?.gameObject;

        if (viewFusion == null)
            viewFusion = forgeRoot.Find("Pnl_BodyRow/Pnl_CenterAction/View_Fusion")?.gameObject;

        if (viewTransmutation == null)
            viewTransmutation = forgeRoot.Find("Pnl_BodyRow/Pnl_CenterAction/View_Transmutation")?.gameObject;

        // Widok 1: Osadzanie
        if (viewSocketing != null)
        {
            if (imgTowerHeaderIcon == null)
                imgTowerHeaderIcon = viewSocketing.transform.Find("Pnl_TowerHeaderCard/Img_TowerIconFrame/Img_TowerIcon")?.GetComponent<Image>()
                                  ?? viewSocketing.transform.Find("Pnl_TowerHeaderCard/Img_TowerIconFrame")?.GetComponent<Image>();

            if (txtTowerHeaderName == null)
                txtTowerHeaderName = viewSocketing.transform.Find("Pnl_TowerHeaderCard/Txt_TowerHeaderName")?.GetComponent<TextMeshProUGUI>();

            if (pnlSlotsRow == null)
                pnlSlotsRow = viewSocketing.transform.Find("Pnl_SlotsRow");
        }

        // Widok 2: Fuzja
        if (viewFusion != null)
        {
            if (btnFusionBackToTower == null)
                btnFusionBackToTower = viewFusion.transform.Find("Pnl_FusionTopBar/Btn_BackToTower")?.GetComponent<Button>();

            if (slotFusion1 == null)
                slotFusion1 = viewFusion.transform.Find("Pnl_AnvilFusion/Pnl_InputSlots/Slot_Fusion_1");

            if (slotFusion2 == null)
                slotFusion2 = viewFusion.transform.Find("Pnl_AnvilFusion/Pnl_InputSlots/Slot_Fusion_2");

            if (slotFusionResult == null)
                slotFusionResult = viewFusion.transform.Find("Pnl_AnvilFusion/Slot_Fusion_Result");

            if (txtFusionReqHeader == null)
                txtFusionReqHeader = viewFusion.transform.Find("Pnl_FusionReqs/Txt_ReqHeader")?.GetComponent<TextMeshProUGUI>();

            if (btnCraftFusion == null)
                btnCraftFusion = viewFusion.transform.Find("Btn_CraftFusion")?.GetComponent<Button>();
        }

        // Widok 3: Transmutacja
        if (viewTransmutation != null)
        {
            if (btnTransmuteBackToTower == null)
                btnTransmuteBackToTower = viewTransmutation.transform.Find("Pnl_TransmutationTopBar/Btn_BackToTower")?.GetComponent<Button>()
                                       ?? viewTransmutation.transform.Find("Pnl_TransmuteTopBar/Btn_BackToTower")?.GetComponent<Button>();

            if (slotTransmute1 == null)
                slotTransmute1 = viewTransmutation.transform.Find("Pnl_AnvilFusion/Pnl_InputSlots/Slot_Transmutation_1")
                              ?? viewTransmutation.transform.Find("Pnl_AnvilTransmute/Pnl_TransmuteInputs/Slot_Transmute_1");

            if (slotTransmute2 == null)
                slotTransmute2 = viewTransmutation.transform.Find("Pnl_AnvilFusion/Pnl_InputSlots/Slot_Transmutation_2")
                              ?? viewTransmutation.transform.Find("Pnl_AnvilTransmute/Pnl_TransmuteInputs/Slot_Transmute_2");

            if (slotTransmute3 == null)
                slotTransmute3 = viewTransmutation.transform.Find("Pnl_AnvilFusion/Pnl_InputSlots/Slot_Transmutation_3")
                              ?? viewTransmutation.transform.Find("Pnl_AnvilTransmute/Pnl_TransmuteInputs/Slot_Transmute_3");

            if (slotTransmuteResult == null)
                slotTransmuteResult = viewTransmutation.transform.Find("Pnl_AnvilFusion/Slot_Fusion_Result")
                                   ?? viewTransmutation.transform.Find("Pnl_AnvilTransmute/Slot_Transmute_Result");

            if (txtTransmuteReqHeader == null)
                txtTransmuteReqHeader = viewTransmutation.transform.Find("Pnl_FusionReqs/Txt_ReqHeader")?.GetComponent<TextMeshProUGUI>()
                                     ?? viewTransmutation.transform.Find("Pnl_TransmuteReqs/Txt_ReqHeader")?.GetComponent<TextMeshProUGUI>();

            if (btnCraftTransmute == null)
                btnCraftTransmute = viewTransmutation.transform.Find("Btn_CraftTransmutation")?.GetComponent<Button>()
                                 ?? viewTransmutation.transform.Find("Btn_CraftTransmute")?.GetComponent<Button>();
        }

        // 4. Ekwipunek
        if (txtInvCount == null)
            txtInvCount = forgeRoot.Find("Pnl_BodyRow/Pnl_RightInventory/Pnl_InvHeader/Txt_InvTitle")?.GetComponent<TextMeshProUGUI>()
                       ?? forgeRoot.Find("Pnl_BodyRow/Pnl_RightInventory/Pnl_InvHeader/Txt_InvCount")?.GetComponent<TextMeshProUGUI>();

        if (ddRarity == null)
            ddRarity = forgeRoot.Find("Pnl_BodyRow/Pnl_RightInventory/Pnl_FilterRow/Dd_Rarity")?.GetComponent<TMP_Dropdown>();

        if (ddStat == null)
            ddStat = forgeRoot.Find("Pnl_BodyRow/Pnl_RightInventory/Pnl_FilterRow/Dd_Stat")?.GetComponent<TMP_Dropdown>();

        if (inventoryContent == null)
            inventoryContent = forgeRoot.Find("Pnl_BodyRow/Pnl_RightInventory/Scroll_Inventory/Viewport/Content");

#if UNITY_EDITOR
        if (resourceVisualsDB == null)
        {
            resourceVisualsDB = UnityEditor.AssetDatabase.LoadAssetAtPath<ResourceVisualsSO>("Assets/Main folder/ScriptableObjects/Configs/ResourceVisualDatabase.asset");
        }
#endif

        // 5. Dolny Panel: Statystyki
        if (pnlStatsGrid == null)
            pnlStatsGrid = forgeRoot.Find("Pnl_BottomPanel/Sec_TowerStats/Pnl_StatsGrid");

        AutoBindCombatStats();

        // 6. Dolny Panel: Sekcja Kowali (Pnl_RaceControllers)
        Transform raceControllers = forgeRoot.Find("Pnl_BottomPanel/Sec_RuneSmith/Pnl_RaceControllers")
                                 ?? forgeRoot.Find("Pnl_BottomPanel/Sec_Runesmiths/Pnl_RaceControllers");

        if (raceControllers != null)
        {
            Transform humans = raceControllers.Find("Pnl_Race_HUMANS") ?? raceControllers.Find("Pnl_Race_Humans");
            if (humans != null) humans.gameObject.SetActive(false);

            Transform elves = raceControllers.Find("Pnl_Race_ELVES") ?? raceControllers.Find("Pnl_Race_Elves");
            if (elves != null) elves.gameObject.SetActive(false);

            Transform dwarves = raceControllers.Find("Pnl_Race_DWARVES") ?? raceControllers.Find("Pnl_Race_Dwarves");
            if (dwarves != null)
            {
                pnlRaceDwarves = dwarves.gameObject;
                pnlRaceDwarves.SetActive(true);

                if (txtDwarfTitle == null) txtDwarfTitle = dwarves.Find("Txt_RaceTitle")?.GetComponent<TextMeshProUGUI>();
                if (txtDwarfFree == null) txtDwarfFree = dwarves.Find("Txt_FreeWorkers")?.GetComponent<TextMeshProUGUI>();

                Transform btnRow = dwarves.Find("Pnl_Buttons");
                if (btnRow != null)
                {
                    if (btnDwarfMinus == null) btnDwarfMinus = btnRow.Find("Btn_Minus")?.GetComponent<Button>();
                    if (txtDwarfCount == null) txtDwarfCount = btnRow.Find("Txt_Count")?.GetComponent<TextMeshProUGUI>();
                    if (btnDwarfPlus == null) btnDwarfPlus = btnRow.Find("Btn_Plus")?.GetComponent<Button>();
                }
            }
        }

        // Kontroler Drag & Drop
        if (GetComponent<RuneDragDropController>() == null)
        {
            gameObject.AddComponent<RuneDragDropController>();
        }
    }

    private void AutoBindCombatStats()
    {
        if (pnlStatsGrid == null) return;

        if (combatStatViews == null) combatStatViews = new List<CombatStatItemView>();
        combatStatViews.Clear();

        var canonicalStats = new (BattleStats stat, string label)[]
        {
            (BattleStats.Damage, "Obrażenia"),
            (BattleStats.FireRate, "Szybkostrzelność"),
            (BattleStats.Range, "Zasięg"),
            (BattleStats.DamageType, "Typ obrażeń"),
            (BattleStats.CriticalChancel, "Szansa na krytyk"),
            (BattleStats.CriticalMultiplier, "Mnożnik krytyczny"),
            (BattleStats.ArmorPenetration, "Przebicie pancerza"),
            (BattleStats.MagicPenetration, "Przebicie magii")
        };

        for (int i = 0; i < pnlStatsGrid.childCount; i++)
        {
            if (i >= canonicalStats.Length) break;

            Transform itemTr = pnlStatsGrid.GetChild(i);
            var (statType, defaultLabel) = canonicalStats[i];

            Transform valuesTr = itemTr.Find("Pnl_Values");
            var nameTmp = itemTr.Find("Txt_StatName")?.GetComponent<TextMeshProUGUI>();
            if (nameTmp != null) nameTmp.text = defaultLabel;

            CombatStatItemView statView = new CombatStatItemView
            {
                statType = statType,
                rootObject = itemTr.gameObject,
                imgIcon = itemTr.Find("Img_StatIcon")?.GetComponent<Image>(),
                txtName = nameTmp,
                txtBaseValue = valuesTr?.Find("Txt_BaseValue")?.GetComponent<TextMeshProUGUI>(),
                txtCurrentValue = valuesTr?.Find("Txt_CurrentValue")?.GetComponent<TextMeshProUGUI>()
            };

            combatStatViews.Add(statView);
        }
    }

    // =========================================================================
    // KONFIGURACJA I OTWARCIE (SETUP)
    // =========================================================================

    public void Setup(RuneForgeEntity forge)
    {
        AutoBindReferences();
        UnsubscribeEvents();

        currentForge = forge;
        gameObject.SetActive(true);

        if (currentForge == null) return;

        SubscribeEvents();
        RegisterButtons();
        InitializeFilterDropdowns();

        SwitchTab(RuneForgeTab.Socketing);
        RefreshAll();
        ForceRebuildImmediate();
    }

    private void SubscribeEvents()
    {
        if (RuneManager.Instance != null)
        {
            RuneManager.Instance.OnInventoryChanged += HandleInventoryChanged;
        }

        if (WorkforceManager.Instance != null)
        {
            WorkforceManager.Instance.OnWorkforceStatsChanged += HandleWorkforceChanged;
            WorkforceManager.Instance.OnWorkerAssigned += HandleWorkerAssigned;
            WorkforceManager.Instance.OnWorkerUnassigned += HandleWorkerUnassigned;
        }

        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted += HandleMorningStarted;
        }
    }

    private void UnsubscribeEvents()
    {
        if (RuneManager.Instance != null)
        {
            RuneManager.Instance.OnInventoryChanged -= HandleInventoryChanged;
        }

        if (WorkforceManager.Instance != null)
        {
            WorkforceManager.Instance.OnWorkforceStatsChanged -= HandleWorkforceChanged;
            WorkforceManager.Instance.OnWorkerAssigned -= HandleWorkerAssigned;
            WorkforceManager.Instance.OnWorkerUnassigned -= HandleWorkerUnassigned;
        }

        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= HandleMorningStarted;
        }
    }

    private void RegisterButtons()
    {
        if (btnClose != null)
        {
            btnClose.onClick.RemoveAllListeners();
            btnClose.onClick.AddListener(CloseWindow);
        }

        // Zakładki
        if (btnTabSocketing != null)
        {
            btnTabSocketing.onClick.RemoveAllListeners();
            btnTabSocketing.onClick.AddListener(() => SwitchTab(RuneForgeTab.Socketing));
        }

        if (btnTabFusion != null)
        {
            btnTabFusion.onClick.RemoveAllListeners();
            btnTabFusion.onClick.AddListener(() => SwitchTab(RuneForgeTab.Fusion));
        }

        if (btnTabTransmutation != null)
        {
            btnTabTransmutation.onClick.RemoveAllListeners();
            btnTabTransmutation.onClick.AddListener(() => SwitchTab(RuneForgeTab.Transmutation));
        }

        if (btnFusionBackToTower != null)
        {
            btnFusionBackToTower.onClick.RemoveAllListeners();
            btnFusionBackToTower.onClick.AddListener(() => SwitchTab(RuneForgeTab.Socketing));
        }

        if (btnTransmuteBackToTower != null)
        {
            btnTransmuteBackToTower.onClick.RemoveAllListeners();
            btnTransmuteBackToTower.onClick.AddListener(() => SwitchTab(RuneForgeTab.Socketing));
        }

        // Rzemiosło
        if (btnCraftFusion != null)
        {
            btnCraftFusion.onClick.RemoveAllListeners();
            btnCraftFusion.onClick.AddListener(ExecuteFusion);
        }

        if (btnCraftTransmute != null)
        {
            btnCraftTransmute.onClick.RemoveAllListeners();
            btnCraftTransmute.onClick.AddListener(ExecuteTransmutation);
        }

        // Kowale w Pnl_Race_DWARVES
        if (btnDwarfMinus != null)
        {
            btnDwarfMinus.onClick.RemoveAllListeners();
            btnDwarfMinus.onClick.AddListener(HandleRemoveSmith);
        }

        if (btnDwarfPlus != null)
        {
            btnDwarfPlus.onClick.RemoveAllListeners();
            btnDwarfPlus.onClick.AddListener(HandleAddSmith);
        }
    }

    private void InitializeFilterDropdowns()
    {
        if (ddRarity != null)
        {
            ddRarity.ClearOptions();
            ddRarity.AddOptions(new List<string> {
                "Wszystkie rzadkości",
                "Zwykłe (Common)",
                "Niepospolite (Uncommon)",
                "Rzadkie (Rare)",
                "Legendarne (Legendary)",
                "Przeklęte (Cursed)"
            });
            ddRarity.onValueChanged.RemoveAllListeners();
            ddRarity.onValueChanged.AddListener(_ => RefreshInventoryView());
        }

        if (ddStat != null)
        {
            ddStat.ClearOptions();
            ddStat.AddOptions(new List<string> {
                "Wszystkie statystyki",
                "Obrażenia",
                "Zasięg",
                "Szybkostrzelność",
                "Przebicie pancerza",
                "Przebicie magii",
                "Szansa na krytyk",
                "Mnożnik krytyczny"
            });
            ddStat.onValueChanged.RemoveAllListeners();
            ddStat.onValueChanged.AddListener(_ => RefreshInventoryView());
        }
    }

    private void HandleInventoryChanged()
    {
        RefreshInventoryView();
        RefreshCraftingSlots();
        RefreshBottomStats();
    }

    private void HandleWorkforceChanged(Race race, WorkforceManager.WorkforceStats stats)
    {
        RefreshRunesmiths();
        RefreshSocketingView();
    }

    private void HandleWorkerAssigned(Citizen citizen, BuildingEntity building)
    {
        if (building == currentForge)
        {
            RefreshRunesmiths();
            RefreshSocketingView();
        }
    }

    private void HandleWorkerUnassigned(Citizen citizen, BuildingEntity building)
    {
        if (building == currentForge)
        {
            RefreshRunesmiths();
            RefreshSocketingView();
        }
    }

    private void HandleMorningStarted()
    {
        RefreshAll();
    }

    // =========================================================================
    // NAWIGACJA ZAKŁADEK
    // =========================================================================

    public void SwitchTab(RuneForgeTab tab)
    {
        currentTab = tab;

        if (viewSocketing != null) viewSocketing.SetActive(tab == RuneForgeTab.Socketing);
        if (viewFusion != null) viewFusion.SetActive(tab == RuneForgeTab.Fusion);
        if (viewTransmutation != null) viewTransmutation.SetActive(tab == RuneForgeTab.Transmutation);

        UpdateTabButtonStyles();

        // Czyścimy kowadło przy przełączaniu
        if (tab == RuneForgeTab.Fusion)
        {
            fusionRune1 = null;
            fusionRune2 = null;
        }
        else if (tab == RuneForgeTab.Transmutation)
        {
            transmuteRune1 = null;
            transmuteRune2 = null;
            transmuteRune3 = null;
        }

        RefreshAll();
    }

    private void UpdateTabButtonStyles()
    {
        SetTabStyle(btnTabSocketing, currentTab == RuneForgeTab.Socketing);
        SetTabStyle(btnTabFusion, currentTab == RuneForgeTab.Fusion);
        SetTabStyle(btnTabTransmutation, currentTab == RuneForgeTab.Transmutation);
    }

    private void SetTabStyle(Button btn, bool isActive)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = isActive ? tabActiveBg : tabInactiveBg;

        var txt = btn.transform.Find("Txt_Label")?.GetComponent<TextMeshProUGUI>();
        if (txt != null) txt.color = isActive ? tabActiveTxt : tabInactiveTxt;
    }

    // =========================================================================
    // ODŚWIEŻANIE WIDOKÓW (REFRESH)
    // =========================================================================

    public void RefreshAll()
    {
        if (currentForge == null) return;

        RefreshTopBar();
        RefreshLeftNav();
        RefreshSocketingView();
        RefreshCraftingSlots();
        RefreshInventoryView();
        RefreshBottomStats();
        RefreshRunesmiths();
    }

    private void RefreshTopBar()
    {
        if (txtForgeTitle != null)
        {
            string towerName = currentForge.infusedTowerData != null ? currentForge.infusedTowerData.buildingName.ToUpper() : "BRAK PRZYPISANEJ WIEŻY";
            txtForgeTitle.text = $"KUŹNIA RUNICZNA — [{towerName}]";
        }
    }

    private void RefreshLeftNav()
    {
        if (txtRuneTowersDesc != null && currentForge != null)
        {
            int towersCount = currentForge.GetNeighboringRuneTowersCount();
            int maxSlots = currentForge.GetMaxSlots();
            txtRuneTowersDesc.text = $"Sąsiednie wieże runiczne: <b>{towersCount}/4</b>\nOdblokowane sloty: <b>{maxSlots}/5</b>";
        }
    }

    private void RefreshSocketingView()
    {
        if (currentForge == null || viewSocketing == null) return;

        var tower = currentForge.infusedTowerData;
        if (txtTowerHeaderName != null)
        {
            txtTowerHeaderName.text = tower != null ? tower.buildingName.ToUpper() : "NIE WYBRANO WIEŻY";
        }

        if (imgTowerHeaderIcon != null)
        {
            imgTowerHeaderIcon.sprite = tower != null ? tower.icon : null;
            imgTowerHeaderIcon.color = tower != null && tower.icon != null ? Color.white : new Color(0.96f, 0.78f, 0.28f, 0.8f);
        }

        // Sloty na runy (Card_RunesmithSlot)
        if (pnlSlotsRow != null)
        {
            int maxSlots = currentForge.GetMaxSlots();
            int activeSlots = currentForge.GetActiveRuneSlots();
            var equipped = currentForge.equippedRunes ?? new List<RuneItem>();

            for (int i = 0; i < pnlSlotsRow.childCount; i++)
            {
                int slotIndex = i;
                Transform slotTr = pnlSlotsRow.GetChild(i);
                slotTr.gameObject.SetActive(true);

                bool isUnlockedByTower = slotIndex < maxSlots;
                bool hasSmith = slotIndex < activeSlots;
                RuneItem currentRune = slotIndex < equipped.Count ? equipped[slotIndex] : null;

                // Elementy prefabu Card_RunesmithSlot
                Transform badgeMissingSmith = slotTr.Find("Badge_MissingSmith");
                Transform lockObj = slotTr.Find("Img_locked");
                Transform socketIconRoot = slotTr.Find("Img_SocketIcon");
                Image iconImg = slotTr.Find("Img_SocketIcon/Img_SocketIcon")?.GetComponent<Image>()
                             ?? socketIconRoot?.GetComponentInChildren<Image>(true)
                             ?? socketIconRoot?.GetComponent<Image>()
                             ?? slotTr.Find("Img_Icon")?.GetComponent<Image>();
                TextMeshProUGUI lblTmp = slotTr.Find("Txt_SlotLabel")?.GetComponent<TextMeshProUGUI>();

                // Komponent interaktywny Drag & Drop + Click
                var slotScript = slotTr.GetComponent<RuneSocketSlotUI>();
                if (slotScript == null) slotScript = slotTr.gameObject.AddComponent<RuneSocketSlotUI>();
                slotScript.Setup(slotIndex, currentRune, isUnlockedByTower, hasSmith, this);

                if (!isUnlockedByTower)
                {
                    // 1. Zablokowany (brak wieży)
                    if (lockObj != null) lockObj.gameObject.SetActive(true);
                    if (badgeMissingSmith != null) badgeMissingSmith.gameObject.SetActive(false);
                    if (iconImg != null) iconImg.gameObject.SetActive(false);
                    if (lblTmp != null)
                    {
                        lblTmp.text = $"<color=#666666>SLOT {slotIndex + 1}</color>\n<size=10><color=#555555>WYMAGA WIEŻY</color></size>";
                    }
                }
                else if (!hasSmith)
                {
                    // 2. Odblokowany przez wieżę, ale brak kowala
                    if (lockObj != null) lockObj.gameObject.SetActive(false);
                    if (badgeMissingSmith != null) badgeMissingSmith.gameObject.SetActive(true);
                    if (iconImg != null)
                    {
                        if (currentRune != null)
                        {
                            iconImg.gameObject.SetActive(true);
                            iconImg.sprite = currentRune.definition?.icon;
                            iconImg.color = currentRune.definition?.icon != null ? new Color(1f, 1f, 1f, 0.45f) : new Color(0.96f, 0.78f, 0.28f, 0.45f);
                            iconImg.preserveAspect = true;
                        }
                        else
                        {
                            iconImg.gameObject.SetActive(true);
                            iconImg.sprite = null;
                            iconImg.color = new Color(0.96f, 0.78f, 0.28f, 0.35f);
                        }
                    }
                    if (lblTmp != null)
                    {
                        lblTmp.text = $"<color=#CCCCCC>SLOT {slotIndex + 1}</color>\n<size=10><color=#FFCC66>WYMAGA KOWALA</color></size>";
                    }
                }
                else
                {
                    // 3. Aktywny slot (z kowalem)
                    if (lockObj != null) lockObj.gameObject.SetActive(false);
                    if (badgeMissingSmith != null) badgeMissingSmith.gameObject.SetActive(false);

                    if (currentRune != null)
                    {
                        // Runa osadzona - żółta ikonka zamienia się w ikonkę osadzonej runy
                        if (iconImg != null)
                        {
                            iconImg.gameObject.SetActive(true);
                            iconImg.sprite = currentRune.definition?.icon;
                            iconImg.color = currentRune.definition?.icon != null ? Color.white : GetRarityColor(currentRune.definition != null ? currentRune.definition.rarity : RuneRarity.Common);
                            iconImg.preserveAspect = true;
                        }

                        if (lblTmp != null)
                        {
                            string labelText = $"<b>{currentRune.definition?.runeName}</b>\n<size=10>{currentRune.GetPrimaryText()}</size>";
                            if (currentRune.hasPenalty)
                            {
                                labelText += $"\n<size=9>{currentRune.GetPenaltyText()}</size>";
                            }
                            lblTmp.text = labelText;
                        }
                    }
                    else
                    {
                        // Wolny slot - domyślna żółta ikonka pustego slotu
                        if (iconImg != null)
                        {
                            iconImg.gameObject.SetActive(true);
                            iconImg.sprite = null;
                            iconImg.color = new Color(0.96f, 0.78f, 0.28f, 0.8f);
                        }
                        if (lblTmp != null)
                        {
                            lblTmp.text = $"<color=white>SLOT {slotIndex + 1}</color>\n<size=10><color=#888888>WOLNY</color></size>";
                        }
                    }
                }
            }
        }
    }

    // =========================================================================
    // OBSŁUGA INTERAKCJI: SLOTY WIEŻY
    // =========================================================================

    public void OnSlotClicked(int slotIndex)
    {
        if (currentForge == null) return;
        var equipped = currentForge.equippedRunes;
        if (equipped != null && slotIndex < equipped.Count)
        {
            var rune = equipped[slotIndex];
            currentForge.UnequipRune(rune);
            RefreshAll();
        }
    }

    public void OnRuneDroppedOnSlot(int targetSlotIndex, RuneItem rune, RuneDragSource source, int sourceIndex)
    {
        if (currentForge == null || rune == null) return;

        if (source == RuneDragSource.Inventory)
        {
            // Jeśli slot ma już runę, zdejmij starą i wstaw nową
            if (targetSlotIndex < currentForge.equippedRunes.Count)
            {
                var oldRune = currentForge.equippedRunes[targetSlotIndex];
                currentForge.UnequipRune(oldRune);
            }

            currentForge.EquipRune(rune);
            RefreshAll();
        }
        else if (source == RuneDragSource.Socket)
        {
            // Zamiana miejsc między slotami
            if (sourceIndex >= 0 && sourceIndex < currentForge.equippedRunes.Count && targetSlotIndex < currentForge.equippedRunes.Count)
            {
                var temp = currentForge.equippedRunes[targetSlotIndex];
                currentForge.equippedRunes[targetSlotIndex] = currentForge.equippedRunes[sourceIndex];
                currentForge.equippedRunes[sourceIndex] = temp;
                currentForge.ForceRescan();
                RefreshAll();
            }
        }
        else if (source == RuneDragSource.Fusion || source == RuneDragSource.Transmutation)
        {
            // Usunięcie z kowadła i wstawienie do slotu
            ClearCraftSlot(source, sourceIndex);
            if (targetSlotIndex < currentForge.equippedRunes.Count)
            {
                var oldRune = currentForge.equippedRunes[targetSlotIndex];
                currentForge.UnequipRune(oldRune);
            }
            currentForge.EquipRune(rune);
            RefreshAll();
        }
    }

    // =========================================================================
    // OBSŁUGA INTERAKCJI: EKWIPUNEK
    // =========================================================================

    public void OnInventoryRuneClicked(RuneItem rune)
    {
        if (rune == null) return;

        if (currentTab == RuneForgeTab.Socketing)
        {
            if (currentForge != null)
            {
                currentForge.EquipRune(rune);
                RefreshAll();
            }
        }
        else if (currentTab == RuneForgeTab.Fusion)
        {
            if (fusionRune1 == null) fusionRune1 = rune;
            else if (fusionRune2 == null && rune != fusionRune1) fusionRune2 = rune;
            RefreshCraftingSlots();
            RefreshInventoryView();
        }
        else if (currentTab == RuneForgeTab.Transmutation)
        {
            if (transmuteRune1 == null) transmuteRune1 = rune;
            else if (transmuteRune2 == null && rune != transmuteRune1) transmuteRune2 = rune;
            else if (transmuteRune3 == null && rune != transmuteRune1 && rune != transmuteRune2) transmuteRune3 = rune;
            RefreshCraftingSlots();
            RefreshInventoryView();
        }
    }

    private void RefreshInventoryView()
    {
        if (inventoryContent == null || RuneManager.Instance == null) return;

        var allRunes = RuneManager.Instance.playerRunes ?? new List<RuneItem>();
        var filtered = new List<RuneItem>();

        RuneRarity? selectedRarity = GetSelectedRarityFilter();
        RuneStatType? selectedStat = GetSelectedStatFilter();

        foreach (var r in allRunes)
        {
            if (r == null || r.definition == null) continue;

            // Pomiń runy leżące aktualnie na kowadle
            if (r == fusionRune1 || r == fusionRune2) continue;
            if (r == transmuteRune1 || r == transmuteRune2 || r == transmuteRune3) continue;

            if (selectedRarity.HasValue && r.definition.rarity != selectedRarity.Value)
                continue;

            if (selectedStat.HasValue && r.definition.primaryStat != selectedStat.Value)
                continue;

            filtered.Add(r);
        }

        if (txtInvCount != null)
        {
            txtInvCount.text = $"EKWIPUNEK RUN ({filtered.Count}/{allRunes.Count})";
        }

        int activeCount = 0;
        for (int i = 0; i < filtered.Count; i++)
        {
            var rune = filtered[i];
            Transform itemTr;

            if (i < inventoryContent.childCount)
            {
                itemTr = inventoryContent.GetChild(i);
                itemTr.gameObject.SetActive(true);
            }
            else
            {
                if (inventoryContent.childCount > 0)
                {
                    itemTr = Instantiate(inventoryContent.GetChild(0), inventoryContent, false);
                    itemTr.gameObject.SetActive(true);
                }
                else break;
            }

            SetupInventoryRuneCard(itemTr, rune);
            activeCount++;
        }

        for (int i = activeCount; i < inventoryContent.childCount; i++)
        {
            inventoryContent.GetChild(i).gameObject.SetActive(false);
        }
    }

    private void SetupInventoryRuneCard(Transform itemTr, RuneItem rune)
    {
        if (itemTr == null || rune == null) return;

        var cardScript = itemTr.GetComponent<RuneCardItemUI>();
        if (cardScript == null) cardScript = itemTr.gameObject.AddComponent<RuneCardItemUI>();
        cardScript.Setup(rune, this, resourceVisualsDB);
    }

    private RuneRarity? GetSelectedRarityFilter()
    {
        if (ddRarity == null || ddRarity.value == 0) return null;

        return ddRarity.value switch
        {
            1 => RuneRarity.Common,
            2 => RuneRarity.Uncommon,
            3 => RuneRarity.Rare,
            4 => RuneRarity.Legendary,
            5 => RuneRarity.Cursed,
            _ => null
        };
    }

    private RuneStatType? GetSelectedStatFilter()
    {
        if (ddStat == null || ddStat.value == 0) return null;

        return ddStat.value switch
        {
            1 => RuneStatType.Damage,
            2 => RuneStatType.Range,
            3 => RuneStatType.FireRate,
            4 => RuneStatType.ArmorPenetration,
            5 => RuneStatType.MagicPenetration,
            6 => RuneStatType.CriticalChance,
            7 => RuneStatType.CriticalDamage,
            _ => null
        };
    }

    // =========================================================================
    // OBSŁUGA KOWADŁA: FUZJA I TRANSMUTACJA
    // =========================================================================

    private void RefreshCraftingSlots()
    {
        // ── 1. Fuzja ────────────────────────────────────────────────────────
        SetupCraftSlot(slotFusion1, RuneDragSource.Fusion, 0, fusionRune1);
        SetupCraftSlot(slotFusion2, RuneDragSource.Fusion, 1, fusionRune2);

        bool canFuse = fusionRune1 != null && fusionRune2 != null &&
                       fusionRune1.definition.rarity == fusionRune2.definition.rarity &&
                       fusionRune1.definition.primaryStat == fusionRune2.definition.primaryStat &&
                       fusionRune1.definition.rarity != RuneRarity.Cursed;

        SetupResultSlot(slotFusionResult, canFuse, canFuse ? GetNextRarity(fusionRune1.definition.rarity) : null);

        if (txtFusionReqHeader != null)
        {
            int placed = (fusionRune1 != null ? 1 : 0) + (fusionRune2 != null ? 1 : 0);
            txtFusionReqHeader.text = $"WYMAGANIA FUZJI: <color={(canFuse ? "#45F375" : "#FFCC66")}>{placed}/2 RUN (TA SAMA STATYSTYKA I RZADKOŚĆ)</color>";
        }

        if (btnCraftFusion != null)
        {
            btnCraftFusion.interactable = canFuse;
        }

        // ── 2. Transmutacja ──────────────────────────────────────────────────
        SetupCraftSlot(slotTransmute1, RuneDragSource.Transmutation, 0, transmuteRune1);
        SetupCraftSlot(slotTransmute2, RuneDragSource.Transmutation, 1, transmuteRune2);
        SetupCraftSlot(slotTransmute3, RuneDragSource.Transmutation, 2, transmuteRune3);

        bool canTransmute = transmuteRune1 != null && transmuteRune2 != null && transmuteRune3 != null &&
                            transmuteRune1.definition.rarity == transmuteRune2.definition.rarity &&
                            transmuteRune1.definition.rarity == transmuteRune3.definition.rarity &&
                            transmuteRune1.definition.rarity != RuneRarity.Cursed;

        SetupResultSlot(slotTransmuteResult, canTransmute, canTransmute ? GetNextRarity(transmuteRune1.definition.rarity) : null);

        if (txtTransmuteReqHeader != null)
        {
            int placed = (transmuteRune1 != null ? 1 : 0) + (transmuteRune2 != null ? 1 : 0) + (transmuteRune3 != null ? 1 : 0);
            txtTransmuteReqHeader.text = $"WYMAGANIA TRANSMUTACJI: <color={(canTransmute ? "#45F375" : "#FFCC66")}>{placed}/3 RUN (TA SAMA RZADKOŚĆ)</color>";
        }

        if (btnCraftTransmute != null)
        {
            btnCraftTransmute.interactable = canTransmute;
        }
    }

    private void SetupCraftSlot(Transform slotTr, RuneDragSource craftType, int index, RuneItem rune)
    {
        if (slotTr == null) return;

        TextMeshProUGUI lblTmp = slotTr.Find("Txt_Label")?.GetComponent<TextMeshProUGUI>()
                              ?? slotTr.Find("Txt_SlotLabel")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI plusTmp = slotTr.Find("Txt_Plus")?.GetComponent<TextMeshProUGUI>();
        Image iconImg = slotTr.Find("Img_SocketIcon/Img_SocketIcon")?.GetComponent<Image>()
                     ?? slotTr.Find("Img_SocketIcon")?.GetComponentInChildren<Image>(true)
                     ?? slotTr.Find("Img_SocketIcon")?.GetComponent<Image>()
                     ?? slotTr.Find("Img_Icon")?.GetComponent<Image>();

        var craftScript = slotTr.GetComponent<RuneCraftSlotUI>();
        if (craftScript == null) craftScript = slotTr.gameObject.AddComponent<RuneCraftSlotUI>();
        craftScript.Setup(craftType, index, rune, this);

        if (rune != null)
        {
            if (plusTmp != null) plusTmp.gameObject.SetActive(false);
            if (lblTmp != null) lblTmp.text = $"{rune.definition?.runeName}\n<size=10>{rune.GetPrimaryText()}</size>";
            if (iconImg != null)
            {
                iconImg.gameObject.SetActive(true);
                iconImg.sprite = rune.definition?.icon;
                iconImg.color = rune.definition?.icon != null ? Color.white : GetRarityColor(rune.definition != null ? rune.definition.rarity : RuneRarity.Common);
                iconImg.preserveAspect = true;
            }
        }
        else
        {
            if (plusTmp != null) plusTmp.gameObject.SetActive(true);
            if (lblTmp != null) lblTmp.text = "WOLNY SLOT\n<size=10><color=#888888>PRZECIĄGNIJ / KLIKNIJ</color></size>";
            if (iconImg != null)
            {
                iconImg.gameObject.SetActive(false);
            }
        }
    }

    private void SetupResultSlot(Transform slotTr, bool isReady, RuneRarity? nextRarity)
    {
        if (slotTr == null) return;

        TextMeshProUGUI lblTmp = slotTr.Find("Txt_Label")?.GetComponent<TextMeshProUGUI>()
                              ?? slotTr.Find("Txt_SlotLabel")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI plusTmp = slotTr.Find("Txt_Plus")?.GetComponent<TextMeshProUGUI>();
        Image iconImg = slotTr.Find("Img_SocketIcon/Img_SocketIcon")?.GetComponent<Image>()
                     ?? slotTr.Find("Img_SocketIcon")?.GetComponentInChildren<Image>(true)
                     ?? slotTr.Find("Img_SocketIcon")?.GetComponent<Image>()
                     ?? slotTr.Find("Img_Icon")?.GetComponent<Image>();

        if (isReady && nextRarity.HasValue)
        {
            if (plusTmp != null) plusTmp.gameObject.SetActive(false);
            if (lblTmp != null) lblTmp.text = $"REZULTAT:\n<color=#FFD700>Poziom {nextRarity.Value}</color>";
            if (iconImg != null)
            {
                iconImg.gameObject.SetActive(true);
                iconImg.color = GetRarityColor(nextRarity.Value);
            }
        }
        else
        {
            if (plusTmp != null) plusTmp.gameObject.SetActive(true);
            if (lblTmp != null) lblTmp.text = "REZULTAT\n<size=10><color=#888888>OCZEKIWANIE</color></size>";
            if (iconImg != null) iconImg.gameObject.SetActive(false);
        }
    }

    public void OnCraftSlotClicked(RuneDragSource craftType, int index)
    {
        ClearCraftSlot(craftType, index);
        RefreshCraftingSlots();
        RefreshInventoryView();
    }

    public void OnRuneDroppedOnCraftSlot(RuneDragSource craftType, int targetIndex, RuneItem rune, RuneDragSource source, int sourceIndex)
    {
        if (rune == null) return;

        // Jeśli runa pochodziła ze slotu wieży, zdejmij ją najpierw
        if (source == RuneDragSource.Socket && currentForge != null)
        {
            currentForge.UnequipRune(rune);
        }
        else if (source == RuneDragSource.Fusion || source == RuneDragSource.Transmutation)
        {
            ClearCraftSlot(source, sourceIndex);
        }

        // Wstawienie do kowadła
        if (craftType == RuneDragSource.Fusion)
        {
            if (targetIndex == 0) fusionRune1 = rune;
            else if (targetIndex == 1) fusionRune2 = rune;
        }
        else if (craftType == RuneDragSource.Transmutation)
        {
            if (targetIndex == 0) transmuteRune1 = rune;
            else if (targetIndex == 1) transmuteRune2 = rune;
            else if (targetIndex == 2) transmuteRune3 = rune;
        }

        RefreshAll();
    }

    private void ClearCraftSlot(RuneDragSource craftType, int index)
    {
        if (craftType == RuneDragSource.Fusion)
        {
            if (index == 0) fusionRune1 = null;
            else if (index == 1) fusionRune2 = null;
        }
        else if (craftType == RuneDragSource.Transmutation)
        {
            if (index == 0) transmuteRune1 = null;
            else if (index == 1) transmuteRune2 = null;
            else if (index == 2) transmuteRune3 = null;
        }
    }

    public void ExecuteFusion()
    {
        if (RuneManager.Instance == null) return;
        if (RuneManager.Instance.TryFuseRunes(fusionRune1, fusionRune2))
        {
            fusionRune1 = null;
            fusionRune2 = null;
            RefreshAll();
        }
    }

    public void ExecuteTransmutation()
    {
        if (RuneManager.Instance == null) return;
        if (RuneManager.Instance.TryTransmuteRunes(transmuteRune1, transmuteRune2, transmuteRune3))
        {
            transmuteRune1 = null;
            transmuteRune2 = null;
            transmuteRune3 = null;
            RefreshAll();
        }
    }

    private RuneRarity? GetNextRarity(RuneRarity rarity)
    {
        return rarity switch
        {
            RuneRarity.Common => RuneRarity.Uncommon,
            RuneRarity.Uncommon => RuneRarity.Rare,
            RuneRarity.Rare => RuneRarity.Legendary,
            _ => null
        };
    }

    // =========================================================================
    // DOLNY PANEL: STATYSTYKI BOJOWE (Pnl_CombatStatItem)
    // =========================================================================

    private void RefreshBottomStats()
    {
        if (currentForge == null || currentForge.infusedTowerData == null || combatStatViews == null) return;

        var tower = currentForge.infusedTowerData;
        var equipped = currentForge.equippedRunes ?? new List<RuneItem>();

        // Agregacja modyfikatorów ze wszystkich osadzonych run
        float dmgFlat = 0, dmgPct = 0;
        float rngFlat = 0, rngPct = 0;
        float frFlat = 0, frPct = 0;
        float apFlat = 0, apPct = 0;
        float mpFlat = 0, mpPct = 0;
        float ccFlat = 0, ccPct = 0;
        float cdFlat = 0, cdPct = 0;

        foreach (var r in equipped)
        {
            if (r == null || r.definition == null) continue;

            AddStatContribution(r.definition.primaryStat, r.definition.valueType, r.primaryValue,
                ref dmgFlat, ref dmgPct, ref rngFlat, ref rngPct, ref frFlat, ref frPct,
                ref apFlat, ref apPct, ref mpFlat, ref mpPct, ref ccFlat, ref ccPct, ref cdFlat, ref cdPct);

            if (r.hasPenalty)
            {
                AddStatContribution(r.penaltyStat, r.penaltyValueType, r.penaltyValue,
                    ref dmgFlat, ref dmgPct, ref rngFlat, ref rngPct, ref frFlat, ref frPct,
                    ref apFlat, ref apPct, ref mpFlat, ref mpPct, ref ccFlat, ref ccPct, ref cdFlat, ref cdPct);
            }
        }

        foreach (var view in combatStatViews)
        {
            if (view == null || view.rootObject == null) continue;

            float baseVal = 0;
            float modVal = 0;
            string unit = "";

            switch (view.statType)
            {
                case BattleStats.Damage:
                    baseVal = tower.baseDamage;
                    modVal = (baseVal + dmgFlat) * (1f + dmgPct / 100f);
                    break;

                case BattleStats.FireRate:
                    baseVal = tower.fireRate;
                    modVal = (baseVal + frFlat) * (1f + frPct / 100f);
                    unit = "/s";
                    break;

                case BattleStats.Range:
                    baseVal = tower.baseRange;
                    modVal = (baseVal + rngFlat) * (1f + rngPct / 100f);
                    break;

                case BattleStats.DamageType:
                    if (view.txtBaseValue != null) view.txtBaseValue.text = $"{tower.damageType}";
                    if (view.txtCurrentValue != null) view.txtCurrentValue.text = $"{tower.damageType}";
                    continue;

                case BattleStats.CriticalChancel:
                    baseVal = tower.criticalChancel;
                    modVal = (baseVal + ccFlat) * (1f + ccPct / 100f);
                    unit = "%";
                    break;

                case BattleStats.CriticalMultiplier:
                    baseVal = tower.criticalDamageMultiplier;
                    modVal = (baseVal + cdFlat) * (1f + cdPct / 100f);
                    unit = "%";
                    break;

                case BattleStats.ArmorPenetration:
                    baseVal = tower.armorPenetration;
                    modVal = (baseVal + apFlat) * (1f + apPct / 100f);
                    unit = "%";
                    break;

                case BattleStats.MagicPenetration:
                    baseVal = tower.magicPenetration;
                    modVal = (baseVal + mpFlat) * (1f + mpPct / 100f);
                    unit = "%";
                    break;
            }

            float diff = modVal - baseVal;

            if (view.txtBaseValue != null)
            {
                view.txtBaseValue.text = $"{baseVal:0.#}{unit}";
            }

            if (view.txtCurrentValue != null)
            {
                string colorHex = Mathf.Abs(diff) < 0.01f ? "#FFFFFF" : (diff > 0 ? "#45F375" : "#FF5555");
                string diffStr = Mathf.Abs(diff) >= 0.05f ? $" ({(diff > 0 ? "+" : "")}{diff:0.#})" : "";
                view.txtCurrentValue.text = $"<color={colorHex}>{modVal:0.#}{unit}{diffStr}</color>";
            }
        }
    }

    private void AddStatContribution(RuneStatType stat, RuneValueType vType, float val,
        ref float dmgF, ref float dmgP, ref float rngF, ref float rngP, ref float frF, ref float frP,
        ref float apF, ref float apP, ref float mpF, ref float mpP, ref float ccF, ref float ccP, ref float cdF, ref float cdP)
    {
        switch (stat)
        {
            case RuneStatType.Damage:
                if (vType == RuneValueType.Flat) dmgF += val; else dmgP += val;
                break;
            case RuneStatType.Range:
                if (vType == RuneValueType.Flat) rngF += val; else rngP += val;
                break;
            case RuneStatType.FireRate:
                if (vType == RuneValueType.Flat) frF += val; else frP += val;
                break;
            case RuneStatType.ArmorPenetration:
                if (vType == RuneValueType.Flat) apF += val; else apP += val;
                break;
            case RuneStatType.MagicPenetration:
                if (vType == RuneValueType.Flat) mpF += val; else mpP += val;
                break;
            case RuneStatType.CriticalChance:
                if (vType == RuneValueType.Flat) ccF += val; else ccP += val;
                break;
            case RuneStatType.CriticalDamage:
                if (vType == RuneValueType.Flat) cdF += val; else cdP += val;
                break;
        }
    }

    // =========================================================================
    // DOLNY PANEL: SEKCJA KOWALI (Pnl_RaceControllers)
    // =========================================================================

    private void RefreshRunesmiths()
    {
        if (currentForge == null) return;

        int assigned = currentForge.assignedDwarves != null ? currentForge.assignedDwarves.Count : 0;
        int maxSmiths = currentForge.GetMaxRunesmiths();
        int freeDwarves = WorkforceManager.Instance != null ? WorkforceManager.Instance.GetIdleCount(Race.Dwarves) : 0;

        if (txtDwarfTitle != null)
        {
            txtDwarfTitle.text = "KOWALE RUN (KRASNOLUDY)";
        }

        if (txtDwarfFree != null)
        {
            txtDwarfFree.text = $"Wolni: {freeDwarves}";
        }

        if (txtDwarfCount != null)
        {
            txtDwarfCount.text = $"{assigned} / {maxSmiths}";
        }

        if (btnDwarfMinus != null) btnDwarfMinus.interactable = assigned > 0;
        if (btnDwarfPlus != null) btnDwarfPlus.interactable = assigned < maxSmiths && freeDwarves > 0;
    }

    private void HandleAddSmith()
    {
        if (currentForge == null) return;
        if (currentForge.TryAddRunesmith())
        {
            RefreshAll();
        }
    }

    private void HandleRemoveSmith()
    {
        if (currentForge == null) return;
        if (currentForge.TryRemoveRunesmith())
        {
            RefreshAll();
        }
    }

    // =========================================================================
    // AKCJE I POMOCNICZE
    // =========================================================================

    public void CloseWindow()
    {
        UnsubscribeEvents();
        gameObject.SetActive(false);

        if (CenterInfoPanelControllerUI.Instance != null)
        {
            CenterInfoPanelControllerUI.Instance.HideAll();
        }

        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.DeselectAll();
        }
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

    public Color GetRarityColor(RuneRarity rarity)
    {
        return rarity switch
        {
            RuneRarity.Common => new Color(0.85f, 0.85f, 0.85f, 1f),
            RuneRarity.Uncommon => new Color(0.35f, 0.85f, 0.45f, 1f),
            RuneRarity.Rare => new Color(0.3f, 0.65f, 0.95f, 1f),
            RuneRarity.Legendary => new Color(0.96f, 0.78f, 0.28f, 1f),
            RuneRarity.Cursed => new Color(0.92f, 0.28f, 0.28f, 1f),
            _ => Color.white
        };
    }
}
