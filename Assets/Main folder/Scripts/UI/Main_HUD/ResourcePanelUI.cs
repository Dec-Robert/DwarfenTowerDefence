using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ResourcePanelUI : MonoBehaviour
{
    [Serializable]
    public struct ResourceUIElement
    {
        public ResourceType Type;
        public TextMeshProUGUI AmountText;
    }
    
    [SerializeField] private List<ResourceUIElement> resourceElements;

    // Zoptymalizowana mapa referencji
    private Dictionary<ResourceType, TextMeshProUGUI> uiDictionary = new Dictionary<ResourceType, TextMeshProUGUI>();

    private void Awake()
    {
        // Generowanie słownika przy starcie
        foreach (var element in resourceElements)
        {
            if (!uiDictionary.ContainsKey(element.Type) && element.AmountText != null)
            {
                uiDictionary.Add(element.Type, element.AmountText);
            }
            else
            {
                Debug.LogWarning($"[ResourcePanelUI] Zduplikowany typ lub brak referencji Text dla: {element.Type}");
            }
        }
    }

    private void Start()
    {
        // Subskrypcja zdarzenia z ResourceManager
        ResourceManager.Instance.OnResourceChanged += UpdateResourceText;
        
        // Wymuszenie pierwszej aktualizacji po podpięciu UI
        ResourceManager.Instance.UpdateAllUI();
    }

    private void OnDestroy()
    {
        // Zwalnianie subskrypcji - krytyczne dla zarządzania pamięcią
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged -= UpdateResourceText;
        }
    }

    private void UpdateResourceText(ResourceType type, float amount)
    {
        if (uiDictionary.TryGetValue(type, out TextMeshProUGUI textComponent))
        {
            // Formatowanie float do wyświetlania. "F0" zaokrągla do liczby całkowitej.
            // Alternatywa: Mathf.FloorToInt(amount).ToString()
            textComponent.text = amount.ToString("F0"); 
        }
    }
}