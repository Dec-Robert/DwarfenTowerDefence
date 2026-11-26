using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopulationInfoPanelUI : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private Race race;
    [SerializeField] private ResourceVisualsSO raceVisuals;

    [Header("UI References")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI txtFree;
    [SerializeField] private TextMeshProUGUI txtWorking;
    [SerializeField] private TextMeshProUGUI txtAll;

    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color noFreeWorkersColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    [SerializeField] private Color growthIncomingColor = new Color32(74, 222, 128, 255); // #4ADE80

    private bool isSubscribedWorkforce;
    private bool isSubscribedTime;
    private bool isSubscribedResources;
    private bool isSubscribedEconomy;
    private bool isRefreshDirty;

    public Race Race => race;
    public TextMeshProUGUI TxtFree => txtFree;
    public TextMeshProUGUI TxtWorking => txtWorking;
    public TextMeshProUGUI TxtAll => txtAll;

    private void Awake()
    {
        AutoBindFields();
    }

    private void OnValidate()
    {
        AutoBindFields();
    }

    private void AutoBindFields()
    {
        if (txtWorking != null && txtAll != null && txtFree != null) return;

        var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in texts)
        {
            Transform current = t.transform.parent;
            while (current != null && current != transform)
            {
                if (txtFree == null && current.name.IndexOf("Free", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    txtFree = t;
                    break;
                }
                if (txtWorking == null && current.name.IndexOf("Working", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    txtWorking = t;
                    break;
                }
                if (txtAll == null && current.name.IndexOf("Overall", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    txtAll = t;
                    break;
                }
                current = current.parent;
            }
        }
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        if (icon != null && raceVisuals != null)
            icon.sprite = raceVisuals.GetIcon(race);

        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (!isSubscribedWorkforce && WorkforceManager.Instance != null)
        {
            WorkforceManager.Instance.OnWorkforceStatsChanged += HandleStatsChanged;
            isSubscribedWorkforce = true;
        }

        if (!isSubscribedTime && TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted += HandleEnvironmentChanged;
            TimePhaseManager.Instance.OnDuskStarted += HandleEnvironmentChanged;
            TimePhaseManager.Instance.OnNightStarted += HandleEnvironmentChanged;
            isSubscribedTime = true;
        }

        if (!isSubscribedResources && ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged += HandleResourceChanged;
            isSubscribedResources = true;
        }

        if (!isSubscribedEconomy)
        {
            BuildingEntity.OnAnyBuildingEconomyChanged += HandleEnvironmentChanged;
            PhaseResourceForecastManager.OnForecastDirty += HandleEnvironmentChanged;
            isSubscribedEconomy = true;
        }

        if (WorkforceManager.Instance != null)
        {
            UpdateUI(WorkforceManager.Instance.GetStats(race));
        }
    }

    private void Unsubscribe()
    {
        if (isSubscribedWorkforce && WorkforceManager.Instance != null)
        {
            WorkforceManager.Instance.OnWorkforceStatsChanged -= HandleStatsChanged;
        }
        isSubscribedWorkforce = false;

        if (isSubscribedTime && TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= HandleEnvironmentChanged;
            TimePhaseManager.Instance.OnDuskStarted -= HandleEnvironmentChanged;
            TimePhaseManager.Instance.OnNightStarted -= HandleEnvironmentChanged;
        }
        isSubscribedTime = false;

        if (isSubscribedResources && ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged -= HandleResourceChanged;
        }
        isSubscribedResources = false;

        if (isSubscribedEconomy)
        {
            BuildingEntity.OnAnyBuildingEconomyChanged -= HandleEnvironmentChanged;
            PhaseResourceForecastManager.OnForecastDirty -= HandleEnvironmentChanged;
            isSubscribedEconomy = false;
        }
    }

    private void LateUpdate()
    {
        if (isRefreshDirty)
        {
            isRefreshDirty = false;
            RefreshFromManagers();
        }
    }

    private void HandleStatsChanged(Race updatedRace, WorkforceManager.WorkforceStats stats)
    {
        if (updatedRace != race) return;
        isRefreshDirty = true;
    }

    private void HandleResourceChanged(ResourceType type, int amount)
    {
        isRefreshDirty = true;
    }

    private void HandleEnvironmentChanged()
    {
        isRefreshDirty = true;
    }

    public void RefreshFromManagers()
    {
        isRefreshDirty = false;
        if (WorkforceManager.Instance == null) return;
        UpdateUI(WorkforceManager.Instance.GetStats(race));
    }

    private void UpdateUI(WorkforceManager.WorkforceStats stats)
    {
        if (stats == null) return;

        if (txtFree != null)
        {
            txtFree.text = stats.Idle.ToString();
            txtFree.color = stats.Idle > 0 ? normalColor : noFreeWorkersColor;
        }

        if (txtWorking != null)
        {
            txtWorking.text = stats.Working.ToString();
            txtWorking.color = normalColor;
        }

        bool isGrowthIncoming = false;
        if (TimePhaseManager.Instance != null && TimePhaseManager.Instance.CurrentTimePhase == TimePhases.Morning)
        {
            var growthForecast = PhaseResourceForecastManager.PredictIncomingPopulationGrowth(TimePhases.Morning);
            isGrowthIncoming = growthForecast.TryGetValue(race, out bool willGrow) && willGrow;
        }

        if (txtAll != null)
        {
            txtAll.text = stats.Total.ToString();
            txtAll.color = isGrowthIncoming ? growthIncomingColor : normalColor;
        }
    }
}
