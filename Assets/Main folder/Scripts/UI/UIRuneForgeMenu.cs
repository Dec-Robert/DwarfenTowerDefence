using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class UIRuneForgeMenu : MonoBehaviour
{
    public static UIRuneForgeMenu Instance { get; private set; }

    [Header("Referencje")]
    public UIDocument uiDocument;
    
    // Elementy UI
    private VisualElement root;
    private VisualElement towerSelectScreen;
    private VisualElement mainForgeScreen;
    private VisualElement confirmPopup;

    private ScrollView towerGrid;
    private ScrollView inventoryGrid;
    private VisualElement forgeSlotsContainer;

    private Label lblForgeTarget, lblInfoTitle, lblInfoStats, lblPopupText;
    private Button btnFusion, btnTransmute, btnPopupYes, btnPopupNo;

    // Stan
    private RuneForgeEntity currentForge;
    private TowerData pendingTowerInfusion;
    
    private enum MenuMode { Normal, Fusion, Transmute }
    private MenuMode currentMode = MenuMode.Normal;
    private List<RuneItem> selectedRunesForAction = new List<RuneItem>();

    // Callbacki dla popupu
    private System.Action onConfirmAction;
    private System.Action onCancelAction;

    private void Awake() => Instance = this;

    private void OnEnable()
    {
        var docRoot = uiDocument.rootVisualElement;
        root = docRoot.Q<VisualElement>("ForgeRoot");

        towerSelectScreen = root.Q<VisualElement>("TowerSelectScreen");
        mainForgeScreen = root.Q<VisualElement>("MainForgeScreen");
        confirmPopup = root.Q<VisualElement>("ConfirmPopup");

        towerGrid = root.Q<ScrollView>("TowerGrid");
        inventoryGrid = root.Q<ScrollView>("InventoryGrid");
        forgeSlotsContainer = root.Q<VisualElement>("ForgeSlots");

        lblForgeTarget = root.Q<Label>("Lbl_ForgeTarget");
        lblInfoTitle = root.Q<Label>("Lbl_InfoTitle");
        lblInfoStats = root.Q<Label>("Lbl_InfoStats");
        lblPopupText = root.Q<Label>("Lbl_PopupText");

        btnFusion = root.Q<Button>("Btn_Fusion");
        btnTransmute = root.Q<Button>("Btn_Transmute");
        btnPopupYes = root.Q<Button>("Btn_PopupYes");
        btnPopupNo = root.Q<Button>("Btn_PopupNo");

        // Bindowanie przycisków
        root.Q<Button>("Btn_CloseTowerSelect").clicked += CloseMenu;
        root.Q<Button>("Btn_CloseForge").clicked += CloseMenu;

        btnFusion.clicked += () => ToggleMode(MenuMode.Fusion);
        btnTransmute.clicked += () => ToggleMode(MenuMode.Transmute);

        btnPopupYes.clicked += () => { confirmPopup.style.display = DisplayStyle.None; onConfirmAction?.Invoke(); };
        btnPopupNo.clicked += () => { confirmPopup.style.display = DisplayStyle.None; onCancelAction?.Invoke(); };
    }

    private void Start()
    {
        if (RuneManager.Instance != null)
            RuneManager.Instance.OnInventoryChanged += RefreshUI;
    }

    private void OnDestroy()
    {
        if (RuneManager.Instance != null)
            RuneManager.Instance.OnInventoryChanged -= RefreshUI;
    }

    // =========================================================================
    // API OTWIERANIA MENU
    // =========================================================================

    public void OpenMenu(RuneForgeEntity forge)
    {
        currentForge = forge;
        root.style.display = DisplayStyle.Flex;
        confirmPopup.style.display = DisplayStyle.None;
        ToggleMode(MenuMode.Normal);

        if (forge.infusedTowerData == null)
        {
            towerSelectScreen.style.display = DisplayStyle.Flex;
            mainForgeScreen.style.display = DisplayStyle.None;
            PopulateTowerSelection();
        }
        else
        {
            towerSelectScreen.style.display = DisplayStyle.None;
            mainForgeScreen.style.display = DisplayStyle.Flex;
            RefreshUI();
        }
    }

    public void CloseMenu()
    {
        root.style.display = DisplayStyle.None;
        currentForge = null;
        ToggleMode(MenuMode.Normal);
    }

    private void RefreshUI()
    {
        // Przerywamy, jeśli nie ma kuźni lub jeśli ekran główny kuźni jest ukryty
        if (currentForge == null || mainForgeScreen.style.display == DisplayStyle.None) return;

        // BŁĄD BYŁ TUTAJ: Zabezpieczamy przed odczytem nazwy, gdy wieża nie jest jeszcze wybrana!
        if (currentForge.infusedTowerData == null) return;

        lblForgeTarget.text = $"Zinfuzowano: {currentForge.infusedTowerData.buildingName}";
        PopulateInventory();
        PopulateForgeSlots();
    }

    // =========================================================================
    // EKRAN 1: WYBÓR WIEŻY
    // =========================================================================

    private void PopulateTowerSelection()
    {
        towerGrid.Clear();

        // Ustawienie ułożenia siatki (Grid) na wewnętrznym kontenerze ScrollView
        towerGrid.contentContainer.style.flexDirection = FlexDirection.Row;
        towerGrid.contentContainer.style.flexWrap = Wrap.Wrap;
        towerGrid.contentContainer.style.justifyContent = Justify.Center;

        if (UIBuildManager.Instance == null)
        {
            Debug.LogError("[RuneForgeUI] UIBuildManager.Instance jest NULL! Upewnij się, że skrypt UIBuildManager ma metodę Awake() z Instance = this;");
            return;
        }

        if (UIBuildManager.Instance.allBuildingsDatabase == null || UIBuildManager.Instance.allBuildingsDatabase.Count == 0)
        {
            Debug.LogError("[RuneForgeUI] Baza budynków (allBuildingsDatabase) w UIBuildManager jest pusta!");
            return;
        }

        int foundTowers = 0;

        foreach (var building in UIBuildManager.Instance.allBuildingsDatabase)
        {
            // Szukamy tylko wież
            if (building is TowerData towerData)
            {
                foundTowers++;

                Button btn = new Button();
                btn.AddToClassList("tower-btn");
                
                VisualElement icon = new VisualElement();
                icon.AddToClassList("tower-icon");
                if (towerData.icon != null) 
                {
                    icon.style.backgroundImage = new StyleBackground(towerData.icon);
                    // Opcjonalnie upewniamy się, że ikona się nie rozciągnie brzydko:
                    icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                }
                
                Label nameLbl = new Label(towerData.buildingName);
                nameLbl.style.color = Color.white;
                nameLbl.style.whiteSpace = WhiteSpace.Normal; // Pozwala zawijać długie nazwy
                nameLbl.style.unityTextAlign = TextAnchor.MiddleCenter;
                
                btn.Add(icon);
                btn.Add(nameLbl);

                btn.clicked += () => ShowPopup(
                    $"Czy na pewno chcesz, aby ta Kuźnia wzmacniała na stałe:\n<color=#ffaa00>{towerData.buildingName}</color>?",
                    () => { currentForge.SetInfusedTower(towerData); OpenMenu(currentForge); },
                    null);

                towerGrid.Add(btn);
            }
        }

        Debug.Log($"[RuneForgeUI] Znaleziono i wygenerowano {foundTowers} przycisków wież.");
    }

    // =========================================================================
    // EKRAN 2: GŁÓWNA KUŹNIA (Ekwipunek i Sloty)
    // =========================================================================

    private void PopulateInventory()
    {
        inventoryGrid.Clear();
        
        inventoryGrid.contentContainer.style.flexDirection = FlexDirection.Row;
        inventoryGrid.contentContainer.style.flexWrap = Wrap.Wrap;

        if (RuneManager.Instance == null) return;

        foreach (var rune in RuneManager.Instance.playerRunes)
        {
            VisualElement slot = CreateRuneElement(rune);
            
            // Podświetlenie jeśli runa jest wybrana do akcji
            if (selectedRunesForAction.Contains(rune)) 
            {
                slot.AddToClassList("rune-selected-for-fusion");
            }

            // --- NOWE: Sprawdzanie czy runa pasuje do wybranego trybu ---
            bool isSelectable = IsRuneSelectable(rune);

            if (!isSelectable)
            {
                slot.AddToClassList("rune-disabled"); // Nakładamy CSS z opacity: 0.25
            }
            
            // Kliknięcie działa tylko jeśli runa jest "selectable"
            slot.RegisterCallback<ClickEvent>(evt => 
            {
                if (isSelectable) OnInventoryRuneClicked(rune);
            });
            // -------------------------------------------------------------

            // Wyświetlanie statystyk po najechaniu zostawiamy włączone dla WSZYSTKICH run
            slot.RegisterCallback<MouseEnterEvent>(evt => ShowRuneInfo(rune));
            
            inventoryGrid.Add(slot);
        }
    }

    private void PopulateForgeSlots()
    {
        forgeSlotsContainer.Clear();
        int maxSlots = currentForge.GetMaxSlots();

        for (int i = 0; i < 4; i++) // Zawsze pokazujemy 4 pola, żeby gracz widział limit
        {
            if (i < maxSlots)
            {
                if (i < currentForge.equippedRunes.Count)
                {
                    // Slot z runą
                    RuneItem rune = currentForge.equippedRunes[i];
                    VisualElement slot = CreateRuneElement(rune);
                    slot.RegisterCallback<ClickEvent>(evt => currentForge.UnequipRune(rune));
                    slot.RegisterCallback<MouseEnterEvent>(evt => ShowRuneInfo(rune));
                    forgeSlotsContainer.Add(slot);
                }
                else
                {
                    // Slot pusty, ale odblokowany
                    VisualElement slot = new VisualElement();
                    slot.AddToClassList("rune-slot");
                    Label lbl = new Label("+"); lbl.style.color = new Color(0.3f,0.3f,0.3f);
                    slot.Add(lbl);
                    forgeSlotsContainer.Add(slot);
                }
            }
            else
            {
                // Slot zablokowany (wymaga Wieży Runicznej)
                VisualElement slot = new VisualElement();
                slot.AddToClassList("rune-slot");
                slot.AddToClassList("slot-locked");
                Label lbl = new Label("🔒"); lbl.style.color = new Color(0.5f,0.1f,0.1f);
                slot.Add(lbl);
                slot.tooltip = "Zbuduj Wieżę Runiczną obok Kuźni, aby odblokować ten slot.";
                forgeSlotsContainer.Add(slot);
            }
        }
    }

    private VisualElement CreateRuneElement(RuneItem rune)
    {
        VisualElement slot = new VisualElement();
        slot.AddToClassList("rune-slot");
        slot.AddToClassList($"rarity-{rune.definition.rarity}");

        if (rune.definition.icon != null)
        {
            slot.style.backgroundImage = new StyleBackground(rune.definition.icon);
        }
        else
        {
            Label tmpLabel = new Label(rune.definition.rarity.ToString().Substring(0, 1)); // np. "C" dla Common
            tmpLabel.style.color = Color.white;
            tmpLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            slot.Add(tmpLabel);
        }

        return slot;
    }

    // =========================================================================
    // LOGIKA KLIKNIĘĆ I TRYBÓW (Fuzja/Transmutacja)
    // =========================================================================

    private void ToggleMode(MenuMode newMode)
    {
        if (currentMode == newMode) newMode = MenuMode.Normal; // Odklikanie przycisku

        currentMode = newMode;
        selectedRunesForAction.Clear();

        btnFusion.RemoveFromClassList("btn-active");
        btnTransmute.RemoveFromClassList("btn-active");

        if (currentMode == MenuMode.Fusion) btnFusion.AddToClassList("btn-active");
        if (currentMode == MenuMode.Transmute) btnTransmute.AddToClassList("btn-active");

        RefreshUI();
    }

    private void OnInventoryRuneClicked(RuneItem rune)
    {
        if (currentMode == MenuMode.Normal)
        {
            // Zwykłe wkładanie runy do kuźni
            if (!currentForge.CanModifyRunes())
            {
                Debug.Log("<color=orange>Runy można zmieniać tylko w fazie dnia (i gdy przypisany jest Krasnolud)!</color>");
                return;
            }
            currentForge.EquipRune(rune);
            return;
        }

        // Tryby Akcji
        if (selectedRunesForAction.Contains(rune))
            selectedRunesForAction.Remove(rune);
        else
            selectedRunesForAction.Add(rune);

        RefreshUI();

        // Wyzwalacze akcji
        if (currentMode == MenuMode.Fusion && selectedRunesForAction.Count == 2)
        {
            ShowPopup("Czy chcesz dokonać fuzji 2 run w runę o wyższej rzadkości?", 
                () => { RuneManager.Instance.TryFuseRunes(selectedRunesForAction[0], selectedRunesForAction[1]); ToggleMode(MenuMode.Normal); },
                () => ToggleMode(MenuMode.Normal));
        }
        else if (currentMode == MenuMode.Transmute && selectedRunesForAction.Count == 3)
        {
            ShowPopup("Czy chcesz dokonać transmutacji 3 run w inną losową o wyższej rzadkości?", 
                () => { RuneManager.Instance.TryTransmuteRunes(selectedRunesForAction[0], selectedRunesForAction[1], selectedRunesForAction[2]); ToggleMode(MenuMode.Normal); },
                () => ToggleMode(MenuMode.Normal));
        }
    }
    
    private bool IsRuneSelectable(RuneItem rune)
    {
        // 1. Zawsze pozwalamy odkliknąć runę, która jest już zaznaczona!
        if (selectedRunesForAction.Contains(rune)) return true;

        // 2. W trybie normalnym można klikać wszystkie runy (wkładanie do kuźni)
        if (currentMode == MenuMode.Normal) return true;

        // 3. Przeklętych run (Cursed) nie można ani fuzjować, ani transmutować
        if (rune.definition.rarity == RuneRarity.Cursed) return false;

        // 4. Jeśli nic jeszcze nie zaznaczyliśmy, każda (nie-przeklęta) runa jest dobra
        if (selectedRunesForAction.Count == 0) return true;

        // 5. Mamy już wybraną pierwszą runę - musimy do niej pasować!
        RuneItem firstSelected = selectedRunesForAction[0];

        if (currentMode == MenuMode.Fusion)
        {
            // FUZJA: Runa musi mieć tę samą RZADKOŚĆ oraz tę samą GŁÓWNĄ STATYSTYKĘ
            return rune.definition.rarity == firstSelected.definition.rarity && 
                   rune.definition.primaryStat == firstSelected.definition.primaryStat;
        }
        else if (currentMode == MenuMode.Transmute)
        {
            // TRANSMUTACJA: Runa musi mieć tylko tę samą RZADKOŚĆ (statystyki mogą być różne)
            return rune.definition.rarity == firstSelected.definition.rarity;
        }

        return true;
    }

    // =========================================================================
    // POPUP I INFO
    // =========================================================================

    private void ShowPopup(string text, System.Action onYes, System.Action onNo)
    {
        lblPopupText.text = text;
        onConfirmAction = onYes;
        onCancelAction = onNo;
        confirmPopup.style.display = DisplayStyle.Flex;
    }

    private void ShowRuneInfo(RuneItem rune)
    {
        lblInfoTitle.text = rune.definition.runeName;
        string colorHex = rune.definition.rarity switch {
            RuneRarity.Common => "#FFFFFF", RuneRarity.Uncommon => "#44FF44",
            RuneRarity.Rare => "#44AAFF", RuneRarity.Legendary => "#FFD700", RuneRarity.Cursed => "#FF2222", _ => "#FFF"
        };

        lblInfoStats.text = $"<color={colorHex}>{rune.definition.rarity}</color>\n\n" +
                            $"{rune.GetPrimaryText()}\n" +
                            $"{rune.GetPenaltyText()}";
    }
}