using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Kontroler pojedynczego kafelka ulepszenia na liście Legacy of Town (Pnl_MetaUpgrade w ScrollView).
/// Obsługuje:
/// - Prezentację nazwy, ikony, opisu oraz paska postępu
/// - Mechanikę "Hold-to-Invest": kliknięcie i przytrzymanie wpłaca walutę Nadziei z przyspieszeniem
/// - Wykrycie ukończenia (100%), odblokowanie ulepszenia w SaveManager i powiadomienie kontrolera
/// - Wyświetlanie stanu "X / Y Nadziei" lub "ODBLOKOWANE"
/// - Nienaruszanie czcionek i ich rozmiarów ze sceny (zgodnie z wytycznymi użytkownika)
/// </summary>
public class MetaUpgradeItemUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("Referencje UI")]
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private Image imgIcon;
    [SerializeField] private TextMeshProUGUI txtDescription;
    [SerializeField] private RectTransform pnlProgressBackground;
    [SerializeField] private Image imgProgressFill;
    [SerializeField] private TextMeshProUGUI txtProgress;

    private MetaUpgradeSO upgrade;
    private Action<MetaUpgradeSO> onUnlockedCallback;
    private Action onHopeChangedCallback;

    private bool isPointerDown = false;
    private float holdTimer = 0f;
    private float accumulatedHope = 0f;

    public MetaUpgradeSO Upgrade => upgrade;
    public bool IsUnlocked => upgrade != null && upgrade.isUnlocked;

    private void Awake()
    {
        FindReferencesIfNull();
    }

    public void FindReferencesIfNull()
    {
        if (txtName == null)
        {
            Transform t = transform.Find("Txt_Name");
            if (t != null) txtName = t.GetComponent<TextMeshProUGUI>();
        }

        if (imgIcon == null)
        {
            Transform t = transform.Find("Img_Icon");
            if (t != null) imgIcon = t.GetComponent<Image>();
        }

        if (txtDescription == null)
        {
            Transform t = transform.Find("Txt_Description");
            if (t != null) txtDescription = t.GetComponent<TextMeshProUGUI>();
        }

        if (pnlProgressBackground == null)
        {
            Transform t = transform.Find("Pnl_ProgressBackground");
            if (t != null) pnlProgressBackground = t.GetComponent<RectTransform>();
        }

        if (pnlProgressBackground != null)
        {
            if (imgProgressFill == null)
            {
                Transform t = pnlProgressBackground.Find("Pnl_ProgressBackground (1)");
                if (t != null) imgProgressFill = t.GetComponent<Image>();
            }

            if (txtProgress == null)
            {
                Transform t = pnlProgressBackground.Find("Text (TMP)");
                if (t != null) txtProgress = t.GetComponent<TextMeshProUGUI>();
            }
        }
    }

    public void Setup(MetaUpgradeSO upgradeSO, Action<MetaUpgradeSO> onUnlocked, Action onHopeChanged)
    {
        FindReferencesIfNull();
        upgrade = upgradeSO;
        onUnlockedCallback = onUnlocked;
        onHopeChangedCallback = onHopeChanged;

        if (upgrade == null) return;

        // Sprawdź czy ulepszenie jest już odblokowane w SaveManager
        bool isAlreadyUnlocked = upgrade.isUnlocked;
        if (!isAlreadyUnlocked && SaveManager.Instance != null && SaveManager.Instance.currentSaveData != null)
        {
            if (SaveManager.Instance.currentSaveData.unlockedUpgradeIDs.Contains(upgrade.id))
            {
                isAlreadyUnlocked = true;
                upgrade.isUnlocked = true;
            }
        }

        // Nazwa i opis (zostawiamy czcionki i rozmiary nienaruszone)
        if (txtName != null)
        {
            txtName.text = upgrade.DisplayName;
        }

        if (txtDescription != null)
        {
            txtDescription.text = upgrade.DisplayDescription;
        }

        if (imgIcon != null)
        {
            imgIcon.sprite = upgrade.icon;
            imgIcon.enabled = (upgrade.icon != null);
        }

        UpdateVisuals();
    }

    public void UpdateVisuals()
    {
        if (upgrade == null) return;

        int cost = Mathf.Max(1, upgrade.cost);
        int invested = 0;
        if (SaveManager.Instance != null)
        {
            invested = SaveManager.Instance.GetInvestedHope(upgrade.id);
        }

        if (upgrade.isUnlocked || invested >= cost)
        {
            invested = cost;
            if (txtProgress != null)
            {
                txtProgress.text = "ODBLOKOWANE";
            }
            SetFillRatio(1f);
        }
        else
        {
            if (txtProgress != null)
            {
                txtProgress.text = $"{invested} / {cost} Nadziei";
            }
            SetFillRatio((float)invested / cost);
        }
    }

    private void SetFillRatio(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);

        if (imgProgressFill != null)
        {
            if (imgProgressFill.type == Image.Type.Filled)
            {
                imgProgressFill.fillAmount = ratio;
            }
            else
            {
                RectTransform rt = imgProgressFill.rectTransform;
                rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
                rt.anchorMax = new Vector2(ratio, rt.anchorMax.y);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (upgrade == null || upgrade.isUnlocked) return;

        isPointerDown = true;
        holdTimer = 0f;
        accumulatedHope = 0f;

        // Pojedynczy klik natychmiast wpłaca 1 punkt Nadziei
        TryDepositHope(1);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isPointerDown)
        {
            isPointerDown = false;
            accumulatedHope = 0f;
            SaveManager.Instance?.SaveGame();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isPointerDown)
        {
            isPointerDown = false;
            accumulatedHope = 0f;
            SaveManager.Instance?.SaveGame();
        }
    }

    private void Update()
    {
        if (!isPointerDown || upgrade == null || upgrade.isUnlocked) return;

        holdTimer += Time.unscaledDeltaTime;

        // Płynne przyspieszanie (ramping): start od ok. 10 Nadziei/s, rosnące do ok. 70 Nadziei/s po 2 sekundach
        float t = Mathf.Clamp01(holdTimer / 2f);
        float rate = Mathf.Lerp(10f, 70f, t);

        accumulatedHope += rate * Time.unscaledDeltaTime;
        int toDeposit = Mathf.FloorToInt(accumulatedHope);

        if (toDeposit > 0)
        {
            accumulatedHope -= toDeposit;
            TryDepositHope(toDeposit);
        }
    }

    private void TryDepositHope(int amount)
    {
        if (upgrade == null || upgrade.isUnlocked || amount <= 0) return;

        if (SaveManager.Instance == null || SaveManager.Instance.currentSaveData == null) return;

        int availableHope = SaveManager.Instance.currentSaveData.totalHope;
        if (availableHope <= 0)
        {
            isPointerDown = false;
            return;
        }

        int currentInvested = SaveManager.Instance.GetInvestedHope(upgrade.id);
        int remaining = upgrade.cost - currentInvested;
        if (remaining <= 0)
        {
            CompleteUpgrade();
            return;
        }

        int actualToDeposit = Mathf.Min(amount, Mathf.Min(remaining, availableHope));
        if (actualToDeposit <= 0) return;

        SaveManager.Instance.currentSaveData.totalHope -= actualToDeposit;
        int newInvested = currentInvested + actualToDeposit;
        SaveManager.Instance.SetInvestedHope(upgrade.id, newInvested, autoSave: false);

        UpdateVisuals();
        onHopeChangedCallback?.Invoke();

        if (newInvested >= upgrade.cost)
        {
            CompleteUpgrade();
        }
    }

    private void CompleteUpgrade()
    {
        if (upgrade == null) return;

        upgrade.isUnlocked = true;
        isPointerDown = false;
        accumulatedHope = 0f;

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SetInvestedHope(upgrade.id, upgrade.cost, autoSave: false);
            SaveManager.Instance.UnlockUpgrade(upgrade.id);
            SaveManager.Instance.SaveGame();
        }

        UpdateVisuals();
        onHopeChangedCallback?.Invoke();

        // Jeśli to węzeł draftu nowej wieży, otwórz modal
        bool isDraft = (MetaUpgradeManager.Instance != null && MetaUpgradeManager.Instance.IsTowerDraftUpgrade(upgrade)) ||
                       (!string.IsNullOrEmpty(upgrade.id) && upgrade.id.StartsWith("nowa_wie", StringComparison.OrdinalIgnoreCase));

        if (isDraft)
        {
            TowerDraftModalUI draftModal = TowerDraftModalUI.Instance ?? TowerDraftModalUI.GetOrCreateInstance();
            draftModal?.ShowDraft(SaveManager.Instance?.allTowers);
        }

        onUnlockedCallback?.Invoke(upgrade);
    }
}
