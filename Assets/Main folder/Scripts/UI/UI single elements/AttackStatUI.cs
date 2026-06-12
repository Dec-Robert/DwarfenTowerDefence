using System;
using Microsoft.Unity.VisualStudio.Editor;
using TMPro;
using UnityEngine;

public class AttackStatUI : MonoBehaviour
{
    
    public BattleStats stats;
    public TextMeshProUGUI value;
    public Image icon;

    private void Awake()
    {
        if (value == null) value = GetComponentInChildren<TextMeshProUGUI>();
    }

    public void Setup(TowerData data)
    {
            
        switch (stats)
        {
            case BattleStats.Range:
                SetText(data.baseRange);
                break;
            case BattleStats.Damage:
                SetText(data.baseDamage);
                break;
            case BattleStats.FireRate:
                SetText(data.fireRate);
                break;
            case BattleStats.CriticalChancel:
                SetText(data.criticalChancel);
                break;
            case BattleStats.CriticalMultiplier:
                SetText(data.criticalDamageMultiplier);
                break;
            case BattleStats.ArmorPenetration:
                SetText(data.armorPenetration);
                break;
            case BattleStats.MagicPenetration:
                SetText(data.magicPenetration);
                break;
            case BattleStats.DamageType:
                SetText(data.damageType);
                break;
        }
        
    }

    private void SetupExtraInfo(TowerData data)
    {
        SetExtraText(data);
    }

    private void SetText<T>(T text)
    {
        if (text is DamageType)
        {
            if (text.Equals(DamageType.Magic))
            {
                value.text = "Magic";
                value.color = Color.blue;
            }else if (text.Equals(DamageType.Physical))
            {
                value.text = "Physical";
                value.color = new Color(245,71,39);
            }
            else
            {
                value.text = "Holy";
                value.color = Color.black;
            }
        }
        else
        {
            switch (stats)
            {
                case BattleStats.Range or BattleStats.Damage or BattleStats.FireRate:
                    value.text = text.ToString();
                    break;
                case BattleStats.ArmorPenetration or BattleStats.MagicPenetration or BattleStats.CriticalChancel:
                    value.text = text.ToString() + "%";
                    break;
                case BattleStats.CriticalMultiplier:
                    value.text = (Convert.ToSingle(text) * 100f).ToString() + "%";
                    break;
            }
            
        }
    }
     
    private void SetExtraText(TowerData data)
    {
        switch (stats)
        {
            case BattleStats.Range:
                SetText(data.baseRange);
                break;
            case BattleStats.Damage:
                SetText(data.baseDamage);
                break;
            case BattleStats.FireRate:
                SetText(data.fireRate);
                break;
            case BattleStats.CriticalChancel:
                SetText(data.criticalChancel);
                break;
            case BattleStats.CriticalMultiplier:
                SetText(data.criticalDamageMultiplier);
                break;
            case BattleStats.ArmorPenetration:
                SetText(data.armorPenetration);
                break;
            case BattleStats.MagicPenetration:
                SetText(data.magicPenetration);
                break;
            case BattleStats.DamageType:
                SetText(data.damageType);
                break;
        }
    }

}
