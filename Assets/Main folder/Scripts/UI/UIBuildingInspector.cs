using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Text;

public class UIBuildingInspector : MonoBehaviour
{
    public static UIBuildingInspector Instance { get; private set; }

    [Header("Referencje")]
    public UIDocument uiDocument;

    // Elementy G³ówne
    private VisualElement totalContainer, mainInspector, upgradeDetailsPanel;
    private Label lblName, lblWorkerCounts;
    private Button btnClose, btnDestroy;

    // Elementy Ekonomiczne
    private VisualElement statsContainer, upgradeSection;
    private Label lblProd, lblUpkeep;
    private VisualElement upgradesList;

    // Elementy Bojowe (NOWE)
    private VisualElement combatStatsContainer;
    private Label lblDamage, lblRange, lblFireRate;

    // Elementy Pracowników
    private VisualElement shiftsContainer;
    private Button btnAddH, btnRemH, btnAddE, btnRemE, btnAddD, btnRemD;

    // Elementy Szczegó³ów Ulepszenia
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

        lblName = mainInspector.Q<Label>("Lbl_Name");
        lblWorkerCounts = mainInspector.Q<Label>("Lbl_WorkerCounts");
        shiftsContainer = mainInspector.Q<VisualElement>("ShiftsContainer");

        btnClose = mainInspector.Q<Button>("Btn_Close");
        btnDestroy = mainInspector.Q<Button>("Btn_Destroy");

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

        // Detale
        detTitle = upgradeDetailsPanel.Q<Label>("Det_Title");
        detDesc = upgradeDetailsPanel.Q<Label>("Det_Desc");
        detEffects = upgradeDetailsPanel.Q<Label>("Det_Effects");
        detCost = upgradeDetailsPanel.Q<Label>("Det_Cost");
        btnConfirmBuy = upgradeDetailsPanel.Q<Button>("Btn_ConfirmBuy");
        btnCancelBuy = upgradeDetailsPanel.Q<Button>("Btn_CancelBuy");

        // Przyciski pracowników
        btnAddH = mainInspector.Q<Button>("Btn_AddHuman"); btnRemH = mainInspector.Q<Button>("Btn_RemHuman");
        btnAddE = mainInspector.Q<Button>("Btn_AddElf"); btnRemE = mainInspector.Q<Button>("Btn_RemElf");
        btnAddD = mainInspector.Q<Button>("Btn_AddDwarf"); btnRemD = mainInspector.Q<Button>("Btn_RemDwarf");

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
    }

    public void RefreshContent()
    {
        if (currentTarget == null) return;

        // 1. Nazwa
        string tierInfo = (currentTarget is TowerEntity) ? "" : $"(Tier {currentTarget.currentTier + 1})";
        lblName.text = $"{currentTarget.data.buildingName} {tierInfo}";

        // 2. Rozró¿nienie UI (Wie¿a vs Ekonomia)
        if (currentTarget is TowerEntity tower)
        {
            // TRYB WIE¯Y
            statsContainer.style.display = DisplayStyle.None;
            upgradeSection.style.display = DisplayStyle.None;
            combatStatsContainer.style.display = DisplayStyle.Flex;

            UpdateCombatStatsUI(tower);
        }
        else
        {
            // TRYB EKONOMII
            statsContainer.style.display = DisplayStyle.Flex;
            upgradeSection.style.display = DisplayStyle.Flex;
            combatStatsContainer.style.display = DisplayStyle.None;

            lblProd.text = "Produkcja: " + FormatResourcesWithHourly(currentTarget.GetCurrentProduction(), currentTarget.shiftLength);
            lblUpkeep.text = "Utrzymanie: " + FormatResourcesWithHourly(currentTarget.GetCurrentUpkeep(), currentTarget.shiftLength);

            GenerateUpgradesList();
        }

        // 3. Pracownicy (Wspólne)
        int h = currentTarget.GetWorkerCount(Race.Humans);
        int e = currentTarget.GetWorkerCount(Race.Elves);
        int d = currentTarget.GetWorkerCount(Race.Dwarves);
        lblWorkerCounts.text = $"H: {h} | E: {e} | D: {d}";

        GenerateShiftSlots();
    }

    // --- LOGIKA WIE¯Y ---
    void UpdateCombatStatsUI(TowerEntity tower)
    {
        if (tower.controller == null) return;

        float curDmg = tower.controller.GetCurrentDamage();
        float baseDmg = tower.controller.GetBaseDamage();
        lblDamage.text = FormatStat("Obra¿enia", curDmg, baseDmg);

        float curRange = tower.controller.GetCurrentRange();
        float baseRange = tower.controller.GetBaseRange();
        lblRange.text = FormatStat("Zasiêg", curRange, baseRange);

        float curRate = tower.controller.GetCurrentFireRate();
        float baseRate = tower.controller.GetBaseFireRate();
        lblFireRate.text = FormatStat("Szybkoœæ", curRate, baseRate, "/s");
    }

    string FormatStat(string name, float current, float baseVal, string suffix = "")
    {
        // Jeœli wartoœci s¹ ró¿ne (np. buff od elfa lub kara za brak ludzi), poka¿ bazê w nawiasie
        if (Mathf.Abs(current - baseVal) > 0.01f)
        {
            // Kolorowanie: Jeœli current > base -> Zielony, jeœli mniej -> Czerwony
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
            // Reset klas
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

                // TOOLTIP DLA PRACOWNIKA W WIE¯Y
                if (currentTarget is TowerEntity)
                {
                    string bonus = "";
                    if (worker.race == Race.Elves) bonus = "+10% Zasiêg";
                    else if (worker.race == Race.Dwarves) bonus = "+10% Obra¿enia";
                    else if (worker.race == Race.Humans) bonus = "+10% Szybkoœæ";

                    slot.tooltip = $"{worker.race} ({worker.workState})\nEfekt: {bonus}";
                }
                else
                {
                    slot.tooltip = $"{worker.race} ({worker.workState})";
                }
            }
            else
            {
                slot.AddToClassList("slot-empty");
                slot.tooltip = "Pusty slot";
            }
        }
    }

    // --- ULEPSZENIA & RESZTA (Bez zmian logicznych) ---
    // (Skopiuj resztê metod z poprzedniego pliku: GenerateUpgradesList, ShowUpgradeDetails, OnWorkerAction itp.)

    // SKRÓT POZOSTA£YCH METOD (Wklej tu pe³ne wersje z poprzedniej odpowiedzi)
    void GenerateUpgradesList() { upgradesList.Clear(); var upgrades = currentTarget.GetAvailableUpgrades(); if (upgrades.Count == 0) { upgradesList.Add(new Label("Maksymalny poziom.")); return; } foreach (var up in upgrades) { Button btn = new Button(); btn.text = up.upgradeName; btn.AddToClassList("upgrade-btn"); btn.clicked += () => ShowUpgradeDetails(up); upgradesList.Add(btn); } }
    void ShowUpgradeDetails(BuildingUpgradeSO upgrade) { selectedUpgrade = upgrade; upgradeDetailsPanel.style.display = DisplayStyle.Flex; detTitle.text = upgrade.upgradeName; detDesc.text = upgrade.description; StringBuilder costSb = new StringBuilder("Koszt: "); foreach (var c in upgrade.cost) costSb.Append($"{c.amount} {c.type}, "); detCost.text = costSb.ToString().TrimEnd(',', ' '); StringBuilder effSb = new StringBuilder(); if (upgrade.productionBonus != null) foreach (var p in upgrade.productionBonus) effSb.Append($"+{p.amount} {p.type} Prod, "); if (upgrade.upkeepIncrease != null) foreach (var u in upgrade.upkeepIncrease) effSb.Append($"+{u.amount} {u.type} Utrzymania, "); detEffects.text = effSb.Length > 0 ? effSb.ToString().TrimEnd(',', ' ') : "Brak zmiany statystyk"; }
    void CloseUpgradeDetails() { selectedUpgrade = null; upgradeDetailsPanel.style.display = DisplayStyle.None; }
    void OnBuyConfirm() { if (selectedUpgrade == null || currentTarget == null) return; Dictionary<ResourceType, int> costs = new Dictionary<ResourceType, int>(); foreach (var c in selectedUpgrade.cost) costs.Add(c.type, c.amount); if (ResourceManager.Instance.SpendResources(costs)) { currentTarget.ApplyUpgrade(selectedUpgrade); RefreshContent(); CloseUpgradeDetails(); } else { Debug.Log("Nie staæ Ciê!"); } }
    void OnWorkerAction(Race race, bool add) { if (currentTarget == null) return; bool changed = false; if (add) changed = currentTarget.TryAddWorker(race); else { currentTarget.RemoveWorker(race); changed = true; } if (changed) RefreshContent(); }
    void OnDestroyClicked() { if (currentTarget != null) { currentTarget.Demolish(); Hide(); } }
    string FormatResourcesWithHourly(Dictionary<ResourceType, int> dict, int shiftLength) { if (dict.Count == 0) return "-"; StringBuilder sb = new StringBuilder(); foreach (var kvp in dict) { float hourly = (float)kvp.Value / shiftLength; sb.Append($"{kvp.Value} {kvp.Key} ({hourly:F1}/h)\n"); } return sb.ToString(); }
}