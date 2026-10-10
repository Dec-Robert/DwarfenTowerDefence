using System;
using UnityEngine;

public class CenterInfoPanelControllerUI : MonoBehaviour
{
    private static CenterInfoPanelControllerUI _instance;
    public static CenterInfoPanelControllerUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CenterInfoPanelControllerUI>();
            }
            return _instance;
        }
        private set => _instance = value;
    }
    
    [Header("Panels")]
    public GameObject productionPanel;
    public GameObject housingPanel;
    public GameObject towerPanel;
    public GameObject runeForgePanel;
    public GameObject runeTowerSelectionModal;
    public GameObject hearthWindowPanel;
    
    void Awake()
    {
        _instance = this;

        if (towerPanel == null)
        {
            var pnl = transform.Find("Pnl_TowerContext");
            if (pnl != null) towerPanel = pnl.gameObject;
        }

        if (productionPanel == null)
        {
            var pnl = transform.Find("Pnl_ProductionContext");
            if (pnl != null) productionPanel = pnl.gameObject;
        }

        if (housingPanel == null)
        {
            var pnl = transform.Find("Pnl_HousingContext");
            if (pnl != null) housingPanel = pnl.gameObject;
        }

        if (runeForgePanel == null)
        {
            var pnl = transform.Find("Pnl_RuneForgeFullscreen");
            if (pnl != null) runeForgePanel = pnl.gameObject;
        }

        if (runeTowerSelectionModal == null)
        {
            var pnl = transform.Find("Pnl_RuneTowerSelectionModal");
            if (pnl != null) runeTowerSelectionModal = pnl.gameObject;
        }

        if (hearthWindowPanel == null)
        {
            var pnl = transform.Find("Pnl_HearthWindow");
            if (pnl != null) hearthWindowPanel = pnl.gameObject;
        }

        HideAll();
    }

    private void Start()
    {
        PrewarmPanels();
    }

    /// <summary>
    /// Wstępna inicjalizacja i przeliczenie układu UI obu paneli na początku gry,
    /// dzięki czemu przy pierwszym kliknięciu gracza panele renderują się natychmiast poprawnie.
    /// </summary>
    private void PrewarmPanels()
    {
        if (towerPanel != null)
        {
            towerPanel.SetActive(true);
            var towerUI = towerPanel.GetComponent<TowerContextWindowUI>();
            if (towerUI != null)
            {
                towerUI.AutoBindReferences();
                towerUI.ForceRebuildImmediate();
            }
        }

        if (productionPanel != null)
        {
            productionPanel.SetActive(true);
            var prodUI = productionPanel.GetComponent<ProductionContextWindowUI>();
            if (prodUI != null)
            {
                prodUI.AutoBindReferences();
                prodUI.ForceRebuildImmediate();
            }
        }

        if (housingPanel != null)
        {
            housingPanel.SetActive(true);
            var housingUI = housingPanel.GetComponent<HousingContextWindowUI>();
            if (housingUI != null)
            {
                housingUI.AutoBindReferences();
                housingUI.ForceRebuildImmediate();
            }
        }

        if (runeForgePanel != null)
        {
            runeForgePanel.SetActive(true);
            var runeForgeUI = runeForgePanel.GetComponent<RuneForgeWindowUI>();
            if (runeForgeUI == null) runeForgeUI = runeForgePanel.AddComponent<RuneForgeWindowUI>();
            if (runeForgeUI != null)
            {
                runeForgeUI.AutoBindReferences();
            }
        }

        if (runeTowerSelectionModal != null)
        {
            runeTowerSelectionModal.SetActive(true);
            var modalUI = runeTowerSelectionModal.GetComponent<RuneTowerSelectionModalUI>();
            if (modalUI == null) modalUI = runeTowerSelectionModal.AddComponent<RuneTowerSelectionModalUI>();
            if (modalUI != null)
            {
                modalUI.AutoBindReferences();
                modalUI.InitializeTowers();
            }
        }

        if (hearthWindowPanel != null)
        {
            hearthWindowPanel.SetActive(true);
            var hearthUI = hearthWindowPanel.GetComponent<HearthWindowUI>();
            if (hearthUI == null) hearthUI = hearthWindowPanel.AddComponent<HearthWindowUI>();
            if (hearthUI != null)
            {
                hearthUI.AutoBindReferences();
                hearthUI.ForceRebuildImmediate();
            }
        }

        Canvas.ForceUpdateCanvases();
        HideAll();
    }

    public void Setup(BuildingEntity buildingEntity)
    {
        HideAll();
        if (buildingEntity == null) return;

        if (buildingEntity is TowerEntity towerEntity)
        {
            if (towerPanel != null)
            {
                towerPanel.SetActive(true);
                var newContextUI = towerPanel.GetComponent<TowerContextWindowUI>();
                if (newContextUI != null)
                {
                    newContextUI.Setup(towerEntity);
                }
                else
                {
                    towerPanel.GetComponent<TowerInfoPanelUI>()?.Setup(towerEntity);
                }
            }
        }

        if (buildingEntity.data != null && buildingEntity.data.type == BuildingType.Economic)
        {
            if (productionPanel != null)
            {
                productionPanel.SetActive(true);
                var prodContextUI = productionPanel.GetComponent<ProductionContextWindowUI>();
                if (prodContextUI != null)
                {
                    prodContextUI.Setup(buildingEntity);
                }
                else
                {
                    productionPanel.GetComponent<ProductionInfoPanelUI>()?.Setup(buildingEntity);
                }
            }
        }

        if (buildingEntity is HousingEntity housingEntity)
        {
            if (housingPanel != null)
            {
                housingPanel.SetActive(true);
                var housingUI = housingPanel.GetComponent<HousingContextWindowUI>();
                if (housingUI != null)
                {
                    housingUI.Setup(housingEntity);
                }
            }
        }

        if (buildingEntity is RuneForgeEntity runeForgeEntity)
        {
            if (runeForgeEntity.infusedTowerData == null)
            {
                OpenRuneTowerSelectionModal(runeForgeEntity);
            }
            else
            {
                OpenRuneForge(runeForgeEntity);
            }
        }

        if (buildingEntity is BeaconEntity)
        {
            bool previewOnly = TimePhaseManager.Instance != null && TimePhaseManager.Instance.CurrentTimePhase != TimePhases.Dusk;
            OpenHearthWindow(previewOnly);
        }
    }

    public bool OpenHearthWindow(bool isPreviewOnly = false)
    {
        if (hearthWindowPanel == null)
        {
            var pnl = transform.Find("Pnl_HearthWindow");
            if (pnl != null) hearthWindowPanel = pnl.gameObject;
        }

        if (hearthWindowPanel == null) return false;

        HideAll();
        hearthWindowPanel.SetActive(true);
        var hearthUI = hearthWindowPanel.GetComponent<HearthWindowUI>();
        if (hearthUI == null) hearthUI = hearthWindowPanel.AddComponent<HearthWindowUI>();
        if (hearthUI != null)
        {
            hearthUI.Setup(isPreviewOnly);
        }
        return true;
    }

    public void OpenRuneForge(RuneForgeEntity forge)
    {
        HideAll();
        if (runeForgePanel != null)
        {
            runeForgePanel.SetActive(true);
            var runeForgeUI = runeForgePanel.GetComponent<RuneForgeWindowUI>();
            if (runeForgeUI == null) runeForgeUI = runeForgePanel.AddComponent<RuneForgeWindowUI>();
            if (runeForgeUI != null)
            {
                runeForgeUI.Setup(forge);
            }
        }
    }

    public void OpenRuneTowerSelectionModal(RuneForgeEntity forge)
    {
        HideAll();
        if (runeTowerSelectionModal != null)
        {
            runeTowerSelectionModal.SetActive(true);
            var modalUI = runeTowerSelectionModal.GetComponent<RuneTowerSelectionModalUI>();
            if (modalUI == null) modalUI = runeTowerSelectionModal.AddComponent<RuneTowerSelectionModalUI>();
            if (modalUI != null)
            {
                modalUI.Setup(forge);
            }
        }
    }
    
    public void HideAll()
    {
        if (productionPanel != null) productionPanel.SetActive(false);
        if (housingPanel != null) housingPanel.SetActive(false);
        if (towerPanel != null) towerPanel.SetActive(false);
        if (runeForgePanel != null) runeForgePanel.SetActive(false);
        if (runeTowerSelectionModal != null) runeTowerSelectionModal.SetActive(false);
        if (hearthWindowPanel != null) hearthWindowPanel.SetActive(false);
    }
}
