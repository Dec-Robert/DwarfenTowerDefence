using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI hpText;
    //Resources
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI woodText;
    public TextMeshProUGUI stoneText;
    public TextMeshProUGUI foodText;
    public TextMeshProUGUI populationText;
    public TextMeshProUGUI ironText;
    public TextMeshProUGUI coalText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameManager.Instance.OnHealthChanged += UpdateHpUI;
        UpdateHpUI(GameManager.Instance.mainGateHP);

        ResourceManager.Instance.OnResourceChanged += UpdateResourceUI;
        ResourceManager.Instance.UpdateAllUI();
    }

    // Update is called once per frame
    void Update()
    {

    }


    // --- OBS£UGA ZDROWIA (GameManager) ---
    private void UpdateHpUI(int currentHp)
    {
        if (hpText != null)
        {
            hpText.text = $"HP: {currentHp}";

            // Zmiana koloru na czerwony gdy krytycznie ma³o ¿ycia
            hpText.color = (currentHp <= 5) ? Color.red : Color.white;
        }
    }

    // --- OBS£UGA SUROWCÓW (ResourceManager) ---
    private void UpdateResourceUI(ResourceType type, int amount)
    {
        // Tutaj decydujemy, który tekst zaktualizowaæ w zale¿noœci od typu surowca
        switch (type)
        {
            case ResourceType.Gold:
                if (goldText) goldText.text = $"G: {amount}";
                break;

            case ResourceType.Wood:
                if (woodText) woodText.text = $"D: {amount}";
                break;

            case ResourceType.Stone:
                if (stoneText) stoneText.text = $"K: {amount}";
                break;

            case ResourceType.Food:
                if (foodText) foodText.text = $"J: {amount}";
                break;

            case ResourceType.Population:
                if (populationText) populationText.text = $"L: {amount}";
                break;

            case ResourceType.Iron:
                if (ironText) ironText.text = $"¯: {amount}";
                break;

            case ResourceType.Coal:
                if (coalText) coalText.text = $"W: {amount}";
                break;

        }
    }

    public void NextWave()
    {
        if (GameManager.Instance.currentGameState != GameManager.gameStates.InWave)
        {
            GameManager.Instance.StartWave();

        }
    }

    void OnDestroy()
    {
        // Pamiêtaj o odsubskrybowaniu, ¿eby unikn¹æ b³êdów przy prze³adowaniu sceny
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnHealthChanged -= UpdateHpUI;
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged -= UpdateResourceUI;
        }
    }
}
