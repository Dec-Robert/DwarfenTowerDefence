using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AttackStatUI : MonoBehaviour
{
    public BattleStats stats;
    public TextMeshProUGUI value;
    public Image icon;

    private void Awake()
    {
        if (value == null) value = GetComponentInChildren<TextMeshProUGUI>();
    }

    public void Setup(TowerStats data)
    {
        switch (stats)
        {
            case BattleStats.Range:
                SetText(data.range.ToString("F1"));
                break;

            case BattleStats.Damage:
                SetText(data.damage.ToString("F1"));
                break;

            case BattleStats.FireRate:
                SetText(data.fireRate.ToString("F2"));
                break;

            case BattleStats.CriticalChancel:
                SetText((data.criticalChancel * 100f).ToString("F0") + "%");
                break;

            case BattleStats.CriticalMultiplier:
                SetText((data.criticalDamageMultiplier * 100f).ToString("F0") + "%");
                break;

            case BattleStats.ArmorPenetration:
                SetText((data.armorPenetration * 100f).ToString("F0") + "%");
                break;

            case BattleStats.MagicPenetration:
                SetText((data.magicPenetration * 100f).ToString("F0") + "%");
                break;

            case BattleStats.DamageType:
                SetDamageTypeText(data.damageType);
                break;
        }
    }

    public void Setup()
    {
    }

    private void SetText(string text)
    {
        if (value != null)
        {
            value.text = text;
        }
    }

    private void SetDamageTypeText(DamageType damageType)
    {
        if (value == null) return;

        switch (damageType)
        {
            case DamageType.Magic:
                value.text = "Magic";
                value.color = Color.blue;
                break;

            case DamageType.Physical:
                value.text = "Physical";
                value.color = new Color32(245, 71, 39, 255);
                break;

            case DamageType.True:
                value.text = "Holy";
                value.color = Color.white;
                break;
        }
    }
}