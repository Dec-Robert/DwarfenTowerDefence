using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Text;
using System.Linq;

public class UIBuildManager : MonoBehaviour
{
    public static UIBuildManager Instance { get; private set; }

    [Header("Referencje")]
    public UIDocument uiDocument;
    public List<BuildingData> allBuildingsDatabase;

    // Listy posortowane
    private List<BuildingData> prodBuildings = new List<BuildingData>();
    private List<BuildingData> defBuildings = new List<BuildingData>();
    private List<BuildingData> houseBuildings = new List<BuildingData>();
    private List<BuildingData> utilBuildings = new List<BuildingData>();
    private List<BuildingData> uniqueBuildings = new List<BuildingData>();

    // Elementy UI
    private Button btnMainBuild;
    private VisualElement categoryPanel;
    private VisualElement buildingsListPanel;
    private VisualElement buildingsContainer;
    private VisualElement expandedMenuContainer;


    private Button btnCatProd, btnCatDef, btnCatHouse, btnCatUtil, btnCatUnique;

    // Cache Tooltipa
    private VisualElement tooltipBox;
    private Label tooltipTitle, tooltipBody;

    private bool isMenuOpen = false;

    private void Awake() => Instance = this;

    private void Start()
    {
        SortBuildings();

        var root = uiDocument.rootVisualElement;

        btnMainBuild = root.Q<Button>("Btn_MainBuild");
        categoryPanel = root.Q<VisualElement>("CategoryPanel");
        buildingsListPanel = root.Q<VisualElement>("BuildingsListPanel");
        buildingsContainer = root.Q<VisualElement>("BuildingsContainer");

        btnCatProd = root.Q<Button>("Btn_Cat_Production");
        btnCatDef = root.Q<Button>("Btn_Cat_Defense");
        btnCatHouse = root.Q<Button>("Btn_Cat_Housing");
        btnCatUtil = root.Q<Button>("Btn_Cat_Utility");
        btnCatUnique = root.Q<Button>("Btn_Cat_Unique");

        tooltipBox = root.Q<VisualElement>("Tooltip");
        tooltipTitle = root.Q<Label>("TooltipTitle");
        tooltipBody = root.Q<Label>("TooltipBody"); // Połączyliśmy koszt i opis w jedno duże body
        expandedMenuContainer = root.Q<VisualElement>("ExpandedMenuContainer"); // NOWE


        if (btnMainBuild != null) btnMainBuild.clicked += ToggleMainMenu;

        if (btnCatProd != null) btnCatProd.clicked += () => ShowCategory(prodBuildings, btnCatProd);
        if (btnCatDef != null) btnCatDef.clicked += () => ShowCategory(defBuildings, btnCatDef);
        if (btnCatHouse != null) btnCatHouse.clicked += () => ShowCategory(houseBuildings, btnCatHouse);
        if (btnCatUtil != null) btnCatUtil.clicked += () => ShowCategory(utilBuildings, btnCatUtil);
        if (btnCatUnique != null) btnCatUnique.clicked += () => ShowCategory(uniqueBuildings, btnCatUnique);

        CloseAll();
        HideTooltip();
    }

    void SortBuildings()
    {
        prodBuildings.Clear(); defBuildings.Clear(); houseBuildings.Clear(); utilBuildings.Clear(); uniqueBuildings.Clear();

        foreach (var b in allBuildingsDatabase)
        {
            if (b == null) continue;
            switch (b.type)
            {
                case BuildingType.Economic: prodBuildings.Add(b); break;
                case BuildingType.Defense: defBuildings.Add(b); break;
                case BuildingType.Housing: houseBuildings.Add(b); break;
                case BuildingType.Utility: utilBuildings.Add(b); break;
                case BuildingType.Unique: uniqueBuildings.Add(b); break;
            }
        }
    }

    void ToggleMainMenu()
    {
        isMenuOpen = !isMenuOpen;
        if (isMenuOpen)
        {
            expandedMenuContainer.style.display = DisplayStyle.Flex; // NOWE
            btnMainBuild.text = "X";
            btnMainBuild.AddToClassList("main-build-btn-open");
            ShowCategory(prodBuildings, btnCatProd);
        }
        else CloseAll();
    }

    void CloseAll()
    {
        isMenuOpen = false;
        if (expandedMenuContainer != null) expandedMenuContainer.style.display = DisplayStyle.None; // NOWE

        if (btnMainBuild != null)
        {
            btnMainBuild.text = "BUDUJ";
            btnMainBuild.RemoveFromClassList("main-build-btn-open");
        }

        InteractionManager.Instance?.CancelBuilding();
        HideTooltip();
    }

    void ShowCategory(List<BuildingData> buildings, Button activeBtn)
    {
        // 1. Wymuszenie zawijania (FlexWrap.Wrap) na zawartości ScrollView
        if (buildingsListPanel != null)
        {
            ScrollView scroll = buildingsListPanel.Q<ScrollView>();
            if (scroll != null)
            {
                scroll.contentContainer.style.flexDirection = FlexDirection.Row;
                scroll.contentContainer.style.flexWrap = Wrap.Wrap;
                scroll.contentContainer.style.justifyContent = Justify.FlexStart; // Od lewej
            }
        }

        // --- Reszta twojego oryginalnego kodu w ShowCategory ---
        btnCatProd?.RemoveFromClassList("category-btn-active");
        btnCatDef?.RemoveFromClassList("category-btn-active");
        btnCatHouse?.RemoveFromClassList("category-btn-active");
        btnCatUtil?.RemoveFromClassList("category-btn-active");
        btnCatUnique?.RemoveFromClassList("category-btn-active");

        activeBtn?.AddToClassList("category-btn-active");
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
            nameLbl.AddToClassList("generated-btn-label");
            btn.Add(nameLbl);

            btn.clicked += () => InteractionManager.Instance?.SelectBuildingToBuild(data);
            btn.RegisterCallback<MouseEnterEvent>(evt => ShowTooltip(data, evt));
            btn.RegisterCallback<MouseMoveEvent>(evt => MoveTooltip(evt.mousePosition));
            btn.RegisterCallback<MouseLeaveEvent>(evt => HideTooltip());

            buildingsContainer.Add(btn);
        }
    }

    // =========================================================================
    // SYSTEM TOOLTIPÓW (FROSTPUNK 2 / SPELLFORCE STYLE)
    // =========================================================================

    private void ShowTooltip(BuildingData data, MouseEnterEvent evt)
    {
        if (tooltipBox == null) return;
        tooltipBox.style.display = DisplayStyle.Flex;
        UpdateTooltipContent(data);
        MoveTooltip(evt.mousePosition);
    }

    private void HideTooltip() { if (tooltipBox != null) tooltipBox.style.display = DisplayStyle.None; }

    private void MoveTooltip(Vector2 mousePos)
    {
        if (tooltipBox == null) return;
        
        // ZWIĘKSZONY OFFSET:
        float offsetX = 20; // 20 pikseli w prawo od kursora
        float offsetY = -tooltipBox.layout.height - 20; // 20 pikseli nad kursorem

        tooltipBox.style.left = mousePos.x + offsetX;
        tooltipBox.style.top = mousePos.y + offsetY;
    }

    private void UpdateTooltipContent(BuildingData data)
    {
        tooltipTitle.text = data.buildingName.ToUpper();
        StringBuilder sb = new StringBuilder();

        // --- 1. KOSZT BUDOWY (Wspólne) ---
        sb.AppendLine("<color=#AAAAAA>KOSZT BUDOWY:</color>");
        if (data.constructionCost != null && data.constructionCost.Count > 0)
        {
            foreach (var cost in data.constructionCost)
            {
                bool canAfford = ResourceManager.Instance.CanAfford(cost.type, cost.amount);
                string colorHex = canAfford ? "#FFFFFF" : "#FF4444";
                sb.AppendLine($"<color={colorHex}> • {cost.amount} {cost.type}</color>");
            }
        }
        else sb.AppendLine(" <color=#88FF88>Darmowe</color>");

        // --- 2. WYMAGANIA POPULACJI (Wspólne) ---
        if (data.requiresSpecificCitizen)
        {
            sb.AppendLine($"\n<color=#FF8800>WYMAGA (Na stałe): 1x {data.requiredCitizenRace}</color>");
        }

        sb.AppendLine("<color=#444444>────────────────────────</color>");

        // --- 3. DANE ZALEŻNE OD TYPU (FROSTPUNK STYLE) ---
        switch (data.type)
        {
            case BuildingType.Housing:
                            if (data is HousingBuildingData houseData)
                            {
                                // --- OBLICZANIE RZECZYWISTYCH WARTOŚCI (Baza + Config + Meta) ---
                                int startPop = houseData.initialResidents;
                                int maxPop = houseData.maxResidents;

                                var globalCfg = ResourceManager.Instance?.cityConfig;
                                if (globalCfg != null && globalCfg.overrideHousingData)
                                {
                                    if (houseData.housingRace == Race.Humans) { startPop = globalCfg.humanStartPop; maxPop = globalCfg.humanMaxPop; }
                                    else if (houseData.housingRace == Race.Elves) { startPop = globalCfg.elfStartPop; maxPop = globalCfg.elfMaxPop; }
                                    else if (houseData.housingRace == Race.Dwarves) { startPop = globalCfg.dwarfStartPop; maxPop = globalCfg.dwarfMaxPop; }
                                }

                                int metaStartBonus = MetaUpgradeManager.Instance != null ? Mathf.RoundToInt(MetaUpgradeManager.Instance.GetValue(MetaEffectType.HousingStartPopulation)) : 0;
                                startPop += metaStartBonus;

                                int metaMaxBonusGlobal = MetaUpgradeManager.Instance != null ? Mathf.RoundToInt(MetaUpgradeManager.Instance.GetValue(MetaEffectType.HousingMaxResidents)) : 0;
                                MetaEffectType specificEffect = houseData.housingRace switch {
                                    Race.Humans => MetaEffectType.HousingMaxResidents_Humans,
                                    Race.Elves => MetaEffectType.HousingMaxResidents_Elves,
                                    Race.Dwarves => MetaEffectType.HousingMaxResidents_Dwarves,
                                    _ => MetaEffectType.None
                                };
                                int metaMaxBonusSpecific = MetaUpgradeManager.Instance != null ? Mathf.RoundToInt(MetaUpgradeManager.Instance.GetValue(specificEffect)) : 0;
                                maxPop += (metaMaxBonusGlobal + metaMaxBonusSpecific);
                                // ----------------------------------------------------------------

                                // Zmienione wyświetlanie: oddzielamy Startową i Maksymalną dla czytelności
                                sb.AppendLine($"<color=#AAAAAA>Pojemność na start:</color> <b>{startPop} {houseData.housingRace}</b>");
                                sb.AppendLine($"<color=#AAAAAA>Pojemność max:</color> <b>{maxPop} {houseData.housingRace}</b>");
                                
                                if (houseData.growthSurplusCost != null && houseData.growthSurplusCost.Count > 0)
                                {
                                    sb.AppendLine("\n<color=#AAAAAA>Koszt przyrostu (na cykl):</color>");
                                    foreach (var cost in houseData.growthSurplusCost) sb.AppendLine($" <color=#FF8888>-{cost.amount} {cost.type}</color>");
                                }
                            }
                            break;

            case BuildingType.Defense:
                if (data is TowerData tower)
                {
                    sb.AppendLine($"<color=#AAAAAA>Obrażenia:</color> <b>{tower.baseDamage}</b>  |  <color=#AAAAAA>Zasięg:</color> <b>{tower.baseRange}</b>");
                    sb.AppendLine($"<color=#AAAAAA>Szybkość:</color> <b>{tower.fireRate}/s</b>");
                    
                    if (tower.armorPenetration > 0 || tower.magicPenetration > 0)
                        sb.AppendLine($"<color=#AAAAAA>Penetracja:</color> <b>{tower.armorPenetration}% Fiz | {tower.magicPenetration}% Mag</b>");
                    
                    if (tower.criticalChancel > 0)
                        sb.AppendLine($"<color=#AAAAAA>Kryt:</color> <b>{tower.criticalChancel}% szans ({tower.criticalDamageMultiplier}x DMG)</b>");

                    if (tower.upkeepPerCycle != null && tower.upkeepPerCycle.Count > 0)
                    {
                        sb.AppendLine("\n<color=#AAAAAA>Amunicja (na noc):</color>");
                        foreach (var ammo in tower.upkeepPerCycle) sb.AppendLine($" <color=#FF8888>-{ammo.amount} {ammo.type}</color>");
                    }
                }
                break;

            case BuildingType.Economic:
                if (data.productionPerCycle != null && data.productionPerCycle.Count > 0)
                {
                    sb.AppendLine("<color=#AAAAAA>Produkcja (zmiana):</color>");
                    foreach (var prod in data.productionPerCycle) sb.AppendLine($" <color=#88FF88>+{prod.amount} {prod.type}</color>");
                }
                if (data.upkeepPerCycle != null && data.upkeepPerCycle.Count > 0)
                {
                    sb.AppendLine("<color=#AAAAAA>Zużycie (zmiana):</color>");
                    foreach (var upkeep in data.upkeepPerCycle) sb.AppendLine($" <color=#FF8888>-{upkeep.amount} {upkeep.type}</color>");
                }

                // Teren
                if (data.allowedTerrain != null && data.allowedTerrain.Count == 1 && data.allowedTerrain[0] != HexFeatureType.None)
                {
                    sb.AppendLine($"\n<color=#AAAAAA>Wymaga podłoża:</color> <b>{data.allowedTerrain[0]}</b>");
                }
                
                // Bonusy z Terenu
                if (data.bonusRule.requiredFeature != HexFeatureType.None)
                {
                    sb.AppendLine($"\n<color=#AAAAAA>Bonus z terenu:</color> <b>{data.bonusRule.requiredFeature}</b>");
                    if (data.bonusRule.onTopProductionBonus > 0) 
                        sb.AppendLine($" <color=#88FF88>+{data.bonusRule.onTopProductionBonus} za postawienie na źródle</color>");
                    if (data.bonusRule.baseBonusPerHex > 0) 
                        sb.AppendLine($" <color=#88FF88>+{data.bonusRule.baseBonusPerHex} za każde źródło obok</color>");
                }
                break;

            case BuildingType.Utility:
            case BuildingType.Unique:
                // SPECJALNE BUDYNKI
                sb.AppendLine($"<i>{data.description}</i>\n");

                if (data.isOutpost)
                {
                    sb.AppendLine("<color=#FFD700>• Odblokowuje pełne budownictwo na chunku.</color>");
                }
                else if (data.prefab != null)
                {
                    if (data.prefab.GetComponent<ExpeditionCenterEntity>() != null)
                    {
                        sb.AppendLine("<color=#44AAFF>• Pozwala wysyłać zwiadowców w Mgłę Wojny.</color>");
                    }
                    else if (data.prefab.GetComponent<RuneForgeEntity>() != null)
                    {
                        sb.AppendLine("<color=#FFD700>• Pozwala umieszczać Runy w wieżach.</color>");
                        sb.AppendLine("<color=#AAAAAA>Zinfuzowane wieże w innych kuźniach:</color>");
                        
                        var forges = BuildingRegistry.Instance?.GetAllOfType<RuneForgeEntity>();
                        int count = 0;
                        if (forges != null)
                        {
                            foreach (var f in forges) {
                                if (f.infusedTowerData != null) {
                                    sb.AppendLine($" - {f.infusedTowerData.buildingName}");
                                    count++;
                                }
                            }
                        }
                        if (count == 0) sb.AppendLine(" - Brak");

                        int limit = Mathf.RoundToInt(MetaUpgradeManager.Instance?.GetValue(MetaEffectType.RuneTowerLimit) ?? 0);
                        sb.AppendLine($"\n<color=#AAAAAA>Twój limit Wież Runicznych:</color> <b>{limit}</b>");
                    }
                    else if (data.prefab.GetComponent<RuneTowerEntity>() != null)
                    {
                        sb.AppendLine("<color=#FFD700>• Odblokowuje dodatkowy slot w Kuźni Runicznej.</color>");
                        sb.AppendLine("<color=#FF4444>• Musi zostać zbudowana w przylegającym Heksie do Kuźni!</color>");
                    }
                }
                break;
        }

        tooltipBody.text = sb.ToString();
    }
}