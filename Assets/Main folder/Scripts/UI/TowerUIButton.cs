using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TowerUIBtn : MonoBehaviour
{
    [Header("Dane")]
    public TowerData towerData; // Tu przeci¹gniesz plik (Archer lub Cannon)

    [Header("Elementy UI Przycisku")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI costText;
    public Image iconImage;
    public Button myButton;

    void Start()
    {
        // Automatyczne uzupe³nienie wygl¹du przycisku na podstawie danych
        if (towerData != null)
        {
            SetupButton();
        }
    }

    void SetupButton()
    {
        if (nameText != null) nameText.text = towerData.towerName;
        if (costText != null) costText.text = towerData.cost.ToString() + " G";

        if (towerData.icon != null && iconImage != null)
        {
            iconImage.sprite = towerData.icon;
        }

        // Dodanie funkcjonalnoœci klikniêcia
        myButton.onClick.AddListener(OnBtnClicked);
    }

    void OnBtnClicked()
    {
        
        TowerBuilder.Instance.SelectTower(towerData);

    }

    void Update()
    {

        bool canAfford = GameManager.Instance.gold >= towerData.cost;
        myButton.interactable = canAfford; 

    }
}