using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("G³ówne Statystyki")]
    public TextMeshProUGUI hpText;

    [Header("Surowce")]
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI woodText;
    public TextMeshProUGUI stoneText;
    public TextMeshProUGUI foodText;
    public TextMeshProUGUI populationText;
    public TextMeshProUGUI ironText;
    public TextMeshProUGUI coalText; // Dodane dla wêgla

    [Header("Kontrola Czasu")]
    public Button btnPause;
    public Button btn1x;
    public Button btn2x;
    public Button btn3x;
    public Button btn5x;

    void Start()
    {
        // 1. Subskrypcja HP z GameManagera
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnHealthChanged += UpdateHpUI;
            UpdateHpUI(GameManager.Instance.mainGateHP);
        }

        // 2. Subskrypcja Surowców z ResourceManagera
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged += UpdateResourceUI;
            // Wymuszamy odœwie¿enie wszystkich surowców na start, ¿eby nie by³o pustych pól
            ResourceManager.Instance.UpdateAllUI();
        }

        // 3. Konfiguracja przycisków czasu
        if (btnPause) btnPause.onClick.AddListener(() => SetSpeed(0f));
        if (btn1x) btn1x.onClick.AddListener(() => SetSpeed(1f));
        if (btn2x) btn2x.onClick.AddListener(() => SetSpeed(2f));
        if (btn3x) btn3x.onClick.AddListener(() => SetSpeed(3f));
        if (btn5x) btn5x.onClick.AddListener(() => SetSpeed(5f));
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnHealthChanged -= UpdateHpUI;
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged -= UpdateResourceUI;
        }
    }

    // --- AKTUALIZACJA UI ---

    private void UpdateHpUI(int currentHp)
    {
        if (hpText != null)
        {
            hpText.text = $"HP: {currentHp}";
            hpText.color = (currentHp <= 5) ? Color.red : Color.white;
        }
    }

    private void UpdateResourceUI(ResourceType type, int amount)
    {
        switch (type)
        {
            case ResourceType.Gold:
                if (goldText) goldText.text = $"Z³oto: {amount}";
                break;

            case ResourceType.Wood:
                if (woodText) woodText.text = $"Drewno: {amount}";
                break;

            case ResourceType.Stone:
                if (stoneText) stoneText.text = $"Kamieñ: {amount}";
                break;

            case ResourceType.Food:
                if (foodText) foodText.text = $"Jedzenie: {amount}";
                break;

            case ResourceType.Coal:
                if (coalText) coalText.text = $"Wêgiel: {amount}";
                break;

            case ResourceType.Iron:
                if (ironText) ironText.text = $"¯elazo: {amount}";
                break;

            case ResourceType.Population:
                if (populationText != null)
                {
                    // Jeœli mamy CitizenManagera, pokazujemy podzia³ na rasy
                    if (CitizenManager.Instance != null)
                    {
                        int h = CitizenManager.Instance.GetRaceCount(Race.Humans);
                        int e = CitizenManager.Instance.GetRaceCount(Race.Elves);
                        int d = CitizenManager.Instance.GetRaceCount(Race.Dwarves);
                        populationText.text = $"H:{h} | E:{e} | D:{d}";
                    }
                    else
                    {
                        // Fallback, gdyby nie by³o managera
                        populationText.text = $"Pop: {amount}";
                    }
                }
                break;
        }
    }

    // --- KONTROLA CZASU ---

    void SetSpeed(float speed)
    {
        if (TimeCycleManager.Instance != null)
        {
            TimeCycleManager.Instance.SetTimeSpeed(speed);
        }
    }
}