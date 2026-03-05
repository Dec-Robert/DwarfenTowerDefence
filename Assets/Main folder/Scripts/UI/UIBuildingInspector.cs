using UnityEngine;
using UnityEngine.UIElements;
using System.Text;

/// <summary>
/// Cienki koordynator inspektora budynku.
/// Jedyna odpowiedzialność: wybrać właściwe panele dla danego typu budynku
/// i przekazać im aktualny cel do odświeżenia.
/// </summary>
public class UIBuildingInspector : MonoBehaviour
{
    public static UIBuildingInspector Instance { get; private set; }

    [Header("Referencje")]
    public UIDocument uiDocument;

    // -------------------------------------------------------------------------
    // Elementy UI
    // -------------------------------------------------------------------------

    private VisualElement totalContainer;
    private VisualElement mainInspector;
    private VisualElement upgradeDetailsPanel;

    private Label  lblName;
    private Button btnClose;
    private Button btnDestroy;

    // Sekcje
    private VisualElement workerSection;
    private VisualElement statsContainer;
    private VisualElement combatStatsContainer;
    private VisualElement beaconControlSection;
    private VisualElement housingSection;
    private VisualElement upgradeSection;
    private VisualElement budgetSection;
    private VisualElement expeditionCenterSection;   // ← nowe

    private Label lblProd;
    private Label lblUpkeep;

    // -------------------------------------------------------------------------
    // Panele logiczne
    // -------------------------------------------------------------------------

    private BeaconInspectorPanel           beaconPanel;
    private HousingInspectorPanel          housingPanel;
    private TowerInspectorPanel            towerPanel;
    private WorkerInspectorPanel           workerPanel;
    private UpgradeInspectorPanel          upgradePanel;
    private BudgetInspectorPanel           budgetPanel;
    private ExpeditionCenterInspectorPanel expeditionPanel;  // ← nowe

    // -------------------------------------------------------------------------
    // Stan
    // -------------------------------------------------------------------------

    private BuildingEntity currentTarget;

    // =========================================================================
    // Cykl życia Unity
    // =========================================================================

    private void Awake() => Instance = this;

    private void OnEnable()
    {
        var uiRoot = uiDocument.rootVisualElement;

        totalContainer      = uiRoot.Q<VisualElement>("ScreenOverlay");
        mainInspector       = uiRoot.Q<VisualElement>("InspectorRoot");
        upgradeDetailsPanel = uiRoot.Q<VisualElement>("UpgradeDetailsPanel");

        lblName    = mainInspector.Q<Label>("Lbl_Name");
        btnClose   = mainInspector.Q<Button>("Btn_Close");
        btnDestroy = mainInspector.Q<Button>("Btn_Destroy");

        workerSection           = mainInspector.Q<VisualElement>("WorkerSection");
        statsContainer          = mainInspector.Q<VisualElement>("StatsContainer");
        combatStatsContainer    = mainInspector.Q<VisualElement>("CombatStatsContainer");
        beaconControlSection    = mainInspector.Q<VisualElement>("BeaconControlSection");
        housingSection          = mainInspector.Q<VisualElement>("HousingSection");
        upgradeSection          = mainInspector.Q<VisualElement>("UpgradeSection");
        budgetSection           = mainInspector.Q<VisualElement>("BudgetSection");
        expeditionCenterSection = mainInspector.Q<VisualElement>("ExpeditionCenterSection");  // ← nowe

        lblProd   = mainInspector.Q<Label>("Lbl_Production");
        lblUpkeep = mainInspector.Q<Label>("Lbl_Upkeep");

        btnClose.clicked   += Hide;
        btnDestroy.clicked += OnDestroyClicked;

        CreatePanels(mainInspector);
    }

    private void Update()
    {
        if (totalContainer.style.display == DisplayStyle.Flex && currentTarget != null)
            workerPanel?.UpdateSlotColors();
    }

    // =========================================================================
    // Tworzenie paneli
    // =========================================================================

    private void CreatePanels(VisualElement mi)
    {
        beaconPanel = new BeaconInspectorPanel(
            beaconControlSection,
            mi.Q<SliderInt>("Slider_CoalInput"),
            mi.Q<Label>("Lbl_CoalInputVal"));

        housingPanel = new HousingInspectorPanel(
            housingSection,
            mi.Q<VisualElement>("BirthBarFill"),
            mi.Q<VisualElement>("ResidentCapsules"),
            mi.Q<Label>("Lbl_BirthDays"),
            mi.Q<Label>("Lbl_HouseBase"),
            mi.Q<Label>("Lbl_HouseMaintenance"),
            mi.Q<Label>("Lbl_HouseTotal"),
            mi.Q<Label>("Lbl_GrowthMod"),
            mi.Q<Label>("Lbl_HouseStatus"));

        towerPanel = new TowerInspectorPanel(
            combatStatsContainer,
            mi.Q<Label>("Lbl_Damage"),
            mi.Q<Label>("Lbl_Range"),
            mi.Q<Label>("Lbl_FireRate"));

        workerPanel = new WorkerInspectorPanel(
            workerSection,
            mi.Q<Label>("Lbl_WorkerCounts"),
            mi.Q<VisualElement>("ShiftsContainer"),
            mi.Q<Button>("Btn_AddHuman"),  mi.Q<Button>("Btn_RemHuman"),
            mi.Q<Button>("Btn_AddElf"),    mi.Q<Button>("Btn_RemElf"),
            mi.Q<Button>("Btn_AddDwarf"),  mi.Q<Button>("Btn_RemDwarf"),
            onWorkerChanged: RefreshContent);

        upgradePanel = new UpgradeInspectorPanel(
            upgradeSection,
            mi.Q<VisualElement>("UpgradesList"),
            upgradeDetailsPanel,
            upgradeDetailsPanel.Q<Label>("Det_Title"),
            upgradeDetailsPanel.Q<Label>("Det_Desc"),
            upgradeDetailsPanel.Q<Label>("Det_Effects"),
            upgradeDetailsPanel.Q<Label>("Det_Cost"),
            upgradeDetailsPanel.Q<Button>("Btn_ConfirmBuy"),
            upgradeDetailsPanel.Q<Button>("Btn_CancelBuy"),
            onUpgradeBought: RefreshContent);

        budgetPanel = new BudgetInspectorPanel(
            budgetSection,
            mi.Q<VisualElement>("BudgetContainer"));

        // ── Nowy panel Centrum Ekspedycyjnego ─────────────────────────────────
        expeditionPanel = new ExpeditionCenterInspectorPanel(expeditionCenterSection);
    }

    // =========================================================================
    // Publiczne API
    // =========================================================================

    public void ShowInspector(BuildingEntity entity)
    {
        // --- NOWE: PRZECHWYCENIE KLIKNIĘCIA W KUŹNIĘ ---
        if (entity is RuneForgeEntity forge )
        {
            Hide(); // Zamknij małego inspektora jeśli był otwarty
            if (UIRuneForgeMenu.Instance != null)
            {
                UIRuneForgeMenu.Instance.OpenMenu(forge);
            }
            return;
        }
        // -----------------------------------------------

        currentTarget = entity;
        totalContainer.style.display = DisplayStyle.Flex;
        upgradePanel.CloseDetails();
        RefreshContent();
    }

    public void Hide()
    {
        totalContainer.style.display = DisplayStyle.None;
        currentTarget = null;
        InteractionManager.Instance?.DeselectAll();
    }

    public void RefreshContent()
    {
        if (currentTarget == null) return;

        RefreshHeader();
        HideAllSections();
        RouteToPanel();
    }

    // =========================================================================
    // Routing
    // =========================================================================

    private void RouteToPanel()
    {
        switch (currentTarget)
        {
            // ── Centrum Ekspedycyjne ─────────────────────────────────────────
            case ExpeditionCenterEntity expCenter:
                expeditionCenterSection.style.display = DisplayStyle.Flex;
                expeditionPanel.Refresh(expCenter);

                // Ulepszenia (jeśli zdefiniowane w BuildingData)
                if (expCenter.data.tier1Upgrades != null && expCenter.data.tier1Upgrades.Count > 0)
                {
                    upgradeSection.style.display = DisplayStyle.Flex;
                    upgradePanel.Refresh(expCenter);
                }
                break;

            // ── Latarnia ─────────────────────────────────────────────────────
            case BeaconEntity beacon:
                beaconPanel.Show();
                beaconPanel.Refresh(beacon);
                break;

            // ── Dom mieszkalny ───────────────────────────────────────────────
            case HousingEntity house:
                housingPanel.Show();
                housingPanel.Refresh(house);
                upgradeSection.style.display = DisplayStyle.Flex;
                upgradePanel.Refresh(house);
                break;

            // ── Wieża ─────────────────────────────────────────────────────────
            case TowerEntity tower:
                workerSection.style.display      = DisplayStyle.Flex;
                combatStatsContainer.style.display = DisplayStyle.Flex;
                workerPanel.Refresh(tower);
                towerPanel.Refresh(tower);
                break;
            
            // ── POSTERUNEK ─────────────────────────────────────────────────────
            case OutpostEntity outpost:
                expeditionCenterSection.style.display = DisplayStyle.Flex;
                
                // Używamy tych samych elementów co Ekspedycja, by oszczędzić czas
                var lblStatus = expeditionCenterSection.Q<Label>("Exp_ElfStatusLabel");
                var lblMission = expeditionCenterSection.Q<Label>("Exp_MissionStatusLabel");
                var dot = expeditionCenterSection.Q<VisualElement>("Exp_ElfStatusDot");
                var header = expeditionCenterSection.Q<Label>("expedition-section-header"); // Pobierz nagłówek jeśli istnieje
                
                if (header != null) header.text = "POSTERUNEK GRANICZNY";

                if (outpost.TransformAvailable)
                {
                    dot.style.backgroundColor = Color.green;
                    lblStatus.text = "Posterunek gotowy do przebudowy!";
                    lblStatus.style.color = Color.green;
                    lblMission.text = "Użyj menu kontekstowego by wybrać nowy budynek mieszkalny.";
                    
                    // Pokazujemy menu wyboru
                    MapExpansionManager.Instance.OnOutpostReadyToTransform(outpost, outpost.ActiveOptions);
                }
                else
                {
                    dot.style.backgroundColor = Color.yellow;
                    lblStatus.text = $"Zasiedlanie... (pozostało dni: {outpost.DaysRemaining})";
                    lblStatus.style.color = Color.yellow;
                    lblMission.text = "Zabezpiecza teren, wkrótce będzie można tu zbudować wioskę.";
                }
                break;
            // ── Budynek ekonomiczny (domyślny) ────────────────────────────────
            default:
                workerSection.style.display  = DisplayStyle.Flex;
                statsContainer.style.display = DisplayStyle.Flex;
                upgradeSection.style.display = DisplayStyle.Flex;
                budgetSection.style.display  = DisplayStyle.Flex;

                workerPanel.Refresh(currentTarget);
                upgradePanel.Refresh(currentTarget);
                budgetPanel.Refresh(currentTarget);

                if (lblProd   != null) lblProd.text   = "Produkcja (zmiana): "  + FormatResourcesPerShift(currentTarget.GetCurrentProduction());
                if (lblUpkeep != null) lblUpkeep.text = "Utrzymanie (zmiana): " + FormatResourcesPerShift(currentTarget.GetCurrentUpkeep());
                break;
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private void RefreshHeader()
    {
        bool isSpecial = currentTarget is TowerEntity
                      || currentTarget is BeaconEntity
                      || currentTarget is ExpeditionCenterEntity;

        string tierInfo = isSpecial ? "" : $"(Tier {currentTarget.currentTier + 1})";
        if (lblName != null) lblName.text = $"{currentTarget.data.buildingName} {tierInfo}";

        if (btnDestroy != null)
        {
            bool isUndestroyable = currentTarget.data.type == BuildingType.Unique
                                || currentTarget is BeaconEntity;
            btnDestroy.style.display = isUndestroyable ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }

    private void HideAllSections()
    {
        void HideEl(VisualElement el) { if (el != null) el.style.display = DisplayStyle.None; }

        HideEl(statsContainer);
        HideEl(combatStatsContainer);
        HideEl(upgradeSection);
        HideEl(beaconControlSection);
        HideEl(housingSection);
        HideEl(workerSection);
        HideEl(budgetSection);
        HideEl(expeditionCenterSection);
    }

    private void OnDestroyClicked()
    {
        if (currentTarget == null) return;
        currentTarget.Demolish();
        Hide();
    }

    private string FormatResourcesPerShift(System.Collections.Generic.Dictionary<ResourceType, float> dict)
    {
        if (dict.Count == 0) return "-";
        var sb = new StringBuilder();
        foreach (var kvp in dict) sb.Append($"{kvp.Value:F2} {kvp.Key}\n");
        return sb.ToString();
    }
}