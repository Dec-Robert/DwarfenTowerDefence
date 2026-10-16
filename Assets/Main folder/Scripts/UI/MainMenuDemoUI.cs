using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Kontroler Menu Głównego dla sceny MainMenu Demo scene.
/// Obsługuje przyciski Rozpocznij Demo, Jak Grać (poradnik z 4 kartami) oraz Wyjście.
/// Standardy interfejsu zgodne z GEMINI.md:
/// - 20 pt nagłówki
/// - 16 pt sekcje i przyciski
/// - 12-14 pt opisy i karty
/// - 10 pt teksty pomocnicze
/// </summary>
public class MainMenuDemoUI : MonoBehaviour
{
    [Header("Przyciski Główne")]
    [SerializeField] private Button btnStart;
    [SerializeField] private Button btnHowToPlay;
    [SerializeField] private Button btnExit;

    [Header("Konfiguracja Sceny")]
    [SerializeField] private string gameSceneName = "Game scene";

    [Header("Modal Jak Grać (opcjonalny prefab / obiekt)")]
    [SerializeField] private GameObject howToPlayModal;

    // Struktura karty poradnika
    [Serializable]
    public class GuideCard
    {
        public string tabTitle;
        public string cardTitle;
        [TextArea(4, 10)] public string content;
    }

    private readonly List<GuideCard> cards = new List<GuideCard>()
    {
        new GuideCard
        {
            tabTitle = "Cykl Dnia",
            cardTitle = "Cykl Dnia i Nocy w Bastionie",
            content = "Rozgrywka toczy się w stałym, dobowym rytmie podzielonym na kluczowe fazy:\n\n" +
                      "• ŚWIT / PORANEK: Czas wytchnienia po nocnej walce. Otrzymujesz podsumowanie strat, raport produkcji i przydział zasobów.\n\n" +
                      "• ZMIERZCH: Ostateczne przygotowania przed zapadnięciem zmroku. Ustaw priorytety obrony i uzupełnij wieże zaopatrzeniem.\n\n" +
                      "• NOC: Spaczona Mgła rodzi hordy potworów nacierających ku Świętej Bramie Bastionu. Twoje wieże prowadzą automatyczny ostrzał."
        },
        new GuideCard
        {
            tabTitle = "Rasy i Ekspedycje",
            cardTitle = "Rasy Mieszkańców oraz Zwiad Mgły",
            content = "Bastion zrzesza trzy zróżnicowane rasy o unikalnych predyspozycjach roboczych:\n\n" +
                      "• KRASNOLUDY: Mistrzowie kowalstwa, kamieniarstwa i ciężkiej technologii. Niezbędni w kopalniach oraz machinach parowych.\n\n" +
                      "• ELFY: Związane z magią eteru i naturą. Najbardziej wydajne w tartakach, badaniach i mistycznych strukturach obronnych.\n\n" +
                      "• LUDZIE: Wszechstronni rzemieślnicy, organizatorzy i zwiadowcy.\n\n" +
                      "• CENTRUM EKSPEDYCJI: Wysyłaj zwiadowców w nieodkryte obszary mgły, odsłaniaj chunki i zabezpieczaj nowe złoża surowców."
        },
        new GuideCard
        {
            tabTitle = "Palenisko",
            cardTitle = "Święte Palenisko Bastionu (Beacon)",
            content = "Palenisko jest sercem osady i najważniejszym punktem oporu przeciw spaczeniu:\n\n" +
                      "• MOC ŚWIĘTEGO OGNIA: Płomień odpędza opary mgły, zmniejsza agresję i osłabia żywotność nocnych bestii.\n\n" +
                      "• ZAOPATRZENIE W PALIWO: Pilnuj zapasów węgla i drewna. Wygaszenie ognia gwałtownie wzmacnia nacierające potwory!\n\n" +
                      "• SYSTEM NADZIEI: Pomyślne przetrwanie nocy i sukcesy ekspedycji generują Nadzieję, kluczową dla rozwoju bastionu."
        },
        new GuideCard
        {
            tabTitle = "Wieże i Amunicja",
            cardTitle = "Wieże Obronne i Zaopatrzenie Bojowe",
            content = "Obrona bastionu opiera się na wyspecjalizowanych wieżach strażniczych:\n\n" +
                      "• ARBALET STRAŻNICZY: Podstawowa, dalekosiężna wieża fizyczna eliminująca pierwsze fale nacierających potworów.\n\n" +
                      "• BOMBARDA KRÓLEWSKA: Ciężka artyleria burząca z pancerzobójczymi pociskami, idealna na zwarte grupy i opancerzone aberracje.\n\n" +
                      "• REPETIER PAROWY: Szybkostrzelna konstrukcja o gęstym ogniu, powstrzymująca szybkich łowców mgły.\n\n" +
                      "• IGLICA CIENI: Mistyczna iglica zadająca potężne obrażenia magiczne najgroźniejszym abominacjom.\n\n" +
                      "Pamiętaj: Każda wieża wymaga surowców na budowę oraz dziennego utrzymania!"
        }
    };

    private int currentCardIndex = 0;

    public int CurrentCardIndex => currentCardIndex;
    public int CardsCount => cards.Count;
    public GameObject HowToPlayModal => howToPlayModal;
    public IReadOnlyList<GuideCard> Cards => cards;

    // Elementy runtime dla modalu
    private TextMeshProUGUI modalCardTitle;
    private TextMeshProUGUI modalCardContent;
    private TextMeshProUGUI modalPageIndicator;
    private Button modalBtnPrev;
    private Button modalBtnNext;
    private readonly List<Button> tabButtons = new List<Button>();

    private void Awake()
    {
        FindButtonReferences();
    }

    private void Start()
    {
        FindButtonReferences();
        SetupMainButtons();
        ApplyGeminiUIStandards();

        // Przygotuj modal "Jak Grać"
        SetupHowToPlayModal();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (howToPlayModal != null && howToPlayModal.activeSelf)
            {
                CloseHowToPlay();
            }
        }
    }

    private void FindButtonReferences()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        Transform container = canvas != null ? canvas.transform : transform;

        if (btnStart == null)
        {
            Transform t = container.Find("Pnl_Background/Pnl_Button/Btn_Start") 
                       ?? container.Find("Pnl_Button/Btn_Start")
                       ?? container.Find("Btn_Start");
            if (t != null) btnStart = t.GetComponent<Button>();
        }

        if (btnHowToPlay == null)
        {
            Transform t = container.Find("Pnl_Background/Pnl_Button/Btn_HowToPlay") 
                       ?? container.Find("Pnl_Button/Btn_HowToPlay")
                       ?? container.Find("Btn_HowToPlay");
            if (t != null) btnHowToPlay = t.GetComponent<Button>();
        }

        if (btnExit == null)
        {
            Transform t = container.Find("Pnl_Background/Pnl_Button/Btn_Exit") 
                       ?? container.Find("Pnl_Button/Btn_Exit")
                       ?? container.Find("Btn_Exit");
            if (t != null) btnExit = t.GetComponent<Button>();
        }
    }

    private void SetupMainButtons()
    {
        if (btnStart != null)
        {
            btnStart.onClick.RemoveAllListeners();
            btnStart.onClick.AddListener(StartDemoGame);
        }

        if (btnHowToPlay != null)
        {
            btnHowToPlay.onClick.RemoveAllListeners();
            btnHowToPlay.onClick.AddListener(OpenHowToPlay);
        }

        if (btnExit != null)
        {
            btnExit.onClick.RemoveAllListeners();
            btnExit.onClick.AddListener(ExitGame);
        }
    }

    private void ApplyGeminiUIStandards()
    {
        // Standardy UI z GEMINI.md: 16 pt dla przycisków / opisów sekcji
        SetButtonTextAndSize(btnStart, "Rozpocznij Demo", 16f);
        SetButtonTextAndSize(btnHowToPlay, "Jak Grać", 16f);
        SetButtonTextAndSize(btnExit, "Wyjście", 16f);
    }

    private void SetButtonTextAndSize(Button btn, string newText, float fontSize)
    {
        if (btn == null) return;
        TextMeshProUGUI tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
        {
            if (!string.IsNullOrEmpty(newText)) tmp.text = newText;
            tmp.fontSize = fontSize;
        }
    }

    public void StartDemoGame()
    {
        Time.timeScale = 1f;
        if (Application.CanStreamedLevelBeLoaded(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
        else
        {
            Debug.LogError($"[MainMenuDemoUI] Nie można załadować sceny: {gameSceneName}!");
        }
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OpenHowToPlay()
    {
        if (howToPlayModal == null)
        {
            BuildRuntimeHowToPlayModal();
        }

        if (howToPlayModal != null)
        {
            howToPlayModal.SetActive(true);
            ShowCard(0);
        }
    }

    public void CloseHowToPlay()
    {
        if (howToPlayModal != null)
        {
            howToPlayModal.SetActive(false);
        }
    }

    public void NextCard()
    {
        if (currentCardIndex < cards.Count - 1)
        {
            ShowCard(currentCardIndex + 1);
        }
        else
        {
            CloseHowToPlay();
        }
    }

    public void PrevCard()
    {
        if (currentCardIndex > 0)
        {
            ShowCard(currentCardIndex - 1);
        }
    }

    public void ShowCard(int index)
    {
        if (index < 0 || index >= cards.Count) return;
        currentCardIndex = index;

        GuideCard card = cards[index];

        if (modalCardTitle != null) modalCardTitle.text = card.cardTitle;
        if (modalCardContent != null) modalCardContent.text = card.content;
        if (modalPageIndicator != null) modalPageIndicator.text = $"Karta {index + 1} z {cards.Count} • Poradnik Obrońcy";

        if (modalBtnPrev != null)
        {
            modalBtnPrev.interactable = index > 0;
        }

        if (modalBtnNext != null)
        {
            TextMeshProUGUI nextTxt = modalBtnNext.GetComponentInChildren<TextMeshProUGUI>();
            if (nextTxt != null)
            {
                nextTxt.text = (index == cards.Count - 1) ? "Zakończ" : "Następna >";
                nextTxt.fontSize = 14f;
            }
        }

        // Podświetlenie zakładek
        for (int i = 0; i < tabButtons.Count; i++)
        {
            Button tabBtn = tabButtons[i];
            if (tabBtn != null)
            {
                Image img = tabBtn.GetComponent<Image>();
                if (img != null)
                {
                    img.color = (i == index) ? new Color(0.28f, 0.42f, 0.32f, 1f) : new Color(0.18f, 0.18f, 0.20f, 0.9f);
                }
            }
        }
    }

    private void SetupHowToPlayModal()
    {
        if (howToPlayModal == null)
        {
            // Przygotuj modal przy starcie, aby był gotowy i ukryty
            BuildRuntimeHowToPlayModal();
        }

        if (howToPlayModal != null)
        {
            howToPlayModal.SetActive(false);
        }
    }

    private void BuildRuntimeHowToPlayModal()
    {
        Canvas canvas = GetComponentInParent<Canvas>() ?? FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        // Pobierz czcionkę z istniejącego przycisku
        TMP_FontAsset fontAsset = null;
        if (btnStart != null)
        {
            TextMeshProUGUI startTmp = btnStart.GetComponentInChildren<TextMeshProUGUI>();
            if (startTmp != null) fontAsset = startTmp.font;
        }

        // Główny kontener modalu (overlay)
        GameObject modalObj = new GameObject("Pnl_HowToPlayModal", typeof(RectTransform), typeof(Image));
        modalObj.transform.SetParent(canvas.transform, false);

        RectTransform modalRect = modalObj.GetComponent<RectTransform>();
        modalRect.anchorMin = Vector2.zero;
        modalRect.anchorMax = Vector2.one;
        modalRect.offsetMin = Vector2.zero;
        modalRect.offsetMax = Vector2.zero;

        Image overlayImg = modalObj.GetComponent<Image>();
        overlayImg.color = new Color(0.04f, 0.05f, 0.07f, 0.88f); // ciemne tło

        // Okno modalu
        GameObject windowObj = new GameObject("Window", typeof(RectTransform), typeof(Image));
        windowObj.transform.SetParent(modalObj.transform, false);

        RectTransform windowRect = windowObj.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(860f, 580f);

        Image windowImg = windowObj.GetComponent<Image>();
        windowImg.color = new Color(0.12f, 0.13f, 0.15f, 0.98f); // elegancki ciemny grafit

        // Pasek Nagłówka (Header)
        GameObject headerObj = new GameObject("Header", typeof(RectTransform));
        headerObj.transform.SetParent(windowObj.transform, false);
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -15f);
        headerRect.sizeDelta = new Vector2(-40f, 44f);

        GameObject titleTxtObj = new GameObject("Txt_Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleTxtObj.transform.SetParent(headerObj.transform, false);
        RectTransform titleRect = titleTxtObj.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = new Vector2(-50f, 0f);

        TextMeshProUGUI headerTitle = titleTxtObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) headerTitle.font = fontAsset;
        headerTitle.text = "JAK GRAĆ • PORADNIK BASTIONU";
        headerTitle.fontSize = 20f; // GEMINI.md: 20 pt nagłówek
        headerTitle.fontStyle = FontStyles.Bold;
        headerTitle.color = new Color(0.95f, 0.85f, 0.45f); // złoty akcent
        headerTitle.alignment = TextAlignmentOptions.MidlineLeft;

        // Przycisk Zamknij ("X")
        GameObject btnCloseObj = CreateSimpleButton(headerObj.transform, "Btn_Close", "✕", fontAsset, 16f, new Vector2(40f, 40f));
        RectTransform closeRect = btnCloseObj.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = Vector2.zero;
        btnCloseObj.GetComponent<Button>().onClick.AddListener(CloseHowToPlay);

        // Pasek Zakładek (4 karty)
        GameObject tabsContainer = new GameObject("TabsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        tabsContainer.transform.SetParent(windowObj.transform, false);
        RectTransform tabsRect = tabsContainer.GetComponent<RectTransform>();
        tabsRect.anchorMin = new Vector2(0f, 1f);
        tabsRect.anchorMax = new Vector2(1f, 1f);
        tabsRect.pivot = new Vector2(0.5f, 1f);
        tabsRect.anchoredPosition = new Vector2(0f, -68f);
        tabsRect.sizeDelta = new Vector2(-40f, 38f);

        HorizontalLayoutGroup tabsLayout = tabsContainer.GetComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 10f;
        tabsLayout.childForceExpandWidth = true;
        tabsLayout.childForceExpandHeight = true;

        tabButtons.Clear();
        for (int i = 0; i < cards.Count; i++)
        {
            int cardIdx = i;
            GameObject tabBtnObj = CreateSimpleButton(tabsContainer.transform, $"Tab_{i}", cards[i].tabTitle, fontAsset, 14f, new Vector2(180f, 36f));
            Button tabBtn = tabBtnObj.GetComponent<Button>();
            tabBtn.onClick.AddListener(() => ShowCard(cardIdx));
            tabButtons.Add(tabBtn);
        }

        // Obszar Treści Karty (Content Box)
        GameObject contentBox = new GameObject("ContentBox", typeof(RectTransform), typeof(Image));
        contentBox.transform.SetParent(windowObj.transform, false);
        RectTransform contentRect = contentBox.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.offsetMin = new Vector2(20f, 75f);
        contentRect.offsetMax = new Vector2(-20f, -118f);

        Image contentBoxImg = contentBox.GetComponent<Image>();
        contentBoxImg.color = new Color(0.08f, 0.09f, 0.10f, 0.95f);

        // Tytuł Karty
        GameObject cardTitleObj = new GameObject("Txt_CardTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        cardTitleObj.transform.SetParent(contentBox.transform, false);
        RectTransform cardTitleRect = cardTitleObj.GetComponent<RectTransform>();
        cardTitleRect.anchorMin = new Vector2(0f, 1f);
        cardTitleRect.anchorMax = new Vector2(1f, 1f);
        cardTitleRect.pivot = new Vector2(0f, 1f);
        cardTitleRect.anchoredPosition = new Vector2(20f, -16f);
        cardTitleRect.sizeDelta = new Vector2(-40f, 30f);

        modalCardTitle = cardTitleObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) modalCardTitle.font = fontAsset;
        modalCardTitle.fontSize = 16f; // GEMINI.md: 16 pt tytuł sekcji
        modalCardTitle.fontStyle = FontStyles.Bold;
        modalCardTitle.color = new Color(0.85f, 0.9f, 1f);

        // Treść Karty
        GameObject cardBodyObj = new GameObject("Txt_CardBody", typeof(RectTransform), typeof(TextMeshProUGUI));
        cardBodyObj.transform.SetParent(contentBox.transform, false);
        RectTransform cardBodyRect = cardBodyObj.GetComponent<RectTransform>();
        cardBodyRect.anchorMin = Vector2.zero;
        cardBodyRect.anchorMax = Vector2.one;
        cardBodyRect.offsetMin = new Vector2(20f, 15f);
        cardBodyRect.offsetMax = new Vector2(-20f, -50f);

        modalCardContent = cardBodyObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) modalCardContent.font = fontAsset;
        modalCardContent.fontSize = 13f; // GEMINI.md: 12-14 pt opisy
        modalCardContent.color = new Color(0.82f, 0.85f, 0.88f);
        modalCardContent.textWrappingMode = TextWrappingModes.Normal;
        modalCardContent.lineSpacing = 10f;

        // Pasek Dolny (Footer)
        GameObject footerObj = new GameObject("Footer", typeof(RectTransform));
        footerObj.transform.SetParent(windowObj.transform, false);
        RectTransform footerRect = footerObj.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = new Vector2(0f, 15f);
        footerRect.sizeDelta = new Vector2(-40f, 45f);

        // Wskaźnik strony
        GameObject pageIndObj = new GameObject("Txt_PageIndicator", typeof(RectTransform), typeof(TextMeshProUGUI));
        pageIndObj.transform.SetParent(footerObj.transform, false);
        RectTransform pageIndRect = pageIndObj.GetComponent<RectTransform>();
        pageIndRect.anchorMin = new Vector2(0f, 0.5f);
        pageIndRect.anchorMax = new Vector2(0.5f, 0.5f);
        pageIndRect.pivot = new Vector2(0f, 0.5f);
        pageIndRect.anchoredPosition = Vector2.zero;
        pageIndRect.sizeDelta = new Vector2(250f, 30f);

        modalPageIndicator = pageIndObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) modalPageIndicator.font = fontAsset;
        modalPageIndicator.fontSize = 10f; // GEMINI.md: 10 pt tekst pomocniczy
        modalPageIndicator.color = new Color(0.6f, 0.65f, 0.7f);
        modalPageIndicator.alignment = TextAlignmentOptions.MidlineLeft;

        // Przyciski Nawigacji (Poprzednia / Następna / Zamknij)
        GameObject navBtnContainer = new GameObject("NavButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        navBtnContainer.transform.SetParent(footerObj.transform, false);
        RectTransform navRect = navBtnContainer.GetComponent<RectTransform>();
        navRect.anchorMin = new Vector2(1f, 0.5f);
        navRect.anchorMax = new Vector2(1f, 0.5f);
        navRect.pivot = new Vector2(1f, 0.5f);
        navRect.anchoredPosition = Vector2.zero;
        navRect.sizeDelta = new Vector2(380f, 40f);

        HorizontalLayoutGroup navLayout = navBtnContainer.GetComponent<HorizontalLayoutGroup>();
        navLayout.spacing = 10f;
        navLayout.childAlignment = TextAnchor.MiddleRight;
        navLayout.childForceExpandWidth = true;
        navLayout.childForceExpandHeight = true;

        GameObject btnPrevObj = CreateSimpleButton(navBtnContainer.transform, "Btn_Prev", "< Poprzednia", fontAsset, 14f, new Vector2(110f, 36f));
        modalBtnPrev = btnPrevObj.GetComponent<Button>();
        modalBtnPrev.onClick.AddListener(PrevCard);

        GameObject btnNextObj = CreateSimpleButton(navBtnContainer.transform, "Btn_Next", "Następna >", fontAsset, 14f, new Vector2(110f, 36f));
        modalBtnNext = btnNextObj.GetComponent<Button>();
        modalBtnNext.onClick.AddListener(NextCard);

        GameObject btnCloseFooterObj = CreateSimpleButton(navBtnContainer.transform, "Btn_CloseFooter", "Zamknij", fontAsset, 14f, new Vector2(110f, 36f));
        btnCloseFooterObj.GetComponent<Button>().onClick.AddListener(CloseHowToPlay);

        howToPlayModal = modalObj;
    }

    private GameObject CreateSimpleButton(Transform parent, string name, string label, TMP_FontAsset fontAsset, float fontSize, Vector2 size)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.sizeDelta = size;

        Image img = btnObj.GetComponent<Image>();
        img.color = new Color(0.2f, 0.22f, 0.26f, 1f);

        Button btn = btnObj.GetComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = new Color(0.2f, 0.22f, 0.26f, 1f);
        colors.highlightedColor = new Color(0.28f, 0.32f, 0.38f, 1f);
        colors.pressedColor = new Color(0.14f, 0.16f, 0.20f, 1f);
        colors.disabledColor = new Color(0.12f, 0.14f, 0.16f, 0.5f);
        btn.colors = colors;

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        if (fontAsset != null) tmp.font = fontAsset;
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        return btnObj;
    }
}
