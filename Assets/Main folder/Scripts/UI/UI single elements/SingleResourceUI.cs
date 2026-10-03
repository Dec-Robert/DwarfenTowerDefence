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

    // Update is called once per frame
    void Update()
    {
        
    }
}
