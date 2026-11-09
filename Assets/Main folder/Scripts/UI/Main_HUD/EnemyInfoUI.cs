using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class EnemyInfoUI : MonoBehaviour
{
    public static EnemyInfoUI Instance { get; private set; }

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

    public void Start()
    {
        CloseWindow();
    }

    void Update()
    {
        if (enemyStats == null)
        {
            if (isOpenWindow) CloseWindow();
            return;
        }

        UpdateUIComponent();
    }

    private void UpdateHealth()
    {
        if (enemyStats == null) return;
        healthText.text = $"{Mathf.CeilToInt(enemyStats.GetCurrentHealth())} / {Mathf.CeilToInt(enemyStats.GetMaxHealth())}";
        if (healthBar != null && enemyStats.GetMaxHealth() > 0f)
        {
            healthBar.fillAmount = Mathf.Clamp01(enemyStats.GetCurrentHealth() / enemyStats.GetMaxHealth());
        }
    }

    private void UpdateUIComponent()
    {
        if (enemyStats == null || enemyStats.data == null) return;

        string tierText = (enemyStats.data.rank == EnemyRank.Boss || enemyStats.data.isBoss) ? "" : $" (T{enemyStats.Tier})";
        monsterNameText.text = $"{enemyStats.data.DisplayName}{tierText}";
        healthText.text = $"{Mathf.CeilToInt(enemyStats.GetCurrentHealth())} / {Mathf.CeilToInt(enemyStats.GetMaxHealth())}";
        monsterSpeedText.text = $"{enemyStats.CurrentMoveSpeed:0.##}";
        monsterArmourText.text = $"{enemyStats.CurrentArmor * 100f:0.#}%";
        monsterMagicDefenseText.text = $"{enemyStats.CurrentMagicResist * 100f:0.#}%";

        Sprite icon = enemyStats.CurrentSprite;
        if (icon != null) enemyIcon.sprite = icon;

        if (healthBar != null && enemyStats.GetMaxHealth() > 0f)
        {
            healthBar.fillAmount = Mathf.Clamp01(enemyStats.GetCurrentHealth() / enemyStats.GetMaxHealth());
        }
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
        rectTransform.DOAnchorPosY(hiddenYPosition, 0.1f).SetUpdate(true);
    }
}
