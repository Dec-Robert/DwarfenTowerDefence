using UnityEngine;
using UnityEngine.UIElements; // WA¯NE: Namespace dla UI Toolkit
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    [Header("UI Toolkit References")]
    public UIDocument uiDocument; // Przypisz tu komponent UIDocument z hierarchii

    // Cache elementów (¿eby nie szukaæ ich co klatkê)
    private Label labelHP;
    private Label labelGold;
    private Label labelWood;
    private Label labelStone;
    private Label labelFood;
    private Label labelPop;
    private Label labelTime;
    // public Label labelIron; // Opcjonalnie

    private void OnEnable()
    {
        // Pobieramy g³ówny korzeñ drzewa UI
        var root = uiDocument.rootVisualElement;

        // Wyszukujemy etykiety po nazwach zdefiniowanych w UXML (name="...")
        labelHP = root.Q<Label>("Label_HP");
        labelGold = root.Q<Label>("Label_Gold");
        labelWood = root.Q<Label>("Label_Wood");
        labelStone = root.Q<Label>("Label_Stone");
        labelFood = root.Q<Label>("Label_Food");
        labelPop = root.Q<Label>("Label_Pop");
        labelTime = root.Q<Label>("Label_Time");

        // Konfiguracja przycisków czasu
        SetupTimeButton(root, "Btn_Pause", 0f);
        SetupTimeButton(root, "Btn_1x", 1f);
        SetupTimeButton(root, "Btn_2x", 2f);
        SetupTimeButton(root, "Btn_3x", 3f);
        SetupTimeButton(root, "Btn_5x", 5f);
    }

    private void SetupTimeButton(VisualElement root, string btnName, float speed)
    {
        var btn = root.Q<Button>(btnName);
        if (btn != null)
        {
            btn.clicked += () => SetSpeed(speed);
        }
    }

    void Start()
    {
        // Subskrypcje (Bez zmian)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnHealthChanged += UpdateHpUI;
            UpdateHpUI(GameManager.Instance.mainGateHP);
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged += UpdateResourceUI;
            ResourceManager.Instance.UpdateAllUI();
        }
    }

    // WA¯NE: Update dla zegara, bo TimeCycleManager nie wysy³a eventu co klatkê
    void Update()
    {
        if (TimeCycleManager.Instance != null && labelTime != null)
        {
            labelTime.text = TimeCycleManager.Instance.GetFormattedTime();
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnHealthChanged -= UpdateHpUI;
        if (ResourceManager.Instance != null) ResourceManager.Instance.OnResourceChanged -= UpdateResourceUI;
    }

    // --- AKTUALIZACJA UI (Label.text zamiast TextMeshPro.text) ---

    private void UpdateHpUI(int currentHp)
    {
        if (labelHP != null)
        {
            labelHP.text = currentHp.ToString();
            labelHP.style.color = (currentHp <= 5) ? Color.red : Color.white;
        }
    }

    private void UpdateResourceUI(ResourceType type, int amount)
    {
        switch (type)
        {
            case ResourceType.Gold:
                if (labelGold != null) labelGold.text = amount.ToString();
                break;
            case ResourceType.Wood:
                if (labelWood != null) labelWood.text = amount.ToString();
                break;
            case ResourceType.Stone:
                if (labelStone != null) labelStone.text = amount.ToString();
                break;
            case ResourceType.Food:
                if (labelFood != null) labelFood.text = amount.ToString();
                break;
            case ResourceType.Population:
                if (labelPop != null && CitizenManager.Instance != null)
                {
                    int h = CitizenManager.Instance.GetRaceCount(Race.Humans);
                    int e = CitizenManager.Instance.GetRaceCount(Race.Elves);
                    int d = CitizenManager.Instance.GetRaceCount(Race.Dwarves);
                    labelPop.text = $"H:{h} | E:{e} | D:{d}";
                }
                break;
        }
    }

    void SetSpeed(float speed)
    {
        if (TimeCycleManager.Instance != null)
        {
            TimeCycleManager.Instance.SetTimeSpeed(speed);
        }
    }
}