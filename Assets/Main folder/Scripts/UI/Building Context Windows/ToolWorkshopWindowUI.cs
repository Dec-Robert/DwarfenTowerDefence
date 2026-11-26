using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kontroler nowego okna kontekstowego Warsztatu narzędziowego (Pnl_ToolWorkshopWindow).
/// Obsługuje zakładki budynków (Sec_BuildingsTab), drzewko ulepszeń (Pnl_Research),
/// panel szczegółów wybranej technologii (Pnl_UpgradeInfo) oraz przydział pracowników (SingleRaceWorkerControllerUI).
/// </summary>
public class ToolWorkshopWindowUI : MonoBehaviour
{
    [Serializable]
    public class BuildingTabBinding
    {
        public GameObject root;
        public Button button;
        public Image background;
        public Image icon;
        public TMP_Text titleText;
    }

    [Serializable]
    public class UpgradeOptionSlotView
    {
        public Button button;
        public Image background;
        public Image icon;
        public TMP_Text titleText;
    }

    [Serializable]
    public class TierRowBinding
    {
        public GameObject root;
        public TMP_Text tierNumberText;
        public UpgradeOptionSlotView optionA = new UpgradeOptionSlotView();
        public UpgradeOptionSlotView optionB = new UpgradeOptionSlotView();
    }

    [Header("Nagłówek")]
    [SerializeField] private TMP_Text txtTitle;
    [SerializeField] private Button btnClose;

    [Header("Zakładki Budynków")]
    [SerializeField] private Transform secBuildingsTab;
    [SerializeField] private List<BuildingTabBinding> buildingTabs = new List<BuildingTabBinding>();

    [Header("Drzewko Badań (Lewa Kolumna)")]
    [SerializeField] private TMP_Text txtFlavourHeader;
    [SerializeField] private TMP_Text txtSelectedBuilding;
    [SerializeField] private TMP_Text txtProgress;
    [SerializeField] private Transform pnlUpgradeTabs;
    [SerializeField] private List<TierRowBinding> tierRows = new List<TierRowBinding>();

    [Header("Załoga Warsztatu (Prawa Kolumna)")]
    [SerializeField] private SingleRaceWorkerControllerUI workerController;
    [SerializeField] private TMP_Text txtRequiredWorkers;

    [Header("Szczegóły Ulepszenia (Pnl_UpgradeInfo)")]
    [SerializeField] private GameObject pnlUpgradeInfo;
    [SerializeField] private Image imgUpgradeIcon;
    [SerializeField] private TMP_Text txtUpgradeName;
    [SerializeField] private GameObject pnlDescription;
    [SerializeField] private TMP_Text txtDescription;
    [SerializeField] private GameObject pnlEffect;
    [SerializeField] private TMP_Text txtEffectDescription;

    [Header("Koszty, Czas i Akcja (Pnl_Costs)")]
    [SerializeField] private TMP_Text txtCostsHeader;
    [SerializeField] private Transform pnlCostsContainer;
    [SerializeField] private List<SingleResourceUI> costResourceSlots = new List<SingleResourceUI>();
    [SerializeField] private GameObject objCostDivider;
    [SerializeField] private TMP_Text txtTime;
    [SerializeField] private Button btnResearch;
    [SerializeField] private Image imgResearchButton;
    [SerializeField] private TMP_Text txtResearchButtonLabel;

    private ToolWorkshopEntity currentWorkshop;
    private BuildingData selectedBuilding;
    private int selectedTier = 1;
    private bool selectedIsOptionA = true;

    private bool listenersInitialized;
    private bool isSubscribed;

    private static readonly string[] RomanNumerals = { "I", "II", "III", "IV", "V" };

    // Kolory kafelków drzewka (tła bazują na białym sprite z teksturą)
    private static readonly Color ColorSlotDefault = Color.white;
    private static readonly Color ColorSlotSelected = new Color(1.00f, 0.92f, 0.68f, 1f);
    private static readonly Color ColorSlotResearched = new Color(0.65f, 0.95f, 0.70f, 1f);
    private static readonly Color ColorSlotActiveResearch = new Color(1.00f, 0.80f, 0.42f, 1f);
    private static readonly Color ColorSlotLocked = new Color(0.55f, 0.55f, 0.58f, 0.80f);

    // Kolory zakładek budynków
    private static readonly Color ColorTabInactive = new Color(0.82f, 0.82f, 0.85f, 0.90f);
    private static readonly Color ColorTabSelected = Color.white;
    private static readonly Color ColorTabResearching = new Color(1.00f, 0.85f, 0.55f, 1f);

    // Kolory przycisku Btn_Research
    private static readonly Color ColorBtnReadyBg = Color.white;
    private static readonly Color ColorBtnReadyText = new Color(0.18f, 0.14f, 0.10f, 1f);
    private static readonly Color ColorBtnCancelBg = new Color(0.95f, 0.58f, 0.54f, 1f);
    private static readonly Color ColorBtnCancelText = Color.white;
    private static readonly Color ColorBtnDisabledBg = new Color(0.52f, 0.52f, 0.55f, 0.75f);
    private static readonly Color ColorBtnDisabledText = new Color(0.76f, 0.76f, 0.78f, 1f);

    // Kolory kosztów surowców
    private static readonly Color ColorCostAffordable = Color.white;
    private static readonly Color ColorCostMissing = new Color(0.95f, 0.35f, 0.35f, 1f);

    private static readonly Dictionary<string, string> SpecialEffectDescriptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "FARM_CREATE_SOIL", "Co 4 dni tworzy nowe żyzne pole na pustym heksie w zasięgu" },
            { "IGNORE_OCCUPIED_PENALTY", "Ignoruje kary za współdzielenie pól z innymi budynkami tego typu" },
            { "FARM_REDUCE_FOOD_SURVIVAL", "Zmniejsza zapotrzebowanie mieszkańców na Jedzenie o 25%" },
            { "MINE_MOUNTAIN_CHAIN", "Czerpie surowce z całego połączonego pasma górskiego" },
            { "MINES_TOWER_DAMAGE_SYNERGY", "Każda aktywna Kopalnia zwiększa obrażenia wszystkich wież o +5% (maks. +25%)" },
            { "QUARRY_IGNORE_ELEVATION_PENALTY", "Usuwa karę współdzielenia wzgórz przez sąsiednie Kamieniołomy" },
            { "QUARRY_FORTIFY_GATES", "Każdy aktywny Kamieniołom naprawia i wzmacnia bramy (+15 HP dziennie, maks. +100 bonusowego HP)" },
            { "QUARRY_FLATLAND_MIN_ELEVATION_2", "Traktuje płaskie pola jako min. wysokość 2" },
            { "RETORT_FOREST_COAL_SYNERGY", "+2 Węgla za każdy sąsiedni Las" },
            { "RETORT_REDUCE_TOWER_UPKEEP", "Obniża nocny koszt utrzymania wież o 25% (40% przy 2+ aktywnych Retortach)" },
            { "FORESTER_PLANT_TREE", "Co 3 dni sadzi nowy las na pustym polu w zasięgu" },
            { "SAWMILL_DESTROY_FOREST", "Co 3 dni ścina jeden sąsiedni las" },
            { "INCREASE_RANGE_1", "+1 do zasięgu skanowania terenu" },
            { "INCREASE_RANGE_2", "+1 do zasięgu skanowania terenu" }
        };

    private void Awake()
    {
        AutoBindReferences();
        InitializeListeners();
    }

    private void OnEnable()
    {
        AutoBindReferences();
        InitializeListeners();
        SubscribeEvents();
        Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    public void Setup(ToolWorkshopEntity workshop)
    {
        currentWorkshop = workshop;
        AutoBindReferences();
        InitializeListeners();
        SubscribeEvents();

        if (workerController != null)
        {
            workerController.Setup(workshop);
        }

        EnsureDefaultSelection();
        Refresh();
        ForceRebuildImmediate();
    }

    public void AutoBindReferences()
    {
        if (txtTitle == null) txtTitle = FindDeepChild<TMP_Text>(transform, "Txt_Title");
        if (btnClose == null) btnClose = FindDeepChild<Button>(transform, "Btn_Close");

        if (secBuildingsTab == null) secBuildingsTab = FindDeepTransform(transform, "Sec_BuildingsTab");
        BindBuildingTabsIfNeeded();

        Transform pnlResearch = FindDeepTransform(transform, "Pnl_Research");
        if (pnlResearch != null)
        {
            if (txtFlavourHeader == null) txtFlavourHeader = FindDeepChild<TMP_Text>(pnlResearch, "Txt_Flavour");
            if (txtSelectedBuilding == null) txtSelectedBuilding = FindDeepChild<TMP_Text>(pnlResearch, "Txt_SelectedBuilding");
            if (txtProgress == null) txtProgress = FindDeepChild<TMP_Text>(pnlResearch, "Txt_Progress");
            if (pnlUpgradeTabs == null) pnlUpgradeTabs = FindDeepTransform(pnlResearch, "Pnl_UpgradeTabs");
        }
        BindTierRowsIfNeeded();

        Transform pnlInfo = FindDeepTransform(transform, "Pnl_Info");
        if (pnlInfo != null)
        {
            if (workerController == null) workerController = pnlInfo.GetComponentInChildren<SingleRaceWorkerControllerUI>(true);
            if (txtRequiredWorkers == null) txtRequiredWorkers = FindDeepChild<TMP_Text>(pnlInfo, "Txt_RequiredWorkers");
            if (pnlUpgradeInfo == null) pnlUpgradeInfo = FindDeepTransform(pnlInfo, "Pnl_UpgradeInfo")?.gameObject;
        }

        if (pnlUpgradeInfo != null)
        {
            Transform infoRoot = pnlUpgradeInfo.transform;
            Transform header = infoRoot.Find("Pnl_Header");
            if (header != null)
            {
                if (imgUpgradeIcon == null) imgUpgradeIcon = FindDeepChild<Image>(header, "Img_Icon");
                if (txtUpgradeName == null) txtUpgradeName = FindDeepChild<TMP_Text>(header, "Txt_Name");
            }

            if (pnlDescription == null) pnlDescription = infoRoot.Find("Pnl_Description")?.gameObject;
            if (txtDescription == null) txtDescription = FindDeepChild<TMP_Text>(infoRoot, "Txt_Description");

            if (pnlEffect == null) pnlEffect = infoRoot.Find("Pnl_Effect")?.gameObject;
            if (txtEffectDescription == null)
            {
                txtEffectDescription = FindDeepChild<TMP_Text>(infoRoot, "Txt_EffectDesciptiorn")
                                       ?? FindDeepChild<TMP_Text>(infoRoot, "Txt_EffectDescription");
            }

            Transform outerCosts = infoRoot.Find("Pnl_Costs");
            if (outerCosts != null)
            {
                if (txtCostsHeader == null) txtCostsHeader = outerCosts.Find("Txt_Costs")?.GetComponent<TMP_Text>();
                if (pnlCostsContainer == null) pnlCostsContainer = outerCosts.Find("Pnl_Costs");
                if (objCostDivider == null) objCostDivider = outerCosts.Find("Divider")?.gameObject;
                if (txtTime == null) txtTime = outerCosts.Find("Txt_Time")?.GetComponent<TMP_Text>();
                if (btnResearch == null) btnResearch = outerCosts.Find("Btn_Research")?.GetComponent<Button>();

                if (btnResearch != null)
                {
                    if (imgResearchButton == null) imgResearchButton = btnResearch.GetComponent<Image>();
                    if (txtResearchButtonLabel == null) txtResearchButtonLabel = btnResearch.GetComponentInChildren<TMP_Text>(true);
                }
            }
        }

        BindCostSlotsIfNeeded();
    }

    public void ForceRebuildImmediate()
    {
        Canvas.ForceUpdateCanvases();
        if (transform is RectTransform rt)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    public void Refresh()
    {
        AutoBindReferences();

        var mgr = ToolWorkshopResearchManager.Instance;
        if (mgr != null) mgr.EnsureCatalogLoaded();

        if (txtTitle != null)
        {
            txtTitle.text = currentWorkshop != null && currentWorkshop.data != null
                ? currentWorkshop.data.DisplayName
                : LocalizationManager.Get("BUILDING_WARSZTAT_NARZEDZIOWY_NAME", "Warsztat narzędziowy");
        }

        EnsureSelectionValid(mgr);
        RefreshBuildingTabs(mgr);
        RefreshTreeSection(mgr);
        RefreshWorkersSection();
        RefreshUpgradeDetailsSection(mgr);
    }

    private void BindBuildingTabsIfNeeded()
    {
        if (secBuildingsTab == null) return;
        if (buildingTabs != null && buildingTabs.Count == secBuildingsTab.childCount &&
            buildingTabs.TrueForAll(t => t != null && t.button != null))
        {
            return;
        }

        buildingTabs = new List<BuildingTabBinding>();
        for (int i = 0; i < secBuildingsTab.childCount; i++)
        {
            Transform child = secBuildingsTab.GetChild(i);
            var binding = new BuildingTabBinding
            {
                root = child.gameObject,
                button = child.GetComponent<Button>(),
                background = child.GetComponent<Image>(),
                icon = FindDeepChild<Image>(child, "Img_Icon"),
                titleText = FindDeepChild<TMP_Text>(child, "Txt_BuildingName")
            };
            buildingTabs.Add(binding);
        }
    }

    private void BindTierRowsIfNeeded()
    {
        if (pnlUpgradeTabs == null) return;
        if (tierRows != null && tierRows.Count == pnlUpgradeTabs.childCount &&
            tierRows.TrueForAll(r => r != null && r.optionA.button != null && r.optionB.button != null))
        {
            return;
        }

        tierRows = new List<TierRowBinding>();
        for (int i = 0; i < pnlUpgradeTabs.childCount; i++)
        {
            Transform rowTr = pnlUpgradeTabs.GetChild(i);
            var row = new TierRowBinding
            {
                root = rowTr.gameObject,
                tierNumberText = FindDeepChild<TMP_Text>(rowTr, "Txt_TierNumber"),
                optionA = BindOptionSlot(rowTr.Find("Pnl_UpgradeA")),
                optionB = BindOptionSlot(rowTr.Find("Pnl_UpgradeB"))
            };
            tierRows.Add(row);
        }
    }

    private static UpgradeOptionSlotView BindOptionSlot(Transform slotTransform)
    {
        var view = new UpgradeOptionSlotView();
        if (slotTransform == null) return view;

        view.button = slotTransform.GetComponent<Button>();
        view.background = slotTransform.GetComponent<Image>();
        view.icon = FindDeepChild<Image>(slotTransform, "Img_Icon");
        view.titleText = FindDeepChild<TMP_Text>(slotTransform, "Txt_UpgradeName");
        return view;
    }

    private void BindCostSlotsIfNeeded()
    {
        if (pnlCostsContainer == null) return;
        if (costResourceSlots != null && costResourceSlots.Count == pnlCostsContainer.childCount &&
            costResourceSlots.TrueForAll(s => s != null))
        {
            return;
        }

        costResourceSlots = new List<SingleResourceUI>();
        for (int i = 0; i < pnlCostsContainer.childCount; i++)
        {
            var slot = pnlCostsContainer.GetChild(i).GetComponent<SingleResourceUI>();
            if (slot != null) costResourceSlots.Add(slot);
        }
    }

    private void InitializeListeners()
    {
        if (listenersInitialized) return;
        listenersInitialized = true;

        if (btnClose != null)
        {
            btnClose.onClick.AddListener(() => gameObject.SetActive(false));
        }

        if (btnResearch != null)
        {
            btnResearch.onClick.AddListener(OnResearchButtonClicked);
        }

        if (buildingTabs != null)
        {
            for (int i = 0; i < buildingTabs.Count; i++)
            {
                int tabIndex = i;
                var tab = buildingTabs[i];
                if (tab != null && tab.button != null)
                {
                    tab.button.onClick.AddListener(() => OnBuildingTabClicked(tabIndex));
                }
            }
        }

        if (tierRows != null)
        {
            for (int i = 0; i < tierRows.Count; i++)
            {
                int rowIndex = i;
                var row = tierRows[i];
                if (row == null) continue;

                if (row.optionA?.button != null)
                {
                    row.optionA.button.onClick.AddListener(() => OnUpgradeSlotClicked(rowIndex, true));
                }
                if (row.optionB?.button != null)
                {
                    row.optionB.button.onClick.AddListener(() => OnUpgradeSlotClicked(rowIndex, false));
                }
            }
        }
    }

    private void SubscribeEvents()
    {
        if (isSubscribed) return;
        isSubscribed = true;

        if (ToolWorkshopResearchManager.Instance != null)
        {
            ToolWorkshopResearchManager.Instance.OnResearchStateChanged += HandleExternalStateChanged;
        }

        if (workerController != null)
        {
            workerController.OnWorkersChanged += HandleWorkersChanged;
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged += HandleResourceChanged;
        }

        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted += HandleExternalStateChanged;
            TimePhaseManager.Instance.OnDuskStarted += HandleExternalStateChanged;
            TimePhaseManager.Instance.OnNightStarted += HandleExternalStateChanged;
        }

        LocalizationManager.OnLanguageChanged += HandleExternalStateChanged;
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed) return;
        isSubscribed = false;

        if (ToolWorkshopResearchManager.Instance != null)
        {
            ToolWorkshopResearchManager.Instance.OnResearchStateChanged -= HandleExternalStateChanged;
        }

        if (workerController != null)
        {
            workerController.OnWorkersChanged -= HandleWorkersChanged;
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged -= HandleResourceChanged;
        }

        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= HandleExternalStateChanged;
            TimePhaseManager.Instance.OnDuskStarted -= HandleExternalStateChanged;
            TimePhaseManager.Instance.OnNightStarted -= HandleExternalStateChanged;
        }

        LocalizationManager.OnLanguageChanged -= HandleExternalStateChanged;
    }

    private void HandleExternalStateChanged()
    {
        Refresh();
    }

    private void HandleWorkersChanged(Race _, int __)
    {
        Refresh();
    }

    private void HandleResourceChanged(ResourceType _, int __)
    {
        RefreshUpgradeDetailsSection(ToolWorkshopResearchManager.Instance);
    }

    private void EnsureDefaultSelection()
    {
        var mgr = ToolWorkshopResearchManager.Instance;
        if (mgr == null) return;
        mgr.EnsureCatalogLoaded();

        if (mgr.ActiveResearch != null && mgr.ActiveResearch.IsActive && mgr.ActiveResearch.targetBuilding != null)
        {
            selectedBuilding = mgr.ActiveResearch.targetBuilding;
            selectedTier = mgr.ActiveResearch.tier;
            selectedIsOptionA = mgr.ActiveResearch.isOptionA;
            return;
        }

        var catalog = mgr.TechCatalog;
        if (catalog != null && catalog.buildingTrees != null && catalog.buildingTrees.Count > 0)
        {
            if (selectedBuilding == null || !catalog.TryGetTree(selectedBuilding, out _))
            {
                selectedBuilding = FindFirstValidTreeBuilding(catalog);
            }
            SelectDefaultTierForBuilding(mgr, selectedBuilding);
        }
    }

    private void EnsureSelectionValid(ToolWorkshopResearchManager mgr)
    {
        if (mgr == null || mgr.TechCatalog == null || mgr.TechCatalog.buildingTrees == null) return;

        if (selectedBuilding == null || !mgr.TechCatalog.TryGetTree(selectedBuilding, out var tree))
        {
            selectedBuilding = FindFirstValidTreeBuilding(mgr.TechCatalog);
            SelectDefaultTierForBuilding(mgr, selectedBuilding);
            return;
        }

        if (tree != null && tree.GetTier(selectedTier) == null)
        {
            SelectDefaultTierForBuilding(mgr, selectedBuilding);
        }
    }

    private static BuildingData FindFirstValidTreeBuilding(WorkshopTechTreeCatalogSO catalog)
    {
        if (catalog == null || catalog.buildingTrees == null) return null;
        for (int i = 0; i < catalog.buildingTrees.Count; i++)
        {
            if (catalog.buildingTrees[i] != null && catalog.buildingTrees[i].targetBuilding != null)
                return catalog.buildingTrees[i].targetBuilding;
        }
        return null;
    }

    private void SelectDefaultTierForBuilding(ToolWorkshopResearchManager mgr, BuildingData building)
    {
        selectedTier = 1;
        selectedIsOptionA = true;
        if (mgr == null || building == null || mgr.TechCatalog == null) return;
        if (!mgr.TechCatalog.TryGetTree(building, out var tree) || tree.tiers == null || tree.tiers.Count == 0) return;

        if (mgr.ActiveResearch != null && mgr.ActiveResearch.IsActive &&
            IsSameBuilding(mgr.ActiveResearch.targetBuilding, building))
        {
            selectedTier = mgr.ActiveResearch.tier;
            selectedIsOptionA = mgr.ActiveResearch.isOptionA;
            return;
        }

        int nextTier = mgr.GetUnlockedTierCount(building) + 1;
        var nextEntry = tree.GetTier(nextTier);
        if (nextEntry != null)
        {
            selectedTier = nextEntry.tier;
            selectedIsOptionA = nextEntry.optionA != null;
            return;
        }

        var sorted = GetSortedTiers(tree);
        if (sorted.Count > 0)
        {
            selectedTier = sorted[0].tier;
            selectedIsOptionA = !mgr.IsOptionResearched(building, selectedTier, false);
        }
    }

    private void OnBuildingTabClicked(int tabIndex)
    {
        var mgr = ToolWorkshopResearchManager.Instance;
        if (mgr == null || mgr.TechCatalog == null || mgr.TechCatalog.buildingTrees == null) return;
        if (tabIndex < 0 || tabIndex >= mgr.TechCatalog.buildingTrees.Count) return;

        var tree = mgr.TechCatalog.buildingTrees[tabIndex];
        if (tree == null || tree.targetBuilding == null) return;

        selectedBuilding = tree.targetBuilding;
        SelectDefaultTierForBuilding(mgr, selectedBuilding);
        Refresh();
    }

    private void OnUpgradeSlotClicked(int rowIndex, bool isOptionA)
    {
        var mgr = ToolWorkshopResearchManager.Instance;
        if (mgr == null || mgr.TechCatalog == null || selectedBuilding == null) return;
        if (!mgr.TechCatalog.TryGetTree(selectedBuilding, out var tree)) return;

        var tiers = GetSortedTiers(tree);
        if (rowIndex < 0 || rowIndex >= tiers.Count) return;

        var entry = tiers[rowIndex];
        if (entry == null || entry.GetOption(isOptionA) == null) return;

        selectedTier = entry.tier;
        selectedIsOptionA = isOptionA;
        RefreshTreeSection(mgr);
        RefreshUpgradeDetailsSection(mgr);
    }

    private void OnResearchButtonClicked()
    {
        var mgr = ToolWorkshopResearchManager.Instance;
        if (mgr == null) return;

        if (mgr.ActiveResearch != null && mgr.ActiveResearch.IsActive)
        {
            mgr.CancelCurrentResearch();
            Refresh();
            return;
        }

        if (selectedBuilding != null && mgr.TryStartResearch(selectedBuilding, selectedTier, selectedIsOptionA))
        {
            Refresh();
        }
    }

    private void RefreshBuildingTabs(ToolWorkshopResearchManager mgr)
    {
        if (buildingTabs == null) return;
        var trees = mgr != null && mgr.TechCatalog != null ? mgr.TechCatalog.buildingTrees : null;

        for (int i = 0; i < buildingTabs.Count; i++)
        {
            var tab = buildingTabs[i];
            if (tab == null || tab.root == null) continue;

            WorkshopBuildingTree tree = trees != null && i < trees.Count ? trees[i] : null;
            bool hasBuilding = tree != null && tree.targetBuilding != null;
            tab.root.SetActive(hasBuilding);
            if (!hasBuilding) continue;

            BuildingData building = tree.targetBuilding;
            if (tab.titleText != null) tab.titleText.text = building.DisplayName;
            if (tab.icon != null)
            {
                tab.icon.sprite = building.icon;
                tab.icon.enabled = building.icon != null;
            }

            bool isSelected = IsSameBuilding(building, selectedBuilding);
            bool isResearchingThis = mgr != null && mgr.ActiveResearch != null && mgr.ActiveResearch.IsActive &&
                                     IsSameBuilding(mgr.ActiveResearch.targetBuilding, building);

            if (tab.background != null)
            {
                if (isSelected) tab.background.color = ColorTabSelected;
                else if (isResearchingThis) tab.background.color = ColorTabResearching;
                else tab.background.color = ColorTabInactive;
            }
        }
    }

    private void RefreshTreeSection(ToolWorkshopResearchManager mgr)
    {
        if (txtFlavourHeader != null)
        {
            txtFlavourHeader.text = LocalizationManager.Get("UI_WORKSHOP_TREE_HEADER", "Drzewko ulepszeń");
        }

        if (selectedBuilding == null || mgr == null || mgr.TechCatalog == null ||
            !mgr.TechCatalog.TryGetTree(selectedBuilding, out var tree))
        {
            if (txtSelectedBuilding != null) txtSelectedBuilding.text = "-";
            if (txtProgress != null) txtProgress.text = string.Format(LocalizationManager.Get("UI_WORKSHOP_PROGRESS_FMT", "Ukończone {0}/{1}"), 0, 0);
            HideAllTierRows();
            return;
        }

        var tiers = GetSortedTiers(tree);
        int totalTiers = tiers.Count;
        int unlockedTiers = Mathf.Min(mgr.GetUnlockedTierCount(selectedBuilding), totalTiers);

        if (txtSelectedBuilding != null)
        {
            txtSelectedBuilding.text = selectedBuilding.DisplayName;
        }

        if (txtProgress != null)
        {
            string progressText = string.Format(LocalizationManager.Get("UI_WORKSHOP_PROGRESS_FMT", "Ukończone {0}/{1}"), unlockedTiers, totalTiers);
            if (mgr.ActiveResearch != null && mgr.ActiveResearch.IsActive &&
                IsSameBuilding(mgr.ActiveResearch.targetBuilding, selectedBuilding))
            {
                progressText += string.Format(LocalizationManager.Get("UI_WORKSHOP_PROGRESS_RESEARCHING_FMT", "  (Badanie: {0:F0}%)"), mgr.ActiveResearch.NormalizedProgress * 100f);
            }
            txtProgress.text = progressText;
        }

        if (tierRows == null) return;

        for (int i = 0; i < tierRows.Count; i++)
        {
            var row = tierRows[i];
            if (row == null || row.root == null) continue;

            if (i >= tiers.Count)
            {
                row.root.SetActive(false);
                continue;
            }

            row.root.SetActive(true);
            var tierEntry = tiers[i];

            if (row.tierNumberText != null)
            {
                int romanIndex = Mathf.Clamp(tierEntry.tier - 1, 0, RomanNumerals.Length - 1);
                row.tierNumberText.text = RomanNumerals[romanIndex];
            }

            RefreshOptionSlot(mgr, row.optionA, tierEntry.tier, true, tierEntry.optionA);
            RefreshOptionSlot(mgr, row.optionB, tierEntry.tier, false, tierEntry.optionB);
        }
    }

    private void RefreshOptionSlot(
        ToolWorkshopResearchManager mgr,
        UpgradeOptionSlotView slot,
        int tier,
        bool isOptionA,
        BuildingUpgradeSO upgrade)
    {
        if (slot == null || slot.button == null) return;

        bool hasUpgrade = upgrade != null;
        slot.button.gameObject.SetActive(hasUpgrade);
        if (!hasUpgrade) return;

        if (slot.titleText != null)
        {
            slot.titleText.text = upgrade.DisplayName;
        }

        if (slot.icon != null)
        {
            Sprite sprite = upgrade.icon != null ? upgrade.icon : selectedBuilding?.icon;
            slot.icon.sprite = sprite;
            slot.icon.enabled = sprite != null;
        }

        bool isResearched = mgr.IsOptionResearched(selectedBuilding, tier, isOptionA);
        bool isBlockedByAlt = mgr.IsTierLockedByAlternative(selectedBuilding, tier, isOptionA);
        bool isResearching = IsCurrentlyResearching(mgr, selectedBuilding, tier, isOptionA);
        bool isAvailable = mgr.IsTierAvailableToResearch(selectedBuilding, tier);
        bool isSelected = tier == selectedTier && isOptionA == selectedIsOptionA;

        if (slot.background != null)
        {
            if (isResearching) slot.background.color = ColorSlotActiveResearch;
            else if (isResearched) slot.background.color = ColorSlotResearched;
            else if (isBlockedByAlt || !isAvailable) slot.background.color = isSelected ? ColorSlotSelected : ColorSlotLocked;
            else slot.background.color = isSelected ? ColorSlotSelected : ColorSlotDefault;
        }

        // Każdy istniejący kafelek można kliknąć w celu podglądu szczegółów w Pnl_UpgradeInfo
        slot.button.interactable = true;
    }

    private void HideAllTierRows()
    {
        if (tierRows == null) return;
        for (int i = 0; i < tierRows.Count; i++)
        {
            if (tierRows[i]?.root != null) tierRows[i].root.SetActive(false);
        }
    }

    private void RefreshWorkersSection()
    {
        if (workerController != null && workerController.TargetBuilding != currentWorkshop)
        {
            workerController.Setup(currentWorkshop);
        }
        else if (workerController != null)
        {
            workerController.Refresh();
        }

        if (txtRequiredWorkers == null) return;

        int required = ToolWorkshopEntity.RequiredWorkersForResearch;
        int workers = currentWorkshop != null ? currentWorkshop.GetWorkerCount(Race.Humans) : 0;
        bool isOperational = currentWorkshop != null && currentWorkshop.IsContributingToResearch;
        bool isNight = TimePhaseManager.Instance != null && TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Night;

        string statusPart = isOperational
            ? string.Format(LocalizationManager.Get("UI_WORKSHOP_WORKERS_ACTIVE_FMT", "<color=#7EE081>Aktywny ({0}/{1})</color>"), workers, required)
            : string.Format(LocalizationManager.Get("UI_WORKSHOP_WORKERS_INACTIVE_FMT", "<color=#E07A7A>Nieaktywny ({0}/{1})</color>"), workers, required);

        string text = string.Format(LocalizationManager.Get("UI_WORKSHOP_WORKERS_REQ_FMT", "Do pracy budynku wymagana jest {0} liczba pracowników.\n{1}"), required, statusPart);
        if (isNight)
        {
            text += LocalizationManager.Get("UI_WORKSHOP_WORKERS_NIGHT_LOCKED", "  <color=#C8C8C8>(Noc: zmiana załogi zablokowana)</color>");
        }

        txtRequiredWorkers.text = text;
    }

    private void RefreshUpgradeDetailsSection(ToolWorkshopResearchManager mgr)
    {
        BuildingUpgradeSO upgrade = ResolveSelectedUpgrade(mgr);
        if (upgrade == null)
        {
            if (txtUpgradeName != null) txtUpgradeName.text = "-";
            if (pnlDescription != null) pnlDescription.SetActive(false);
            if (pnlEffect != null) pnlEffect.SetActive(false);
            if (txtCostsHeader != null) txtCostsHeader.gameObject.SetActive(false);
            if (pnlCostsContainer != null) pnlCostsContainer.gameObject.SetActive(false);
            if (txtTime != null) txtTime.text = string.Empty;
            SetResearchButtonState(false, LocalizationManager.Get("UI_WORKSHOP_BTN_RESEARCH", "Opracuj technologię"), ColorBtnDisabledBg, ColorBtnDisabledText);
            return;
        }

        if (imgUpgradeIcon != null)
        {
            Sprite sprite = upgrade.icon != null ? upgrade.icon : selectedBuilding?.icon;
            imgUpgradeIcon.sprite = sprite;
            imgUpgradeIcon.enabled = sprite != null;
        }

        if (txtUpgradeName != null)
        {
            txtUpgradeName.text = upgrade.DisplayName;
        }

        string flavour = upgrade.DisplayFlavourDescription;
        bool hasFlavour = !string.IsNullOrWhiteSpace(flavour);
        if (pnlDescription != null) pnlDescription.SetActive(hasFlavour);
        if (txtDescription != null && hasFlavour) txtDescription.text = flavour;

        string effectsText = BuildUpgradeEffectsText(upgrade);
        bool hasEffects = !string.IsNullOrWhiteSpace(effectsText);
        if (pnlEffect != null) pnlEffect.SetActive(hasEffects);
        if (txtEffectDescription != null && hasEffects) txtEffectDescription.text = effectsText;

        RefreshCostSection(upgrade);
        RefreshTimeSection(mgr, upgrade);
        RefreshResearchActionButton(mgr, upgrade);
    }

    private void RefreshCostSection(BuildingUpgradeSO upgrade)
    {
        bool hasCosts = upgrade.cost != null && upgrade.cost.Count > 0;
        if (txtCostsHeader != null)
        {
            txtCostsHeader.text = LocalizationManager.Get("UI_WORKSHOP_COSTS_HEADER", "Koszt Ulepszenia");
            txtCostsHeader.gameObject.SetActive(hasCosts);
        }
        if (pnlCostsContainer != null) pnlCostsContainer.gameObject.SetActive(hasCosts);
        if (objCostDivider != null) objCostDivider.SetActive(true);

        if (!hasCosts || costResourceSlots == null) return;

        for (int i = 0; i < costResourceSlots.Count; i++)
        {
            var slot = costResourceSlots[i];
            if (slot == null) continue;

            if (i < upgrade.cost.Count && upgrade.cost[i].amount > 0f)
            {
                var cost = upgrade.cost[i];
                slot.gameObject.SetActive(true);
                slot.Setup(cost.type, cost.amount);

                bool affordable = ResourceManager.Instance == null ||
                                  ResourceManager.Instance.CanAfford(cost.type, Mathf.CeilToInt(cost.amount));
                slot.SetTextColor(affordable ? ColorCostAffordable : ColorCostMissing);
            }
            else
            {
                slot.gameObject.SetActive(false);
            }
        }
    }

    private void RefreshTimeSection(ToolWorkshopResearchManager mgr, BuildingUpgradeSO upgrade)
    {
        if (txtTime == null || mgr == null) return;

        int activeWorkshops = mgr.ActiveFullyStaffedWorkshopsCount;
        bool isResearchingSelected = IsCurrentlyResearching(mgr, selectedBuilding, selectedTier, selectedIsOptionA);

        if (isResearchingSelected && mgr.ActiveResearch != null)
        {
            float remDays = mgr.GetRemainingDaysEstimate();
            float pct = mgr.ActiveResearch.NormalizedProgress * 100f;
            if (activeWorkshops > 0)
            {
                txtTime.text = string.Format(LocalizationManager.Get("UI_WORKSHOP_TIME_PROGRESS_FMT", "Czas opracowania: ~{0:F1} dni (postęp: {1:F0}%)"), remDays, pct);
            }
            else
            {
                txtTime.text = string.Format(LocalizationManager.Get("UI_WORKSHOP_TIME_PAUSED_FMT", "WSTRZYMANE — brak aktywnego warsztatu (postęp: {0:F0}%)"), pct);
            }
            return;
        }

        float estDays = mgr.GetEstimatedDurationForTier(selectedTier);
        int baseDays = ToolWorkshopResearchManager.CalculateBaseDuration(selectedTier, mgr.DiscoveredUpgradesCount);

        if (float.IsInfinity(estDays) || activeWorkshops <= 0)
        {
            txtTime.text = string.Format(LocalizationManager.Get("UI_WORKSHOP_TIME_NO_WORKSHOP_FMT", "Czas opracowania: {0} dni (brak aktywnego warsztatu 3/3)"), baseDays);
        }
        else
        {
            txtTime.text = string.Format(LocalizationManager.Get("UI_WORKSHOP_TIME_EST_FMT", "Czas opracowania: ~{0:F1} dni"), estDays);
        }
    }

    private void RefreshResearchActionButton(ToolWorkshopResearchManager mgr, BuildingUpgradeSO upgrade)
    {
        if (btnResearch == null || mgr == null) return;

        if (mgr.ActiveResearch != null && mgr.ActiveResearch.IsActive)
        {
            int refundPct = Mathf.RoundToInt(mgr.GetCurrentCancelRefundRatio() * 100f);
            bool hasPaidCost = mgr.ActiveResearch.paidCost != null && mgr.ActiveResearch.paidCost.Count > 0;
            string cancelLabel = hasPaidCost
                ? string.Format(LocalizationManager.Get("UI_WORKSHOP_BTN_CANCEL_REFUND_FMT", "Anuluj badanie (zwrot {0}%)"), refundPct)
                : LocalizationManager.Get("UI_WORKSHOP_BTN_CANCEL", "Anuluj badanie");
            SetResearchButtonState(true, cancelLabel, ColorBtnCancelBg, ColorBtnCancelText);
            return;
        }

        bool isResearched = mgr.IsOptionResearched(selectedBuilding, selectedTier, selectedIsOptionA);
        if (isResearched)
        {
            SetResearchButtonState(false, LocalizationManager.Get("UI_WORKSHOP_BTN_RESEARCHED", "Zbadano"), ColorBtnDisabledBg, ColorBtnDisabledText);
            return;
        }

        bool isBlockedByAlt = mgr.IsTierLockedByAlternative(selectedBuilding, selectedTier, selectedIsOptionA);
        if (isBlockedByAlt)
        {
            SetResearchButtonState(false, LocalizationManager.Get("UI_WORKSHOP_BTN_LOCKED", "Zablokowano"), ColorBtnDisabledBg, ColorBtnDisabledText);
            return;
        }

        bool isTierAvailable = mgr.IsTierAvailableToResearch(selectedBuilding, selectedTier);
        if (!isTierAvailable)
        {
            SetResearchButtonState(false, string.Format(LocalizationManager.Get("UI_WORKSHOP_BTN_REQUIRES_TIER_FMT", "Wymaga Tier {0}"), selectedTier - 1), ColorBtnDisabledBg, ColorBtnDisabledText);
            return;
        }

        if (!mgr.CanAffordUpgrade(upgrade))
        {
            SetResearchButtonState(false, LocalizationManager.Get("UI_WORKSHOP_BTN_NO_RESOURCES", "Brak surowców"), ColorBtnDisabledBg, ColorBtnDisabledText);
            return;
        }

        SetResearchButtonState(true, LocalizationManager.Get("UI_WORKSHOP_BTN_RESEARCH", "Opracuj technologię"), ColorBtnReadyBg, ColorBtnReadyText);
    }

    private void SetResearchButtonState(bool interactable, string label, Color bgColor, Color textColor)
    {
        if (btnResearch != null) btnResearch.interactable = interactable;
        if (imgResearchButton != null) imgResearchButton.color = bgColor;
        if (txtResearchButtonLabel != null)
        {
            txtResearchButtonLabel.text = label;
            txtResearchButtonLabel.color = textColor;
        }
    }

    private BuildingUpgradeSO ResolveSelectedUpgrade(ToolWorkshopResearchManager mgr)
    {
        if (mgr == null || mgr.TechCatalog == null || selectedBuilding == null) return null;
        if (!mgr.TechCatalog.TryGetTree(selectedBuilding, out var tree)) return null;
        return tree.GetTier(selectedTier)?.GetOption(selectedIsOptionA);
    }

    public static string BuildUpgradeEffectsText(BuildingUpgradeSO upgrade)
    {
        if (upgrade == null) return string.Empty;

        var lines = new List<string>();

        if (upgrade.productionBonus != null)
        {
            for (int i = 0; i < upgrade.productionBonus.Count; i++)
            {
                var res = upgrade.productionBonus[i];
                if (Mathf.Abs(res.amount) > 0.001f)
                {
                    lines.Add(string.Format(LocalizationManager.Get("UI_WORKSHOP_EFF_BASE_PROD_FMT", "• {0} {1} do bazowej produkcji"), FormatSigned(res.amount), GetResourceName(res.type)));
                }
            }
        }

        if (upgrade.upkeepReduction != null)
        {
            for (int i = 0; i < upgrade.upkeepReduction.Count; i++)
            {
                var res = upgrade.upkeepReduction[i];
                if (res.amount > 0.001f)
                {
                    lines.Add(string.Format(LocalizationManager.Get("UI_WORKSHOP_EFF_UPKEEP_RED_FMT", "• -{0} {1} dziennego utrzymania"), FormatNumber(res.amount), GetResourceName(res.type)));
                }
            }
        }

        if (upgrade.upkeepIncrease != null)
        {
            for (int i = 0; i < upgrade.upkeepIncrease.Count; i++)
            {
                var res = upgrade.upkeepIncrease[i];
                if (res.amount > 0.001f)
                {
                    lines.Add(string.Format(LocalizationManager.Get("UI_WORKSHOP_EFF_UPKEEP_INC_FMT", "• +{0} {1} dziennego utrzymania"), FormatNumber(res.amount), GetResourceName(res.type)));
                }
            }
        }

        if (Mathf.Abs(upgrade.globalProductionMultiplierBonus) > 0.001f)
        {
            lines.Add(string.Format(LocalizationManager.Get("UI_WORKSHOP_EFF_GLOBAL_PROD_FMT", "• {0} globalnej wydajności budynku"), FormatSignedPercent(upgrade.globalProductionMultiplierBonus)));
        }

        if (Mathf.Abs(upgrade.globalUpkeepMultiplierBonus) > 0.001f)
        {
            lines.Add(string.Format(LocalizationManager.Get("UI_WORKSHOP_EFF_GLOBAL_UPKEEP_FMT", "• {0} kosztów utrzymania budynku"), FormatSignedPercent(upgrade.globalUpkeepMultiplierBonus)));
        }

        if (upgrade.extraSearchRadius != 0)
        {
            lines.Add(string.Format(LocalizationManager.Get("UI_WORKSHOP_EFF_SEARCH_RADIUS_FMT", "• {0} do zasięgu skanowania terenu"), FormatSigned(upgrade.extraSearchRadius)));
        }

        if (Mathf.Abs(upgrade.terrainFlatBonus) > 0.001f)
        {
            lines.Add(string.Format(LocalizationManager.Get("UI_WORKSHOP_EFF_TERRAIN_FLAT_FMT", "• {0} produkcji za każde pasujące pole w zasięgu"), FormatSigned(upgrade.terrainFlatBonus)));
        }

        if (Mathf.Abs(upgrade.terrainPercentBonus) > 0.001f)
        {
            lines.Add(string.Format(LocalizationManager.Get("UI_WORKSHOP_EFF_TERRAIN_PCT_FMT", "• {0} premii z sąsiadującego terenu"), FormatSignedPercent(upgrade.terrainPercentBonus)));
        }

        if (upgrade.extraShifts != 0)
        {
            lines.Add(string.Format(LocalizationManager.Get("UI_WORKSHOP_EFF_EXTRA_SHIFTS_FMT", "• {0} zmiana pracy"), FormatSigned(upgrade.extraShifts)));
        }

        if (upgrade.extraWorkersPerShift != 0)
        {
            lines.Add(string.Format(LocalizationManager.Get("UI_WORKSHOP_EFF_EXTRA_WORKERS_FMT", "• {0} miejsc na pracowników"), FormatSigned(upgrade.extraWorkersPerShift)));
        }

        if (Mathf.Abs(upgrade.firstWorkerBonusMultiplier) > 0.001f)
        {
            lines.Add(string.Format(LocalizationManager.Get("UI_WORKSHOP_EFF_FIRST_WORKER_FMT", "• {0} wydajności 1. pracownika"), FormatSignedPercent(upgrade.firstWorkerBonusMultiplier)));
        }

        if (upgrade.passiveNoWorkerEfficiency > 0.001f)
        {
            lines.Add(string.Format(LocalizationManager.Get("UI_WORKSHOP_EFF_PASSIVE_EFF_FMT", "• {0}% pasywnej wydajności bez pracowników"), Mathf.RoundToInt(upgrade.passiveNoWorkerEfficiency * 100f)));
        }

        bool hasUnmappedSpecial = false;
        if (upgrade.specialEffectIDs != null)
        {
            for (int i = 0; i < upgrade.specialEffectIDs.Count; i++)
            {
                string id = upgrade.specialEffectIDs[i];
                if (string.IsNullOrEmpty(id)) continue;

                if (SpecialEffectDescriptions.TryGetValue(id, out string desc))
                {
                    lines.Add($"• {LocalizationManager.Get($"WORKSHOP_SPEC_{id}", desc)}");
                }
                else
                {
                    hasUnmappedSpecial = true;
                }
            }
        }

        if (lines.Count == 0 || hasUnmappedSpecial)
        {
            string fallbackDesc = upgrade.DisplayDescription;
            if (!string.IsNullOrWhiteSpace(fallbackDesc))
            {
                if (lines.Count == 0)
                {
                    lines.Add($"• {fallbackDesc}");
                }
            }
        }

        return string.Join("\n", lines);
    }

    private static List<WorkshopTierEntry> GetSortedTiers(WorkshopBuildingTree tree)
    {
        var result = new List<WorkshopTierEntry>();
        if (tree == null || tree.tiers == null) return result;

        for (int i = 0; i < tree.tiers.Count; i++)
        {
            if (tree.tiers[i] != null) result.Add(tree.tiers[i]);
        }

        result.Sort((a, b) => a.tier.CompareTo(b.tier));
        return result;
    }

    private static bool IsCurrentlyResearching(ToolWorkshopResearchManager mgr, BuildingData building, int tier, bool isOptionA)
    {
        if (mgr == null || mgr.ActiveResearch == null || !mgr.ActiveResearch.IsActive || building == null)
            return false;

        return IsSameBuilding(mgr.ActiveResearch.targetBuilding, building) &&
               mgr.ActiveResearch.tier == tier &&
               mgr.ActiveResearch.isOptionA == isOptionA;
    }

    private static bool IsSameBuilding(BuildingData a, BuildingData b)
    {
        if (a == null || b == null) return false;
        return a == b || string.Equals(
            ToolWorkshopResearchManager.GetBuildingKey(a),
            ToolWorkshopResearchManager.GetBuildingKey(b),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatNumber(float value)
    {
        float rounded = Mathf.Round(value * 100f) / 100f;
        return Mathf.Approximately(rounded, Mathf.Round(rounded))
            ? Mathf.RoundToInt(rounded).ToString(CultureInfo.InvariantCulture)
            : rounded.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatSigned(float value)
    {
        string prefix = value >= 0f ? "+" : "-";
        return $"{prefix}{FormatNumber(Mathf.Abs(value))}";
    }

    private static string FormatSignedPercent(float value)
    {
        float pct = value * 100f;
        string prefix = pct >= 0f ? "+" : "-";
        return $"{prefix}{FormatNumber(Mathf.Abs(pct))}%";
    }

    private static string GetResourceName(ResourceType type)
    {
        return type switch
        {
            ResourceType.Ether => LocalizationManager.Get("RES_ETHER", "Kryształu Eteru"),
            ResourceType.Wood => LocalizationManager.Get("RES_WOOD", "Drewna"),
            ResourceType.Stone => LocalizationManager.Get("RES_STONE", "Kamienia"),
            ResourceType.Coal => LocalizationManager.Get("RES_COAL", "Węgla"),
            ResourceType.Food => LocalizationManager.Get("RES_FOOD", "Jedzenia"),
            ResourceType.Iron => LocalizationManager.Get("RES_IRON", "Metalu"),
            ResourceType.Hope => LocalizationManager.Get("RES_HOPE", "Nadziei"),
            _ => type.ToString()
        };
    }

    private static Transform FindDeepTransform(Transform parent, string childName)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName) return child;
            Transform nested = FindDeepTransform(child, childName);
            if (nested != null) return nested;
        }
        return null;
    }

    private static T FindDeepChild<T>(Transform parent, string childName) where T : Component
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                var c = child.GetComponent<T>();
                if (c != null) return c;
            }
            var found = FindDeepChild<T>(child, childName);
            if (found != null) return found;
        }
        return null;
    }
}
