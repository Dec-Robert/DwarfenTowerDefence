using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class EnemyInfoUI : MonoBehaviour
{
    public static EnemyInfoUI  Instance { get; private set; }

    [Header("Text")]
    public TextMeshProUGUI monsterNameText;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI monsterSpeedText;
    public TextMeshProUGUI monsterArmourText;
    public TextMeshProUGUI monsterMagicDefenseText;

    [Header("Images")] 
    public Image enemyIcon;
    public Image healthBar;

    [Header("Anchores")]
    private RectTransform rectTransform;
    public float hiddenYPosition, shownYPosition;

    private EnemyStats enemyStats;
    private bool isOpenWindow;


    public void Awake()
    {
        Instance = this;
        rectTransform = GetComponent<RectTransform>();
    }
    
    void Update()
    {
        if (enemyStats == null)
        {
            if (isOpenWindow) CloseWindow();
            return;
        }
        
        if (enemyStats != null) UpdateUIComponent();


        
    }

    private void UpdateHealth()
    {
        healthText.text = enemyStats.GetCurrentHealth().ToString(); 
        healthBar.fillAmount = 0.5f; //Todo: Tymczasowo trwałe 0.5
    }
    
    private void UpdateUIComponent()
    {
    monsterNameText.text = enemyStats.data.enemyName;
    healthText.text = enemyStats.GetCurrentHealth().ToString();
    monsterSpeedText.text = enemyStats.data.moveSpeed.ToString();
    monsterArmourText.text = enemyStats.data.armor.ToString();
    monsterMagicDefenseText.text = enemyStats.data.magicResist.ToString();

    enemyIcon.sprite = enemyStats.data.sprite;
    healthBar.fillAmount = 0.5f; //Todo: Tymczasowo trwałe 0.5
    }

    public void OpenWindow(EnemyStats stats)
    {
        enemyStats = stats;
        rectTransform.DOAnchorPosY(shownYPosition, 0.1f).SetUpdate(true);
        isOpenWindow = true;
        UpdateUIComponent();
    }

    public void CloseWindow()
    {
        enemyStats = null;
        isOpenWindow = false;
        rectTransform.DOAnchorPosY(hiddenYPosition,0.1f).SetUpdate(true);
    }
}
