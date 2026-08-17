using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TowerInfoPanelUI : MonoBehaviour
{
    public GameObject focusArray;
    public GameObject statArray;
    public GameObject workerArray;
    public TextMeshProUGUI efficencyText;
    
    public ResourceVisualsSO resourceVisualsSO;
    
    private TowerEntity currentTower;
   
    
    public GameObject targetImagePrefab;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        Initialize();
    }
    
    private void ChangeTargeting(TargetingMode targetType)
    {
        if (currentTower.controller.activeTargetingRules.Contains(targetType)) currentTower.controller.activeTargetingRules.Remove(targetType);
        else currentTower.controller.activeTargetingRules.Add(targetType);

    }

    //Do it once to set up window
    private void Initialize()
    {
        foreach (var target in resourceVisualsSO.focusVisuals)
        {
            GameObject targetButton = Instantiate(targetImagePrefab, focusArray.transform);
            Button btn = targetButton.GetComponent<Button>();
            
            targetButton.transform.GetChild(0).GetComponent<Image>().sprite = target.icon;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
                {
                    ChangeTargeting(target.focus);
                }
            );
        }
    }

    //Do it everytime windows is opened
    public void Setup(TowerEntity tower)
    {
        if(currentTower != null) currentTower.controller.OnStatsChanged -= UpdateStatsUI;
        currentTower = tower;
        currentTower.controller.OnStatsChanged += UpdateStatsUI;
  
        UpdateStatsUI();

        
        for (int i = 0; i < workerArray.transform.childCount; i++)
        {
            var child = workerArray.transform.GetChild(i);
            try
            {
                child.GetComponent<SingleWorkerManagerUI>().Setup(tower);
            }
            catch (Exception e){}
        }

    }
    
    //Update combat stats
    private void UpdateStatsUI()
    {
        TowerStats towerStats = currentTower.controller.GetAllStats();
        for (int i = 0; i < statArray.transform.childCount; i++)
        {
            var child = statArray.transform.GetChild(i);
            try
            {
                child.GetComponent<AttackStatUI>().Setup(towerStats);
            }
            catch (Exception e){}
        }

        string efficiency = (currentTower.GetEfficencyTable()[currentTower.GetTotalWorkerCount()] * 10f).ToString();
        if (currentTower.GetTotalWorkerCount() > 0) efficiency = efficiency + "0%";
        else efficiency = "0%";
        
        efficencyText.text = efficiency;
    }

    //TODO: Do zaimplementowa w pełni podczas zamknięcia okna
    public void CloseWindow()
    {
        currentTower.controller.OnStatsChanged -= UpdateStatsUI;
    }
    private void OnDisable()
    {
        currentTower.controller.OnStatsChanged -= UpdateStatsUI;
    }
    
    
}
