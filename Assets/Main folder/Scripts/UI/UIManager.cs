using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    
    public 
    
    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void OnEnable()
    {
        
    }//

    private void SetupTimeButton(VisualElement root, string btnName, float speed)
    {
        var btn = root.Q<Button>(btnName);
        if (btn != null) btn.clicked += () => SetSpeed(speed);
    }

    void Start()
    {
        
    }

    void Update()
    {
        if (TimeCycleManager.Instance != null)
        {
            HandleInput();
        }
    }

    void HandleInput()
    {
       
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnHealthChanged -= UpdateHpUI;
        if (ResourceManager.Instance != null) ResourceManager.Instance.OnResourceChanged -= UpdateResourceUI;
    }

    private void UpdateHpUI(int currentHp)
    {
        
    }

    private void UpdateResourceUI(ResourceType type, float amount)
    {
        
    }

    void SetSpeed(float speed)
    {
        if (TimeCycleManager.Instance != null)
        {
            TimeCycleManager.Instance.SetTimeSpeed(speed);
        }
    }
}