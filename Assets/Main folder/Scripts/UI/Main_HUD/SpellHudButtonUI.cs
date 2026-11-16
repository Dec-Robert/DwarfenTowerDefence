using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpellHudButtonUI : MonoBehaviour
{
    public static SpellHudButtonUI Instance { get; private set; }

    [Header("Referencje Menu Zaklęć (ze sceny pod Canvas_HUD)")]
    public GameObject buttonRoot;
    public Button castButton;
    public Image iconImage;
    public Image cooldownFillImage;
    public TextMeshProUGUI spellNameText;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI hotkeyText;

    private bool isSubscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        AutoBindReferences();
    }

    private void Start()
    {
        BindButtonListener();
        TrySubscribe();
        Refresh();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (isSubscribed && SpellCastManager.Instance != null)
        {
            SpellCastManager.Instance.OnSpellStateChanged -= Refresh;
            isSubscribed = false;
        }
    }

    public void AutoBindReferences()
    {
        if (buttonRoot == null)
        {
            buttonRoot = transform.Find("Btn_HudSpellCast")?.gameObject ?? gameObject;
        }

        if (buttonRoot != null)
        {
            castButton ??= buttonRoot.GetComponent<Button>();
            if (castButton == null)
            {
                castButton = buttonRoot.AddComponent<Button>();
                var img = buttonRoot.GetComponent<Image>();
                if (img != null) castButton.targetGraphic = img;
            }
            iconImage ??= buttonRoot.transform.Find("Img_Icon")?.GetComponent<Image>();
            cooldownFillImage ??= buttonRoot.transform.Find("Img_CooldownFill")?.GetComponent<Image>();
            spellNameText ??= buttonRoot.transform.Find("Txt_SpellName")?.GetComponent<TextMeshProUGUI>();
            statusText ??= buttonRoot.transform.Find("Txt_Status")?.GetComponent<TextMeshProUGUI>();
            hotkeyText ??= buttonRoot.transform.Find("Txt_Hotkey")?.GetComponent<TextMeshProUGUI>();
        }
    }

    private void BindButtonListener()
    {
        if (castButton == null) return;
        castButton.onClick.RemoveAllListeners();
        castButton.onClick.AddListener(OnClicked);
    }

    private void TrySubscribe()
    {
        if (!isSubscribed && SpellCastManager.Instance != null)
        {
            SpellCastManager.Instance.OnSpellStateChanged += Refresh;
            isSubscribed = true;
        }
    }

    private void Update()
    {
        if (!isSubscribed && SpellCastManager.Instance != null)
        {
            TrySubscribe();
            Refresh();
        }

        if (buttonRoot != null && buttonRoot.activeSelf)
        {
            RefreshDynamicCooldown();
        }
    }

    private void OnClicked()
    {
        SpellCastManager.Instance?.ToggleTargetingMode();
    }

    public void Refresh()
    {
        if (buttonRoot == null) return;

        var mgr = SpellCastManager.Instance;
        bool hasSpell = mgr != null && mgr.ActiveTower != null && mgr.ActiveTower.State == DruidTowerState.Active && mgr.ActiveTower.ActiveSpell != null;
        bool managedBySpellMenu = GetComponentInParent<SpellMenuUI>() != null;
        if (!managedBySpellMenu)
        {
            buttonRoot.SetActive(hasSpell);
        }
        if (!hasSpell) return;

        var spell = mgr.ActiveTower.ActiveSpell;
        if (iconImage != null)
        {
            iconImage.sprite = spell.icon;
            iconImage.color = spell.icon != null ? Color.white : spell.indicatorColor;
        }

        if (spellNameText != null)
        {
            spellNameText.text = spell.spellName;
        }

        if (hotkeyText != null)
        {
            hotkeyText.text = $"[{mgr.castHotkey}]";
        }

        if (castButton != null)
        {
            castButton.interactable = mgr.CanCastNow();
        }

        RefreshDynamicCooldown();
    }

    private void RefreshDynamicCooldown()
    {
        var mgr = SpellCastManager.Instance;
        if (mgr == null || mgr.ActiveTower == null || mgr.ActiveTower.ActiveSpell == null) return;

        var spell = mgr.ActiveTower.ActiveSpell;
        if (cooldownFillImage != null)
        {
            cooldownFillImage.fillAmount = mgr.GetCooldownProgress01();
        }

        if (statusText != null)
        {
            if (!mgr.IsNightPhase())
            {
                statusText.text = "Dzień";
                return;
            }

            if (mgr.IsTargeting)
            {
                statusText.text = "Celuj...";
                return;
            }

            statusText.text = spell.castLimitMode switch
            {
                SpellCastLimitMode.LimitedUsesPerNight => $"{mgr.RemainingUses}/{mgr.ActiveTower.GetEffectiveMaxUsesOrCharges()}",
                SpellCastLimitMode.RechargeableCharges => mgr.CurrentCharges < mgr.ActiveTower.GetEffectiveMaxUsesOrCharges()
                    ? $"{mgr.CurrentCharges} ({mgr.CooldownTimer:0}s)"
                    : $"{mgr.CurrentCharges}/{mgr.ActiveTower.GetEffectiveMaxUsesOrCharges()}",
                SpellCastLimitMode.CooldownOnly => mgr.CooldownTimer > 0f ? $"{mgr.CooldownTimer:0.0}s" : "Gotowy",
                _ => ""
            };
        }
    }
}
