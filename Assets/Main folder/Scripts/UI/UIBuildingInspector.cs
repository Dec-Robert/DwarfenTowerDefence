using UnityEngine;
using UnityEngine.UIElements;
using System.Text;

/// <summary>
/// Cienki koordynator inspektora budynku.
/// Jedyna odpowiedzialność: wybrać właściwe panele dla danego typu budynku
/// i przekazać im aktualny cel do odświeżenia.
///
/// Cała logika wizualna mieszka w dedykowanych panelach:
///   BeaconInspectorPanel, HousingInspectorPanel, TowerInspectorPanel,
///   WorkerInspectorPanel, UpgradeInspectorPanel, BudgetInspectorPanel.
/// </summary>
public class UIBuildingInspector : MonoBehaviour
{
    public static UIBuildingInspector Instance { get; private set; }

    [Header("Referencje")]
    public UIDocument uiDocument;

    // -------------------------------------------------------------------------
    // Elementy UI (pobierane w OnEnable)
    // -------------------------------------------------------------------------

    private VisualElement totalContainer;
    private VisualElement mainInspector;
    private VisualElement upgradeDetailsPanel;

    private Label lblName;
    private Button btnClose;
    private Button btnDestroy;

    // Sekcje – widoczność przełączana przez koordynatora
    private VisualElement workerSection;
    private VisualElement statsContainer;
    private VisualElement combatStatsContainer;
    private VisualElement beaconControlSection;
    private VisualElement housingSection;
    private VisualElement upgradeSection;
    private VisualElement budgetSection;

    private Label lblProd;
    private Label lblUpkeep;

    // -------------------------------------------------------------------------
    // Panele logiczne
    // -------------------------------------------------------------------------

    private BeaconInspectorPanel  beaconPanel;
    private HousingInspectorPanel housingPanel;
    private TowerInspectorPanel   towerPanel;
    private WorkerInspectorPanel  workerPanel;
    private UpgradeInspectorPanel upgradePanel;
    private BudgetInspectorPanel  budgetPanel;

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

        // Kontenery
        totalContainer       = uiRoot.Q<VisualElement>("TotalContainer");
        mainInspector        = uiRoot.Q<VisualElement>("InspectorRoot");
        upgradeDetailsPanel  = uiRoot.Q<VisualElement>("UpgradeDetailsPanel");

        // Główne elementy
        lblName    = mainInspector.Q<Label>("Lbl_Name");
        btnClose   = mainInspector.Q<Button>("Btn_Close");
        btnDestroy = mainInspector.Q<Button>("Btn_Destroy");

        // Sekcje
        workerSection        = mainInspector.Q<VisualElement>("WorkerSection");
        statsContainer       = mainInspector.Q<VisualElement>("StatsContainer");
        combatStatsContainer = mainInspector.Q<VisualElement>("CombatStatsContainer");
        beaconControlSection = mainInspector.Q<VisualElement>("BeaconControlSection");
        housingSection       = mainInspector.Q<VisualElement>("HousingSection");
        upgradeSection       = mainInspector.Q<VisualElement>("UpgradeSection");
        budgetSection        = mainInspector.Q<VisualElement>("BudgetSection");

        lblProd   = mainInspector.Q<Label>("Lbl_Production");
        lblUpkeep = mainInspector.Q<Label>("Lbl_Upkeep");

        // Przyciski globalne
        btnClose.clicked   += Hide;
        btnDestroy.clicked += OnDestroyClicked;

        // Tworzenie paneli
        CreatePanels(mainInspector);
    }

    private void Update()
    {
        if (totalContainer.style.display == DisplayStyle.Flex && currentTarget != null)
            workerPanel?.UpdateSlotColors();
    }

    // =========================================================================
    // Tworzenie paneli (okablowanie UI → logika)
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
    }

    // =========================================================================
    // Publiczne API
    // =========================================================================

    public void ShowInspector(BuildingEntity entity)
    {
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
    // Routing – jedyne miejsce gdzie sprawdzamy typ budynku
    // =========================================================================

    private void RouteToPanel()
    {
        switch (currentTarget)
        {
            case BeaconEntity beacon:
                beaconPanel.Show();
                beaconPanel.Refresh(beacon);
                break;

            case HousingEntity house:
                housingPanel.Show();
                housingPanel.Refresh(house);
                upgradeSection.style.display = DisplayStyle.Flex;
                upgradePanel.Refresh(house);
                break;

            case TowerEntity tower:
                workerSection.style.display = DisplayStyle.Flex;
                workerPanel.Refresh(tower);
                combatStatsContainer.style.display = DisplayStyle.Flex;
                towerPanel.Refresh(tower);
                break;

            default: // Budynek ekonomiczny
                workerSection.style.display  = DisplayStyle.Flex;
                statsContainer.style.display = DisplayStyle.Flex;
                upgradeSection.style.display = DisplayStyle.Flex;
                budgetSection.style.display  = DisplayStyle.Flex;

                workerPanel.Refresh(currentTarget);
                upgradePanel.Refresh(currentTarget);
                budgetPanel.Refresh(currentTarget);

                if (lblProd   != null) lblProd.text   = "Produkcja (zmiana): "   + FormatResourcesPerShift(currentTarget.GetCurrentProduction());
                if (lblUpkeep != null) lblUpkeep.text = "Utrzymanie (zmiana): "  + FormatResourcesPerShift(currentTarget.GetCurrentUpkeep());
                break;
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private void RefreshHeader()
    {
        bool isSpecial = currentTarget is TowerEntity || currentTarget is BeaconEntity;
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
        void Hide(VisualElement el) { if (el != null) el.style.display = DisplayStyle.None; }

        Hide(statsContainer);
        Hide(combatStatsContainer);
        Hide(upgradeSection);
        Hide(beaconControlSection);
        Hide(housingSection);
        Hide(workerSection);
        Hide(budgetSection);
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
