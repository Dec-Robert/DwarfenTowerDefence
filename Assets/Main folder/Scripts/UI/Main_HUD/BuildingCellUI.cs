using UnityEngine;
using UnityEngine.EventSystems;

public class BuildingCellUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private BuildingData buildingData;
    
    public void Setup(BuildingData data)
    {
        buildingData = data;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        PointerEnter();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        PointerExit();
    }

    private void OnDisable()
    {
        PointerExit();
    }

    public void PointerEnter()
    {
        if (Tooltip_BuildingExtraInfo.Instance != null && buildingData != null)
        {
            Tooltip_BuildingExtraInfo.Instance.ShowTooltip(buildingData);
        }
        else if (BuildingTooltipManagerUI.Instance != null && buildingData != null)
        {
            BuildingTooltipManagerUI.Instance.ShowTooltip(buildingData);
        }
    }
    
    public void PointerExit()
    {
        if (Tooltip_BuildingExtraInfo.Instance != null)
        {
            Tooltip_BuildingExtraInfo.Instance.HideTooltip();
        }
        else if (BuildingTooltipManagerUI.Instance != null)
        {
            BuildingTooltipManagerUI.Instance.HideTooltip();
        }
    }
}
