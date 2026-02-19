using System.Collections.Generic;
using UnityEngine.UIElements;

/// <summary>
/// Panel inspektora obsługujący sloty pracowników i przyciski dodawania/usuwania.
/// Wspólny dla wież i budynków ekonomicznych.
/// </summary>
public class WorkerInspectorPanel : BuildingInspectorPanel
{
    private readonly Label lblWorkerCounts;
    private readonly VisualElement shiftsContainer;

    private readonly Button btnAddH, btnRemH;
    private readonly Button btnAddE, btnRemE;
    private readonly Button btnAddD, btnRemD;

    private readonly List<VisualElement> visualSlots = new List<VisualElement>();

    // Callback do koordynatora (żeby odświeżył cały inspektor po zmianie pracownika)
    private readonly System.Action onWorkerChanged;

    private BuildingEntity currentTarget;

    public WorkerInspectorPanel(
        VisualElement root,
        Label lblWorkerCounts,
        VisualElement shiftsContainer,
        Button btnAddH, Button btnRemH,
        Button btnAddE, Button btnRemE,
        Button btnAddD, Button btnRemD,
        System.Action onWorkerChanged)
        : base(root)
    {
        this.lblWorkerCounts  = lblWorkerCounts;
        this.shiftsContainer  = shiftsContainer;
        this.btnAddH = btnAddH; this.btnRemH = btnRemH;
        this.btnAddE = btnAddE; this.btnRemE = btnRemE;
        this.btnAddD = btnAddD; this.btnRemD = btnRemD;
        this.onWorkerChanged  = onWorkerChanged;

        RegisterButtons();
    }

    public override void Refresh(BuildingEntity target)
    {
        currentTarget = target;

        if (lblWorkerCounts != null)
        {
            int h = target.GetWorkerCount(Race.Humans);
            int e = target.GetWorkerCount(Race.Elves);
            int d = target.GetWorkerCount(Race.Dwarves);
            lblWorkerCounts.text = $"H: {h} | E: {e} | D: {d}";
        }

        GenerateShiftSlots(target);
    }

    /// <summary>Aktualizuje tylko kolory slotów – wołane co klatkę przez Update inspektora.</summary>
    public void UpdateSlotColors()
    {
        if (currentTarget == null) return;

        List<Citizen> workers = currentTarget.GetAssignedCitizens();
        bool isTower = currentTarget is TowerEntity;

        for (int i = 0; i < visualSlots.Count; i++)
        {
            VisualElement slot = visualSlots[i];
            slot.RemoveFromClassList("slot-empty");
            slot.RemoveFromClassList("slot-assigned");
            slot.RemoveFromClassList("slot-working");
            slot.RemoveFromClassList("slot-exhausted");

            if (i < workers.Count)
            {
                Citizen worker = workers[i];
                slot.AddToClassList(worker.workState switch
                {
                    WorkState.Assigned  => "slot-assigned",
                    WorkState.Working   => "slot-working",
                    WorkState.Exhausted => "slot-exhausted",
                    _                   => "slot-empty"
                });

                slot.tooltip = isTower
                    ? $"{worker.race} ({worker.workState})\nEfekt: {GetTowerBonus(worker.race)}"
                    : $"{worker.race} ({worker.workState})";
            }
            else
            {
                slot.AddToClassList("slot-empty");
                slot.tooltip = "Pusty slot";
            }
        }
    }

    // =========================================================================
    // Prywatne
    // =========================================================================

    private void RegisterButtons()
    {
        btnAddH?.RegisterCallback<ClickEvent>(_ => OnWorkerAction(Race.Humans,  add: true));
        btnRemH?.RegisterCallback<ClickEvent>(_ => OnWorkerAction(Race.Humans,  add: false));
        btnAddE?.RegisterCallback<ClickEvent>(_ => OnWorkerAction(Race.Elves,   add: true));
        btnRemE?.RegisterCallback<ClickEvent>(_ => OnWorkerAction(Race.Elves,   add: false));
        btnAddD?.RegisterCallback<ClickEvent>(_ => OnWorkerAction(Race.Dwarves, add: true));
        btnRemD?.RegisterCallback<ClickEvent>(_ => OnWorkerAction(Race.Dwarves, add: false));
    }

    private void OnWorkerAction(Race race, bool add)
    {
        if (currentTarget == null) return;
        if (add) currentTarget.TryAddWorker(race);
        else     currentTarget.RemoveWorker(race);
        onWorkerChanged?.Invoke();
    }

    private void GenerateShiftSlots(BuildingEntity target)
    {
        shiftsContainer.Clear();
        visualSlots.Clear();

        int shifts        = target.getMaxShifts();
        int slotsPerShift = target.getMaxWorkersPerShift();

        for (int i = 0; i < shifts; i++)
        {
            var row = new VisualElement();
            row.AddToClassList("shift-row");

            var label = new Label($"Zmiana {i + 1}");
            label.AddToClassList("shift-label");
            row.Add(label);

            for (int j = 0; j < slotsPerShift; j++)
            {
                var slot = new VisualElement();
                slot.AddToClassList("worker-slot");
                visualSlots.Add(slot);
                row.Add(slot);
            }

            shiftsContainer.Add(row);
        }

        UpdateSlotColors();
    }

    private string GetTowerBonus(Race race) => race switch
    {
        Race.Elves   => "+10% Zasięg",
        Race.Dwarves => "+10% Obrażenia",
        Race.Humans  => "+10% Szybkość",
        _            => ""
    };
}
