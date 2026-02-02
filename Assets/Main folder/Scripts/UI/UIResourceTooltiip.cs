using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class UIResourceTooltip : MonoBehaviour
{
    [Header("Referencje")]
    public UIDocument uiDocument;

    private VisualElement root;
    private VisualElement topBar;
    private VisualElement tooltip;

    private Label lblName, lblGain, lblLoss, lblNet;

    // Mapa: Nazwa Elementu UI -> Typ Zasobu
    private Dictionary<string, ResourceType> resourceMap = new Dictionary<string, ResourceType>
    {
        { "Gold_Group", ResourceType.Gold },
        { "Wood_Group", ResourceType.Wood },
        { "Stone_Group", ResourceType.Stone },
        { "Food_Group", ResourceType.Food },
        { "Coal_Group", ResourceType.Coal },
        { "Iron_Group", ResourceType.Iron },
        { "Pop_Group", ResourceType.Population } // Dla populacji logika mo¿e byæ inna
    };

    private void OnEnable()
    {
        root = uiDocument.rootVisualElement;
        topBar = root.Q<VisualElement>("TopBar");
        tooltip = root.Q<VisualElement>("ResourceTooltip");

        lblName = tooltip.Q<Label>("ResTool_Name");
        lblGain = tooltip.Q<Label>("ResTool_Gain");
        lblLoss = tooltip.Q<Label>("ResTool_Loss");
        lblNet = tooltip.Q<Label>("ResTool_Net");

        // Rejestracja klikniêæ na grupy surowców
        foreach (var mapping in resourceMap)
        {
            var element = root.Q<VisualElement>(mapping.Key);
            if (element != null)
            {
                // Rejestrujemy klikniêcie - przekazujemy typ zasobu i sam element (do pozycji)
                element.RegisterCallback<ClickEvent>(evt => OpenTooltip(mapping.Value, element));
            }
        }

        // Rejestracja wyjœcia myszk¹ z paska TopBar
        if (topBar != null)
        {
            topBar.RegisterCallback<MouseLeaveEvent>(evt => CloseTooltip());
        }
    }

    void OpenTooltip(ResourceType type, VisualElement targetElement)
    {
        // 1. Oblicz dane
        var data = EconomyForecastSystem.GetForecastFor(type);

        // 2. Ustaw teksty
        lblName.text = type.ToString().ToUpper();
        lblGain.text = $"+{data.dailyProduction:F1}";
        lblLoss.text = $"-{data.dailyConsumption:F1}";

        string sign = data.netBalance > 0 ? "+" : "";
        lblNet.text = $"{sign}{data.netBalance:F1}";
        lblNet.style.color = data.netBalance >= 0 ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.3f, 0.3f);

        // 3. Pozycjonowanie
        // Tooltip ma byæ pod elementem.
        // targetElement.layout zawiera pozycjê wzglêdem rodzica (TopBar).
        // TopBar jest na górze (y=0).

        float xPos = targetElement.layout.x;
        // Centrujemy: X elementu + po³owa szerokoœci elementu - po³owa szerokoœci tooltipa
        // Ale tooltip.layout.width mo¿e byæ nieznane przed pierwszym renderem, u¿yjmy sta³ego offsetu lub wyrównajmy do lewej.

        tooltip.style.left = xPos;
        tooltip.style.top = targetElement.layout.height + 5; // Zaraz pod paskiem

        tooltip.style.display = DisplayStyle.Flex;
    }

    void CloseTooltip()
    {
        if (tooltip != null) tooltip.style.display = DisplayStyle.None;
    }
}