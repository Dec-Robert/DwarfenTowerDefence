using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ExtendedBuildingUI : MonoBehaviour
{
    public static ExtendedBuildingUI Instance { get; private set; }

    [Header("Baza Dostępnych Budynków")]
    [SerializeField] private List<BuildingData> allBuildingsDatabase = new List<BuildingData>();

    [Header("Prefaby i Kontener")]
    [SerializeField] private GameObject buildingSlotPrefab;
    [SerializeField] private Transform buildingsContainer;

    [Header("Przyciski Zakładek (Wewnątrz Panelu)")]
    [SerializeField] private Button btnTabTowers;
    [SerializeField] private Button btnTabProduction;
    [SerializeField] private Button btnTabHousing;
    [SerializeField] private Button btnTabSpecial;
    [SerializeField] private Button btnClose;

    [Header("Przyciski Głównego HUD")]
    [SerializeField] private Button btnHudTower;
    [SerializeField] private Button btnHudProduction;
    [SerializeField] private Button btnHudHousing;
    [SerializeField] private Button btnHudSpecial;

    [Header("Ustawienia Widoczności")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private bool closeOnStart = true;
    [SerializeField] private BuildingType defaultCategory = BuildingType.Defense;

    [Header("Stylizacja Zakładek")]
    [SerializeField] private Color activeTabColor = Color.white;
    [SerializeField] private Color inactiveTabColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);

    private class BuildingSlotEntry
    {
        public GameObject gameObject;
        public BuildingSlotUI slotUI;
        public BuildingData data;

        public BuildingSlotEntry(GameObject go, BuildingSlotUI ui, BuildingData data)
        {
            this.gameObject = go;
            this.slotUI = ui;
            this.data = data;
        }
    }

    private readonly List<BuildingSlotEntry> slotEntries = new();
    private BuildingType currentCategory = BuildingType.Defense;
    private bool isOpen = false;

    public bool IsOpen => isOpen;
    public BuildingType CurrentCategory => currentCategory;

    private void Awake()
    {
        Instance = this;
        if (panelRoot == null) panelRoot = gameObject;

        RegisterTabButtons();
        RegisterHudButtons();
    }

    private void Start()
    {
        GenerateBuildingSlots();

        if (closeOnStart)
        {
            CloseWindow();
        }
        else
        {
            OpenCategory(defaultCategory);
        }
    }

    private void Update()
    {
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseWindow();
        }
    }

    private void OnDestroy()
    {
        UnregisterButtons();
    }

    public List<BuildingData> AllBuildingsDatabase => allBuildingsDatabase;

    public void GenerateBuildingSlots()
    {
        ClearContainer(buildingsContainer);
        slotEntries.Clear();

        if (buildingSlotPrefab == null || buildingsContainer == null) return;

        foreach (var building in allBuildingsDatabase)
        {
            if (building == null || !building.isUnlocked) continue;

            GameObject slotObj = Instantiate(buildingSlotPrefab, buildingsContainer);
            slotObj.SetActive(false);

            BuildingSlotUI slotUI = slotObj.GetComponent<BuildingSlotUI>();
            if (slotUI != null)
            {
                slotUI.SetUpPanel(building);
            }

            slotEntries.Add(new BuildingSlotEntry(slotObj, slotUI, building));
        }
    }

    public void RefreshAvailableBuildings()
    {
        if (!Application.isPlaying) return;

        GenerateBuildingSlots();
        if (isOpen)
        {
            FilterSlots(currentCategory);
        }
    }

    public void OpenCategory(BuildingType category)
    {
        currentCategory = category;
        if (panelRoot != null) panelRoot.SetActive(true);
        isOpen = true;

        FilterSlots(category);
        UpdateTabVisuals(category);
    }

    public void OnHudCategoryClicked(BuildingType category)
    {
        // Jeśli okno jest otwarte i kliknięto tę samą kategorię -> nic nie rób
        if (isOpen && currentCategory == category)
        {
            return;
        }

        OpenCategory(category);
    }

    public void CloseWindow()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        isOpen = false;
        InteractionManager.Instance?.CancelBuilding();
    }

    public void SetBuildingsDatabase(List<BuildingData> buildings)
    {
        allBuildingsDatabase = buildings ?? new List<BuildingData>();
        GenerateBuildingSlots();
        if (isOpen)
        {
            FilterSlots(currentCategory);
        }
    }

    private void FilterSlots(BuildingType category)
    {
        foreach (var entry in slotEntries)
        {
            if (entry.gameObject == null || entry.data == null) continue;

            if (!entry.data.isUnlocked)
            {
                entry.gameObject.SetActive(false);
                continue;
            }

            bool isMatch;
            if (category is BuildingType.Unique or BuildingType.Utility)
            {
                isMatch = (entry.data.type is BuildingType.Unique or BuildingType.Utility);
            }
            else
            {
                isMatch = (entry.data.type == category);
            }

            entry.gameObject.SetActive(isMatch);
        }
    }

    private void UpdateTabVisuals(BuildingType activeCategory)
    {
        SetTabVisual(btnTabTowers, activeCategory == BuildingType.Defense);
        SetTabVisual(btnTabProduction, activeCategory == BuildingType.Economic);
        SetTabVisual(btnTabHousing, activeCategory == BuildingType.Housing);
        SetTabVisual(btnTabSpecial, activeCategory is BuildingType.Unique or BuildingType.Utility);
    }

    private void SetTabVisual(Button btn, bool isActive)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = isActive ? activeTabColor : inactiveTabColor;
        }
    }

    private void RegisterTabButtons()
    {
        if (btnTabTowers != null) btnTabTowers.onClick.AddListener(() => OpenCategory(BuildingType.Defense));
        if (btnTabProduction != null) btnTabProduction.onClick.AddListener(() => OpenCategory(BuildingType.Economic));
        if (btnTabHousing != null) btnTabHousing.onClick.AddListener(() => OpenCategory(BuildingType.Housing));
        if (btnTabSpecial != null) btnTabSpecial.onClick.AddListener(() => OpenCategory(BuildingType.Unique));
        if (btnClose != null) btnClose.onClick.AddListener(CloseWindow);
    }

    private void RegisterHudButtons()
    {
        if (btnHudTower != null) btnHudTower.onClick.AddListener(() => OnHudCategoryClicked(BuildingType.Defense));
        if (btnHudProduction != null) btnHudProduction.onClick.AddListener(() => OnHudCategoryClicked(BuildingType.Economic));
        if (btnHudHousing != null) btnHudHousing.onClick.AddListener(() => OnHudCategoryClicked(BuildingType.Housing));
        if (btnHudSpecial != null) btnHudSpecial.onClick.AddListener(() => OnHudCategoryClicked(BuildingType.Unique));
    }

    private void UnregisterButtons()
    {
        if (btnTabTowers != null) btnTabTowers.onClick.RemoveAllListeners();
        if (btnTabProduction != null) btnTabProduction.onClick.RemoveAllListeners();
        if (btnTabHousing != null) btnTabHousing.onClick.RemoveAllListeners();
        if (btnTabSpecial != null) btnTabSpecial.onClick.RemoveAllListeners();
        if (btnClose != null) btnClose.onClick.RemoveAllListeners();

        if (btnHudTower != null) btnHudTower.onClick.RemoveAllListeners();
        if (btnHudProduction != null) btnHudProduction.onClick.RemoveAllListeners();
        if (btnHudHousing != null) btnHudHousing.onClick.RemoveAllListeners();
        if (btnHudSpecial != null) btnHudSpecial.onClick.RemoveAllListeners();
    }

    private void ClearContainer(Transform container)
    {
        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Transform child = container.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }
}
