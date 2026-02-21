using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class ExpansionUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject uiPanel;
    public TextMeshProUGUI costText;
    public Button buyButton;
    public Button closeButton; // <--- NOWY PRZYCISK
    public event Action OnPanelClosed;


    private Vector2Int currentTarget;
    private Action<Vector2Int> onConfirmAction;

    private Vector3 targetWorldPos;
    private bool isVisible = false;

    // Cache kamery
    private CameraController camController;

    private void Start()
    {
        uiPanel.SetActive(false);
        buyButton.onClick.AddListener(OnBuyClicked);
    //
        // Obs�uga przycisku zamkni�cia
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);
        }

        // Znajd� kontroler kamery
        camController = Camera.main.GetComponent<CameraController>();
    }

    private void LateUpdate()
    {
        if (isVisible && uiPanel != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(targetWorldPos);
            if (screenPos.z > 0)
            {
                uiPanel.transform.position = screenPos;
            }
        }
    }

    public void ShowPanel(Vector2Int chunkCoord, int goldCost, Vector3 worldPos, Action<Vector2Int> onBuy)
    {
        currentTarget = chunkCoord;
        onConfirmAction = onBuy;
        targetWorldPos = worldPos;

        costText.text = $"Odkryj Teren\nKoszt: {goldCost} Z�ota";

        uiPanel.transform.position = Camera.main.WorldToScreenPoint(targetWorldPos);
        uiPanel.SetActive(true);
        isVisible = true;

        if (GameManager.Instance != null)
        {
            buyButton.interactable = ResourceManager.Instance.GetResourceAmount(ResourceType.Gold) >= goldCost;
        }

        // ZABLOKUJ KAMER�
        if (camController != null) camController.isInputLocked = true;
    }

    public void Hide()
    {
        uiPanel.SetActive(false);
        isVisible = false;

        if (camController != null) camController.isInputLocked = false;

        // POWIADOM O ZAMKNI�CIU
        OnPanelClosed?.Invoke();
    }

    void OnBuyClicked()
    {
        onConfirmAction?.Invoke(currentTarget);
        Hide();
    }
}