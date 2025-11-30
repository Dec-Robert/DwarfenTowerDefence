using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public TextMeshProUGUI goldText;
    public TextMeshProUGUI hpText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GameManager.Instance.OnGoldChanged += UpdateGoldUI;
        GameManager.Instance.OnHealthChanged += UpdateHpUI;
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void UpdateGoldUI(int currentGold)
    {
        // Mo¿esz tu dodaæ formatowanie, np. "100 G"
        goldText.text = $"Z³oto: {currentGold}";
    }

    private void UpdateHpUI(int currentHp)
    {
        hpText.text = $"HP: {currentHp}";

        // Opcjonalnie: zmiana koloru na czerwony gdy ma³o ¿ycia
        if (currentHp <= 5) hpText.color = Color.red;
        else hpText.color = Color.white;
    }

    public void NextWave()
    {
        if(GameManager.Instance.currentGameState != GameManager.gameStates.InWave)
        {
            GameManager.Instance.StartWave();

        }
    }
}
