using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Trwały menedżer ekranu ładowania (DontDestroyOnLoad).
/// Zapewnia płynne przejścia (fade in/out), pasek postępu z procentami,
/// losowe wskazówki krasnoludzkie (zgodne z GEMINI.md) oraz asynchroniczne ładowanie scen.
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    private static LoadingScreenManager instance;
    public static LoadingScreenManager Instance
    {
        get
        {
            if (instance == null)
            {
                EnsureInitialized();
            }
            return instance;
        }
    }

    [Header("Komponenty UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Slider progressBar;
    [SerializeField] private Image progressFillImage;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI percentText;
    [SerializeField] private TextMeshProUGUI tipCategoryText;
    [SerializeField] private TextMeshProUGUI tipBodyText;
    [SerializeField] private TextMeshProUGUI hintFooterText;

    [Header("Konfiguracja")]
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private float minLoadingDuration = 1.0f;

    [Header("Wskazówki Krasnoludzkie")]
    [TextArea(2, 4)]
    [SerializeField] private string[] dwarvenTips = new string[]
    {
        "Rune Forge pozwala nasycać wieże potężnymi runami żywiołów – eksperymentuj z ich kombinacjami!",
        "Latarnie (Beacons) rozpraszają mgłę wojny i odsłaniają ukryte złoża surowców w górach.",
        "Krasnoludzcy inżynierowie potrzebują kwater (Housing) oraz zapasów, aby zachować pełną wydajność.",
        "Wieże obronne wzniesione na wyższym terenie (Hill/Mountain) zyskują przewagę w zasięgu widzenia i ostrzału.",
        "Nocne hordy atakują falami z mroku – zabezpiecz magazyny i strategiczne punkty przejścia.",
        "Ulepszanie wież w drzewku rozwoju odblokowuje silne efekty obszarowe, spowalniające i niszczące pancerz.",
        "Centrum Ekspedycji pozwala wysyłać krasnoludzkie wyprawy po pradawne relikwie i cenne złoto.",
        "Różne gatunki wrogów posiadają odmienne pancerze – dopasuj rodzaj wież do nacierających bestii.",
        "Ekspansja terytorialna wymaga ciągłego dopływu drewna, kamienia i złota – nie zaniedbuj kopalń."
    };

    private bool isLoading = false;
    public bool IsLoading => isLoading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void EnsureInitialized()
    {
        if (instance != null) return;

        // Sprawdź czy w projekcie istnieje prefab Resources/LoadingScreen
        GameObject prefab = Resources.Load<GameObject>("LoadingScreen");
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
            go.name = "[LoadingScreenManager]";
        }
        else
        {
            go = new GameObject("[LoadingScreenManager]");
        }

        instance = go.GetComponent<LoadingScreenManager>();
        if (instance == null)
        {
            instance = go.AddComponent<LoadingScreenManager>();
        }

        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeUIIfNeeded();
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    /// <summary>
    /// Główna metoda statyczna do wywołania przejścia do dowolnej sceny przez ekran ładowania.
    /// </summary>
    public static void LoadScene(string sceneName, string customStatus = null)
    {
        Instance.StartSceneTransition(sceneName, customStatus);
    }

    public void StartSceneTransition(string sceneName, string customStatus = null)
    {
        if (isLoading)
        {
            Debug.LogWarning($"[LoadingScreenManager] Przejście do sceny jest już w toku!");
            return;
        }

        StartCoroutine(TransitionRoutine(sceneName, customStatus));
    }

    private IEnumerator TransitionRoutine(string sceneName, string customStatus)
    {
        isLoading = true;
        Time.timeScale = 1f;

        InitializeUIIfNeeded();

        // 1. Wybierz losową wskazówkę
        if (tipBodyText != null && dwarvenTips != null && dwarvenTips.Length > 0)
        {
            int tipIdx = UnityEngine.Random.Range(0, dwarvenTips.Length);
            tipBodyText.text = LocalizationManager.Get($"loading_tip_{tipIdx}", dwarvenTips[tipIdx]);
        }

        // 2. Ustaw status początkowy
        string defaultStatus = sceneName.ToLower().Contains("menu")
            ? LocalizationManager.Get("loading_status_return", "Powrót do twierdzy...")
            : LocalizationManager.Get("loading_status_enter", "Wkraczanie do doliny...");
        string initialStatus = !string.IsNullOrEmpty(customStatus) ? customStatus : defaultStatus;

        UpdateStatusText(initialStatus);
        UpdateProgressUI(0f);
        if (tipCategoryText != null) tipCategoryText.text = LocalizationManager.Get("loading_tip_category", "WSKAZÓWKA KRASNOLUDÓW");
        if (hintFooterText != null) hintFooterText.text = LocalizationManager.Get("loading_hint_fullscreen", "Wciśnij ALT+ENTER, aby przełączyć tryb pełnoekranowy.");

        canvas.gameObject.SetActive(true);
        canvas.enabled = true;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;

        // 3. Płynny Fade In (DOTween z fallbackiem na unscaled Time)
        yield return StartCoroutine(FadeCanvasGroupRoutine(0f, 1f, fadeDuration));

        // 4. Jeśli prekompilacja shaderów jest w trakcie, dokończ ją
        if (ShaderPrewarmer.Instance != null && !ShaderPrewarmer.Instance.IsWarmedUp)
        {
            UpdateStatusText(LocalizationManager.Get("loading_status_shaders", "Rozpalanie kuźni... Podgrzewanie shaderów..."));
            while (!ShaderPrewarmer.Instance.IsWarmedUp)
            {
                float shaderProg = ShaderPrewarmer.Instance.Progress * 0.4f;
                UpdateProgressUI(shaderProg);
                yield return null;
            }
            UpdateStatusText(initialStatus);
        }

        // 5. Asynchroniczne ładowanie sceny
        AsyncOperation asyncOp = null;
        try
        {
            asyncOp = SceneManager.LoadSceneAsync(sceneName);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LoadingScreenManager] Błąd podczas wywołania LoadSceneAsync('{sceneName}'): {ex.Message}");
        }

        if (asyncOp == null)
        {
            Debug.LogError($"[LoadingScreenManager] Nie udało się zainicjować ładowania sceny: {sceneName}!");
            yield return StartCoroutine(FadeCanvasGroupRoutine(canvasGroup.alpha, 0f, fadeDuration));
            canvasGroup.blocksRaycasts = false;
            canvas.enabled = false;
            isLoading = false;
            yield break;
        }

        asyncOp.allowSceneActivation = false;

        float elapsedTime = 0f;
        float displayedProgress = 0f;

        // 6. Pętla ładowania: monitorowanie postępu i gwarancja minimalnego czasu wyświetlania
        while (asyncOp.progress < 0.9f || elapsedTime < minLoadingDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            // asyncOp.progress idzie od 0.0 do 0.9 podczas faktycznego ładowania
            float rawTarget = Mathf.Clamp01(asyncOp.progress / 0.9f);
            float timeTarget = Mathf.Clamp01(elapsedTime / minLoadingDuration);
            float targetProgress = Mathf.Min(rawTarget, timeTarget);

            displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, Time.unscaledDeltaTime * 2.5f);
            UpdateProgressUI(displayedProgress);

            yield return null;
        }

        // 7. Zwieńczenie do 100%
        while (displayedProgress < 1f)
        {
            displayedProgress = Mathf.MoveTowards(displayedProgress, 1f, Time.unscaledDeltaTime * 3f);
            UpdateProgressUI(displayedProgress);
            yield return null;
        }

        UpdateProgressUI(1f);
        yield return new WaitForSecondsRealtime(0.15f);

        // 8. Aktywacja nowej sceny
        asyncOp.allowSceneActivation = true;
        while (!asyncOp.isDone)
        {
            yield return null;
        }

        yield return null; // Odczekaj 1 klatkę na start skryptów nowej sceny

        // 9. Płynny Fade Out po załadowaniu
        yield return StartCoroutine(FadeCanvasGroupRoutine(1f, 0f, fadeDuration));

        canvasGroup.blocksRaycasts = false;
        canvas.enabled = false;
        canvas.gameObject.SetActive(false);
        isLoading = false;
    }

    private IEnumerator FadeCanvasGroupRoutine(float from, float to, float duration)
    {
        if (canvasGroup == null) yield break;

        float timer = 0f;
        canvasGroup.alpha = from;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, timer / duration);
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    private void UpdateProgressUI(float val)
    {
        val = Mathf.Clamp01(val);

        if (progressBar != null)
        {
            progressBar.value = val;
        }

        if (progressFillImage != null)
        {
            progressFillImage.fillAmount = val;
        }

        if (percentText != null)
        {
            percentText.text = $"{Mathf.RoundToInt(val * 100f)}%";
        }
    }

    private void UpdateStatusText(string text)
    {
        if (statusText != null)
        {
            statusText.text = text;
        }
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }
        return go;
    }

    /// <summary>
    /// Tworzy czysty, stylizowany proceduralny Canvas, jeśli w scenie/prefabie go nie ma.
    /// Wszystkie rozmiary tekstów i ikony są ściśle zgodne z GEMINI.md.
    /// </summary>
    private void InitializeUIIfNeeded()
    {
        if (canvas != null && canvasGroup != null) return;

        // Szukaj istniejącego w dzieciach
        canvas = GetComponentInChildren<Canvas>(true);
        if (canvas == null)
        {
            GameObject canvasObj = CreateUIObject("LoadingCanvas", transform);
            canvas = canvasObj.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster == null) raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup = canvas.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();

        // Próba wczytania stylizowanego fontu krasnoludzkiego
        TMP_FontAsset fontAsset = Resources.Load<TMP_FontAsset>("Fonts/PirataOne-Regular SDF");
        if (fontAsset == null)
        {
            fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        // Tło
        Transform existingBg = canvas.transform.Find("Background");
        GameObject bgObj = existingBg != null ? existingBg.gameObject : CreateUIObject("Background", canvas.transform);
        Image bgImg = bgObj.GetComponent<Image>() ?? bgObj.AddComponent<Image>();
        bgImg.color = new Color32(0x13, 0x14, 0x1A, 0xFF); // Głęboki krasnoludzki ciemny kamień
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        if (bgRect != null)
        {
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
        }

        // Kontener główny zawartości (dół ekranu)
        Transform existingBottom = canvas.transform.Find("BottomPanel");
        GameObject bottomObj = existingBottom != null ? existingBottom.gameObject : CreateUIObject("BottomPanel", canvas.transform);
        RectTransform bottomRect = bottomObj.GetComponent<RectTransform>();
        if (bottomRect != null)
        {
            bottomRect.anchorMin = new Vector2(0.5f, 0f);
            bottomRect.anchorMax = new Vector2(0.5f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.sizeDelta = new Vector2(1200f, 320f);
            bottomRect.anchoredPosition = new Vector2(0f, 50f);
        }

        // 1. Status Text (GEMINI.md: 20 pt – nagłówek / główna informacja)
        if (statusText == null)
        {
            GameObject statusObj = CreateUIObject("StatusText", bottomObj.transform);
            statusText = statusObj.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null) statusText.font = fontAsset;
            statusText.fontSize = 20f;
            statusText.fontStyle = FontStyles.Bold;
            statusText.color = new Color32(0xF0, 0xE6, 0xD2, 0xFF); // Krasnoludzki pergamin
            statusText.alignment = TextAlignmentOptions.BottomLeft;
            RectTransform stRect = statusObj.GetComponent<RectTransform>();
            if (stRect != null)
            {
                stRect.anchorMin = new Vector2(0f, 0.45f);
                stRect.anchorMax = new Vector2(0.8f, 0.45f);
                stRect.pivot = new Vector2(0f, 0f);
                stRect.sizeDelta = new Vector2(0f, 36f);
                stRect.anchoredPosition = new Vector2(0f, 0f);
            }
        }

        // 2. Percent Text (GEMINI.md: 16 pt)
        if (percentText == null)
        {
            GameObject pctObj = CreateUIObject("PercentText", bottomObj.transform);
            percentText = pctObj.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null) percentText.font = fontAsset;
            percentText.fontSize = 16f;
            percentText.fontStyle = FontStyles.Bold;
            percentText.color = new Color32(0xE5, 0xA8, 0x3B, 0xFF); // Krasnoludzkie złoto
            percentText.alignment = TextAlignmentOptions.BottomRight;
            RectTransform pctRect = pctObj.GetComponent<RectTransform>();
            if (pctRect != null)
            {
                pctRect.anchorMin = new Vector2(0.8f, 0.45f);
                pctRect.anchorMax = new Vector2(1f, 0.45f);
                pctRect.pivot = new Vector2(1f, 0f);
                pctRect.sizeDelta = new Vector2(0f, 36f);
                pctRect.anchoredPosition = new Vector2(0f, 0f);
            }
        }

        // 3. Pasek Postępu (Progress Bar)
        if (progressBar == null)
        {
            GameObject barObj = CreateUIObject("ProgressBar", bottomObj.transform);
            RectTransform barRect = barObj.GetComponent<RectTransform>();
            if (barRect != null)
            {
                barRect.anchorMin = new Vector2(0f, 0.35f);
                barRect.anchorMax = new Vector2(1f, 0.35f);
                barRect.pivot = new Vector2(0.5f, 0.5f);
                barRect.sizeDelta = new Vector2(0f, 18f);
                barRect.anchoredPosition = new Vector2(0f, 0f);
            }

            // Tło paska
            Image barBg = barObj.AddComponent<Image>();
            barBg.color = new Color32(0x22, 0x25, 0x30, 0xFF);

            // Fill container
            GameObject fillArea = CreateUIObject("FillArea", barObj.transform);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            if (fillAreaRect != null)
            {
                fillAreaRect.anchorMin = Vector2.zero;
                fillAreaRect.anchorMax = Vector2.one;
                fillAreaRect.offsetMin = new Vector2(2f, 2f);
                fillAreaRect.offsetMax = new Vector2(-2f, -2f);
            }

            GameObject fillObj = CreateUIObject("Fill", fillArea.transform);
            progressFillImage = fillObj.AddComponent<Image>();
            progressFillImage.color = new Color32(0xE5, 0xA8, 0x3B, 0xFF); // Ciepłe złoto
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            if (fillRect != null)
            {
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
            }

            progressBar = barObj.AddComponent<Slider>();
            progressBar.fillRect = fillRect;
            progressBar.targetGraphic = barBg;
            progressBar.direction = Slider.Direction.LeftToRight;
            progressBar.minValue = 0f;
            progressBar.maxValue = 1f;
            progressBar.value = 0f;
            progressBar.interactable = false;
        }

        // 4. Sekcja porad (GEMINI.md: 16 pt nagłówek sekcji, 13 pt opis)
        if (tipCategoryText == null)
        {
            GameObject tipCatObj = CreateUIObject("TipCategory", bottomObj.transform);
            tipCategoryText = tipCatObj.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null) tipCategoryText.font = fontAsset;
            tipCategoryText.fontSize = 16f; // GEMINI.md: 16 pt
            tipCategoryText.fontStyle = FontStyles.Bold;
            tipCategoryText.color = new Color32(0xE5, 0xA8, 0x3B, 0xFF);
            tipCategoryText.text = LocalizationManager.Get("loading_tip_category", "WSKAZÓWKA KRASNOLUDÓW");
            RectTransform catRect = tipCatObj.GetComponent<RectTransform>();
            if (catRect != null)
            {
                catRect.anchorMin = new Vector2(0f, 0.9f);
                catRect.anchorMax = new Vector2(1f, 0.9f);
                catRect.pivot = new Vector2(0f, 1f);
                catRect.sizeDelta = new Vector2(0f, 26f);
                catRect.anchoredPosition = new Vector2(0f, 0f);
            }
        }

        if (tipBodyText == null)
        {
            GameObject tipBodyObj = CreateUIObject("TipBody", bottomObj.transform);
            tipBodyText = tipBodyObj.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null) tipBodyText.font = fontAsset;
            tipBodyText.fontSize = 13f; // GEMINI.md: 12-14 pt opisy
            tipBodyText.color = new Color32(0xCE, 0xD0, 0xD8, 0xFF);
            tipBodyText.alignment = TextAlignmentOptions.TopLeft;
            tipBodyText.enableWordWrapping = true;
            RectTransform bodyRect = tipBodyObj.GetComponent<RectTransform>();
            if (bodyRect != null)
            {
                bodyRect.anchorMin = new Vector2(0f, 0.58f);
                bodyRect.anchorMax = new Vector2(1f, 0.85f);
                bodyRect.pivot = new Vector2(0f, 1f);
                bodyRect.offsetMin = Vector2.zero;
                bodyRect.offsetMax = Vector2.zero;
            }
        }

        // 5. Hint Footer (GEMINI.md: 10 pt bardzo mały tekst pomocniczy)
        if (hintFooterText == null)
        {
            GameObject hintObj = CreateUIObject("HintFooter", bottomObj.transform);
            hintFooterText = hintObj.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null) hintFooterText.font = fontAsset;
            hintFooterText.fontSize = 10f; // GEMINI.md: 10 pt
            hintFooterText.color = new Color32(0x80, 0x85, 0x95, 0xFF);
            hintFooterText.alignment = TextAlignmentOptions.Center;
            hintFooterText.text = LocalizationManager.Get("loading_hint_fullscreen", "Wciśnij ALT+ENTER, aby przełączyć tryb pełnoekranowy.");
            RectTransform hintRect = hintObj.GetComponent<RectTransform>();
            if (hintRect != null)
            {
                hintRect.anchorMin = new Vector2(0f, 0.05f);
                hintRect.anchorMax = new Vector2(1f, 0.2f);
                hintRect.pivot = new Vector2(0.5f, 0.5f);
                hintRect.offsetMin = Vector2.zero;
                hintRect.offsetMax = Vector2.zero;
            }
        }

        canvas.enabled = false;
        canvas.gameObject.SetActive(false);
    }
}
