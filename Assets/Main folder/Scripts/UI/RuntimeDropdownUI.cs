using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Prosty, niezawodny komponent listy rozwijanej (Dropdown) generowany w runtime.
/// Zaprojektowany dla Unity uGUI, nie wymaga prefabów ani zewnętrznych assetów.
/// Zgodny ze standardami UI z GEMINI.md.
/// </summary>
public class RuntimeDropdownUI : MonoBehaviour
{
    private RectTransform rootRect;
    private Button mainButton;
    private Image mainButtonImage;
    private TextMeshProUGUI captionText;
    private TextMeshProUGUI arrowText;

    private GameObject blockerObj;
    private GameObject popupPanel;
    private RectTransform popupRect;
    private Transform popupContainer;

    private readonly List<string> options = new List<string>();
    private readonly List<Button> itemButtons = new List<Button>();
    private readonly List<TextMeshProUGUI> itemTexts = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> itemCheckmarks = new List<TextMeshProUGUI>();

    private int selectedIndex = 0;
    private TMP_FontAsset fontAsset;
    private Transform windowTransform;

    public event Action<int> onValueChanged;
    public int Value => selectedIndex;
    public string SelectedText => (selectedIndex >= 0 && selectedIndex < options.Count) ? options[selectedIndex] : string.Empty;
    public bool IsOpen => popupPanel != null && popupPanel.activeSelf;

    private static readonly List<RuntimeDropdownUI> activeDropdowns = new List<RuntimeDropdownUI>();

    private void Awake()
    {
        if (!activeDropdowns.Contains(this))
        {
            activeDropdowns.Add(this);
        }
    }

    private void OnDestroy()
    {
        activeDropdowns.Remove(this);
        if (blockerObj != null)
        {
            Destroy(blockerObj);
        }
        if (popupPanel != null)
        {
            Destroy(popupPanel);
        }
    }

    /// <summary>
    /// Zamyka wszystkie otwarte listy rozwijane na scenie.
    /// </summary>
    public static void CloseAllDropdowns()
    {
        for (int i = 0; i < activeDropdowns.Count; i++)
        {
            if (activeDropdowns[i] != null && activeDropdowns[i].IsOpen)
            {
                activeDropdowns[i].CloseDropdown();
            }
        }
    }

    /// <summary>
    /// Fabryka tworząca gotowy element Dropdown w podanym kontenerze.
    /// </summary>
    public static RuntimeDropdownUI Create(Transform parent, Transform windowRoot, string name, Vector2 size, TMP_FontAsset font)
    {
        GameObject rootObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(RuntimeDropdownUI));
        rootObj.transform.SetParent(parent, false);

        RectTransform rt = rootObj.GetComponent<RectTransform>();
        rt.sizeDelta = size;

        RuntimeDropdownUI dropdown = rootObj.GetComponent<RuntimeDropdownUI>();
        dropdown.windowTransform = windowRoot != null ? windowRoot : parent;
        dropdown.fontAsset = font;
        dropdown.BuildUI(rt, font);

        return dropdown;
    }

    private void BuildUI(RectTransform rt, TMP_FontAsset font)
    {
        rootRect = rt;
        mainButton = GetComponent<Button>();
        mainButtonImage = GetComponent<Image>();

        // Tło głównego przycisku rozwijanego
        mainButtonImage.color = new Color(0.16f, 0.18f, 0.22f, 1f);
        ColorBlock colors = mainButton.colors;
        colors.normalColor = new Color(0.16f, 0.18f, 0.22f, 1f);
        colors.highlightedColor = new Color(0.24f, 0.28f, 0.35f, 1f);
        colors.pressedColor = new Color(0.12f, 0.14f, 0.18f, 1f);
        colors.selectedColor = new Color(0.18f, 0.21f, 0.26f, 1f);
        mainButton.colors = colors;

        mainButton.onClick.RemoveAllListeners();
        mainButton.onClick.AddListener(ToggleDropdown);

        // Etykieta wybranej wartości (Caption)
        GameObject captionObj = new GameObject("Txt_Caption", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        captionObj.transform.SetParent(rt, false);
        RectTransform captionRect = captionObj.GetComponent<RectTransform>();
        captionRect.anchorMin = Vector2.zero;
        captionRect.anchorMax = Vector2.one;
        captionRect.offsetMin = new Vector2(14f, 0f);
        captionRect.offsetMax = new Vector2(-36f, 0f);

        captionText = captionObj.GetComponent<TextMeshProUGUI>();
        if (font != null) captionText.font = font;
        captionText.fontSize = 14f; // GEMINI.md: 12-14 pt dla etykiet i opcji
        captionText.color = new Color(0.92f, 0.94f, 0.97f, 1f);
        captionText.alignment = TextAlignmentOptions.MidlineLeft;
        captionText.overflowMode = TextOverflowModes.Ellipsis;

        // Strzałka rozwijania
        GameObject arrowObj = new GameObject("Txt_Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        arrowObj.transform.SetParent(rt, false);
        RectTransform arrowRect = arrowObj.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(1f, 0f);
        arrowRect.anchorMax = new Vector2(1f, 1f);
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.anchoredPosition = Vector2.zero;
        arrowRect.sizeDelta = new Vector2(34f, 0f);

        arrowText = arrowObj.GetComponent<TextMeshProUGUI>();
        if (font != null) arrowText.font = font;
        arrowText.text = "▼";
        arrowText.fontSize = 12f;
        arrowText.color = new Color(0.95f, 0.85f, 0.45f, 1f); // złoty akcent Bastionu
        arrowText.alignment = TextAlignmentOptions.Center;

        // Blocker (przezroczysty overlay do zamykania po kliknięciu poza listę)
        blockerObj = new GameObject("Dd_Blocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        blockerObj.transform.SetParent(windowTransform, false);
        RectTransform blockerRect = blockerObj.GetComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = Vector2.zero;
        blockerRect.offsetMax = Vector2.zero;

        Image blockerImg = blockerObj.GetComponent<Image>();
        blockerImg.color = new Color(0f, 0f, 0f, 0.01f); // prawie niewidoczny, łapie kliknięcia
        blockerImg.raycastTarget = true;

        Button blockerBtn = blockerObj.GetComponent<Button>();
        ColorBlock blkColors = blockerBtn.colors;
        blkColors.normalColor = Color.clear;
        blkColors.highlightedColor = Color.clear;
        blkColors.pressedColor = Color.clear;
        blkColors.selectedColor = Color.clear;
        blockerBtn.colors = blkColors;
        blockerBtn.onClick.AddListener(CloseDropdown);
        blockerObj.SetActive(false);

        // Panel listy opcji
        popupPanel = new GameObject("Dd_PopupPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        popupPanel.transform.SetParent(windowTransform, false);
        popupRect = popupPanel.GetComponent<RectTransform>();
        popupRect.pivot = new Vector2(0.5f, 1f);

        Image popupBg = popupPanel.GetComponent<Image>();
        popupBg.color = new Color(0.10f, 0.11f, 0.14f, 0.99f); // ciemne tło listy opcji

        // Kontener elementów
        GameObject listContainer = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        listContainer.transform.SetParent(popupPanel.transform, false);
        RectTransform listRt = listContainer.GetComponent<RectTransform>();
        listRt.anchorMin = Vector2.zero;
        listRt.anchorMax = Vector2.one;
        listRt.offsetMin = new Vector2(4f, 4f);
        listRt.offsetMax = new Vector2(-4f, -4f);

        VerticalLayoutGroup popupLayout = listContainer.GetComponent<VerticalLayoutGroup>();
        popupLayout.spacing = 3f;
        popupLayout.childControlHeight = true;
        popupLayout.childControlWidth = true;
        popupLayout.childForceExpandHeight = false;
        popupLayout.childForceExpandWidth = true;

        popupContainer = listContainer.transform;
        popupPanel.SetActive(false);
    }

    /// <summary>
    /// Ustawia listę dostępnych opcji i indeks domyślny.
    /// </summary>
    public void SetOptions(List<string> newOptions, int defaultIndex = 0)
    {
        options.Clear();
        if (newOptions != null)
        {
            options.AddRange(newOptions);
        }

        RebuildOptionItems();
        SetValue(defaultIndex, notify: false);
    }

    private void RebuildOptionItems()
    {
        // Usuń stare przyciski opcji
        for (int i = 0; i < itemButtons.Count; i++)
        {
            if (itemButtons[i] != null)
            {
                Destroy(itemButtons[i].gameObject);
            }
        }
        itemButtons.Clear();
        itemTexts.Clear();
        itemCheckmarks.Clear();

        if (popupContainer == null) return;

        for (int i = 0; i < options.Count; i++)
        {
            int index = i;
            GameObject itemObj = new GameObject($"Item_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            itemObj.transform.SetParent(popupContainer, false);

            LayoutElement le = itemObj.GetComponent<LayoutElement>();
            le.minHeight = 32f;
            le.preferredHeight = 32f;

            Image itemImg = itemObj.GetComponent<Image>();
            itemImg.color = new Color(0.14f, 0.16f, 0.19f, 1f);

            Button itemBtn = itemObj.GetComponent<Button>();
            ColorBlock cb = itemBtn.colors;
            cb.normalColor = new Color(0.14f, 0.16f, 0.19f, 1f);
            cb.highlightedColor = new Color(0.24f, 0.28f, 0.36f, 1f);
            cb.pressedColor = new Color(0.18f, 0.22f, 0.30f, 1f);
            cb.selectedColor = new Color(0.20f, 0.30f, 0.24f, 1f);
            itemBtn.colors = cb;

            itemBtn.onClick.AddListener(() =>
            {
                SetValue(index, notify: true);
                CloseDropdown();
            });

            // Etykieta opcji
            GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtObj.transform.SetParent(itemObj.transform, false);
            RectTransform txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(10f, 0f);
            txtRt.offsetMax = new Vector2(-28f, 0f);

            TextMeshProUGUI tmp = txtObj.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) tmp.font = fontAsset;
            tmp.text = options[i];
            tmp.fontSize = 13f; // GEMINI.md: 12-14 pt
            tmp.color = new Color(0.88f, 0.90f, 0.94f, 1f);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.overflowMode = TextOverflowModes.Ellipsis;

            // Znacznik wyboru (✓)
            GameObject chkObj = new GameObject("Checkmark", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            chkObj.transform.SetParent(itemObj.transform, false);
            RectTransform chkRt = chkObj.GetComponent<RectTransform>();
            chkRt.anchorMin = new Vector2(1f, 0f);
            chkRt.anchorMax = new Vector2(1f, 1f);
            chkRt.pivot = new Vector2(1f, 0.5f);
            chkRt.offsetMin = new Vector2(-26f, 0f);
            chkRt.offsetMax = Vector2.zero;

            TextMeshProUGUI chkTmp = chkObj.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) chkTmp.font = fontAsset;
            chkTmp.text = "✓";
            chkTmp.fontSize = 13f;
            chkTmp.color = new Color(0.95f, 0.85f, 0.45f, 1f);
            chkTmp.alignment = TextAlignmentOptions.Center;
            chkTmp.gameObject.SetActive(false);

            itemButtons.Add(itemBtn);
            itemTexts.Add(tmp);
            itemCheckmarks.Add(chkTmp);
        }
    }

    /// <summary>
    /// Ustawia indeks wybranej opcji.
    /// </summary>
    public void SetValue(int index, bool notify = true)
    {
        if (options.Count == 0)
        {
            selectedIndex = -1;
            if (captionText != null) captionText.text = "";
            return;
        }

        selectedIndex = Mathf.Clamp(index, 0, options.Count - 1);
        if (captionText != null)
        {
            captionText.text = options[selectedIndex];
        }

        HighlightSelectedItem();

        if (notify)
        {
            onValueChanged?.Invoke(selectedIndex);
        }
    }

    private void HighlightSelectedItem()
    {
        for (int i = 0; i < itemButtons.Count; i++)
        {
            bool isSel = (i == selectedIndex);
            if (itemCheckmarks[i] != null)
            {
                itemCheckmarks[i].gameObject.SetActive(isSel);
            }
            if (itemButtons[i] != null)
            {
                Image img = itemButtons[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = isSel ? new Color(0.18f, 0.28f, 0.22f, 1f) : new Color(0.14f, 0.16f, 0.19f, 1f);
                }
            }
        }
    }

    public void ToggleDropdown()
    {
        if (IsOpen)
            CloseDropdown();
        else
            OpenDropdown();
    }

    public void OpenDropdown()
    {
        CloseAllDropdowns();

        if (popupPanel == null || rootRect == null) return;

        HighlightSelectedItem();

        if (blockerObj != null)
        {
            blockerObj.SetActive(true);
            blockerObj.transform.SetAsLastSibling();
        }

        popupPanel.SetActive(true);
        popupPanel.transform.SetAsLastSibling();

        // Oblicz pozycję poniżej przycisku rozwijania
        float itemH = 34f;
        float totalH = (options.Count * itemH) + 12f;
        popupRect.sizeDelta = new Vector2(rootRect.rect.width, totalH);

        // Przelicz współrzędne względem windowTransform
        Vector3 worldBottomCenter = rootRect.TransformPoint(new Vector3(0f, -rootRect.rect.height / 2f, 0f));
        Vector3 localPos = windowTransform.InverseTransformPoint(worldBottomCenter);
        popupRect.localPosition = new Vector3(localPos.x, localPos.y - 2f, 0f);

        if (arrowText != null)
        {
            arrowText.text = "▲";
        }
    }

    public void CloseDropdown()
    {
        if (blockerObj != null)
        {
            blockerObj.SetActive(false);
        }
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
        if (arrowText != null)
        {
            arrowText.text = "▼";
        }
    }
}
