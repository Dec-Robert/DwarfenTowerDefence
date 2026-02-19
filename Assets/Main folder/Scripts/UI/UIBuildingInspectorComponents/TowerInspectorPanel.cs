using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Panel inspektora dla TowerEntity.
/// Wyświetla aktualne statystyki walki z porównaniem do wartości bazowych.
/// </summary>
public class TowerInspectorPanel : BuildingInspectorPanel
{
    private readonly Label lblDamage;
    private readonly Label lblRange;
    private readonly Label lblFireRate;

    public TowerInspectorPanel(
        VisualElement root,
        Label lblDamage,
        Label lblRange,
        Label lblFireRate)
        : base(root)
    {
        this.lblDamage   = lblDamage;
        this.lblRange    = lblRange;
        this.lblFireRate = lblFireRate;
    }

    public override void Refresh(BuildingEntity target)
    {
        if (target is not TowerEntity tower) return;
        if (tower.controller == null) return;

        if (lblDamage   != null) lblDamage.text   = FormatStat("Obrażenia",  tower.controller.GetCurrentDamage(),   tower.controller.GetBaseDamage());
        if (lblRange    != null) lblRange.text    = FormatStat("Zasięg",     tower.controller.GetCurrentRange(),    tower.controller.GetBaseRange());
        if (lblFireRate != null) lblFireRate.text = FormatStat("Szybkość",   tower.controller.GetCurrentFireRate(), tower.controller.GetBaseFireRate(), "/s");
    }

    private string FormatStat(string name, float current, float baseVal, string suffix = "")
    {
        if (Mathf.Abs(current - baseVal) > 0.01f)
        {
            string color = current >= baseVal ? "#88FF88" : "#FF8888";
            return $"{name}: <color={color}>{current:F1}{suffix}</color> <color=#AAAAAA>({baseVal:F1})</color>";
        }
        return $"{name}: {current:F1}{suffix}";
    }
}
