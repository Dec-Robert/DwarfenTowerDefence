using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SingleResourceUI : MonoBehaviour
{
    [SerializeField] private ResourceVisualsSO ResourceVisuals;
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private bool trackResourceManager = true;
    
    [SerializeField] private TextMeshProUGUI ammount;
    [SerializeField] private Image icon;

    private bool isSubscribed;
    private bool isStaticValue;

    private void OnEnable()
    {
        Subscribe();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        UpdateIcon();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (!trackResourceManager || isStaticValue || isSubscribed || ResourceManager.Instance == null) return;

        ResourceManager.Instance.OnResourceChanged += HandleResourceChanged;
        isSubscribed = true;
        UpdateAmountText(ResourceManager.Instance.GetResourceAmount(resourceType));
    }

    private void Unsubscribe()
    {
        if (!isSubscribed) return;

        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceChanged -= HandleResourceChanged;

        isSubscribed = false;
    }

    private void HandleResourceChanged(ResourceType updatedType, int currentAmount)
    {
        if (updatedType != resourceType) return;
        UpdateAmountText(currentAmount);
    }

    private void UpdateAmountText(int value)
    {
        if (ammount != null)
            ammount.text = value.ToString();
    }

    private void UpdateIcon()
    {
        if (icon != null && ResourceVisuals != null)
        {
            icon.color = Color.white;
            icon.sprite = ResourceVisuals.GetIcon(resourceType);
        }
    }

    public void Setup(ResourceType type, float value, ResourceVisualsSO visuals = null)
    {
        isStaticValue = true;
        Unsubscribe();

        if (visuals != null) ResourceVisuals = visuals;
        resourceType = type;

        UpdateAmountText(Mathf.RoundToInt(value));
        UpdateIcon();
    }

    public void Setup(ResourceType type, int value, ResourceVisualsSO visuals = null)
    {
        Setup(type, (float)value, visuals);
    }
}
