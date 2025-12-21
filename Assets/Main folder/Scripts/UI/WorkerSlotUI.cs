using UnityEngine;
using UnityEngine.UI;

public class WorkerSlotUI : MonoBehaviour
{
    public Image slotImage; // Przypisz tu komponent Image tego prefabu

    private void Awake()
    {
        if (slotImage == null) slotImage = GetComponent<Image>();
    }

    public void UpdateSlot(Citizen citizen)
    {
        if (citizen == null)
        {
            // Pusty slot
            slotImage.color = Color.white;
        }
        else
        {
            // Zmiana koloru w zale¿noœci od stanu
            switch (citizen.workState)
            {
                case WorkState.Assigned:
                    slotImage.color = Color.green; // Przypisany, czeka
                    break;
                case WorkState.Working:
                    slotImage.color = Color.black; // Pracuje
                    break;
                case WorkState.Exhausted:
                    slotImage.color = Color.red;   // Wyczerpany
                    break;
                case WorkState.Idle:
                    // Teoretycznie nie powinno siê zdarzyæ w budynku, ale dla bezpieczeñstwa:
                    slotImage.color = Color.gray;
                    break;
                default:
                    slotImage.color = Color.white;
                    break;
            }
        }
    }
}