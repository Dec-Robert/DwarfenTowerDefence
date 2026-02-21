using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

public class BuildUIButton : MonoBehaviour
{
    [Header("DANE (Przypisz w Inspektorze!)")]
    public BuildingData myBuildingData;

    [Header("Elementy Wizualne Przycisku")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI costText;
    public Image iconImage;
    public Button btn;
    //
    void Start()
    {
        // Je�li dane zosta�y przypisane r�cznie w Unity, konfigurujemy przycisk na starcie
        if (myBuildingData != null)
        {
            Setup(myBuildingData);
        }
        btn = GetComponent<Button>();
        iconImage = GetComponent<Image>();
    }

    // Metoda publiczna - w razie gdyby� kiedy� chcia� jednak generowa� przyciski automatycznie
    public void Setup(BuildingData data)
    {
        myBuildingData = data;

        if (nameText != null) nameText.text = myBuildingData.buildingName;

        if (iconImage != null) iconImage.sprite = myBuildingData.icon;


        if (costText != null)
        {
            StringBuilder sb = new StringBuilder();
            if (myBuildingData.constructionCost != null)
            {
                foreach (var cost in myBuildingData.constructionCost)
                {
                    // Np. 100 G
                    // Mo�esz tu doda� logik� skracania liter (Gold -> G, Wood -> W)
                    string shortName = cost.type.ToString().Substring(0, 1);
                    sb.AppendLine($"{cost.amount}{shortName}");
                }
            }
            costText.text = sb.ToString();
        }

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.SelectBuildingToBuild(myBuildingData);
        }
    }

    void Update()
    {
        // Sprawdzanie czy sta� gracza (co klatk�)
        if (ResourceManager.Instance != null && myBuildingData != null)
        {
            bool canAfford = true;
            foreach (var cost in myBuildingData.constructionCost)
            {
                if (!ResourceManager.Instance.CanAfford(cost.type, cost.amount))
                {
                    canAfford = false;
                    break;
                }
            }

            // Zamiast tylko wy��cza�, mo�emy np. zmienia� kolor na czerwony
            btn.interactable = canAfford;

            // Opcjonalnie: Zmiana koloru tekstu kosztu, je�li nie sta�
            if (costText != null)
                costText.color = canAfford ? Color.white : Color.red;
        }
    }
}