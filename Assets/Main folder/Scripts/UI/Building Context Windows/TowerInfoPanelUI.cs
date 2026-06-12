using System;
using UnityEngine;
using UnityEngine.UI;

public class TowerInfoPanelUI : MonoBehaviour
{
    public GameObject focusArray;
    
    public ResourceVisualsSO resourceVisualsSO;

    private TowerEntity currentTower;
    
    public GameObject targetImagePrefab;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake()
    {
        foreach (var target in resourceVisualsSO.focusVisuals)
        {
            GameObject targetButton = Instantiate(targetImagePrefab, focusArray.transform);
            Button btn = targetButton.GetComponent<Button>();
            
            targetButton.GetComponentInChildren<Image>().sprite = target.icon;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
                {
                    ChangeTargeting(target.focus);
                }
            );
        }
    }

    public void Setup(TowerEntity tower)
    {
        currentTower = tower;
    }
    
    private void ChangeTargeting(TargetingMode targetType)
    {
        if (currentTower.controller.activeTargetingRules.Contains(targetType)) currentTower.controller.activeTargetingRules.Remove(targetType);
        else currentTower.controller.activeTargetingRules.Add(targetType);

    }
    
    
    
}
