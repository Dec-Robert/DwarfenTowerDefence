using TMPro;
using UnityEngine;

public class GateHPUpdater : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform gateHPBarVisualizer;
    [SerializeField] private TextMeshProUGUI gateHPTxt;

    private float gateVisualizeMaxSizeInPixels;
    private int maxGateHP = -1;

    private void Awake()
    {
        if (gateHPBarVisualizer != null)
        {
            gateVisualizeMaxSizeInPixels = gateHPBarVisualizer.sizeDelta.x;
        }
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnHealthChanged += UpdateUI;
        }
    }

    private void Start()
    {
        UpdateUI(GameManager.Instance.mainGateHP);
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnHealthChanged -= UpdateUI;
        }
    }

    private void UpdateUI(int hp)
    {
        int currentHP = Mathf.Max(0, hp);
        
        if (maxGateHP <= 0 && currentHP > 0) maxGateHP = currentHP;
        float healthRatio = maxGateHP > 0 ? Mathf.Clamp01((float)currentHP / maxGateHP) : 0f;
        
        if (gateHPBarVisualizer != null)
        {
            float targetWidth = gateVisualizeMaxSizeInPixels * healthRatio;
            gateHPBarVisualizer.sizeDelta = new Vector2(targetWidth, gateHPBarVisualizer.sizeDelta.y);
        }

        gateHPTxt.text = (currentHP + "/" + maxGateHP).ToString();


    }
}