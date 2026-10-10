using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Komponent dołączany do prefabu Card_RunesmithSlot w widoku osadzania Kuźni.
/// Obsługuje kliknięcie (zdjęcie runy), przeciągnięcie runy ze slotu na zewnątrz (zdjęcie runy)
/// oraz upuszczenie przeciąganej runy do tego slotu (osadzenie runy).
/// </summary>
public class RuneSocketSlotUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    private int slotIndex;
    private RuneItem equippedRune;
    private bool isUnlocked;
    private bool hasSmith;
    private RuneForgeWindowUI window;
    private bool isDragging;

    public void Setup(int index, RuneItem rune, bool unlocked, bool smith, RuneForgeWindowUI windowUI)
    {
        this.slotIndex = index;
        this.equippedRune = rune;
        this.isUnlocked = unlocked;
        this.hasSmith = smith;
        this.window = windowUI;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (equippedRune != null && window != null)
        {
            window.OnSlotClicked(slotIndex);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (equippedRune == null) return;

        isDragging = true;
        if (RuneDragDropController.Instance != null)
        {
            RuneDragDropController.Instance.StartDragging(equippedRune, RuneDragSource.Socket, slotIndex, eventData.position);
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
            // Jeśli przeciągnięto osadzoną runę i nie upuszczono jej na inny akceptujący slot -> zdejmij ją
            if (!controller.DropHandled && window != null)
            {
                window.OnSlotClicked(slotIndex);
            }
            controller.EndDragging();
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        var controller = RuneDragDropController.Instance;
        if (controller == null || !controller.IsDragging || controller.DraggedRune == null) return;

        // Slot musi być odblokowany przez wieże i mieć przydzielonego kowala
        if (!isUnlocked || !hasSmith) return;

        controller.DropHandled = true;
        if (window != null)
        {
            window.OnRuneDroppedOnSlot(slotIndex, controller.DraggedRune, controller.SourceType, controller.SourceIndex);
        }
    }
}
