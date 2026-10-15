using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Kontroler panelu Pnl_TopBonusInfo w górnym HUD.
/// Prezentuje wiszące pigułki podglądu bilansu surowców na koniec bieżącej fazy gry.
/// - Zielone "+X" przy zysku netto.
/// - Czerwone "-X" przy ubytku netto.
/// - Styl kryzysowy "! -X" z czerwonym tłem przy deficycie (gdy w skarbcu zabraknie surowca).
/// - 0 kapsułek (wszystkie ukryte) w fazie Nocy oraz gdy bilans surowca wynosi 0.
/// </summary>
public class TopBonusInfoUI : MonoBehaviour
{
    public static TopBonusInfoUI Instance { get; private set; }

    [Serializable]
    public class BonusSlotItem
    {
        public ResourceType resourceType;
        public GameObject rootObject;
        public Image backgroundImage;
        public TextMeshProUGUI amountText;
        [HideInInspector] public Color defaultBgColor;
    }

    [Header("Sloty Surowców")]
    [SerializeField] private List<BonusSlotItem> bonusSlots = new();

    [Header("Kolorystyka Tekstu")]
    [SerializeField] private Color positiveColor = new Color32(74, 222, 128, 255); // #4ADE80 Jasna zieleń
    [SerializeField] private Color negativeColor = new Color32(248, 113, 113, 255); // #F87171 Miękka czerwień
    [SerializeField] private Color deficitTextColor = Color.white;

    [Header("Kolorystyka Tła")]
    [SerializeField] private Color normalBgColor = new Color32(32, 32, 36, 240);
    [SerializeField] private Color crisisBgColor = new Color32(180, 24, 24, 245);

    [Header("Formatowanie")]
    [SerializeField] private float fontSize = 12f; // Zgodnie z wytycznymi interfejsu w GEMINI.md

    private float pollTimer = 0f;
    private const float PollInterval = 0.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        var panel = GameObject.Find("Pnl_TopBonusInfo");
        if (panel != null && panel.GetComponent<TopBonusInfoUI>() == null)
        {
            panel.AddComponent<TopBonusInfoUI>();
        }
    }

    private void Reset()
    {
        FindAndConfigureSlots();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        FindAndConfigureSlots();
    }

    private void Start()
    {
        SubscribeEvents();
        RefreshUI();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        pollTimer += Time.unscaledDeltaTime;
        if (pollTimer >= PollInterval)
        {
            pollTimer = 0f;
            RefreshUI();
        }
    }

    private void FindAndConfigureSlots()
    {
        if (bonusSlots != null && bonusSlots.Count > 0)
        {
            // Sloty już skonfigurowane w inspektorze
            foreach (var slot in bonusSlots)
            {
                DisableConflictComponents(slot.rootObject);
            }
            return;
        }

        bonusSlots = new List<BonusSlotItem>();

        Transform leftContainer = transform.Find("Pnl_Resource_Left");
        Transform rightContainer = transform.Find("Pnl_Resource_Right");

        // Lewa strona: Ether, Food, Coal
        if (leftContainer != null)
        {
            TryAddSlot(leftContainer, "Pnl_SingleResource_Ether", ResourceType.Ether);
            TryAddSlot(leftContainer, "Pnl_SingleResource_Food", ResourceType.Food);
            TryAddSlot(leftContainer, "Pnl_SingleResource_Coal", ResourceType.Coal);
        }

        // Prawa strona: Wood, Stone, Iron
        if (rightContainer != null)
        {
            TryAddSlot(rightContainer, "Pnl_SingleResource_Wood", ResourceType.Wood);
            TryAddSlot(rightContainer, "Pnl_SingleResource_Stone", ResourceType.Stone);
            TryAddSlot(rightContainer, "Pnl_SingleResource_Iron", ResourceType.Iron);
        }
    }

    private void TryAddSlot(Transform parent, string childName, ResourceType resType)
    {
        Transform child = parent.Find(childName);
        if (child == null) return;

        DisableConflictComponents(child.gameObject);

        Image bgImage = child.GetComponent<Image>();
        TextMeshProUGUI txt = child.GetComponentInChildren<TextMeshProUGUI>(true);

        if (txt != null)
        {
            txt.fontSize = fontSize;
        }

        Color defaultColor = bgImage != null ? bgImage.color : normalBgColor;

        bonusSlots.Add(new BonusSlotItem
        {
            resourceType = resType,
            rootObject = child.gameObject,
            backgroundImage = bgImage,
            amountText = txt,
            defaultBgColor = defaultColor
        });
    }

    /// <summary>
    /// Wyłącza SingleResourceUI na pigułkach podglądu, aby nie nadpisywały tekstu stanem z banku.
    /// </summary>
    private void DisableConflictComponents(GameObject slotObj)
    {
        if (slotObj == null) return;

        var singleRes = slotObj.GetComponent<SingleResourceUI>();
        if (singleRes != null)
        {
            singleRes.enabled = false;
            if (Application.isPlaying)
            {
                Destroy(singleRes);
            }
            else
            {
                DestroyImmediate(singleRes);
            }
        }
    }

    private void SubscribeEvents()
    {
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted += RefreshUI;
            TimePhaseManager.Instance.OnDuskStarted += RefreshUI;
            TimePhaseManager.Instance.OnNightStarted += RefreshUI;
        }

        if (BuildingRegistry.Instance != null)
        {
            BuildingRegistry.Instance.OnBuildingRegistered += HandleBuildingChanged;
            BuildingRegistry.Instance.OnBuildingUnregistered += HandleBuildingChanged;
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged += HandleResourceChanged;
        }

        BuildingEntity.OnAnyBuildingEconomyChanged += RefreshUI;
        PhaseResourceForecastManager.OnForecastDirty += RefreshUI;
    }

    private void UnsubscribeEvents()
    {
        if (TimePhaseManager.Instance != null)
        {
            TimePhaseManager.Instance.OnMorningStarted -= RefreshUI;
            TimePhaseManager.Instance.OnDuskStarted -= RefreshUI;
            TimePhaseManager.Instance.OnNightStarted -= RefreshUI;
        }

        if (BuildingRegistry.Instance != null)
        {
            BuildingRegistry.Instance.OnBuildingRegistered -= HandleBuildingChanged;
            BuildingRegistry.Instance.OnBuildingUnregistered -= HandleBuildingChanged;
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged -= HandleResourceChanged;
        }

        BuildingEntity.OnAnyBuildingEconomyChanged -= RefreshUI;
        PhaseResourceForecastManager.OnForecastDirty -= RefreshUI;
    }

    private void HandleBuildingChanged(BuildingEntity b)
    {
        RefreshUI();
    }

    private void HandleResourceChanged(ResourceType type, int amount)
    {
        RefreshUI();
    }

    public void RefreshUI()
    {
        if (TimePhaseManager.Instance == null) return;

        TimePhases currentPhase = TimePhaseManager.Instance.CurrentTimePhase;

        // W nocy: 0 kapsułek (wszystkie ukryte, czysty ekran na walkę)
        if (currentPhase == TimePhases.Night)
        {
            HideAllSlots();
            return;
        }

        var forecast = PhaseResourceForecastManager.CalculateForecast(currentPhase);

        foreach (var slot in bonusSlots)
        {
            if (slot.rootObject == null || slot.amountText == null) continue;

            int delta = forecast.ContainsKey(slot.resourceType) ? forecast[slot.resourceType] : 0;
            int currentTreasury = ResourceManager.Instance != null
                ? ResourceManager.Instance.GetResourceAmount(slot.resourceType)
                : 0;

            if (delta == 0)
            {
                // Brak zmian dla tego surowca -> ukryj kapsułkę
                slot.rootObject.SetActive(false);
            }
            else
            {
                slot.rootObject.SetActive(true);

                if (delta > 0)
                {
                    slot.amountText.text = $"+{delta}";
                    slot.amountText.color = positiveColor;
                    if (slot.backgroundImage != null)
                    {
                        slot.backgroundImage.color = slot.defaultBgColor;
                    }
                }
                else
                {
                    // Ujemny bilans (ubytek / koszt)
                    bool isDeficit = (currentTreasury + delta) < 0;

                    slot.amountText.text = isDeficit ? $"! {delta}" : $"{delta}";
                    slot.amountText.color = isDeficit ? deficitTextColor : negativeColor;

                    if (slot.backgroundImage != null)
                    {
                        slot.backgroundImage.color = isDeficit ? crisisBgColor : slot.defaultBgColor;
                    }
                }
            }
        }
    }

    private void HideAllSlots()
    {
        foreach (var slot in bonusSlots)
        {
            if (slot.rootObject != null)
            {
                slot.rootObject.SetActive(false);
            }
        }
    }
}
