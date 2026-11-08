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

    [Header("Ustawienia Widoczności i Pozycji")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private float hiddenY = -184f;
    [SerializeField] private float shownY = 0f;
    [SerializeField] private float slideSpeed = 16f;
    [SerializeField] private bool closeOnStart = true;
    [SerializeField] private BuildingType defaultCategory = BuildingType.Defense;

    [Header("Stylizacja Zakładek")]
    [SerializeField] private Color activeTabColor = new Color(0.95f, 0.72f, 0.22f, 1.0f); // Złotawy kolor
    [SerializeField] private Color inactiveTabColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);

    [Header("Debug")]
    [Tooltip("Gdy włączone, wszystkie wieże są widoczne i dostępne niezależnie od stanu odblokowania.")]
    [SerializeField] private bool unlockAllTowersForDebug = true;

    public bool UnlockAllTowersForDebug
    {
        get => unlockAllTowersForDebug;
        set => unlockAllTowersForDebug = value;
    }

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
    private float targetY = -184f;

    public bool IsOpen => isOpen;
    public BuildingType CurrentCategory => currentCategory;

    private void Awake()
    {
        Instance = this;
        if (panelRoot == null) panelRoot = gameObject;
        if (panelRect == null) panelRect = panelRoot.GetComponent<RectTransform>() ?? GetComponent<RectTransform>();

        targetY = hiddenY;

        RegisterTabButtons();
        RegisterHudButtons();
    }

    private void Start()
    {
        EnsureAllTowersInDatabase();
        GenerateBuildingSlots();

        if (panelRoot != null && !panelRoot.activeSelf)
        {
            panelRoot.SetActive(true);
        }

        if (panelRect != null)
        {
            panelRect.anchoredPosition = new Vector2(panelRect.anchoredPosition.x, hiddenY);
        }

        targetY = hiddenY;
        isOpen = false;
        UpdateTabVisuals(null);
    }

    public void EnsureAllTowersInDatabase()
    {
        if (allBuildingsDatabase == null)
        {
            allBuildingsDatabase = new List<BuildingData>();
        }

        // 1. Zsynchronizuj z SaveManagerem, jeśli jest dostępny
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.EnsureTowersLoaded();
            if (SaveManager.Instance.allTowers != null)
            {
                foreach (var tower in SaveManager.Instance.allTowers)
                {
                    if (tower != null && !allBuildingsDatabase.Contains(tower))
                    {
                        allBuildingsDatabase.Add(tower);
                    }
                }
            }
        }
#if UNITY_EDITOR
        // 2. W edytorze jako zabezpieczenie wyszukaj wszystkie ScriptableObjecty typu TowerData
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:TowerData");
        foreach (string guid in guids)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            TowerData tower = UnityEditor.AssetDatabase.LoadAssetAtPath<TowerData>(assetPath);
            if (tower != null && !allBuildingsDatabase.Contains(tower))
            {
                allBuildingsDatabase.Add(tower);
            }
        }
#endif

        if (SaveManager.Instance != null && allBuildingsDatabase != null)
        {
            List<TowerData> towers = new List<TowerData>();
            foreach (var b in allBuildingsDatabase)
            {
                if (b is TowerData td) towers.Add(td);
            }
            if (towers.Count > 0)
            {
                SaveManager.Instance.SyncTowersWithSave(towers);
            }
        }
    }

    private void Update()
    {
        // Płynna animacja wysuwania / chowania panelu
        if (panelRect != null && Mathf.Abs(panelRect.anchoredPosition.y - targetY) > 0.05f)
        {
            float newY = Mathf.Lerp(panelRect.anchoredPosition.y, targetY, Time.unscaledDeltaTime * slideSpeed);
            if (Mathf.Abs(newY - targetY) < 0.2f)
            {
                newY = targetY;
            }
            panelRect.anchoredPosition = new Vector2(panelRect.anchoredPosition.x, newY);
        }

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

        bool debugUnlockAllTowers = unlockAllTowersForDebug || (SaveManager.Instance != null && SaveManager.Instance.unlockAllTowersForDebug);

        foreach (var building in allBuildingsDatabase)
        {
            if (building == null) continue;

            bool isUnlocked = building.isUnlocked || (debugUnlockAllTowers && building is TowerData);
            if (!isUnlocked) continue;

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
        isOpen = true;
        targetY = shownY;
        if (panelRoot != null && !panelRoot.activeSelf) panelRoot.SetActive(true);

        FilterSlots(category);
        UpdateTabVisuals(category);
    }

    public void OnCategoryTabClicked(BuildingType category)
    {
        // Kliknięcie w aktualnie wybraną kategorię chowa menu
        if (isOpen && currentCategory == category)
        {
            CloseWindow();
        }
        else
        {
            OpenCategory(category);
        }
    }

    public void OnHudCategoryClicked(BuildingType category)
    {
        OnCategoryTabClicked(category);
    }

    public void CloseWindow()
    {
        isOpen = false;
        targetY = hiddenY;
        UpdateTabVisuals(null);
        InteractionManager.Instance?.CancelBuilding();
    }

    public void SetBuildingsDatabase(List<BuildingData> buildings)
    {
        allBuildingsDatabase = buildings ?? new List<BuildingData>();
        if (SaveManager.Instance != null && allBuildingsDatabase != null)
        {
            List<TowerData> towers = new List<TowerData>();
            foreach (var b in allBuildingsDatabase)
            {
                if (b is TowerData td) towers.Add(td);
            }
            if (towers.Count > 0)
            {
                SaveManager.Instance.SyncTowersWithSave(towers);
            }
        }
        GenerateBuildingSlots();
        if (isOpen)
        {
            FilterSlots(currentCategory);
        }
    }

    private void FilterSlots(BuildingType category)
    {
        bool debugUnlockAllTowers = unlockAllTowersForDebug || (SaveManager.Instance != null && SaveManager.Instance.unlockAllTowersForDebug);

        foreach (var entry in slotEntries)
        {
            if (entry.gameObject == null || entry.data == null) continue;

            bool isUnlocked = entry.data.isUnlocked || (debugUnlockAllTowers && entry.data is TowerData);
            if (!isUnlocked)
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
            if (isMatch && entry.slotUI != null)
            {
                entry.slotUI.UpdateAffordability();
            }
        }
    }

    private void UpdateTabVisuals(BuildingType? activeCategory)
    {
        bool hasActive = activeCategory.HasValue && isOpen;
        BuildingType cat = activeCategory.GetValueOrDefault();

        SetTabVisual(btnTabTowers, hasActive && cat == BuildingType.Defense);
        SetTabVisual(btnTabProduction, hasActive && cat == BuildingType.Economic);
        SetTabVisual(btnTabHousing, hasActive && cat == BuildingType.Housing);
        SetTabVisual(btnTabSpecial, hasActive && (cat is BuildingType.Unique or BuildingType.Utility));

        SetTabVisual(btnHudTower, hasActive && cat == BuildingType.Defense);
        SetTabVisual(btnHudProduction, hasActive && cat == BuildingType.Economic);
        SetTabVisual(btnHudHousing, hasActive && cat == BuildingType.Housing);
        SetTabVisual(btnHudSpecial, hasActive && (cat is BuildingType.Unique or BuildingType.Utility));
    }

    private void SetTabVisual(Button btn, bool isActive)
    {
        if (btn == null) return;
        Color targetColor = isActive ? activeTabColor : inactiveTabColor;

        Image img = (btn.targetGraphic as Image) ?? btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = targetColor;
        }

        var colors = btn.colors;
        colors.normalColor = targetColor;
        colors.selectedColor = targetColor;
        btn.colors = colors;
    }

    private void RegisterTabButtons()
    {
        if (btnTabTowers != null) btnTabTowers.onClick.AddListener(() => OnCategoryTabClicked(BuildingType.Defense));
        if (btnTabProduction != null) btnTabProduction.onClick.AddListener(() => OnCategoryTabClicked(BuildingType.Economic));
        if (btnTabHousing != null) btnTabHousing.onClick.AddListener(() => OnCategoryTabClicked(BuildingType.Housing));
        if (btnTabSpecial != null) btnTabSpecial.onClick.AddListener(() => OnCategoryTabClicked(BuildingType.Unique));
        if (btnClose != null) btnClose.onClick.AddListener(CloseWindow);
    }

    private void RegisterHudButtons()
    {
        if (btnHudTower != null) btnHudTower.onClick.AddListener(() => OnCategoryTabClicked(BuildingType.Defense));
        if (btnHudProduction != null) btnHudProduction.onClick.AddListener(() => OnCategoryTabClicked(BuildingType.Economic));
        if (btnHudHousing != null) btnHudHousing.onClick.AddListener(() => OnCategoryTabClicked(BuildingType.Housing));
        if (btnHudSpecial != null) btnHudSpecial.onClick.AddListener(() => OnCategoryTabClicked(BuildingType.Unique));
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
