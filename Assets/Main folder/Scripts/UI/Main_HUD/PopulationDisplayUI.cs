using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using DG.Tweening;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.HID;

public class PopulationDisplayUI : MonoBehaviour
{
    [Header("Extended panel movement")] 
    public float speed = 5f;
    public float hiddenPosition;
    public float shownPosition;
    public RectTransform extendedPanel;
    private bool isShown = false;
    
    
    [System.Serializable]
    struct MenuTextForRaces
    {
        public Race race;
        public TextMeshProUGUI standardPopulationFree;
        public TextMeshProUGUI extendedPopulationFree;
        public TextMeshProUGUI extendedPopulationWorking; 
        public TextMeshProUGUI extendedPopulationAll;
    }
    
    [SerializeField]
    List<MenuTextForRaces> citizens = new List<MenuTextForRaces>();
    
    private bool isSubscribed;

    public void OnEnable()
    {
        Subscribe();
    }

    public void Start()
    {
        Subscribe();
    }

    public void OnDisable()
    {
        if (!isSubscribed) return;

        if (WorkforceManager.Instance != null)
            WorkforceManager.Instance.OnWorkforceStatsChanged -= RefreshRaceStats;

        isSubscribed = false;
    }

    private void Subscribe()
    {
        if (isSubscribed || WorkforceManager.Instance == null) return;

        WorkforceManager.Instance.OnWorkforceStatsChanged += RefreshRaceStats;
        isSubscribed = true;

        foreach (MenuTextForRaces citizen in citizens)
            RefreshRaceStats(citizen.race, WorkforceManager.Instance.GetStats(citizen.race));
    }

    private void RefreshRaceStats(Race race, WorkforceManager.WorkforceStats raceStats)
    {
        foreach (MenuTextForRaces citizen in citizens)
        {
            if (citizen.race == race)
            {
                citizen.standardPopulationFree.text = raceStats.Idle.ToString();
                citizen.extendedPopulationFree.text = raceStats.Idle.ToString();
                citizen.extendedPopulationWorking.text = raceStats.Working.ToString();
                citizen.extendedPopulationAll.text = raceStats.Total.ToString();
            }
        }
    }

    private void showExtendedPanel()
    {
        extendedPanel.DOAnchorPosY(shownPosition, 0.5f).SetUpdate(true); 

    }

    private void hideExtendedPanel()
    {
        extendedPanel.DOAnchorPosY(hiddenPosition, 0.5f).SetUpdate(true);
    }


    private void Update()
    {
        
        //Todo: Usunać po debugu
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.f4Key.wasPressedThisFrame)
        {
            hideExtendedPanel();
        }

        if (keyboard.f4Key.wasReleasedThisFrame)
        {
            showExtendedPanel();
        }
        // Do tego miejsca
    }
}
