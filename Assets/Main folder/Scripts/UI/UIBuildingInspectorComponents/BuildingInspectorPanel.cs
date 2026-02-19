using UnityEngine.UIElements;

/// <summary>
/// Bazowy interfejs dla każdego panelu inspektora budynku.
/// Każdy panel zna swój korzeń UI i wie jak się odświeżyć.
/// </summary>
public abstract class BuildingInspectorPanel
{
    protected readonly VisualElement root;

    protected BuildingInspectorPanel(VisualElement root)
    {
        this.root = root;
    }

    public void Show() => root.style.display = DisplayStyle.Flex;
    public void Hide() => root.style.display = DisplayStyle.None;

    /// <summary>Odświeża zawartość panelu na podstawie aktualnego budynku.</summary>
    public abstract void Refresh(BuildingEntity target);
}
