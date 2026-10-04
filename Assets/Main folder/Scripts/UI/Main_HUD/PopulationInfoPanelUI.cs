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
    [SerializeField] private TextMeshProUGUI txtAll;

    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color noFreeWorkersColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    private bool isSubscribed;

    public Race Race => race;

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
        if (isSubscribed || WorkforceManager.Instance == null) return;

        WorkforceManager.Instance.OnWorkforceStatsChanged += HandleStatsChanged;
        isSubscribed = true;
        UpdateUI(WorkforceManager.Instance.GetStats(race));
    }

    private void Unsubscribe()
    {
        if (!isSubscribed) return;

        if (WorkforceManager.Instance != null)
            WorkforceManager.Instance.OnWorkforceStatsChanged -= HandleStatsChanged;

        isSubscribed = false;
    }

    private void HandleStatsChanged(Race updatedRace, WorkforceManager.WorkforceStats stats)
    {
        if (updatedRace != race) return;
        UpdateUI(stats);
    }

    private void UpdateUI(WorkforceManager.WorkforceStats stats)
    {
        if (stats == null) return;

        if (txtFree != null)
        {
            txtFree.text = stats.Idle.ToString();
            txtFree.color = stats.Idle > 0 ? normalColor : noFreeWorkersColor;
        }

        if (txtAll != null)
            txtAll.text = stats.Total.ToString();
    }
}
