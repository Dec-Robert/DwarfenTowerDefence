using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    [Header("UI Toolkit References")]
    public UIDocument uiDocument;

    // Cache elementów
    private Label labelHP, labelGold, labelWood, labelStone, labelFood, labelPop, labelTime, labelIron, labelCoal;

    private void OnEnable()
    {
        if (uiDocument == null) return;
        var root = uiDocument.rootVisualElement;

        labelHP = root.Q<Label>("Label_HP");
        labelGold = root.Q<Label>("Label_Gold");
        labelWood = root.Q<Label>("Label_Wood");
        labelStone = root.Q<Label>("Label_Stone");
        labelFood = root.Q<Label>("Label_Food");
        labelPop = root.Q<Label>("Label_Pop");
        labelTime = root.Q<Label>("Label_Time");
        labelCoal = root.Q<Label>("Label_Coal");
        labelIron = root.Q<Label>("Label_Iron");

        SetupTimeButton(root, "Btn_Pause", 0f);
        SetupTimeButton(root, "Btn_1x", 1f);
        SetupTimeButton(root, "Btn_2x", 2f);
        SetupTimeButton(root, "Btn_3x", 3f);
        SetupTimeButton(root, "Btn_5x", 5f);
    }

    private void SetupTimeButton(VisualElement root, string btnName, float speed)
    {
        var btn = root.Q<Button>(btnName);
        if (btn != null) btn.clicked += () => SetSpeed(speed);
    }

    void Start()
    {
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

    void Update()
    {
        if (TimeCycleManager.Instance != null)
        {
            // Aktualizacja tekstu zegara
            if (labelTime != null)
                labelTime.text = TimeCycleManager.Instance.GetFormattedTime();

            // --- OBS£UGA KLAWISZY (NOWOŒÆ) ---
            HandleInput();
        }
    }

    // Nowa metoda do obs³ugi skrótów klawiszowych
    void HandleInput()
    {
        // Spacja - Toggle Pause
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TimeCycleManager.Instance.TogglePause();
        }

        // Klawisze numeryczne (nad literami)
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetSpeed(1f);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetSpeed(2f);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetSpeed(3f);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SetSpeed(5f); // 4 klawisz = 5x speed
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnHealthChanged -= UpdateHpUI;
        if (ResourceManager.Instance != null) ResourceManager.Instance.OnResourceChanged -= UpdateResourceUI;
    }

    private void UpdateHpUI(int currentHp)
    {
        if (labelHP != null)
        {
            labelHP.text = currentHp.ToString();
            labelHP.style.color = (currentHp <= 5) ? Color.red : Color.white;
        }
    }

    private void UpdateResourceUI(ResourceType type, float amount)
    {
        string txt = amount.ToString("F1");
        switch (type)
        {
            case ResourceType.Gold: if (labelGold != null) labelGold.text = amount.ToString(); break;
            case ResourceType.Wood: if (labelWood != null) labelWood.text = amount.ToString(); break;
            case ResourceType.Stone: if (labelStone != null) labelStone.text = amount.ToString(); break;
            case ResourceType.Food: if (labelFood != null) labelFood.text = amount.ToString(); break;
            case ResourceType.Coal: if (labelCoal != null) labelCoal.text = amount.ToString(); break;
            case ResourceType.Iron: if (labelIron != null) labelIron.text = amount.ToString(); break;

            case ResourceType.Population:
                if (labelPop != null)
                {
                    if (CitizenManager.Instance != null)
                    {
                        int h = CitizenManager.Instance.GetRaceCount(Race.Humans);
                        int e = CitizenManager.Instance.GetRaceCount(Race.Elves);
                        int d = CitizenManager.Instance.GetRaceCount(Race.Dwarves);
                        labelPop.text = $"H:{h} | E:{e} | D:{d}";
                    }
                    else labelPop.text = $"Pop: {amount}";
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