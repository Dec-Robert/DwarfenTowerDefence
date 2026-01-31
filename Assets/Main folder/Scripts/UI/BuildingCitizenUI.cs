using UnityEngine;
using System.Collections.Generic;

public class BuildingCitizenUI : MonoBehaviour
{
    public static BuildingCitizenUI Instance;

    [Header("Referencje")]
    public GameObject shiftPanel;
    public GameObject shiftPrefab;
    public GameObject workerPrefab;

    // Lista przechowuj¹ca aktywne sloty UI
    private List<WorkerSlotUI> allSlots = new List<WorkerSlotUI>();

    private void Awake()
    {
        Instance = this;
    }

    public void Setup(int shiftNumber, int workersPerShift)
    {
        // 1. WA¯NE: Czyœcimy listê referencji, bo stare obiekty zaraz zostan¹ zniszczone
        allSlots.Clear();

        // 2. Czyœcimy wizualnie stare obiekty z panelu
        foreach (Transform child in shiftPanel.transform)
        {
            Destroy(child.gameObject);
        }

        // 3. Tworzymy nowe
        for (int i = 0; i < shiftNumber; i++)
        {
            GameObject shift = Instantiate(shiftPrefab, shiftPanel.transform);

            // Zak³adamy, ¿e prefab zmiany ma kontener na sloty
            // Jeœli nie ma dziecka o tej nazwie, u¿ywamy samego obiektu zmiany
            Transform workerContainer = shift.transform.Find("Background for slots");
            if (workerContainer == null) workerContainer = shift.transform;

            // Czyœcimy œmieci z prefabu (jeœli s¹)
            foreach (Transform child in workerContainer)
            {
                Destroy(child.gameObject);
            }

            for (int j = 0; j < workersPerShift; j++)
            {
                GameObject worker = Instantiate(workerPrefab, workerContainer);
                WorkerSlotUI workerUI = worker.GetComponent<WorkerSlotUI>();

                if (workerUI != null)
                {
                    allSlots.Add(workerUI);
                    // Domyœlnie ustawiamy na pusty (bia³y), Refresh zaraz to nadpisze danymi
                    workerUI.UpdateSlot(null);
                }
            }
        }
    }

    public void Refresh(BuildingEntity building)
    {
        if (building == null) return;

        List<Citizen> workers = building.GetAssignedCitizens();

        // Pêtla po wszystkich slotach UI
        for (int i = 0; i < allSlots.Count; i++)
        {
            // Dodatkowe zabezpieczenie: jeœli slot zosta³ zniszczony, pomiñ go
            if (allSlots[i] == null) continue;

            if (i < workers.Count)
            {
                // Mamy pracownika w tym slocie
                allSlots[i].UpdateSlot(workers[i]);
            }
            else
            {
                // Slot pusty (brak pracownika na tej pozycji)
                allSlots[i].UpdateSlot(null);
            }
        }
    }
}