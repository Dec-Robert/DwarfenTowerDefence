using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Główny kontroler pełnoekranowego panelu Kuźni Runicznej (Pnl_RuneForgeFullscreen).
/// Obsługuje:
/// 1. Zakładkę Osadzania Run (View_Socketing) – sloty zależne od kowali i sąsiednich wież runicznych,
/// 2. Zakładkę Fuzji Run (View_Fusion) – łączenie 2 run tego samego poziomu w 1 wyższą,
/// 3. Zakładkę Transmutacji Run (View_Transmutation) – wymianę 3 run tego samego poziomu na losową wyższą,
/// 4. Prawy panel ekwipunku gracza z filtrami rzadkości,
/// 5. Dolny panel podsumowania statystyk nasycanej wieży oraz przydział kowali.
/// </summary>
public class RuneForgeWindowUI : MonoBehaviour
{
    public enum RuneForgeTab
    {
        Socketing,
        Fusion,
        Transmutation
    }

    // ── 1. Górna Belka ──────────────────────────────────────────────────────
    [Header("Górna Belka")]
    [SerializeField] private TextMeshProUGUI txtForgeTitle;
    [SerializeField] private TextMeshProUGUI txtSubTitle;
    [SerializeField] private Button btnClose;

    // ── 2. Lewy Panel Nawigacji ─────────────────────────────────────────────
    [Header("Lewy Panel Nawigacji")]
    [SerializeField] private Button btnTabSocketing;
    [SerializeField] private Button btnTabFusion;
    [SerializeField] private Button btnTabTransmutation;
    [SerializeField] private TextMeshProUGUI txtRuneTowersDesc;

    // ── 3. Środkowy Panel Akcji ─────────────────────────────────────────────
    [Header("Środkowe Widoki")]
    [SerializeField] private GameObject viewSocketing;
    [SerializeField] private GameObject viewFusion;
    [SerializeField] private GameObject viewTransmutation;

    // ── Widok 1: Osadzanie Run ──────────────────────────────────────────────
    [Header("Widok 1: Osadzanie")]
    [SerializeField] private Image imgTowerHeaderIcon;
    [SerializeField] private TextMeshProUGUI txtTowerHeaderName;
    [SerializeField] private TextMeshProUGUI txtTowerSubTitle;
    [SerializeField] private Transform pnlSlotsRow;

    // ── Widok 2: Fuzja Run ──────────────────────────────────────────────────
    [Header("Widok 2: Fuzja")]
    [SerializeField] private Button btnFusionBackToTower;
    [SerializeField] private Transform slotFusion1;
    [SerializeField] private Transform slotFusion2;
    [SerializeField] private Transform slotFusionResult;
    [SerializeField] private TextMeshProUGUI txtFusionReqHeader;
    [SerializeField] private Button btnCraftFusion;

    // ── Widok 3: Transmutacja Run ───────────────────────────────────────────
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
    [SerializeField] private Button btnFilterAll;
    [SerializeField] private Button btnFilterCommon;
    [SerializeField] private Button btnFilterRare;
    [SerializeField] private Button btnFilterLegendary;
    [SerializeField] private Transform inventoryContent;

    // ── 5. Dolny Panel: Statystyki i Kowale ─────────────────────────────────
    [Header("Dolny Panel")]
    [SerializeField] private Transform pnlStatsGrid;
    [SerializeField] private TextMeshProUGUI txtSmithsCount;
    [SerializeField] private Button btnRemoveSmith;
    [SerializeField] private Button btnAddSmith;
    [SerializeField] private Transform pnlStationsRow;

    // ── Stan Bieżącej Kuźni ─────────────────────────────────────────────────
    private RuneForgeEntity currentForge;
    private RuneForgeTab currentTab = RuneForgeTab.Socketing;
    private RuneRarity? currentFilter = null;

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
        UnsubscribeEvents();
    }

    // =========================================================================
    // INICJALIZACJA I AUTO-BINDING
    // =========================================================================

    public void AutoBindReferences()
    {
        // 1. Górna Belka
        if (txtForgeTitle == null)
            txtForgeTitle = transform.Find("Pnl_TopBar/Txt_ForgeTitle")?.GetComponent<TextMeshProUGUI>();

        if (txtSubTitle == null)
            txtSubTitle = transform.Find("Pnl_TopBar/Txt_SubTitle")?.GetComponent<TextMeshProUGUI>();

        if (btnClose == null)
            btnClose = transform.Find("Pnl_TopBar/Btn_Close")?.GetComponent<Button>();

        // 2. Lewy Panel
        if (btnTabSocketing == null)
            btnTabSocketing = transform.Find("Pnl_BodyRow/Pnl_LeftNav/Btn_TabSocketing")?.GetComponent<Button>();

        if (btnTabFusion == null)
            btnTabFusion = transform.Find("Pnl_BodyRow/Pnl_LeftNav/Btn_TabFusion")?.GetComponent<Button>();

        if (btnTabTransmutation == null)
            btnTabTransmutation = transform.Find("Pnl_BodyRow/Pnl_LeftNav/Btn_TabTransmutation")?.GetComponent<Button>();

        if (txtRuneTowersDesc == null)
            txtRuneTowersDesc = transform.Find("Pnl_BodyRow/Pnl_LeftNav/Pnl_RuneTowersStatus/Txt_InfoDesc")?.GetComponent<TextMeshProUGUI>();

        // 3. Widoki Środkowe
        if (viewSocketing == null)
            viewSocketing = transform.Find("Pnl_BodyRow/Pnl_CenterAction/View_Socketing")?.gameObject;

        if (viewFusion == null)
            viewFusion = transform.Find("Pnl_BodyRow/Pnl_CenterAction/View_Fusion")?.gameObject;

        if (viewTransmutation == null)
            viewTransmutation = transform.Find("Pnl_BodyRow/Pnl_CenterAction/View_Transmutation")?.gameObject;

        // Widok 1: Osadzanie
        if (viewSocketing != null)
        {
            if (imgTowerHeaderIcon == null)
                imgTowerHeaderIcon = viewSocketing.transform.Find("Pnl_TowerHeaderCard/Img_TowerIconFrame/Img_TowerIcon")?.GetComponent<Image>();

            if (txtTowerHeaderName == null)
                txtTowerHeaderName = viewSocketing.transform.Find("Pnl_TowerHeaderCard/Txt_TowerHeaderName")?.GetComponent<TextMeshProUGUI>();

            if (txtTowerSubTitle == null)
                txtTowerSubTitle = viewSocketing.transform.Find("Pnl_TowerHeaderCard/Txt_TowerSubTitle")?.GetComponent<TextMeshProUGUI>();

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
                btnTransmuteBackToTower = viewTransmutation.transform.Find("Pnl_TransmuteTopBar/Btn_BackToTower")?.GetComponent<Button>();

            if (slotTransmute1 == null)
                slotTransmute1 = viewTransmutation.transform.Find("Pnl_AnvilTransmute/Pnl_TransmuteInputs/Slot_Transmute_1");

            if (slotTransmute2 == null)
                slotTransmute2 = viewTransmutation.transform.Find("Pnl_AnvilTransmute/Pnl_TransmuteInputs/Slot_Transmute_2");

            if (slotTransmute3 == null)
                slotTransmute3 = viewTransmutation.transform.Find("Pnl_AnvilTransmute/Pnl_TransmuteInputs/Slot_Transmute_3");

            if (slotTransmuteResult == null)
                slotTransmuteResult = viewTransmutation.transform.Find("Pnl_AnvilTransmute/Slot_Transmute_Result");

            if (txtTransmuteReqHeader == null)
                txtTransmuteReqHeader = viewTransmutation.transform.Find("Pnl_TransmuteReqs/Txt_ReqHeader")?.GetComponent<TextMeshProUGUI>();

            if (btnCraftTransmute == null)
                btnCraftTransmute = viewTransmutation.transform.Find("Btn_CraftTransmute")?.GetComponent<Button>();
        }

        // 4. Ekwipunek
        if (txtInvCount == null)
            txtInvCount = transform.Find("Pnl_BodyRow/Pnl_RightInventory/Pnl_InvHeader/Txt_InvCount")?.GetComponent<TextMeshProUGUI>();

        if (btnFilterAll == null)
            btnFilterAll = transform.Find("Pnl_BodyRow/Pnl_RightInventory/Pnl_FilterRow/Btn_Filter_All")?.GetComponent<Button>();

        if (btnFilterCommon == null)
            btnFilterCommon = transform.Find("Pnl_BodyRow/Pnl_RightInventory/Pnl_FilterRow/Btn_Filter_Common")?.GetComponent<Button>();

        if (btnFilterRare == null)
            btnFilterRare = transform.Find("Pnl_BodyRow/Pnl_RightInventory/Pnl_FilterRow/Btn_Filter_Rare")?.GetComponent<Button>();

        if (btnFilterLegendary == null)
            btnFilterLegendary = transform.Find("Pnl_BodyRow/Pnl_RightInventory/Pnl_FilterRow/Btn_Filter_Legendary")?.GetComponent<Button>();

        if (inventoryContent == null)
            inventoryContent = transform.Find("Pnl_BodyRow/Pnl_RightInventory/Scroll_Inventory/Viewport/Content");

        // 5. Dolny Panel
        if (pnlStatsGrid == null)
            pnlStatsGrid = transform.Find("Pnl_BottomPanel/Sec_TowerStats/Pnl_StatsGrid");

        if (txtSmithsCount == null)
            txtSmithsCount = transform.Find("Pnl_BottomPanel/Sec_Runesmiths/Pnl_SmithsStepper/Txt_SmithsCount")?.GetComponent<TextMeshProUGUI>();

        if (btnRemoveSmith == null)
            btnRemoveSmith = transform.Find("Pnl_BottomPanel/Sec_Runesmiths/Pnl_SmithsStepper/Btn_RemoveSmith")?.GetComponent<Button>();

        if (btnAddSmith == null)
            btnAddSmith = transform.Find("Pnl_BottomPanel/Sec_Runesmiths/Pnl_SmithsStepper/Btn_AddSmith")?.GetComponent<Button>();

        if (pnlStationsRow == null)
            pnlStationsRow = transform.Find("Pnl_BottomPanel/Sec_Runesmiths/Pnl_StationsRow");
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

        // Domyślnie otwieramy zakładkę osadzania
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

        // Filtry inwentarza
        if (btnFilterAll != null)
        {
            btnFilterAll.onClick.RemoveAllListeners();
            btnFilterAll.onClick.AddListener(() => SetRarityFilter(null));
        }

        if (btnFilterCommon != null)
        {
            btnFilterCommon.onClick.RemoveAllListeners();
            btnFilterCommon.onClick.AddListener(() => SetRarityFilter(RuneRarity.Common));
        }

        if (btnFilterRare != null)
        {
            btnFilterRare.onClick.RemoveAllListeners();
            btnFilterRare.onClick.AddListener(() => SetRarityFilter(RuneRarity.Rare));
        }

        if (btnFilterLegendary != null)
        {
            btnFilterLegendary.onClick.RemoveAllListeners();
            btnFilterLegendary.onClick.AddListener(() => SetRarityFilter(RuneRarity.Legendary));
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

        // Przydział kowali
        if (btnRemoveSmith != null)
        {
            btnRemoveSmith.onClick.RemoveAllListeners();
            btnRemoveSmith.onClick.AddListener(HandleRemoveSmith);
        }

        if (btnAddSmith != null)
        {
            btnAddSmith.onClick.RemoveAllListeners();
            btnAddSmith.onClick.AddListener(HandleAddSmith);
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

        // Czyścimy sloty rzemiosła przy przełączaniu
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
            string towerName = currentForge.infusedTowerData != null ? currentForge.infusedTowerData.buildingName.ToUpper() : "BRAK WIEŻY";
            txtForgeTitle.text = $"KUŹNIA RUNICZNA — [{towerName}]";
        }
    }

    private void RefreshLeftNav()
    {
        if (txtRuneTowersDesc != null && currentForge != null)
        {
            int towersCount = currentForge.GetNeighboringRuneTowersCount();
            txtRuneTowersDesc.text = $"Sąsiedztwo: {towersCount} z 4 wież\nOdblokowują sloty i kowali.";
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

        // Sloty na runy
        if (pnlSlotsRow != null)
        {
            int maxSlots = currentForge.GetMaxSlots();
            int activeSlots = currentForge.GetActiveRuneSlots();
            var equipped = currentForge.equippedRunes ?? new List<RuneItem>();

            for (int i = 0; i < pnlSlotsRow.childCount; i++)
            {
                int slotIndex = i; // 0-indexed
                Transform slotTr = pnlSlotsRow.GetChild(i);
                slotTr.gameObject.SetActive(true);

                bool isUnlockedByTower = slotIndex < maxSlots;
                bool hasSmith = slotIndex < activeSlots;

                // Baner braku kowala
                Transform badge = slotTr.Find("Badge_MissingSmith");
                if (badge != null)
                {
                    badge.gameObject.SetActive(isUnlockedByTower && !hasSmith);
                }

                // Tło slotu
                Image slotBg = slotTr.GetComponent<Image>();
                if (slotBg != null)
                {
                    slotBg.color = (isUnlockedByTower && hasSmith)
                        ? new Color(0.13f, 0.15f, 0.2f, 0.95f)
                        : new Color(0.08f, 0.09f, 0.11f, 0.85f);
                }

                // Ikona i tekst
                Image iconImg = slotTr.Find("Img_SocketIcon")?.GetComponent<Image>() ?? slotTr.Find("Img_LockIcon")?.GetComponent<Image>();
                TextMeshProUGUI lblTmp = slotTr.Find("Txt_SlotLabel")?.GetComponent<TextMeshProUGUI>();

                if (isUnlockedByTower && hasSmith)
                {
                    if (slotIndex < equipped.Count)
                    {
                        // Runa osadzona w tym slocie
                        var rune = equipped[slotIndex];
                        if (iconImg != null)
                        {
                            iconImg.sprite = rune.definition != null ? rune.definition.icon : null;
                            iconImg.color = GetRarityColor(rune.definition != null ? rune.definition.rarity : RuneRarity.Common);
                        }

                        if (lblTmp != null)
                        {
                            lblTmp.text = $"<color=white><b>{rune.definition?.runeName}</b></color>\n<size=10>{rune.GetPrimaryText()}</size>";
                        }
                    }
                    else
                    {
                        // Wolny odblokowany slot
                        if (iconImg != null)
                        {
                            iconImg.sprite = null;
                            iconImg.color = new Color(0.96f, 0.78f, 0.28f, 0.8f);
                        }

                        if (lblTmp != null)
                        {
                            lblTmp.text = $"SLOT {slotIndex + 1}\n<size=10><color=#999999>WOLNY SLOT</color></size>";
                        }
                    }
                }
                else if (isUnlockedByTower && !hasSmith)
                {
                    // Odblokowany przez wieżę, ale brak kowala
                    if (iconImg != null)
                    {
                        iconImg.sprite = null;
                        iconImg.color = new Color(0.96f, 0.78f, 0.28f, 0.4f);
                    }

                    if (lblTmp != null)
                    {
                        lblTmp.text = $"SLOT {slotIndex + 1}\n<size=10><color=#FFCC66>WYMAGA KOWALA</color></size>";
                    }
                }
                else
                {
                    // Zablokowany (wymaga wieży runicznej)
                    if (iconImg != null)
                    {
                        iconImg.sprite = null;
                        iconImg.color = new Color(0.55f, 0.58f, 0.65f, 0.4f);
                    }

                    if (lblTmp != null)
                    {
                        lblTmp.text = $"SLOT {slotIndex + 1}\n<size=10><color=#666666>WYMAGA WIEŻY</color></size>";
                    }
                }

                // Podpięcie kliknięcia w slot (do zdjęcia osadzonej runy)
                Button slotBtn = slotTr.GetComponent<Button>();
                if (slotBtn == null) slotBtn = slotTr.gameObject.AddComponent<Button>();
                slotBtn.onClick.RemoveAllListeners();
                slotBtn.onClick.AddListener(() => OnSlotClicked(slotIndex));
            }
        }
    }

    private void OnSlotClicked(int slotIndex)
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

    // =========================================================================
    // PRAWE MENU EKWIPUNKU
    // =========================================================================

    public void SetRarityFilter(RuneRarity? rarity)
    {
        currentFilter = rarity;
        RefreshInventoryView();
    }

    private void RefreshInventoryView()
    {
        if (inventoryContent == null || RuneManager.Instance == null) return;

        var allRunes = RuneManager.Instance.playerRunes ?? new List<RuneItem>();
        var filtered = new List<RuneItem>();

        foreach (var r in allRunes)
        {
            if (r == null || r.definition == null) continue;
            // Pomijamy runy aktualnie umieszczone na kowadle fuzji/transmutacji
            if (r == fusionRune1 || r == fusionRune2) continue;
            if (r == transmuteRune1 || r == transmuteRune2 || r == transmuteRune3) continue;

            if (currentFilter == null || r.definition.rarity == currentFilter.Value)
            {
                filtered.Add(r);
            }
        }

        if (txtInvCount != null)
        {
            txtInvCount.text = $"{allRunes.Count} / 30";
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

        Color rarityColor = GetRarityColor(rune.definition != null ? rune.definition.rarity : RuneRarity.Common);

        Image itemBg = itemTr.GetComponent<Image>();
        if (itemBg != null) itemBg.color = new Color(0.14f, 0.16f, 0.2f, 0.95f);

        Image iconImg = itemTr.Find("Img_RuneIcon")?.GetComponent<Image>();
        if (iconImg != null)
        {
            iconImg.sprite = rune.definition != null ? rune.definition.icon : null;
            iconImg.color = rarityColor;
        }

        TextMeshProUGUI valTmp = itemTr.Find("Txt_Val")?.GetComponent<TextMeshProUGUI>();
        if (valTmp != null)
        {
            valTmp.text = rune.GetPrimaryText();
            valTmp.color = Color.white;
        }

        Button itemBtn = itemTr.GetComponent<Button>();
        if (itemBtn == null) itemBtn = itemTr.gameObject.AddComponent<Button>();
        itemBtn.onClick.RemoveAllListeners();
        itemBtn.onClick.AddListener(() => OnInventoryRuneClicked(rune));
    }

    private void OnInventoryRuneClicked(RuneItem rune)
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

    // =========================================================================
    // FUZJA I TRANSMUTACJA
    // =========================================================================

    private void RefreshCraftingSlots()
    {
        // ── 1. Fuzja ────────────────────────────────────────────────────────
        SetupCraftSlot(slotFusion1, fusionRune1, () => { fusionRune1 = null; RefreshCraftingSlots(); RefreshInventoryView(); });
        SetupCraftSlot(slotFusion2, fusionRune2, () => { fusionRune2 = null; RefreshCraftingSlots(); RefreshInventoryView(); });

        bool canFuse = fusionRune1 != null && fusionRune2 != null &&
                       fusionRune1.definition.rarity == fusionRune2.definition.rarity &&
                       fusionRune1.definition.primaryStat == fusionRune2.definition.primaryStat &&
                       fusionRune1.definition.rarity != RuneRarity.Cursed;

        if (txtFusionReqHeader != null)
        {
            int placed = (fusionRune1 != null ? 1 : 0) + (fusionRune2 != null ? 1 : 0);
            txtFusionReqHeader.text = $"WYMAGANIA FUZJI: <color={(canFuse ? "#45f375" : "#FFCC66")}>{placed} / 2 RUN</color>";
        }

        if (btnCraftFusion != null)
        {
            btnCraftFusion.interactable = canFuse;
        }

        // ── 2. Transmutacja ──────────────────────────────────────────────────
        SetupCraftSlot(slotTransmute1, transmuteRune1, () => { transmuteRune1 = null; RefreshCraftingSlots(); RefreshInventoryView(); });
        SetupCraftSlot(slotTransmute2, transmuteRune2, () => { transmuteRune2 = null; RefreshCraftingSlots(); RefreshInventoryView(); });
        SetupCraftSlot(slotTransmute3, transmuteRune3, () => { transmuteRune3 = null; RefreshCraftingSlots(); RefreshInventoryView(); });

        bool canTransmute = transmuteRune1 != null && transmuteRune2 != null && transmuteRune3 != null &&
                            transmuteRune1.definition.rarity == transmuteRune2.definition.rarity &&
                            transmuteRune1.definition.rarity == transmuteRune3.definition.rarity &&
                            transmuteRune1.definition.rarity != RuneRarity.Cursed;

        if (txtTransmuteReqHeader != null)
        {
            int placed = (transmuteRune1 != null ? 1 : 0) + (transmuteRune2 != null ? 1 : 0) + (transmuteRune3 != null ? 1 : 0);
            txtTransmuteReqHeader.text = $"WYMAGANIA TRANSMUTACJI: <color={(canTransmute ? "#45f375" : "#FFCC66")}>{placed} / 3 RUN</color>";
        }

        if (btnCraftTransmute != null)
        {
            btnCraftTransmute.interactable = canTransmute;
        }
    }

    private void SetupCraftSlot(Transform slotTr, RuneItem rune, System.Action onRemove)
    {
        if (slotTr == null) return;

        TextMeshProUGUI lblTmp = slotTr.Find("Txt_SlotLabel")?.GetComponent<TextMeshProUGUI>();
        Image iconImg = slotTr.Find("Img_SocketIcon")?.GetComponent<Image>();
        Button slotBtn = slotTr.GetComponent<Button>();
        if (slotBtn == null) slotBtn = slotTr.gameObject.AddComponent<Button>();

        slotBtn.onClick.RemoveAllListeners();

        if (rune != null)
        {
            if (lblTmp != null) lblTmp.text = $"{rune.definition?.runeName}\n<size=10>{rune.GetPrimaryText()}</size>";
            if (iconImg != null)
            {
                iconImg.sprite = rune.definition?.icon;
                iconImg.color = GetRarityColor(rune.definition != null ? rune.definition.rarity : RuneRarity.Common);
            }
            if (onRemove != null) slotBtn.onClick.AddListener(() => onRemove());
        }
        else
        {
            if (lblTmp != null) lblTmp.text = "WOLNY SLOT\n<size=10><color=#999999>KLIKNIJ RUNĘ</color></size>";
            if (iconImg != null)
            {
                iconImg.sprite = null;
                iconImg.color = new Color(0.96f, 0.78f, 0.28f, 0.4f);
            }
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

    // =========================================================================
    // DOLNY PANEL: STATYSTYKI I KOWALE
    // =========================================================================

    private void RefreshBottomStats()
    {
        if (currentForge == null || currentForge.infusedTowerData == null || pnlStatsGrid == null) return;

        var tower = currentForge.infusedTowerData;
        var equipped = currentForge.equippedRunes ?? new List<RuneItem>();

        // Agregacja modyfikatorów z wyposażonych run
        float dmgFlat = 0, dmgPct = 0;
        float rngFlat = 0, rngPct = 0;
        float frFlat = 0, frPct = 0;
        float apFlat = 0, apPct = 0;
        float ccFlat = 0, ccPct = 0;
        float cdFlat = 0, cdPct = 0;

        foreach (var r in equipped)
        {
            AddStatContribution(r.definition.primaryStat, r.definition.valueType, r.primaryValue,
                ref dmgFlat, ref dmgPct, ref rngFlat, ref rngPct, ref frFlat, ref frPct,
                ref apFlat, ref apPct, ref ccFlat, ref ccPct, ref cdFlat, ref cdPct);

            if (r.hasPenalty)
            {
                AddStatContribution(r.penaltyStat, r.penaltyValueType, r.penaltyValue,
                    ref dmgFlat, ref dmgPct, ref rngFlat, ref rngPct, ref frFlat, ref frPct,
                    ref apFlat, ref apPct, ref ccFlat, ref ccPct, ref cdFlat, ref cdPct);
            }
        }

        // Wyliczenie zmodyfikowanych wartości
        float baseDmg = tower.baseDamage;
        float modDmg = (baseDmg + dmgFlat) * (1f + dmgPct / 100f);

        float baseRng = tower.baseRange;
        float modRng = (baseRng + rngFlat) * (1f + rngPct / 100f);

        float baseFr = tower.fireRate;
        float modFr = (baseFr + frFlat) * (1f + frPct / 100f);

        float baseAp = tower.armorPenetration;
        float modAp = (baseAp + apFlat) * (1f + apPct / 100f);

        float baseCc = tower.criticalChancel;
        float modCc = (baseCc + ccFlat) * (1f + ccPct / 100f);

        float baseCd = tower.criticalDamageMultiplier;
        float modCd = (baseCd + cdFlat) * (1f + cdPct / 100f);

        // Ustawienie tekstów w wierszach statystyk
        UpdateStatRow("Col_Left/Row_Obrażenia", $"{baseDmg:0.#}", $"{modDmg:0.#}", modDmg - baseDmg);
        UpdateStatRow("Col_Left/Row_Zasięg", $"{baseRng:0.#}", $"{modRng:0.#}", modRng - baseRng);
        UpdateStatRow("Col_Left/Row_Szybkostrzelność", $"{baseFr:0.##}/s", $"{modFr:0.##}/s", modFr - baseFr);

        UpdateStatRow("Col_Right/Row_Przebicie_pancerza", $"{baseAp:0.#}%", $"{modAp:0.#}%", modAp - baseAp);
        UpdateStatRow("Col_Right/Row_Szansa_na_krytyk", $"{baseCc:0.#}%", $"{modCc:0.#}%", modCc - baseCc);
        UpdateStatRow("Col_Right/Row_Mnożnik_krytyczny", $"{baseCd:0.#}%", $"{modCd:0.#}%", modCd - baseCd);
    }

    private void UpdateStatRow(string path, string baseVal, string modVal, float diff)
    {
        var row = pnlStatsGrid.Find(path);
        if (row == null) return;

        var valTmp = row.Find("Txt_Value")?.GetComponent<TextMeshProUGUI>();
        if (valTmp != null)
        {
            string diffStr = Mathf.Abs(diff) > 0.01f ? $" ({(diff > 0 ? "+" : "")}{diff:0.#})" : "";
            valTmp.text = $"<color=#888888>{baseVal}</color> → <color={(diff >= 0 ? "#45f375" : "#FF6666")}>{modVal}{diffStr}</color>";
        }
    }

    private void AddStatContribution(RuneStatType stat, RuneValueType vType, float val,
        ref float dmgF, ref float dmgP, ref float rngF, ref float rngP, ref float frF, ref float frP,
        ref float apF, ref float apP, ref float ccF, ref float ccP, ref float cdF, ref float cdP)
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
            case RuneStatType.CriticalChance:
                if (vType == RuneValueType.Flat) ccF += val; else ccP += val;
                break;
            case RuneStatType.CriticalDamage:
                if (vType == RuneValueType.Flat) cdF += val; else cdP += val;
                break;
        }
    }

    private void RefreshRunesmiths()
    {
        if (currentForge == null) return;

        int assigned = currentForge.assignedDwarves != null ? currentForge.assignedDwarves.Count : 0;
        int maxSmiths = currentForge.GetMaxRunesmiths();

        if (txtSmithsCount != null)
        {
            txtSmithsCount.text = $"Kowale: {assigned} / {maxSmiths}";
        }

        if (btnRemoveSmith != null) btnRemoveSmith.interactable = assigned > 0;
        if (btnAddSmith != null) btnAddSmith.interactable = assigned < maxSmiths;

        // Stanowiska robocze
        if (pnlStationsRow != null)
        {
            for (int i = 0; i < pnlStationsRow.childCount; i++)
            {
                int stationIdx = i; // 0-indexed
                Transform stationTr = pnlStationsRow.GetChild(i);
                stationTr.gameObject.SetActive(true);

                bool isUnlocked = stationIdx < maxSmiths;
                bool hasSmith = stationIdx < assigned;

                Image stImg = stationTr.GetComponent<Image>();
                if (stImg != null)
                {
                    stImg.color = hasSmith
                        ? new Color(0.18f, 0.28f, 0.18f, 0.95f)
                        : (isUnlocked ? new Color(0.14f, 0.16f, 0.2f, 0.95f) : new Color(0.08f, 0.09f, 0.11f, 0.85f));
                }

                Image icon = stationTr.Find("Img_StationIcon")?.GetComponent<Image>();
                if (icon != null)
                {
                    icon.color = hasSmith ? new Color(0.45f, 0.95f, 0.55f, 1f) : new Color(0.5f, 0.52f, 0.6f, 0.6f);
                }

                TextMeshProUGUI lbl = stationTr.Find("Txt_StationLbl")?.GetComponent<TextMeshProUGUI>();
                if (lbl != null)
                {
                    lbl.text = hasSmith ? $"Kowal {stationIdx + 1}" : (isUnlocked ? "Wolny" : "Zablok.");
                    lbl.color = hasSmith ? new Color(0.45f, 0.95f, 0.55f, 1f) : new Color(0.6f, 0.62f, 0.68f, 1f);
                }
            }
        }
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

    private Color GetRarityColor(RuneRarity rarity)
    {
        return rarity switch
        {
            RuneRarity.Common => new Color(0.8f, 0.8f, 0.8f, 1f),
            RuneRarity.Uncommon => new Color(0.35f, 0.85f, 0.45f, 1f),
            RuneRarity.Rare => new Color(0.3f, 0.65f, 0.95f, 1f),
            RuneRarity.Legendary => new Color(0.96f, 0.78f, 0.28f, 1f),
            RuneRarity.Cursed => new Color(0.85f, 0.35f, 0.95f, 1f),
            _ => Color.white
        };
    }
}
