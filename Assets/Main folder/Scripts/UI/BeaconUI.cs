using UnityEngine;
using UnityEngine.UIElements;
using System.Collections; // Potrzebne do IEnumerator
using System.Collections.Generic;

public class BeaconUI : MonoBehaviour
{
    [Header("Referencje")]
    public UIDocument uiDocument;

    private List<VisualElement> segments = new List<VisualElement>();
    private Label fuelInfoLabel;

    [Header("Kolory Poziomów")]
    public Color colorLvl1 = new Color(0.05f, 0.05f, 0.2f); // Ciemny
    public Color colorLvl2 = new Color(0.2f, 0.4f, 0.8f);  // Niebieski
    public Color colorLvl3 = new Color(1f, 1f, 0.8f);      // ¯ó³tawy
    public Color colorLvl4 = new Color(1f, 0.6f, 0.0f);    // Pomarañcz
    public Color colorLvl5 = new Color(1f, 0.2f, 0.0f);    // Czerwony
    public Color colorInactive = new Color(0.2f, 0.2f, 0.2f, 0.5f); // Szary (t³o)

    private IEnumerator Start()
    {
        var root = uiDocument.rootVisualElement;

        // 1. Pobranie referencji do UI
        for (int i = 0; i < 9; i++)
        {
            var seg = root.Q<VisualElement>($"Segment_{i}");
            if (seg != null) segments.Add(seg);
        }

        fuelInfoLabel = root.Q<Label>("Lbl_FuelInfo");

        // 2. OCZEKIWANIE NA SPAWN BEACONA (Naprawa b³êdu)
        // Czekamy, dopóki Instance jest nullem
        yield return new WaitUntil(() => BeaconEntity.Instance != null);

        // 3. Subskrypcja
        BeaconEntity.Instance.OnBeaconStateChanged += RefreshUI;

        // 4. Pierwsze odœwie¿enie
        RefreshUI();
    }

    private void OnDestroy()
    {
        if (BeaconEntity.Instance != null)
        {
            BeaconEntity.Instance.OnBeaconStateChanged -= RefreshUI;
        }
    }

    void RefreshUI()
    {
        if (BeaconEntity.Instance == null) return;

        int level = BeaconEntity.Instance.CurrentFireLevel;
        float fuel = BeaconEntity.Instance.currentFuel;

        if (fuelInfoLabel != null)
            fuelInfoLabel.text = $"Paliwo: {fuel:F0}/100 (Poz: {level})";

        Color activeColor = GetColorForLevel(level);

        // Symetryczne zapalanie pasków (Œrodek to index 4)
        int range = (level - 1);
        int minIdx = 4 - range;
        int maxIdx = 4 + range;

        for (int i = 0; i < segments.Count; i++)
        {
            if (i >= minIdx && i <= maxIdx)
                segments[i].style.backgroundColor = activeColor;
            else
                segments[i].style.backgroundColor = colorInactive;
        }
    }

    Color GetColorForLevel(int level)
    {
        switch (level)
        {
            case 1: return colorLvl1;
            case 2: return colorLvl2;
            case 3: return colorLvl3;
            case 4: return colorLvl4;
            case 5: return colorLvl5;
            default: return Color.white;
        }
    }
}