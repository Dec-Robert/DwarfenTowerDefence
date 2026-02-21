using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class TutorialController : MonoBehaviour
{
    [Header("Referencje")]
    public UIDocument uiDocument;

    // Elementy UI
    private VisualElement popup;
    private Button closeBtn;
    private Label contentLabel;

    // Dane
    private int currentStepIndex = 0;
    private List<string> tutorialSteps = new List<string>();

    private void Awake()
    {
        // KROK 1: Sterowanie (NOWY)
        tutorialSteps.Add(
            "Witaj w grze!\n\n" +
            "Sterowanie kamer�:\n" +
            "� [W, A, S, D] - Poruszanie si� po mapie\n" +
            "� [Q] i [E] - Zmiana wysoko�ci (Zoom)\n\n" +
            "U�yj lewego przycisku myszy, aby wchodzi� w interakcj� z budynkami i mg��."
        );

        // KROK 2: Pracownicy (Przesuni�ty)
        tutorialSteps.Add(
            "�eby budynek generowa� surowce lub �eby wie�e strzela�y potrzebuj� pracownik�w.\n\n" +
            "Kliknij na budynek aby otworzy� menu.\n\n" +
            "Twoi pracownicy pracuj� tylko podczas zmian."
        );
//
        // KROK 3: Beacon (Przesuni�ty)
        tutorialSteps.Add(
            "Pami�taj, �eby poziom ognia latarni nie spad� poni�ej 3 poziomu!\n\n" +
            "Ka�dy poziom poni�ej wzmacnia twoich wrog�w, ale wrogowie z odm�t�w przynosz� wtedy wi�cej skarb�w.\n\n" +
            "Je�li masz wystarczaj�co w�gla, mo�esz roz�wietli� latarni� do wy�szych poziom�w � wtedy wrogowie stan� si� s�absi, a twoi ludzie b�d� pracowa� wydajniej."
        );
    }

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        popup = root.Q<VisualElement>("TutorialPopup");
        closeBtn = root.Q<Button>("Btn_CloseTutorial");

        // Musimy znale�� Label, �eby zmienia� jego tekst. 
        // W poprzednim UXML nie dali�my mu nazwy, wi�c szukamy po klasie lub typie.
        // Je�li masz nazw� w UXML, u�yj jej. Je�li nie, to zadzia�a:
        contentLabel = popup.Q<Label>(className: "tutorial-text");

        if (closeBtn != null)
        {
            closeBtn.clicked += NextStepOrClose;
        }

        // Poka� pierwszy krok na starcie
        ShowStep(0);
    }

    void ShowStep(int index)
    {
        if (contentLabel != null && index < tutorialSteps.Count)
        {
            contentLabel.text = tutorialSteps[index];

            // Opcjonalnie: Mo�emy zmieni� tekst przycisku na "Dalej" (je�li to nie jest ostatni krok)
            // Ale "X" te� jest ok (jako "zamknij t� wiadomo��")
        }
    }

    void NextStepOrClose()
    {
        currentStepIndex++;

        if (currentStepIndex < tutorialSteps.Count)
        {
            // Mamy kolejn� stron� -> wy�wietl j�
            ShowStep(currentStepIndex);
        }
        else
        {
            // Koniec tutoriala -> zamknij okno
            if (popup != null)
            {
                popup.style.display = DisplayStyle.None;
            }
        }
    }
}