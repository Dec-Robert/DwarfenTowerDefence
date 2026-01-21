using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Text; // Potrzebne do StringBuilder

public class UIBuildManager : MonoBehaviour
{
    [Header("Referencje")]
    public UIDocument uiDocument;

    [Header("Baza Danych Budynków")]
    public List<TowerData> availableTowers;
    public List<BuildingData> availableBuildings;

    // Cache elementów UI
    private VisualElement towerPanel;
    private VisualElement buildingPanel;
    private Button toggleButton;

    // Cache Tooltipa
    private VisualElement tooltipBox;
    private Label tooltipTitle;
    private Label tooltipBody;
    private Label tooltipCost;

    private bool isBuildMenuOpen = false;

    private void Start()
    {
        var root = uiDocument.rootVisualElement;

        // 1. ZnajdŸ kontenery
        towerPanel = root.Q<VisualElement>("TowerPanel");
        buildingPanel = root.Q<VisualElement>("BuildingPanel");
        toggleButton = root.Q<Button>("Btn_OpenBuildMenu");

        // 2. ZnajdŸ elementy Tooltipa
        tooltipBox = root.Q<VisualElement>("Tooltip");
        tooltipTitle = root.Q<Label>("TooltipTitle");
        tooltipBody = root.Q<Label>("TooltipBody");
        tooltipCost = root.Q<Label>("TooltipCost");

        if (toggleButton != null)
        {
            toggleButton.clicked += ToggleBuildMenu;
        }

        // 3. Wygeneruj przyciski
        GenerateButtons(towerPanel, availableTowers);
        GenerateButtons(buildingPanel, availableBuildings);

        // Ukryj tooltip na start
        HideTooltip();
    }

    private void GenerateButtons<T>(VisualElement container, List<T> dataList) where T : BuildingData
    {
        if (container == null) return;
        container.Clear();

        foreach (var data in dataList)
        {
            Button btn = new Button();
            btn.AddToClassList("generated-build-btn");

            if (data.icon != null)
            {
                btn.style.backgroundImage = new StyleBackground(data.icon);
                btn.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }

            Label nameLbl = new Label(data.buildingName);
            nameLbl.style.fontSize = 10;
            nameLbl.style.color = Color.white;
            nameLbl.style.backgroundColor = new Color(0, 0, 0, 0.7f);
            nameLbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            btn.Add(nameLbl);

            // LOGIKA KLIKNIÊCIA
            btn.clicked += () => OnBuildingClicked(data);

            // LOGIKA TOOLTIPA (Mouse Events)
            // MouseEnter -> Poka¿ i Wype³nij
            btn.RegisterCallback<MouseEnterEvent>(evt => ShowTooltip(data, evt));
            // MouseMove -> Przesuwaj za myszk¹
            btn.RegisterCallback<MouseMoveEvent>(evt => MoveTooltip(evt));
            // MouseLeave -> Ukryj
            btn.RegisterCallback<MouseLeaveEvent>(evt => HideTooltip());

            container.Add(btn);
        }
    }

    // --- LOGIKA TOOLTIPA ---

    private void ShowTooltip(BuildingData data, MouseEnterEvent evt)
    {
        if (tooltipBox == null) return;

        tooltipBox.style.display = DisplayStyle.Flex; // Poka¿
        UpdateTooltipContent(data);
        MoveTooltip(evt.mousePosition); // Ustaw pozycjê startow¹
    }

    private void HideTooltip()
    {
        if (tooltipBox != null) tooltipBox.style.display = DisplayStyle.None;
    }

    private void MoveTooltip(MouseMoveEvent evt)
    {
        MoveTooltip(evt.mousePosition);
    }

    private void MoveTooltip(Vector2 mousePos)
    {
        if (tooltipBox == null) return;

        // Przesuniêcie, ¿eby kursor nie zas³ania³ tekstu
        float offsetX = 15;
        float offsetY = -tooltipBox.layout.height - 10; // Nad myszk¹

        // Ustawienie pozycji (Absolute)
        // W UI Toolkit root ma te same wspó³rzêdne co myszka w zdarzeniu
        tooltipBox.style.left = mousePos.x + offsetX;
        tooltipBox.style.top = mousePos.y + offsetY;
    }

    private void UpdateTooltipContent(BuildingData data)
    {
        tooltipTitle.text = data.buildingName;

        StringBuilder bodySb = new StringBuilder();
        StringBuilder costSb = new StringBuilder();

        // 1. KOSZT (Wspólne dla wszystkich)
        costSb.Append("KOSZT:\n");
        if (data.constructionCost.Count > 0)
        {
            foreach (var cost in data.constructionCost)
            {
                // Kolorowanie: Czerwony jak nie staæ, Zielony/Bia³y jak staæ
                bool canAfford = ResourceManager.Instance.CanAfford(cost.type, cost.amount);
                string colorHex = canAfford ? "#88FF88" : "#FF4444";
                // Niestety Label w UI Toolkit nie obs³uguje Rich Text (HTML) domyœlnie tak dobrze jak TMP,
                // ale podstawowe tagi mog¹ nie dzia³aæ jeœli nie w³¹czysz "Enable Rich Text" w panelu.
                // UI Toolkit jest tu specyficzny. Jeœli tagi nie dzia³aj¹, po prostu wypisz tekst.
                costSb.AppendLine($"- {cost.amount} {cost.type}");
            }
        }
        else
        {
            costSb.Append("Darmowe");
        }
        tooltipCost.text = costSb.ToString();

        // 2. ZAWARTOŒÆ ZALE¯NA OD TYPU
        if (data is TowerData tower)
        {
            // --- WIE¯A ---
            bodySb.AppendLine("TYP: Wie¿a Obronna");
            bodySb.AppendLine($"Obra¿enia: {tower.baseDamage}");
            bodySb.AppendLine($"Zasiêg: {tower.baseRange}");
            bodySb.AppendLine($"Szybkoœæ: {tower.fireRate}/s");

            if (!string.IsNullOrEmpty(tower.description))
            {
                bodySb.AppendLine("\n" + tower.description);
            }
        }
        else
        {
            // --- BUDYNEK EKONOMICZNY ---
            bodySb.AppendLine("TYP: Budynek Ekonomiczny");

            // Produkcja
            if (data.productionPerCycle != null && data.productionPerCycle.Count > 0)
            {
                bodySb.AppendLine("\nPRODUKCJA (na zmianê):");
                foreach (var p in data.productionPerCycle)
                    bodySb.AppendLine($"+ {p.amount} {p.type}");
            }

            // Utrzymanie
            if (data.upkeepPerCycle != null && data.upkeepPerCycle.Count > 0)
            {
                bodySb.AppendLine("\nUTRZYMANIE (na zmianê):");
                foreach (var u in data.upkeepPerCycle)
                    bodySb.AppendLine($"- {u.amount} {u.type}");
            }

            // Teren
            if (data.allowedTerrain != null && data.allowedTerrain.Count > 0)
            {
                bodySb.AppendLine("\nWYMAGANY TEREN:");
                bodySb.Append(string.Join(", ", data.allowedTerrain));
            }
        }

        tooltipBody.text = bodySb.ToString();
    }

    // --- RESZTA METOD (Bez zmian) ---
    private void ToggleBuildMenu()
    {
        isBuildMenuOpen = !isBuildMenuOpen;
        if (isBuildMenuOpen) { towerPanel.style.display = DisplayStyle.None; buildingPanel.style.display = DisplayStyle.Flex; toggleButton.text = "WRÓÆ"; toggleButton.AddToClassList("build-menu-btn-cancel"); }
        else { towerPanel.style.display = DisplayStyle.Flex; buildingPanel.style.display = DisplayStyle.None; toggleButton.text = "BUDUJ"; toggleButton.RemoveFromClassList("build-menu-btn-cancel"); if (InteractionManager.Instance != null) InteractionManager.Instance.CancelBuilding(); }
    }

    private void OnBuildingClicked(BuildingData data)
    {
        if (InteractionManager.Instance != null) InteractionManager.Instance.SelectBuildingToBuild(data);
    }
}