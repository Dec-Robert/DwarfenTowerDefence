using Mono.Cecil;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResourceRowUI : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI amountText;
    
    public void Setup(Sprite icon, float ammout)
    {
        this.icon.sprite = icon;
        amountText.text = ammout.ToString();
    }
    
}
