using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Komponent dołączany do prefabu Card_Rune_Item w ekwipunku Kuźni.
/// Odpowiada za:
/// 1. Prezentację wizualną runy (ikona runy, kompaktowe wiersze Pnl_StatInfo dla buffa i ewentualnej kary)
/// 2. Obsługę interakcji (kliknięcie do szybkiego osadzenia oraz Drag & Drop)
/// </summary>
public class RuneCardItemUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Referencje UI")]
    [SerializeField] private Image imgRuneIcon;
    [SerializeField] private Image imgCardBackground;
    [SerializeField] private Transform pnlStatContainer;
    [SerializeField] private GameObject statInfoPrefab;
    [SerializeField] private ResourceVisualsSO resourceVisuals;

    private RuneItem rune;
    private RuneForgeWindowUI window;
    private bool isDragging;

    public RuneItem CurrentRune => rune;

    private void Awake()
    {
        EnsureReferences();
    }

    private void EnsureReferences()
    {
        if (imgRuneIcon == null)
            imgRuneIcon = transform.Find("Img_RuneIcon")?.GetComponent<Image>();

        if (imgCardBackground == null)
            imgCardBackground = GetComponent<Image>();

        if (pnlStatContainer == null)
            pnlStatContainer = transform.Find("Pnl_StatContainer");
    }

    /// <summary>
    /// Konfiguruje kartę pod przekazaną runę i okno kuźni.
    /// </summary>
    public void Setup(RuneItem runeItem, RuneForgeWindowUI windowUI, ResourceVisualsSO visuals = null)
    {
        this.rune = runeItem;
        this.window = windowUI;
        if (visuals != null) this.resourceVisuals = visuals;

        EnsureReferences();
        if (rune == null || rune.definition == null) return;

        // 1. Ikona runy
        if (imgRuneIcon != null)
        {
            imgRuneIcon.gameObject.SetActive(true);
            imgRuneIcon.sprite = rune.definition.icon;
            imgRuneIcon.color = rune.definition.icon != null ? Color.white : (windowUI != null ? windowUI.GetRarityColor(rune.definition.rarity) : Color.white);
            imgRuneIcon.preserveAspect = true;
        }

        // 2. Tło karty i subtelny akcent rzadkości
        if (imgCardBackground != null)
        {
            imgCardBackground.color = new Color(0.14f, 0.16f, 0.2f, 0.95f);
        }

        // 3. Wiersze statystyk (Pnl_StatInfo)
        SetupStatRows(rune);
    }

    private void SetupStatRows(RuneItem runeData)
    {
        if (pnlStatContainer == null) return;

        // Wiersz 1: Główna statystyka (Buff)
        Transform row1 = GetOrCreateStatRow(0);
        if (row1 != null)
        {
            row1.gameObject.SetActive(true);
            Sprite icon = GetStatIcon(runeData.definition.primaryStat);
            string valText = FormatStatValue(runeData.primaryValue, runeData.definition.primaryStat, runeData.definition.valueType, isBuff: true);
            ConfigureStatRow(row1, icon, valText, new Color(0.92f, 0.92f, 0.92f, 1f));
        }

        // Wiersz 2: Kara (Debuff tylko dla Cursed)
        if (runeData.hasPenalty)
        {
            Transform row2 = GetOrCreateStatRow(1);
            if (row2 != null)
            {
                row2.gameObject.SetActive(true);
                Sprite icon = GetStatIcon(runeData.penaltyStat);
                string valText = FormatStatValue(runeData.penaltyValue, runeData.penaltyStat, runeData.penaltyValueType, isBuff: false);
                ConfigureStatRow(row2, icon, valText, new Color(0.95f, 0.28f, 0.28f, 1f));
            }
        }
        else
        {
            // Jeśli brak kary, chowamy drugi wiersz
            if (pnlStatContainer.childCount > 1)
            {
                pnlStatContainer.GetChild(1).gameObject.SetActive(false);
            }
        }

        // Chowamy ewentualne pozostałe wiersze
        int neededRows = runeData.hasPenalty ? 2 : 1;
        for (int i = neededRows; i < pnlStatContainer.childCount; i++)
        {
            pnlStatContainer.GetChild(i).gameObject.SetActive(false);
        }
    }

    private void ConfigureStatRow(Transform rowTr, Sprite icon, string text, Color textColor)
    {
        if (rowTr == null) return;

        Image statImg = rowTr.Find("Img_StatIcon")?.GetComponent<Image>();
        if (statImg != null)
        {
            statImg.gameObject.SetActive(icon != null);
            statImg.sprite = icon;
            statImg.color = Color.white;
            statImg.preserveAspect = true;
        }

        TextMeshProUGUI valTmp = rowTr.Find("Txt_Val")?.GetComponent<TextMeshProUGUI>()
                              ?? rowTr.GetComponentInChildren<TextMeshProUGUI>();
        if (valTmp != null)
        {
            valTmp.text = text;
            valTmp.color = textColor;
        }
    }

    private Transform GetOrCreateStatRow(int index)
    {
        if (pnlStatContainer == null) return null;

        if (index < pnlStatContainer.childCount)
        {
            return pnlStatContainer.GetChild(index);
        }

        if (pnlStatContainer.childCount > 0)
        {
            GameObject clone = Instantiate(pnlStatContainer.GetChild(0).gameObject, pnlStatContainer, false);
            clone.name = $"Pnl_StatInfo_{index + 1}";
            return clone.transform;
        }
        else if (statInfoPrefab != null)
        {
            GameObject inst = Instantiate(statInfoPrefab, pnlStatContainer, false);
            inst.name = $"Pnl_StatInfo_{index + 1}";
            return inst.transform;
        }

        return null;
    }

    private string FormatStatValue(float value, RuneStatType stat, RuneValueType valType, bool isBuff)
    {
        string sign = isBuff ? "+" : "-";
        float absVal = Mathf.Abs(value);
        bool isPct = RuneItem.IsPercentageStat(stat, valType);

        string numStr = (absVal % 1 == 0) ? absVal.ToString("F0") : absVal.ToString("F1");

        return isPct ? $"{sign}{numStr}%" : $"{sign}{numStr}";
    }

    private Sprite GetStatIcon(RuneStatType stat)
    {
        if (resourceVisuals != null)
        {
            Sprite s = resourceVisuals.GetIcon(stat);
            if (s != null) return s;
        }

        if (window != null && window.ResourceVisualsDB != null)
        {
            return window.ResourceVisualsDB.GetIcon(stat);
        }

        return null;
    }

    // ── Obsługa interakcji ───────────────────────────────────────────────────

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (window != null && rune != null)
        {
            window.OnInventoryRuneClicked(rune);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (rune == null) return;

        isDragging = true;
        if (RuneDragDropController.Instance != null)
        {
            RuneDragDropController.Instance.StartDragging(rune, RuneDragSource.Inventory, -1, eventData.position);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        if (RuneDragDropController.Instance != null)
        {
            RuneDragDropController.Instance.UpdateDragging(eventData.position);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
        if (RuneDragDropController.Instance != null)
        {
            RuneDragDropController.Instance.EndDragging();
        }
    }
}
