using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Text;

public class UIBuildingInspector : MonoBehaviour
{
    public static UIBuildingInspector Instance { get; private set; }

    [Header("Referencje")]
    public UIDocument uiDocument;

    // Cache elementów UI
    private VisualElement totalContainer; // NOWY ROOT
    private VisualElement mainInspector;
    private VisualElement upgradeDetailsPanel; // Panel boczny

    private Label lblName, lblProd, lblUpkeep, lblWorkerCounts;
    private VisualElement shiftsContainer, upgradesList;
    private Button btnClose, btnDestroy;
    private Button btnAddH, btnRemH, btnAddE, btnRemE, btnAddD, btnRemD;

    // Cache Szczegó³ów
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

        // Pobieramy nowy g³ówny kontener
        totalContainer = uiRoot.Q<VisualElement>("TotalContainer");
        mainInspector = uiRoot.Q<VisualElement>("InspectorRoot");
        upgradeDetailsPanel = uiRoot.Q<VisualElement>("UpgradeDetailsPanel");

        // Elementy wewn¹trz Main Inspector
        lblName = mainInspector.Q<Label>("Lbl_Name");
        lblProd = mainInspector.Q<Label>("Lbl_Production");
        lblUpkeep = mainInspector.Q<Label>("Lbl_Upkeep");
        lblWorkerCounts = mainInspector.Q<Label>("Lbl_WorkerCounts");

        shiftsContainer = mainInspector.Q<VisualElement>("ShiftsContainer");
        upgradesList = mainInspector.Q<VisualElement>("UpgradesList");

        btnClose = mainInspector.Q<Button>("Btn_Close");
        btnDestroy = mainInspector.Q<Button>("Btn_Destroy");

        btnAddH = mainInspector.Q<Button>("Btn_AddHuman");
        btnRemH = mainInspector.Q<Button>("Btn_RemHuman");
        btnAddE = mainInspector.Q<Button>("Btn_AddElf");
        btnRemE = mainInspector.Q<Button>("Btn_RemElf");
        btnAddD = mainInspector.Q<Button>("Btn_AddDwarf");
        btnRemD = mainInspector.Q<Button>("Btn_RemDwarf");

        // Elementy wewn¹trz Details Panel
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
        // Sprawdzamy czy g³ówny kontener jest widoczny
        if (totalContainer.style.display == DisplayStyle.Flex && currentTarget != null)
        {
            UpdateSlotColors();
        }
    }

    public void ShowInspector(BuildingEntity entity)
    {
        currentTarget = entity;

        // Poka¿ ca³y kontener
        totalContainer.style.display = DisplayStyle.Flex;

        // Ukryj panel boczny na start
        CloseUpgradeDetails();

        RefreshContent();
    }

    public void Hide()
    {
        totalContainer.style.display = DisplayStyle.None;
        currentTarget = null;
    }

    // --- ULEPSZENIA (Zaktualizowane) ---

    void ShowUpgradeDetails(BuildingUpgradeSO upgrade)
    {
        selectedUpgrade = upgrade;

        // Poka¿ panel boczny (po lewej stronie)
        upgradeDetailsPanel.style.display = DisplayStyle.Flex;
        // NIE ukrywamy listy ulepszeñ w g³ównym oknie (upgradesList.style.display = DisplayStyle.Flex)

        detTitle.text = upgrade.upgradeName;
        detDesc.text = upgrade.description;

        StringBuilder costSb = new StringBuilder("Koszt: ");
        foreach (var c in upgrade.cost) costSb.Append($"{c.amount} {c.type}, ");
        detCost.text = costSb.ToString().TrimEnd(',', ' ');

        StringBuilder effSb = new StringBuilder();
        if (upgrade.productionBonus != null)
            foreach (var p in upgrade.productionBonus) effSb.Append($"+{p.amount} {p.type} Prod, ");

        if (upgrade.upkeepIncrease != null)
            foreach (var u in upgrade.upkeepIncrease) effSb.Append($"+{u.amount} {u.type} Utrzymania, ");

        detEffects.text = effSb.Length > 0 ? effSb.ToString().TrimEnd(',', ' ') : "Brak zmiany statystyk";
    }

    void CloseUpgradeDetails()
    {
        selectedUpgrade = null;
        upgradeDetailsPanel.style.display = DisplayStyle.None;
    }

    void OnBuyConfirm()
    {
        if (selectedUpgrade == null || currentTarget == null) return;

        Dictionary<ResourceType, int> costs = new Dictionary<ResourceType, int>();
        foreach (var c in selectedUpgrade.cost) costs.Add(c.type, c.amount);

        if (ResourceManager.Instance.SpendResources(costs))
        {
            currentTarget.ApplyUpgrade(selectedUpgrade);
            RefreshContent();
            CloseUpgradeDetails(); // Zamknij panel boczny po zakupie
        }
        else
        {
            Debug.Log("Nie staæ Ciê!");
        }
    }

    // ... RESZTA METOD BEZ ZMIAN (RefreshContent, GenerateShiftSlots, OnWorkerAction itd.) ...
    // Pamiêtaj, aby skopiowaæ resztê metod z poprzedniego pliku!
    // Poni¿ej kluczowe metody skrótowe dla kompletnoœci:

    public void RefreshContent()
    {
        if (currentTarget == null) return;
        lblName.text = $"{currentTarget.data.buildingName} (Tier {currentTarget.currentTier + 1})";
        lblProd.text = "Produkcja: " + FormatResourcesWithHourly(currentTarget.GetCurrentProduction(), currentTarget.shiftLength);
        lblUpkeep.text = "Utrzymanie: " + FormatResourcesWithHourly(currentTarget.GetCurrentUpkeep(), currentTarget.shiftLength);
        int h = currentTarget.GetWorkerCount(Race.Humans); int e = currentTarget.GetWorkerCount(Race.Elves); int d = currentTarget.GetWorkerCount(Race.Dwarves);
        lblWorkerCounts.text = $"H: {h} | E: {e} | D: {d}";
        GenerateShiftSlots(); GenerateUpgradesList();
    }
    string FormatResourcesWithHourly(Dictionary<ResourceType, int> dict, int shiftLength) { if (dict.Count == 0) return "-"; StringBuilder sb = new StringBuilder(); foreach (var kvp in dict) { float hourly = (float)kvp.Value / shiftLength; sb.Append($"{kvp.Value} {kvp.Key} ({hourly:F1}/h)\n"); } return sb.ToString(); }
    void GenerateShiftSlots() { shiftsContainer.Clear(); visualSlots.Clear(); int shifts = currentTarget.getMaxShifts(); int slotsPerShift = currentTarget.getMaxWorkersPerShift(); for (int i = 0; i < shifts; i++) { VisualElement row = new VisualElement(); row.AddToClassList("shift-row"); Label label = new Label($"Zmiana {i + 1}"); label.AddToClassList("shift-label"); row.Add(label); for (int j = 0; j < slotsPerShift; j++) { VisualElement slot = new VisualElement(); slot.AddToClassList("worker-slot"); visualSlots.Add(slot); row.Add(slot); } shiftsContainer.Add(row); } UpdateSlotColors(); }
    void UpdateSlotColors() { if (currentTarget == null) return; List<Citizen> workers = currentTarget.GetAssignedCitizens(); for (int i = 0; i < visualSlots.Count; i++) { VisualElement slot = visualSlots[i]; slot.RemoveFromClassList("slot-empty"); slot.RemoveFromClassList("slot-assigned"); slot.RemoveFromClassList("slot-working"); slot.RemoveFromClassList("slot-exhausted"); if (i < workers.Count) { Citizen worker = workers[i]; switch (worker.workState) { case WorkState.Assigned: slot.AddToClassList("slot-assigned"); break; case WorkState.Working: slot.AddToClassList("slot-working"); break; case WorkState.Exhausted: slot.AddToClassList("slot-exhausted"); break; default: slot.AddToClassList("slot-empty"); break; } } else { slot.AddToClassList("slot-empty"); } } }
    void GenerateUpgradesList() { upgradesList.Clear(); var upgrades = currentTarget.GetAvailableUpgrades(); if (upgrades.Count == 0) { upgradesList.Add(new Label("Maksymalny poziom osi¹gniêty.")); return; } foreach (var up in upgrades) { Button btn = new Button(); btn.text = up.upgradeName; btn.AddToClassList("upgrade-btn"); btn.clicked += () => ShowUpgradeDetails(up); upgradesList.Add(btn); } }
    void OnWorkerAction(Race race, bool add) { if (currentTarget == null) return; bool changed = false; if (add) changed = currentTarget.TryAddWorker(race); else { currentTarget.RemoveWorker(race); changed = true; } if (changed) RefreshContent(); }
    void OnDestroyClicked() { if (currentTarget != null) { currentTarget.Demolish(); Hide(); } }
}