using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Komponent wyzwalający wyświetlanie dobowego tooltipa bilansu surowców
/// po najechaniu kursorem myszy na element UI (kafelek surowca w HUD lub pigułkę bilansu).
/// </summary>
public class ResourceTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private ResourceType resourceType;
    private RectTransform rectTransform;

    public ResourceType ResourceType
    {
        get => resourceType;
        set => resourceType = value;
    }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void Setup(ResourceType type)
    {
        resourceType = type;
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (ResourceTooltipUI.Instance != null)
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();

            RectTransform target = rectTransform;
            var singleRes = GetComponentInParent<SingleResourceUI>();
            if (singleRes != null)
            {
                target = singleRes.GetComponent<RectTransform>();
            }

            ResourceTooltipUI.Instance.Show(resourceType, target);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (ResourceTooltipUI.Instance != null)
        {
            ResourceTooltipUI.Instance.Hide();
        }
    }

    private void OnDisable()
    {
        if (ResourceTooltipUI.Instance != null)
        {
            ResourceTooltipUI.Instance.Hide();
        }
    }
}
