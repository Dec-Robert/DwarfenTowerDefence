using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DruidTowerWindowUI : MonoBehaviour
{

    // =========================================================================
    // 1. MODAL ODBUDOWY (Pnl_RebuildModal)
    // =========================================================================
    [Header("1. Modal Odbudowy")]
    public GameObject pnlRebuildModal;
    public TextMeshProUGUI txtRebuildTitle;
    public Button btnRebuildClose;
    public TextMeshProUGUI txtRebuildDescription;
    public TextMeshProUGUI txtRebuildReqSettlement;
    public Transform pnlRepairContainer;
    [SerializeField] private GameObject singleResourcePrefab;
    [SerializeField] private ResourceVisualsSO resourceVisuals;
    public Button btnRebuild;
    public TextMeshProUGUI txtRebuildButtonLabel;

    // =========================================================================
    // 2. MODAL WYBORU ZAKLĘCIA (Pnl_SpellChoiceModal)
    // =========================================================================
    [Header("2. Modal Wyboru (Draft 1 z 2)")]
    public GameObject pnlSpellChoiceModal;
    public TextMeshProUGUI txtChoiceTitle;
    public TextMeshProUGUI txtChoicePrompt;
    public Transform pnlCardsRow;
    [SerializeField] private GameObject cardSpellPrefab;

    // =========================================================================
    // 3. MODAL GŁÓWNY (Pnl_MainSpellModal)
    // =========================================================================
    [Header("3. Modal Główny (Wybrane Zaklęcie)")]
    public GameObject pnlMainSpellModal;
    public TextMeshProUGUI txtMainTitle;
    public Button btnMainClose;
    public Image imgMainSpellIcon;
    public TextMeshProUGUI txtMainSpellDescription;
    public TextMeshProUGUI txtMainSpellDamage;
    public TextMeshProUGUI txtMainSpellStats;
    public TextMeshProUGUI txtMainCastHint;
    public GameObject secUpgradePaths;
    public Transform pnlLinearTiers;
    [SerializeField] private GameObject cardSpellTierPrefab;

    private DruidTowerEntity currentTower;
    private bool listenersBound;
    private bool resourceEventsSubscribed;
    private readonly List<SingleResourceUI> repairCostItems = new List<SingleResourceUI>();
    private readonly List<SpellChoiceCardUI> choiceCardItems = new List<SpellChoiceCardUI>();
    private readonly List<SpellTierCardUI> tierCardItems = new List<SpellTierCardUI>();

    private void Awake()
    {
        BindStaticListeners();
    }

    public void AutoBindReferences()
    {
        // 1. Modal Odbudowy
        pnlRebuildModal ??= FindGO("Pnl_RebuildModal");
        txtRebuildTitle ??= FindTMP("Pnl_RebuildModal/Header/Txt_Title");
        btnRebuildClose ??= FindButton("Pnl_RebuildModal/Header/Btn_Close");
        txtRebuildDescription ??= FindTMP("Pnl_RebuildModal/Body/Txt_Description");
        txtRebuildReqSettlement ??= FindTMP("Pnl_RebuildModal/Body/Pnl_Requirements/Txt_ReqSettlement");
        if (pnlRepairContainer == null)
        {
            var repairGo = FindGO("Pnl_RebuildModal/Body/Pnl_Requirements/Pnl_RepairCost/Pnl_Repair");
            if (repairGo != null) pnlRepairContainer = repairGo.transform;
        }
        btnRebuild ??= FindButton("Pnl_RebuildModal/Body/Btn_Rebuild");
        txtRebuildButtonLabel ??= FindTMP("Pnl_RebuildModal/Body/Btn_Rebuild/Txt_Label");

        // 2. Modal Wyboru
        pnlSpellChoiceModal ??= FindGO("Pnl_SpellChoiceModal");
        txtChoiceTitle ??= FindTMP("Pnl_SpellChoiceModal/Pnl_Window/Txt_Title");
        txtChoicePrompt ??= FindTMP("Pnl_SpellChoiceModal/Pnl_Window/Body/Txt_Prompt");
        if (pnlCardsRow == null)
        {
            var rowGo = FindGO("Pnl_SpellChoiceModal/Pnl_Window/Body/Pnl_CardsRow");
            if (rowGo != null) pnlCardsRow = rowGo.transform;
        }

        // 3. Modal Główny
        pnlMainSpellModal ??= FindGO("Pnl_MainSpellModal");
        txtMainTitle ??= FindTMP("Pnl_MainSpellModal/Header/Txt_Title");
        btnMainClose ??= FindButton("Pnl_MainSpellModal/Header/Btn_Close");
        imgMainSpellIcon ??= FindImage("Pnl_MainSpellModal/Body/Pnl_SpellSummary/Img_SpellIcon");
        txtMainSpellDescription ??= FindTMP("Pnl_MainSpellModal/Body/Pnl_SpellSummary/Pnl_SummaryText/Txt_SpellDescription");
        txtMainSpellDamage ??= FindTMP("Pnl_MainSpellModal/Body/Pnl_SpellSummary/Pnl_SummaryText/Txt_SpellDamage");
        txtMainSpellStats ??= FindTMP("Pnl_MainSpellModal/Body/Pnl_SpellSummary/Pnl_SummaryText/Txt_SpellStats");
        txtMainCastHint ??= FindTMP("Pnl_MainSpellModal/Body/Pnl_SpellSummary/Pnl_SummaryText/Txt_CastHint");
        secUpgradePaths ??= FindGO("Pnl_MainSpellModal/Body/Sec_UpgradePaths");
        if (pnlLinearTiers == null)
        {
            var tiersGo = FindGO("Pnl_MainSpellModal/Body/Sec_UpgradePaths/Pnl_LinearTiers");
            if (tiersGo != null) pnlLinearTiers = tiersGo.transform;
        }
    }

    private void BindStaticListeners()
    {
        if (listenersBound) return;
        listenersBound = true;

        if (btnRebuildClose != null)
        {
            btnRebuildClose.onClick.RemoveAllListeners();
            btnRebuildClose.onClick.AddListener(() => CenterInfoPanelControllerUI.Instance?.HideAll());
        }

        if (btnMainClose != null)
        {
            btnMainClose.onClick.RemoveAllListeners();
            btnMainClose.onClick.AddListener(() => CenterInfoPanelControllerUI.Instance?.HideAll());
        }

        if (btnRebuild != null)
        {
            btnRebuild.onClick.RemoveAllListeners();
            btnRebuild.onClick.AddListener(() => currentTower?.TryRebuild());
        }
    }

    private void OnEnable()
    {
        SubscribeResourceEvents();
    }

    private void OnDisable()
    {
        UnsubscribeResourceEvents();
    }

    private void SubscribeResourceEvents()
    {
        if (resourceEventsSubscribed || ResourceManager.Instance == null) return;
        ResourceManager.Instance.OnResourceChanged += HandleResourceChanged;
        resourceEventsSubscribed = true;
    }

    private void UnsubscribeResourceEvents()
    {
        if (!resourceEventsSubscribed) return;
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged -= HandleResourceChanged;
        }
        resourceEventsSubscribed = false;
    }

    private void HandleResourceChanged(ResourceType _, int __)
    {
        if (currentTower != null && gameObject.activeInHierarchy)
        {
            Refresh();
        }
    }

    public void Setup(DruidTowerEntity tower)
    {
        if (currentTower != null)
        {
            currentTower.OnDruidTowerStateChanged -= Refresh;
        }

        currentTower = tower;

        if (currentTower != null)
        {
            currentTower.OnDruidTowerStateChanged += Refresh;
        }

        AutoBindReferences();
        BindStaticListeners();
        SubscribeResourceEvents();
        Refresh();
    }

    private void OnDestroy()
    {
        UnsubscribeResourceEvents();
        if (currentTower != null)
        {
            currentTower.OnDruidTowerStateChanged -= Refresh;
        }
    }

    public void Refresh()
    {
        if (currentTower == null) return;

        if (pnlRebuildModal != null) pnlRebuildModal.SetActive(currentTower.State == DruidTowerState.Ruined);
        if (pnlSpellChoiceModal != null) pnlSpellChoiceModal.SetActive(currentTower.State == DruidTowerState.RebuiltPendingDraft);
        if (pnlMainSpellModal != null) pnlMainSpellModal.SetActive(currentTower.State == DruidTowerState.Active);

        switch (currentTower.State)
        {
            case DruidTowerState.Ruined:
                RefreshRebuildModal();
                break;

            case DruidTowerState.RebuiltPendingDraft:
                RefreshChoiceModal();
                break;

            case DruidTowerState.Active:
                RefreshMainModal();
                break;
        }
    }

    private void RefreshRebuildModal()
    {
        if (txtRebuildTitle != null)
            txtRebuildTitle.text = LocalizationManager.Get("DRUID_CAVE_RUINED_TITLE", "Zawalona Jama");

        if (txtRebuildDescription != null)
            txtRebuildDescription.text = "Starożytna, zawalona jama szalonego druida. Oczyść wejście i odbuduj ją, aby poznać potężne zaklęcie wspierające obronę podczas nocy.";

        bool isSettled = currentTower.IsChunkSettled();
        bool canRebuild = currentTower.CanRebuild(out _);

        if (txtRebuildReqSettlement != null)
        {
            txtRebuildReqSettlement.text = isSettled
                ? "• Teren zasiedlony posterunkiem (Spełniono)"
                : "• Wymagane: Zasiedl ten teren posterunkiem, aby odblokować odbudowę";
            txtRebuildReqSettlement.color = isSettled ? new Color(0.55f, 0.92f, 0.62f) : new Color(0.95f, 0.55f, 0.45f);
        }

        RefreshRepairCostGrid();

        if (btnRebuild != null) btnRebuild.interactable = canRebuild;
        if (txtRebuildButtonLabel != null) txtRebuildButtonLabel.text = LocalizationManager.Get("DRUID_CAVE_REBUILD_BTN", "Odbuduj");
    }

    private void RefreshRepairCostGrid()
    {
        PopulateResourceCostContainer(
            pnlRepairContainer,
            repairCostItems,
            currentTower.rebuildCosts,
            singleResourcePrefab,
            resourceVisuals);
    }

    public static bool PopulateResourceCostContainer(
        Transform container,
        List<SingleResourceUI> pool,
        List<ResourceCost> costs,
        GameObject resourcePrefab,
        ResourceVisualsSO visuals)
    {
        if (container == null || pool == null) return false;

        var activeCosts = new List<ResourceCost>();
        if (costs != null)
        {
            for (int i = 0; i < costs.Count; i++)
            {
                if (costs[i].amount > 0)
                {
                    activeCosts.Add(costs[i]);
                }
            }
        }

        if (pool.Count == 0)
        {
            var existing = container.GetComponentsInChildren<SingleResourceUI>(true);
            if (existing != null && existing.Length > 0)
            {
                pool.AddRange(existing);
            }
        }

        if (resourcePrefab != null)
        {
            while (pool.Count < activeCosts.Count)
            {
                var go = Instantiate(resourcePrefab, container);
                var item = go.GetComponent<SingleResourceUI>();
                if (item == null)
                {
                    Destroy(go);
                    break;
                }
                pool.Add(item);
            }
        }

        for (int i = 0; i < pool.Count; i++)
        {
            var item = pool[i];
            if (item == null) continue;

            if (i < activeCosts.Count)
            {
                var cost = activeCosts[i];
                item.gameObject.SetActive(true);
                item.Setup(cost.type, cost.amount, visuals);

                bool canAfford = ResourceManager.Instance != null && ResourceManager.Instance.CanAfford(cost.type, cost.amount);
                Color color = canAfford ? Color.white : new Color(0.95f, 0.26f, 0.21f, 1f);
                item.SetTextColor(color);
            }
            else
            {
                item.gameObject.SetActive(false);
            }
        }

        if (container is RectTransform rt)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        return activeCosts.Count > 0;
    }

    private void RefreshChoiceModal()
    {
        if (txtChoiceTitle != null)
            txtChoiceTitle.text = LocalizationManager.Get("DRUID_CAVE_DRAFT_TITLE", "Dokonaj wyboru");

        if (txtChoicePrompt != null)
            txtChoicePrompt.text = LocalizationManager.Get("druid_cave_flavour_txt", "Z jaskini wyłania się szalony druid. Za uwolnienie go oferuje ci zakazaną wiedzę");

        if (pnlCardsRow == null) return;

        var drafted = currentTower.DraftedOptions;
        int optionCount = drafted != null ? drafted.Count : 0;

        if (choiceCardItems.Count == 0)
        {
            var existing = pnlCardsRow.GetComponentsInChildren<SpellChoiceCardUI>(true);
            if (existing != null && existing.Length > 0)
            {
                choiceCardItems.AddRange(existing);
            }
        }

        if (cardSpellPrefab != null)
        {
            while (choiceCardItems.Count < optionCount)
            {
                var go = Instantiate(cardSpellPrefab, pnlCardsRow);
                var card = go.GetComponent<SpellChoiceCardUI>() ?? go.AddComponent<SpellChoiceCardUI>();
                choiceCardItems.Add(card);
            }
        }

        for (int i = 0; i < choiceCardItems.Count; i++)
        {
            var card = choiceCardItems[i];
            if (card == null) continue;

            bool hasSpell = i < optionCount && drafted[i] != null;
            card.gameObject.SetActive(hasSpell);
            if (!hasSpell) continue;

            int idx = i;
            card.Setup(drafted[i], () =>
            {
                if (currentTower != null && currentTower.SelectDraftedSpell(idx))
                {
                    CenterInfoPanelControllerUI.Instance?.HideAll();
                }
            });
        }

        if (pnlCardsRow is RectTransform rt)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    private void RefreshMainModal()
    {
        var spell = currentTower.ActiveSpell;
        if (spell == null) return;

        if (txtMainTitle != null)
            txtMainTitle.text = $"{spell.spellName} — Jama Druida";

        if (imgMainSpellIcon != null)
        {
            imgMainSpellIcon.sprite = spell.icon;
            imgMainSpellIcon.color = spell.icon != null ? Color.white : spell.indicatorColor;
        }

        if (txtMainSpellDescription != null)
            txtMainSpellDescription.text = spell.description;

        if (txtMainSpellDamage != null)
            txtMainSpellDamage.text = SpellChoiceCardUI.FormatDamageText(spell.damageType, currentTower.GetEffectiveDamage());

        if (txtMainSpellStats != null)
            txtMainSpellStats.text = $"Zasięg: {currentTower.GetEffectiveRadius():0.0}  |  Odnowienie: {currentTower.GetEffectiveCooldown():0.0}s  |  {FormatLimitMode(spell)}";

        if (txtMainCastHint != null)
            txtMainCastHint.text = "W nocy aktywuj zaklęcie w menu zaklęć lub klawiszem [Q].";

        RefreshLinearTierCards(spell);
    }

    private void RefreshLinearTierCards(SpellDataSO spell)
    {
        bool allPurchased = currentTower.IsLinearTreeCompleted();
        if (secUpgradePaths != null)
            secUpgradePaths.SetActive(!allPurchased);

        if (pnlLinearTiers == null) return;

        if (tierCardItems.Count == 0)
        {
            var existing = pnlLinearTiers.GetComponentsInChildren<SpellTierCardUI>(true);
            if (existing != null && existing.Length > 0)
            {
                tierCardItems.AddRange(existing);
            }
        }

        if (allPurchased)
        {
            for (int i = 0; i < tierCardItems.Count; i++)
            {
                if (tierCardItems[i] != null) tierCardItems[i].gameObject.SetActive(false);
            }
            return;
        }

        int tierCount = spell.linearTiers != null ? spell.linearTiers.Count : 0;
        if (cardSpellTierPrefab != null)
        {
            while (tierCardItems.Count < tierCount)
            {
                var go = Instantiate(cardSpellTierPrefab, pnlLinearTiers);
                var card = go.GetComponent<SpellTierCardUI>() ?? go.AddComponent<SpellTierCardUI>();
                tierCardItems.Add(card);
            }
        }

        for (int i = 0; i < tierCardItems.Count; i++)
        {
            var card = tierCardItems[i];
            if (card == null) continue;

            // Wykupione tiery są całkowicie ukryte; przyszłe widoczne bez przycisku; aktualny ma przycisk Ulepsz
            bool isVisible = i < tierCount && i >= currentTower.CurrentLinearTier && spell.linearTiers[i] != null;
            card.gameObject.SetActive(isVisible);
            if (!isVisible) continue;

            bool isNext = i == currentTower.CurrentLinearTier;
            bool canAfford = isNext && currentTower.CanUpgradeNextLinearTier();

            card.Setup(
                spell.linearTiers[i],
                isNext,
                canAfford,
                singleResourcePrefab,
                resourceVisuals,
                () => currentTower?.TryUpgradeNextLinearTier());
        }

        if (pnlLinearTiers is RectTransform rt)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    private GameObject FindGO(string path) => transform.Find(path)?.gameObject;
    private TextMeshProUGUI FindTMP(string path) => transform.Find(path)?.GetComponent<TextMeshProUGUI>();
    private Button FindButton(string path) => transform.Find(path)?.GetComponent<Button>();
    private Image FindImage(string path) => transform.Find(path)?.GetComponent<Image>();

    private static string FormatLimitMode(SpellDataSO spell)
    {
        return spell.castLimitMode switch
        {
            SpellCastLimitMode.LimitedUsesPerNight => $"{spell.maxUsesPerNight}/noc",
            SpellCastLimitMode.RechargeableCharges => $"{spell.maxCharges} ład. ({spell.chargeRechargeSeconds:0}s)",
            SpellCastLimitMode.CooldownOnly => $"CD {spell.cooldownSeconds:0}s",
            _ => ""
        };
    }
}
