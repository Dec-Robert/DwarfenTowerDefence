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
        Subscribe();
    }

    private void Start()
    {
        Subscribe();
        if (GameManager.Instance != null)
        {
            maxGateHP = GameManager.Instance.mainGateHP;
            UpdateUI(GameManager.Instance.mainGateHP);
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnHealthChanged -= UpdateUI;
            GameManager.Instance.OnHealthChanged += UpdateUI;
        }
    }

    private void Unsubscribe()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnHealthChanged -= UpdateUI;
        }
    }

    private void UpdateUI(int hp)
    {
        int currentHP = Mathf.Max(0, hp);

        float healthRatio = maxGateHP > 0 ? Mathf.Clamp01((float)currentHP / maxGateHP) : 0f;
        
        if (gateHPBarVisualizer != null)
        {
            if (gateVisualizeMaxSizeInPixels <= 0f)
            {
                gateVisualizeMaxSizeInPixels = gateHPBarVisualizer.sizeDelta.x;
            }
            float targetWidth = gateVisualizeMaxSizeInPixels * healthRatio;
            gateHPBarVisualizer.sizeDelta = new Vector2(targetWidth, gateHPBarVisualizer.sizeDelta.y);
        }

        if (gateHPTxt != null)
        {
            gateHPTxt.text = $"{currentHP}/{maxGateHP}";
        }
    }
}