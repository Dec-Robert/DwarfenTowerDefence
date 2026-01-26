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
            "Sterowanie kamer¹:\n" +
            "• [W, A, S, D] - Poruszanie siê po mapie\n" +
            "• [Q] i [E] - Zmiana wysokoœci (Zoom)\n\n" +
            "U¿yj lewego przycisku myszy, aby wchodziæ w interakcjê z budynkami i mg³¹."
        );

        // KROK 2: Pracownicy (Przesuniêty)
        tutorialSteps.Add(
            "¯eby budynek generowa³ surowce lub ¿eby wie¿e strzela³y potrzebuj¹ pracowników.\n\n" +
            "Kliknij na budynek aby otworzyæ menu.\n\n" +
            "Twoi pracownicy pracuj¹ tylko podczas zmian."
        );

        // KROK 3: Beacon (Przesuniêty)
        tutorialSteps.Add(
            "Pamiêtaj, ¿eby poziom ognia latarni nie spad³ poni¿ej 3 poziomu!\n\n" +
            "Ka¿dy poziom poni¿ej wzmacnia twoich wrogów, ale wrogowie z odmêtów przynosz¹ wtedy wiêcej skarbów.\n\n" +
            "Jeœli masz wystarczaj¹co wêgla, mo¿esz rozœwietliæ latarniê do wy¿szych poziomów – wtedy wrogowie stan¹ siê s³absi, a twoi ludzie bêd¹ pracowaæ wydajniej."
        );
    }

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        popup = root.Q<VisualElement>("TutorialPopup");
        closeBtn = root.Q<Button>("Btn_CloseTutorial");

        // Musimy znaleŸæ Label, ¿eby zmieniaæ jego tekst. 
        // W poprzednim UXML nie daliœmy mu nazwy, wiêc szukamy po klasie lub typie.
        // Jeœli masz nazwê w UXML, u¿yj jej. Jeœli nie, to zadzia³a:
        contentLabel = popup.Q<Label>(className: "tutorial-text");

        if (closeBtn != null)
        {
            closeBtn.clicked += NextStepOrClose;
        }

        // Poka¿ pierwszy krok na starcie
        ShowStep(0);
    }

    void ShowStep(int index)
    {
        if (contentLabel != null && index < tutorialSteps.Count)
        {
            contentLabel.text = tutorialSteps[index];

            // Opcjonalnie: Mo¿emy zmieniæ tekst przycisku na "Dalej" (jeœli to nie jest ostatni krok)
            // Ale "X" te¿ jest ok (jako "zamknij tê wiadomoœæ")
        }
    }

    void NextStepOrClose()
    {
        currentStepIndex++;

        if (currentStepIndex < tutorialSteps.Count)
        {
            // Mamy kolejn¹ stronê -> wyœwietl j¹
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