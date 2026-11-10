using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kontroler okna Meta-Progresji (Pnl_MetaUpgrade) w Menu Głównym.
/// Obsługuje:
/// - Podział na zakładki Legacy (Legacy of Town - aktywne; Legacy of Power i Defence - wkrótce dostępne)
/// - Wyświetlanie listy ulepszeń w ScrollView z ukrywaniem zablokowanych (brak spełnionych prerequisites)
/// - Przenoszenie w pełni odblokowanych ulepszeń na sam dół listy
/// - Prezentację stanu waluty Nadziei w Txt_hope
/// - Integrację z mechaniką Hold-to-Invest na kafelkach MetaUpgradeItemUI
/// - Wywołanie TowerDraftModalUI w przypadku ulepszeń odblokowujących wybór nowej wieży
/// - Zamknięcie klawiszem ESC oraz dedykowanym przyciskiem
/// - Nienaruszanie czcionek i ich rozmiarów ze sceny (zgodnie z wytycznymi użytkownika)
/// </summary>
public class MetaGraphControllerUI : MonoBehaviour
{
    public static MetaGraphControllerUI Instance { get; private set; }

    [Header("Referencje Główne")]
    [SerializeField] private GameObject graphRoot;
    [SerializeField] private TextMeshProUGUI txtGraphTitle;
    [SerializeField] private TextMeshProUGUI txtHopeCurrency;
    [SerializeField] private Button btnClose;

    [Header("Zakładki Legacy")]
    [SerializeField] private Button tabTown;
    [SerializeField] private Button tabPower;
    [SerializeField] private Button tabDefence;

    [Header("Kontener ScrollView")]
    [SerializeField] private Transform contentContainer;
    [SerializeField] private GameObject itemTemplate;

    [Header("Katalog Ulepszeń")]
    [SerializeField] private List<MetaUpgradeSO> allUpgrades = new List<MetaUpgradeSO>();

    private readonly List<MetaUpgradeItemUI> spawnedItems = new List<MetaUpgradeItemUI>();
    private bool isInitialized = false;

    public bool IsOpen => graphRoot != null && graphRoot.activeSelf;
    public List<MetaUpgradeSO> AllUpgrades => allUpgrades;
    public TextMeshProUGUI TxtHopeCurrency => txtHopeCurrency;
    public Button BtnClose => btnClose;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        FindReferencesIfNull();
    }

    private void Start()
    {
        FindReferencesIfNull();
        SetupTabs();
        SetupCloseButton();
        EnsureUpgradesLoaded();

        if (graphRoot != null)
        {
            graphRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && IsOpen)
        {
            CloseGraph();
        }
    }

    public static MetaGraphControllerUI GetOrCreateInstance()
    {
        if (Instance != null) return Instance;

        Instance = FindFirstObjectByType<MetaGraphControllerUI>(FindObjectsInactive.Include);
        if (Instance != null) return Instance;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        Transform metaTr = canvas != null ? canvas.transform.Find("Pnl_MetaUpgrade") : null;
        if (metaTr != null)
        {
            Instance = metaTr.GetComponent<MetaGraphControllerUI>() ?? metaTr.gameObject.AddComponent<MetaGraphControllerUI>();
            return Instance;
        }

        GameObject go = new GameObject("Pnl_MetaUpgrade", typeof(RectTransform));
        if (canvas != null) go.transform.SetParent(canvas.transform, false);
        Instance = go.AddComponent<MetaGraphControllerUI>();
        return Instance;
    }

    public void FindReferencesIfNull()
    {
        if (graphRoot == null)
        {
            if (name.Contains("MetaUpgrade") || name.Contains("Pnl_Meta"))
            {
                graphRoot = gameObject;
            }
            else
            {
                Transform t = transform.Find("Pnl_MetaUpgrade") ?? FindFirstObjectByType<Canvas>()?.transform.Find("Pnl_MetaUpgrade");
                if (t != null) graphRoot = t.gameObject;
                else graphRoot = gameObject;
            }
        }

        Transform rootTr = graphRoot != null ? graphRoot.transform : transform;

        if (txtGraphTitle == null)
        {
            Transform t = rootTr.Find("Txt_GameName");
            if (t != null) txtGraphTitle = t.GetComponent<TextMeshProUGUI>();
        }

        if (txtHopeCurrency == null)
        {
            Transform t = rootTr.Find("Txt_hope");
            if (t != null) txtHopeCurrency = t.GetComponent<TextMeshProUGUI>();
        }

        if (btnClose == null)
        {
            // Przycisk powrotu / zamknięcia
            Button[] btns = rootTr.GetComponentsInChildren<Button>(true);
            foreach (var b in btns)
            {
                if (b.name.Contains("Close") || b.name.Contains("Back") || b.name.Contains("Return") || b.transform.parent == rootTr)
                {
                    if (b != tabTown && b != tabPower && b != tabDefence)
                    {
                        btnClose = b;
                        break;
                    }
                }
            }
        }

        Transform tabsTr = rootTr.Find("Pnl_LegacyTabs/Pnl_Tabs");
        if (tabsTr != null)
        {
            if (tabTown == null) tabTown = tabsTr.Find("Pnl_Tab1")?.GetComponent<Button>();
            if (tabPower == null) tabPower = tabsTr.Find("Pnl_Tab2")?.GetComponent<Button>();
            if (tabDefence == null) tabDefence = tabsTr.Find("Pnl_Tab3")?.GetComponent<Button>();
        }

        if (contentContainer == null)
        {
            Transform cTr = rootTr.Find("Pnl_LegacyTabs/Content/Scroll View/Viewport/Content");
            if (cTr != null) contentContainer = cTr;
        }

        if (contentContainer != null && itemTemplate == null)
        {
            Transform t = contentContainer.Find("Pnl_MetaUpgrade");
            if (t != null) itemTemplate = t.gameObject;
        }
    }

    private void SetupTabs()
    {
        if (tabTown != null)
        {
            tabTown.interactable = true;
            tabTown.onClick.RemoveAllListeners();
            tabTown.onClick.AddListener(SelectTabTown);
        }

        // Zgodnie z ustaleniami: Tab 2 i Tab 3 są zablokowane (wkrótce dostępne)
        if (tabPower != null)
        {
            tabPower.interactable = false;
            DimTabVisual(tabPower);
        }

        if (tabDefence != null)
        {
            tabDefence.interactable = false;
            DimTabVisual(tabDefence);
        }
    }

    private void DimTabVisual(Button btn)
    {
        if (btn == null) return;
        CanvasGroup cg = btn.GetComponent<CanvasGroup>();
        if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0.45f;
    }

    private void SetupCloseButton()
    {
        if (btnClose != null)
        {
            btnClose.onClick.RemoveAllListeners();
            btnClose.onClick.AddListener(CloseGraph);
        }
    }

    private void SelectTabTown()
    {
        RefreshUpgradesList();
    }

    public void OpenGraph()
    {
        FindReferencesIfNull();
        EnsureUpgradesLoaded();

        if (graphRoot != null)
        {
            graphRoot.SetActive(true);
        }

        RefreshHopeUI();
        RefreshUpgradesList();
    }

    public void CloseGraph()
    {
        if (graphRoot != null)
        {
            graphRoot.SetActive(false);
        }
    }

    public void RefreshHopeUI()
    {
        if (txtHopeCurrency != null)
        {
            int hope = 0;
            if (SaveManager.Instance != null && SaveManager.Instance.currentSaveData != null)
            {
                hope = SaveManager.Instance.currentSaveData.totalHope;
            }
            txtHopeCurrency.text = $"{hope} Nadziei";
        }
    }

    private void EnsureUpgradesLoaded()
    {
        if (allUpgrades == null || allUpgrades.Count == 0)
        {
            if (MetaUpgradeManager.Instance != null && MetaUpgradeManager.Instance.allUpgrades != null && MetaUpgradeManager.Instance.allUpgrades.Count > 0)
            {
                allUpgrades = new List<MetaUpgradeSO>(MetaUpgradeManager.Instance.allUpgrades);
            }
            else
            {
                MetaUpgradeManager mgr = FindFirstObjectByType<MetaUpgradeManager>();
                if (mgr != null && mgr.allUpgrades != null && mgr.allUpgrades.Count > 0)
                {
                    allUpgrades = new List<MetaUpgradeSO>(mgr.allUpgrades);
                }
                else
                {
                    allUpgrades = new List<MetaUpgradeSO>(Resources.LoadAll<MetaUpgradeSO>(""));
                }
            }
        }
    }

    public void RefreshUpgradesList()
    {
        FindReferencesIfNull();
        EnsureUpgradesLoaded();

        if (contentContainer == null || itemTemplate == null) return;

        // Upewnij się, że template ma MetaUpgradeItemUI i jest ukryty
        MetaUpgradeItemUI templateItem = itemTemplate.GetComponent<MetaUpgradeItemUI>();
        if (templateItem == null)
        {
            templateItem = itemTemplate.AddComponent<MetaUpgradeItemUI>();
        }
        itemTemplate.SetActive(false);

        // Usuń stare placeholderowe rodzeństwo ze sceny (np. Pnl_MetaUpgrade (1) do (4)) jeśli nie mają poprawnego setupu
        for (int i = contentContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = contentContainer.GetChild(i);
            if (child.gameObject == itemTemplate) continue;

            MetaUpgradeItemUI itemUI = child.GetComponent<MetaUpgradeItemUI>();
            if (itemUI == null || !spawnedItems.Contains(itemUI))
            {
                Destroy(child.gameObject);
            }
        }

        // 1. Filtrowanie ulepszeń dla Legacy of Town:
        // - Brak ulepszeń typu A/B
        // - Brak ulepszeń, których prerequisites nie zostały jeszcze spełnione
        List<MetaUpgradeSO> eligibleUpgrades = new List<MetaUpgradeSO>();
        foreach (var u in allUpgrades)
        {
            if (u == null) continue;

            // Wyklucz warianty A/B zgodnie z ustaleniami
            if (u.hasVariants || u.isVariantOption) continue;

            // Sprawdź czy ulepszenie jest już odblokowane w SaveManager
            if (SaveManager.Instance != null && SaveManager.Instance.currentSaveData != null)
            {
                if (SaveManager.Instance.currentSaveData.unlockedUpgradeIDs.Contains(u.id))
                {
                    u.isUnlocked = true;
                }
            }

            // Jeśli ulepszenie nie jest odblokowane, sprawdź czy jego prerequisites są spełnione
            if (!u.isUnlocked && !ArePrerequisitesMet(u))
            {
                // Ukryj zablokowane ulepszenia
                continue;
            }

            eligibleUpgrades.Add(u);
        }

        // 2. Sortowanie:
        // - Dostępne / w trakcie rozwijania (nieodblokowane) na górze listy
        // - W pełni odblokowane ulepszenia na sam dół listy
        eligibleUpgrades.Sort((a, b) =>
        {
            int unlockedA = a.isUnlocked ? 1 : 0;
            int unlockedB = b.isUnlocked ? 1 : 0;
            if (unlockedA != unlockedB)
            {
                return unlockedA.CompareTo(unlockedB);
            }
            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
        });

        // 3. Dopasowanie liczby obiektów w puli
        while (spawnedItems.Count < eligibleUpgrades.Count)
        {
            GameObject newObj = Instantiate(itemTemplate, contentContainer);
            MetaUpgradeItemUI newItem = newObj.GetComponent<MetaUpgradeItemUI>() ?? newObj.AddComponent<MetaUpgradeItemUI>();
            spawnedItems.Add(newItem);
        }

        for (int i = 0; i < spawnedItems.Count; i++)
        {
            if (i < eligibleUpgrades.Count)
            {
                spawnedItems[i].gameObject.SetActive(true);
                spawnedItems[i].transform.SetSiblingIndex(i + 1); // po template
                spawnedItems[i].Setup(eligibleUpgrades[i], OnUpgradeUnlocked, RefreshHopeUI);
            }
            else
            {
                spawnedItems[i].gameObject.SetActive(false);
            }
        }
    }

    private bool ArePrerequisitesMet(MetaUpgradeSO upgrade)
    {
        if (upgrade == null || upgrade.prerequisites == null || upgrade.prerequisites.Count == 0)
        {
            return true;
        }

        foreach (var req in upgrade.prerequisites)
        {
            if (req == null) continue;

            bool isReqUnlocked = req.isUnlocked;
            if (!isReqUnlocked && SaveManager.Instance != null && SaveManager.Instance.currentSaveData != null)
            {
                isReqUnlocked = SaveManager.Instance.currentSaveData.unlockedUpgradeIDs.Contains(req.id);
            }

            if (!isReqUnlocked)
            {
                return false;
            }
        }

        return true;
    }

    private void OnUpgradeUnlocked(MetaUpgradeSO unlockedUpgrade)
    {
        RefreshHopeUI();
        RefreshUpgradesList();
    }

    /// <summary>
    /// Metoda pomocnicza dla kompatybilności wstecznej (np. DebugCheatManager).
    /// </summary>
    public void UpdateHopeCurrency()
    {
        RefreshHopeUI();
    }

    /// <summary>
    /// Metoda pomocnicza dla kompatybilności wstecznej (np. DebugCheatManager).
    /// </summary>
    public void RefreshGraph()
    {
        RefreshUpgradesList();
    }

    /// <summary>
    /// Metoda pomocnicza dla kompatybilności wstecznej.
    /// </summary>
    public void EnsureHierarchyBuilt()
    {
        FindReferencesIfNull();
    }

    /// <summary>
    /// Inicjalizuje listę ulepszeń z podanej listy.
    /// </summary>
    public void InitializeGraph(List<MetaUpgradeSO> upgrades)
    {
        allUpgrades = upgrades != null ? new List<MetaUpgradeSO>(upgrades) : new List<MetaUpgradeSO>();
        RefreshUpgradesList();
    }
}
