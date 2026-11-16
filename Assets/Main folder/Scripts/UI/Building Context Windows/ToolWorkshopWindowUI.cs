using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ToolWorkshopWindowUI : MonoBehaviour
{
    [Serializable]
    public class TechOptionSlotBinding
    {
        public string buildingKey;
        public int tier;
        public bool isOptionA;
        public Button button;
        public TMP_Text titleText;
        public TMP_Text statusText;
        public Image background;
    }

    [Header("Naglowek")]
    [SerializeField] private TMP_Text txtTitle;
    [SerializeField] private Button btnClose;

    [Header("Zaloga Warsztatu")]
    [SerializeField] private TMP_Text txtWorkersStatus;
    [SerializeField] private Button btnAddWorker;
    [SerializeField] private Button btnRemoveWorker;

    [Header("Status Badan Globalnych")]
    [SerializeField] private TMP_Text txtResearchStatus;
    [SerializeField] private Button btnCancelResearch;
    [SerializeField] private TMP_Text txtUpgradeDetails;

    [Header("Sloty Drzewka Technologii")]
    [SerializeField] private List<TechOptionSlotBinding> techOptionSlots = new List<TechOptionSlotBinding>();

    private ToolWorkshopEntity currentWorkshop;
    private bool listenersInitialized;

    private static readonly Color ColorAvailable = new Color(0.22f, 0.27f, 0.34f, 0.95f);
    private static readonly Color ColorResearched = new Color(0.18f, 0.45f, 0.24f, 0.98f);
    private static readonly Color ColorActiveResearch = new Color(0.55f, 0.42f, 0.14f, 0.98f);
    private static readonly Color ColorLocked = new Color(0.16f, 0.16f, 0.18f, 0.70f);

    private void OnEnable()
    {
        if (ToolWorkshopResearchManager.Instance != null)
        {
            ToolWorkshopResearchManager.Instance.OnResearchStateChanged += Refresh;
        }
    }

    private void OnDisable()
    {
        if (ToolWorkshopResearchManager.Instance != null)
        {
            ToolWorkshopResearchManager.Instance.OnResearchStateChanged -= Refresh;
        }
    }

    public void Setup(ToolWorkshopEntity workshop)
    {
        currentWorkshop = workshop;
        AutoBindIfNeeded();
        InitializeListeners();

        if (txtUpgradeDetails != null)
        {
            txtUpgradeDetails.text = "Wybierz ulepszenie z drzewka ponizej, aby rozpoczac badania (najedz kursorem po opis).";
        }

        Refresh();
    }

    public void Refresh()
    {
        AutoBindIfNeeded();

        var mgr = ToolWorkshopResearchManager.Instance;
        if (mgr != null) mgr.EnsureCatalogLoaded();

        if (txtTitle != null)
        {
            txtTitle.text = currentWorkshop != null && currentWorkshop.data != null
                ? currentWorkshop.data.DisplayName
                : "Warsztat narzedziowy";
        }

        RefreshWorkersSection();
        RefreshResearchStatusSection(mgr);
        RefreshTechSlots(mgr);
    }

    private void RefreshWorkersSection()
    {
        int workers = currentWorkshop != null && currentWorkshop.Workers != null
            ? currentWorkshop.Workers.GetWorkerCount(Race.Humans)
            : 0;
        int maxWorkers = ToolWorkshopEntity.RequiredWorkersForResearch;
        bool isOperational = currentWorkshop != null && currentWorkshop.IsContributingToResearch;
        bool canModify = currentWorkshop != null &&
                         (TimePhaseManager.Instance == null || TimePhaseManager.Instance.CurrentTimePhase != TimePhases.Night);

        if (txtWorkersStatus != null)
        {
            string stateLabel = isOperational ? "<color=#7EE081>Aktywny (3/3)</color>" : "<color=#E07A7A>Nieaktywny (wymaga 3 Ludzi)</color>";
            txtWorkersStatus.text = $"Zaloga (Ludzie): {workers} / {maxWorkers}  |  {stateLabel}";
        }

        if (btnAddWorker != null)
        {
            btnAddWorker.interactable = canModify && currentWorkshop != null && workers < maxWorkers;
        }

        if (btnRemoveWorker != null)
        {
            btnRemoveWorker.interactable = canModify && currentWorkshop != null && workers > 0;
        }
    }

    private void RefreshResearchStatusSection(ToolWorkshopResearchManager mgr)
    {
        if (txtResearchStatus == null) return;

        if (mgr == null)
        {
            txtResearchStatus.text = "Brak menedzera badan warsztatu.";
            if (btnCancelResearch != null) btnCancelResearch.interactable = false;
            return;
        }

        int activeCount = mgr.ActiveFullyStaffedWorkshopsCount;
        float speed = ToolWorkshopResearchManager.CalculateDailyResearchSpeed(activeCount);
        int discovered = mgr.DiscoveredUpgradesCount;

        if (mgr.ActiveResearch != null && mgr.ActiveResearch.IsActive)
        {
            var r = mgr.ActiveResearch;
            string optionLabel = r.isOptionA ? "A" : "B";
            float remDays = mgr.GetRemainingDaysEstimate();
            string remText = activeCount > 0 ? $"~{remDays:F1} dni" : "WSTRZYMANE (brak aktywnego warsztatu)";
            txtResearchStatus.text =
                $"Aktywne warsztaty: {activeCount} (tempo: {speed:F2}/dzien)  |  Odkryte ulepszenia: {discovered}\n" +
                $"Badanie: <b>{r.buildingKey} T{r.tier}{optionLabel} - {r.upgrade.DisplayName}</b>  " +
                $"({r.currentProgress:F2} / {r.requiredProgress:F0}, pozostalo: {remText})";
            if (btnCancelResearch != null) btnCancelResearch.interactable = true;
        }
        else
        {
            txtResearchStatus.text =
                $"Aktywne warsztaty: {activeCount} (tempo: {speed:F2}/dzien)  |  Odkryte ulepszenia: {discovered}\n" +
                "Aktualnie brak prowadzonych badan. Wybierz dostepne ulepszenie ponizej.";
            if (btnCancelResearch != null) btnCancelResearch.interactable = false;
        }
    }

    private void RefreshTechSlots(ToolWorkshopResearchManager mgr)
    {
        if (techOptionSlots == null) return;

        var catalog = mgr != null ? mgr.TechCatalog : null;
        foreach (var slot in techOptionSlots)
        {
            if (slot == null || slot.button == null) continue;

            BuildingData targetBuilding = FindBuildingInCatalog(catalog, slot.buildingKey);
            BuildingUpgradeSO upgrade = ResolveUpgrade(catalog, slot.buildingKey, slot.tier, slot.isOptionA);

            if (slot.titleText != null)
            {
                slot.titleText.text = upgrade != null ? upgrade.DisplayName : $"{slot.buildingKey} T{slot.tier}{(slot.isOptionA ? "A" : "B")}";
            }

            bool isResearched = mgr != null && targetBuilding != null && mgr.IsOptionResearched(targetBuilding, slot.tier, slot.isOptionA);
            bool isBlockedByAlt = mgr != null && targetBuilding != null && mgr.IsTierLockedByAlternative(targetBuilding, slot.tier, slot.isOptionA);
            bool isTierAvailable = mgr != null && targetBuilding != null && mgr.IsTierAvailableToResearch(targetBuilding, slot.tier);
            bool isCurrentlyResearching = mgr != null && mgr.ActiveResearch != null && mgr.ActiveResearch.IsActive &&
                                          mgr.ActiveResearch.buildingKey == slot.buildingKey &&
                                          mgr.ActiveResearch.tier == slot.tier &&
                                          mgr.ActiveResearch.isOptionA == slot.isOptionA;

            string status;
            Color bgColor;
            bool canClick = false;

            if (isResearched)
            {
                status = "ZBADANE";
                bgColor = ColorResearched;
            }
            else if (isBlockedByAlt)
            {
                status = "ZABLOKOWANE";
                bgColor = ColorLocked;
            }
            else if (isCurrentlyResearching)
            {
                status = $"W TRAKCIE ({mgr.ActiveResearch.NormalizedProgress * 100f:F0}%)";
                bgColor = ColorActiveResearch;
            }
            else if (isTierAvailable && upgrade != null)
            {
                float est = mgr.GetEstimatedDurationForTier(slot.tier);
                int baseDays = ToolWorkshopResearchManager.CalculateBaseDuration(slot.tier, mgr.DiscoveredUpgradesCount);
                status = float.IsInfinity(est) ? $"T{slot.tier} ({baseDays} d)" : $"Badaj ({est:F1} d)";
                bgColor = ColorAvailable;
                canClick = true;
            }
            else
            {
                status = $"Wymaga T{slot.tier - 1}";
                bgColor = ColorLocked;
            }

            if (slot.statusText != null) slot.statusText.text = status;
            if (slot.background != null) slot.background.color = bgColor;
            slot.button.interactable = canClick;
        }
    }

    private void InitializeListeners()
    {
        if (listenersInitialized) return;
        listenersInitialized = true;

        if (btnClose != null)
        {
            btnClose.onClick.AddListener(() => gameObject.SetActive(false));
        }

        if (btnAddWorker != null)
        {
            btnAddWorker.onClick.AddListener(() =>
            {
                if (currentWorkshop != null && currentWorkshop.TryAddWorker(Race.Humans))
                {
                    Refresh();
                }
            });
        }

        if (btnRemoveWorker != null)
        {
            btnRemoveWorker.onClick.AddListener(() =>
            {
                if (currentWorkshop != null)
                {
                    currentWorkshop.RemoveWorker(Race.Humans);
                    Refresh();
                }
            });
        }

        if (btnCancelResearch != null)
        {
            btnCancelResearch.onClick.AddListener(() =>
            {
                if (ToolWorkshopResearchManager.Instance != null)
                {
                    ToolWorkshopResearchManager.Instance.CancelCurrentResearch();
                    Refresh();
                }
            });
        }

        if (techOptionSlots != null)
        {
            foreach (var slot in techOptionSlots)
            {
                if (slot == null || slot.button == null) continue;
                var captured = slot;

                captured.button.onClick.AddListener(() => OnOptionClicked(captured));

                var trigger = captured.button.GetComponent<EventTrigger>() ?? captured.button.gameObject.AddComponent<EventTrigger>();
                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entry.callback.AddListener(_ => ShowOptionDetails(captured));
                trigger.triggers.Add(entry);
            }
        }
    }

    private void OnOptionClicked(TechOptionSlotBinding slot)
    {
        var mgr = ToolWorkshopResearchManager.Instance;
        if (mgr == null || mgr.TechCatalog == null) return;

        var targetBuilding = FindBuildingInCatalog(mgr.TechCatalog, slot.buildingKey);
        if (targetBuilding == null) return;

        if (mgr.TryStartResearch(targetBuilding, slot.tier, slot.isOptionA))
        {
            ShowOptionDetails(slot);
            Refresh();
        }
    }

    private void ShowOptionDetails(TechOptionSlotBinding slot)
    {
        if (txtUpgradeDetails == null) return;

        var mgr = ToolWorkshopResearchManager.Instance;
        var upgrade = mgr != null ? ResolveUpgrade(mgr.TechCatalog, slot.buildingKey, slot.tier, slot.isOptionA) : null;
        if (upgrade == null) return;

        string optLabel = slot.isOptionA ? "A" : "B";
        txtUpgradeDetails.text = $"<b>[{slot.buildingKey} T{slot.tier}{optLabel}] {upgrade.DisplayName}</b>: {upgrade.DisplayDescription}";
    }

    private static BuildingData FindBuildingInCatalog(WorkshopTechTreeCatalogSO catalog, string key)
    {
        if (catalog == null || catalog.buildingTrees == null || string.IsNullOrEmpty(key)) return null;
        foreach (var tree in catalog.buildingTrees)
        {
            if (tree != null && tree.targetBuilding != null &&
                string.Equals(ToolWorkshopResearchManager.GetBuildingKey(tree.targetBuilding), key, StringComparison.OrdinalIgnoreCase))
            {
                return tree.targetBuilding;
            }
        }
        return null;
    }

    private static BuildingUpgradeSO ResolveUpgrade(WorkshopTechTreeCatalogSO catalog, string key, int tier, bool isOptionA)
    {
        var building = FindBuildingInCatalog(catalog, key);
        if (building == null || !catalog.TryGetTree(building, out var tree)) return null;
        return tree.GetTier(tier)?.GetOption(isOptionA);
    }

    private void AutoBindIfNeeded()
    {
        if (txtTitle == null) txtTitle = FindDeepChild<TMP_Text>(transform, "Txt_Title");
        if (btnClose == null) btnClose = FindDeepChild<Button>(transform, "Btn_Close");
        if (txtWorkersStatus == null) txtWorkersStatus = FindDeepChild<TMP_Text>(transform, "Txt_WorkersStatus");
        if (btnAddWorker == null) btnAddWorker = FindDeepChild<Button>(transform, "Btn_AddWorker");
        if (btnRemoveWorker == null) btnRemoveWorker = FindDeepChild<Button>(transform, "Btn_RemoveWorker");
        if (txtResearchStatus == null) txtResearchStatus = FindDeepChild<TMP_Text>(transform, "Txt_ResearchStatus");
        if (btnCancelResearch == null) btnCancelResearch = FindDeepChild<Button>(transform, "Btn_CancelResearch");
        if (txtUpgradeDetails == null) txtUpgradeDetails = FindDeepChild<TMP_Text>(transform, "Txt_UpgradeDetails");
    }

    private static T FindDeepChild<T>(Transform parent, string childName) where T : Component
    {
        if (parent == null) return null;
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                var c = child.GetComponent<T>();
                if (c != null) return c;
            }
            var found = FindDeepChild<T>(child, childName);
            if (found != null) return found;
        }
        return null;
    }
}
