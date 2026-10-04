using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SingleWorkerManagerUI : MonoBehaviour
{
    public Race race;
    public ResourceVisualsSO raceVisuals;

    public Image icon;
    public TextMeshProUGUI countText;
    public Button buttonAdd;
    public Button buttonRemove;
    private BuildingEntity currentBuilding;

    private void Awake()
    {
        if (raceVisuals != null && icon != null)
        {
            icon.sprite = raceVisuals.GetIcon(race);
        }
    }

    public void Setup(BuildingEntity currentBuilding)
    {
        this.currentBuilding = currentBuilding;

        buttonAdd.onClick.RemoveAllListeners();
        buttonAdd.onClick.AddListener(() =>
        {
            currentBuilding.TryAddWorker(race);
            RefreshUI();
        });

        buttonRemove.onClick.RemoveAllListeners();
        buttonRemove.onClick.AddListener(() =>
        {
            currentBuilding.RemoveWorker(race);
            RefreshUI();
        });

        RefreshUI();
    }

    public void RefreshUI()
    {
        if (currentBuilding == null) return;

        int currentWorkers = currentBuilding.GetWorkerCount(race);
        countText.text = currentWorkers.ToString();

        buttonRemove.interactable = currentWorkers > 0;

        int maxWorkers = currentBuilding.data != null ? currentBuilding.data.baseMaxWorkers : 1;
        if (currentBuilding.Upgrades != null)
        {
            maxWorkers += currentBuilding.Upgrades.LocalBonusWorkers;
        }
        if (CityStatsManager.Instance != null)
        {
            maxWorkers += CityStatsManager.Instance.globalBonusWorkersPerShift;
        }

        bool isBuildingFull = currentBuilding.GetTotalWorkerCount() >= maxWorkers;
        bool hasFreeCitizen = WorkforceManager.Instance != null && WorkforceManager.Instance.HasIdleWorker(race);

        buttonAdd.interactable = !isBuildingFull && hasFreeCitizen;
    }
}