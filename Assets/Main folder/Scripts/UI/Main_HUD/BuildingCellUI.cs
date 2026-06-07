using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class BuildingCellUI : MonoBehaviour
{
    private BuildingData buildingData;
    
    public void Setup(BuildingData data)
    {

        buildingData = data;
    }

    public void PointerEnter()
    {
        //Debug.Log("OnPointerEnter");
        BuildingTooltipManagerUI.Instance.ShowTooltip(buildingData);
    }
    
    public void PointerExit()
    {
        //Debug.Log("OnPointerExit");
        BuildingTooltipManagerUI.Instance.HideTooltip();
    }
    
}
