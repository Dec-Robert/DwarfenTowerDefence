using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShiftInfoPanelUI : MonoBehaviour
{
    //Objects in unity
    public TextMeshProUGUI shiftNumberText;
    public GameObject workersStatesPanel;
    
    //Prefabs
    public GameObject shiftImagePrefab;
    public Sprite freeSpot;
    public Sprite occupiedSpot;

    
    //Outer Scripts initializes once
    public void Initialize(int shiftNumber)
    {
        shiftNumberText.text = "Shift " + shiftNumber.ToString();
        
        for (int i = 0; i < 10; i++)
        {
           GameObject go = Instantiate(shiftImagePrefab,workersStatesPanel.transform);
           Image img = go.GetComponent<Image>();
           img.sprite = freeSpot;
        }
    }

    //Outer scripts setups everytime it changes
    public void Setup(int working,int maxWorkers)
    {
        hideAllWorkersSlots();
        for (int i = 0; i < maxWorkers; i++)
        {
            workersStatesPanel.transform.GetChild(i).gameObject.SetActive(true);
            Image img = workersStatesPanel.transform.GetChild(i).GetComponent<Image>();
            if (i<working) img.sprite = occupiedSpot;
            else img.sprite = freeSpot;
            
            Debug.Log( "Max workers" +maxWorkers +" "+  i );
            
        }
    }

    private void hideAllWorkersSlots()
    {
        foreach (Transform worker in  workersStatesPanel.transform)
        {
            worker.gameObject.SetActive(false);
        }
    }
}
