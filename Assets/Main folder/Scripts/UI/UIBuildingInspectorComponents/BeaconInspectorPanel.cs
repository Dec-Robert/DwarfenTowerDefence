using UnityEngine.UIElements;

/// <summary>
/// Panel inspektora dla BeaconEntity.
/// Obsługuje suwak węgla i wyświetlanie aktualnej wartości.
/// </summary>
public class BeaconInspectorPanel : BuildingInspectorPanel
{
    private readonly SliderInt coalSlider;
    private readonly Label lblCoalVal;

    public BeaconInspectorPanel(VisualElement root, SliderInt coalSlider, Label lblCoalVal)
        : base(root)
    {
        this.coalSlider = coalSlider;
        this.lblCoalVal = lblCoalVal;

        if (coalSlider != null)
            coalSlider.RegisterValueChangedCallback(evt => OnCoalSliderChanged(evt.newValue));
    }

    public override void Refresh(BuildingEntity target)
    {
        if (target is not BeaconEntity beacon) return;

        if (coalSlider != null) coalSlider.SetValueWithoutNotify(beacon.dailyCoalInput);
        if (lblCoalVal != null) lblCoalVal.text = beacon.dailyCoalInput.ToString();
    }

    private void OnCoalSliderChanged(int newValue)
    {
        // Beacon jest singletonem – możemy się do niego odwołać bezpośrednio
        if (BeaconEntity.Instance != null)
        {
            BeaconEntity.Instance.dailyCoalInput = newValue;
            if (lblCoalVal != null) lblCoalVal.text = newValue.ToString();
        }
    }
}
