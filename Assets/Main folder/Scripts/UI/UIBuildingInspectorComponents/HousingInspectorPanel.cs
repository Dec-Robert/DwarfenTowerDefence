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

    public HousingInspectorPanel(
        VisualElement root,
        VisualElement birthBarFill,
        VisualElement residentCapsules,
        Label lblBirthDays,
        Label lblHouseBase,
        Label lblHouseMaint,
        Label lblHouseTotal,
        Label lblGrowthMod,
        Label lblHouseStatus)
        : base(root)
    {
        this.birthBarFill    = birthBarFill;
        this.residentCapsules = residentCapsules;
        this.lblBirthDays    = lblBirthDays;
        this.lblHouseBase    = lblHouseBase;
        this.lblHouseMaint   = lblHouseMaint;
        this.lblHouseTotal   = lblHouseTotal;
        this.lblGrowthMod    = lblGrowthMod;
        this.lblHouseStatus  = lblHouseStatus;
    }

    public override void Refresh(BuildingEntity target)
    {
        if (target is not HousingEntity house) return;

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

        // 5. Status wzrostu
        if (lblHouseStatus != null)
        {
            string status = house.GetGrowthStatus();
            lblHouseStatus.text = $"Status: {status}";
            lblHouseStatus.style.color = status == "ROSNĄCY" ? Color.green : Color.red;
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
}
