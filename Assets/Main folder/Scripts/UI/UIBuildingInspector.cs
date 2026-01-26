using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Text;

public class UIBuildingInspector : MonoBehaviour
{
    public static UIBuildingInspector Instance { get; private set; }

    [Header("Referencje")]
    public UIDocument uiDocument;

    // Kontenery
    private VisualElement totalContainer, mainInspector, upgradeDetailsPanel;

    // G³ówne
    private Label lblName, lblWorkerCounts;
    private Button btnClose, btnDestroy;

    // Sekcje
    private VisualElement workerSection;
    private VisualElement shiftsContainer;
    private VisualElement statsContainer, upgradeSection, upgradesList;
    private Label lblProd, lblUpkeep;
    private VisualElement combatStatsContainer;
    private Label lblDamage, lblRange, lblFireRate;
    private VisualElement beaconControlSection;
    private SliderInt coalSlider;
    private Label lblCoalVal;

    // Przyciski Pracowników
    private Button btnAddH, btnRemH, btnAddE, btnRemE, btnAddD, btnRemD;

    // Detale Ulepszeñ
    private Label detTitle, detDesc, detEffects, detCost;
    private Button btnConfirmBuy, btnCancelBuy;

    private BuildingEntity currentTarget;
    private BuildingUpgradeSO selectedUpgrade;
    private List<VisualElement> visualSlots = new List<VisualElement>();

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        var uiRoot = uiDocument.rootVisualElement;

        totalContainer = uiRoot.Q<VisualElement>("TotalContainer");
        mainInspector = uiRoot.Q<VisualElement>("InspectorRoot");
        upgradeDetailsPanel = uiRoot.Q<VisualElement>("UpgradeDetailsPanel");

        // G³ówne
        lblName = mainInspector.Q<Label>("Lbl_Name");
        btnClose = mainInspector.Q<Button>("Btn_Close");
        btnDestroy = mainInspector.Q<Button>("Btn_Destroy");

        // Pracownicy
        workerSection = mainInspector.Q<VisualElement>("WorkerSection");
        shiftsContainer = mainInspector.Q<VisualElement>("ShiftsContainer");
        lblWorkerCounts = mainInspector.Q<Label>("Lbl_WorkerCounts");

        btnAddH = mainInspector.Q<Button>("Btn_AddHuman"); btnRemH = mainInspector.Q<Button>("Btn_RemHuman");
        btnAddE = mainInspector.Q<Button>("Btn_AddElf"); btnRemE = mainInspector.Q<Button>("Btn_RemElf");
        btnAddD = mainInspector.Q<Button>("Btn_AddDwarf"); btnRemD = mainInspector.Q<Button>("Btn_RemDwarf");

        // Ekonomia
        statsContainer = mainInspector.Q<VisualElement>("StatsContainer");
        lblProd = mainInspector.Q<Label>("Lbl_Production");
        lblUpkeep = mainInspector.Q<Label>("Lbl_Upkeep");
        upgradeSection = mainInspector.Q<VisualElement>("UpgradeSection");
        upgradesList = mainInspector.Q<VisualElement>("UpgradesList");

        // Walka
        combatStatsContainer = mainInspector.Q<VisualElement>("CombatStatsContainer");
        lblDamage = mainInspector.Q<Label>("Lbl_Damage");
        lblRange = mainInspector.Q<Label>("Lbl_Range");
        lblFireRate = mainInspector.Q<Label>("Lbl_FireRate");

        // Beacon
        beaconControlSection = mainInspector.Q<VisualElement>("BeaconControlSection");
        coalSlider = mainInspector.Q<SliderInt>("Slider_CoalInput");
        lblCoalVal = mainInspector.Q<Label>("Lbl_CoalInputVal");

        if (coalSlider != null)
            coalSlider.RegisterValueChangedCallback(evt => OnCoalSliderChanged(evt.newValue));

        // Detale
        detTitle = upgradeDetailsPanel.Q<Label>("Det_Title");
        detDesc = upgradeDetailsPanel.Q<Label>("Det_Desc");
        detEffects = upgradeDetailsPanel.Q<Label>("Det_Effects");
        detCost = upgradeDetailsPanel.Q<Label>("Det_Cost");
        btnConfirmBuy = upgradeDetailsPanel.Q<Button>("Btn_ConfirmBuy");
        btnCancelBuy = upgradeDetailsPanel.Q<Button>("Btn_CancelBuy");

        // Eventy
        btnClose.clicked += Hide;
        btnDestroy.clicked += OnDestroyClicked;
        btnConfirmBuy.clicked += OnBuyConfirm;
        btnCancelBuy.clicked += CloseUpgradeDetails;

        btnAddH.clicked += () => OnWorkerAction(Race.Humans, true);
        btnRemH.clicked += () => OnWorkerAction(Race.Humans, false);
        btnAddE.clicked += () => OnWorkerAction(Race.Elves, true);
        btnRemE.clicked += () => OnWorkerAction(Race.Elves, false);
        btnAddD.clicked += () => OnWorkerAction(Race.Dwarves, true);
        btnRemD.clicked += () => OnWorkerAction(Race.Dwarves, false);
    }

    private void Update()
    {
        if (totalContainer.style.display == DisplayStyle.Flex && currentTarget != null)
        {
            UpdateSlotColors();
        }
    }

    public void ShowInspector(BuildingEntity entity)
    {
        currentTarget = entity;
        totalContainer.style.display = DisplayStyle.Flex;
        CloseUpgradeDetails();
        RefreshContent();
    }

    public void Hide()
    {
        totalContainer.style.display = DisplayStyle.None;
        currentTarget = null;

        // Powiadom InteractionManager, ¿eby schowa³ zasiêg
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.DeselectAll();
        }
    }

    public void RefreshContent()
    {
        if (currentTarget == null) return;

        string tierInfo = (currentTarget is TowerEntity || currentTarget is BeaconEntity) ? "" : $"(Tier {currentTarget.currentTier + 1})";
        lblName.text = $"{currentTarget.data.buildingName} {tierInfo}";

        // --- OBS£UGA PRZYCISKU NISZCZENIA ---
        if (btnDestroy != null)
        {
            // Ukryj przycisk, jeœli to budynek unikalny (Unique) lub Beacon
            if (currentTarget.data.type == BuildingType.Unique || currentTarget.data.type == BuildingType.Unique || currentTarget is BeaconEntity)
            {
                btnDestroy.style.display = DisplayStyle.None;
            }
            else
            {
                btnDestroy.style.display = DisplayStyle.Flex;
            }
        }
        // ------------------------------------

        // Reset widocznoœci sekcji
        if (statsContainer != null) statsContainer.style.display = DisplayStyle.None;
        if (combatStatsContainer != null) combatStatsContainer.style.display = DisplayStyle.None;
        if (upgradeSection != null) upgradeSection.style.display = DisplayStyle.None;
        if (beaconControlSection != null) beaconControlSection.style.display = DisplayStyle.None;
        if (workerSection != null) workerSection.style.display = DisplayStyle.None;

        // --- PRZE£¥CZANIE WIDOKÓW ---

        // A. BEACON
        if (currentTarget is BeaconEntity beacon)
        {
            if (beaconControlSection != null)
            {
                beaconControlSection.style.display = DisplayStyle.Flex;

                if (coalSlider != null) coalSlider.SetValueWithoutNotify(beacon.dailyCoalInput);
                if (lblCoalVal != null) lblCoalVal.text = beacon.dailyCoalInput.ToString();
            }
            return;
        }

        // B. RESZTA (Poka¿ pracowników)
        if (workerSection != null) workerSection.style.display = DisplayStyle.Flex;

        if (currentTarget is TowerEntity tower)
        {
            // C. WIE¯A
            if (combatStatsContainer != null)
            {
                combatStatsContainer.style.display = DisplayStyle.Flex;
                UpdateCombatStatsUI(tower);
            }
        }
        else
        {
            // D. BUDYNEK EKONOMICZNY
            if (statsContainer != null) statsContainer.style.display = DisplayStyle.Flex;
            if (upgradeSection != null) upgradeSection.style.display = DisplayStyle.Flex;

            lblProd.text = "Produkcja: " + FormatResourcesWithHourly(currentTarget.GetCurrentProduction(), currentTarget.shiftLength);
            lblUpkeep.text = "Utrzymanie: " + FormatResourcesWithHourly(currentTarget.GetCurrentUpkeep(), currentTarget.shiftLength);

            GenerateUpgradesList();
        }

        // Pracownicy
        int h = currentTarget.GetWorkerCount(Race.Humans);
        int e = currentTarget.GetWorkerCount(Race.Elves);
        int d = currentTarget.GetWorkerCount(Race.Dwarves);
        lblWorkerCounts.text = $"H: {h} | E: {e} | D: {d}";

        GenerateShiftSlots();
    }

    // --- BEACON ---
    void OnCoalSliderChanged(int newValue)
    {
        if (currentTarget is BeaconEntity beacon)
        {
            beacon.dailyCoalInput = newValue;
            if (lblCoalVal != null) lblCoalVal.text = newValue.ToString();
        }
    }

    // --- WIE¯A ---
    void UpdateCombatStatsUI(TowerEntity tower)
    {
        if (tower.controller == null) return;
        lblDamage.text = FormatStat("Obra¿enia", tower.controller.GetCurrentDamage(), tower.controller.GetBaseDamage());
        lblRange.text = FormatStat("Zasiêg", tower.controller.GetCurrentRange(), tower.controller.GetBaseRange());
        lblFireRate.text = FormatStat("Szybkoœæ", tower.controller.GetCurrentFireRate(), tower.controller.GetBaseFireRate(), "/s");
    }

    string FormatStat(string name, float current, float baseVal, string suffix = "")
    {
        if (Mathf.Abs(current - baseVal) > 0.01f)
        {
            string color = current >= baseVal ? "#88FF88" : "#FF8888";
            return $"{name}: <color={color}>{current:F1}{suffix}</color> <color=#AAAAAA>({baseVal:F1})</color>";
        }
        return $"{name}: {current:F1}{suffix}";
    }

    // --- PRACOWNICY ---
    void GenerateShiftSlots()
    {
        shiftsContainer.Clear();
        visualSlots.Clear();
        int shifts = currentTarget.getMaxShifts();
        int slotsPerShift = currentTarget.getMaxWorkersPerShift();

        for (int i = 0; i < shifts; i++)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("shift-row");
            Label label = new Label($"Zmiana {i + 1}");
            label.AddToClassList("shift-label");
            row.Add(label);

            for (int j = 0; j < slotsPerShift; j++)
            {
                VisualElement slot = new VisualElement();
                slot.AddToClassList("worker-slot");
                visualSlots.Add(slot);
                row.Add(slot);
            }
            shiftsContainer.Add(row);
        }
        UpdateSlotColors();
    }

    void UpdateSlotColors()
    {
        if (currentTarget == null) return;
        List<Citizen> workers = currentTarget.GetAssignedCitizens();

        for (int i = 0; i < visualSlots.Count; i++)
        {
            VisualElement slot = visualSlots[i];
            slot.RemoveFromClassList("slot-empty"); slot.RemoveFromClassList("slot-assigned");
            slot.RemoveFromClassList("slot-working"); slot.RemoveFromClassList("slot-exhausted");

            if (i < workers.Count)
            {
                Citizen worker = workers[i];
                switch (worker.workState)
                {
                    case WorkState.Assigned: slot.AddToClassList("slot-assigned"); break;
                    case WorkState.Working: slot.AddToClassList("slot-working"); break;
                    case WorkState.Exhausted: slot.AddToClassList("slot-exhausted"); break;
                    default: slot.AddToClassList("slot-empty"); break;
                }

                if (currentTarget is TowerEntity)
                {
                    string bonus = "";
                    if (worker.race == Race.Elves) bonus = "+10% Zasiêg";
                    else if (worker.race == Race.Dwarves) bonus = "+10% Obra¿enia";
                    else if (worker.race == Race.Humans) bonus = "+10% Szybkoœæ";
                    slot.tooltip = $"{worker.race} ({worker.workState})\nEfekt: {bonus}";
                }
                else slot.tooltip = $"{worker.race} ({worker.workState})";
            }
            else
            {
                slot.AddToClassList("slot-empty");
                slot.tooltip = "Pusty slot";
            }
        }
    }

    void OnWorkerAction(Race race, bool add)
    {
        if (currentTarget == null) return;
        bool changed = false;
        if (add) changed = currentTarget.TryAddWorker(race);
        else { currentTarget.RemoveWorker(race); changed = true; }
        if (changed) RefreshContent();
    }

    // --- ULEPSZENIA ---
    void GenerateUpgradesList()
    {
        upgradesList.Clear();
        var upgrades = currentTarget.GetAvailableUpgrades();

        if (upgrades.Count == 0)
        {
            upgradesList.Add(new Label("Maksymalny poziom."));
            return;
        }

        foreach (var up in upgrades)
        {
            Button btn = new Button();
            btn.text = up.upgradeName;
            btn.AddToClassList("upgrade-btn");
            btn.clicked += () => ShowUpgradeDetails(up);
            upgradesList.Add(btn);
        }
    }

    void ShowUpgradeDetails(BuildingUpgradeSO upgrade)
    {
        selectedUpgrade = upgrade;
        upgradeDetailsPanel.style.display = DisplayStyle.Flex;
        detTitle.text = upgrade.upgradeName;
        detDesc.text = upgrade.description;

        StringBuilder costSb = new StringBuilder("Koszt: ");
        foreach (var c in upgrade.cost) costSb.Append($"{c.amount} {c.type}, ");
        detCost.text = costSb.ToString().TrimEnd(',', ' ');

        StringBuilder effSb = new StringBuilder();
        if (upgrade.productionBonus != null) foreach (var p in upgrade.productionBonus) effSb.Append($"+{p.amount} {p.type} Prod, ");
        if (upgrade.upkeepIncrease != null) foreach (var u in upgrade.upkeepIncrease) effSb.Append($"+{u.amount} {u.type} Utrzymania, ");
        detEffects.text = effSb.Length > 0 ? effSb.ToString().TrimEnd(',', ' ') : "Brak zmiany statystyk";
    }

    void CloseUpgradeDetails() { selectedUpgrade = null; upgradeDetailsPanel.style.display = DisplayStyle.None; }

    void OnBuyConfirm()
    {
        if (selectedUpgrade == null || currentTarget == null) return;
        Dictionary<ResourceType, int> costs = new Dictionary<ResourceType, int>();
        foreach (var c in selectedUpgrade.cost) costs.Add(c.type, c.amount);

        if (ResourceManager.Instance.SpendResources(costs))
        {
            currentTarget.ApplyUpgrade(selectedUpgrade);
            RefreshContent(); CloseUpgradeDetails();
        }
        else Debug.Log("Nie staæ Ciê!");
    }

    void OnDestroyClicked()
    {
        if (currentTarget != null) { currentTarget.Demolish(); Hide(); }
    }

    string FormatResourcesWithHourly(Dictionary<ResourceType, int> dict, int shiftLength)
    {
        if (dict.Count == 0) return "-";
        StringBuilder sb = new StringBuilder();
        foreach (var kvp in dict)
        {
            float hourly = (float)kvp.Value / shiftLength;
            sb.Append($"{kvp.Value} {kvp.Key} ({hourly:F1}/h)\n");
        }
        return sb.ToString();
    }
}