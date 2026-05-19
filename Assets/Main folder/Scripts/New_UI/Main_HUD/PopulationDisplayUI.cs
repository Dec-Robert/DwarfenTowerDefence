using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using DG.Tweening;
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
    
    public void Start()
    {
        CitizenManager.Instance.PopulationChange += RefreshRaceStats;
    }

    public void OnDisable()
    {
        CitizenManager.Instance.PopulationChange -= RefreshRaceStats;
    }

    private void RefreshRaceStats(CitizenManager.RaceStats raceStats, Race race)
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
        extendedPanel.DOAnchorPosY(shownPosition, 0.5f); 

    }

    private void hideExtendedPanel()
    {
        extendedPanel.DOAnchorPosY(hiddenPosition, 0.5f);
    }


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F4))
        {
            hideExtendedPanel();
        }

        if (Input.GetKeyDown(KeyCode.F5))
        {
            showExtendedPanel();
        }
    }
}
