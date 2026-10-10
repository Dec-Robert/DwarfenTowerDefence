using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Komponent dołączany do slotów kowadła w zakładkach Fuzji i Transmutacji.
/// Obsługuje kliknięcie (zdjęcie z kowadła), przeciągnięcie z kowadła na zewnątrz oraz upuszczenie runy na kowadło.
/// </summary>
public class RuneCraftSlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    private RuneDragSource craftType;
    private int craftIndex;
    private RuneItem placedRune;
    private RuneForgeWindowUI window;
    private bool isDragging;

    public void Setup(RuneDragSource type, int index, RuneItem rune, RuneForgeWindowUI windowUI)
    {
        this.craftType = type;
        this.craftIndex = index;
        this.placedRune = rune;
        this.window = windowUI;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (placedRune != null && window != null)
        {
            window.OnCraftSlotClicked(craftType, craftIndex);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (placedRune == null) return;

        isDragging = true;
        if (RuneDragDropController.Instance != null)
        {
            RuneDragDropController.Instance.StartDragging(placedRune, craftType, craftIndex, eventData.position);
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
        if (!isDragging) return;
        isDragging = false;

        var controller = RuneDragDropController.Instance;
        if (controller != null)
        {
            if (!controller.DropHandled && window != null)
            {
                window.OnCraftSlotClicked(craftType, craftIndex);
            }
            controller.EndDragging();
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        var controller = RuneDragDropController.Instance;
        if (controller == null || !controller.IsDragging || controller.DraggedRune == null) return;

        controller.DropHandled = true;
        if (window != null)
        {
            window.OnRuneDroppedOnCraftSlot(craftType, craftIndex, controller.DraggedRune, controller.SourceType, controller.SourceIndex);
        }
    }
}
