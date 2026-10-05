using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SingleResourceUI : MonoBehaviour
{
    [SerializeField] private ResourceVisualsSO ResourceVisuals;
    [SerializeField] private ResourceType resourceType;
    
    [SerializeField] private TextMeshProUGUI ammount;
    [SerializeField] private Image icon;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        icon.color = Color.white;
        icon.sprite = ResourceVisuals.GetIcon(resourceType);
    }

    public void Setup(ResourceType type, float value, ResourceVisualsSO visuals = null)
    {
        if (visuals != null) ResourceVisuals = visuals;
        resourceType = type;

        if (ammount != null)
        {
            ammount.text = Mathf.RoundToInt(value).ToString();
        }

        if (icon != null && ResourceVisuals != null)
        {
            icon.color = Color.white;
            icon.sprite = ResourceVisuals.GetIcon(resourceType);
        }
    }

    public void Setup(ResourceType type, int value, ResourceVisualsSO visuals = null)
    {
        Setup(type, (float)value, visuals);
    }
    
}
