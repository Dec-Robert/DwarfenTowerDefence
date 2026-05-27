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
        Debug.Log("OnPointerEnter");
        BuildMenuUI.Instance.OpenExtraInfo(buildingData);
    }
    
    public void PointerExit()
    {
        Debug.Log("OnPointerExit");
        BuildMenuUI.Instance.HideExtraInfo();
    }
    
}
