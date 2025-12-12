using UnityEngine;
using UnityEngine.UI;

public class BuildMenuManager : MonoBehaviour
{
    [Header("Menu Ekonomii")]
    public GameObject economyMenuPanel; // Panel z list¹ budynków (Tartak, Kopalnia...)
    public Button openEconomyButton;    // Przycisk "Buduj" (M³otek?)
    public Button closeEconomyButton;   // Przycisk "X" wewn¹trz panelu

    [Header("Menu Wie¿")]
    public GameObject towersPanel;      // Panel z wie¿ami (zawsze widoczny)

    private bool isEconomyOpen = false;

    void Start()
    {
        // Na start ukrywamy menu ekonomii, pokazujemy wie¿e
        if (economyMenuPanel != null) economyMenuPanel.SetActive(false);
        if (towersPanel != null) towersPanel.SetActive(true);

        // Podpiêcie przycisków
        if (openEconomyButton != null)
            openEconomyButton.onClick.AddListener(ToggleEconomyMenu);

        if (closeEconomyButton != null)
            closeEconomyButton.onClick.AddListener(CloseEconomyMenu);
    }

    public void ToggleEconomyMenu()
    {
        isEconomyOpen = !isEconomyOpen;
        economyMenuPanel.SetActive(isEconomyOpen);
    }

    public void CloseEconomyMenu()
    {
        isEconomyOpen = false;
        economyMenuPanel.SetActive(false);
    }

    // Opcjonalnie: Zamknij menu, jeœli klikniemy w budynek (¿eby ods³oniæ widok)
    // Mo¿esz to wywo³aæ z BuildUIButton
    public void OnBuildingSelected()
    {
        // CloseEconomyMenu(); // Odkomentuj, jeœli chcesz zamykaæ menu po wyborze
    }
}