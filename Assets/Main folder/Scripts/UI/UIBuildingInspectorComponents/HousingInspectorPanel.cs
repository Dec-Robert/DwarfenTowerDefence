using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Panel inspektora dla HousingEntity.
/// Obsługuje pasek wzrostu, kapsułki mieszkańców i statystyki utrzymania.
/// </summary>
public class HousingInspectorPanel : BuildingInspectorPanel
{
    private readonly VisualElement birthBarFill;
    private readonly VisualElement residentCapsules;
    private readonly Label lblBirthDays;
    private readonly Label lblHouseBase;
    private readonly Label lblHouseMaint;
    private readonly Label lblHouseTotal;
    private readonly Label lblGrowthMod;
    private readonly Label lblHouseStatus;
    private readonly Button btnToggleGrowth; 
    private HousingEntity currentHouseTarget;

    public HousingInspectorPanel(
        VisualElement root,
        VisualElement birthBarFill,
        VisualElement residentCapsules,
        Label lblBirthDays,
        Label lblHouseBase,
        Label lblHouseMaint,
        Label lblHouseTotal,
        Label lblGrowthMod,
        Label lblHouseStatus,
        Button btnToggleGrowth) // <--- Dodane w parametrze!
        : base(root)
    {
        this.birthBarFill    = birthBarFill;
        // ... reszta ...
        this.lblHouseStatus  = lblHouseStatus;
        this.btnToggleGrowth = btnToggleGrowth; // <--- Zapisz

        // Podpinamy event
        if (this.btnToggleGrowth != null)
        {
            this.btnToggleGrowth.clicked += OnToggleGrowthClicked;
        }
    }

    public override void Refresh(BuildingEntity target)
    {
        if (target is not HousingEntity house) return;
        currentHouseTarget = house; // Zapisujemy cel dla naszego guzika!

        // 1. Pasek progresu narodzin
        float progress = house.GetGrowthProgress();
        if (birthBarFill != null)
            birthBarFill.style.width = Length.Percent(progress * 100f);

        if (lblBirthDays != null)
            lblBirthDays.text = $"Następne narodziny za: {house.GetDaysRemaining()} dni";

        // 2. Kapsułki mieszkańców
        RefreshResidentCapsules(house);

        // 3. Koszty utrzymania
        RefreshUpkeepLabels(house);

        // 4. Modyfikatory zewnętrzne (np. Beacon w przyszłości)
        float mod = 0f;
        if (lblGrowthMod != null)
            lblGrowthMod.text = $"Modyfikatory: {mod:+0;-0}%";

        if (btnToggleGrowth != null)
        {
            if (house.stopGrowth)
            {
                btnToggleGrowth.text = "WZNÓW WZROST";
                btnToggleGrowth.style.backgroundColor = new Color(0.2f, 0.5f, 0.2f); // Zielony
                btnToggleGrowth.style.color = new Color(0.6f, 1f, 0.6f);
            }
            else
            {
                btnToggleGrowth.text = "WSTRZYMAJ WZROST";
                btnToggleGrowth.style.backgroundColor = new Color(0.4f, 0.1f, 0.1f); // Czerwony
                btnToggleGrowth.style.color = new Color(1f, 0.6f, 0.6f);
            }

            // Opcjonalnie: ukryj przycisk, jeśli dom jest pełny (żeby gracz się nie mylił)
            btnToggleGrowth.style.display = (house.residents.Count >= house.GetEffectiveMaxResidents()) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // Status wzrostu
        if (lblHouseStatus != null)
        {
            string status = house.GetGrowthStatus();
            lblHouseStatus.text = $"Status: {status}";
            lblHouseStatus.style.color = status == "ROSNĄCY" ? Color.green : (status == "WSTRZYMANY" ? Color.yellow : Color.red);
        }
    }

    private void RefreshResidentCapsules(HousingEntity house)
    {
        if (residentCapsules == null) return;

        residentCapsules.Clear();
        
        int max     = house.GetEffectiveMaxResidents(); 
        int current = house.residents.Count;

        for (int i = 0; i < max; i++)
        {
            var cap = new VisualElement();
            cap.AddToClassList("res-capsule");
            cap.AddToClassList(i < current ? "res-capsule-occupied" : "res-capsule-empty");
            residentCapsules.Add(cap);
        }
    }

    private void RefreshUpkeepLabels(HousingEntity house)
    {
        float baseVal = house.housingData.baseDailyUpkeep.Count > 0
            ? house.housingData.baseDailyUpkeep[0].amount
            : 0f;

        float total = house.GetProjectedUpkeep();

        if (lblHouseBase  != null) lblHouseBase.text  = $"Koszt bazy: {baseVal} Food";
        if (lblHouseMaint != null) lblHouseMaint.text = $"Mieszkańcy: {total - baseVal:F1} Food";
        if (lblHouseTotal != null) lblHouseTotal.text = $"SUMA (06:00): {total:F1} Food";
    }
    private void OnToggleGrowthClicked()
    {
        if (currentHouseTarget != null)
        {
            // Zmieniamy stan na przeciwny
            currentHouseTarget.stopGrowth = !currentHouseTarget.stopGrowth;
            
            // Odświeżamy UI żeby zobaczyć efekty (kolor przycisku i napis "WSTRZYMANY")
            Refresh(currentHouseTarget);
        }
    }
}
