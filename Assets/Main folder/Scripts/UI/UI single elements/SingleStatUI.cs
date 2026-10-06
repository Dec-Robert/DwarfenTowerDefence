using TMPro;
using UnityEngine;

public class SingleStatUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI txtLabel;
    [SerializeField] private TextMeshProUGUI txtValue;

    private void Awake()
    {
        AutoFindReferences();
    }

    private void OnValidate()
    {
        AutoFindReferences();
    }

    private void AutoFindReferences()
    {
        if (txtLabel == null || txtValue == null)
        {
            Transform descTransform = transform.Find("Txt_Description");
            if (descTransform != null)
            {
                txtLabel = descTransform.GetComponent<TextMeshProUGUI>();
            }

            Transform valTransform = transform.Find("Txt_Value");
            if (valTransform != null)
            {
                txtValue = valTransform.GetComponent<TextMeshProUGUI>();
            }

            // Fallback if names differ
            if (txtLabel == null || txtValue == null)
            {
                TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                if (texts.Length > 0 && txtLabel == null) txtLabel = texts[0];
                if (texts.Length > 1 && txtValue == null) txtValue = texts[1];
            }
        }
    }

    public void Setup(string label, string value, Color? valueColor = null)
    {
        if (txtLabel == null || txtValue == null)
        {
            AutoFindReferences();
        }

        if (txtLabel != null)
        {
            txtLabel.text = label;
        }

        if (txtValue != null)
        {
            txtValue.text = value;
            if (valueColor.HasValue)
            {
                txtValue.color = valueColor.Value;
            }
        }
    }
}
