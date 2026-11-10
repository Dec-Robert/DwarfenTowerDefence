using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Komponent reprezentujący pojedynczą pozycję statystyki bojowej (Pnl_CombatStatItem).
/// Obsługuje nazwę statystyki, wartość bazową (opcjonalnie przekreśloną) oraz wartość bieżącą.
/// </summary>
public class CombatStatItemUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image imgIcon;
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private TextMeshProUGUI txtBaseValue;
    [SerializeField] private TextMeshProUGUI txtCurrentValue;

    public Image ImgIcon => imgIcon;
    public TextMeshProUGUI TxtName => txtName;
    public TextMeshProUGUI TxtBaseValue => txtBaseValue;
    public TextMeshProUGUI TxtCurrentValue => txtCurrentValue;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void OnValidate()
    {
        AutoFindReferences();
    }

    public void AutoFindReferences()
    {
        if (imgIcon == null)
        {
            imgIcon = transform.Find("Img_StatType")?.GetComponent<Image>()
                   ?? transform.Find("Img_StatIcon")?.GetComponent<Image>()
                   ?? transform.Find("Img_Icon")?.GetComponent<Image>();
        }

        if (txtName == null)
        {
            txtName = transform.Find("Txt_StatName")?.GetComponent<TextMeshProUGUI>();
        }

        Transform valuesTr = transform.Find("Pnl_Values");
        if (txtBaseValue == null)
        {
            txtBaseValue = valuesTr != null
                ? valuesTr.Find("Txt_BaseValue")?.GetComponent<TextMeshProUGUI>()
                : transform.Find("Txt_BaseValue")?.GetComponent<TextMeshProUGUI>();
        }

        if (txtCurrentValue == null)
        {
            txtCurrentValue = valuesTr != null
                ? valuesTr.Find("Txt_CurrentValue")?.GetComponent<TextMeshProUGUI>()
                : transform.Find("Txt_CurrentValue")?.GetComponent<TextMeshProUGUI>();
        }

        // Elastyczny fallback, gdyby nazwy w hierarchii się różniły
        if (txtName == null || txtCurrentValue == null)
        {
            var tmps = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var tmp in tmps)
            {
                if (txtName == null && tmp.name.IndexOf("Name", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    txtName = tmp;
                }
                else if (txtBaseValue == null && tmp.name.IndexOf("Base", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    txtBaseValue = tmp;
                }
                else if (txtCurrentValue == null && tmp.name.IndexOf("Current", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    txtCurrentValue = tmp;
                }
            }
        }
    }

    public void SetStatName(string name)
    {
        if (txtName != null)
        {
            txtName.text = name;
        }
    }

    public void SetIcon(Sprite icon)
    {
        if (imgIcon != null)
        {
            if (icon != null)
            {
                imgIcon.gameObject.SetActive(true);
                imgIcon.sprite = icon;
                imgIcon.color = Color.white;
            }
            else
            {
                imgIcon.gameObject.SetActive(false);
            }
        }
    }

    public void SetValues(string currentValue, string baseValue = null, Color? currentColor = null)
    {
        if (txtCurrentValue != null)
        {
            txtCurrentValue.text = currentValue;
            if (currentColor.HasValue)
            {
                txtCurrentValue.color = currentColor.Value;
            }
        }

        if (txtBaseValue != null)
        {
            if (!string.IsNullOrEmpty(baseValue))
            {
                txtBaseValue.gameObject.SetActive(true);
                txtBaseValue.text = $"<s>{baseValue}</s>";
            }
            else
            {
                txtBaseValue.gameObject.SetActive(false);
            }
        }
    }
}
