using Mono.Cecil;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResourceRowUI : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI amountText;

    public void Setup(Sprite sprite, float ammout)
    {
        icon.sprite = sprite;
        amountText.text = ammout.ToString();
    }
    
    public void Setup(ResourceType resource, float ammout)
    {
        icon.transform.gameObject.SetActive(false);
        amountText.text = resource.ToString() +" "+ ammout.ToString();
    }
}
