using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pojedyncza karta wyboru zaklęcia w Pnl_SpellChoiceModal (prefab Card_Spell).
/// </summary>
public class SpellChoiceCardUI : MonoBehaviour
{
    [SerializeField] private Image imgIcon;
    [SerializeField] private TextMeshProUGUI txtTitle;
    [SerializeField] private TextMeshProUGUI txtDescription;
    [SerializeField] private TextMeshProUGUI txtDamage;
    [SerializeField] private TextMeshProUGUI txtStats;
    [SerializeField] private Button btnChoose;
    [SerializeField] private TextMeshProUGUI txtChooseLabel;

    public void AutoBindReferences()
    {
        imgIcon ??= transform.Find("Img_Icon")?.GetComponent<Image>();
        txtTitle ??= transform.Find("Txt_Title")?.GetComponent<TextMeshProUGUI>();
        txtDescription ??= transform.Find("Txt_Desc")?.GetComponent<TextMeshProUGUI>();
        txtDamage ??= transform.Find("Txt_Damage")?.GetComponent<TextMeshProUGUI>();
        txtStats ??= transform.Find("Txt_Stats")?.GetComponent<TextMeshProUGUI>();
        btnChoose ??= transform.Find("Btn_Choose")?.GetComponent<Button>();
        txtChooseLabel ??= transform.Find("Btn_Choose/Txt_Label")?.GetComponent<TextMeshProUGUI>();
    }

    public void Setup(SpellDataSO spell, Action onChoose)
    {
        AutoBindReferences();
        if (spell == null) return;

        if (imgIcon != null)
        {
            imgIcon.sprite = spell.icon;
            imgIcon.color = spell.icon != null ? Color.white : spell.indicatorColor;
        }

        if (txtTitle != null)
            txtTitle.text = spell.spellName;

        if (txtDescription != null)
            txtDescription.text = spell.description;

        if (txtDamage != null)
            txtDamage.text = FormatDamageText(spell.damageType, spell.baseDamage);

        if (txtStats != null)
            txtStats.text = FormatCooldownStats(spell);

        if (txtChooseLabel != null)
            txtChooseLabel.text = LocalizationManager.Get("DRUID_CAVE_CHOOSE_BTN", "Wybierz");

        if (btnChoose != null)
        {
            btnChoose.onClick.RemoveAllListeners();
            if (onChoose != null)
            {
                btnChoose.onClick.AddListener(() => onChoose());
            }
        }
    }

    public static string FormatDamageText(DamageType damageType, float damage)
    {
        string typeName = damageType switch
        {
            DamageType.Physical => LocalizationManager.Get("damage_type_physical", "Fizyczne"),
            DamageType.Magic => LocalizationManager.Get("damage_type_magic", "Magiczne"),
            DamageType.True => LocalizationManager.Get("damage_type_true", "Czyste"),
            _ => damageType.ToString()
        };

        string hex = damageType switch
        {
            DamageType.Physical => "#FF8C34",
            DamageType.Magic => "#00B0F0",
            DamageType.True => "#FFFFFF",
            _ => "#FFFFFF"
        };

        return $"DMG: {damage:0} (<color={hex}>{typeName}</color>)";
    }

    private static string FormatCooldownStats(SpellDataSO spell)
    {
        return spell.castLimitMode switch
        {
            SpellCastLimitMode.LimitedUsesPerNight => $"{spell.maxUsesPerNight} na noc",
            SpellCastLimitMode.RechargeableCharges => $"{spell.maxCharges} ład. | {spell.chargeRechargeSeconds:0}s / ład.",
            SpellCastLimitMode.CooldownOnly => $"Odnowienie: {spell.cooldownSeconds:0}s",
            _ => string.Empty
        };
    }
}
