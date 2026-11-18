using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kontroler pojedynczego panelu przydziału pracowników dla jednej konkretnej rasy
/// (przeznaczony dla prefaba Pnl_RaceControllersSingle Variant / Pnl_Race_Universal).
/// Obsługuje standardowe budynki (BuildingEntity, TowerEntity, ToolWorkshopEntity)
/// oraz Kuźnię Runiczną (RuneForgeEntity).
/// </summary>
public class SingleRaceWorkerControllerUI : MonoBehaviour
{
    [Header("Konfiguracja")]
    [SerializeField] private Race race = Race.Humans;
    [SerializeField] private BuildingEntity targetBuilding;
    [SerializeField] private bool autoDetectRaceFromBuilding = true;
    [SerializeField] private bool showMaxInCount = false;

    [Header("Referencje UI")]
    [SerializeField] private TextMeshProUGUI txtTitle;
    [SerializeField] private TextMeshProUGUI txtFree;
    [SerializeField] private TextMeshProUGUI txtCount;
    [SerializeField] private Button btnMinus;
    [SerializeField] private Button btnPlus;

    private bool listenersBound;
    private bool isSubscribedWorkforce;
    private bool isSubscribedTime;
    private bool isSubscribedEconomy;
    private bool isSubscribedLocalization;

    public Race Race => race;
    public BuildingEntity TargetBuilding => targetBuilding;

    /// <summary>Wywoływane po udanej zmianie liczby pracowników tej rasy (rasa, nowa liczba w budynku).</summary>
    public event Action<Race, int> OnWorkersChanged;

    private void Awake()
    {
        AutoBindReferences();
        BindButtons();
    }

    private void Reset()
    {
        AutoBindReferences();
    }

    private void OnValidate()
    {
        AutoBindReferences();
    }

    private void OnEnable()
    {
        AutoBindReferences();
        BindButtons();
        SubscribeEvents();
        Refresh();
    }

    private void Start()
    {
        SubscribeEvents();
        Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
        UnbindButtons();
    }

    /// <summary>
    /// Ustawia docelowy budynek. Jeśli autoDetectRaceFromBuilding jest włączone i budynek wymaga
    /// konkretnej rasy (lub jest Kuźnią Runiczną), automatycznie dopasowuje rasę.
    /// </summary>
    public void Setup(BuildingEntity building)
    {
        targetBuilding = building;

        if (autoDetectRaceFromBuilding && building != null)
        {
            if (building is RuneForgeEntity)
            {
                race = Race.Dwarves;
            }
            else if (building.data != null && building.data.requiresSpecificCitizen)
            {
                race = building.data.requiredCitizenRace;
            }
        }

        AutoBindReferences();
        BindButtons();
        SubscribeEvents();
        Refresh();
    }

    /// <summary>
    /// Ustawia docelowy budynek oraz konkretną rasę obsługiwaną przez kontroler.
    /// </summary>
    public void Setup(BuildingEntity building, Race targetRace)
    {
        targetBuilding = building;
        race = targetRace;

        AutoBindReferences();
        BindButtons();
        SubscribeEvents();
        Refresh();
    }

    public void SetRace(Race targetRace)
    {
        race = targetRace;
        Refresh();
    }

    public void AutoBindReferences()
    {
        Transform box = transform.Find("Pnl_Race_Universal") ?? transform;

        if (txtTitle == null)
            txtTitle = box.Find("Txt_RaceTitle")?.GetComponent<TextMeshProUGUI>() ?? FindDeepChild<TextMeshProUGUI>(box, "Txt_RaceTitle");

        if (txtFree == null)
            txtFree = box.Find("Txt_FreeWorkers")?.GetComponent<TextMeshProUGUI>() ?? FindDeepChild<TextMeshProUGUI>(box, "Txt_FreeWorkers");

        Transform btnRow = box.Find("Pnl_Buttons");
        if (btnRow != null)
        {
            if (btnMinus == null) btnMinus = btnRow.Find("Btn_Minus")?.GetComponent<Button>();
            if (txtCount == null) txtCount = btnRow.Find("Txt_Count")?.GetComponent<TextMeshProUGUI>();
            if (btnPlus == null) btnPlus = btnRow.Find("Btn_Plus")?.GetComponent<Button>();
        }
        else
        {
            if (btnMinus == null) btnMinus = FindDeepChild<Button>(box, "Btn_Minus");
            if (txtCount == null) txtCount = FindDeepChild<TextMeshProUGUI>(box, "Txt_Count");
            if (btnPlus == null) btnPlus = FindDeepChild<Button>(box, "Btn_Plus");
        }
    }

    public void Refresh()
    {
        if (txtTitle != null)
        {
            txtTitle.text = GetRaceDisplayName(race);
        }

        int freeCount = WorkforceManager.Instance != null ? WorkforceManager.Instance.GetIdleCount(race) : 0;
        if (txtFree != null)
        {
            txtFree.text = LocalizationManager.GetFormat("UI_PROD_FREE_WORKERS_FMT", freeCount);
        }

        int assignedCount = GetAssignedCountForRace();
        int maxWorkers = GetMaxWorkers();

        if (txtCount != null)
        {
            txtCount.text = showMaxInCount ? $"{assignedCount}/{maxWorkers}" : assignedCount.ToString();
        }

        if (btnMinus != null)
        {
            btnMinus.interactable = CanRemoveWorker();
        }

        if (btnPlus != null)
        {
            btnPlus.interactable = CanAddWorker(freeCount, maxWorkers);
        }
    }

    private void BindButtons()
    {
        if (listenersBound) return;

        if (btnPlus != null)
        {
            btnPlus.onClick.RemoveListener(OnPlusClicked);
            btnPlus.onClick.AddListener(OnPlusClicked);
        }

        if (btnMinus != null)
        {
            btnMinus.onClick.RemoveListener(OnMinusClicked);
            btnMinus.onClick.AddListener(OnMinusClicked);
        }

        listenersBound = true;
    }

    private void UnbindButtons()
    {
        if (btnPlus != null) btnPlus.onClick.RemoveListener(OnPlusClicked);
        if (btnMinus != null) btnMinus.onClick.RemoveListener(OnMinusClicked);
        listenersBound = false;
    }

    private void OnPlusClicked()
    {
        if (targetBuilding == null || !targetBuilding.isBuildingActive) return;

        bool changed = false;
        if (targetBuilding is RuneForgeEntity forge)
        {
            if (race == Race.Dwarves)
            {
                changed = forge.TryAddRunesmith();
            }
        }
        else
        {
            changed = targetBuilding.TryAddWorker(race);
        }

        if (changed)
        {
            NotifyAndRefresh();
        }
    }

    private void OnMinusClicked()
    {
        if (targetBuilding == null || !targetBuilding.isBuildingActive) return;

        bool changed = false;
        if (targetBuilding is RuneForgeEntity forge)
        {
            if (race == Race.Dwarves)
            {
                changed = forge.TryRemoveRunesmith();
            }
        }
        else
        {
            int before = targetBuilding.GetWorkerCount(race);
            targetBuilding.RemoveWorker(race);
            changed = targetBuilding.GetWorkerCount(race) != before;
        }

        if (changed)
        {
            NotifyAndRefresh();
        }
    }

    private void NotifyAndRefresh()
    {
        Refresh();
        OnWorkersChanged?.Invoke(race, GetAssignedCountForRace());
    }

    private int GetAssignedCountForRace()
    {
        if (targetBuilding == null) return 0;

        if (targetBuilding is RuneForgeEntity forge)
        {
            return race == Race.Dwarves && forge.assignedDwarves != null ? forge.assignedDwarves.Count : 0;
        }

        return targetBuilding.GetWorkerCount(race);
    }

    private int GetTotalAssignedCount()
    {
        if (targetBuilding == null) return 0;

        if (targetBuilding is RuneForgeEntity forge)
        {
            return forge.assignedDwarves != null ? forge.assignedDwarves.Count : 0;
        }

        return targetBuilding.GetTotalWorkerCount();
    }

    private int GetMaxWorkers()
    {
        if (targetBuilding == null) return 0;

        if (targetBuilding is RuneForgeEntity forge)
        {
            return forge.GetMaxRunesmiths();
        }

        int baseMax = targetBuilding.data != null ? targetBuilding.data.baseMaxWorkers : 1;
        int upgradeBonus = targetBuilding.Upgrades != null ? targetBuilding.Upgrades.LocalBonusWorkers : 0;
        int globalBonus = targetBuilding.GetGlobalWorkerBonus();
        return Mathf.Max(0, baseMax + upgradeBonus + globalBonus);
    }

    private bool CanAddWorker(int freeCount, int maxWorkers)
    {
        if (targetBuilding == null || !targetBuilding.isBuildingActive) return false;
        if (freeCount <= 0) return false;

        bool isNight = TimePhaseManager.Instance != null && TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Night;
        if (isNight) return false;

        if (targetBuilding is RuneForgeEntity)
        {
            if (race != Race.Dwarves) return false;
        }
        else if (targetBuilding.data != null && targetBuilding.data.requiresSpecificCitizen && targetBuilding.data.requiredCitizenRace != race)
        {
            return false;
        }

        return GetTotalAssignedCount() < maxWorkers;
    }

    private bool CanRemoveWorker()
    {
        if (targetBuilding == null || !targetBuilding.isBuildingActive) return false;

        if (targetBuilding is RuneForgeEntity)
        {
            return race == Race.Dwarves && GetAssignedCountForRace() > 0;
        }

        return targetBuilding.Workers != null
            ? targetBuilding.Workers.HasRemovableWorker(race)
            : GetAssignedCountForRace() > 0;
    }

    private void SubscribeEvents()
    {
        if (!isSubscribedWorkforce && WorkforceManager.Instance != null)
        {
            WorkforceManager.Instance.OnWorkforceStatsChanged += HandleWorkforceChanged;
            isSubscribedWorkforce = true;
        }

        if (!isSubscribedTime && TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted += HandleEnvironmentChanged;
            TimePhaseManager.Instance.OnDuskStarted += HandleEnvironmentChanged;
            TimePhaseManager.Instance.OnNightStarted += HandleEnvironmentChanged;
            isSubscribedTime = true;
        }

        if (!isSubscribedEconomy)
        {
            BuildingEntity.OnAnyBuildingEconomyChanged += HandleEnvironmentChanged;
            isSubscribedEconomy = true;
        }

        if (!isSubscribedLocalization)
        {
            LocalizationManager.OnLanguageChanged += HandleEnvironmentChanged;
            isSubscribedLocalization = true;
        }
    }

    private void UnsubscribeEvents()
    {
        if (isSubscribedWorkforce && WorkforceManager.Instance != null)
        {
            WorkforceManager.Instance.OnWorkforceStatsChanged -= HandleWorkforceChanged;
        }
        isSubscribedWorkforce = false;

        if (isSubscribedTime && TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= HandleEnvironmentChanged;
            TimePhaseManager.Instance.OnDuskStarted -= HandleEnvironmentChanged;
            TimePhaseManager.Instance.OnNightStarted -= HandleEnvironmentChanged;
        }
        isSubscribedTime = false;

        if (isSubscribedEconomy)
        {
            BuildingEntity.OnAnyBuildingEconomyChanged -= HandleEnvironmentChanged;
        }
        isSubscribedEconomy = false;

        if (isSubscribedLocalization)
        {
            LocalizationManager.OnLanguageChanged -= HandleEnvironmentChanged;
        }
        isSubscribedLocalization = false;
    }

    private void HandleWorkforceChanged(Race updatedRace, WorkforceManager.WorkforceStats _)
    {
        if (updatedRace != race) return;
        Refresh();
    }

    private void HandleEnvironmentChanged()
    {
        Refresh();
    }

    private static string GetRaceDisplayName(Race targetRace)
    {
        return targetRace switch
        {
            Race.Humans => LocalizationManager.Get("RACE_HUMANS", "LUDZIE").ToUpper(),
            Race.Elves => LocalizationManager.Get("RACE_ELVES", "ELFY").ToUpper(),
            Race.Dwarves => LocalizationManager.Get("RACE_DWARVES", "KRASNOLUDY").ToUpper(),
            _ => targetRace.ToString().ToUpper()
        };
    }

    private static T FindDeepChild<T>(Transform parent, string childName) where T : Component
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                T comp = child.GetComponent<T>();
                if (comp != null) return comp;
            }

            T nested = FindDeepChild<T>(child, childName);
            if (nested != null) return nested;
        }
        return null;
    }
}
