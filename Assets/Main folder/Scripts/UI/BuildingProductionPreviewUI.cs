using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Trójwymiarowa wisząca plakietka podglądu (World-Space Billboard) prezentująca szacowaną produkcję
/// surowców nad duchem budynku w trakcie celowania/budowania na mapie.
/// Zgodna ze standardami UI z GEMINI.md:
/// - 16 pt dla tekstu produkcji
/// - 10 pt dla pomocniczego wskaźnika kary z sąsiadów
/// - 24 px dla małych ikon surowców
/// </summary>
public class BuildingProductionPreviewUI : MonoBehaviour
{
    private class ResourceSlotView
    {
        public GameObject root;
        public Image icon;
        public TextMeshProUGUI txtAmount;
        public TextMeshProUGUI txtPenalty;
    }

    [SerializeField] private ResourceVisualsSO resourceVisualsSO;
    [SerializeField] private RectTransform itemsContainer;

    private readonly List<ResourceSlotView> pooledSlots = new List<ResourceSlotView>();

    private HexCell lastCell;
    private BuildingData lastData;
    private bool lastIsValid;

    public void SetVisualsDB(ResourceVisualsSO visualsDB)
    {
        resourceVisualsSO = visualsDB;
    }

    private void LateUpdate()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            transform.rotation = cam.transform.rotation;
        }
    }

    /// <summary>
    /// Aktualizuje treść plakietki na podstawie aktualnie wskazywanego heksa oraz wybranego budynku.
    /// </summary>
    public void UpdatePreview(HexCell cell, BuildingData data, bool isValid)
    {
        if (!isValid || cell == null || data == null || !BuildingProductionCalculator.HasProduction(data))
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            lastCell = null;
            lastData = null;
            return;
        }

        // Cache zapobiegający niepotrzebnym przeliczeniom przy braku zmiany pozycji/stanu
        if (cell == lastCell && data == lastData && isValid == lastIsValid && gameObject.activeSelf)
        {
            return;
        }

        lastCell = cell;
        lastData = data;
        lastIsValid = isValid;

        var estimates = BuildingProductionCalculator.CalculateProjectedProduction(cell, data);
        if (estimates == null || estimates.Count == 0)
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            return;
        }

        // Wyświetl plakietkę
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        // Dopasuj liczbę slotów
        while (pooledSlots.Count < estimates.Count)
        {
            pooledSlots.Add(CreateSlot(itemsContainer));
        }

        for (int i = 0; i < pooledSlots.Count; i++)
        {
            if (i < estimates.Count)
            {
                pooledSlots[i].root.SetActive(true);
                PopulateSlot(pooledSlots[i], estimates[i]);
            }
            else
            {
                pooledSlots[i].root.SetActive(false);
            }
        }

        // Wymuszenie odświeżenia layoutu
        LayoutRebuilder.ForceRebuildLayoutImmediate(itemsContainer);
        if (transform is RectTransform rt)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    /// <summary>
    /// Aktualizuje tresc plakietki w trybie podgladu wplywu na sasiedni istniejacy budynek.
    /// Prezentuje przekreslona biezaca produkcje oraz nowa (na czerwono jesli maleje).
    /// </summary>
    public void UpdateImpact(List<BuildingProductionCalculator.NeighborBuildingImpact> impacts)
    {
        if (impacts == null || impacts.Count == 0)
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            return;
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        while (pooledSlots.Count < impacts.Count)
        {
            pooledSlots.Add(CreateSlot(itemsContainer));
        }

        for (int i = 0; i < pooledSlots.Count; i++)
        {
            if (i < impacts.Count)
            {
                pooledSlots[i].root.SetActive(true);
                PopulateImpactSlot(pooledSlots[i], impacts[i]);
            }
            else
            {
                pooledSlots[i].root.SetActive(false);
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(itemsContainer);
        if (transform is RectTransform rt)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }
    }

    private void PopulateImpactSlot(ResourceSlotView slot, BuildingProductionCalculator.NeighborBuildingImpact impact)
    {
        // 1. Ikona surowca (24 px per GEMINI.md)
        if (slot.icon != null)
        {
            Sprite s = resourceVisualsSO != null ? resourceVisualsSO.GetIcon(impact.resourceType) : null;
            if (s != null)
            {
                slot.icon.sprite = s;
                slot.icon.enabled = true;
            }
            else
            {
                slot.icon.enabled = false;
            }
        }

        // 2. Wartosc produkcji: przekreslona biezaca + nowa (na czerwono jesli maleje)
        if (slot.txtAmount != null)
        {
            string curStr = FormatAmount(impact.currentProduction);
            string projStr = FormatAmount(impact.projectedProduction);

            string colorTag = impact.difference < 0 ? "<color=#F87171>" : "<color=#4ADE80>";
            slot.txtAmount.text = $"<s>{curStr}</s>  {colorTag}{projStr}</color>";
        }

        // 3. Wskaznik zmiany / roznicy (10 pt per GEMINI.md)
        if (slot.txtPenalty != null)
        {
            float diff = impact.difference;
            if (Mathf.Abs(diff) > 0.05f)
            {
                slot.txtPenalty.gameObject.SetActive(true);
                string diffStr = (Mathf.Abs(diff % 1f) < 0.01f)
                    ? $"({(diff > 0 ? "+" : "")}{(int)Mathf.Round(diff)})"
                    : $"({(diff > 0 ? "+" : "")}{diff:0.#})";
                slot.txtPenalty.color = diff < 0 ? new Color32(248, 113, 113, 255) : new Color32(74, 222, 128, 255);
                slot.txtPenalty.text = diffStr;
            }
            else
            {
                slot.txtPenalty.gameObject.SetActive(false);
            }
        }
    }

    private static string FormatAmount(float val)
    {
        return (Mathf.Abs(val % 1f) < 0.01f)
            ? $"+{(int)Mathf.Round(val)}"
            : $"+{val:0.#}";
    }

    private void PopulateSlot(ResourceSlotView slot, BuildingProductionCalculator.ResourceProductionEstimate est)
    {
        // 1. Ikona surowca (24 px)
        if (slot.icon != null)
        {
            Sprite s = resourceVisualsSO != null ? resourceVisualsSO.GetIcon(est.resourceType) : null;
            if (s != null)
            {
                slot.icon.sprite = s;
                slot.icon.enabled = true;
            }
            else
            {
                slot.icon.enabled = false;
            }
        }

        // 2. Wartość produkcji (16 pt, #4ADE80)
        if (slot.txtAmount != null)
        {
            float val = est.totalProduction;
            string formatted = (Mathf.Abs(val % 1f) < 0.01f)
                ? $"+{(int)Mathf.Round(val)}"
                : $"+{val:0.#}";

            slot.txtAmount.text = formatted;
        }

        // 3. Wskaźnik kary z sąsiadów (10 pt, #F87171)
        if (slot.txtPenalty != null)
        {
            if (est.neighborPenalty > 0.05f)
            {
                slot.txtPenalty.gameObject.SetActive(true);
                string penStr = (Mathf.Abs(est.neighborPenalty % 1f) < 0.01f)
                    ? $"(-{(int)Mathf.Round(est.neighborPenalty)})"
                    : $"(-{est.neighborPenalty:0.#})";
                slot.txtPenalty.text = penStr;
            }
            else
            {
                slot.txtPenalty.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Fabryka tworząca procedularnie obiekt World-Space plakietki nad wskazanym duchem budynku.
    /// </summary>
    public static GameObject CreatePreview(Transform parent, ResourceVisualsSO visualsDB)
    {
        var root = new GameObject("Pnl_ProductionPreview", typeof(RectTransform));
        if (parent != null)
        {
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        }
        else
        {
            root.transform.position = Vector3.zero;
        }
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * 0.012f;

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 500;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        var group = root.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        // Tło kapsułki (Sleek Dark Pill)
        var bgObj = new GameObject("Background", typeof(RectTransform));
        bgObj.transform.SetParent(root.transform, false);

        var bgImage = bgObj.AddComponent<Image>();
        bgImage.color = new Color32(20, 22, 28, 235);
        bgImage.raycastTarget = false;

        var hlg = bgObj.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(8, 8, 4, 4);
        hlg.spacing = 8;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var csf = bgObj.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var previewComp = root.AddComponent<BuildingProductionPreviewUI>();
        previewComp.itemsContainer = bgObj.GetComponent<RectTransform>();
        previewComp.SetVisualsDB(visualsDB);

        root.SetActive(false);
        return root;
    }

    private ResourceSlotView CreateSlot(RectTransform parent)
    {
        var slotObj = new GameObject("ResourceSlot", typeof(RectTransform));
        slotObj.transform.SetParent(parent, false);

        var hlg = slotObj.AddComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(2, 2, 2, 2);
        hlg.spacing = 4;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var csf = slotObj.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 1. Ikona (24 px per GEMINI.md)
        var iconObj = new GameObject("Icon", typeof(RectTransform));
        iconObj.transform.SetParent(slotObj.transform, false);
        var iconRect = iconObj.GetComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(24f, 24f);

        var iconLayout = iconObj.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 24f;
        iconLayout.preferredHeight = 24f;
        iconLayout.minWidth = 24f;
        iconLayout.minHeight = 24f;

        var iconImg = iconObj.AddComponent<Image>();
        iconImg.raycastTarget = false;

        // 2. Tekst wartości (16 pt per GEMINI.md)
        var txtAmountObj = new GameObject("Txt_Amount", typeof(RectTransform));
        txtAmountObj.transform.SetParent(slotObj.transform, false);

        var txtAmount = txtAmountObj.AddComponent<TextMeshProUGUI>();
        txtAmount.fontSize = 16f;
        txtAmount.fontStyle = FontStyles.Bold;
        txtAmount.color = new Color32(74, 222, 128, 255); // #4ADE80
        txtAmount.alignment = TextAlignmentOptions.Center;
        txtAmount.raycastTarget = false;

        var txtAmountLayout = txtAmountObj.AddComponent<LayoutElement>();
        txtAmountLayout.preferredHeight = 24f;

        // 3. Tekst kary z sąsiadów (10 pt per GEMINI.md)
        var txtPenaltyObj = new GameObject("Txt_Penalty", typeof(RectTransform));
        txtPenaltyObj.transform.SetParent(slotObj.transform, false);

        var txtPenalty = txtPenaltyObj.AddComponent<TextMeshProUGUI>();
        txtPenalty.fontSize = 10f;
        txtPenalty.fontStyle = FontStyles.Normal;
        txtPenalty.color = new Color32(248, 113, 113, 255); // #F87171
        txtPenalty.alignment = TextAlignmentOptions.Center;
        txtPenalty.raycastTarget = false;

        var txtPenaltyLayout = txtPenaltyObj.AddComponent<LayoutElement>();
        txtPenaltyLayout.preferredHeight = 24f;
        txtPenaltyObj.SetActive(false);

        return new ResourceSlotView
        {
            root = slotObj,
            icon = iconImg,
            txtAmount = txtAmount,
            txtPenalty = txtPenalty
        };
    }
}
