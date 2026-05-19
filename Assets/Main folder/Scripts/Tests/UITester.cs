using UnityEngine;

public class UITester : MonoBehaviour
{
    [Header("Instrukcja:")]
    [TextArea(3, 5)]
    public string info = "Wciśnij Play, a następnie używaj klawiszy 1-5 na klawiaturze, aby testować UI. Możesz też klikać prawym przyciskiem myszy na ten komponent, by wywołać funkcje z Context Menu.";

    [Header("Ustawienia symulacji")]
    public float goldToAdd = 150.5f;
    public float woodToAdd = 50f;
    public int damageToTake = 2;

    private void Update()
    {
        // Klawisz 1: Dodaj surowce
        if (Input.GetKeyDown(KeyCode.Alpha1)) 
            SimulateAddResources();

        // Klawisz 2: Symuluj wydatki (Sprawdza, czy stać)
        if (Input.GetKeyDown(KeyCode.Alpha2)) 
            SimulateSpendResources();

        // Klawisz 3: Otrzymaj obrażenia (Pasek HP)
        if (Input.GetKeyDown(KeyCode.Alpha3)) 
            SimulateDamage();

        // Klawisz 4: Narodziny obywateli (Populacja)
        if (Input.GetKeyDown(KeyCode.Alpha4)) 
            SimulateNewCitizen();

        // Klawisz 5: Symuluj zmianę dnia (Zegar/Dni)
        if (Input.GetKeyDown(KeyCode.Alpha5)) 
            SimulateNextDay();
    }

    [ContextMenu("1. Symuluj: Dodaj Surowce")]
    public void SimulateAddResources()
    {
        if (ResourceManager.Instance == null) return;

        ResourceManager.Instance.AddResource(ResourceType.Gold, goldToAdd);
        ResourceManager.Instance.AddResource(ResourceType.Wood, woodToAdd);
        
        Debug.Log($"[UI TEST] Dodano {goldToAdd} Złota i {woodToAdd} Drewna.");
    }

    [ContextMenu("2. Symuluj: Wydaj Surowce (Kupno)")]
    public void SimulateSpendResources()
    {
        if (ResourceManager.Instance == null) return;

        if (ResourceManager.Instance.SpendResource(ResourceType.Gold, 50f))
        {
            Debug.Log("[UI TEST] Udany zakup za 50 Złota. UI powinno odjąć surowce.");
        }
        else
        {
            Debug.LogWarning("[UI TEST] Brak złota na zakup! UI powinno zablokować przycisk.");
        }
    }

    [ContextMenu("3. Symuluj: Otrzymanie Obrażeń (Baza)")]
    public void SimulateDamage()
    {
        if (GameManager.Instance == null) return;

        // Odejmujemy HP z bazy
        GameManager.Instance.ModifyBaseHealth(-damageToTake);
        Debug.Log($"[UI TEST] Baza otrzymała {damageToTake} obrażeń. UI HP powinno się zaktualizować.");
    }

    [ContextMenu("4. Symuluj: Dodaj Obywateli")]
    public void SimulateNewCitizen()
    {
        if (CitizenManager.Instance == null || ResourceManager.Instance == null) return;

        // Tworzymy sztucznego elfa, omijając domy, żeby tylko sprawdzić górny pasek
        Citizen mockElf = new Citizen();
        mockElf.InitializeCitizen(Race.Elves);
        CitizenManager.Instance.citizens.Add(mockElf);

        // Powiadamiamy ekonomię o wzroście populacji (co odświeży Twoje UI)
        ResourceManager.Instance.AddResource(ResourceType.Population, 1f);
        
        Debug.Log("[UI TEST] Dodano sztucznego Elfa do systemu.");
    }

    [ContextMenu("5. Symuluj: Zmiana Dnia (Przyspieszenie)")]
    public void SimulateNextDay()
    {
        if (TimeCycleManager.Instance == null) return;

        // Ręczne przepchnięcie czasu, by wywołać eventy OnHourTick / OnDayChanged
        TimeCycleManager.Instance.currentTime += 5f; 
        Debug.Log("[UI TEST] Przewinięto czas o 5 godzin.");
    }
}