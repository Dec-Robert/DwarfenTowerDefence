using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Text;

public class UIBuildManager : MonoBehaviour
{
    [Header("Referencje")]
    public UIDocument uiDocument;

    [Header("Baza Danych")]
    // Jedna du¿a lista, któr¹ posortujemy w Start
    public List<BuildingData> allBuildingsDatabase;

    // Listy posortowane
    private List<BuildingData> productionBuildings = new List<BuildingData>();
    private List<BuildingData> defenseBuildings = new List<BuildingData>();
    private List<BuildingData> housingBuildings = new List<BuildingData>();

    // Elementy UI
    private Button btnMainBuild;
    private VisualElement categoryPanel;
    private VisualElement buildingsListPanel;
    private VisualElement buildingsContainer;

    private Button btnCatProd, btnCatDef, btnCatHouse;

    // Cache Tooltipa
    private VisualElement tooltipBox;
    private Label tooltipTitle, tooltipBody, tooltipCost;

    // Stan
    private bool isMenuOpen = false;

    private void Start()
    {
        // 1. Sortowanie budynków
        SortBuildings();

        var root = uiDocument.rootVisualElement;

        // 2. ZnajdŸ elementy interfejsu (z nowego GameHUD.uxml)
        btnMainBuild = root.Q<Button>("Btn_MainBuild");
        categoryPanel = root.Q<VisualElement>("CategoryPanel");
        buildingsListPanel = root.Q<VisualElement>("BuildingsListPanel");
        buildingsContainer = root.Q<VisualElement>("BuildingsContainer");

        btnCatProd = root.Q<Button>("Btn_Cat_Production");
        btnCatDef = root.Q<Button>("Btn_Cat_Defense");
        btnCatHouse = root.Q<Button>("Btn_Cat_Housing");

        // Tooltip
        tooltipBox = root.Q<VisualElement>("Tooltip");
        tooltipTitle = root.Q<Label>("TooltipTitle");
        tooltipBody = root.Q<Label>("TooltipBody");
        tooltipCost = root.Q<Label>("TooltipCost");

        // 3. Podepnij eventy
        if (btnMainBuild != null)
            btnMainBuild.clicked += ToggleMainMenu;

        if (btnCatProd != null) btnCatProd.clicked += () => ShowCategory(productionBuildings, btnCatProd);
        if (btnCatDef != null) btnCatDef.clicked += () => ShowCategory(defenseBuildings, btnCatDef);
        if (btnCatHouse != null) btnCatHouse.clicked += () => ShowCategory(housingBuildings, btnCatHouse);

        // Na start ukryte
        CloseAll();
        HideTooltip();
    }

    void SortBuildings()
    {
        productionBuildings.Clear();
        defenseBuildings.Clear();
        housingBuildings.Clear();

        foreach (var b in allBuildingsDatabase)
        {
            if (b == null) continue;

            if (b.type == BuildingType.Economic) productionBuildings.Add(b);
            else if (b.type == BuildingType.Defense) defenseBuildings.Add(b);
            else if (b.type == BuildingType.Utility) housingBuildings.Add(b);
            // Unique (Beacon, Kapitol) pomijamy w menu budowania
        }
    }

    void ToggleMainMenu()
    {
        isMenuOpen = !isMenuOpen;

        if (isMenuOpen)
        {
            categoryPanel.style.display = DisplayStyle.Flex;
            btnMainBuild.text = "X"; // Zmieñ na Zamknij
            btnMainBuild.AddToClassList("build-menu-btn-cancel");

            // Domyœlnie otwórz pierwsz¹ kategoriê (Produkcja)
            ShowCategory(productionBuildings, btnCatProd);
        }
        else
        {
            CloseAll();
        }
    }

    void CloseAll()
    {
        isMenuOpen = false;
        if (categoryPanel != null) categoryPanel.style.display = DisplayStyle.None;
        if (buildingsListPanel != null) buildingsListPanel.style.display = DisplayStyle.None;

        if (btnMainBuild != null)
        {
            btnMainBuild.text = "BUDUJ";
            btnMainBuild.RemoveFromClassList("build-menu-btn-cancel");
        }

        if (InteractionManager.Instance != null) InteractionManager.Instance.CancelBuilding();
        HideTooltip();
    }

    void ShowCategory(List<BuildingData> buildings, Button activeBtn)
    {
        // Poka¿ panel listy
        if (buildingsListPanel != null) buildingsListPanel.style.display = DisplayStyle.Flex;

        // Reset stylów przycisków kategorii
        if (btnCatProd != null) btnCatProd.RemoveFromClassList("category-btn-active");
        if (btnCatDef != null) btnCatDef.RemoveFromClassList("category-btn-active");
        if (btnCatHouse != null) btnCatHouse.RemoveFromClassList("category-btn-active");

        // Aktywuj wybrany
        if (activeBtn != null) activeBtn.AddToClassList("category-btn-active");

        // Wygeneruj przyciski
        GenerateButtons(buildings);
    }

    private void GenerateButtons(List<BuildingData> dataList)
    {
        if (buildingsContainer == null) return;
        buildingsContainer.Clear();

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
            nameLbl.style.fontSize = 9;
            nameLbl.style.color = Color.white;
            nameLbl.style.backgroundColor = new Color(0, 0, 0, 0.7f);
            nameLbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            nameLbl.style.whiteSpace = WhiteSpace.Normal;
            btn.Add(nameLbl);

            // KLIKNIÊCIE -> Wybierz budynek
            btn.clicked += () => {
                if (InteractionManager.Instance != null)
                    InteractionManager.Instance.SelectBuildingToBuild(data);
            };

            // TOOLTIPY
            btn.RegisterCallback<MouseEnterEvent>(evt => ShowTooltip(data, evt));
            btn.RegisterCallback<MouseMoveEvent>(evt => MoveTooltip(evt));
            btn.RegisterCallback<MouseLeaveEvent>(evt => HideTooltip());

            buildingsContainer.Add(btn);
        }
    }

    // --- LOGIKA TOOLTIPA ---

    private void ShowTooltip(BuildingData data, MouseEnterEvent evt)
    {
        if (tooltipBox == null) return;

        tooltipBox.style.display = DisplayStyle.Flex;
        UpdateTooltipContent(data);
        MoveTooltip(evt.mousePosition);
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

        float offsetX = 15;
        float offsetY = -tooltipBox.layout.height - 10;

        tooltipBox.style.left = mousePos.x + offsetX;
        tooltipBox.style.top = mousePos.y + offsetY;
    }

    private void UpdateTooltipContent(BuildingData data)
    {
        tooltipTitle.text = data.buildingName;

        StringBuilder bodySb = new StringBuilder();
        StringBuilder costSb = new StringBuilder();

        // 1. KOSZT BUDOWY (Wspólny dla wszystkich)
        costSb.Append("<b>Koszt Budowy:</b>\n");
        if (data.constructionCost != null && data.constructionCost.Count > 0)
        {
            foreach (var cost in data.constructionCost)
            {
                // Kolorowanie tekstu (zielony/czerwony)
                bool canAfford = ResourceManager.Instance.CanAfford(cost.type, cost.amount);
                string colorHex = canAfford ? "#88FF88" : "#FF4444";
                costSb.AppendLine($"<color={colorHex}>- {cost.amount} {cost.type}</color>");
            }
        }
        else
        {
            costSb.Append("Darmowe");
        }
        tooltipCost.text = costSb.ToString();

        // 2. SZCZEGÓ£OWY OPIS ZALE¯NY OD TYPU BUDYNKU
        switch (data.type)
        {
            // --- A) OBRONA ---
            case BuildingType.Defense:
                // Rzutujemy dane, ¿eby dostaæ siê do statystyk wie¿y
                if (data is TowerData tower)
                {
                    bodySb.AppendLine("<b>Typ:</b> Wie¿a Obronna");
                    bodySb.AppendLine($"<b>Obra¿enia:</b> {tower.baseDamage}");
                    bodySb.AppendLine($"<b>Zasiêg:</b> {tower.baseRange}");
                    bodySb.AppendLine($"<b>Szybkoœæ:</b> {tower.fireRate}/s");

                    // KOSZT AMUNICJI (czyli upkeepPerCycle)
                    if (tower.upkeepPerCycle != null && tower.upkeepPerCycle.Count > 0)
                    {
                        bodySb.Append("\n<b>Koszt Amunicji (na noc):</b>\n");
                        foreach (var upkeep in tower.upkeepPerCycle)
                            bodySb.AppendLine($"- {upkeep.amount} {upkeep.type}");
                    }
                }
                break;

            // --- B) MIESZKANIA ---
            case BuildingType.Utility:
                // Zak³adamy, ¿e Utility to Mieszkania
                if (data is HousingBuildingData house)
                {
                    bodySb.AppendLine("<b>Typ:</b> Budynek Mieszkalny");
                    bodySb.AppendLine($"<b>Rasa:</b> {house.housingRace}");
                    bodySb.AppendLine($"<b>Startowa Populacja:</b> {house.initialResidents}");
                    bodySb.AppendLine($"<b>Max Populacja:</b> {house.maxResidents}");

                    if (house.baseDailyUpkeep != null && house.baseDailyUpkeep.Count > 0)
                    {
                        bodySb.Append("\n<b>Utrzymanie Budynku (dziennie):</b>\n");
                        foreach (var upkeep in house.baseDailyUpkeep)
                            bodySb.AppendLine($"- {upkeep.amount} {upkeep.type}");
                    }
                    if (house.upkeepPerResident != null && house.upkeepPerResident.Count > 0)
                    {
                        bodySb.Append("\n<b>Utrzymanie Mieszkañca (dziennie):</b>\n");
                        foreach (var upkeep in house.upkeepPerResident)
                            bodySb.AppendLine($"- {upkeep.amount:F2} {upkeep.type}");
                    }
                    if (house.growthSurplusCost != null && house.growthSurplusCost.Count > 0)
                    {
                        bodySb.Append("\n<b>Koszt Przyrostu (dziennie):</b>\n");
                        foreach (var upkeep in house.growthSurplusCost)
                            bodySb.AppendLine($"- {upkeep.amount} {upkeep.type}");
                    }
                }
                break;

            // --- C) PRODUKCJA (Domyœlnie) ---
            case BuildingType.Economic:
            default:
                bodySb.AppendLine("<b>Typ:</b> Budynek Ekonomiczny");

                if (data.productionPerCycle != null && data.productionPerCycle.Count > 0)
                {
                    bodySb.Append("\n<b>Produkcja (na zmianê):</b>\n");
                    foreach (var prod in data.productionPerCycle)
                        bodySb.AppendLine($"+ {prod.amount} {prod.type}");
                }

                if (data.upkeepPerCycle != null && data.upkeepPerCycle.Count > 0)
                {
                    bodySb.Append("\n<b>Utrzymanie (na zmianê):</b>\n");
                    foreach (var upkeep in data.upkeepPerCycle)
                        bodySb.AppendLine($"- {upkeep.amount} {upkeep.type}");
                }

                if (data.allowedTerrain != null && data.allowedTerrain.Count > 0)
                {
                    bodySb.AppendLine("\n<b>Wymagany Teren:</b>");
                    bodySb.Append(string.Join(", ", data.allowedTerrain));
                }
                break;
        }

        // Dodanie ogólnego opisu, jeœli istnieje
        if (!string.IsNullOrEmpty(data.description))
        {
            bodySb.AppendLine($"\n<i>{data.description}</i>");
        }

        tooltipBody.text = bodySb.ToString();
    }
}