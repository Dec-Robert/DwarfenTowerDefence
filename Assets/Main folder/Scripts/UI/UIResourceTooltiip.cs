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

    // Kontenery tryb�w
    private VisualElement modeStandard;
    private VisualElement modePopulation;

    // Elementy Standardowe
    private Label lblName, lblGain, lblLoss, lblNet;

    // Elementy Populacji (Tabelka)
    private Label pIdleH, pIdleE, pIdleD;
    private Label pAssignedH, pAssignedE, pAssignedD;
    private Label pWorkH, pWorkE, pWorkD;

    private Dictionary<string, ResourceType> resourceMap = new Dictionary<string, ResourceType>
    {
        { "Gold_Group", ResourceType.Gold },
        { "Wood_Group", ResourceType.Wood },
        { "Stone_Group", ResourceType.Stone },
        { "Food_Group", ResourceType.Food },
        { "Coal_Group", ResourceType.Coal },
        { "Iron_Group", ResourceType.Iron },
        { "Pop_Group", ResourceType.Population }
    };

    private void OnEnable()
    {//
        if (uiDocument == null) return;
        root = uiDocument.rootVisualElement;
        topBar = root.Q<VisualElement>("TopBar");
        tooltip = root.Q<VisualElement>("ResourceTooltip");

        // Kontenery
        modeStandard = tooltip.Q<VisualElement>("Mode_Standard");
        modePopulation = tooltip.Q<VisualElement>("Mode_Population");

        // Standard
        lblName = tooltip.Q<Label>("ResTool_Name");
        lblGain = tooltip.Q<Label>("ResTool_Gain");
        lblLoss = tooltip.Q<Label>("ResTool_Loss");
        lblNet = tooltip.Q<Label>("ResTool_Net");

        // Populacja - Wolni
        pIdleH = tooltip.Q<Label>("Pop_Idle_H");
        pIdleE = tooltip.Q<Label>("Pop_Idle_E");
        pIdleD = tooltip.Q<Label>("Pop_Idle_D");

        // Populacja - Zarezerwowani
        pAssignedH = tooltip.Q<Label>("Pop_Assigned_H");
        pAssignedE = tooltip.Q<Label>("Pop_Assigned_E");
        pAssignedD = tooltip.Q<Label>("Pop_Assigned_D");

        // Populacja - Pracuj�cy
        pWorkH = tooltip.Q<Label>("Pop_Work_H");
        pWorkE = tooltip.Q<Label>("Pop_Work_E");
        pWorkD = tooltip.Q<Label>("Pop_Work_D");

        // Rejestracja event�w
        foreach (var mapping in resourceMap)
        {
            var element = root.Q<VisualElement>(mapping.Key);
            if (element != null)
            {
                element.RegisterCallback<ClickEvent>(evt => OpenTooltip(mapping.Value, element));
            }
        }

        if (topBar != null)
        {
            topBar.RegisterCallback<MouseLeaveEvent>(evt => CloseTooltip());
        }
    }

    void OpenTooltip(ResourceType type, VisualElement targetElement)
    {
        lblName.text = type.ToString().ToUpper();

        // 1. TRYB POPULACJI
        if (type == ResourceType.Population)
        {
            modeStandard.style.display = DisplayStyle.None;
            modePopulation.style.display = DisplayStyle.Flex;

            UpdatePopulationTable();
        }
        // 2. TRYB STANDARDOWY (SUROWCE)
        else
        {
            modeStandard.style.display = DisplayStyle.Flex;
            modePopulation.style.display = DisplayStyle.None;

            var data = EconomyForecastSystem.GetForecastFor(type);
            lblGain.text = $"+{data.dailyProduction:F1}";
            lblLoss.text = $"-{data.dailyConsumption:F1}";

            string sign = data.netBalance > 0 ? "+" : "";
            lblNet.text = $"{sign}{data.netBalance:F1}";
            lblNet.style.color = data.netBalance >= 0 ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.3f, 0.3f);
        }

        // Pozycjonowanie
        float xPos = targetElement.layout.x;
        // Korekta dla populacji (jest po prawej, wi�c tooltip m�g�by wyj�� za ekran)
        if (type == ResourceType.Population) xPos -= 50;

        tooltip.style.left = xPos;
        tooltip.style.top = targetElement.layout.height + 5;

        tooltip.style.display = DisplayStyle.Flex;
    }

    void UpdatePopulationTable()
    {
        if (CitizenManager.Instance == null) return;

        // Wolni (Idle)
        pIdleH.text = CitizenManager.Instance.GetCountByState(Race.Humans, WorkState.Idle).ToString();
        pIdleE.text = CitizenManager.Instance.GetCountByState(Race.Elves, WorkState.Idle).ToString();
        pIdleD.text = CitizenManager.Instance.GetCountByState(Race.Dwarves, WorkState.Idle).ToString();

        // Zarezerwowani (Assigned)
        pAssignedH.text = CitizenManager.Instance.GetCountByState(Race.Humans, WorkState.Assigned).ToString();
        pAssignedE.text = CitizenManager.Instance.GetCountByState(Race.Elves, WorkState.Assigned).ToString();
        pAssignedD.text = CitizenManager.Instance.GetCountByState(Race.Dwarves, WorkState.Assigned).ToString();

        // Pracuj�cy (Working + Exhausted, bo Exhausted te� s� "w pracy" do ko�ca dnia, tylko nieefektywni)
        pWorkH.text = CitizenManager.Instance.GetCountByState(Race.Humans, WorkState.Working, WorkState.Exhausted).ToString();
        pWorkE.text = CitizenManager.Instance.GetCountByState(Race.Elves, WorkState.Working, WorkState.Exhausted).ToString();
        pWorkD.text = CitizenManager.Instance.GetCountByState(Race.Dwarves, WorkState.Working, WorkState.Exhausted).ToString();
    }

    void CloseTooltip()
    {
        if (tooltip != null) tooltip.style.display = DisplayStyle.None;
    }
}