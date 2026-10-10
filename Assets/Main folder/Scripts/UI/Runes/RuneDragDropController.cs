using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum RuneDragSource
{
    Inventory,
    Socket,
    Fusion,
    Transmutation
}

/// <summary>
/// Kontroler Drag & Drop dla run w Kuźni Runicznej.
/// Zarządza pływającą ikoną-duchem (Ghost Icon), która podąża za kursorem myszy na wierzchu interfejsu.
/// </summary>
public class RuneDragDropController : MonoBehaviour
{
    public static RuneDragDropController Instance { get; private set; }

    private RectTransform ghostRoot;
    private Image ghostIcon;
    private Canvas rootCanvas;

    public bool IsDragging { get; private set; }
    public RuneItem DraggedRune { get; private set; }
    public RuneDragSource SourceType { get; private set; }
    public int SourceIndex { get; private set; }
    public bool DropHandled { get; set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        EnsureGhostSetup();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (ghostRoot != null) Destroy(ghostRoot.gameObject);
    }

    private void EnsureGhostSetup()
    {
        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null)
                rootCanvas = Object.FindFirstObjectByType<Canvas>();
        }

        if (ghostRoot == null && rootCanvas != null)
        {
            GameObject ghostObj = new GameObject("RuneGhostIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            ghostObj.transform.SetParent(rootCanvas.transform, false);

            ghostRoot = ghostObj.GetComponent<RectTransform>();
            ghostRoot.sizeDelta = new Vector2(56f, 56f);
            ghostRoot.pivot = new Vector2(0.5f, 0.5f);

            ghostIcon = ghostObj.GetComponent<Image>();
            ghostIcon.raycastTarget = false;

            CanvasGroup cg = ghostObj.GetComponent<CanvasGroup>();
            cg.alpha = 0.85f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            ghostObj.SetActive(false);
        }
    }

    public void StartDragging(RuneItem rune, RuneDragSource source, int sourceIndex, Vector2 screenPos)
    {
        if (rune == null) return;

        EnsureGhostSetup();
        IsDragging = true;
        DraggedRune = rune;
        SourceType = source;
        SourceIndex = sourceIndex;
        DropHandled = false;

        if (ghostRoot != null)
        {
            ghostRoot.gameObject.SetActive(true);
            ghostRoot.SetAsLastSibling();

            if (ghostIcon != null)
            {
                ghostIcon.sprite = rune.definition != null ? rune.definition.icon : null;
                ghostIcon.color = rune.definition?.icon != null ? Color.white : GetRarityColor(rune.definition != null ? rune.definition.rarity : RuneRarity.Common);
                ghostIcon.preserveAspect = true;
            }

            UpdatePosition(screenPos);
        }
    }

    public void UpdateDragging(Vector2 screenPos)
    {
        if (!IsDragging) return;
        UpdatePosition(screenPos);
    }

    private void UpdatePosition(Vector2 screenPos)
    {
        if (ghostRoot == null || rootCanvas == null) return;

        RectTransform canvasRt = rootCanvas.transform as RectTransform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPos, rootCanvas.worldCamera, out Vector2 localPoint))
        {
            ghostRoot.anchoredPosition = localPoint;
        }
    }

    public void EndDragging()
    {
        IsDragging = false;
        if (ghostRoot != null)
        {
            ghostRoot.gameObject.SetActive(false);
        }
    }

    private Color GetRarityColor(RuneRarity rarity)
    {
        return rarity switch
        {
            RuneRarity.Common => new Color(0.85f, 0.85f, 0.85f, 1f),
            RuneRarity.Uncommon => new Color(0.35f, 0.85f, 0.45f, 1f),
            RuneRarity.Rare => new Color(0.3f, 0.65f, 0.95f, 1f),
            RuneRarity.Legendary => new Color(0.96f, 0.78f, 0.28f, 1f),
            RuneRarity.Cursed => new Color(0.92f, 0.28f, 0.28f, 1f),
            _ => Color.white
        };
    }
}
